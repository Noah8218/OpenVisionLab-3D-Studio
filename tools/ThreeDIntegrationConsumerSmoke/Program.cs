using System.Text;
using System.Text.Json;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.Integration;
using OpenVisionLab.ThreeD.Tools;

if (args.Length is not (4 or 5)
    || !string.Equals(args[0], "--consume-3d", StringComparison.OrdinalIgnoreCase)
    || args.Length == 5
        && !string.Equals(args[4], "--require-projection", StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine(
        "Usage: ThreeDIntegrationConsumerSmoke --consume-3d <exchangeRoot> <manifestPath> <evidencePath> [--require-projection]");
    return 2;
}

try
{
    var exchangeRoot = Path.GetFullPath(args[1]);
    var manifestPath = Path.GetFullPath(args[2]);
    var evidencePath = Path.GetFullPath(args[3]);
    var manifest = ReadManifest(manifestPath);
    var handoff = ThreeDIntegrationExchange.ReadHandoff(
        exchangeRoot,
        manifest.TransactionId);

    Require(
        handoff.MessageId == manifest.MessageId,
        "The manifest message identity does not match the persisted Handoff.");
    Require(
        handoff.Context.Modality == IntegrationInspectionModality.ThreeD,
        "The Handoff modality is not ThreeD.");
    Require(
        handoff.Context.InputKind == IntegrationInspectionInputKind.HeightMap,
        "The Handoff input kind is not HeightMap.");
    Require(
        ApplicationIdentitiesMatch(handoff.Context.ConsumerBuild, manifest.Consumer),
        "The Handoff consumer identity does not match the producer manifest.");
    Require(
        string.Equals(
            handoff.Context.InputSha256,
            manifest.SourceSha256,
            StringComparison.OrdinalIgnoreCase),
        "The manifest source hash does not match the Handoff.");
    Require(
        string.Equals(
            handoff.Context.RecipeSha256,
            manifest.RecipeSha256,
            StringComparison.OrdinalIgnoreCase),
        "The manifest recipe hash does not match the Handoff.");

    var sourceArtifact = handoff.Context.Artifacts.Single(artifact =>
        string.Equals(
            artifact.Role,
            IntegrationArtifactRoles.InspectionSource,
            StringComparison.Ordinal));
    var recipeArtifact = handoff.Context.Artifacts.Single(artifact =>
        string.Equals(
            artifact.Role,
            IntegrationArtifactRoles.InspectionRecipe,
            StringComparison.Ordinal));
    var transactionDirectory = Path.Combine(
        exchangeRoot,
        IntegrationTransactionLayout.TransactionsDirectoryName,
        manifest.TransactionId.ToString("D"));
    var recipePath = ResolveTransactionArtifactPath(transactionDirectory, recipeArtifact);
    var recipe = C3DWarpageRecipe.Load(recipePath);

    var acknowledgement = ThreeDIntegrationExchange.PublishAcknowledgement(
        exchangeRoot,
        handoff,
        manifest.Consumer);
    Require(
        acknowledgement.Status == IntegrationAcknowledgementStatus.Accepted,
        "The 3D Handoff was not accepted by the consumer.");

    var result = ThreeDIntegrationHeightMapRunner.RunAcceptedHandoffFromRecipe(
        exchangeRoot,
        manifest.TransactionId,
        manifest.Consumer);
    var persistedResult = ThreeDIntegrationExchange.ReadResult(
        exchangeRoot,
        manifest.TransactionId);

    Require(
        persistedResult.Status == IntegrationResultStatus.Completed,
        "The persisted 3D Result is not completed.");
    Require(
        persistedResult.Outcome is IntegrationInspectionOutcome.Pass
            or IntegrationInspectionOutcome.Ng,
        "The 3D Result did not produce a Pass or Ng decision.");
    Require(
        persistedResult.Outcome == result.Outcome,
        "The persisted 3D Result outcome does not match the in-process result.");
    Require(
        persistedResult.Correlation.Modality == IntegrationInspectionModality.ThreeD
            && persistedResult.Correlation.InputKind == IntegrationInspectionInputKind.HeightMap,
        "The persisted Result correlation is not ThreeD/HeightMap.");

    var projectionArtifact = persistedResult.Evidence.SingleOrDefault(artifact =>
        string.Equals(
            artifact.Role,
            ThreeDCoordinateProjectionContract.ResultEvidenceRole,
            StringComparison.Ordinal));
    ThreeDCoordinateProjectionResult? projection = null;
    if (projectionArtifact is not null)
    {
        projection = ThreeDCoordinateProjectionContract.ReadResult(
            ResolveTransactionArtifactPath(transactionDirectory, projectionArtifact));
        Require(
            projection.ThreeDTransactionId == manifest.TransactionId.ToString("D")
                && projection.ThreeDRunId == persistedResult.RunId
                && projection.TwoDToThreeD.Count > 0
                && projection.ThreeDToTwoD.Count > 0,
            "The coordinate projection evidence is not correlated or does not contain both directions.");
    }
    if (args.Length == 5 && projection is null)
    {
        throw new InvalidDataException(
            "The requested coordinate projection evidence was not published.");
    }

    var sourcePath = ResolveTransactionArtifactPath(transactionDirectory, sourceArtifact);
    var snapshot = C3DHeightFieldSnapshot.LoadIdentified(
        sourcePath,
        sourceArtifact.ArtifactId,
        handoff.Context.Unit,
        handoff.Context.FrameId);
    Require(
        snapshot.ByteLength == sourceArtifact.ByteLength
            && string.Equals(
                snapshot.ContentSha256,
                sourceArtifact.Sha256,
                StringComparison.OrdinalIgnoreCase),
        "The consumer raw C3D snapshot identity does not match the Handoff artifact.");
    Require(
        snapshot.Values.Length == checked(snapshot.Width * snapshot.Height),
        "The consumer raw C3D buffer length does not match its grid dimensions.");

    var evidenceDirectory = Path.GetDirectoryName(evidencePath);
    if (string.IsNullOrWhiteSpace(evidenceDirectory))
    {
        throw new InvalidOperationException("The evidence path must include a directory.");
    }

    Directory.CreateDirectory(evidenceDirectory);
    var evidence = new StringBuilder()
        .AppendLine("OpenVisionLab 3D cross-process HeightMap integration smoke")
        .AppendLine($"consumerProcessId={Environment.ProcessId}")
        .AppendLine($"transactionId={manifest.TransactionId:D}")
        .AppendLine($"messageId={manifest.MessageId:D}")
        .AppendLine($"acknowledgement={acknowledgement.Status}")
        .AppendLine($"resultStatus={persistedResult.Status}")
        .AppendLine($"outcome={persistedResult.Outcome}")
        .AppendLine($"runId={persistedResult.RunId}")
        .AppendLine($"modality={persistedResult.Correlation.Modality}")
        .AppendLine($"inputKind={persistedResult.Correlation.InputKind}")
        .AppendLine($"sourceArtifactBytes={snapshot.ByteLength}")
        .AppendLine($"sourceArtifactSha256={snapshot.ContentSha256}")
        .AppendLine($"sourceGrid={snapshot.Width}x{snapshot.Height}")
        .AppendLine($"sourceValidSamples={snapshot.ValidCount}")
        .AppendLine($"sourceMissingSamples={snapshot.MissingCount}")
        .AppendLine($"sourceMinimum={snapshot.Minimum:R}")
        .AppendLine($"sourceMaximum={snapshot.Maximum:R}")
        .AppendLine("rawHeightBufferMaterialized=True")
        .AppendLine($"recipeType={recipe.RecipeType}")
        .AppendLine($"recipeVersion={recipe.Version}")
        .AppendLine($"inspectionUnit={recipe.Step.Unit}")
        .AppendLine($"inspectionFrame={recipe.Step.FrameId}")
        .AppendLine($"inspectionMaximumPeakToValley={recipe.Step.Acceptance.MaximumPeakToValley:R}")
        .AppendLine($"inspectionRoi=row:{recipe.Step.Roi.Row},column:{recipe.Step.Roi.Column},rows:{recipe.Step.Roi.RowCount},columns:{recipe.Step.Roi.ColumnCount}")
        .AppendLine($"inspectionMinimumValidSamples={recipe.Step.MinimumValidSamples}")
        .AppendLine($"projectionEvidence={(projection is not null)}")
        .AppendLine($"projectionEvidenceRelativePath={projectionArtifact?.RelativePath ?? "none"}")
        .AppendLine($"projection2DTo3DPoints={projection?.TwoDToThreeD.Count ?? 0}")
        .AppendLine($"projection3DTo2DPoints={projection?.ThreeDToTwoD.Count ?? 0}")
        .AppendLine($"projectionId={projection?.ProjectionId ?? "none"}")
        .AppendLine($"producer={manifest.Producer.ApplicationId}/{manifest.Producer.ApplicationVersion}/{manifest.Producer.SourceCommit}")
        .AppendLine($"consumer={manifest.Consumer.ApplicationId}/{manifest.Consumer.ApplicationVersion}/{manifest.Consumer.SourceCommit}")
        .AppendLine($"runRecordRelativePath={persistedResult.RunRecord?.RelativePath ?? "none"}");

    foreach (var metric in persistedResult.Metrics)
    {
        evidence.AppendLine($"metric.{metric.Name}={metric.Value:R}{(string.IsNullOrWhiteSpace(metric.Unit) ? string.Empty : $" {metric.Unit}")}");
    }

    File.WriteAllText(evidencePath, evidence.ToString(), new UTF8Encoding(false));
    Console.WriteLine($"3D consumer completed ThreeD/HeightMap Handoff: {manifest.TransactionId}");
    Console.WriteLine($"ConsumerProcessId={Environment.ProcessId}");
    Console.WriteLine($"Outcome={persistedResult.Outcome}; RunId={persistedResult.RunId}");
    Console.WriteLine($"Evidence={evidencePath}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"3D consumer smoke failed: {exception.Message}");
    return 1;
}

static MachineThreeDProducerManifest ReadManifest(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException("The producer manifest was not found.", path);
    }

    var manifest = JsonSerializer.Deserialize<MachineThreeDProducerManifest>(
        File.ReadAllText(path),
        new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    if (manifest is null || manifest.SchemaVersion != "1.0")
    {
        throw new InvalidDataException("The producer manifest is missing or unsupported.");
    }

    return manifest;
}

static bool ApplicationIdentitiesMatch(
    IntegrationApplicationIdentity actual,
    IntegrationApplicationIdentity expected) =>
    string.Equals(actual.ApplicationId, expected.ApplicationId, StringComparison.Ordinal)
    && string.Equals(actual.ApplicationVersion, expected.ApplicationVersion, StringComparison.Ordinal)
    && string.Equals(actual.SourceCommit, expected.SourceCommit, StringComparison.OrdinalIgnoreCase)
    && actual.SourceState == expected.SourceState;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidDataException(message);
    }
}

static string ResolveTransactionArtifactPath(
    string transactionDirectory,
    IntegrationArtifactReference artifact)
{
    var root = Path.GetFullPath(transactionDirectory);
    var path = Path.GetFullPath(Path.Combine(
        root,
        artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
    var rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar)
                     + Path.DirectorySeparatorChar;
    if (!path.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException(
            $"Artifact path escapes the transaction directory: {artifact.RelativePath}");
    }

    return path;
}

internal sealed record MachineThreeDProducerManifest(
    string SchemaVersion,
    Guid TransactionId,
    Guid MessageId,
    DateTimeOffset CreatedAtUtc,
    IntegrationApplicationIdentity Producer,
    IntegrationApplicationIdentity Consumer,
    string SourceSha256,
    string RecipeSha256);

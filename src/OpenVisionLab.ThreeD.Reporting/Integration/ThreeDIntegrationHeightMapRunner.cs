using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

public sealed record ThreeDHeightMapInspectionRequest(
    double MaximumPeakToValley,
    double? MaximumRms = null,
    VisionSdkGridRoi? Roi = null,
    int MinimumValidSamples = 3,
    double MinimumValidCoverageRatio = 0.0);

/// <summary>
/// Explicit 3D consumer path for a v2 Handoff whose source is a C3D raw-height
/// artifact. The raw values are materialized into an immutable snapshot and
/// passed to the existing Vision SDK height-map inspection boundary. No image
/// projection is used as a substitute for the height buffer.
/// </summary>
public static class ThreeDIntegrationHeightMapRunner
{
    public static IntegrationResultV2 RunAcceptedHandoffFromRecipe(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity consumerBuild) =>
        RunAcceptedHandoffFromRecipe(
            exchangeRoot,
            transactionId,
            consumerBuild,
            CancellationToken.None);

    public static IntegrationResultV2 RunAcceptedHandoffFromRecipe(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity consumerBuild,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
        ArgumentNullException.ThrowIfNull(consumerBuild);
        cancellationToken.ThrowIfCancellationRequested();

        var handoff = ThreeDIntegrationExchange.ReadHandoff(
            exchangeRoot,
            transactionId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureThreeDHeightMapHandoff(handoff, consumerBuild);

        string transactionDirectory = GetTransactionDirectory(
            exchangeRoot,
            transactionId);
        var recipeArtifact = RequireArtifact(
            handoff,
            IntegrationArtifactRoles.InspectionRecipe,
            handoff.Context.RecipeSha256);
        string recipePath = ResolveArtifactPath(transactionDirectory, recipeArtifact);
        VerifyArtifactIdentity(recipePath, recipeArtifact);
        cancellationToken.ThrowIfCancellationRequested();

        var recipe = C3DWarpageRecipe.Load(recipePath);
        cancellationToken.ThrowIfCancellationRequested();
        if (!recipe.Step.Enabled)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "The C3D Warpage recipe step is disabled.");
        }

        if (!string.Equals(recipe.Source.Unit, recipe.Step.Unit, StringComparison.Ordinal)
            || !string.Equals(recipe.Step.Unit, handoff.Context.Unit, StringComparison.Ordinal)
            || !string.Equals(recipe.Step.FrameId, handoff.Context.FrameId, StringComparison.Ordinal))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "The C3D Warpage recipe source/step unit and frame must match the Handoff context.");
        }

        return RunAcceptedHandoff(
            exchangeRoot,
            transactionId,
            consumerBuild,
            new ThreeDHeightMapInspectionRequest(
                recipe.Step.Acceptance.MaximumPeakToValley,
                recipe.Step.Acceptance.MaximumRms,
                new VisionSdkGridRoi(
                    recipe.Step.Roi.Row,
                    recipe.Step.Roi.Column,
                    recipe.Step.Roi.RowCount,
                    recipe.Step.Roi.ColumnCount),
                recipe.Step.MinimumValidSamples),
            cancellationToken);
    }

    public static IntegrationResultV2 RunAcceptedHandoff(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity consumerBuild,
        ThreeDHeightMapInspectionRequest request) =>
        RunAcceptedHandoff(
            exchangeRoot,
            transactionId,
            consumerBuild,
            request,
            CancellationToken.None);

    public static IntegrationResultV2 RunAcceptedHandoff(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity consumerBuild,
        ThreeDHeightMapInspectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
        ArgumentNullException.ThrowIfNull(consumerBuild);
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var handoff = ThreeDIntegrationExchange.ReadHandoff(
            exchangeRoot,
            transactionId);
        cancellationToken.ThrowIfCancellationRequested();
        EnsureThreeDHeightMapHandoff(handoff, consumerBuild);
        var acknowledgement = ThreeDIntegrationExchange.ReadAcknowledgement(
            exchangeRoot,
            transactionId);
        cancellationToken.ThrowIfCancellationRequested();
        if (acknowledgement.Status != IntegrationAcknowledgementStatus.Accepted)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "A completed 3D inspection requires an accepted Acknowledgement.");
        }

        string transactionDirectory = GetTransactionDirectory(
            exchangeRoot,
            transactionId);
        if (File.Exists(Path.Combine(
                transactionDirectory,
                IntegrationTransactionLayout.ResultFileName)))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "The Handoff already has a Result.");
        }

        var sourceArtifact = RequireArtifact(
            handoff,
            IntegrationArtifactRoles.InspectionSource,
            handoff.Context.InputSha256);
        var recipeArtifact = RequireArtifact(
            handoff,
            IntegrationArtifactRoles.InspectionRecipe,
            handoff.Context.RecipeSha256);
        string sourcePath = ResolveArtifactPath(transactionDirectory, sourceArtifact);

        var snapshot = C3DHeightFieldSnapshot.LoadIdentified(
            sourcePath,
            sourceArtifact.ArtifactId,
            handoff.Context.Unit,
            handoff.Context.FrameId);
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.ByteLength != sourceArtifact.ByteLength
            || !string.Equals(
                snapshot.ContentSha256,
                sourceArtifact.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.ArtifactHashMismatch,
                "Materialized C3D identity does not match the declared Handoff artifact.");
        }

        var source = new VisionSdkHeightMapInput(
            snapshot.EntityId,
            snapshot.Height,
            snapshot.Width,
            snapshot.GridOriginColumn,
            snapshot.GridOriginRow,
            1.0,
            1.0,
            snapshot.Values.ToArray(),
            handoff.Context.Unit,
            handoff.Context.FrameId)
        {
            PlanarUnit = "grid-index",
            HeightUnit = handoff.Context.Unit,
            ExpectedContract = new VisionSdkHeightMapContract(
                "grid-index",
                handoff.Context.Unit,
                handoff.Context.FrameId)
        };
        cancellationToken.ThrowIfCancellationRequested();
        var evaluation = VisionSdkHeightMapInspection.EvaluateWarpage(
            new VisionSdkWarpageInspectionInput(
                source,
                request.Roi,
                request.MaximumPeakToValley,
                request.MaximumRms,
                request.MinimumValidSamples,
                request.MinimumValidCoverageRatio));
        cancellationToken.ThrowIfCancellationRequested();
        if (evaluation.Result.Status is not (ResultStatus.Pass or ResultStatus.Fail))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.ExecutionFailed,
                $"3D height-map inspection did not produce Pass/Fail: {evaluation.Result.Message}");
        }

        string runId = CreateRunId(handoff, request);
        string runRecordPath = Path.Combine(
            transactionDirectory,
            $".3d-run-record-input-{Guid.NewGuid():N}.json");
        string? projectionResultPath = null;
        var runRecord = CreateRunRecord(
            runId,
            handoff,
            sourceArtifact,
            recipeArtifact,
            evaluation);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            InspectionRunRecordJson.Write(runRecordPath, runRecord);
            cancellationToken.ThrowIfCancellationRequested();
            var projectionResult = CreateProjectionResult(
                exchangeRoot,
                transactionDirectory,
                handoff,
                snapshot,
                request.Roi,
                evaluation,
                runId,
                cancellationToken);
            if (projectionResult is not null)
            {
                projectionResultPath = WriteProjectionResult(
                    transactionDirectory,
                    projectionResult);
            }

            return ThreeDIntegrationExchange.PublishCompletedResult(
                exchangeRoot,
                transactionId,
                consumerBuild,
                runRecordPath,
                projectionResultPath is null
                    ? null
                    : [new ThreeDIntegrationEvidenceArtifact(
                        ThreeDCoordinateProjectionContract.ResultEvidenceRole,
                        ThreeDCoordinateProjectionContract.ResultEvidenceArtifactId,
                        projectionResultPath,
                        "artifacts/coordinate-projection-result.json")]);
        }
        finally
        {
            TryDeleteFile(runRecordPath);
            TryDeleteFile(projectionResultPath);
        }
    }

    private static InspectionRunRecord CreateRunRecord(
        string runId,
        IntegrationHandoffV2 handoff,
        IntegrationArtifactReference sourceArtifact,
        IntegrationArtifactReference recipeArtifact,
        VisionSdkInspectionEvaluation evaluation)
    {
        var context = handoff.Context;
        var toolResult = evaluation.Result;
        var integrationContext = new InspectionRunIntegrationContext(
            context.ProjectId,
            context.ProjectSchema,
            context.SequenceId,
            context.StepId,
            context.CameraId,
            context.AcquisitionId,
            context.FrameId,
            context.Unit,
            context.Modality.ToString(),
            context.InputKind.ToString(),
            context.ConsumerBuild.ApplicationId,
            context.ConsumerBuild.ApplicationVersion,
            context.ConsumerBuild.SourceCommit,
            context.ConsumerBuild.SourceState.ToString());
        var source = new InspectionRunSource(
            sourceArtifact.ArtifactId,
            sourceArtifact.RelativePath,
            sourceArtifact.Sha256,
            sourceArtifact.ByteLength,
            context.Unit);

        return new InspectionRunRecord(
            "1.9",
            runId,
            DateTimeOffset.UtcNow,
            new InspectionRunRecipe(
                "integration-heightmap-warpage",
                "1.0",
                recipeArtifact.RelativePath,
                recipeArtifact.Sha256),
            source,
            toolResult.ToolName,
            toolResult.Status,
            toolResult.Message,
            toolResult.Elapsed.TotalMilliseconds,
            toolResult.Metrics
                .Where(metric => double.IsFinite(metric.Value))
                .Select(metric => new InspectionRunMetric(
                    metric.Name,
                    metric.Kind,
                    metric.Value,
                    metric.Unit,
                    metric.Status))
                .ToArray(),
            toolResult.Overlays
                .Select(overlay => new InspectionRunOverlay(
                    overlay.Id,
                    overlay.Kind,
                    overlay.Label,
                    overlay.Status,
                    overlay.SourceEntityId))
                .ToArray(),
            "integration-adapter",
            new InspectionRunArtifacts(
                "3D height-map integration inspection",
                null,
                null,
                runId,
                null,
                null))
        {
            Step = new InspectionRunStep(
                context.StepId,
                sourceArtifact.ArtifactId,
                [],
                []),
            IntegrationContext = integrationContext
        };
    }

    private static ThreeDCoordinateProjectionResult? CreateProjectionResult(
        string exchangeRoot,
        string transactionDirectory,
        IntegrationHandoffV2 handoff,
        C3DHeightFieldSnapshot snapshot,
        VisionSdkGridRoi? roi,
        VisionSdkInspectionEvaluation evaluation,
        string threeDRunId,
        CancellationToken cancellationToken)
    {
        var profileArtifact = handoff.Context.Artifacts.SingleOrDefault(artifact =>
            string.Equals(
                artifact.Role,
                ThreeDCoordinateProjectionContract.ProfileArtifactRole,
                StringComparison.Ordinal));
        if (profileArtifact is null)
        {
            return null;
        }

        var profilePath = ResolveArtifactPath(transactionDirectory, profileArtifact);
        VerifyArtifactIdentity(profilePath, profileArtifact);
        var profile = ThreeDCoordinateProjectionContract.ReadProfile(profilePath);
        if (string.IsNullOrWhiteSpace(profile.TwoDTransactionId)
            || !Guid.TryParse(profile.TwoDTransactionId, out var twoDTransactionId)
            || twoDTransactionId == Guid.Empty)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "A projection-enabled ThreeD Handoff must identify its completed TwoD transaction.");
        }

        if (!string.Equals(profile.Grid.Unit, handoff.Context.Unit, StringComparison.Ordinal)
            || !string.Equals(profile.Grid.FrameId, handoff.Context.FrameId, StringComparison.Ordinal))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "The projection grid contract does not match the ThreeD Handoff unit or frame.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var twoDHandoff = ThreeDIntegrationExchange.ReadHandoff(
            exchangeRoot,
            twoDTransactionId);
        if (twoDHandoff.Context.Modality != IntegrationInspectionModality.TwoD
            || twoDHandoff.Context.InputKind != IntegrationInspectionInputKind.Image)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "The projection profile does not point to a TwoD/Image Handoff.");
        }

        var twoDProfileArtifact = twoDHandoff.Context.Artifacts.SingleOrDefault(artifact =>
            string.Equals(
                artifact.Role,
                ThreeDCoordinateProjectionContract.ProfileArtifactRole,
                StringComparison.Ordinal));
        if (twoDProfileArtifact is null)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidArtifact,
                "The paired TwoD Handoff does not contain a coordinate projection profile.");
        }

        var twoDTransactionDirectory = GetTransactionDirectory(
            exchangeRoot,
            twoDTransactionId);
        var twoDProfile = ThreeDCoordinateProjectionContract.ReadProfile(
            ResolveArtifactPath(twoDTransactionDirectory, twoDProfileArtifact));
        if (!ThreeDCoordinateProjectionContract.ProfilesMatch(profile, twoDProfile))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "The paired TwoD and ThreeD projection profiles do not match.");
        }

        var twoDResult = ThreeDIntegrationExchange.ReadResult(
            exchangeRoot,
            twoDTransactionId);
        if (twoDResult.Status != IntegrationResultStatus.Completed
            || twoDResult.RunRecord is null
            || !string.Equals(
                twoDResult.Correlation.Modality.ToString(),
                IntegrationInspectionModality.TwoD.ToString(),
                StringComparison.Ordinal)
            || !string.Equals(
                twoDResult.Correlation.InputKind.ToString(),
                IntegrationInspectionInputKind.Image.ToString(),
                StringComparison.Ordinal))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "The paired TwoD transaction does not contain a completed Image Result.");
        }

        var twoDRunRecord = ThreeDCoordinateProjectionContract.ReadTwoDRunRecord(
            ResolveArtifactPath(twoDTransactionDirectory, twoDResult.RunRecord));
        if (!string.Equals(twoDRunRecord.RunId, twoDResult.RunId, StringComparison.Ordinal)
            || twoDRunRecord.SourceImageWidth != profile.Image.Width
            || twoDRunRecord.SourceImageHeight != profile.Image.Height)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "The paired TwoD Run Record does not match the projection profile identity or dimensions.");
        }

        var twoDPoints = new List<ThreeDProjectedCoordinate>();
        foreach (var step in twoDRunRecord.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var overlays = step.Overlays ?? [];
            for (var overlayIndex = 0; overlayIndex < overlays.Count; overlayIndex++)
            {
                var overlay = overlays[overlayIndex];
                var points = overlay.Points is { Count: > 0 }
                    ? overlay.Points
                    : [new TwoDIntegrationOverlayPointDocument(overlay.CenterX, overlay.CenterY)];
                for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
                {
                    var imagePoint = points[pointIndex];
                    var gridPoint = ThreeDCoordinateProjectionContract.MapImageToGrid(
                        profile,
                        imagePoint.X,
                        imagePoint.Y,
                        snapshot.Width,
                        snapshot.Height);
                    var sampled = SampleHeight(snapshot, gridPoint.X, gridPoint.Y);
                    twoDPoints.Add(new(
                        "2D->3D",
                        $"2d-{step.Index}-{overlayIndex}-{pointIndex}",
                        overlay.Kind,
                        overlay.Label,
                        imagePoint.X,
                        imagePoint.Y,
                        gridPoint.X,
                        gridPoint.Y,
                        sampled.Value,
                        sampled.Status,
                        step.Status));
                }
            }
        }

        if (twoDPoints.Count == 0)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.ExecutionFailed,
                "The paired TwoD Run Record contains no runtime overlay geometry to project.");
        }

        var threeDPoints = new List<ThreeDProjectedCoordinate>();
        if (roi is { } inspectionRoi)
        {
            var lastRow = checked(inspectionRoi.Row + inspectionRoi.RowCount - 1);
            var lastColumn = checked(inspectionRoi.Column + inspectionRoi.ColumnCount - 1);
            var gridPoints = new[]
            {
                (X: (double)inspectionRoi.Column, Y: (double)inspectionRoi.Row),
                (X: (double)lastColumn, Y: (double)inspectionRoi.Row),
                (X: (double)lastColumn, Y: (double)lastRow),
                (X: (double)inspectionRoi.Column, Y: (double)lastRow),
                (X: (inspectionRoi.Column + lastColumn) / 2.0,
                    Y: (inspectionRoi.Row + lastRow) / 2.0)
            };
            for (var pointIndex = 0; pointIndex < gridPoints.Length; pointIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var gridPoint = gridPoints[pointIndex];
                var imagePoint = ThreeDCoordinateProjectionContract.MapGridToImage(
                    profile,
                    gridPoint.X,
                    gridPoint.Y,
                    snapshot.Width,
                    snapshot.Height);
                var sampled = SampleHeight(snapshot, gridPoint.X, gridPoint.Y);
                var imageStatus = imagePoint.X >= 0.0
                    && imagePoint.X <= profile.Image.Width - 1
                    && imagePoint.Y >= 0.0
                    && imagePoint.Y <= profile.Image.Height - 1
                    ? sampled.Status
                    : "OutsideImage";
                threeDPoints.Add(new(
                    "3D->2D",
                    $"3d-roi-{pointIndex}",
                    "warpage-roi",
                    "C3D inspection ROI",
                    imagePoint.X,
                    imagePoint.Y,
                    gridPoint.X,
                    gridPoint.Y,
                    sampled.Value,
                    imageStatus,
                    evaluation.Result.Status.ToString()));
            }
        }

        return new(
            ThreeDCoordinateProjectionContract.SchemaVersion,
            profile.ProjectionId,
            twoDTransactionId.ToString("D"),
            handoff.TransactionId.ToString("D"),
            evaluation.Result.Status.ToString(),
            twoDRunRecord.RunId,
            threeDRunId,
            profile.Image.Width,
            profile.Image.Height,
            snapshot.Width,
            snapshot.Height,
            twoDPoints,
            threeDPoints,
            DateTimeOffset.UtcNow);
    }

    private static string WriteProjectionResult(
        string transactionDirectory,
        ThreeDCoordinateProjectionResult result)
    {
        var path = Path.Combine(
            transactionDirectory,
            $".coordinate-projection-result-input-{Guid.NewGuid():N}.json");
        File.WriteAllText(
            path,
            ThreeDCoordinateProjectionContract.SerializeResult(result));
        return path;
    }

    private static (double? Value, string Status) SampleHeight(
        C3DHeightFieldSnapshot snapshot,
        double gridX,
        double gridY)
    {
        if (gridX < 0.0
            || gridX > snapshot.Width - 1
            || gridY < 0.0
            || gridY > snapshot.Height - 1)
        {
            return (null, "OutsideGrid");
        }

        var column = (int)Math.Round(gridX, MidpointRounding.AwayFromZero);
        var row = (int)Math.Round(gridY, MidpointRounding.AwayFromZero);
        if (column < 0 || column >= snapshot.Width || row < 0 || row >= snapshot.Height)
        {
            return (null, "OutsideGrid");
        }

        var value = snapshot.Values.Span[checked(row * snapshot.Width + column)];
        return double.IsFinite(value) ? (value, "Valid") : (null, "Missing");
    }

    private static void EnsureThreeDHeightMapHandoff(
        IntegrationHandoffV2 handoff,
        IntegrationApplicationIdentity consumerBuild)
    {
        if (handoff.Context.Modality != IntegrationInspectionModality.ThreeD
            || handoff.Context.InputKind != IntegrationInspectionInputKind.HeightMap
            || !string.Equals(
                handoff.Context.ConsumerBuild.ApplicationId,
                IntegrationApplicationIds.ThreeDStudio,
                StringComparison.Ordinal)
            || !ApplicationIdentitiesMatch(
                handoff.Context.ConsumerBuild,
                consumerBuild))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.RequestRejected,
                "The Handoff is not a ThreeDStudio HeightMap request for the supplied consumer build.");
        }
    }

    private static IntegrationArtifactReference RequireArtifact(
        IntegrationHandoffV2 handoff,
        string role,
        string expectedHash)
    {
        var matches = handoff.Context.Artifacts
            .Where(artifact => string.Equals(
                artifact.Role,
                role,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidArtifact,
                $"A 3D Handoff requires exactly one '{role}' artifact.");
        }

        var artifact = matches[0];
        if (!string.Equals(
                artifact.Sha256,
                expectedHash,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                $"The '{role}' artifact hash does not match the inspection context.");
        }

        return artifact;
    }

    private static void ValidateRequest(ThreeDHeightMapInspectionRequest request)
    {
        if (!double.IsFinite(request.MaximumPeakToValley)
            || request.MaximumPeakToValley < 0.0
            || (request.MaximumRms is not null
                && (!double.IsFinite(request.MaximumRms.Value)
                    || request.MaximumRms.Value < 0.0))
            || request.MinimumValidSamples <= 0
            || !double.IsFinite(request.MinimumValidCoverageRatio)
            || request.MinimumValidCoverageRatio < 0.0
            || request.MinimumValidCoverageRatio > 1.0)
        {
            throw new ArgumentException(
                "3D height-map inspection limits and coverage must be finite and within their supported ranges.",
                nameof(request));
        }
    }

    private static string CreateRunId(
        IntegrationHandoffV2 handoff,
        ThreeDHeightMapInspectionRequest request)
    {
        var bytes = Encoding.UTF8.GetBytes(
            $"{handoff.TransactionId:D}|{handoff.Context.InputSha256}|{handoff.Context.RecipeSha256}|{request.MaximumPeakToValley:R}|{request.MaximumRms:R}");
        return $"3d-{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}";
    }

    private static string GetTransactionDirectory(
        string exchangeRoot,
        Guid transactionId) =>
        Path.Combine(
            Path.GetFullPath(exchangeRoot),
            IntegrationTransactionLayout.TransactionsDirectoryName,
            transactionId.ToString("D"));

    private static string ResolveArtifactPath(
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
            throw new IntegrationContractException(
                IntegrationErrorCode.UnsafeArtifactPath,
                "Artifact path escapes the transaction directory.");
        }

        return path;
    }

    private static void VerifyArtifactIdentity(
        string path,
        IntegrationArtifactReference artifact)
    {
        if (!File.Exists(path))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidArtifact,
                $"The declared artifact is missing: {artifact.RelativePath}");
        }

        var fileInfo = new FileInfo(path);
        using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        if (fileInfo.Length != artifact.ByteLength
            || !string.Equals(hash, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.ArtifactHashMismatch,
                $"The declared artifact identity does not match its content: {artifact.RelativePath}");
        }
    }

    private static bool ApplicationIdentitiesMatch(
        IntegrationApplicationIdentity actual,
        IntegrationApplicationIdentity expected) =>
        string.Equals(actual.ApplicationId, expected.ApplicationId, StringComparison.Ordinal)
        && string.Equals(actual.ApplicationVersion, expected.ApplicationVersion, StringComparison.Ordinal)
        && string.Equals(actual.SourceCommit, expected.SourceCommit, StringComparison.OrdinalIgnoreCase)
        && actual.SourceState == expected.SourceState;

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Preserve the original run or publication failure.
        }
    }
}

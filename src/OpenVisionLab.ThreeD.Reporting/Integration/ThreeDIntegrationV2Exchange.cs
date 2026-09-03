using System.Security.Cryptography;
using System.Text.Json;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

public sealed record ThreeDIntegrationV2TransactionSummary(
    IntegrationHandoffV2 Handoff,
    bool HasAcknowledgement,
    bool HasResult);

/// <summary>
/// Owns the current schema 2.0 3D-side local-file exchange. Discovery and
/// reading never load a recipe or invoke Preview, Publish, or Run.
/// </summary>
public static class ThreeDIntegrationV2Exchange
{
    public static IReadOnlyList<ThreeDIntegrationV2TransactionSummary> DiscoverHandoffs(
        string exchangeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
        var transactionsRoot = Path.Combine(
            Path.GetFullPath(exchangeRoot),
            IntegrationTransactionLayout.TransactionsDirectoryName);
        if (!Directory.Exists(transactionsRoot))
        {
            return [];
        }

        var transactions = new List<ThreeDIntegrationV2TransactionSummary>();
        foreach (var directory in Directory.EnumerateDirectories(transactionsRoot))
        {
            if (!Guid.TryParse(Path.GetFileName(directory), out var transactionId))
            {
                continue;
            }
            var handoffPath = Path.Combine(
                directory,
                IntegrationTransactionLayout.HandoffFileName);
            if (!File.Exists(handoffPath)
                || !UsesSchema(handoffPath, IntegrationContractSchema.V2))
            {
                continue;
            }

            var handoff = ReadHandoffEnvelope(exchangeRoot, transactionId);
            transactions.Add(new(
                handoff,
                File.Exists(Path.Combine(
                    directory,
                    IntegrationTransactionLayout.AcknowledgementFileName)),
                File.Exists(Path.Combine(
                    directory,
                    IntegrationTransactionLayout.ResultFileName))));
        }

        return transactions
            .OrderByDescending(transaction => transaction.Handoff.CreatedAtUtc)
            .ToArray();
    }

    public static IntegrationHandoffV2 ReadHandoff(
        string exchangeRoot,
        Guid transactionId)
    {
        var handoff = ReadHandoffEnvelope(exchangeRoot, transactionId);
        ValidateThreeDConsumer(handoff);
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        foreach (var artifact in handoff.Context.Artifacts)
        {
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                artifact,
                transactionDirectory));
        }

        RequireContextArtifact(handoff, IntegrationArtifactRoles.InspectionSource);
        RequireContextArtifact(handoff, IntegrationArtifactRoles.InspectionRecipe);
        return handoff;
    }

    public static IntegrationHandoffV2 ReadHandoffEnvelope(
        string exchangeRoot,
        Guid transactionId)
    {
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        var handoff = IntegrationContractJson.DeserializeHandoffV2(
            ReadMessage(
                transactionDirectory,
                IntegrationTransactionLayout.HandoffFileName));
        if (handoff.TransactionId != transactionId)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Handoff transaction identity does not match its directory.");
        }
        return handoff;
    }

    public static IntegrationAcknowledgementV2 PublishAcknowledgement(
        string exchangeRoot,
        IntegrationHandoffV2 handoff,
        IntegrationApplicationIdentity consumerBuild,
        string? rejectionReason = null)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(consumerBuild);
        if (rejectionReason is not null && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException(
                "Rejection reason cannot be blank.",
                nameof(rejectionReason));
        }

        var persisted = rejectionReason is null
            ? ReadHandoff(exchangeRoot, handoff.TransactionId)
            : ReadHandoffEnvelope(exchangeRoot, handoff.TransactionId);
        EnsureConsumerIdentity(persisted, consumerBuild);
        if (!IntegrationContractJson.SerializeCanonical(persisted)
            .SequenceEqual(IntegrationContractJson.SerializeCanonical(handoff)))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Acknowledgement Handoff does not match the persisted message.");
        }

        var acknowledgement = new IntegrationAcknowledgementV2(
            IntegrationContractSchema.V2,
            IntegrationMessageKind.Acknowledgement,
            Guid.NewGuid(),
            handoff.TransactionId,
            handoff.MessageId,
            NotBefore(handoff.CreatedAtUtc),
            consumerBuild,
            rejectionReason is null
                ? IntegrationAcknowledgementStatus.Accepted
                : IntegrationAcknowledgementStatus.Rejected,
            rejectionReason is null
                ? null
                : new IntegrationError(
                    IntegrationErrorCode.RequestRejected,
                    rejectionReason,
                    false));
        ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
            handoff,
            acknowledgement));
        WriteNewMessage(
            GetTransactionDirectory(exchangeRoot, handoff.TransactionId),
            IntegrationTransactionLayout.AcknowledgementFileName,
            IntegrationContractJson.SerializeCanonical(acknowledgement));
        return acknowledgement;
    }

    public static IntegrationAcknowledgementV2 ReadAcknowledgement(
        string exchangeRoot,
        Guid transactionId)
    {
        var handoff = ReadHandoff(exchangeRoot, transactionId);
        var acknowledgement = IntegrationContractJson.DeserializeAcknowledgementV2(
            ReadMessage(
                GetTransactionDirectory(exchangeRoot, transactionId),
                IntegrationTransactionLayout.AcknowledgementFileName));
        ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
            handoff,
            acknowledgement));
        return acknowledgement;
    }

    public static IntegrationResultV2 PublishCompletedResult(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity consumerBuild,
        string existingRunRecordPath)
    {
        ArgumentNullException.ThrowIfNull(consumerBuild);
        ArgumentException.ThrowIfNullOrWhiteSpace(existingRunRecordPath);
        var handoff = ReadHandoff(exchangeRoot, transactionId);
        EnsureConsumerIdentity(handoff, consumerBuild);
        var acknowledgement = ReadAcknowledgement(exchangeRoot, transactionId);
        if (acknowledgement.Status != IntegrationAcknowledgementStatus.Accepted)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "A completed Result requires an accepted Acknowledgement.");
        }

        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        var resultPath = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.ResultFileName);
        if (File.Exists(resultPath))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "The Handoff already has a Result.");
        }

        var runRecord = InspectionRunRecordJson.Read(existingRunRecordPath);
        if (string.IsNullOrWhiteSpace(runRecord.RunId))
        {
            throw new InvalidDataException("Run Record identity is required.");
        }
        EnsureRunRecordCorrelation(handoff.Context, runRecord);
        var outcome = runRecord.Status switch
        {
            ResultStatus.Pass => IntegrationInspectionOutcome.Pass,
            ResultStatus.Fail => IntegrationInspectionOutcome.Ng,
            ResultStatus.Warning => IntegrationInspectionOutcome.Indeterminate,
            _ => throw new InvalidDataException(
                $"Run Record status '{runRecord.Status}' is not a completed inspection state.")
        };

        var artifactsDirectory = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.ArtifactsDirectoryName);
        Directory.CreateDirectory(artifactsDirectory);
        var targetPath = Path.Combine(artifactsDirectory, "3d-run-record.json");
        File.Copy(Path.GetFullPath(existingRunRecordPath), targetPath, overwrite: false);
        try
        {
            var runRecordReference = CreateArtifactReference(
                IntegrationArtifactRoles.RunRecord,
                runRecord.RunId,
                targetPath,
                $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/3d-run-record.json");
            var result = new IntegrationResultV2(
                IntegrationContractSchema.V2,
                IntegrationMessageKind.Result,
                Guid.NewGuid(),
                handoff.TransactionId,
                handoff.MessageId,
                acknowledgement.MessageId,
                NotBefore(acknowledgement.CreatedAtUtc),
                consumerBuild,
                IntegrationResultStatus.Completed,
                outcome,
                runRecord.RunId,
                runRecordReference,
                IntegrationRunCorrelation.FromContext(handoff.Context),
                CreateMetrics(runRecord),
                [],
                null);
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
                handoff,
                acknowledgement,
                result));
            WriteNewMessage(
                transactionDirectory,
                IntegrationTransactionLayout.ResultFileName,
                IntegrationContractJson.SerializeCanonical(result));
            return result;
        }
        catch
        {
            TryDeleteFile(targetPath);
            throw;
        }
    }

    public static IntegrationResultV2 ReadResult(
        string exchangeRoot,
        Guid transactionId)
    {
        var handoff = ReadHandoff(exchangeRoot, transactionId);
        var acknowledgement = ReadAcknowledgement(exchangeRoot, transactionId);
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        var result = IntegrationContractJson.DeserializeResultV2(
            ReadMessage(
                transactionDirectory,
                IntegrationTransactionLayout.ResultFileName));
        ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
            handoff,
            acknowledgement,
            result));
        if (result.RunRecord is not null)
        {
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                result.RunRecord,
                transactionDirectory));
        }
        foreach (var evidence in result.Evidence)
        {
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                evidence,
                transactionDirectory));
        }
        return result;
    }

    private static IReadOnlyList<IntegrationMetric> CreateMetrics(
        InspectionRunRecord runRecord)
    {
        var metrics = new List<IntegrationMetric>();
        if (double.IsFinite(runRecord.ElapsedMilliseconds))
        {
            metrics.Add(new(
                "elapsedMilliseconds",
                runRecord.ElapsedMilliseconds,
                "ms"));
        }
        var runMetrics = runRecord.Metrics ?? [];
        for (var index = 0; index < runMetrics.Count; index++)
        {
            var metric = runMetrics[index];
            if (double.IsFinite(metric.Value))
            {
                metrics.Add(new(
                    $"metric.{index}.{metric.Name}",
                    metric.Value,
                    string.IsNullOrWhiteSpace(metric.Unit) ? "unitless" : metric.Unit));
            }
        }
        return metrics;
    }

    private static void EnsureRunRecordCorrelation(
        IntegrationInspectionContextV2 context,
        InspectionRunRecord runRecord)
    {
        if (runRecord.Source is null
            || !string.Equals(
                runRecord.Source.Sha256,
                context.InputSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record source SHA-256 does not match the Handoff input.");
        }
        if (runRecord.Recipe is null
            || !string.Equals(
                runRecord.Recipe.Sha256,
                context.RecipeSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record recipe SHA-256 does not match the Handoff recipe.");
        }
    }

    private static IntegrationArtifactReference RequireContextArtifact(
        IntegrationHandoffV2 handoff,
        string role) =>
        handoff.Context.Artifacts.SingleOrDefault(artifact =>
            string.Equals(artifact.Role, role, StringComparison.Ordinal))
        ?? throw new IntegrationContractException(
            IntegrationErrorCode.InvalidArtifact,
            $"The Handoff does not contain the required '{role}' artifact.");

    private static IntegrationArtifactReference CreateArtifactReference(
        string role,
        string artifactId,
        string fullPath,
        string relativePath)
    {
        using var stream = File.OpenRead(fullPath);
        return new(
            role,
            artifactId,
            relativePath,
            stream.Length,
            Convert.ToHexString(SHA256.HashData(stream)));
    }

    private static void ValidateThreeDConsumer(IntegrationHandoffV2 handoff)
    {
        if (handoff.Context.Modality != IntegrationInspectionModality.ThreeD
            || handoff.Context.InputKind != IntegrationInspectionInputKind.HeightMap
            || !string.Equals(
                handoff.Context.ConsumerBuild.ApplicationId,
                IntegrationApplicationIds.ThreeDStudio,
                StringComparison.Ordinal))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.RequestRejected,
                "The Handoff is not a 3D HeightMap inspection request.");
        }
    }

    private static void EnsureConsumerIdentity(
        IntegrationHandoffV2 handoff,
        IntegrationApplicationIdentity consumerBuild)
    {
        ValidateThreeDConsumer(handoff);
        if (!ApplicationIdentitiesMatch(handoff.Context.ConsumerBuild, consumerBuild))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "The supplied 3D consumer build does not match the Handoff context.");
        }
    }

    private static bool ApplicationIdentitiesMatch(
        IntegrationApplicationIdentity actual,
        IntegrationApplicationIdentity expected) =>
        string.Equals(actual.ApplicationId, expected.ApplicationId, StringComparison.Ordinal)
        && string.Equals(actual.ApplicationVersion, expected.ApplicationVersion, StringComparison.Ordinal)
        && string.Equals(actual.SourceCommit, expected.SourceCommit, StringComparison.OrdinalIgnoreCase)
        && actual.SourceState == expected.SourceState;

    private static bool UsesSchema(string path, string expectedSchema)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllBytes(path));
            return document.RootElement.TryGetProperty("schemaVersion", out var value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), expectedSchema, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static DateTimeOffset NotBefore(DateTimeOffset predecessor)
    {
        var now = DateTimeOffset.UtcNow;
        return now < predecessor ? predecessor : now;
    }

    private static string GetTransactionDirectory(string exchangeRoot, Guid transactionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Transaction identity cannot be empty.",
                nameof(transactionId));
        }
        return Path.Combine(
            Path.GetFullPath(exchangeRoot),
            IntegrationTransactionLayout.TransactionsDirectoryName,
            transactionId.ToString("D"));
    }

    private static byte[] ReadMessage(string transactionDirectory, string fileName) =>
        File.ReadAllBytes(Path.Combine(transactionDirectory, fileName));

    private static void WriteNewMessage(
        string transactionDirectory,
        string fileName,
        byte[] bytes)
    {
        var target = Path.Combine(transactionDirectory, fileName);
        if (File.Exists(target))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                $"The transaction already contains '{fileName}'.");
        }
        var temporary = Path.Combine(
            transactionDirectory,
            $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, target);
        }
        finally
        {
            TryDeleteFile(temporary);
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Preserve the original contract, Run Record, or I/O failure.
        }
    }

    private static void ThrowIfInvalid(IntegrationValidationResult validation)
    {
        if (validation.IsValid)
        {
            return;
        }
        var issue = validation.Issues[0];
        throw new IntegrationContractException(
            issue.Code,
            $"{issue.Field}: {issue.Message}");
    }
}

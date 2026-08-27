using System.Security.Cryptography;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

public sealed record ThreeDIntegrationTransactionSummary(
    IntegrationHandoffV2 Handoff,
    bool HasAcknowledgement,
    bool HasResult);

/// <summary>
/// Owns explicit 3D-side file exchange. Reading never changes the workspace or
/// invokes Preview, Publish, or Run. A completed Result is published only when
/// the selected Run Record carries the exact Handoff project, sequence, step,
/// acquisition, input, recipe, and consumer-build identity.
/// </summary>
public static class ThreeDIntegrationExchange
{
    public static IReadOnlyList<ThreeDIntegrationTransactionSummary> DiscoverHandoffs(
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

        var transactions = new List<ThreeDIntegrationTransactionSummary>();
        foreach (var directory in Directory.EnumerateDirectories(transactionsRoot))
        {
            if (!Guid.TryParse(Path.GetFileName(directory), out var transactionId))
            {
                continue;
            }

            var handoffPath = Path.Combine(
                directory,
                IntegrationTransactionLayout.HandoffFileName);
            if (!File.Exists(handoffPath))
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
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        foreach (var artifact in handoff.Context.Artifacts)
        {
            EnsureNoReparsePoints(transactionDirectory, artifact.RelativePath);
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                artifact,
                transactionDirectory));
        }

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

    public static IntegrationResultV2 ReadResult(
        string exchangeRoot,
        Guid transactionId)
    {
        var handoff = ReadHandoff(exchangeRoot, transactionId);
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        var acknowledgement = IntegrationContractJson.DeserializeAcknowledgementV2(
            ReadMessage(
                transactionDirectory,
                IntegrationTransactionLayout.AcknowledgementFileName));
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
            EnsureNoReparsePoints(transactionDirectory, result.RunRecord.RelativePath);
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                result.RunRecord,
                transactionDirectory));
        }
        foreach (var evidence in result.Evidence)
        {
            EnsureNoReparsePoints(transactionDirectory, evidence.RelativePath);
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                evidence,
                transactionDirectory));
        }

        return result;
    }

    public static IntegrationAcknowledgementV2 PublishAcknowledgement(
        string exchangeRoot,
        IntegrationHandoffV2 handoff,
        IntegrationApplicationIdentity producer,
        string? rejectionReason = null)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentNullException.ThrowIfNull(producer);
        if (rejectionReason is not null && string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException(
                "Rejection reason cannot be blank.",
                nameof(rejectionReason));
        }

        var persisted = rejectionReason is null
            ? ReadHandoff(exchangeRoot, handoff.TransactionId)
            : ReadHandoffEnvelope(exchangeRoot, handoff.TransactionId);
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
            producer,
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

    public static IntegrationResultV2 PublishCompletedResult(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity producer,
        string existingRunRecordPath)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentException.ThrowIfNullOrWhiteSpace(existingRunRecordPath);

        var handoff = ReadHandoff(exchangeRoot, transactionId);
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        var acknowledgement = IntegrationContractJson.DeserializeAcknowledgementV2(
            ReadMessage(
                transactionDirectory,
                IntegrationTransactionLayout.AcknowledgementFileName));
        ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
            handoff,
            acknowledgement));
        if (acknowledgement.Status != IntegrationAcknowledgementStatus.Accepted)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "A completed Result requires an accepted Acknowledgement.");
        }

        EnsureNoReparsePointsForExternalFile(existingRunRecordPath);
        var runRecord = InspectionRunRecordJson.Read(existingRunRecordPath);
        ValidateRunRecordCorrelation(runRecord, handoff);
        var outcome = MapOutcome(runRecord.Status);
        var artifactsDirectory = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.ArtifactsDirectoryName);
        Directory.CreateDirectory(artifactsDirectory);

        var targetPath = Path.Combine(artifactsDirectory, "3d-run-record.json");
        var temporaryPath = Path.Combine(
            artifactsDirectory,
            $".3d-run-record.{Guid.NewGuid():N}.tmp");
        try
        {
            File.Copy(
                Path.GetFullPath(existingRunRecordPath),
                temporaryPath,
                overwrite: false);
            File.Move(temporaryPath, targetPath);

            var reference = CreateArtifactReference(
                IntegrationArtifactRoles.RunRecord,
                runRecord.RunId,
                targetPath,
                $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/3d-run-record.json");
            var result = new IntegrationResultV2(
                IntegrationContractSchema.V2,
                IntegrationMessageKind.Result,
                Guid.NewGuid(),
                transactionId,
                handoff.MessageId,
                acknowledgement.MessageId,
                NotBefore(acknowledgement.CreatedAtUtc),
                producer,
                IntegrationResultStatus.Completed,
                outcome,
                runRecord.RunId,
                reference,
                IntegrationRunCorrelation.FromContext(handoff.Context),
                runRecord.Metrics
                    .Select(metric => new IntegrationMetric(
                        metric.Name,
                        metric.Value,
                        metric.Unit))
                    .ToArray(),
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
            TryDeleteFile(temporaryPath);
            throw;
        }
    }

    private static void ValidateRunRecordCorrelation(
        InspectionRunRecord runRecord,
        IntegrationHandoffV2 handoff)
    {
        if (string.IsNullOrWhiteSpace(runRecord.RunId))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record identity is required for an integration Result.");
        }

        var context = runRecord.IntegrationContext
                      ?? throw new IntegrationContractException(
                          IntegrationErrorCode.CorrelationMismatch,
                          "Run Record does not contain the exact integration context.");
        if (!TryParseModality(context.Modality, out var modality)
            || !TryParseInputKind(context.InputKind, out var inputKind)
            || !TryParseSourceState(
                context.ConsumerSourceState,
                out var sourceState))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record integration context contains an unsupported enum value.");
        }

        var actual = new IntegrationRunCorrelation(
            context.ProjectId,
            context.ProjectSchema,
            context.SequenceId,
            context.StepId,
            context.CameraId,
            context.AcquisitionId,
            context.FrameId,
            context.Unit,
            modality,
            inputKind,
            runRecord.Source.Sha256,
            runRecord.Recipe.Sha256,
            new IntegrationApplicationIdentity(
                context.ConsumerApplicationId,
                context.ConsumerApplicationVersion,
                context.ConsumerSourceCommit,
                sourceState));
        var expected = IntegrationRunCorrelation.FromContext(handoff.Context);

        if (!CorrelationsMatch(actual, expected)
            || !string.Equals(
                context.Unit,
                runRecord.Source.Unit,
                StringComparison.Ordinal)
            || !IsStepRecorded(runRecord, context.StepId))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record identity does not match the exact Handoff project, sequence, step, acquisition, input, recipe, or consumer build.");
        }
    }

    private static bool IsStepRecorded(
        InspectionRunRecord runRecord,
        string stepId) =>
        string.Equals(runRecord.Step?.Id, stepId, StringComparison.Ordinal)
        || runRecord.Steps?.Any(step => string.Equals(
            step.Id,
            stepId,
            StringComparison.Ordinal)) == true;

    private static bool CorrelationsMatch(
        IntegrationRunCorrelation actual,
        IntegrationRunCorrelation expected) =>
        string.Equals(actual.ProjectId, expected.ProjectId, StringComparison.Ordinal)
        && string.Equals(actual.ProjectSchema, expected.ProjectSchema, StringComparison.Ordinal)
        && string.Equals(actual.SequenceId, expected.SequenceId, StringComparison.Ordinal)
        && string.Equals(actual.StepId, expected.StepId, StringComparison.Ordinal)
        && string.Equals(actual.CameraId, expected.CameraId, StringComparison.Ordinal)
        && string.Equals(actual.AcquisitionId, expected.AcquisitionId, StringComparison.Ordinal)
        && string.Equals(actual.FrameId, expected.FrameId, StringComparison.Ordinal)
        && string.Equals(actual.Unit, expected.Unit, StringComparison.Ordinal)
        && actual.Modality == expected.Modality
        && actual.InputKind == expected.InputKind
        && string.Equals(actual.InputSha256, expected.InputSha256, StringComparison.OrdinalIgnoreCase)
        && string.Equals(actual.RecipeSha256, expected.RecipeSha256, StringComparison.OrdinalIgnoreCase)
        && ApplicationIdentitiesMatch(actual.ConsumerBuild, expected.ConsumerBuild);

    private static bool ApplicationIdentitiesMatch(
        IntegrationApplicationIdentity actual,
        IntegrationApplicationIdentity expected) =>
        string.Equals(actual.ApplicationId, expected.ApplicationId, StringComparison.Ordinal)
        && string.Equals(actual.ApplicationVersion, expected.ApplicationVersion, StringComparison.Ordinal)
        && string.Equals(actual.SourceCommit, expected.SourceCommit, StringComparison.OrdinalIgnoreCase)
        && actual.SourceState == expected.SourceState;

    private static bool TryParseModality(
        string value,
        out IntegrationInspectionModality modality) =>
        Enum.TryParse(value, ignoreCase: false, out modality)
        && Enum.IsDefined(modality);

    private static bool TryParseInputKind(
        string value,
        out IntegrationInspectionInputKind inputKind) =>
        Enum.TryParse(value, ignoreCase: false, out inputKind)
        && Enum.IsDefined(inputKind);

    private static bool TryParseSourceState(
        string value,
        out IntegrationSourceState sourceState) =>
        Enum.TryParse(value, ignoreCase: false, out sourceState)
        && Enum.IsDefined(sourceState);

    private static IntegrationInspectionOutcome MapOutcome(ResultStatus status) =>
        status switch
        {
            ResultStatus.Pass => IntegrationInspectionOutcome.Pass,
            ResultStatus.Fail => IntegrationInspectionOutcome.Ng,
            ResultStatus.Warning => IntegrationInspectionOutcome.Indeterminate,
            _ => throw new IntegrationContractException(
                IntegrationErrorCode.ExecutionFailed,
                $"Run Record status '{status}' cannot be published as a completed Result.")
        };

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

    private static DateTimeOffset NotBefore(DateTimeOffset predecessor)
    {
        var now = DateTimeOffset.UtcNow;
        return now < predecessor ? predecessor : now;
    }

    private static string GetTransactionDirectory(
        string exchangeRoot,
        Guid transactionId)
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

    private static void EnsureNoReparsePoints(
        string transactionDirectory,
        string relativePath)
    {
        var current = Path.GetFullPath(transactionDirectory);
        var root = new DirectoryInfo(current);
        if (root.Exists && root.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.UnsafeArtifactPath,
                "The transaction directory cannot be a symbolic link or reparse point.");
        }

        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            FileSystemInfo entry = index == segments.Length - 1
                ? new FileInfo(current)
                : new DirectoryInfo(current);
            if (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.UnsafeArtifactPath,
                    "Artifact paths cannot traverse symbolic links or reparse points.");
            }
        }
    }

    private static void EnsureNoReparsePointsForExternalFile(string path)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        if (file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.UnsafeArtifactPath,
                "A Run Record source cannot be a symbolic link or reparse point.");
        }
    }

    private static byte[] ReadMessage(
        string transactionDirectory,
        string fileName) => File.ReadAllBytes(Path.Combine(transactionDirectory, fileName));

    private static void WriteNewMessage(
        string transactionDirectory,
        string fileName,
        byte[] bytes)
    {
        var target = Path.Combine(transactionDirectory, fileName);
        var temporary = Path.Combine(
            transactionDirectory,
            $".{fileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(
                       temporary,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.Read,
                       bufferSize: 4096,
                       options: FileOptions.SequentialScan))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporary, target);
        }
        finally
        {
            TryDeleteFile(temporary);
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

    private static void TryDeleteFile(string path)
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
            // Preserve the original contract or I/O failure.
        }
    }
}

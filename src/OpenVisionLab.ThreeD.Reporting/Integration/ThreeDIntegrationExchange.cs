using System.Security.Cryptography;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

public sealed record ThreeDIntegrationTransactionSummary(
    IntegrationHandoff Handoff,
    bool HasAcknowledgement,
    bool HasResult);

/// <summary>
/// Owns explicit 3D-side file exchange. Reading never changes the workspace or
/// invokes Preview, Publish, or Run.
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
            var handoffPath = Path.Combine(directory, IntegrationTransactionLayout.HandoffFileName);
            if (!File.Exists(handoffPath))
            {
                continue;
            }
            var handoff = ReadHandoffEnvelope(exchangeRoot, transactionId);
            transactions.Add(new(
                handoff,
                File.Exists(Path.Combine(directory, IntegrationTransactionLayout.AcknowledgementFileName)),
                File.Exists(Path.Combine(directory, IntegrationTransactionLayout.ResultFileName))));
        }

        return transactions
            .OrderByDescending(transaction => transaction.Handoff.CreatedAtUtc)
            .ToArray();
    }

    public static IntegrationHandoff ReadHandoff(
        string exchangeRoot,
        Guid transactionId)
    {
        var handoff = ReadHandoffEnvelope(exchangeRoot, transactionId);
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        foreach (var artifact in handoff.Context.Artifacts)
        {
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                artifact,
                transactionDirectory));
        }
        return handoff;
    }

    public static IntegrationHandoff ReadHandoffEnvelope(
        string exchangeRoot,
        Guid transactionId)
    {
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        var handoff = IntegrationContractJson.DeserializeHandoff(
            ReadMessage(transactionDirectory, IntegrationTransactionLayout.HandoffFileName));
        if (handoff.TransactionId != transactionId)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Handoff transaction identity does not match its directory.");
        }
        return handoff;
    }

    public static IntegrationAcknowledgement PublishAcknowledgement(
        string exchangeRoot,
        IntegrationHandoff handoff,
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
        if (!IntegrationContractJson.Serialize(persisted)
            .SequenceEqual(IntegrationContractJson.Serialize(handoff)))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Acknowledgement Handoff does not match the persisted message.");
        }

        var acknowledgement = new IntegrationAcknowledgement(
            IntegrationContractSchema.Current,
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
        var transactionDirectory = GetTransactionDirectory(
            exchangeRoot,
            handoff.TransactionId);
        WriteNewMessage(
            transactionDirectory,
            IntegrationTransactionLayout.AcknowledgementFileName,
            IntegrationContractJson.Serialize(acknowledgement));
        return acknowledgement;
    }

    public static IntegrationResult PublishCompletedResult(
        string exchangeRoot,
        Guid transactionId,
        IntegrationApplicationIdentity producer,
        string existingRunRecordPath)
    {
        ArgumentNullException.ThrowIfNull(producer);
        ArgumentException.ThrowIfNullOrWhiteSpace(existingRunRecordPath);
        var handoff = ReadHandoff(exchangeRoot, transactionId);
        var transactionDirectory = GetTransactionDirectory(exchangeRoot, transactionId);
        var acknowledgement = IntegrationContractJson.DeserializeAcknowledgement(
            ReadMessage(
                transactionDirectory,
                IntegrationTransactionLayout.AcknowledgementFileName));
        ThrowIfInvalid(IntegrationContractValidator.ValidateSequence(
            handoff,
            acknowledgement));
        if (acknowledgement.Status != IntegrationAcknowledgementStatus.Accepted)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "A completed Result requires an accepted Acknowledgement.");
        }

        var runRecord = InspectionRunRecordJson.Read(existingRunRecordPath);
        if (string.IsNullOrWhiteSpace(runRecord.RunId))
        {
            throw new InvalidDataException("Run Record identity is required.");
        }
        var disposition = runRecord.Status switch
        {
            ResultStatus.Pass => IntegrationInspectionDisposition.Pass,
            ResultStatus.Fail => IntegrationInspectionDisposition.Fail,
            ResultStatus.Warning => IntegrationInspectionDisposition.Indeterminate,
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
            using var stream = File.OpenRead(targetPath);
            var reference = new IntegrationArtifactReference(
                IntegrationArtifactRoles.RunRecord,
                runRecord.RunId,
                $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/3d-run-record.json",
                stream.Length,
                Convert.ToHexString(SHA256.HashData(stream)));
            var result = new IntegrationResult(
                IntegrationContractSchema.Current,
                IntegrationMessageKind.Result,
                Guid.NewGuid(),
                transactionId,
                handoff.MessageId,
                acknowledgement.MessageId,
                NotBefore(acknowledgement.CreatedAtUtc),
                producer,
                IntegrationResultStatus.Completed,
                disposition,
                runRecord.RunId,
                reference,
                null);
            ThrowIfInvalid(IntegrationContractValidator.ValidateSequence(
                handoff,
                acknowledgement,
                result));
            WriteNewMessage(
                transactionDirectory,
                IntegrationTransactionLayout.ResultFileName,
                IntegrationContractJson.Serialize(result));
            return result;
        }
        catch
        {
            try
            {
                File.Delete(targetPath);
            }
            catch
            {
                // Preserve the contract, Run Record, or I/O failure.
            }
            throw;
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
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
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

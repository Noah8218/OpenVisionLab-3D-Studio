using System.Text.Json;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;

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
            if (!UsesSchema(handoffPath, IntegrationContractSchema.Legacy))
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
            IntegrationContractSchema.Legacy,
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

        using var publication = ThreeDIntegrationRunRecordPublication.Create(
            existingRunRecordPath,
            transactionDirectory);
        var result = new IntegrationResult(
            IntegrationContractSchema.Legacy,
            IntegrationMessageKind.Result,
            Guid.NewGuid(),
            transactionId,
            handoff.MessageId,
            acknowledgement.MessageId,
            NotBefore(acknowledgement.CreatedAtUtc),
            producer,
            IntegrationResultStatus.Completed,
            publication.Disposition,
            publication.RunId,
            publication.Artifact,
            null);
        ThrowIfInvalid(IntegrationContractValidator.ValidateSequence(
            handoff,
            acknowledgement,
            result));
        WriteNewMessage(
            transactionDirectory,
            IntegrationTransactionLayout.ResultFileName,
            IntegrationContractJson.Serialize(result));
        publication.Commit();
        return result;
    }

    private static DateTimeOffset NotBefore(DateTimeOffset predecessor)
    {
        var now = DateTimeOffset.UtcNow;
        return now < predecessor ? predecessor : now;
    }

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

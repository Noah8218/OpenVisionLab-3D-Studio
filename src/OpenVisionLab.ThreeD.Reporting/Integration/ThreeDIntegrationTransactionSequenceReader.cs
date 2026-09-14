using System.IO;
using OpenVisionLab.Integration.Contracts;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

/// <summary>
/// Reads and validates the local V2 handoff/acknowledgement/result sequence.
/// TCP server/client lifetime belongs to the separate transport owner.
/// </summary>
public static class ThreeDIntegrationTransactionSequenceReader
{
    public static ThreeDIntegrationTcpSequence ReadValidatedV2Sequence(
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

        var transactionDirectory = Path.Combine(
            Path.GetFullPath(exchangeRoot),
            IntegrationTransactionLayout.TransactionsDirectoryName,
            transactionId.ToString("D"));
        var handoff = IntegrationContractJson.DeserializeHandoffV2(
            File.ReadAllBytes(Path.Combine(
                transactionDirectory,
                IntegrationTransactionLayout.HandoffFileName)));
        if (handoff.TransactionId != transactionId)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Handoff transaction identity does not match its directory.");
        }

        foreach (var artifact in handoff.Context.Artifacts)
        {
            ThrowIfInvalid(IntegrationContractValidator.ValidateArtifactFile(
                artifact,
                transactionDirectory));
        }

        var acknowledgementPath = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.AcknowledgementFileName);
        IntegrationAcknowledgementV2? acknowledgement = null;
        if (File.Exists(acknowledgementPath))
        {
            acknowledgement = IntegrationContractJson.DeserializeAcknowledgementV2(
                File.ReadAllBytes(acknowledgementPath));
            ThrowIfInvalid(IntegrationContractValidator.ValidateV2Sequence(
                handoff,
                acknowledgement));
        }

        var resultPath = Path.Combine(
            transactionDirectory,
            IntegrationTransactionLayout.ResultFileName);
        IntegrationResultV2? result = null;
        if (File.Exists(resultPath))
        {
            if (acknowledgement is null)
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.InvalidState,
                    "A Result cannot be read before its Acknowledgement.");
            }

            result = IntegrationContractJson.DeserializeResultV2(
                File.ReadAllBytes(resultPath));
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
        }

        return new(handoff, acknowledgement, result);
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

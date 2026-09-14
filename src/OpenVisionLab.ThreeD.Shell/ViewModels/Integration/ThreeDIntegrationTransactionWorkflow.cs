using System.IO;
using System.Text.Json;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Reporting.Integration;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Integration;

public sealed record ThreeDIntegrationTransactionItem(
    Guid TransactionId,
    string SchemaVersion,
    DateTimeOffset CreatedAtUtc,
    string ProjectId,
    string SequenceId,
    string StepId,
    string CameraId,
    string State,
    string ModalitySummary,
    string AcknowledgementSummary,
    string ResultSummary,
    bool CanInspectInThreeD,
    bool HasAcknowledgement,
    bool HasResult,
    IntegrationAcknowledgementStatus? AcknowledgementStatus)
{
    public string Title => $"{ProjectId} | {State}";
    public string Detail => $"schema {SchemaVersion} | {ModalitySummary} | {AcknowledgementSummary} | {ResultSummary} | {SequenceId} / {StepId} | {CameraId} | {CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
}

internal sealed record ThreeDIntegrationPublishedResult(string Outcome, string RunId);

/// <summary>
/// Owns transaction discovery, review, and result publication without WPF state.
/// The ViewModel remains responsible for bindings, commands, and status projection.
/// </summary>
internal sealed class ThreeDIntegrationTransactionWorkflow
{
    private readonly Func<IntegrationApplicationIdentity?, IntegrationApplicationIdentity> identityResolver;
    private readonly Func<string, string, string, string> localize;

    public ThreeDIntegrationTransactionWorkflow(
        Func<IntegrationApplicationIdentity?, IntegrationApplicationIdentity> identityResolver,
        Func<string, string, string, string> localize)
    {
        this.identityResolver = identityResolver ?? throw new ArgumentNullException(nameof(identityResolver));
        this.localize = localize ?? throw new ArgumentNullException(nameof(localize));
    }

    public IReadOnlyList<ThreeDIntegrationTransactionItem> Discover(string exchangeRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
        var root = Path.GetFullPath(exchangeRoot);
        var discoveredV2 = ThreeDIntegrationV2Exchange.DiscoverHandoffs(root);
        var discoveredLegacy = ThreeDIntegrationExchange.DiscoverHandoffs(root);
        var items = new List<ThreeDIntegrationTransactionItem>();
        foreach (var transaction in discoveredV2)
        {
            var sequence = ThreeDIntegrationTransactionSequenceReader.ReadValidatedV2Sequence(
                root,
                transaction.Handoff.TransactionId);
            var state = sequence.Result is not null
                ? localize("StatePublished", "결과 게시됨", "Result published")
                : sequence.Acknowledgement is not null
                    ? localize("StateReviewed", "검토됨", "Reviewed")
                    : localize("StatePending", "검토 대기", "Pending review");
            items.Add(new(
                sequence.Handoff.TransactionId,
                sequence.Handoff.SchemaVersion,
                sequence.Handoff.CreatedAtUtc,
                sequence.Handoff.Context.ProjectId,
                sequence.Handoff.Context.SequenceId,
                sequence.Handoff.Context.StepId,
                sequence.Handoff.Context.CameraId,
                state,
                $"{sequence.Handoff.Context.Modality}/{sequence.Handoff.Context.InputKind}",
                DescribeAcknowledgement(sequence.Acknowledgement),
                DescribeResult(sequence.Result),
                IsThreeDInspectionRequest(sequence.Handoff),
                sequence.Acknowledgement is not null,
                sequence.Result is not null,
                sequence.Acknowledgement?.Status));
        }

        foreach (var transaction in discoveredLegacy)
        {
            var state = transaction.HasResult
                ? localize("StatePublished", "결과 게시됨", "Result published")
                : transaction.HasAcknowledgement
                    ? localize("StateReviewed", "검토됨", "Reviewed")
                    : localize("StatePending", "검토 대기", "Pending review");
            items.Add(new(
                transaction.Handoff.TransactionId,
                transaction.Handoff.SchemaVersion,
                transaction.Handoff.CreatedAtUtc,
                transaction.Handoff.Context.ProjectId,
                transaction.Handoff.Context.SequenceId,
                transaction.Handoff.Context.StepId,
                transaction.Handoff.Context.CameraId,
                state,
                "Legacy/3D",
                transaction.HasAcknowledgement
                    ? localize("AckPresent", "ACK 있음", "ACK present")
                    : localize("AckAbsent", "ACK 없음", "ACK absent"),
                transaction.HasResult
                    ? localize("ResultPresent", "Result 있음", "Result present")
                    : localize("ResultAbsent", "Result 없음", "Result absent"),
                true,
                transaction.HasAcknowledgement,
                transaction.HasResult,
                ReadLegacyAcknowledgementStatus(root, transaction.Handoff.TransactionId)));
        }

        return items.OrderByDescending(item => item.CreatedAtUtc).ToArray();
    }

    public IntegrationAcknowledgementStatus Review(
        string exchangeRoot,
        ThreeDIntegrationTransactionItem selected,
        string? rejectionReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
        ArgumentNullException.ThrowIfNull(selected);
        var root = Path.GetFullPath(exchangeRoot);
        if (selected.SchemaVersion == IntegrationContractSchema.V2)
        {
            var handoff = rejectionReason is null
                ? ThreeDIntegrationV2Exchange.ReadHandoff(root, selected.TransactionId)
                : ThreeDIntegrationV2Exchange.ReadHandoffEnvelope(root, selected.TransactionId);
            return ThreeDIntegrationV2Exchange.PublishAcknowledgement(
                root,
                handoff,
                identityResolver(handoff.Context.ConsumerBuild),
                rejectionReason).Status;
        }

        return ThreeDIntegrationExchange.PublishAcknowledgement(
            root,
            rejectionReason is null
                ? ThreeDIntegrationExchange.ReadHandoff(root, selected.TransactionId)
                : ThreeDIntegrationExchange.ReadHandoffEnvelope(root, selected.TransactionId),
            identityResolver(null),
            rejectionReason).Status;
    }

    public ThreeDIntegrationPublishedResult PublishResult(
        string exchangeRoot,
        ThreeDIntegrationTransactionItem selected,
        string runRecordPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exchangeRoot);
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentException.ThrowIfNullOrWhiteSpace(runRecordPath);
        var root = Path.GetFullPath(exchangeRoot);
        if (selected.SchemaVersion == IntegrationContractSchema.V2)
        {
            var handoff = ThreeDIntegrationV2Exchange.ReadHandoff(root, selected.TransactionId);
            var result = ThreeDIntegrationV2Exchange.PublishCompletedResult(
                root,
                selected.TransactionId,
                identityResolver(handoff.Context.ConsumerBuild),
                runRecordPath);
            return new(result.Outcome.ToString(), result.RunId!);
        }

        var legacyResult = ThreeDIntegrationExchange.PublishCompletedResult(
            root,
            selected.TransactionId,
            identityResolver(null),
            runRecordPath);
        return new(legacyResult.Disposition.ToString(), legacyResult.RunId!);
    }

    private string DescribeAcknowledgement(IntegrationAcknowledgementV2? acknowledgement) =>
        acknowledgement is null
            ? localize("AckAbsent", "ACK 없음", "ACK absent")
            : $"ACK {acknowledgement.Status}";

    private string DescribeResult(IntegrationResultV2? result) =>
        result is null
            ? localize("ResultAbsent", "Result 없음", "Result absent")
            : $"Result {result.Status}/{result.Outcome}/Run {result.RunId ?? "-"}";

    private static IntegrationAcknowledgementStatus? ReadLegacyAcknowledgementStatus(
        string exchangeRoot,
        Guid transactionId)
    {
        var path = Path.Combine(
            Path.GetFullPath(exchangeRoot),
            IntegrationTransactionLayout.TransactionsDirectoryName,
            transactionId.ToString("D"),
            IntegrationTransactionLayout.AcknowledgementFileName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return IntegrationContractJson.DeserializeAcknowledgement(
                File.ReadAllBytes(path)).Status;
        }
        catch (Exception exception) when (
            exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidDataException
            or IntegrationContractException)
        {
            return null;
        }
    }

    private static bool IsThreeDInspectionRequest(IntegrationHandoffV2 handoff) =>
        handoff.Context.Modality == IntegrationInspectionModality.ThreeD
        && handoff.Context.InputKind == IntegrationInspectionInputKind.HeightMap
        && string.Equals(
            handoff.Context.ConsumerBuild.ApplicationId,
            IntegrationApplicationIds.ThreeDStudio,
            StringComparison.Ordinal);
}

using System.Collections.Concurrent;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.Integration.Transport.Tcp;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

/// <summary>
/// Owns the 3D consumer's active inspection cancellation tokens and the
/// authenticated Machine Studio cancellation request boundary.
/// </summary>
public sealed class ThreeDIntegrationCancellationCoordinator : IDisposable
{
    private readonly string exchangeRoot;
    private readonly IntegrationApplicationIdentity consumerBuild;
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> activeRuns = new();
    private int disposed;

    public ThreeDIntegrationCancellationCoordinator(
        string exchangeRoot,
        IntegrationApplicationIdentity consumerBuild)
    {
        this.exchangeRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(exchangeRoot)
                ? throw new ArgumentException("An exchange root is required.", nameof(exchangeRoot))
                : exchangeRoot.Trim());
        this.consumerBuild = consumerBuild ?? throw new ArgumentNullException(nameof(consumerBuild));
    }

    public T RunWithCancellation<T>(
        Guid transactionId,
        Func<CancellationToken, T> operation,
        Func<IntegrationResultV2, T> cancellationResultFactory,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(cancellationResultFactory);
        using var runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (!activeRuns.TryAdd(transactionId, runCancellation))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.InvalidState,
                "The 3D transaction already has an active inspection.");
        }

        try
        {
            try
            {
                return operation(runCancellation.Token);
            }
            catch (OperationCanceledException) when (runCancellation.IsCancellationRequested)
            {
                return cancellationResultFactory(
                    ThreeDIntegrationV2Exchange.PublishCancelledResult(
                        exchangeRoot,
                        transactionId,
                        consumerBuild));
            }
        }
        finally
        {
            activeRuns.TryRemove(
                new KeyValuePair<Guid, CancellationTokenSource>(
                    transactionId,
                    runCancellation));
        }
    }

    public Task<TcpIntegrationCancellationReceipt> HandleCancellationRequestedAsync(
        IntegrationCancelRequestV2 request,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(
                request.Requester.ApplicationId,
                IntegrationApplicationIds.MachineStudio,
                StringComparison.Ordinal))
        {
            return Task.FromResult(CreateReceipt(
                request,
                TcpIntegrationCancellationStatus.Rejected,
                idempotent: false));
        }

        IntegrationHandoffV2 handoff;
        try
        {
            handoff = ThreeDIntegrationV2Exchange.ReadHandoffEnvelope(
                exchangeRoot,
                request.TransactionId);
        }
        catch (Exception exception) when (
            exception is FileNotFoundException
                or DirectoryNotFoundException
                or IntegrationContractException)
        {
            return Task.FromResult(CreateReceipt(
                request,
                TcpIntegrationCancellationStatus.NotFound,
                idempotent: true));
        }

        if (handoff.MessageId != request.HandoffMessageId)
        {
            return Task.FromResult(CreateReceipt(
                request,
                TcpIntegrationCancellationStatus.Rejected,
                idempotent: false));
        }

        var resultPath = Path.Combine(
            exchangeRoot,
            IntegrationTransactionLayout.TransactionsDirectoryName,
            request.TransactionId.ToString("D"),
            IntegrationTransactionLayout.ResultFileName);
        if (File.Exists(resultPath))
        {
            try
            {
                var result = ThreeDIntegrationV2Exchange.ReadResult(
                    exchangeRoot,
                    request.TransactionId);
                var status = result.Status switch
                {
                    IntegrationResultStatus.Completed =>
                        TcpIntegrationCancellationStatus.AlreadyCompleted,
                    IntegrationResultStatus.Failed =>
                        TcpIntegrationCancellationStatus.AlreadyFailed,
                    IntegrationResultStatus.Cancelled =>
                        TcpIntegrationCancellationStatus.AlreadyCancelled,
                    _ => TcpIntegrationCancellationStatus.Rejected
                };
                return Task.FromResult(CreateReceipt(request, status, idempotent: true));
            }
            catch (IntegrationContractException)
            {
                return Task.FromResult(CreateReceipt(
                    request,
                    TcpIntegrationCancellationStatus.Rejected,
                    idempotent: false));
            }
        }

        if (!activeRuns.TryGetValue(request.TransactionId, out var runCancellation))
        {
            return Task.FromResult(CreateReceipt(
                request,
                TcpIntegrationCancellationStatus.NotFound,
                idempotent: true));
        }

        if (runCancellation.IsCancellationRequested)
        {
            return Task.FromResult(CreateReceipt(
                request,
                TcpIntegrationCancellationStatus.AlreadyCancelled,
                idempotent: true));
        }

        runCancellation.Cancel();
        return Task.FromResult(CreateReceipt(
            request,
            TcpIntegrationCancellationStatus.Accepted,
            idempotent: false));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        foreach (var run in activeRuns.Values)
        {
            run.Cancel();
        }

        activeRuns.Clear();
    }

    private static TcpIntegrationCancellationReceipt CreateReceipt(
        IntegrationCancelRequestV2 request,
        TcpIntegrationCancellationStatus status,
        bool idempotent) =>
        new(
            request.RequestId,
            request.TransactionId,
            status,
            idempotent,
            IntegrationApplicationIds.ThreeDStudio);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed != 0, this);
}

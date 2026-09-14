using System.Net;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.Integration.Transport.Tcp;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

public sealed record ThreeDIntegrationTcpSequence(
    IntegrationHandoffV2 Handoff,
    IntegrationAcknowledgementV2? Acknowledgement,
    IntegrationResultV2? Result);

/// <summary>
/// Compatibility facade for the former combined TCP/file exchange surface.
/// Transport and local transaction sequence policy live in explicit owners.
/// </summary>
public sealed class ThreeDIntegrationTcpExchange : IAsyncDisposable
{
    private readonly ThreeDIntegrationTcpTransport transport;

    public ThreeDIntegrationTcpExchange(
        string exchangeRoot,
        ReadOnlySpan<byte> sharedKey,
        TcpIntegrationOptions? options = null)
    {
        transport = new ThreeDIntegrationTcpTransport(exchangeRoot, sharedKey, options);
    }

    public string ExchangeRoot => transport.ExchangeRoot;

    public IPEndPoint? LocalEndpoint => transport.LocalEndpoint;

    public Task<IPEndPoint> StartListeningAsync(
        IPAddress listenAddress,
        int port,
        CancellationToken cancellationToken = default) =>
        transport.StartListeningAsync(listenAddress, port, cancellationToken);

    public Task StopListeningAsync(CancellationToken cancellationToken = default) =>
        transport.StopListeningAsync(cancellationToken);

    public Task<TcpIntegrationTransferReceipt> PingAsync(
        TcpIntegrationEndpoint peer,
        CancellationToken cancellationToken = default) =>
        transport.PingAsync(peer, cancellationToken);

    public Task<TcpIntegrationTransferReceipt> PushTransactionAsync(
        TcpIntegrationEndpoint peer,
        Guid transactionId,
        CancellationToken cancellationToken = default) =>
        transport.PushTransactionAsync(peer, transactionId, cancellationToken);

    public Task<TcpIntegrationTransferReceipt> PullTransactionAsync(
        TcpIntegrationEndpoint peer,
        Guid transactionId,
        CancellationToken cancellationToken = default) =>
        transport.PullTransactionAsync(peer, transactionId, cancellationToken);

    public static ThreeDIntegrationTcpSequence ReadValidatedV2Sequence(
        string exchangeRoot,
        Guid transactionId) =>
        ThreeDIntegrationTransactionSequenceReader.ReadValidatedV2Sequence(
            exchangeRoot,
            transactionId);

    public ValueTask DisposeAsync() => transport.DisposeAsync();
}

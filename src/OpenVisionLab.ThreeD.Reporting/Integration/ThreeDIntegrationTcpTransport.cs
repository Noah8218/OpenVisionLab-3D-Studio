using System.Net;
using System.Security.Cryptography;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.Integration.Transport.Tcp;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

/// <summary>
/// Owns the 3D-side TCP server/client lifetime and transfer operations.
/// Local handoff file validation belongs to ThreeDIntegrationTransactionSequenceReader.
/// </summary>
public sealed class ThreeDIntegrationTcpTransport : IAsyncDisposable
{
    private readonly byte[] sharedKey;
    private readonly TcpIntegrationOptions options;
    private TcpIntegrationServer? server;
    private bool disposed;

    public ThreeDIntegrationTcpTransport(
        string exchangeRoot,
        ReadOnlySpan<byte> sharedKey,
        TcpIntegrationOptions? options = null)
    {
        ExchangeRoot = Path.GetFullPath(
            string.IsNullOrWhiteSpace(exchangeRoot)
                ? throw new ArgumentException("An exchange root is required.", nameof(exchangeRoot))
                : exchangeRoot.Trim());
        if (sharedKey.Length < 32)
        {
            throw new ArgumentException(
                "The TCP integration shared key must contain at least 32 bytes.",
                nameof(sharedKey));
        }

        this.sharedKey = sharedKey.ToArray();
        this.options = options ?? new TcpIntegrationOptions();
    }

    public string ExchangeRoot { get; }

    public IPEndPoint? LocalEndpoint => server?.LocalEndpoint;

    public async Task<IPEndPoint> StartListeningAsync(
        IPAddress listenAddress,
        int port,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(listenAddress);
        if (server is not null)
        {
            throw new InvalidOperationException("The 3D TCP integration listener is already started.");
        }

        var nextServer = new TcpIntegrationServer(
            IntegrationApplicationIds.ThreeDStudio,
            ExchangeRoot,
            listenAddress,
            port,
            sharedKey,
            options);
        try
        {
            await nextServer.StartAsync(cancellationToken).ConfigureAwait(false);
            server = nextServer;
            return nextServer.LocalEndpoint
                ?? throw new InvalidOperationException("The 3D TCP integration listener has no local endpoint.");
        }
        catch
        {
            await nextServer.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        var currentServer = server;
        if (currentServer is null)
        {
            return;
        }

        server = null;
        try
        {
            await currentServer.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await currentServer.DisposeAsync().ConfigureAwait(false);
        }
    }

    public Task<TcpIntegrationTransferReceipt> PingAsync(
        TcpIntegrationEndpoint peer,
        CancellationToken cancellationToken = default) =>
        ExecuteClientAsync(peer, (client, token) => client.PingAsync(token), cancellationToken);

    public Task<TcpIntegrationTransferReceipt> PushTransactionAsync(
        TcpIntegrationEndpoint peer,
        Guid transactionId,
        CancellationToken cancellationToken = default) =>
        ExecuteClientAsync(
            peer,
            (client, token) => client.PushTransactionAsync(ExchangeRoot, transactionId, token),
            cancellationToken);

    public Task<TcpIntegrationTransferReceipt> PullTransactionAsync(
        TcpIntegrationEndpoint peer,
        Guid transactionId,
        CancellationToken cancellationToken = default) =>
        ExecuteClientAsync(
            peer,
            (client, token) => client.PullTransactionAsync(ExchangeRoot, transactionId, token),
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            await StopListeningAsync().ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(sharedKey);
            disposed = true;
        }
    }

    private async Task<TcpIntegrationTransferReceipt> ExecuteClientAsync(
        TcpIntegrationEndpoint peer,
        Func<TcpIntegrationClient, CancellationToken, Task<TcpIntegrationTransferReceipt>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(peer);
        using var client = new TcpIntegrationClient(
            IntegrationApplicationIds.ThreeDStudio,
            peer,
            sharedKey,
            options);
        return await operation(client, cancellationToken).ConfigureAwait(false);
    }
}

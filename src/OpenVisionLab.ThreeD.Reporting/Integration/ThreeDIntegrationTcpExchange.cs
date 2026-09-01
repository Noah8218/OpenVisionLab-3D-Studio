using System.Net;
using System.Security.Cryptography;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.Integration.Transport.Tcp;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

public sealed record ThreeDIntegrationTcpSequence(
    IntegrationHandoffV2 Handoff,
    IntegrationAcknowledgementV2? Acknowledgement,
    IntegrationResultV2? Result);

/// <summary>
/// Owns explicit 3D-side TCP transfer while preserving the existing local
/// exchange root and file-first inspection lifecycle.
/// </summary>
public sealed class ThreeDIntegrationTcpExchange : IAsyncDisposable
{
    private readonly byte[] _sharedKey;
    private readonly TcpIntegrationOptions _options;
    private TcpIntegrationServer? _server;
    private bool _disposed;

    public ThreeDIntegrationTcpExchange(
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

        _sharedKey = sharedKey.ToArray();
        _options = options ?? new TcpIntegrationOptions();
    }

    public string ExchangeRoot { get; }

    public IPEndPoint? LocalEndpoint => _server?.LocalEndpoint;

    public async Task<IPEndPoint> StartListeningAsync(
        IPAddress listenAddress,
        int port,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(listenAddress);
        if (_server is not null)
        {
            throw new InvalidOperationException("The 3D TCP integration listener is already started.");
        }

        var server = new TcpIntegrationServer(
            IntegrationApplicationIds.ThreeDStudio,
            ExchangeRoot,
            listenAddress,
            port,
            _sharedKey,
            _options);
        try
        {
            await server.StartAsync(cancellationToken).ConfigureAwait(false);
            _server = server;
            return server.LocalEndpoint
                ?? throw new InvalidOperationException("The 3D TCP integration listener has no local endpoint.");
        }
        catch
        {
            await server.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopListeningAsync(CancellationToken cancellationToken = default)
    {
        var server = _server;
        if (server is null)
        {
            return;
        }

        _server = null;
        try
        {
            await server.StopAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await server.DisposeAsync().ConfigureAwait(false);
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

    /// <summary>
    /// Reads transport lifecycle state without claiming that the transaction
    /// is a 3D inspection request. This keeps cross-product ACK/Result status
    /// visible while the 3D inspection adapter remains fail-closed for 2D/Ai.
    /// </summary>
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            await StopListeningAsync().ConfigureAwait(false);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(_sharedKey);
            _disposed = true;
        }
    }

    private async Task<TcpIntegrationTransferReceipt> ExecuteClientAsync(
        TcpIntegrationEndpoint peer,
        Func<TcpIntegrationClient, CancellationToken, Task<TcpIntegrationTransferReceipt>> operation,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(peer);
        using var client = new TcpIntegrationClient(
            IntegrationApplicationIds.ThreeDStudio,
            peer,
            _sharedKey,
            _options);
        return await operation(client, cancellationToken).ConfigureAwait(false);
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

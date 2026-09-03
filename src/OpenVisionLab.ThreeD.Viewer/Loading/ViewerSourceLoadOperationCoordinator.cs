namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns the lifetime of one Viewer source mutation. A newer operation cancels
/// the previous one, while current checks prevent stale prepared data from
/// entering the View-owned source and render state.
/// </summary>
internal sealed class ViewerSourceLoadOperationCoordinator : IDisposable
{
    private readonly object gate = new();
    private ViewerSourceLoadOperation? current;
    private long nextGeneration;
    private bool disposed;

    public ViewerSourceLoadOperation Begin(CancellationToken externalCancellationToken = default)
    {
        ViewerSourceLoadOperation? previous;
        ViewerSourceLoadOperation operation;
        lock (gate)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ViewerSourceLoadOperationCoordinator));
            }

            previous = current;
            operation = new(this, unchecked(++nextGeneration), externalCancellationToken);
            current = operation;
        }

        previous?.Cancel();
        return operation;
    }

    public void CancelCurrent()
    {
        ViewerSourceLoadOperation? active;
        lock (gate)
        {
            active = current;
        }

        active?.Cancel();
    }

    internal bool IsCurrent(ViewerSourceLoadOperation operation)
    {
        lock (gate)
        {
            return !disposed && ReferenceEquals(current, operation);
        }
    }

    /// <summary>
    /// Serializes the current-operation check with the start of a View apply.
    /// The callback remains owned by the View; this owner only decides whether
    /// the operation is still allowed to enter that callback.
    /// </summary>
    internal bool TryApply(ViewerSourceLoadOperation operation, Action apply)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(apply);
        lock (gate)
        {
            if (disposed
                || !ReferenceEquals(current, operation)
                || operation.IsCancellationRequested)
            {
                return false;
            }

            apply();
            return true;
        }
    }

    internal void Complete(ViewerSourceLoadOperation operation)
    {
        lock (gate)
        {
            if (ReferenceEquals(current, operation))
            {
                current = null;
            }
        }

        operation.DisposeCancellation();
    }

    public void Dispose()
    {
        ViewerSourceLoadOperation? active;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            active = current;
            current = null;
        }

        // The operation owns the final cancellation-source disposal. The
        // async load may still be observing the token after cancellation, so
        // disposing it here would race with an older continuation.
        active?.Cancel();
    }
}

internal sealed class ViewerSourceLoadOperation : IDisposable
{
    private readonly ViewerSourceLoadOperationCoordinator owner;
    private readonly object cancellationGate = new();
    private readonly CancellationToken token;
    private readonly CancellationToken externalCancellationToken;
    private CancellationTokenSource? cancellation;

    internal ViewerSourceLoadOperation(
        ViewerSourceLoadOperationCoordinator owner,
        long generation,
        CancellationToken externalCancellationToken)
    {
        this.owner = owner;
        Generation = generation;
        this.externalCancellationToken = externalCancellationToken;
        cancellation = externalCancellationToken.CanBeCanceled
            ? CancellationTokenSource.CreateLinkedTokenSource(externalCancellationToken)
            : new CancellationTokenSource();
        token = cancellation.Token;
    }

    public long Generation { get; }

    public CancellationToken Token => token;

    public bool IsCancellationRequested => token.IsCancellationRequested;

    public bool IsExternalCancellationRequested =>
        externalCancellationToken.IsCancellationRequested;

    public bool IsCurrent => owner.IsCurrent(this);

    internal void Cancel()
    {
        lock (cancellationGate)
        {
            cancellation?.Cancel();
        }
    }

    internal void DisposeCancellation()
    {
        CancellationTokenSource? source;
        lock (cancellationGate)
        {
            source = Interlocked.Exchange(ref cancellation, null);
        }

        source?.Dispose();
    }

    public void Dispose() => owner.Complete(this);
}

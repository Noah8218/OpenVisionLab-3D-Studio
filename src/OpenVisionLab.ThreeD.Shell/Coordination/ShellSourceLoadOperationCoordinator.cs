namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Owns the lifetime of one active Shell source-load operation. Starting a
/// newer operation cancels the previous operation, while completion and
/// disposal remain safe when older continuations finish later.
/// </summary>
internal sealed class ShellSourceLoadOperationCoordinator : IDisposable
{
    private readonly object gate = new();
    private ShellSourceLoadOperation? current;
    private long nextGeneration;
    private bool disposed;

    public ShellSourceLoadOperation Begin()
    {
        ShellSourceLoadOperation? previous;
        ShellSourceLoadOperation operation;
        lock (gate)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(ShellSourceLoadOperationCoordinator));
            }

            previous = current;
            operation = new(this, unchecked(++nextGeneration));
            current = operation;
        }

        previous?.Cancel();
        return operation;
    }

    public void CancelCurrent()
    {
        ShellSourceLoadOperation? active;
        lock (gate)
        {
            active = current;
        }

        active?.Cancel();
    }

    internal bool IsCurrent(ShellSourceLoadOperation operation)
    {
        lock (gate)
        {
            return !disposed && ReferenceEquals(current, operation);
        }
    }

    internal void Complete(ShellSourceLoadOperation operation)
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
        ShellSourceLoadOperation? active;
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

internal sealed class ShellSourceLoadOperation : IDisposable
{
    private readonly ShellSourceLoadOperationCoordinator owner;
    private readonly object cancellationGate = new();
    private readonly CancellationToken token;
    private CancellationTokenSource? cancellation = new();

    internal ShellSourceLoadOperation(
        ShellSourceLoadOperationCoordinator owner,
        long generation)
    {
        this.owner = owner;
        Generation = generation;
        var source = new CancellationTokenSource();
        cancellation = source;
        token = source.Token;
    }

    public long Generation { get; }

    public CancellationToken Token => token;

    public bool IsCancellationRequested => token.IsCancellationRequested;

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

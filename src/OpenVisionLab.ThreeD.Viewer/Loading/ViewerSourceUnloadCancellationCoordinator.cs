using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Defers source-load cancellation until an Unloaded transition remains
/// unloaded. The View owns the lifecycle decision and cancellation callback;
/// this owner keeps one Dispatcher operation and its generation lifetime.
/// </summary>
internal sealed class ViewerSourceUnloadCancellationCoordinator : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly Func<bool> isDisposed;
    private readonly Func<bool> isLoaded;
    private readonly Action cancelSourceLoads;
    private readonly object operationGate = new();
    private DispatcherOperation? operation;
    private int generation;
    private bool disposed;

    public ViewerSourceUnloadCancellationCoordinator(
        Dispatcher dispatcher,
        Func<bool> isDisposed,
        Func<bool> isLoaded,
        Action cancelSourceLoads)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.isLoaded = isLoaded ?? throw new ArgumentNullException(nameof(isLoaded));
        this.cancelSourceLoads = cancelSourceLoads ?? throw new ArgumentNullException(nameof(cancelSourceLoads));
    }

    public void MarkLoaded()
    {
        unchecked
        {
            generation++;
        }

        CancelPendingOperation();
    }

    public void ScheduleAfterUnload()
    {
        if (IsUnavailable())
        {
            return;
        }

        var unloadGeneration = unchecked(++generation);
        CancelPendingOperation();
        if (IsUnavailable()
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            var scheduledOperation = dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() => Apply(unloadGeneration)));
            lock (operationGate)
            {
                operation = scheduledOperation;
            }
        }
        catch (InvalidOperationException)
        {
            lock (operationGate)
            {
                operation = null;
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        unchecked
        {
            generation++;
        }

        CancelPendingOperation();
        GC.SuppressFinalize(this);
    }

    private bool IsUnavailable() => disposed || isDisposed();

    private void Apply(int unloadGeneration)
    {
        ClearPendingOperation();
        if (IsUnavailable()
            || unloadGeneration != generation
            || isLoaded())
        {
            return;
        }

        cancelSourceLoads();
    }

    private void CancelPendingOperation()
    {
        DispatcherOperation? pendingOperation;
        lock (operationGate)
        {
            pendingOperation = operation;
            operation = null;
        }

        if (pendingOperation?.Status == DispatcherOperationStatus.Pending)
        {
            pendingOperation.Abort();
        }
    }

    private void ClearPendingOperation()
    {
        lock (operationGate)
        {
            operation = null;
        }
    }
}

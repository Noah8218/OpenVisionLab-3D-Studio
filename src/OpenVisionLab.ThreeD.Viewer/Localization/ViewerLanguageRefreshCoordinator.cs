using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Viewer.Localization;

/// <summary>
/// Owns cross-thread language-refresh scheduling for the Viewer. The View
/// supplies lifecycle and presentation callbacks; this owner keeps one queued
/// Dispatcher operation, generation invalidation, and cleanup together.
/// </summary>
internal sealed class ViewerLanguageRefreshCoordinator : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly Func<bool> isDisposed;
    private readonly Func<bool> isLoaded;
    private readonly Action refresh;
    private readonly object operationGate = new();
    private DispatcherOperation? operation;
    private int generation;
    private bool disposed;

    public ViewerLanguageRefreshCoordinator(
        Dispatcher dispatcher,
        Func<bool> isDisposed,
        Func<bool> isLoaded,
        Action refresh)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.isLoaded = isLoaded ?? throw new ArgumentNullException(nameof(isLoaded));
        this.refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
    }

    public void Request()
    {
        if (IsUnavailable())
        {
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            Queue();
            return;
        }

        if (!IsUnavailable())
        {
            refresh();
        }
    }

    public void Cancel()
    {
        unchecked
        {
            generation++;
        }

        CancelPendingOperation();
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

    private void Queue()
    {
        if (IsUnavailable()
            || dispatcher.HasShutdownStarted
            || dispatcher.HasShutdownFinished)
        {
            return;
        }

        lock (operationGate)
        {
            if (operation?.Status
                is DispatcherOperationStatus.Pending
                or DispatcherOperationStatus.Executing)
            {
                return;
            }

            var refreshGeneration = unchecked(++generation);
            try
            {
                operation = dispatcher.BeginInvoke(
                    DispatcherPriority.Normal,
                    new Action(() => Apply(refreshGeneration)));
            }
            catch (InvalidOperationException)
            {
                operation = null;
            }
        }
    }

    private void Apply(int refreshGeneration)
    {
        ClearPendingOperation();
        if (IsUnavailable()
            || refreshGeneration != generation
            || !isLoaded())
        {
            return;
        }

        refresh();
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

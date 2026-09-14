using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns Dispatcher scheduling for a visible Viewer frame. The control supplies
/// the WPF/OpenGL readiness and render callbacks; this owner keeps generations,
/// duplicate-request suppression, retry timing, and cleanup together.
/// </summary>
internal sealed class ViewerVisibleFrameRequestCoordinator : IDisposable
{
    private readonly Dispatcher dispatcher;
    private readonly Func<bool> isDisposed;
    private readonly Func<bool> canRender;
    private readonly Action render;
    private readonly object operationGate = new();
    private DispatcherTimer? retryTimer;
    private DispatcherOperation? operation;
    private int generation;
    private int retryGeneration;
    private int retryAttempt;
    private bool disposed;

    public ViewerVisibleFrameRequestCoordinator(
        Dispatcher dispatcher,
        Func<bool> isDisposed,
        Func<bool> canRender,
        Action render)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.canRender = canRender ?? throw new ArgumentNullException(nameof(canRender));
        this.render = render ?? throw new ArgumentNullException(nameof(render));
    }

    public int RequestCount { get; private set; }

    public void Request()
    {
        if (IsUnavailable())
        {
            return;
        }

        if (!dispatcher.CheckAccess())
        {
            if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                QueueRequest();
            }

            return;
        }

        if (IsUnavailable())
        {
            return;
        }

        var requestGeneration = unchecked(++generation);
        StopRetryTimer();
        RequestCore(requestGeneration, attempt: 0);
    }

    /// <summary>
    /// Invalidates pending work when the View leaves the visual tree. The
    /// View remains responsible for deciding when this lifecycle transition
    /// occurs; this owner only retires its scheduled callbacks and timer.
    /// </summary>
    public void Invalidate()
    {
        unchecked
        {
            generation++;
        }

        CancelPendingRequest();
        StopRetryTimer();
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

        CancelPendingRequest();
        StopRetryTimer();
        GC.SuppressFinalize(this);
    }

    private bool IsUnavailable() => disposed || isDisposed();

    private void RequestCore(int requestGeneration, int attempt)
    {
        CancelPendingRequest();
        if (IsUnavailable() || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            var scheduledOperation = dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() => ApplyRequest(requestGeneration, attempt)));
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

    private void QueueRequest()
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

            try
            {
                var queuedGeneration = generation;
                operation = dispatcher.BeginInvoke(
                    DispatcherPriority.Loaded,
                    new Action(() => ApplyQueuedRequest(queuedGeneration)));
            }
            catch (InvalidOperationException)
            {
                operation = null;
            }
        }
    }

    private void ApplyQueuedRequest(int queuedGeneration)
    {
        ClearPendingRequest();
        if (!IsUnavailable() && queuedGeneration == generation)
        {
            Request();
        }
    }

    private void ApplyRequest(int requestGeneration, int attempt)
    {
        ClearPendingRequest();
        if (IsUnavailable() || requestGeneration != generation)
        {
            return;
        }

        if (canRender())
        {
            render();
            RequestCount++;
        }

        if (attempt >= 2)
        {
            return;
        }

        StopRetryTimer();
        retryGeneration = requestGeneration;
        retryAttempt = attempt;
        retryTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(attempt == 0 ? 160 : 360)
        };
        retryTimer.Tick += OnRetryTimerTick;
        retryTimer.Start();
    }

    private void OnRetryTimerTick(object? sender, EventArgs args)
    {
        if (sender is not DispatcherTimer timer
            || !ReferenceEquals(timer, retryTimer))
        {
            return;
        }

        var requestGeneration = retryGeneration;
        var attempt = retryAttempt;
        StopRetryTimer(timer);
        if (IsUnavailable())
        {
            return;
        }

        RequestCore(requestGeneration, attempt + 1);
    }

    private void CancelPendingRequest()
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

    private void ClearPendingRequest()
    {
        lock (operationGate)
        {
            operation = null;
        }
    }

    private void StopRetryTimer(DispatcherTimer? expectedTimer = null)
    {
        var timer = retryTimer;
        if (timer is null
            || expectedTimer is not null && !ReferenceEquals(timer, expectedTimer))
        {
            return;
        }

        timer.Stop();
        timer.Tick -= OnRetryTimerTick;
        retryTimer = null;
        retryGeneration = 0;
        retryAttempt = 0;
    }
}

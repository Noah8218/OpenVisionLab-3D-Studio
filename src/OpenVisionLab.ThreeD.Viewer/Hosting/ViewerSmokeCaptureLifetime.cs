namespace OpenVisionLab.ThreeD.Viewer.Hosting;

/// <summary>
/// Owns the asynchronous lifetime of the command-line Smoke capture started by
/// the Viewer Loaded event. ViewerSmokeScenarioRunner owns the operation;
/// its native adapter retains Dispatcher, OpenGL and application access.
/// </summary>
internal sealed class ViewerSmokeCaptureLifetime : IDisposable
{
    private readonly CancellationToken lifetimeToken;
    private readonly Func<bool> isDisposed;
    private readonly Action<Exception> handleFailure;
    private readonly object sync = new();
    private Task? operationTask;
    private Task? observationTask;
    private bool operationStarting;
    private bool disposed;

    public ViewerSmokeCaptureLifetime(
        CancellationToken lifetimeToken,
        Func<bool> isDisposed,
        Action<Exception> handleFailure)
    {
        this.lifetimeToken = lifetimeToken;
        this.isDisposed = isDisposed ?? throw new ArgumentNullException(nameof(isDisposed));
        this.handleFailure = handleFailure ?? throw new ArgumentNullException(nameof(handleFailure));
    }

    public bool CanStart()
    {
        lock (sync)
        {
            return !disposed
                && !isDisposed()
                && !lifetimeToken.IsCancellationRequested
                && !operationStarting
                && (operationTask is null || operationTask.IsCompleted);
        }
    }

    // Capture before disposal when a host needs deterministic observation of
    // the already-started operation; disposal itself remains non-blocking.
    internal Task Completion
    {
        get { lock (sync) return observationTask ?? Task.CompletedTask; }
    }

    public bool Start(Func<Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        lock (sync)
        {
            if (disposed
                || isDisposed()
                || lifetimeToken.IsCancellationRequested
                || operationStarting
                || operationTask is { IsCompleted: false })
            {
                return false;
            }

            operationStarting = true;
        }

        Task task;
        try
        {
            task = operation();
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                operationStarting = false;
            }

            if (!isDisposed() && !lifetimeToken.IsCancellationRequested)
            {
                handleFailure(exception);
            }

            return false;
        }

        bool trackTask;
        lock (sync)
        {
            operationStarting = false;
            trackTask = !disposed;
            if (trackTask)
            {
                operationTask = task;
            }
        }

        var observer = ObserveAsync(task);
        if (trackTask)
        {
            lock (sync)
            {
                if (ReferenceEquals(operationTask, task))
                {
                    observationTask = observer;
                }
            }
        }

        return trackTask;
    }

    public void Dispose()
    {
        lock (sync)
        {
            disposed = true;
            operationTask = null;
            observationTask = null;
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (isDisposed() || lifetimeToken.IsCancellationRequested)
        {
            // Control disposal owns cancellation; no late Smoke projection is needed.
        }
        catch (Exception exception)
        {
            if (!isDisposed() && !lifetimeToken.IsCancellationRequested)
            {
                handleFailure(exception);
            }
        }
        finally
        {
            lock (sync)
            {
                if (ReferenceEquals(operationTask, task))
                {
                    operationTask = null;
                    observationTask = null;
                }
            }
        }
    }
}

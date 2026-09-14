using System.Threading;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Owns one asynchronous Shell Workbench request slot. The owner prevents
/// duplicate requests, cancels the request when the Window lifetime ends, and
/// suppresses failure callbacks after disposal.
/// </summary>
internal sealed class ShellWorkbenchRequestOwner : IDisposable
{
    private readonly object gate = new();
    private readonly Action<Exception> reportFailure;
    private CancellationTokenSource? cancellation = new();
    private Task? requestTask;
    private Task? observationTask;
    private int disposalState;

    public ShellWorkbenchRequestOwner(Action<Exception> reportFailure)
    {
        this.reportFailure = reportFailure;
    }

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool IsRunning => Volatile.Read(ref requestTask) is { IsCompleted: false };

    public CancellationToken Token => Volatile.Read(ref cancellation)?.Token ?? new CancellationToken(true);

    public bool TryStart(Func<CancellationToken, Task> request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Task? task = null;
        Exception? synchronousFailure = null;
        lock (gate)
        {
            if (Volatile.Read(ref disposalState) != 0
                || requestTask is { IsCompleted: false }
                || cancellation is null)
            {
                return false;
            }

            try
            {
                task = request(cancellation.Token);
            }
            catch (Exception exception)
            {
                synchronousFailure = exception;
            }

            if (task is not null)
            {
                Volatile.Write(ref requestTask, task);
            }
        }

        if (synchronousFailure is not null)
        {
            ReportFailure(synchronousFailure);
            return false;
        }

        if (task is not null)
        {
            var observer = ObserveAsync(task);
            lock (gate)
            {
                if (ReferenceEquals(requestTask, task))
                {
                    Volatile.Write(ref observationTask, observer);
                }
            }
        }

        return true;
    }

    public void Dispose()
    {
        CancellationTokenSource? sourceToCancel;
        CancellationTokenSource? sourceToDispose = null;
        lock (gate)
        {
            if (Interlocked.Exchange(ref disposalState, 1) != 0)
            {
                return;
            }

            sourceToCancel = cancellation;
            if (requestTask is null || requestTask.IsCompleted)
            {
                sourceToDispose = Interlocked.Exchange(ref cancellation, null);
                Volatile.Write(ref requestTask, null);
                Volatile.Write(ref observationTask, null);
            }
        }

        try
        {
            sourceToCancel?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrently completed request already retired the source.
        }
        sourceToDispose?.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the normal shutdown path for an active request.
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
        }
        finally
        {
            CancellationTokenSource? sourceToDispose = null;
            lock (gate)
            {
                if (ReferenceEquals(requestTask, task))
                {
                    Volatile.Write(ref requestTask, null);
                    Volatile.Write(ref observationTask, null);
                }

                if (Volatile.Read(ref disposalState) != 0 && requestTask is null)
                {
                    sourceToDispose = Interlocked.Exchange(ref cancellation, null);
                }
            }

            sourceToDispose?.Dispose();
        }
    }

    private void ReportFailure(Exception exception)
    {
        if (!IsDisposed)
        {
            reportFailure(exception);
        }
    }
}

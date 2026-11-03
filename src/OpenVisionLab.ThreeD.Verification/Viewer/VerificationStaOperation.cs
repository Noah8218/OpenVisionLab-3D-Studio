using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

/// <summary>
/// Runs an unshown verification owner on its STA. Cancellation requests work
/// to stop; only the owner thread may release it and shut down its Dispatcher.
/// A deadline never establishes that the owner or thread was released.
/// </summary>
internal static class VerificationStaOperation
{
    internal static VerificationStaObservation Run<T>(
        Func<T> create,
        Func<T, CancellationToken, Task> execute,
        Action<T> release,
        CancellationToken cancellationToken,
        TimeSpan workTimeout,
        TimeSpan cleanupTimeout)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(release);
        if (workTimeout <= TimeSpan.Zero || cleanupTimeout <= TimeSpan.Zero
            || cleanupTimeout.TotalMilliseconds > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(workTimeout));
        cancellationToken.ThrowIfCancellationRequested();

        var state = new VerificationStaState();
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try { workCancellation.CancelAfter(workTimeout); }
        catch { workCancellation.Dispose(); throw; }
        var workToken = workCancellation.Token;
        var thread = new Thread(() =>
        {
            state.ThreadId = Environment.CurrentManagedThreadId;
            try
            {
                var dispatcher = Dispatcher.CurrentDispatcher;
                SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(async () =>
                {
                    T? owner = null;
                    try
                    {
                        workToken.ThrowIfCancellationRequested();
                        owner = create();
                        state.ResourceCreated = true;
                        workToken.ThrowIfCancellationRequested();
                        state.BodyStarted = true;
                        var task = execute(owner, workToken);
                        state.BodyTask = task;
                        await task;
                        workToken.ThrowIfCancellationRequested();
                        state.BodyCompleted = true;
                    }
                    catch (Exception exception)
                    {
                        state.BodyFailure = exception;
                    }
                    finally
                    {
                        try
                        {
                            if (owner is not null)
                            {
                                state.CleanupStarted = true;
                                release(owner);
                            }
                            state.CleanupCompleted = true;
                        }
                        catch (Exception exception)
                        {
                            state.CleanupFailure = exception;
                        }
                        try { dispatcher.InvokeShutdown(); }
                        catch (Exception exception) { state.DispatcherFailure = exception; }
                    }
                }));
                Dispatcher.Run();
                state.DispatcherReturned = dispatcher.HasShutdownFinished;
            }
            catch (Exception exception)
            {
                state.DispatcherFailure = exception;
            }
            finally
            {
                try { workCancellation.Dispose(); state.CancellationReleased = true; }
                catch (Exception exception)
                {
                    state.CleanupFailure = state.CleanupFailure is null ? exception
                        : new AggregateException(state.CleanupFailure, exception);
                }
                completed.TrySetResult();
            }
        });
        state.Thread = thread;
        try { thread.SetApartmentState(ApartmentState.STA); thread.Start(); }
        catch { workCancellation.Dispose(); throw; }

        var canceledOrExpired = false;
        while (!completed.Task.Wait(millisecondsTimeout: 25))
        {
            if (workToken.IsCancellationRequested)
            {
                canceledOrExpired = true;
                break;
            }
        }

        // This grace bounds observation only. An unfinished thread remains
        // owned and observable; it is never killed, detached, or called Pass.
        var cleanupClock = Stopwatch.StartNew();
        try { completed.Task.WaitAsync(cleanupTimeout).GetAwaiter().GetResult(); }
        catch (TimeoutException)
        {
            throw new VerificationStaIncompleteException(state, completed.Task);
        }
        var remaining = cleanupTimeout - cleanupClock.Elapsed;
        if (!thread.Join(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero))
        {
            throw new VerificationStaIncompleteException(state, completed.Task);
        }

        var workWasCanceled = workToken.IsCancellationRequested;
        var observation = state.Observe();
        var failures = new[] { state.BodyFailure, state.CleanupFailure, state.DispatcherFailure }
            .Where(failure => failure is not null).Cast<Exception>().ToArray();
        if (state.CleanupFailure is not null || state.DispatcherFailure is not null)
        {
            var failure = new AggregateException("STA verification work or owner cleanup failed.", failures);
            failure.Data["VerificationStaObservation"] = observation;
            throw failure;
        }
        if (canceledOrExpired || cancellationToken.IsCancellationRequested || workWasCanceled)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                var failure = new OperationCanceledException("STA verification canceled after owner cleanup and thread exit.", state.BodyFailure, cancellationToken);
                failure.Data["VerificationStaObservation"] = observation;
                throw failure;
            }
            var timeout = new TimeoutException("STA verification work deadline expired; owner cleanup and thread exit were observed.", state.BodyFailure);
            timeout.Data["VerificationStaObservation"] = observation;
            throw timeout;
        }
        if (state.BodyFailure is not null)
        {
            state.BodyFailure.Data["VerificationStaObservation"] = observation;
            ExceptionDispatchInfo.Capture(state.BodyFailure).Throw();
        }
        return observation;

    }
}

internal sealed class VerificationStaState
{
    internal Thread? Thread;
    internal volatile int ThreadId;
    internal volatile bool ResourceCreated;
    internal volatile bool BodyStarted;
    internal volatile bool BodyCompleted;
    internal volatile bool CleanupStarted;
    internal volatile bool CleanupCompleted;
    internal volatile bool DispatcherReturned;
    internal volatile bool CancellationReleased;
    internal Task? BodyTask;
    internal Exception? BodyFailure;
    internal Exception? CleanupFailure;
    internal Exception? DispatcherFailure;

    internal VerificationStaObservation Observe() => new(
        ThreadId, ResourceCreated, BodyStarted, BodyCompleted,
        CleanupStarted, CleanupCompleted, DispatcherReturned,
        Thread is { IsAlive: false }, CancellationReleased,
        BodyTask is { IsCompleted: false } ? 1 : 0);
}

internal sealed record VerificationStaObservation(
    int ThreadId, bool ResourceCreated, bool BodyStarted, bool BodyCompleted,
    bool CleanupStarted, bool CleanupCompleted, bool DispatcherReturned,
    bool ThreadExited, bool CancellationReleased, int PendingBodyTasks);

internal sealed class VerificationStaIncompleteException : TimeoutException
{
    private readonly VerificationStaState state;
    private readonly Task completed;

    internal VerificationStaIncompleteException(VerificationStaState state, Task completed)
        : base("STA verification cleanup/thread exit was not confirmed; retained owner and fixture state must not be treated as released.")
    {
        this.state = state;
        this.completed = completed;
    }

    internal VerificationStaObservation Observation => state.Observe();

    // Used by the owning verifier process to observe eventual normal cleanup.
    // This only waits; it cannot cancel, abort, dispose from another thread,
    // remove a lock, or change the unfinished operation into a passing result.
    internal bool TryObserveExit(TimeSpan timeout)
    {
        if (timeout <= TimeSpan.Zero) return false;
        var clock = Stopwatch.StartNew();
        try { completed.WaitAsync(timeout).GetAwaiter().GetResult(); }
        catch (TimeoutException) { return false; }
        var remaining = timeout - clock.Elapsed;
        return state.Thread is { } thread
            && thread.Join(remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero);
    }
}

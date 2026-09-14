using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the cancellable execution lifetime shared by explicit Validation Set
/// Run, development revalidation, and Held-out replay.
/// </summary>
internal sealed class ToolWorkbenchValidationSetExecutionOwner : IDisposable
{
    private readonly Action onStateChanged;
    private CancellationTokenSource? cancellation;
    private Task? commandTask;
    private Task? commandObservationTask;
    private int commandInFlight;
    private int executionGate;
    private int disposalState;

    public ToolWorkbenchValidationSetExecutionOwner(Action onStateChanged)
    {
        this.onStateChanged = onStateChanged;
    }

    public bool IsRunning { get; private set; }

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool IsCommandRunning => !IsDisposed && Volatile.Read(ref commandInFlight) != 0;

    public bool CanStart => !IsDisposed && !IsRunning && !IsCommandRunning;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(ref cancellation, null);
        CancelAndDispose(currentCancellation);
        IsRunning = false;
        Volatile.Write(ref commandTask, null);
        Volatile.Write(ref commandObservationTask, null);
        Interlocked.Exchange(ref commandInFlight, 0);
    }

    public bool TryStartCommand(Func<Task> command, Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(reportFailure);

        if (IsDisposed
            || Interlocked.CompareExchange(ref commandInFlight, 1, 0) != 0)
        {
            return false;
        }

        Task task;
        try
        {
            task = command();
        }
        catch (Exception exception)
        {
            Interlocked.Exchange(ref commandInFlight, 0);
            if (!IsDisposed)
            {
                reportFailure(exception);
            }
            return false;
        }

        Volatile.Write(ref commandTask, task);
        Volatile.Write(
            ref commandObservationTask,
            ObserveCommandAsync(task, reportFailure));
        return true;
    }

    public async Task<ToolRecipeValidationSetResult> ExecuteAsync(
        ToolRecipeDocument document,
        IReadOnlyList<ToolRecipeValidationSampleInput> samples,
        IProgress<ToolRecipeValidationProgress> progress)
    {
        if (IsDisposed)
        {
            throw new OperationCanceledException();
        }

        // Validation Set commands normally serialize through CanExecute, but
        // threshold replay and verification callers can arrive concurrently.
        // Reject the second logical execution before it can replace the first
        // cancellation source or publish an early idle state.
        if (Interlocked.CompareExchange(ref executionGate, 1, 0) != 0)
        {
            throw new InvalidOperationException(
                "Validation Set execution is already running.");
        }

        try
        {
            if (IsDisposed)
            {
                throw new OperationCanceledException();
            }

            var currentCancellation = new CancellationTokenSource();
            var currentToken = currentCancellation.Token;
            var previousCancellation = Interlocked.Exchange(
                ref cancellation,
                currentCancellation);
            CancelAndDispose(previousCancellation);
            if (IsDisposed)
            {
                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref cancellation, null, currentCancellation),
                    currentCancellation))
                {
                    currentCancellation.Dispose();
                }

                throw new OperationCanceledException(currentToken);
            }

            SetRunning(true);
            return await Task.Run(
                () => ToolRecipeValidationSetExecution.Execute(
                    document,
                    samples,
                    currentToken,
                    progress),
                currentToken);
        }
        finally
        {
            var currentCancellation = Interlocked.Exchange(ref cancellation, null);
            if (currentCancellation is not null)
            {
                currentCancellation.Dispose();
            }

            SetRunning(false);
            Volatile.Write(ref executionGate, 0);
        }
    }

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref cancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        if (IsRunning == value)
        {
            return;
        }

        IsRunning = value;
        onStateChanged();
    }

    private async Task ObserveCommandAsync(
        Task task,
        Action<Exception> reportFailure)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Validation Set execution consumes expected cancellation; keep
            // command observation defensive for future execution changes.
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                reportFailure(exception);
            }
        }
        finally
        {
            if (ReferenceEquals(Volatile.Read(ref commandTask), task))
            {
                Volatile.Write(ref commandTask, null);
                Volatile.Write(ref commandObservationTask, null);
                Interlocked.Exchange(ref commandInFlight, 0);
            }
        }
    }

}

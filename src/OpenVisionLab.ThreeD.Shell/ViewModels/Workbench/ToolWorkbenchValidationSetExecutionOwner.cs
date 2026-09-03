using System.Threading;
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
    private int executionGate;
    private int disposalState;

    public ToolWorkbenchValidationSetExecutionOwner(Action onStateChanged)
    {
        this.onStateChanged = onStateChanged;
    }

    public bool IsRunning { get; private set; }

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public bool CanStart => !IsDisposed && !IsRunning;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(ref cancellation, null);
        CancelAndDispose(currentCancellation);
        IsRunning = false;
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

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }

        cancellation.Dispose();
    }
}

using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the cancellable execution lifetime shared by explicit Validation Set
/// Run, development revalidation, and Held-out replay.
/// </summary>
internal sealed class ToolWorkbenchValidationSetExecutionOwner
{
    private readonly Action onStateChanged;
    private CancellationTokenSource? cancellation;

    public ToolWorkbenchValidationSetExecutionOwner(Action onStateChanged)
    {
        this.onStateChanged = onStateChanged;
    }

    public bool IsRunning { get; private set; }

    public async Task<ToolRecipeValidationSetResult> ExecuteAsync(
        ToolRecipeDocument document,
        IReadOnlyList<ToolRecipeValidationSampleInput> samples,
        IProgress<ToolRecipeValidationProgress> progress)
    {
        cancellation?.Dispose();
        cancellation = new CancellationTokenSource();
        var currentCancellation = cancellation;
        SetRunning(true);
        try
        {
            return await Task.Run(() =>
                ToolRecipeValidationSetExecution.Execute(
                    document,
                    samples,
                    currentCancellation.Token,
                    progress));
        }
        finally
        {
            if (ReferenceEquals(cancellation, currentCancellation))
            {
                currentCancellation.Dispose();
                cancellation = null;
            }

            SetRunning(false);
        }
    }

    public void Cancel() => cancellation?.Cancel();

    private void SetRunning(bool value)
    {
        if (IsRunning == value)
        {
            return;
        }

        IsRunning = value;
        onStateChanged();
    }
}

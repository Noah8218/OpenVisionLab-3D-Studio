using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler<ToolWorkbenchTwoPointLineDisplayRequestEventArgs>? TwoPointLineDisplayRequested;
    public event EventHandler? TwoPointLineDisplayCleared;

    public bool IsSelectedStepTwoPointLine =>
        twoPointLineExecutionOwner.IsSelectedStepTwoPointLine;
    public bool IsTwoPointLinePreviewRunning =>
        twoPointLineExecutionOwner.IsPreviewRunning;
    public bool HasCurrentTwoPointLinePreview =>
        twoPointLineExecutionOwner.HasCurrentPreview;
    public bool IsTwoPointLinePreviewStale =>
        twoPointLineExecutionOwner.IsPreviewStale;
    public bool IsTwoPointLinePreviewPublished =>
        twoPointLineExecutionOwner.IsPreviewPublished;
    internal C3DTwoPointLineFeature? CurrentTwoPointLineOutput =>
        twoPointLineExecutionOwner.CurrentOutput;
    internal bool TryGetPublishedTwoPointLineOutput(string outputEntityId, out C3DTwoPointLineFeature? output) =>
        twoPointLineExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);
    internal bool TryGetPublishedLineGeometry(string outputEntityId, out IC3DLineGeometry? output)
    {
        if (TryGetPublishedTwoPointLineOutput(outputEntityId, out var picked) && picked is not null)
        {
            output = picked;
            return true;
        }
        if (TryGetPublishedLineFitOutput(outputEntityId, out var fitted) && fitted is not null)
        {
            output = fitted;
            return true;
        }
        output = null;
        return false;
    }

    public string TwoPointLineExecutionSummary =>
        twoPointLineExecutionOwner.ExecutionSummary;
    public string TwoPointLineOutputHashSummary =>
        twoPointLineExecutionOwner.OutputHashSummary;
    public string TwoPointLineSelectionSummary =>
        twoPointLineExecutionOwner.SelectionSummary;

    public Task<bool> PreviewSelectedTwoPointLineAsync() =>
        twoPointLineExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedTwoPointLine()
        => twoPointLineExecutionOwner.CanPreview();

    private void PublishSelectedTwoPointLine()
        => twoPointLineExecutionOwner.Publish();

    private void CancelTwoPointLinePreview() =>
        twoPointLineExecutionOwner.Cancel();

    private void MarkTwoPointLinePreviewStaleIfNeeded(object? sender = null)
        => twoPointLineExecutionOwner.MarkStaleIfNeeded(sender);

    private void ClearTwoPointLinePreview(string summary)
        => twoPointLineExecutionOwner.Clear(summary);

    private void RefreshTwoPointLineExecutionState()
        => twoPointLineExecutionOwner.RefreshState();
}

public sealed class ToolWorkbenchTwoPointLineDisplayRequestEventArgs(C3DTwoPointLineFeature output, bool isPublished) : EventArgs
{
    public C3DTwoPointLineFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

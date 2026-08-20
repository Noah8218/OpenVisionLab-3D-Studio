using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler<ToolWorkbenchThreePointPlaneDisplayRequestEventArgs>? ThreePointPlaneDisplayRequested;
    public event EventHandler? ThreePointPlaneDisplayCleared;

    public bool IsSelectedStepThreePointPlane =>
        threePointPlaneExecutionOwner.IsSelectedStepThreePointPlane;
    public bool IsThreePointPlanePreviewRunning =>
        threePointPlaneExecutionOwner.IsPreviewRunning;
    public bool HasCurrentThreePointPlanePreview =>
        threePointPlaneExecutionOwner.HasCurrentPreview;
    public bool IsThreePointPlanePreviewStale =>
        threePointPlaneExecutionOwner.IsPreviewStale;
    public bool IsThreePointPlanePreviewPublished =>
        threePointPlaneExecutionOwner.IsPreviewPublished;
    internal C3DThreePointPlaneFeature? CurrentThreePointPlaneOutput =>
        threePointPlaneExecutionOwner.CurrentOutput;
    internal bool TryGetPublishedThreePointPlaneOutput(string outputEntityId, out C3DThreePointPlaneFeature? output) =>
        threePointPlaneExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);

    public string ThreePointPlaneExecutionSummary =>
        threePointPlaneExecutionOwner.ExecutionSummary;
    public string ThreePointPlaneOutputHashSummary =>
        threePointPlaneExecutionOwner.OutputHashSummary;
    public string ThreePointPlaneSelectionSummary =>
        threePointPlaneExecutionOwner.SelectionSummary;

    public Task<bool> PreviewSelectedThreePointPlaneAsync() =>
        threePointPlaneExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedThreePointPlane()
        => threePointPlaneExecutionOwner.CanPreview();

    private void PublishSelectedThreePointPlane()
        => threePointPlaneExecutionOwner.Publish();

    private void CancelThreePointPlanePreview() =>
        threePointPlaneExecutionOwner.Cancel();

    private void MarkThreePointPlanePreviewStaleIfNeeded(object? sender = null)
        => threePointPlaneExecutionOwner.MarkStaleIfNeeded(sender);

    private void ClearThreePointPlanePreview(string summary)
        => threePointPlaneExecutionOwner.Clear(summary);

    private void RefreshThreePointPlaneExecutionState()
        => threePointPlaneExecutionOwner.RefreshState();
}

public sealed class ToolWorkbenchThreePointPlaneDisplayRequestEventArgs(C3DThreePointPlaneFeature output, bool isPublished) : EventArgs
{
    public C3DThreePointPlaneFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

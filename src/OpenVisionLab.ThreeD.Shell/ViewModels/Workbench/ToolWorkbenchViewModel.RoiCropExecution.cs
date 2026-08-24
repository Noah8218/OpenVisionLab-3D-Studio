using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepRoiCrop => roiCropExecutionOwner.IsSelected;
    public bool IsRoiCropPreviewRunning => roiCropExecutionOwner.IsRunning;
    public bool HasCurrentRoiCropPreview => roiCropExecutionOwner.HasCurrentPreview;
    public bool IsRoiCropPreviewStale => roiCropExecutionOwner.IsStale;
    public bool IsRoiCropPreviewPublished => roiCropExecutionOwner.IsPublished;
    public C3DHeightFieldSnapshot? CurrentRoiCropPreviewOutput => roiCropExecutionOwner.CurrentOutput;
    public ToolRecipeGridRectangle? CurrentRoiCropRegion => roiCropExecutionOwner.CurrentRegion;
    public string? CurrentRoiCropPreviewPath => roiCropExecutionOwner.CurrentPreviewPath;
    public string RoiCropExecutionSummary => roiCropExecutionOwner.Summary;
    public string RoiCropRegionSummary => roiCropExecutionOwner.RegionSummary;
    public string RoiCropOutputSummary => roiCropExecutionOwner.OutputSummary;

    public Task<bool> PreviewSelectedRoiCropAsync() => roiCropExecutionOwner.PreviewAsync();
    internal bool TryGetPublishedRoiCropOutput(string outputEntityId, out C3DHeightFieldSnapshot? output) =>
        roiCropExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);
    private bool CanPreviewSelectedRoiCrop() => roiCropExecutionOwner.CanPreview();
    private void PublishSelectedRoiCrop() => roiCropExecutionOwner.Publish();
    private void CancelRoiCropPreview() => roiCropExecutionOwner.Cancel();
    private void MarkRoiCropPreviewStaleIfNeeded(object? sender)
    {
        var outputEntityId = CurrentRoiCropPreviewOutput?.EntityId;
        roiCropExecutionOwner.MarkStaleIfNeeded(sender);
        if (IsRoiCropPreviewStale)
        {
            heightMeasurementExecutionOwner.MarkInputStaleIfNeeded(outputEntityId);
        }
    }
    private void ClearRoiCropPreview(string summary) => roiCropExecutionOwner.Clear(summary);
    private void RefreshRoiCropExecutionState() => roiCropExecutionOwner.Refresh();
}

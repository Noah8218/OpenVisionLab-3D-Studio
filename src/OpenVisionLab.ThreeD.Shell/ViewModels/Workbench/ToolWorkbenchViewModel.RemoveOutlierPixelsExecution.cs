using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepRemoveOutlierPixels => removeOutlierExecutionOwner.IsSelectedStepRemoveOutlierPixels;
    public bool IsRemoveOutlierPreviewRunning => removeOutlierExecutionOwner.IsRemoveOutlierPreviewRunning;
    public bool HasCurrentRemoveOutlierPreview => removeOutlierExecutionOwner.HasCurrentRemoveOutlierPreview;
    public bool IsRemoveOutlierPreviewStale => removeOutlierExecutionOwner.IsRemoveOutlierPreviewStale;
    public bool IsRemoveOutlierPreviewPublished => removeOutlierExecutionOwner.IsRemoveOutlierPreviewPublished;
    public C3DHeightFieldSnapshot? CurrentRemoveOutlierPreviewOutput => removeOutlierExecutionOwner.CurrentRemoveOutlierPreviewOutput;
    public C3DOutlierCellMap? CurrentRemoveOutlierMask => removeOutlierExecutionOwner.CurrentRemoveOutlierMask;
    public string? CurrentRemoveOutlierPreviewPath => removeOutlierExecutionOwner.CurrentRemoveOutlierPreviewPath;
    public string RemoveOutlierExecutionSummary => removeOutlierExecutionOwner.RemoveOutlierExecutionSummary;
    public string RemoveOutlierRuleSummary => removeOutlierExecutionOwner.RemoveOutlierRuleSummary;
    public string RemoveOutlierOutputSummary => removeOutlierExecutionOwner.RemoveOutlierOutputSummary;
    public string RemoveOutlierMaskSummary => removeOutlierExecutionOwner.RemoveOutlierMaskSummary;

    public Task<bool> PreviewSelectedRemoveOutlierPixelsAsync() =>
        removeOutlierExecutionOwner.PreviewSelectedRemoveOutlierPixelsAsync();

    private bool CanPreviewSelectedRemoveOutlierPixels() =>
        removeOutlierExecutionOwner.CanPreviewSelectedRemoveOutlierPixels();

    private void PublishSelectedRemoveOutlierPixels() =>
        removeOutlierExecutionOwner.PublishSelectedRemoveOutlierPixels();

    private void CancelRemoveOutlierPreview() =>
        removeOutlierExecutionOwner.CancelRemoveOutlierPreview();

    private void MarkRemoveOutlierPreviewStaleIfNeeded(object? sender) =>
        removeOutlierExecutionOwner.MarkRemoveOutlierPreviewStaleIfNeeded(sender);

    private void ClearRemoveOutlierPreview(string summary) =>
        removeOutlierExecutionOwner.ClearRemoveOutlierPreview(summary);

    private void RefreshRemoveOutlierExecutionState() =>
        removeOutlierExecutionOwner.RefreshRemoveOutlierExecutionState();

    private void SetRemoveOutlierRunning(bool value) =>
        removeOutlierExecutionOwner.SetRemoveOutlierRunning(value);
}

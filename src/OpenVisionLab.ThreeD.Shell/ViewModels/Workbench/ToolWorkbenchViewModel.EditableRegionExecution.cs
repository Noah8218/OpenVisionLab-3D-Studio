using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepEditableRegion => editableRegionExecutionOwner.IsSelectedStepEditableRegion;
    public bool IsEditableRegionPreviewRunning => editableRegionExecutionOwner.IsPreviewRunning;
    public bool HasCurrentEditableRegionPreview => editableRegionExecutionOwner.HasCurrentPreview;
    public bool IsEditableRegionPreviewStale => editableRegionExecutionOwner.IsPreviewStale;
    public bool IsEditableRegionPreviewPublished => editableRegionExecutionOwner.IsPreviewPublished;
    public C3DEditableRegionArtifact? CurrentEditableRegionArtifact => editableRegionExecutionOwner.CurrentArtifact;
    public string? CurrentEditableRegionArtifactPath => editableRegionExecutionOwner.CurrentArtifactPath;
    public string EditableRegionExecutionSummary => editableRegionExecutionOwner.ExecutionSummary;

    public Task<bool> PreviewSelectedEditableRegionAsync() =>
        editableRegionExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedEditableRegion() =>
        editableRegionExecutionOwner.CanPreview();

    private void PublishSelectedEditableRegion() =>
        editableRegionExecutionOwner.Publish();

    private void CancelEditableRegionPreview() =>
        editableRegionExecutionOwner.Cancel();

    private void MarkEditableRegionPreviewStaleIfNeeded(object? sender) =>
        editableRegionExecutionOwner.MarkStaleIfNeeded(sender);

    private void MarkEditableRegionPreviewStaleIfUpstreamChanged() =>
        editableRegionExecutionOwner.MarkStaleIfUpstreamChanged();

    private void ClearEditableRegionPreview(string summary) =>
        editableRegionExecutionOwner.Clear(summary);

    private void RefreshEditableRegionExecutionState() =>
        editableRegionExecutionOwner.RefreshState();
}

using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepConnectedRegion => connectedRegionExecutionOwner.IsSelectedStepConnectedRegion;
    public bool IsConnectedRegionPreviewRunning => connectedRegionExecutionOwner.IsConnectedRegionPreviewRunning;
    public bool HasCurrentConnectedRegionPreview => connectedRegionExecutionOwner.HasCurrentConnectedRegionPreview;
    public bool IsConnectedRegionPreviewStale => connectedRegionExecutionOwner.IsConnectedRegionPreviewStale;
    public bool IsConnectedRegionPreviewPublished => connectedRegionExecutionOwner.IsConnectedRegionPreviewPublished;
    public C3DConnectedRegionArtifact? CurrentConnectedRegionArtifact => connectedRegionExecutionOwner.CurrentConnectedRegionArtifact;
    public string? CurrentConnectedRegionArtifactPath => connectedRegionExecutionOwner.CurrentConnectedRegionArtifactPath;
    public string ConnectedRegionExecutionSummary => connectedRegionExecutionOwner.ConnectedRegionExecutionSummary;

    public Task<bool> PreviewSelectedConnectedRegionAsync() =>
        connectedRegionExecutionOwner.PreviewSelectedConnectedRegionAsync();

    private bool CanPreviewSelectedConnectedRegion() =>
        connectedRegionExecutionOwner.CanPreviewSelectedConnectedRegion();

    private void PublishSelectedConnectedRegion() =>
        connectedRegionExecutionOwner.PublishSelectedConnectedRegion();

    private void CancelConnectedRegionPreview() =>
        connectedRegionExecutionOwner.CancelConnectedRegionPreview();

    private void MarkConnectedRegionPreviewStaleIfNeeded(object? sender) =>
        connectedRegionExecutionOwner.MarkConnectedRegionPreviewStaleIfNeeded(sender);

    private void ClearConnectedRegionPreview(string summary) =>
        connectedRegionExecutionOwner.ClearConnectedRegionPreview(summary);

    private void RefreshConnectedRegionExecutionState() =>
        connectedRegionExecutionOwner.RefreshConnectedRegionExecutionState();

    private void SetConnectedRegionRunning(bool value) =>
        connectedRegionExecutionOwner.SetConnectedRegionRunning(value);
}

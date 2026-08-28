using OpenVisionLab.ThreeD.Core;
using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepLevelSurface => levelSurfaceExecutionOwner.IsSelectedStepLevelSurface;
    public bool IsLevelSurfacePreviewRunning => levelSurfaceExecutionOwner.IsLevelSurfacePreviewRunning;
    public bool HasCurrentLevelSurfacePreview => levelSurfaceExecutionOwner.HasCurrentLevelSurfacePreview;
    public bool IsLevelSurfacePreviewStale => levelSurfaceExecutionOwner.IsLevelSurfacePreviewStale;
    public bool IsLevelSurfacePreviewPublished => levelSurfaceExecutionOwner.IsLevelSurfacePreviewPublished;
    public C3DHeightFieldSnapshot? CurrentLevelSurfacePreviewOutput => levelSurfaceExecutionOwner.CurrentLevelSurfacePreviewOutput;
    public C3DLevelingTransform? CurrentLevelSurfaceTransform => levelSurfaceExecutionOwner.CurrentLevelSurfaceTransform;
    public C3DLevelFrameArtifact? CurrentLevelSurfaceLevelFrame => levelSurfaceExecutionOwner.CurrentLevelSurfaceLevelFrame;
    public C3DLevelFrameQualityEvidence? CurrentLevelSurfaceQualityEvidence => levelSurfaceExecutionOwner.CurrentLevelSurfaceQualityEvidence;
    public C3DLevelSurfaceCoordinateFrameChain? CurrentLevelSurfaceFrameChain => levelSurfaceExecutionOwner.CurrentLevelSurfaceFrameChain;
    public double CurrentLevelSurfaceOutputSlopeX => levelSurfaceExecutionOwner.CurrentLevelSurfaceOutputSlopeX;
    public double CurrentLevelSurfaceOutputSlopeZ => levelSurfaceExecutionOwner.CurrentLevelSurfaceOutputSlopeZ;
    public string? CurrentLevelSurfacePreviewPath => levelSurfaceExecutionOwner.CurrentLevelSurfacePreviewPath;
    public string LevelSurfaceExecutionSummary => levelSurfaceExecutionOwner.LevelSurfaceExecutionSummary;
    public string LevelSurfaceReferenceSummary => levelSurfaceExecutionOwner.LevelSurfaceReferenceSummary;
    public string LevelSurfaceTransformSummary => levelSurfaceExecutionOwner.LevelSurfaceTransformSummary;
    public string LevelSurfaceFrameSummary => levelSurfaceExecutionOwner.LevelSurfaceFrameSummary;
    public string LevelSurfaceFrameChainSummary => levelSurfaceExecutionOwner.LevelSurfaceFrameChainSummary;
    public string LevelSurfaceResidualSummary => levelSurfaceExecutionOwner.LevelSurfaceResidualSummary;
    public string LevelSurfaceOutputSummary => levelSurfaceExecutionOwner.LevelSurfaceOutputSummary;

    public Task<bool> PreviewSelectedLevelSurfaceAsync() =>
        levelSurfaceExecutionOwner.PreviewSelectedLevelSurfaceAsync();

    private bool CanPreviewSelectedLevelSurface() =>
        levelSurfaceExecutionOwner.CanPreviewSelectedLevelSurface();

    private void PublishSelectedLevelSurface() =>
        levelSurfaceExecutionOwner.PublishSelectedLevelSurface();

    private void CancelLevelSurfacePreview() =>
        levelSurfaceExecutionOwner.CancelLevelSurfacePreview();

    private void MarkLevelSurfacePreviewStaleIfNeeded(object? sender) =>
        levelSurfaceExecutionOwner.MarkLevelSurfacePreviewStaleIfNeeded(sender);

    private void ClearLevelSurfacePreview(string summary) =>
        levelSurfaceExecutionOwner.ClearLevelSurfacePreview(summary);

    private void RefreshLevelSurfaceExecutionState() =>
        levelSurfaceExecutionOwner.RefreshLevelSurfaceExecutionState();

    private void SetLevelSurfaceRunning(bool value) =>
        levelSurfaceExecutionOwner.SetLevelSurfaceRunning(value);
}

using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Compatibility projection for XYZ Affine Solve bindings and shared commands.
/// The independent XYZ Affine execution owner holds the A1/A2 lifecycle.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepXYZAffineSolve => xyzAffineExecutionOwner.IsSelectedSolve;
    public bool IsAffineSolvePreviewRunning => xyzAffineExecutionOwner.IsSolveRunning;
    public bool HasCurrentAffineSolvePreview => xyzAffineExecutionOwner.HasCurrentSolvePreview;
    public bool IsAffineSolvePreviewStale => xyzAffineExecutionOwner.IsSolveStale;
    public bool IsAffineSolvePreviewPublished => xyzAffineExecutionOwner.IsSolvePublished;
    internal C3DAffineTransform3D? CurrentAffineSolveOutput =>
        xyzAffineExecutionOwner.CurrentSolveOutput;
    internal bool TryGetPublishedAffineSolveOutput(
        string outputEntityId,
        out C3DAffineTransform3D? output) =>
        xyzAffineExecutionOwner.TryGetPublishedSolveOutput(outputEntityId, out output);
    public string AffineSolveExecutionSummary => xyzAffineExecutionOwner.SolveSummary;
    public string AffineSolveOutputHashSummary => xyzAffineExecutionOwner.SolveOutputHashSummary;
    public string AffineSolveUpstreamSummary => xyzAffineExecutionOwner.SolveUpstreamSummary;
    public string AffineSolveEvidenceSummary => xyzAffineExecutionOwner.SolveEvidenceSummary;
    public string AffineSolveMatrixSummary => xyzAffineExecutionOwner.SolveMatrixSummary;

    public Task<bool> PreviewSelectedXYZAffineSolveAsync() =>
        xyzAffineExecutionOwner.PreviewSolveAsync();

    private bool CanPreviewSelectedXYZAffineSolve() =>
        xyzAffineExecutionOwner.CanPreviewSolve();

    private void PublishSelectedXYZAffineSolve() =>
        xyzAffineExecutionOwner.PublishSolve();

    private void CancelXYZAffineSolvePreview() =>
        xyzAffineExecutionOwner.CancelSolve();

    private void MarkAffineSolvePreviewStaleIfNeeded(object? sender = null) =>
        xyzAffineExecutionOwner.MarkSolveStaleIfNeeded(sender);

    private void ClearXYZAffineSolvePreview(string summary) =>
        xyzAffineExecutionOwner.ClearSolve(summary);

    private void RefreshXYZAffineSolveExecutionState() =>
        xyzAffineExecutionOwner.RefreshSolveState();
}

using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Compatibility projection for XYZ Affine Apply bindings, smoke setup, and
/// shared commands. The independent XYZ Affine owner holds execution state.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepXYZAffineApply => xyzAffineExecutionOwner.IsSelectedApply;
    public bool IsAffineApplyPreviewRunning => xyzAffineExecutionOwner.IsApplyRunning;
    public bool HasCurrentAffineApplyPreview => xyzAffineExecutionOwner.HasCurrentApplyPreview;
    public bool IsAffineApplyPreviewStale => xyzAffineExecutionOwner.IsApplyStale;
    public bool IsAffineApplyPreviewPublished => xyzAffineExecutionOwner.IsApplyPublished;
    internal C3DTransformedPointCloud? CurrentAffineApplyOutput =>
        xyzAffineExecutionOwner.CurrentApplyOutput;
    internal bool TryGetPublishedAffineApplyOutput(
        string outputEntityId,
        out C3DTransformedPointCloud? output) =>
        xyzAffineExecutionOwner.TryGetPublishedApplyOutput(outputEntityId, out output);
    internal bool TryRegisterSyntheticPublishedAffineApplyOutputForSmoke(
        C3DTransformedPointCloud output,
        out string message) =>
        xyzAffineExecutionOwner.TryRegisterSyntheticPublishedApplyOutput(output, out message);
    public string AffineApplyExecutionSummary => xyzAffineExecutionOwner.ApplySummary;
    public string AffineApplyOutputHashSummary => xyzAffineExecutionOwner.ApplyOutputHashSummary;
    public string AffineApplyUpstreamSummary => xyzAffineExecutionOwner.ApplyUpstreamSummary;
    public string AffineApplyEvidenceSummary => xyzAffineExecutionOwner.ApplyEvidenceSummary;

    public Task<bool> PreviewSelectedXYZAffineApplyAsync() =>
        xyzAffineExecutionOwner.PreviewApplyAsync();

    private bool CanPreviewSelectedXYZAffineApply() =>
        xyzAffineExecutionOwner.CanPreviewApply();

    private void PublishSelectedXYZAffineApply() =>
        xyzAffineExecutionOwner.PublishApply();

    private void CancelXYZAffineApplyPreview() =>
        xyzAffineExecutionOwner.CancelApply();

    private void ClearXYZAffineApplyPreview(string summary) =>
        xyzAffineExecutionOwner.ClearApply(summary);

    private void RefreshXYZAffineApplyExecutionState() =>
        xyzAffineExecutionOwner.RefreshApplyState();
}

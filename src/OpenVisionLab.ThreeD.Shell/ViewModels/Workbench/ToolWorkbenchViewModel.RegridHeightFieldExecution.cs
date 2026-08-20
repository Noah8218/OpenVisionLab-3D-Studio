using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepRegridHeightField =>
        regridHeightFieldExecutionOwner.IsSelectedStepRegridHeightField;
    public bool IsRegridHeightFieldPreviewRunning =>
        regridHeightFieldExecutionOwner.IsPreviewRunning;
    public bool HasCurrentRegridHeightFieldPreview =>
        regridHeightFieldExecutionOwner.HasCurrentPreview;
    public bool IsRegridHeightFieldPreviewStale =>
        regridHeightFieldExecutionOwner.IsPreviewStale;
    public bool IsRegridHeightFieldPreviewPublished =>
        regridHeightFieldExecutionOwner.IsPreviewPublished;
    internal C3DTransformedHeightField? CurrentRegridHeightFieldOutput =>
        regridHeightFieldExecutionOwner.CurrentOutput;
    internal bool TryGetPublishedRegridHeightFieldOutput(string outputEntityId, out C3DTransformedHeightField? output) =>
        regridHeightFieldExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);
    public string RegridHeightFieldExecutionSummary =>
        regridHeightFieldExecutionOwner.ExecutionSummary;
    public string RegridHeightFieldOutputHashSummary =>
        regridHeightFieldExecutionOwner.OutputHashSummary;
    public string RegridHeightFieldUpstreamSummary =>
        regridHeightFieldExecutionOwner.UpstreamSummary;
    public string RegridHeightFieldEvidenceSummary =>
        regridHeightFieldExecutionOwner.EvidenceSummary;
    private bool CanPublishRegridHeightFieldPreview =>
        regridHeightFieldExecutionOwner.CanPublish;

    public Task<bool> PreviewSelectedRegridHeightFieldAsync() =>
        regridHeightFieldExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedRegridHeightField()
        => regridHeightFieldExecutionOwner.CanPreview();

    private void PublishSelectedRegridHeightField()
        => regridHeightFieldExecutionOwner.Publish();

    private void CancelRegridHeightFieldPreview() =>
        regridHeightFieldExecutionOwner.Cancel();

    private void ClearRegridHeightFieldPreview(string summary)
        => regridHeightFieldExecutionOwner.Clear(summary);

    private void RefreshRegridHeightFieldExecutionState()
        => regridHeightFieldExecutionOwner.RefreshState();
}

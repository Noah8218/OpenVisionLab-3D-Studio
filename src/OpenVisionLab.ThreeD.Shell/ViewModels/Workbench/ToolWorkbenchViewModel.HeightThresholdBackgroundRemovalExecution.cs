using System.Threading.Tasks;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepHeightThresholdBackgroundRemoval =>
        heightThresholdExecutionOwner.IsSelectedStep;
    public bool IsHeightThresholdPreviewRunning =>
        heightThresholdExecutionOwner.IsPreviewRunning;
    public bool HasCurrentHeightThresholdPreview =>
        heightThresholdExecutionOwner.HasCurrentPreview;
    public bool IsHeightThresholdPreviewStale =>
        heightThresholdExecutionOwner.IsPreviewStale;
    public bool IsHeightThresholdPreviewPublished =>
        heightThresholdExecutionOwner.IsPreviewPublished;
    public C3DHeightFieldSnapshot? CurrentHeightThresholdPreviewOutput =>
        heightThresholdExecutionOwner.CurrentOutput;
    public C3DHeightThresholdBackgroundRemovalEvidence? CurrentHeightThresholdPreviewEvidence =>
        heightThresholdExecutionOwner.CurrentEvidence;
    public string? CurrentHeightThresholdPreviewPath =>
        heightThresholdExecutionOwner.CurrentOutputPath;
    public string HeightThresholdExecutionSummary =>
        heightThresholdExecutionOwner.ExecutionSummary;
    public string HeightThresholdRuleSummary =>
        heightThresholdExecutionOwner.RuleSummary;
    public string HeightThresholdOutputSummary =>
        heightThresholdExecutionOwner.OutputSummary;
    public string HeightThresholdEvidenceSummary =>
        heightThresholdExecutionOwner.EvidenceSummary;

    public Task<bool> PreviewSelectedHeightThresholdBackgroundRemovalAsync() =>
        heightThresholdExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedHeightThresholdBackgroundRemoval() =>
        heightThresholdExecutionOwner.CanPreview();

    private void PublishSelectedHeightThresholdBackgroundRemoval() =>
        heightThresholdExecutionOwner.Publish();

    private void CancelHeightThresholdPreview() =>
        heightThresholdExecutionOwner.Cancel();

    private void MarkHeightThresholdPreviewStaleIfNeeded(object? sender) =>
        heightThresholdExecutionOwner.MarkStaleIfNeeded(sender);

    private void ClearHeightThresholdPreview(string summary) =>
        heightThresholdExecutionOwner.Clear(summary);

    internal bool TryGetPublishedHeightThresholdOutput(
        string outputEntityId,
        out C3DHeightFieldSnapshot? output) =>
        heightThresholdExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);
}

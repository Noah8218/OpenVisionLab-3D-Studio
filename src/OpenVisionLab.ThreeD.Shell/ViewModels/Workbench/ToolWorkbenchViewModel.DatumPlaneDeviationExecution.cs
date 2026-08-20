using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler<ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs>? DatumPlaneDeviationDisplayRequested;
    public event EventHandler? DatumPlaneDeviationDisplayCleared;

    public bool IsSelectedStepDatumPlaneDeviation =>
        datumPlaneDeviationExecutionOwner.IsSelectedStepDatumPlaneDeviation;
    public bool IsDatumPlaneDeviationPreviewRunning =>
        datumPlaneDeviationExecutionOwner.IsPreviewRunning;
    public bool HasCurrentDatumPlaneDeviationPreview =>
        datumPlaneDeviationExecutionOwner.HasCurrentPreview;
    public bool IsDatumPlaneDeviationPreviewStale =>
        datumPlaneDeviationExecutionOwner.IsPreviewStale;
    public bool IsDatumPlaneDeviationPreviewPublished =>
        datumPlaneDeviationExecutionOwner.IsPreviewPublished;
    internal C3DDatumPlaneDeviationFeature? CurrentDatumPlaneDeviationOutput =>
        datumPlaneDeviationExecutionOwner.CurrentOutput;
    public string DatumPlaneDeviationExecutionSummary =>
        datumPlaneDeviationExecutionOwner.ExecutionSummary;
    public string DatumPlaneDeviationOutputHashSummary =>
        datumPlaneDeviationExecutionOwner.OutputHashSummary;
    public string DatumPlaneDeviationUpstreamSummary =>
        datumPlaneDeviationExecutionOwner.UpstreamSummary;
    public string DatumPlaneDeviationEvidenceSummary =>
        datumPlaneDeviationExecutionOwner.EvidenceSummary;

    internal bool TryGetPublishedDatumPlaneDeviationOutput(
        string outputEntityId,
        out C3DDatumPlaneDeviationFeature? output) =>
        datumPlaneDeviationExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);

    internal bool TryGetCurrentDatumPlaneDeviationInputs(
        out C3DThreePointPlaneFeature? plane,
        out ToolRecipeSelection? measurementSelection) =>
        datumPlaneDeviationExecutionOwner.TryGetCurrentInputs(out plane, out measurementSelection);

    public Task<bool> PreviewSelectedDatumPlaneDeviationAsync() =>
        datumPlaneDeviationExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedDatumPlaneDeviation() =>
        datumPlaneDeviationExecutionOwner.CanPreview();

    private void PublishSelectedDatumPlaneDeviation() =>
        datumPlaneDeviationExecutionOwner.Publish();

    private void CancelDatumPlaneDeviationPreview() =>
        datumPlaneDeviationExecutionOwner.Cancel();

    private void MarkDatumPlaneDeviationPreviewStaleIfNeeded(
        object? sender = null,
        string? upstreamPlaneOutputId = null) =>
        datumPlaneDeviationExecutionOwner.MarkStaleIfNeeded(sender, upstreamPlaneOutputId);

    private void ClearDatumPlaneDeviationPreview(string summary) =>
        datumPlaneDeviationExecutionOwner.Clear(summary);

    private void RefreshDatumPlaneDeviationExecutionState() =>
        datumPlaneDeviationExecutionOwner.RefreshState();
}

public sealed class ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(
    C3DThreePointPlaneFeature plane,
    ToolRecipeSelection measurementSelection,
    C3DDatumPlaneDeviationFeature output,
    bool isPublished) : EventArgs
{
    public C3DThreePointPlaneFeature Plane { get; } = plane;
    public ToolRecipeSelection MeasurementSelection { get; } = measurementSelection;
    public C3DDatumPlaneDeviationFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler<ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs>? LandmarkCorrespondenceDisplayRequested;
    public event EventHandler? LandmarkCorrespondenceDisplayCleared;

    public bool IsSelectedStepLandmarkCorrespondence =>
        landmarkCorrespondenceExecutionOwner.IsSelected;
    public bool IsLandmarkCorrespondencePreviewRunning =>
        landmarkCorrespondenceExecutionOwner.IsPreviewRunning;
    public bool HasCurrentLandmarkCorrespondencePreview =>
        landmarkCorrespondenceExecutionOwner.HasCurrentPreview;
    public bool IsLandmarkCorrespondencePreviewStale =>
        landmarkCorrespondenceExecutionOwner.IsPreviewStale;
    public bool IsLandmarkCorrespondencePreviewPublished =>
        landmarkCorrespondenceExecutionOwner.IsPreviewPublished;
    internal C3DLandmarkCorrespondenceSet? CurrentLandmarkCorrespondenceOutput =>
        landmarkCorrespondenceExecutionOwner.CurrentOutput;

    internal bool TryGetPublishedLandmarkCorrespondenceOutput(
        string outputEntityId,
        out C3DLandmarkCorrespondenceSet? output) =>
        landmarkCorrespondenceExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);

    internal bool TryRegisterSyntheticPublishedLandmarkCorrespondenceOutputForSmoke(
        C3DLandmarkCorrespondenceSet output,
        out string message) =>
        landmarkCorrespondenceExecutionOwner.TryRegisterSyntheticPublishedOutput(output, out message);

    internal bool TryGetCurrentLandmarkCorrespondenceInputs(
        out IReadOnlyList<C3DLineIntersectionFeature> anchors) =>
        landmarkCorrespondenceExecutionOwner.TryGetCurrentInputs(out anchors);

    public string LandmarkCorrespondenceExecutionSummary =>
        landmarkCorrespondenceExecutionOwner.ExecutionSummary;
    public string LandmarkCorrespondenceOutputHashSummary =>
        landmarkCorrespondenceExecutionOwner.OutputHashSummary;
    public string LandmarkCorrespondenceUpstreamSummary =>
        landmarkCorrespondenceExecutionOwner.UpstreamSummary;
    public string LandmarkCorrespondenceEvidenceSummary =>
        landmarkCorrespondenceExecutionOwner.EvidenceSummary;

    public Task<bool> PreviewSelectedLandmarkCorrespondenceAsync() =>
        landmarkCorrespondenceExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedLandmarkCorrespondence() =>
        landmarkCorrespondenceExecutionOwner.CanPreview();

    private void PublishSelectedLandmarkCorrespondence() =>
        landmarkCorrespondenceExecutionOwner.Publish();

    private void CancelLandmarkCorrespondencePreview() =>
        landmarkCorrespondenceExecutionOwner.Cancel();

    private void MarkLandmarkCorrespondencePreviewStaleIfNeeded(object? sender = null) =>
        landmarkCorrespondenceExecutionOwner.MarkStaleIfNeeded(sender);

    private void ClearLandmarkCorrespondencePreview(string summary) =>
        landmarkCorrespondenceExecutionOwner.Clear(summary);

    private void RefreshLandmarkCorrespondenceExecutionState() =>
        landmarkCorrespondenceExecutionOwner.RefreshState();
}

public sealed class ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs(
    IReadOnlyList<C3DLineIntersectionFeature> anchors,
    C3DLandmarkCorrespondenceSet output,
    bool isPublished) : EventArgs
{
    public IReadOnlyList<C3DLineIntersectionFeature> Anchors { get; } = anchors;
    public C3DLandmarkCorrespondenceSet Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

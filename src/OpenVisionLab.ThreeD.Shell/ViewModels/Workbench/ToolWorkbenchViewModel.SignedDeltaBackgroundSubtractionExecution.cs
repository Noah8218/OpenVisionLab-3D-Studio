using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public bool IsSelectedStepSignedDeltaBackgroundSubtraction => signedDeltaBackgroundExecutionOwner.IsSelectedStep;
    public bool IsSignedDeltaPreviewRunning => signedDeltaBackgroundExecutionOwner.IsPreviewRunning;
    public bool HasCurrentSignedDeltaPreview => signedDeltaBackgroundExecutionOwner.HasCurrentPreview;
    public bool IsSignedDeltaPreviewStale => signedDeltaBackgroundExecutionOwner.IsPreviewStale;
    public bool IsSignedDeltaPreviewPublished => signedDeltaBackgroundExecutionOwner.IsPreviewPublished;
    public C3DSignedHeightDeltaArtifact? CurrentSignedDeltaPreviewOutput => signedDeltaBackgroundExecutionOwner.CurrentOutput;
    public string SignedDeltaExecutionSummary => signedDeltaBackgroundExecutionOwner.ExecutionSummary;
    public string SignedDeltaOutputSummary => signedDeltaBackgroundExecutionOwner.OutputSummary;
    public string SignedDeltaEvidenceSummary => signedDeltaBackgroundExecutionOwner.EvidenceSummary;

    public Task<bool> PreviewSelectedSignedDeltaBackgroundSubtractionAsync() => signedDeltaBackgroundExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedSignedDeltaBackgroundSubtraction() => signedDeltaBackgroundExecutionOwner.CanPreview();

    private void PublishSelectedSignedDeltaBackgroundSubtraction() => signedDeltaBackgroundExecutionOwner.Publish();

    private void CancelSignedDeltaBackgroundSubtractionPreview() => signedDeltaBackgroundExecutionOwner.Cancel();

    private void MarkSignedDeltaBackgroundSubtractionPreviewStaleIfNeeded(object? sender) => signedDeltaBackgroundExecutionOwner.MarkStaleIfNeeded(sender);

    private void ClearSignedDeltaBackgroundSubtractionPreview(string summary) => signedDeltaBackgroundExecutionOwner.Clear(summary);

    private void RefreshSignedDeltaBackgroundSubtractionExecutionState() => signedDeltaBackgroundExecutionOwner.RefreshState();

    private void RefreshSignedDeltaBackgroundSubtractionStateFromOwner()
    {
        RebuildEntities();
        OnPropertyChanged(nameof(IsSelectedStepSignedDeltaBackgroundSubtraction));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(IsSignedDeltaPreviewRunning));
        OnPropertyChanged(nameof(SignedDeltaExecutionSummary));
        OnPropertyChanged(nameof(SignedDeltaOutputSummary));
        OnPropertyChanged(nameof(SignedDeltaEvidenceSummary));
        OnPropertyChanged(nameof(CurrentSignedDeltaPreviewOutput));
        OnPropertyChanged(nameof(HasCurrentSignedDeltaPreview));
        OnPropertyChanged(nameof(IsSignedDeltaPreviewStale));
        OnPropertyChanged(nameof(IsSignedDeltaPreviewPublished));
        RefreshFilterCommands();
        RefreshSelectedToolWorkspaceProjection();
    }

    internal bool TryGetPublishedSignedDeltaOutput(string entityId, out C3DSignedHeightDeltaArtifact? output) =>
        signedDeltaBackgroundExecutionOwner.TryGetPublishedOutput(entityId, out output);

    internal C3DSignedHeightDeltaArtifact? GetRenderableSignedDeltaArtifact(string? entityId)
    {
        var output = CurrentSignedDeltaPreviewOutput;
        return output is not null
               && !IsSignedDeltaPreviewStale
               && string.Equals(output.OutputEntityId, entityId, StringComparison.OrdinalIgnoreCase)
               && output.TryValidate(out _)
            ? output
            : null;
    }
}

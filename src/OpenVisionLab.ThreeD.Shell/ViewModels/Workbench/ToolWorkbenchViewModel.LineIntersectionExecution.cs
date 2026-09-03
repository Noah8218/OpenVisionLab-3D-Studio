using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler<ToolWorkbenchLineIntersectionDisplayRequestEventArgs>? LineIntersectionDisplayRequested;
    public event EventHandler? LineIntersectionDisplayCleared;

    public bool IsSelectedStepLineIntersection =>
        lineIntersectionExecutionOwner.IsSelectedStepLineIntersection;
    public bool IsLineIntersectionPreviewRunning =>
        lineIntersectionExecutionOwner.IsPreviewRunning;
    public bool HasCurrentLineIntersectionPreview =>
        lineIntersectionExecutionOwner.HasCurrentPreview;
    public bool IsLineIntersectionPreviewStale =>
        lineIntersectionExecutionOwner.IsPreviewStale;
    public bool IsLineIntersectionPreviewPublished =>
        lineIntersectionExecutionOwner.IsPreviewPublished;
    internal C3DLineIntersectionFeature? CurrentLineIntersectionOutput =>
        lineIntersectionExecutionOwner.CurrentOutput;

    internal bool TryGetPublishedLineIntersectionOutput(
        string outputEntityId,
        out C3DLineIntersectionFeature? output) =>
        lineIntersectionExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);

    internal bool TryRegisterSyntheticPublishedLineIntersectionOutputForSmoke(
        C3DLineIntersectionFeature output,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        if (lineIntersectionExecutionOwner.IsDisposed)
        {
            message = "Line Intersection execution owner has been disposed.";
            return false;
        }

        var isRoutedLandmarkInput = Selections
            .Where(selection => string.Equals(
                selection.Kind,
                ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
                StringComparison.Ordinal))
            .SelectMany(selection => selection.Rows ?? [])
            .Any(row => string.Equals(
                row.SourceEntityId,
                output.OutputEntityId,
                StringComparison.OrdinalIgnoreCase));
        if (!isRoutedLandmarkInput
            || !string.Equals(Source.Id, output.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || SourceSession.SourceBinding is not { } binding
            || !string.Equals(binding.ContentSha256, output.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Source.Unit, output.Unit, StringComparison.Ordinal)
            || !string.Equals(Source.FrameId, output.FrameId, StringComparison.Ordinal))
        {
            message = "Synthetic smoke CornerAnchor identity does not match a routed Landmark Correspondence row and the loaded recipe source.";
            return false;
        }

        lineIntersectionExecutionOwner.RegisterSyntheticPublishedOutput(output);
        if (lineIntersectionExecutionOwner.IsDisposed)
        {
            message = "Line Intersection execution owner has been disposed.";
            return false;
        }

        message = $"Synthetic Published CornerAnchor registered for smoke-only execution: {output.ContentSha256}";
        return true;
    }

    internal bool TryGetCurrentLineIntersectionInputs(
        out IC3DLineGeometry? first,
        out IC3DLineGeometry? second) =>
        lineIntersectionExecutionOwner.TryGetCurrentInputs(out first, out second);

    public string LineIntersectionExecutionSummary =>
        lineIntersectionExecutionOwner.ExecutionSummary;
    public string LineIntersectionOutputHashSummary =>
        lineIntersectionExecutionOwner.OutputHashSummary;
    public string LineIntersectionUpstreamSummary =>
        lineIntersectionExecutionOwner.UpstreamSummary;
    public string LineIntersectionEvidenceSummary =>
        lineIntersectionExecutionOwner.EvidenceSummary;

    public Task<bool> PreviewSelectedLineIntersectionAsync() =>
        lineIntersectionExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedLineIntersection() =>
        lineIntersectionExecutionOwner.CanPreview();

    private void PublishSelectedLineIntersection() =>
        lineIntersectionExecutionOwner.Publish();

    private void CancelLineIntersectionPreview() =>
        lineIntersectionExecutionOwner.Cancel();

    private void MarkLineIntersectionPreviewStaleIfNeeded(object? sender = null) =>
        lineIntersectionExecutionOwner.MarkStaleIfNeeded(sender);

    private void ClearLineIntersectionPreview(string summary) =>
        lineIntersectionExecutionOwner.Clear(summary);

    private void RefreshLineIntersectionExecutionState() =>
        lineIntersectionExecutionOwner.RefreshState();
}

public sealed class ToolWorkbenchLineIntersectionDisplayRequestEventArgs(
    IC3DLineGeometry firstLine,
    IC3DLineGeometry secondLine,
    C3DLineIntersectionFeature output,
    bool isPublished) : EventArgs
{
    public IC3DLineGeometry FirstLine { get; } = firstLine;
    public IC3DLineGeometry SecondLine { get; } = secondLine;
    public C3DLineIntersectionFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs>? HeightDifferenceEdgeDisplayRequested;

    public bool IsSelectedStepHeightDifferenceEdge =>
        heightDifferenceEdgeExecutionOwner.IsSelectedStepHeightDifferenceEdge;
    public bool IsEdgePreviewRunning => heightDifferenceEdgeExecutionOwner.IsPreviewRunning;
    public bool HasCurrentEdgePreview => heightDifferenceEdgeExecutionOwner.HasCurrentPreview;
    public bool IsEdgePreviewStale => heightDifferenceEdgeExecutionOwner.IsPreviewStale;
    public bool IsEdgePreviewPublished => heightDifferenceEdgeExecutionOwner.IsPreviewPublished;
    internal C3DHeightDifferenceEdgePointSet? CurrentHeightDifferenceEdgeOutput =>
        heightDifferenceEdgeExecutionOwner.CurrentOutput;
    internal bool TryGetPublishedHeightDifferenceEdgeOutput(
        string outputEntityId,
        out C3DHeightDifferenceEdgePointSet? output) =>
        heightDifferenceEdgeExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);
    public IReadOnlyList<string> HeightDifferenceEdgeComparisonAxisOptions =>
        heightDifferenceEdgeExecutionOwner.ComparisonAxisOptions;
    public IReadOnlyList<string> HeightDifferenceEdgePolarityOptions =>
        heightDifferenceEdgeExecutionOwner.EdgePolarityOptions;

    public string SelectedHeightDifferenceEdgeComparisonAxis
    {
        get => heightDifferenceEdgeExecutionOwner.SelectedComparisonAxis;
        set => heightDifferenceEdgeExecutionOwner.SelectedComparisonAxis = value;
    }

    public string SelectedHeightDifferenceEdgePolarity
    {
        get => heightDifferenceEdgeExecutionOwner.SelectedPolarity;
        set => heightDifferenceEdgeExecutionOwner.SelectedPolarity = value;
    }

    public string HeightDifferenceEdgeMinimumDelta
    {
        get => heightDifferenceEdgeExecutionOwner.MinimumDelta;
        set => heightDifferenceEdgeExecutionOwner.MinimumDelta = value;
    }

    public string HeightDifferenceEdgeExpectedOrientation =>
        heightDifferenceEdgeExecutionOwner.ExpectedOrientation;
    public string HeightDifferenceEdgeUpstreamSummary =>
        heightDifferenceEdgeExecutionOwner.UpstreamSummary;
    public string HeightDifferenceEdgeBandSummary =>
        heightDifferenceEdgeExecutionOwner.BandSummary;
    public string HeightDifferenceEdgeFixedPolicySummary =>
        "Strongest per scanline | lowest-index tie | adjacent-pair midpoint | SkipPair | WithinSelection";
    public string HeightDifferenceEdgeExecutionSummary =>
        heightDifferenceEdgeExecutionOwner.ExecutionSummary;
    public string HeightDifferenceEdgeOutputHashSummary =>
        heightDifferenceEdgeExecutionOwner.OutputHashSummary;

    public Task<bool> PreviewSelectedHeightDifferenceEdgeAsync() =>
        heightDifferenceEdgeExecutionOwner.PreviewAsync();

    public bool TryConfigureHeightDifferenceEdgeSmoke(
        string stepId,
        ToolRecipeGridRectangle rectangle,
        string comparisonAxis,
        string polarity,
        string minimumDelta,
        out string message)
    {
        var step = PipelineSteps.SingleOrDefault(item =>
            string.Equals(item.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (step is null || SourceSession.SourceBinding is null)
        {
            message = "Smoke Edge step or verified source binding is unavailable.";
            return false;
        }

        SelectedPipelineStep = step;
        var selection = new ToolRecipeSelection(
            $"selection.smoke.{NormalizeId(step.Id)}",
            "Smoke-only Edge search band",
            ToolRecipeSelectionKinds.GridRectangle,
            Source.Id,
            Source.FrameId,
            SourceSession.SourceBinding,
            rectangle,
            null,
            null);
        PersistSelectionForSelectedStep(selection);
        heightDifferenceEdgeExecutionOwner.SetParameter("ComparisonAxis", comparisonAxis);
        heightDifferenceEdgeExecutionOwner.SetParameter("Polarity", polarity);
        heightDifferenceEdgeExecutionOwner.SetParameter("MinimumDelta", minimumDelta);
        message = HeightDifferenceEdgeBandSummary;
        return true;
    }

    private bool CanPreviewSelectedHeightDifferenceEdge() =>
        heightDifferenceEdgeExecutionOwner.CanPreview();
    private void PublishSelectedHeightDifferenceEdge() =>
        heightDifferenceEdgeExecutionOwner.Publish();
    private void CancelHeightDifferenceEdgePreview() =>
        heightDifferenceEdgeExecutionOwner.Cancel();
    private void MarkHeightDifferenceEdgePreviewStaleIfNeeded(object? sender = null) =>
        heightDifferenceEdgeExecutionOwner.MarkStaleIfNeeded(sender);
    private void MarkHeightDifferenceEdgePreviewStale(string summary) =>
        heightDifferenceEdgeExecutionOwner.MarkStale(summary);
    private void ClearHeightDifferenceEdgePreview(string summary) =>
        heightDifferenceEdgeExecutionOwner.Clear(summary);
    private void RefreshHeightDifferenceEdgeExecutionState() =>
        heightDifferenceEdgeExecutionOwner.RefreshState();

    private void RefreshHeightDifferenceEdgeCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}

public sealed class ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs(
    string c3DPath,
    C3DHeightDifferenceEdgePointSet output,
    bool isPublished) : EventArgs
{
    public string C3DPath { get; } = c3DPath;
    public C3DHeightDifferenceEdgePointSet Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

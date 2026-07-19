using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? landmarkCorrespondencePreviewCancellation;
    private C3DLandmarkCorrespondenceSet? landmarkCorrespondencePreviewOutput;
    private readonly Dictionary<string, C3DLandmarkCorrespondenceSet> publishedLandmarkCorrespondenceOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isLandmarkCorrespondencePreviewRunning;
    private bool isLandmarkCorrespondencePreviewStale;
    private bool isLandmarkCorrespondencePreviewPublished;
    private string landmarkCorrespondenceExecutionSummary = "Teach four explicit CornerAnchor/reference pairs and the reference descriptor, then publish every named CornerAnchor before Preview.";

    public event EventHandler<ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs>? LandmarkCorrespondenceDisplayRequested;
    public event EventHandler? LandmarkCorrespondenceDisplayCleared;

    public bool IsSelectedStepLandmarkCorrespondence =>
        string.Equals(SelectedPipelineStep?.ToolId, "landmark-correspondence", StringComparison.Ordinal);
    public bool IsLandmarkCorrespondencePreviewRunning => isLandmarkCorrespondencePreviewRunning;
    public bool HasCurrentLandmarkCorrespondencePreview => landmarkCorrespondencePreviewOutput is not null && !isLandmarkCorrespondencePreviewStale;
    public bool IsLandmarkCorrespondencePreviewStale => isLandmarkCorrespondencePreviewStale;
    public bool IsLandmarkCorrespondencePreviewPublished => isLandmarkCorrespondencePreviewPublished;
    internal C3DLandmarkCorrespondenceSet? CurrentLandmarkCorrespondenceOutput => landmarkCorrespondencePreviewOutput;
    internal bool TryGetPublishedLandmarkCorrespondenceOutput(string outputEntityId, out C3DLandmarkCorrespondenceSet? output) =>
        publishedLandmarkCorrespondenceOutputs.TryGetValue(outputEntityId, out output);
    internal bool TryGetCurrentLandmarkCorrespondenceInputs(out IReadOnlyList<C3DLineIntersectionFeature> anchors)
    {
        anchors = [];
        if (!TryGetSelectedLandmarkCorrespondenceSelection(out var selection)) return false;
        var results = new List<C3DLineIntersectionFeature>();
        foreach (var row in selection.Rows ?? [])
        {
            if (!TryGetPublishedLineIntersectionOutput(row.SourceEntityId, out var anchor) || anchor is null) return false;
            results.Add(anchor);
        }
        anchors = results;
        return results.Count == 4;
    }

    public string LandmarkCorrespondenceExecutionSummary => landmarkCorrespondenceExecutionSummary;
    public string LandmarkCorrespondenceOutputHashSummary => landmarkCorrespondencePreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {landmarkCorrespondencePreviewOutput.ContentSha256}";
    public string LandmarkCorrespondenceUpstreamSummary
    {
        get
        {
            if (!TryGetSelectedLandmarkCorrespondenceSelection(out var selection))
            {
                return "One routed landmark-correspondence selection is required.";
            }
            var rows = selection.Rows ?? [];
            var states = rows.Select(row =>
                $"{row.SourceEntityId}: {(TryGetPublishedLineIntersectionOutput(row.SourceEntityId, out var output) && output is not null ? "Published" : "missing/stale")}");
            return $"{rows.Count}/4 authored rows | {string.Join(" | ", states)}";
        }
    }
    public string LandmarkCorrespondenceEvidenceSummary => landmarkCorrespondencePreviewOutput is null
        ? "No correspondence evidence until Preview completes."
        : $"{landmarkCorrespondencePreviewOutput.Pairs.Count} pairs | source rank {landmarkCorrespondencePreviewOutput.SourceRank}/4 | reference rank {landmarkCorrespondencePreviewOutput.ReferenceRank}/4 | normalized volume {landmarkCorrespondencePreviewOutput.SourceNormalizedTetrahedronVolume:G6} / {landmarkCorrespondencePreviewOutput.ReferenceNormalizedTetrahedronVolume:G6}";

    public async Task<bool> PreviewSelectedLandmarkCorrespondenceAsync()
    {
        if (!CanPreviewSelectedLandmarkCorrespondence() || SelectedPipelineStep is not { } step
            || !TryGetCurrentLandmarkCorrespondenceInputs(out var anchors)) return false;

        landmarkCorrespondencePreviewCancellation?.Dispose();
        landmarkCorrespondencePreviewCancellation = new CancellationTokenSource();
        SetLandmarkCorrespondenceRunning(true);
        isLandmarkCorrespondencePreviewStale = false;
        isLandmarkCorrespondencePreviewPublished = false;
        step.State = "Preview running";
        SetLandmarkCorrespondenceSummary("Landmark Correspondence Preview validates only the four exact Published CornerAnchors and explicit reference coordinates.");
        AppendLog("Preview", $"Landmark Correspondence Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeLandmarkCorrespondenceExecution.Execute(CreateDocument(), step.Id, anchors, landmarkCorrespondencePreviewCancellation.Token),
                landmarkCorrespondencePreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                landmarkCorrespondencePreviewOutput = null;
                step.State = "Error";
                SetLandmarkCorrespondenceSummary(evaluation.Result.Message);
                AppendLog("Error", $"Landmark Correspondence Preview failed: {evaluation.Result.Message}");
                return false;
            }

            landmarkCorrespondencePreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetLandmarkCorrespondenceSummary($"Preview ready | {LandmarkCorrespondenceEvidenceSummary} | no affine matrix or OK/NG");
            AppendLog("Preview", $"Landmark Correspondence Preview ready: {evaluation.Output.ContentSha256}.");
            LandmarkCorrespondenceDisplayRequested?.Invoke(this, new ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs(anchors, evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetLandmarkCorrespondenceSummary("Preview canceled. Published CornerAnchors and authored recipe were not changed.");
            AppendLog("Preview", "Landmark Correspondence Preview canceled.");
            return false;
        }
        finally
        {
            SetLandmarkCorrespondenceRunning(false);
        }
    }

    private bool CanPreviewSelectedLandmarkCorrespondence()
    {
        if (!IsSelectedStepLandmarkCorrespondence || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isLandmarkCorrespondencePreviewRunning || SelectedPipelineStep is not { } step
            || !TryGetCurrentLandmarkCorrespondenceInputs(out var anchors)) return false;
        return ToolRecipeLandmarkCorrespondenceExecution.TryPrepare(CreateDocument(), step.Id, anchors, out _, out _);
    }

    private void PublishSelectedLandmarkCorrespondence()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentLandmarkCorrespondencePreview) return;
        isLandmarkCorrespondencePreviewPublished = true;
        publishedLandmarkCorrespondenceOutputs[landmarkCorrespondencePreviewOutput!.OutputEntityId] = landmarkCorrespondencePreviewOutput;
        step.State = "Published";
        SetLandmarkCorrespondenceSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {landmarkCorrespondencePreviewOutput.ContentSha256} | correspondence evidence only, no affine matrix or OK/NG");
        AppendLog("Publish", $"Landmark Correspondence output published without re-running: {step.OutputEntityId}.");
        if (TryGetCurrentLandmarkCorrespondenceInputs(out var anchors))
        {
            LandmarkCorrespondenceDisplayRequested?.Invoke(this, new ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs(anchors, landmarkCorrespondencePreviewOutput, true));
        }
    }

    private void CancelLandmarkCorrespondencePreview() => landmarkCorrespondencePreviewCancellation?.Cancel();

    private void MarkLandmarkCorrespondencePreviewStaleIfNeeded(object? sender = null)
    {
        if (landmarkCorrespondencePreviewOutput is null || isLandmarkCorrespondencePreviewRunning) return;
        isLandmarkCorrespondencePreviewStale = true;
        isLandmarkCorrespondencePreviewPublished = false;
        publishedLandmarkCorrespondenceOutputs.Clear();
        var step = PipelineSteps.FirstOrDefault(item => string.Equals(item.OutputEntityId, landmarkCorrespondencePreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is not null) step.State = "Preview stale";
        LandmarkCorrespondenceDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetLandmarkCorrespondenceSummary("Recipe, correspondence row, descriptor, or published CornerAnchor changed. Preview again before Publish.");
    }

    private void ClearLandmarkCorrespondencePreview(string summary)
    {
        landmarkCorrespondencePreviewCancellation?.Cancel();
        landmarkCorrespondencePreviewOutput = null;
        publishedLandmarkCorrespondenceOutputs.Clear();
        isLandmarkCorrespondencePreviewStale = false;
        isLandmarkCorrespondencePreviewPublished = false;
        LandmarkCorrespondenceDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetLandmarkCorrespondenceSummary(summary);
    }

    private void RefreshLandmarkCorrespondenceExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepLandmarkCorrespondence));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(LandmarkCorrespondenceUpstreamSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepLandmarkCorrespondence
            && (landmarkCorrespondencePreviewOutput is null
                || !string.Equals(landmarkCorrespondencePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isLandmarkCorrespondencePreviewStale)
            && !isLandmarkCorrespondencePreviewRunning)
        {
            if (!TryGetCurrentLandmarkCorrespondenceInputs(out var anchors))
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeLandmarkCorrespondenceExecution.TryPrepare(CreateDocument(), step.Id, anchors, out _, out var message))
            {
                step.State = "Ready";
                landmarkCorrespondenceExecutionSummary = "Ready for explicit Preview. Line Intersection and Affine will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                landmarkCorrespondenceExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(LandmarkCorrespondenceExecutionSummary));
        OnPropertyChanged(nameof(LandmarkCorrespondenceOutputHashSummary));
        OnPropertyChanged(nameof(LandmarkCorrespondenceEvidenceSummary));
        RefreshLandmarkCorrespondenceCommands();
    }

    private bool TryGetSelectedLandmarkCorrespondenceSelection(out ToolRecipeSelection selection)
    {
        selection = null!;
        if (SelectedPipelineStep is not { InputEntityIds.Count: 1 } step) return false;
        var candidate = Selections.SingleOrDefault(item => string.Equals(item.Id, step.InputEntityIds[0], StringComparison.OrdinalIgnoreCase));
        if (candidate is null || !string.Equals(candidate.Kind, ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, StringComparison.Ordinal)) return false;
        selection = candidate;
        return true;
    }

    private void SetLandmarkCorrespondenceRunning(bool value)
    {
        isLandmarkCorrespondencePreviewRunning = value;
        OnPropertyChanged(nameof(IsLandmarkCorrespondencePreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshLandmarkCorrespondenceCommands();
    }

    private void SetLandmarkCorrespondenceSummary(string value)
    {
        landmarkCorrespondenceExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(LandmarkCorrespondenceExecutionSummary));
        OnPropertyChanged(nameof(LandmarkCorrespondenceOutputHashSummary));
        OnPropertyChanged(nameof(LandmarkCorrespondenceEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentLandmarkCorrespondencePreview));
        OnPropertyChanged(nameof(IsLandmarkCorrespondencePreviewStale));
        OnPropertyChanged(nameof(IsLandmarkCorrespondencePreviewPublished));
        RefreshLandmarkCorrespondenceCommands();
    }

    private void RefreshLandmarkCorrespondenceCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
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


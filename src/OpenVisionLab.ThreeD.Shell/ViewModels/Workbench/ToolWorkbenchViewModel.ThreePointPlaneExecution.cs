using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? threePointPlanePreviewCancellation;
    private C3DThreePointPlaneFeature? threePointPlanePreviewOutput;
    private readonly Dictionary<string, C3DThreePointPlaneFeature> publishedThreePointPlaneOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> staleThreePointPlaneOutputIds = new(StringComparer.OrdinalIgnoreCase);
    private bool isThreePointPlanePreviewRunning;
    private bool isThreePointPlanePreviewStale;
    private bool isThreePointPlanePreviewPublished;
    private string threePointPlaneExecutionSummary = "Capture exactly three ordered non-collinear C3D grid cells, teach an output role, then Preview explicitly.";
    private bool IsThreePointPlanePreviewForSelectedStep => threePointPlanePreviewOutput is not null
        && string.Equals(SelectedPipelineStep?.OutputEntityId, threePointPlanePreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase);

    public event EventHandler<ToolWorkbenchThreePointPlaneDisplayRequestEventArgs>? ThreePointPlaneDisplayRequested;
    public event EventHandler? ThreePointPlaneDisplayCleared;

    public bool IsSelectedStepThreePointPlane => string.Equals(SelectedPipelineStep?.ToolId, "three-point-plane", StringComparison.Ordinal);
    public bool IsThreePointPlanePreviewRunning => isThreePointPlanePreviewRunning;
    public bool HasCurrentThreePointPlanePreview => IsThreePointPlanePreviewForSelectedStep && !IsThreePointPlanePreviewStale;
    public bool IsThreePointPlanePreviewStale => SelectedPipelineStep is { } step && staleThreePointPlaneOutputIds.Contains(step.OutputEntityId);
    public bool IsThreePointPlanePreviewPublished => isThreePointPlanePreviewPublished && IsThreePointPlanePreviewForSelectedStep;
    internal C3DThreePointPlaneFeature? CurrentThreePointPlaneOutput => IsThreePointPlanePreviewForSelectedStep ? threePointPlanePreviewOutput : null;
    internal bool TryGetPublishedThreePointPlaneOutput(string outputEntityId, out C3DThreePointPlaneFeature? output) =>
        publishedThreePointPlaneOutputs.TryGetValue(outputEntityId, out output);

    public string ThreePointPlaneExecutionSummary => threePointPlaneExecutionSummary;
    public string ThreePointPlaneOutputHashSummary => threePointPlanePreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {threePointPlanePreviewOutput.ContentSha256}";
    public string ThreePointPlaneSelectionSummary => SelectedStepTeachingSelection?.Points is { Count: 3 } points
        ? $"Ordered picks: ({points[0].Locator.Row}, {points[0].Locator.Column}) -> ({points[1].Locator.Row}, {points[1].Locator.Column}) -> ({points[2].Locator.Row}, {points[2].Locator.Column})"
        : "Capture exactly three ordered non-collinear grid-cell picks before Preview.";

    public async Task<bool> PreviewSelectedThreePointPlaneAsync()
    {
        if (!CanPreviewSelectedThreePointPlane() || SelectedPipelineStep is not { } step) return false;
        threePointPlanePreviewCancellation?.Dispose();
        threePointPlanePreviewCancellation = new CancellationTokenSource();
        SetThreePointPlaneRunning(true);
        isThreePointPlanePreviewStale = false;
        isThreePointPlanePreviewPublished = false;
        staleThreePointPlaneOutputIds.Remove(step.OutputEntityId);
        step.State = "Preview running";
        SetThreePointPlaneSummary("3-Point Plane Preview is resolving the exact current raw C3D values for the authored ordered picks.");
        AppendLog("Preview", $"3-Point Plane Preview started: {step.Id}.");
        try
        {
            var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            var evaluation = await Task.Run(
                () => ToolRecipeThreePointPlaneExecution.Execute(CreateDocument(), step.Id, recipeDirectory, threePointPlanePreviewCancellation.Token),
                threePointPlanePreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                threePointPlanePreviewOutput = null;
                step.State = "Error";
                SetThreePointPlaneSummary(evaluation.Result.Message);
                AppendLog("Error", $"3-Point Plane Preview failed: {evaluation.Result.Message}");
                return false;
            }

            threePointPlanePreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetThreePointPlaneSummary($"Preview ready | {ThreePointPlaneSelectionSummary} | oriented normal {evaluation.Output.NormalX:G4}, {evaluation.Output.NormalY:G4}, {evaluation.Output.NormalZ:G4} | no fit or OK/NG");
            AppendLog("Preview", $"3-Point Plane Preview ready: {evaluation.Output.ContentSha256}.");
            ThreePointPlaneDisplayRequested?.Invoke(this, new ToolWorkbenchThreePointPlaneDisplayRequestEventArgs(evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetThreePointPlaneSummary("Preview canceled. The source, picks, and authored recipe were not changed.");
            AppendLog("Preview", "3-Point Plane Preview canceled.");
            return false;
        }
        finally
        {
            SetThreePointPlaneRunning(false);
        }
    }

    private bool CanPreviewSelectedThreePointPlane()
    {
        if (!IsSelectedStepThreePointPlane || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isThreePointPlanePreviewRunning || SelectedPipelineStep is not { } step) return false;
        var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
        return ToolRecipeThreePointPlaneExecution.TryPrepare(CreateDocument(), step.Id, recipeDirectory, out _, out _);
    }

    private void PublishSelectedThreePointPlane()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentThreePointPlanePreview) return;
        isThreePointPlanePreviewPublished = true;
        publishedThreePointPlaneOutputs[threePointPlanePreviewOutput!.OutputEntityId] = threePointPlanePreviewOutput;
        step.State = "Published";
        SetThreePointPlaneSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {threePointPlanePreviewOutput.ContentSha256} | datum construction evidence only, no fit or OK/NG");
        AppendLog("Publish", $"3-Point Plane output published without re-running: {step.OutputEntityId}.");
        ThreePointPlaneDisplayRequested?.Invoke(this, new ToolWorkbenchThreePointPlaneDisplayRequestEventArgs(threePointPlanePreviewOutput, true));
    }

    private void CancelThreePointPlanePreview() => threePointPlanePreviewCancellation?.Cancel();

    private void MarkThreePointPlanePreviewStaleIfNeeded(object? sender = null)
    {
        var preview = threePointPlanePreviewOutput;
        if (isThreePointPlanePreviewRunning) return;
        ToolWorkbenchPipelineStepItem? affectedStep = null;
        if (sender is not null)
        {
            var step = SelectedPipelineStep;
            var selectedIsThreePointPlane = string.Equals(step?.ToolId, "three-point-plane", StringComparison.Ordinal);
            var parameterChanged = sender is ToolWorkbenchParameterItem parameter && (step?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsThreePointPlane || (!(ReferenceEquals(sender, step)) && !parameterChanged)) return;
            affectedStep = step;
        }
        else if (preview is not null)
        {
            affectedStep = PipelineSteps.FirstOrDefault(item => string.Equals(item.OutputEntityId, preview.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        }
        if (affectedStep is null) return;

        var affectedOutputId = affectedStep.OutputEntityId;
        var currentPreviewIsAffected = preview is not null
            && string.Equals(preview.OutputEntityId, affectedOutputId, StringComparison.OrdinalIgnoreCase);
        var hadPublishedOutput = publishedThreePointPlaneOutputs.Remove(affectedOutputId);
        if (!currentPreviewIsAffected && !hadPublishedOutput) return;

        staleThreePointPlaneOutputIds.Add(affectedOutputId);
        if (currentPreviewIsAffected)
        {
            isThreePointPlanePreviewStale = true;
            isThreePointPlanePreviewPublished = false;
        }
        affectedStep.State = "Preview stale";
        MarkDatumPlaneDeviationPreviewStaleIfNeeded(upstreamPlaneOutputId: affectedOutputId);
        ThreePointPlaneDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetThreePointPlaneSummary("Input, ordered picks, 3-Point Plane parameters, route, or output changed. Preview again before Publish.");
    }

    private void ClearThreePointPlanePreview(string summary)
    {
        threePointPlanePreviewCancellation?.Cancel();
        threePointPlanePreviewOutput = null;
        publishedThreePointPlaneOutputs.Clear();
        staleThreePointPlaneOutputIds.Clear();
        isThreePointPlanePreviewStale = false;
        isThreePointPlanePreviewPublished = false;
        ThreePointPlaneDisplayCleared?.Invoke(this, EventArgs.Empty);
        ClearDatumPlaneDeviationPreview("Published 3-Point Plane source cleared. Datum-plane residual preview is unavailable until a new plane is Published.");
        SetThreePointPlaneSummary(summary);
    }

    private void RefreshThreePointPlaneExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepThreePointPlane));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(HasCurrentThreePointPlanePreview));
        OnPropertyChanged(nameof(IsThreePointPlanePreviewStale));
        OnPropertyChanged(nameof(IsThreePointPlanePreviewPublished));
        OnPropertyChanged(nameof(ThreePointPlaneSelectionSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepThreePointPlane
            && (threePointPlanePreviewOutput is null || !string.Equals(threePointPlanePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase) || isThreePointPlanePreviewStale)
            && !isThreePointPlanePreviewRunning)
        {
            var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            if (ToolRecipeThreePointPlaneExecution.TryPrepare(CreateDocument(), step.Id, recipeDirectory, out _, out var message))
            {
                step.State = "Ready";
                threePointPlaneExecutionSummary = "Ready for explicit Preview. Pick capture and source resolution never run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                threePointPlaneExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(ThreePointPlaneExecutionSummary));
        OnPropertyChanged(nameof(ThreePointPlaneOutputHashSummary));
        RefreshThreePointPlaneCommands();
    }

    private void SetThreePointPlaneRunning(bool value)
    {
        isThreePointPlanePreviewRunning = value;
        OnPropertyChanged(nameof(IsThreePointPlanePreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshThreePointPlaneCommands();
    }

    private void SetThreePointPlaneSummary(string value)
    {
        threePointPlaneExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(ThreePointPlaneExecutionSummary));
        OnPropertyChanged(nameof(ThreePointPlaneOutputHashSummary));
        OnPropertyChanged(nameof(ThreePointPlaneSelectionSummary));
        OnPropertyChanged(nameof(HasCurrentThreePointPlanePreview));
        OnPropertyChanged(nameof(IsThreePointPlanePreviewStale));
        OnPropertyChanged(nameof(IsThreePointPlanePreviewPublished));
        RefreshThreePointPlaneCommands();
    }

    private void RefreshThreePointPlaneCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}

public sealed class ToolWorkbenchThreePointPlaneDisplayRequestEventArgs(C3DThreePointPlaneFeature output, bool isPublished) : EventArgs
{
    public C3DThreePointPlaneFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

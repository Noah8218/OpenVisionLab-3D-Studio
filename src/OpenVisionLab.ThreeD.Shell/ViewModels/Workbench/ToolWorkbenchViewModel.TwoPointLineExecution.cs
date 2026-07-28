using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? twoPointLinePreviewCancellation;
    private C3DTwoPointLineFeature? twoPointLinePreviewOutput;
    private readonly Dictionary<string, C3DTwoPointLineFeature> publishedTwoPointLineOutputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> staleTwoPointLineOutputIds = new(StringComparer.OrdinalIgnoreCase);
    private bool isTwoPointLinePreviewRunning;
    private bool isTwoPointLinePreviewStale;
    private bool isTwoPointLinePreviewPublished;
    private string twoPointLineExecutionSummary = "Capture exactly two ordered C3D grid cells, teach an output role, then Preview explicitly.";
    private bool IsTwoPointLinePreviewForSelectedStep => twoPointLinePreviewOutput is not null
        && string.Equals(SelectedPipelineStep?.OutputEntityId, twoPointLinePreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase);

    public event EventHandler<ToolWorkbenchTwoPointLineDisplayRequestEventArgs>? TwoPointLineDisplayRequested;
    public event EventHandler? TwoPointLineDisplayCleared;

    public bool IsSelectedStepTwoPointLine => string.Equals(SelectedPipelineStep?.ToolId, "two-point-line", StringComparison.Ordinal);
    public bool IsTwoPointLinePreviewRunning => isTwoPointLinePreviewRunning;
    public bool HasCurrentTwoPointLinePreview => IsTwoPointLinePreviewForSelectedStep && !IsTwoPointLinePreviewStale;
    public bool IsTwoPointLinePreviewStale => SelectedPipelineStep is { } step && staleTwoPointLineOutputIds.Contains(step.OutputEntityId);
    public bool IsTwoPointLinePreviewPublished => isTwoPointLinePreviewPublished && IsTwoPointLinePreviewForSelectedStep;
    internal C3DTwoPointLineFeature? CurrentTwoPointLineOutput => IsTwoPointLinePreviewForSelectedStep ? twoPointLinePreviewOutput : null;
    internal bool TryGetPublishedTwoPointLineOutput(string outputEntityId, out C3DTwoPointLineFeature? output) =>
        publishedTwoPointLineOutputs.TryGetValue(outputEntityId, out output);
    internal bool TryGetPublishedLineGeometry(string outputEntityId, out IC3DLineGeometry? output)
    {
        if (TryGetPublishedTwoPointLineOutput(outputEntityId, out var picked) && picked is not null)
        {
            output = picked;
            return true;
        }
        if (TryGetPublishedLineFitOutput(outputEntityId, out var fitted) && fitted is not null)
        {
            output = fitted;
            return true;
        }
        output = null;
        return false;
    }

    public string TwoPointLineExecutionSummary => twoPointLineExecutionSummary;
    public string TwoPointLineOutputHashSummary => twoPointLinePreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {twoPointLinePreviewOutput.ContentSha256}";
    public string TwoPointLineSelectionSummary
    {
        get
        {
            var selection = SelectedStepTeachingSelection;
            return (selection?.Points is { Count: 2 } points
                ? $"Ordered picks: row {points[0].Locator.Row}, column {points[0].Locator.Column} → row {points[1].Locator.Row}, column {points[1].Locator.Column}"
                : "Capture exactly two ordered grid-cell picks before Preview.").Replace("??", "->", StringComparison.Ordinal);
        }
    }

    public async Task<bool> PreviewSelectedTwoPointLineAsync()
    {
        if (!CanPreviewSelectedTwoPointLine() || SelectedPipelineStep is not { } step) return false;
        twoPointLinePreviewCancellation?.Dispose();
        twoPointLinePreviewCancellation = new CancellationTokenSource();
        SetTwoPointLineRunning(true);
        isTwoPointLinePreviewStale = false;
        isTwoPointLinePreviewPublished = false;
        staleTwoPointLineOutputIds.Remove(step.OutputEntityId);
        step.State = "Preview running";
        SetTwoPointLineSummary("2-Point Line Preview is resolving the exact current raw C3D values for the authored ordered picks.");
        AppendLog("Preview", $"2-Point Line Preview started: {step.Id}.");
        try
        {
            var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            var evaluation = await Task.Run(
                () => ToolRecipeTwoPointLineExecution.Execute(CreateDocument(), step.Id, recipeDirectory, twoPointLinePreviewCancellation.Token),
                twoPointLinePreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                twoPointLinePreviewOutput = null;
                step.State = "Error";
                SetTwoPointLineSummary(evaluation.Result.Message);
                AppendLog("Error", $"2-Point Line Preview failed: {evaluation.Result.Message}");
                return false;
            }

            twoPointLinePreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetTwoPointLineSummary($"Preview ready | {TwoPointLineSelectionSummary} | segment {evaluation.Output.SegmentLength:G6} source-coordinate | no fitting or OK/NG");
            AppendLog("Preview", $"2-Point Line Preview ready: {evaluation.Output.ContentSha256}.");
            TwoPointLineDisplayRequested?.Invoke(this, new ToolWorkbenchTwoPointLineDisplayRequestEventArgs(evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetTwoPointLineSummary("Preview canceled. The source, picks, and authored recipe were not changed.");
            AppendLog("Preview", "2-Point Line Preview canceled.");
            return false;
        }
        finally
        {
            SetTwoPointLineRunning(false);
        }
    }

    private bool CanPreviewSelectedTwoPointLine()
    {
        if (!IsSelectedStepTwoPointLine || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isTwoPointLinePreviewRunning || SelectedPipelineStep is not { } step) return false;
        var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
        return ToolRecipeTwoPointLineExecution.TryPrepare(CreateDocument(), step.Id, recipeDirectory, out _, out _);
    }

    private void PublishSelectedTwoPointLine()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentTwoPointLinePreview) return;
        isTwoPointLinePreviewPublished = true;
        publishedTwoPointLineOutputs[twoPointLinePreviewOutput!.OutputEntityId] = twoPointLinePreviewOutput;
        step.State = "Published";
        SetTwoPointLineSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {twoPointLinePreviewOutput.ContentSha256} | construction evidence only, no fit or OK/NG");
        AppendLog("Publish", $"2-Point Line output published without re-running: {step.OutputEntityId}.");
        TwoPointLineDisplayRequested?.Invoke(this, new ToolWorkbenchTwoPointLineDisplayRequestEventArgs(twoPointLinePreviewOutput, true));
        RefreshLineIntersectionExecutionState();
    }

    private void CancelTwoPointLinePreview() => twoPointLinePreviewCancellation?.Cancel();

    private void MarkTwoPointLinePreviewStaleIfNeeded(object? sender = null)
    {
        var preview = twoPointLinePreviewOutput;
        if (isTwoPointLinePreviewRunning) return;
        ToolWorkbenchPipelineStepItem? affectedStep = null;
        if (sender is not null)
        {
            var step = SelectedPipelineStep;
            var selectedIsTwoPointLine = string.Equals(step?.ToolId, "two-point-line", StringComparison.Ordinal);
            var parameterChanged = sender is ToolWorkbenchParameterItem parameter && (step?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsTwoPointLine || (!(ReferenceEquals(sender, step)) && !parameterChanged)) return;
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
        var hadPublishedOutput = publishedTwoPointLineOutputs.Remove(affectedOutputId);
        if (!currentPreviewIsAffected && !hadPublishedOutput) return;

        staleTwoPointLineOutputIds.Add(affectedOutputId);
        if (currentPreviewIsAffected)
        {
            isTwoPointLinePreviewStale = true;
            isTwoPointLinePreviewPublished = false;
        }
        MarkLineIntersectionPreviewStaleIfNeeded();
        affectedStep.State = "Preview stale";
        TwoPointLineDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetTwoPointLineSummary("Input, ordered picks, 2-Point Line parameters, route, or output changed. Preview again before Publish.");
    }

    private void ClearTwoPointLinePreview(string summary)
    {
        twoPointLinePreviewCancellation?.Cancel();
        twoPointLinePreviewOutput = null;
        publishedTwoPointLineOutputs.Clear();
        staleTwoPointLineOutputIds.Clear();
        isTwoPointLinePreviewStale = false;
        isTwoPointLinePreviewPublished = false;
        ClearLineIntersectionPreview("Upstream LineFeature was cleared. Line Intersection Preview was cleared without execution.");
        TwoPointLineDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetTwoPointLineSummary(summary);
    }

    private void RefreshTwoPointLineExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepTwoPointLine));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(HasCurrentTwoPointLinePreview));
        OnPropertyChanged(nameof(IsTwoPointLinePreviewStale));
        OnPropertyChanged(nameof(IsTwoPointLinePreviewPublished));
        OnPropertyChanged(nameof(TwoPointLineSelectionSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepTwoPointLine
            && (twoPointLinePreviewOutput is null || !string.Equals(twoPointLinePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase) || isTwoPointLinePreviewStale)
            && !isTwoPointLinePreviewRunning)
        {
            var recipeDirectory = RecipePath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(Path.GetFullPath(RecipePath));
            if (ToolRecipeTwoPointLineExecution.TryPrepare(CreateDocument(), step.Id, recipeDirectory, out _, out var message))
            {
                step.State = "Ready";
                twoPointLineExecutionSummary = "Ready for explicit Preview. Pick capture and source resolution never run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                twoPointLineExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(TwoPointLineExecutionSummary));
        OnPropertyChanged(nameof(TwoPointLineOutputHashSummary));
        RefreshTwoPointLineCommands();
    }

    private void SetTwoPointLineRunning(bool value)
    {
        isTwoPointLinePreviewRunning = value;
        OnPropertyChanged(nameof(IsTwoPointLinePreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshTwoPointLineCommands();
    }

    private void SetTwoPointLineSummary(string value)
    {
        twoPointLineExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(TwoPointLineExecutionSummary));
        OnPropertyChanged(nameof(TwoPointLineOutputHashSummary));
        OnPropertyChanged(nameof(TwoPointLineSelectionSummary));
        OnPropertyChanged(nameof(HasCurrentTwoPointLinePreview));
        OnPropertyChanged(nameof(IsTwoPointLinePreviewStale));
        OnPropertyChanged(nameof(IsTwoPointLinePreviewPublished));
        RefreshTwoPointLineCommands();
    }

    private void RefreshTwoPointLineCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}

public sealed class ToolWorkbenchTwoPointLineDisplayRequestEventArgs(C3DTwoPointLineFeature output, bool isPublished) : EventArgs
{
    public C3DTwoPointLineFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchTwoPointLineExecutionOwner
{
    private readonly Func<bool> isSelectedStepTwoPointLine;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeSelection?> getSelectedTeachingSelection;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchTwoPointLineDisplayRequestEventArgs> requestDisplay;
    private readonly Action clearDisplay;
    private readonly Action refreshLineIntersection;
    private readonly Action markLineIntersectionStale;
    private readonly Action clearLineIntersection;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DTwoPointLineFeature? previewOutput;
    private readonly Dictionary<string, C3DTwoPointLineFeature> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> staleOutputIds = new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Capture exactly two ordered C3D grid cells, teach an output role, then Preview explicitly.";

    public ToolWorkbenchTwoPointLineExecutionOwner(
        Func<bool> isSelectedStepTwoPointLine,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeSelection?> getSelectedTeachingSelection,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Action<string, string> appendLog,
        Action<ToolWorkbenchTwoPointLineDisplayRequestEventArgs> requestDisplay,
        Action clearDisplay,
        Action refreshLineIntersection,
        Action markLineIntersectionStale,
        Action clearLineIntersection,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepTwoPointLine = isSelectedStepTwoPointLine;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getSelectedTeachingSelection = getSelectedTeachingSelection;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.clearDisplay = clearDisplay;
        this.refreshLineIntersection = refreshLineIntersection;
        this.markLineIntersectionStale = markLineIntersectionStale;
        this.clearLineIntersection = clearLineIntersection;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    private bool IsPreviewForSelectedStep => previewOutput is not null
        && string.Equals(
            getSelectedPipelineStep()?.OutputEntityId,
            previewOutput.OutputEntityId,
            StringComparison.OrdinalIgnoreCase);

    public bool IsSelectedStepTwoPointLine => isSelectedStepTwoPointLine();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => IsPreviewForSelectedStep && !IsPreviewStale;
    public bool IsPreviewStale => getSelectedPipelineStep() is { } step
        && staleOutputIds.Contains(step.OutputEntityId);
    public bool IsPreviewPublished => isPreviewPublished && IsPreviewForSelectedStep;
    public C3DTwoPointLineFeature? CurrentOutput => IsPreviewForSelectedStep
        ? previewOutput
        : null;
    public string ExecutionSummary => executionSummary;

    public string OutputHashSummary => previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string SelectionSummary
    {
        get
        {
            var selection = getSelectedTeachingSelection();
            return (selection?.Points is { Count: 2 } points
                ? $"Ordered picks: row {points[0].Locator.Row}, column {points[0].Locator.Column} → row {points[1].Locator.Row}, column {points[1].Locator.Column}"
                : "Capture exactly two ordered grid-cell picks before Preview.").Replace(
                    "??",
                    "->",
                    StringComparison.Ordinal);
        }
    }

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DTwoPointLineFeature? output) =>
        publishedOutputs.TryGetValue(outputEntityId, out output);

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        previewCancellation?.Dispose();
        previewCancellation = new CancellationTokenSource();
        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        staleOutputIds.Remove(step.OutputEntityId);
        step.State = "Preview running";
        SetSummary("2-Point Line Preview is resolving the exact current raw C3D values for the authored ordered picks.");
        appendLog("Preview", $"2-Point Line Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeTwoPointLineExecution.Execute(
                    createDocument(),
                    step.Id,
                    GetRecipeDirectory(),
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"2-Point Line Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetSummary($"Preview ready | {SelectionSummary} | segment {evaluation.Output.SegmentLength:G6} source-coordinate | no fitting or OK/NG");
            appendLog("Preview", $"2-Point Line Preview ready: {evaluation.Output.ContentSha256}.");
            requestDisplay(new ToolWorkbenchTwoPointLineDisplayRequestEventArgs(evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. The source, picks, and authored recipe were not changed.");
            appendLog("Preview", "2-Point Line Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview()
    {
        if (!IsSelectedStepTwoPointLine || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeTwoPointLineExecution.TryPrepare(
            createDocument(),
            step.Id,
            GetRecipeDirectory(),
            out _,
            out _);
    }

    public void Publish()
    {
        if (getSelectedPipelineStep() is not { } step || !HasCurrentPreview)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = previewOutput;
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | construction evidence only, no fit or OK/NG");
        appendLog("Publish", $"2-Point Line output published without re-running: {step.OutputEntityId}.");
        requestDisplay(new ToolWorkbenchTwoPointLineDisplayRequestEventArgs(previewOutput, true));
        refreshLineIntersection();
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void MarkStaleIfNeeded(object? sender = null)
    {
        var preview = previewOutput;
        if (isPreviewRunning)
        {
            return;
        }

        ToolWorkbenchPipelineStepItem? affectedStep = null;
        if (sender is not null)
        {
            var step = getSelectedPipelineStep();
            var selectedIsTwoPointLine = string.Equals(
                step?.ToolId,
                "two-point-line",
                StringComparison.Ordinal);
            var parameterChanged = sender is ToolWorkbenchParameterItem parameter
                && (step?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsTwoPointLine
                || (!ReferenceEquals(sender, step) && !parameterChanged))
            {
                return;
            }

            affectedStep = step;
        }
        else if (preview is not null)
        {
            affectedStep = findStepByOutputEntityId(preview.OutputEntityId);
        }

        if (affectedStep is null)
        {
            return;
        }

        var affectedOutputId = affectedStep.OutputEntityId;
        var currentPreviewIsAffected = preview is not null
            && string.Equals(
                preview.OutputEntityId,
                affectedOutputId,
                StringComparison.OrdinalIgnoreCase);
        var hadPublishedOutput = publishedOutputs.Remove(affectedOutputId);
        if (!currentPreviewIsAffected && !hadPublishedOutput)
        {
            return;
        }

        staleOutputIds.Add(affectedOutputId);
        if (currentPreviewIsAffected)
        {
            isPreviewStale = true;
            isPreviewPublished = false;
        }

        markLineIntersectionStale();
        affectedStep.State = "Preview stale";
        clearDisplay();
        SetSummary("Input, ordered picks, 2-Point Line parameters, route, or output changed. Preview again before Publish.");
    }

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        previewOutput = null;
        publishedOutputs.Clear();
        staleOutputIds.Clear();
        isPreviewStale = false;
        isPreviewPublished = false;
        clearLineIntersection();
        clearDisplay();
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step && IsSelectedStepTwoPointLine
            && (previewOutput is null
                || !string.Equals(
                    previewOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            if (ToolRecipeTwoPointLineExecution.TryPrepare(
                createDocument(),
                step.Id,
                GetRecipeDirectory(),
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Pick capture and source resolution never run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
    }

    private void SetRunning(bool value)
    {
        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        executionSummary = value;
        onExecutionStateChanged();
    }

    private string? GetRecipeDirectory()
    {
        var path = getRecipePath();
        return path is null
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(path));
    }
}

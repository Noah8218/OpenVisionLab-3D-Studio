using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchThreePointPlaneExecutionOwner
{
    private readonly Func<bool> isSelectedStepThreePointPlane;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolRecipeSelection?> getSelectedTeachingSelection;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchThreePointPlaneDisplayRequestEventArgs> requestDisplay;
    private readonly Action clearDisplay;
    private readonly Action<string> markDatumPlaneDeviationStale;
    private readonly Action clearDatumPlaneDeviation;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DThreePointPlaneFeature? previewOutput;
    private readonly Dictionary<string, C3DThreePointPlaneFeature> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> staleOutputIds = new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Capture exactly three ordered non-collinear C3D grid cells, teach an output role, then Preview explicitly.";

    public ToolWorkbenchThreePointPlaneExecutionOwner(
        Func<bool> isSelectedStepThreePointPlane,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolRecipeSelection?> getSelectedTeachingSelection,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Action<string, string> appendLog,
        Action<ToolWorkbenchThreePointPlaneDisplayRequestEventArgs> requestDisplay,
        Action clearDisplay,
        Action<string> markDatumPlaneDeviationStale,
        Action clearDatumPlaneDeviation,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepThreePointPlane = isSelectedStepThreePointPlane;
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
        this.markDatumPlaneDeviationStale = markDatumPlaneDeviationStale;
        this.clearDatumPlaneDeviation = clearDatumPlaneDeviation;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    private bool IsPreviewForSelectedStep => previewOutput is not null
        && string.Equals(
            getSelectedPipelineStep()?.OutputEntityId,
            previewOutput.OutputEntityId,
            StringComparison.OrdinalIgnoreCase);

    public bool IsSelectedStepThreePointPlane => isSelectedStepThreePointPlane();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => IsPreviewForSelectedStep && !IsPreviewStale;
    public bool IsPreviewStale => getSelectedPipelineStep() is { } step
        && staleOutputIds.Contains(step.OutputEntityId);
    public bool IsPreviewPublished => isPreviewPublished && IsPreviewForSelectedStep;
    public C3DThreePointPlaneFeature? CurrentOutput => IsPreviewForSelectedStep
        ? previewOutput
        : null;
    public string ExecutionSummary => executionSummary;

    public string OutputHashSummary => previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string SelectionSummary => getSelectedTeachingSelection()?.Points is { Count: 3 } points
        ? $"Ordered picks: ({points[0].Locator.Row}, {points[0].Locator.Column}) -> ({points[1].Locator.Row}, {points[1].Locator.Column}) -> ({points[2].Locator.Row}, {points[2].Locator.Column})"
        : "Capture exactly three ordered non-collinear grid-cell picks before Preview.";

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DThreePointPlaneFeature? output) =>
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
        SetSummary("3-Point Plane Preview is resolving the exact current raw C3D values for the authored ordered picks.");
        appendLog("Preview", $"3-Point Plane Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeThreePointPlaneExecution.Execute(
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
                appendLog("Error", $"3-Point Plane Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetSummary($"Preview ready | {SelectionSummary} | oriented normal {evaluation.Output.NormalX:G4}, {evaluation.Output.NormalY:G4}, {evaluation.Output.NormalZ:G4} | no fit or OK/NG");
            appendLog("Preview", $"3-Point Plane Preview ready: {evaluation.Output.ContentSha256}.");
            requestDisplay(new ToolWorkbenchThreePointPlaneDisplayRequestEventArgs(evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. The source, picks, and authored recipe were not changed.");
            appendLog("Preview", "3-Point Plane Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview()
    {
        if (!IsSelectedStepThreePointPlane || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeThreePointPlaneExecution.TryPrepare(
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
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | datum construction evidence only, no fit or OK/NG");
        appendLog("Publish", $"3-Point Plane output published without re-running: {step.OutputEntityId}.");
        requestDisplay(new ToolWorkbenchThreePointPlaneDisplayRequestEventArgs(previewOutput, true));
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
            var selectedIsThreePointPlane = string.Equals(
                step?.ToolId,
                "three-point-plane",
                StringComparison.Ordinal);
            var parameterChanged = sender is ToolWorkbenchParameterItem parameter
                && (step?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsThreePointPlane
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

        affectedStep.State = "Preview stale";
        markDatumPlaneDeviationStale(affectedOutputId);
        clearDisplay();
        SetSummary("Input, ordered picks, 3-Point Plane parameters, route, or output changed. Preview again before Publish.");
    }

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        previewOutput = null;
        publishedOutputs.Clear();
        staleOutputIds.Clear();
        isPreviewStale = false;
        isPreviewPublished = false;
        clearDisplay();
        clearDatumPlaneDeviation();
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step && IsSelectedStepThreePointPlane
            && (previewOutput is null
                || !string.Equals(
                    previewOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            if (ToolRecipeThreePointPlaneExecution.TryPrepare(
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

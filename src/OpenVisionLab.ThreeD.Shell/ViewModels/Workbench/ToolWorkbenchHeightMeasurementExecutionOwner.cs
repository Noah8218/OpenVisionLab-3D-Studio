using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchHeightMeasurementExecutionOwner
{
    private readonly Func<bool> isSelectedStepMeasurement;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string> getSourceEntityId;
    private readonly Func<string, C3DHeightFieldSnapshot?> getPublishedCroppedHeightField;
    private readonly Func<string, C3DTransformedHeightField?> getPublishedHeightField;
    private readonly Func<string, C3DEditableRegionArtifact?> getPublishedEditableRegion;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolRecipeHeightMeasurementOutput?> updateCompletenessPresentation;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private ToolRecipeHeightMeasurementOutput? previewOutput;
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Route a verified HeightField and recipe-owned GridRectangle, then Preview explicitly.";

    public ToolWorkbenchHeightMeasurementExecutionOwner(
        Func<bool> isSelectedStepMeasurement,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> hasPendingStepParameterChanges,
        Func<string?> getRecipePath,
        Func<string> getSourceEntityId,
        Func<string, C3DHeightFieldSnapshot?> getPublishedCroppedHeightField,
        Func<string, C3DTransformedHeightField?> getPublishedHeightField,
        Func<string, C3DEditableRegionArtifact?> getPublishedEditableRegion,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<ToolRecipeHeightMeasurementOutput?> updateCompletenessPresentation,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepMeasurement = isSelectedStepMeasurement;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getRecipePath = getRecipePath;
        this.getSourceEntityId = getSourceEntityId;
        this.getPublishedCroppedHeightField = getPublishedCroppedHeightField;
        this.getPublishedHeightField = getPublishedHeightField;
        this.getPublishedEditableRegion = getPublishedEditableRegion;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.updateCompletenessPresentation = updateCompletenessPresentation;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished;
    public ToolRecipeHeightMeasurementOutput? CurrentOutput => previewOutput;
    public string ExecutionSummary => executionSummary;
    public string EvidenceSummary => previewOutput?.EvidenceSummary
        ?? "No measurement evidence until Preview completes.";

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            if (getSelectedPipelineStep() is { } waiting)
            {
                waiting.State = "Taught incomplete";
            }

            SetSummary("A current raw or Published transformed HeightField and its owned GridRectangle are required.");
            return false;
        }

        previewCancellation?.Dispose();
        previewCancellation = new CancellationTokenSource();
        isPreviewRunning = true;
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary($"{step.ToolName} Preview is evaluating only the selected tool step.");
        appendLog("Preview", $"{step.ToolName} Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeHeightMeasurementExecution.Execute(
                    createDocument(),
                    step.Id,
                    GetCurrentCroppedHeightField(),
                    GetCurrentTransformedHeightField(),
                    GetCurrentEditableRegion(),
                    GetRecipeDirectory(),
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Output is null || evaluation.Result.Status == ResultStatus.Error)
            {
                previewOutput = null;
                updateCompletenessPresentation(null);
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"{step.ToolName} Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            updateCompletenessPresentation(previewOutput);
            step.State = "Preview ready";
            SetSummary($"Preview ready | {previewOutput.EvidenceSummary} | {evaluation.Result.Status} | declared source units only.");
            appendLog("Preview", $"{step.ToolName} Preview ready: {previewOutput.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. The source, ROI, and authored recipe were not changed.");
            return false;
        }
        finally
        {
            isPreviewRunning = false;
            onExecutionStateChanged();
        }
    }

    public bool CanPreview()
    {
        if (!isSelectedStepMeasurement() || hasPendingStepParameterChanges()
            || isPreviewRunning || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeHeightMeasurementExecution.TryPrepare(
            createDocument(),
            step.Id,
            GetCurrentCroppedHeightField(),
            GetCurrentTransformedHeightField(),
            GetCurrentEditableRegion(),
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
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput!.ContentSha256} | no recalculation.");
        appendLog("Publish", $"{step.ToolName} output published without re-running: {step.OutputEntityId}.");
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        previewOutput = null;
        updateCompletenessPresentation(null);
        isPreviewStale = false;
        isPreviewPublished = false;
        SetSummary(summary);
    }

    public void MarkStaleIfNeeded(object? sender = null)
    {
        if (previewOutput is null || isPreviewRunning)
        {
            return;
        }

        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is null)
        {
            return;
        }

        if (sender is not null
            && !ReferenceEquals(sender, step)
            && (sender is not ToolWorkbenchParameterItem parameter
                || !step.Parameters.Contains(parameter)))
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        updateCompletenessPresentation(null);
        step.State = "Preview stale";
        SetSummary("Source, route, ROI, output, or parameter changed. Preview again before Publish.");
    }

    public void MarkInputStaleIfNeeded(string? inputEntityId)
    {
        if (previewOutput is null || isPreviewRunning || string.IsNullOrWhiteSpace(inputEntityId))
        {
            return;
        }

        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is null || step.InputEntityIds.Count == 0
            || !string.Equals(step.InputEntityIds[0], inputEntityId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        updateCompletenessPresentation(null);
        step.State = "Preview stale";
        SetSummary("The Published input HeightField changed. Preview this measurement again before Publish.");
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step && isSelectedStepMeasurement()
            && (previewOutput is null || isPreviewStale)
            && !isPreviewRunning)
        {
            if (ToolRecipeHeightMeasurementExecution.TryPrepare(
                createDocument(),
                step.Id,
                GetCurrentCroppedHeightField(),
                GetCurrentTransformedHeightField(),
                GetCurrentEditableRegion(),
                GetRecipeDirectory(),
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = $"{step.ToolName} is ready for explicit Preview. It remains one composable recipe step.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
    }

    private C3DTransformedHeightField? GetCurrentTransformedHeightField()
    {
        if (getSelectedPipelineStep() is not { InputEntityIds.Count: > 0 } step
            || string.Equals(
                step.InputEntityIds[0],
                getSourceEntityId(),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return getPublishedHeightField(step.InputEntityIds[0]);
    }

    private C3DHeightFieldSnapshot? GetCurrentCroppedHeightField()
    {
        if (getSelectedPipelineStep() is not { InputEntityIds.Count: > 0 } step
            || string.Equals(
                step.InputEntityIds[0],
                getSourceEntityId(),
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return getPublishedCroppedHeightField(step.InputEntityIds[0]);
    }

    private C3DEditableRegionArtifact? GetCurrentEditableRegion()
    {
        if (getSelectedPipelineStep() is not { } step
            || !string.Equals(step.ToolId, "completeness-grid", StringComparison.Ordinal)
            || step.InputEntityIds.Count < 3)
        {
            return null;
        }

        return getPublishedEditableRegion(step.InputEntityIds[2]);
    }

    private string? GetRecipeDirectory()
    {
        var recipePath = getRecipePath();
        return recipePath is null
            ? Environment.CurrentDirectory
            : Path.GetDirectoryName(Path.GetFullPath(recipePath));
    }

    private void SetSummary(string value)
    {
        executionSummary = value;
        onExecutionStateChanged();
    }
}

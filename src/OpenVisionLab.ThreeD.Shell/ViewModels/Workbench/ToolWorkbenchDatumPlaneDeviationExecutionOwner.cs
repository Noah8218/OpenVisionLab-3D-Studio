using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchDatumPlaneDeviationExecutionOwner
{
    private readonly Func<bool> isSelectedStepDatumPlaneDeviation;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<string, C3DThreePointPlaneFeature?> getPublishedPlane;
    private readonly Func<string, ToolRecipeSelection?> getSelection;
    private readonly Func<ToolRecipeSelection, bool> isSelectionCurrent;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<string?> getRecipePath;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs> requestDisplay;
    private readonly Action clearDisplay;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DDatumPlaneDeviationFeature? previewOutput;
    private readonly Dictionary<string, C3DDatumPlaneDeviationFeature> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Publish one 3-Point Plane, capture a measurement rectangle, teach the raw-height P2V limit, then Preview explicitly.";

    public ToolWorkbenchDatumPlaneDeviationExecutionOwner(
        Func<bool> isSelectedStepDatumPlaneDeviation,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<string, C3DThreePointPlaneFeature?> getPublishedPlane,
        Func<string, ToolRecipeSelection?> getSelection,
        Func<ToolRecipeSelection, bool> isSelectionCurrent,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Action<string, string> appendLog,
        Action<ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs> requestDisplay,
        Action clearDisplay,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepDatumPlaneDeviation = isSelectedStepDatumPlaneDeviation;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getPublishedPlane = getPublishedPlane;
        this.getSelection = getSelection;
        this.isSelectionCurrent = isSelectionCurrent;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.getRecipePath = getRecipePath;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.clearDisplay = clearDisplay;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepDatumPlaneDeviation => isSelectedStepDatumPlaneDeviation();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished;
    public C3DDatumPlaneDeviationFeature? CurrentOutput => previewOutput;
    public string ExecutionSummary => executionSummary;

    public string OutputHashSummary => previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string UpstreamSummary
    {
        get
        {
            var step = getSelectedPipelineStep();
            if (step is null || step.InputEntityIds.Count != 3)
            {
                return "Raw source, Published PlaneFeature, and GridRectangle are required.";
            }

            var planeReady = getPublishedPlane(step.InputEntityIds[1]) is not null;
            var selection = getSelection(step.InputEntityIds[2]);
            var selectionReady = selection is not null
                && selection.Kind == ToolRecipeSelectionKinds.GridRectangle
                && isSelectionCurrent(selection);
            return $"Plane {step.InputEntityIds[1]}: {(planeReady ? "Published" : "missing/stale")} | ROI {step.InputEntityIds[2]}: {(selectionReady ? "current" : "missing/stale")}";
        }
    }

    public string EvidenceSummary => previewOutput is null
        ? "No residual evidence until Preview completes."
        : $"{previewOutput.OutputRole} | P2V {previewOutput.PeakToValleyRawHeight:G6} raw-height | RMS {previewOutput.RmsRawHeightResidual:G6} | {previewOutput.ValidSampleCount:N0} valid samples";

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DDatumPlaneDeviationFeature? output) =>
        publishedOutputs.TryGetValue(outputEntityId, out output);

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedPipelineStep() is not { } step
            || !TryGetCurrentInputs(out var plane, out var selection)
            || plane is null || selection is null)
        {
            if (getSelectedPipelineStep() is { } waiting)
            {
                waiting.State = "Waiting for upstream";
            }

            SetSummary("The current raw C3D, a Published 3-Point Plane, and a current GridRectangle are required.");
            return false;
        }

        previewCancellation?.Dispose();
        previewCancellation = new CancellationTokenSource();
        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Datum Plane Raw-Height Deviation Preview is evaluating the exact Published plane and recipe-owned measurement rectangle.");
        appendLog("Preview", $"Datum Plane Raw-Height Deviation Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeDatumPlaneDeviationExecution.Execute(
                    createDocument(),
                    step.Id,
                    plane,
                    GetRecipeDirectory(),
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Output is null || evaluation.Result.Status is not (ResultStatus.Pass or ResultStatus.Fail))
            {
                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Datum Plane Raw-Height Deviation Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetSummary($"Preview ready | {EvidenceSummary} | local raw-height software result only; source C3D is unchanged.");
            appendLog("Preview", $"Datum Plane Raw-Height Deviation Preview ready: {evaluation.Output.ContentSha256}.");
            requestDisplay(new ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(plane, selection, evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. The source, Published PlaneFeature, ROI, and authored recipe were not changed.");
            appendLog("Preview", "Datum Plane Raw-Height Deviation Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview()
    {
        if (!IsSelectedStepDatumPlaneDeviation || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || getSelectedPipelineStep() is not { InputEntityIds.Count: 3 } step
            || !TryGetCurrentInputs(out var plane, out _) || plane is null)
        {
            return false;
        }

        return ToolRecipeDatumPlaneDeviationExecution.TryPrepare(
            createDocument(),
            step.Id,
            plane,
            GetRecipeDirectory(),
            out _,
            out _);
    }

    public void Publish()
    {
        if (getSelectedPipelineStep() is not { } step || !HasCurrentPreview
            || !TryGetCurrentInputs(out var plane, out var selection)
            || plane is null || selection is null)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = previewOutput;
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | local raw-height software result only; not physical metrology.");
        appendLog("Publish", $"Datum Plane Raw-Height Deviation output published without re-running: {step.OutputEntityId}.");
        requestDisplay(new ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(plane, selection, previewOutput, true));
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void MarkStaleIfNeeded(object? sender = null, string? upstreamPlaneOutputId = null)
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

        if (upstreamPlaneOutputId is not null
            && !string.Equals(step.InputEntityIds.ElementAtOrDefault(1), upstreamPlaneOutputId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (sender is not null)
        {
            var selectedIsCurrentDatum = IsSelectedStepDatumPlaneDeviation
                && ReferenceEquals(getSelectedPipelineStep(), step);
            var isDatumParameter = sender is ToolWorkbenchParameterItem parameter
                && step.Parameters.Contains(parameter);
            var isUpstreamPlaneStep = sender is ToolWorkbenchPipelineStepItem upstream
                && string.Equals(upstream.OutputEntityId, step.InputEntityIds.ElementAtOrDefault(1), StringComparison.OrdinalIgnoreCase);
            if (!selectedIsCurrentDatum && !isDatumParameter && !isUpstreamPlaneStep)
            {
                return;
            }
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        step.State = "Preview stale";
        clearDisplay();
        SetSummary("Published plane, measurement ROI, source, parameter, route, or output changed. Preview again before Publish.");
    }

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        previewOutput = null;
        publishedOutputs.Clear();
        isPreviewStale = false;
        isPreviewPublished = false;
        clearDisplay();
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step && IsSelectedStepDatumPlaneDeviation
            && (previewOutput is null
                || !string.Equals(previewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            if (!TryGetCurrentInputs(out var plane, out _) || plane is null)
            {
                step.State = "Waiting for upstream";
                executionSummary = "A current raw C3D, Published 3-Point Plane, and current measurement rectangle are required. No upstream tool will run implicitly.";
            }
            else if (ToolRecipeDatumPlaneDeviationExecution.TryPrepare(
                createDocument(),
                step.Id,
                plane,
                GetRecipeDirectory(),
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Published plane and source residual calculation will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
    }

    public bool TryGetCurrentInputs(
        out C3DThreePointPlaneFeature? plane,
        out ToolRecipeSelection? measurementSelection)
    {
        plane = null;
        measurementSelection = null;
        if (getSelectedPipelineStep() is not { InputEntityIds.Count: 3 } step)
        {
            return false;
        }

        plane = getPublishedPlane(step.InputEntityIds[1]);
        if (plane is null)
        {
            return false;
        }

        measurementSelection = getSelection(step.InputEntityIds[2]);
        return measurementSelection is not null;
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

    private string GetRecipeDirectory()
    {
        var path = getRecipePath();
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.CurrentDirectory;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        return string.IsNullOrWhiteSpace(directory)
            ? Environment.CurrentDirectory
            : directory;
    }
}

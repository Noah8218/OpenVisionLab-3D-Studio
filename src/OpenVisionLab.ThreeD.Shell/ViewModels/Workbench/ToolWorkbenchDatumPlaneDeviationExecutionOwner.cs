using System.IO;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchDatumPlaneDeviationExecutionOwner : IDisposable
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
    private ToolWorkbenchPipelineStepItem? previewStep;
    private string previewContext = string.Empty;
    private ToolResult? previewResult;
    private readonly Dictionary<string, (C3DDatumPlaneDeviationFeature Output, string StepId, string Context, ToolResult Result)> publishedOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Publish one 3-Point Plane, capture a measurement rectangle, teach the raw-height P2V limit, then Preview explicitly.";
    private int disposalState;

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

    public bool IsSelectedStepDatumPlaneDeviation => !IsDisposed && isSelectedStepDatumPlaneDeviation();
    public bool IsPreviewRunning => !IsDisposed && isPreviewRunning;
    public bool HasCurrentPreview => !IsDisposed && ReferenceEquals(previewStep, getSelectedPipelineStep())
        && previewOutput is not null && !isPreviewStale && IsOutputCurrent(previewOutput, previewStep!.Id, previewContext, createDocument());
    public bool IsPreviewStale => !IsDisposed && isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished && HasCurrentPreview;
    public C3DDatumPlaneDeviationFeature? CurrentOutput => !IsDisposed && ReferenceEquals(previewStep, getSelectedPipelineStep()) ? previewOutput : null;
    public ToolResult? CurrentResult => CurrentOutput is not null ? previewResult : null;
    public string ExecutionSummary => IsDisposed
        ? "Datum Plane Deviation execution owner has been disposed."
        : executionSummary;

    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    public string OutputHashSummary => IsDisposed || previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string UpstreamSummary
    {
        get
        {
            if (IsDisposed)
            {
                return "Datum Plane Deviation is disposed.";
            }

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

    public string EvidenceSummary => IsDisposed || previewOutput is null
        ? "No residual evidence until Preview completes."
        : $"{previewOutput.OutputRole} | P2V {previewOutput.PeakToValleyRawHeight:0.##} raw-height | RMS {previewOutput.RmsRawHeightResidual:0.##} | {previewOutput.ValidSampleCount:N0} valid samples";

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DDatumPlaneDeviationFeature? output)
    {
        if (IsDisposed)
        {
            output = null;
            return false;
        }

        if (publishedOutputs.TryGetValue(outputEntityId, out var published) && IsOutputCurrent(published.Output, published.StepId, published.Context, createDocument()))
        {
            output = published.Output;
            return true;
        }
        output = null;
        return false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        previewStep = null;
        previewContext = string.Empty;
        previewResult = null;
        publishedOutputs.Clear();
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
    }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed || !CanPreview() || getSelectedPipelineStep() is not { } step
            || !TryGetCurrentInputs(out var plane, out var selection)
            || plane is null || selection is null)
        {
            if (!IsDisposed && getSelectedPipelineStep() is { } waiting)
            {
                waiting.State = "Waiting for upstream";
            }

            if (!IsDisposed)
            {
                SetSummary("The current raw C3D, a Published 3-Point Plane, and a current GridRectangle are required.");
            }

            return false;
        }

        var currentCancellation = new CancellationTokenSource();
        var cancellationToken = currentCancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref previewCancellation,
            currentCancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    currentCancellation),
                currentCancellation))
            {
                currentCancellation.Dispose();
            }

            return false;
        }

        var document = createDocument();
        var context = document.CreateStepExecutionContext(step.Id);
        var recipeDirectory = GetRecipeDirectory();
        publishedOutputs.Remove(step.OutputEntityId);
        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Datum Plane Raw-Height Deviation Preview is evaluating the exact Published plane and recipe-owned measurement rectangle.");
        if (!IsCurrentPreview(currentCancellation))
        {
            return false;
        }

        appendLog("Preview", $"Datum Plane Raw-Height Deviation Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeDatumPlaneDeviationExecution.Execute(
                    document,
                    step.Id,
                    plane,
                    recipeDirectory,
                    cancellationToken),
                cancellationToken);
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            if (!ReferenceEquals(step, getSelectedPipelineStep())
                || !string.Equals(context, createDocument().CreateStepExecutionContext(step.Id), StringComparison.Ordinal)
                || !string.Equals(getPublishedPlane(plane.OutputEntityId)?.ContentSha256, plane.ContentSha256, StringComparison.OrdinalIgnoreCase))
            {
                isPreviewStale = previewOutput is not null;
                step.State = "Preview stale";
                SetSummary("Input or conditions changed. Run again for the current result.");
                return false;
            }

            if (evaluation.Output is null || evaluation.Result.Status == ResultStatus.Error)
            {
                if (!IsCurrentPreview(currentCancellation))
                {
                    return false;
                }

                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                if (IsCurrentPreview(currentCancellation))
                {
                    appendLog("Error", $"Datum Plane Raw-Height Deviation Preview failed: {evaluation.Result.Message}");
                }

                return false;
            }

            previewOutput = evaluation.Output;
            previewStep = step;
            previewContext = context;
            previewResult = evaluation.Result;
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Preview ready";
            SetSummary($"Preview ready | {EvidenceSummary} | local raw-height software result only; source C3D is unchanged.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", $"Datum Plane Raw-Height Deviation Preview ready: {evaluation.Output.ContentSha256}.");
            }

            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            requestDisplay(new ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(plane, selection, evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            if (!IsCurrentPreview(currentCancellation))
            {
                return false;
            }

            step.State = "Ready";
            SetSummary("Preview canceled. The source, Published PlaneFeature, ROI, and authored recipe were not changed.");
            if (IsCurrentPreview(currentCancellation))
            {
                appendLog("Preview", "Datum Plane Raw-Height Deviation Preview canceled.");
            }

            return false;
        }
        finally
        {
            var ownsCancellation = ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    currentCancellation),
                currentCancellation);
            if (ownsCancellation)
            {
                currentCancellation.Dispose();
                if (!IsDisposed)
                {
                    SetRunning(false);
                }
            }
        }
    }

    public bool CanPreview()
    {
        if (IsDisposed || !IsSelectedStepDatumPlaneDeviation || !isSourceReadyForRecipe()
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
        if (IsDisposed || getSelectedPipelineStep() is not { } step || !HasCurrentPreview
            || !TryGetCurrentInputs(out var plane, out var selection)
            || plane is null || selection is null)
        {
            return;
        }

        isPreviewPublished = true;
        publishedOutputs[previewOutput!.OutputEntityId] = (previewOutput, step.Id, previewContext, previewResult!);
        step.State = "Published";
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | local raw-height software result only; not physical metrology.");
        appendLog("Publish", $"Datum Plane Raw-Height Deviation output published without re-running: {step.OutputEntityId}.");
        requestDisplay(new ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs(plane, selection, previewOutput, true));
    }

    public void Cancel()
    {
        if (IsDisposed)
        {
            return;
        }

        try
        {
            Volatile.Read(ref previewCancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal or replacement already released the token source.
        }
    }

    public void MarkStaleIfNeeded(object? sender = null, string? upstreamPlaneOutputId = null)
    {
        if (IsDisposed || isPreviewRunning || previewOutput is null && publishedOutputs.Count == 0) return;
        var document = createDocument();
        var changed = false;
        foreach (var (id, published) in publishedOutputs.ToArray())
        {
            if (IsOutputCurrent(published.Output, published.StepId, published.Context, document)) continue;
            publishedOutputs.Remove(id);
            if (findStepByOutputEntityId(id) is { } step) step.State = "Preview stale";
            changed = true;
        }
        if (previewOutput is not null && !isPreviewStale && previewStep is not null
            && !IsOutputCurrent(previewOutput, previewStep.Id, previewContext, document))
        {
            isPreviewStale = true;
            isPreviewPublished = false;
            previewStep.State = "Preview stale";
            clearDisplay();
            changed = true;
        }
        if (!changed || IsDisposed) return;
        if (isPreviewStale) SetSummary("Published plane, measurement ROI, source, parameter, route, or output changed. Preview again before Publish.");
        else onExecutionStateChanged();
    }

    public void ForgetOutput(string outputEntityId)
    {
        if (IsDisposed) return;
        publishedOutputs.Remove(outputEntityId);
        if (!string.Equals(previewOutput?.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)) return;
        previewOutput = null;
        previewStep = null;
        previewContext = string.Empty;
        previewResult = null;
        isPreviewStale = false;
        isPreviewPublished = false;
        clearDisplay();
    }

    public void Clear(string summary)
    {
        if (IsDisposed)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(
            ref previewCancellation,
            null);
        CancelAndDispose(currentCancellation);
        previewOutput = null;
        previewStep = null;
        previewContext = string.Empty;
        previewResult = null;
        publishedOutputs.Clear();
        isPreviewRunning = false;
        isPreviewStale = false;
        isPreviewPublished = false;
        clearDisplay();
        SetSummary(summary);
    }

    public void RefreshState()
    {
        if (IsDisposed)
        {
            return;
        }

        MarkStaleIfNeeded();
        if (!isPreviewRunning && IsSelectedStepDatumPlaneDeviation && getSelectedPipelineStep() is { } selected)
        {
            if (TryGetPublishedOutput(selected.OutputEntityId, out _) && publishedOutputs.TryGetValue(selected.OutputEntityId, out var published))
            {
                previewOutput = published.Output;
                previewStep = selected;
                previewContext = published.Context;
                previewResult = published.Result;
                isPreviewStale = false;
                isPreviewPublished = true;
            }
            else if (!ReferenceEquals(previewStep, selected))
            {
                previewOutput = null;
                previewStep = null;
                previewContext = string.Empty;
                previewResult = null;
                isPreviewStale = false;
                isPreviewPublished = false;
            }
        }

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
        if (IsDisposed)
        {
            return false;
        }

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

    private bool IsOutputCurrent(C3DDatumPlaneDeviationFeature output, string stepId, string context, ToolRecipeDocument document) =>
        string.Equals(context, document.CreateStepExecutionContext(stepId), StringComparison.Ordinal)
        && string.Equals(getPublishedPlane(output.PlaneFeatureEntityId)?.ContentSha256, output.PlaneFeatureContentSha256, StringComparison.OrdinalIgnoreCase);

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isPreviewRunning = value;
        onExecutionStateChanged();
    }

    private void SetSummary(string value)
    {
        if (IsDisposed)
        {
            return;
        }

        executionSummary = value;
        onExecutionStateChanged();
    }

    private bool IsCurrentPreview(CancellationTokenSource cancellation) =>
        !IsDisposed && ReferenceEquals(
            Volatile.Read(ref previewCancellation),
            cancellation);

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

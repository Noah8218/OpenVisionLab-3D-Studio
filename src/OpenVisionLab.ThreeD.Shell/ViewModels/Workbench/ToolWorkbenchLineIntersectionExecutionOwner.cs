using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchLineIntersectionExecutionOwner
{
    private readonly Func<bool> isSelectedStepLineIntersection;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<string, IC3DLineGeometry?> getPublishedLineGeometry;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchLineIntersectionDisplayRequestEventArgs> requestDisplay;
    private readonly Action clearDisplay;
    private readonly Action refreshLandmarkCorrespondence;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DLineIntersectionFeature? previewOutput;
    private readonly Dictionary<string, C3DLineIntersectionFeature> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Teach the explicit gap, acute-angle, and support-extension limits, then publish both named line-geometry inputs before Preview.";

    public ToolWorkbenchLineIntersectionExecutionOwner(
        Func<bool> isSelectedStepLineIntersection,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<string, IC3DLineGeometry?> getPublishedLineGeometry,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<ToolWorkbenchLineIntersectionDisplayRequestEventArgs> requestDisplay,
        Action clearDisplay,
        Action refreshLandmarkCorrespondence,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepLineIntersection = isSelectedStepLineIntersection;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getPublishedLineGeometry = getPublishedLineGeometry;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.clearDisplay = clearDisplay;
        this.refreshLandmarkCorrespondence = refreshLandmarkCorrespondence;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepLineIntersection => isSelectedStepLineIntersection();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished;
    public C3DLineIntersectionFeature? CurrentOutput => previewOutput;
    public string ExecutionSummary => executionSummary;

    public string OutputHashSummary => previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public string UpstreamSummary
    {
        get
        {
            var step = getSelectedPipelineStep();
            if (step is null || step.InputEntityIds.Count != 2)
            {
                return "Two routed published line inputs are required.";
            }

            var first = getPublishedLineGeometry(step.InputEntityIds[0]) is not null;
            var second = getPublishedLineGeometry(step.InputEntityIds[1]) is not null;
            return $"Line A {step.InputEntityIds[0]}: {(first ? "Published" : "missing/stale")} | Line B {step.InputEntityIds[1]}: {(second ? "Published" : "missing/stale")}";
        }
    }

    public string EvidenceSummary => previewOutput is null
        ? "No corner evidence until Preview completes."
        : $"{previewOutput.OutputRole} | gap {previewOutput.ClosestApproachDistance:G6} source-coordinate | acute angle {previewOutput.AcuteAngleDegrees:G6} degrees | support extension {previewOutput.FirstSupportExtension:G6} / {previewOutput.SecondSupportExtension:G6}";

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DLineIntersectionFeature? output) =>
        publishedOutputs.TryGetValue(outputEntityId, out output);

    public void RegisterSyntheticPublishedOutput(C3DLineIntersectionFeature output)
    {
        ArgumentNullException.ThrowIfNull(output);
        publishedOutputs[output.OutputEntityId] = output;
        appendLog(
            "Smoke",
            $"Registered deterministic synthetic Published CornerAnchor prerequisite {output.OutputEntityId} ({output.ContentSha256}); normal Line Intersection Preview/Publish remains explicit.");
        refreshLandmarkCorrespondence();
    }

    public bool TryGetCurrentInputs(
        out IC3DLineGeometry? first,
        out IC3DLineGeometry? second)
    {
        first = null;
        second = null;
        if (getSelectedPipelineStep() is not { InputEntityIds.Count: 2 } step)
        {
            return false;
        }

        first = getPublishedLineGeometry(step.InputEntityIds[0]);
        second = getPublishedLineGeometry(step.InputEntityIds[1]);
        return first is not null && second is not null;
    }

    public async Task<bool> PreviewAsync()
    {
        if (!CanPreview() || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        var first = getPublishedLineGeometry(step.InputEntityIds[0]);
        var second = getPublishedLineGeometry(step.InputEntityIds[1]);
        if (first is null || second is null)
        {
            step.State = "Waiting for upstream";
            SetSummary("Both routed line inputs must be current and Published.");
            return false;
        }

        previewCancellation?.Dispose();
        previewCancellation = new CancellationTokenSource();
        SetRunning(true);
        isPreviewStale = false;
        isPreviewPublished = false;
        step.State = "Preview running";
        SetSummary("Line Intersection Preview is evaluating only the exact two Published line inputs.");
        appendLog("Preview", $"Line Intersection Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeLineIntersectionExecution.Execute(
                    createDocument(),
                    step.Id,
                    first,
                    second,
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Line Intersection Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetSummary($"Preview ready | {EvidenceSummary} | no OK/NG");
            appendLog("Preview", $"Line Intersection Preview ready: {evaluation.Output.ContentSha256}.");
            requestDisplay(new ToolWorkbenchLineIntersectionDisplayRequestEventArgs(
                first,
                second,
                evaluation.Output,
                false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. Published LineFeature inputs and authored recipe were not changed.");
            appendLog("Preview", "Line Intersection Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview()
    {
        if (!IsSelectedStepLineIntersection || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || getSelectedPipelineStep() is not { InputEntityIds.Count: 2 } step)
        {
            return false;
        }

        var first = getPublishedLineGeometry(step.InputEntityIds[0]);
        var second = getPublishedLineGeometry(step.InputEntityIds[1]);
        return first is not null
            && second is not null
            && ToolRecipeLineIntersectionExecution.TryPrepare(
                createDocument(),
                step.Id,
                first,
                second,
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
        SetSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {previewOutput.ContentSha256} | feature extraction only, no OK/NG");
        appendLog("Publish", $"Line Intersection output published without re-running: {step.OutputEntityId}.");
        var first = getPublishedLineGeometry(step.InputEntityIds[0]);
        var second = getPublishedLineGeometry(step.InputEntityIds[1]);
        if (first is not null && second is not null)
        {
            requestDisplay(new ToolWorkbenchLineIntersectionDisplayRequestEventArgs(
                first,
                second,
                previewOutput,
                true));
        }

        refreshLandmarkCorrespondence();
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void MarkStaleIfNeeded(object? sender = null)
    {
        if (previewOutput is null || isPreviewRunning)
        {
            return;
        }

        if (sender is not null)
        {
            var selected = getSelectedPipelineStep();
            var selectedIsLineIntersection = string.Equals(
                selected?.ToolId,
                "line-intersection",
                StringComparison.Ordinal);
            var selectedIsCurrentIntersection = selectedIsLineIntersection
                && string.Equals(
                    selected?.OutputEntityId,
                    previewOutput.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase);
            var isSelectedIntersectionParameter = selectedIsLineIntersection
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsCurrentIntersection
                || (!ReferenceEquals(sender, selected) && !isSelectedIntersectionParameter))
            {
                return;
            }
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is not null)
        {
            step.State = "Preview stale";
        }

        clearDisplay();
        SetSummary("Input, Line Intersection parameter, route, or output changed. Preview again before Publish.");
        refreshLandmarkCorrespondence();
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
        refreshLandmarkCorrespondence();
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step && IsSelectedStepLineIntersection
            && (previewOutput is null
                || !string.Equals(
                    previewOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            var first = step.InputEntityIds.Count == 2
                ? getPublishedLineGeometry(step.InputEntityIds[0])
                : null;
            var second = step.InputEntityIds.Count == 2
                ? getPublishedLineGeometry(step.InputEntityIds[1])
                : null;
            if (first is null || second is null)
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeLineIntersectionExecution.TryPrepare(
                createDocument(),
                step.Id,
                first,
                second,
                out _,
                out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Upstream line construction or fitting will not run implicitly.";
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
}

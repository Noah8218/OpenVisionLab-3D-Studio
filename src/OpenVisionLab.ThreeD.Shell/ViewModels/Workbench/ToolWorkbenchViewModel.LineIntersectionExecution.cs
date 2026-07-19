using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? lineIntersectionPreviewCancellation;
    private C3DLineIntersectionFeature? lineIntersectionPreviewOutput;
    private readonly Dictionary<string, C3DLineIntersectionFeature> publishedLineIntersectionOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isLineIntersectionPreviewRunning;
    private bool isLineIntersectionPreviewStale;
    private bool isLineIntersectionPreviewPublished;
    private string lineIntersectionExecutionSummary = "Teach the explicit gap, acute-angle, and support-extension limits, then publish both named LineFeature inputs before Preview.";

    public event EventHandler<ToolWorkbenchLineIntersectionDisplayRequestEventArgs>? LineIntersectionDisplayRequested;
    public event EventHandler? LineIntersectionDisplayCleared;

    public bool IsSelectedStepLineIntersection => string.Equals(SelectedPipelineStep?.ToolId, "line-intersection", StringComparison.Ordinal);
    public bool IsLineIntersectionPreviewRunning => isLineIntersectionPreviewRunning;
    public bool HasCurrentLineIntersectionPreview => lineIntersectionPreviewOutput is not null && !isLineIntersectionPreviewStale;
    public bool IsLineIntersectionPreviewStale => isLineIntersectionPreviewStale;
    public bool IsLineIntersectionPreviewPublished => isLineIntersectionPreviewPublished;
    internal C3DLineIntersectionFeature? CurrentLineIntersectionOutput => lineIntersectionPreviewOutput;
    internal bool TryGetPublishedLineIntersectionOutput(string outputEntityId, out C3DLineIntersectionFeature? output) =>
        publishedLineIntersectionOutputs.TryGetValue(outputEntityId, out output);
    internal bool TryGetCurrentLineIntersectionInputs(out C3DLineFeature? first, out C3DLineFeature? second)
    {
        first = null;
        second = null;
        if (SelectedPipelineStep is not { InputEntityIds.Count: 2 } step) return false;
        return TryGetPublishedLineFitOutput(step.InputEntityIds[0], out first)
            && first is not null
            && TryGetPublishedLineFitOutput(step.InputEntityIds[1], out second)
            && second is not null;
    }
    public string LineIntersectionExecutionSummary => lineIntersectionExecutionSummary;
    public string LineIntersectionOutputHashSummary => lineIntersectionPreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {lineIntersectionPreviewOutput.ContentSha256}";
    public string LineIntersectionUpstreamSummary
    {
        get
        {
            var step = SelectedPipelineStep;
            if (step is null || step.InputEntityIds.Count != 2) return "Two routed LineFeature inputs are required.";
            var first = TryGetPublishedLineFitOutput(step.InputEntityIds[0], out var firstLine) && firstLine is not null;
            var second = TryGetPublishedLineFitOutput(step.InputEntityIds[1], out var secondLine) && secondLine is not null;
            return $"Line A {step.InputEntityIds[0]}: {(first ? "Published" : "missing/stale")} | Line B {step.InputEntityIds[1]}: {(second ? "Published" : "missing/stale")}";
        }
    }
    public string LineIntersectionEvidenceSummary => lineIntersectionPreviewOutput is null
        ? "No corner evidence until Preview completes."
        : $"{lineIntersectionPreviewOutput.OutputRole} | gap {lineIntersectionPreviewOutput.ClosestApproachDistance:G6} source-coordinate | acute angle {lineIntersectionPreviewOutput.AcuteAngleDegrees:G6} degrees | support extension {lineIntersectionPreviewOutput.FirstSupportExtension:G6} / {lineIntersectionPreviewOutput.SecondSupportExtension:G6}";

    public async Task<bool> PreviewSelectedLineIntersectionAsync()
    {
        if (!CanPreviewSelectedLineIntersection() || SelectedPipelineStep is not { } step) return false;
        if (!TryGetPublishedLineFitOutput(step.InputEntityIds[0], out var first) || first is null
            || !TryGetPublishedLineFitOutput(step.InputEntityIds[1], out var second) || second is null)
        {
            step.State = "Waiting for upstream";
            SetLineIntersectionSummary("Both routed LineFeature inputs must be current and Published.");
            return false;
        }

        lineIntersectionPreviewCancellation?.Dispose();
        lineIntersectionPreviewCancellation = new CancellationTokenSource();
        SetLineIntersectionRunning(true);
        isLineIntersectionPreviewStale = false;
        isLineIntersectionPreviewPublished = false;
        step.State = "Preview running";
        SetLineIntersectionSummary("Line Intersection Preview is evaluating only the exact two Published LineFeatures.");
        AppendLog("Preview", $"Line Intersection Preview started: {step.Id}.");
        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeLineIntersectionExecution.Execute(CreateDocument(), step.Id, first, second, lineIntersectionPreviewCancellation.Token),
                lineIntersectionPreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                lineIntersectionPreviewOutput = null;
                step.State = "Error";
                SetLineIntersectionSummary(evaluation.Result.Message);
                AppendLog("Error", $"Line Intersection Preview failed: {evaluation.Result.Message}");
                return false;
            }

            lineIntersectionPreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            SetLineIntersectionSummary($"Preview ready | {LineIntersectionEvidenceSummary} | no OK/NG");
            AppendLog("Preview", $"Line Intersection Preview ready: {evaluation.Output.ContentSha256}.");
            LineIntersectionDisplayRequested?.Invoke(this, new ToolWorkbenchLineIntersectionDisplayRequestEventArgs(first, second, evaluation.Output, false));
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetLineIntersectionSummary("Preview canceled. Published LineFeature inputs and authored recipe were not changed.");
            AppendLog("Preview", "Line Intersection Preview canceled.");
            return false;
        }
        finally
        {
            SetLineIntersectionRunning(false);
        }
    }

    private bool CanPreviewSelectedLineIntersection()
    {
        if (!IsSelectedStepLineIntersection || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isLineIntersectionPreviewRunning || SelectedPipelineStep is not { } step || step.InputEntityIds.Count != 2) return false;
        return TryGetPublishedLineFitOutput(step.InputEntityIds[0], out var first) && first is not null
            && TryGetPublishedLineFitOutput(step.InputEntityIds[1], out var second) && second is not null
            && ToolRecipeLineIntersectionExecution.TryPrepare(CreateDocument(), step.Id, first, second, out _, out _);
    }

    private void PublishSelectedLineIntersection()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentLineIntersectionPreview) return;
        isLineIntersectionPreviewPublished = true;
        publishedLineIntersectionOutputs[lineIntersectionPreviewOutput!.OutputEntityId] = lineIntersectionPreviewOutput;
        step.State = "Published";
        SetLineIntersectionSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {lineIntersectionPreviewOutput.ContentSha256} | feature extraction only, no OK/NG");
        AppendLog("Publish", $"Line Intersection output published without re-running: {step.OutputEntityId}.");
        if (TryGetPublishedLineFitOutput(step.InputEntityIds[0], out var first) && first is not null
            && TryGetPublishedLineFitOutput(step.InputEntityIds[1], out var second) && second is not null)
        {
            LineIntersectionDisplayRequested?.Invoke(this, new ToolWorkbenchLineIntersectionDisplayRequestEventArgs(first, second, lineIntersectionPreviewOutput, true));
        }
    }

    private void CancelLineIntersectionPreview() => lineIntersectionPreviewCancellation?.Cancel();

    private void MarkLineIntersectionPreviewStaleIfNeeded(object? sender = null)
    {
        if (lineIntersectionPreviewOutput is null || isLineIntersectionPreviewRunning) return;
        if (sender is not null)
        {
            var selected = SelectedPipelineStep;
            var selectedIsLineIntersection = string.Equals(selected?.ToolId, "line-intersection", StringComparison.Ordinal);
            var selectedIsCurrentIntersection = selectedIsLineIntersection
                && string.Equals(selected?.OutputEntityId, lineIntersectionPreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase);
            var isSelectedIntersectionParameter = selectedIsLineIntersection
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsCurrentIntersection
                || (!(ReferenceEquals(sender, selected)) && !isSelectedIntersectionParameter))
            {
                return;
            }
        }
        isLineIntersectionPreviewStale = true;
        isLineIntersectionPreviewPublished = false;
        publishedLineIntersectionOutputs.Clear();
        var step = PipelineSteps.FirstOrDefault(item => string.Equals(item.OutputEntityId, lineIntersectionPreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is not null) step.State = "Preview stale";
        LineIntersectionDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetLineIntersectionSummary("Input, Line Intersection parameter, route, or output changed. Preview again before Publish.");
    }

    private void ClearLineIntersectionPreview(string summary)
    {
        lineIntersectionPreviewCancellation?.Cancel();
        lineIntersectionPreviewOutput = null;
        publishedLineIntersectionOutputs.Clear();
        isLineIntersectionPreviewStale = false;
        isLineIntersectionPreviewPublished = false;
        LineIntersectionDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetLineIntersectionSummary(summary);
    }

    private void RefreshLineIntersectionExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepLineIntersection));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(LineIntersectionUpstreamSummary));
        if (SelectedPipelineStep is { } step && IsSelectedStepLineIntersection
            && (lineIntersectionPreviewOutput is null
                || !string.Equals(lineIntersectionPreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isLineIntersectionPreviewStale)
            && !isLineIntersectionPreviewRunning)
        {
            if (step.InputEntityIds.Count != 2
                || !TryGetPublishedLineFitOutput(step.InputEntityIds[0], out var first) || first is null
                || !TryGetPublishedLineFitOutput(step.InputEntityIds[1], out var second) || second is null)
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeLineIntersectionExecution.TryPrepare(CreateDocument(), step.Id, first, second, out _, out var message))
            {
                step.State = "Ready";
                lineIntersectionExecutionSummary = "Ready for explicit Preview. Line Fit will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                lineIntersectionExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(LineIntersectionExecutionSummary));
        OnPropertyChanged(nameof(LineIntersectionOutputHashSummary));
        OnPropertyChanged(nameof(LineIntersectionEvidenceSummary));
        RefreshLineIntersectionCommands();
    }

    private void SetLineIntersectionRunning(bool value)
    {
        isLineIntersectionPreviewRunning = value;
        OnPropertyChanged(nameof(IsLineIntersectionPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshLineIntersectionCommands();
    }

    private void SetLineIntersectionSummary(string value)
    {
        lineIntersectionExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(LineIntersectionExecutionSummary));
        OnPropertyChanged(nameof(LineIntersectionOutputHashSummary));
        OnPropertyChanged(nameof(LineIntersectionEvidenceSummary));
        OnPropertyChanged(nameof(HasCurrentLineIntersectionPreview));
        OnPropertyChanged(nameof(IsLineIntersectionPreviewStale));
        OnPropertyChanged(nameof(IsLineIntersectionPreviewPublished));
        RefreshLineIntersectionCommands();
    }

    private void RefreshLineIntersectionCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}

public sealed class ToolWorkbenchLineIntersectionDisplayRequestEventArgs(
    C3DLineFeature firstLine,
    C3DLineFeature secondLine,
    C3DLineIntersectionFeature output,
    bool isPublished) : EventArgs
{
    public C3DLineFeature FirstLine { get; } = firstLine;
    public C3DLineFeature SecondLine { get; } = secondLine;
    public C3DLineIntersectionFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

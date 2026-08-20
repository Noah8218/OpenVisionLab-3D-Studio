using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

internal sealed class ToolWorkbenchHeightDifferenceEdgeExecutionOwner
{
    private static readonly string[] AxisOptions =
        ["Select comparison axis", "AcrossColumns (+X)", "AcrossRows (+Z)"];
    private static readonly string[] PolarityOptions =
        ["Select polarity", "Rising", "Falling", "Absolute"];

    private readonly Func<bool> isSelectedStepHeightDifferenceEdge;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Func<bool> isSourceReadyForRecipe;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<bool> isFilterPreviewRunning;
    private readonly Func<C3DHeightFieldSnapshot?> getFilterPreviewOutput;
    private readonly Func<string?> getFilterPreviewPath;
    private readonly Func<bool> isFilterPreviewStale;
    private readonly Func<bool> isFilterPreviewPublished;
    private readonly Func<ToolRecipeSelection?> getSelectedTeachingSelection;
    private readonly Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Action<string, string> appendLog;
    private readonly Action<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs> requestDisplay;
    private readonly Action refreshLineFit;
    private readonly Action markLineFitStale;
    private readonly Action clearLineFit;
    private readonly Action onExecutionStateChanged;

    private CancellationTokenSource? previewCancellation;
    private C3DHeightDifferenceEdgePointSet? previewOutput;
    private readonly Dictionary<string, C3DHeightDifferenceEdgePointSet> publishedOutputs =
        new(StringComparer.OrdinalIgnoreCase);
    private bool isPreviewRunning;
    private bool isPreviewStale;
    private bool isPreviewPublished;
    private string executionSummary =
        "Teach a search band and explicit MinimumDelta, then publish Filter before Preview.";

    public ToolWorkbenchHeightDifferenceEdgeExecutionOwner(
        Func<bool> isSelectedStepHeightDifferenceEdge,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Func<bool> isSourceReadyForRecipe,
        Func<bool> hasPendingStepParameterChanges,
        Func<bool> isFilterPreviewRunning,
        Func<C3DHeightFieldSnapshot?> getFilterPreviewOutput,
        Func<string?> getFilterPreviewPath,
        Func<bool> isFilterPreviewStale,
        Func<bool> isFilterPreviewPublished,
        Func<ToolRecipeSelection?> getSelectedTeachingSelection,
        Func<string, ToolWorkbenchPipelineStepItem?> findStepByOutputEntityId,
        Func<ToolRecipeDocument> createDocument,
        Action<string, string> appendLog,
        Action<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs> requestDisplay,
        Action refreshLineFit,
        Action markLineFitStale,
        Action clearLineFit,
        Action onExecutionStateChanged)
    {
        this.isSelectedStepHeightDifferenceEdge = isSelectedStepHeightDifferenceEdge;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.isSourceReadyForRecipe = isSourceReadyForRecipe;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.isFilterPreviewRunning = isFilterPreviewRunning;
        this.getFilterPreviewOutput = getFilterPreviewOutput;
        this.getFilterPreviewPath = getFilterPreviewPath;
        this.isFilterPreviewStale = isFilterPreviewStale;
        this.isFilterPreviewPublished = isFilterPreviewPublished;
        this.getSelectedTeachingSelection = getSelectedTeachingSelection;
        this.findStepByOutputEntityId = findStepByOutputEntityId;
        this.createDocument = createDocument;
        this.appendLog = appendLog;
        this.requestDisplay = requestDisplay;
        this.refreshLineFit = refreshLineFit;
        this.markLineFitStale = markLineFitStale;
        this.clearLineFit = clearLineFit;
        this.onExecutionStateChanged = onExecutionStateChanged;
    }

    public bool IsSelectedStepHeightDifferenceEdge => isSelectedStepHeightDifferenceEdge();
    public bool IsPreviewRunning => isPreviewRunning;
    public bool HasCurrentPreview => previewOutput is not null && !isPreviewStale;
    public bool IsPreviewStale => isPreviewStale;
    public bool IsPreviewPublished => isPreviewPublished;
    public C3DHeightDifferenceEdgePointSet? CurrentOutput => previewOutput;
    public IReadOnlyList<string> ComparisonAxisOptions => AxisOptions;
    public IReadOnlyList<string> EdgePolarityOptions => PolarityOptions;
    public string ExecutionSummary => executionSummary;

    public string SelectedComparisonAxis
    {
        get => GetParameter("ComparisonAxis") switch
        {
            "AcrossColumns" => AxisOptions[1],
            "AcrossRows" => AxisOptions[2],
            _ => AxisOptions[0]
        };
        set => SetParameter("ComparisonAxis", value switch
        {
            "AcrossColumns (+X)" => "AcrossColumns",
            "AcrossRows (+Z)" => "AcrossRows",
            _ => "Set explicitly"
        });
    }

    public string SelectedPolarity
    {
        get => GetParameter("Polarity") switch
        {
            "Rising" => "Rising",
            "Falling" => "Falling",
            "Absolute" => "Absolute",
            _ => PolarityOptions[0]
        };
        set => SetParameter(
            "Polarity",
            value == PolarityOptions[0] ? "Set explicitly" : value);
    }

    public string MinimumDelta
    {
        get => GetParameter("MinimumDelta") ?? string.Empty;
        set => SetParameter("MinimumDelta", value ?? string.Empty);
    }

    public string ExpectedOrientation => GetParameter("ComparisonAxis") == "AcrossRows"
        ? "Expected edge along columns (X)"
        : "Expected edge along rows (Z)";

    public string UpstreamSummary
    {
        get
        {
            var step = getSelectedPipelineStep();
            if (step is null || step.InputEntityIds.Count == 0)
            {
                return "Missing routed height field";
            }

            var filterOutput = getFilterPreviewOutput();
            if (filterOutput is null || isFilterPreviewStale())
            {
                return $"{step.InputEntityIds[0]} | no current Filter Preview";
            }

            return string.Equals(
                step.InputEntityIds[0],
                filterOutput.EntityId,
                StringComparison.OrdinalIgnoreCase)
                ? $"{step.InputEntityIds[0]} | {(isFilterPreviewPublished() ? "Published | current" : "Preview only | publish required")}"
                : $"{step.InputEntityIds[0]} | does not match current Filter output";
        }
    }

    public string BandSummary
    {
        get
        {
            var rectangle = getSelectedTeachingSelection()?.GridRectangle;
            return rectangle is null
                ? "No recipe-owned GridRectangle routed"
                : $"Rows {rectangle.Row}..{rectangle.Row + rectangle.RowCount - 1} | columns {rectangle.Column}..{rectangle.Column + rectangle.ColumnCount - 1}";
        }
    }

    public string OutputHashSummary => previewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {previewOutput.ContentSha256}";

    public bool TryGetPublishedOutput(
        string outputEntityId,
        out C3DHeightDifferenceEdgePointSet? output) =>
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
        step.State = "Preview running";
        SetSummary("Height Difference Edge Preview is running from the exact Published Filter output.");
        appendLog("Preview", $"Height Difference Edge Preview started: {step.Id}.");

        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeHeightDifferenceEdgeExecution.Execute(
                    createDocument(),
                    step.Id,
                    getFilterPreviewOutput()!,
                    previewCancellation.Token),
                previewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                previewOutput = null;
                step.State = "Error";
                SetSummary(evaluation.Result.Message);
                appendLog("Error", $"Height Difference Edge Preview failed: {evaluation.Result.Message}");
                return false;
            }

            previewOutput = evaluation.Output;
            step.State = "Preview ready";
            var diagnostics = evaluation.Output.Diagnostics;
            SetSummary($"Preview ready | points {diagnostics.AcceptedScanlineCount:N0}/{diagnostics.ScanlineCount:N0} | eligible pairs {diagnostics.EligiblePairCount:N0} | missing skips {diagnostics.SkippedMissingPairCount:N0} | no OK/NG");
            appendLog("Preview", $"Height Difference Edge Preview ready: {evaluation.Output.ContentSha256}.");
            RequestDisplay(evaluation.Output, false);
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetSummary("Preview canceled. Published Filter output and authored recipe were not changed.");
            appendLog("Preview", "Height Difference Edge Preview canceled.");
            return false;
        }
        finally
        {
            SetRunning(false);
        }
    }

    public bool CanPreview()
    {
        var filterOutput = getFilterPreviewOutput();
        if (!IsSelectedStepHeightDifferenceEdge || !isSourceReadyForRecipe()
            || hasPendingStepParameterChanges() || isPreviewRunning
            || isFilterPreviewRunning() || filterOutput is null
            || isFilterPreviewStale() || !isFilterPreviewPublished()
            || getSelectedPipelineStep() is not { } step)
        {
            return false;
        }

        return ToolRecipeHeightDifferenceEdgeExecution.TryPrepare(
            createDocument(), step.Id, filterOutput, out _, out _);
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
        refreshLineFit();
        appendLog("Publish", $"Height Difference Edge output published without re-running: {step.OutputEntityId}.");
        RequestDisplay(previewOutput, true);
    }

    public void Cancel() => previewCancellation?.Cancel();

    public void SetParameter(string name, string value)
    {
        if (!IsSelectedStepHeightDifferenceEdge)
        {
            return;
        }

        var parameter = getSelectedPipelineStep()!.Parameters.SingleOrDefault(item => item.Name == name);
        if (parameter is null || parameter.Value == value)
        {
            return;
        }

        parameter.Value = value;
        onExecutionStateChanged();
    }

    public void MarkStaleIfNeeded(object? sender = null)
    {
        if (previewOutput is null || isPreviewRunning)
        {
            return;
        }

        if (sender is not null)
        {
            var selected = getSelectedPipelineStep();
            var selectedIsEdge = string.Equals(
                selected?.ToolId,
                "height-difference-edge",
                StringComparison.Ordinal);
            var selectedIsCurrentEdge = selectedIsEdge
                && string.Equals(
                    selected?.OutputEntityId,
                    previewOutput.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase);
            var isSelectedEdgeParameter = selectedIsEdge
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsCurrentEdge
                || (!ReferenceEquals(sender, selected) && !isSelectedEdgeParameter))
            {
                return;
            }
        }

        MarkStale("Input, search band, parameter, route, or output changed. Preview again before Publish.");
    }

    public void MarkStale(string summary)
    {
        if (previewOutput is null)
        {
            return;
        }

        isPreviewStale = true;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        var step = findStepByOutputEntityId(previewOutput.OutputEntityId);
        if (step is not null)
        {
            step.State = "Preview stale";
        }

        markLineFitStale();
        SetSummary(summary);
    }

    public void Clear(string summary)
    {
        previewCancellation?.Cancel();
        previewOutput = null;
        isPreviewStale = false;
        isPreviewPublished = false;
        publishedOutputs.Clear();
        SetSummary(summary);
        clearLineFit();
    }

    public void RefreshState()
    {
        if (getSelectedPipelineStep() is { } step && IsSelectedStepHeightDifferenceEdge
            && (previewOutput is null
                || !string.Equals(
                    previewOutput.OutputEntityId,
                    step.OutputEntityId,
                    StringComparison.OrdinalIgnoreCase)
                || isPreviewStale)
            && !isPreviewRunning)
        {
            var filterOutput = getFilterPreviewOutput();
            if (filterOutput is null || isFilterPreviewStale() || !isFilterPreviewPublished())
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeHeightDifferenceEdgeExecution.TryPrepare(
                createDocument(), step.Id, filterOutput, out _, out var message))
            {
                step.State = "Ready";
                executionSummary = "Ready for explicit Preview. Filter will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                executionSummary = message;
            }
        }

        onExecutionStateChanged();
        refreshLineFit();
    }

    private string? GetParameter(string name) => IsSelectedStepHeightDifferenceEdge
        ? getSelectedPipelineStep()!.Parameters.SingleOrDefault(parameter => parameter.Name == name)?.Value
        : null;

    private void RequestDisplay(C3DHeightDifferenceEdgePointSet output, bool isPublished)
    {
        var path = getFilterPreviewPath();
        if (path is null || !File.Exists(path))
        {
            return;
        }

        requestDisplay(new ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs(
            path,
            output,
            isPublished));
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

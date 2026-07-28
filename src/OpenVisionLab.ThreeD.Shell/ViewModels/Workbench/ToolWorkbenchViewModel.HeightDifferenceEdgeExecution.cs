using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private static readonly string[] EdgeAxisOptions = ["Select comparison axis", "AcrossColumns (+X)", "AcrossRows (+Z)"];
    private static readonly string[] EdgePolarityOptions = ["Select polarity", "Rising", "Falling", "Absolute"];
    private CancellationTokenSource? edgePreviewCancellation;
    private C3DHeightDifferenceEdgePointSet? edgePreviewOutput;
    private readonly Dictionary<string, C3DHeightDifferenceEdgePointSet> publishedEdgeOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isEdgePreviewRunning;
    private bool isEdgePreviewStale;
    private bool isEdgePreviewPublished;
    private string edgeExecutionSummary = "Teach a search band and explicit MinimumDelta, then publish Filter before Preview.";

    public event EventHandler<ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs>? HeightDifferenceEdgeDisplayRequested;

    public bool IsSelectedStepHeightDifferenceEdge =>
        string.Equals(SelectedPipelineStep?.ToolId, "height-difference-edge", StringComparison.Ordinal);
    public bool IsEdgePreviewRunning => isEdgePreviewRunning;
    public bool HasCurrentEdgePreview => edgePreviewOutput is not null && !isEdgePreviewStale;
    public bool IsEdgePreviewStale => isEdgePreviewStale;
    public bool IsEdgePreviewPublished => isEdgePreviewPublished;
    internal C3DHeightDifferenceEdgePointSet? CurrentHeightDifferenceEdgeOutput => edgePreviewOutput;
    internal bool TryGetPublishedHeightDifferenceEdgeOutput(string outputEntityId, out C3DHeightDifferenceEdgePointSet? output) =>
        publishedEdgeOutputs.TryGetValue(outputEntityId, out output);
    public IReadOnlyList<string> HeightDifferenceEdgeComparisonAxisOptions => EdgeAxisOptions;
    public IReadOnlyList<string> HeightDifferenceEdgePolarityOptions => EdgePolarityOptions;

    public string SelectedHeightDifferenceEdgeComparisonAxis
    {
        get => GetEdgeParameter("ComparisonAxis") switch
        {
            "AcrossColumns" => EdgeAxisOptions[1],
            "AcrossRows" => EdgeAxisOptions[2],
            _ => EdgeAxisOptions[0]
        };
        set => SetEdgeParameter("ComparisonAxis", value switch
        {
            "AcrossColumns (+X)" => "AcrossColumns",
            "AcrossRows (+Z)" => "AcrossRows",
            _ => "Set explicitly"
        });
    }

    public string SelectedHeightDifferenceEdgePolarity
    {
        get => GetEdgeParameter("Polarity") switch
        {
            "Rising" => "Rising",
            "Falling" => "Falling",
            "Absolute" => "Absolute",
            _ => EdgePolarityOptions[0]
        };
        set => SetEdgeParameter("Polarity", value == EdgePolarityOptions[0] ? "Set explicitly" : value);
    }

    public string HeightDifferenceEdgeMinimumDelta
    {
        get => GetEdgeParameter("MinimumDelta") ?? string.Empty;
        set => SetEdgeParameter("MinimumDelta", value ?? string.Empty);
    }

    public string HeightDifferenceEdgeExpectedOrientation =>
        GetEdgeParameter("ComparisonAxis") == "AcrossRows"
            ? "Expected edge along columns (X)"
            : "Expected edge along rows (Z)";

    public string HeightDifferenceEdgeUpstreamSummary
    {
        get
        {
            var step = SelectedPipelineStep;
            if (step is null || step.InputEntityIds.Count == 0)
            {
                return "Missing routed height field";
            }
            if (filterPreviewOutput is null || isFilterPreviewStale)
            {
                return $"{step.InputEntityIds[0]} | no current Filter Preview";
            }
            return string.Equals(step.InputEntityIds[0], filterPreviewOutput.EntityId, StringComparison.OrdinalIgnoreCase)
                ? $"{step.InputEntityIds[0]} | {(isFilterPreviewPublished ? "Published | current" : "Preview only | publish required")}"
                : $"{step.InputEntityIds[0]} | does not match current Filter output";
        }
    }

    public string HeightDifferenceEdgeBandSummary
    {
        get
        {
            var rectangle = SelectedStepTeachingSelection?.GridRectangle;
            return rectangle is null
                ? "No recipe-owned GridRectangle routed"
                : $"Rows {rectangle.Row}..{rectangle.Row + rectangle.RowCount - 1} | columns {rectangle.Column}..{rectangle.Column + rectangle.ColumnCount - 1}";
        }
    }

    public string HeightDifferenceEdgeFixedPolicySummary =>
        "Strongest per scanline | lowest-index tie | adjacent-pair midpoint | SkipPair | WithinSelection";
    public string HeightDifferenceEdgeExecutionSummary => edgeExecutionSummary;
    public string HeightDifferenceEdgeOutputHashSummary => edgePreviewOutput is null
        ? "No output hash until Preview completes."
        : $"Output SHA-256 {edgePreviewOutput.ContentSha256}";

    public async Task<bool> PreviewSelectedHeightDifferenceEdgeAsync()
    {
        if (!CanPreviewSelectedHeightDifferenceEdge() || SelectedPipelineStep is not { } step)
        {
            return false;
        }

        edgePreviewCancellation?.Dispose();
        edgePreviewCancellation = new CancellationTokenSource();
        SetEdgeRunning(true);
        isEdgePreviewStale = false;
        isEdgePreviewPublished = false;
        step.State = "Preview running";
        SetEdgeSummary("Height Difference Edge Preview is running from the exact Published Filter output.");
        AppendLog("Preview", $"Height Difference Edge Preview started: {step.Id}.");

        try
        {
            var evaluation = await Task.Run(
                () => ToolRecipeHeightDifferenceEdgeExecution.Execute(
                    CreateDocument(), step.Id, filterPreviewOutput!, edgePreviewCancellation.Token),
                edgePreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                edgePreviewOutput = null;
                step.State = "Error";
                SetEdgeSummary(evaluation.Result.Message);
                AppendLog("Error", $"Height Difference Edge Preview failed: {evaluation.Result.Message}");
                return false;
            }

            edgePreviewOutput = evaluation.Output;
            step.State = "Preview ready";
            var diagnostics = evaluation.Output.Diagnostics;
            SetEdgeSummary($"Preview ready | points {diagnostics.AcceptedScanlineCount:N0}/{diagnostics.ScanlineCount:N0} | eligible pairs {diagnostics.EligiblePairCount:N0} | missing skips {diagnostics.SkippedMissingPairCount:N0} | no OK/NG");
            AppendLog("Preview", $"Height Difference Edge Preview ready: {evaluation.Output.ContentSha256}.");
            RaiseEdgeDisplayRequested(evaluation.Output, false);
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetEdgeSummary("Preview canceled. Published Filter output and authored recipe were not changed.");
            AppendLog("Preview", "Height Difference Edge Preview canceled.");
            return false;
        }
        finally
        {
            SetEdgeRunning(false);
        }
    }

    public bool TryConfigureHeightDifferenceEdgeSmoke(
        string stepId,
        ToolRecipeGridRectangle rectangle,
        string comparisonAxis,
        string polarity,
        string minimumDelta,
        out string message)
    {
        var step = PipelineSteps.SingleOrDefault(item => string.Equals(item.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (step is null || loadedSourceBinding is null)
        {
            message = "Smoke Edge step or verified source binding is unavailable.";
            return false;
        }

        SelectedPipelineStep = step;
        var selection = new ToolRecipeSelection(
            $"selection.smoke.{NormalizeId(step.Id)}",
            "Smoke-only Edge search band",
            ToolRecipeSelectionKinds.GridRectangle,
            Source.Id,
            Source.FrameId,
            loadedSourceBinding,
            rectangle,
            null,
            null);
        PersistSelectionForSelectedStep(selection);
        SetEdgeParameter("ComparisonAxis", comparisonAxis);
        SetEdgeParameter("Polarity", polarity);
        SetEdgeParameter("MinimumDelta", minimumDelta);
        message = HeightDifferenceEdgeBandSummary;
        return true;
    }

    private bool CanPreviewSelectedHeightDifferenceEdge()
    {
        if (!IsSelectedStepHeightDifferenceEdge || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isEdgePreviewRunning || isFilterPreviewRunning
            || filterPreviewOutput is null || isFilterPreviewStale || !isFilterPreviewPublished
            || SelectedPipelineStep is null)
        {
            return false;
        }

        return ToolRecipeHeightDifferenceEdgeExecution.TryPrepare(
            CreateDocument(), SelectedPipelineStep.Id, filterPreviewOutput, out _, out _);
    }

    private void PublishSelectedHeightDifferenceEdge()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentEdgePreview)
        {
            return;
        }

        isEdgePreviewPublished = true;
        publishedEdgeOutputs[edgePreviewOutput!.OutputEntityId] = edgePreviewOutput;
        step.State = "Published";
        SetEdgeSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {edgePreviewOutput!.ContentSha256} | feature extraction only, no OK/NG");
        RefreshLineFitExecutionState();
        AppendLog("Publish", $"Height Difference Edge output published without re-running: {step.OutputEntityId}.");
        RaiseEdgeDisplayRequested(edgePreviewOutput, true);
    }

    private void CancelHeightDifferenceEdgePreview() => edgePreviewCancellation?.Cancel();

    private void RaiseEdgeDisplayRequested(C3DHeightDifferenceEdgePointSet output, bool isPublished)
    {
        var path = filterPreviewPath;
        if (path is null || !File.Exists(path))
        {
            return;
        }
        HeightDifferenceEdgeDisplayRequested?.Invoke(
            this, new ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs(path, output, isPublished));
    }

    private void SetEdgeParameter(string name, string value)
    {
        if (!IsSelectedStepHeightDifferenceEdge)
        {
            return;
        }
        var parameter = SelectedPipelineStep!.Parameters.SingleOrDefault(item => item.Name == name);
        if (parameter is null || parameter.Value == value)
        {
            return;
        }
        parameter.Value = value;
        OnPropertyChanged(nameof(SelectedHeightDifferenceEdgeComparisonAxis));
        OnPropertyChanged(nameof(SelectedHeightDifferenceEdgePolarity));
        OnPropertyChanged(nameof(HeightDifferenceEdgeMinimumDelta));
        OnPropertyChanged(nameof(HeightDifferenceEdgeExpectedOrientation));
    }

    private string? GetEdgeParameter(string name) => IsSelectedStepHeightDifferenceEdge
        ? SelectedPipelineStep!.Parameters.SingleOrDefault(parameter => parameter.Name == name)?.Value
        : null;

    private void MarkHeightDifferenceEdgePreviewStaleIfNeeded(object? sender = null)
    {
        if (edgePreviewOutput is not null && !isEdgePreviewRunning)
        {
            if (sender is not null)
            {
                var selected = SelectedPipelineStep;
                var selectedIsEdge = string.Equals(selected?.ToolId, "height-difference-edge", StringComparison.Ordinal);
                var selectedIsCurrentEdge = selectedIsEdge
                    && string.Equals(selected?.OutputEntityId, edgePreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase);
                var isSelectedEdgeParameter = selectedIsEdge
                    && sender is ToolWorkbenchParameterItem parameter
                    && (selected?.Parameters.Contains(parameter) ?? false);
                if (!selectedIsCurrentEdge
                    || (!(ReferenceEquals(sender, selected)) && !isSelectedEdgeParameter))
                {
                    return;
                }
            }
            MarkHeightDifferenceEdgePreviewStale("Input, search band, parameter, route, or output changed. Preview again before Publish.");
        }
    }

    private void MarkHeightDifferenceEdgePreviewStale(string summary)
    {
        if (edgePreviewOutput is null)
        {
            return;
        }
        isEdgePreviewStale = true;
        isEdgePreviewPublished = false;
        var step = PipelineSteps.FirstOrDefault(item => string.Equals(item.OutputEntityId, edgePreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is not null)
        {
            step.State = "Preview stale";
        }
        SetEdgeSummary(summary);
        publishedEdgeOutputs.Clear();
        MarkLineFitPreviewStaleIfNeeded();
    }

    private void ClearHeightDifferenceEdgePreview(string summary)
    {
        edgePreviewCancellation?.Cancel();
        edgePreviewOutput = null;
        isEdgePreviewStale = false;
        isEdgePreviewPublished = false;
        publishedEdgeOutputs.Clear();
        SetEdgeSummary(summary);
        ClearLineFitPreview("Upstream EdgePointSet was cleared. Line Fit Preview was cleared without execution.");
    }

    private void RefreshHeightDifferenceEdgeExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepHeightDifferenceEdge));
        OnPropertyChanged(nameof(SelectedHeightDifferenceEdgeComparisonAxis));
        OnPropertyChanged(nameof(SelectedHeightDifferenceEdgePolarity));
        OnPropertyChanged(nameof(HeightDifferenceEdgeMinimumDelta));
        OnPropertyChanged(nameof(HeightDifferenceEdgeExpectedOrientation));
        OnPropertyChanged(nameof(HeightDifferenceEdgeUpstreamSummary));
        OnPropertyChanged(nameof(HeightDifferenceEdgeBandSummary));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));

        if (SelectedPipelineStep is { } step && IsSelectedStepHeightDifferenceEdge
            && (edgePreviewOutput is null
                || !string.Equals(edgePreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isEdgePreviewStale)
            && !isEdgePreviewRunning)
        {
            if (filterPreviewOutput is null || isFilterPreviewStale || !isFilterPreviewPublished)
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeHeightDifferenceEdgeExecution.TryPrepare(
                CreateDocument(), step.Id, filterPreviewOutput, out _, out var message))
            {
                step.State = "Ready";
                edgeExecutionSummary = "Ready for explicit Preview. Filter will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                edgeExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(HeightDifferenceEdgeExecutionSummary));
        RefreshHeightDifferenceEdgeCommands();
        RefreshLineFitExecutionState();
    }

    private void RefreshHeightDifferenceEdgeCommands()
    {
        if (previewSelectedStepCommand is null)
        {
            return;
        }
        previewSelectedStepCommand.RaiseCanExecuteChanged();
        publishSelectedStepCommand.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand.RaiseCanExecuteChanged();
    }

    private void SetEdgeRunning(bool value)
    {
        isEdgePreviewRunning = value;
        OnPropertyChanged(nameof(IsEdgePreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshFilterCommands();
        RefreshHeightDifferenceEdgeCommands();
        RefreshLineFitCommands();
    }

    private void SetEdgeSummary(string value)
    {
        edgeExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(HeightDifferenceEdgeExecutionSummary));
        OnPropertyChanged(nameof(HeightDifferenceEdgeOutputHashSummary));
        OnPropertyChanged(nameof(HasCurrentEdgePreview));
        OnPropertyChanged(nameof(IsEdgePreviewStale));
        OnPropertyChanged(nameof(IsEdgePreviewPublished));
        RefreshHeightDifferenceEdgeCommands();
    }
}

public sealed class ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs : EventArgs
{
    public ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs(string c3DPath, C3DHeightDifferenceEdgePointSet output, bool isPublished)
    {
        C3DPath = c3DPath;
        Output = output;
        IsPublished = isPublished;
    }
    public string C3DPath { get; }
    public C3DHeightDifferenceEdgePointSet Output { get; }
    public bool IsPublished { get; }
}

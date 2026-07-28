using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private CancellationTokenSource? lineFitPreviewCancellation;
    private C3DLineFeature? lineFitPreviewOutput;
    private readonly Dictionary<string, C3DLineFeature> publishedLineFitOutputs = new(StringComparer.OrdinalIgnoreCase);
    private bool isLineFitPreviewRunning;
    private bool isLineFitPreviewStale;
    private bool isLineFitPreviewPublished;
    private C3DLineFeaturePointDiagnostic? selectedLineFitDiagnostic;
    private RelayCommand selectLineFitDiagnosticCommand = null!;
    private string lineFitExecutionSummary = "Teach all four Line Fit limits, then publish the exact upstream EdgePointSet before Preview.";

    public event EventHandler<ToolWorkbenchLineFitDisplayRequestEventArgs>? LineFitDisplayRequested;
    public event EventHandler? LineFitDisplayCleared;

    public bool IsSelectedStepLineFit => string.Equals(SelectedPipelineStep?.ToolId, "three-d-line-fit", StringComparison.Ordinal);
    public bool IsLineFitPreviewRunning => isLineFitPreviewRunning;
    public bool HasCurrentLineFitPreview => lineFitPreviewOutput is not null && !isLineFitPreviewStale;
    public bool IsLineFitPreviewStale => isLineFitPreviewStale;
    public bool IsLineFitPreviewPublished => isLineFitPreviewPublished;
    internal C3DLineFeature? CurrentLineFitOutput => lineFitPreviewOutput;
    internal bool TryGetPublishedLineFitOutput(string outputEntityId, out C3DLineFeature? output) =>
        publishedLineFitOutputs.TryGetValue(outputEntityId, out output);
    public C3DLineFeaturePointDiagnostic? SelectedLineFitDiagnostic
    {
        get => selectedLineFitDiagnostic;
        private set
        {
            if (ReferenceEquals(selectedLineFitDiagnostic, value)) return;
            selectedLineFitDiagnostic = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(LineFitSelectedDiagnosticSummary));
        }
    }
    public IReadOnlyList<C3DLineFeaturePointDiagnostic> LineFitPointDiagnostics => lineFitPreviewOutput?.PointDiagnostics ?? [];
    public ObservableCollection<LineFitResidualPlotPoint> LineFitResidualPlotPoints { get; } = [];
    public ICommand SelectLineFitDiagnosticCommand { get; private set; } = null!;
    public string LineFitExecutionSummary => lineFitExecutionSummary;
    public string LineFitOutputHashSummary => lineFitPreviewOutput is null ? "No output hash until Preview completes." : $"Output SHA-256 {lineFitPreviewOutput.ContentSha256}";
    public string LineFitUpstreamSummary
    {
        get
        {
            var step = SelectedPipelineStep;
            if (step is null || step.InputEntityIds.Count != 1) return "Missing routed EdgePointSet";
            return TryGetPublishedHeightDifferenceEdgeOutput(step.InputEntityIds[0], out var output) && output is not null
                ? $"{step.InputEntityIds[0]} | Published | {output.ContentSha256[..12]}"
                : $"{step.InputEntityIds[0]} | no current Published EdgePointSet";
        }
    }
    public string LineFitSelectedDiagnosticSummary => SelectedLineFitDiagnostic is null
        ? "Select an inlier/outlier diagnostic to review its source-coordinate residual."
        : $"scanline {SelectedLineFitDiagnostic.ScanlineIndex} | residual {SelectedLineFitDiagnostic.OrthogonalResidual:G6} source-coordinate | {(SelectedLineFitDiagnostic.IsInlier ? "inlier" : "outlier")} | XYZ ({SelectedLineFitDiagnostic.X:G6}, {SelectedLineFitDiagnostic.Y:G6}, {SelectedLineFitDiagnostic.Z:G6})";

    private void InitializeLineFitDiagnostics()
    {
        selectLineFitDiagnosticCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is int index) SelectLineFitDiagnostic(index);
                else if (int.TryParse(parameter?.ToString(), out var parsed)) SelectLineFitDiagnostic(parsed);
            },
            _ => HasCurrentLineFitPreview);
        SelectLineFitDiagnosticCommand = selectLineFitDiagnosticCommand;
    }

    public async Task<bool> PreviewSelectedLineFitAsync()
    {
        if (!CanPreviewSelectedLineFit() || SelectedPipelineStep is not { } step) return false;
        lineFitPreviewCancellation?.Dispose();
        lineFitPreviewCancellation = new CancellationTokenSource();
        SetLineFitRunning(true);
        isLineFitPreviewStale = false;
        isLineFitPreviewPublished = false;
        step.State = "Preview running";
        SetLineFitSummary("3D Line Fit Preview is running from the exact Published EdgePointSet.");
        AppendLog("Preview", $"3D Line Fit Preview started: {step.Id}.");
        try
        {
            if (!TryGetPublishedHeightDifferenceEdgeOutput(step.InputEntityIds[0], out var publishedEdge) || publishedEdge is null)
            {
                step.State = "Waiting for upstream";
                SetLineFitSummary("The routed EdgePointSet is not current and Published.");
                return false;
            }
            var evaluation = await Task.Run(
                () => ToolRecipeLineFitExecution.Execute(CreateDocument(), step.Id, publishedEdge, lineFitPreviewCancellation.Token),
                lineFitPreviewCancellation.Token);
            if (evaluation.Result.Status != ResultStatus.Pass || evaluation.Output is null)
            {
                lineFitPreviewOutput = null;
                SelectedLineFitDiagnostic = null;
                step.State = "Error";
                SetLineFitSummary(evaluation.Result.Message);
                AppendLog("Error", $"3D Line Fit Preview failed: {evaluation.Result.Message}");
                return false;
            }
            lineFitPreviewOutput = evaluation.Output;
            SelectedLineFitDiagnostic = evaluation.Output.PointDiagnostics.FirstOrDefault();
            RebuildLineFitResidualPlot(evaluation.Output);
            OnPropertyChanged(nameof(LineFitPointDiagnostics));
            OnPropertyChanged(nameof(LineFitResidualPlotPoints));
            step.State = "Preview ready";
            var diagnostics = evaluation.Output.Diagnostics;
            SetLineFitSummary($"Preview ready | inliers {diagnostics.InlierCount:N0}/{diagnostics.InputPointCount:N0} ({diagnostics.InlierRatio:P1}) | residual RMS {diagnostics.ResidualRms:G6} source-coordinate | no OK/NG");
            AppendLog("Preview", $"3D Line Fit Preview ready: {evaluation.Output.ContentSha256}.");
            RaiseLineFitDisplayRequested(evaluation.Output, false);
            return true;
        }
        catch (OperationCanceledException)
        {
            step.State = "Ready";
            SetLineFitSummary("Preview canceled. Published EdgePointSet and authored recipe were not changed.");
            AppendLog("Preview", "3D Line Fit Preview canceled.");
            return false;
        }
        finally
        {
            SetLineFitRunning(false);
        }
    }

    public bool TryConfigureLineFitSmoke(
        string edgeOutputEntityId,
        string maximumResidual,
        string minimumInlierCount,
        string minimumInlierRatio,
        string minimumInlierScanlineSpan,
        out string message)
    {
        var step = PipelineSteps.FirstOrDefault(item =>
            string.Equals(item.ToolId, "three-d-line-fit", StringComparison.Ordinal)
            && item.InputEntityIds.Count == 1
            && string.Equals(item.InputEntityIds[0], edgeOutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            message = "No 3D Line Fit step is routed from the smoke EdgePointSet output.";
            return false;
        }
        SelectedPipelineStep = step;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["FitMethod"] = "DeterministicConsensusOrthogonalTls",
            ["MaximumOrthogonalResidual"] = maximumResidual,
            ["MinimumInlierCount"] = minimumInlierCount,
            ["MinimumInlierRatio"] = minimumInlierRatio,
            ["MinimumInlierScanlineSpan"] = minimumInlierScanlineSpan,
            ["HypothesisPolicy"] = "Sha256PairSchedule",
            ["MaximumHypotheses"] = "256",
            ["RefinementPolicy"] = "OrthogonalTlsUntilStable10",
            ["DirectionPolicy"] = "PositiveScanlineAxis",
            ["EndpointPolicy"] = "InlierProjectionExtents"
        };
        foreach (var pair in values)
        {
            var parameter = step.Parameters.SingleOrDefault(item => item.Name == pair.Key);
            if (parameter is null)
            {
                message = $"Line Fit smoke parameter is missing: {pair.Key}.";
                return false;
            }
            parameter.Value = pair.Value;
        }
        message = "Smoke-only Line Fit limits configured in memory. They are not saved teaching values or production evidence.";
        return true;
    }

    public void SelectLineFitDiagnostic(int inputPointIndex) =>
        SelectedLineFitDiagnostic = LineFitPointDiagnostics.FirstOrDefault(point => point.InputPointIndex == inputPointIndex);

    private bool CanPreviewSelectedLineFit()
    {
        if (!IsSelectedStepLineFit || !IsSourceReadyForRecipe || HasPendingStepParameterChanges
            || isLineFitPreviewRunning || isEdgePreviewRunning
            || SelectedPipelineStep is null) return false;
        return TryGetPublishedHeightDifferenceEdgeOutput(SelectedPipelineStep.InputEntityIds.Single(), out var publishedEdge)
            && publishedEdge is not null
            && ToolRecipeLineFitExecution.TryPrepare(CreateDocument(), SelectedPipelineStep.Id, publishedEdge, out _, out _);
    }

    private void PublishSelectedLineFit()
    {
        if (SelectedPipelineStep is not { } step || !HasCurrentLineFitPreview) return;
        isLineFitPreviewPublished = true;
        publishedLineFitOutputs[lineFitPreviewOutput!.OutputEntityId] = lineFitPreviewOutput;
        step.State = "Published";
        SetLineFitSummary($"Published exact Preview as {step.OutputEntityId} | SHA-256 {lineFitPreviewOutput!.ContentSha256} | feature extraction only, no OK/NG");
        AppendLog("Publish", $"3D Line Fit output published without re-running: {step.OutputEntityId}.");
        RaiseLineFitDisplayRequested(lineFitPreviewOutput, true);
        RefreshLineIntersectionExecutionState();
    }

    private void CancelLineFitPreview() => lineFitPreviewCancellation?.Cancel();
    private void RaiseLineFitDisplayRequested(C3DLineFeature output, bool isPublished) => LineFitDisplayRequested?.Invoke(this, new ToolWorkbenchLineFitDisplayRequestEventArgs(output, isPublished));

    private void MarkLineFitPreviewStaleIfNeeded(object? sender = null)
    {
        if (lineFitPreviewOutput is null || isLineFitPreviewRunning) return;
        if (sender is not null)
        {
            var selected = SelectedPipelineStep;
            var selectedIsLineFit = string.Equals(selected?.ToolId, "three-d-line-fit", StringComparison.Ordinal);
            var selectedIsCurrentLineFit = selectedIsLineFit
                && string.Equals(selected?.OutputEntityId, lineFitPreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase);
            var isSelectedLineFitParameter = selectedIsLineFit
                && sender is ToolWorkbenchParameterItem parameter
                && (selected?.Parameters.Contains(parameter) ?? false);
            if (!selectedIsCurrentLineFit
                || (!(ReferenceEquals(sender, selected)) && !isSelectedLineFitParameter))
            {
                return;
            }
        }
        isLineFitPreviewStale = true;
        isLineFitPreviewPublished = false;
        publishedLineFitOutputs.Clear();
        MarkLineIntersectionPreviewStaleIfNeeded();
        LineFitResidualPlotPoints.Clear();
        var step = PipelineSteps.FirstOrDefault(item => string.Equals(item.OutputEntityId, lineFitPreviewOutput.OutputEntityId, StringComparison.OrdinalIgnoreCase));
        if (step is not null) step.State = "Preview stale";
        LineFitDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetLineFitSummary("Input, Line Fit parameter, route, or output changed. Preview again before Publish.");
    }

    private void ClearLineFitPreview(string summary)
    {
        lineFitPreviewCancellation?.Cancel();
        lineFitPreviewOutput = null;
        publishedLineFitOutputs.Clear();
        ClearLineIntersectionPreview("Upstream LineFeature was cleared. Line Intersection Preview was cleared without execution.");
        selectedLineFitDiagnostic = null;
        LineFitResidualPlotPoints.Clear();
        isLineFitPreviewStale = false;
        isLineFitPreviewPublished = false;
        LineFitDisplayCleared?.Invoke(this, EventArgs.Empty);
        SetLineFitSummary(summary);
        OnPropertyChanged(nameof(LineFitPointDiagnostics));
        OnPropertyChanged(nameof(LineFitResidualPlotPoints));
        OnPropertyChanged(nameof(SelectedLineFitDiagnostic));
    }

    private void RefreshLineFitExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepLineFit));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        OnPropertyChanged(nameof(LineFitUpstreamSummary));
        OnPropertyChanged(nameof(LineFitPointDiagnostics));
        if (SelectedPipelineStep is { } step && IsSelectedStepLineFit
            && (lineFitPreviewOutput is null
                || !string.Equals(lineFitPreviewOutput.OutputEntityId, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || isLineFitPreviewStale)
            && !isLineFitPreviewRunning)
        {
            if (!TryGetPublishedHeightDifferenceEdgeOutput(step.InputEntityIds.Single(), out var publishedEdge) || publishedEdge is null)
            {
                step.State = "Waiting for upstream";
            }
            else if (ToolRecipeLineFitExecution.TryPrepare(CreateDocument(), step.Id, publishedEdge, out _, out var message))
            {
                step.State = "Ready";
                lineFitExecutionSummary = "Ready for explicit Preview. Height Difference Edge will not run implicitly.";
            }
            else
            {
                step.State = "Taught incomplete";
                lineFitExecutionSummary = message;
            }
        }
        OnPropertyChanged(nameof(LineFitExecutionSummary));
        OnPropertyChanged(nameof(LineFitOutputHashSummary));
        RefreshLineFitCommands();
    }

    private void RefreshLineFitCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }

    private void SetLineFitRunning(bool value)
    {
        isLineFitPreviewRunning = value;
        OnPropertyChanged(nameof(IsLineFitPreviewRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshLineFitCommands();
    }

    private void SetLineFitSummary(string value)
    {
        lineFitExecutionSummary = value;
        RebuildEntities();
        OnPropertyChanged(nameof(LineFitExecutionSummary));
        OnPropertyChanged(nameof(LineFitOutputHashSummary));
        OnPropertyChanged(nameof(HasCurrentLineFitPreview));
        OnPropertyChanged(nameof(IsLineFitPreviewStale));
        OnPropertyChanged(nameof(IsLineFitPreviewPublished));
        OnPropertyChanged(nameof(LineFitPointDiagnostics));
        selectLineFitDiagnosticCommand?.RaiseCanExecuteChanged();
        RefreshLineFitCommands();
    }

    private void RebuildLineFitResidualPlot(C3DLineFeature output)
    {
        LineFitResidualPlotPoints.Clear();
        var points = output.PointDiagnostics;
        if (points.Count == 0) return;
        var maximum = Math.Max(output.MaximumOrthogonalResidual, points.Max(point => point.OrthogonalResidual));
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = points.Count == 1 ? 142 : 8 + 268 * index / (points.Count - 1.0);
            var y = 64 - 54 * point.OrthogonalResidual / maximum;
            LineFitResidualPlotPoints.Add(new LineFitResidualPlotPoint(point.InputPointIndex, point.ScanlineIndex, Math.Clamp(x, 0, 280), Math.Clamp(y, 2, 64), point.IsInlier, point.OrthogonalResidual));
        }
    }
}

public sealed class ToolWorkbenchLineFitDisplayRequestEventArgs(C3DLineFeature output, bool isPublished) : EventArgs
{
    public C3DLineFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

public sealed record LineFitResidualPlotPoint(int InputPointIndex, int ScanlineIndex, double PlotX, double PlotY, bool IsInlier, double Residual);

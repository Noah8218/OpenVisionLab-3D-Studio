using System.Collections.ObjectModel;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    public event EventHandler<ToolWorkbenchLineFitDisplayRequestEventArgs>? LineFitDisplayRequested;
    public event EventHandler? LineFitDisplayCleared;

    public bool IsSelectedStepLineFit => lineFitExecutionOwner.IsSelectedStepLineFit;
    public bool IsLineFitPreviewRunning => lineFitExecutionOwner.IsPreviewRunning;
    public bool HasCurrentLineFitPreview => lineFitExecutionOwner.HasCurrentPreview;
    public bool IsLineFitPreviewStale => lineFitExecutionOwner.IsPreviewStale;
    public bool IsLineFitPreviewPublished => lineFitExecutionOwner.IsPreviewPublished;
    internal C3DLineFeature? CurrentLineFitOutput => lineFitExecutionOwner.CurrentOutput;

    internal bool TryGetPublishedLineFitOutput(
        string outputEntityId,
        out C3DLineFeature? output) =>
        lineFitExecutionOwner.TryGetPublishedOutput(outputEntityId, out output);

    public C3DLineFeaturePointDiagnostic? SelectedLineFitDiagnostic =>
        lineFitExecutionOwner.SelectedDiagnostic;
    public IReadOnlyList<C3DLineFeaturePointDiagnostic> LineFitPointDiagnostics =>
        lineFitExecutionOwner.PointDiagnostics;
    public ObservableCollection<LineFitResidualPlotPoint> LineFitResidualPlotPoints =>
        lineFitExecutionOwner.ResidualPlotPoints;
    public ICommand SelectLineFitDiagnosticCommand =>
        lineFitExecutionOwner.SelectDiagnosticCommand;
    public string LineFitExecutionSummary => lineFitExecutionOwner.ExecutionSummary;
    public string LineFitOutputHashSummary => lineFitExecutionOwner.OutputHashSummary;
    public string LineFitUpstreamSummary => lineFitExecutionOwner.UpstreamSummary;
    public string LineFitSelectedDiagnosticSummary =>
        lineFitExecutionOwner.SelectedDiagnosticSummary;

    public Task<bool> PreviewSelectedLineFitAsync() =>
        lineFitExecutionOwner.PreviewAsync();

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
            && string.Equals(
                item.InputEntityIds[0],
                edgeOutputEntityId,
                StringComparison.OrdinalIgnoreCase));
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
        lineFitExecutionOwner.SelectDiagnostic(inputPointIndex);

    private bool CanPreviewSelectedLineFit() => lineFitExecutionOwner.CanPreview();
    private void PublishSelectedLineFit() => lineFitExecutionOwner.Publish();
    private void CancelLineFitPreview() => lineFitExecutionOwner.Cancel();
    private void MarkLineFitPreviewStaleIfNeeded(object? sender = null) =>
        lineFitExecutionOwner.MarkStaleIfNeeded(sender);
    private void ClearLineFitPreview(string summary) => lineFitExecutionOwner.Clear(summary);
    private void RefreshLineFitExecutionState() => lineFitExecutionOwner.RefreshState();

    private void RefreshLineFitCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }
}

public sealed class ToolWorkbenchLineFitDisplayRequestEventArgs(
    C3DLineFeature output,
    bool isPublished) : EventArgs
{
    public C3DLineFeature Output { get; } = output;
    public bool IsPublished { get; } = isPublished;
}

public sealed record LineFitResidualPlotPoint(
    int InputPointIndex,
    int ScanlineIndex,
    double PlotX,
    double PlotY,
    bool IsInlier,
    double Residual);

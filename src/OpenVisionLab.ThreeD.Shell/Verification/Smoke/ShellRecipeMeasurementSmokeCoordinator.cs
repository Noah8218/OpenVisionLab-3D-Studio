using System.IO;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Snapshot of the recipe-measurement Smoke switches owned by the command-line
/// parser. Keeping the snapshot separate makes the coordinator independent of
/// MainWindow field state while preserving the existing flags and order.
/// </summary>
internal sealed record ShellRecipeMeasurementSmokeRequest
{
    public string? ThicknessRepeatGridMode { get; init; }
    public bool FilterPublish { get; init; }
    public bool TwoPointLinePreview { get; init; }
    public bool TwoPointLinePublish { get; init; }
    public bool ThreePointPlanePreview { get; init; }
    public bool ThreePointPlanePublish { get; init; }
    public bool DatumPlaneDeviationPreview { get; init; }
    public bool DatumPlaneDeviationPublish { get; init; }
    public bool EdgePreview { get; init; }
    public bool EdgePublish { get; init; }
    public bool LineFitPreview { get; init; }
    public string? EdgeStepId { get; init; }
    public string? EdgeSmokeReportPath { get; init; }
    public string? LineFitSmokeReportPath { get; init; }
}

/// <summary>
/// Owns the recipe measurement Preview/Publish Smoke policy and its
/// smoke-only diagnostic reports. MainWindow supplies only the Workbench and
/// the two View callbacks needed to frame the repeat-grid panel.
/// </summary>
internal sealed class ShellRecipeMeasurementSmokeCoordinator
{
    private readonly ToolWorkbenchViewModel workbench;

    public ShellRecipeMeasurementSmokeCoordinator(ToolWorkbenchViewModel workbench)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
    }

    public async Task<string?> RunAsync(
        ShellRecipeMeasurementSmokeRequest request,
        Action bringThicknessRepeatGridIntoView,
        Func<Task> yieldRenderAsync)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(bringThicknessRepeatGridIntoView);
        ArgumentNullException.ThrowIfNull(yieldRenderAsync);

        if (request.ThicknessRepeatGridMode is not null)
        {
            if (!workbench.BeginThicknessRepeatGridCommand.CanExecute(null))
            {
                return "Thickness repeat-grid smoke requires one complete selected Thickness step.";
            }

            workbench.BeginThicknessRepeatGridCommand.Execute(null);
            if (string.Equals(
                    request.ThicknessRepeatGridMode,
                    "apply",
                    StringComparison.OrdinalIgnoreCase))
            {
                if (!workbench.ApplyThicknessRepeatGridCommand.CanExecute(null))
                {
                    return workbench.ThicknessRepeatGridValidationSummary;
                }
                workbench.ApplyThicknessRepeatGridCommand.Execute(null);
            }
            else if (!string.Equals(
                         request.ThicknessRepeatGridMode,
                         "review",
                         StringComparison.OrdinalIgnoreCase))
            {
                return $"Unknown Thickness repeat-grid smoke mode: {request.ThicknessRepeatGridMode}.";
            }

            bringThicknessRepeatGridIntoView();
            await yieldRenderAsync();
        }

        if (request.FilterPublish)
        {
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsFilterPreviewPublished)
            {
                return "Filter Publish did not accept the current Preview output.";
            }
        }

        if (request.TwoPointLinePreview
            && (string.IsNullOrWhiteSpace(request.EdgeStepId)
                || !workbench.SelectPipelineStep(request.EdgeStepId)
                || !await workbench.PreviewSelectedTwoPointLineAsync()))
        {
            return workbench.TwoPointLineExecutionSummary;
        }

        if (request.TwoPointLinePublish)
        {
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsTwoPointLinePreviewPublished)
            {
                return "2-Point Line Publish did not accept the current Preview output.";
            }
        }

        if (request.ThreePointPlanePreview
            && (string.IsNullOrWhiteSpace(request.EdgeStepId)
                || !workbench.SelectPipelineStep(request.EdgeStepId)
                || !await workbench.PreviewSelectedThreePointPlaneAsync()))
        {
            return workbench.ThreePointPlaneExecutionSummary;
        }

        if (request.ThreePointPlanePublish)
        {
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsThreePointPlanePreviewPublished)
            {
                return "3-Point Plane Publish did not accept the current Preview output.";
            }
        }

        if (request.DatumPlaneDeviationPreview)
        {
            var datumStep = workbench.PipelineSteps.SingleOrDefault(step =>
                string.Equals(step.ToolId, "datum-plane-raw-height-deviation", StringComparison.Ordinal));
            var planeStep = datumStep is null
                ? null
                : workbench.PipelineSteps.SingleOrDefault(step =>
                    string.Equals(step.ToolId, "three-point-plane", StringComparison.Ordinal)
                    && string.Equals(
                        step.OutputEntityId,
                        datumStep.InputEntityIds.ElementAtOrDefault(1),
                        StringComparison.OrdinalIgnoreCase));
            if (datumStep is null
                || planeStep is null
                || !workbench.SelectPipelineStep(planeStep.Id)
                || !await workbench.PreviewSelectedThreePointPlaneAsync())
            {
                return "Datum Plane Deviation smoke could not create its explicit Published 3-Point Plane prerequisite.";
            }

            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsThreePointPlanePreviewPublished
                || !workbench.SelectPipelineStep(datumStep.Id)
                || !await workbench.PreviewSelectedDatumPlaneDeviationAsync())
            {
                return workbench.DatumPlaneDeviationExecutionSummary;
            }
        }

        if (request.DatumPlaneDeviationPublish)
        {
            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsDatumPlaneDeviationPreviewPublished)
            {
                return "Datum Plane Deviation Publish did not accept the current Preview output.";
            }
        }

        if (request.EdgePreview)
        {
            var filterStep = workbench.PipelineSteps.FirstOrDefault(step =>
                string.Equals(step.ToolId, "filter", StringComparison.Ordinal));
            if (filterStep is null
                || !workbench.SelectPipelineStep(filterStep.Id)
                || !await workbench.PreviewSelectedFilterAsync())
            {
                return "Edge smoke could not create the explicit Filter Preview prerequisite.";
            }

            workbench.PublishSelectedStepCommand.Execute(null);
            if (!workbench.IsFilterPreviewPublished
                || string.IsNullOrWhiteSpace(request.EdgeStepId)
                || !workbench.TryConfigureHeightDifferenceEdgeSmoke(
                    request.EdgeStepId,
                    new ToolRecipeGridRectangle(156, 180, 135, 16),
                    "AcrossColumns",
                    "Rising",
                    "4",
                    out var edgeConfiguration))
            {
                return "Edge smoke prerequisite failed: Published Filter or smoke-only search band is unavailable.";
            }

            OVLog.Write(
                LogCategory.UI,
                LogLevel.Info,
                $"Edge smoke-only configuration: {edgeConfiguration}; no recipe file was saved.");
            if (!await workbench.PreviewSelectedHeightDifferenceEdgeAsync())
            {
                return workbench.HeightDifferenceEdgeExecutionSummary;
            }

            if (request.EdgeSmokeReportPath is not null
                && workbench.CurrentHeightDifferenceEdgeOutput is { } edgeOutput)
            {
                WriteEdgeReport(request.EdgeSmokeReportPath, edgeOutput);
            }

            if (request.EdgePublish)
            {
                workbench.PublishSelectedStepCommand.Execute(null);
                if (!workbench.IsEdgePreviewPublished)
                {
                    return "Height Difference Edge Publish did not reuse the current Preview output.";
                }
            }
        }

        if (request.LineFitPreview)
        {
            if (!workbench.IsEdgePreviewPublished)
            {
                workbench.PublishSelectedStepCommand.Execute(null);
            }

            if (workbench.CurrentHeightDifferenceEdgeOutput is not { } edgeOutput
                || !workbench.IsEdgePreviewPublished
                || !workbench.TryConfigureLineFitSmoke(
                    edgeOutput.OutputEntityId,
                    "100",
                    "3",
                    "0.10",
                    "2",
                    out var lineFitConfiguration)
                || !await workbench.PreviewSelectedLineFitAsync())
            {
                return $"Line Fit smoke prerequisite failed: {workbench.LineFitExecutionSummary}";
            }

            OVLog.Write(LogCategory.UI, LogLevel.Info, lineFitConfiguration);
            if (request.LineFitSmokeReportPath is not null
                && workbench.CurrentLineFitOutput is { } lineFitOutput)
            {
                WriteLineFitReport(
                    request.LineFitSmokeReportPath,
                    lineFitOutput,
                    workbench.LineFitResidualPlotPoints.Count);
            }
        }

        return null;
    }

    private static void WriteEdgeReport(
        string reportPath,
        C3DHeightDifferenceEdgePointSet edgeOutput)
    {
        var diagnostics = edgeOutput.Diagnostics;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(
            reportPath,
            [
                "OpenVisionLab 3D Height Difference Edge smoke-only report",
                "Boundary|Smoke-only band and threshold are not saved teaching values or production evidence.",
                $"Input|entity={edgeOutput.InputEntityId}|sha256={edgeOutput.InputContentSha256}|rootSha256={edgeOutput.RootSourceSha256}",
                $"Selection|row={edgeOutput.Selection.Row}|column={edgeOutput.Selection.Column}|rowCount={edgeOutput.Selection.RowCount}|columnCount={edgeOutput.Selection.ColumnCount}",
                $"Rule|axis={edgeOutput.ComparisonAxis}|polarity={edgeOutput.Polarity}|minimumDelta={edgeOutput.MinimumDelta:R}",
                $"Output|entity={edgeOutput.OutputEntityId}|points={edgeOutput.Points.Count}|sha256={edgeOutput.ContentSha256}",
                $"Diagnostics|scanlines={diagnostics.ScanlineCount}|eligiblePairs={diagnostics.EligiblePairCount}|missingPairSkips={diagnostics.SkippedMissingPairCount}|accepted={diagnostics.AcceptedScanlineCount}|noCandidate={diagnostics.NoCandidateScanlineCount}|magnitudeMin={diagnostics.AcceptedMagnitudeMinimum:R}|magnitudeMax={diagnostics.AcceptedMagnitudeMaximum:R}|magnitudeMean={diagnostics.AcceptedMagnitudeMean:R}"
            ]);
    }

    private static void WriteLineFitReport(
        string reportPath,
        C3DLineFeature lineFitOutput,
        int plotPointCount)
    {
        var diagnostics = lineFitOutput.Diagnostics;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(
            reportPath,
            [
                "OpenVisionLab 3D Line Fit smoke-only report",
                "Boundary|Smoke-only limits are not saved teaching values, production evidence, inspection OK/NG, or metrology evidence.",
                $"Input|entity={lineFitOutput.InputEdgePointSetEntityId}|sha256={lineFitOutput.InputContentSha256}|rootSha256={lineFitOutput.RootSourceSha256}",
                $"Output|entity={lineFitOutput.OutputEntityId}|sha256={lineFitOutput.ContentSha256}|points={diagnostics.InputPointCount}|inliers={diagnostics.InlierCount}|outliers={diagnostics.OutlierCount}",
                $"Diagnostics|residualRms={diagnostics.ResidualRms:R}|residualMax={diagnostics.ResidualMaximum:R}|scanlineSpan={diagnostics.InlierScanlineSpan}|plotPoints={plotPointCount}",
                $"Line|anchor={lineFitOutput.AnchorX:R},{lineFitOutput.AnchorY:R},{lineFitOutput.AnchorZ:R}|direction={lineFitOutput.DirectionX:R},{lineFitOutput.DirectionY:R},{lineFitOutput.DirectionZ:R}"
            ]);
    }
}

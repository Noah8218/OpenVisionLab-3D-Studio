using System.Security.Cryptography;
using System.Text;
using NoahCompletenessCellDecision = Lib.ThreeD.FeatureExtraction.CompletenessCellDecision;
using NoahCompletenessCoverageDisposition = Lib.ThreeD.FeatureExtraction.CompletenessCoverageDisposition;
using NoahCompletenessGridInspectionTool = Lib.ThreeD.FeatureExtraction.CompletenessGridInspectionTool;
using NoahCompletenessGridProfile = Lib.ThreeD.FeatureExtraction.CompletenessGridProfile;
using NoahCompletenessHeightDisposition = Lib.ThreeD.FeatureExtraction.CompletenessHeightDisposition;
using NoahCompletenessPresencePolicy = Lib.ThreeD.FeatureExtraction.CompletenessPresencePolicy;
using NoahHeightGridRegion = Lib.ThreeD.FeatureExtraction.HeightGridRegion;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DCompletenessGridInput(
    string OutputEntityId,
    string RootSourceEntityId,
    string InputEntityId,
    string InputContentSha256,
    string Unit,
    string FrameId,
    int GridWidth,
    int GridHeight,
    IReadOnlyList<double> Values,
    ToolRecipeSelection ReferenceSelection,
    ToolRecipeSelection InspectionGridSelection,
    C3DCompletenessGridProfile Profile,
    C3DCompletenessPresencePolicy? PresencePolicy = null);

public sealed record C3DCompletenessGridEvaluation(
    ToolResult Result,
    C3DCompletenessGridMetricOutput? Output);

public static class C3DCompletenessGridRule
{
    public const string ToolName = "Completeness Grid";

    public static C3DCompletenessGridEvaluation Evaluate(
        C3DCompletenessGridInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            ValidateStudioContract(input);
            var reference = input.ReferenceSelection.GridRectangle!;
            var inspection = input.InspectionGridSelection.GridRectangle!;
            var inspectionResult = new NoahCompletenessGridInspectionTool().Execute(
                input.GridHeight,
                input.GridWidth,
                input.Values,
                ToNoahRegion(reference),
                ToNoahRegion(inspection),
                new NoahCompletenessGridProfile
                {
                    Rows = input.Profile.Rows,
                    Columns = input.Profile.Columns,
                    XPitchColumns = input.Profile.XPitchColumns,
                    ZPitchRows = input.Profile.ZPitchRows,
                    CellWidthColumns = input.Profile.CellWidthColumns,
                    CellHeightRows = input.Profile.CellHeightRows
                },
                input.PresencePolicy is null
                    ? null
                    : new NoahCompletenessPresencePolicy
                    {
                        MinimumFiniteCoverageRatio = input.PresencePolicy.MinimumFiniteCoverageRatio,
                        MinimumReferenceRelativeMeanHeight = input.PresencePolicy.MinimumReferenceRelativeMeanRawHeight,
                        MaximumReferenceRelativeMeanHeight = input.PresencePolicy.MaximumReferenceRelativeMeanRawHeight
                    });
            if (!inspectionResult.Success)
            {
                throw new InvalidDataException(inspectionResult.Message);
            }

            var referenceMean = inspectionResult.ReferenceMeanHeight;
            var cells = inspectionResult.Cells
                .Select(cell =>
                {
                    var decision = ToStudioDecision(cell.Decision);
                    return new C3DCompletenessCellMetric(
                        $"r{cell.GridRow + 1:D3}.c{cell.GridColumn + 1:D3}",
                        cell.GridRow,
                        cell.GridColumn,
                        new ToolRecipeGridRectangle(
                            cell.Region.Row,
                            cell.Region.Column,
                            cell.Region.RowCount,
                            cell.Region.ColumnCount),
                        cell.TotalCellCount,
                        cell.FiniteCellCount,
                        cell.MissingCellCount,
                        cell.FiniteCoverageRatio,
                        cell.MeanHeight,
                        cell.ReferenceMeanHeight,
                        cell.ReferenceRelativeMeanHeight,
                        decision,
                        CreateDecisionReason(
                            cell.CoverageDisposition,
                            cell.HeightDisposition,
                            input.PresencePolicy,
                            decision));
                })
                .ToArray();

            var hash = CalculateContentSha256(input, referenceMean, cells);
            var passedCellCount = inspectionResult.PassedCellCount;
            var failedCellCount = inspectionResult.FailedCellCount;
            var aggregateStatus = inspectionResult.AggregateDecision switch
            {
                NoahCompletenessCellDecision.Pass => ResultStatus.Pass,
                NoahCompletenessCellDecision.Fail => ResultStatus.Fail,
                _ => ResultStatus.Warning
            };
            var cellOverlays = input.PresencePolicy is null
                ? []
                : cells.Select(cell => new C3DCompletenessCellOverlay(
                    $"overlay.completeness.{input.OutputEntityId}.{cell.CellId}",
                    cell.CellId,
                    cell.Region,
                    cell.Decision ?? ResultStatus.Fail)).ToArray();
            var output = new C3DCompletenessGridMetricOutput(
                input.OutputEntityId,
                input.RootSourceEntityId,
                input.InputEntityId,
                input.InputContentSha256,
                input.Unit,
                input.FrameId,
                input.ReferenceSelection.Id,
                reference,
                inspectionResult.ReferenceFiniteCellCount,
                referenceMean,
                input.InspectionGridSelection.Id,
                inspection,
                input.Profile,
                cells,
                hash,
                input.PresencePolicy,
                passedCellCount,
                failedCellCount,
                aggregateStatus,
                cellOverlays);
            var metrics = new List<Metric>
            {
                new(
                    input.PresencePolicy is null ? "Cell count" : "Passed cells",
                    MetricKind.Count,
                    input.PresencePolicy is null ? cells.Length : passedCellCount,
                    "cells",
                    aggregateStatus),
                new("Reference mean raw height", MetricKind.Number, referenceMean, input.Unit),
                new("Reference finite cells", MetricKind.Count, inspectionResult.ReferenceFiniteCellCount, "cells")
            };
            if (input.PresencePolicy is not null)
            {
                metrics.Add(new Metric(
                    "Failed cells",
                    MetricKind.Count,
                    failedCellCount,
                    "cells",
                    aggregateStatus));
            }
            foreach (var cell in cells)
            {
                metrics.Add(new Metric(
                    C3DCompletenessMetricNames.FiniteCoverage(cell.CellId),
                    MetricKind.Number,
                    cell.FiniteCoverageRatio,
                    "ratio",
                    cell.Decision));
                if (cell.ReferenceRelativeMeanRawHeight is { } relative)
                {
                    metrics.Add(new Metric(
                        C3DCompletenessMetricNames.ReferenceRelativeMean(
                            cell.CellId),
                        MetricKind.Deviation,
                        relative,
                        input.Unit,
                        cell.Decision));
                }
            }

            var result = new ToolResult(
                ToolName,
                aggregateStatus,
                input.PresencePolicy is null
                    ? $"Calculated {cells.Length} deterministic cell metrics; no acceptance policy was applied."
                    : $"Completeness Grid {aggregateStatus}: {passedCellCount} passed, {failedCellCount} failed, {cells.Length} total cells.",
                TimeSpan.Zero,
                metrics,
                cellOverlays.Select(overlay => new Overlay(
                    overlay.OverlayId,
                    OverlayKind.ColorMap,
                    $"{overlay.CellId} {overlay.Status}",
                    overlay.Status,
                    input.InputEntityId)).ToArray());
            return new C3DCompletenessGridEvaluation(result, output);
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            return new C3DCompletenessGridEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Error,
                    exception.Message,
                    TimeSpan.Zero,
                    [],
                    []),
                null);
        }
    }

    private static void ValidateStudioContract(C3DCompletenessGridInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.InputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.InputContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.FrameId);
        if (input.GridWidth < 1 || input.GridHeight < 1
            || input.Values.Count != checked(input.GridWidth * input.GridHeight))
        {
            throw new InvalidDataException(
                "Completeness Grid source dimensions and row-major value count must agree.");
        }

        if (input.ReferenceSelection.GridRectangle is not { } reference
            || input.InspectionGridSelection.GridRectangle is not { } inspection)
        {
            throw new InvalidDataException(
                "Completeness Grid v1 requires ordered Reference and Inspection GridRectangle selections.");
        }

        ValidateRegion(reference, input.GridWidth, input.GridHeight, "Reference ROI");
        ValidateRegion(inspection, input.GridWidth, input.GridHeight, "Inspection Grid ROI");
        if (input.Profile.CellShape != C3DCompletenessCellShape.GridRectangle)
        {
            throw new InvalidDataException(
                "Completeness Grid v1 supports only the typed GridRectangle cell shape.");
        }
    }

    private static void ValidateRegion(
        ToolRecipeGridRectangle region,
        int width,
        int height,
        string label)
    {
        if (region.Row < 0 || region.Column < 0
            || region.RowCount < 1 || region.ColumnCount < 1
            || region.Row + region.RowCount > height
            || region.Column + region.ColumnCount > width)
        {
            throw new InvalidDataException(
                $"{label} is outside the {width} x {height} source grid.");
        }
    }

    private static string CalculateContentSha256(
        C3DCompletenessGridInput input,
        double referenceMean,
        IReadOnlyList<C3DCompletenessCellMetric> cells)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write("OpenVisionLab.C3DCompletenessGridMetricOutput");
            writer.Write(input.PresencePolicy is null ? "1.0" : "1.1");
            writer.Write(input.OutputEntityId);
            writer.Write(input.RootSourceEntityId);
            writer.Write(input.InputEntityId);
            writer.Write(input.InputContentSha256.ToUpperInvariant());
            writer.Write(input.Unit);
            writer.Write(input.FrameId);
            writer.Write(input.ReferenceSelection.Id);
            Write(writer, input.ReferenceSelection.GridRectangle!);
            writer.Write(referenceMean);
            writer.Write(input.InspectionGridSelection.Id);
            Write(writer, input.InspectionGridSelection.GridRectangle!);
            writer.Write(input.Profile.Rows);
            writer.Write(input.Profile.Columns);
            writer.Write(input.Profile.XPitchColumns);
            writer.Write(input.Profile.ZPitchRows);
            writer.Write(input.Profile.CellWidthColumns);
            writer.Write(input.Profile.CellHeightRows);
            writer.Write(input.Profile.CellShape.ToString());
            writer.Write(cells.Count);
            foreach (var cell in cells)
            {
                writer.Write(cell.CellId);
                writer.Write(cell.GridRow);
                writer.Write(cell.GridColumn);
                Write(writer, cell.Region);
                writer.Write(cell.TotalCellCount);
                writer.Write(cell.FiniteCellCount);
                writer.Write(cell.MissingCellCount);
                writer.Write(cell.FiniteCoverageRatio);
                writer.Write(cell.MeanRawHeight.HasValue);
                if (cell.MeanRawHeight is { } mean) writer.Write(mean);
                writer.Write(cell.ReferenceRelativeMeanRawHeight.HasValue);
                if (cell.ReferenceRelativeMeanRawHeight is { } relative)
                {
                    writer.Write(relative);
                }
                if (input.PresencePolicy is not null)
                {
                    writer.Write(cell.Decision?.ToString() ?? string.Empty);
                    writer.Write(cell.DecisionReason);
                }
            }
            if (input.PresencePolicy is { } policy)
            {
                writer.Write(policy.MinimumFiniteCoverageRatio);
                writer.Write(policy.MinimumReferenceRelativeMeanRawHeight);
                writer.Write(policy.MaximumReferenceRelativeMeanRawHeight);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static string CreateDecisionReason(
        NoahCompletenessCoverageDisposition coverageDisposition,
        NoahCompletenessHeightDisposition heightDisposition,
        C3DCompletenessPresencePolicy? policy,
        ResultStatus? decision)
    {
        if (policy is null || decision is null)
        {
            return "No acceptance policy was authored.";
        }

        var coverage = coverageDisposition == NoahCompletenessCoverageDisposition.Accepted
            ? "coverage accepted"
            : "coverage below minimum";
        var height = heightDisposition switch
        {
            NoahCompletenessHeightDisposition.Missing => "finite mean missing",
            NoahCompletenessHeightDisposition.BelowMinimum => "relative mean below minimum",
            NoahCompletenessHeightDisposition.AboveMaximum => "relative mean above maximum",
            _ => "relative mean accepted"
        };
        return $"{decision}: {coverage}; {height}.";
    }

    private static NoahHeightGridRegion ToNoahRegion(ToolRecipeGridRectangle region) =>
        new(region.Row, region.Column, region.RowCount, region.ColumnCount);

    private static ResultStatus? ToStudioDecision(NoahCompletenessCellDecision decision) =>
        decision switch
        {
            NoahCompletenessCellDecision.Pass => ResultStatus.Pass,
            NoahCompletenessCellDecision.Fail => ResultStatus.Fail,
            _ => null
        };

    private static void Write(
        BinaryWriter writer,
        ToolRecipeGridRectangle region)
    {
        writer.Write(region.Row);
        writer.Write(region.Column);
        writer.Write(region.RowCount);
        writer.Write(region.ColumnCount);
    }
}

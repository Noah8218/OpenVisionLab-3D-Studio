using System.Security.Cryptography;
using System.Text;
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
            Validate(input);
            var reference = input.ReferenceSelection.GridRectangle!;
            var inspection = input.InspectionGridSelection.GridRectangle!;
            var referenceValues = FiniteValues(input, reference).ToArray();
            if (referenceValues.Length == 0)
            {
                throw new InvalidDataException(
                    "Completeness Grid v1 requires at least one finite cell in the explicit Reference ROI.");
            }

            var referenceMean = referenceValues.Average();
            var cells = new List<C3DCompletenessCellMetric>(
                checked(input.Profile.Rows * input.Profile.Columns));
            for (var gridRow = 0; gridRow < input.Profile.Rows; gridRow++)
            {
                for (var gridColumn = 0;
                     gridColumn < input.Profile.Columns;
                     gridColumn++)
                {
                    var region = new ToolRecipeGridRectangle(
                        inspection.Row + gridRow * input.Profile.ZPitchRows,
                        inspection.Column + gridColumn * input.Profile.XPitchColumns,
                        input.Profile.CellHeightRows,
                        input.Profile.CellWidthColumns);
                    var finite = FiniteValues(input, region).ToArray();
                    var total = checked(region.RowCount * region.ColumnCount);
                    var mean = finite.Length == 0 ? (double?)null : finite.Average();
                    var coverage = finite.Length / (double)total;
                    double? relative = mean is null
                        ? null
                        : mean.Value - referenceMean;
                    var decision = input.PresencePolicy is null
                        ? (ResultStatus?)null
                        : coverage >= input.PresencePolicy.MinimumFiniteCoverageRatio
                          && relative is { } value
                          && value >= input.PresencePolicy.MinimumReferenceRelativeMeanRawHeight
                          && value <= input.PresencePolicy.MaximumReferenceRelativeMeanRawHeight
                            ? ResultStatus.Pass
                            : ResultStatus.Fail;
                    cells.Add(new C3DCompletenessCellMetric(
                        $"r{gridRow + 1:D3}.c{gridColumn + 1:D3}",
                        gridRow,
                        gridColumn,
                        region,
                        total,
                        finite.Length,
                        total - finite.Length,
                        coverage,
                        mean,
                        referenceMean,
                        relative,
                        decision,
                        CreateDecisionReason(
                            coverage,
                            relative,
                            input.PresencePolicy,
                            decision)));
                }
            }

            var hash = CalculateContentSha256(input, referenceMean, cells);
            var passedCellCount = cells.Count(cell => cell.Decision == ResultStatus.Pass);
            var failedCellCount = cells.Count(cell => cell.Decision == ResultStatus.Fail);
            var aggregateStatus = input.PresencePolicy is null
                ? ResultStatus.Warning
                : failedCellCount == 0
                    ? ResultStatus.Pass
                    : ResultStatus.Fail;
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
                referenceValues.Length,
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
                    input.PresencePolicy is null ? cells.Count : passedCellCount,
                    "cells",
                    aggregateStatus),
                new("Reference mean raw height", MetricKind.Number, referenceMean, input.Unit),
                new("Reference finite cells", MetricKind.Count, referenceValues.Length, "cells")
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
                    ? $"Calculated {cells.Count} deterministic cell metrics; no acceptance policy was applied."
                    : $"Completeness Grid {aggregateStatus}: {passedCellCount} passed, {failedCellCount} failed, {cells.Count} total cells.",
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

    private static void Validate(C3DCompletenessGridInput input)
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
        if (input.Profile.Rows < 1 || input.Profile.Columns < 1
            || input.Profile.XPitchColumns < input.Profile.CellWidthColumns
            || input.Profile.ZPitchRows < input.Profile.CellHeightRows
            || input.Profile.CellWidthColumns < 1
            || input.Profile.CellHeightRows < 1)
        {
            throw new InvalidDataException(
                "Completeness Grid profile requires positive non-overlapping rows, columns, pitch, and cell size.");
        }

        var requiredRows = checked(
            (input.Profile.Rows - 1) * input.Profile.ZPitchRows
            + input.Profile.CellHeightRows);
        var requiredColumns = checked(
            (input.Profile.Columns - 1) * input.Profile.XPitchColumns
            + input.Profile.CellWidthColumns);
        if (requiredRows > inspection.RowCount
            || requiredColumns > inspection.ColumnCount)
        {
            throw new InvalidDataException(
                $"Completeness Grid extent {requiredColumns} x {requiredRows} cells does not fit "
                + $"inside the authored Inspection Grid ROI {inspection.ColumnCount} x {inspection.RowCount}.");
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

    private static IEnumerable<double> FiniteValues(
        C3DCompletenessGridInput input,
        ToolRecipeGridRectangle region)
    {
        for (var row = region.Row; row < region.Row + region.RowCount; row++)
        {
            for (var column = region.Column;
                 column < region.Column + region.ColumnCount;
                 column++)
            {
                var value = input.Values[row * input.GridWidth + column];
                if (double.IsFinite(value))
                {
                    yield return value;
                }
            }
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
        double finiteCoverageRatio,
        double? referenceRelativeMeanRawHeight,
        C3DCompletenessPresencePolicy? policy,
        ResultStatus? decision)
    {
        if (policy is null || decision is null)
        {
            return "No acceptance policy was authored.";
        }

        var coverage = finiteCoverageRatio >= policy.MinimumFiniteCoverageRatio
            ? "coverage accepted"
            : "coverage below minimum";
        var height = referenceRelativeMeanRawHeight is null
            ? "finite mean missing"
            : referenceRelativeMeanRawHeight < policy.MinimumReferenceRelativeMeanRawHeight
                ? "relative mean below minimum"
                : referenceRelativeMeanRawHeight > policy.MaximumReferenceRelativeMeanRawHeight
                    ? "relative mean above maximum"
                    : "relative mean accepted";
        return $"{decision}: {coverage}; {height}.";
    }

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

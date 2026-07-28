using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DDatumPlaneDeviationInput(
    string StepId,
    C3DHeightFieldSnapshot RawSource,
    C3DThreePointPlaneFeature Plane,
    ToolRecipeSelection MeasurementSelection,
    string OutputEntityId,
    double MaximumPeakToValleyRawHeight,
    int MinimumValidSampleCount,
    double MinimumAbsoluteNormalY,
    string OutputRole);

public sealed record C3DDatumPlaneDeviationEvaluation(
    ToolResult Result,
    C3DDatumPlaneDeviationFeature? Output);

/// <summary>
/// Studio owns strict C3D/recipe lineage and read-only display sampling. The
/// raw-height plane residual arithmetic itself remains in Library-Noah.
/// </summary>
public static class C3DDatumPlaneDeviationRule
{
    public const string ToolName = "C3D Datum Plane Raw-Height Deviation";

    public static C3DDatumPlaneDeviationEvaluation Evaluate(
        C3DDatumPlaneDeviationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            ValidateInput(input);
            cancellationToken.ThrowIfCancellationRequested();
            var rectangle = input.MeasurementSelection.GridRectangle!;
            var package = LibraryNoahHeightMapInspection.EvaluateDatumPlaneRawHeightDeviation(
                new LibraryNoahDatumPlaneRawHeightDeviationInspectionInput(
                    new LibraryNoahHeightMapInput(
                        input.RawSource.EntityId,
                        input.RawSource.Height,
                        input.RawSource.Width,
                        0d,
                        0d,
                        1d,
                        1d,
                        input.RawSource.Values.ToArray(),
                        input.RawSource.Unit,
                        input.RawSource.FrameId),
                    new LibraryNoahGridRoi(rectangle.Row, rectangle.Column, rectangle.RowCount, rectangle.ColumnCount),
                    input.Plane.NormalX,
                    input.Plane.NormalY,
                    input.Plane.NormalZ,
                    input.Plane.PlaneOffset,
                    input.MaximumPeakToValleyRawHeight,
                    input.MinimumValidSampleCount,
                    input.MinimumAbsoluteNormalY));

            var metrics = OrderMetrics(package.Result.Metrics);
            if (!package.HasMeasurement)
            {
                return new C3DDatumPlaneDeviationEvaluation(
                    new ToolResult(ToolName, ResultStatus.Error, package.Result.Message, package.Result.Elapsed, metrics, []),
                    null);
            }

            var outputStatus = package.Result.Status;
            var samples = CreateDisplaySamples(input, rectangle, cancellationToken);
            var output = C3DDatumPlaneDeviationFeature.Create(
                input.OutputEntityId,
                input.Plane,
                input.MeasurementSelection,
                input.MaximumPeakToValleyRawHeight,
                input.MinimumValidSampleCount,
                input.MinimumAbsoluteNormalY,
                MetricValue(metrics, "MinimumRawHeightResidual"),
                MetricValue(metrics, "MaximumRawHeightResidual"),
                MetricValue(metrics, "PeakToValleyRawHeight"),
                MetricValue(metrics, "RmsRawHeightResidual"),
                MetricCount(metrics, "ValidSampleCount"),
                MetricCount(metrics, "MissingSampleCount"),
                MetricCount(metrics, "MinimumResidualRow"),
                MetricCount(metrics, "MinimumResidualColumn"),
                MetricCount(metrics, "MaximumResidualRow"),
                MetricCount(metrics, "MaximumResidualColumn"),
                outputStatus,
                input.OutputRole,
                samples,
                $"datum-plane:{input.Plane.ContentSha256};selection:{C3DDatumPlaneDeviationFeature.CalculateMeasurementSelectionContentSha256(input.MeasurementSelection)}");

            return new C3DDatumPlaneDeviationEvaluation(
                new ToolResult(
                    ToolName,
                    outputStatus,
                    outputStatus == ResultStatus.Pass
                        ? "Datum-plane raw-height residuals are within the local limit. Source C3D is unchanged."
                        : "Datum-plane raw-height residuals exceed the local limit. Source C3D is unchanged.",
                    package.Result.Elapsed,
                    metrics,
                    [
                        new Overlay("overlay.c3d-datum-plane", OverlayKind.Plane, "Published manual datum plane", ResultStatus.Pass, input.Plane.OutputEntityId),
                        new Overlay("overlay.c3d-datum-deviation-roi", OverlayKind.Box, "Datum-plane raw-height measurement rectangle", outputStatus, input.RawSource.EntityId),
                        new Overlay("overlay.c3d-datum-deviation-map", OverlayKind.ColorMap, "Read-only sampled raw-height residual overlay", outputStatus, input.RawSource.EntityId)
                    ]),
                output);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            return new C3DDatumPlaneDeviationEvaluation(new ToolResult(ToolName, ResultStatus.Error, exception.Message, TimeSpan.Zero, [], []), null);
        }
    }

    /// <summary>
    /// Validates the typed source, plane, selection, and policy lineage only.
    /// This is intentionally callable by readiness checks without evaluating
    /// residuals or producing a Preview result.
    /// </summary>
    public static void ValidateInput(C3DDatumPlaneDeviationInput input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input.StepId);
        ArgumentNullException.ThrowIfNull(input.RawSource);
        ArgumentNullException.ThrowIfNull(input.Plane);
        ArgumentNullException.ThrowIfNull(input.MeasurementSelection);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputRole);
        var rectangle = input.MeasurementSelection.GridRectangle
            ?? throw new InvalidDataException("Datum-plane deviation requires one measurement GridRectangle.");
        if (!string.Equals(input.Plane.RootSourceEntityId, input.RawSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Plane.RootSourceSha256, input.RawSource.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.Plane.Unit, input.RawSource.Unit, StringComparison.Ordinal)
            || !string.Equals(input.Plane.FrameId, input.RawSource.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.MeasurementSelection.RootSourceId, input.RawSource.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(input.MeasurementSelection.FrameId, input.RawSource.FrameId, StringComparison.Ordinal)
            || !string.Equals(input.MeasurementSelection.SourceBinding.ContentSha256, input.RawSource.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || input.MeasurementSelection.SourceBinding.GridWidth != input.RawSource.Width
            || input.MeasurementSelection.SourceBinding.GridHeight != input.RawSource.Height)
        {
            throw new InvalidDataException("Datum-plane inputs do not share the exact current raw C3D source identity.");
        }
        if (rectangle.Row < 0 || rectangle.Column < 0 || rectangle.RowCount <= 0 || rectangle.ColumnCount <= 0
            || rectangle.Row > input.RawSource.Height - rectangle.RowCount
            || rectangle.Column > input.RawSource.Width - rectangle.ColumnCount)
        {
            throw new InvalidDataException("Datum-plane measurement rectangle is outside the current C3D grid.");
        }
        if (!double.IsFinite(input.MaximumPeakToValleyRawHeight) || input.MaximumPeakToValleyRawHeight <= 0d
            || input.MinimumValidSampleCount < 3
            || !double.IsFinite(input.MinimumAbsoluteNormalY) || input.MinimumAbsoluteNormalY <= 0d || input.MinimumAbsoluteNormalY > 1d)
        {
            throw new InvalidDataException("Datum-plane raw-height limits or validity policy are invalid.");
        }
    }

    private static IReadOnlyList<C3DDatumPlaneDeviationOverlaySample> CreateDisplaySamples(
        C3DDatumPlaneDeviationInput input,
        ToolRecipeGridRectangle rectangle,
        CancellationToken cancellationToken)
    {
        var area = checked((long)rectangle.RowCount * rectangle.ColumnCount);
        var stride = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(area / (double)C3DDatumPlaneDeviationFeature.MaximumOverlaySampleCount)));
        var samples = new List<C3DDatumPlaneDeviationOverlaySample>();
        var values = input.RawSource.Values.Span;
        for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row += stride)
        {
            for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column += stride)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rawHeight = values[row * input.RawSource.Width + column];
                if (!double.IsFinite(rawHeight)
                    || !LibraryNoahHeightMapInspection.TryCalculateDatumPlaneRawHeightResidual(
                        input.Plane.NormalX, input.Plane.NormalY, input.Plane.NormalZ, input.Plane.PlaneOffset,
                        column, row, rawHeight, out var residual))
                {
                    continue;
                }
                samples.Add(new C3DDatumPlaneDeviationOverlaySample(row, column, rawHeight, residual));
            }
        }
        if (samples.Count == 0) throw new InvalidDataException("Datum-plane display sampling found no finite residual samples.");
        return samples;
    }

    private static IReadOnlyList<Metric> OrderMetrics(IReadOnlyList<Metric> metrics)
    {
        var order = new[]
        {
            "PeakToValleyRawHeight", "RmsRawHeightResidual", "MinimumRawHeightResidual", "MaximumRawHeightResidual",
            "ValidSampleCount", "MissingSampleCount", "MaximumPeakToValleyRawHeight", "MinimumAbsoluteNormalY",
            "PlaneNormalX", "PlaneNormalY", "PlaneNormalZ", "PlaneOffset",
            "MinimumResidualRow", "MinimumResidualColumn", "MaximumResidualRow", "MaximumResidualColumn"
        };
        return metrics.OrderBy(metric => Array.IndexOf(order, metric.Name) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(metric => metric.Name, StringComparer.Ordinal).ToArray();
    }

    private static double MetricValue(IReadOnlyList<Metric> metrics, string name) =>
        metrics.FirstOrDefault(metric => string.Equals(metric.Name, name, StringComparison.Ordinal))?.Value ?? double.NaN;

    private static int MetricCount(IReadOnlyList<Metric> metrics, string name)
    {
        var value = MetricValue(metrics, name);
        return double.IsFinite(value) && value >= 0d && value <= int.MaxValue
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : -1;
    }
}

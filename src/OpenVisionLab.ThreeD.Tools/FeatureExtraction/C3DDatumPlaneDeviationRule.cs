using System.Threading;
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
/// raw-height plane residual arithmetic itself remains in OpenVisionLab Vision SDK.
/// </summary>
public static class C3DDatumPlaneDeviationRule
{
    public const string ToolName = "C3D Datum Plane Raw-Height Deviation";

    public static C3DDatumPlaneDeviationEvaluation Evaluate(
        C3DDatumPlaneDeviationInput input,
        CancellationToken cancellationToken = default)
    {
            cancellationToken.ThrowIfCancellationRequested();
            C3DDatumPlaneDeviationEvaluation Complete(C3DDatumPlaneDeviationEvaluation value) { cancellationToken.ThrowIfCancellationRequested(); return value; }
        ArgumentNullException.ThrowIfNull(input);
        try
        {
            ValidateInput(input);
            cancellationToken.ThrowIfCancellationRequested();
            var rectangle = input.MeasurementSelection.GridRectangle!;
            var package = VisionSdkHeightMapInspection.EvaluateDatumPlaneRawHeightDeviation(
                new VisionSdkDatumPlaneRawHeightDeviationInspectionInput(
                    new VisionSdkHeightMapInput(
                        input.RawSource.EntityId,
                        input.RawSource.Height,
                        input.RawSource.Width,
                        0d,
                        0d,
                        1d,
                        1d,
                        CopyValues(input.RawSource.Values, cancellationToken),
                        input.RawSource.Unit,
                        input.RawSource.FrameId),
                    new VisionSdkGridRoi(rectangle.Row, rectangle.Column, rectangle.RowCount, rectangle.ColumnCount),
                    input.Plane.NormalX,
                    input.Plane.NormalY,
                    input.Plane.NormalZ,
                    input.Plane.PlaneOffset,
                    input.MaximumPeakToValleyRawHeight,
                    input.MinimumValidSampleCount,
                    input.MinimumAbsoluteNormalY), cancellationToken);

            var metrics = OrderMetrics(package.Result.Metrics);
            if (!package.HasMeasurement)
            {
                return Complete(new C3DDatumPlaneDeviationEvaluation(
                    new ToolResult(ToolName, ResultStatus.Error, package.Result.Message, package.Result.Elapsed, metrics, []),
                    null));
            }

            var outputStatus = package.Result.Status;
            var samples = CreateDisplaySamples(input, rectangle, metrics, cancellationToken);
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

            return Complete(new C3DDatumPlaneDeviationEvaluation(
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
                output));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            return Complete(new C3DDatumPlaneDeviationEvaluation(new ToolResult(ToolName, ResultStatus.Error, exception.Message, TimeSpan.Zero, [], []), null));
        }
    }

    /// <summary>
    /// Validates the typed source, plane, selection, and policy lineage only.
    /// This is intentionally callable by readiness checks without evaluating
    /// residuals or producing a Preview result.
    /// </summary>
    private static double[] CopyValues(ReadOnlyMemory<double> source, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled) return source.ToArray();
        var result = new double[source.Length];
        for (var index = 0; index < result.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result[index] = source.Span[index];
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
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
        if (ToolRecipeGridRectangleGeometry.Validate(
                rectangle,
                input.RawSource.Width,
                input.RawSource.Height).Count > 0)
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
        IReadOnlyList<Metric> metrics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var minimumStride = 1;
        var maximumStride = Math.Max(rectangle.RowCount, rectangle.ColumnCount);
        // Bound the actual grid cardinality, including partial edge rows and columns.
        while (minimumStride < maximumStride)
        {
            var candidateStride = minimumStride + (maximumStride - minimumStride) / 2;
            var rowSampleCount = 1L + (rectangle.RowCount - 1L) / candidateStride;
            var columnSampleCount = 1L + (rectangle.ColumnCount - 1L) / candidateStride;
            if (rowSampleCount * columnSampleCount > C3DDatumPlaneDeviationFeature.MaximumOverlaySampleCount)
            {
                minimumStride = candidateStride + 1;
            }
            else
            {
                maximumStride = candidateStride;
            }
        }
        var stride = minimumStride;
        var samples = new List<C3DDatumPlaneDeviationOverlaySample>();
        var values = input.RawSource.Values.Span;
        var rowEnd = (long)rectangle.Row + rectangle.RowCount;
        var columnEnd = (long)rectangle.Column + rectangle.ColumnCount;
        for (var row = (long)rectangle.Row; row < rowEnd; row += stride)
        {
            for (var column = (long)rectangle.Column; column < columnEnd; column += stride)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var rawHeight = values[checked((int)(row * input.RawSource.Width + column))];
                if (!double.IsFinite(rawHeight)
                    || !VisionSdkHeightMapInspection.TryCalculateDatumPlaneRawHeightResidual(
                        input.Plane.NormalX, input.Plane.NormalY, input.Plane.NormalZ, input.Plane.PlaneOffset,
                        column, row, rawHeight, out var residual))
                {
                    continue;
                }
                samples.Add(new C3DDatumPlaneDeviationOverlaySample((int)row, (int)column, rawHeight, residual));
            }
        }
        if (samples.Count == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // A completed SDK measurement has a finite minimum in this same ROI,
            // even when every point on the display lattice is missing.
            var row = MetricCount(metrics, "MinimumResidualRow");
            var column = MetricCount(metrics, "MinimumResidualColumn");
            if (row < rectangle.Row || row >= rowEnd || column < rectangle.Column || column >= columnEnd)
            {
                throw new InvalidDataException("Datum-plane minimum residual location is outside the measurement rectangle.");
            }
            samples.Add(new C3DDatumPlaneDeviationOverlaySample(
                row,
                column,
                values[checked((int)((long)row * input.RawSource.Width + column))],
                MetricValue(metrics, "MinimumRawHeightResidual")));
        }
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

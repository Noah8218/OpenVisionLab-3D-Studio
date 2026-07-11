using System.Diagnostics;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record PlaneFlatnessRuleInput(
    string SourceEntityId,
    IReadOnlyList<HeightFieldPlaneSample> ReferenceSamples,
    IReadOnlyList<HeightFieldPlaneSample> MeasurementSamples,
    double Tolerance,
    string Unit);

public sealed record PlaneFlatnessEvaluation(
    ToolResult Result,
    HeightFieldPlaneFitResult? ReferencePlane,
    int ReferenceSampleCount,
    int MeasurementSampleCount,
    double MinimumSignedDistance,
    double MaximumSignedDistance,
    double Flatness,
    double RootMeanSquareDistance,
    Vector3 MinimumPoint,
    Vector3 MaximumPoint,
    Vector3 MinimumProjection,
    Vector3 MaximumProjection);

public static class PlaneFlatnessRule
{
    public const string ToolName = "C3D Plane Flatness";

    public static PlaneFlatnessEvaluation Evaluate(PlaneFlatnessRuleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(input.SourceEntityId))
        {
            return Error(input, "Source entity ID is required.", stopwatch.Elapsed);
        }

        if (string.IsNullOrWhiteSpace(input.Unit))
        {
            return Error(input, "Flatness unit is required.", stopwatch.Elapsed);
        }

        if (!double.IsFinite(input.Tolerance) || input.Tolerance <= 0.0)
        {
            return Error(input, "Flatness tolerance must be a positive finite value.", stopwatch.Elapsed);
        }

        if (input.ReferenceSamples is null || input.ReferenceSamples.Count < 3)
        {
            return Error(input, "Reference ROI must contain at least three finite samples.", stopwatch.Elapsed);
        }

        if (input.MeasurementSamples is null || input.MeasurementSamples.Count < 3)
        {
            return Error(input, "Measurement surface must contain at least three finite samples.", stopwatch.Elapsed);
        }

        HeightFieldPlaneFitResult referencePlane;
        try
        {
            referencePlane = HeightFieldPlaneFit.Fit(input.ReferenceSamples);
        }
        catch (ArgumentException exception)
        {
            return Error(input, exception.Message, stopwatch.Elapsed);
        }

        var minimumDistance = double.PositiveInfinity;
        var maximumDistance = double.NegativeInfinity;
        var squaredDistanceSum = 0.0;
        var minimumPoint = Vector3.Zero;
        var maximumPoint = Vector3.Zero;
        foreach (var sample in input.MeasurementSamples)
        {
            if (!IsFinite(sample))
            {
                return Error(input, "Measurement surface contains a non-finite sample.", stopwatch.Elapsed);
            }

            var distance = SignedDistance(sample.Position, referencePlane.Normal, referencePlane.Offset);
            squaredDistanceSum += distance * distance;
            if (distance < minimumDistance)
            {
                minimumDistance = distance;
                minimumPoint = sample.Position;
            }

            if (distance > maximumDistance)
            {
                maximumDistance = distance;
                maximumPoint = sample.Position;
            }
        }

        var flatness = maximumDistance - minimumDistance;
        var rootMeanSquareDistance = Math.Sqrt(squaredDistanceSum / input.MeasurementSamples.Count);
        var status = flatness <= input.Tolerance ? ResultStatus.Pass : ResultStatus.Fail;
        var minimumProjection = minimumPoint - referencePlane.Normal * (float)minimumDistance;
        var maximumProjection = maximumPoint - referencePlane.Normal * (float)maximumDistance;
        stopwatch.Stop();

        var result = new ToolResult(
            ToolName,
            status,
            status == ResultStatus.Pass
                ? "Measured surface flatness is within tolerance. Source geometry is unchanged."
                : "Measured surface flatness exceeds tolerance. Source geometry is unchanged.",
            stopwatch.Elapsed,
            [
                new Metric("Flatness", MetricKind.Deviation, flatness, input.Unit, status),
                new Metric("Flatness tolerance", MetricKind.Deviation, input.Tolerance, input.Unit, status),
                new Metric("Minimum signed deviation", MetricKind.Deviation, minimumDistance, input.Unit),
                new Metric("Maximum signed deviation", MetricKind.Deviation, maximumDistance, input.Unit),
                new Metric("Surface RMS deviation", MetricKind.Deviation, rootMeanSquareDistance, input.Unit),
                new Metric("Reference fit RMS", MetricKind.Deviation, referencePlane.RootMeanSquareDistance, input.Unit),
                new Metric("Reference sample count", MetricKind.Count, input.ReferenceSamples.Count, "count"),
                new Metric("Measurement sample count", MetricKind.Count, input.MeasurementSamples.Count, "count")
            ],
            [
                new Overlay("overlay.c3d-flatness-reference-plane", OverlayKind.Plane, "Reference ROI fitted plane", ResultStatus.Pass, input.SourceEntityId),
                new Overlay("overlay.c3d-flatness-deviation-map", OverlayKind.ColorMap, "Signed deviation to reference plane", status, input.SourceEntityId),
                new Overlay("overlay.c3d-flatness-extrema", OverlayKind.Marker, "Minimum and maximum signed deviation", status, input.SourceEntityId)
            ]);

        return new PlaneFlatnessEvaluation(
            result,
            referencePlane,
            input.ReferenceSamples.Count,
            input.MeasurementSamples.Count,
            minimumDistance,
            maximumDistance,
            flatness,
            rootMeanSquareDistance,
            minimumPoint,
            maximumPoint,
            minimumProjection,
            maximumProjection);
    }

    private static PlaneFlatnessEvaluation Error(PlaneFlatnessRuleInput input, string message, TimeSpan elapsed)
    {
        var tolerance = double.IsFinite(input.Tolerance) ? input.Tolerance : double.NaN;
        var referenceCount = input.ReferenceSamples?.Count ?? 0;
        var measurementCount = input.MeasurementSamples?.Count ?? 0;
        return new PlaneFlatnessEvaluation(
            new ToolResult(
                ToolName,
                ResultStatus.Error,
                message,
                elapsed,
                [
                    new Metric("Flatness", MetricKind.Deviation, double.NaN, input.Unit ?? string.Empty, ResultStatus.Error),
                    new Metric("Flatness tolerance", MetricKind.Deviation, tolerance, input.Unit ?? string.Empty, ResultStatus.Error),
                    new Metric("Reference sample count", MetricKind.Count, referenceCount, "count", ResultStatus.Error),
                    new Metric("Measurement sample count", MetricKind.Count, measurementCount, "count", ResultStatus.Error)
                ],
                []),
            null,
            referenceCount,
            measurementCount,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.Zero);
    }

    private static bool IsFinite(HeightFieldPlaneSample sample) =>
        float.IsFinite(sample.Position.X)
        && float.IsFinite(sample.Position.Y)
        && float.IsFinite(sample.Position.Z)
        && double.IsFinite(sample.RawHeight);

    private static double SignedDistance(Vector3 point, Vector3 normal, double offset) =>
        normal.X * point.X + normal.Y * point.Y + normal.Z * point.Z + offset;
}

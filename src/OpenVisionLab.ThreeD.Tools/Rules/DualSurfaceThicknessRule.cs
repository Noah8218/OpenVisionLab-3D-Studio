using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record DualSurfaceThicknessInput(
    string SourceEntityId,
    IReadOnlyList<HeightFieldPlaneSample> ReferenceSamples,
    IReadOnlyList<HeightFieldPlaneSample> MeasurementSamples,
    double MinimumThickness,
    double MaximumThickness,
    int MinimumValidSamples,
    string Unit);

public sealed record DualSurfaceThicknessEvaluation(
    ToolResult Result,
    HeightFieldPlaneFitResult? ReferencePlane,
    double Mean,
    double Minimum,
    double Maximum,
    double Range,
    double RootMeanSquareSpread,
    int ReferenceSampleCount,
    int MeasurementSampleCount,
    int BelowLowerLimitCount,
    int AboveUpperLimitCount);

/// <summary>
/// Measures signed height-axis separation from a least-squares reference surface.
/// It deliberately does not reinterpret a height-field footprint as a 3D volume.
/// </summary>
public static class DualSurfaceThicknessRule
{
    public const string ToolName = "C3D Dual-surface Thickness";

    public static DualSurfaceThicknessEvaluation Evaluate(DualSurfaceThicknessInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(input.SourceEntityId) || string.IsNullOrWhiteSpace(input.Unit))
            return Error(input, "Source entity ID and declared height unit are required.", stopwatch.Elapsed);
        if (!double.IsFinite(input.MinimumThickness)
            || !double.IsFinite(input.MaximumThickness)
            || input.MinimumThickness > input.MaximumThickness)
            return Error(input, "Thickness limits must be finite and ordered minimum to maximum.", stopwatch.Elapsed);
        if (input.MinimumValidSamples < 1)
            return Error(input, "Minimum valid measurement samples must be at least one.", stopwatch.Elapsed);
        if (input.ReferenceSamples is null || input.ReferenceSamples.Count < 3)
            return Error(input, "Reference ROI requires at least three finite height samples.", stopwatch.Elapsed);
        if (input.MeasurementSamples is null || input.MeasurementSamples.Count < input.MinimumValidSamples)
            return Error(input, $"Measurement ROI requires at least {input.MinimumValidSamples} finite height sample(s).", stopwatch.Elapsed);

        HeightFieldPlaneFitResult plane;
        try
        {
            plane = HeightFieldPlaneFit.Fit(input.ReferenceSamples);
        }
        catch (ArgumentException exception)
        {
            return Error(input, $"Reference surface fit failed: {exception.Message}", stopwatch.Elapsed);
        }

        var values = input.MeasurementSamples
            .Select(sample => sample.RawHeight - plane.EvaluateY(sample.Position.X, sample.Position.Z))
            .Where(double.IsFinite)
            .ToArray();
        if (values.Length < input.MinimumValidSamples)
            return Error(input, $"Measurement ROI contains {values.Length} usable height-axis sample(s); {input.MinimumValidSamples} required.", stopwatch.Elapsed);

        var mean = values.Average();
        var minimum = values.Min();
        var maximum = values.Max();
        var range = maximum - minimum;
        var rmsSpread = Math.Sqrt(values.Average(value => (value - mean) * (value - mean)));
        var referenceFitHeightRms = Math.Sqrt(input.ReferenceSamples.Average(sample =>
        {
            var residual = sample.RawHeight - plane.EvaluateY(sample.Position.X, sample.Position.Z);
            return residual * residual;
        }));
        var below = values.Count(value => value < input.MinimumThickness);
        var above = values.Count(value => value > input.MaximumThickness);
        var status = below == 0 && above == 0 ? ResultStatus.Pass : ResultStatus.Fail;
        stopwatch.Stop();

        var result = new ToolResult(
            ToolName,
            status,
            status == ResultStatus.Pass
                ? "All measured H-axis separations from the fitted reference surface are within limits."
                : "One or more measured H-axis separations from the fitted reference surface exceed the limits.",
            stopwatch.Elapsed,
            [
                new Metric("Mean", MetricKind.Deviation, mean, input.Unit, status),
                new Metric("Minimum", MetricKind.Deviation, minimum, input.Unit, below == 0 ? ResultStatus.Pass : ResultStatus.Fail),
                new Metric("Maximum", MetricKind.Deviation, maximum, input.Unit, above == 0 ? ResultStatus.Pass : ResultStatus.Fail),
                new Metric("Range", MetricKind.Deviation, range, input.Unit),
                new Metric("RMS spread", MetricKind.Deviation, rmsSpread, input.Unit),
                new Metric("Reference fit H RMS", MetricKind.Deviation, referenceFitHeightRms, input.Unit),
                new Metric("Reference sample count", MetricKind.Count, input.ReferenceSamples.Count, "count"),
                new Metric("ValidSampleCount", MetricKind.Count, values.Length, "count"),
                new Metric("LowerLimit", MetricKind.Deviation, input.MinimumThickness, input.Unit),
                new Metric("UpperLimit", MetricKind.Deviation, input.MaximumThickness, input.Unit),
                new Metric("BelowLowerLimitCount", MetricKind.Count, below, "count"),
                new Metric("AboveUpperLimitCount", MetricKind.Count, above, "count")
            ],
            [
                new Overlay("overlay.c3d-thickness-reference-roi", OverlayKind.Box, "Thickness reference surface ROI", status, input.SourceEntityId),
                new Overlay("overlay.c3d-thickness-reference-plane", OverlayKind.Plane, "Least-squares thickness reference surface", status, input.SourceEntityId),
                new Overlay("overlay.c3d-thickness-measurement-roi", OverlayKind.Box, "Thickness measurement surface ROI", status, input.SourceEntityId),
                new Overlay("overlay.c3d-thickness-height-axis", OverlayKind.Marker, "Signed H-axis separation from reference surface", status, input.SourceEntityId)
            ]);

        return new DualSurfaceThicknessEvaluation(
            result,
            plane,
            mean,
            minimum,
            maximum,
            range,
            rmsSpread,
            input.ReferenceSamples.Count,
            values.Length,
            below,
            above);
    }

    private static DualSurfaceThicknessEvaluation Error(
        DualSurfaceThicknessInput input,
        string message,
        TimeSpan elapsed) =>
        new(
            new ToolResult(
                ToolName,
                ResultStatus.Error,
                message,
                elapsed,
                [new Metric("Mean", MetricKind.Deviation, double.NaN, input.Unit ?? string.Empty, ResultStatus.Error)],
                []),
            null,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            input.ReferenceSamples?.Count ?? 0,
            input.MeasurementSamples?.Count ?? 0,
            0,
            0);
}

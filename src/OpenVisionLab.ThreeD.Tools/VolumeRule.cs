using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record VolumeRuleInput(
    string SourceEntityId,
    IReadOnlyList<HeightFieldPlaneSample> ReferenceSamples,
    IReadOnlyList<HeightFieldPlaneSample> MeasurementSamples,
    double SampleArea,
    double ExpectedNetVolume,
    double Tolerance,
    string Unit);

public sealed record VolumeEvaluation(
    ToolResult Result,
    HeightFieldPlaneFitResult? ReferencePlane,
    double AboveVolume,
    double BelowVolume,
    double NetVolume,
    int ReferenceSampleCount,
    int MeasurementSampleCount);

public static class VolumeRule
{
    public const string ToolName = "C3D Volume";

    public static VolumeEvaluation Evaluate(VolumeRuleInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(input.SourceEntityId) || string.IsNullOrWhiteSpace(input.Unit))
            return Error(input, "Source entity ID and volume unit are required.", stopwatch.Elapsed);
        if (!double.IsFinite(input.SampleArea) || input.SampleArea <= 0.0
            || !double.IsFinite(input.ExpectedNetVolume)
            || !double.IsFinite(input.Tolerance) || input.Tolerance < 0.0)
            return Error(input, "Sample area and acceptance values must be finite; area positive and tolerance non-negative.", stopwatch.Elapsed);
        if (input.ReferenceSamples is null || input.ReferenceSamples.Count < 3)
            return Error(input, "Reference ROI requires at least three samples.", stopwatch.Elapsed);
        if (input.MeasurementSamples is null || input.MeasurementSamples.Count == 0)
            return Error(input, "Measurement ROI requires at least one sample.", stopwatch.Elapsed);

        HeightFieldPlaneFitResult plane;
        try { plane = HeightFieldPlaneFit.Fit(input.ReferenceSamples); }
        catch (ArgumentException ex) { return Error(input, ex.Message, stopwatch.Elapsed); }

        var above = 0.0;
        var below = 0.0;
        foreach (var sample in input.MeasurementSamples)
        {
            if (!float.IsFinite(sample.Position.X) || !float.IsFinite(sample.Position.Y) || !float.IsFinite(sample.Position.Z))
                return Error(input, "Measurement ROI contains a non-finite sample.", stopwatch.Elapsed);
            var delta = sample.Position.Y - plane.EvaluateY(sample.Position.X, sample.Position.Z);
            if (delta >= 0.0) above += delta * input.SampleArea;
            else below += -delta * input.SampleArea;
        }

        var net = above - below;
        var status = Math.Abs(net - input.ExpectedNetVolume) <= input.Tolerance ? ResultStatus.Pass : ResultStatus.Fail;
        stopwatch.Stop();
        var result = new ToolResult(
            ToolName,
            status,
            status == ResultStatus.Pass ? "Signed net volume is within tolerance. Source geometry is unchanged." : "Signed net volume exceeds tolerance. Source geometry is unchanged.",
            stopwatch.Elapsed,
            [
                new Metric("Above-plane volume", MetricKind.Volume, above, input.Unit),
                new Metric("Below-plane volume", MetricKind.Volume, below, input.Unit),
                new Metric("Signed net volume", MetricKind.Volume, net, input.Unit, status),
                new Metric("Expected net volume", MetricKind.Volume, input.ExpectedNetVolume, input.Unit),
                new Metric("Volume tolerance", MetricKind.Volume, input.Tolerance, input.Unit),
                new Metric("Reference samples", MetricKind.Count, input.ReferenceSamples.Count, "count"),
                new Metric("Measurement samples", MetricKind.Count, input.MeasurementSamples.Count, "count")
            ],
            [
                new Overlay("overlay.c3d-volume-reference", OverlayKind.Plane, "Volume reference plane", status, input.SourceEntityId),
                new Overlay("overlay.c3d-volume-region", OverlayKind.Box, "Volume measurement ROI", status, input.SourceEntityId),
                new Overlay("overlay.c3d-volume-deviation", OverlayKind.ColorMap, "Above/below reference volume", status, input.SourceEntityId)
            ]);
        return new VolumeEvaluation(result, plane, above, below, net, input.ReferenceSamples.Count, input.MeasurementSamples.Count);
    }

    private static VolumeEvaluation Error(VolumeRuleInput input, string message, TimeSpan elapsed) =>
        new(new ToolResult(ToolName, ResultStatus.Error, message, elapsed,
            [new Metric("Signed net volume", MetricKind.Volume, double.NaN, input.Unit ?? string.Empty, ResultStatus.Error)], []),
            null, double.NaN, double.NaN, double.NaN, input.ReferenceSamples?.Count ?? 0, input.MeasurementSamples?.Count ?? 0);
}

using System.Threading;
using System.Diagnostics;
using OpenVisionLab.Vision3D.Inspection;
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
        => Evaluate(input, CancellationToken.None);

    public static VolumeEvaluation Evaluate(VolumeRuleInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(input.SourceEntityId) || string.IsNullOrWhiteSpace(input.Unit))
            return Complete(Error(input, "Source entity ID and volume unit are required.", stopwatch.Elapsed), cancellationToken);
        if (!double.IsFinite(input.SampleArea) || input.SampleArea <= 0.0
            || !double.IsFinite(input.ExpectedNetVolume)
            || !double.IsFinite(input.Tolerance) || input.Tolerance < 0.0)
            return Complete(Error(input, "Sample area and acceptance values must be finite; area positive and tolerance non-negative.", stopwatch.Elapsed), cancellationToken);
        if (input.ReferenceSamples is null || input.ReferenceSamples.Count < 3)
            return Complete(Error(input, "Reference ROI requires at least three samples.", stopwatch.Elapsed), cancellationToken);
        if (input.MeasurementSamples is null || input.MeasurementSamples.Count == 0)
            return Complete(Error(input, "Measurement ROI requires at least one sample.", stopwatch.Elapsed), cancellationToken);
        if (input.MeasurementSamples.Any(sample =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return !float.IsFinite(sample.Position.X)
                || !float.IsFinite(sample.Position.Y)
                || !float.IsFinite(sample.Position.Z);
        }))
        {
            return Complete(Error(input, "Measurement ROI contains a non-finite sample.", stopwatch.Elapsed), cancellationToken);
        }

        VolumeInspectionResult evaluation;
        try
        {
            evaluation = new VolumeInspectionTool().Execute(
                ToSdkSamples(input.ReferenceSamples, cancellationToken),
                ToSdkSamples(input.MeasurementSamples, cancellationToken),
                new VolumeInspectionOptions
                {
                    SampleArea = input.SampleArea,
                    ExpectedNetVolume = input.ExpectedNetVolume,
                    Tolerance = input.Tolerance
                }, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return Complete(Error(input, ex.Message, stopwatch.Elapsed), cancellationToken);
        }

        var plane = HeightFieldPlaneFit.FromSdkResult(evaluation.ReferencePlane);
        var above = evaluation.AboveVolume;
        var below = evaluation.BelowVolume;
        var net = evaluation.NetVolume;
        var status = evaluation.Passed ? ResultStatus.Pass : ResultStatus.Fail;
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
        return Complete(new VolumeEvaluation(result, plane, above, below, net, input.ReferenceSamples.Count, input.MeasurementSamples.Count), cancellationToken);
    }

    private static VolumeEvaluation Complete(VolumeEvaluation result, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static OpenVisionLab.Vision3D.FeatureExtraction.HeightFieldPlaneFitSample[]? ToSdkSamples(IReadOnlyList<HeightFieldPlaneSample>? samples, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (samples is null) return null;
        var result = samples.Select(sample =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return HeightFieldPlaneFit.ToSdkSample(sample);
        }).ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static VolumeEvaluation Error(VolumeRuleInput input, string message, TimeSpan elapsed) =>
        new(new ToolResult(ToolName, ResultStatus.Error, message, elapsed,
            [new Metric("Signed net volume", MetricKind.Volume, double.NaN, input.Unit ?? string.Empty, ResultStatus.Error)], []),
            null, double.NaN, double.NaN, double.NaN, input.ReferenceSamples?.Count ?? 0, input.MeasurementSamples?.Count ?? 0);
}

using System.Diagnostics;
using SdkDecision = OpenVisionLab.Vision3D.FeatureExtraction.DualSurfaceThicknessDecision;
using SdkInspectionTool = OpenVisionLab.Vision3D.FeatureExtraction.DualSurfaceThicknessInspectionTool;
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
        var evaluation = new SdkInspectionTool().Execute(
            input.ReferenceSamples?.Select(HeightFieldPlaneFit.ToSdkSample).ToArray(),
            input.MeasurementSamples?.Select(HeightFieldPlaneFit.ToSdkSample).ToArray(),
            input.MinimumThickness,
            input.MaximumThickness,
            input.MinimumValidSamples);
        if (!evaluation.Success || evaluation.ReferencePlane is null)
            return Error(input, evaluation.Message, stopwatch.Elapsed);

        var plane = HeightFieldPlaneFit.FromSdkResult(evaluation.ReferencePlane);
        var status = evaluation.Decision == SdkDecision.Pass ? ResultStatus.Pass : ResultStatus.Fail;
        stopwatch.Stop();

        var result = new ToolResult(
            ToolName,
            status,
            evaluation.Message,
            stopwatch.Elapsed,
            [
                new Metric("Mean", MetricKind.Deviation, evaluation.Mean, input.Unit, status),
                new Metric("Minimum", MetricKind.Deviation, evaluation.Minimum, input.Unit, evaluation.BelowLowerLimitCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
                new Metric("Maximum", MetricKind.Deviation, evaluation.Maximum, input.Unit, evaluation.AboveUpperLimitCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
                new Metric("Range", MetricKind.Deviation, evaluation.Range, input.Unit),
                new Metric("RMS spread", MetricKind.Deviation, evaluation.RootMeanSquareSpread, input.Unit),
                new Metric("Reference fit H RMS", MetricKind.Deviation, evaluation.ReferenceFitHeightRootMeanSquare, input.Unit),
                new Metric("Reference sample count", MetricKind.Count, evaluation.ReferenceSampleCount, "count"),
                new Metric("ValidSampleCount", MetricKind.Count, evaluation.MeasurementSampleCount, "count"),
                new Metric("LowerLimit", MetricKind.Deviation, input.MinimumThickness, input.Unit),
                new Metric("UpperLimit", MetricKind.Deviation, input.MaximumThickness, input.Unit),
                new Metric("BelowLowerLimitCount", MetricKind.Count, evaluation.BelowLowerLimitCount, "count"),
                new Metric("AboveUpperLimitCount", MetricKind.Count, evaluation.AboveUpperLimitCount, "count")
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
            evaluation.Mean,
            evaluation.Minimum,
            evaluation.Maximum,
            evaluation.Range,
            evaluation.RootMeanSquareSpread,
            evaluation.ReferenceSampleCount,
            evaluation.MeasurementSampleCount,
            evaluation.BelowLowerLimitCount,
            evaluation.AboveUpperLimitCount);
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

using System.Threading;
using System.Diagnostics;
using SdkDecision = OpenVisionLab.Vision3D.FeatureExtraction.DualSurfaceThicknessDecision;
using SdkInspectionTool = OpenVisionLab.Vision3D.FeatureExtraction.DualSurfaceThicknessInspectionTool;
using SdkFitSample = OpenVisionLab.Vision3D.FeatureExtraction.HeightFieldPlaneFitSample;
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
/// For each measurement sample the scalar is
/// <c>sample.RawHeight - referencePlane.EvaluateY(sample.Position.X, sample.Position.Z)</c>.
/// It is not perpendicular normal distance, and it deliberately does not
/// reinterpret a height-field footprint as a 3D volume.
/// </summary>
public static class DualSurfaceThicknessRule
{
    public const string ToolName = "C3D Dual-surface Thickness";

    public static DualSurfaceThicknessEvaluation Evaluate(DualSurfaceThicknessInput input)
        => Evaluate(input, CancellationToken.None);

    public static DualSurfaceThicknessEvaluation Evaluate(DualSurfaceThicknessInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);
        return EvaluateReferenceAxes(
            input.SourceEntityId,
            ToSdkSamples(input.ReferenceSamples, cancellationToken),
            ToSdkSamples(input.MeasurementSamples, cancellationToken),
            input.MinimumThickness,
            input.MaximumThickness,
            input.MinimumValidSamples,
            input.Unit, cancellationToken);
    }

    // Recipe height fields already carry double reference-axis coordinates. Keep them
    // intact while retaining the public Vector3 adapter for existing callers.
    internal static DualSurfaceThicknessEvaluation EvaluateReferenceAxes(
        string sourceEntityId,
        IReadOnlyList<SdkFitSample>? referenceSamples,
        IReadOnlyList<SdkFitSample>? measurementSamples,
        double minimumThickness,
        double maximumThickness,
        int minimumValidSamples,
        string unit)
        => EvaluateReferenceAxes(sourceEntityId, referenceSamples, measurementSamples, minimumThickness, maximumThickness, minimumValidSamples, unit, CancellationToken.None);

    internal static DualSurfaceThicknessEvaluation EvaluateReferenceAxes(
        string sourceEntityId,
        IReadOnlyList<SdkFitSample>? referenceSamples,
        IReadOnlyList<SdkFitSample>? measurementSamples,
        double minimumThickness,
        double maximumThickness,
        int minimumValidSamples,
        string unit, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(sourceEntityId) || string.IsNullOrWhiteSpace(unit))
            return Complete(Error(unit, referenceSamples?.Count ?? 0, measurementSamples?.Count ?? 0,
                "Source entity ID and declared height unit are required.", stopwatch.Elapsed), cancellationToken);
        var evaluation = new SdkInspectionTool().Execute(
            referenceSamples,
            measurementSamples,
            minimumThickness,
            maximumThickness,
            minimumValidSamples, cancellationToken);
        if (!evaluation.Success || evaluation.ReferencePlane is null)
            return Complete(Error(unit, referenceSamples?.Count ?? 0, measurementSamples?.Count ?? 0,
                evaluation.Message, stopwatch.Elapsed), cancellationToken);

        var plane = HeightFieldPlaneFit.FromSdkResult(evaluation.ReferencePlane);
        var status = evaluation.Decision == SdkDecision.Pass ? ResultStatus.Pass : ResultStatus.Fail;
        stopwatch.Stop();

        var result = new ToolResult(
            ToolName,
            status,
            evaluation.Message,
            stopwatch.Elapsed,
            [
                new Metric("Mean", MetricKind.Deviation, evaluation.Mean, unit, status),
                new Metric("Minimum", MetricKind.Deviation, evaluation.Minimum, unit, evaluation.BelowLowerLimitCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
                new Metric("Maximum", MetricKind.Deviation, evaluation.Maximum, unit, evaluation.AboveUpperLimitCount == 0 ? ResultStatus.Pass : ResultStatus.Fail),
                new Metric("Range", MetricKind.Deviation, evaluation.Range, unit),
                new Metric("RMS spread", MetricKind.Deviation, evaluation.RootMeanSquareSpread, unit),
                new Metric("Reference fit H RMS", MetricKind.Deviation, evaluation.ReferenceFitHeightRootMeanSquare, unit),
                new Metric("Reference sample count", MetricKind.Count, evaluation.ReferenceSampleCount, "count"),
                new Metric("ValidSampleCount", MetricKind.Count, evaluation.MeasurementSampleCount, "count"),
                new Metric("LowerLimit", MetricKind.Deviation, minimumThickness, unit),
                new Metric("UpperLimit", MetricKind.Deviation, maximumThickness, unit),
                new Metric("BelowLowerLimitCount", MetricKind.Count, evaluation.BelowLowerLimitCount, "count"),
                new Metric("AboveUpperLimitCount", MetricKind.Count, evaluation.AboveUpperLimitCount, "count")
            ],
            [
                new Overlay("overlay.c3d-thickness-reference-roi", OverlayKind.Box, "Thickness reference surface ROI", status, sourceEntityId),
                new Overlay("overlay.c3d-thickness-reference-plane", OverlayKind.Plane, "Least-squares thickness reference surface", status, sourceEntityId),
                new Overlay("overlay.c3d-thickness-measurement-roi", OverlayKind.Box, "Thickness measurement surface ROI", status, sourceEntityId),
                new Overlay("overlay.c3d-thickness-height-axis", OverlayKind.Marker, "Signed H-axis separation from reference surface", status, sourceEntityId)
            ]);

        return Complete(new DualSurfaceThicknessEvaluation(
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
            evaluation.AboveUpperLimitCount), cancellationToken);
    }

    private static DualSurfaceThicknessEvaluation Complete(DualSurfaceThicknessEvaluation result, CancellationToken cancellationToken)
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

    private static DualSurfaceThicknessEvaluation Error(
        string? unit,
        int referenceSampleCount,
        int measurementSampleCount,
        string message,
        TimeSpan elapsed) =>
        new(
            new ToolResult(
                ToolName,
                ResultStatus.Error,
                message,
                elapsed,
                [new Metric("Mean", MetricKind.Deviation, double.NaN, unit ?? string.Empty, ResultStatus.Error)],
                []),
            null,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            referenceSampleCount,
            measurementSampleCount,
            0,
            0);
}

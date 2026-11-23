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
    string Unit,
    // 기준 영역의 평면 맞춤 RMS 상한. null이면 검사하지 않는다(기존 동작). 한계 비교는 Studio의 판정 정책이고 RMS 계산은 SDK가 한다.
    double? MaximumReferenceFitRms = null);

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

    /// <summary>기준면 RMS가 상한을 넘어 계산을 거부했을 때 결과 메시지가 시작하는 문구. 화면 한글화가 이 문구로 구분한다.</summary>
    public const string ReferenceFitExceededMessagePrefix = "Reference surface fit RMS exceeds the allowed maximum:";
    public const string ReferenceFitExceededMessageSuffix = ". The reference ROI is not flat enough.";

    public const string ReferenceFitRmsMetricName = "Reference fit H RMS";
    public const string MaximumReferenceFitRmsMetricName = "MaximumReferenceFitRms";

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
            input.Unit, cancellationToken, input.MaximumReferenceFitRms);
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
        string unit, CancellationToken cancellationToken, double? maximumReferenceFitRms = null)
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

        // 기준 영역이 평면이 아니면 두께는 계산되지만 틀린 값이다(예: 기준 띠가 다른 면에 걸친 경우).
        // 한계를 정한 레시피에서는 그 값을 OK/NG로 내지 않고 오류와 원인을 낸다. NaN도 한계를 만족하지 못한 것으로 본다.
        if (maximumReferenceFitRms is { } referenceFitLimit
            && !(evaluation.ReferenceFitHeightRootMeanSquare <= referenceFitLimit))
        {
            stopwatch.Stop();
            return Complete(ReferenceFitExceeded(
                unit,
                evaluation.ReferenceFitHeightRootMeanSquare,
                referenceFitLimit,
                evaluation.ReferenceSampleCount,
                evaluation.MeasurementSampleCount,
                stopwatch.Elapsed), cancellationToken);
        }

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
                new Metric(ReferenceFitRmsMetricName, MetricKind.Deviation, evaluation.ReferenceFitHeightRootMeanSquare, unit),
                .. maximumReferenceFitRms is { } configuredLimit
                    ? new[] { new Metric(MaximumReferenceFitRmsMetricName, MetricKind.Deviation, configuredLimit, unit) }
                    : Array.Empty<Metric>(),
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

    // 기준면 RMS가 상한을 넘은 경우의 오류 결과. 두께 값 대신 원인을 보이도록 평균은 비워 두고(NaN),
    // 측정한 RMS와 상한을 지표로 남겨 화면과 보고서가 같은 근거를 쓴다. 판정(OK/NG)이 아니라 계산 실패다.
    private static DualSurfaceThicknessEvaluation ReferenceFitExceeded(
        string unit,
        double referenceFitRms,
        double limit,
        int referenceSampleCount,
        int measurementSampleCount,
        TimeSpan elapsed) =>
        new(
            new ToolResult(
                ToolName,
                ResultStatus.Error,
                FormattableString.Invariant($"{ReferenceFitExceededMessagePrefix} {referenceFitRms:G6} > {limit:G6} {unit}{ReferenceFitExceededMessageSuffix}"),
                elapsed,
                [
                    new Metric("Mean", MetricKind.Deviation, double.NaN, unit, ResultStatus.Error),
                    new Metric(ReferenceFitRmsMetricName, MetricKind.Deviation, referenceFitRms, unit, ResultStatus.Error),
                    new Metric(MaximumReferenceFitRmsMetricName, MetricKind.Deviation, limit, unit),
                    new Metric("Reference sample count", MetricKind.Count, referenceSampleCount, "count"),
                    new Metric("ValidSampleCount", MetricKind.Count, measurementSampleCount, "count")
                ],
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

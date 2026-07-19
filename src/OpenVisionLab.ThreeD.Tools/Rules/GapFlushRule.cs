using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record GapFlushRegionStats(int PointCount, double RawMean, double ModelYMean);

public sealed record GapFlushInput(
    string SourceEntityId,
    HeightDeviationRecipeRoiRegion LeftRegion,
    HeightDeviationRecipeRoiRegion RightRegion,
    GapFlushRegionStats Left,
    GapFlushRegionStats Right,
    C3DGapFlushAcceptance Acceptance,
    string GapUnit,
    string FlushUnit);

public sealed record GapFlushEvaluation(
    ToolResult Result,
    double SignedGap,
    double SignedFlush,
    double ModelFlush,
    int LeftPointCount,
    int RightPointCount);

public static class GapFlushRule
{
    public const string ToolName = "C3D Gap / Flush";

    public static GapFlushEvaluation Evaluate(GapFlushInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(input.SourceEntityId)
            || string.IsNullOrWhiteSpace(input.GapUnit)
            || string.IsNullOrWhiteSpace(input.FlushUnit))
        {
            return Error(input, "Source entity ID and units are required.", stopwatch.Elapsed);
        }

        if (!ValidRegion(input.LeftRegion)
            || !ValidRegion(input.RightRegion)
            || input.Left is null
            || input.Right is null)
        {
            return Error(input, "Two finite ROI regions are required.", stopwatch.Elapsed);
        }

        if (input.Left.PointCount <= 0 || input.Right.PointCount <= 0)
        {
            return Error(input, "Both ROI regions require at least one point.", stopwatch.Elapsed);
        }

        if (!double.IsFinite(input.Left.RawMean)
            || !double.IsFinite(input.Right.RawMean)
            || !double.IsFinite(input.Left.ModelYMean)
            || !double.IsFinite(input.Right.ModelYMean)
            || !ValidAcceptance(input.Acceptance))
        {
            return Error(input, "ROI statistics, expected values, and tolerances must be finite; tolerances must be non-negative.", stopwatch.Elapsed);
        }

        var signedGap = (input.RightRegion.CenterX - input.RightRegion.HalfWidth)
            - (input.LeftRegion.CenterX + input.LeftRegion.HalfWidth);
        var signedFlush = input.Right.RawMean - input.Left.RawMean;
        var modelFlush = input.Right.ModelYMean - input.Left.ModelYMean;
        var gapStatus = Status(signedGap, input.Acceptance.ExpectedGap, input.Acceptance.GapTolerance);
        var flushStatus = Status(signedFlush, input.Acceptance.ExpectedFlush, input.Acceptance.FlushTolerance);
        var status = gapStatus == ResultStatus.Pass && flushStatus == ResultStatus.Pass
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        stopwatch.Stop();

        var result = new ToolResult(
            ToolName,
            status,
            status == ResultStatus.Pass
                ? "Signed gap and flush are within configured tolerances. Source geometry is unchanged."
                : "Signed gap or flush exceeds configured tolerance. Source geometry is unchanged.",
            stopwatch.Elapsed,
            [
                new Metric("Signed gap", MetricKind.Length, signedGap, input.GapUnit, gapStatus),
                new Metric("Signed flush", MetricKind.Deviation, signedFlush, input.FlushUnit, flushStatus),
                new Metric("Model Y flush", MetricKind.Deviation, modelFlush, input.GapUnit),
                new Metric("Left ROI points", MetricKind.Count, input.Left.PointCount, "count"),
                new Metric("Right ROI points", MetricKind.Count, input.Right.PointCount, "count"),
                new Metric("Expected gap", MetricKind.Length, input.Acceptance.ExpectedGap, input.GapUnit),
                new Metric("Gap tolerance", MetricKind.Length, input.Acceptance.GapTolerance, input.GapUnit),
                new Metric("Expected flush", MetricKind.Deviation, input.Acceptance.ExpectedFlush, input.FlushUnit),
                new Metric("Flush tolerance", MetricKind.Deviation, input.Acceptance.FlushTolerance, input.FlushUnit)
            ],
            [
                new Overlay("overlay.c3d-gap-flush-regions", OverlayKind.Box, "Gap / Flush left and right ROI regions", status, input.SourceEntityId),
                new Overlay("overlay.c3d-gap-line", OverlayKind.Polyline, "Signed gap between facing ROI edges", gapStatus, input.SourceEntityId),
                new Overlay("overlay.c3d-flush-marker", OverlayKind.Marker, "Signed flush between ROI mean heights", flushStatus, input.SourceEntityId)
            ]);

        return new GapFlushEvaluation(result, signedGap, signedFlush, modelFlush, input.Left.PointCount, input.Right.PointCount);
    }

    private static GapFlushEvaluation Error(GapFlushInput input, string message, TimeSpan elapsed) =>
        new(
            new ToolResult(
                ToolName,
                ResultStatus.Error,
                message,
                elapsed,
                [new Metric("Signed gap", MetricKind.Length, double.NaN, input.GapUnit ?? string.Empty, ResultStatus.Error)],
                []),
            double.NaN,
            double.NaN,
            double.NaN,
            input.Left?.PointCount ?? 0,
            input.Right?.PointCount ?? 0);

    private static ResultStatus Status(double actual, double expected, double tolerance) =>
        Math.Abs(actual - expected) <= tolerance ? ResultStatus.Pass : ResultStatus.Fail;

    private static bool ValidRegion(HeightDeviationRecipeRoiRegion? region) =>
        region is not null
        && double.IsFinite(region.CenterX)
        && double.IsFinite(region.CenterZ)
        && double.IsFinite(region.HalfWidth)
        && double.IsFinite(region.HalfDepth)
        && region.HalfWidth > 0.0
        && region.HalfDepth > 0.0;

    private static bool ValidAcceptance(C3DGapFlushAcceptance? acceptance) =>
        acceptance is not null
        && double.IsFinite(acceptance.ExpectedGap)
        && double.IsFinite(acceptance.GapTolerance)
        && acceptance.GapTolerance >= 0.0
        && double.IsFinite(acceptance.ExpectedFlush)
        && double.IsFinite(acceptance.FlushTolerance)
        && acceptance.FlushTolerance >= 0.0;
}

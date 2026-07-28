using System.Diagnostics;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record HeightDeviationRuleInput(
    string SourceEntityId,
    string SourceName,
    double Min,
    double Max,
    double Mean,
    int ValidSampleCount,
    double PeakTolerance,
    string Unit);

public static class HeightDeviationRule
{
    public static ToolResult Evaluate(HeightDeviationRuleInput input)
    {
        var stopwatch = Stopwatch.StartNew();
        if (input.ValidSampleCount <= 0
            || !double.IsFinite(input.Min)
            || !double.IsFinite(input.Max)
            || !double.IsFinite(input.Mean)
            || !double.IsFinite(input.PeakTolerance)
            || input.PeakTolerance <= 0.0)
        {
            stopwatch.Stop();
            return new ToolResult(
                "C3D Height Deviation Rule",
                ResultStatus.Error,
                "Invalid height-grid statistics or tolerance.",
                stopwatch.Elapsed,
                [],
                []);
        }

        var lowDeviation = Math.Abs(input.Mean - input.Min);
        var highDeviation = Math.Abs(input.Max - input.Mean);
        var peakDeviation = Math.Max(lowDeviation, highDeviation);
        var status = peakDeviation <= input.PeakTolerance ? ResultStatus.Pass : ResultStatus.Fail;
        var message = string.Create(
            CultureInfo.InvariantCulture,
            $"{input.SourceName}: peak deviation {peakDeviation:F3} {input.Unit}, tolerance {input.PeakTolerance:F3} {input.Unit}.");

        stopwatch.Stop();
        return new ToolResult(
            "C3D Height Deviation Rule",
            status,
            message,
            stopwatch.Elapsed,
            [
                new Metric("Mean height", MetricKind.Number, input.Mean, input.Unit),
                new Metric("Minimum height", MetricKind.Number, input.Min, input.Unit),
                new Metric("Maximum height", MetricKind.Number, input.Max, input.Unit),
                new Metric("Peak absolute deviation", MetricKind.Deviation, peakDeviation, input.Unit, status),
                new Metric("Peak tolerance", MetricKind.Deviation, input.PeakTolerance, input.Unit, ResultStatus.Pass),
                new Metric("Valid samples", MetricKind.Count, input.ValidSampleCount, "count")
            ],
            [
                new Overlay("overlay.c3d-height-tolerance-band", OverlayKind.ColorMap, "Mean height tolerance band", status, input.SourceEntityId),
                new Overlay("overlay.c3d-height-profile", OverlayKind.Polyline, "C3D height profile", status, input.SourceEntityId),
                new Overlay("overlay.c3d-height-peak-marker", OverlayKind.Marker, "Peak deviation marker", status, input.SourceEntityId)
            ]);
    }
}

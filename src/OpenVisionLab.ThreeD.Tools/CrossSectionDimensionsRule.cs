using System.Diagnostics;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record CrossSectionSample(int Column, Vector3 Position, double RawHeight);

public sealed record CrossSectionDimensionsInput(
    string SourceEntityId,
    int Row,
    int StartColumn,
    int EndColumn,
    IReadOnlyList<CrossSectionSample> Samples,
    double ExpectedWidth,
    double WidthTolerance,
    double ExpectedHeightRange,
    double HeightTolerance,
    string WidthUnit,
    string HeightUnit);

public sealed record CrossSectionEvaluation(
    ToolResult Result,
    double Width,
    double HeightRange,
    double RawMinimum,
    double RawMaximum,
    int ValidSampleCount);

public static class CrossSectionDimensionsRule
{
    public const string ToolName = "C3D Cross-section Dimensions";

    public static CrossSectionEvaluation Evaluate(CrossSectionDimensionsInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var stopwatch = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(input.SourceEntityId)
            || string.IsNullOrWhiteSpace(input.WidthUnit)
            || string.IsNullOrWhiteSpace(input.HeightUnit))
            return Error(input, "Source entity ID and units are required.", stopwatch.Elapsed);
        if (input.Row < 0 || input.StartColumn < 0 || input.EndColumn <= input.StartColumn)
            return Error(input, "Cross-section row and ordered inclusive columns are required.", stopwatch.Elapsed);
        if (!AcceptanceIsValid(input))
            return Error(input, "Cross-section acceptance values must be finite and tolerances non-negative.", stopwatch.Elapsed);
        if (input.Samples is null || input.Samples.Count < 2)
            return Error(input, "Cross-section requires at least two valid source samples.", stopwatch.Elapsed);
        if (input.Samples.Any(sample => sample.Column < input.StartColumn
            || sample.Column > input.EndColumn
            || !float.IsFinite(sample.Position.X)
            || !float.IsFinite(sample.Position.Y)
            || !float.IsFinite(sample.Position.Z)
            || !double.IsFinite(sample.RawHeight)))
            return Error(input, "Cross-section contains an out-of-range or non-finite sample.", stopwatch.Elapsed);

        var minimumX = input.Samples.Min(sample => sample.Position.X);
        var maximumX = input.Samples.Max(sample => sample.Position.X);
        var rawMinimum = input.Samples.Min(sample => sample.RawHeight);
        var rawMaximum = input.Samples.Max(sample => sample.RawHeight);
        var width = maximumX - minimumX;
        var heightRange = rawMaximum - rawMinimum;
        var widthStatus = Math.Abs(width - input.ExpectedWidth) <= input.WidthTolerance ? ResultStatus.Pass : ResultStatus.Fail;
        var heightStatus = Math.Abs(heightRange - input.ExpectedHeightRange) <= input.HeightTolerance ? ResultStatus.Pass : ResultStatus.Fail;
        var status = widthStatus == ResultStatus.Pass && heightStatus == ResultStatus.Pass ? ResultStatus.Pass : ResultStatus.Fail;
        stopwatch.Stop();

        var result = new ToolResult(
            ToolName,
            status,
            status == ResultStatus.Pass
                ? "Cross-section width and raw-height range are within tolerance. Source geometry is unchanged."
                : "One or more cross-section dimensions exceed tolerance. Source geometry is unchanged.",
            stopwatch.Elapsed,
            [
                new Metric("Section width", MetricKind.Length, width, input.WidthUnit, widthStatus),
                new Metric("Raw-height range", MetricKind.Deviation, heightRange, input.HeightUnit, heightStatus),
                new Metric("Raw minimum", MetricKind.Number, rawMinimum, input.HeightUnit),
                new Metric("Raw maximum", MetricKind.Number, rawMaximum, input.HeightUnit),
                new Metric("Valid section samples", MetricKind.Count, input.Samples.Count, "count")
            ],
            [
                new Overlay("overlay.c3d-cross-section-plane", OverlayKind.Plane, $"Source row {input.Row}", status, input.SourceEntityId),
                new Overlay("overlay.c3d-cross-section-width", OverlayKind.Polyline, "Cross-section width span", widthStatus, input.SourceEntityId),
                new Overlay("overlay.c3d-cross-section-height", OverlayKind.Marker, "Cross-section raw-height extrema", heightStatus, input.SourceEntityId)
            ]);
        return new CrossSectionEvaluation(result, width, heightRange, rawMinimum, rawMaximum, input.Samples.Count);
    }

    private static bool AcceptanceIsValid(CrossSectionDimensionsInput input) =>
        double.IsFinite(input.ExpectedWidth)
        && double.IsFinite(input.WidthTolerance)
        && input.WidthTolerance >= 0.0
        && double.IsFinite(input.ExpectedHeightRange)
        && double.IsFinite(input.HeightTolerance)
        && input.HeightTolerance >= 0.0;

    private static CrossSectionEvaluation Error(CrossSectionDimensionsInput input, string message, TimeSpan elapsed) =>
        new(new ToolResult(ToolName, ResultStatus.Error, message, elapsed,
            [new Metric("Section width", MetricKind.Length, double.NaN, input.WidthUnit ?? string.Empty, ResultStatus.Error)], []),
            double.NaN, double.NaN, double.NaN, double.NaN, input.Samples?.Count ?? 0);
}

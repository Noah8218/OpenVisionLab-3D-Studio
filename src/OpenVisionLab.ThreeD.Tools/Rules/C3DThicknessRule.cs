using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DThicknessInput(
    string SourceEntityId,
    int Rows,
    int Columns,
    IReadOnlyList<double>? Values,
    C3DGridRoi Roi,
    C3DThicknessAcceptance Acceptance,
    string Unit,
    string FrameId,
    int MinimumValidSamples = 1);

public sealed record C3DThicknessEvaluation(
    ToolResult Result,
    bool HasMeasurement,
    string PackageResultStatus,
    string PackageErrorCode,
    C3DGridRoi Roi,
    double Mean,
    double Minimum,
    double Maximum,
    double Range,
    int ValidSampleCount,
    int BelowLowerLimitCount,
    int AboveUpperLimitCount);

/// <summary>
/// Studio-level thickness contract. The scalar calculation is delegated to Library-Noah;
/// this rule adds stable Studio result names and the taught ROI overlay contract.
/// </summary>
public static class C3DThicknessRule
{
    public const string ToolName = "C3D Thickness";

    public static C3DThicknessEvaluation Evaluate(C3DThicknessInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var package = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(
                new LibraryNoahHeightMapInput(
                    input.SourceEntityId,
                    input.Rows,
                    input.Columns,
                    0.0,
                    0.0,
                    1.0,
                    1.0,
                    input.Values,
                    input.Unit,
                    input.FrameId),
                new LibraryNoahGridRoi(input.Roi.Row, input.Roi.Column, input.Roi.RowCount, input.Roi.ColumnCount),
                input.Acceptance.MinimumThickness,
                input.Acceptance.MaximumThickness,
                input.MinimumValidSamples));

        var metrics = OrderMetrics(package.Result.Metrics);
        var result = new ToolResult(
            ToolName,
            package.Result.Status,
            FormatMessage(package.Result, input.Unit),
            package.Result.Elapsed,
            metrics,
            package.HasMeasurement
                ? [new Overlay(
                    "overlay.c3d-thickness-roi",
                    OverlayKind.Box,
                    "Taught C3D thickness grid ROI",
                    package.Result.Status,
                    input.SourceEntityId)]
                : []);

        return new C3DThicknessEvaluation(
            result,
            package.HasMeasurement,
            package.PackageResultStatus,
            package.PackageErrorCode,
            input.Roi,
            MetricValue(metrics, "Mean"),
            MetricValue(metrics, "Minimum"),
            MetricValue(metrics, "Maximum"),
            MetricValue(metrics, "Range"),
            MetricCount(metrics, "ValidSampleCount"),
            MetricCount(metrics, "BelowLowerLimitCount"),
            MetricCount(metrics, "AboveUpperLimitCount"));
    }

    private static IReadOnlyList<Metric> OrderMetrics(IReadOnlyList<Metric> metrics)
    {
        var order = new[]
        {
            "Mean",
            "Minimum",
            "Maximum",
            "Range",
            "ValidSampleCount",
            "LowerLimit",
            "UpperLimit",
            "BelowLowerLimitCount",
            "AboveUpperLimitCount"
        };

        return metrics
            .OrderBy(metric => Array.IndexOf(order, metric.Name) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(metric => metric.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatMessage(ToolResult packageResult, string unit) =>
        packageResult.Status == ResultStatus.Error
            ? packageResult.Message
            : $"{packageResult.Message} Declared scalar values are {unit}; physical calibration is not inferred.";

    private static double MetricValue(IReadOnlyList<Metric> metrics, string name) =>
        metrics.FirstOrDefault(metric => metric.Name.Equals(name, StringComparison.Ordinal))?.Value ?? double.NaN;

    private static int MetricCount(IReadOnlyList<Metric> metrics, string name)
    {
        var value = MetricValue(metrics, name);
        return double.IsFinite(value) && value >= 0.0 && value <= int.MaxValue
            ? (int)Math.Round(value, MidpointRounding.AwayFromZero)
            : 0;
    }
}

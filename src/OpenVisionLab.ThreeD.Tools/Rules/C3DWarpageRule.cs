using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DWarpageInput(
    string SourceEntityId,
    int Rows,
    int Columns,
    IReadOnlyList<double>? Values,
    C3DGridRoi Roi,
    C3DWarpageAcceptance Acceptance,
    string Unit,
    string FrameId,
    int MinimumValidSamples = 3);

public sealed record C3DWarpageEvaluation(
    ToolResult Result,
    bool HasMeasurement,
    string PackageResultStatus,
    string PackageErrorCode,
    C3DGridRoi Roi,
    double PeakToValley,
    double Rms,
    double MinimumResidual,
    double MaximumResidual,
    double PlaneSlopeX,
    double PlaneSlopeY,
    double PlaneIntercept,
    int ValidSampleCount);

/// <summary>
/// Studio-level raw-height Warpage contract. The calculation is delegated to
/// Library-Noah; this rule fixes the Studio identity, metric order, and ROI overlay.
/// </summary>
public static class C3DWarpageRule
{
    public const string ToolName = "C3D Warpage";

    public static C3DWarpageEvaluation Evaluate(C3DWarpageInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var package = LibraryNoahHeightMapInspection.EvaluateWarpage(
            new LibraryNoahWarpageInspectionInput(
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
                input.Acceptance.MaximumPeakToValley,
                input.Acceptance.MaximumRms,
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
                    "overlay.c3d-warpage-roi",
                    OverlayKind.Box,
                    "C3D Warpage best-fit inspection ROI",
                    package.Result.Status,
                    input.SourceEntityId)]
                : []);

        return new C3DWarpageEvaluation(
            result,
            package.HasMeasurement,
            package.PackageResultStatus,
            package.PackageErrorCode,
            input.Roi,
            MetricValue(metrics, "PeakToValley"),
            MetricValue(metrics, "Rms"),
            MetricValue(metrics, "MinimumResidual"),
            MetricValue(metrics, "MaximumResidual"),
            MetricValue(metrics, "PlaneSlopeX"),
            MetricValue(metrics, "PlaneSlopeY"),
            MetricValue(metrics, "PlaneIntercept"),
            MetricCount(metrics, "ValidSampleCount"));
    }

    private static IReadOnlyList<Metric> OrderMetrics(IReadOnlyList<Metric> metrics)
    {
        var order = new[]
        {
            "PeakToValley",
            "Rms",
            "MinimumResidual",
            "MaximumResidual",
            "PlaneSlopeX",
            "PlaneSlopeY",
            "PlaneIntercept",
            "ValidSampleCount",
            "MaximumPeakToValley",
            "MaximumRms"
        };

        return metrics
            .OrderBy(metric => Array.IndexOf(order, metric.Name) is var index && index >= 0 ? index : int.MaxValue)
            .ThenBy(metric => metric.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatMessage(ToolResult packageResult, string unit) =>
        packageResult.Status == ResultStatus.Error
            ? packageResult.Message
            : $"{packageResult.Message} Best-fit residuals use declared {unit} scalar values; physical calibration is not inferred.";

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

using NoahHeightMap3D = Lib.ThreeD.Geometry.HeightMap3D;
using NoahHeightMapRoi = Lib.ThreeD.Geometry.HeightMapRoi;
using NoahInspectionResult = Lib.ThreeD.Inspection.ThreeDInspectionResult;
using NoahInspectionStatus = Lib.ThreeD.Inspection.ThreeDInspectionResultStatus;
using NoahThicknessInspectionOptions = Lib.ThreeD.Inspection.ThicknessInspectionOptions;
using NoahThicknessInspectionTool = Lib.ThreeD.Inspection.ThicknessInspectionTool;
using NoahWarpageInspectionOptions = Lib.ThreeD.Inspection.WarpageInspectionOptions;
using NoahWarpageInspectionTool = Lib.ThreeD.Inspection.WarpageInspectionTool;
using NoahDatumPlaneRawHeightDeviationInspectionOptions = Lib.ThreeD.Inspection.DatumPlaneRawHeightDeviationInspectionOptions;
using NoahDatumPlaneRawHeightDeviationInspectionTool = Lib.ThreeD.Inspection.DatumPlaneRawHeightDeviationInspectionTool;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record LibraryNoahHeightMapInput(
    string SourceEntityId,
    int Rows,
    int Columns,
    double OriginX,
    double OriginY,
    double ColumnPitch,
    double RowPitch,
    IReadOnlyList<double>? Values,
    string Unit,
    string FrameId);

public sealed record LibraryNoahGridRoi(int Row, int Column, int RowCount, int ColumnCount);

public sealed record LibraryNoahThicknessInspectionInput(
    LibraryNoahHeightMapInput? Source,
    LibraryNoahGridRoi? Roi,
    double MinimumThickness,
    double MaximumThickness,
    int MinimumValidSamples = 1);

public sealed record LibraryNoahWarpageInspectionInput(
    LibraryNoahHeightMapInput? Source,
    LibraryNoahGridRoi? Roi,
    double MaximumPeakToValley,
    double? MaximumRms = null,
    int MinimumValidSamples = 3);

public sealed record LibraryNoahDatumPlaneRawHeightDeviationInspectionInput(
    LibraryNoahHeightMapInput? Source,
    LibraryNoahGridRoi? Roi,
    double PlaneNormalX,
    double PlaneNormalY,
    double PlaneNormalZ,
    double PlaneOffset,
    double MaximumPeakToValleyRawHeight,
    int MinimumValidSamples = 3,
    double MinimumAbsoluteNormalY = 0.1);

public sealed record LibraryNoahInspectionEvaluation(
    ToolResult Result,
    bool HasMeasurement,
    string PackageResultStatus,
    string PackageErrorCode,
    LibraryNoahGridRoi? Roi);

/// <summary>
/// Explicit Studio-to-Library-Noah boundary for a declared scalar height map.
/// It does not infer physical units, scalar meaning, calibration, or a Viewer overlay.
/// </summary>
public static class LibraryNoahHeightMapInspection
{
    public const string PackageId = "Lib.ThreeD";
    public const string PackageVersion = "2.7.7";
    public const string PackageSourceCommit = "6aba3d5b37e9d10f2d90977e483956b6d57e2aaf";

    public static string PackageAssemblyName => typeof(NoahHeightMap3D).Assembly.GetName().Name ?? string.Empty;

    public static LibraryNoahInspectionEvaluation EvaluateThickness(LibraryNoahThicknessInspectionInput? input)
    {
        const string toolName = "Library-Noah Thickness";
        if (input is null)
        {
            return Error(toolName, null, null, "Thickness inspection input is required.");
        }

        return Execute(
            toolName,
            input.Source,
            input.Roi,
            heightMap => new NoahThicknessInspectionTool(new NoahThicknessInspectionOptions
            {
                Roi = ToNoahRoi(input.Roi),
                MinimumThickness = input.MinimumThickness,
                MaximumThickness = input.MaximumThickness,
                MinimumValidSamples = input.MinimumValidSamples
            }).Execute(heightMap));
    }

    public static LibraryNoahInspectionEvaluation EvaluateWarpage(LibraryNoahWarpageInspectionInput? input)
    {
        const string toolName = "Library-Noah Warpage";
        if (input is null)
        {
            return Error(toolName, null, null, "Warpage inspection input is required.");
        }

        return Execute(
            toolName,
            input.Source,
            input.Roi,
            heightMap => new NoahWarpageInspectionTool(new NoahWarpageInspectionOptions
            {
                Roi = ToNoahRoi(input.Roi),
                MaximumPeakToValley = input.MaximumPeakToValley,
                MaximumRms = input.MaximumRms,
                MinimumValidSamples = input.MinimumValidSamples
            }).Execute(heightMap));
    }

    public static LibraryNoahInspectionEvaluation EvaluateDatumPlaneRawHeightDeviation(
        LibraryNoahDatumPlaneRawHeightDeviationInspectionInput? input)
    {
        const string toolName = "Library-Noah Datum Plane Raw-Height Deviation";
        if (input is null)
        {
            return Error(toolName, null, null, "Datum-plane raw-height deviation input is required.");
        }

        return Execute(
            toolName,
            input.Source,
            input.Roi,
            heightMap => new NoahDatumPlaneRawHeightDeviationInspectionTool(
                new NoahDatumPlaneRawHeightDeviationInspectionOptions
                {
                    Roi = ToNoahRoi(input.Roi),
                    PlaneNormalX = input.PlaneNormalX,
                    PlaneNormalY = input.PlaneNormalY,
                    PlaneNormalZ = input.PlaneNormalZ,
                    PlaneOffset = input.PlaneOffset,
                    MaximumPeakToValleyRawHeight = input.MaximumPeakToValleyRawHeight,
                    MinimumValidSamples = input.MinimumValidSamples,
                    MinimumAbsoluteNormalY = input.MinimumAbsoluteNormalY
                }).Execute(heightMap));
    }

    public static bool TryCalculateDatumPlaneRawHeightResidual(
        double normalX,
        double normalY,
        double normalZ,
        double planeOffset,
        double gridX,
        double gridY,
        double rawHeight,
        out double residual) =>
        NoahDatumPlaneRawHeightDeviationInspectionTool.TryCalculateRawHeightResidual(
            normalX,
            normalY,
            normalZ,
            planeOffset,
            gridX,
            gridY,
            rawHeight,
            out residual);

    private static LibraryNoahInspectionEvaluation Execute(
        string toolName,
        LibraryNoahHeightMapInput? source,
        LibraryNoahGridRoi? roi,
        Func<NoahHeightMap3D, NoahInspectionResult> execute)
    {
        if (!TryCreateHeightMap(source, out var heightMap, out var errorMessage))
        {
            return Error(toolName, source, roi, errorMessage);
        }

        try
        {
            return Translate(toolName, execute(heightMap!), source!, roi);
        }
        catch (Exception exception)
        {
            return Error(toolName, source, roi, $"Library-Noah execution failed: {exception.Message}");
        }
    }

    private static bool TryCreateHeightMap(
        LibraryNoahHeightMapInput? source,
        out NoahHeightMap3D? heightMap,
        out string errorMessage)
    {
        heightMap = null;
        if (source is null)
        {
            errorMessage = "Declared height-map source is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(source.SourceEntityId)
            || string.IsNullOrWhiteSpace(source.Unit)
            || string.IsNullOrWhiteSpace(source.FrameId))
        {
            errorMessage = "Source entity ID, unit, and frame ID are required.";
            return false;
        }

        if (source.Values is null)
        {
            errorMessage = "Height-map values are required.";
            return false;
        }

        try
        {
            heightMap = new NoahHeightMap3D(
                source.Rows,
                source.Columns,
                source.OriginX,
                source.OriginY,
                source.ColumnPitch,
                source.RowPitch,
                source.Values.ToArray(),
                source.Unit,
                source.FrameId,
                source.SourceEntityId);
            errorMessage = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or ArgumentOutOfRangeException)
        {
            errorMessage = $"Declared height map is invalid: {exception.Message}";
            return false;
        }
    }

    private static NoahHeightMapRoi? ToNoahRoi(LibraryNoahGridRoi? roi) =>
        roi is null
            ? null
            : new NoahHeightMapRoi(roi.Row, roi.Column, roi.RowCount, roi.ColumnCount);

    private static LibraryNoahInspectionEvaluation Translate(
        string toolName,
        NoahInspectionResult inspection,
        LibraryNoahHeightMapInput source,
        LibraryNoahGridRoi? roi)
    {
        var status = inspection.ResultStatus switch
        {
            NoahInspectionStatus.Passed => ResultStatus.Pass,
            NoahInspectionStatus.Failed => ResultStatus.Fail,
            _ => ResultStatus.Error
        };
        var unit = string.IsNullOrWhiteSpace(inspection.Unit) ? source.Unit : inspection.Unit;
        ResultStatus? metricStatus = inspection.HasMeasurement ? null : ResultStatus.Error;
        var metrics = inspection.Metrics
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new Metric(
                pair.Key,
                ResolveMetricKind(pair.Key),
                pair.Value,
                ResolveMetricUnit(pair.Key, unit),
                metricStatus))
            .ToArray();

        if (metrics.Length == 0 && status == ResultStatus.Error)
        {
            metrics =
            [
                new Metric(
                    "Library-Noah error code",
                    MetricKind.Number,
                    inspection.ErrorCodeValue,
                    "code",
                    ResultStatus.Error)
            ];
        }

        return new LibraryNoahInspectionEvaluation(
            new ToolResult(toolName, status, inspection.Message, inspection.Elapsed, metrics, []),
            inspection.HasMeasurement,
            inspection.ResultStatusName,
            inspection.ErrorName,
            roi);
    }

    private static LibraryNoahInspectionEvaluation Error(
        string toolName,
        LibraryNoahHeightMapInput? source,
        LibraryNoahGridRoi? roi,
        string message) =>
        new(
            new ToolResult(
                toolName,
                ResultStatus.Error,
                message,
                TimeSpan.Zero,
                [new Metric("Library-Noah error code", MetricKind.Number, double.NaN, "code", ResultStatus.Error)],
                []),
            false,
            "BridgeError",
            "BridgeValidation",
            roi);

    private static MetricKind ResolveMetricKind(string name) =>
        name switch
        {
            "ValidSampleCount" or "MissingSampleCount" or "BelowLowerLimitCount" or "AboveUpperLimitCount"
                or "MinimumResidualRow" or "MinimumResidualColumn" or "MaximumResidualRow" or "MaximumResidualColumn" => MetricKind.Count,
            "PeakToValley" or "Rms" or "MinimumResidual" or "MaximumResidual" or "MaximumPeakToValley" or "MaximumRms"
                or "MinimumRawHeightResidual" or "MaximumRawHeightResidual" or "PeakToValleyRawHeight" or "RmsRawHeightResidual" or "MaximumPeakToValleyRawHeight" => MetricKind.Deviation,
            "PlaneSlopeX" or "PlaneSlopeY" or "PlaneIntercept" or "PlaneNormalX" or "PlaneNormalY" or "PlaneNormalZ" or "PlaneOffset" or "MinimumAbsoluteNormalY" => MetricKind.Number,
            _ => MetricKind.Length
        };

    private static string ResolveMetricUnit(string name, string unit) =>
        ResolveMetricKind(name) switch
        {
            MetricKind.Count => "count",
            MetricKind.Number => "ratio",
            _ => unit
        };
}

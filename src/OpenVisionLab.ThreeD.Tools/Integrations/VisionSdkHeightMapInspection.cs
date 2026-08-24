using SdkHeightMap3D = OpenVisionLab.Vision3D.Geometry.HeightMap3D;
using SdkHeightMapRoi = OpenVisionLab.Vision3D.Geometry.HeightMapRoi;
using SdkHeightMapInputRequirements = OpenVisionLab.Vision3D.Inspection.HeightMapInputRequirements;
using SdkInspectionResult = OpenVisionLab.Vision3D.Inspection.ThreeDInspectionResult;
using SdkInspectionStatus = OpenVisionLab.Vision3D.Inspection.ThreeDInspectionResultStatus;
using SdkThicknessInspectionOptions = OpenVisionLab.Vision3D.Inspection.ThicknessInspectionOptions;
using SdkThicknessInspectionTool = OpenVisionLab.Vision3D.Inspection.ThicknessInspectionTool;
using SdkWarpageInspectionOptions = OpenVisionLab.Vision3D.Inspection.WarpageInspectionOptions;
using SdkWarpageInspectionTool = OpenVisionLab.Vision3D.Inspection.WarpageInspectionTool;
using SdkDatumPlaneRawHeightDeviationInspectionOptions = OpenVisionLab.Vision3D.Inspection.DatumPlaneRawHeightDeviationInspectionOptions;
using SdkDatumPlaneRawHeightDeviationInspectionTool = OpenVisionLab.Vision3D.Inspection.DatumPlaneRawHeightDeviationInspectionTool;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record VisionSdkHeightMapContract(
    string PlanarUnit,
    string HeightUnit,
    string FrameId);

public sealed record VisionSdkHeightMapInput(
    string SourceEntityId,
    int Rows,
    int Columns,
    double OriginX,
    double OriginY,
    double ColumnPitch,
    double RowPitch,
    IReadOnlyList<double>? Values,
    string Unit,
    string FrameId)
{
    public string? PlanarUnit { get; init; }

    public string? HeightUnit { get; init; }

    public VisionSdkHeightMapContract? ExpectedContract { get; init; }

    internal string EffectivePlanarUnit => string.IsNullOrWhiteSpace(PlanarUnit) ? Unit : PlanarUnit;

    internal string EffectiveHeightUnit => string.IsNullOrWhiteSpace(HeightUnit) ? Unit : HeightUnit;
}

public sealed record VisionSdkGridRoi(int Row, int Column, int RowCount, int ColumnCount);

public sealed record VisionSdkThicknessInspectionInput(
    VisionSdkHeightMapInput? Source,
    VisionSdkGridRoi? Roi,
    double MinimumThickness,
    double MaximumThickness,
    int MinimumValidSamples = 1,
    double MinimumValidCoverageRatio = 0.0);

public sealed record VisionSdkWarpageInspectionInput(
    VisionSdkHeightMapInput? Source,
    VisionSdkGridRoi? Roi,
    double MaximumPeakToValley,
    double? MaximumRms = null,
    int MinimumValidSamples = 3,
    double MinimumValidCoverageRatio = 0.0);

public sealed record VisionSdkDatumPlaneRawHeightDeviationInspectionInput(
    VisionSdkHeightMapInput? Source,
    VisionSdkGridRoi? Roi,
    double PlaneNormalX,
    double PlaneNormalY,
    double PlaneNormalZ,
    double PlaneOffset,
    double MaximumPeakToValleyRawHeight,
    int MinimumValidSamples = 3,
    double MinimumAbsoluteNormalY = 0.1,
    double MinimumValidCoverageRatio = 0.0);

public sealed record VisionSdkInspectionEvaluation(
    ToolResult Result,
    bool HasMeasurement,
    string PackageResultStatus,
    string PackageErrorCode,
    VisionSdkGridRoi? Roi,
    string PlanarUnit = "",
    string HeightUnit = "",
    string CoordinateConvention = "");

/// <summary>
/// Explicit Studio-to-OpenVisionLab Vision SDK boundary for a declared scalar height map.
/// It does not infer physical units, scalar meaning, calibration, or a Viewer overlay.
/// </summary>
public static class VisionSdkHeightMapInspection
{
    public const string PackageId = "OpenVisionLab.Vision3D";
    public const string PackageVersion = "3.0.1-dev.20260823.grid-diagnostics.1";
    public const string PackageSourceCommit = "8be38403d0d00698431d7ffa4de60a63289672c6";

    public static string PackageAssemblyName => typeof(SdkHeightMap3D).Assembly.GetName().Name ?? string.Empty;

    public static VisionSdkInspectionEvaluation EvaluateThickness(VisionSdkThicknessInspectionInput? input)
    {
        const string toolName = "OpenVisionLab Vision SDK Thickness";
        if (input is null)
        {
            return Error(toolName, null, null, "Thickness inspection input is required.");
        }

        return Execute(
            toolName,
            input.Source,
            input.Roi,
            heightMap => new SdkThicknessInspectionTool(new SdkThicknessInspectionOptions
            {
                Roi = ToSdkRoi(input.Roi),
                MinimumThickness = input.MinimumThickness,
                MaximumThickness = input.MaximumThickness,
                MinimumValidSamples = input.MinimumValidSamples,
                MinimumValidCoverageRatio = input.MinimumValidCoverageRatio,
                InputRequirements = ToSdkRequirements(input.Source!)
            }).Execute(heightMap));
    }

    public static VisionSdkInspectionEvaluation EvaluateWarpage(VisionSdkWarpageInspectionInput? input)
    {
        const string toolName = "OpenVisionLab Vision SDK Warpage";
        if (input is null)
        {
            return Error(toolName, null, null, "Warpage inspection input is required.");
        }

        return Execute(
            toolName,
            input.Source,
            input.Roi,
            heightMap => new SdkWarpageInspectionTool(new SdkWarpageInspectionOptions
            {
                Roi = ToSdkRoi(input.Roi),
                MaximumPeakToValley = input.MaximumPeakToValley,
                MaximumRms = input.MaximumRms,
                MinimumValidSamples = input.MinimumValidSamples,
                MinimumValidCoverageRatio = input.MinimumValidCoverageRatio,
                InputRequirements = ToSdkRequirements(input.Source!)
            }).Execute(heightMap));
    }

    public static VisionSdkInspectionEvaluation EvaluateDatumPlaneRawHeightDeviation(
        VisionSdkDatumPlaneRawHeightDeviationInspectionInput? input)
    {
        const string toolName = "OpenVisionLab Vision SDK Datum Plane Raw-Height Deviation";
        if (input is null)
        {
            return Error(toolName, null, null, "Datum-plane raw-height deviation input is required.");
        }

        return Execute(
            toolName,
            input.Source,
            input.Roi,
            heightMap => new SdkDatumPlaneRawHeightDeviationInspectionTool(
                new SdkDatumPlaneRawHeightDeviationInspectionOptions
                {
                    Roi = ToSdkRoi(input.Roi),
                    PlaneNormalX = input.PlaneNormalX,
                    PlaneNormalY = input.PlaneNormalY,
                    PlaneNormalZ = input.PlaneNormalZ,
                    PlaneOffset = input.PlaneOffset,
                    MaximumPeakToValleyRawHeight = input.MaximumPeakToValleyRawHeight,
                    MinimumValidSamples = input.MinimumValidSamples,
                    MinimumAbsoluteNormalY = input.MinimumAbsoluteNormalY,
                    MinimumValidCoverageRatio = input.MinimumValidCoverageRatio,
                    InputRequirements = ToSdkRequirements(input.Source!)
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
        SdkDatumPlaneRawHeightDeviationInspectionTool.TryCalculateRawHeightResidual(
            normalX,
            normalY,
            normalZ,
            planeOffset,
            gridX,
            gridY,
            rawHeight,
            out residual);

    private static VisionSdkInspectionEvaluation Execute(
        string toolName,
        VisionSdkHeightMapInput? source,
        VisionSdkGridRoi? roi,
        Func<SdkHeightMap3D, SdkInspectionResult> execute)
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
            return Error(toolName, source, roi, $"OpenVisionLab Vision SDK execution failed: {exception.Message}");
        }
    }

    private static bool TryCreateHeightMap(
        VisionSdkHeightMapInput? source,
        out SdkHeightMap3D? heightMap,
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
            || string.IsNullOrWhiteSpace(source.EffectivePlanarUnit)
            || string.IsNullOrWhiteSpace(source.EffectiveHeightUnit)
            || string.IsNullOrWhiteSpace(source.FrameId))
        {
            errorMessage = "Source entity ID, planar unit, height unit, legacy unit, and frame ID are required.";
            return false;
        }

        if (source.Values is null)
        {
            errorMessage = "Height-map values are required.";
            return false;
        }

        try
        {
            heightMap = new SdkHeightMap3D(
                source.Rows,
                source.Columns,
                source.OriginX,
                source.OriginY,
                source.ColumnPitch,
                source.RowPitch,
                source.Values.ToArray(),
                source.EffectivePlanarUnit,
                source.EffectiveHeightUnit,
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

    private static SdkHeightMapRoi? ToSdkRoi(VisionSdkGridRoi? roi) =>
        roi is null
            ? null
            : new SdkHeightMapRoi(roi.Row, roi.Column, roi.RowCount, roi.ColumnCount);

    private static SdkHeightMapInputRequirements ToSdkRequirements(VisionSdkHeightMapInput source)
    {
        var contract = source.ExpectedContract
            ?? new VisionSdkHeightMapContract(
                source.EffectivePlanarUnit,
                source.EffectiveHeightUnit,
                source.FrameId);
        return new SdkHeightMapInputRequirements(contract.PlanarUnit, contract.HeightUnit, contract.FrameId);
    }

    private static VisionSdkInspectionEvaluation Translate(
        string toolName,
        SdkInspectionResult inspection,
        VisionSdkHeightMapInput source,
        VisionSdkGridRoi? roi)
    {
        var status = inspection.ResultStatus switch
        {
            SdkInspectionStatus.Passed => ResultStatus.Pass,
            SdkInspectionStatus.Failed => ResultStatus.Fail,
            _ => ResultStatus.Error
        };
        var planarUnit = string.IsNullOrWhiteSpace(inspection.PlanarUnit) ? source.EffectivePlanarUnit : inspection.PlanarUnit;
        var heightUnit = string.IsNullOrWhiteSpace(inspection.HeightUnit) ? source.EffectiveHeightUnit : inspection.HeightUnit;
        ResultStatus? metricStatus = inspection.HasMeasurement ? null : ResultStatus.Error;
        var metrics = inspection.Metrics
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new Metric(
                pair.Key,
                ResolveMetricKind(pair.Key),
                pair.Value,
                ResolveMetricUnit(inspection, pair.Key, planarUnit, heightUnit),
                metricStatus))
            .ToArray();

        if (metrics.Length == 0 && status == ResultStatus.Error)
        {
            metrics =
            [
                new Metric(
                    "OpenVisionLab Vision SDK error code",
                    MetricKind.Number,
                    inspection.ErrorCodeValue,
                    "code",
                    ResultStatus.Error)
            ];
        }

        return new VisionSdkInspectionEvaluation(
            new ToolResult(toolName, status, inspection.Message, inspection.Elapsed, metrics, []),
            inspection.HasMeasurement,
            inspection.ResultStatusName,
            inspection.ErrorName,
            roi,
            planarUnit,
            heightUnit,
            inspection.CoordinateConvention);
    }

    private static VisionSdkInspectionEvaluation Error(
        string toolName,
        VisionSdkHeightMapInput? source,
        VisionSdkGridRoi? roi,
        string message) =>
        new(
            new ToolResult(
                toolName,
                ResultStatus.Error,
                message,
                TimeSpan.Zero,
                [new Metric("OpenVisionLab Vision SDK error code", MetricKind.Number, double.NaN, "code", ResultStatus.Error)],
                []),
            false,
            "BridgeError",
            "BridgeValidation",
            roi,
            source?.EffectivePlanarUnit ?? string.Empty,
            source?.EffectiveHeightUnit ?? string.Empty);

    private static MetricKind ResolveMetricKind(string name) =>
        name switch
        {
            "TotalSampleCount" or "ValidSampleCount" or "MissingSampleCount" or "MinimumValidSamples"
                or "BelowLowerLimitCount" or "AboveUpperLimitCount"
                or "MinimumResidualRow" or "MinimumResidualColumn" or "MaximumResidualRow" or "MaximumResidualColumn" => MetricKind.Count,
            "ValidCoverageRatio" or "MinimumValidCoverageRatio" => MetricKind.Number,
            "PeakToValley" or "Rms" or "MinimumResidual" or "MaximumResidual" or "MaximumPeakToValley" or "MaximumRms"
                or "MinimumRawHeightResidual" or "MaximumRawHeightResidual" or "PeakToValleyRawHeight" or "RmsRawHeightResidual" or "MaximumPeakToValleyRawHeight" => MetricKind.Deviation,
            "PlaneSlopeX" or "PlaneSlopeY" or "PlaneIntercept" or "PlaneNormalX" or "PlaneNormalY" or "PlaneNormalZ" or "PlaneOffset" or "MinimumAbsoluteNormalY" => MetricKind.Number,
            _ => MetricKind.Length
        };

    private static string ResolveMetricUnit(
        SdkInspectionResult inspection,
        string name,
        string planarUnit,
        string heightUnit)
    {
        if (inspection.MetricUnits.TryGetValue(name, out var unit) && !string.IsNullOrWhiteSpace(unit))
        {
            return unit;
        }

        return ResolveMetricKind(name) switch
        {
            MetricKind.Count => "count",
            MetricKind.Number => "ratio",
            _ when name is "PlaneSlopeX" or "PlaneSlopeY" => $"{heightUnit}/{planarUnit}",
            _ => heightUnit
        };
    }
}

using System.Diagnostics;
using SdkConnectedRegionConnectivity = OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegionConnectivity;
using SdkConnectedRegionMetricsOptions = OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegionMetricsOptions;
using SdkConnectedRegionMetricsTool = OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegionMetricsTool;
using SdkConnectedRegionOptions = OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegionOptions;
using SdkConnectedRegionTool = OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegionTool;
using SdkHeightGridMask = OpenVisionLab.Vision3D.FeatureExtraction.HeightGridMask;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DConnectedRegionInput(
    string OutputEntityId,
    string RootSourceEntityId,
    C3DHeightFieldSnapshot Source,
    C3DConnectedRegionMask Mask,
    C3DConnectedRegionConnectivity Connectivity = C3DConnectedRegionConnectivity.Four);

public sealed record C3DConnectedRegionEvaluation(
    ToolResult Result,
    C3DConnectedRegionOutput? Output);

/// <summary>
/// Studio adapter for the Vision SDK connected-region and region-metrics
/// tools. Studio validates source/mask identity and maps typed evidence; all
/// labeling and metric arithmetic remain in the SDK.
/// </summary>
public static class C3DConnectedRegionRule
{
    public const string ToolName = "Connected Region";

    public static C3DConnectedRegionEvaluation Evaluate(
        C3DConnectedRegionInput input,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ValidateStudioContract(input);
            var sdkConnectivity = ToSdkConnectivity(input.Connectivity);
            var sdkMask = new SdkHeightGridMask(
                input.Source.Height,
                input.Source.Width,
                input.Mask.Foreground);
            var labeled = new SdkConnectedRegionTool().Execute(
                sdkMask,
                new SdkConnectedRegionOptions
                {
                    Connectivity = sdkConnectivity
                },
                cancellationToken);
            if (!labeled.Success)
            {
                throw new InvalidDataException(labeled.Message);
            }

            var metrics = new SdkConnectedRegionMetricsTool().Execute(
                labeled,
                new SdkConnectedRegionMetricsOptions
                {
                    OriginX = input.Source.GridOriginColumn,
                    OriginY = input.Source.GridOriginRow,
                    ColumnPitch = 1d,
                    RowPitch = 1d
                },
                cancellationToken);
            if (!metrics.Success)
            {
                throw new InvalidDataException(metrics.Message);
            }

            if (labeled.RegionCount != metrics.RegionCount
                || labeled.ForegroundCellCount < 1
                || labeled.VisitedCellCount != labeled.ForegroundCellCount)
            {
                throw new InvalidDataException(
                    "Connected-region labeling and metrics counts do not agree.");
            }

            var regions = labeled.Regions
                .Select((region, position) => MapRegion(
                    input.OutputEntityId,
                    region,
                    metrics.Regions[position]))
                .ToArray();
            var provenance =
                $"{ToolName}:contract={C3DConnectedRegionOutput.ContractVersion}:source={input.Source.RootSourceSha256}:input={input.Source.ContentSha256}:mask={input.Mask.ContentSha256}:connectivity={input.Connectivity}:coordinates=GridXGridYCellCenterFootprint";
            var output = C3DConnectedRegionOutput.Create(
                input.OutputEntityId,
                input.RootSourceEntityId,
                input.Source.RootSourceSha256,
                input.Source.EntityId,
                input.Source.ContentSha256,
                input.Mask,
                input.Source.Unit,
                input.Source.FrameId,
                input.Source.Width,
                input.Source.Height,
                input.Connectivity,
                labeled.ForegroundCellCount,
                labeled.VisitedCellCount,
                regions,
                provenance);

            var resultMetrics = new List<Metric>
            {
                new("Region count", MetricKind.Count, output.RegionCount, "regions", ResultStatus.Pass),
                new("Foreground cell count", MetricKind.Count, output.ForegroundCellCount, "cells", ResultStatus.Pass),
                new("Total region area", MetricKind.Area, metrics.TotalArea, "grid-index²", ResultStatus.Pass)
            };
            foreach (var region in output.Regions)
            {
                resultMetrics.Add(new Metric(
                    $"{region.RegionId} cell count",
                    MetricKind.Count,
                    region.CellCount,
                    "cells",
                    ResultStatus.Pass));
                resultMetrics.Add(new Metric(
                    $"{region.RegionId} area",
                    MetricKind.Area,
                    region.Area,
                    "grid-index²",
                    ResultStatus.Pass));
                resultMetrics.Add(new Metric(
                    $"{region.RegionId} center X",
                    MetricKind.Number,
                    region.CenterX,
                    "grid-index",
                    ResultStatus.Pass));
                resultMetrics.Add(new Metric(
                    $"{region.RegionId} center Y",
                    MetricKind.Number,
                    region.CenterY,
                    "grid-index",
                    ResultStatus.Pass));
                if (region.HasOrientation)
                {
                    resultMetrics.Add(new Metric(
                        $"{region.RegionId} orientation",
                        MetricKind.Angle,
                        region.OrientationDegrees,
                        "degrees",
                        ResultStatus.Pass));
                }
            }

            stopwatch.Stop();
            return new C3DConnectedRegionEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Pass,
                    $"Detected {output.RegionCount} deterministic connected region(s) from the explicit source-bound mask; area is reported in grid-index², not calibrated physical units.",
                    stopwatch.Elapsed,
                    resultMetrics,
                    []),
                output);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            stopwatch.Stop();
            return new C3DConnectedRegionEvaluation(
                new ToolResult(
                    ToolName,
                    ResultStatus.Error,
                    exception.Message,
                    stopwatch.Elapsed,
                    [],
                    []),
                null);
        }
    }

    private static C3DConnectedRegionMetricOutput MapRegion(
        string outputEntityId,
        OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegion region,
        OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegionMetric metric)
    {
        if (region.Index != metric.Index || region.CellCount != metric.CellCount)
        {
            throw new InvalidDataException(
                "Connected-region metrics are not aligned with the labeled region order.");
        }

        var bounding = metric.Bounding
            ?? throw new InvalidDataException("Connected-region metrics did not return bounds.");
        if (!double.IsFinite(metric.Area)
            || !double.IsFinite(metric.CenterX)
            || !double.IsFinite(metric.CenterY)
            || !double.IsFinite(bounding.MinimumX)
            || !double.IsFinite(bounding.MinimumY)
            || !double.IsFinite(bounding.MaximumX)
            || !double.IsFinite(bounding.MaximumY)
            || !double.IsFinite(bounding.Width)
            || !double.IsFinite(bounding.Height)
            || (metric.HasOrientation && !double.IsFinite(metric.OrientationDegrees)))
        {
            throw new InvalidDataException(
                "Connected-region metrics must contain finite mapped geometry.");
        }

        return new C3DConnectedRegionMetricOutput(
            $"{outputEntityId}.region.{region.Index + 1:D3}",
            region.Index,
            region.CellCount,
            region.SeedRow,
            region.SeedColumn,
            bounding.MinimumRow,
            bounding.MinimumColumn,
            bounding.MaximumRow,
            bounding.MaximumColumn,
            metric.Area,
            metric.CenterX,
            metric.CenterY,
            metric.HasOrientation,
            metric.HasOrientation ? metric.OrientationDegrees : double.NaN,
            bounding.MinimumX,
            bounding.MinimumY,
            bounding.MaximumX,
            bounding.MaximumY,
            bounding.Width,
            bounding.Height,
            bounding.CoordinateConvention,
            region.Cells
                .Select(cell => new C3DConnectedRegionCell(cell.Row, cell.Column))
                .ToArray());
    }

    private static void ValidateStudioContract(C3DConnectedRegionInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.Source);
        ArgumentNullException.ThrowIfNull(input.Mask);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.OutputEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.RootSourceEntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Source.EntityId);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Source.ContentSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Source.RootSourceSha256);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Source.Unit);
        ArgumentException.ThrowIfNullOrWhiteSpace(input.Source.FrameId);
        if (input.Source.Width < 1
            || input.Source.Height < 1
            || input.Source.Values.Length != checked(input.Source.Width * input.Source.Height))
        {
            throw new InvalidDataException(
                "Connected Region source dimensions and row-major values must agree.");
        }

        if (input.Mask.Width != input.Source.Width
            || input.Mask.Height != input.Source.Height
            || input.Mask.Foreground.Count
                != checked(input.Source.Width * input.Source.Height))
        {
            throw new InvalidDataException(
                "Connected Region mask dimensions must match the source height field.");
        }

        if (!string.Equals(
                input.Mask.SourceEntityId,
                input.Source.EntityId,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                input.Mask.SourceContentSha256,
                input.Source.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "Connected Region mask must share the exact current source identity.");
        }

        if (!Enum.IsDefined(input.Connectivity))
        {
            throw new InvalidDataException(
                "Connected Region connectivity must be Four or Eight.");
        }

        var sourceValues = input.Source.Values.Span;
        var hasForeground = false;
        for (var index = 0; index < input.Mask.Foreground.Count; index++)
        {
            if (!input.Mask.Foreground[index])
            {
                continue;
            }

            hasForeground = true;
            if (!double.IsFinite(sourceValues[index]))
            {
                throw new InvalidDataException(
                    "Connected Region foreground cells must reference finite source heights.");
            }
        }

        if (!hasForeground)
        {
            throw new InvalidDataException(
                "Connected Region requires at least one foreground cell.");
        }
    }

    private static SdkConnectedRegionConnectivity ToSdkConnectivity(
        C3DConnectedRegionConnectivity connectivity) =>
        connectivity switch
        {
            C3DConnectedRegionConnectivity.Four => SdkConnectedRegionConnectivity.Four,
            C3DConnectedRegionConnectivity.Eight => SdkConnectedRegionConnectivity.Eight,
            _ => throw new InvalidDataException(
                "Connected Region connectivity must be Four or Eight.")
        };
}

using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Maps the existing G11/G12 analysis adapters into the source-neutral Core
/// connected-region artifact contract. No connected-region arithmetic is
/// performed here.
/// </summary>
public static class C3DConnectedRegionArtifactFactory
{
    public static C3DConnectedRegionArtifact Create(
        string artifactId,
        string name,
        C3DHeightFieldSnapshot source,
        C3DOutlierCellMap mask,
        C3DConnectedRegionAnalysis analysis,
        C3DConnectedRegionConnectivity connectivity,
        C3DConnectedRegionMetricsAnalysis? metrics = null,
        double originX = 0.0,
        double originY = 0.0,
        double columnPitch = 1.0,
        double rowPitch = 1.0,
        string? areaUnit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mask);
        ArgumentNullException.ThrowIfNull(analysis);

        if (source.Width != mask.Width || source.Height != mask.Height)
        {
            throw new InvalidDataException(
                "Connected-region source and mask grids must have identical dimensions.");
        }

        if (!analysis.Success)
        {
            throw new InvalidDataException(
                $"Connected-region analysis is not successful: {analysis.Message}");
        }

        var connectivityName = connectivity switch
        {
            C3DConnectedRegionConnectivity.Four =>
                C3DConnectedRegionArtifact.FourConnectivity,
            C3DConnectedRegionConnectivity.Eight =>
                C3DConnectedRegionArtifact.EightConnectivity,
            _ => throw new ArgumentOutOfRangeException(nameof(connectivity))
        };

        var metricByIndex = BuildMetricsByIndex(analysis, metrics);
        var resolvedAreaUnit = string.IsNullOrWhiteSpace(areaUnit)
            ? metrics?.AreaUnit
            : areaUnit;
        resolvedAreaUnit = string.IsNullOrWhiteSpace(resolvedAreaUnit)
            ? "grid-unit^2"
            : resolvedAreaUnit.Trim();

        var regions = (analysis.Regions ?? [])
            .Select(region => new C3DConnectedRegionArtifactRegion(
                region.Index,
                region.SeedRow,
                region.SeedColumn,
                (region.Cells ?? [])
                    .Select(cell => new C3DConnectedRegionArtifactCell(
                        cell.Row,
                        cell.Column))
                    .ToArray(),
                region.MinimumRow,
                region.MinimumColumn,
                region.MaximumRow,
                region.MaximumColumn,
                metricByIndex is not null
                    ? MapMetrics(metricByIndex[region.Index])
                    : null))
            .ToArray();

        return C3DConnectedRegionArtifact.Create(
            artifactId,
            name,
            source.EntityId,
            source.ContentSha256,
            source.RootSourceSha256,
            mask.Sha256,
            source.Unit,
            source.FrameId,
            source.Width,
            source.Height,
            connectivityName,
            originX,
            originY,
            columnPitch,
            rowPitch,
            resolvedAreaUnit,
            regions);
    }

    private static IReadOnlyDictionary<int, C3DConnectedRegionMetric>? BuildMetricsByIndex(
        C3DConnectedRegionAnalysis analysis,
        C3DConnectedRegionMetricsAnalysis? metrics)
    {
        if (metrics is null)
        {
            return null;
        }

        if (!metrics.Success)
        {
            throw new InvalidDataException(
                $"Connected-region metrics analysis is not successful: {metrics.Message}");
        }

        var analysisRegions = analysis.Regions ?? [];
        var metricRegions = metrics.Regions ?? [];
        if (metricRegions.Count != analysisRegions.Count)
        {
            throw new InvalidDataException(
                "Connected-region metrics count does not match the detection result.");
        }

        var analysisIndices = analysisRegions.Select(region => region.Index).ToHashSet();
        var metricByIndex = metricRegions.ToDictionary(region => region.Index);
        if (!analysisIndices.SetEquals(metricByIndex.Keys))
        {
            throw new InvalidDataException(
                "Connected-region metrics indexes do not match the detection result.");
        }

        foreach (var region in analysisRegions)
        {
            var metric = metricByIndex[region.Index];
            if (metric.CellCount != region.CellCount
                || metric.Bounding is null
                || metric.Bounding.MinimumRow != region.MinimumRow
                || metric.Bounding.MinimumColumn != region.MinimumColumn
                || metric.Bounding.MaximumRow != region.MaximumRow
                || metric.Bounding.MaximumColumn != region.MaximumColumn)
            {
                throw new InvalidDataException(
                    $"Connected-region metrics for region {region.Index} do not preserve the detection bounds.");
            }
        }

        return metricByIndex;
    }

    private static C3DConnectedRegionArtifactMetrics MapMetrics(
        C3DConnectedRegionMetric metric) =>
        new(
            metric.CellCount,
            metric.Area,
            metric.CenterX,
            metric.CenterY,
            metric.HasOrientation,
            metric.OrientationDegrees,
            new C3DConnectedRegionArtifactBounding(
                metric.Bounding.MinimumRow,
                metric.Bounding.MinimumColumn,
                metric.Bounding.MaximumRow,
                metric.Bounding.MaximumColumn,
                metric.Bounding.MinimumX,
                metric.Bounding.MinimumY,
                metric.Bounding.MaximumX,
                metric.Bounding.MaximumY));
}

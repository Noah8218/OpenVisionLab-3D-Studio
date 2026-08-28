using OpenVisionLab.Vision3D.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Data;

public enum C3DConnectedRegionConnectivity
{
    Four,
    Eight
}

public readonly record struct C3DConnectedRegionCell(int Row, int Column);

public sealed record C3DConnectedRegion(
    int Index,
    int SeedRow,
    int SeedColumn,
    IReadOnlyList<C3DConnectedRegionCell> Cells,
    int MinimumRow,
    int MinimumColumn,
    int MaximumRow,
    int MaximumColumn)
{
    public int CellCount => Cells.Count;
}

public sealed record C3DConnectedRegionAnalysis(
    bool Success,
    string Message,
    IReadOnlyList<C3DConnectedRegion> Regions,
    int ForegroundCellCount,
    int VisitedCellCount)
{
    public int RegionCount => Regions.Count;
}

public sealed record C3DConnectedRegionMetricsOptions
{
    public double OriginX { get; init; }
    public double OriginY { get; init; }
    public double ColumnPitch { get; init; } = 1.0;
    public double RowPitch { get; init; } = 1.0;
    public string? AreaUnit { get; init; } = "grid-unit^2";
    public int? SelectedRegionIndex { get; init; }
}

public sealed record C3DConnectedRegionBoundingArtifact(
    int MinimumRow,
    int MinimumColumn,
    int MaximumRow,
    int MaximumColumn,
    double MinimumX,
    double MinimumY,
    double MaximumX,
    double MaximumY)
{
    public double Width => MaximumX - MinimumX;
    public double Height => MaximumY - MinimumY;
    public string CoordinateConvention => "GridXGridYCellCenterFootprint";
}

public sealed record C3DConnectedRegionMetric(
    int Index,
    int CellCount,
    double Area,
    double CenterX,
    double CenterY,
    bool HasOrientation,
    double OrientationDegrees,
    C3DConnectedRegionBoundingArtifact Bounding);

public sealed record C3DConnectedRegionOverlay(
    int RegionIndex,
    bool IsSelected,
    C3DConnectedRegionBoundingArtifact Bounding);

public sealed record C3DConnectedRegionMetricsAnalysis(
    bool Success,
    string Message,
    string AreaUnit,
    IReadOnlyList<C3DConnectedRegionMetric> Regions,
    IReadOnlyList<C3DConnectedRegionOverlay> Overlays,
    int? SelectedRegionIndex,
    double TotalArea)
{
    public int RegionCount => Regions.Count;
}

public sealed record C3DConnectedRegionDimensionsOptions
{
    public double OriginX { get; init; }
    public double OriginY { get; init; }
    public double ColumnPitch { get; init; } = 1.0;
    public double RowPitch { get; init; } = 1.0;
    public string? DimensionUnit { get; init; } = "grid-unit";
    public string? AreaUnit { get; init; } = "grid-unit^2";
}

public sealed record C3DConnectedRegionDimensionMetric(
    int Index,
    int CellCount,
    double Width,
    double Height,
    double Area);

public sealed record C3DConnectedRegionDimensionsAnalysis(
    bool Success,
    string Message,
    string DimensionUnit,
    string AreaUnit,
    IReadOnlyList<C3DConnectedRegionDimensionMetric> Regions,
    double TotalArea)
{
    public int RegionCount => Regions.Count;
}

public enum C3DConnectedRegionPresenceDecision
{
    NotEvaluated,
    Present,
    Missing
}

public enum C3DConnectedRegionPresenceCoverageDisposition
{
    NotEvaluated,
    Accepted,
    BelowMinimum
}

public enum C3DConnectedRegionPresenceHeightDisposition
{
    NotEvaluated,
    Accepted,
    Missing,
    BelowMinimum,
    AboveMaximum
}

public sealed record C3DConnectedRegionPresenceOptions
{
    public double MinimumFiniteCoverageRatio { get; init; } = 1.0;
    public double? MinimumMeanHeight { get; init; }
    public double? MaximumMeanHeight { get; init; }
    public string? HeightUnit { get; init; } = "raw-height";
}

public sealed record C3DConnectedRegionPresenceMetric(
    int Index,
    int TotalCellCount,
    int FiniteCellCount,
    double FiniteCoverageRatio,
    double? MeanHeight,
    C3DConnectedRegionPresenceCoverageDisposition CoverageDisposition,
    C3DConnectedRegionPresenceHeightDisposition HeightDisposition,
    C3DConnectedRegionPresenceDecision Decision)
{
    public int MissingCellCount => TotalCellCount - FiniteCellCount;
}

public sealed record C3DConnectedRegionPresenceAnalysis(
    bool Success,
    string Message,
    string HeightUnit,
    IReadOnlyList<C3DConnectedRegionPresenceMetric> Regions,
    int PresentRegionCount,
    int MissingRegionCount,
    C3DConnectedRegionPresenceDecision AggregateDecision)
{
    public int RegionCount => Regions.Count;
}

public enum C3DConnectedRegionAllRegionsAcceptanceDecision
{
    NotEvaluated,
    Accepted,
    Rejected
}

public sealed record C3DConnectedRegionAllRegionsAcceptanceAnalysis(
    bool Success,
    string Message,
    string HeightUnit,
    IReadOnlyList<C3DConnectedRegionPresenceMetric> Regions,
    int AcceptedRegionCount,
    int RejectedRegionCount,
    C3DConnectedRegionAllRegionsAcceptanceDecision AggregateDecision)
{
    public int RegionCount => Regions.Count;
}

public enum C3DConnectedRegionFillHeightDecision
{
    NotEvaluated,
    Accepted,
    Rejected
}

public enum C3DConnectedRegionFillHeightCoverageDisposition
{
    NotEvaluated,
    Accepted,
    BelowMinimum
}

public enum C3DConnectedRegionFillHeightDisposition
{
    NotEvaluated,
    Accepted,
    Missing,
    BelowMinimum,
    AboveMaximum
}

public sealed record C3DConnectedRegionFillHeightReferenceSurface(
    double SlopeX,
    double SlopeZ,
    double Intercept);

public sealed record C3DConnectedRegionFillHeightOptions
{
    public C3DConnectedRegionFillHeightReferenceSurface? ReferenceSurface { get; init; }
    public double MinimumFiniteCoverageRatio { get; init; } = 1.0;
    public double? MinimumMeanFillHeight { get; init; }
    public double? MaximumMeanFillHeight { get; init; }
    public string? HeightUnit { get; init; } = "raw-height";
}

public sealed record C3DConnectedRegionFillHeightMetric(
    int Index,
    int TotalCellCount,
    int FiniteCellCount,
    double FiniteCoverageRatio,
    double? MeanFillHeight,
    double? MinimumFillHeight,
    double? MaximumFillHeight,
    C3DConnectedRegionFillHeightCoverageDisposition CoverageDisposition,
    C3DConnectedRegionFillHeightDisposition FillHeightDisposition,
    C3DConnectedRegionFillHeightDecision Decision)
{
    public int MissingCellCount => TotalCellCount - FiniteCellCount;
}

public sealed record C3DConnectedRegionFillHeightAnalysis(
    bool Success,
    string Message,
    string HeightUnit,
    C3DConnectedRegionFillHeightReferenceSurface? ReferenceSurface,
    IReadOnlyList<C3DConnectedRegionFillHeightMetric> Regions,
    int AcceptedRegionCount,
    int RejectedRegionCount)
{
    public int RegionCount => Regions.Count;
}

/// <summary>
/// Maps an explicit C3D height-field mask to the SDK connected-region tool.
/// Studio owns mask identity and source policy; the SDK owns region labeling.
/// </summary>
public static class C3DConnectedRegionAnalyzer
{
    private static readonly ConnectedRegionTool Tool = new();
    private static readonly ConnectedRegionMetricsTool MetricsTool = new();
    private static readonly ConnectedRegionPresenceTool PresenceTool = new();
    private static readonly ConnectedRegionFillHeightTool FillHeightTool = new();

    public static C3DConnectedRegionAnalysis Analyze(
        int width,
        int height,
        IReadOnlyList<bool>? foreground,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        CancellationToken cancellationToken = default)
    {
        if (foreground is null)
        {
            return Failed("Connected-region foreground mask is required.");
        }

        if (!TryMapConnectivity(connectivity, out var sdkConnectivity))
        {
            return Failed("Connected-region connectivity must be Four or Eight.");
        }

        var result = Tool.Execute(
            new HeightGridMask(height, width, foreground),
            new ConnectedRegionOptions { Connectivity = sdkConnectivity },
            cancellationToken);
        return Map(result);
    }

    public static C3DConnectedRegionAnalysis AnalyzeOutlierMask(
        C3DOutlierCellMap mask,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mask);

        if (!TryBuildOutlierForeground(mask, out var foreground))
        {
            return Failed("Connected-region outlier mask coordinates are invalid.");
        }

        return Analyze(mask.Width, mask.Height, foreground, connectivity, cancellationToken);
    }

    public static C3DConnectedRegionMetricsAnalysis AnalyzeMetrics(
        int width,
        int height,
        IReadOnlyList<bool>? foreground,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionMetricsOptions? metricsOptions = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = metricsOptions ?? new C3DConnectedRegionMetricsOptions();
        if (foreground is null)
        {
            return MetricsFailed(
                "Connected-region metrics foreground mask is required.",
                resolvedOptions);
        }

        if (!TryMapConnectivity(connectivity, out var sdkConnectivity))
        {
            return MetricsFailed(
                "Connected-region connectivity must be Four or Eight.",
                resolvedOptions);
        }

        if (!TryValidateMetricsOptions(resolvedOptions, out var validationMessage))
        {
            return MetricsFailed(validationMessage, resolvedOptions);
        }

        var connectedRegions = Tool.Execute(
            new HeightGridMask(height, width, foreground),
            new ConnectedRegionOptions { Connectivity = sdkConnectivity },
            cancellationToken);
        if (!connectedRegions.Success)
        {
            return MetricsFailed(connectedRegions.Message, resolvedOptions);
        }

        return MapMetrics(
            MetricsTool.Execute(
                connectedRegions,
                new ConnectedRegionMetricsOptions
                {
                    OriginX = resolvedOptions.OriginX,
                    OriginY = resolvedOptions.OriginY,
                    ColumnPitch = resolvedOptions.ColumnPitch,
                    RowPitch = resolvedOptions.RowPitch
                },
                cancellationToken),
            resolvedOptions);
    }

    public static C3DConnectedRegionMetricsAnalysis AnalyzeOutlierMaskMetrics(
        C3DOutlierCellMap mask,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionMetricsOptions? metricsOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var resolvedOptions = metricsOptions ?? new C3DConnectedRegionMetricsOptions();
        if (!TryBuildOutlierForeground(mask, out var foreground))
        {
            return MetricsFailed(
                "Connected-region outlier mask coordinates are invalid.",
                resolvedOptions);
        }

        return AnalyzeMetrics(
            mask.Width,
            mask.Height,
            foreground,
            connectivity,
            resolvedOptions,
            cancellationToken);
    }

    public static C3DConnectedRegionDimensionsAnalysis AnalyzeDimensions(
        int width,
        int height,
        IReadOnlyList<bool>? foreground,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionDimensionsOptions? dimensionsOptions = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = dimensionsOptions ?? new C3DConnectedRegionDimensionsOptions();
        if (foreground is null)
        {
            return DimensionsFailed(
                "Connected-region dimensions foreground mask is required.",
                resolvedOptions);
        }

        if (!TryMapConnectivity(connectivity, out _))
        {
            return DimensionsFailed(
                "Connected-region connectivity must be Four or Eight.",
                resolvedOptions);
        }

        if (!TryValidateDimensionsOptions(resolvedOptions, out var validationMessage))
        {
            return DimensionsFailed(validationMessage, resolvedOptions);
        }

        var metrics = AnalyzeMetrics(
            width,
            height,
            foreground,
            connectivity,
            new C3DConnectedRegionMetricsOptions
            {
                OriginX = resolvedOptions.OriginX,
                OriginY = resolvedOptions.OriginY,
                ColumnPitch = resolvedOptions.ColumnPitch,
                RowPitch = resolvedOptions.RowPitch,
                AreaUnit = resolvedOptions.AreaUnit
            },
            cancellationToken);
        return MapDimensions(metrics, resolvedOptions);
    }

    public static C3DConnectedRegionDimensionsAnalysis AnalyzeOutlierMaskDimensions(
        C3DOutlierCellMap mask,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionDimensionsOptions? dimensionsOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var resolvedOptions = dimensionsOptions ?? new C3DConnectedRegionDimensionsOptions();
        if (!TryBuildOutlierForeground(mask, out var foreground))
        {
            return DimensionsFailed(
                "Connected-region dimensions outlier mask coordinates are invalid.",
                resolvedOptions);
        }

        return AnalyzeDimensions(
            mask.Width,
            mask.Height,
            foreground,
            connectivity,
            resolvedOptions,
            cancellationToken);
    }

    public static C3DConnectedRegionPresenceAnalysis AnalyzePresence(
        int width,
        int height,
        IReadOnlyList<bool>? foreground,
        IReadOnlyList<double>? values,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionPresenceOptions? presenceOptions = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = presenceOptions ?? new C3DConnectedRegionPresenceOptions();
        if (foreground is null)
        {
            return PresenceFailed(
                "Connected-region presence foreground mask is required.",
                resolvedOptions);
        }

        if (values is null)
        {
            return PresenceFailed(
                "Connected-region presence height values are required.",
                resolvedOptions);
        }

        if (!TryMapConnectivity(connectivity, out var sdkConnectivity))
        {
            return PresenceFailed(
                "Connected-region connectivity must be Four or Eight.",
                resolvedOptions);
        }

        if (string.IsNullOrWhiteSpace(resolvedOptions.HeightUnit))
        {
            return PresenceFailed(
                "Connected-region presence height unit is required.",
                resolvedOptions);
        }

        var connectedRegions = Tool.Execute(
            new HeightGridMask(height, width, foreground),
            new ConnectedRegionOptions { Connectivity = sdkConnectivity },
            cancellationToken);
        if (!connectedRegions.Success)
        {
            return PresenceFailed(connectedRegions.Message, resolvedOptions);
        }

        return MapPresence(
            PresenceTool.Execute(
                connectedRegions,
                height,
                width,
                values,
                new ConnectedRegionPresenceOptions
                {
                    MinimumFiniteCoverageRatio = resolvedOptions.MinimumFiniteCoverageRatio,
                    MinimumMeanHeight = resolvedOptions.MinimumMeanHeight,
                    MaximumMeanHeight = resolvedOptions.MaximumMeanHeight
                },
                cancellationToken),
            resolvedOptions);
    }

    public static C3DConnectedRegionPresenceAnalysis AnalyzeOutlierMaskPresence(
        C3DOutlierCellMap mask,
        IReadOnlyList<double>? values,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionPresenceOptions? presenceOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var resolvedOptions = presenceOptions ?? new C3DConnectedRegionPresenceOptions();
        if (!TryBuildOutlierForeground(mask, out var foreground))
        {
            return PresenceFailed(
                "Connected-region presence outlier mask coordinates are invalid.",
                resolvedOptions);
        }

        return AnalyzePresence(
            mask.Width,
            mask.Height,
            foreground,
            values,
            connectivity,
            resolvedOptions,
            cancellationToken);
    }

    public static C3DConnectedRegionAllRegionsAcceptanceAnalysis AnalyzeAllRegionsAcceptance(
        int width,
        int height,
        IReadOnlyList<bool>? foreground,
        IReadOnlyList<double>? values,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionPresenceOptions? presenceOptions = null,
        CancellationToken cancellationToken = default)
    {
        return EvaluateAllRegionsAcceptance(
            AnalyzePresence(
                width,
                height,
                foreground,
                values,
                connectivity,
                presenceOptions,
                cancellationToken));
    }

    public static C3DConnectedRegionAllRegionsAcceptanceAnalysis AnalyzeOutlierMaskAllRegionsAcceptance(
        C3DOutlierCellMap mask,
        IReadOnlyList<double>? values,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionPresenceOptions? presenceOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mask);

        return EvaluateAllRegionsAcceptance(
            AnalyzeOutlierMaskPresence(
                mask,
                values,
                connectivity,
                presenceOptions,
                cancellationToken));
    }

    /// <summary>
    /// Applies the G-15 all-regions acceptance policy to existing G-13 evidence.
    /// The SDK Presence aggregate remains intentionally any-region Present.
    /// </summary>
    public static C3DConnectedRegionAllRegionsAcceptanceAnalysis EvaluateAllRegionsAcceptance(
        C3DConnectedRegionPresenceAnalysis? presence)
    {
        if (presence is null)
        {
            return AllRegionsAcceptanceFailed(
                "Connected-region presence analysis is required.",
                string.Empty);
        }

        if (!presence.Success || presence.Regions is null)
        {
            return AllRegionsAcceptanceFailed(
                presence.Message,
                presence.HeightUnit);
        }

        var regions = presence.Regions;
        var acceptedRegionCount = regions.Count(
            region => region.Decision == C3DConnectedRegionPresenceDecision.Present);
        var rejectedRegionCount = regions.Count - acceptedRegionCount;
        var aggregateDecision = regions.Count > 0 && rejectedRegionCount == 0
            ? C3DConnectedRegionAllRegionsAcceptanceDecision.Accepted
            : C3DConnectedRegionAllRegionsAcceptanceDecision.Rejected;
        var message = regions.Count == 0
            ? "At least one connected region is required for all-regions acceptance."
            : aggregateDecision == C3DConnectedRegionAllRegionsAcceptanceDecision.Accepted
                ? "All connected regions are accepted."
                : "One or more connected regions are rejected.";

        return new(
            true,
            message,
            presence.HeightUnit,
            regions,
            acceptedRegionCount,
            rejectedRegionCount,
            aggregateDecision);
    }

    public static C3DConnectedRegionFillHeightAnalysis AnalyzeFillHeight(
        int width,
        int height,
        IReadOnlyList<bool>? foreground,
        IReadOnlyList<double>? values,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionFillHeightOptions? fillHeightOptions = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedOptions = fillHeightOptions ?? new C3DConnectedRegionFillHeightOptions();
        if (foreground is null)
        {
            return FillHeightFailed(
                "Connected-region fill-height foreground mask is required.",
                resolvedOptions);
        }

        if (values is null)
        {
            return FillHeightFailed(
                "Connected-region fill-height values are required.",
                resolvedOptions);
        }

        if (!TryMapConnectivity(connectivity, out var sdkConnectivity))
        {
            return FillHeightFailed(
                "Connected-region fill-height connectivity must be Four or Eight.",
                resolvedOptions);
        }

        if (!TryValidateFillHeightOptions(resolvedOptions, out var validationMessage))
        {
            return FillHeightFailed(validationMessage, resolvedOptions);
        }

        var connectedRegions = Tool.Execute(
            new HeightGridMask(height, width, foreground),
            new ConnectedRegionOptions { Connectivity = sdkConnectivity },
            cancellationToken);
        if (!connectedRegions.Success)
        {
            return FillHeightFailed(connectedRegions.Message, resolvedOptions);
        }

        return MapFillHeight(
            FillHeightTool.Execute(
                connectedRegions,
                height,
                width,
                values,
                new ConnectedRegionFillHeightOptions
                {
                    ReferenceSurface = new OpenVisionLab.Vision3D.FeatureExtraction.ConnectedRegionFillHeightReferenceSurface
                    {
                        SlopeX = resolvedOptions.ReferenceSurface!.SlopeX,
                        SlopeZ = resolvedOptions.ReferenceSurface.SlopeZ,
                        Intercept = resolvedOptions.ReferenceSurface.Intercept
                    },
                    MinimumFiniteCoverageRatio = resolvedOptions.MinimumFiniteCoverageRatio,
                    MinimumMeanFillHeight = resolvedOptions.MinimumMeanFillHeight,
                    MaximumMeanFillHeight = resolvedOptions.MaximumMeanFillHeight
                },
                cancellationToken),
            resolvedOptions);
    }

    public static C3DConnectedRegionFillHeightAnalysis AnalyzeOutlierMaskFillHeight(
        C3DOutlierCellMap mask,
        IReadOnlyList<double>? values,
        C3DConnectedRegionConnectivity connectivity = C3DConnectedRegionConnectivity.Four,
        C3DConnectedRegionFillHeightOptions? fillHeightOptions = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mask);

        var resolvedOptions = fillHeightOptions ?? new C3DConnectedRegionFillHeightOptions();
        if (!TryBuildOutlierForeground(mask, out var foreground))
        {
            return FillHeightFailed(
                "Connected-region fill-height outlier mask coordinates are invalid.",
                resolvedOptions);
        }

        return AnalyzeFillHeight(
            mask.Width,
            mask.Height,
            foreground,
            values,
            connectivity,
            resolvedOptions,
            cancellationToken);
    }

    private static C3DConnectedRegionAnalysis Map(ConnectedRegionResult result)
    {
        var regions = result.Regions
            .Select(region => new C3DConnectedRegion(
                region.Index,
                region.SeedRow,
                region.SeedColumn,
                Array.AsReadOnly(
                    region.Cells
                        .Select(cell => new C3DConnectedRegionCell(cell.Row, cell.Column))
                        .ToArray()),
                region.MinimumRow,
                region.MinimumColumn,
                region.MaximumRow,
                region.MaximumColumn))
            .ToArray();
        return new C3DConnectedRegionAnalysis(
            result.Success,
            result.Message,
            Array.AsReadOnly(regions),
            result.ForegroundCellCount,
            result.VisitedCellCount);
    }

    private static bool TryMapConnectivity(
        C3DConnectedRegionConnectivity connectivity,
        out ConnectedRegionConnectivity sdkConnectivity)
    {
        switch (connectivity)
        {
            case C3DConnectedRegionConnectivity.Four:
                sdkConnectivity = ConnectedRegionConnectivity.Four;
                return true;
            case C3DConnectedRegionConnectivity.Eight:
                sdkConnectivity = ConnectedRegionConnectivity.Eight;
                return true;
            default:
                sdkConnectivity = default;
                return false;
        }
    }

    private static bool TryBuildOutlierForeground(
        C3DOutlierCellMap mask,
        out bool[] foreground)
    {
        foreground = new bool[mask.CellCount];
        for (var row = 0; row < mask.Height; row++)
        {
            for (var column = 0; column < mask.Width; column++)
            {
                if (!mask.TryIsOutlier(column, row, out var isOutlier))
                {
                    foreground = Array.Empty<bool>();
                    return false;
                }

                foreground[row * mask.Width + column] = isOutlier;
            }
        }

        return true;
    }

    private static bool TryValidateMetricsOptions(
        C3DConnectedRegionMetricsOptions options,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(options.AreaUnit))
        {
            message = "Connected-region area unit is required.";
            return false;
        }

        if (options.SelectedRegionIndex is < 0)
        {
            message = "Connected-region selected index must be non-negative.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryValidateDimensionsOptions(
        C3DConnectedRegionDimensionsOptions options,
        out string message)
    {
        if (string.IsNullOrWhiteSpace(options.DimensionUnit))
        {
            message = "Connected-region dimension unit is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.AreaUnit))
        {
            message = "Connected-region area unit is required.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static C3DConnectedRegionMetricsAnalysis MapMetrics(
        ConnectedRegionMetricsResult result,
        C3DConnectedRegionMetricsOptions options)
    {
        if (!result.Success)
        {
            return MetricsFailed(result.Message, options);
        }

        if (options.SelectedRegionIndex is int selectedIndex
            && !result.Regions.Any(region => region.Index == selectedIndex))
        {
            return MetricsFailed(
                $"Connected-region selected index {selectedIndex} does not exist.",
                options);
        }

        var regions = result.Regions
            .Select(region => new C3DConnectedRegionMetric(
                region.Index,
                region.CellCount,
                region.Area,
                region.CenterX,
                region.CenterY,
                region.HasOrientation,
                region.OrientationDegrees,
                MapBounding(region.Bounding)))
            .ToArray();
        var overlays = regions
            .Select(region => new C3DConnectedRegionOverlay(
                region.Index,
                options.SelectedRegionIndex == region.Index,
                region.Bounding))
            .ToArray();
        return new C3DConnectedRegionMetricsAnalysis(
            true,
            result.Message,
            options.AreaUnit!,
            Array.AsReadOnly(regions),
            Array.AsReadOnly(overlays),
            options.SelectedRegionIndex,
            result.TotalArea);
    }

    private static C3DConnectedRegionPresenceAnalysis MapPresence(
        ConnectedRegionPresenceResult result,
        C3DConnectedRegionPresenceOptions options)
    {
        if (!result.Success)
        {
            return PresenceFailed(result.Message, options);
        }

        var regions = result.Regions
            .Select(region => new C3DConnectedRegionPresenceMetric(
                region.Index,
                region.TotalCellCount,
                region.FiniteCellCount,
                region.FiniteCoverageRatio,
                region.MeanHeight,
                MapCoverageDisposition(region.CoverageDisposition),
                MapHeightDisposition(region.HeightDisposition),
                MapPresenceDecision(region.Decision)))
            .ToArray();
        return new C3DConnectedRegionPresenceAnalysis(
            true,
            result.Message,
            options.HeightUnit!,
            Array.AsReadOnly(regions),
            result.PresentRegionCount,
            result.MissingRegionCount,
            MapPresenceDecision(result.AggregateDecision));
    }

    private static C3DConnectedRegionDimensionsAnalysis MapDimensions(
        C3DConnectedRegionMetricsAnalysis result,
        C3DConnectedRegionDimensionsOptions options)
    {
        if (!result.Success)
        {
            return DimensionsFailed(result.Message, options);
        }

        var regions = result.Regions
            .Select(region => new C3DConnectedRegionDimensionMetric(
                region.Index,
                region.CellCount,
                region.Bounding.Width,
                region.Bounding.Height,
                region.Area))
            .ToArray();
        return new C3DConnectedRegionDimensionsAnalysis(
            true,
            result.Message,
            options.DimensionUnit!,
            options.AreaUnit!,
            Array.AsReadOnly(regions),
            result.TotalArea);
    }

    private static C3DConnectedRegionFillHeightAnalysis MapFillHeight(
        ConnectedRegionFillHeightResult result,
        C3DConnectedRegionFillHeightOptions options)
    {
        if (!result.Success)
        {
            return FillHeightFailed(result.Message, options);
        }

        var regions = result.Regions
            .Select(region => new C3DConnectedRegionFillHeightMetric(
                region.Index,
                region.TotalCellCount,
                region.FiniteCellCount,
                region.FiniteCoverageRatio,
                region.MeanFillHeight,
                region.MinimumFillHeight,
                region.MaximumFillHeight,
                MapFillHeightCoverageDisposition(region.CoverageDisposition),
                MapFillHeightDisposition(region.FillHeightDisposition),
                MapFillHeightDecision(region.Decision)))
            .ToArray();
        return new C3DConnectedRegionFillHeightAnalysis(
            true,
            result.Message,
            options.HeightUnit!,
            options.ReferenceSurface,
            Array.AsReadOnly(regions),
            result.AcceptedRegionCount,
            result.RejectedRegionCount);
    }

    private static C3DConnectedRegionFillHeightCoverageDisposition
        MapFillHeightCoverageDisposition(
            ConnectedRegionFillHeightCoverageDisposition disposition)
    {
        switch (disposition)
        {
            case ConnectedRegionFillHeightCoverageDisposition.Accepted:
                return C3DConnectedRegionFillHeightCoverageDisposition.Accepted;
            case ConnectedRegionFillHeightCoverageDisposition.BelowMinimum:
                return C3DConnectedRegionFillHeightCoverageDisposition.BelowMinimum;
            default:
                return C3DConnectedRegionFillHeightCoverageDisposition.NotEvaluated;
        }
    }

    private static C3DConnectedRegionFillHeightDisposition
        MapFillHeightDisposition(
            ConnectedRegionFillHeightDisposition disposition)
    {
        switch (disposition)
        {
            case ConnectedRegionFillHeightDisposition.Accepted:
                return C3DConnectedRegionFillHeightDisposition.Accepted;
            case ConnectedRegionFillHeightDisposition.Missing:
                return C3DConnectedRegionFillHeightDisposition.Missing;
            case ConnectedRegionFillHeightDisposition.BelowMinimum:
                return C3DConnectedRegionFillHeightDisposition.BelowMinimum;
            case ConnectedRegionFillHeightDisposition.AboveMaximum:
                return C3DConnectedRegionFillHeightDisposition.AboveMaximum;
            default:
                return C3DConnectedRegionFillHeightDisposition.NotEvaluated;
        }
    }

    private static C3DConnectedRegionFillHeightDecision MapFillHeightDecision(
        ConnectedRegionFillHeightDecision decision)
    {
        switch (decision)
        {
            case ConnectedRegionFillHeightDecision.Accepted:
                return C3DConnectedRegionFillHeightDecision.Accepted;
            case ConnectedRegionFillHeightDecision.Rejected:
                return C3DConnectedRegionFillHeightDecision.Rejected;
            default:
                return C3DConnectedRegionFillHeightDecision.NotEvaluated;
        }
    }

    private static C3DConnectedRegionPresenceCoverageDisposition
        MapCoverageDisposition(
            ConnectedRegionPresenceCoverageDisposition disposition)
    {
        switch (disposition)
        {
            case ConnectedRegionPresenceCoverageDisposition.Accepted:
                return C3DConnectedRegionPresenceCoverageDisposition.Accepted;
            case ConnectedRegionPresenceCoverageDisposition.BelowMinimum:
                return C3DConnectedRegionPresenceCoverageDisposition.BelowMinimum;
            default:
                return C3DConnectedRegionPresenceCoverageDisposition.NotEvaluated;
        }
    }

    private static C3DConnectedRegionPresenceHeightDisposition
        MapHeightDisposition(
            ConnectedRegionPresenceHeightDisposition disposition)
    {
        switch (disposition)
        {
            case ConnectedRegionPresenceHeightDisposition.Accepted:
                return C3DConnectedRegionPresenceHeightDisposition.Accepted;
            case ConnectedRegionPresenceHeightDisposition.Missing:
                return C3DConnectedRegionPresenceHeightDisposition.Missing;
            case ConnectedRegionPresenceHeightDisposition.BelowMinimum:
                return C3DConnectedRegionPresenceHeightDisposition.BelowMinimum;
            case ConnectedRegionPresenceHeightDisposition.AboveMaximum:
                return C3DConnectedRegionPresenceHeightDisposition.AboveMaximum;
            default:
                return C3DConnectedRegionPresenceHeightDisposition.NotEvaluated;
        }
    }

    private static C3DConnectedRegionPresenceDecision MapPresenceDecision(
        ConnectedRegionPresenceDecision decision)
    {
        switch (decision)
        {
            case ConnectedRegionPresenceDecision.Present:
                return C3DConnectedRegionPresenceDecision.Present;
            case ConnectedRegionPresenceDecision.Missing:
                return C3DConnectedRegionPresenceDecision.Missing;
            default:
                return C3DConnectedRegionPresenceDecision.NotEvaluated;
        }
    }

    private static bool TryValidateFillHeightOptions(
        C3DConnectedRegionFillHeightOptions options,
        out string message)
    {
        if (options.ReferenceSurface is null
            || !double.IsFinite(options.ReferenceSurface.SlopeX)
            || !double.IsFinite(options.ReferenceSurface.SlopeZ)
            || !double.IsFinite(options.ReferenceSurface.Intercept))
        {
            message = "Connected-region fill-height reference surface coefficients are required and must be finite.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.HeightUnit))
        {
            message = "Connected-region fill-height height unit is required.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static C3DConnectedRegionBoundingArtifact MapBounding(
        ConnectedRegionBoundingArtifact bounding) =>
        new(
            bounding.MinimumRow,
            bounding.MinimumColumn,
            bounding.MaximumRow,
            bounding.MaximumColumn,
            bounding.MinimumX,
            bounding.MinimumY,
            bounding.MaximumX,
            bounding.MaximumY);

    private static C3DConnectedRegionAnalysis Failed(string message) =>
        new(
            false,
            message,
            Array.Empty<C3DConnectedRegion>(),
            0,
            0);

    private static C3DConnectedRegionMetricsAnalysis MetricsFailed(
        string message,
        C3DConnectedRegionMetricsOptions options) =>
        new(
            false,
            message,
            options.AreaUnit ?? string.Empty,
            Array.Empty<C3DConnectedRegionMetric>(),
            Array.Empty<C3DConnectedRegionOverlay>(),
            options.SelectedRegionIndex,
            0.0);

    private static C3DConnectedRegionDimensionsAnalysis DimensionsFailed(
        string message,
        C3DConnectedRegionDimensionsOptions options) =>
        new(
            false,
            message,
            options.DimensionUnit ?? string.Empty,
            options.AreaUnit ?? string.Empty,
            Array.Empty<C3DConnectedRegionDimensionMetric>(),
            0.0);

    private static C3DConnectedRegionPresenceAnalysis PresenceFailed(
        string message,
        C3DConnectedRegionPresenceOptions options) =>
        new(
            false,
            message,
            options.HeightUnit ?? string.Empty,
            Array.Empty<C3DConnectedRegionPresenceMetric>(),
            0,
            0,
            C3DConnectedRegionPresenceDecision.NotEvaluated);

    private static C3DConnectedRegionAllRegionsAcceptanceAnalysis AllRegionsAcceptanceFailed(
        string message,
        string heightUnit) =>
        new(
            false,
            message,
            heightUnit,
            Array.Empty<C3DConnectedRegionPresenceMetric>(),
            0,
            0,
            C3DConnectedRegionAllRegionsAcceptanceDecision.NotEvaluated);

    private static C3DConnectedRegionFillHeightAnalysis FillHeightFailed(
        string message,
        C3DConnectedRegionFillHeightOptions options) =>
        new(
            false,
            message,
            options.HeightUnit ?? string.Empty,
            options.ReferenceSurface,
            Array.Empty<C3DConnectedRegionFillHeightMetric>(),
            0,
            0);
}

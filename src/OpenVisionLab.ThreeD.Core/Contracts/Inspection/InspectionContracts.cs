namespace OpenVisionLab.ThreeD.Core;

public enum EntityKind
{
    Mesh,
    PointCloud,
    HeightGrid,
    Overlay
}

public enum LayerKind
{
    Source,
    Preview,
    Result
}

public enum ResultStatus
{
    NotRun,
    Pass,
    Fail,
    Warning,
    Error
}

public enum MetricKind
{
    Number,
    Length,
    Angle,
    Area,
    Volume,
    Count,
    Deviation
}

public enum OverlayKind
{
    Point,
    Polyline,
    Box,
    Plane,
    ColorMap,
    Marker
}

public sealed record SourceEntity(
    string Id,
    string Name,
    EntityKind Kind,
    string Unit,
    string? SourcePath,
    ModelTransform Transform);

public sealed record ResultEntity(
    string Id,
    string Name,
    string SourceEntityId,
    ResultStatus Status,
    string Message,
    IReadOnlyList<Metric> Metrics,
    IReadOnlyList<Overlay> Overlays);

public sealed record EntityLayer(
    string Id,
    string Name,
    LayerKind Kind,
    bool IsVisible,
    IReadOnlyList<string> EntityIds);

public sealed record InspectionStep(
    string Id,
    string ToolName,
    string SourceEntityId,
    string ReferenceId,
    bool IsEnabled);

public sealed record ToolResult(
    string ToolName,
    ResultStatus Status,
    string Message,
    TimeSpan Elapsed,
    IReadOnlyList<Metric> Metrics,
    IReadOnlyList<Overlay> Overlays);

public sealed record Metric(
    string Name,
    MetricKind Kind,
    double Value,
    string Unit,
    ResultStatus? Status = null);

public sealed record Overlay(
    string Id,
    OverlayKind Kind,
    string Label,
    ResultStatus? Status = null,
    string? SourceEntityId = null);

public readonly record struct ModelTransform(
    double TranslateX,
    double TranslateY,
    double TranslateZ,
    double RotateXDegrees,
    double RotateYDegrees,
    double RotateZDegrees,
    double Scale)
{
    public static ModelTransform Identity { get; } = new(0, 0, 0, 0, 0, 0, 1);
}

using System.Globalization;

namespace OpenVisionLab.ThreeD.Core;

public static class InspectionContractText
{
    public const string Missing = "(none)";
    public const string GeneratedSource = "(generated)";
    public const string ToolResultPrefix = "ToolResult";
    public const string PreviewToolResultMarker = "PreviewToolResult";
    public const string PreviewMetricsMarker = "PreviewMetrics";
    public const string PreviewOverlaysMarker = "PreviewOverlays";
    public const string MetricsMarker = "Metrics";
    public const string OverlaysMarker = "Overlays";

    public static string FormatSourceEntity(SourceEntity entity) =>
        $"{entity.Id}|{entity.Kind}|unit={Clean(entity.Unit)}|source={Clean(entity.SourcePath ?? GeneratedSource)}";

    public static string FormatEntityLayer(EntityLayer layer) =>
        $"{layer.Id}|{layer.Kind}|visible={layer.IsVisible}|entities={string.Join(",", layer.EntityIds)}";

    public static string FormatToolResult(ToolResult result, bool includePrefix = false)
    {
        var prefix = includePrefix ? ToolResultPrefix + "|" : string.Empty;
        return $"{prefix}{Clean(result.ToolName)}|{result.Status}|elapsedMs={FormatNumber(result.Elapsed.TotalMilliseconds)}|metrics={result.Metrics.Count}|overlays={result.Overlays.Count}|message={Clean(result.Message)}";
    }

    public static string FormatMetric(Metric metric, string? prefix = null)
    {
        var leading = string.IsNullOrWhiteSpace(prefix) ? string.Empty : Clean(prefix) + "|";
        return $"{leading}{Clean(metric.Name)}|{metric.Kind}|value={FormatNumber(metric.Value)}|unit={Clean(metric.Unit)}|status={metric.Status?.ToString() ?? Missing}";
    }

    public static string FormatOverlay(Overlay overlay, string? prefix = null)
    {
        var leading = string.IsNullOrWhiteSpace(prefix) ? string.Empty : Clean(prefix) + "|";
        return $"{leading}{overlay.Id}|{overlay.Kind}|label={Clean(overlay.Label)}|status={overlay.Status?.ToString() ?? Missing}|source={Clean(overlay.SourceEntityId ?? Missing)}";
    }

    public static string FormatResultEntity(ResultEntity entity) =>
        $"{entity.Id}|source={Clean(entity.SourceEntityId)}|status={entity.Status}|metrics={entity.Metrics.Count}|overlays={entity.Overlays.Count}|message={Clean(entity.Message)}";

    public static string FormatNumber(double value) =>
        double.IsFinite(value) ? value.ToString("F3", CultureInfo.InvariantCulture) : "(pending)";

    public static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}

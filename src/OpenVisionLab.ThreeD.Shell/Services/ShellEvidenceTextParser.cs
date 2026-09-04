using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.Services;

/// <summary>
/// Owns the WPF-neutral parsing and formatting of Shell evidence text.
/// The ViewModel keeps bindable projections and View-only screenshot state;
/// this owner keeps marker vocabulary, metric selection, and path previews
/// deterministic and directly testable.
/// </summary>
internal static class ShellEvidenceTextParser
{
    public static ToolComparisonEvidence ExtractUiEvidence(string[] lines)
    {
        var nominalActual = ExtractNominalActualUiEvidence(lines);
        if (nominalActual is not null)
        {
            return nominalActual;
        }

        var resultLine = FindLineAfterMarker(lines, InspectionContractText.PreviewToolResultMarker);
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.PreviewMetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 0 } ? parts[0] : "(missing)",
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    public static ToolComparisonEvidence ExtractRunnerEvidence(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line => line.StartsWith(InspectionContractText.ToolResultPrefix + "|", StringComparison.Ordinal));
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.MetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            parts is { Length: > 2 } ? parts[2] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    public static string ExtractRecipePath(string root, string[] reportLines)
    {
        var recipeLine = reportLines.FirstOrDefault(line => line.StartsWith("Recipe|", StringComparison.Ordinal));
        var path = recipeLine is null ? null : ExtractTaggedValue(recipeLine.Split('|'), "path=");
        return string.IsNullOrWhiteSpace(path)
            ? Path.Combine(root, "recipes", "c3d-height-deviation.recipe.json")
            : ResolvePath(root, path, path);
    }

    public static string ExtractSourceLoadStatus(string[] reportLines) =>
        reportLines.Any(line =>
            line.StartsWith("Source|", StringComparison.Ordinal)
            || line.StartsWith("NominalActualActualSource|", StringComparison.Ordinal))
            ? "Loaded"
            : "Pending";

    public static string ExtractSourceSummary(string root, string[] reportLines, string[] contractLines)
    {
        var actual = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualActualSource|", StringComparison.Ordinal));
        var nominal = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualNominalSource|", StringComparison.Ordinal));
        var query = reportLines.FirstOrDefault(line =>
            line.StartsWith("NominalActualQuerySource|", StringComparison.Ordinal));
        if (actual is not null && nominal is not null && query is not null)
        {
            return string.Join(
                " | ",
                FormatNominalActualSource(root, "actual", actual),
                FormatNominalActualSource(root, "nominal", nominal),
                FormatNominalActualSource(root, "query", query));
        }

        var sourceLine = reportLines.FirstOrDefault(line => line.StartsWith("Source|", StringComparison.Ordinal));
        if (sourceLine is not null)
        {
            var parts = sourceLine.Split('|');
            var name = ExtractTaggedValue(parts, "name=") ?? (parts.Length > 1 ? parts[1] : "source");
            var unit = ExtractTaggedValue(parts, "unit=") ?? "(unit unknown)";
            var path = ExtractTaggedValue(parts, "path=");
            var shortPath = string.IsNullOrWhiteSpace(path) ? "(path unknown)" : ShortenWorkspacePaths(root, path);
            return $"{name} | unit {unit} | {shortPath}";
        }

        var sourceEntitiesIndex = Array.FindIndex(contractLines, line => line.Equals("SourceEntities", StringComparison.Ordinal));
        if (sourceEntitiesIndex >= 0 && sourceEntitiesIndex + 1 < contractLines.Length)
        {
            return ShortenWorkspacePaths(root, contractLines[sourceEntitiesIndex + 1]);
        }

        return "No source evidence found.";
    }

    public static string SelectEvidenceStatus(ToolComparisonEvidence uiEvidence, ToolComparisonEvidence runnerEvidence) =>
        runnerEvidence.Status != "(missing)" ? runnerEvidence.Status : uiEvidence.Status;

    public static string SelectEvidenceMetric(ToolComparisonEvidence uiEvidence, ToolComparisonEvidence runnerEvidence) =>
        runnerEvidence.KeyMetricSummary != "(missing)" ? runnerEvidence.KeyMetricSummary : uiEvidence.KeyMetricSummary;

    public static string? ExtractTaggedValue(string[] parts, string prefix) =>
        parts.FirstOrDefault(part => part.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    public static string FormatEvidencePath(string root, string path)
    {
        var displayPath = Path.GetRelativePath(root, path);
        if (!File.Exists(path))
        {
            return $"missing: {displayPath}";
        }

        var timestamp = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return $"{displayPath}\n  modified: {timestamp}";
    }

    public static string FormatShortEvidencePath(string root, string path) =>
        File.Exists(path) ? Path.GetRelativePath(root, path) : $"missing: {Path.GetRelativePath(root, path)}";

    public static string FormatOptionalArtifact(string root, string? path) =>
        string.IsNullOrWhiteSpace(path) ? "(not requested)" : FormatShortEvidencePath(root, path);

    public static string FormatScreenshotTarget(string root, string? screenshotPath)
    {
        if (string.IsNullOrWhiteSpace(screenshotPath))
        {
            return "(not requested)";
        }

        var path = Path.IsPathRooted(screenshotPath)
            ? screenshotPath
            : Path.Combine(root, screenshotPath);
        return Path.GetRelativePath(root, path);
    }

    public static string FormatRunTime(string reportPath, string contractPath)
    {
        var evidencePath = File.Exists(reportPath) ? reportPath : contractPath;
        if (!File.Exists(evidencePath))
        {
            return "(pending)";
        }

        return File.GetLastWriteTime(evidencePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    public static string PreviewLines(string root, string title, string path, string[] lines)
    {
        var displayPath = FormatShortEvidencePath(root, path);
        if (lines.Length == 0)
        {
            return $"{title}: missing or empty: {displayPath}";
        }

        return $"{title}: {displayPath}\n{string.Join(Environment.NewLine, lines.Take(18).Select(line => ShortenWorkspacePaths(root, line)))}";
    }

    public static string ShortenWorkspacePaths(string root, string text)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return text
            .Replace(normalizedRoot + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(normalizedRoot + Path.AltDirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static ToolComparisonEvidence? ExtractNominalActualUiEvidence(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line =>
            line.StartsWith("NominalActualResult|", StringComparison.Ordinal));
        var statisticsLine = lines.FirstOrDefault(line =>
            line.StartsWith("NominalActualSignedStatistics|", StringComparison.Ordinal));
        if (resultLine is null || statisticsLine is null)
        {
            return null;
        }

        var resultParts = resultLine.Split('|');
        if (!string.Equals(
                ExtractTaggedValue(resultParts, "available="),
                "True",
                StringComparison.OrdinalIgnoreCase)
            || !long.TryParse(
                ExtractTaggedValue(resultParts, "below="),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var below)
            || !long.TryParse(
                ExtractTaggedValue(resultParts, "above="),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var above))
        {
            return null;
        }

        var statisticsParts = statisticsLine.Split('|');
        if (!double.TryParse(
                ExtractTaggedValue(statisticsParts, "mean="),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var signedMean))
        {
            return null;
        }

        var unit = ExtractTaggedValue(statisticsParts, "unit=") ?? "(unitless)";
        return new ToolComparisonEvidence(
            NominalActualComparisonContract.ToolName,
            ExtractTaggedValue(resultParts, "status=") ?? "(missing)",
            string.Create(
                CultureInfo.InvariantCulture,
                $"Signed mean deviation {signedMean:F3} {unit} | Out-of-tolerance point count {below + above:F3} count"));
    }

    private static string FormatNominalActualSource(string root, string role, string line)
    {
        var parts = line.Split('|');
        var id = ExtractTaggedValue(parts, "id=") ?? "(missing ID)";
        var path = ExtractTaggedValue(parts, "path=");
        return $"{role} {id} ({(string.IsNullOrWhiteSpace(path) ? "path unknown" : ShortenWorkspacePaths(root, path))})";
    }

    private static string? FindLineAfterMarker(string[] lines, string marker)
    {
        var index = Array.FindIndex(lines, line => line.Equals(marker, StringComparison.Ordinal));
        if (index < 0)
        {
            return null;
        }

        for (var i = index + 1; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                return lines[i];
            }
        }

        return null;
    }

    private static List<MetricEvidence> ExtractMetrics(string[] lines, string marker)
    {
        var index = Array.FindIndex(lines, line => line.Equals(marker, StringComparison.Ordinal));
        if (index < 0)
        {
            return [];
        }

        var metrics = new List<MetricEvidence>();
        for (var i = index + 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split('|');
            var value = ExtractTaggedValue(parts, "value=");
            if (parts.Length < 3 || value is null)
            {
                break;
            }

            metrics.Add(new MetricEvidence(
                parts[0],
                value,
                ExtractTaggedValue(parts, "unit=") ?? "(unitless)",
                ExtractTaggedValue(parts, "status=") ?? "(none)"));
        }

        return metrics;
    }

    private static string FormatKeyMetricSummary(IReadOnlyList<MetricEvidence> metrics)
    {
        var signedMean = metrics.FirstOrDefault(metric =>
            metric.Name.Equals("Signed mean deviation", StringComparison.Ordinal));
        var outOfTolerance = metrics.FirstOrDefault(metric =>
            metric.Name.Equals("Out-of-tolerance point count", StringComparison.Ordinal));
        if (signedMean is not null && outOfTolerance is not null)
        {
            return $"Signed mean deviation {FormatMetricValue(signedMean)} | Out-of-tolerance point count {FormatMetricValue(outOfTolerance)}";
        }

        var peakDeviation = metrics.FirstOrDefault(metric => metric.Name.Equals("Peak absolute deviation", StringComparison.Ordinal));
        if (peakDeviation is not null)
        {
            return $"Peak {FormatMetricValue(peakDeviation)}";
        }

        var distance = metrics.FirstOrDefault(metric => metric.Name.Equals("Distance", StringComparison.Ordinal));
        var heightDelta = metrics.FirstOrDefault(metric => metric.Name.Equals("Source Z height delta", StringComparison.Ordinal));
        if (distance is not null && heightDelta is not null)
        {
            return $"Distance {FormatMetricValue(distance)} | Height {FormatMetricValue(heightDelta)}";
        }

        var fallback = metrics.FirstOrDefault();
        return fallback is null ? "(missing)" : $"{fallback.Name} {FormatMetricValue(fallback)}";
    }

    private static string FormatMetricValue(MetricEvidence metric) =>
        string.IsNullOrWhiteSpace(metric.Unit) || metric.Unit.Equals("(unitless)", StringComparison.Ordinal)
            ? metric.Value
            : $"{metric.Value} {metric.Unit}";

    private static string ResolvePath(string root, string requestedPath, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return fallbackPath;
        }

        return Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.Combine(root, requestedPath);
    }
}

internal sealed record ToolComparisonEvidence(string ToolName, string Status, string KeyMetricSummary)
{
    public bool Matches(ToolComparisonEvidence other) =>
        !Status.Equals("(missing)", StringComparison.Ordinal)
        && ToolName.Equals(other.ToolName, StringComparison.Ordinal)
        && Status.Equals(other.Status, StringComparison.Ordinal)
        && KeyMetricSummary.Equals(other.KeyMetricSummary, StringComparison.Ordinal);
}

internal sealed record MetricEvidence(string Name, string Value, string Unit, string Status);

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell;

public sealed class ShellMainWindowViewModel : INotifyPropertyChanged
{
    private readonly string? comparisonContractPath;
    private readonly string? comparisonReportPath;
    private string statusText = "Viewer hosted";
    private string recipeComparisonSummary = "No recipe comparison evidence loaded.";
    private string recipeComparisonHistory = "(pending)";
    private string recipeComparisonDetails = "(pending)";
    private int selectedEvidenceTabIndex;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ShellMainWindowViewModel(string? comparisonContractPath = null, string? comparisonReportPath = null)
    {
        this.comparisonContractPath = comparisonContractPath;
        this.comparisonReportPath = comparisonReportPath;
        RefreshRecipeComparison();
    }

    public string StatusText
    {
        get => statusText;
        private set => SetField(ref statusText, value);
    }

    public string RecipeComparisonSummary
    {
        get => recipeComparisonSummary;
        private set => SetField(ref recipeComparisonSummary, value);
    }

    public string RecipeComparisonHistory
    {
        get => recipeComparisonHistory;
        private set => SetField(ref recipeComparisonHistory, value);
    }

    public string RecipeComparisonDetails
    {
        get => recipeComparisonDetails;
        private set => SetField(ref recipeComparisonDetails, value);
    }

    public int SelectedEvidenceTabIndex
    {
        get => selectedEvidenceTabIndex;
        set => SetField(ref selectedEvidenceTabIndex, Math.Clamp(value, 0, 2));
    }

    public ObservableCollection<RecipeRunHistoryItem> RecipeRunHistory { get; } = [];

    public void SetViewerSmokeFailed(string viewerStatus)
    {
        StatusText = "Viewer hosted | viewer smoke failed";
        RecipeComparisonSummary = string.IsNullOrWhiteSpace(viewerStatus)
            ? "Viewer smoke failed before recipe comparison."
            : $"Viewer smoke failed before recipe comparison.\n{viewerStatus}";
        RecipeComparisonHistory = "No recipe comparison was run for this failed viewer smoke.";
        RecipeComparisonDetails = "See Tool / Inspector and Viewer contract output for the loader failure details.";
        RecipeRunHistory.Clear();
        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            "ViewerFailed",
            "No recipe metric",
            "Blocked",
            "(viewer smoke failure)"));
    }

    public void RefreshRecipeComparison()
    {
        var root = ResolveWorkspaceRoot();
        var contractPath = ResolvePath(root, comparisonContractPath, Path.Combine(root, "artifacts", "shell_recipe_ui_after.txt"));
        var reportPath = ResolvePath(root, comparisonReportPath, Path.Combine(root, "artifacts", "runner_shell_recipe_ui_compare_after.txt"));

        var contractLines = ReadLinesOrEmpty(contractPath);
        var reportLines = ReadLinesOrEmpty(reportPath);
        var recipePath = ExtractRecipePath(root, reportLines);
        var uiEvidence = ExtractUiEvidence(contractLines);
        var runnerEvidence = ExtractRunnerEvidence(reportLines);
        var comparisonState = uiEvidence.Matches(runnerEvidence)
            ? "Runner/UI contract matched"
            : "Comparison evidence missing or different";

        RecipeComparisonSummary =
            $"{comparisonState}\nUI: {uiEvidence.ToolName} / {uiEvidence.Status}\nRunner: {runnerEvidence.ToolName} / {runnerEvidence.Status}\nUI metric: {uiEvidence.KeyMetricSummary}\nRunner metric: {runnerEvidence.KeyMetricSummary}";
        RecipeComparisonHistory =
            $"Recipe: {FormatEvidencePath(root, recipePath)}\nUI contract: {FormatEvidencePath(root, contractPath)}\nRunner report: {FormatEvidencePath(root, reportPath)}";
        RecipeComparisonDetails =
            $"{PreviewLines(root, "Runner report", reportPath, reportLines)}\n\n{PreviewLines(root, "UI contract", contractPath, contractLines)}";
        RefreshRunHistory(root, contractPath, reportPath, uiEvidence, runnerEvidence, comparisonState);
        StatusText = comparisonState == "Runner/UI contract matched"
            ? "Viewer hosted | recipe comparison matched"
            : "Viewer hosted | recipe comparison pending";
    }

    private void RefreshRunHistory(
        string root,
        string contractPath,
        string reportPath,
        ToolComparisonEvidence uiEvidence,
        ToolComparisonEvidence runnerEvidence,
        string comparisonState)
    {
        RecipeRunHistory.Clear();

        var status = runnerEvidence.Status != "(missing)" ? runnerEvidence.Status : uiEvidence.Status;
        var keyMetricSummary = runnerEvidence.KeyMetricSummary != "(missing)" ? runnerEvidence.KeyMetricSummary : uiEvidence.KeyMetricSummary;
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            FormatRunTime(reportPath, contractPath),
            status,
            keyMetricSummary,
            evidenceState,
            FormatShortEvidencePath(root, reportPath)));
    }

    private static string ResolveWorkspaceRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OpenVisionLab.ThreeDStudio.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string ResolvePath(string root, string? requestedPath, string fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(requestedPath))
        {
            return fallbackPath;
        }

        return Path.IsPathRooted(requestedPath)
            ? requestedPath
            : Path.Combine(root, requestedPath);
    }

    private static string[] ReadLinesOrEmpty(string path) =>
        File.Exists(path) ? File.ReadAllLines(path) : [];

    private static ToolComparisonEvidence ExtractUiEvidence(string[] lines)
    {
        var resultLine = FindLineAfterMarker(lines, InspectionContractText.PreviewToolResultMarker);
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.PreviewMetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 0 } ? parts[0] : "(missing)",
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    private static ToolComparisonEvidence ExtractRunnerEvidence(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line => line.StartsWith(InspectionContractText.ToolResultPrefix + "|", StringComparison.Ordinal));
        var parts = resultLine?.Split('|');
        var metrics = ExtractMetrics(lines, InspectionContractText.MetricsMarker);
        return new ToolComparisonEvidence(
            parts is { Length: > 1 } ? parts[1] : "(missing)",
            parts is { Length: > 2 } ? parts[2] : "(missing)",
            FormatKeyMetricSummary(metrics));
    }

    private static string ExtractRecipePath(string root, string[] reportLines)
    {
        var recipeLine = reportLines.FirstOrDefault(line => line.StartsWith("Recipe|", StringComparison.Ordinal));
        var path = recipeLine is null ? null : ExtractTaggedValue(recipeLine.Split('|'), "path=");
        return string.IsNullOrWhiteSpace(path)
            ? Path.Combine(root, "recipes", "c3d-height-deviation.recipe.json")
            : ResolvePath(root, path, path);
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

    private static string? ExtractTaggedValue(string[] parts, string prefix) =>
        parts.FirstOrDefault(part => part.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];

    private static string FormatKeyMetricSummary(IReadOnlyList<MetricEvidence> metrics)
    {
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

    private static string FormatEvidencePath(string root, string path)
    {
        var displayPath = Path.GetRelativePath(root, path);
        if (!File.Exists(path))
        {
            return $"missing: {displayPath}";
        }

        var timestamp = File.GetLastWriteTime(path).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        return $"{displayPath}\n  modified: {timestamp}";
    }

    private static string FormatShortEvidencePath(string root, string path) =>
        File.Exists(path) ? Path.GetRelativePath(root, path) : $"missing: {Path.GetRelativePath(root, path)}";

    private static string FormatRunTime(string reportPath, string contractPath)
    {
        var evidencePath = File.Exists(reportPath) ? reportPath : contractPath;
        if (!File.Exists(evidencePath))
        {
            return "(pending)";
        }

        return File.GetLastWriteTime(evidencePath).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    private static string PreviewLines(string root, string title, string path, string[] lines)
    {
        var displayPath = FormatShortEvidencePath(root, path);
        if (lines.Length == 0)
        {
            return $"{title}: missing or empty: {displayPath}";
        }

        return $"{title}: {displayPath}\n{string.Join(Environment.NewLine, lines.Take(18).Select(line => ShortenWorkspacePaths(root, line)))}";
    }

    private static string ShortenWorkspacePaths(string root, string text)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return text
            .Replace(normalizedRoot + Path.DirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(normalizedRoot + Path.AltDirectorySeparatorChar, string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class RecipeRunHistoryItem
{
    public RecipeRunHistoryItem(
        string runTime,
        string status,
        string keyMetricSummary,
        string evidenceState,
        string reportPath)
    {
        RunTime = runTime;
        Status = status;
        KeyMetricSummary = keyMetricSummary;
        EvidenceState = evidenceState;
        ReportPath = reportPath;
    }

    public string RunTime { get; }

    public string Status { get; }

    public string KeyMetricSummary { get; }

    public string EvidenceState { get; }

    public string ReportPath { get; }
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

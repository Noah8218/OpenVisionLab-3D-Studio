using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;

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

    public void RefreshRecipeComparison()
    {
        var root = ResolveWorkspaceRoot();
        var recipePath = Path.Combine(root, "recipes", "c3d-height-deviation.recipe.json");
        var contractPath = ResolvePath(root, comparisonContractPath, Path.Combine(root, "artifacts", "shell_recipe_ui_after.txt"));
        var reportPath = ResolvePath(root, comparisonReportPath, Path.Combine(root, "artifacts", "runner_shell_recipe_ui_compare_after.txt"));

        var contractLines = ReadLinesOrEmpty(contractPath);
        var reportLines = ReadLinesOrEmpty(reportPath);
        var uiStatus = ExtractUiStatus(contractLines);
        var runnerStatus = ExtractRunnerStatus(reportLines);
        var uiPeak = ExtractMetricValue(contractLines, "Peak absolute deviation");
        var runnerPeak = ExtractMetricValue(reportLines, "Peak absolute deviation");
        var comparisonState = uiStatus == runnerStatus && uiPeak == runnerPeak && uiStatus != "(missing)"
            ? "Runner/UI contract matched"
            : "Comparison evidence missing or different";

        RecipeComparisonSummary =
            $"{comparisonState}\nUI status: {uiStatus} | Runner status: {runnerStatus}\nUI peak: {uiPeak} | Runner peak: {runnerPeak}";
        RecipeComparisonHistory =
            $"Recipe: {FormatEvidencePath(root, recipePath)}\nUI contract: {FormatEvidencePath(root, contractPath)}\nRunner report: {FormatEvidencePath(root, reportPath)}";
        RecipeComparisonDetails =
            $"{PreviewLines("Runner report", reportPath, reportLines)}\n\n{PreviewLines("UI contract", contractPath, contractLines)}";
        RefreshRunHistory(root, contractPath, reportPath, uiStatus, runnerStatus, uiPeak, runnerPeak, comparisonState);
        StatusText = comparisonState == "Runner/UI contract matched"
            ? "Viewer hosted | recipe comparison matched"
            : "Viewer hosted | recipe comparison pending";
    }

    private void RefreshRunHistory(
        string root,
        string contractPath,
        string reportPath,
        string uiStatus,
        string runnerStatus,
        string uiPeak,
        string runnerPeak,
        string comparisonState)
    {
        RecipeRunHistory.Clear();

        var status = runnerStatus != "(missing)" ? runnerStatus : uiStatus;
        var peakDeviation = runnerPeak != "(missing)" ? runnerPeak : uiPeak;
        var evidenceState = comparisonState == "Runner/UI contract matched" ? "Matched" : "Pending";

        RecipeRunHistory.Add(new RecipeRunHistoryItem(
            FormatRunTime(reportPath, contractPath),
            status,
            peakDeviation,
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

    private static string ExtractUiStatus(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line => line.StartsWith("C3D Height Deviation Rule|", StringComparison.Ordinal));
        var parts = resultLine?.Split('|');
        return parts is { Length: > 1 } ? parts[1] : "(missing)";
    }

    private static string ExtractRunnerStatus(string[] lines)
    {
        var resultLine = lines.FirstOrDefault(line => line.StartsWith("ToolResult|", StringComparison.Ordinal));
        var parts = resultLine?.Split('|');
        return parts is { Length: > 2 } ? parts[2] : "(missing)";
    }

    private static string ExtractMetricValue(string[] lines, string metricName)
    {
        var metricLine = lines.FirstOrDefault(line => line.StartsWith(metricName + "|", StringComparison.Ordinal));
        if (metricLine is null)
        {
            return "(missing)";
        }

        foreach (var part in metricLine.Split('|'))
        {
            if (part.StartsWith("value=", StringComparison.Ordinal))
            {
                return part["value=".Length..];
            }
        }

        return "(missing)";
    }

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

    private static string PreviewLines(string title, string path, string[] lines)
    {
        if (lines.Length == 0)
        {
            return $"{title}\nmissing or empty: {path}";
        }

        return $"{title}\n{path}\n{string.Join(Environment.NewLine, lines.Take(18))}";
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
        string peakDeviation,
        string evidenceState,
        string reportPath)
    {
        RunTime = runTime;
        Status = status;
        PeakDeviation = peakDeviation;
        EvidenceState = evidenceState;
        ReportPath = reportPath;
    }

    public string RunTime { get; }

    public string Status { get; }

    public string PeakDeviation { get; }

    public string EvidenceState { get; }

    public string ReportPath { get; }
}

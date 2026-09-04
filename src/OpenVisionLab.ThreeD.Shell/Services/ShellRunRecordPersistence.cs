using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Services;

/// <summary>
/// Owns file-system operations for Shell Run Records.
/// The ViewModel keeps bindable state and translates failures into operator text;
/// this owner keeps JSON, bundle, and recent-path persistence deterministic.
/// </summary>
internal sealed class ShellRunRecordPersistence
{
    public const int MaximumRecentRecords = RecipeRecentFileStore.MaximumEntries;

    private static readonly JsonSerializerOptions RunRecordJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string recentRunRecordsPath;

    public ShellRunRecordPersistence(string recentRunRecordsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recentRunRecordsPath);
        this.recentRunRecordsPath = recentRunRecordsPath;
    }

    public InspectionRunRecord? Read(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InspectionRunRecord>(
                File.ReadAllText(path),
                RunRecordJsonOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> LoadRecentPaths() =>
        RecipeRecentFileStore.Load(recentRunRecordsPath);

    public void SaveRecentPaths(IEnumerable<string> paths) =>
        RecipeRecentFileStore.Save(recentRunRecordsPath, paths);

    public string ExportRunRecordBundle(
        string runRecordPath,
        InspectionRunRecord record,
        string? htmlReportPath,
        string? csvReportPath,
        string targetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRecordPath);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);

        var safeRunId = string.Concat(record.RunId.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var exportDirectory = Path.Combine(Path.GetFullPath(targetRoot), $"RunRecord-{safeRunId}");
        for (var suffix = 2; Directory.Exists(exportDirectory); suffix++)
        {
            exportDirectory = Path.Combine(Path.GetFullPath(targetRoot), $"RunRecord-{safeRunId}-{suffix}");
        }

        Directory.CreateDirectory(exportDirectory);
        foreach (var sourcePath in new[] { runRecordPath, htmlReportPath, csvReportPath }
                     .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
        {
            var source = sourcePath!;
            File.Copy(source, Path.Combine(exportDirectory, Path.GetFileName(source)), overwrite: false);
        }

        return exportDirectory;
    }

    public string ExportPrivacySafeSupportBundle(
        string runRecordPath,
        string targetRoot,
        IReadOnlyList<ToolWorkbenchLogItem> sessionLog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRecordPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentNullException.ThrowIfNull(sessionLog);

        return PrivacySafeSupportBundleWriter.Write(
            runRecordPath,
            targetRoot,
            sessionLog);
    }
}

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell;

internal static class RunRecordHistoryVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Run Record history verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            var fixtureRoot = Path.Combine(reportDirectory, $"run-record-history-fixture-{Guid.NewGuid():N}");
            Directory.CreateDirectory(fixtureRoot);
            var recentPath = Path.Combine(fixtureRoot, "recent.json");

            var first = WriteFixture(fixtureRoot, "first", "1.4", ResultStatus.Pass, includeOutputHash: true);
            var viewModel = new ShellMainWindowViewModel(
                runRecordPath: first.Json,
                htmlReportPath: first.Html,
                csvReportPath: first.Csv,
                recentRunRecordsPath: recentPath);

            Check(
                "schema 1.4 record loads into ordered Run Record view",
                viewModel.InspectionSteps.Count == 1
                && viewModel.InspectionStepSummary.Contains("1.4", StringComparison.Ordinal)
                && viewModel.InspectionStepSummary.Contains("Pass", StringComparison.Ordinal),
                viewModel.InspectionStepSummary);
            Check(
                "loaded record becomes the first persisted recent item",
                viewModel.RecentRunRecords.Count == 1
                && PathsEqual(viewModel.RecentRunRecords[0].Path, first.Json)
                && RecipeRecentFileStore.Load(recentPath).Count == 1,
                string.Join(";", viewModel.RecentRunRecords.Select(item => item.Path)));
            Check(
                "current JSON HTML CSV folder and export commands are enabled",
                viewModel.OpenRunRecordCommand.CanExecute(null)
                && viewModel.OpenHtmlReportCommand.CanExecute(null)
                && viewModel.OpenCsvReportCommand.CanExecute(null)
                && viewModel.OpenRunRecordFolderCommand.CanExecute(null)
                && viewModel.ExportRunRecordCommand.CanExecute(null),
                "all current artifact commands enabled");

            var exportRoot = Path.Combine(fixtureRoot, "exports");
            var exported = viewModel.ExportCurrentRunRecordBundle(exportRoot, out var exportDirectory);
            var exportedJson = Path.Combine(exportDirectory, Path.GetFileName(first.Json));
            var exportedHtml = Path.Combine(exportDirectory, Path.GetFileName(first.Html));
            var exportedCsv = Path.Combine(exportDirectory, Path.GetFileName(first.Csv));
            Check(
                "export creates a collision-safe folder with byte-identical JSON HTML CSV",
                exported
                && File.Exists(exportedJson)
                && File.Exists(exportedHtml)
                && File.Exists(exportedCsv)
                && File.ReadAllBytes(exportedJson).SequenceEqual(File.ReadAllBytes(first.Json))
                && File.ReadAllBytes(exportedHtml).SequenceEqual(File.ReadAllBytes(first.Html))
                && File.ReadAllBytes(exportedCsv).SequenceEqual(File.ReadAllBytes(first.Csv)),
                exportDirectory);

            var second = WriteFixture(fixtureRoot, "second", "1.3", ResultStatus.Fail, includeOutputHash: false);
            var loadedSecond = viewModel.LoadRunRecord(second.Json, out var secondMessage);
            Check(
                "schema 1.3 record remains readable and moves to recent first",
                loadedSecond
                && viewModel.InspectionSteps.Count == 1
                && viewModel.InspectionStepSummary.Contains("1.3", StringComparison.Ordinal)
                && viewModel.InspectionStepSummary.Contains("Fail", StringComparison.Ordinal)
                && viewModel.RecentRunRecords.Count == 2
                && PathsEqual(viewModel.RecentRunRecords[0].Path, second.Json),
                secondMessage);

            var invalidPath = Path.Combine(fixtureRoot, "invalid.json");
            File.WriteAllText(invalidPath, "{ invalid");
            var previousPath = viewModel.SelectedRecentRunRecord?.Path;
            var invalidLoaded = viewModel.LoadRunRecord(invalidPath, out var invalidMessage);
            Check(
                "invalid JSON is rejected without replacing the current record",
                !invalidLoaded
                && PathsEqual(viewModel.SelectedRecentRunRecord?.Path, previousPath)
                && viewModel.InspectionStepSummary.Contains("1.3", StringComparison.Ordinal),
                invalidMessage);

            var firstRecent = viewModel.RecentRunRecords.Single(item => PathsEqual(item.Path, first.Json));
            viewModel.OpenRecentRunRecordCommand.Execute(firstRecent);
            Check(
                "recent selection reopens the exact record without executing inspection",
                PathsEqual(viewModel.SelectedRecentRunRecord?.Path, first.Json)
                && viewModel.InspectionStepSummary.Contains("1.4", StringComparison.Ordinal)
                && viewModel.RecentRunRecords.Count == 2,
                viewModel.InspectionStepSummary);
            Check(
                "recent list persists newest-first and stays bounded",
                RecipeRecentFileStore.Load(recentPath).Count == 2
                && PathsEqual(RecipeRecentFileStore.Load(recentPath)[0], first.Json)
                && RecipeRecentFileStore.Load(recentPath).Count <= RecipeRecentFileStore.MaximumEntries,
                string.Join(";", RecipeRecentFileStore.Load(recentPath)));
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var outputDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Run Record history verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static FixturePaths WriteFixture(
        string root,
        string name,
        string schema,
        ResultStatus status,
        bool includeOutputHash)
    {
        var json = Path.Combine(root, $"{name}.json");
        var html = Path.Combine(root, $"{name}.html");
        var csv = Path.Combine(root, $"{name}.csv");
        File.WriteAllText(html, $"<html><body>{name}</body></html>");
        File.WriteAllText(csv, $"name,status{Environment.NewLine}{name},{status}");

        var step = new InspectionRunStepResult(
            0,
            $"step.{name}",
            "filter",
            "C3D Median Filter",
            ["source.c3d"],
            $"output.{name}",
            status,
            "fixture",
            1.25,
            [],
            [])
        {
            OutputContentSha256 = includeOutputHash ? new string('A', 64) : null
        };
        var record = new InspectionRunRecord(
            schema,
            $"run-{name}",
            new DateTimeOffset(2026, 7, 23, 12, name == "first" ? 1 : 2, 0, TimeSpan.Zero),
            new InspectionRunRecipe("tool-recipe", "1.3", Path.Combine(root, "recipe.json"), new string('B', 64)),
            new InspectionRunSource("source.c3d", Path.Combine(root, "source.c3d"), new string('C', 64), 1, "raw-height"),
            "Ordered Tool Recipe",
            status,
            "fixture",
            1.25,
            [],
            [],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.Combine(root, "runner.txt"),
                null,
                null,
                json,
                html,
                csv))
        {
            Steps = [step]
        };
        File.WriteAllText(json, JsonSerializer.Serialize(record, JsonOptions));
        return new FixturePaths(json, html, csv);
    }

    private static bool PathsEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed record FixturePaths(string Json, string Html, string Csv);
}

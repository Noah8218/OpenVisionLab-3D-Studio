using OpenVisionLab;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using System.IO;

namespace OpenVisionLab.ThreeD.Shell;

internal static class RunLogRetentionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Workbench run-log retention verification",
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

        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        try
        {
            var workbench = new ToolWorkbenchViewModel(
                Path.Combine(Path.GetTempPath(), $"run-log-recent-{Guid.NewGuid():N}.json"));
            workbench.RunLog.Clear();
            var dirtyBefore = workbench.IsDirty;
            var stepCountBefore = workbench.PipelineSteps.Count;
            var selectionCountBefore = workbench.Selections.Count;
            var markerPrefix = $"RunLogRetention[{Guid.NewGuid():N}]";
            var generatedCount = ToolWorkbenchViewModel.MaximumRunLogEntries + 2;
            var startedAt = DateTime.UtcNow;

            for (var index = 0; index < generatedCount; index++)
            {
                workbench.AppendLog("Retention", $"{markerPrefix}|entry={index:D4}");
            }

            Check(
                "Workbench session memory is bounded",
                workbench.RunLog.Count == ToolWorkbenchViewModel.MaximumRunLogEntries,
                $"count={workbench.RunLog.Count}; max={ToolWorkbenchViewModel.MaximumRunLogEntries}");
            Check(
                "Newest-first ordering is retained",
                workbench.RunLog[0].Message == $"{markerPrefix}|entry={generatedCount - 1:D4}"
                && workbench.RunLog[^1].Message == $"{markerPrefix}|entry=0002",
                $"newest={workbench.RunLog[0].Message}; oldest={workbench.RunLog[^1].Message}");
            Check(
                "Only the oldest overflow entries are pruned",
                workbench.RunLog.All(item => item.Message != $"{markerPrefix}|entry=0000"
                    && item.Message != $"{markerPrefix}|entry=0001"),
                $"retained={workbench.RunLog.Count}; generated={generatedCount}");

            var flushed = OVLog.Flush();
            var logDirectory = OVLog.GetLogDirectory();
            var markerLines = ReadMarkerLines(logDirectory, markerPrefix, startedAt);
            Check(
                "Pruned entries remain in durable OVLog",
                flushed
                && markerLines.Count == generatedCount
                && markerLines.Any(line => line.Contains($"{markerPrefix}|entry=0000", StringComparison.Ordinal))
                && markerLines.Any(line => line.Contains($"{markerPrefix}|entry={generatedCount - 1:D4}", StringComparison.Ordinal)),
                $"flushed={flushed}; persisted={markerLines.Count}/{generatedCount}; directory={logDirectory ?? "<none>"}");

            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishApplication = workbench.Localization.WorkbenchApplicationLogRetention;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanApplication = workbench.Localization.WorkbenchApplicationLogRetention;
            Check(
                "Localized UI states the shared memory and file boundary",
                englishApplication.Contains("3,000", StringComparison.Ordinal)
                && englishApplication.Contains("rolling Application Log files", StringComparison.Ordinal)
                && koreanApplication.Contains("3,000", StringComparison.Ordinal)
                && koreanApplication.Contains("\uC21C\uD658 \uC800\uC7A5", StringComparison.Ordinal),
                $"en={englishApplication} | ko={koreanApplication}");
            Check(
                "Retention has no recipe or execution side effect",
                workbench.IsDirty == dirtyBefore
                && workbench.PipelineSteps.Count == stepCountBefore
                && workbench.Selections.Count == selectionCountBefore
                && !workbench.IsSelectedStepPreviewRunning
                && !workbench.IsValidationSetRunning
                && workbench.RunLog.All(item => item.Category == "Retention"),
                $"dirty={dirtyBefore}->{workbench.IsDirty}; steps={stepCountBefore}->{workbench.PipelineSteps.Count}; selections={selectionCountBefore}->{workbench.Selections.Count}; preview={workbench.IsSelectedStepPreviewRunning}; validation={workbench.IsValidationSetRunning}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
        }

        var directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        lines.Add($"Result: {(passed == total && total > 0 ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Workbench run-log retention verification: {(passed == total && total > 0 ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return passed == total && total > 0;
    }

    private static IReadOnlyList<string> ReadMarkerLines(
        string? logDirectory,
        string markerPrefix,
        DateTime startedAt)
    {
        if (string.IsNullOrWhiteSpace(logDirectory) || !Directory.Exists(logDirectory))
        {
            return [];
        }

        var markerLines = new List<string>();
        foreach (var path in Directory.EnumerateFiles(logDirectory, "*ALL.log*", SearchOption.AllDirectories)
                     .Where(path => File.GetLastWriteTimeUtc(path) >= startedAt.AddSeconds(-5)))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(stream);
            while (reader.ReadLine() is { } line)
            {
                if (line.Contains(markerPrefix, StringComparison.Ordinal))
                {
                    markerLines.Add(line);
                }
            }
        }

        return markerLines;
    }
}

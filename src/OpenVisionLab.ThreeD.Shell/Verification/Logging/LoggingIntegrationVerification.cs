using OpenVisionLab.Logging;
using System.IO;
using System.Linq;

namespace OpenVisionLab.ThreeD.Shell;

internal static class LoggingIntegrationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var marker = $"3D Studio logging verification {Guid.NewGuid():N}";
        var lines = new List<string>
        {
            "OpenVisionLab 3D Studio logging integration verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;

        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            OVLog.Write(LogCategory.System, LogLevel.Info, marker);
            Check("flush", OVLog.Flush(), "log4net flush completed");

            var logDirectory = OVLog.GetLogDirectory();
            Check(
                "configured log directory",
                !string.IsNullOrWhiteSpace(logDirectory) && Directory.Exists(logDirectory),
                logDirectory ?? "<null>");

            var latestLog = string.IsNullOrWhiteSpace(logDirectory) || !Directory.Exists(logDirectory)
                ? null
                : Directory.EnumerateFiles(logDirectory, "*ALL.log", SearchOption.AllDirectories)
                    .Select(path => new FileInfo(path))
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .FirstOrDefault();
            Check(
                "all log file",
                latestLog is { Length: > 0 },
                latestLog is null ? "<none>" : $"{latestLog.FullName} ({latestLog.Length} bytes)");

            var content = latestLog is null ? string.Empty : ReadSharedText(latestLog.FullName);
            Check("written marker", content.Contains(marker, StringComparison.Ordinal), marker);
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var directory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var expected = 4;
        lines.Add($"Result: {(passed == expected ? "Pass" : "Fail")} ({passed}/{expected} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Logging integration verification: {(passed == expected ? "Pass" : "Fail")} ({passed}/{expected} checks)";
        return passed == expected;
    }

    private static string ReadSharedText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

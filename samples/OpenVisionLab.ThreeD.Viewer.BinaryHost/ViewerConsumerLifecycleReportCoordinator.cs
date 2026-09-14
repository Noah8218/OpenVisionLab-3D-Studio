using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Owns the WPF-neutral report line, check, and single-write policy for the
/// independent Viewer consumer lifecycle run.
/// </summary>
internal sealed class ViewerConsumerLifecycleReportCoordinator
{
    private readonly string reportPath;
    private readonly List<string> reportLines = [];
    private int totalChecks;
    private int failedChecks;
    private bool reportWritten;

    public ViewerConsumerLifecycleReportCoordinator(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        this.reportPath = Path.GetFullPath(reportPath);
    }

    public int FailedChecks => failedChecks;

    public void AddLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        reportLines.Add(line);
    }

    public void AddSanitizedLine(string prefix, string details)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(details);
        AddLine($"{prefix}{Sanitize(details)}");
    }

    public void RecordCheck(string name, bool passed, string details)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(details);
        totalChecks++;
        if (!passed)
        {
            failedChecks++;
        }

        AddLine($"Check|name={name}|pass={passed}|{Sanitize(details)}");
    }

    public void Write()
    {
        if (reportWritten)
        {
            return;
        }

        reportWritten = true;
        AddLine($"Result|{(failedChecks == 0 ? "Pass" : "Fail")}|checks={totalChecks - failedChecks}/{totalChecks}|failed={failedChecks}");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllLines(reportPath, reportLines);
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
}

using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerLinkedHeightHoverSamplingVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D linked-height hover sampling verification",
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

        var state = new ViewerLinkedHeightHoverSamplingState();
        var firstTimestamp = 1000L;
        var intervalTicks = (long)Math.Ceiling(
            Stopwatch.Frequency * ViewerLinkedHeightHoverSamplingState.MinimumIntervalMilliseconds / 1000.0);

        Check(
            "first position is accepted",
            state.TryAccept(10.0, 20.0, firstTimestamp),
            "accepted=True");
        Check(
            "same position inside interval is rejected",
            !state.TryAccept(10.0, 20.0, firstTimestamp + 1),
            "accepted=False");
        Check(
            "small movement inside interval is rejected",
            !state.TryAccept(11.0, 21.0, firstTimestamp + 2),
            "distance=sqrt(2)|accepted=False");
        Check(
            "threshold movement inside interval is accepted",
            state.TryAccept(12.0, 20.0, firstTimestamp + 3),
            "distance=2|accepted=True");
        Check(
            "same threshold position inside interval is rejected",
            !state.TryAccept(12.0, 20.0, firstTimestamp + 4),
            "accepted=False");
        Check(
            "small movement after interval is accepted",
            state.TryAccept(13.0, 20.5, firstTimestamp + intervalTicks + 5),
            $"intervalTicks={intervalTicks}|accepted=True");

        state.Reset();
        Check(
            "reset clears the previous sample gate",
            state.TryAccept(13.0, 20.5, firstTimestamp + 6),
            "accepted=True");
        Check(
            "reset still rejects an immediate duplicate",
            !state.TryAccept(13.0, 20.5, firstTimestamp + 7),
            "accepted=False");

        summary = $"Viewer linked-height hover sampling verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}

using System.IO;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class C3DGpuTelemetryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer C3D GPU telemetry verification",
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

        var telemetry = new C3DGpuTelemetry();
        Check(
            "new telemetry starts empty",
            telemetry.UploadCount == 0
                && telemetry.ReleaseCount == 0
                && telemetry.ReleaseFailureCount == 0
                && telemetry.FallbackCount == 0
                && telemetry.DrawCount == 0
                && telemetry.UploadedBytes == 0
                && telemetry.LastUploadMilliseconds == 0.0
                && telemetry.LastFailure == string.Empty,
            $"uploads={telemetry.UploadCount}|releases={telemetry.ReleaseCount}|failures={telemetry.ReleaseFailureCount}|fallbacks={telemetry.FallbackCount}|draws={telemetry.DrawCount}");

        telemetry.RecordUpload(1024, 3.5);
        Check(
            "upload records latest payload and timing",
            telemetry.UploadCount == 1
                && telemetry.UploadedBytes == 1024
                && telemetry.LastUploadMilliseconds == 3.5
                && telemetry.LastFailure == string.Empty,
            $"uploads={telemetry.UploadCount}|bytes={telemetry.UploadedBytes}|ms={telemetry.LastUploadMilliseconds}");

        telemetry.RecordUpload(2048, 4.25);
        Check(
            "repeated upload increments count while replacing latest sample",
            telemetry.UploadCount == 2
                && telemetry.UploadedBytes == 2048
                && telemetry.LastUploadMilliseconds == 4.25,
            $"uploads={telemetry.UploadCount}|bytes={telemetry.UploadedBytes}|ms={telemetry.LastUploadMilliseconds}");

        telemetry.RecordFallback("context unavailable");
        Check(
            "fallback records count and diagnostic text",
            telemetry.FallbackCount == 1
                && telemetry.LastFailure == "context unavailable",
            $"fallbacks={telemetry.FallbackCount}|failure={telemetry.LastFailure}");

        telemetry.RecordDraw();
        telemetry.RecordDraw();
        Check(
            "draw observations are cumulative",
            telemetry.DrawCount == 2,
            $"draws={telemetry.DrawCount}");

        telemetry.RecordRelease(succeeded: true);
        telemetry.RecordRelease(succeeded: false);
        Check(
            "release success and failure are separated",
            telemetry.ReleaseCount == 1
                && telemetry.ReleaseFailureCount == 1,
            $"releases={telemetry.ReleaseCount}|releaseFailures={telemetry.ReleaseFailureCount}");

        telemetry.RecordFallback("second failure");
        Check(
            "multiple fallback diagnostics keep the latest failure",
            telemetry.FallbackCount == 2
                && telemetry.LastFailure == "second failure",
            $"fallbacks={telemetry.FallbackCount}|failure={telemetry.LastFailure}");

        telemetry.RecordUpload(4096, 1.25);
        Check(
            "successful replacement clears stale fallback text",
            telemetry.UploadCount == 3
                && telemetry.UploadedBytes == 4096
                && telemetry.LastUploadMilliseconds == 1.25
                && telemetry.LastFailure == string.Empty
                && telemetry.FallbackCount == 2,
            $"uploads={telemetry.UploadCount}|bytes={telemetry.UploadedBytes}|failure={telemetry.LastFailure}|fallbacks={telemetry.FallbackCount}");

        summary = $"C3D GPU telemetry verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}

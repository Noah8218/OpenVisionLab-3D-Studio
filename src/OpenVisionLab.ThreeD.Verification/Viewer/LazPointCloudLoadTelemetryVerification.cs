using System.IO;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class LazPointCloudLoadTelemetryVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer LAZ/LAS load telemetry verification",
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

        var telemetry = new LazPointCloudLoadTelemetry();
        Check(
            "new load telemetry starts with a completed task and empty counters",
            telemetry.ReloadTask.IsCompleted
                && !telemetry.IsDensityReloadSuppressed
                && telemetry.LoadRequestCount == 0
                && telemetry.DensityEventReloadCount == 0
                && telemetry.SmokeReloadCount == 0
                && telemetry.DecodeCount == 0
                && telemetry.CacheHitCount == 0
                && telemetry.CancellationCount == 0
                && telemetry.ProgressUpdateCount == 0
                && telemetry.LastProgress == 0.0,
            $"taskCompleted={telemetry.ReloadTask.IsCompleted}|requests={telemetry.LoadRequestCount}|progress={telemetry.LastProgress}");

        telemetry.SetDensityReloadSuppressed(true);
        Check(
            "density reload suppression is explicit state",
            telemetry.IsDensityReloadSuppressed,
            $"suppressed={telemetry.IsDensityReloadSuppressed}");
        telemetry.SetDensityReloadSuppressed(false);

        telemetry.RecordLoadRequest();
        var lowProgress = telemetry.RecordProgress(-5.0);
        var highProgress = telemetry.RecordProgress(125.0);
        Check(
            "load requests and progress updates are counted with clamped values",
            telemetry.LoadRequestCount == 1
                && telemetry.ProgressUpdateCount == 2
                && lowProgress == 0.0
                && highProgress == 100.0
                && telemetry.LastProgress == 100.0,
            $"requests={telemetry.LoadRequestCount}|updates={telemetry.ProgressUpdateCount}|last={telemetry.LastProgress}");

        telemetry.RecordCacheHit();
        telemetry.RecordDecode();
        telemetry.RecordCancellation();
        Check(
            "decode, cache-hit, and cancellation observations remain separate",
            telemetry.CacheHitCount == 1
                && telemetry.DecodeCount == 1
                && telemetry.CancellationCount == 1,
            $"cacheHits={telemetry.CacheHitCount}|decodes={telemetry.DecodeCount}|cancellations={telemetry.CancellationCount}");

        var densityFactorySawCount = false;
        var densityTask = telemetry.RecordDensityEventReload(() =>
        {
            densityFactorySawCount = telemetry.DensityEventReloadCount == 1;
            return Task.CompletedTask;
        });
        Check(
            "density reload count is recorded before the reload factory runs",
            densityFactorySawCount
                && densityTask.IsCompleted
                && ReferenceEquals(telemetry.ReloadTask, densityTask)
                && telemetry.DensityEventReloadCount == 1,
            $"densityReloads={telemetry.DensityEventReloadCount}|taskCompleted={densityTask.IsCompleted}");

        var smokeFactorySawCount = false;
        var smokeTask = telemetry.RecordSmokeReload(() =>
        {
            smokeFactorySawCount = telemetry.SmokeReloadCount == 1;
            return new TaskCompletionSource<object?>().Task;
        });
        Check(
            "Smoke reload replaces the current task after incrementing its count",
            smokeFactorySawCount
                && !smokeTask.IsCompleted
                && ReferenceEquals(telemetry.ReloadTask, smokeTask)
                && telemetry.SmokeReloadCount == 1,
            $"smokeReloads={telemetry.SmokeReloadCount}|taskCompleted={smokeTask.IsCompleted}");

        telemetry.ClearReloadTask();
        Check(
            "clearing reload task leaves cumulative observations intact",
            telemetry.ReloadTask.IsCompleted
                && telemetry.LoadRequestCount == 1
                && telemetry.DensityEventReloadCount == 1
                && telemetry.SmokeReloadCount == 1
                && telemetry.CancellationCount == 1,
            $"taskCompleted={telemetry.ReloadTask.IsCompleted}|requests={telemetry.LoadRequestCount}|smokeReloads={telemetry.SmokeReloadCount}");

        telemetry.SetDensityReloadSuppressed(true);
        telemetry.RecordLoadRequest();
        telemetry.RecordProgress(42.5);
        Check(
            "later requests and progress keep the latest state without resetting history",
            telemetry.IsDensityReloadSuppressed
                && telemetry.LoadRequestCount == 2
                && telemetry.ProgressUpdateCount == 3
                && telemetry.LastProgress == 42.5,
            $"suppressed={telemetry.IsDensityReloadSuppressed}|requests={telemetry.LoadRequestCount}|updates={telemetry.ProgressUpdateCount}|last={telemetry.LastProgress}");

        summary = $"LAZ/LAS load telemetry verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return passed == total;
    }
}

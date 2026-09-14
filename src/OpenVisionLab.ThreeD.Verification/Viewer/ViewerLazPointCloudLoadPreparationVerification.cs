using System.IO;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerLazPointCloudLoadPreparationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer LAZ/LAS load preparation verification",
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

        var samplePath = Path.Combine(
            "3D",
            "PublicSamples",
            "PointCloud",
            "interesting.las");
        var fixtureExists = File.Exists(samplePath);
        Check(
            "LAZ/LAS fixture exists for WPF-free preparation",
            fixtureExists,
            $"path={Path.GetFullPath(samplePath)}");

        var request = ViewerLazPointCloudLoadPreparation.Prepare(samplePath, 1);
        Check(
            "preparation normalizes source identity and clamps the sample budget",
            string.Equals(request.FullPath, Path.GetFullPath(samplePath), StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.SourceName, "interesting.las", StringComparison.Ordinal)
            && request.MaxSampledPoints == 2
            && request.FileExists == fixtureExists,
            $"fullPath={request.FullPath};sourceName={request.SourceName};budget={request.MaxSampledPoints};exists={request.FileExists}");

        var missingRequest = ViewerLazPointCloudLoadPreparation.Prepare(
            Path.Combine("3D", "PublicSamples", "PointCloud", "missing-owner.las"),
            int.MaxValue);
        Check(
            "preparation reports missing sources without touching WPF state",
            !missingRequest.FileExists
            && missingRequest.MaxSampledPoints == int.MaxValue
            && string.Equals(missingRequest.SourceName, "missing-owner.las", StringComparison.Ordinal),
            $"exists={missingRequest.FileExists};budget={missingRequest.MaxSampledPoints};sourceName={missingRequest.SourceName}");

        if (fixtureExists)
        {
            var cache = new LazPointCloudSampleCache();
            using var coordinator = new LazPointCloudLoadCoordinator(cache);
            var first = ViewerLazPointCloudLoadPreparation.Load(coordinator, request);
            Check(
                "sync preparation delegates decode without WPF",
                first.PointCloud is not null && !first.Reused && !first.WasCanceled,
                $"loaded={first.PointCloud is not null};reused={first.Reused};cancelled={first.WasCanceled}");

            var second = ViewerLazPointCloudLoadPreparation.Load(coordinator, request);
            Check(
                "sync preparation preserves injected cache reuse",
                second.PointCloud is not null
                && second.Reused
                && !second.WasCanceled
                && ReferenceEquals(first.PointCloud, second.PointCloud),
                $"loaded={second.PointCloud is not null};reused={second.Reused};sameObject={ReferenceEquals(first.PointCloud, second.PointCloud)}");

            var asyncRequest = ViewerLazPointCloudLoadPreparation.Prepare(samplePath, 123);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var cancelled = ViewerLazPointCloudLoadPreparation
                .LoadAsync(coordinator, asyncRequest, cancellation.Token)
                .GetAwaiter()
                .GetResult();
            Check(
                "async preparation preserves the coordinator cancellation outcome",
                cancelled is { PointCloud: null, WasCanceled: true },
                $"loaded={cancelled?.PointCloud is not null};cancelled={cancelled?.WasCanceled}");
        }

        var missingRejected = false;
        try
        {
            using var coordinator = new LazPointCloudLoadCoordinator(new LazPointCloudSampleCache());
            _ = ViewerLazPointCloudLoadPreparation.Load(coordinator, missingRequest);
        }
        catch (FileNotFoundException)
        {
            missingRejected = true;
        }

        Check(
            "preparation rejects a missing source before decode",
            missingRejected,
            $"rejected={missingRejected}");

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerLazPointCloudLoadPreparation|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}

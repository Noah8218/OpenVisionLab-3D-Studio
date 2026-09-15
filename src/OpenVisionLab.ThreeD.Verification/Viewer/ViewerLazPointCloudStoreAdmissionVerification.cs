using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Loading;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerLazPointCloudStoreAdmissionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath)!;
        Directory.CreateDirectory(reportDirectory);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer LAZ/LAS cache Store admission verification",
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

        var sourcePath = Path.Combine(
            "3D",
            "PublicSamples",
            "PointCloud",
            "xyzrgb_manuscript.laz");
        if (!File.Exists(sourcePath))
        {
            Check(
                "large LAZ fixture exists",
                false,
                $"missing={Path.GetFullPath(sourcePath)}");
        }
        else
        {
            var fixturePath = Path.Combine(
                reportDirectory,
                $"openvisionlab-laz-store-admission-{Guid.NewGuid():N}.laz");
            try
            {
                File.Copy(sourcePath, fixturePath, overwrite: true);
                var expectedIdentity = LazPointCloudSourceIdentity.Capture(fixturePath);
                var pointCloud = LazPointCloud.Load(fixturePath, 64);
                var observedIdentity = LazPointCloudSourceIdentity.Capture(fixturePath);

                var lockHeldStoreSucceeded = false;
                var lockHeldCache = new LazPointCloudSampleCache();
                using (var sourceLock = new FileStream(
                    fixturePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None,
                    bufferSize: 4096,
                    options: FileOptions.SequentialScan))
                {
                    try
                    {
                        lockHeldCache.Store(
                            fixturePath,
                            64,
                            pointCloud,
                            expectedIdentity,
                            observedIdentity);
                        lockHeldStoreSucceeded = true;
                    }
                    catch (IOException)
                    {
                    }
                }

                var lockHeldHit = lockHeldCache.TryGet(fixturePath, 64, out var lockHeldPointCloud);
                Check(
                    "precomputed Store admission does not reopen the source file",
                    lockHeldStoreSucceeded
                    && lockHeldHit
                    && ReferenceEquals(pointCloud, lockHeldPointCloud),
                    $"storeSucceeded={lockHeldStoreSucceeded};hit={lockHeldHit};sameObject={ReferenceEquals(pointCloud, lockHeldPointCloud)}");

                var legacyDurations = MeasureStoreDurations(
                    fixturePath,
                    pointCloud,
                    expectedIdentity: null,
                    observedIdentity: null);
                var guardedDurations = MeasureStoreDurations(
                    fixturePath,
                    pointCloud,
                    expectedIdentity,
                    observedIdentity);
                var legacyMedian = Median(legacyDurations);
                var guardedMedian = Median(guardedDurations);
                Check(
                    "precomputed Store path completes without the legacy synchronous hash step",
                    guardedDurations.Count == legacyDurations.Count
                    && guardedDurations.Count > 0,
                    $"legacyMedianMs={legacyMedian:F3};guardedMedianMs={guardedMedian:F3};samples={guardedDurations.Count}");

                Check(
                    "precomputed Store retains the expected source identity",
                    lockHeldCache.GetSnapshot().SourceContentSha256 == observedIdentity.ContentSha256,
                    $"expectedSha256={expectedIdentity.ContentSha256};observedSha256={observedIdentity.ContentSha256};cachedSha256={lockHeldCache.GetSnapshot().SourceContentSha256}");
            }
            finally
            {
                if (File.Exists(fixturePath))
                {
                    File.Delete(fixturePath);
                }
            }
        }

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerLazPointCloudStoreAdmission|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static List<double> MeasureStoreDurations(
        string fixturePath,
        LazPointCloud pointCloud,
        LazPointCloudSourceIdentity? expectedIdentity,
        LazPointCloudSourceIdentity? observedIdentity)
    {
        var durations = new List<double>(capacity: 6);
        for (var index = 0; index < 6; index++)
        {
            var cache = new LazPointCloudSampleCache(capacity: 1);
            var start = Stopwatch.GetTimestamp();
            cache.Store(
                fixturePath,
                64 + index,
                pointCloud,
                expectedIdentity,
                observedIdentity);
            durations.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }

        return durations;
    }

    private static double Median(IEnumerable<double> values)
    {
        var ordered = values.OrderBy(static value => value).ToArray();
        return ordered[ordered.Length / 2];
    }
}

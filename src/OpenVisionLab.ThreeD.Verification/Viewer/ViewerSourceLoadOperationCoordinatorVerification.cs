using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerSourceLoadOperationCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer LAZ/LAS load coordinator verification",
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

        using (var coordinator = new ViewerSourceLoadOperationCoordinator())
        {
            using var first = coordinator.Begin();
            Check(
                "first operation starts current and identified",
                first.IsCurrent && first.Generation == 1 && !first.IsCancellationRequested,
                $"current={first.IsCurrent};generation={first.Generation};cancelled={first.IsCancellationRequested}");

            using var second = coordinator.Begin();
            Check(
                "new operation cancels and supersedes the previous one",
                first.IsCancellationRequested
                && !first.IsCurrent
                && second.IsCurrent
                && second.Generation == 2,
                $"firstCancelled={first.IsCancellationRequested};firstCurrent={first.IsCurrent};secondCurrent={second.IsCurrent};secondGeneration={second.Generation}");

            var staleApplyCount = 0;
            var staleApplied = coordinator.TryApply(first, () => staleApplyCount++);
            var activeApplyCount = 0;
            var activeApplied = coordinator.TryApply(second, () => activeApplyCount++);
            Check(
                "stale operation cannot enter the View apply callback",
                !staleApplied && staleApplyCount == 0 && activeApplied && activeApplyCount == 1,
                $"staleApplied={staleApplied};staleCount={staleApplyCount};activeApplied={activeApplied};activeCount={activeApplyCount}");

            coordinator.Dispose();
            Check(
                "Dispose cancels and retires the active operation",
                second.IsCancellationRequested && !second.IsCurrent,
                $"cancelled={second.IsCancellationRequested};current={second.IsCurrent}");

            var tokenWaitHandleAvailableAfterCoordinatorDispose = false;
            try
            {
                using var waitHandle = second.Token.WaitHandle;
                tokenWaitHandleAvailableAfterCoordinatorDispose = true;
            }
            catch (ObjectDisposedException)
            {
            }

            Check(
                "Dispose defers cancellation-source disposal until operation completion",
                tokenWaitHandleAvailableAfterCoordinatorDispose,
                $"waitHandleAvailable={tokenWaitHandleAvailableAfterCoordinatorDispose}");

            second.Dispose();
            var tokenWaitHandleDisposedAfterCompletion = false;
            try
            {
                using var waitHandle = second.Token.WaitHandle;
            }
            catch (ObjectDisposedException)
            {
                tokenWaitHandleDisposedAfterCompletion = true;
            }

            Check(
                "operation completion releases its cancellation source",
                tokenWaitHandleDisposedAfterCompletion,
                $"waitHandleDisposed={tokenWaitHandleDisposedAfterCompletion}");

            var rejectedAfterDispose = false;
            try
            {
                coordinator.Begin();
            }
            catch (ObjectDisposedException)
            {
                rejectedAfterDispose = true;
            }

            Check(
                "disposed coordinator rejects a new operation",
                rejectedAfterDispose,
                $"rejected={rejectedAfterDispose}");
        }

        using (var externalCancellation = new CancellationTokenSource())
        using (var coordinator = new ViewerSourceLoadOperationCoordinator())
        {
            using var operation = coordinator.Begin(externalCancellation.Token);
            externalCancellation.Cancel();
            var applied = coordinator.TryApply(operation, static () => { });
            Check(
                "external cancellation is linked and blocks View apply",
                operation.IsCancellationRequested && operation.IsCurrent && !applied,
                $"cancelled={operation.IsCancellationRequested};current={operation.IsCurrent};applied={applied}");
        }

        using (var coordinator = new ViewerSourceLoadOperationCoordinator())
        {
            using var operation = coordinator.Begin();
            coordinator.CancelCurrent();
            Check(
                "Viewer-owned cancellation is distinguishable from external cancellation",
                operation.IsCancellationRequested
                && operation.IsCurrent
                && !operation.IsExternalCancellationRequested,
                $"cancelled={operation.IsCancellationRequested};current={operation.IsCurrent};external={operation.IsExternalCancellationRequested}");
        }

        using (var completionCoordinator = new ViewerSourceLoadOperationCoordinator())
        {
            var completed = completionCoordinator.Begin();
            completed.Dispose();
            using var replacement = completionCoordinator.Begin();
            Check(
                "completion clears only its own active operation",
                !completed.IsCurrent
                && !completed.IsCancellationRequested
                && replacement.IsCurrent
                && replacement.Generation == 2,
                $"completedCurrent={completed.IsCurrent};completedCancelled={completed.IsCancellationRequested};replacementCurrent={replacement.IsCurrent};replacementGeneration={replacement.Generation}");
        }

        var samplePath = Path.Combine(
            "3D",
            "PublicSamples",
            "PointCloud",
            "interesting.las");
        if (!File.Exists(samplePath))
        {
            Check(
                "sample cache fixture exists",
                false,
                $"missing={Path.GetFullPath(samplePath)}");
        }
        else
        {
            var pointCloud = LazPointCloud.Load(samplePath, 64);
            var cache = new LazPointCloudSampleCache();
            cache.Store(samplePath, 64, pointCloud);
            var sameSourceHit = cache.TryGet(
                Path.GetFullPath(samplePath),
                64,
                out var cachedPointCloud);
            Check(
                "sample cache reuses the same source and budget",
                sameSourceHit
                && ReferenceEquals(pointCloud, cachedPointCloud)
                && cache.Count == 1,
                $"hit={sameSourceHit};sameObject={ReferenceEquals(pointCloud, cachedPointCloud)};count={cache.Count}");

            var asyncCachedPointCloud = cache
                .TryGetAsync(Path.GetFullPath(samplePath), 64)
                .GetAwaiter()
                .GetResult();
            Check(
                "sample cache async identity lookup reuses the same source and budget",
                ReferenceEquals(pointCloud, asyncCachedPointCloud),
                $"sameObject={ReferenceEquals(pointCloud, asyncCachedPointCloud)};count={cache.Count}");

            using var canceledIdentityLookup = new CancellationTokenSource();
            canceledIdentityLookup.Cancel();
            var identityLookupCanceled = false;
            try
            {
                _ = cache
                    .TryGetAsync(Path.GetFullPath(samplePath), 64, canceledIdentityLookup.Token)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (OperationCanceledException)
            {
                identityLookupCanceled = true;
            }

            Check(
                "sample cache async identity lookup observes cancellation before hashing",
                identityLookupCanceled && cache.Count == 1,
                $"cancelled={identityLookupCanceled};count={cache.Count}");

            var replacementPath = Path.Combine(Path.GetTempPath(), $"openvisionlab-laz-replacement-{Guid.NewGuid():N}.las");
            try
            {
                File.Copy(samplePath, replacementPath, overwrite: true);
                var replacementCache = new LazPointCloudSampleCache();
                replacementCache.Store(replacementPath, 64, pointCloud);
                var originalWriteTime = File.GetLastWriteTimeUtc(replacementPath);
                var replacementBytes = File.ReadAllBytes(replacementPath);
                replacementBytes[^1] ^= 0x01;
                File.WriteAllBytes(replacementPath, replacementBytes);
                File.SetLastWriteTimeUtc(replacementPath, originalWriteTime);
                var sameFingerprintReplacementHit = replacementCache.TryGet(replacementPath, 64, out _);
                Check(
                    "sample cache rejects same-path same-fingerprint content replacement",
                    !sameFingerprintReplacementHit && replacementCache.Count == 0,
                    $"hit={sameFingerprintReplacementHit};count={replacementCache.Count}");
            }
            finally
            {
                if (File.Exists(replacementPath))
                {
                    File.Delete(replacementPath);
                }
            }

            var densePointCloud = LazPointCloud.Load(samplePath, 128);
            var byteLimitedCache = new LazPointCloudSampleCache(
                capacity: 8,
                byteBudget: checked(densePointCloud.SampledPointView.Count * Marshal.SizeOf<LazPointCloudPoint>()));
            byteLimitedCache.Store(samplePath, 64, pointCloud);
            byteLimitedCache.Store(samplePath, 128, densePointCloud);
            var byteLimitedSnapshot = byteLimitedCache.GetSnapshot();
            var retainedDenseEntry = byteLimitedCache.TryGet(samplePath, 128, out var retainedDensePointCloud);
            Check(
                "sample cache enforces the configured managed payload byte budget",
                byteLimitedSnapshot.EstimatedSampledPointBytes <= byteLimitedSnapshot.ByteBudget
                && byteLimitedSnapshot.EntryCount == 1
                && retainedDenseEntry
                && ReferenceEquals(retainedDensePointCloud, densePointCloud),
                $"entries={byteLimitedSnapshot.EntryCount};payload={byteLimitedSnapshot.EstimatedSampledPointBytes};budget={byteLimitedSnapshot.ByteBudget};denseHit={retainedDenseEntry}");

            var precisionTransform = new LazSceneTransform(1_000_000.0, 2_000_000.0, 3_000_000.0);
            var precisePoint = new LazPointCloudPoint(Vector3.Zero, 0, 0, 0, 0)
            {
                HasPreciseSourceCoordinate = true,
                SourceCoordinate = new LazPointCloudSourceCoordinate(1_000_000.001, 2_000_000.001, 3_000_000.001, 1)
            };
            var mappedPrecisePoint = precisionTransform.Map(precisePoint);
            Check(
                "LAZ scene mapping subtracts origin before float conversion",
                Math.Abs(mappedPrecisePoint.X - 0.001f) < 1e-6f
                && Math.Abs(mappedPrecisePoint.Y - 0.001f) < 1e-6f
                && Math.Abs(mappedPrecisePoint.Z - 0.001f) < 1e-6f,
                $"mapped={mappedPrecisePoint}");

            var cacheSnapshot = cache.GetSnapshot();
            Check(
                "sample cache reports bounded managed-array residency",
                cacheSnapshot.EntryCount == 1
                && cacheSnapshot.Capacity == 3
                && cacheSnapshot.SampledPointCount == pointCloud.SampledPointView.Count
                && cacheSnapshot.EstimatedSampledPointBytes > 0
                && cacheSnapshot.ByteBudget >= cacheSnapshot.EstimatedSampledPointBytes
                && !string.IsNullOrWhiteSpace(cacheSnapshot.SourceContentSha256)
                && string.Equals(cacheSnapshot.SourcePath, Path.GetFullPath(samplePath), StringComparison.OrdinalIgnoreCase),
                $"entries={cacheSnapshot.EntryCount};capacity={cacheSnapshot.Capacity};byteBudget={cacheSnapshot.ByteBudget};sampledPoints={cacheSnapshot.SampledPointCount};estimatedBytes={cacheSnapshot.EstimatedSampledPointBytes};sourceBytes={cacheSnapshot.SourceByteLength};sourceSha256={cacheSnapshot.SourceContentSha256};source={cacheSnapshot.SourcePath}");

            var boundedCache = new LazPointCloudSampleCache();
            boundedCache.Store(samplePath, 64, pointCloud);
            boundedCache.Store(samplePath, 128, pointCloud);
            boundedCache.Store(samplePath, 256, pointCloud);
            var bounded64Hit = boundedCache.TryGet(samplePath, 64, out _);
            var bounded128Hit = boundedCache.TryGet(samplePath, 128, out _);
            var bounded256Hit = boundedCache.TryGet(samplePath, 256, out _);
            Check(
                "default sample cache bounds recent density entries",
                boundedCache.Capacity == 3
                && boundedCache.Count == 3
                && bounded64Hit
                && bounded128Hit
                && bounded256Hit,
                $"capacity={boundedCache.Capacity};count={boundedCache.Count};budget64={bounded64Hit};budget128={bounded128Hit};budget256={bounded256Hit}");

            var boundedSnapshot = boundedCache.GetSnapshot();
            Check(
                "sample cache snapshot counts shared point-cloud arrays once",
                boundedSnapshot.EntryCount == 3
                && boundedSnapshot.SampledPointCount == pointCloud.SampledPointView.Count,
                $"entries={boundedSnapshot.EntryCount};sampledPoints={boundedSnapshot.SampledPointCount};sourceArrayLength={pointCloud.SampledPointView.Count}");

            _ = boundedCache.TryGet(samplePath, 64, out _);
            boundedCache.Store(samplePath, 512, pointCloud);
            var retainedAfterLruTouch = boundedCache.TryGet(samplePath, 64, out _);
            var evictedLeastRecent = !boundedCache.TryGet(samplePath, 128, out _);
            var retained256 = boundedCache.TryGet(samplePath, 256, out _);
            var retained512 = boundedCache.TryGet(samplePath, 512, out _);
            Check(
                "default sample cache evicts the least-recent density entry",
                boundedCache.Count == 3
                && retainedAfterLruTouch
                && evictedLeastRecent
                && retained256
                && retained512,
                $"count={boundedCache.Count};retained64={retainedAfterLruTouch};evicted128={evictedLeastRecent};retained256={retained256};retained512={retained512}");

            var unboundedTestCache = new LazPointCloudSampleCache(capacity: 64);
            unboundedTestCache.Store(samplePath, 64, pointCloud);
            unboundedTestCache.Store(samplePath, 128, pointCloud);
            var budget64Hit = unboundedTestCache.TryGet(samplePath, 64, out _);
            var budget128Hit = unboundedTestCache.TryGet(samplePath, 128, out _);
            Check(
                "sample cache keeps multiple budgets for one source",
                budget64Hit && budget128Hit && unboundedTestCache.Count == 2,
                $"budget64={budget64Hit};budget128={budget128Hit};count={unboundedTestCache.Count}");

            Parallel.For(0, 32, index => unboundedTestCache.Store(samplePath, 256 + index, pointCloud));
            Check(
                "sample cache serializes concurrent budget writes",
                unboundedTestCache.Count == 34
                && unboundedTestCache.TryGet(samplePath, 256, out _)
                && unboundedTestCache.TryGet(samplePath, 287, out _),
                $"count={unboundedTestCache.Count};firstConcurrentBudget={unboundedTestCache.TryGet(samplePath, 256, out _)};lastConcurrentBudget={unboundedTestCache.TryGet(samplePath, 287, out _)}");

            unboundedTestCache.Store(samplePath + ".replacement", 64, pointCloud);
            var staleSourceHit = unboundedTestCache.TryGet(samplePath, 64, out _);
            Check(
                "sample cache invalidates entries when source changes",
                !staleSourceHit && unboundedTestCache.Count == 1,
                $"staleSourceHit={staleSourceHit};source={unboundedTestCache.SourcePath};count={unboundedTestCache.Count}");

            unboundedTestCache.Clear();
            Check(
                "sample cache clear drops managed references",
                !unboundedTestCache.HasEntries && unboundedTestCache.SourcePath is null && unboundedTestCache.Count == 0,
                $"hasEntries={unboundedTestCache.HasEntries};source={unboundedTestCache.SourcePath};count={unboundedTestCache.Count}");

            using var session = new ViewerLazPointCloudSession();
            session.State.SetPointCloud(pointCloud);
            session.Cache.Store(samplePath, 64, pointCloud);
            Check(
                "LAZ session centralizes current state and cache ownership",
                session.HasManagedData
                && ReferenceEquals(session.State.PointCloud, pointCloud)
                && session.Cache.Count == 1,
                $"hasManagedData={session.HasManagedData};sameState={ReferenceEquals(session.State.PointCloud, pointCloud)};cacheCount={session.Cache.Count}");

            session.Clear();
            Check(
                "LAZ session Clear drops current and cached managed references",
                !session.HasManagedData
                && session.State.PointCloud is null
                && session.State.Metadata is null
                && session.Cache.Count == 0,
                $"hasManagedData={session.HasManagedData};statePointCloud={session.State.PointCloud is not null};stateMetadata={session.State.Metadata is not null};cacheCount={session.Cache.Count}");

            session.Dispose();
            var rejectedAfterSessionDispose = false;
            try
            {
                _ = session.LoadCoordinator
                    .LoadAsync(ViewerLazPointCloudLoadPreparation.Prepare(samplePath, 64))
                    .GetAwaiter()
                    .GetResult();
            }
            catch (ObjectDisposedException)
            {
                rejectedAfterSessionDispose = true;
            }

            Check(
                "LAZ session Dispose retires its load coordinator",
                rejectedAfterSessionDispose && !session.HasManagedData,
                $"rejected={rejectedAfterSessionDispose};hasManagedData={session.HasManagedData}");

            using var repeatedDensitySession = new ViewerLazPointCloudSession();
            var repeatedDensityBudgets = new[] { 64, 128, 256, 512, 256, 128, 64 };
            // GC values are observations only; the bounded acceptance uses the cache snapshot payload.
            var managedBytesBeforeDensity = GC.GetTotalMemory(forceFullCollection: true);
            var managedBytesPeak = managedBytesBeforeDensity;
            var payloadBytesPeak = 0L;
            var sampledPointPeak = 0L;
            var densityObservations = new List<string>(repeatedDensityBudgets.Length);
            foreach (var budget in repeatedDensityBudgets)
            {
                var densityResult = repeatedDensitySession.LoadCoordinator.Load(
                    ViewerLazPointCloudLoadPreparation.Prepare(samplePath, budget));
                if (densityResult.PointCloud is not { } densityPointCloud)
                {
                    break;
                }

                repeatedDensitySession.State.SetPointCloud(densityPointCloud);
                var densitySnapshot = repeatedDensitySession.Cache.GetSnapshot();
                payloadBytesPeak = Math.Max(payloadBytesPeak, densitySnapshot.EstimatedSampledPointBytes);
                sampledPointPeak = Math.Max(sampledPointPeak, densitySnapshot.SampledPointCount);
                managedBytesPeak = Math.Max(managedBytesPeak, GC.GetTotalMemory(forceFullCollection: false));
                densityObservations.Add(
                    $"budget={budget};entries={densitySnapshot.EntryCount};sampledPoints={densitySnapshot.SampledPointCount};payloadBytes={densitySnapshot.EstimatedSampledPointBytes}");
            }

            var densityPeakSnapshot = repeatedDensitySession.Cache.GetSnapshot();
            var boundedPointUpperBound = repeatedDensityBudgets
                .OrderByDescending(static budget => budget)
                .Distinct()
                .Take(repeatedDensitySession.Cache.Capacity)
                .Select(static budget => (long)budget)
                .Sum();
            repeatedDensitySession.Clear();
            var managedBytesAfterDensityClear = GC.GetTotalMemory(forceFullCollection: true);
            var densityClearedSnapshot = repeatedDensitySession.Cache.GetSnapshot();
            Check(
                "repeated density load path keeps managed sampled payload bounded",
                densityObservations.Count == repeatedDensityBudgets.Length
                && densityPeakSnapshot.EntryCount == repeatedDensitySession.Cache.Capacity
                && densityPeakSnapshot.SampledPointCount <= boundedPointUpperBound
                && payloadBytesPeak > 0
                && sampledPointPeak > 0
                && !repeatedDensitySession.HasManagedData
                && densityClearedSnapshot.EntryCount == 0,
                $"loads={densityObservations.Count}/{repeatedDensityBudgets.Length};peakEntries={densityPeakSnapshot.EntryCount};capacity={densityPeakSnapshot.Capacity};peakSampledPoints={sampledPointPeak};peakPayloadBytes={payloadBytesPeak};upperBound={boundedPointUpperBound};managedBefore={managedBytesBeforeDensity};managedPeak={managedBytesPeak};managedAfterClear={managedBytesAfterDensityClear};observations={string.Join(',', densityObservations)}");

            var loadCache = new LazPointCloudSampleCache();
            using var loadCoordinator = new LazPointCloudLoadCoordinator(loadCache);
            var syncRequest = ViewerLazPointCloudLoadPreparation.Prepare(samplePath, 72);
            var syncFirst = loadCoordinator.Load(syncRequest);
            Check(
                "sync load coordinator decodes a fixture without WPF",
                syncFirst.PointCloud is not null && !syncFirst.Reused && !syncFirst.WasCanceled,
                $"loaded={syncFirst.PointCloud is not null};reused={syncFirst.Reused};cancelled={syncFirst.WasCanceled}");

            var syncSecond = loadCoordinator.Load(syncRequest);
            Check(
                "sync load coordinator reuses its injected cache",
                syncSecond.PointCloud is not null
                && syncSecond.Reused
                && !syncSecond.WasCanceled
                && ReferenceEquals(syncFirst.PointCloud, syncSecond.PointCloud),
                $"loaded={syncSecond.PointCloud is not null};reused={syncSecond.Reused};sameObject={ReferenceEquals(syncFirst.PointCloud, syncSecond.PointCloud)}");

            var asyncRequest = ViewerLazPointCloudLoadPreparation.Prepare(samplePath, 96);
            var asyncFirst = loadCoordinator
                .LoadAsync(asyncRequest)
                .GetAwaiter()
                .GetResult();
            Check(
                "async load coordinator decodes a fixture without WPF",
                asyncFirst is { PointCloud: not null, Reused: false, WasCanceled: false },
                $"loaded={asyncFirst?.PointCloud is not null};reused={asyncFirst?.Reused};cancelled={asyncFirst?.WasCanceled}");

            var asyncSecond = loadCoordinator
                .LoadAsync(asyncRequest)
                .GetAwaiter()
                .GetResult();
            Check(
                "async load coordinator reuses its injected cache",
                asyncSecond is { PointCloud: not null, Reused: true, WasCanceled: false }
                && ReferenceEquals(asyncFirst?.PointCloud, asyncSecond?.PointCloud),
                $"loaded={asyncSecond?.PointCloud is not null};reused={asyncSecond?.Reused};sameObject={ReferenceEquals(asyncFirst?.PointCloud, asyncSecond?.PointCloud)}");

            using var cancelledLoad = new CancellationTokenSource();
            cancelledLoad.Cancel();
            var cancelledRequest = ViewerLazPointCloudLoadPreparation.Prepare(samplePath, 97);
            var cancelled = loadCoordinator
                .LoadAsync(cancelledRequest, cancelledLoad.Token)
                .GetAwaiter()
                .GetResult();
            Check(
                "async load coordinator returns a cancellation outcome",
                cancelled is { PointCloud: null, WasCanceled: true },
                $"loaded={cancelled?.PointCloud is not null};cancelled={cancelled?.WasCanceled}");

            loadCoordinator.Dispose();
            var rejectedAfterLoadCoordinatorDispose = false;
            try
            {
                _ = loadCoordinator
                    .LoadAsync(asyncRequest)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (ObjectDisposedException)
            {
                rejectedAfterLoadCoordinatorDispose = true;
            }

            Check(
                "disposed async load coordinator rejects a new request",
                rejectedAfterLoadCoordinatorDispose,
                $"rejected={rejectedAfterLoadCoordinatorDispose}");
        }

        var succeeded = passed == total;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerLazPointCloudLoadCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}

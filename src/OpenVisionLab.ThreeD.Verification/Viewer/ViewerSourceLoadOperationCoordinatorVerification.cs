using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Loading;

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

            var loadCache = new LazPointCloudSampleCache();
            using var loadCoordinator = new LazPointCloudLoadCoordinator(loadCache);
            var syncFirst = loadCoordinator.Load(samplePath, 72);
            Check(
                "sync load coordinator decodes a fixture without WPF",
                syncFirst.PointCloud is not null && !syncFirst.Reused && !syncFirst.WasCanceled,
                $"loaded={syncFirst.PointCloud is not null};reused={syncFirst.Reused};cancelled={syncFirst.WasCanceled}");

            var syncSecond = loadCoordinator.Load(samplePath, 72);
            Check(
                "sync load coordinator reuses its injected cache",
                syncSecond.PointCloud is not null
                && syncSecond.Reused
                && !syncSecond.WasCanceled
                && ReferenceEquals(syncFirst.PointCloud, syncSecond.PointCloud),
                $"loaded={syncSecond.PointCloud is not null};reused={syncSecond.Reused};sameObject={ReferenceEquals(syncFirst.PointCloud, syncSecond.PointCloud)}");

            var asyncFirst = loadCoordinator
                .LoadAsync(samplePath, 96)
                .GetAwaiter()
                .GetResult();
            Check(
                "async load coordinator decodes a fixture without WPF",
                asyncFirst is { PointCloud: not null, Reused: false, WasCanceled: false },
                $"loaded={asyncFirst?.PointCloud is not null};reused={asyncFirst?.Reused};cancelled={asyncFirst?.WasCanceled}");

            var asyncSecond = loadCoordinator
                .LoadAsync(samplePath, 96)
                .GetAwaiter()
                .GetResult();
            Check(
                "async load coordinator reuses its injected cache",
                asyncSecond is { PointCloud: not null, Reused: true, WasCanceled: false }
                && ReferenceEquals(asyncFirst?.PointCloud, asyncSecond?.PointCloud),
                $"loaded={asyncSecond?.PointCloud is not null};reused={asyncSecond?.Reused};sameObject={ReferenceEquals(asyncFirst?.PointCloud, asyncSecond?.PointCloud)}");

            using var cancelledLoad = new CancellationTokenSource();
            cancelledLoad.Cancel();
            var cancelled = loadCoordinator
                .LoadAsync(samplePath, 97, cancelledLoad.Token)
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
                _ = loadCoordinator.LoadAsync(samplePath, 96).GetAwaiter().GetResult();
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

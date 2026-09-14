using System.Globalization;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

internal sealed record ViewerConsumerMemoryCycleResult(
    bool Loaded,
    bool SourceMatch,
    bool Disposed);

internal sealed record ViewerConsumerMemoryObservation(bool Passed, string Details);

/// <summary>
/// Aggregates process/native observations for repeated Viewer recreation.
/// It owns measurement policy and report formatting, not Viewer or WPF
/// lifecycle callbacks.
/// </summary>
internal sealed class ViewerConsumerMemoryObservationCoordinator
{
    private readonly int cycleCount;
    private readonly long baselinePrivateMemory;
    private readonly long baselineManagedMemory;
    private readonly NativeResourceSnapshot baselineNativeResources;
    private readonly long emptyWindowPrivateMemory;
    private readonly long emptyWindowManagedMemory;
    private readonly NativeResourceSnapshot emptyWindowNativeResources;
    private readonly long cleanProcessPrivateMemory;
    private readonly long cleanProcessManagedMemory;
    private readonly NativeResourceSnapshot cleanProcessNativeResources;
    private readonly Action<string> reportLineSink;

    public ViewerConsumerMemoryObservationCoordinator(
        int cycleCount,
        long baselinePrivateMemory,
        long baselineManagedMemory,
        NativeResourceSnapshot baselineNativeResources,
        long emptyWindowPrivateMemory,
        long emptyWindowManagedMemory,
        NativeResourceSnapshot emptyWindowNativeResources,
        long cleanProcessPrivateMemory,
        long cleanProcessManagedMemory,
        NativeResourceSnapshot cleanProcessNativeResources,
        Action<string> reportLineSink)
    {
        if (cycleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cycleCount));
        }

        ArgumentNullException.ThrowIfNull(reportLineSink);
        this.cycleCount = cycleCount;
        this.baselinePrivateMemory = baselinePrivateMemory;
        this.baselineManagedMemory = baselineManagedMemory;
        this.baselineNativeResources = baselineNativeResources;
        this.emptyWindowPrivateMemory = emptyWindowPrivateMemory;
        this.emptyWindowManagedMemory = emptyWindowManagedMemory;
        this.emptyWindowNativeResources = emptyWindowNativeResources;
        this.cleanProcessPrivateMemory = cleanProcessPrivateMemory;
        this.cleanProcessManagedMemory = cleanProcessManagedMemory;
        this.cleanProcessNativeResources = cleanProcessNativeResources;
        this.reportLineSink = reportLineSink;
    }

    public async Task<ViewerConsumerMemoryObservation> RunAsync(
        Func<int, Task<ViewerConsumerMemoryCycleResult>> executeCycle)
    {
        ArgumentNullException.ThrowIfNull(executeCycle);

        var cycleObservations = 0;
        var minimumPrivateMemory = long.MaxValue;
        var maximumPrivateMemory = 0L;
        var minimumManagedMemory = long.MaxValue;
        var maximumManagedMemory = 0L;
        var minimumNativeResources = baselineNativeResources;
        var maximumNativeResources = baselineNativeResources;
        for (var cycle = 1; cycle <= cycleCount; cycle++)
        {
            ViewerConsumerProcessResourceObserver.CollectForObservation();
            var beforePrivate = ViewerConsumerProcessResourceObserver.ReadPrivateMemoryBytes();
            var beforeManaged = ViewerConsumerProcessResourceObserver.ReadManagedMemoryBytes();
            var beforeNative = ViewerConsumerProcessResourceObserver.ReadNativeResources();
            minimumPrivateMemory = Math.Min(minimumPrivateMemory, beforePrivate);
            maximumPrivateMemory = Math.Max(maximumPrivateMemory, beforePrivate);
            minimumManagedMemory = Math.Min(minimumManagedMemory, beforeManaged);
            maximumManagedMemory = Math.Max(maximumManagedMemory, beforeManaged);
            minimumNativeResources = minimumNativeResources.Min(beforeNative);
            maximumNativeResources = maximumNativeResources.Max(beforeNative);

            var result = await executeCycle(cycle);

            ViewerConsumerProcessResourceObserver.CollectForObservation();
            var afterPrivate = ViewerConsumerProcessResourceObserver.ReadPrivateMemoryBytes();
            var afterManaged = ViewerConsumerProcessResourceObserver.ReadManagedMemoryBytes();
            var afterNative = ViewerConsumerProcessResourceObserver.ReadNativeResources();
            minimumPrivateMemory = Math.Min(minimumPrivateMemory, afterPrivate);
            maximumPrivateMemory = Math.Max(maximumPrivateMemory, afterPrivate);
            minimumManagedMemory = Math.Min(minimumManagedMemory, afterManaged);
            maximumManagedMemory = Math.Max(maximumManagedMemory, afterManaged);
            minimumNativeResources = minimumNativeResources.Min(afterNative);
            maximumNativeResources = maximumNativeResources.Max(afterNative);
            reportLineSink(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"RecreateCycle|index={cycle}|loaded={result.Loaded}|sourceMatch={result.SourceMatch}|dispose={result.Disposed}|privateBeforeBytes={beforePrivate}|privateAfterBytes={afterPrivate}|privateAfterMiB={afterPrivate / 1048576.0:F3}|managedBeforeBytes={beforeManaged}|managedAfterBytes={afterManaged}|managedAfterMiB={afterManaged / 1048576.0:F3}|nativeBefore={beforeNative}|nativeAfter={afterNative}"));
            if (result.Loaded && result.SourceMatch && result.Disposed)
            {
                cycleObservations++;
            }
        }

        var finalPrivateMemory = ViewerConsumerProcessResourceObserver.ReadPrivateMemoryBytes();
        var finalManagedMemory = ViewerConsumerProcessResourceObserver.ReadManagedMemoryBytes();
        var finalNativeResources = ViewerConsumerProcessResourceObserver.ReadNativeResources();
        reportLineSink(
            string.Create(
                CultureInfo.InvariantCulture,
                $"MemoryObservation|baselineAfterDataPrivateBytes={baselinePrivateMemory}|minimumCycleBeforePrivateBytes={minimumPrivateMemory}|maximumCycleObservedPrivateBytes={maximumPrivateMemory}|finalPrivateBytes={finalPrivateMemory}|privateDeltaFromBaselineMiB={(finalPrivateMemory - baselinePrivateMemory) / 1048576.0:F3}|emptyWindowPrivateBytes={emptyWindowPrivateMemory}|privateDeltaFromEmptyWindowMiB={(finalPrivateMemory - emptyWindowPrivateMemory) / 1048576.0:F3}|cleanProcessPrivateBytes={cleanProcessPrivateMemory}|privateDeltaFromCleanProcessMiB={(finalPrivateMemory - cleanProcessPrivateMemory) / 1048576.0:F3}|baselineAfterDataManagedBytes={baselineManagedMemory}|minimumCycleBeforeManagedBytes={minimumManagedMemory}|maximumCycleObservedManagedBytes={maximumManagedMemory}|finalManagedBytes={finalManagedMemory}|managedDeltaFromBaselineMiB={(finalManagedMemory - baselineManagedMemory) / 1048576.0:F3}|emptyWindowManagedBytes={emptyWindowManagedMemory}|managedDeltaFromEmptyWindowMiB={(finalManagedMemory - emptyWindowManagedMemory) / 1048576.0:F3}|cleanProcessManagedBytes={cleanProcessManagedMemory}|managedDeltaFromCleanProcessMiB={(finalManagedMemory - cleanProcessManagedMemory) / 1048576.0:F3}|baselineNative={baselineNativeResources}|minimumNativeObserved={minimumNativeResources}|maximumNativeObserved={maximumNativeResources}|finalNative={finalNativeResources}|nativeDelta={finalNativeResources.DeltaFrom(baselineNativeResources)}|emptyWindowNativeDelta={finalNativeResources.DeltaFrom(emptyWindowNativeResources)}|cleanProcessNativeDelta={finalNativeResources.DeltaFrom(cleanProcessNativeResources)}|interpretation=observation-only-no-leak-free-claim"));
        var passed = cycleObservations == cycleCount;
        return new ViewerConsumerMemoryObservation(
            passed,
            $"observed={cycleObservations}/{cycleCount}|privateDeltaMiB={(finalPrivateMemory - baselinePrivateMemory) / 1048576.0:F3}|managedDeltaMiB={(finalManagedMemory - baselineManagedMemory) / 1048576.0:F3}|nativeDelta={finalNativeResources.DeltaFrom(baselineNativeResources)}|emptyWindowNativeDelta={finalNativeResources.DeltaFrom(emptyWindowNativeResources)}");
    }
}

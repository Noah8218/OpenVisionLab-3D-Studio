namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Aggregates repeated Window-close observations without owning WPF Window or
/// Viewer lifecycle operations.
/// </summary>
internal sealed class ViewerConsumerWindowCloseObservationCoordinator
{
    private readonly int cycleCount;
    private readonly Func<NativeResourceSnapshot> readNativeResources;
    private readonly Action<string> reportLineSink;

    public ViewerConsumerWindowCloseObservationCoordinator(
        int cycleCount,
        Func<NativeResourceSnapshot> readNativeResources,
        Action<string> reportLineSink)
    {
        ArgumentNullException.ThrowIfNull(readNativeResources);
        ArgumentNullException.ThrowIfNull(reportLineSink);
        this.cycleCount = cycleCount;
        this.readNativeResources = readNativeResources;
        this.reportLineSink = reportLineSink;
    }

    public async Task<ViewerConsumerWindowCloseCyclesResult> RunAsync(
        Func<int, Task<ViewerConsumerWindowCloseCycleResult>> executeCycle)
    {
        ArgumentNullException.ThrowIfNull(executeCycle);

        var observed = 0;
        var first = readNativeResources();
        var last = first;
        for (var cycle = 1; cycle <= cycleCount; cycle++)
        {
            var observation = await executeCycle(cycle);
            if (observation.Passed)
            {
                observed++;
            }

            last = observation.After;
            reportLineSink($"WindowCloseCycle|index={cycle}|{Sanitize(observation.Details)}");
        }

        return new ViewerConsumerWindowCloseCyclesResult(
            observed,
            last.HandleCount - first.HandleCount,
            last.GdiObjects - first.GdiObjects,
            last.UserObjects - first.UserObjects);
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
}

internal sealed record ViewerConsumerWindowCloseCycleResult(
    bool Passed,
    NativeResourceSnapshot Before,
    NativeResourceSnapshot After,
    string Details);

internal sealed record ViewerConsumerWindowCloseCyclesResult(
    int Observed,
    long NativeHandleDelta,
    long GdiDelta,
    long UserDelta);

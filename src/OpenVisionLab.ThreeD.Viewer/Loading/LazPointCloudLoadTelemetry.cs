namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns LAZ/LAS reload-task and load-observation state for one Viewer.
/// The View retains progress/status projection and source application; this
/// owner keeps counters, density suppression, and task replacement explicit.
/// </summary>
internal sealed class LazPointCloudLoadTelemetry
{
    public Task ReloadTask { get; private set; } = Task.CompletedTask;

    public bool IsDensityReloadSuppressed { get; private set; }

    public int LoadRequestCount { get; private set; }

    public int DensityEventReloadCount { get; private set; }

    public int SmokeReloadCount { get; private set; }

    public int DecodeCount { get; private set; }

    public int CacheHitCount { get; private set; }

    public int CancellationCount { get; private set; }

    public int ProgressUpdateCount { get; private set; }

    public double LastProgress { get; private set; }

    public void SetDensityReloadSuppressed(bool suppressed) => IsDensityReloadSuppressed = suppressed;

    public Task RecordDensityEventReload(Func<Task> reloadFactory)
    {
        ArgumentNullException.ThrowIfNull(reloadFactory);
        DensityEventReloadCount++;
        ReloadTask = reloadFactory();
        return ReloadTask;
    }

    public Task RecordSmokeReload(Func<Task> reloadFactory)
    {
        ArgumentNullException.ThrowIfNull(reloadFactory);
        SmokeReloadCount++;
        ReloadTask = reloadFactory();
        return ReloadTask;
    }

    public void RecordLoadRequest() => LoadRequestCount++;

    public void RecordCacheHit() => CacheHitCount++;

    public void RecordDecode() => DecodeCount++;

    public void RecordCancellation() => CancellationCount++;

    public double RecordProgress(double value)
    {
        ProgressUpdateCount++;
        LastProgress = Math.Clamp(value, 0.0, 100.0);
        return LastProgress;
    }

    public void ClearReloadTask() => ReloadTask = Task.CompletedTask;
}

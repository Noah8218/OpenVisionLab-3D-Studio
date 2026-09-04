using System.Diagnostics;
using System.IO;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Coordinates LAZ/LAS decoding without depending on WPF or OpenGL. The
/// caller owns presentation of progress, status, and the decoded point cloud;
/// this owner only admits current data into the shared cache.
/// </summary>
internal sealed class LazPointCloudLoadCoordinator : IDisposable
{
    private readonly LazPointCloudSampleCache cache;
    private readonly ViewerSourceLoadOperationCoordinator operations = new();

    public LazPointCloudLoadCoordinator(LazPointCloudSampleCache cache)
    {
        this.cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public void CancelCurrent() => operations.CancelCurrent();

    public LazPointCloudLoadResult Load(string path, int maxSampledPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var candidate = Path.GetFullPath(path);
        var sampleLimit = Math.Max(2, maxSampledPoints);
        if (cache.TryGet(candidate, sampleLimit, out var cached))
        {
            return new LazPointCloudLoadResult(cached, 0.0, Reused: true, WasCanceled: false);
        }

        var loadStart = Stopwatch.GetTimestamp();
        var pointCloud = LazPointCloud.Load(candidate, sampleLimit);
        var loadMilliseconds = Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds;
        cache.Store(candidate, sampleLimit, pointCloud);
        return new LazPointCloudLoadResult(pointCloud, loadMilliseconds, Reused: false, WasCanceled: false);
    }

    public async Task<LazPointCloudLoadResult?> LoadAsync(
        string path,
        int maxSampledPoints,
        CancellationToken externalCancellationToken = default,
        IProgress<double>? progress = null,
        Func<bool>? isCurrent = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var candidate = Path.GetFullPath(path);
        var sampleLimit = Math.Max(2, maxSampledPoints);
        using var operation = operations.Begin(externalCancellationToken);
        bool IsOperationCurrent() => operation.IsCurrent && (isCurrent?.Invoke() ?? true);

        try
        {
            if (cache.TryGet(candidate, sampleLimit, out var cached))
            {
                if (!IsOperationCurrent())
                {
                    return null;
                }

                return new LazPointCloudLoadResult(cached, 0.0, Reused: true, WasCanceled: false);
            }

            var loadStart = Stopwatch.GetTimestamp();
            var decodeProgress = new Progress<double>(value =>
            {
                if (IsOperationCurrent())
                {
                    progress?.Report(value);
                }
            });
            var pointCloud = await Task.Run(
                () => LazPointCloud.Load(candidate, sampleLimit, operation.Token, decodeProgress),
                operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            if (!IsOperationCurrent())
            {
                return null;
            }

            var loadMilliseconds = Stopwatch.GetElapsedTime(loadStart).TotalMilliseconds;
            if (!operations.TryApply(
                    operation,
                    () => cache.Store(candidate, sampleLimit, pointCloud)))
            {
                operation.Token.ThrowIfCancellationRequested();
                return null;
            }

            if (!IsOperationCurrent())
            {
                return null;
            }

            return new LazPointCloudLoadResult(pointCloud, loadMilliseconds, Reused: false, WasCanceled: false);
        }
        catch (OperationCanceledException) when (operation.IsCancellationRequested)
        {
            return new LazPointCloudLoadResult(null, 0.0, Reused: false, WasCanceled: true);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            if (!IsOperationCurrent())
            {
                return null;
            }

            throw;
        }
    }

    public void Dispose() => operations.Dispose();
}

internal readonly record struct LazPointCloudLoadResult(
    LazPointCloud? PointCloud,
    double LoadMilliseconds,
    bool Reused,
    bool WasCanceled);

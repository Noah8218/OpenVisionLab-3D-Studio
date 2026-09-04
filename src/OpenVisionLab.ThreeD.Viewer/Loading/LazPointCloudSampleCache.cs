using System.IO;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns the Viewer-only LAZ/LAS sampled-point cache identity. One source path
/// may retain several sample budgets; changing the source invalidates the old
/// entries so a replaced control cannot keep unrelated point-cloud arrays.
/// </summary>
internal sealed class LazPointCloudSampleCache
{
    private readonly object syncRoot = new();
    private readonly Dictionary<int, LazPointCloud> samples = [];
    private string? sourcePath;

    public string? SourcePath
    {
        get
        {
            lock (syncRoot)
            {
                return sourcePath;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (syncRoot)
            {
                return samples.Count;
            }
        }
    }

    public bool HasEntries
    {
        get
        {
            lock (syncRoot)
            {
                return sourcePath is not null || samples.Count > 0;
            }
        }
    }

    public bool TryGet(
        string path,
        int maxSampledPoints,
        out LazPointCloud pointCloud)
    {
        var fullPath = Path.GetFullPath(path);
        lock (syncRoot)
        {
            if (string.Equals(sourcePath, fullPath, StringComparison.OrdinalIgnoreCase)
                && samples.TryGetValue(maxSampledPoints, out var cached))
            {
                pointCloud = cached;
                return true;
            }
        }

        pointCloud = null!;
        return false;
    }

    public void Store(
        string path,
        int maxSampledPoints,
        LazPointCloud pointCloud)
    {
        ArgumentNullException.ThrowIfNull(pointCloud);

        var fullPath = Path.GetFullPath(path);
        lock (syncRoot)
        {
            if (!string.Equals(sourcePath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                samples.Clear();
                sourcePath = fullPath;
            }

            samples[maxSampledPoints] = pointCloud;
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            samples.Clear();
            sourcePath = null;
        }
    }
}

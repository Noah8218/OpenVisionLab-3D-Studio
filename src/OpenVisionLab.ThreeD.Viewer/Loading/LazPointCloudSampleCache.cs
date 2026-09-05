using System.IO;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns the Viewer-only LAZ/LAS sampled-point cache identity. One source path
/// may retain a bounded set of recent sample budgets; changing the source
/// invalidates the old entries so a replaced control cannot keep unrelated
/// point-cloud arrays. The production default is three LRU entries, matching
/// the Viewer density choices.
/// </summary>
internal sealed class LazPointCloudSampleCache
{
    private const int DefaultCapacity = 3;
    private readonly object syncRoot = new();
    private readonly Dictionary<int, LazPointCloud> samples = [];
    private readonly LinkedList<int> recency = [];
    private readonly int capacity;
    private string? sourcePath;

    public LazPointCloudSampleCache(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        this.capacity = capacity;
    }

    public int Capacity => capacity;

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
                Touch(maxSampledPoints);
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
                recency.Clear();
                sourcePath = fullPath;
            }

            if (samples.ContainsKey(maxSampledPoints))
            {
                samples[maxSampledPoints] = pointCloud;
                Touch(maxSampledPoints);
                return;
            }

            if (samples.Count >= capacity && recency.First is { } leastRecent)
            {
                samples.Remove(leastRecent.Value);
                recency.RemoveFirst();
            }

            samples[maxSampledPoints] = pointCloud;
            recency.AddLast(maxSampledPoints);
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            samples.Clear();
            recency.Clear();
            sourcePath = null;
        }
    }

    private void Touch(int maxSampledPoints)
    {
        var node = recency.Find(maxSampledPoints);
        if (node is null)
        {
            recency.AddLast(maxSampledPoints);
            return;
        }

        recency.Remove(node);
        recency.AddLast(node);
    }
}

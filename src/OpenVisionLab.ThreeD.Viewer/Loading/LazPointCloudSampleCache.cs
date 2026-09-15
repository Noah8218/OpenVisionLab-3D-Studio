using System.IO;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns Viewer-only LAZ/LAS sampled-point cache identity and bounded managed
/// payload residency. A hit validates both the fast file fingerprint and the
/// exact content hash, so replacing a file at the same path cannot reuse an
/// old cloud. The byte budget covers unique managed sampled-point arrays only;
/// process, GC, native decoder, and GPU memory remain outside this owner.
/// </summary>
internal sealed class LazPointCloudSampleCache
{
    private const int DefaultCapacity = 3;
    private const long DefaultByteBudget = 256L * 1024 * 1024;
    private static readonly int SampledPointSizeBytes = Marshal.SizeOf<LazPointCloudPoint>();
    private readonly object syncRoot = new();
    private readonly Dictionary<int, LazPointCloud> samples = [];
    private readonly LinkedList<int> recency = [];
    private readonly int capacity;
    private readonly long byteBudget;
    private string? sourcePath;
    private LazPointCloudSourceIdentity? sourceIdentity;

    public LazPointCloudSampleCache(int capacity = DefaultCapacity, long byteBudget = DefaultByteBudget)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteBudget);
        this.capacity = capacity;
        this.byteBudget = byteBudget;
    }

    public int Capacity => capacity;

    public long ByteBudget => byteBudget;

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
                return samples.Count > 0;
            }
        }
    }

    /// <summary>
    /// Returns a locked snapshot of cache residency. The byte count covers
    /// unique managed sampled-point arrays and is bounded by <see
    /// cref="ByteBudget"/>; it is not a process, GC-heap, native, or GPU
    /// measurement.
    /// </summary>
    public LazPointCloudSampleCacheSnapshot GetSnapshot()
    {
        lock (syncRoot)
        {
            var sampledPointCount = GetUniqueSampledPointCountLocked();
            var identity = sourceIdentity;
            return new LazPointCloudSampleCacheSnapshot(
                sourcePath,
                capacity,
                byteBudget,
                samples.Count,
                sampledPointCount,
                checked(sampledPointCount * SampledPointSizeBytes),
                identity?.Length ?? 0,
                identity?.LastWriteUtcTicks ?? 0,
                identity?.ContentSha256);
        }
    }

    public bool TryGet(
        string path,
        int maxSampledPoints,
        out LazPointCloud pointCloud)
    {
        var fullPath = Path.GetFullPath(path);
        var currentIdentity = LazPointCloudSourceIdentity.Capture(fullPath);
        pointCloud = TryGetCore(fullPath, maxSampledPoints, currentIdentity)!;
        return pointCloud is not null;
    }

    /// <summary>
    /// Performs the exact source-identity lookup without blocking the caller
    /// thread on file hashing. The cancellation token covers the file stream
    /// read and is checked before any cache state is inspected or mutated.
    /// </summary>
    public async ValueTask<LazPointCloud?> TryGetAsync(
        string path,
        int maxSampledPoints,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(path);
        var currentIdentity = await LazPointCloudSourceIdentity
            .CaptureAsync(fullPath, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return TryGetCore(fullPath, maxSampledPoints, currentIdentity);
    }

    private LazPointCloud? TryGetCore(
        string fullPath,
        int maxSampledPoints,
        LazPointCloudSourceIdentity currentIdentity)
    {
        lock (syncRoot)
        {
            if (!string.Equals(sourcePath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (!currentIdentity.Exists
                || sourceIdentity is not { Exists: true } cachedIdentity
                || cachedIdentity != currentIdentity)
            {
                InvalidateEntriesLocked();
                sourceIdentity = currentIdentity;
                return null;
            }

            if (samples.TryGetValue(maxSampledPoints, out var cached))
            {
                Touch(maxSampledPoints);
                return cached;
            }
        }

        return null;
    }

    public void Store(
        string path,
        int maxSampledPoints,
        LazPointCloud pointCloud,
        LazPointCloudSourceIdentity? expectedIdentity = null,
        LazPointCloudSourceIdentity? observedIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(pointCloud);

        var fullPath = Path.GetFullPath(path);
        var identity = observedIdentity ?? LazPointCloudSourceIdentity.Capture(fullPath);
        if (expectedIdentity is { } expected && expected != identity)
        {
            throw new InvalidDataException(
                $"LAZ/LAS source changed between decode admission and cache store: {fullPath}");
        }

        lock (syncRoot)
        {
            if (!string.Equals(sourcePath, fullPath, StringComparison.OrdinalIgnoreCase)
                || sourceIdentity is not { } previousIdentity
                || previousIdentity != identity)
            {
                InvalidateEntriesLocked();
                sourcePath = fullPath;
                sourceIdentity = identity;
            }

            samples[maxSampledPoints] = pointCloud;
            Touch(maxSampledPoints);
            TrimToBoundsLocked();
        }
    }

    public void Clear()
    {
        lock (syncRoot)
        {
            InvalidateEntriesLocked();
            sourcePath = null;
            sourceIdentity = null;
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

    private void TrimToBoundsLocked()
    {
        while (samples.Count > capacity || GetUniqueSampledPointBytesLocked() > byteBudget)
        {
            if (recency.First is not { } leastRecent)
            {
                break;
            }

            samples.Remove(leastRecent.Value);
            recency.RemoveFirst();
        }
    }

    private void InvalidateEntriesLocked()
    {
        samples.Clear();
        recency.Clear();
    }

    private long GetUniqueSampledPointCountLocked()
    {
        long sampledPointCount = 0;
        var uniquePointClouds = new HashSet<LazPointCloud>(ReferenceEqualityComparer.Instance);
        foreach (var pointCloud in samples.Values)
        {
            if (uniquePointClouds.Add(pointCloud))
            {
                sampledPointCount = checked(sampledPointCount + pointCloud.SampledPointView.Count);
            }
        }

        return sampledPointCount;
    }

    private long GetUniqueSampledPointBytesLocked() =>
        checked(GetUniqueSampledPointCountLocked() * SampledPointSizeBytes);
}

internal readonly record struct LazPointCloudSourceIdentity(
    bool Exists,
    long Length,
    long LastWriteUtcTicks,
    string ContentSha256)
{
    public static LazPointCloudSourceIdentity Capture(string fullPath)
    {
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            return new LazPointCloudSourceIdentity(false, 0, 0, string.Empty);
        }

        using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 128 * 1024,
            options: FileOptions.SequentialScan);
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        fileInfo.Refresh();
        return new LazPointCloudSourceIdentity(
            true,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc.Ticks,
            hash);
    }

    public static async ValueTask<LazPointCloudSourceIdentity> CaptureAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var fileInfo = new FileInfo(fullPath);
        if (!fileInfo.Exists)
        {
            return new LazPointCloudSourceIdentity(false, 0, 0, string.Empty);
        }

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = Convert.ToHexString(
            await CryptographicOperations.HashDataAsync(
                HashAlgorithmName.SHA256,
                stream,
                cancellationToken)
            .ConfigureAwait(false));
        cancellationToken.ThrowIfCancellationRequested();
        fileInfo.Refresh();
        return new LazPointCloudSourceIdentity(
            true,
            fileInfo.Length,
            fileInfo.LastWriteTimeUtc.Ticks,
            hash);
    }
}

internal readonly record struct LazPointCloudSampleCacheSnapshot(
    string? SourcePath,
    int Capacity,
    long ByteBudget,
    int EntryCount,
    long SampledPointCount,
    long EstimatedSampledPointBytes,
    long SourceByteLength,
    long SourceLastWriteUtcTicks,
    string? SourceContentSha256);

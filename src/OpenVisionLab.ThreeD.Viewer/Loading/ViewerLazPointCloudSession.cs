using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns the Viewer LAZ/LAS data session: the current snapshot, the bounded
/// sampled-point cache, and the decode operation lifetime. WPF and OpenGL
/// presentation remain with the control; this owner only defines data
/// ownership and release order.
/// </summary>
internal sealed class ViewerLazPointCloudSession : IDisposable
{
    public ViewerLazPointCloudSession()
    {
        Cache = new LazPointCloudSampleCache();
        LoadCoordinator = new LazPointCloudLoadCoordinator(Cache);
    }

    public ViewerLazPointCloudState State { get; } = new();

    public LazPointCloudSampleCache Cache { get; }

    public LazPointCloudLoadCoordinator LoadCoordinator { get; }

    public bool HasManagedData =>
        State.Metadata is not null
        || State.PointCloud is not null
        || Cache.HasEntries;

    public void Clear()
    {
        State.Clear();
        Cache.Clear();
    }

    public void Dispose()
    {
        LoadCoordinator.Dispose();
        Clear();
    }
}

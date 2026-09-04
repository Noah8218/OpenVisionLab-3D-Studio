using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// Owns the current LAZ/LAS source snapshot used by the Viewer.
/// It deliberately stores data and the source-to-scene transform only;
/// selection, rendering, and WPF status remain with their existing owners.
/// </summary>
internal sealed class ViewerLazPointCloudState
{
    public LazPointCloudMetadata? Metadata { get; set; }

    public LazPointCloud? PointCloud { get; set; }

    public LazSceneTransform SceneTransform { get; set; }

    public void SetPointCloud(LazPointCloud pointCloud)
    {
        ArgumentNullException.ThrowIfNull(pointCloud);
        PointCloud = pointCloud;
        Metadata = pointCloud.Metadata;
    }

    public void Clear()
    {
        Metadata = null;
        PointCloud = null;
        SceneTransform = default;
    }
}

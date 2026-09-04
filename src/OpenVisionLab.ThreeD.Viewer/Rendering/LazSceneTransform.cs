using System.Numerics;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Maps LAS/LAZ source coordinates into the Viewer scene frame. Source X/Y/Z
/// are centered at the metadata bounds midpoint and displayed as scene X/Z/Y;
/// raw source units are preserved and are not interpreted as millimetres.
/// </summary>
internal readonly record struct LazSceneTransform(
    double OriginX,
    double OriginY,
    double OriginZ)
{
    public static LazSceneTransform FromMetadata(LazPointCloudMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new LazSceneTransform(
            (metadata.MinX + metadata.MaxX) * 0.5,
            (metadata.MinY + metadata.MaxY) * 0.5,
            (metadata.MinZ + metadata.MaxZ) * 0.5);
    }

    public Vector3 Map(Vector3 source) => Map(source.X, source.Y, source.Z);

    public Vector3 Map(double x, double y, double z) =>
        new((float)(x - OriginX), (float)(z - OriginZ), (float)(y - OriginY));

    public Vector3[] CreateBoundsCorners(LazPointCloudMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return
        [
            Map(metadata.MinX, metadata.MinY, metadata.MinZ),
            Map(metadata.MaxX, metadata.MinY, metadata.MinZ),
            Map(metadata.MaxX, metadata.MaxY, metadata.MinZ),
            Map(metadata.MinX, metadata.MaxY, metadata.MinZ),
            Map(metadata.MinX, metadata.MinY, metadata.MaxZ),
            Map(metadata.MaxX, metadata.MinY, metadata.MaxZ),
            Map(metadata.MaxX, metadata.MaxY, metadata.MaxZ),
            Map(metadata.MinX, metadata.MaxY, metadata.MaxZ)
        ];
    }
}

using System.Numerics;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Provides deterministic, WPF/OpenGL-neutral geometry rules for the
/// Height-Deviation ROI pair. The Viewer owns the editable state; this policy
/// owns only the value calculations and predicates.
/// </summary>
internal static class HeightDeviationRoiGeometry
{
    public static HeightDeviationRecipeRoiRegion FromBounds(HeightDeviationRoiBounds bounds) =>
        new(
            (bounds.MinX + bounds.MaxX) * 0.5,
            (bounds.MinZ + bounds.MaxZ) * 0.5,
            Math.Max(0.0001, (bounds.MaxX - bounds.MinX) * 0.5),
            Math.Max(0.0001, (bounds.MaxZ - bounds.MinZ) * 0.5));

    public static HeightDeviationRecipeRoiRegion Offset(
        HeightDeviationRecipeRoiRegion region,
        double offsetX,
        double offsetZ) =>
        new(region.CenterX + offsetX, region.CenterZ + offsetZ, region.HalfWidth, region.HalfDepth);

    public static bool IsValid(HeightDeviationRecipeRoiRegion region) =>
        double.IsFinite(region.CenterX)
        && double.IsFinite(region.CenterZ)
        && double.IsFinite(region.HalfWidth)
        && double.IsFinite(region.HalfDepth)
        && region.HalfWidth > 0.0
        && region.HalfDepth > 0.0;

    public static bool Overlaps(
        HeightDeviationRecipeRoiRegion left,
        HeightDeviationRecipeRoiRegion right) =>
        Math.Abs(left.CenterX - right.CenterX) < left.HalfWidth + right.HalfWidth
        && Math.Abs(left.CenterZ - right.CenterZ) < left.HalfDepth + right.HalfDepth;

    public static bool Intersects(
        HeightDeviationRecipeRoiRegion region,
        HeightDeviationRoiBounds bounds) =>
        region.CenterX + region.HalfWidth >= bounds.MinX
        && region.CenterX - region.HalfWidth <= bounds.MaxX
        && region.CenterZ + region.HalfDepth >= bounds.MinZ
        && region.CenterZ - region.HalfDepth <= bounds.MaxZ;

    public static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;
}

internal readonly record struct HeightDeviationRoiBounds(
    float MinX,
    float MaxX,
    float MinZ,
    float MaxZ);

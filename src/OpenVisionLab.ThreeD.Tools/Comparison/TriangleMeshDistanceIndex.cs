using System.Numerics;
using Noah = Lib.ThreeD.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Tools;

public readonly record struct MeshTriangle(long SourceTriangleIndex, Vector3 A, Vector3 B, Vector3 C);

public enum MeshClosestFeature
{
    FaceInterior,
    Edge,
    Vertex
}

public readonly record struct PointMeshDistance(
    long SourceTriangleIndex,
    Vector3 ClosestPoint,
    Vector3 TriangleNormal,
    MeshClosestFeature ClosestFeature,
    double UnsignedDistance,
    double? SignedDistance,
    bool SignResolved);

public sealed class TriangleMeshDistanceIndex
{
    public const double RobustSignDistanceEpsilon = Noah.TriangleMeshDistanceTool.RobustSignDistanceEpsilon;

    private readonly Noah.TriangleMeshDistanceTool _tool;

    public TriangleMeshDistanceIndex(IReadOnlyList<MeshTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        if (triangles.Count == 0)
        {
            throw new ArgumentException(
                "A distance index requires at least one triangle.",
                nameof(triangles));
        }

        _tool = new Noah.TriangleMeshDistanceTool(
            triangles.Select(ToNoah).ToArray());
    }

    public int TriangleCount => _tool.TriangleCount;

    public PointMeshDistance FindClosest(Vector3 point) =>
        ToStudio(_tool.Execute(ToNoah(point)));

    public PointMeshDistance ResolveRobustSign(Vector3 point, double nearestUnsignedDistance) =>
        ToStudio(_tool.ExecuteRobustSign(ToNoah(point), nearestUnsignedDistance));

    private static Noah.MeshTriangle ToNoah(MeshTriangle triangle) =>
        new(
            triangle.SourceTriangleIndex,
            ToNoah(triangle.A),
            ToNoah(triangle.B),
            ToNoah(triangle.C));

    private static Noah.ThreeDPoint ToNoah(Vector3 point) =>
        new(point.X, point.Y, point.Z);

    private static PointMeshDistance ToStudio(Noah.PointMeshDistance distance) =>
        new(
            distance.SourceTriangleIndex,
            ToStudio(distance.ClosestPoint),
            ToStudio(distance.TriangleNormal),
            distance.ClosestFeature switch
            {
                Noah.MeshClosestFeature.FaceInterior => MeshClosestFeature.FaceInterior,
                Noah.MeshClosestFeature.Edge => MeshClosestFeature.Edge,
                Noah.MeshClosestFeature.Vertex => MeshClosestFeature.Vertex,
                _ => throw new InvalidDataException($"Unsupported Noah closest feature: {distance.ClosestFeature}")
            },
            distance.UnsignedDistance,
            distance.SignedDistance,
            distance.SignResolved);

    private static Vector3 ToStudio(Noah.ThreeDPoint point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);
}

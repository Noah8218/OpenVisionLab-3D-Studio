using System.Numerics;
using Sdk = OpenVisionLab.Vision3D.FeatureExtraction;

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
    public const double RobustSignDistanceEpsilon = Sdk.TriangleMeshDistanceTool.RobustSignDistanceEpsilon;

    private readonly Sdk.TriangleMeshDistanceTool _tool;

    public TriangleMeshDistanceIndex(IReadOnlyList<MeshTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        if (triangles.Count == 0)
        {
            throw new ArgumentException(
                "A distance index requires at least one triangle.",
                nameof(triangles));
        }

        _tool = new Sdk.TriangleMeshDistanceTool(
            triangles.Select(ToSdk).ToArray());
    }

    public int TriangleCount => _tool.TriangleCount;

    public PointMeshDistance FindClosest(Vector3 point) =>
        ToStudio(_tool.Execute(ToSdk(point)));

    public PointMeshDistance ResolveRobustSign(Vector3 point, double nearestUnsignedDistance) =>
        ToStudio(_tool.ExecuteRobustSign(ToSdk(point), nearestUnsignedDistance));

    private static Sdk.MeshTriangle ToSdk(MeshTriangle triangle) =>
        new(
            triangle.SourceTriangleIndex,
            ToSdk(triangle.A),
            ToSdk(triangle.B),
            ToSdk(triangle.C));

    private static Sdk.ThreeDPoint ToSdk(Vector3 point) =>
        new(point.X, point.Y, point.Z);

    private static PointMeshDistance ToStudio(Sdk.PointMeshDistance distance) =>
        new(
            distance.SourceTriangleIndex,
            ToStudio(distance.ClosestPoint),
            ToStudio(distance.TriangleNormal),
            distance.ClosestFeature switch
            {
                Sdk.MeshClosestFeature.FaceInterior => MeshClosestFeature.FaceInterior,
                Sdk.MeshClosestFeature.Edge => MeshClosestFeature.Edge,
                Sdk.MeshClosestFeature.Vertex => MeshClosestFeature.Vertex,
                _ => throw new InvalidDataException($"Unsupported Sdk closest feature: {distance.ClosestFeature}")
            },
            distance.UnsignedDistance,
            distance.SignedDistance,
            distance.SignResolved);

    private static Vector3 ToStudio(Sdk.ThreeDPoint point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);
}

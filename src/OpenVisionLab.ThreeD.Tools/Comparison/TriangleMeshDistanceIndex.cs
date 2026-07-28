using System.Numerics;

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
    private const int LeafTriangleCount = 8;
    public const double RobustSignDistanceEpsilon = 1.1920928955078125e-7;

    private readonly TriangleEntry[] _triangles;
    private readonly Node _root;

    public TriangleMeshDistanceIndex(IReadOnlyList<MeshTriangle> triangles)
    {
        ArgumentNullException.ThrowIfNull(triangles);
        if (triangles.Count == 0)
        {
            throw new ArgumentException("A distance index requires at least one triangle.", nameof(triangles));
        }

        _triangles = new TriangleEntry[triangles.Count];
        for (var index = 0; index < triangles.Count; index++)
        {
            _triangles[index] = CreateEntry(triangles[index]);
        }

        _root = BuildNode(0, _triangles.Length);
    }

    public int TriangleCount => _triangles.Length;

    public PointMeshDistance FindClosest(Vector3 point)
    {
        if (!IsFinite(point))
        {
            throw new ArgumentException("The query point must contain finite coordinates.", nameof(point));
        }

        var best = new SearchResult(double.PositiveInfinity, long.MaxValue, default, default);
        Search(_root, point, ref best);

        var unsignedDistance = Math.Sqrt(Math.Max(0.0, best.DistanceSquared));
        double? signedDistance = null;
        var signResolved = best.Closest.Feature == MeshClosestFeature.FaceInterior;
        // Edge and vertex signs need topology-aware adjacency; an unresolved sign is safer than a guessed one.
        if (signResolved)
        {
            var side = Dot(point - best.Closest.Point, best.Triangle.Normal);
            if (unsignedDistance == 0.0)
            {
                signedDistance = 0.0;
            }
            else if (side == 0.0)
            {
                signResolved = false;
            }
            else
            {
                signedDistance = Math.CopySign(unsignedDistance, side);
            }
        }

        return new PointMeshDistance(
            best.Triangle.Source.SourceTriangleIndex,
            best.Closest.Point,
            best.Triangle.Normal,
            best.Closest.Feature,
            unsignedDistance,
            signedDistance,
            signResolved);
    }

    public PointMeshDistance ResolveRobustSign(Vector3 point, double nearestUnsignedDistance)
    {
        if (!IsFinite(point))
        {
            throw new ArgumentException("The query point must contain finite coordinates.", nameof(point));
        }

        if (!double.IsFinite(nearestUnsignedDistance) || nearestUnsignedDistance < 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nearestUnsignedDistance),
                "The nearest unsigned distance must be finite and non-negative.");
        }

        var maximumCandidateDistance = nearestUnsignedDistance + RobustSignDistanceEpsilon;
        var state = new RobustSearchState(maximumCandidateDistance);
        SearchRobustCandidates(_root, point, ref state);
        var selected = state.BestInterior ?? state.BestBoundary
            ?? throw new InvalidOperationException("No robust sign candidate was found within the nearest-distance tolerance.");
        var side = Dot(point - selected.Closest.Point, selected.Triangle.Normal);
        var signedDistance = selected.Distance == 0.0
            ? 0.0
            : side < 0.0
                ? -selected.Distance
                : selected.Distance;
        return new PointMeshDistance(
            selected.Triangle.Source.SourceTriangleIndex,
            selected.Closest.Point,
            selected.Triangle.Normal,
            selected.Closest.Feature,
            selected.Distance,
            signedDistance,
            SignResolved: true);
    }

    private Node BuildNode(int start, int count)
    {
        var (minimum, maximum) = CalculateBounds(start, count);
        if (count <= LeafTriangleCount)
        {
            return new Node(minimum, maximum, start, count, null, null);
        }

        var (centroidMinimum, centroidMaximum) = CalculateCentroidBounds(start, count);
        var span = centroidMaximum - centroidMinimum;
        var axis = span.X >= span.Y && span.X >= span.Z ? 0 : span.Y >= span.Z ? 1 : 2;
        Array.Sort(_triangles, start, count, CentroidComparer.ForAxis(axis));

        var leftCount = count / 2;
        var left = BuildNode(start, leftCount);
        var right = BuildNode(start + leftCount, count - leftCount);
        return new Node(minimum, maximum, start, count, left, right);
    }

    private void Search(Node node, Vector3 point, ref SearchResult best)
    {
        if (DistanceSquaredToBounds(point, node.Minimum, node.Maximum) > best.DistanceSquared)
        {
            return;
        }

        if (node.Left is null || node.Right is null)
        {
            var end = node.Start + node.Count;
            for (var index = node.Start; index < end; index++)
            {
                var triangle = _triangles[index];
                var closest = FindClosestPoint(point, triangle.Source);
                var distanceSquared = DistanceSquared(point, closest.Point);
                if (distanceSquared < best.DistanceSquared
                    || (distanceSquared == best.DistanceSquared
                        && triangle.Source.SourceTriangleIndex < best.SourceTriangleIndex))
                {
                    best = new SearchResult(
                        distanceSquared,
                        triangle.Source.SourceTriangleIndex,
                        triangle,
                        closest);
                }
            }

            return;
        }

        var leftDistance = DistanceSquaredToBounds(point, node.Left.Minimum, node.Left.Maximum);
        var rightDistance = DistanceSquaredToBounds(point, node.Right.Minimum, node.Right.Maximum);
        if (leftDistance <= rightDistance)
        {
            Search(node.Left, point, ref best);
            Search(node.Right, point, ref best);
        }
        else
        {
            Search(node.Right, point, ref best);
            Search(node.Left, point, ref best);
        }
    }

    private void SearchRobustCandidates(Node node, Vector3 point, ref RobustSearchState state)
    {
        if (DistanceSquaredToBounds(point, node.Minimum, node.Maximum) > state.MaximumDistanceSquared)
        {
            return;
        }

        if (node.Left is null || node.Right is null)
        {
            var end = node.Start + node.Count;
            for (var index = node.Start; index < end; index++)
            {
                var triangle = _triangles[index];
                var closest = FindClosestPoint(point, triangle.Source);
                var distance = Math.Sqrt(Math.Max(0.0, DistanceSquared(point, closest.Point)));
                if (distance > state.MaximumDistance)
                {
                    continue;
                }

                var orthogonality = distance == 0.0
                    ? 1.0
                    : Math.Min(1.0, Math.Abs(Dot(point - closest.Point, triangle.Normal)) / distance);
                state.Consider(new RobustCandidate(triangle, closest, distance, orthogonality));
            }

            return;
        }

        SearchRobustCandidates(node.Left, point, ref state);
        SearchRobustCandidates(node.Right, point, ref state);
    }

    private (Vector3 Minimum, Vector3 Maximum) CalculateBounds(int start, int count)
    {
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        var end = start + count;
        for (var index = start; index < end; index++)
        {
            minimum = Vector3.Min(minimum, _triangles[index].Minimum);
            maximum = Vector3.Max(maximum, _triangles[index].Maximum);
        }

        return (minimum, maximum);
    }

    private (Vector3 Minimum, Vector3 Maximum) CalculateCentroidBounds(int start, int count)
    {
        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        var end = start + count;
        for (var index = start; index < end; index++)
        {
            minimum = Vector3.Min(minimum, _triangles[index].Centroid);
            maximum = Vector3.Max(maximum, _triangles[index].Centroid);
        }

        return (minimum, maximum);
    }

    private static TriangleEntry CreateEntry(MeshTriangle triangle)
    {
        if (!IsFinite(triangle.A) || !IsFinite(triangle.B) || !IsFinite(triangle.C))
        {
            throw new ArgumentException(
                $"Triangle {triangle.SourceTriangleIndex} contains a non-finite coordinate.",
                nameof(triangle));
        }

        var cross = Vector3.Cross(triangle.B - triangle.A, triangle.C - triangle.A);
        var crossLengthSquared = Dot(cross, cross);
        if (!double.IsFinite(crossLengthSquared) || crossLengthSquared <= 0.0)
        {
            throw new ArgumentException(
                $"Triangle {triangle.SourceTriangleIndex} is degenerate.",
                nameof(triangle));
        }

        var normal = cross / (float)Math.Sqrt(crossLengthSquared);
        var minimum = Vector3.Min(triangle.A, Vector3.Min(triangle.B, triangle.C));
        var maximum = Vector3.Max(triangle.A, Vector3.Max(triangle.B, triangle.C));
        var centroid = new Vector3(
            (float)(((double)triangle.A.X + triangle.B.X + triangle.C.X) / 3.0),
            (float)(((double)triangle.A.Y + triangle.B.Y + triangle.C.Y) / 3.0),
            (float)(((double)triangle.A.Z + triangle.B.Z + triangle.C.Z) / 3.0));
        return new TriangleEntry(triangle, minimum, maximum, centroid, normal);
    }

    private static ClosestPointResult FindClosestPoint(Vector3 point, MeshTriangle triangle)
    {
        var ab = triangle.B - triangle.A;
        var ac = triangle.C - triangle.A;
        var ap = point - triangle.A;
        var d1 = Dot(ab, ap);
        var d2 = Dot(ac, ap);
        if (d1 <= 0.0 && d2 <= 0.0)
        {
            return new ClosestPointResult(triangle.A, MeshClosestFeature.Vertex);
        }

        var bp = point - triangle.B;
        var d3 = Dot(ab, bp);
        var d4 = Dot(ac, bp);
        if (d3 >= 0.0 && d4 <= d3)
        {
            return new ClosestPointResult(triangle.B, MeshClosestFeature.Vertex);
        }

        var vc = d1 * d4 - d3 * d2;
        if (vc <= 0.0 && d1 >= 0.0 && d3 <= 0.0)
        {
            var scale = d1 / (d1 - d3);
            return new ClosestPointResult(triangle.A + (float)scale * ab, MeshClosestFeature.Edge);
        }

        var cp = point - triangle.C;
        var d5 = Dot(ab, cp);
        var d6 = Dot(ac, cp);
        if (d6 >= 0.0 && d5 <= d6)
        {
            return new ClosestPointResult(triangle.C, MeshClosestFeature.Vertex);
        }

        var vb = d5 * d2 - d1 * d6;
        if (vb <= 0.0 && d2 >= 0.0 && d6 <= 0.0)
        {
            var scale = d2 / (d2 - d6);
            return new ClosestPointResult(triangle.A + (float)scale * ac, MeshClosestFeature.Edge);
        }

        var va = d3 * d6 - d5 * d4;
        if (va <= 0.0 && d4 - d3 >= 0.0 && d5 - d6 >= 0.0)
        {
            var scale = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return new ClosestPointResult(triangle.B + (float)scale * (triangle.C - triangle.B), MeshClosestFeature.Edge);
        }

        var denominator = 1.0 / (va + vb + vc);
        var v = vb * denominator;
        var w = vc * denominator;
        return new ClosestPointResult(
            triangle.A + (float)v * ab + (float)w * ac,
            MeshClosestFeature.FaceInterior);
    }

    private static double DistanceSquared(Vector3 first, Vector3 second)
    {
        var x = (double)first.X - second.X;
        var y = (double)first.Y - second.Y;
        var z = (double)first.Z - second.Z;
        return x * x + y * y + z * z;
    }

    private static double DistanceSquaredToBounds(Vector3 point, Vector3 minimum, Vector3 maximum)
    {
        var x = AxisDistance(point.X, minimum.X, maximum.X);
        var y = AxisDistance(point.Y, minimum.Y, maximum.Y);
        var z = AxisDistance(point.Z, minimum.Z, maximum.Z);
        return x * x + y * y + z * z;
    }

    private static double AxisDistance(float value, float minimum, float maximum) =>
        value < minimum ? minimum - (double)value : value > maximum ? value - (double)maximum : 0.0;

    private static double Dot(Vector3 first, Vector3 second) =>
        (double)first.X * second.X + (double)first.Y * second.Y + (double)first.Z * second.Z;

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private sealed record Node(
        Vector3 Minimum,
        Vector3 Maximum,
        int Start,
        int Count,
        Node? Left,
        Node? Right);

    private readonly record struct TriangleEntry(
        MeshTriangle Source,
        Vector3 Minimum,
        Vector3 Maximum,
        Vector3 Centroid,
        Vector3 Normal);

    private readonly record struct ClosestPointResult(Vector3 Point, MeshClosestFeature Feature);

    private readonly record struct SearchResult(
        double DistanceSquared,
        long SourceTriangleIndex,
        TriangleEntry Triangle,
        ClosestPointResult Closest);

    private readonly record struct RobustCandidate(
        TriangleEntry Triangle,
        ClosestPointResult Closest,
        double Distance,
        double Orthogonality);

    private struct RobustSearchState(double maximumDistance)
    {
        public readonly double MaximumDistance = maximumDistance;
        public readonly double MaximumDistanceSquared = maximumDistance * maximumDistance;
        public RobustCandidate? BestInterior;
        public RobustCandidate? BestBoundary;

        public void Consider(RobustCandidate candidate)
        {
            if (candidate.Closest.Feature == MeshClosestFeature.FaceInterior)
            {
                if (BestInterior is not { } current
                    || candidate.Distance < current.Distance
                    || (candidate.Distance == current.Distance
                        && candidate.Triangle.Source.SourceTriangleIndex < current.Triangle.Source.SourceTriangleIndex))
                {
                    BestInterior = candidate;
                }

                return;
            }

            if (BestBoundary is not { } boundary)
            {
                BestBoundary = candidate;
                return;
            }

            var distanceDifference = candidate.Distance - boundary.Distance;
            if (Math.Abs(distanceDifference) <= RobustSignDistanceEpsilon)
            {
                if (candidate.Orthogonality > boundary.Orthogonality
                    || (candidate.Orthogonality == boundary.Orthogonality
                        && candidate.Triangle.Source.SourceTriangleIndex < boundary.Triangle.Source.SourceTriangleIndex))
                {
                    BestBoundary = candidate;
                }
            }
            else if (distanceDifference < 0.0)
            {
                BestBoundary = candidate;
            }
        }
    }

    private sealed class CentroidComparer(int axis) : IComparer<TriangleEntry>
    {
        private static readonly CentroidComparer X = new(0);
        private static readonly CentroidComparer Y = new(1);
        private static readonly CentroidComparer Z = new(2);

        public static CentroidComparer ForAxis(int axis) => axis switch
        {
            0 => X,
            1 => Y,
            _ => Z
        };

        public int Compare(TriangleEntry first, TriangleEntry second)
        {
            var comparison = GetAxis(first.Centroid).CompareTo(GetAxis(second.Centroid));
            return comparison != 0
                ? comparison
                : first.Source.SourceTriangleIndex.CompareTo(second.Source.SourceTriangleIndex);
        }

        private float GetAxis(Vector3 value) => axis switch
        {
            0 => value.X,
            1 => value.Y,
            _ => value.Z
        };
    }
}

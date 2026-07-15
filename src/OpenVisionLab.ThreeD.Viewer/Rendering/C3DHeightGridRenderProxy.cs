using System.IO;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

internal sealed class C3DHeightGridRenderProxy
{
    public const int SurfaceEdgeSampleInterval = 4;

    private C3DHeightGridRenderProxy(
        HeightGridPoint[] points,
        int[] triangleIndices,
        int[] edgeIndices,
        int[] gridEdgeIndices,
        int[] surfaceEdgeIndices)
    {
        Points = points;
        TriangleIndices = triangleIndices;
        EdgeIndices = edgeIndices;
        GridEdgeIndices = gridEdgeIndices;
        SurfaceEdgeIndices = surfaceEdgeIndices;
    }

    public HeightGridPoint[] Points { get; }

    public int[] TriangleIndices { get; }

    public int[] EdgeIndices { get; }

    public int[] GridEdgeIndices { get; }

    public int[] SurfaceEdgeIndices { get; }

    public int TriangleCount => TriangleIndices.Length / 3;

    public int EdgeCount => EdgeIndices.Length / 2;

    public int GridEdgeCount => GridEdgeIndices.Length / 2;

    public int SurfaceEdgeCount => SurfaceEdgeIndices.Length / 2;

    public bool HasSurface => TriangleIndices.Length > 0;

    public static C3DHeightGridRenderProxy Create(C3DHeightGrid grid)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return Create(grid.Points, grid.PointStride);
    }

    internal static C3DHeightGridRenderProxy Create(
        IReadOnlyList<HeightGridPoint> sourcePoints,
        int pointStride)
    {
        ArgumentNullException.ThrowIfNull(sourcePoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pointStride);

        var points = sourcePoints as HeightGridPoint[] ?? sourcePoints.ToArray();
        var pointIndices = new Dictionary<(int Row, int Column), int>(points.Length);
        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            if (point.Row < 0 || point.Column < 0)
            {
                throw new InvalidDataException("C3D display topology requires non-negative source cells.");
            }

            if (!pointIndices.TryAdd((point.Row, point.Column), index))
            {
                throw new InvalidDataException(
                    $"C3D display topology contains duplicate source cell ({point.Row}, {point.Column}).");
            }
        }

        var triangleIndices = new List<int>();
        var edgeIndices = new List<int>();
        var gridEdgeIndices = new List<int>();
        var surfaceEdgeIndices = new List<int>();
        var uniqueEdges = new HashSet<long>();
        var uniqueGridEdges = new HashSet<long>();
        var uniqueSurfaceEdges = new HashSet<long>();
        var minimumRow = points.Length == 0 ? 0 : points.Min(point => point.Row);
        var maximumRow = points.Length == 0 ? 0 : points.Max(point => point.Row);
        var minimumColumn = points.Length == 0 ? 0 : points.Min(point => point.Column);
        var maximumColumn = points.Length == 0 ? 0 : points.Max(point => point.Column);
        for (var topLeft = 0; topLeft < points.Length; topLeft++)
        {
            var point = points[topLeft];
            if (!pointIndices.TryGetValue((point.Row, point.Column + pointStride), out var topRight)
                || !pointIndices.TryGetValue((point.Row + pointStride, point.Column), out var bottomLeft)
                || !pointIndices.TryGetValue(
                    (point.Row + pointStride, point.Column + pointStride),
                    out var bottomRight))
            {
                continue;
            }

            AddTriangle(topLeft, bottomLeft, topRight);
            AddTriangle(topRight, bottomLeft, bottomRight);
            AddGridEdge(topLeft, topRight);
            AddGridEdge(topLeft, bottomLeft);
            AddGridEdge(topRight, bottomRight);
            AddGridEdge(bottomLeft, bottomRight);

            if (UseSurfaceEdgeLine(point.Row, minimumRow, maximumRow))
            {
                AddSurfaceEdge(topLeft, topRight);
            }

            if (UseSurfaceEdgeLine(point.Row + pointStride, minimumRow, maximumRow))
            {
                AddSurfaceEdge(bottomLeft, bottomRight);
            }

            if (UseSurfaceEdgeLine(point.Column, minimumColumn, maximumColumn))
            {
                AddSurfaceEdge(topLeft, bottomLeft);
            }

            if (UseSurfaceEdgeLine(point.Column + pointStride, minimumColumn, maximumColumn))
            {
                AddSurfaceEdge(topRight, bottomRight);
            }
        }

        return new C3DHeightGridRenderProxy(
            points,
            triangleIndices.ToArray(),
            edgeIndices.ToArray(),
            gridEdgeIndices.ToArray(),
            surfaceEdgeIndices.ToArray());

        void AddTriangle(int first, int second, int third)
        {
            triangleIndices.Add(first);
            triangleIndices.Add(second);
            triangleIndices.Add(third);
            AddEdge(first, second);
            AddEdge(second, third);
            AddEdge(third, first);
        }

        void AddEdge(int first, int second)
        {
            var minimum = Math.Min(first, second);
            var maximum = Math.Max(first, second);
            var key = ((long)minimum << 32) | (uint)maximum;
            if (!uniqueEdges.Add(key))
            {
                return;
            }

            edgeIndices.Add(first);
            edgeIndices.Add(second);
        }

        void AddSurfaceEdge(int first, int second)
        {
            var minimum = Math.Min(first, second);
            var maximum = Math.Max(first, second);
            var key = ((long)minimum << 32) | (uint)maximum;
            if (!uniqueSurfaceEdges.Add(key))
            {
                return;
            }

            surfaceEdgeIndices.Add(first);
            surfaceEdgeIndices.Add(second);
        }

        void AddGridEdge(int first, int second)
        {
            var minimum = Math.Min(first, second);
            var maximum = Math.Max(first, second);
            var key = ((long)minimum << 32) | (uint)maximum;
            if (!uniqueGridEdges.Add(key))
            {
                return;
            }

            gridEdgeIndices.Add(first);
            gridEdgeIndices.Add(second);
        }

        bool UseSurfaceEdgeLine(int coordinate, int minimum, int maximum) =>
            coordinate == minimum
            || coordinate == maximum
            || (coordinate - minimum) % (pointStride * SurfaceEdgeSampleInterval) == 0;
    }
}

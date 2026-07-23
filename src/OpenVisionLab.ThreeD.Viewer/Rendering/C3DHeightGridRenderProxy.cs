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

    public static C3DHeightGridRenderProxy Create(
        C3DHeightGrid grid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grid);
        return Create(grid.Points, grid.PointStride, cancellationToken);
    }

    internal static C3DHeightGridRenderProxy Create(
        IReadOnlyList<HeightGridPoint> sourcePoints,
        int pointStride,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourcePoints);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pointStride);

        var points = sourcePoints as HeightGridPoint[] ?? sourcePoints.ToArray();
        var pointIndices = new Dictionary<long, int>(points.Length);
        var minimumRow = 0;
        var maximumRow = 0;
        var minimumColumn = 0;
        var maximumColumn = 0;
        for (var index = 0; index < points.Length; index++)
        {
            if ((index & 0xfff) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var point = points[index];
            if (point.Row < 0 || point.Column < 0)
            {
                throw new InvalidDataException("C3D display topology requires non-negative source cells.");
            }

            if (!pointIndices.TryAdd(CellKey(point.Row, point.Column), index))
            {
                throw new InvalidDataException(
                    $"C3D display topology contains duplicate source cell ({point.Row}, {point.Column}).");
            }

            if (index == 0)
            {
                minimumRow = maximumRow = point.Row;
                minimumColumn = maximumColumn = point.Column;
            }
            else
            {
                minimumRow = Math.Min(minimumRow, point.Row);
                maximumRow = Math.Max(maximumRow, point.Row);
                minimumColumn = Math.Min(minimumColumn, point.Column);
                maximumColumn = Math.Max(maximumColumn, point.Column);
            }
        }

        var triangleIndices = new List<int>();
        var edgeIndices = new List<int>();
        var gridEdgeIndices = new List<int>();
        var surfaceEdgeIndices = new List<int>();
        var edgeHorizontal = new bool[points.Length];
        var edgeVertical = new bool[points.Length];
        var gridHorizontal = new bool[points.Length];
        var gridVertical = new bool[points.Length];
        var surfaceHorizontal = new bool[points.Length];
        var surfaceVertical = new bool[points.Length];
        for (var topLeft = 0; topLeft < points.Length; topLeft++)
        {
            if ((topLeft & 0xfff) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var point = points[topLeft];
            if (!pointIndices.TryGetValue(CellKey(point.Row, point.Column + pointStride), out var topRight)
                || !pointIndices.TryGetValue(CellKey(point.Row + pointStride, point.Column), out var bottomLeft)
                || !pointIndices.TryGetValue(
                    CellKey(point.Row + pointStride, point.Column + pointStride),
                    out var bottomRight))
            {
                continue;
            }

            triangleIndices.Add(topLeft);
            triangleIndices.Add(bottomLeft);
            triangleIndices.Add(topRight);
            AddOrthogonalEdge(edgeIndices, edgeHorizontal, edgeVertical, topLeft, bottomLeft);
            edgeIndices.Add(bottomLeft);
            edgeIndices.Add(topRight);
            AddOrthogonalEdge(edgeIndices, edgeHorizontal, edgeVertical, topRight, topLeft);

            triangleIndices.Add(topRight);
            triangleIndices.Add(bottomLeft);
            triangleIndices.Add(bottomRight);
            AddOrthogonalEdge(edgeIndices, edgeHorizontal, edgeVertical, bottomLeft, bottomRight);
            AddOrthogonalEdge(edgeIndices, edgeHorizontal, edgeVertical, bottomRight, topRight);

            AddOrthogonalEdge(gridEdgeIndices, gridHorizontal, gridVertical, topLeft, topRight);
            AddOrthogonalEdge(gridEdgeIndices, gridHorizontal, gridVertical, topLeft, bottomLeft);
            AddOrthogonalEdge(gridEdgeIndices, gridHorizontal, gridVertical, topRight, bottomRight);
            AddOrthogonalEdge(gridEdgeIndices, gridHorizontal, gridVertical, bottomLeft, bottomRight);

            if (UseSurfaceEdgeLine(point.Row, minimumRow, maximumRow))
            {
                AddOrthogonalEdge(
                    surfaceEdgeIndices,
                    surfaceHorizontal,
                    surfaceVertical,
                    topLeft,
                    topRight);
            }

            if (UseSurfaceEdgeLine(point.Row + pointStride, minimumRow, maximumRow))
            {
                AddOrthogonalEdge(
                    surfaceEdgeIndices,
                    surfaceHorizontal,
                    surfaceVertical,
                    bottomLeft,
                    bottomRight);
            }

            if (UseSurfaceEdgeLine(point.Column, minimumColumn, maximumColumn))
            {
                AddOrthogonalEdge(
                    surfaceEdgeIndices,
                    surfaceHorizontal,
                    surfaceVertical,
                    topLeft,
                    bottomLeft);
            }

            if (UseSurfaceEdgeLine(point.Column + pointStride, minimumColumn, maximumColumn))
            {
                AddOrthogonalEdge(
                    surfaceEdgeIndices,
                    surfaceHorizontal,
                    surfaceVertical,
                    topRight,
                    bottomRight);
            }
        }

        return new C3DHeightGridRenderProxy(
            points,
            triangleIndices.ToArray(),
            edgeIndices.ToArray(),
            gridEdgeIndices.ToArray(),
            surfaceEdgeIndices.ToArray());

        void AddOrthogonalEdge(
            List<int> target,
            bool[] horizontal,
            bool[] vertical,
            int first,
            int second)
        {
            var firstPoint = points[first];
            var secondPoint = points[second];
            bool[] occupancy;
            int owner;
            if (firstPoint.Row == secondPoint.Row)
            {
                occupancy = horizontal;
                owner = firstPoint.Column < secondPoint.Column ? first : second;
            }
            else if (firstPoint.Column == secondPoint.Column)
            {
                occupancy = vertical;
                owner = firstPoint.Row < secondPoint.Row ? first : second;
            }
            else
            {
                throw new InvalidDataException("C3D grid topology contains a non-orthogonal grid edge.");
            }

            if (occupancy[owner])
            {
                return;
            }

            occupancy[owner] = true;
            target.Add(first);
            target.Add(second);
        }

        static long CellKey(int row, int column) => ((long)row << 32) | (uint)column;

        bool UseSurfaceEdgeLine(int coordinate, int minimum, int maximum) =>
            coordinate == minimum
            || coordinate == maximum
            || (coordinate - minimum) % (pointStride * SurfaceEdgeSampleInterval) == 0;
    }
}

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Deterministically maps a validated source-grid polygon to covered cell
/// centers. Coordinates remain in the recipe's row/column grid space; no
/// scale, origin, axis, height, or SDK conversion is performed here.
/// </summary>
public static class ToolRecipeGridPolygonRasterizer
{
    private const double BoundaryEpsilon = 1e-12;

    public static ToolRecipeGridPolygonRasterization Rasterize(
        ToolRecipeGridPolygon? polygon,
        int gridWidth,
        int gridHeight)
    {
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            return new(
                gridWidth,
                gridHeight,
                Array.Empty<ToolRecipeGridPolygonCell>(),
                ["grid width and height must be positive"]);
        }

        var errors = ToolRecipeGridPolygonGeometry.Validate(polygon, gridWidth, gridHeight);
        if (errors.Count > 0)
        {
            return new(
                gridWidth,
                gridHeight,
                Array.Empty<ToolRecipeGridPolygonCell>(),
                errors.ToArray());
        }

        var vertices = polygon!.Vertices;
        var cells = new List<ToolRecipeGridPolygonCell>();
        // ponytail: scan the authored grid directly; use a scanline path only if measured ROI latency requires it.
        for (var row = 0; row < gridHeight; row++)
        {
            for (var column = 0; column < gridWidth; column++)
            {
                if (ContainsCellCenter(vertices, row, column))
                {
                    cells.Add(new ToolRecipeGridPolygonCell(row, column));
                }
            }
        }

        return new(gridWidth, gridHeight, cells.ToArray(), Array.Empty<string>());
    }

    private static bool ContainsCellCenter(
        IReadOnlyList<ToolRecipeGridPolygonVertex> vertices,
        double row,
        double column)
    {
        for (var index = 0; index < vertices.Count; index++)
        {
            var nextIndex = (index + 1) % vertices.Count;
            if (IsOnSegment(vertices[index], vertices[nextIndex], row, column))
            {
                return true;
            }
        }

        var inside = false;
        for (int index = 0, previousIndex = vertices.Count - 1;
             index < vertices.Count;
             previousIndex = index++)
        {
            var current = vertices[index];
            var previous = vertices[previousIndex];
            var crossesRow = (current.Row > row) != (previous.Row > row);
            if (!crossesRow)
            {
                continue;
            }

            var crossingColumn = (previous.Column - current.Column)
                * (row - current.Row)
                / (previous.Row - current.Row)
                + current.Column;
            if (column < crossingColumn)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool IsOnSegment(
        ToolRecipeGridPolygonVertex first,
        ToolRecipeGridPolygonVertex second,
        double row,
        double column)
    {
        var cross = (second.Column - first.Column) * (row - first.Row)
            - (second.Row - first.Row) * (column - first.Column);
        if (Math.Abs(cross) > BoundaryEpsilon)
        {
            return false;
        }

        return row >= Math.Min(first.Row, second.Row) - BoundaryEpsilon
            && row <= Math.Max(first.Row, second.Row) + BoundaryEpsilon
            && column >= Math.Min(first.Column, second.Column) - BoundaryEpsilon
            && column <= Math.Max(first.Column, second.Column) + BoundaryEpsilon;
    }
}

public sealed record ToolRecipeGridPolygonRasterization(
    int GridWidth,
    int GridHeight,
    IReadOnlyList<ToolRecipeGridPolygonCell> Cells,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public int CellCount => Cells.Count;
}

public sealed record ToolRecipeGridPolygonCell(int Row, int Column);

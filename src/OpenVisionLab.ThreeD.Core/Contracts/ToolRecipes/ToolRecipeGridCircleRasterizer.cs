namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Deterministically maps a validated source-grid circle to covered cell
/// centers. Coordinates remain in the recipe's row/column grid space; no
/// scale, origin, axis, height, or SDK conversion is performed here.
/// </summary>
public static class ToolRecipeGridCircleRasterizer
{
    public static ToolRecipeGridCircleRasterization Rasterize(
        ToolRecipeGridCircle? circle,
        int gridWidth,
        int gridHeight)
    {
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            return new(
                gridWidth,
                gridHeight,
                Array.Empty<ToolRecipeGridCircleCell>(),
                ["grid width and height must be positive"]);
        }

        var errors = ToolRecipeGridCircleGeometry.Validate(circle, gridWidth, gridHeight);
        if (errors.Count > 0)
        {
            return new(
                gridWidth,
                gridHeight,
                Array.Empty<ToolRecipeGridCircleCell>(),
                errors.ToArray());
        }

        var cells = new List<ToolRecipeGridCircleCell>();
        // ponytail: scan the authored grid directly; use a bounded scanline only if measured ROI latency requires it.
        for (var row = 0; row < gridHeight; row++)
        {
            for (var column = 0; column < gridWidth; column++)
            {
                var rowDistance = (double)row - circle!.CenterRow;
                var columnDistance = (double)column - circle.CenterColumn;
                if ((rowDistance * rowDistance) + (columnDistance * columnDistance)
                    <= (circle.Radius * circle.Radius))
                {
                    cells.Add(new ToolRecipeGridCircleCell(row, column));
                }
            }
        }

        return new(gridWidth, gridHeight, cells.ToArray(), Array.Empty<string>());
    }
}

public sealed record ToolRecipeGridCircleRasterization(
    int GridWidth,
    int GridHeight,
    IReadOnlyList<ToolRecipeGridCircleCell> Cells,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public int CellCount => Cells.Count;
}

public sealed record ToolRecipeGridCircleCell(int Row, int Column);

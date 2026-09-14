namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Deterministically maps a validated source-grid rectangle to covered cell
/// centers. Coordinates remain in the recipe's row/column grid space; no
/// scale, origin, axis, height, or SDK conversion is performed here.
/// </summary>
public static class ToolRecipeGridRectangleRasterizer
{
    public static ToolRecipeGridRectangleRasterization Rasterize(
        ToolRecipeGridRectangle? rectangle,
        int gridWidth,
        int gridHeight)
    {
        if (gridWidth <= 0 || gridHeight <= 0)
        {
            return new(
                gridWidth,
                gridHeight,
                Array.Empty<ToolRecipeGridRectangleCell>(),
                ["grid width and height must be positive"]);
        }

        var errors = ToolRecipeGridRectangleGeometry.Validate(rectangle, gridWidth, gridHeight);
        if (errors.Count > 0)
        {
            return new(
                gridWidth,
                gridHeight,
                Array.Empty<ToolRecipeGridRectangleCell>(),
                errors.ToArray());
        }

        var validatedRectangle = rectangle!;
        var cells = new List<ToolRecipeGridRectangleCell>();
        // ponytail: enumerate the authored rectangle directly; do not allocate a mask until an inspection consumer owns that contract.
        for (var row = validatedRectangle.Row; row < validatedRectangle.Row + validatedRectangle.RowCount; row++)
        {
            for (var column = validatedRectangle.Column; column < validatedRectangle.Column + validatedRectangle.ColumnCount; column++)
            {
                cells.Add(new ToolRecipeGridRectangleCell(row, column));
            }
        }

        return new(gridWidth, gridHeight, cells.ToArray(), Array.Empty<string>());
    }
}

public sealed record ToolRecipeGridRectangleRasterization(
    int GridWidth,
    int GridHeight,
    IReadOnlyList<ToolRecipeGridRectangleCell> Cells,
    IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;

    public int CellCount => Cells.Count;
}

public sealed record ToolRecipeGridRectangleCell(int Row, int Column);

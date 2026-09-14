namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Runtime-neutral bounds checks for a rectangular height-field footprint.
/// </summary>
public static class ToolRecipeGridRectangleGeometry
{
    public static IReadOnlyList<string> Validate(
        ToolRecipeGridRectangle? rectangle,
        int gridWidth,
        int gridHeight)
    {
        var errors = new List<string>();
        if (rectangle is null)
        {
            errors.Add("grid rectangle payload is required");
            return errors;
        }

        if (rectangle.Row < 0 || rectangle.Column < 0
            || rectangle.RowCount <= 0 || rectangle.ColumnCount <= 0)
        {
            errors.Add("grid rectangle must have a non-negative origin and positive dimensions");
            return errors;
        }

        if (gridWidth > 0 && gridHeight > 0
            && (rectangle.RowCount > gridHeight
                || rectangle.ColumnCount > gridWidth
                || rectangle.Row > gridHeight - rectangle.RowCount
                || rectangle.Column > gridWidth - rectangle.ColumnCount))
        {
            errors.Add($"grid rectangle is outside the recorded {gridWidth} x {gridHeight} bound grid");
        }

        return errors;
    }
}

namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Runtime-neutral bounds checks for a circular height-field footprint.
/// </summary>
public static class ToolRecipeGridCircleGeometry
{
    public const double MinimumRadius = 1.0;

    public static IReadOnlyList<string> Validate(
        ToolRecipeGridCircle? circle,
        int gridWidth,
        int gridHeight)
    {
        var errors = new List<string>();
        if (circle is null)
        {
            errors.Add("grid circle payload is required");
            return errors;
        }

        if (circle.CenterRow < 0 || circle.CenterColumn < 0)
        {
            errors.Add("grid circle center must be non-negative");
        }
        if (!double.IsFinite(circle.Radius) || circle.Radius < MinimumRadius)
        {
            errors.Add($"grid circle radius must be finite and at least {MinimumRadius:R}");
        }
        if (gridWidth > 0 && gridHeight > 0
            && double.IsFinite(circle.Radius)
            && (circle.CenterRow - circle.Radius < 0
                || circle.CenterColumn - circle.Radius < 0
                || circle.CenterRow + circle.Radius > gridHeight - 1
                || circle.CenterColumn + circle.Radius > gridWidth - 1))
        {
            errors.Add($"grid circle must stay inside the recorded {gridWidth} x {gridHeight} bound grid");
        }

        return errors;
    }
}

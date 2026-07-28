namespace OpenVisionLab.ThreeD.Core;

/// <summary>
/// Runtime-neutral geometry checks shared by recipe validation and numeric
/// authoring. Axes must form one finite right-handed orthonormal basis and
/// every half-extent must be finite and positive.
/// </summary>
public static class ToolRecipeOrientedBox3DGeometry
{
    public const double AxisTolerance = 1e-5;

    public static IReadOnlyList<string> Validate(ToolRecipeOrientedBox3D? box)
    {
        var errors = new List<string>();
        if (box is null)
        {
            errors.Add("oriented box payload is required");
            return errors;
        }

        if (!IsFinite(box.Center))
        {
            errors.Add("center XYZ must be finite");
        }

        if (!IsFinite(box.HalfExtents)
            || box.HalfExtents.X <= 0
            || box.HalfExtents.Y <= 0
            || box.HalfExtents.Z <= 0)
        {
            errors.Add("half-extents XYZ must be finite and positive");
        }

        ValidateUnitAxis(box.AxisX, "X axis", errors);
        ValidateUnitAxis(box.AxisY, "Y axis", errors);
        ValidateUnitAxis(box.AxisZ, "Z axis", errors);
        if (errors.Any(error => error.Contains("axis", StringComparison.OrdinalIgnoreCase)))
        {
            return errors;
        }

        if (Math.Abs(Dot(box.AxisX, box.AxisY)) > AxisTolerance
            || Math.Abs(Dot(box.AxisX, box.AxisZ)) > AxisTolerance
            || Math.Abs(Dot(box.AxisY, box.AxisZ)) > AxisTolerance)
        {
            errors.Add("axes must be mutually orthogonal");
        }

        var handedness = Dot(Cross(box.AxisX, box.AxisY), box.AxisZ);
        if (!double.IsFinite(handedness) || handedness < 1.0 - AxisTolerance)
        {
            errors.Add("axes must form a right-handed basis");
        }

        return errors;
    }

    private static void ValidateUnitAxis(
        ToolRecipeXyz axis,
        string label,
        ICollection<string> errors)
    {
        if (!IsFinite(axis))
        {
            errors.Add($"{label} XYZ must be finite");
            return;
        }

        var length = Math.Sqrt(Dot(axis, axis));
        if (!double.IsFinite(length) || Math.Abs(length - 1.0) > AxisTolerance)
        {
            errors.Add($"{label} must have unit length");
        }
    }

    private static bool IsFinite(ToolRecipeXyz? value) =>
        value is not null
        && double.IsFinite(value.X)
        && double.IsFinite(value.Y)
        && double.IsFinite(value.Z);

    private static double Dot(ToolRecipeXyz left, ToolRecipeXyz right) =>
        left.X * right.X + left.Y * right.Y + left.Z * right.Z;

    private static ToolRecipeXyz Cross(ToolRecipeXyz left, ToolRecipeXyz right) =>
        new(
            left.Y * right.Z - left.Z * right.Y,
            left.Z * right.X - left.X * right.Z,
            left.X * right.Y - left.Y * right.X);
}

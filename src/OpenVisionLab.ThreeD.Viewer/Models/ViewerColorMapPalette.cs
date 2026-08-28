using System.Numerics;

namespace OpenVisionLab.ThreeD.Viewer.Models;

internal static class ViewerColorMapPalette
{
    public static (double R, double G, double B) Grayscale(double value)
    {
        var normalized = Normalize(value);
        return (normalized, normalized, normalized);
    }

    public static (double R, double G, double B) Thermal(double value)
    {
        var normalized = Normalize(value);
        if (normalized <= 1.0 / 3.0)
        {
            return (normalized * 3.0, 0.0, 0.0);
        }

        if (normalized <= 2.0 / 3.0)
        {
            return (1.0, (normalized - 1.0 / 3.0) * 3.0, 0.0);
        }

        return (1.0, 1.0, (normalized - 2.0 / 3.0) * 3.0);
    }

    public static (double R, double G, double B) NormalMap(Vector3 normal)
    {
        return (
            Normalize(normal.X * 0.5 + 0.5),
            Normalize(normal.Y * 0.5 + 0.5),
            Normalize(normal.Z * 0.5 + 0.5));
    }

    private static double Normalize(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.0;
}

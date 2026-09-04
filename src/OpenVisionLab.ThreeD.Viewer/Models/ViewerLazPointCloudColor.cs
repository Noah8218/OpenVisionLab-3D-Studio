using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// Resolves LAZ/LAS display colors without touching OpenGL or WPF state.
/// Colors are display-only; they are not measurement or calibration values.
/// </summary>
internal static class ViewerLazPointCloudColor
{
    public static (double R, double G, double B) Resolve(
        LazPointCloudPoint point,
        string colorMode,
        LazPointCloudMetadata? metadata)
    {
        static double Normalize(ushort value) => value > 255 ? value / 65535.0 : value / 255.0;

        return colorMode switch
        {
            "Solid" => (0.72, 0.84, 1.0),
            "Height" => C3DPointMapPalette.Height(NormalizeHeight(point.Position.Z, metadata)),
            "Intensity" => ViewerColorMapPalette.Grayscale(Normalize(point.Intensity)),
            _ => (Normalize(point.Red), Normalize(point.Green), Normalize(point.Blue))
        };
    }

    public static double NormalizeHeight(float sourceZ, LazPointCloudMetadata? metadata)
    {
        if (metadata is null || Math.Abs(metadata.MaxZ - metadata.MinZ) < 0.000001)
        {
            return 0.5;
        }

        return (sourceZ - metadata.MinZ) / (metadata.MaxZ - metadata.MinZ);
    }
}

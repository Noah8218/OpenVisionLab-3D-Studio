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
        => Resolve(point, colorMode, metadata, null);

    public static (double R, double G, double B) Resolve(
        LazPointCloudPoint point,
        string colorMode,
        LazPointCloudMetadata? metadata,
        LazPointCloud? pointCloud)
    {
        // LAS channels are ushort. Retain legacy 8-bit-in-16 display inference
        // only for a whole decoded dataset, never for each point/component.
        // All-low true 16-bit data is indistinguishable from legacy 8-bit data;
        // without a dataset use the declared 16-bit range.
        var intensityScale = pointCloud is { MaximumIntensity: <= 255 } ? 255.0 : 65535.0;
        var rgbScale = pointCloud is { MaximumRgbComponent: <= 255 } ? 255.0 : 65535.0;
        static double Normalize(ushort value, double scale) => Math.Clamp(value / scale, 0.0, 1.0);
        var sourceZ = point.HasPreciseSourceCoordinate ? point.SourceCoordinate.Z : point.Position.Z;

        return colorMode switch
        {
            "Solid" => (0.72, 0.84, 1.0),
            "Height" => C3DPointMapPalette.Height(NormalizeHeight(sourceZ, metadata)),
            "Intensity" => ViewerColorMapPalette.Grayscale(Normalize(point.Intensity, intensityScale)),
            _ => (Normalize(point.Red, rgbScale), Normalize(point.Green, rgbScale), Normalize(point.Blue, rgbScale))
        };
    }

    public static double NormalizeHeight(float sourceZ, LazPointCloudMetadata? metadata)
        => NormalizeHeight((double)sourceZ, metadata);

    public static double NormalizeHeight(double sourceZ, LazPointCloudMetadata? metadata)
    {
        if (metadata is null || Math.Abs(metadata.MaxZ - metadata.MinZ) < 0.000001)
        {
            return 0.5;
        }

        return (sourceZ - metadata.MinZ) / (metadata.MaxZ - metadata.MinZ);
    }
}

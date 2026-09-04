using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Creates the display-only BGRA height-map raster used by the Viewer.
/// Raw C3D values stay owned by <see cref="C3DHeightGrid"/>; this class only
/// consumes the already-normalized point color scalar and produces pixels.
/// </summary>
internal static class C3DHeightMapRasterizer
{
    private const byte BackgroundBlue = 31;
    private const byte BackgroundGreen = 24;
    private const byte BackgroundRed = 17;
    private const byte Alpha = 255;

    public static byte[] CreatePixels(C3DHeightGrid grid, int pixelWidth, int pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(grid);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        var pixels = new byte[checked(pixelWidth * pixelHeight * 4)];
        FillBackground(pixels);
        foreach (var point in grid.Points)
        {
            var x = (point.Position.X + grid.XHalfExtent) /
                Math.Max(0.0001f, grid.XHalfExtent * 2.0f);
            var z = (point.Position.Z + grid.ZHalfExtent) /
                Math.Max(0.0001f, grid.ZHalfExtent * 2.0f);
            var column = (int)Math.Round(Math.Clamp(x, 0.0f, 1.0f) * (pixelWidth - 1));
            var row = (int)Math.Round(Math.Clamp(z, 0.0f, 1.0f) * (pixelHeight - 1));
            PaintPixel(pixels, pixelWidth, pixelHeight, column, row, point.HeightScalar);
        }

        return pixels;
    }

    private static void FillBackground(byte[] pixels)
    {
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = BackgroundBlue;
            pixels[index + 1] = BackgroundGreen;
            pixels[index + 2] = BackgroundRed;
            pixels[index + 3] = Alpha;
        }
    }

    private static void PaintPixel(byte[] pixels, int pixelWidth, int pixelHeight, int column, int row, double heightScalar)
    {
        var (red, green, blue) = HeightMapColor(heightScalar);
        for (var y = Math.Max(0, row - 1); y <= Math.Min(pixelHeight - 1, row + 1); y++)
        {
            for (var x = Math.Max(0, column - 1); x <= Math.Min(pixelWidth - 1, column + 1); x++)
            {
                var index = (y * pixelWidth + x) * 4;
                pixels[index] = blue;
                pixels[index + 1] = green;
                pixels[index + 2] = red;
                pixels[index + 3] = Alpha;
            }
        }
    }

    private static (byte R, byte G, byte B) HeightMapColor(double value)
    {
        var (red, green, blue) = C3DPointMapPalette.Height(value);
        return ((byte)(red * 255), (byte)(green * 255), (byte)(blue * 255));
    }
}

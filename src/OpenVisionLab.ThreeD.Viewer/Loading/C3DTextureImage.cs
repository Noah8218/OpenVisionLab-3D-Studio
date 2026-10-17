using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>An owned, decoded display image. File streams are closed before admission.</summary>
internal sealed class C3DTextureImage
{
    private C3DTextureImage(string path, string sha256, int width, int height, byte[] pixels)
    {
        Path = path;
        Sha256 = sha256;
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public string Path { get; }
    public string Sha256 { get; }
    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }

    public static C3DTextureImage Load(string path, string? expectedSha256 = null)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > 128 * 1024 * 1024)
            throw new InvalidDataException("Texture image must be between 1 byte and 128 MiB.");
        var bytes = new byte[checked((int)file.Length)];
        file.ReadExactly(bytes);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes));
        if (expectedSha256 is not null && !string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The saved texture image has changed. Select the image again to confirm it.");
        using var stream = new MemoryStream(bytes, writable: false);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        if (decoder.Frames.Count != 1)
            throw new InvalidDataException("Select a single-frame texture image.");
        BitmapSource bitmap = decoder.Frames[0];
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0 || (long)bitmap.PixelWidth * bitmap.PixelHeight > 32 * 1024 * 1024)
            throw new InvalidDataException("Texture image must contain at most 32 million pixels.");
        if (bitmap.Format != PixelFormats.Bgra32)
            bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var stride = checked(bitmap.PixelWidth * 4);
        var pixels = new byte[checked(stride * bitmap.PixelHeight)];
        bitmap.CopyPixels(pixels, stride, 0);
        return new(fullPath, sha256, bitmap.PixelWidth, bitmap.PixelHeight, pixels);
    }

    // Source sample centres, independent of render density. WPF rows are uploaded
    // top-first, so v=0 addresses the first source row without a second vertical flip.
    public static Vector2 Coordinate(int row, int column, int width, int height) =>
        new((column + 0.5f) / width, (row + 0.5f) / height);

    public static float[] CreateCoordinates(C3DHeightGrid grid)
    {
        var result = new float[checked(grid.Points.Length * 2)];
        for (var index = 0; index < grid.Points.Length; index++)
        {
            var point = grid.Points[index];
            var uv = Coordinate(point.Row, point.Column, grid.Width, grid.Height);
            result[index * 2] = uv.X;
            result[index * 2 + 1] = uv.Y;
        }
        return result;
    }
}

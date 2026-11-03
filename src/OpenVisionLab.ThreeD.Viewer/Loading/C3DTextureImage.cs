using System.IO;
using System.Numerics;
using System.Security.Cryptography;
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
        var decoded = TextureImageDecoder.DecodeBgra32(bytes, requireSingleFrame: true, maximumTextureDimension: int.MaxValue);
        return new(fullPath, sha256, decoded.Width, decoded.Height, decoded.Pixels);
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

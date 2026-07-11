using System.Globalization;
using System.Numerics;
using System.Text;

namespace OpenVisionLab.ThreeD.Data;

public sealed record C3DPointMapPlyExport(
    string Path,
    int PointCount,
    int FaceCount,
    Vector3 Minimum,
    Vector3 Maximum,
    long ByteLength);

public static class C3DPointMapPly
{
    public static C3DPointMapPlyExport Export(C3DHeightGrid grid, string path)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.Points.Length == 0)
        {
            throw new InvalidDataException("C3D point-map export requires at least one rendered point.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var faces = CreateFaces(grid);
        using (var writer = new StreamWriter(fullPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.WriteLine("ply");
            writer.WriteLine("format ascii 1.0");
            writer.WriteLine("comment OpenVisionLab C3D viewer-frame reference export");
            writer.WriteLine("comment vertices are exact rendered samples; faces are compatibility visualization only and must not be measured");
            writer.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"comment x=(column-center)*{grid.HorizontalScale:R} y=(raw-mean)*{C3DHeightGrid.ViewerHeightScale:R} z=(row-center)*{grid.HorizontalScale:R}"));
            writer.WriteLine($"element vertex {grid.Points.Length.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine("property float x");
            writer.WriteLine("property float y");
            writer.WriteLine("property float z");
            writer.WriteLine("property uchar red");
            writer.WriteLine("property uchar green");
            writer.WriteLine("property uchar blue");
            writer.WriteLine($"element face {faces.Count.ToString(CultureInfo.InvariantCulture)}");
            writer.WriteLine("property list uchar int vertex_indices");
            writer.WriteLine("end_header");

            foreach (var point in grid.Points)
            {
                var color = C3DPointMapPalette.HeightBytes(point.HeightScalar);
                writer.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{point.Position.X:R} {point.Position.Y:R} {point.Position.Z:R} {color.R} {color.G} {color.B}"));
            }

            foreach (var face in faces)
            {
                writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"3 {face.A} {face.B} {face.C}"));
            }
        }

        var minimum = new Vector3(float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity);
        foreach (var point in grid.Points)
        {
            minimum = Vector3.Min(minimum, point.Position);
            maximum = Vector3.Max(maximum, point.Position);
        }

        return new C3DPointMapPlyExport(
            fullPath,
            grid.Points.Length,
            faces.Count,
            minimum,
            maximum,
            new FileInfo(fullPath).Length);
    }

    private static List<(int A, int B, int C)> CreateFaces(C3DHeightGrid grid)
    {
        var indices = new Dictionary<(int Row, int Column), int>(grid.Points.Length);
        for (var index = 0; index < grid.Points.Length; index++)
        {
            var point = grid.Points[index];
            indices[(point.Row, point.Column)] = index;
        }

        var faces = new List<(int A, int B, int C)>();
        foreach (var point in grid.Points)
        {
            var topLeft = indices[(point.Row, point.Column)];
            if (!indices.TryGetValue((point.Row, point.Column + grid.PointStride), out var topRight)
                || !indices.TryGetValue((point.Row + grid.PointStride, point.Column), out var bottomLeft)
                || !indices.TryGetValue((point.Row + grid.PointStride, point.Column + grid.PointStride), out var bottomRight))
            {
                continue;
            }

            faces.Add((topLeft, bottomLeft, topRight));
            faces.Add((topRight, bottomLeft, bottomRight));
        }

        return faces;
    }
}

public static class C3DPointMapPalette
{
    public static (double R, double G, double B) Height(double value)
    {
        var t = Math.Clamp(value, 0.0, 1.0);
        if (t < 0.5)
        {
            var local = t / 0.5;
            return (0.05, 0.35 + 0.55 * local, 0.95 - 0.30 * local);
        }

        var high = (t - 0.5) / 0.5;
        return (0.05 + 0.95 * high, 0.90 - 0.20 * high, 0.65 - 0.55 * high);
    }

    public static (byte R, byte G, byte B) HeightBytes(double value)
    {
        var color = Height(value);
        return (ToByte(color.R), ToByte(color.G), ToByte(color.B));
    }

    private static byte ToByte(double value) =>
        (byte)(Math.Clamp(value, 0.0, 1.0) * byte.MaxValue);
}

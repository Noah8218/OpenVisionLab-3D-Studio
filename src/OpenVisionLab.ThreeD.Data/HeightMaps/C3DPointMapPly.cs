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
    public static C3DPointMapPlyExport Export(C3DHeightGrid grid, string path, bool includeFaces = true)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (grid.Points.Length == 0)
        {
            throw new InvalidDataException("C3D point-map export requires at least one rendered point.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        List<(int A, int B, int C)> faces = includeFaces ? CreateFaces(grid) : [];
        using (var writer = new StreamWriter(fullPath, false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.WriteLine("ply");
            writer.WriteLine("format ascii 1.0");
            writer.WriteLine("comment OpenVisionLab C3D viewer-frame reference export");
            writer.WriteLine(includeFaces
                ? "comment vertices are exact rendered samples; faces are compatibility visualization only and must not be measured"
                : "comment point-only reference; vertices are exact rendered samples");
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
    public static IReadOnlyList<(double Offset, (double R, double G, double B) Color)> DisplayStops(
        double minimum, double maximum, double dataMinimum, double dataMaximum, string palette, HeightDisplayOptions options)
    {
        if (options.ColorStyle == "tolerance" && options.Measurement is not null)
            return [(0, (79 / 255d, 139 / 255d, 232 / 255d)), (1d / 3, (79 / 255d, 139 / 255d, 232 / 255d)),
                (1d / 3, (145 / 255d, 209 / 255d, 168 / 255d)), (2d / 3, (145 / 255d, 209 / 255d, 168 / 255d)),
                (2d / 3, (229 / 255d, 83 / 255d, 75 / 255d)), (1, (229 / 255d, 83 / 255d, 75 / 255d))];
        var low = Math.Min(minimum, dataMinimum);
        var high = Math.Max(maximum, dataMaximum);
        if (!double.IsFinite(low) || !double.IsFinite(high) || low >= high) return [(0, DisplayColor(minimum, minimum, maximum, palette, options))];
        var stops = new List<(double, (double, double, double))>();
        void Add(double value, double sample) => stops.Add(((value - low) / (high - low), DisplayColor(sample, minimum, maximum, palette, options)));
        Add(low, low);
        if (low < minimum) Add(minimum, low);
        if (options.ColorStyle == "bands")
        {
            for (var index = 0; index < options.BandCount; index++)
            {
                var sample = minimum + (maximum - minimum) * (index + 0.5) / options.BandCount;
                Add(minimum + (maximum - minimum) * index / options.BandCount, sample);
                Add(minimum + (maximum - minimum) * (index + 1) / options.BandCount, sample);
            }
        }
        else
        {
            var positions = palette == "ColorVisionAssist" ? new[] { 0d, .25, .5, .75, 1d }
                : palette == "Thermal" ? new[] { 0d, 1d / 3, 2d / 3, 1d } : new[] { 0d, .5, 1d };
            foreach (var position in positions)
            {
                var value = minimum + (maximum - minimum) * position;
                Add(value, value);
            }
        }
        if (high > maximum) Add(maximum, high);
        Add(high, high);
        return stops;
    }

    public static (double R, double G, double B) DisplayColor(double value, double minimum, double maximum, string palette, HeightDisplayOptions options, int sampleIndex = -1)
    {
        var normalized = maximum > minimum ? (value - minimum) / (maximum - minimum) : 0.5;
        if (options.ColorStyle == "tolerance")
        {
            var measured = options.Measurement;
            var sample = measured is not null && sampleIndex >= 0 && sampleIndex < measured.Values.Length
                ? measured.Values.Span[sampleIndex] : sampleIndex < 0 ? value : double.NaN;
            if (double.IsFinite(sample))
                return sample < (measured?.LowerLimit ?? minimum) ? (79 / 255.0, 139 / 255.0, 232 / 255.0)
                    : sample > (measured?.UpperLimit ?? maximum) ? (229 / 255.0, 83 / 255.0, 75 / 255.0) : (145 / 255.0, 209 / 255.0, 168 / 255.0);
        }
        if (options.HighlightOutside && (normalized < 0 || normalized > 1))
            return normalized < 0 ? (106 / 255.0, 75 / 255.0, 196 / 255.0) : (240 / 255.0, 79 / 255.0, 208 / 255.0);
        normalized = Math.Clamp(normalized, 0, 1);
        if (options.ColorStyle == "bands") normalized = (Math.Min(Math.Floor(normalized * options.BandCount), options.BandCount - 1) + 0.5) / options.BandCount;
        if (palette == "Grayscale") return (normalized, normalized, normalized);
        if (palette == "ColorVisionAssist") return ColorVisionAssist(normalized);
        if (palette == "Solid") return (0.62, 0.82, 1.0);
        if (palette == "Thermal") return (Math.Clamp(normalized * 3, 0, 1), Math.Clamp(normalized * 3 - 1, 0, 1), Math.Clamp(normalized * 3 - 2, 0, 1));
        return Height(normalized);
    }

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

    public static (byte R, byte G, byte B) GrayscaleBytes(double value)
    {
        var normalized = Normalize(value);
        var channel = ToByte(normalized);
        return (channel, channel, channel);
    }

    public static (byte R, byte G, byte B) ThermalBytes(double value)
    {
        var normalized = Normalize(value);
        if (normalized <= 1.0 / 3.0)
        {
            return (ToByte(normalized * 3.0), 0, 0);
        }

        if (normalized <= 2.0 / 3.0)
        {
            return (byte.MaxValue, ToByte((normalized - 1.0 / 3.0) * 3.0), 0);
        }

        return (
            byte.MaxValue,
            byte.MaxValue,
            ToByte((normalized - 2.0 / 3.0) * 3.0));
    }

    public static (double R, double G, double B) ColorVisionAssist(double value)
    {
        var position = Normalize(value) * 4;
        var (first, second) = position switch
        {
            < 1 => ((0.0, 32.0, 77.0), (65.0, 77.0, 107.0)),
            < 2 => ((65.0, 77.0, 107.0), (124.0, 123.0, 120.0)),
            < 3 => ((124.0, 123.0, 120.0), (188.0, 175.0, 111.0)),
            _ => ((188.0, 175.0, 111.0), (255.0, 234.0, 70.0))
        };
        var fraction = position - Math.Min(3, Math.Floor(position));
        return ((first.Item1 + (second.Item1 - first.Item1) * fraction) / 255,
            (first.Item2 + (second.Item2 - first.Item2) * fraction) / 255,
            (first.Item3 + (second.Item3 - first.Item3) * fraction) / 255);
    }

    public static (byte R, byte G, byte B) ColorVisionAssistBytes(double value)
    {
        var color = ColorVisionAssist(value);
        return (ToByte(color.R), ToByte(color.G), ToByte(color.B));
    }

    private static double Normalize(double value) =>
        double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 0.0;

    private static byte ToByte(double value) =>
        (byte)(Math.Clamp(value, 0.0, 1.0) * byte.MaxValue);
}

using System.IO;
using System.Numerics;

namespace OpenVisionLab.ThreeD.Data;

public sealed class C3DHeightGrid
{
    public const float ViewerHorizontalSpan = 10.0f;
    public const float ViewerHeightScale = 0.0006f;

    private C3DHeightGrid(
        string sourcePath,
        int width,
        int height,
        int validSampleCount,
        int zeroSampleCount,
        float min,
        float max,
        double mean,
        int pointStride,
        HeightGridPoint[] points)
    {
        SourcePath = sourcePath;
        Width = width;
        Height = height;
        ValidSampleCount = validSampleCount;
        ZeroSampleCount = zeroSampleCount;
        Min = min;
        Max = max;
        Mean = mean;
        PointStride = pointStride;
        Points = points;
        HorizontalScale = CalculateHorizontalScale(width, height);
        XHalfExtent = (width - 1) * HorizontalScale / 2.0f;
        ZHalfExtent = (height - 1) * HorizontalScale / 2.0f;
    }

    public string SourcePath { get; }

    public int Width { get; }

    public int Height { get; }

    public int ValidSampleCount { get; }

    public int ZeroSampleCount { get; }

    public float Min { get; }

    public float Max { get; }

    public double Mean { get; }

    public int PointStride { get; }

    public HeightGridPoint[] Points { get; }

    public float HorizontalScale { get; }

    public float XHalfExtent { get; }

    public float ZHalfExtent { get; }

    public HeightGridPoint ReadPoint(int row, int column)
    {
        if (row < 0 || row >= Height)
        {
            throw new ArgumentOutOfRangeException(nameof(row), row, $"C3D row must be between 0 and {Height - 1}.");
        }

        if (column < 0 || column >= Width)
        {
            throw new ArgumentOutOfRangeException(nameof(column), column, $"C3D column must be between 0 and {Width - 1}.");
        }

        using var reader = new BinaryReader(File.OpenRead(SourcePath));
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        if (width != Width || height != Height)
        {
            throw new InvalidDataException("C3D dimensions changed after the source was loaded.");
        }

        reader.BaseStream.Seek(8L + ((long)row * Width + column) * sizeof(float), SeekOrigin.Begin);
        var value = reader.ReadSingle();
        if (!float.IsFinite(value) || value == 0.0f)
        {
            throw new InvalidDataException($"C3D point ({row}, {column}) is not a finite non-zero sample.");
        }

        return CreatePoint(value, row, column, Width, Height, Min, Max, Mean);
    }

    public static C3DHeightGrid Load(string path, int maxRenderedPoints = 55000)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        var width = reader.ReadInt32();
        var height = reader.ReadInt32();
        var sampleCount = checked(width * height);
        var expectedBytes = 8L + sampleCount * sizeof(float);
        if (width <= 0 || height <= 0 || reader.BaseStream.Length != expectedBytes)
        {
            throw new InvalidDataException($"Unsupported C3D height-grid layout: {path}");
        }

        var samples = new float[sampleCount];
        var validCount = 0;
        var zeroCount = 0;
        var min = float.PositiveInfinity;
        var max = float.NegativeInfinity;
        var sum = 0.0;

        for (var i = 0; i < samples.Length; i++)
        {
            var value = reader.ReadSingle();
            samples[i] = value;

            if (!float.IsFinite(value))
            {
                continue;
            }

            if (value == 0.0f)
            {
                zeroCount++;
                continue;
            }

            validCount++;
            min = Math.Min(min, value);
            max = Math.Max(max, value);
            sum += value;
        }

        if (validCount == 0)
        {
            throw new InvalidDataException($"C3D contains no non-zero finite samples: {path}");
        }

        var mean = sum / validCount;
        var pointStride = maxRenderedPoints <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)sampleCount / maxRenderedPoints)));
        var points = pointStride == 0
            ? []
            : CreatePoints(samples, width, height, pointStride, min, max, mean);
        return new C3DHeightGrid(path, width, height, validCount, zeroCount, min, max, mean, pointStride, points);
    }

    private static HeightGridPoint[] CreatePoints(float[] samples, int width, int height, int stride, float min, float max, double mean)
    {
        var points = new List<HeightGridPoint>();
        for (var row = 0; row < height; row += stride)
        {
            for (var column = 0; column < width; column += stride)
            {
                var value = samples[row * width + column];
                if (!float.IsFinite(value) || value == 0.0f)
                {
                    continue;
                }

                points.Add(CreatePoint(value, row, column, width, height, min, max, mean));
            }
        }

        return points.ToArray();
    }

    private static HeightGridPoint CreatePoint(
        float value,
        int row,
        int column,
        int width,
        int height,
        float min,
        float max,
        double mean)
    {
        var xyScale = CalculateHorizontalScale(width, height);
        var centerX = (width - 1) / 2.0f;
        var centerZ = (height - 1) / 2.0f;
        var colorSpan = Math.Max(0.0001, max - min);
        var deviationSpan = Math.Max(0.0001, Math.Max(Math.Abs(min - mean), Math.Abs(max - mean)));
        var position = new Vector3(
            (column - centerX) * xyScale,
            (float)((value - mean) * ViewerHeightScale),
            (row - centerZ) * xyScale);
        return new HeightGridPoint(
            position,
            Math.Clamp((value - min) / colorSpan, 0.0, 1.0),
            Math.Clamp(Math.Abs(value - mean) / deviationSpan, 0.0, 1.0),
            value,
            row,
            column);
    }

    private static float CalculateHorizontalScale(int width, int height) =>
        ViewerHorizontalSpan / Math.Max(1, Math.Max(width - 1, height - 1));
}

public readonly record struct HeightGridPoint(
    Vector3 Position,
    double HeightScalar,
    double DeviationScalar,
    float RawValue,
    int Row = -1,
    int Column = -1);

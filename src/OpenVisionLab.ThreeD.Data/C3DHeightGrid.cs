using System.IO;
using System.Numerics;

namespace OpenVisionLab.ThreeD.Data;

public sealed class C3DHeightGrid
{
    private C3DHeightGrid(
        string sourcePath,
        int width,
        int height,
        int validSampleCount,
        int zeroSampleCount,
        float min,
        float max,
        double mean,
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
        Points = points;
        var xyScale = 10.0f / Math.Max(width - 1, height - 1);
        XHalfExtent = (width - 1) * xyScale / 2.0f;
        ZHalfExtent = (height - 1) * xyScale / 2.0f;
    }

    public string SourcePath { get; }

    public int Width { get; }

    public int Height { get; }

    public int ValidSampleCount { get; }

    public int ZeroSampleCount { get; }

    public float Min { get; }

    public float Max { get; }

    public double Mean { get; }

    public HeightGridPoint[] Points { get; }

    public float XHalfExtent { get; }

    public float ZHalfExtent { get; }

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
        var points = maxRenderedPoints <= 0
            ? []
            : CreatePoints(samples, width, height, Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)sampleCount / maxRenderedPoints))), min, max, mean);
        return new C3DHeightGrid(path, width, height, validCount, zeroCount, min, max, mean, points);
    }

    private static HeightGridPoint[] CreatePoints(float[] samples, int width, int height, int stride, float min, float max, double mean)
    {
        var points = new List<HeightGridPoint>();
        var xyScale = 10.0f / Math.Max(width - 1, height - 1);
        var yScale = 0.0006f;
        var centerX = (width - 1) / 2.0f;
        var centerZ = (height - 1) / 2.0f;
        var colorSpan = Math.Max(0.0001, max - min);
        var deviationSpan = Math.Max(Math.Abs(min - mean), Math.Abs(max - mean));

        for (var row = 0; row < height; row += stride)
        {
            for (var column = 0; column < width; column += stride)
            {
                var value = samples[row * width + column];
                if (!float.IsFinite(value) || value == 0.0f)
                {
                    continue;
                }

                var x = (column - centerX) * xyScale;
                var y = (float)((value - mean) * yScale);
                var z = (row - centerZ) * xyScale;
                var heightScalar = Math.Clamp((value - min) / colorSpan, 0.0, 1.0);
                var deviationScalar = Math.Clamp(Math.Abs(value - mean) / deviationSpan, 0.0, 1.0);
                points.Add(new HeightGridPoint(new Vector3(x, y, z), heightScalar, deviationScalar, value));
            }
        }

        return points.ToArray();
    }
}

public readonly record struct HeightGridPoint(Vector3 Position, double HeightScalar, double DeviationScalar, float RawValue);

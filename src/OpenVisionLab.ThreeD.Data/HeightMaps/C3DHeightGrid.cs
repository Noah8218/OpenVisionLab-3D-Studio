using System.IO;
using System.Diagnostics;
using System.Numerics;
using System.Security.Cryptography;
using OpenVisionLab.Vision3D.FeatureExtraction;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable C3D load snapshot. All cell reads and render-density views use
/// the exact raw samples whose identity and statistics were recorded at load.
/// </summary>
public sealed class C3DHeightGrid
{
    public const float ViewerHorizontalSpan = 10.0f;
    public const float ViewerHeightScale = 0.0006f;

    private readonly float[] samples;

    private C3DHeightGrid(
        string sourcePath,
        long sourceByteLength,
        string contentSha256,
        int width,
        int height,
        int validSampleCount,
        int zeroSampleCount,
        float min,
        float max,
        double mean,
        C3DHeightDistribution heightDistribution,
        C3DHeightGridLoadPerformance loadPerformance,
        int pointStride,
        HeightGridPoint[] points,
        float[] samples)
    {
        SourcePath = sourcePath;
        SourceByteLength = sourceByteLength;
        ContentSha256 = contentSha256;
        Width = width;
        Height = height;
        ValidSampleCount = validSampleCount;
        ZeroSampleCount = zeroSampleCount;
        Min = min;
        Max = max;
        Mean = mean;
        HeightDistribution = heightDistribution;
        LoadPerformance = loadPerformance;
        PointStride = pointStride;
        Points = points;
        this.samples = samples;
        HorizontalScale = CalculateHorizontalScale(width, height);
        XHalfExtent = (width - 1) * HorizontalScale / 2.0f;
        ZHalfExtent = (height - 1) * HorizontalScale / 2.0f;
    }

    public string SourcePath { get; }

    public long SourceByteLength { get; }

    public string ContentSha256 { get; }

    public int Width { get; }

    public int Height { get; }

    public int ValidSampleCount { get; }

    public int ZeroSampleCount { get; }

    public float Min { get; }

    public float Max { get; }

    public double Mean { get; }

    public C3DHeightDistribution HeightDistribution { get; }

    public C3DHeightGridLoadPerformance LoadPerformance { get; }

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

        var value = samples[row * Width + column];
        if (!float.IsFinite(value) || value == 0.0f)
        {
            throw new InvalidDataException($"C3D point ({row}, {column}) is not a finite non-zero sample.");
        }

        return CreatePoint(value, row, column, Width, Height, Min, Max, Mean);
    }

    public HeightGridPoint[] ReadRowRange(int row, int startColumn, int endColumn)
    {
        if (row < 0 || row >= Height)
            throw new ArgumentOutOfRangeException(nameof(row), row, $"C3D row must be between 0 and {Height - 1}.");
        if (startColumn < 0 || endColumn >= Width || startColumn > endColumn)
            throw new ArgumentOutOfRangeException(nameof(startColumn), $"C3D columns must satisfy 0 <= start <= end < {Width}.");

        var points = new List<HeightGridPoint>(endColumn - startColumn + 1);
        for (var column = startColumn; column <= endColumn; column++)
        {
            var value = samples[row * Width + column];
            if (float.IsFinite(value) && value != 0.0f)
                points.Add(CreatePoint(value, row, column, Width, Height, Min, Max, Mean));
        }

        return points.ToArray();
    }

    /// <summary>
    /// Reads finite, non-zero C3D cells along the inclusive grid line from
    /// P1 to P2. The integer raster is deterministic and reversing P1/P2
    /// returns the same cells in reverse order.
    /// </summary>
    public HeightGridPoint[] ReadLineProfile(
        int startRow,
        int startColumn,
        int endRow,
        int endColumn)
    {
        ValidateGridCoordinate(startRow, startColumn, nameof(startRow), nameof(startColumn));
        ValidateGridCoordinate(endRow, endColumn, nameof(endRow), nameof(endColumn));

        var stepCount = Math.Max(Math.Abs(endRow - startRow), Math.Abs(endColumn - startColumn));
        var points = new List<HeightGridPoint>(stepCount + 1);
        for (var step = 0; step <= stepCount; step++)
        {
            var row = InterpolateGridCoordinate(startRow, endRow, step, stepCount);
            var column = InterpolateGridCoordinate(startColumn, endColumn, step, stepCount);
            var value = samples[row * Width + column];
            if (float.IsFinite(value) && value != 0.0f)
            {
                points.Add(CreatePoint(value, row, column, Width, Height, Min, Max, Mean));
            }
        }

        return points.ToArray();
    }

    /// <summary>
    /// Reads the complete source map in row-major order for an inspection tool.
    /// C3D zero and non-finite samples are represented as <see cref="double.NaN"/>,
    /// which is the declared missing-value contract at the inspection boundary.
    /// </summary>
    public double[] ReadHeightMapValues()
    {
        var values = new double[checked(Width * Height)];
        for (var index = 0; index < values.Length; index++)
        {
            var value = samples[index];
            values[index] = float.IsFinite(value) && value != 0.0f
                ? value
                : double.NaN;
        }

        return values;
    }

    /// <summary>
    /// Creates another render-density view of this exact loaded snapshot.
    /// The source path is not reopened.
    /// </summary>
    public C3DHeightGrid WithMaxRenderedPoints(int maxRenderedPoints)
    {
        var pointStride = CalculatePointStride(samples.Length, maxRenderedPoints);
        if (pointStride == PointStride)
        {
            return this;
        }

        var pointsStart = Stopwatch.GetTimestamp();
        var points = pointStride == 0
            ? []
            : CreatePoints(
                samples,
                Width,
                Height,
                pointStride,
                Min,
                Max,
                Mean,
                CancellationToken.None,
                progress: null);
        var pointsMilliseconds = Stopwatch.GetElapsedTime(pointsStart).TotalMilliseconds;
        var loadPerformance = LoadPerformance with
        {
            RenderPointsMilliseconds = pointsMilliseconds,
            TotalMilliseconds = LoadPerformance.TotalMilliseconds
                - LoadPerformance.RenderPointsMilliseconds
                + pointsMilliseconds
        };
        return new C3DHeightGrid(
            SourcePath,
            SourceByteLength,
            ContentSha256,
            Width,
            Height,
            ValidSampleCount,
            ZeroSampleCount,
            Min,
            Max,
            Mean,
            HeightDistribution,
            loadPerformance,
            pointStride,
            points,
            samples);
    }

    public static C3DHeightGrid Load(string path, int maxRenderedPoints = 55000)
        => Load(path, maxRenderedPoints, CancellationToken.None, progress: null);

    public static C3DHeightGrid Load(
        string path,
        int maxRenderedPoints,
        CancellationToken cancellationToken,
        IProgress<double>? progress)
    {
        var totalStart = Stopwatch.GetTimestamp();
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0.0);
        using var reader = new BinaryReader(File.OpenRead(path));
        var layout = C3DSourceTopology.ReadAndValidate(reader.BaseStream);
        var width = layout.Width;
        var height = layout.Height;
        var sampleCount = layout.SampleCount;

        var samples = new float[sampleCount];
        var readStart = Stopwatch.GetTimestamp();
        for (var i = 0; i < samples.Length; i++)
        {
            if ((i & 0x3fff) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(5.0 + 60.0 * i / Math.Max(1, samples.Length));
            }

            var value = reader.ReadSingle();
            samples[i] = value;
        }
        var readMilliseconds = Stopwatch.GetElapsedTime(readStart).TotalMilliseconds;
        progress?.Report(65.0);
        var distributionStart = Stopwatch.GetTimestamp();
        var summary = new HeightGridSummaryTool().Execute(
            samples,
            new HeightGridSummaryOptions
            {
                ZeroIsMissing = true,
                DistributionBinCount = C3DHeightDistribution.DefaultBinCount
            },
            cancellationToken);
        if (!summary.Success)
        {
            throw new InvalidDataException(
                $"C3D contains no non-zero finite samples: {path}");
        }
        var validCount = summary.ValidSampleCount;
        var zeroCount = summary.ZeroSampleCount;
        var min = checked((float)summary.Minimum);
        var max = checked((float)summary.Maximum);
        var mean = summary.Mean;
        var heightDistribution = C3DHeightDistribution.Create(summary);
        var distributionMilliseconds = Stopwatch.GetElapsedTime(distributionStart).TotalMilliseconds;
        progress?.Report(78.0);
        var pointStride = CalculatePointStride(sampleCount, maxRenderedPoints);
        var pointsStart = Stopwatch.GetTimestamp();
        var points = pointStride == 0
            ? []
            : CreatePoints(samples, width, height, pointStride, min, max, mean, cancellationToken, progress);
        var pointsMilliseconds = Stopwatch.GetElapsedTime(pointsStart).TotalMilliseconds;
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(96.0);
        var sourceByteLength = reader.BaseStream.Length;
        reader.BaseStream.Position = 0;
        var hashStart = Stopwatch.GetTimestamp();
        var contentSha256 = Convert.ToHexString(SHA256.HashData(reader.BaseStream));
        var hashMilliseconds = Stopwatch.GetElapsedTime(hashStart).TotalMilliseconds;
        cancellationToken.ThrowIfCancellationRequested();
        var loadPerformance = new C3DHeightGridLoadPerformance(
            readMilliseconds,
            distributionMilliseconds,
            pointsMilliseconds,
            hashMilliseconds,
            Stopwatch.GetElapsedTime(totalStart).TotalMilliseconds);
        var grid = new C3DHeightGrid(
            path,
            sourceByteLength,
            contentSha256,
            width,
            height,
            validCount,
            zeroCount,
            min,
            max,
            mean,
            heightDistribution,
            loadPerformance,
            pointStride,
            points,
            samples);
        progress?.Report(100.0);
        return grid;
    }

    private static HeightGridPoint[] CreatePoints(
        float[] samples,
        int width,
        int height,
        int stride,
        float min,
        float max,
        double mean,
        CancellationToken cancellationToken,
        IProgress<double>? progress)
    {
        var points = new List<HeightGridPoint>();
        var progressRowInterval = Math.Max(stride, height / 32);
        for (var row = 0; row < height; row += stride)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row % progressRowInterval < stride)
            {
                progress?.Report(78.0 + 14.0 * row / Math.Max(1, height));
            }
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

    private static int CalculatePointStride(int sampleCount, int maxRenderedPoints) =>
        maxRenderedPoints <= 0
            ? 0
            : Math.Max(1, (int)Math.Ceiling(Math.Sqrt((double)sampleCount / maxRenderedPoints)));

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
        var colorSpan = max > min ? (double)max - min : 1.0;
        var deviationSpan = Math.Max(0.0001, Math.Max(Math.Abs(min - mean), Math.Abs(max - mean)));
        var position = new Vector3(
            (column - centerX) * xyScale,
            (float)((value - mean) * ViewerHeightScale),
            (row - centerZ) * xyScale);
        return new HeightGridPoint(
            position,
            Math.Clamp(((double)value - min) / colorSpan, 0.0, 1.0),
            Math.Clamp(Math.Abs(value - mean) / deviationSpan, 0.0, 1.0),
            value,
            row,
            column);
    }

    private static float CalculateHorizontalScale(int width, int height) =>
        ViewerHorizontalSpan / Math.Max(1, Math.Max(width - 1, height - 1));

    private void ValidateGridCoordinate(
        int row,
        int column,
        string rowParameterName,
        string columnParameterName)
    {
        if (row < 0 || row >= Height)
        {
            throw new ArgumentOutOfRangeException(rowParameterName, row, $"C3D row must be between 0 and {Height - 1}.");
        }

        if (column < 0 || column >= Width)
        {
            throw new ArgumentOutOfRangeException(columnParameterName, column, $"C3D column must be between 0 and {Width - 1}.");
        }
    }

    private static int InterpolateGridCoordinate(int start, int end, int step, int stepCount)
    {
        if (stepCount == 0)
        {
            return start;
        }

        var numerator = (long)start * (stepCount - step) + (long)end * step;
        return checked((int)((numerator + stepCount / 2L) / stepCount));
    }
}

public readonly record struct HeightGridPoint(
    Vector3 Position,
    double HeightScalar,
    double DeviationScalar,
    float RawValue,
    int Row = -1,
    int Column = -1);

public readonly record struct C3DHeightGridLoadPerformance(
    double ReadAndStatisticsMilliseconds,
    double DistributionMilliseconds,
    double RenderPointsMilliseconds,
    double HashMilliseconds,
    double TotalMilliseconds);

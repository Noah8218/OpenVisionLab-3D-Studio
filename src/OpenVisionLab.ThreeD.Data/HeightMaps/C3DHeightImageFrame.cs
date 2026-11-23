using System.Security.Cryptography;
using System.Runtime.InteropServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable, full-resolution height-image projection of one C3D or signed-delta grid.
/// Pixel X is the source column and pixel Y is the source row. No flip,
/// interpolation, dilation, or sampling is permitted at this boundary.
/// </summary>
public sealed class C3DHeightImageFrame
{
    public const string CoordinateMapping =
        "pixelX=column;pixelY=row;no-flip;one-source-cell-per-pixel";

    private static readonly (byte B, byte G, byte R, byte A) MissingPixel =
        (0x27, 0x18, 0x11, byte.MaxValue);
    private static readonly (byte B, byte G, byte R, byte A) MissingOverlayPixel =
        (0x8D, 0x1D, 0xE1, byte.MaxValue);

    private readonly ReadOnlyMemory<double> values;
    private readonly byte[] bgra32Pixels;
    private double[]? sortedDisplaySamples;

    private C3DHeightImageFrame(
        string sourceEntityId,
        string sourcePath,
        string sourceContentSha256,
        string unit,
        string frameId,
        int width,
        int height,
        int validCount,
        int missingCount,
        double minimum,
        double maximum,
        double mean,
        double defaultDisplayMinimum,
        double defaultDisplayMaximum,
        int gridOriginColumn,
        int gridOriginRow,
        C3DInvalidCellMap invalidCellMap,
        ReadOnlyMemory<double> values,
        byte[] bgra32Pixels,
        string pixelSha256)
    {
        SourceEntityId = sourceEntityId;
        SourcePath = sourcePath;
        SourceContentSha256 = sourceContentSha256;
        Unit = unit;
        FrameId = frameId;
        Width = width;
        Height = height;
        ValidCount = validCount;
        MissingCount = missingCount;
        Minimum = minimum;
        Maximum = maximum;
        Mean = mean;
        DefaultDisplayMinimum = defaultDisplayMinimum;
        DefaultDisplayMaximum = defaultDisplayMaximum;
        GridOriginColumn = gridOriginColumn;
        GridOriginRow = gridOriginRow;
        InvalidCellMap = invalidCellMap;
        this.values = values;
        this.bgra32Pixels = bgra32Pixels;
        PixelSha256 = pixelSha256;
        DefaultDisplayFrame = new C3DHeightImageDisplayFrame(
            Width,
            Height,
            C3DHeightImagePalette.Height,
            DefaultDisplayMinimum,
            DefaultDisplayMaximum,
            bgra32Pixels,
            pixelSha256,
            C3DHeightImageInvalidOverlayMode.Hidden,
            0,
            invalidCellMap.Sha256);
    }

    public string SourceEntityId { get; }
    public string SourcePath { get; }
    public string SourceContentSha256 { get; }
    public string Unit { get; }
    public string FrameId { get; }
    public int Width { get; }
    public int Height { get; }
    public int ValidCount { get; }
    public int MissingCount { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public double Mean { get; }
    public double DefaultDisplayMinimum { get; }
    public double DefaultDisplayMaximum { get; }
    public int GridOriginColumn { get; }
    public int GridOriginRow { get; }
    public C3DInvalidCellMap InvalidCellMap { get; }
    public string PixelSha256 { get; }
    public C3DHeightImageDisplayFrame DefaultDisplayFrame { get; }
    public int Stride => checked(Width * 4);
    public ReadOnlyMemory<byte> Bgra32Pixels => bgra32Pixels;
    private IReadOnlyList<int>? displayHistogramBins;
    public IReadOnlyList<int> DisplayHistogramBins
    {
        get
        {
            if (displayHistogramBins is null)
            {
                // 22MB 복사를 피하려고 전체를 덮는 배열이면 그대로 넘긴다(도구는 읽기만 한다).
                var summaryInput = System.Runtime.InteropServices.MemoryMarshal.TryGetArray(values, out var segment) && segment.Offset == 0 && segment.Count == segment.Array!.Length
                    ? segment.Array : values.ToArray();
                var summary = new OpenVisionLab.Vision3D.FeatureExtraction.HeightDistributionStatisticsTool().Execute(summaryInput,
                    new OpenVisionLab.Vision3D.FeatureExtraction.HeightDistributionStatisticsOptions { ZeroIsMissing = false, BinCount = C3DHeightDistribution.DefaultBinCount });
                displayHistogramBins = summary.Success ? Array.AsReadOnly(summary.Bins.Select(count => checked((int)count)).ToArray()) : [];
            }
            return displayHistogramBins;
        }
    }
    public ReadOnlyMemory<double> DisplayValues => values;
    public ReadOnlyMemory<double> SortedDisplaySamples => sortedDisplaySamples ??= C3DHeightImageFrame.SortDisplaySamples(values.Span);
    public bool IsSignedDelta => SourcePath.StartsWith("signed-delta://", StringComparison.Ordinal);

    public static C3DHeightImageFrame Create(
        C3DHeightFieldSnapshot source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();

        var values = source.Values;
        var invalidCellMap = C3DInvalidCellMap.Create(source);
        var pixels = RenderPixels(
            values,
            invalidCellMap,
            C3DHeightImagePalette.Height,
            source.Minimum,
            source.Maximum,
            C3DHeightImageInvalidOverlayMode.Hidden,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return new C3DHeightImageFrame(
            source.EntityId,
            source.SourcePath,
            source.ContentSha256,
            source.Unit,
            source.FrameId,
            source.Width,
            source.Height,
            source.ValidCount,
            source.MissingCount,
            source.Minimum,
            source.Maximum,
            source.Mean,
            source.Minimum,
            source.Maximum,
            0,
            0,
            invalidCellMap,
            values,
            pixels,
            Convert.ToHexString(SHA256.HashData(pixels)));
    }

    public static C3DHeightImageFrame Create(
        C3DSignedHeightDeltaArtifact source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.TryValidate(out var message))
        {
            throw new InvalidDataException(message);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var values = source.Values.ToArray();
        var invalidCellMap = C3DInvalidCellMap.CreateSignedDelta(
            source.Width,
            source.Height,
            values,
            source.MissingEitherSampleCount);
        if (!C3DSignedHeightDeltaViewerContract.TryGetDisplayRange(
                values,
                out var displayMinimum,
                out var displayMaximum,
                out message))
        {
            throw new InvalidDataException(message);
        }

        var finiteValues = values.Where(double.IsFinite).ToArray();
        var minimum = finiteValues.Min();
        var maximum = finiteValues.Max();
        var mean = finiteValues.Average();
        var pixels = RenderPixels(
            values,
            invalidCellMap,
            C3DHeightImagePalette.Height,
            displayMinimum,
            displayMaximum,
            C3DHeightImageInvalidOverlayMode.Hidden,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return new C3DHeightImageFrame(
            source.OutputEntityId,
            $"signed-delta://{source.ContentSha256}",
            source.ContentSha256,
            source.Unit,
            source.FrameId,
            source.Width,
            source.Height,
            source.PairedValidSampleCount,
            source.MissingEitherSampleCount,
            minimum,
            maximum,
            mean,
            displayMinimum,
            displayMaximum,
            source.GridOriginColumn,
            source.GridOriginRow,
            invalidCellMap,
            values,
            pixels,
            Convert.ToHexString(SHA256.HashData(pixels)));
    }

    /// <summary>Projects SDK scalar evidence without treating a finite zero model height as C3D missing data.</summary>
    public static C3DHeightImageFrame CreateScalarMap(HeightMap3D source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        cancellationToken.ThrowIfCancellationRequested();
        if (source.PlanarUnit != "grid-index" || source.ColumnPitch != 1.0 || source.RowPitch != 1.0
            || source.OriginX != Math.Truncate(source.OriginX) || source.OriginY != Math.Truncate(source.OriginY))
            throw new InvalidDataException("Scalar evidence requires a native grid-index map with integer origins.");
        var values = source.CopyValues();
        var summary = new HeightDistributionStatisticsTool().Execute(values,
            new HeightDistributionStatisticsOptions { ZeroIsMissing = false, BinCount = 1 });
        if (!summary.Success || !summary.HasFiniteSamples) throw new InvalidDataException(summary.Message);
        var mask = C3DInvalidCellMap.CreateScalarMap(source.Columns, source.Rows, values, summary.MissingSampleCount);
        var pixels = RenderPixels(values, mask, C3DHeightImagePalette.Height, summary.Minimum, summary.Maximum,
            C3DHeightImageInvalidOverlayMode.Hidden, cancellationToken);
        var contentHash = Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(values.AsSpan())));
        return new C3DHeightImageFrame(source.SourceId, $"height-map://{contentHash}", contentHash, source.HeightUnit, source.FrameId,
            source.Columns, source.Rows, summary.ValidSampleCount, summary.MissingSampleCount, summary.Minimum, summary.Maximum,
            summary.Mean, summary.Minimum, summary.Maximum, checked((int)source.OriginX), checked((int)source.OriginY),
            mask, values, pixels, Convert.ToHexString(SHA256.HashData(pixels)));
    }

    public C3DHeightImageDisplayFrame CreateDisplayFrame(
        C3DHeightImagePalette palette,
        double minimum,
        double maximum,
        CancellationToken cancellationToken = default)
        => CreateDisplayFrame(
            palette,
            minimum,
            maximum,
            C3DHeightImageInvalidOverlayMode.Hidden,
            cancellationToken);

    public C3DHeightImageDisplayFrame CreateDisplayFrame(
        C3DHeightImagePalette palette,
        double minimum,
        double maximum,
        C3DHeightImageInvalidOverlayMode invalidOverlayMode,
        CancellationToken cancellationToken = default)
        => CreateDisplayFrame(palette, minimum, maximum, invalidOverlayMode, new HeightDisplayOptions(), cancellationToken);

    public C3DHeightImageDisplayFrame CreateDisplayFrame(
        C3DHeightImagePalette palette,
        double minimum,
        double maximum,
        C3DHeightImageInvalidOverlayMode invalidOverlayMode,
        HeightDisplayOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(palette))
        {
            throw new ArgumentOutOfRangeException(nameof(palette));
        }

        if (!Enum.IsDefined(invalidOverlayMode))
        {
            throw new ArgumentOutOfRangeException(nameof(invalidOverlayMode));
        }

        if (!double.IsFinite(minimum))
        {
            throw new ArgumentOutOfRangeException(nameof(minimum));
        }

        if (!double.IsFinite(maximum) || maximum < minimum)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum));
        }

        // 탭을 오갈 때 같은 표시 설정으로 같은 지도를 다시 그리는 일이 잦다. 같은 설정의 결과(불변)를 최근 몇 개 다시 쓴다.
        var sanitizedOptions = options.Sanitize();
        var key = new DisplayFrameKey(palette, minimum, maximum, invalidOverlayMode, sanitizedOptions);
        lock (displayFrameCache)
        {
            var index = displayFrameCache.FindIndex(entry => entry.Key == key);
            if (index >= 0)
            {
                var hit = displayFrameCache[index];
                displayFrameCache.RemoveAt(index);
                displayFrameCache.Insert(0, hit);
                return hit.Frame;
            }
        }

        var pixels = RenderPixels(
            values,
            InvalidCellMap,
            palette,
            minimum,
            maximum,
            invalidOverlayMode,
            cancellationToken,
            sanitizedOptions);
        var frame = new C3DHeightImageDisplayFrame(
            Width,
            Height,
            palette,
            minimum,
            maximum,
            pixels,
            Convert.ToHexString(SHA256.HashData(pixels)),
            invalidOverlayMode,
            invalidOverlayMode == C3DHeightImageInvalidOverlayMode.Visible
                ? InvalidCellMap.MissingCellCount
                : 0,
            InvalidCellMap.Sha256);
        lock (displayFrameCache)
        {
            displayFrameCache.RemoveAll(entry => entry.Key == key);
            displayFrameCache.Insert(0, (key, frame));
            while (displayFrameCache.Count > DisplayFrameCacheCapacity) displayFrameCache.RemoveAt(displayFrameCache.Count - 1);
        }

        return frame;
    }

    private const int DisplayFrameCacheCapacity = 3;
    private readonly List<(DisplayFrameKey Key, C3DHeightImageDisplayFrame Frame)> displayFrameCache = [];

    private readonly record struct DisplayFrameKey(
        C3DHeightImagePalette Palette,
        double Minimum,
        double Maximum,
        C3DHeightImageInvalidOverlayMode InvalidOverlayMode,
        HeightDisplayOptions Options);

    public bool TryGetCell(int pixelX, int pixelY, out C3DHeightImageCell cell)
    {
        if (pixelX < 0 || pixelX >= Width || pixelY < 0 || pixelY >= Height)
        {
            cell = default;
            return false;
        }

        var value = values.Span[checked(pixelY * Width + pixelX)];
        cell = new C3DHeightImageCell(
            pixelX,
            pixelY,
            pixelY,
            pixelX,
            value,
            double.IsFinite(value));
        return true;
    }

    // DQ-08: these ranges and counts describe colour mapping, never measurement or verdicts.
    public static double[] SortDisplaySamples(ReadOnlySpan<double> values)
    {
        var sorted = new double[values.Length];
        var count = 0;
        foreach (var value in values) if (double.IsFinite(value)) sorted[count++] = value;
        Array.Resize(ref sorted, count);
        Array.Sort(sorted);
        return sorted;
    }

    public static (double Minimum, double Maximum) GetDistributionDisplayRange(ReadOnlySpan<double> sorted)
    {
        if (sorted.IsEmpty) return (double.NaN, double.NaN);
        var trim = sorted.Length < 20 ? 0 : (int)Math.Round(0.003 * (sorted.Length - 1), MidpointRounding.AwayFromZero);
        var minimum = sorted[trim];
        var maximum = sorted[sorted.Length - 1 - trim];
        return minimum < maximum ? (minimum, maximum) : (sorted[0], sorted[^1]);
    }

    public static bool TryGetRegionDisplayRange(ReadOnlySpan<double> values, int width, int height, ToolRecipeGridRectangle? rectangle,
        out double minimum, out double maximum)
    {
        minimum = double.PositiveInfinity;
        maximum = double.NegativeInfinity;
        if (rectangle is null || width <= 0 || height <= 0 || (long)width * height != values.Length
            || ToolRecipeGridRectangleGeometry.Validate(rectangle, width, height).Count != 0) return false;
        for (var row = rectangle.Row; row < rectangle.Row + rectangle.RowCount; row++)
        for (var column = rectangle.Column; column < rectangle.Column + rectangle.ColumnCount; column++)
        {
            var value = values[row * width + column];
            if (!double.IsFinite(value)) continue;
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }
        return double.IsFinite(minimum) && double.IsFinite(maximum) && minimum < maximum;
    }

    public static (double Below, double Above) GetOutsideDisplayPercentages(ReadOnlySpan<double> sorted, double minimum, double maximum)
    {
        if (sorted.IsEmpty) return (0, 0);
        return (FindBound(sorted, minimum, false) * 100.0 / sorted.Length,
            (sorted.Length - FindBound(sorted, maximum, true)) * 100.0 / sorted.Length);
    }

    /// <summary>
    /// 정렬하지 않은 값을 한 번 훑어 <see cref="GetOutsideDisplayPercentages"/>와 같은 값을 낸다.
    /// 전체 정렬(수백만 셀에서 수백 ms)을 피하려는 용도이며, 유한한 셀만 세고 경계값은 범위 안으로 본다.
    /// </summary>
    public static (double Below, double Above) GetOutsideDisplayPercentagesFromValues(ReadOnlySpan<double> values, double minimum, double maximum)
    {
        var finite = 0;
        var below = 0;
        var notAbove = 0;
        foreach (var value in values)
        {
            if (!double.IsFinite(value)) continue;
            finite++;
            if (value < minimum) below++;
            if (value < maximum || value == maximum) notAbove++;
        }

        return finite == 0 ? (0, 0) : (below * 100.0 / finite, (finite - notAbove) * 100.0 / finite);
    }

    private static int FindBound(ReadOnlySpan<double> sorted, double value, bool upper)
    {
        var left = 0;
        var right = sorted.Length;
        while (left < right)
        {
            var middle = left + (right - left) / 2;
            if (sorted[middle] < value || upper && sorted[middle] == value) left = middle + 1;
            else right = middle;
        }
        return left;
    }

    private static byte[] RenderPixels(
        ReadOnlyMemory<double> values,
        C3DInvalidCellMap invalidCellMap,
        C3DHeightImagePalette palette,
        double minimum,
        double maximum,
        C3DHeightImageInvalidOverlayMode invalidOverlayMode,
        CancellationToken cancellationToken,
        HeightDisplayOptions? options = null)
    {
        var pixels = new byte[checked(values.Length * 4)];
        var hasRange = double.IsFinite(minimum)
                       && double.IsFinite(maximum)
                       && maximum > minimum;
        var hasDisplayStyle = options is { ColorStyle: not "continuous" } || options?.HighlightOutside == true;
        var paletteName = palette == C3DHeightImagePalette.Monochrome ? "Solid" : palette.ToString();

        // 셀마다 독립적으로 색을 정해 서로 다른 픽셀 위치에만 쓰므로 청크 단위로 병렬 처리해도 결과 바이트는 같다.
        // 2.8M 셀이면 한 스레드로 약 80ms라 탭을 오갈 때 UI 스레드가 그만큼 멈춘다.
        void FillRange(int start, int end)
        {
            var span = values.Span;
            for (var index = start; index < end; index++)
            {
                if ((index & 0x3fff) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var pixelOffset = index * 4;
                if (invalidCellMap.IsMissingIndex(index))
                {
                    var missingColor =
                        invalidOverlayMode == C3DHeightImageInvalidOverlayMode.Visible
                            ? MissingOverlayPixel
                            : MissingPixel;
                    pixels[pixelOffset] = missingColor.B;
                    pixels[pixelOffset + 1] = missingColor.G;
                    pixels[pixelOffset + 2] = missingColor.R;
                    pixels[pixelOffset + 3] = missingColor.A;
                    continue;
                }

                var normalized = hasRange
                    ? (span[index] - minimum) / (maximum - minimum)
                    : 0.5;
                (byte R, byte G, byte B) color;
                if (hasDisplayStyle)
                {
                    var displayColor = C3DPointMapPalette.DisplayColor(span[index], minimum, maximum, paletteName, options!, index);
                    color = ((byte)(displayColor.R * 255), (byte)(displayColor.G * 255), (byte)(displayColor.B * 255));
                }
                else color = palette switch
                {
                    C3DHeightImagePalette.Grayscale => C3DPointMapPalette.GrayscaleBytes(normalized),
                    C3DHeightImagePalette.Thermal => C3DPointMapPalette.ThermalBytes(normalized),
                    C3DHeightImagePalette.ColorVisionAssist => C3DPointMapPalette.ColorVisionAssistBytes(normalized),
                    C3DHeightImagePalette.Monochrome => (R: (byte)115, G: (byte)155, B: (byte)194),
                    _ => C3DPointMapPalette.HeightBytes(normalized)
                };
                pixels[pixelOffset] = color.B;
                pixels[pixelOffset + 1] = color.G;
                pixels[pixelOffset + 2] = color.R;
                pixels[pixelOffset + 3] = byte.MaxValue;
            }
        }

        const int ParallelCellThreshold = 1 << 18;
        const int ChunkCells = 1 << 16;
        if (values.Length < ParallelCellThreshold)
        {
            FillRange(0, values.Length);
        }
        else
        {
            var chunkCount = (values.Length + ChunkCells - 1) / ChunkCells;
            try
            {
                Parallel.For(
                    0,
                    chunkCount,
                    new ParallelOptions { CancellationToken = cancellationToken },
                    chunk => FillRange(chunk * ChunkCells, Math.Min(values.Length, (chunk + 1) * ChunkCells)));
            }
            catch (AggregateException exception) when (exception.InnerExceptions.Count > 0)
            {
                // 직렬 처리 때와 같은 예외 형식을 유지한다.
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerExceptions[0]).Throw();
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return pixels;
    }
}

public enum C3DHeightImagePalette
{
    Height,
    Grayscale,
    Thermal,
    Monochrome,
    ColorVisionAssist
}

public enum C3DHeightImageInvalidOverlayMode
{
    Hidden,
    Visible
}

public sealed class C3DHeightImageDisplayFrame
{
    private readonly byte[] bgra32Pixels;

    internal C3DHeightImageDisplayFrame(
        int width,
        int height,
        C3DHeightImagePalette palette,
        double minimum,
        double maximum,
        byte[] bgra32Pixels,
        string pixelSha256,
        C3DHeightImageInvalidOverlayMode invalidOverlayMode,
        int invalidOverlayPixelCount,
        string invalidCellMapSha256)
    {
        Width = width;
        Height = height;
        Palette = palette;
        Minimum = minimum;
        Maximum = maximum;
        this.bgra32Pixels = bgra32Pixels;
        PixelSha256 = pixelSha256;
        InvalidOverlayMode = invalidOverlayMode;
        InvalidOverlayPixelCount = invalidOverlayPixelCount;
        InvalidCellMapSha256 = invalidCellMapSha256;
    }

    public int Width { get; }
    public int Height { get; }
    public int Stride => checked(Width * 4);
    public C3DHeightImagePalette Palette { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public ReadOnlyMemory<byte> Bgra32Pixels => bgra32Pixels;
    public string PixelSha256 { get; }
    public C3DHeightImageInvalidOverlayMode InvalidOverlayMode { get; }
    public int InvalidOverlayPixelCount { get; }
    public string InvalidCellMapSha256 { get; }
}

public readonly record struct C3DHeightImageCell(
    int PixelX,
    int PixelY,
    int Row,
    int Column,
    double RawHeight,
    bool IsValid);

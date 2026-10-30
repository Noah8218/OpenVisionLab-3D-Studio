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
                var summary = new OpenVisionLab.Vision3D.FeatureExtraction.HeightDistributionStatisticsTool().Execute(values.ToArray(),
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
            values.Span,
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

        var pixels = RenderPixels(
            values.Span,
            InvalidCellMap,
            palette,
            minimum,
            maximum,
            invalidOverlayMode,
            cancellationToken,
            options.Sanitize());
        return new C3DHeightImageDisplayFrame(
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
    }

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
        ReadOnlySpan<double> values,
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

        for (var index = 0; index < values.Length; index++)
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
                ? (values[index] - minimum) / (maximum - minimum)
                : 0.5;
            (byte R, byte G, byte B) color;
            if (hasDisplayStyle)
            {
                var displayColor = C3DPointMapPalette.DisplayColor(values[index], minimum, maximum, paletteName, options!, index);
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

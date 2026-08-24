using System.Security.Cryptography;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Immutable, full-resolution height-image projection of one C3D grid.
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

    private C3DHeightImageFrame(
        C3DHeightFieldSnapshot source,
        C3DInvalidCellMap invalidCellMap,
        ReadOnlyMemory<double> values,
        byte[] bgra32Pixels,
        string pixelSha256)
    {
        SourceEntityId = source.EntityId;
        SourcePath = source.SourcePath;
        SourceContentSha256 = source.ContentSha256;
        Unit = source.Unit;
        FrameId = source.FrameId;
        Width = source.Width;
        Height = source.Height;
        ValidCount = source.ValidCount;
        MissingCount = source.MissingCount;
        Minimum = source.Minimum;
        Maximum = source.Maximum;
        Mean = source.Mean;
        InvalidCellMap = invalidCellMap;
        this.values = values;
        this.bgra32Pixels = bgra32Pixels;
        PixelSha256 = pixelSha256;
        DefaultDisplayFrame = new C3DHeightImageDisplayFrame(
            Width,
            Height,
            C3DHeightImagePalette.Height,
            Minimum,
            Maximum,
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
    public C3DInvalidCellMap InvalidCellMap { get; }
    public string PixelSha256 { get; }
    public C3DHeightImageDisplayFrame DefaultDisplayFrame { get; }
    public int Stride => checked(Width * 4);
    public ReadOnlyMemory<byte> Bgra32Pixels => bgra32Pixels;

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
            source,
            invalidCellMap,
            values,
            pixels,
            Convert.ToHexString(SHA256.HashData(pixels)));
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
            cancellationToken);
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

    private static byte[] RenderPixels(
        ReadOnlySpan<double> values,
        C3DInvalidCellMap invalidCellMap,
        C3DHeightImagePalette palette,
        double minimum,
        double maximum,
        C3DHeightImageInvalidOverlayMode invalidOverlayMode,
        CancellationToken cancellationToken)
    {
        var pixels = new byte[checked(values.Length * 4)];
        var hasRange = double.IsFinite(minimum)
                       && double.IsFinite(maximum)
                       && maximum > minimum;

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
            var color = palette switch
            {
                C3DHeightImagePalette.Grayscale =>
                    C3DPointMapPalette.GrayscaleBytes(normalized),
                C3DHeightImagePalette.Thermal =>
                    C3DPointMapPalette.ThermalBytes(normalized),
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
    Thermal
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

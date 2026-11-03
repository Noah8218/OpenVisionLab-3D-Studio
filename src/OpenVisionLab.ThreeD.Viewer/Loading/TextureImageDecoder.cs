using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>Display-image buffer admission; no inspection or source-grid transform.</summary>
internal static class TextureImageDecoder
{
    // Retain the existing C3D image policy for the owned BGRA buffer. This is
    // not a bound on encoded input, native codec memory or total process RAM.
    internal const long MaximumDecodedPixels = 32L * 1024 * 1024;

    internal static (int Stride, int ByteCount) ValidateBgra32Size(
        int width, int height, int maximumTextureDimension)
    {
        if (width <= 0 || height <= 0)
            throw new InvalidDataException("Texture image dimensions must be positive.");
        if (maximumTextureDimension <= 0)
            throw new InvalidDataException("Texture image size limit is unavailable.");
        if (width > maximumTextureDimension || height > maximumTextureDimension)
            throw new InvalidDataException($"Texture exceeds this viewer's {maximumTextureDimension} pixel image limit.");
        var pixelCount = checked((long)width * height);
        if (pixelCount > MaximumDecodedPixels)
            throw new InvalidDataException("Texture image must contain at most 32 million pixels.");
        return (checked(width * 4), checked((int)(pixelCount * 4)));
    }

    internal static (int Width, int Height, byte[] Pixels) DecodeBgra32(
        byte[] encodedImage, bool requireSingleFrame, int maximumTextureDimension)
    {
        ArgumentNullException.ThrowIfNull(encodedImage);
        using var stream = new MemoryStream(encodedImage, writable: false);
        // Keep the encoded stream alive through CopyPixels. Avoid OnLoad's
        // full image cache before dimensions have passed display admission.
        try
        {
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.None);
            if (requireSingleFrame && decoder.Frames.Count != 1)
                throw new InvalidDataException("Select a single-frame texture image.");
            if (decoder.Frames.Count == 0)
                throw new InvalidOperationException("Texture image has no frames.");
            BitmapSource bitmap = decoder.Frames[0];
            var width = bitmap.PixelWidth;
            var height = bitmap.PixelHeight;
            var layout = ValidateBgra32Size(width, height, maximumTextureDimension);
            if (bitmap.Format != PixelFormats.Bgra32)
                bitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
            var pixels = new byte[layout.ByteCount];
            bitmap.CopyPixels(pixels, layout.Stride, 0);
            return (width, height, pixels);
        }
        catch (FileFormatException exception)
        {
            // WPF's format exception derives from FormatException. Expose a
            // read failure to texture hosts, retaining the original codec cause.
            throw new InvalidDataException("Texture image format is invalid.", exception);
        }
    }
}

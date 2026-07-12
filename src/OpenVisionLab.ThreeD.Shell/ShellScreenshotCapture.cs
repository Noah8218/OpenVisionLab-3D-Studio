using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenVisionLab.ThreeD.Shell;

internal sealed record ShellScreenshotQuality(
    bool IsAcceptable,
    double NearBlackRatio,
    double NearWhiteRatio,
    byte MinimumLuminance,
    byte MaximumLuminance,
    int SampledPixels)
{
    public string Summary =>
        $"acceptable={IsAcceptable}|blackRatio={NearBlackRatio:F4}|whiteRatio={NearWhiteRatio:F4}|luminance={MinimumLuminance}..{MaximumLuminance}|sampledPixels={SampledPixels}";
}

internal sealed record ShellScreenshotCaptureResult(
    RenderTargetBitmap Bitmap,
    ShellScreenshotQuality Quality);

internal static class ShellScreenshotCapture
{
    private const double MaximumNearBlackRatio = 0.55;
    private const double MaximumNearWhiteRatio = 0.97;
    private const int MinimumLuminanceRange = 64;

    public static ShellScreenshotCaptureResult Capture(FrameworkElement target)
    {
        var width = Math.Max(1, (int)Math.Ceiling(target.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(target.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(target);
        return new ShellScreenshotCaptureResult(bitmap, Assess(bitmap));
    }

    public static void Save(BitmapSource bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static ShellScreenshotQuality Assess(BitmapSource bitmap)
    {
        var stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        var nearBlack = 0;
        var nearWhite = 0;
        var minimumLuminance = byte.MaxValue;
        var maximumLuminance = byte.MinValue;

        for (var index = 0; index < pixels.Length; index += 4)
        {
            var blue = pixels[index];
            var green = pixels[index + 1];
            var red = pixels[index + 2];
            var luminance = (byte)((red * 54 + green * 183 + blue * 19) >> 8);

            if (red <= 8 && green <= 8 && blue <= 8)
            {
                nearBlack++;
            }

            if (red >= 247 && green >= 247 && blue >= 247)
            {
                nearWhite++;
            }

            minimumLuminance = Math.Min(minimumLuminance, luminance);
            maximumLuminance = Math.Max(maximumLuminance, luminance);
        }

        var sampledPixels = Math.Max(1, bitmap.PixelWidth * bitmap.PixelHeight);
        var nearBlackRatio = (double)nearBlack / sampledPixels;
        var nearWhiteRatio = (double)nearWhite / sampledPixels;
        var isAcceptable = bitmap.PixelWidth >= 320
            && bitmap.PixelHeight >= 240
            && nearBlackRatio <= MaximumNearBlackRatio
            && nearWhiteRatio <= MaximumNearWhiteRatio
            && maximumLuminance - minimumLuminance >= MinimumLuminanceRange;

        return new ShellScreenshotQuality(
            isAcceptable,
            nearBlackRatio,
            nearWhiteRatio,
            minimumLuminance,
            maximumLuminance,
            sampledPixels);
    }
}

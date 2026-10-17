using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed class HeightImageFrameConverter : IValueConverter, IMultiValueConverter
{
    private static readonly ConditionalWeakTable<C3DHeightImageDisplayFrame, BitmapSource> bitmaps = new();

    public static BitmapSource CreateBitmap(C3DHeightImageDisplayFrame frame) => bitmaps.GetValue(frame, value =>
    {
        var bitmap = BitmapSource.Create(value.Width, value.Height, 96, 96, PixelFormats.Bgra32, null, value.Bgra32Pixels.ToArray(), value.Stride);
        bitmap.Freeze();
        return bitmap;
    });

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is C3DHeightImageDisplayFrame frame ? CreateBitmap(frame) : null;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 2)
            return values[0] is IReadOnlyDictionary<string, BitmapSource> previews && values[1] is string path
                && previews.TryGetValue(path, out var preview) ? preview : null;
        if (values.Length == 5)
            return values[0] is C3DHeightImageDisplayFrame outputFrame && values[1] is string outputId && !string.IsNullOrWhiteSpace(outputId)
                && values[2] is string artifactId && string.Equals(outputId, artifactId, StringComparison.OrdinalIgnoreCase)
                && values[3] is true && values[4] is true ? CreateBitmap(outputFrame) : null;
        if (values.Length == 4)
            return values[0] is C3DHeightImageDisplayFrame galleryFrame && values[1] is string frameHash && frameHash.Length == 64
                && values[2] is string artifactHash && string.Equals(frameHash, artifactHash, StringComparison.OrdinalIgnoreCase)
                && values[3] is true ? CreateBitmap(galleryFrame) : null;
        return values.Length == 3 && values[0] is C3DHeightImageDisplayFrame frame && values[1] is string previewPath
            && values[2] is string samplePath && string.Equals(previewPath, samplePath, StringComparison.OrdinalIgnoreCase)
                ? CreateBitmap(frame) : null;
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => targetTypes.Select(_ => Binding.DoNothing).ToArray();
}

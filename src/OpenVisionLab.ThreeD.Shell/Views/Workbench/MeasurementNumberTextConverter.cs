using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed class MeasurementNumberTextConverter : IValueConverter
{
    private static readonly Regex Number = new(@"(?<![\w.])[-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?(?![\w.])", RegexOptions.CultureInvariant);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string text) return value;
        // Format the read-only text at the binding; result/limit snapshots keep their full precision.
        return Number.Replace(text, match =>
        {
            if (match.Value.IndexOfAny(['.', 'e', 'E']) < 0 || !double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                || !double.IsFinite(number)) return match.Value;
            var display = number.ToString("0.##", CultureInfo.InvariantCulture);
            return display == "-0" ? "0" : display;
        });
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

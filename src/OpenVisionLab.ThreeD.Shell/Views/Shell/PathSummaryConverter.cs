using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

public sealed class PathSummaryConverter : MarkupExtension, IValueConverter
{
    public override object ProvideValue(IServiceProvider serviceProvider) => this;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path)) return string.Empty;
        var display = path.TrimEnd('\\', '/');
        if (display.Length == 0) return path;
        if (display.Length == 2 && char.IsLetter(display[0]) && display[1] == ':') return path;
        var lastSeparator = display.LastIndexOfAny(['\\', '/']);
        if (lastSeparator <= 0) return display;
        var parentSeparator = display.LastIndexOfAny(['\\', '/'], lastSeparator - 1);
        if (parentSeparator < 0 || display[..parentSeparator].All(character => character is '\\' or '/')) return display;
        return display[(lastSeparator + 1)..] + " — …" + display[parentSeparator..lastSeparator];
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

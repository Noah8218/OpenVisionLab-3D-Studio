using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace OpenVisionLab.ThreeD.Shell;

[MarkupExtensionReturnType(typeof(string))]
public sealed class ThreeDTextExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public string Korean { get; set; } = string.Empty;

    public string English { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(ThreeDLocalization.StudioSubtitle))
        {
            Source = ThreeDLocalization.Shared,
            Mode = BindingMode.OneWay,
            Converter = new TextConverter(Key, Korean, English)
        };

        return binding.ProvideValue(serviceProvider);
    }

    private sealed class TextConverter(string key, string korean, string english) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ThreeDLocalization.Shared.Resolve(key, korean, english);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}

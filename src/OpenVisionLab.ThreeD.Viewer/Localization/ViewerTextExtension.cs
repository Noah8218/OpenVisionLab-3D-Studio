using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.Localization;

[MarkupExtensionReturnType(typeof(string))]
public sealed class ViewerTextExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public string Korean { get; set; } = string.Empty;

    public string English { get; set; } = string.Empty;

    public IViewerLocalizationProvider? Localization { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Localization is not null)
        {
            return CreateProviderBinding(Localization, serviceProvider);
        }

        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget
            {
                TargetObject: DependencyObject target
            })
        {
            var binding = new Binding
            {
                Path = new PropertyPath("(0)", ViewerLocalizationScope.RevisionProperty),
                RelativeSource = new RelativeSource(RelativeSourceMode.Self),
                Mode = BindingMode.OneWay,
                Converter = new ScopedTextConverter(target, Key, Korean, English)
            };
            return binding.ProvideValue(serviceProvider);
        }

        return ViewerLocalization.Shared.Resolve(Key, Korean, English);
    }

    private object CreateProviderBinding(
        IViewerLocalizationProvider localization,
        IServiceProvider serviceProvider)
    {
        var binding = new Binding(nameof(IViewerLocalizationProvider.Revision))
        {
            Source = localization,
            Mode = BindingMode.OneWay,
            Converter = new TextConverter(localization, Key, Korean, English)
        };

        return binding.ProvideValue(serviceProvider);
    }

    private sealed class ScopedTextConverter(
        DependencyObject target,
        string key,
        string korean,
        string english) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var localization = ViewerLocalizationScope.GetProvider(target) ?? ViewerLocalization.Shared;
            return localization.Resolve(key, korean, english);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }

    private sealed class TextConverter(
        IViewerLocalizationProvider localization,
        string key,
        string korean,
        string english) : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            localization.Resolve(key, korean, english);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}

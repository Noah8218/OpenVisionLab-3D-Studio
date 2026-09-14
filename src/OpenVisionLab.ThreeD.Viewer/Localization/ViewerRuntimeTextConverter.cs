using System.Globalization;
using System.Windows.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.Localization;

public sealed class ViewerRuntimeTextConverter : IValueConverter
{
    public IViewerLocalizationProvider Localization { get; set; } = ViewerLocalization.Shared;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        Localization.LocalizeRuntimeText(value, parameter?.ToString());

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

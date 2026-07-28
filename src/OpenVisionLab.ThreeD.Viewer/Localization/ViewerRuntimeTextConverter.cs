using System.Globalization;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Viewer.Localization;

public sealed class ViewerRuntimeTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        ViewerLocalization.Shared.LocalizeRuntimeText(value, parameter?.ToString());

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed class InspectionCountGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 0 ? new GridLength(count, GridUnitType.Star) : new GridLength(0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

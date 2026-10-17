using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed class TeachingResultVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length == 3 && values[2] is true && !ReferenceEquals(values[0], values[1])
            ? Visibility.Collapsed : Visibility.Visible;

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        targetTypes.Select(_ => Binding.DoNothing).ToArray();
}

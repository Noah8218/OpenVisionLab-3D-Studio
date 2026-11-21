using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

// 건수가 0이면 숨기되, 지금 선택된 필터는 0건이어도 보이게 한다.
// 선택된 필터가 사라지면 현재 목록이 왜 비었는지 알 수 없기 때문이다.
public sealed class ZeroUnlessCheckedVisibilityConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var count = values.Length > 0 && values[0] is int value ? value : 0;
        var isChecked = values.Length > 1 && values[1] is true;
        return count > 0 || isChecked ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
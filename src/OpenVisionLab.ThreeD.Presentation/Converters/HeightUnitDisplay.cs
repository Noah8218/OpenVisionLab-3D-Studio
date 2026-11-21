using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Presentation.Converters;

// 높이 단위 raw·raw-height는 레시피, 검증, Runner가 쓰는 식별자다. 화면 문구에서는 뺀다.
// 데이터 속성(Unit 등)은 그대로 두고 표시 문자열에서만 지우며, mm·% 같은 실제 단위는 그대로 보인다.
// 단위를 확인하는 자리는 입력 정보 화면의 '높이 단위' 항목이다.
public static class HeightUnitDisplay
{
    private static readonly Regex InternalUnit = new(@"\s*(?<![\w-])raw(?:-height)?(?![\w-])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static bool IsInternal(string? unit) =>
        unit is not null && (unit.Equals("raw", StringComparison.OrdinalIgnoreCase) || unit.Equals("raw-height", StringComparison.OrdinalIgnoreCase));

    public static string ForDisplay(string? unit) => IsInternal(unit) ? string.Empty : unit ?? string.Empty;

    // "7.75–8.25 raw-height"처럼 값이나 범위 뒤에 붙은 raw 단위만 지운다.
    public static string Strip(string? text) => string.IsNullOrEmpty(text) ? string.Empty : InternalUnit.Replace(text, string.Empty).Trim();
}

// 표시 문자열에서 raw 단위를 뺀다. ConverterParameter가 있으면 남은 문자열 앞에 붙여서(구분자 등) 돌려주고, 빈 문자열이면 붙이지 않는다.
public sealed class HeightUnitDisplayConverter : IValueConverter
{
    public static readonly HeightUnitDisplayConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var text = HeightUnitDisplay.Strip(value as string);
        return text.Length > 0 && parameter is string prefix ? prefix + text : text;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

// 단위 칸이 비면 여백까지 함께 접는다.
public sealed class HeightUnitVisibilityConverter : IValueConverter
{
    public static readonly HeightUnitVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        HeightUnitDisplay.Strip(value as string).Length > 0 ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}

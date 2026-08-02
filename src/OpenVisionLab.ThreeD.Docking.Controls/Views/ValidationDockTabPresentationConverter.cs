using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace OpenVisionLab.ThreeD.Docking.Controls;

public sealed class ValidationDockTabPresentationConverter : IMultiValueConverter
{
    private const string ValidationContentId = "evidence-workbench";

    public object Convert(
        object[] values,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        var contentId = values.ElementAtOrDefault(0) as string;
        var filterActive = values.ElementAtOrDefault(1) is true;
        var visibleSupportContentId = values.ElementAtOrDefault(2) as string;
        var isValidationPaneContent = IsValidationPaneContent(contentId);
        var isPresented = !filterActive
                          || !isValidationPaneContent
                          || string.Equals(
                              contentId,
                              ValidationContentId,
                              StringComparison.Ordinal)
                          || string.Equals(
                              contentId,
                              visibleSupportContentId,
                              StringComparison.Ordinal);

        return (parameter as string) switch
        {
            "Width" => !isPresented
                ? 0d
                : filterActive && isValidationPaneContent
                    ? 128d
                    : double.NaN,
            "Margin" => isPresented
                ? new Thickness(0, 0, 3, 0)
                : new Thickness(0),
            "Boolean" => isPresented,
            _ => throw new ArgumentOutOfRangeException(nameof(parameter)),
        };
    }

    private static bool IsValidationPaneContent(string? contentId) =>
        contentId is ValidationContentId
            or "output-compare"
            or "linked-view"
            or "height-profile"
            or "fit-diagnostics"
            or "intersection-evidence"
            or "correspondence-evidence";

    public object[] ConvertBack(
        object value,
        Type[] targetTypes,
        object parameter,
        CultureInfo culture) =>
        throw new NotSupportedException();
}

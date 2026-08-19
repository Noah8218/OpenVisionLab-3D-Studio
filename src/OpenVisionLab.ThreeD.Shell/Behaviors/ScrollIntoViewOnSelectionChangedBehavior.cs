using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>
/// Keeps the newly selected item visible in a virtualized ListBox.
/// This behavior owns only the WPF presentation interaction; it does not
/// mutate recipe, validation, or execution state.
/// </summary>
public static class ScrollIntoViewOnSelectionChangedBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(ScrollIntoViewOnSelectionChangedBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ListBox list)
        {
            return;
        }

        if (args.OldValue is true)
        {
            list.SelectionChanged -= OnSelectionChanged;
        }

        if (args.NewValue is true)
        {
            list.SelectionChanged += OnSelectionChanged;
        }
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is not ListBox list || args.AddedItems.Count == 0)
        {
            return;
        }

        list.ScrollIntoView(args.AddedItems[0]);
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>
/// Forwards a ListBox item double-click to a bound command without making a
/// View code-behind handler reach into a concrete ViewModel.
/// </summary>
public static class ListBoxMouseDoubleClickCommandBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(ListBoxMouseDoubleClickCommandBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    private static void OnCommandChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ListBox list)
        {
            return;
        }

        if (args.OldValue is ICommand)
        {
            list.MouseDoubleClick -= OnMouseDoubleClick;
        }

        if (args.NewValue is ICommand)
        {
            list.MouseDoubleClick += OnMouseDoubleClick;
        }
    }

    private static void OnMouseDoubleClick(object sender, MouseButtonEventArgs args)
    {
        if (sender is not ListBox list
            || FindAncestor<ButtonBase>(args.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        var command = GetCommand(list);
        var parameter = list.SelectedItem;
        if (command?.CanExecute(parameter) != true)
        {
            return;
        }

        command.Execute(parameter);
        args.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }
}

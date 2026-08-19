using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

public static class CheckedCommandBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(CheckedCommandBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(CheckedCommandBehavior),
            new PropertyMetadata(null));

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommandParameter(DependencyObject element, object? value) =>
        element.SetValue(CommandParameterProperty, value);

    public static object? GetCommandParameter(DependencyObject element) =>
        element.GetValue(CommandParameterProperty);

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(CheckedCommandBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ToggleButton button)
        {
            return;
        }

        if (args.NewValue is true)
        {
            button.Checked += OnChecked;
        }
        else
        {
            button.Checked -= OnChecked;
        }
    }

    private static void OnChecked(object sender, RoutedEventArgs args)
    {
        if (sender is not DependencyObject element)
        {
            return;
        }

        var command = GetCommand(element);
        var parameter = GetCommandParameter(element);
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }
}

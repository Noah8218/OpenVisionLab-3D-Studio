using System.Windows;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>
/// Forwards a View-owned PreviewMouseDown gesture to an attached command
/// without making View code-behind reach into a concrete ViewModel.
/// </summary>
public static class PreviewMouseDownCommandBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(PreviewMouseDownCommandBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(PreviewMouseDownCommandBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(PreviewMouseDownCommandBehavior),
            new PropertyMetadata(null));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetCommandParameter(DependencyObject element, object? value) =>
        element.SetValue(CommandParameterProperty, value);

    public static object? GetCommandParameter(DependencyObject element) =>
        element.GetValue(CommandParameterProperty);

    internal static bool TryExecute(DependencyObject element)
    {
        var command = GetCommand(element);
        var parameter = GetCommandParameter(element);
        if (command?.CanExecute(parameter) != true)
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not UIElement element)
        {
            return;
        }

        if (args.OldValue is true)
        {
            element.PreviewMouseDown -= OnPreviewMouseDown;
        }

        if (args.NewValue is true)
        {
            element.PreviewMouseDown += OnPreviewMouseDown;
        }
    }

    private static void OnPreviewMouseDown(object sender, MouseButtonEventArgs args)
    {
        if (sender is DependencyObject element)
        {
            _ = TryExecute(element);
        }
    }
}

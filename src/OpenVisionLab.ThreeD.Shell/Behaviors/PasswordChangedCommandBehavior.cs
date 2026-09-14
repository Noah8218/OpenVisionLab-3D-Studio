using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>
/// Forwards PasswordBox.Password changes to a bound command without making a
/// View code-behind handler reach into a concrete ViewModel.
/// </summary>
public static class PasswordChangedCommandBehavior
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(PasswordChangedCommandBehavior),
            new PropertyMetadata(null, OnCommandChanged));

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    private static void OnCommandChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not PasswordBox passwordBox)
        {
            return;
        }

        if (args.OldValue is ICommand)
        {
            passwordBox.PasswordChanged -= OnPasswordChanged;
        }

        if (args.NewValue is ICommand)
        {
            passwordBox.PasswordChanged += OnPasswordChanged;
        }
    }

    private static void OnPasswordChanged(object sender, RoutedEventArgs args)
    {
        if (sender is not PasswordBox passwordBox)
        {
            return;
        }

        var command = GetCommand(passwordBox);
        var parameter = passwordBox.Password;
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }
}

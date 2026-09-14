using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>
/// Commits a PropertyGrid editor before forwarding one button click to a
/// bound command. The behavior owns only WPF event ordering; recipe state and
/// command policy remain in the existing ViewModel.
/// </summary>
public static class CommitPropertyGridCommandBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(CommitPropertyGridCommandBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty PropertyGridProperty =
        DependencyProperty.RegisterAttached(
            "PropertyGrid",
            typeof(RecipeStepPropertyGridHost),
            typeof(CommitPropertyGridCommandBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(CommitPropertyGridCommandBehavior),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ErrorCommandProperty =
        DependencyProperty.RegisterAttached(
            "ErrorCommand",
            typeof(ICommand),
            typeof(CommitPropertyGridCommandBehavior),
            new PropertyMetadata(null));

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetPropertyGrid(DependencyObject element, RecipeStepPropertyGridHost? value) =>
        element.SetValue(PropertyGridProperty, value);

    public static RecipeStepPropertyGridHost? GetPropertyGrid(DependencyObject element) =>
        (RecipeStepPropertyGridHost?)element.GetValue(PropertyGridProperty);

    public static void SetCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CommandProperty, value);

    public static ICommand? GetCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CommandProperty);

    public static void SetErrorCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(ErrorCommandProperty, value);

    public static ICommand? GetErrorCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(ErrorCommandProperty);

    private static void OnIsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not ButtonBase button)
        {
            return;
        }

        if (args.OldValue is true)
        {
            button.Click -= OnClick;
        }

        if (args.NewValue is true)
        {
            button.Click += OnClick;
        }
    }

    private static void OnClick(object sender, RoutedEventArgs args)
    {
        if (sender is not ButtonBase button
            || GetPropertyGrid(button) is not { } propertyGrid)
        {
            return;
        }

        if (!propertyGrid.CommitPendingEdit(out var message))
        {
            args.Handled = Execute(GetErrorCommand(button), message);
            return;
        }

        args.Handled = Execute(GetCommand(button), parameter: null);
    }

    private static bool Execute(ICommand? command, object? parameter)
    {
        if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
            return true;
        }

        return false;
    }
}

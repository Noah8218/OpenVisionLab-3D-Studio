using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>Routes a gallery click or keyboard activation to the bound pane command.</summary>
public static class GalleryDisplayBehavior
{
    public static readonly DependencyProperty ActivePaneCommandProperty = DependencyProperty.RegisterAttached(
        "ActivePaneCommand", typeof(ICommand), typeof(GalleryDisplayBehavior), new PropertyMetadata(null, OnActiveCommandChanged));
    public static readonly DependencyProperty OtherPaneCommandProperty = DependencyProperty.RegisterAttached(
        "OtherPaneCommand", typeof(ICommand), typeof(GalleryDisplayBehavior), new PropertyMetadata(null));
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.RegisterAttached(
        "CommandParameter", typeof(object), typeof(GalleryDisplayBehavior), new PropertyMetadata(null));

    public static ICommand? GetActivePaneCommand(DependencyObject element) => (ICommand?)element.GetValue(ActivePaneCommandProperty);
    public static void SetActivePaneCommand(DependencyObject element, ICommand? value) => element.SetValue(ActivePaneCommandProperty, value);
    public static ICommand? GetOtherPaneCommand(DependencyObject element) => (ICommand?)element.GetValue(OtherPaneCommandProperty);
    public static void SetOtherPaneCommand(DependencyObject element, ICommand? value) => element.SetValue(OtherPaneCommandProperty, value);
    public static object? GetCommandParameter(DependencyObject element) => element.GetValue(CommandParameterProperty);
    public static void SetCommandParameter(DependencyObject element, object? value) => element.SetValue(CommandParameterProperty, value);

    private static void OnActiveCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not ButtonBase button) return;
        button.Click -= OnClick;
        if (args.NewValue is ICommand) button.Click += OnClick;
    }

    private static void OnClick(object sender, RoutedEventArgs args)
    {
        if (sender is not ButtonBase { IsEnabled: true } button) return;
        var command = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? GetOtherPaneCommand(button) : GetActivePaneCommand(button);
        var parameter = GetCommandParameter(button);
        if (command?.CanExecute(parameter) == true) command.Execute(parameter);
        args.Handled = true;
    }
}

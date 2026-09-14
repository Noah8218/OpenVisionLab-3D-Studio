using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>
/// Translates Height Image keyboard gestures into bound commands while keeping
/// text-entry controls out of the viewer shortcut path.
/// </summary>
public static class HeightImageViewerKeyboardBehavior
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(HeightImageViewerKeyboardBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty ApplyRoiCommandProperty =
        RegisterCommandProperty("ApplyRoiCommand");

    public static readonly DependencyProperty CancelRoiCommandProperty =
        RegisterCommandProperty("CancelRoiCommand");

    public static readonly DependencyProperty DeleteRoiCommandProperty =
        RegisterCommandProperty("DeleteRoiCommand");

    public static readonly DependencyProperty FitCommandProperty =
        RegisterCommandProperty("FitCommand");

    public static readonly DependencyProperty ActualPixelsCommandProperty =
        RegisterCommandProperty("ActualPixelsCommand");

    public static readonly DependencyProperty ZoomInCommandProperty =
        RegisterCommandProperty("ZoomInCommand");

    public static readonly DependencyProperty ZoomOutCommandProperty =
        RegisterCommandProperty("ZoomOutCommand");

    public static readonly DependencyProperty AutoRangeCommandProperty =
        RegisterCommandProperty("AutoRangeCommand");

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetApplyRoiCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(ApplyRoiCommandProperty, value);

    public static ICommand? GetApplyRoiCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(ApplyRoiCommandProperty);

    public static void SetCancelRoiCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(CancelRoiCommandProperty, value);

    public static ICommand? GetCancelRoiCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(CancelRoiCommandProperty);

    public static void SetDeleteRoiCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(DeleteRoiCommandProperty, value);

    public static ICommand? GetDeleteRoiCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(DeleteRoiCommandProperty);

    public static void SetFitCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(FitCommandProperty, value);

    public static ICommand? GetFitCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(FitCommandProperty);

    public static void SetActualPixelsCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(ActualPixelsCommandProperty, value);

    public static ICommand? GetActualPixelsCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(ActualPixelsCommandProperty);

    public static void SetZoomInCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(ZoomInCommandProperty, value);

    public static ICommand? GetZoomInCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(ZoomInCommandProperty);

    public static void SetZoomOutCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(ZoomOutCommandProperty, value);

    public static ICommand? GetZoomOutCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(ZoomOutCommandProperty);

    public static void SetAutoRangeCommand(DependencyObject element, ICommand? value) =>
        element.SetValue(AutoRangeCommandProperty, value);

    public static ICommand? GetAutoRangeCommand(DependencyObject element) =>
        (ICommand?)element.GetValue(AutoRangeCommandProperty);

    internal static bool TryExecute(
        DependencyObject element,
        object? originalSource,
        Key key,
        ModifierKeys modifiers)
    {
        if (originalSource is TextBoxBase or ComboBox)
        {
            return false;
        }

        var command = ResolveCommand(element, key, modifiers);
        if (command?.CanExecute(null) != true)
        {
            return false;
        }

        command.Execute(null);
        return true;
    }

    private static DependencyProperty RegisterCommandProperty(string name) =>
        DependencyProperty.RegisterAttached(
            name,
            typeof(ICommand),
            typeof(HeightImageViewerKeyboardBehavior),
            new PropertyMetadata(null));

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
            element.PreviewKeyDown -= OnPreviewKeyDown;
        }

        if (args.NewValue is true)
        {
            element.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (sender is not DependencyObject element)
        {
            return;
        }

        if (TryExecute(
                element,
                args.OriginalSource,
                args.Key,
                args.KeyboardDevice.Modifiers))
        {
            args.Handled = true;
        }
    }

    private static ICommand? ResolveCommand(
        DependencyObject element,
        Key key,
        ModifierKeys modifiers) =>
        (modifiers, key) switch
        {
            (ModifierKeys.None, Key.Enter) => GetApplyRoiCommand(element),
            (ModifierKeys.None, Key.Escape) => GetCancelRoiCommand(element),
            (ModifierKeys.None, Key.Delete) => GetDeleteRoiCommand(element),
            (ModifierKeys.None, Key.F) => GetFitCommand(element),
            (ModifierKeys.None, Key.D1 or Key.NumPad1) => GetActualPixelsCommand(element),
            (ModifierKeys.None or ModifierKeys.Shift, Key.Add or Key.OemPlus) => GetZoomInCommand(element),
            (ModifierKeys.None or ModifierKeys.Shift, Key.Subtract or Key.OemMinus) => GetZoomOutCommand(element),
            (ModifierKeys.Control, Key.R) => GetAutoRangeCommand(element),
            _ => null
        };
}

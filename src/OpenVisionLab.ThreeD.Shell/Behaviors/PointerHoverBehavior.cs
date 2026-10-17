using System.Windows;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

public static class PointerHoverBehavior
{
    private static readonly MouseEventHandler PointerChangedHandler = OnPointerChanged;
    private static readonly RoutedEvent[] PointerEvents =
    [
        UIElement.MouseEnterEvent, UIElement.MouseLeaveEvent, UIElement.MouseMoveEvent,
        UIElement.GotMouseCaptureEvent, UIElement.LostMouseCaptureEvent,
    ];
    private static readonly DependencyPropertyKey IsHoveredPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsHovered", typeof(bool), typeof(PointerHoverBehavior), new PropertyMetadata(false));

    public static readonly DependencyProperty IsHoveredProperty = IsHoveredPropertyKey.DependencyProperty;
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(PointerHoverBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsHovered(DependencyObject element) => (bool)element.GetValue(IsHoveredProperty);

    private static void OnIsEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not FrameworkElement element) return;
        element.Loaded -= OnLoaded;
        element.Unloaded -= OnUnloaded;
        Detach(element);
        if (args.NewValue is not true) return;
        element.Loaded += OnLoaded;
        element.Unloaded += OnUnloaded;
        if (element.IsLoaded) Attach(element);
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element && GetIsEnabled(element)) Attach(element);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element) Detach(element);
    }

    private static void Attach(FrameworkElement element)
    {
        Detach(element);
        foreach (var routedEvent in PointerEvents) element.AddHandler(routedEvent, PointerChangedHandler, true);
        element.SizeChanged += OnSizeChanged;
        element.IsEnabledChanged += OnAvailabilityChanged;
        element.IsVisibleChanged += OnAvailabilityChanged;
        Update(element);
    }

    private static void Detach(FrameworkElement element)
    {
        foreach (var routedEvent in PointerEvents) element.RemoveHandler(routedEvent, PointerChangedHandler);
        element.SizeChanged -= OnSizeChanged;
        element.IsEnabledChanged -= OnAvailabilityChanged;
        element.IsVisibleChanged -= OnAvailabilityChanged;
        element.SetValue(IsHoveredPropertyKey, false);
    }

    private static void OnPointerChanged(object sender, MouseEventArgs args)
    {
        if (sender is FrameworkElement element) Update(element);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (sender is FrameworkElement element) Update(element);
    }

    private static void OnAvailabilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (sender is FrameworkElement element) Update(element);
    }

    private static void Update(FrameworkElement element)
    {
        var isHovered = false;
        if (element.IsLoaded && element.IsEnabled && element.IsVisible && element.IsMouseOver)
        {
            // Capture can report IsMouseOver outside the control, including a Space press.
            var point = Mouse.GetPosition(element);
            isHovered = point.X >= 0 && point.Y >= 0 && point.X < element.ActualWidth && point.Y < element.ActualHeight;
        }
        element.SetValue(IsHoveredPropertyKey, isHovered);
    }
}

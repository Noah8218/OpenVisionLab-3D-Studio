using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenVisionLab.ThreeD.Presentation.Behaviors;

/// <summary>Routes viewer gestures through existing commands without consuming editor input.</summary>
public static class ViewerKeyboardBehavior
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(ViewerKeyboardBehavior), new PropertyMetadata(false, OnIsEnabledChanged));

    public static readonly DependencyProperty BindingsProperty = DependencyProperty.RegisterAttached(
        "Bindings", typeof(FreezableCollection<KeyBinding>), typeof(ViewerKeyboardBehavior), new PropertyMetadata(null));

    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);

    public static FreezableCollection<KeyBinding> GetBindings(DependencyObject element)
    {
        if (element.GetValue(BindingsProperty) is FreezableCollection<KeyBinding> bindings) return bindings;
        bindings = new FreezableCollection<KeyBinding>();
        element.SetValue(BindingsProperty, bindings);
        return bindings;
    }

    public static void SetBindings(DependencyObject element, FreezableCollection<KeyBinding> value) => element.SetValue(BindingsProperty, value);

    public static bool TryExecute(DependencyObject element, DependencyObject? source, Key key, ModifierKeys modifiers)
    {
        key = key switch { Key.NumPad1 => Key.D1, Key.NumPad2 => Key.D2, Key.NumPad3 => Key.D3, Key.NumPad4 => Key.D4, _ => key };
        for (var current = source; current is not null; current = current is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
        {
            if (current is TextBoxBase or PasswordBox or ComboBox or Slider) return false;
            if (ReferenceEquals(current, element)) break;
        }

        foreach (var binding in GetBindings(element))
        {
            if (binding.Key != key || binding.Modifiers != modifiers || binding.Command is not { } command) continue;
            if (!command.CanExecute(binding.CommandParameter)) return false;
            command.Execute(binding.CommandParameter);
            return true;
        }
        return false;
    }

    private static void OnIsEnabledChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        if (dependencyObject is not UIElement element) return;
        if (args.OldValue is true) element.KeyDown -= OnKeyDown;
        if (args.NewValue is true)
        {
            GetBindings(element);
            element.KeyDown += OnKeyDown;
        }
    }

    private static void OnKeyDown(object sender, KeyEventArgs args)
    {
        if (sender is DependencyObject element && TryExecute(element, args.OriginalSource as DependencyObject, args.Key, args.KeyboardDevice.Modifiers))
            args.Handled = true;
    }
}

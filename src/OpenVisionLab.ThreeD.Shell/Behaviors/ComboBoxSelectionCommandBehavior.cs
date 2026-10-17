using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.Behaviors;

/// <summary>Commits an explicit picker choice through its command; binding restoration does not commit.</summary>
public static class ComboBoxSelectionCommandBehavior
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(ComboBoxSelectionCommandBehavior), new PropertyMetadata(null, OnCommandChanged));

    private static readonly DependencyProperty IsExecutingProperty = DependencyProperty.RegisterAttached(
        "IsExecuting", typeof(bool), typeof(ComboBoxSelectionCommandBehavior), new PropertyMetadata(false));

    public static ICommand? GetCommand(DependencyObject element) => (ICommand?)element.GetValue(CommandProperty);
    public static void SetCommand(DependencyObject element, ICommand? value) => element.SetValue(CommandProperty, value);

    private static void OnCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not ComboBox picker) return;
        picker.SelectionChanged -= OnSelectionChanged;
        if (args.NewValue is ICommand) picker.SelectionChanged += OnSelectionChanged;
    }

    private static void OnSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is not ComboBox picker || (bool)picker.GetValue(IsExecutingProperty)
            || !picker.IsDropDownOpen && !picker.IsKeyboardFocusWithin || args.AddedItems.Count != 1) return;
        var choice = args.AddedItems[0];
        picker.SetValue(IsExecutingProperty, true);
        try
        {
            if (GetCommand(picker) is { } command && command.CanExecute(choice)) command.Execute(choice);
        }
        finally
        {
            picker.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateTarget();
            picker.SetValue(IsExecutingProperty, false);
        }
    }
}

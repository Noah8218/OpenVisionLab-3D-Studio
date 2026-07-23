using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.PropertyGrid;

public partial class RecipeStepPropertyGridHost : UserControl
{
    public static readonly DependencyProperty SelectedObjectProperty = DependencyProperty.Register(
        nameof(SelectedObject),
        typeof(object),
        typeof(RecipeStepPropertyGridHost),
        new PropertyMetadata(null, OnSelectedObjectChanged));

    public RecipeStepPropertyGridHost()
    {
        InitializeComponent();
        InnerGrid.PropertyValueChanged += (_, _) => PropertyValueChanged?.Invoke(this, EventArgs.Empty);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler? PropertyValueChanged;

    public object? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public int VisiblePropertyCount => InnerGrid.Properties?.Count ?? 0;

    public int MatchingPropertyCount => InnerGrid.Properties?.Count(property => property.MatchesFilter) ?? 0;

    public bool HasCategories => InnerGrid.HasCategories;

    public void SetPropertyFilter(string value) => InnerGrid.PropertyFilter = value;

    public bool CommitPendingEdit(out string message)
    {
        InnerGrid.ApplyTemplate();
        InnerGrid.UpdateLayout();
        foreach (var textBox in FindVisualChildren<TextBox>(InnerGrid))
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }

        foreach (var comboBox in FindVisualChildren<ComboBox>(InnerGrid))
        {
            comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
            comboBox.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateSource();
        }

        foreach (var toggle in FindVisualChildren<ToggleButton>(InnerGrid))
        {
            toggle.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateSource();
        }

        if (FindVisualChildren<FrameworkElement>(InnerGrid).Any(Validation.GetHasError))
        {
            message = "Correct the highlighted PropertyGrid value before applying.";
            return false;
        }

        if (Keyboard.FocusedElement is UIElement focused && focused.IsKeyboardFocusWithin)
        {
            focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        message = string.Empty;
        return true;
    }

    private static void OnSelectedObjectChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var host = (RecipeStepPropertyGridHost)sender;
        host.RefreshSelectedObject();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        OpenVisionLanguageService.LanguageChanged += OnLanguageChanged;
        RefreshSelectedObject();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        OpenVisionLanguageService.LanguageChanged -= OnLanguageChanged;

    private void OnLanguageChanged(object? sender, EventArgs args) => RefreshSelectedObject();

    private void RefreshSelectedObject()
    {
        InnerGrid.SelectedObject = null;
        if (SelectedObject is not null)
        {
            InnerGrid.SelectedObject = LocalizedPropertyGridObject.Create(SelectedObject);
        }
    }

    private void OnPropertyFilterTextChanged(object sender, TextChangedEventArgs args) =>
        InnerGrid.PropertyFilter = PropertyFilterBox.Text;

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualChildren<T>(child))
            {
                yield return descendant;
            }
        }
    }
}

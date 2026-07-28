using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.PropertyGrid;

public partial class RecipeStepPropertyGridHost : UserControl
{
    private DispatcherOperation? selectedObjectRefreshOperation;

    public static readonly DependencyProperty SelectedObjectProperty = DependencyProperty.Register(
        nameof(SelectedObject),
        typeof(object),
        typeof(RecipeStepPropertyGridHost),
        new PropertyMetadata(null, OnSelectedObjectChanged));

    public static readonly DependencyProperty PropertyValueChangedCommandProperty = DependencyProperty.Register(
        nameof(PropertyValueChangedCommand),
        typeof(ICommand),
        typeof(RecipeStepPropertyGridHost),
        new PropertyMetadata(null));

    public RecipeStepPropertyGridHost()
    {
        InitializeComponent();
        InnerGrid.PropertyValueChanged += (_, _) =>
        {
            if (PropertyValueChangedCommand?.CanExecute(null) == true)
            {
                PropertyValueChangedCommand.Execute(null);
            }
        };
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public object? SelectedObject
    {
        get => GetValue(SelectedObjectProperty);
        set => SetValue(SelectedObjectProperty, value);
    }

    public ICommand? PropertyValueChangedCommand
    {
        get => (ICommand?)GetValue(PropertyValueChangedCommandProperty);
        set => SetValue(PropertyValueChangedCommandProperty, value);
    }

    public int VisiblePropertyCount => InnerGrid.Properties?.Count ?? 0;

    public int MatchingPropertyCount => InnerGrid.Properties?.Count(property => property.MatchesFilter) ?? 0;

    public bool HasCategories => InnerGrid.HasCategories;

    public void SetPropertyFilter(string value) => InnerGrid.PropertyFilter = value;

    public bool CommitPendingEdit(out string message)
    {
        FlushPendingSelectedObjectRefresh();
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
        if (host.IsLoaded)
        {
            host.QueueSelectedObjectRefresh();
        }
        else
        {
            host.RefreshSelectedObject();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        OpenVisionLanguageService.LanguageChanged += OnLanguageChanged;
        RefreshSelectedObject();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        selectedObjectRefreshOperation?.Abort();
        selectedObjectRefreshOperation = null;
        OpenVisionLanguageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, EventArgs args) => QueueSelectedObjectRefresh();

    private void QueueSelectedObjectRefresh()
    {
        selectedObjectRefreshOperation?.Abort();
        selectedObjectRefreshOperation = Dispatcher.BeginInvoke(
            DispatcherPriority.Background,
            () =>
            {
                selectedObjectRefreshOperation = null;
                RefreshSelectedObject();
            });
    }

    private void FlushPendingSelectedObjectRefresh()
    {
        if (selectedObjectRefreshOperation is null)
        {
            return;
        }

        selectedObjectRefreshOperation.Abort();
        selectedObjectRefreshOperation = null;
        RefreshSelectedObject();
    }

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

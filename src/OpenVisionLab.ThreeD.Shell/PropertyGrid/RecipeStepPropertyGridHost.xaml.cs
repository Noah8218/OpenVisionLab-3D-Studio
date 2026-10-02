using System.ComponentModel;
using System.Globalization;
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

    public static readonly DependencyProperty ShowAdvancedParametersProperty = DependencyProperty.Register(
        nameof(ShowAdvancedParameters), typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(true, OnSelectedObjectChanged));

    public bool ShowAdvancedParameters
    {
        get => (bool)GetValue(ShowAdvancedParametersProperty);
        set => SetValue(ShowAdvancedParametersProperty, value);
    }

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

    public void SetPropertyFilter(string value)
    {
        PropertyFilterBox.Text = value;
        InnerGrid.PropertyFilter = value;
    }

    public bool FocusInvalidEditor()
    {
        FlushPendingSelectedObjectRefresh();
        SetPropertyFilter(string.Empty);
        InnerGrid.ApplyTemplate();
        InnerGrid.UpdateLayout();
        var editors = FindVisualChildren<Control>(InnerGrid).Where(control => control.IsVisible && control.IsEnabled && control.Focusable
            && control is TextBox { IsReadOnly: false } or ComboBox or ToggleButton).ToArray();
        var target = editors.FirstOrDefault(Validation.GetHasError) ?? editors.FirstOrDefault();
        if (target is null) return false;
        target.BringIntoView();
        return target.Focus();
    }

    public void ScrollToTop()
    {
        InnerGrid.ApplyTemplate();
        InnerGrid.UpdateLayout();
        foreach (var scrollViewer in FindVisualChildren<ScrollViewer>(InnerGrid))
        {
            scrollViewer.ScrollToTop();
        }
    }

    public bool CommitPendingEdit(out string message)
    {
        FlushPendingSelectedObjectRefresh();
        InnerGrid.ApplyTemplate();
        InnerGrid.UpdateLayout();
        EnsureNumericEditorValidation();
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

        if (FindVisualChildren<FrameworkElement>(InnerGrid).FirstOrDefault(Validation.GetHasError) is { } invalidEditor)
        {
            FocusInvalidEditor();
            message = Validation.GetErrors(invalidEditor).FirstOrDefault()?.ErrorContent?.ToString()
                ?? "Correct the highlighted PropertyGrid value before applying.";
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
        // WPG caches descriptors by type; this wrapper changes visible fields per instance.
        System.Windows.Controls.WpfPropertyGrid.MetadataRepository.Clear();
        InnerGrid.SelectedObject = null;
        if (SelectedObject is not null)
        {
            InnerGrid.SelectedObject = LocalizedPropertyGridObject.Create(SelectedObject, ShowAdvancedParameters);
        }
        PropertySearchRow.Visibility = VisiblePropertyCount > 6 ? Visibility.Visible : Visibility.Collapsed;
        if (PropertySearchRow.Visibility == Visibility.Collapsed) PropertyFilterBox.SetCurrentValue(TextBox.TextProperty, string.Empty);
        InnerGrid.UpdateLayout();
        WrapPropertyContent();
    }

    private void OnReadOnlyEditorsLoaded(object sender, RoutedEventArgs args) => WrapPropertyContent();

    private void WrapPropertyContent()
    {
        EnsureNumericEditorValidation();
        foreach (var caption in FindVisualChildren<TextBlock>(InnerGrid).Where(text => text.GetType().Name == "PropertyNameTextBlock"))
        {
            caption.SetCurrentValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            caption.SetCurrentValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
        }
        foreach (var combo in FindVisualChildren<ComboBox>(InnerGrid))
        {
            combo.SetCurrentValue(ItemsControl.ItemTemplateProperty, (DataTemplate)Resources["LocalizedEnumOptionTemplate"]);
            if (combo.IsEnabled)
            {
                // WPG can cache the closed caption before its localized item template is assigned.
                foreach (var presenter in FindVisualChildren<ContentPresenter>(combo).Where(presenter => Equals(presenter.Content, combo.SelectionBoxItem)))
                    presenter.SetBinding(ContentPresenter.ContentTemplateProperty, new Binding(nameof(ComboBox.ItemTemplate)) { Source = combo });
                continue;
            }
            combo.SetCurrentValue(Control.TemplateProperty, (ControlTemplate)Resources["ReadOnlyEnumTemplate"]);
            combo.SetCurrentValue(HeightProperty, double.NaN);
        }
        foreach (var textBox in FindVisualChildren<TextBox>(InnerGrid).Where(editor => editor.IsReadOnly || !editor.IsEnabled))
        {
            var property = TypeDescriptor.GetProperties(InnerGrid.SelectedObject)[System.Windows.Automation.AutomationProperties.GetAutomationId(textBox)];
            if (property?.IsReadOnly == true && property.PropertyType == typeof(string) && LocalizedPropertyGridObject.LocalizeOption(textBox.Text) != textBox.Text)
                textBox.SetCurrentValue(Control.TemplateProperty, (ControlTemplate)Resources["ReadOnlyPolicyTemplate"]);
            textBox.SetCurrentValue(TextBox.TextWrappingProperty, TextWrapping.Wrap);
            textBox.SetCurrentValue(HeightProperty, double.NaN);
        }
    }

    private void EnsureNumericEditorValidation()
    {
        foreach (var editor in FindVisualChildren<TextBox>(InnerGrid).Where(editor => !editor.IsReadOnly))
        {
            var expression = editor.GetBindingExpression(TextBox.TextProperty);
            if (expression?.DataItem is System.Windows.Controls.WpfPropertyGrid.PropertyItemValue item
                && (item.ParentProperty.PropertyType == typeof(int) || item.ParentProperty.PropertyType == typeof(double))
                && !expression.ParentBinding.ValidationRules.OfType<NumericTextValidationRule>().Any())
                expression.ParentBinding.ValidationRules.Add(new NumericTextValidationRule());
        }
    }

    private sealed class NumericTextValidationRule() : ValidationRule(ValidationStep.RawProposedValue, validatesOnTargetUpdated: false)
    {
        public override ValidationResult Validate(object value, CultureInfo cultureInfo) => ValidationResult.ValidResult;

        public override ValidationResult Validate(object value, CultureInfo cultureInfo, BindingExpressionBase owner)
        {
            if (owner is not BindingExpression { DataItem: System.Windows.Controls.WpfPropertyGrid.PropertyItemValue item }) return ValidationResult.ValidResult;
            var property = item.ParentProperty;
            if (property.IsReadOnly || (property.PropertyType != typeof(int) && property.PropertyType != typeof(double))) return ValidationResult.ValidResult;
            try
            {
                // WPG shares template bindings; use this update's actual row and its existing converter/culture.
                property.Converter.ConvertFromString(null, CultureInfo.CurrentCulture, (string)value);
                return ValidationResult.ValidResult;
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
            {
                var reason = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean ? "올바른 숫자를 입력하세요." : "Enter a valid numeric value.";
                return new ValidationResult(false, $"{property.DisplayName}: {reason}");
            }
        }
    }

    private void OnPropertyFilterTextChanged(object sender, TextChangedEventArgs args) =>
        InnerGrid.PropertyFilter = PropertyFilterBox.Text;

    private void OnInnerGridPreviewKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Tab || Keyboard.FocusedElement is not UIElement focusedElement)
        {
            return;
        }

        CommitFocusedEditor(focusedElement);

        var direction = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            ? FocusNavigationDirection.Previous
            : FocusNavigationDirection.Next;

        // The bundled PropertyGrid's own Tab traversal can dereference a missing
        // visual child after the host has been scrolled or recomposed. Handle Tab
        // here so an ordinary parameter edit cannot terminate the application.
        if (!TryMoveFocusWithinPropertyGrid(focusedElement, direction))
        {
            focusedElement.MoveFocus(new TraversalRequest(direction));
        }

        args.Handled = true;
    }

    private void OnInnerGridLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (args.OriginalSource is UIElement editor) CommitFocusedEditor(editor);
    }

    private void OnInnerGridEditorTextChanged(object sender, TextChangedEventArgs args)
    {
        if (args.OriginalSource is TextBox { IsKeyboardFocused: true } editor
            && editor.GetBindingExpression(TextBox.TextProperty) is not null
            && PropertyValueChangedCommand?.CanExecute(null) == true)
            PropertyValueChangedCommand.Execute(null);
    }

    private bool TryMoveFocusWithinPropertyGrid(
        UIElement focusedElement,
        FocusNavigationDirection direction)
    {
        var editors = FindVisualChildren<Control>(InnerGrid)
            .Where(control =>
                control.IsVisible
                && control.IsEnabled
                && control.Focusable
                && control switch
                {
                    TextBox textBox => !textBox.IsReadOnly,
                    ComboBox => true,
                    ToggleButton => true,
                    _ => false
                })
            .ToList();

        var currentIndex = editors.FindIndex(editor =>
            ReferenceEquals(editor, focusedElement) || editor.IsKeyboardFocusWithin);
        if (currentIndex < 0)
        {
            return false;
        }

        var targetIndex = direction == FocusNavigationDirection.Previous
            ? currentIndex - 1
            : currentIndex + 1;
        return targetIndex >= 0
            && targetIndex < editors.Count
            && editors[targetIndex].Focus();
    }

    private static void CommitFocusedEditor(UIElement focusedElement)
    {
        if (focusedElement is TextBox textBox)
        {
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        else if (focusedElement is ComboBox comboBox)
        {
            comboBox.GetBindingExpression(Selector.SelectedValueProperty)?.UpdateSource();
            comboBox.GetBindingExpression(Selector.SelectedItemProperty)?.UpdateSource();
        }
        else if (focusedElement is ToggleButton toggle)
        {
            toggle.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateSource();
        }
    }

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

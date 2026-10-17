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
    private DispatcherOperation? parameterPageLayoutOperation;
    private FrameworkElement[] pagedParameterFields = [];
    private int selectedParameterPage;
    private int primaryParameterPage;
    private int detailedParameterPage;
    private bool updatingParameterPages;

    private static readonly DependencyPropertyKey VisiblePropertyCountPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VisiblePropertyCount), typeof(int), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(0));

    public static readonly DependencyProperty VisiblePropertyCountProperty = VisiblePropertyCountPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey HasValidationErrorsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasValidationErrors), typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(false));

    public static readonly DependencyProperty HasValidationErrorsProperty = HasValidationErrorsPropertyKey.DependencyProperty;

    private static readonly DependencyProperty InitialEditorTextProperty = DependencyProperty.RegisterAttached(
        "InitialEditorText", typeof(string), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(null));

    private static readonly DependencyPropertyKey IsEditedValuePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsEditedValue", typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(false));

    public static readonly DependencyProperty IsEditedValueProperty = IsEditedValuePropertyKey.DependencyProperty;

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

    public static readonly DependencyProperty AcceptanceParametersOnlyProperty = DependencyProperty.Register(
        nameof(AcceptanceParametersOnly), typeof(bool?), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(null, OnSelectedObjectChanged));

    public static readonly DependencyProperty IsCompactPresentationProperty = DependencyProperty.Register(
        nameof(IsCompactPresentation), typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(false, OnCompactPresentationChanged));

    public static readonly DependencyProperty PrimaryParametersOnlyProperty = DependencyProperty.Register(
        nameof(PrimaryParametersOnly), typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(false, OnPrimaryParametersOnlyChanged));

    public static readonly DependencyProperty IsPagedPresentationProperty = DependencyProperty.Register(
        nameof(IsPagedPresentation), typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(false, OnPrimaryParametersOnlyChanged));

    public static readonly DependencyProperty CanEditValuesProperty = DependencyProperty.Register(
        nameof(CanEditValues), typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(true));

    public bool CanEditValues
    {
        get => (bool)GetValue(CanEditValuesProperty);
        set => SetValue(CanEditValuesProperty, value);
    }

    public static readonly DependencyProperty IsOnParameterPageProperty = DependencyProperty.RegisterAttached(
        "IsOnParameterPage", typeof(bool), typeof(RecipeStepPropertyGridHost), new PropertyMetadata(true));

    public static bool GetIsOnParameterPage(DependencyObject owner) => (bool)owner.GetValue(IsOnParameterPageProperty);
    public static void SetIsOnParameterPage(DependencyObject owner, bool value) => owner.SetValue(IsOnParameterPageProperty, value);

    public bool IsPagedPresentation
    {
        get => (bool)GetValue(IsPagedPresentationProperty);
        set => SetValue(IsPagedPresentationProperty, value);
    }

    public bool PrimaryParametersOnly
    {
        get => (bool)GetValue(PrimaryParametersOnlyProperty);
        set => SetValue(PrimaryParametersOnlyProperty, value);
    }

    private static void OnPrimaryParametersOnlyChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var host = (RecipeStepPropertyGridHost)owner;
        if (args.Property == PrimaryParametersOnlyProperty)
        {
            if ((bool)args.OldValue) host.primaryParameterPage = host.selectedParameterPage;
            else host.detailedParameterPage = host.selectedParameterPage;
            host.selectedParameterPage = (bool)args.NewValue ? host.primaryParameterPage : host.detailedParameterPage;
        }
        host.WrapPropertyContent();
    }

    public bool IsCompactPresentation
    {
        get => (bool)GetValue(IsCompactPresentationProperty);
        set => SetValue(IsCompactPresentationProperty, value);
    }

    public bool ShowAdvancedParameters
    {
        get => (bool)GetValue(ShowAdvancedParametersProperty);
        set => SetValue(ShowAdvancedParametersProperty, value);
    }

    public bool? AcceptanceParametersOnly
    {
        get => (bool?)GetValue(AcceptanceParametersOnlyProperty);
        set => SetValue(AcceptanceParametersOnlyProperty, value);
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

    public int VisiblePropertyCount => (int)GetValue(VisiblePropertyCountProperty);

    public bool HasValidationErrors => (bool)GetValue(HasValidationErrorsProperty);

    public static bool GetIsEditedValue(DependencyObject editor) => (bool)editor.GetValue(IsEditedValueProperty);

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
        var invalid = IsPagedPresentation ? FindVisualChildren<Control>(InnerGrid).FirstOrDefault(Validation.GetHasError) : null;
        if (invalid is not null) ShowParameterEditorPage(invalid);
        var target = invalid ?? editors.FirstOrDefault(Validation.GetHasError) ?? editors.FirstOrDefault();
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

        RefreshValidationState();
        if (FindVisualChildren<FrameworkElement>(InnerGrid).FirstOrDefault(Validation.GetHasError) is { } invalidEditor)
        {
            FocusInvalidEditor();
            message = Validation.GetErrors(invalidEditor).FirstOrDefault()?.ErrorContent?.ToString()
                ?? "Correct the highlighted PropertyGrid value before applying.";
            return false;
        }

        if (InnerGrid.IsKeyboardFocusWithin && Keyboard.FocusedElement is UIElement focused)
        {
            focused.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
        }

        message = string.Empty;
        return true;
    }

    private static void OnSelectedObjectChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var host = (RecipeStepPropertyGridHost)sender;
        if (args.Property == SelectedObjectProperty) host.selectedParameterPage = host.primaryParameterPage = host.detailedParameterPage = 0;
        if (host.IsLoaded)
        {
            host.QueueSelectedObjectRefresh();
        }
        else
        {
            host.RefreshSelectedObject();
        }
    }

    private static void OnCompactPresentationChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var host = (RecipeStepPropertyGridHost)owner;
        host.ApplyCompactPresentation();
        if (host.IsLoaded) host.QueueSelectedObjectRefresh();
        else if (host.InnerGrid is not null) host.RefreshSelectedObject();
    }

    private void ApplyCompactPresentation()
    {
        if (PropertySearchRow is null || InnerGrid is null) return;
        PropertySearchRow.Visibility = !IsCompactPresentation && VisiblePropertyCount > 6 ? Visibility.Visible : Visibility.Collapsed;
        InnerGrid.PropertyNameColumnWidth = new GridLength(IsCompactPresentation ? 104 : 128);
        InnerGrid.EditorColumnMinWidth = IsCompactPresentation ? 80 : 140;
        if (IsCompactPresentation)
        {
            InnerGrid.SetCurrentValue(UIElement.FocusableProperty, false);
            InnerGrid.SetCurrentValue(Control.IsTabStopProperty, false);
            InnerGrid.SetCurrentValue(Control.TemplateProperty, (ControlTemplate)Resources["CompactParameterGridTemplate"]);
            InnerGrid.SetCurrentValue(Control.FontFamilyProperty, (FontFamily)Resources["CompactParameterFont"]);
            InnerGrid.SetCurrentValue(Control.FontSizeProperty, 13d);
        }
        else
        {
            InnerGrid.ClearValue(UIElement.FocusableProperty);
            InnerGrid.ClearValue(Control.IsTabStopProperty);
            InnerGrid.ClearValue(Control.TemplateProperty);
            InnerGrid.ClearValue(Control.FontFamilyProperty);
            InnerGrid.ClearValue(Control.FontSizeProperty);
            if (InnerGrid.Layout is { } layout)
            {
                layout.ClearValue(UIElement.FocusableProperty);
                layout.ClearValue(Control.IsTabStopProperty);
                layout.ClearValue(Control.TemplateProperty);
            }
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ApplyCompactPresentation();
        OpenVisionLanguageService.LanguageChanged += OnLanguageChanged;
        RefreshSelectedObject();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        parameterPageLayoutOperation?.Abort();
        parameterPageLayoutOperation = null;
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
            InnerGrid.SelectedObject = LocalizedPropertyGridObject.Create(SelectedObject, ShowAdvancedParameters, AcceptanceParametersOnly);
        }
        SetValue(VisiblePropertyCountPropertyKey, InnerGrid.Properties?.Count ?? 0);
        PropertySearchRow.Visibility = !IsCompactPresentation && VisiblePropertyCount > 6 ? Visibility.Visible : Visibility.Collapsed;
        if (PropertySearchRow.Visibility == Visibility.Collapsed) PropertyFilterBox.SetCurrentValue(TextBox.TextProperty, string.Empty);
        InnerGrid.UpdateLayout();
        WrapPropertyContent();
        RefreshValidationState();
    }

    private void OnReadOnlyEditorsLoaded(object sender, RoutedEventArgs args) => WrapPropertyContent();

    private void ConfigureParameterPages()
    {
        if (ParameterPageSelector is null) return;
        if (!IsCompactPresentation || !IsPagedPresentation)
        {
            foreach (var field in pagedParameterFields) SetIsOnParameterPage(field, true);
            pagedParameterFields = [];
            ParameterPageNavigation.Visibility = Visibility.Collapsed;
            return;
        }
        var fields = FindVisualChildren<System.Windows.Controls.WpfPropertyGrid.PropertyContainer>(InnerGrid).Cast<FrameworkElement>().ToArray();
        foreach (var field in fields) SetIsOnParameterPage(field, true);
        pagedParameterFields = fields.Where(field => field.Visibility == Visibility.Visible).ToArray();
        var pageCount = (pagedParameterFields.Length + 2) / 3;
        selectedParameterPage = Math.Clamp(selectedParameterPage, 0, Math.Max(0, pageCount - 1));
        updatingParameterPages = true;
        ParameterPageSelector.ItemsSource = Enumerable.Range(0, pageCount)
            .Select(page => $"{page * 3 + 1}–{Math.Min((page + 1) * 3, pagedParameterFields.Length)} / {pagedParameterFields.Length}").ToArray();
        ParameterPageSelector.SelectedIndex = selectedParameterPage;
        ParameterPageNavigation.Visibility = pageCount > 1 ? Visibility.Visible : Visibility.Collapsed;
        updatingParameterPages = false;
        ApplyParameterPage();
    }

    private void ApplyParameterPage()
    {
        for (var index = 0; index < pagedParameterFields.Length; index++)
            SetIsOnParameterPage(pagedParameterFields[index], index / 3 == selectedParameterPage);
    }

    private void OnParameterPageSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (updatingParameterPages || ParameterPageSelector.SelectedIndex < 0) return;
        selectedParameterPage = ParameterPageSelector.SelectedIndex;
        ApplyParameterPage();
        ScrollToTop();
    }

    private void ShowParameterEditorPage(DependencyObject editor)
    {
        var index = Array.FindIndex(pagedParameterFields, field => ReferenceEquals(field, editor) || field.IsAncestorOf(editor));
        if (index < 0) return;
        ParameterPageSelector.SetCurrentValue(Selector.SelectedIndexProperty, index / 3);
        ApplyParameterPage();
        UpdateLayout();
    }

    private void OnShowInvalidParameterClick(object sender, RoutedEventArgs args) => FocusInvalidEditor();

    private void WrapPropertyContent()
    {
        if (IsCompactPresentation)
        {
            if (InnerGrid.Layout is System.Windows.Controls.WpfPropertyGrid.Design.AlphabeticalLayout layout)
            {
                layout.SetCurrentValue(UIElement.FocusableProperty, false);
                layout.SetCurrentValue(Control.IsTabStopProperty, false);
                layout.SetCurrentValue(Control.TemplateProperty, (ControlTemplate)Resources["CompactParameterLayoutTemplate"]);
            }
            InnerGrid.UpdateLayout();
            foreach (var items in FindVisualChildren<System.Windows.Controls.WpfPropertyGrid.Design.PropertyItemsLayout>(InnerGrid))
            {
                items.SetCurrentValue(ItemsControl.ItemsPanelProperty, (ItemsPanelTemplate)Resources[AcceptanceParametersOnly == true ? "CompactAcceptanceColumnPanel" : "CompactParameterColumnPanel"]);
                items.SetCurrentValue(MarginProperty, AcceptanceParametersOnly == true ? new Thickness(-5, 0, -5, 0) : new Thickness());
            }
            InnerGrid.UpdateLayout();
            foreach (var field in FindVisualChildren<System.Windows.Controls.WpfPropertyGrid.PropertyContainer>(InnerGrid))
            {
                field.SetCurrentValue(UIElement.FocusableProperty, false);
                field.SetCurrentValue(Control.IsTabStopProperty, false);
                field.SetCurrentValue(Control.TemplateProperty, (ControlTemplate)Resources["CompactParameterFieldTemplate"]);
                field.SetCurrentValue(StyleProperty, (Style)Resources["CompactParameterContainerStyle"]);
                field.SetCurrentValue(MarginProperty, AcceptanceParametersOnly == true ? new Thickness(5, 0, 5, 8) : new Thickness(0, 0, 0, 8));
            }
            InnerGrid.UpdateLayout();
            foreach (var editor in FindVisualChildren<TextBox>(InnerGrid).Where(editor =>
                editor.GetBindingExpression(TextBox.TextProperty)?.DataItem is System.Windows.Controls.WpfPropertyGrid.PropertyItemValue))
            {
                editor.SetCurrentValue(StyleProperty, (Style)Resources["CompactParameterTextBoxStyle"]);
                if (editor.GetValue(InitialEditorTextProperty) is null) editor.SetValue(InitialEditorTextProperty, editor.Text);
            }
            foreach (var editor in FindVisualChildren<CheckBox>(InnerGrid).Where(editor =>
                editor.GetBindingExpression(ToggleButton.IsCheckedProperty)?.DataItem is System.Windows.Controls.WpfPropertyGrid.PropertyItemValue))
                editor.SetCurrentValue(StyleProperty, (Style)Resources["CompactParameterCheckBoxStyle"]);
        }
        EnsureNumericEditorValidation();
        foreach (var caption in FindVisualChildren<TextBlock>(InnerGrid).Where(text => text.GetType().Name == "PropertyNameTextBlock"))
        {
            caption.SetCurrentValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            caption.SetCurrentValue(TextBlock.TextTrimmingProperty, TextTrimming.None);
        }
        foreach (var combo in FindVisualChildren<ComboBox>(InnerGrid))
        {
            if (IsCompactPresentation)
            {
                combo.SetCurrentValue(StyleProperty, (Style)Resources["CompactParameterComboBoxStyle"]);
                if (combo.DataContext is System.Windows.Controls.WpfPropertyGrid.PropertyItemValue && combo.GetValue(InitialEditorTextProperty) is null)
                    combo.SetValue(InitialEditorTextProperty, Convert.ToString(combo.SelectedValue, CultureInfo.InvariantCulture));
            }
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
        parameterPageLayoutOperation?.Abort();
        // Visibility bindings must settle before the existing fields are grouped.
        parameterPageLayoutOperation = Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            parameterPageLayoutOperation = null;
            ConfigureParameterPages();
        });
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
        RefreshValidationState();

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
        RefreshValidationState();
    }

    private void OnInnerGridEditorTextChanged(object sender, TextChangedEventArgs args)
    {
        if (args.OriginalSource is not TextBox { IsKeyboardFocused: true } editor
            || editor.GetBindingExpression(TextBox.TextProperty) is not { } expression) return;
        if (IsCompactPresentation)
        {
            // Validate the displayed draft without changing its source or commit timing.
            expression.ValidateWithoutUpdate();
            if (editor.GetValue(InitialEditorTextProperty) is string initial)
                editor.SetValue(IsEditedValuePropertyKey, !string.Equals(initial, editor.Text, StringComparison.Ordinal));
            RefreshValidationState();
        }
        if (PropertyValueChangedCommand?.CanExecute(null) == true) PropertyValueChangedCommand.Execute(null);
    }

    private void OnInnerGridEditorSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.OriginalSource is ListBox { IsEnabled: true, IsKeyboardFocusWithin: true, TemplatedParent: TextBox choiceEditor }
            && choiceEditor.GetBindingExpression(TextBox.TextProperty) is { DataItem: System.Windows.Controls.WpfPropertyGrid.PropertyItemValue } choiceBinding)
        {
            choiceBinding.UpdateSource();
            if (choiceEditor.GetValue(InitialEditorTextProperty) is string initialChoice)
                choiceEditor.SetValue(IsEditedValuePropertyKey, !string.Equals(initialChoice, choiceEditor.Text, StringComparison.Ordinal));
            if (PropertyValueChangedCommand?.CanExecute(null) == true) PropertyValueChangedCommand.Execute(null);
        }
        if (IsCompactPresentation && args.OriginalSource is ComboBox { IsEnabled: true } editor
            && editor.DataContext is System.Windows.Controls.WpfPropertyGrid.PropertyItemValue && editor.GetValue(InitialEditorTextProperty) is string initial)
            editor.SetValue(IsEditedValuePropertyKey, !string.Equals(initial, Convert.ToString(editor.SelectedValue, CultureInfo.InvariantCulture), StringComparison.Ordinal));
    }

    private void RefreshValidationState() => SetValue(HasValidationErrorsPropertyKey,
        FindVisualChildren<FrameworkElement>(InnerGrid).Any(Validation.GetHasError));

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

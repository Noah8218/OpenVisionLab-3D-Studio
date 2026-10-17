using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public enum RecipeReviewPresentationMode
{
    Standard,
    Validation,
    Inspection,
    Results
}

public partial class RecipePipelineReviewView : UserControl
{
    private readonly ControlTemplate standardReviewTabsTemplate;
    private readonly DataGridColumn[] standardValidationColumns;
    private readonly Style standardValidationRowStyle;
    private readonly Style? standardValidationHeaderStyle;
    private readonly Style? standardValidationCellStyle;
    private readonly double standardValidationFontSize;
    private RecipeReviewPresentationMode presentationMode;
    private RecipePipelineReviewValidationViewModel? validationWorkspace;
    private int inspectionReturnTabIndex;

    public event EventHandler? ActiveReviewChanged;

    public static readonly DependencyProperty RunRecordStepsProperty = DependencyProperty.Register(
        nameof(RunRecordSteps),
        typeof(IEnumerable),
        typeof(RecipePipelineReviewView),
        new PropertyMetadata(null));

    public static readonly DependencyProperty IsValidationSamplesExternalProperty = DependencyProperty.Register(
        nameof(IsValidationSamplesExternal), typeof(bool), typeof(RecipePipelineReviewView), new PropertyMetadata(false, OnValidationSamplesExternalChanged));

    public static readonly DependencyProperty IsRunHistoryExternalProperty = DependencyProperty.Register(
        nameof(IsRunHistoryExternal), typeof(bool), typeof(RecipePipelineReviewView), new PropertyMetadata(false, OnRunHistoryExternalChanged));

    public static readonly DependencyProperty DockPaneCommandsProperty = DependencyProperty.Register(
        nameof(DockPaneCommands), typeof(object), typeof(RecipePipelineReviewView), new PropertyMetadata(null));

    public static readonly DependencyProperty ValidationMatrixColumnsProperty = DependencyProperty.Register(
        nameof(ValidationMatrixColumns), typeof(IReadOnlyList<ValidationMatrixColumn>), typeof(RecipePipelineReviewView), new PropertyMetadata(null, OnValidationMatrixColumnsChanged));

    public static readonly DependencyProperty ValidationTableColumnsProperty = DependencyProperty.Register(
        nameof(ValidationTableColumns), typeof(IReadOnlyList<ValidationTableColumn>), typeof(RecipePipelineReviewView), new PropertyMetadata(null, OnValidationMatrixColumnsChanged));

    public static readonly DependencyProperty RunRecordSummaryProperty = DependencyProperty.Register(
        nameof(RunRecordSummary),
        typeof(string),
        typeof(RecipePipelineReviewView),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty RunRecordContextProperty = DependencyProperty.Register(
        nameof(RunRecordContext),
        typeof(ShellMainWindowViewModel),
        typeof(RecipePipelineReviewView),
        new PropertyMetadata(null));

    public RecipePipelineReviewView()
    {
        InitializeComponent();
        standardReviewTabsTemplate = ReviewTabs.Template;
        standardValidationColumns = ValidationSamplesGrid.Columns.ToArray();
        standardValidationRowStyle = ValidationSamplesGrid.RowStyle;
        standardValidationHeaderStyle = ValidationSamplesGrid.ColumnHeaderStyle;
        standardValidationCellStyle = ValidationSamplesGrid.CellStyle;
        standardValidationFontSize = ValidationSamplesGrid.FontSize;
        SetBinding(ValidationMatrixColumnsProperty, new Binding("ValidationMatrixColumns"));
        SetBinding(ValidationTableColumnsProperty, new Binding("ValidationTableColumns"));
        ReviewTabs.SelectionChanged += ReviewTabs_SelectionChanged;
        SizeChanged += RecipePipelineReviewView_SizeChanged;
        DataContextChanged += RecipePipelineReviewView_DataContextChanged;
    }

    public RecipeReviewPresentationMode PresentationMode => presentationMode;

    public bool IsValidationSamplesExternal
    {
        get => (bool)GetValue(IsValidationSamplesExternalProperty);
        set => SetValue(IsValidationSamplesExternalProperty, value);
    }

    public bool IsRunHistoryExternal
    {
        get => (bool)GetValue(IsRunHistoryExternalProperty);
        set => SetValue(IsRunHistoryExternalProperty, value);
    }

    public object? DockPaneCommands
    {
        get => GetValue(DockPaneCommandsProperty);
        set => SetValue(DockPaneCommandsProperty, value);
    }

    public IReadOnlyList<ValidationMatrixColumn> ValidationMatrixColumns => (IReadOnlyList<ValidationMatrixColumn>?)GetValue(ValidationMatrixColumnsProperty) ?? [];
    public IReadOnlyList<ValidationTableColumn> ValidationTableColumns => (IReadOnlyList<ValidationTableColumn>?)GetValue(ValidationTableColumnsProperty) ?? [];

    private static void OnValidationMatrixColumnsChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args) =>
        ((RecipePipelineReviewView)owner).ApplyValidationMatrixColumns();

    private void ApplyValidationMatrixColumns()
    {
        if (ValidationSamplesGrid is null || standardValidationColumns is null) return;
        ValidationSamplesGrid.Columns.Clear();
        var matrix = IsValidationSamplesExternal && presentationMode == RecipeReviewPresentationMode.Validation && ValidationSection == ValidationWorkspaceSection.Results;
        ValidationSamplesGrid.RowStyle = matrix ? (Style)Resources["ValidationMatrixRowStyle"] : standardValidationRowStyle;
        ValidationSamplesGrid.ColumnHeaderStyle = matrix ? (Style)Resources["ValidationMatrixHeaderStyle"] : standardValidationHeaderStyle;
        ValidationSamplesGrid.CellStyle = matrix ? (Style)Resources["ValidationMatrixGridCellStyle"] : standardValidationCellStyle;
        ValidationSamplesGrid.FontSize = matrix ? 12.5 : standardValidationFontSize;
        ValidationSamplesPane.SetResourceReference(Border.BackgroundProperty, matrix ? "ThreeD.PanelBrush" : "ThreeD.PanelAlternateBrush");
        if (!matrix)
        {
            foreach (var column in standardValidationColumns) ValidationSamplesGrid.Columns.Add(column);
            standardValidationColumns[2].Width = new DataGridLength(1.5, DataGridLengthUnitType.Star);
            ValidationMatrixSummary.Visibility = Visibility.Collapsed;
            ValidationMatrixHeaderActions.Visibility = Visibility.Collapsed;
            return;
        }
        ValidationMatrixSummary.SetBinding(VisibilityProperty, new Binding("HasValidationResults") { Converter = (IValueConverter)Resources["BooleanToVisibilityConverter"] });
        ValidationMatrixHeaderActions.Visibility = Visibility.Visible;
        ValidationSamplesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = ThreeDLocalization.Shared.Resolve("ThreeD.Column.Sample", "샘플", "Sample"), Width = 180,
            Binding = new Binding(nameof(ValidationSetSampleRow.FileName)), ElementStyle = (Style)Resources["ValidationMatrixFileStyle"]
        });
        foreach (var (key, korean, english, template) in new[]
        {
            ("Expected", "기대", "Expected", "ValidationMatrixExpectedTemplate"),
            ("Result", "결과", "Result", "ValidationMatrixStatusTemplate"),
            ("Agreement", "비교", "Comparison", "ValidationMatrixAgreementTemplate")
        })
            ValidationSamplesGrid.Columns.Add(new DataGridTemplateColumn
            {
                Header = ThreeDLocalization.Shared.Resolve("ThreeD.Column." + key, korean, english), Width = DataGridLength.Auto,
                MinWidth = key == "Expected" ? 62 : 82, CellTemplate = (DataTemplate)Resources[template]
            });
        foreach (var column in ValidationTableColumns)
        {
            if (column.Members is not null)
            {
                var members = new FrameworkElementFactory(typeof(ItemsControl));
                members.SetValue(StyleProperty, Resources["ValidationGroupCellStyle"]);
                members.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(".") { Converter = (ValidationMatrixValueConverter)Resources["ValidationMatrixDisplayValue"], ConverterParameter = column });
                ValidationSamplesGrid.Columns.Add(new DataGridTemplateColumn
                {
                    Header = column.Header, Width = DataGridLength.Auto, MinWidth = 90,
                    CellTemplate = new DataTemplate { VisualTree = members }
                });
                continue;
            }
            ValidationSamplesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Width = DataGridLength.SizeToHeader, MinWidth = 130,
                Binding = new Binding(".") { Converter = (ValidationMatrixValueConverter)Resources["ValidationMatrixDisplayValue"], ConverterParameter = column.Metric },
                ElementStyle = (Style)Resources["ValidationMatrixDisplayCellStyle"]
            });
        }
        ValidationSamplesGrid.Columns.Add(new DataGridTextColumn
        {
            Header = ThreeDLocalization.Shared.Resolve("ThreeD.Column.Cause", "원인", "Cause"), Width = DataGridLength.Auto, MinWidth = 220,
            Binding = new Binding(nameof(ValidationSetSampleRow.Message)), ElementStyle = (Style)Resources["ValidationMatrixEvidenceStyle"]
        });
    }

    private static void OnRunHistoryExternalChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is RecipePipelineReviewView view && view.RunRecordActionPanel is not null) view.ApplyResponsiveLayout(view.ActualHeight);
    }

    public bool IsDedicatedValidationWorkspace =>
        presentationMode == RecipeReviewPresentationMode.Validation
        && ValidationWorkspaceNavigation.Visibility == Visibility.Visible
        && ReviewTabs.SelectedIndex == 4;

    public ValidationWorkspaceSection ValidationSection =>
        validationWorkspace?.Section ?? ValidationWorkspaceSection.Samples;

    public bool HasLocalizedValidationNavigation =>
        HasAccessibleText(ValidationSamplesNavigation)
        && HasAccessibleText(ValidationResultsNavigation)
        && HasAccessibleText(ValidationFailuresNavigation)
        && HasAccessibleText(ValidationThresholdNavigation)
        && HasAccessibleText(ValidationHeldOutNavigation);

    public bool HasAccessibleValidationSampleSetAction =>
        RunValidationSampleSetButton.Visibility == Visibility.Visible
        && RunValidationSampleSetButton.IsTabStop
        && RunValidationSampleSetButton.Focusable
        && string.Equals(
            AutomationProperties.GetAutomationId(RunValidationSampleSetButton),
            "ValidationSetRunAllButton",
            StringComparison.Ordinal)
        && HasAccessibleText(RunValidationSampleSetButton);

    public bool HasValidationSamplesFirstUseClarity =>
        ValidationSamplesGuidance.Visibility == Visibility.Visible
        && ValidationSampleRoleBar.Visibility == Visibility.Visible
        && ValidationResultsFilterBar.Visibility == Visibility.Collapsed
        && ValidationReviewActions.Visibility == Visibility.Collapsed
        && !string.IsNullOrWhiteSpace(ValidationSamplesGuidanceText())
        && string.Equals(
            AutomationProperties.GetAutomationId(ValidationSamplesGuidance),
            "ValidationSamplesMeaningGuide",
            StringComparison.Ordinal)
        && string.Equals(
            AutomationProperties.GetAutomationId(ValidationSampleRoleBar),
            "ValidationSampleRoleAssignment",
            StringComparison.Ordinal);

    public bool HasValidationResultsReviewControls =>
        ValidationSamplesGuidance.Visibility == Visibility.Collapsed
        && ValidationSampleRoleBar.Visibility == Visibility.Collapsed
        && ValidationResultsFilterBar.Visibility == Visibility.Visible
        && ValidationReviewActions.Visibility == Visibility.Visible;

    public bool IsValidationIssueNavigationVisible =>
        ValidationIssueNavigationHost.Visibility == Visibility.Visible
        && ValidationIssueCommands.Visibility == Visibility.Visible;

    public bool IsFailureOperatorSummaryVisible =>
        ValidationFailureOperatorSummary.Visibility == Visibility.Visible
        && !string.IsNullOrWhiteSpace(ValidationFailureSampleValue.Text)
        && !string.IsNullOrWhiteSpace(ValidationFailureRuleValue.Text)
        && !string.IsNullOrWhiteSpace(ValidationFailureReasonValue.Text);

    public void SetPresentationMode(RecipeReviewPresentationMode mode)
    {
        var previousMode = presentationMode;
        if (mode == RecipeReviewPresentationMode.Results)
        {
            BindingOperations.ClearBinding(ReviewTabs, Selector.SelectedIndexProperty);
        }
        else if (previousMode == RecipeReviewPresentationMode.Results)
        {
            ReviewTabs.SetBinding(Selector.SelectedIndexProperty, new Binding("SelectedReviewTabIndex") { Mode = BindingMode.TwoWay });
        }
        if (mode == RecipeReviewPresentationMode.Inspection
            && previousMode != RecipeReviewPresentationMode.Inspection)
        {
            inspectionReturnTabIndex = Math.Min(ReviewTabs.SelectedIndex, 4);
        }
        presentationMode = mode;
        ValidationSetHeaderSummary.Visibility =
            mode == RecipeReviewPresentationMode.Validation
                ? Visibility.Collapsed
                : Visibility.Visible;
        ValidationSetTitleSummary.Visibility =
            mode == RecipeReviewPresentationMode.Validation
                ? Visibility.Collapsed
                : Visibility.Visible;
        ValidationWorkspaceNavigation.Visibility =
            mode == RecipeReviewPresentationMode.Validation
                ? Visibility.Visible
                : Visibility.Collapsed;
        ReviewTabs.Template = mode is RecipeReviewPresentationMode.Validation
            or RecipeReviewPresentationMode.Inspection
            or RecipeReviewPresentationMode.Results
            ? (ControlTemplate)FindResource("ContentOnlyTabControlTemplate")
            : standardReviewTabsTemplate;

        switch (mode)
        {
            case RecipeReviewPresentationMode.Validation:
                SelectReviewTab(4);
                SetValidationSection(IsValidationSamplesExternal ? ValidationWorkspaceSection.Results : ValidationWorkspaceSection.Samples);
                break;
            case RecipeReviewPresentationMode.Results:
                RestoreStandardValidationLayout();
                SelectReviewTab(3);
                break;
            case RecipeReviewPresentationMode.Inspection:
                RestoreStandardValidationLayout();
                SelectReviewTab(5);
                break;
            default:
                RestoreStandardValidationLayout();
                if (previousMode == RecipeReviewPresentationMode.Inspection)
                {
                    SelectReviewTab(inspectionReturnTabIndex);
                }
                break;
        }

        ApplyExternalSamplesPresentation();

        ApplyResponsiveLayout(ActualHeight);
    }

    public void ActivateFlowMap() => SelectReviewTab(1);

    public bool IsFlowMapSelected => ReviewTabs.SelectedIndex == 1;

    public void ActivateProblems() => SelectReviewTab(2);

    public bool IsProblemsSelected => ReviewTabs.SelectedIndex == 2;

    public IEnumerable? RunRecordSteps
    {
        get => (IEnumerable?)GetValue(RunRecordStepsProperty);
        set => SetValue(RunRecordStepsProperty, value);
    }

    public string RunRecordSummary
    {
        get => (string)GetValue(RunRecordSummaryProperty);
        set => SetValue(RunRecordSummaryProperty, value);
    }

    public ShellMainWindowViewModel? RunRecordContext
    {
        get => (ShellMainWindowViewModel?)GetValue(RunRecordContextProperty);
        set => SetValue(RunRecordContextProperty, value);
    }

    public void ActivateRunRecord() => SelectReviewTab(3);

    public bool IsRunRecordSelected => ReviewTabs.SelectedIndex == 3;

    public bool HasRunRecordHistoryControls =>
        RunRecordOpenButton is not null
        && RunRecordOpenJsonButton is not null
        && RunRecordExportButton is not null
        && RecentRunRecordCombo is not null
        && RunRecordOpenRecentButton is not null
        && RunRecordCompareRecentButton is not null
        && RunRecordScrollViewer is not null
        && RunRecordComparisonPanel is not null
        && RunRecordComparisonDetailsViewer is not null
        && RunRecordEvidenceSummary is not null
        && string.Equals(
            AutomationProperties.GetAutomationId(RunRecordEvidenceSummary),
            "RunRecordEvidenceSummary",
            StringComparison.Ordinal)
        && string.Equals(
            AutomationProperties.GetAutomationId(RunRecordCompareRecentButton),
            "RunRecordCompareRecentButton",
            StringComparison.Ordinal)
        && string.Equals(
            AutomationProperties.GetAutomationId(RunRecordComparisonPanel),
            "RunRecordComparisonPanel",
            StringComparison.Ordinal);

    private void RunRecordComparisonPanel_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (sender is FrameworkElement { IsVisible: true } comparisonPanel)
        {
            _ = Dispatcher.BeginInvoke(() => comparisonPanel.BringIntoView());
        }
    }

    public bool HasPrivacySafeSupportBundleControls =>
        SupportBundleButton is not null
        && SupportBundlePrivacyNotice is not null
        && string.Equals(
            AutomationProperties.GetAutomationId(SupportBundleButton),
            "PrivacySafeSupportBundleButton",
            StringComparison.Ordinal)
        && string.Equals(
            AutomationProperties.GetAutomationId(SupportBundlePrivacyNotice),
            "SupportBundlePrivacyNotice",
            StringComparison.Ordinal);

    public void ActivateValidationSet() => SelectReviewTab(4);

    public bool IsValidationSetSelected => ReviewTabs.SelectedIndex == 4;

    public void ActivateInspectionQueue() => SelectReviewTab(5);

    public bool IsInspectionQueueSelected => ReviewTabs.SelectedIndex == 5;

    private void ReviewTabs_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.OriginalSource, ReviewTabs))
        {
            ActiveReviewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectReviewTab(int index)
    {
        ReviewTabs.SetCurrentValue(Selector.SelectedIndexProperty, index);
    }

    private static bool HasAccessibleText(ContentControl control) =>
        !string.IsNullOrWhiteSpace(control.Content?.ToString())
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control));

    public void SetValidationSection(ValidationWorkspaceSection section)
    {
        if (validationWorkspace?.SelectSectionCommand.CanExecute(section) == true)
        {
            validationWorkspace.SelectSectionCommand.Execute(section);
        }

        ApplyValidationSectionLayout();
    }

    private void ApplyValidationSectionLayout()
    {
        var section = ValidationSection;

        var usesPrimaryReview = section is ValidationWorkspaceSection.Samples
            or ValidationWorkspaceSection.Results
            or ValidationWorkspaceSection.Failures;
        var samplesOnly = section == ValidationWorkspaceSection.Samples;
        ValidationSetFilterBar.Visibility = usesPrimaryReview
            ? Visibility.Visible
            : Visibility.Collapsed;
        ValidationSamplesGuidance.Visibility = samplesOnly
            ? Visibility.Visible
            : Visibility.Collapsed;
        ValidationSampleRoleBar.Visibility = samplesOnly
            ? Visibility.Visible
            : Visibility.Collapsed;
        ValidationResultsFilterBar.Visibility = section is ValidationWorkspaceSection.Results
            or ValidationWorkspaceSection.Failures
                ? Visibility.Visible
                : Visibility.Collapsed;
        ValidationReviewActions.Visibility = section is ValidationWorkspaceSection.Results
            or ValidationWorkspaceSection.Failures
                ? Visibility.Visible
                : Visibility.Collapsed;
        ValidationIssueNavigationHost.Visibility =
            section == ValidationWorkspaceSection.Failures
                ? Visibility.Visible
                : Visibility.Collapsed;
        ValidationSetPrimaryGrid.Visibility = usesPrimaryReview
            ? Visibility.Visible
            : Visibility.Collapsed;
        ValidationEvidenceExpander.Visibility =
            section == ValidationWorkspaceSection.Results
                ? Visibility.Visible
                : Visibility.Collapsed;
        ValidationThresholdExpander.Visibility =
            section is ValidationWorkspaceSection.Thresholds or ValidationWorkspaceSection.HeldOut
                ? Visibility.Visible
                : Visibility.Collapsed;
        ValidationEvidenceExpander.IsExpanded =
            section == ValidationWorkspaceSection.Results;
        ValidationThresholdExpander.IsExpanded =
            section is ValidationWorkspaceSection.Thresholds or ValidationWorkspaceSection.HeldOut;
        RunValidationSampleSetButton.Visibility =
            section == ValidationWorkspaceSection.Samples
                ? Visibility.Visible
                : Visibility.Collapsed;
        OpenValidationIssueInTeachButton.Visibility =
            section == ValidationWorkspaceSection.Failures
                ? Visibility.Visible
                : Visibility.Collapsed;
        ValidationFailureOperatorSummary.Visibility =
            section == ValidationWorkspaceSection.Failures
                ? Visibility.Visible
                : Visibility.Collapsed;

        ApplyValidationPrimaryLayout();
        ApplyThresholdSection(section);
        ApplyExternalSamplesPresentation();
    }

    private static void OnValidationSamplesExternalChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        if (owner is RecipePipelineReviewView view && view.ReviewTabs is not null)
        {
            if (view.IsValidationSamplesExternal && view.ValidationSection == ValidationWorkspaceSection.Samples)
            {
                view.SetValidationSection(ValidationWorkspaceSection.Results);
            }
            view.ApplyValidationSectionLayout();
        }
    }

    private void ApplyExternalSamplesPresentation()
    {
        var external = IsValidationSamplesExternal && presentationMode == RecipeReviewPresentationMode.Validation;
        ValidationHeaderLeading.Visibility = external ? Visibility.Visible : Visibility.Collapsed;
        ValidationSamplesNavigation.Visibility = external ? Visibility.Collapsed : Visibility.Visible;
        ValidationHeldOutNavigation.Visibility = external ? Visibility.Collapsed : Visibility.Visible;
        ValidationAdvancedNavigation.Visibility = external ? Visibility.Visible : Visibility.Collapsed;
        ValidationRunActions.Visibility = external ? Visibility.Collapsed : Visibility.Visible;
        RunCurrentRecipeButton.Visibility = external ? Visibility.Collapsed : Visibility.Visible;
        ValidationCurrentRecipeStatus.Visibility = external ? Visibility.Collapsed : Visibility.Visible;
        ValidationSetHeader.Visibility = external ? Visibility.Collapsed : Visibility.Visible;
        ReviewSurface.Padding = new Thickness(external || presentationMode == RecipeReviewPresentationMode.Inspection ? 0 : 12);
        ReviewSurface.BorderThickness = presentationMode == RecipeReviewPresentationMode.Inspection ? new Thickness(0) : new Thickness(0, 1, 0, 0);
        if (presentationMode == RecipeReviewPresentationMode.Inspection)
        {
            ReviewTabs.Padding = new Thickness(0);
            ReviewTabs.BorderThickness = new Thickness(0);
        }
        else
        {
            ReviewTabs.ClearValue(Control.PaddingProperty);
            ReviewTabs.ClearValue(Control.BorderThicknessProperty);
        }
        ValidationWorkspaceNavigation.Margin = new Thickness(0, 0, 0, external ? 0 : 8);
        ValidationWorkspaceNavigation.Padding = external ? new Thickness(6, 0, 4, 0) : new Thickness(8, 6, 8, 6);
        ValidationWorkspaceNavigation.BorderThickness = external ? new Thickness(0) : new Thickness(1);
        ValidationWorkspaceNavigation.CornerRadius = new CornerRadius(external ? 0 : 4);
        ValidationWorkspaceNavigation.SetResourceReference(Border.BackgroundProperty, external ? "ThreeD.DockHeaderBrush" : "ThreeD.PanelAlternateBrush");
        if (external)
        {
            ValidationWorkspaceNavigation.Height = 34;
        }
        else
        {
            ValidationWorkspaceNavigation.ClearValue(HeightProperty);
        }
        ValidationWorkspaceContent.Margin = new Thickness(0, external ? 0 : 8, 0, 0);
        ValidationRedesignContent.Visibility = external && ValidationSection is ValidationWorkspaceSection.Failures or ValidationWorkspaceSection.Thresholds ? Visibility.Visible : Visibility.Collapsed;
        ValidationMismatchCardsHost.Visibility = external && ValidationSection == ValidationWorkspaceSection.Failures ? Visibility.Visible : Visibility.Collapsed;
        ValidationDistributionWorkspace.Visibility = external && ValidationSection == ValidationWorkspaceSection.Thresholds ? Visibility.Visible : Visibility.Collapsed;
        BindActiveSelector(ValidationMismatchCards, nameof(ToolWorkbenchViewModel.SelectedValidationSetSample), ValidationMismatchCardsHost.IsVisible || ValidationMismatchCardsHost.Visibility == Visibility.Visible);
        BindActiveSelector(ValidationDistributionOutputs, nameof(ToolWorkbenchViewModel.SelectedValidationDistributionOutput), ValidationDistributionWorkspace.Visibility == Visibility.Visible);
        BindActiveSelector(ValidationDistributionCandidates, nameof(ToolWorkbenchViewModel.SelectedValidationThresholdCandidate), ValidationDistributionWorkspace.Visibility == Visibility.Visible);
        if (external)
        {
            ValidationSamplesGuidance.Visibility = Visibility.Collapsed;
            ValidationSampleRoleBar.Visibility = Visibility.Collapsed;
            ValidationResultsFilterBar.Visibility = Visibility.Collapsed;
            ValidationReviewActions.Visibility = Visibility.Collapsed;
            ValidationSetPrimaryGrid.Visibility = ValidationSection == ValidationWorkspaceSection.Results ? Visibility.Visible : Visibility.Collapsed;
            ValidationFailureOperatorSummary.Visibility = Visibility.Collapsed;
            ValidationEvidenceExpander.IsExpanded = false;
            ValidationEvidenceExpander.Visibility = Visibility.Collapsed;
            if (ValidationSection == ValidationWorkspaceSection.Thresholds) ValidationThresholdExpander.Visibility = Visibility.Collapsed;
        }
        ApplyValidationMatrixColumns();
    }

    private static void BindActiveSelector(Selector selector, string path, bool active)
    {
        // Hidden result lists must not clear canonical selection or an active candidate review.
        if (active && !BindingOperations.IsDataBound(selector, Selector.SelectedItemProperty)) selector.SetBinding(Selector.SelectedItemProperty, new Binding(path) { Mode = BindingMode.TwoWay });
        else if (!active && BindingOperations.IsDataBound(selector, Selector.SelectedItemProperty))
        {
            BindingOperations.ClearBinding(selector, Selector.SelectedItemProperty);
            selector.SetCurrentValue(Selector.SelectedItemProperty, null);
        }
    }

    private void RecipePipelineReviewView_DataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (validationWorkspace is not null)
        {
            validationWorkspace.PropertyChanged -= ValidationWorkspace_PropertyChanged;
        }

        validationWorkspace = (e.NewValue as ToolWorkbenchViewModel)?.ValidationWorkspace;
        if (validationWorkspace is not null)
        {
            validationWorkspace.PropertyChanged += ValidationWorkspace_PropertyChanged;
            if (IsValidationSamplesExternal && presentationMode == RecipeReviewPresentationMode.Validation && ValidationSection == ValidationWorkspaceSection.Samples)
            {
                SetValidationSection(ValidationWorkspaceSection.Results);
            }
        }

        ApplyValidationSectionLayout();
    }

    private void ValidationWorkspace_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        ApplyValidationSectionLayout();

    private void ApplyThresholdSection(ValidationWorkspaceSection section)
    {
        var heldOutOnly = section == ValidationWorkspaceSection.HeldOut;
        ReviewValidationThresholdButton.Visibility =
            heldOutOnly ? Visibility.Collapsed : Visibility.Visible;
        CancelValidationThresholdReviewButton.Visibility =
            heldOutOnly ? Visibility.Collapsed : Visibility.Visible;
        ApplyValidationThresholdButton.Visibility =
            heldOutOnly ? Visibility.Collapsed : Visibility.Visible;
        RevalidateDevelopmentButton.Visibility =
            heldOutOnly ? Visibility.Collapsed : Visibility.Visible;
        ReplayHeldOutButton.Visibility =
            heldOutOnly ? Visibility.Visible : Visibility.Collapsed;
        ValidationThresholdParameterChangesGrid.MaxHeight = heldOutOnly ? 0 : 78;
        ValidationThresholdDevelopmentGrid.MaxHeight = heldOutOnly ? 0 : 112;
        ValidationThresholdCandidatesGrid.Visibility =
            heldOutOnly ? Visibility.Collapsed : Visibility.Visible;
        ValidationThresholdDecisionsGrid.Visibility =
            heldOutOnly ? Visibility.Collapsed : Visibility.Visible;
        Grid.SetColumn(
            ValidationThresholdHeldOutGrid,
            heldOutOnly ? 0 : 2);
        Grid.SetColumnSpan(
            ValidationThresholdHeldOutGrid,
            heldOutOnly ? 3 : 1);
        ValidationThresholdHeldOutGrid.MaxHeight = heldOutOnly ? 420 : 0;
    }

    private void RestoreStandardValidationLayout()
    {
        ValidationSetHeaderSummary.Visibility = Visibility.Visible;
        ValidationSetTitleSummary.Visibility = Visibility.Visible;
        ValidationSetFilterBar.Visibility = Visibility.Visible;
        ValidationSamplesGuidance.Visibility = Visibility.Visible;
        ValidationSampleRoleBar.Visibility = Visibility.Visible;
        ValidationResultsFilterBar.Visibility = Visibility.Visible;
        ValidationReviewActions.Visibility = Visibility.Visible;
        ValidationIssueNavigationHost.Visibility = Visibility.Visible;
        ValidationSetPrimaryGrid.Visibility = Visibility.Visible;
        ValidationEvidenceExpander.Visibility = Visibility.Visible;
        ValidationThresholdExpander.Visibility = Visibility.Visible;
        RunValidationSampleSetButton.Visibility = Visibility.Collapsed;
        OpenValidationIssueInTeachButton.Visibility = Visibility.Collapsed;
        ValidationFailureOperatorSummary.Visibility = Visibility.Collapsed;
        ValidationSamplesPane.Visibility = Visibility.Visible;
        Grid.SetColumn(ValidationSamplesPane, 0);
        Grid.SetColumnSpan(ValidationSamplesPane, 1);
        ValidationSamplesColumn.Width = new GridLength(1.35, GridUnitType.Star);
        ValidationPrimaryGapColumn.Width = new GridLength(8);
        ValidationRecordColumn.Width = new GridLength(1.15, GridUnitType.Star);
        ValidationRecordPane.Visibility = Visibility.Visible;
        Grid.SetColumn(ValidationRecordPane, 2);
        Grid.SetColumnSpan(ValidationRecordPane, 1);
        ValidationThresholdCandidatesGrid.Visibility = Visibility.Visible;
        ValidationThresholdDecisionsGrid.Visibility = Visibility.Visible;
        ReviewValidationThresholdButton.Visibility = Visibility.Visible;
        CancelValidationThresholdReviewButton.Visibility = Visibility.Visible;
        ApplyValidationThresholdButton.Visibility = Visibility.Visible;
        RevalidateDevelopmentButton.Visibility = Visibility.Visible;
        ReplayHeldOutButton.Visibility = Visibility.Visible;
        ValidationThresholdParameterChangesGrid.MaxHeight = 78;
        ValidationThresholdDevelopmentGrid.MaxHeight = 112;
        Grid.SetColumn(ValidationThresholdHeldOutGrid, 2);
        Grid.SetColumnSpan(ValidationThresholdHeldOutGrid, 1);
        ValidationThresholdHeldOutGrid.MaxHeight = 78;
    }

    private string ValidationSamplesGuidanceText() =>
        ValidationSamplesGuidance.Child is StackPanel panel
            ? string.Join(
                " ",
                panel.Children
                    .OfType<TextBlock>()
                    .Select(text => text.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text)))
            : string.Empty;

    private void RecipePipelineReviewView_SizeChanged(
        object sender,
        SizeChangedEventArgs e)
    {
        ApplyValidationPrimaryLayout();
        ApplyResponsiveLayout(e.NewSize.Height);
    }

    internal void ApplyResponsiveLayout(double availableHeight)
    {
        var compactResults = presentationMode == RecipeReviewPresentationMode.Results
            && availableHeight > 0
            && availableHeight < 360;
        RunRecordHeader.Visibility = compactResults ? Visibility.Collapsed : Visibility.Visible;
        RunRecordHeader.Margin = compactResults
            ? new Thickness(4, 0, 4, 3)
            : new Thickness(4, 0, 4, 6);
        RunRecordEvidenceSummary.Margin = compactResults
            ? new Thickness(4, 0, 4, 3)
            : new Thickness(4, 0, 4, 7);
        RunRecordEvidenceSummary.Visibility = compactResults
            ? Visibility.Collapsed
            : Visibility.Visible;
        RunRecordEvidenceSummary.Padding = compactResults
            ? new Thickness(6, 3, 6, 3)
            : new Thickness(8, 6, 8, 6);
        RunRecordThresholdDetails.Visibility = compactResults
            ? Visibility.Collapsed
            : Visibility.Visible;
        RunRecordThresholdDetails.MaxHeight = compactResults ? 28 : 54;
        RunRecordActions.Margin = compactResults
            ? new Thickness(4, 1, 4, 3)
            : new Thickness(4, 1, 4, 7);
        RunRecordActionPanel.Visibility = compactResults || IsRunHistoryExternal
            ? Visibility.Collapsed
            : Visibility.Visible;
        RunRecordComparisonPanel.Margin = compactResults
            ? new Thickness(4, 0, 4, 3)
            : new Thickness(4, 0, 4, 7);
        RunRecordComparisonDetailsViewer.MaxHeight = compactResults ? 44 : 92;
    }

    private void ApplyValidationPrimaryLayout()
    {
        if (presentationMode != RecipeReviewPresentationMode.Validation)
        {
            return;
        }

        if (IsValidationSamplesExternal)
        {
            var matrix = ValidationSection == ValidationWorkspaceSection.Results;
            ValidationSamplesPane.Visibility = matrix ? Visibility.Visible : Visibility.Collapsed;
            ValidationSamplesTitle.Visibility = Visibility.Collapsed;
            ValidationSamplesPane.Padding = new Thickness(0);
            ValidationSamplesPane.BorderThickness = new Thickness(0);
            ValidationRecordPane.Visibility = ValidationSection == ValidationWorkspaceSection.Failures ? Visibility.Visible : Visibility.Collapsed;
            ValidationSamplesColumn.Width = new GridLength(1, GridUnitType.Star);
            ValidationPrimaryGapColumn.Width = new GridLength(0);
            ValidationRecordColumn.Width = new GridLength(0);
            Grid.SetColumn(ValidationSamplesPane, 0);
            Grid.SetColumnSpan(ValidationSamplesPane, 3);
            Grid.SetColumn(ValidationRecordPane, 0);
            Grid.SetColumnSpan(ValidationRecordPane, 3);
            return;
        }

        ValidationSamplesTitle.Visibility = Visibility.Visible;
        ValidationSamplesPane.Padding = new Thickness(6);
        ValidationSamplesPane.BorderThickness = new Thickness(1);
        var samplesOnly = ValidationSection == ValidationWorkspaceSection.Samples;
        var compactSinglePane = ActualWidth > 0 && ActualWidth < 560;
        var showSamples = samplesOnly
            || ValidationSection == ValidationWorkspaceSection.Results
            || !compactSinglePane;
        var showRecord = !samplesOnly
            && (ValidationSection == ValidationWorkspaceSection.Failures
                || !compactSinglePane);

        ValidationSamplesPane.Visibility = showSamples
            ? Visibility.Visible
            : Visibility.Collapsed;
        ValidationRecordPane.Visibility = showRecord
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (!showSamples || !showRecord)
        {
            ValidationSamplesColumn.Width = new GridLength(1, GridUnitType.Star);
            ValidationPrimaryGapColumn.Width = new GridLength(0);
            ValidationRecordColumn.Width = new GridLength(0);
            Grid.SetColumn(ValidationSamplesPane, 0);
            Grid.SetColumnSpan(ValidationSamplesPane, 3);
            Grid.SetColumn(ValidationRecordPane, 0);
            Grid.SetColumnSpan(ValidationRecordPane, 3);
            return;
        }

        ValidationSamplesColumn.Width = new GridLength(1.35, GridUnitType.Star);
        ValidationPrimaryGapColumn.Width = new GridLength(8);
        ValidationRecordColumn.Width = new GridLength(1.15, GridUnitType.Star);
        Grid.SetColumn(ValidationSamplesPane, 0);
        Grid.SetColumnSpan(ValidationSamplesPane, 1);
        Grid.SetColumn(ValidationRecordPane, 2);
        Grid.SetColumnSpan(ValidationRecordPane, 1);
    }

}

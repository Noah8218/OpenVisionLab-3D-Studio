using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public enum RecipeReviewPresentationMode
{
    Standard,
    Validation,
    Results
}

public enum ValidationWorkspaceSection
{
    Samples,
    Results,
    Failures,
    Thresholds,
    HeldOut
}

public partial class RecipePipelineReviewView : UserControl
{
    private readonly ControlTemplate standardReviewTabsTemplate;
    private RecipeReviewPresentationMode presentationMode;

    public event EventHandler? ActiveReviewChanged;

    public static readonly DependencyProperty RunRecordStepsProperty = DependencyProperty.Register(
        nameof(RunRecordSteps),
        typeof(IEnumerable),
        typeof(RecipePipelineReviewView),
        new PropertyMetadata(null));

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
        ReviewTabs.SelectionChanged += ReviewTabs_SelectionChanged;
        SizeChanged += RecipePipelineReviewView_SizeChanged;
    }

    public RecipeReviewPresentationMode PresentationMode => presentationMode;

    public bool IsDedicatedValidationWorkspace =>
        presentationMode == RecipeReviewPresentationMode.Validation
        && ValidationWorkspaceNavigation.Visibility == Visibility.Visible
        && ReviewTabs.SelectedIndex == 4;

    public ValidationWorkspaceSection ValidationSection { get; private set; } =
        ValidationWorkspaceSection.Samples;

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
            or RecipeReviewPresentationMode.Results
            ? (ControlTemplate)FindResource("ContentOnlyTabControlTemplate")
            : standardReviewTabsTemplate;

        switch (mode)
        {
            case RecipeReviewPresentationMode.Validation:
                SelectReviewTab(4);
                SetValidationSection(ValidationWorkspaceSection.Samples);
                break;
            case RecipeReviewPresentationMode.Results:
                RestoreStandardValidationLayout();
                SelectReviewTab(3);
                break;
            default:
                RestoreStandardValidationLayout();
                break;
        }
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
        && RunRecordOpenRecentButton is not null;

    public void ActivateValidationSet() => SelectReviewTab(4);

    public bool IsValidationSetSelected => ReviewTabs.SelectedIndex == 4;

    private void ReviewTabs_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.OriginalSource, ReviewTabs))
        {
            ActiveReviewChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SelectReviewTab(int index)
    {
        if (DataContext is ToolWorkbenchViewModel workbench)
        {
            workbench.SelectedReviewTabIndex = index;
            return;
        }

        ReviewTabs.SelectedIndex = index;
    }

    private static bool HasAccessibleText(ContentControl control) =>
        !string.IsNullOrWhiteSpace(control.Content?.ToString())
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control));

    private void ValidationSetStepsList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is ListBox { SelectedItem: { } selected } list)
        {
            list.ScrollIntoView(selected);
        }
    }

    private void ValidationSamplesNavigation_Click(object sender, RoutedEventArgs e) =>
        SetValidationSection(ValidationWorkspaceSection.Samples);

    private void ValidationResultsNavigation_Click(object sender, RoutedEventArgs e) =>
        SetValidationSection(ValidationWorkspaceSection.Results);

    private void ValidationFailuresNavigation_Click(object sender, RoutedEventArgs e) =>
        SetValidationSection(ValidationWorkspaceSection.Failures);

    private void ValidationThresholdNavigation_Click(object sender, RoutedEventArgs e) =>
        SetValidationSection(ValidationWorkspaceSection.Thresholds);

    private void ValidationHeldOutNavigation_Click(object sender, RoutedEventArgs e) =>
        SetValidationSection(ValidationWorkspaceSection.HeldOut);

    public void SetValidationSection(ValidationWorkspaceSection section)
    {
        ValidationSection = section;
        ValidationSamplesNavigation.IsChecked = section == ValidationWorkspaceSection.Samples;
        ValidationResultsNavigation.IsChecked = section == ValidationWorkspaceSection.Results;
        ValidationFailuresNavigation.IsChecked = section == ValidationWorkspaceSection.Failures;
        ValidationThresholdNavigation.IsChecked = section == ValidationWorkspaceSection.Thresholds;
        ValidationHeldOutNavigation.IsChecked = section == ValidationWorkspaceSection.HeldOut;

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

        if (samplesOnly
            && DataContext is ToolWorkbenchViewModel samplesWorkbench
            && samplesWorkbench.ValidationSetFilter != ValidationSetStatusFilter.All)
        {
            samplesWorkbench.SetValidationSetFilterCommand.Execute("All");
        }

        ApplyThresholdSection(section);

        if (section == ValidationWorkspaceSection.Failures
            && DataContext is ToolWorkbenchViewModel workbench
            && workbench.SelectedValidationSetSample?.Status is not ("Fail" or "Error"))
        {
            workbench.SelectedValidationSetSample =
                workbench.ValidationSetSamples.FirstOrDefault(
                    sample => sample.Status is "Fail" or "Error");
        }
    }

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
        SizeChangedEventArgs e) => ApplyValidationPrimaryLayout();

    private void ApplyValidationPrimaryLayout()
    {
        if (presentationMode != RecipeReviewPresentationMode.Validation)
        {
            return;
        }

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

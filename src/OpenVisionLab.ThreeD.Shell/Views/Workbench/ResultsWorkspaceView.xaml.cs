using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public enum ResultsWorkspaceSection
{
    RunRecord,
    OutputCompare,
    Reports
}

public partial class ResultsWorkspaceView : UserControl
{
    public ResultsWorkspaceView()
    {
        InitializeComponent();
        RunRecordReview.SetPresentationMode(RecipeReviewPresentationMode.Results);
        Loaded += (_, _) => RunRecordReview.SetPresentationMode(RecipeReviewPresentationMode.Results);
        SetSection(ResultsWorkspaceSection.RunRecord);
    }

    public ResultsWorkspaceSection ActiveSection { get; private set; }

    public bool IsReadOnlyComposition =>
        RunRecordReview.PresentationMode == RecipeReviewPresentationMode.Results
        && RunRecordReview.IsRunRecordSelected;

    public bool HasRunRecordHistoryControls =>
        RunRecordReview.HasRunRecordHistoryControls;

    public bool HasLocalizedNavigationAndAdvancedRoute =>
        HasAccessibleText(RunRecordNavigation)
        && HasAccessibleText(OutputCompareNavigation)
        && HasAccessibleText(ReportsNavigation)
        && HasAccessibleText(AdvancedDiagnosticsButton)
        && AdvancedDiagnosticsButton.Command?.CanExecute(
            AdvancedDiagnosticsButton.CommandParameter) == true;

    public bool HasOperatorSummaryAndCorrectionRoute =>
        OperatorResultSummary.Visibility == Visibility.Visible
        && !string.IsNullOrWhiteSpace(OperatorDecisionValue.Text)
        && !string.IsNullOrWhiteSpace(OperatorAffectedStepsValue.Text)
        && ResultsFixInTeachButton.IsTabStop
        && ResultsFixInTeachButton.Focusable
        && string.Equals(
            AutomationProperties.GetAutomationId(ResultsFixInTeachButton),
            "ResultsFixInTeach",
            StringComparison.Ordinal)
        && HasAccessibleText(ResultsFixInTeachButton);

    public void SetSection(ResultsWorkspaceSection section)
    {
        ActiveSection = section;
        RunRecordNavigation.IsChecked = section == ResultsWorkspaceSection.RunRecord;
        OutputCompareNavigation.IsChecked = section == ResultsWorkspaceSection.OutputCompare;
        ReportsNavigation.IsChecked = section == ResultsWorkspaceSection.Reports;
        RunRecordReview.Visibility =
            section == ResultsWorkspaceSection.RunRecord ? Visibility.Visible : Visibility.Collapsed;
        OutputCompareWorkspace.Visibility =
            section == ResultsWorkspaceSection.OutputCompare ? Visibility.Visible : Visibility.Collapsed;
        ReportsWorkspace.Visibility =
            section == ResultsWorkspaceSection.Reports ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RunRecordNavigation_Click(object sender, RoutedEventArgs args) =>
        SetSection(ResultsWorkspaceSection.RunRecord);

    private void OutputCompareNavigation_Click(object sender, RoutedEventArgs args) =>
        SetSection(ResultsWorkspaceSection.OutputCompare);

    private void ReportsNavigation_Click(object sender, RoutedEventArgs args) =>
        SetSection(ResultsWorkspaceSection.Reports);

    private static bool HasAccessibleText(ContentControl control) =>
        !string.IsNullOrWhiteSpace(control.Content?.ToString())
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control));
}

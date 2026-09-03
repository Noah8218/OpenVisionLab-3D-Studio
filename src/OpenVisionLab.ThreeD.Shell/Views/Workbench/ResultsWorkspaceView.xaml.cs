using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ResultsWorkspaceView : UserControl, IDisposable
{
    private int disposalState;

    public ResultsWorkspaceView()
    {
        InitializeComponent();
        RunRecordReview.SetPresentationMode(RecipeReviewPresentationMode.Results);
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Releases the Results-owned compare surface at the explicit Shell close
    /// boundary. Results navigation remains reusable across reversible unloads.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        Loaded -= OnLoaded;
        OutputCompareWorkspace.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (Volatile.Read(ref disposalState) == 0)
        {
            RunRecordReview.SetPresentationMode(RecipeReviewPresentationMode.Results);
        }
    }

    public ResultsWorkspaceSection ActiveSection =>
        (DataContext as ShellMainWindowViewModel)?.ResultsWorkspace.ActiveSection
        ?? ResultsWorkspaceSection.RunRecord;

    public bool IsReadOnlyComposition =>
        RunRecordReview.PresentationMode == RecipeReviewPresentationMode.Results
        && RunRecordReview.IsRunRecordSelected;

    public bool HasRunRecordHistoryControls =>
        RunRecordReview.HasRunRecordHistoryControls;

    public bool HasPrivacySafeSupportBundleControls =>
        RunRecordReview.HasPrivacySafeSupportBundleControls;

    public bool HasLocalizedNavigationAndAdvancedRoute =>
        HasAccessibleText(RunRecordNavigation)
        && HasAccessibleText(OutputCompareNavigation)
        && HasAccessibleText(ReportsNavigation)
        && HasAccessibleIconControl(AdvancedDiagnosticsButton)
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

    public void SetSection(ResultsWorkspaceSection section) =>
        (DataContext as ShellMainWindowViewModel)?.ResultsWorkspace.SelectSection(section);

    private static bool HasAccessibleText(ContentControl control) =>
        !string.IsNullOrWhiteSpace(control.Content?.ToString())
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control));

    private static bool HasAccessibleIconControl(FrameworkElement control) =>
        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))
        && control.ReadLocalValue(ToolTipProperty)
            != DependencyProperty.UnsetValue;
}

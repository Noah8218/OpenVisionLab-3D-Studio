using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ResultsWorkspaceView : UserControl, IDisposable
{
    private int disposalState;

    public static readonly DependencyProperty ActiveSectionProperty =
        DependencyProperty.Register(
            nameof(ActiveSection),
            typeof(ResultsWorkspaceSection),
            typeof(ResultsWorkspaceView),
            new PropertyMetadata(ResultsWorkspaceSection.RunRecord));

    public static readonly DependencyProperty SelectSectionCommandProperty =
        DependencyProperty.Register(
            nameof(SelectSectionCommand),
            typeof(ICommand),
            typeof(ResultsWorkspaceView),
            new PropertyMetadata(null));

    public ResultsWorkspaceView()
    {
        InitializeComponent();
        SetBinding(
            ActiveSectionProperty,
            new Binding("ResultsWorkspace.ActiveSection") { Mode = BindingMode.OneWay });
        SetBinding(
            SelectSectionCommandProperty,
            new Binding("ResultsWorkspace.SelectSectionCommand"));
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

    public ResultsWorkspaceSection ActiveSection
    {
        get => (ResultsWorkspaceSection)GetValue(ActiveSectionProperty);
        set => SetValue(ActiveSectionProperty, value);
    }

    public ICommand? SelectSectionCommand
    {
        get => (ICommand?)GetValue(SelectSectionCommandProperty);
        set => SetValue(SelectSectionCommandProperty, value);
    }

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

    public void SetSection(ResultsWorkspaceSection section)
    {
        var command = SelectSectionCommand;
        if (command?.CanExecute(section) == true)
        {
            command.Execute(section);
        }
    }

    private static bool HasAccessibleText(ContentControl control) =>
        !string.IsNullOrWhiteSpace(control.Content?.ToString())
        && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control));

    private static bool HasAccessibleIconControl(FrameworkElement control) =>
        !string.IsNullOrWhiteSpace(AutomationProperties.GetName(control))
        && control.ReadLocalValue(ToolTipProperty)
            != DependencyProperty.UnsetValue;
}

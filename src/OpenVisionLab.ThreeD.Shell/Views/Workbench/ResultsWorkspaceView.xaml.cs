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

    public static readonly DependencyProperty IsCompactLayoutProperty =
        DependencyProperty.Register(
            nameof(IsCompactLayout),
            typeof(bool),
            typeof(ResultsWorkspaceView),
            new PropertyMetadata(false));

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
        SizeChanged += OnSizeChanged;
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
        SizeChanged -= OnSizeChanged;
        OutputCompareWorkspace.Dispose();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (Volatile.Read(ref disposalState) == 0)
        {
            RunRecordReview.SetPresentationMode(RecipeReviewPresentationMode.Results);
            ApplyResponsiveLayout(ActualHeight);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs args) =>
        ApplyResponsiveLayout(args.NewSize.Height);

    internal void ApplyResponsiveLayout(double availableHeight)
    {
        var compact = availableHeight > 0 && availableHeight < 720;
        IsCompactLayout = compact;
        ResultsWorkspaceHeader.Visibility = Visibility.Visible;
        ResultsWorkspaceHeader.Padding = compact ? new Thickness(10, 4, 10, 4) : new Thickness(16, 10, 16, 10);
        ResultsWorkspaceDetail.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        AdvancedDiagnosticsButton.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ResultsSectionHost.RowDefinitions[0].Height = compact ? new GridLength(1, GridUnitType.Star) : GridLength.Auto;
        ResultsSectionHost.RowDefinitions[1].Height = compact ? GridLength.Auto : new GridLength(1, GridUnitType.Star);
        Grid.SetRow(ResultsSectionNavigation, compact ? 1 : 0);
        Grid.SetRow(ResultsWorkspaceContent, compact ? 0 : 1);
        ResultsSectionNavigation.Padding = compact ? new Thickness(8, 2, 8, 2) : new Thickness(10);
        ResultsSectionNavigation.BorderThickness = compact ? new Thickness(0, 1, 0, 0) : new Thickness(0, 0, 1, 0);
        RunRecordNavigation.Margin = compact ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 12, 6);
        OutputCompareNavigation.Margin = compact ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 12, 6);
        ReportsNavigation.Margin = compact ? new Thickness(0) : new Thickness(0, 0, 0, 6);
        RunRecordNavigation.Padding = compact ? new Thickness(8, 3, 8, 3) : new Thickness(10, 8, 10, 8);
        OutputCompareNavigation.Padding = compact ? new Thickness(8, 3, 8, 3) : new Thickness(10, 8, 10, 8);
        ReportsNavigation.Padding = compact ? new Thickness(8, 3, 8, 3) : new Thickness(10, 8, 10, 8);
        ResultsImmutableNotice.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        RunRecordReview.Margin = compact ? new Thickness(8, 2, 8, 2) : new Thickness(12, 4, 12, 12);
        RunRecordReview.ApplyResponsiveLayout(availableHeight);
    }

    public ResultsWorkspaceSection ActiveSection
    {
        get => (ResultsWorkspaceSection)GetValue(ActiveSectionProperty);
        set => SetValue(ActiveSectionProperty, value);
    }

    public bool IsCompactLayout
    {
        get => (bool)GetValue(IsCompactLayoutProperty);
        private set => SetValue(IsCompactLayoutProperty, value);
    }

    public ICommand? SelectSectionCommand
    {
        get => (ICommand?)GetValue(SelectSectionCommandProperty);
        set => SetValue(SelectSectionCommandProperty, value);
    }

    public bool IsReadOnlyComposition =>
        RunRecordReview.PresentationMode == RecipeReviewPresentationMode.Results
        && RunRecordReview.IsRunRecordSelected
        && RunRecordReview.Visibility == Visibility.Visible
        && OutputCompareWorkspace.Visibility == Visibility.Collapsed
        && ReportsWorkspace.Visibility == Visibility.Collapsed;

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

    public bool HasResultsValueValiditySurface =>
        ResultsValueValidityPanel.Visibility == Visibility.Visible
        && !string.IsNullOrWhiteSpace(
            AutomationProperties.GetAutomationId(ResultsValueValidityPanel))
        && !string.IsNullOrWhiteSpace(
            AutomationProperties.GetAutomationId(ResultsValueValidityValue));

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

using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
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

    public static readonly DependencyProperty IsDedicatedResultsLayoutProperty =
        DependencyProperty.Register(nameof(IsDedicatedResultsLayout), typeof(bool), typeof(ResultsWorkspaceView), new PropertyMetadata(false));

    public static readonly DependencyProperty IsAuthoringLayoutProperty =
        DependencyProperty.Register(nameof(IsAuthoringLayout), typeof(bool), typeof(ResultsWorkspaceView), new PropertyMetadata(false));

    public static readonly DependencyProperty DockPaneCommandsProperty = DependencyProperty.Register(
        nameof(DockPaneCommands), typeof(object), typeof(ResultsWorkspaceView), new PropertyMetadata(null));

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

        TeachingDetailsButton.IsChecked = false;
        Loaded -= OnLoaded;
        SizeChanged -= OnSizeChanged;
        OutputCompareWorkspace.Dispose();
        DrawerHeightProfile.Dispose();
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
        var dedicated = IsDedicatedResultsLayout;
        ResultsHistoryColumn.Width = new GridLength(dedicated ? 280 : 0);
        ResultsHistoryPanel.Visibility = dedicated ? Visibility.Visible : Visibility.Collapsed;
        IsCompactLayout = compact;
        ResultsWorkspaceHeader.Visibility = dedicated ? Visibility.Visible : Visibility.Collapsed;
        ResultsWorkspaceHeader.Padding = new Thickness(10, 4, 10, 4);
        AdvancedDiagnosticsButton.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        ResultsSectionHost.RowDefinitions[0].Height = GridLength.Auto;
        ResultsSectionHost.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
        Grid.SetRow(ResultsSectionNavigation, 0);
        Grid.SetRow(ResultsWorkspaceContent, 1);
        ResultsSectionNavigation.Padding = new Thickness(6, 0, IsAuthoringLayout ? 4 : 6, 0);
        ResultsSectionNavigation.BorderThickness = new Thickness(0, 0, 0, 1);
        RunRecordNavigation.Margin = OutputCompareNavigation.Margin = ReportsNavigation.Margin = new Thickness(0);
        RunRecordNavigation.Padding = OutputCompareNavigation.Padding = ReportsNavigation.Padding = new Thickness(0);
        ResultsImmutableNotice.Visibility = Visibility.Collapsed;
        RunRecordReview.Margin = new Thickness(8, 2, 8, 2);
        RunRecordReview.ApplyResponsiveLayout(availableHeight);
    }

    private void OnTeachingDetailKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape) return;
        TeachingDetailsButton.IsChecked = false;
        args.Handled = true;
    }

    private void OnSummaryDetailsLayoutChanged(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement content) return;
        DependencyObject? parent = VisualTreeHelper.GetParent(content);
        while (parent is not null && parent is not ScrollViewer) parent = VisualTreeHelper.GetParent(parent);
        if (parent is not ScrollViewer { ViewportWidth: > 0 } viewport) return;
        var inset = Math.Max(0, content.TransformToAncestor(viewport).Transform(new Point()).X + viewport.HorizontalOffset);
        var available = Math.Max(0, viewport.ViewportWidth - inset);
        if (Math.Abs(content.MaxWidth - available) > .5) content.MaxWidth = available;
    }

    public object? DockPaneCommands
    {
        get => GetValue(DockPaneCommandsProperty);
        set => SetValue(DockPaneCommandsProperty, value);
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

    public bool IsDedicatedResultsLayout
    {
        get => (bool)GetValue(IsDedicatedResultsLayoutProperty);
        set => SetValue(IsDedicatedResultsLayoutProperty, value);
    }

    public bool IsAuthoringLayout
    {
        get => (bool)GetValue(IsAuthoringLayoutProperty);
        set => SetValue(IsAuthoringLayoutProperty, value);
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
        IsDedicatedResultsLayout
        ? HistoryDashboard.HasOperatorSummaryAndCorrectionRoute
        : OperatorResultSummary.Visibility == Visibility.Visible
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

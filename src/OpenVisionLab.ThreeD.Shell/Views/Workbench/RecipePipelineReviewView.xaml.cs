using System.Collections;
using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class RecipePipelineReviewView : UserControl
{
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
        ReviewTabs.SelectionChanged += ReviewTabs_SelectionChanged;
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

    private void ValidationSetStepsList_SelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is ListBox { SelectedItem: { } selected } list)
        {
            list.ScrollIntoView(selected);
        }
    }

}

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class RecipePipelineReviewView : UserControl
{
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
    }

    public void ActivateFlowMap() => ReviewTabs.SelectedIndex = 1;

    public bool IsFlowMapSelected => ReviewTabs.SelectedIndex == 1;

    public void ActivateProblems() => ReviewTabs.SelectedIndex = 2;

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

    public void ActivateRunRecord() => ReviewTabs.SelectedIndex = 3;

    public bool IsRunRecordSelected => ReviewTabs.SelectedIndex == 3;

    public bool HasRunRecordHistoryControls =>
        RunRecordOpenButton is not null
        && RunRecordOpenJsonButton is not null
        && RunRecordExportButton is not null
        && RecentRunRecordCombo is not null
        && RunRecordOpenRecentButton is not null;

    public void ActivateValidationSet() => ReviewTabs.SelectedIndex = 4;

    public bool IsValidationSetSelected => ReviewTabs.SelectedIndex == 4;

    private void SelectValidationSetSources_Click(object sender, RoutedEventArgs args)
    {
        if (DataContext is not ToolWorkbenchViewModel workbench)
        {
            return;
        }

        var english = OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English;
        var dialog = new OpenFileDialog
        {
            Title = english ? "Add validation C3D samples" : "반복 검증 C3D 샘플 추가",
            Filter = english
                ? "C3D height maps (*.c3d)|*.c3d|All files (*.*)|*.*"
                : "C3D 높이 맵 (*.c3d)|*.c3d|모든 파일 (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            workbench.SetValidationSetSources(dialog.FileNames);
        }
    }
}

using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class SelectedToolWorkspaceView : UserControl
{
    public SelectedToolWorkspaceView()
    {
        InitializeComponent();
    }

    public bool IsFailureCorrectionContextVisible =>
        TeachFailureCorrectionContext.Visibility == Visibility.Visible;

    public bool CommitPendingParameterEdit(out string message) =>
        StepPropertyGrid.CommitPendingEdit(out message);

    public void BringOutputIntoView() => OutputSection.BringIntoView();

    public bool HasThicknessRepeatGridAuthoringControls =>
        ThicknessRepeatGridPanel is not null;

    public void BringThicknessRepeatGridIntoView() =>
        ThicknessRepeatGridPanel.BringIntoView();

    private void OnApplyParametersClick(object sender, RoutedEventArgs args)
    {
        if (DataContext is not ToolWorkbenchViewModel viewModel)
        {
            return;
        }

        if (!StepPropertyGrid.CommitPendingEdit(out var message))
        {
            viewModel.ReportParameterDraftCommitError(message);
            return;
        }

        if (viewModel.ApplySelectedStepParameterDraftCommand.CanExecute(null))
        {
            viewModel.ApplySelectedStepParameterDraftCommand.Execute(null);
        }
    }
}

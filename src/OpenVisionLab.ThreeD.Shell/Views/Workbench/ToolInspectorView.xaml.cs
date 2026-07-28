using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ToolInspectorView : UserControl
{
    public ToolInspectorView()
    {
        InitializeComponent();
    }

    public bool CommitPendingParameterEdit(out string message) =>
        StepPropertyGrid.CommitPendingEdit(out message);

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

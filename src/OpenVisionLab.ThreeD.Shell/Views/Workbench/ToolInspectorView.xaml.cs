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

    private void OnPropertyGridValueChanged(object sender, EventArgs args)
    {
        if (DataContext is ToolWorkbenchViewModel viewModel)
        {
            viewModel.MarkSelectedStepParameterDraftDirty();
        }
    }

    private void OnDiscardParametersClick(object sender, RoutedEventArgs args)
    {
        if (DataContext is ToolWorkbenchViewModel viewModel)
        {
            viewModel.DiscardSelectedStepParameterDraft();
        }
    }

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

        viewModel.TryApplySelectedStepParameterDraft(out _);
    }
}

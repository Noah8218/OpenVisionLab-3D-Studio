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

}

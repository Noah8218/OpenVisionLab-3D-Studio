using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class XYZAffineSolveToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };

    public XYZAffineSolveToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "xyz-affine-solve", "XYZ Affine Solve Tool Lab requires an XYZ Affine Solve step.")
    {
        InitializeComponent();
        DataContext = Workbench;
        SourceViewerHost.Content = sourceViewer;
        OwnViewer(sourceViewer);
    }

    public override void RefreshViews()
    {
        if (!HasSourcePath(Workbench.Source.Path)) return;
        sourceViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | XYZ Affine Solve matrix evidence; no transformed surface");
    }

    private void RefreshViewsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();

    private void OnApplyParametersClick(object sender, RoutedEventArgs args)
    {
        if (!AffineStepPropertyGrid.CommitPendingEdit(out var message))
        {
            Workbench.ReportParameterDraftCommitError(message);
            return;
        }
        if (Workbench.ApplySelectedStepParameterDraftCommand.CanExecute(null))
        {
            Workbench.ApplySelectedStepParameterDraftCommand.Execute(null);
        }
    }
}

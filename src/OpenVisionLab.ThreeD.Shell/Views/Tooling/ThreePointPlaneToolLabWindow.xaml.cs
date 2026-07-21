using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class ThreePointPlaneToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public ThreePointPlaneToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        InitializeComponent();
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        DataContext = workbench;
        SetLabStep(step);
    }

    public void SetLabStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!string.Equals(step.ToolId, "three-point-plane", StringComparison.Ordinal)) throw new ArgumentException("Expected a 3-Point Plane step.", nameof(step));
        labStepId = step.Id;
        if (!ReferenceEquals(workbench.SelectedPipelineStep, step)) workbench.SelectedPipelineStep = step;
        RefreshViews();
    }

    public void RefreshViews()
    {
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | recipe-owned ordered grid-cell picks");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | 3-Point Plane is datum evidence, not a transformed surface");
        if (workbench.CurrentThreePointPlaneOutput is { } output && string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.OrdinalIgnoreCase))
        {
            inputViewer.ShowWorkbenchThreePointPlane(output, workbench.IsThreePointPlanePreviewPublished);
            outputViewer.ShowWorkbenchThreePointPlane(output, workbench.IsThreePointPlanePreviewPublished);
        }
    }

    public void ShowThreePointPlaneResult(ToolWorkbenchThreePointPlaneDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | recipe-owned ordered grid-cell picks");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | 3-Point Plane support triangle and normal evidence");
        inputViewer.ShowWorkbenchThreePointPlane(args.Output, args.IsPublished);
        outputViewer.ShowWorkbenchThreePointPlane(args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

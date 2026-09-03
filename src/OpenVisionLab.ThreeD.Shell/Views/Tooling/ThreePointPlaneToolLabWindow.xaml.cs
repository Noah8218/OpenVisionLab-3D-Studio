using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class ThreePointPlaneToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public ThreePointPlaneToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "three-point-plane", "Expected a 3-Point Plane step.", activateOnActivated: false)
    {
        InitializeComponent();
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        OwnViewer(inputViewer);
        OwnViewer(outputViewer);
        DataContext = Workbench;
    }

    public override void RefreshViews()
    {
        if (!HasSourcePath(Workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | recipe-owned ordered grid-cell picks");
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | 3-Point Plane is datum evidence, not a transformed surface");
        if (Workbench.CurrentThreePointPlaneOutput is { } output && IsActiveStep)
        {
            inputViewer.ShowWorkbenchThreePointPlane(output, Workbench.IsThreePointPlanePreviewPublished);
            outputViewer.ShowWorkbenchThreePointPlane(output, Workbench.IsThreePointPlanePreviewPublished);
        }
    }

    public void ShowThreePointPlaneResult(ToolWorkbenchThreePointPlaneDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!HasSourcePath(Workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | recipe-owned ordered grid-cell picks");
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | 3-Point Plane support triangle and normal evidence");
        inputViewer.ShowWorkbenchThreePointPlane(args.Output, args.IsPublished);
        outputViewer.ShowWorkbenchThreePointPlane(args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

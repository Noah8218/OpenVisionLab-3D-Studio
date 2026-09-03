using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class HeightDifferenceEdgeToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public HeightDifferenceEdgeToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "height-difference-edge", "Edge Tool Lab requires a Height Difference Edge step.")
    {
        ArgumentNullException.ThrowIfNull(workbench);
        InitializeComponent();
        DataContext = Workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        OwnViewer(inputViewer);
        OwnViewer(outputViewer);
    }

    public override void RefreshViews()
    {
        ActivateLabStep();
        if (!Workbench.IsFilterPreviewPublished
            || string.IsNullOrWhiteSpace(Workbench.CurrentFilterPreviewPath)
            || !File.Exists(Workbench.CurrentFilterPreviewPath))
        {
            return;
        }

        var inputPath = Workbench.CurrentFilterPreviewPath;
        inputViewer.ShowC3DWorkbenchResult(inputPath, Workbench.CurrentFilterPreviewOutputSummary);
        outputViewer.ShowC3DWorkbenchResult(inputPath, Workbench.CurrentFilterPreviewOutputSummary);
        if (Workbench.CurrentHeightDifferenceEdgeOutput is { } output)
        {
            outputViewer.ShowWorkbenchHeightDifferenceEdge(output, Workbench.IsEdgePreviewPublished);
        }
    }

    public void ShowEdgeResult(ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        inputViewer.ShowC3DWorkbenchResult(args.C3DPath, Workbench.CurrentFilterPreviewOutputSummary);
        outputViewer.ShowC3DWorkbenchResult(args.C3DPath, Workbench.CurrentFilterPreviewOutputSummary);
        outputViewer.ShowWorkbenchHeightDifferenceEdge(args.Output, args.IsPublished);
    }

    private void ShowFilterInputButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

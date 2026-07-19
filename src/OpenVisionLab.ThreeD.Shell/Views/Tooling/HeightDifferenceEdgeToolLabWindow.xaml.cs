using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class HeightDifferenceEdgeToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public HeightDifferenceEdgeToolLabWindow(ToolWorkbenchViewModel workbench)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        InitializeComponent();
        DataContext = workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        Loaded += (_, _) => RefreshViews();
    }

    public void RefreshViews()
    {
        if (!workbench.IsFilterPreviewPublished
            || string.IsNullOrWhiteSpace(workbench.CurrentFilterPreviewPath)
            || !File.Exists(workbench.CurrentFilterPreviewPath))
        {
            return;
        }

        var inputPath = workbench.CurrentFilterPreviewPath;
        inputViewer.ShowC3DWorkbenchResult(inputPath, workbench.CurrentFilterPreviewOutputSummary);
        outputViewer.ShowC3DWorkbenchResult(inputPath, workbench.CurrentFilterPreviewOutputSummary);
        if (workbench.CurrentHeightDifferenceEdgeOutput is { } output)
        {
            outputViewer.ShowWorkbenchHeightDifferenceEdge(output, workbench.IsEdgePreviewPublished);
        }
    }

    public void ShowEdgeResult(ToolWorkbenchHeightDifferenceEdgeDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        inputViewer.ShowC3DWorkbenchResult(args.C3DPath, workbench.CurrentFilterPreviewOutputSummary);
        outputViewer.ShowC3DWorkbenchResult(args.C3DPath, workbench.CurrentFilterPreviewOutputSummary);
        outputViewer.ShowWorkbenchHeightDifferenceEdge(args.Output, args.IsPublished);
    }

    private void ShowFilterInputButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

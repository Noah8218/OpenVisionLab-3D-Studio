using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class HeightDifferenceEdgeToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public HeightDifferenceEdgeToolLabWindow(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        Func<string, bool> selectPipelineStep)
        : base(workbench, step, selectPipelineStep, "height-difference-edge", "Edge Tool Lab requires a Height Difference Edge step.")
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
            inputViewer.ClearC3DTeachingSource(Workbench.Localization.Resolve(
                "ThreeD.ToolLab.Edge.NoInput", "적용된 필터 입력 없음", "No applied filter input"));
            outputViewer.ClearC3DTeachingSource(Workbench.Localization.Resolve(
                "ThreeD.ToolLab.Edge.NoOutput", "결과 없음", "No result"));
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

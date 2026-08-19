using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class LineIntersectionToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public LineIntersectionToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "line-intersection", "Line Intersection Tool Lab requires a Line Intersection step.")
    {
        InitializeComponent();
        DataContext = Workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
    }

    public override void RefreshViews()
    {
        if (!HasSourcePath(Workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | exact root frame for both published LineFeatures");
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | intersection evidence is an overlay, not a new surface");
        if (Workbench.TryGetCurrentLineIntersectionInputs(out var first, out var second) && first is not null && second is not null)
        {
            inputViewer.ShowWorkbenchLineIntersectionInputs(first, second);
            outputViewer.ShowWorkbenchLineIntersectionInputs(first, second);
            if (Workbench.CurrentLineIntersectionOutput is { } output)
            {
                outputViewer.ShowWorkbenchLineIntersection(first, second, output, Workbench.IsLineIntersectionPreviewPublished);
            }
        }
    }

    public void ShowLineIntersectionResult(ToolWorkbenchLineIntersectionDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!HasSourcePath(Workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | exact root frame for both published LineFeatures");
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | intersection evidence is an overlay, not a new surface");
        inputViewer.ShowWorkbenchLineIntersection(args.FirstLine, args.SecondLine, args.Output, args.IsPublished);
        outputViewer.ShowWorkbenchLineIntersection(args.FirstLine, args.SecondLine, args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

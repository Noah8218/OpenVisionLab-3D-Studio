using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class TwoPointLineToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public TwoPointLineToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "two-point-line", "Expected a 2-Point Line step.", activateOnActivated: false)
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
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | 2-Point Line is overlay evidence, not a new surface");
        if (Workbench.CurrentTwoPointLineOutput is { } output && IsActiveStep)
        {
            inputViewer.ShowWorkbenchTwoPointLine(output, Workbench.IsTwoPointLinePreviewPublished);
            outputViewer.ShowWorkbenchTwoPointLine(output, Workbench.IsTwoPointLinePreviewPublished);
        }
    }

    public void ShowTwoPointLineResult(ToolWorkbenchTwoPointLineDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!HasSourcePath(Workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | recipe-owned ordered grid-cell picks");
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | 2-Point Line overlay evidence");
        inputViewer.ShowWorkbenchTwoPointLine(args.Output, args.IsPublished);
        outputViewer.ShowWorkbenchTwoPointLine(args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

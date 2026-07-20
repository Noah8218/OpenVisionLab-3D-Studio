using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class LineIntersectionToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public LineIntersectionToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        SetLabStep(step);
        InitializeComponent();
        DataContext = workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        Loaded += (_, _) => RefreshViews();
        Activated += (_, _) => ActivateLabStep();
    }

    public void SetLabStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!string.Equals(step.ToolId, "line-intersection", StringComparison.Ordinal))
        {
            throw new ArgumentException("Line Intersection Tool Lab requires a Line Intersection step.", nameof(step));
        }

        labStepId = step.Id;
        ActivateLabStep();
    }

    public void ActivateLabStep()
    {
        if (!string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.Ordinal))
        {
            workbench.SelectPipelineStep(labStepId);
        }
    }

    public void RefreshViews()
    {
        ActivateLabStep();
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | exact root frame for both published LineFeatures");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | intersection evidence is an overlay, not a new surface");
        if (workbench.TryGetCurrentLineIntersectionInputs(out var first, out var second) && first is not null && second is not null)
        {
            inputViewer.ShowWorkbenchLineIntersectionInputs(first, second);
            outputViewer.ShowWorkbenchLineIntersectionInputs(first, second);
            if (workbench.CurrentLineIntersectionOutput is { } output)
            {
                outputViewer.ShowWorkbenchLineIntersection(first, second, output, workbench.IsLineIntersectionPreviewPublished);
            }
        }
    }

    public void ShowLineIntersectionResult(ToolWorkbenchLineIntersectionDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | exact root frame for both published LineFeatures");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | intersection evidence is an overlay, not a new surface");
        inputViewer.ShowWorkbenchLineIntersection(args.FirstLine, args.SecondLine, args.Output, args.IsPublished);
        outputViewer.ShowWorkbenchLineIntersection(args.FirstLine, args.SecondLine, args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();

}

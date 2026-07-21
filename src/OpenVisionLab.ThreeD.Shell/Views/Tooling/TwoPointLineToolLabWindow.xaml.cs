using System.Windows;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class TwoPointLineToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public TwoPointLineToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
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
        if (!string.Equals(step.ToolId, "two-point-line", StringComparison.Ordinal)) throw new ArgumentException("Expected a 2-Point Line step.", nameof(step));
        labStepId = step.Id;
        if (!ReferenceEquals(workbench.SelectedPipelineStep, step)) workbench.SelectedPipelineStep = step;
        RefreshViews();
    }

    public void RefreshViews()
    {
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | recipe-owned ordered grid-cell picks");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | 2-Point Line is overlay evidence, not a new surface");
        if (workbench.CurrentTwoPointLineOutput is { } output && string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.OrdinalIgnoreCase))
        {
            inputViewer.ShowWorkbenchTwoPointLine(output, workbench.IsTwoPointLinePreviewPublished);
            outputViewer.ShowWorkbenchTwoPointLine(output, workbench.IsTwoPointLinePreviewPublished);
        }
    }

    public void ShowTwoPointLineResult(ToolWorkbenchTwoPointLineDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | recipe-owned ordered grid-cell picks");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | 2-Point Line overlay evidence");
        inputViewer.ShowWorkbenchTwoPointLine(args.Output, args.IsPublished);
        outputViewer.ShowWorkbenchTwoPointLine(args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

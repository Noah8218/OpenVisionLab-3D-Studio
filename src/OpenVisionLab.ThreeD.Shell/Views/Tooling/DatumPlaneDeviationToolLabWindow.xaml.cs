using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class DatumPlaneDeviationToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public DatumPlaneDeviationToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
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
        if (!string.Equals(step.ToolId, "datum-plane-raw-height-deviation", StringComparison.Ordinal)) throw new ArgumentException("Expected a Datum Plane Raw-Height Deviation step.", nameof(step));
        labStepId = step.Id;
        if (!ReferenceEquals(workbench.SelectedPipelineStep, step)) workbench.SelectedPipelineStep = step;
        RefreshViews();
    }

    public void RefreshViews()
    {
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Input raw C3D | Published datum plane and recipe-owned ROI");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Output raw C3D | read-only residual overlay; source unchanged");
        if (workbench.CurrentDatumPlaneDeviationOutput is { } output
            && string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.OrdinalIgnoreCase)
            && workbench.TryGetCurrentDatumPlaneDeviationInputs(out var plane, out var selection)
            && plane is not null && selection is not null)
        {
            inputViewer.ShowWorkbenchThreePointPlane(plane, true);
            outputViewer.ShowWorkbenchDatumPlaneDeviation(plane, selection, output, workbench.IsDatumPlaneDeviationPreviewPublished);
        }
    }

    public void ShowDatumPlaneDeviationResult(ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Input raw C3D | Published datum-plane evidence");
        outputViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Output raw C3D | read-only residual overlay; source unchanged");
        inputViewer.ShowWorkbenchThreePointPlane(args.Plane, true);
        outputViewer.ShowWorkbenchDatumPlaneDeviation(args.Plane, args.MeasurementSelection, args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

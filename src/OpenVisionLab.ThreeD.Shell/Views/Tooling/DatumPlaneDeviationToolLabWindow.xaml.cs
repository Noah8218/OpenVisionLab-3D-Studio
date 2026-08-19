using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class DatumPlaneDeviationToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public DatumPlaneDeviationToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "datum-plane-raw-height-deviation", "Expected a Datum Plane Raw-Height Deviation step.")
    {
        InitializeComponent();
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        DataContext = Workbench;
    }

    public override void RefreshViews()
    {
        if (!HasSourcePath(Workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Input raw C3D | Published datum plane and recipe-owned ROI");
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Output raw C3D | read-only residual overlay; source unchanged");
        if (Workbench.CurrentDatumPlaneDeviationOutput is { } output
            && string.Equals(Workbench.SelectedPipelineStep?.Id, LabStepId, StringComparison.OrdinalIgnoreCase)
            && Workbench.TryGetCurrentDatumPlaneDeviationInputs(out var plane, out var selection)
            && plane is not null && selection is not null)
        {
            inputViewer.ShowWorkbenchThreePointPlane(plane, true);
            outputViewer.ShowWorkbenchDatumPlaneDeviation(plane, selection, output, Workbench.IsDatumPlaneDeviationPreviewPublished);
        }
    }

    public void ShowDatumPlaneDeviationResult(ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!HasSourcePath(Workbench.Source.Path)) return;
        inputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Input raw C3D | Published datum-plane evidence");
        outputViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Output raw C3D | read-only residual overlay; source unchanged");
        inputViewer.ShowWorkbenchThreePointPlane(args.Plane, true);
        outputViewer.ShowWorkbenchDatumPlaneDeviation(args.Plane, args.MeasurementSelection, args.Output, args.IsPublished);
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

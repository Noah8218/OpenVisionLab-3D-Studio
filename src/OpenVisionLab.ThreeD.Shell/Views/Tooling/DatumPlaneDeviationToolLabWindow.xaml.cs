using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class DatumPlaneDeviationToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public DatumPlaneDeviationToolLabWindow(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        Func<string, bool> selectPipelineStep)
        : base(workbench, step, selectPipelineStep, "datum-plane-raw-height-deviation", "Expected a Datum Plane Raw-Height Deviation step.")
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
        ClearDatumPlaneDeviationResult();
        if (!HasSourcePath(Workbench.Source.Path))
        {
            var status = Workbench.Localization.Resolve("ThreeD.ToolLab.SourceFileUnavailable", "입력 파일을 찾을 수 없습니다. 경로를 확인하세요.", "Source file not found. Check the path.");
            inputViewer.ClearC3DTeachingSource(status);
            outputViewer.ClearC3DTeachingSource(status);
            return;
        }
        var sourcePath = System.IO.Path.GetFullPath(Workbench.Source.Path);
        var inputCamera = inputViewer.CaptureCameraState();
        var outputCamera = outputViewer.CaptureCameraState();
        var preserveInputCamera = string.Equals(inputViewer.CurrentC3DSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase);
        var preserveOutputCamera = string.Equals(outputViewer.CurrentC3DSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase);
        var inputLoaded = inputViewer.ShowC3DWorkbenchResult(sourcePath, "Input raw C3D | Published datum plane and recipe-owned ROI");
        var outputLoaded = outputViewer.ShowC3DWorkbenchResult(sourcePath, "Output raw C3D | read-only residual overlay; source unchanged");
        if (inputLoaded && preserveInputCamera) inputViewer.TryApplyCameraState(inputCamera);
        if (outputLoaded && preserveOutputCamera) outputViewer.TryApplyCameraState(outputCamera);
        if (!inputLoaded) inputViewer.ClearC3DTeachingSource(inputViewer.HostState.ViewerStatus);
        if (!outputLoaded) outputViewer.ClearC3DTeachingSource(outputViewer.HostState.ViewerStatus);
        if (!inputLoaded || !outputLoaded || !IsActiveStep) return;
        if (Workbench.TryGetCurrentDatumPlaneDeviationInputs(out var plane, out var selection) && plane is not null && selection is not null)
        {
            inputViewer.ShowWorkbenchThreePointPlane(plane, true);
            if (Workbench.HasCurrentDatumPlaneDeviationPreview && Workbench.CurrentDatumPlaneDeviationOutput is { } output)
                outputViewer.ShowWorkbenchDatumPlaneDeviation(plane, selection, output, Workbench.IsDatumPlaneDeviationPreviewPublished);
        }
    }

    public void ShowDatumPlaneDeviationResult(ToolWorkbenchDatumPlaneDeviationDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        RefreshViews();
    }

    public void ClearDatumPlaneDeviationResult()
    {
        inputViewer.ClearWorkbenchThreePointPlane();
        outputViewer.ClearWorkbenchDatumPlaneDeviation();
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

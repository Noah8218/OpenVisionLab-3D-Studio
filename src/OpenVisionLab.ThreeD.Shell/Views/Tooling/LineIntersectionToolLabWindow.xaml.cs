using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class LineIntersectionToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public LineIntersectionToolLabWindow(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        Func<string, bool> selectPipelineStep)
        : base(workbench, step, selectPipelineStep, "line-intersection", "Line Intersection Tool Lab requires a Line Intersection step.")
    {
        InitializeComponent();
        DataContext = Workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        OwnViewer(inputViewer);
        OwnViewer(outputViewer);
    }

    public override void RefreshViews()
    {
        ClearLineIntersectionResult();
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
        var inputLoaded = inputViewer.ShowC3DWorkbenchResult(sourcePath, "Source C3D | exact root frame for both published LineFeatures");
        var outputLoaded = outputViewer.ShowC3DWorkbenchResult(sourcePath, "Source C3D | intersection evidence is an overlay, not a new surface");
        if (inputLoaded && preserveInputCamera) inputViewer.TryApplyCameraState(inputCamera);
        if (outputLoaded && preserveOutputCamera) outputViewer.TryApplyCameraState(outputCamera);
        if (!inputLoaded) inputViewer.ClearC3DTeachingSource(inputViewer.HostState.ViewerStatus);
        if (!outputLoaded) outputViewer.ClearC3DTeachingSource(outputViewer.HostState.ViewerStatus);
        if (!inputLoaded || !outputLoaded || !IsActiveStep) return;
        if (Workbench.TryGetCurrentLineIntersectionInputs(out var first, out var second) && first is not null && second is not null)
        {
            inputViewer.ShowWorkbenchLineIntersectionInputs(first, second);
            outputViewer.ShowWorkbenchLineIntersectionInputs(first, second);
            if (Workbench.HasCurrentLineIntersectionPreview && Workbench.CurrentLineIntersectionOutput is { } output)
                outputViewer.ShowWorkbenchLineIntersection(first, second, output, Workbench.IsLineIntersectionPreviewPublished);
        }
    }

    public void ShowLineIntersectionResult(ToolWorkbenchLineIntersectionDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        RefreshViews();
    }

    public void ClearLineIntersectionResult()
    {
        inputViewer.ClearWorkbenchLineIntersection();
        outputViewer.ClearWorkbenchLineIntersection();
    }

    private void ShowInputsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

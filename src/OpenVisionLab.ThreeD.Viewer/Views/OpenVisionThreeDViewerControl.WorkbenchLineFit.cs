using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public event EventHandler<WorkbenchLineFitPointSelectedEventArgs>? WorkbenchLineFitPointSelected;

    public void ShowWorkbenchLineFit(C3DLineFeature output, bool isPublished)
    {
        viewModel.SetWorkbenchLineFit(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchLineFit()
    {
        viewModel.ClearWorkbenchLineFit();
        RenderNow();
    }

    private void RaiseWorkbenchLineFitPointSelected(C3DLineFeaturePointDiagnostic point) =>
        WorkbenchLineFitPointSelected?.Invoke(this, new WorkbenchLineFitPointSelectedEventArgs(point.InputPointIndex));
}

public sealed class WorkbenchLineFitPointSelectedEventArgs(int inputPointIndex) : EventArgs
{
    public int InputPointIndex { get; } = inputPointIndex;
}

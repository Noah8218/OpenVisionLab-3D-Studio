using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public void ShowWorkbenchHeightDifferenceEdge(C3DHeightDifferenceEdgePointSet output, bool isPublished)
    {
        viewModel.SetWorkbenchHeightDifferenceEdge(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchHeightDifferenceEdge()
    {
        viewModel.ClearWorkbenchHeightDifferenceEdge();
        RenderNow();
    }
}

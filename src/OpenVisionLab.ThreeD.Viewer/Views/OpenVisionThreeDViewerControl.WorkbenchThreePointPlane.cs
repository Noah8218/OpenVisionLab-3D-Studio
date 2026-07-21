using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public void ShowWorkbenchThreePointPlane(C3DThreePointPlaneFeature output, bool isPublished)
    {
        viewModel.SetWorkbenchThreePointPlane(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchThreePointPlane()
    {
        viewModel.ClearWorkbenchThreePointPlane();
        RenderNow();
    }
}

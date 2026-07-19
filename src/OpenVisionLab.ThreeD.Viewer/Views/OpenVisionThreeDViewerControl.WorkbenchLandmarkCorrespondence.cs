using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public void ShowWorkbenchLandmarkCorrespondence(
        IReadOnlyList<C3DLineIntersectionFeature> anchors,
        C3DLandmarkCorrespondenceSet output,
        bool isPublished)
    {
        viewModel.SetWorkbenchLandmarkCorrespondence(anchors, output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchLandmarkCorrespondence()
    {
        viewModel.ClearWorkbenchLandmarkCorrespondence();
        RenderNow();
    }
}

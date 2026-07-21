using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public void ShowWorkbenchDatumPlaneDeviation(
        C3DThreePointPlaneFeature plane,
        ToolRecipeSelection measurementSelection,
        C3DDatumPlaneDeviationFeature output,
        bool isPublished)
    {
        viewModel.SetWorkbenchDatumPlaneDeviation(plane, measurementSelection, output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchDatumPlaneDeviation()
    {
        viewModel.ClearWorkbenchDatumPlaneDeviation();
        RenderNow();
    }
}

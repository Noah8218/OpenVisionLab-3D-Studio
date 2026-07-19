using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public void ShowWorkbenchLineIntersection(
        C3DLineFeature firstLine,
        C3DLineFeature secondLine,
        C3DLineIntersectionFeature output,
        bool isPublished)
    {
        viewModel.SetWorkbenchLineIntersection(firstLine, secondLine, output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchLineIntersection()
    {
        viewModel.ClearWorkbenchLineIntersection();
        RenderNow();
    }

    public void ShowWorkbenchLineIntersectionInputs(C3DLineFeature firstLine, C3DLineFeature secondLine)
    {
        viewModel.SetWorkbenchLineIntersectionInputs(firstLine, secondLine);
        RenderNow();
    }
}

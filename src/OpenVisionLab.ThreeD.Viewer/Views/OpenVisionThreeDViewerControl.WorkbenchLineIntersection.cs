using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public void ShowWorkbenchLineIntersection(
        IC3DLineGeometry firstLine,
        IC3DLineGeometry secondLine,
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

    public void ShowWorkbenchLineIntersectionInputs(IC3DLineGeometry firstLine, IC3DLineGeometry secondLine)
    {
        viewModel.SetWorkbenchLineIntersectionInputs(firstLine, secondLine);
        RenderNow();
    }
}

using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public void ShowWorkbenchTwoPointLine(C3DTwoPointLineFeature output, bool isPublished)
    {
        viewModel.SetWorkbenchTwoPointLine(output, isPublished);
        RenderNow();
    }

    public void ClearWorkbenchTwoPointLine()
    {
        viewModel.ClearWorkbenchTwoPointLine();
        RenderNow();
    }
}

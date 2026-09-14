using System.IO;
using System.Numerics;
using System.Text.Json;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Recipes;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public bool PreviewC3DGapFlush() =>
        C3DGapFlushRuleCoordinator.Preview(
            c3dSample,
            viewModel,
            roiEditingSession.ApplyGapFlushPreviewOverlay,
            RenderNow);

}

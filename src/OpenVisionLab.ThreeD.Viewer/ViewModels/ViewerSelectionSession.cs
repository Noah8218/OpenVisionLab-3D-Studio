using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

internal sealed class ViewerSelectionSession
{
    public string SelectedEntity { get; set; } = "Generated Unit Cube";

    public string PickCoordinate { get; set; } = "(none)";

    public C3DGridCursor? LinkedHeightCursor { get; set; }

    public string SelectedMode { get; set; } = "Point";

    public string Summary { get; set; } = "Point selection: generated point cloud peak";

    public bool OverlayVisible { get; set; } = true;
}

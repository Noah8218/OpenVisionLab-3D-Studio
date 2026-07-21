using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private C3DThreePointPlaneFeature? workbenchThreePointPlane;
    private bool isWorkbenchThreePointPlanePublished;

    public C3DThreePointPlaneFeature? WorkbenchThreePointPlane => workbenchThreePointPlane;
    public bool IsWorkbenchThreePointPlanePublished => isWorkbenchThreePointPlanePublished;

    internal void SetWorkbenchThreePointPlane(C3DThreePointPlaneFeature output, bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(output);
        workbenchThreePointPlane = output;
        isWorkbenchThreePointPlanePublished = isPublished;
        var state = isPublished ? "Published" : "Preview - not published";
        SelectionSummary = $"{state} 3-Point Plane | {output.OutputRole} | normal ({output.NormalX:G4}, {output.NormalY:G4}, {output.NormalZ:G4}) | {output.ContentSha256[..12]}";
        ViewerStatus = isPublished ? "Published ordered 3-Point Plane datum" : "Ordered 3-Point Plane Preview - not published";
        OnPropertyChanged(nameof(WorkbenchThreePointPlane));
        OnPropertyChanged(nameof(IsWorkbenchThreePointPlanePublished));
    }

    internal void ClearWorkbenchThreePointPlane()
    {
        workbenchThreePointPlane = null;
        isWorkbenchThreePointPlanePublished = false;
        ViewerStatus = "3-Point Plane preview cleared";
        OnPropertyChanged(nameof(WorkbenchThreePointPlane));
        OnPropertyChanged(nameof(IsWorkbenchThreePointPlanePublished));
    }
}

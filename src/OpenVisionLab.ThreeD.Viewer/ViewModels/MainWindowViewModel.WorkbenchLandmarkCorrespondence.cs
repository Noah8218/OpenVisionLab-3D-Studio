using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    private IReadOnlyList<C3DLineIntersectionFeature> workbenchLandmarkCorrespondenceAnchors = [];
    private C3DLandmarkCorrespondenceSet? workbenchLandmarkCorrespondence;
    private bool isWorkbenchLandmarkCorrespondencePublished;
    private string landmarkCorrespondenceHudSummary = string.Empty;

    public IReadOnlyList<C3DLineIntersectionFeature> WorkbenchLandmarkCorrespondenceAnchors => workbenchLandmarkCorrespondenceAnchors;
    public C3DLandmarkCorrespondenceSet? WorkbenchLandmarkCorrespondence => workbenchLandmarkCorrespondence;
    public bool IsWorkbenchLandmarkCorrespondencePublished => isWorkbenchLandmarkCorrespondencePublished;
    public bool LandmarkCorrespondenceHudVisible => workbenchLandmarkCorrespondence is not null;
    public string LandmarkCorrespondenceHudSummary { get => landmarkCorrespondenceHudSummary; private set => SetField(ref landmarkCorrespondenceHudSummary, value); }

    internal void SetWorkbenchLandmarkCorrespondence(
        IReadOnlyList<C3DLineIntersectionFeature> anchors,
        C3DLandmarkCorrespondenceSet output,
        bool isPublished)
    {
        ArgumentNullException.ThrowIfNull(anchors);
        ArgumentNullException.ThrowIfNull(output);
        workbenchLandmarkCorrespondenceAnchors = anchors.ToArray();
        workbenchLandmarkCorrespondence = output;
        isWorkbenchLandmarkCorrespondencePublished = isPublished;
        var status = isPublished ? "Published" : "Preview - not published";
        SelectionSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"{status} Landmark Correspondence | {anchors.Count}/4 corner anchors | source volume {output.SourceNormalizedTetrahedronVolume:F6} | reference volume {output.ReferenceNormalizedTetrahedronVolume:F6}");
        ViewerStatus = isPublished
            ? "Published landmark correspondence evidence; no affine transform was calculated"
            : "Landmark correspondence Preview - not published";
        LandmarkCorrespondenceHudSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"Correspondence {status} | 4 source anchors | {output.ReferenceFrameId} | min volume {output.MinimumNormalizedTetrahedronVolume:G4}");
        OnPropertyChanged(nameof(WorkbenchLandmarkCorrespondenceAnchors));
        OnPropertyChanged(nameof(WorkbenchLandmarkCorrespondence));
        OnPropertyChanged(nameof(IsWorkbenchLandmarkCorrespondencePublished));
        OnPropertyChanged(nameof(LandmarkCorrespondenceHudVisible));
    }

    internal void ClearWorkbenchLandmarkCorrespondence()
    {
        workbenchLandmarkCorrespondenceAnchors = [];
        workbenchLandmarkCorrespondence = null;
        isWorkbenchLandmarkCorrespondencePublished = false;
        LandmarkCorrespondenceHudSummary = string.Empty;
        OnPropertyChanged(nameof(WorkbenchLandmarkCorrespondenceAnchors));
        OnPropertyChanged(nameof(WorkbenchLandmarkCorrespondence));
        OnPropertyChanged(nameof(IsWorkbenchLandmarkCorrespondencePublished));
        OnPropertyChanged(nameof(LandmarkCorrespondenceHudVisible));
    }
}

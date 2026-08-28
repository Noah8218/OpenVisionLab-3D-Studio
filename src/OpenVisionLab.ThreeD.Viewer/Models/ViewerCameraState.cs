namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// A presentation-only camera snapshot that can be copied between real Viewer
/// instances. It contains no source, selection, recipe, or inspection state.
/// </summary>
public readonly record struct ViewerCameraState(
    double YawDegrees,
    double PitchDegrees,
    double Distance,
    double TargetX,
    double TargetY,
    double TargetZ,
    ViewerProjectionMode ProjectionMode,
    double OrthographicHeight)
{
    public bool IsValid =>
        double.IsFinite(YawDegrees)
        && double.IsFinite(PitchDegrees)
        && double.IsFinite(Distance)
        && Distance > 0.0
        && double.IsFinite(TargetX)
        && double.IsFinite(TargetY)
        && double.IsFinite(TargetZ)
        && double.IsFinite(OrthographicHeight)
        && OrthographicHeight > 0.0
        && ProjectionMode is ViewerProjectionMode.Perspective
            or ViewerProjectionMode.TopOrthographic;
}

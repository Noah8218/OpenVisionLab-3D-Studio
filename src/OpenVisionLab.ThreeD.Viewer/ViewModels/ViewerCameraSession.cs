using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

internal sealed class ViewerCameraSession
{
    private ViewerCameraSnapshot? savedPerspective;
    private double orthographicHeight = 10.0;

    public double YawDegrees { get; set; } = 38.0;

    public double PitchDegrees { get; set; } = 24.0;

    public double Distance { get; set; } = 9.2;

    public double TargetX { get; set; } = 2.05;

    public double TargetY { get; set; } = -0.25;

    public double TargetZ { get; set; }

    public ViewerProjectionMode ProjectionMode { get; set; } = ViewerProjectionMode.Perspective;

    public double OrthographicHeight
    {
        get => orthographicHeight;
        set => orthographicHeight = Math.Max(0.01, value);
    }

    public bool HasSavedPerspective => savedPerspective.HasValue;

    public void SavePerspective()
    {
        savedPerspective = new ViewerCameraSnapshot(
            YawDegrees,
            PitchDegrees,
            Distance,
            TargetX,
            TargetY,
            TargetZ);
    }

    public bool TryGetSavedPerspective(out ViewerCameraSnapshot snapshot)
    {
        if (savedPerspective is { } saved)
        {
            snapshot = saved;
            return true;
        }

        snapshot = default;
        return false;
    }

    public void ClearSavedPerspective()
    {
        savedPerspective = null;
    }
}

internal readonly record struct ViewerCameraSnapshot(
    double YawDegrees,
    double PitchDegrees,
    double Distance,
    double TargetX,
    double TargetY,
    double TargetZ);

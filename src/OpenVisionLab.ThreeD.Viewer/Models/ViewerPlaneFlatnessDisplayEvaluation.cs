using System.Numerics;

namespace OpenVisionLab.ThreeD.Viewer.Models;

/// <summary>
/// Viewer-owned plane geometry needed to draw a flatness reference plane.
/// </summary>
internal sealed record ViewerPlaneFitDisplay(
    double SlopeX,
    double SlopeZ,
    double Intercept,
    Vector3 Normal,
    double Offset)
{
    public double EvaluateY(double x, double z) => SlopeX * x + SlopeZ * z + Intercept;
}

/// <summary>
/// Immutable display projection of a plane-flatness evaluation.
/// Numerical evaluation remains owned by the Tools layer.
/// </summary>
internal sealed record ViewerPlaneFlatnessDisplayEvaluation(
    ViewerPlaneFitDisplay? ReferencePlane,
    double MinimumSignedDistance,
    double MaximumSignedDistance,
    Vector3 MinimumPoint,
    Vector3 MaximumPoint,
    Vector3 MinimumProjection,
    Vector3 MaximumProjection);

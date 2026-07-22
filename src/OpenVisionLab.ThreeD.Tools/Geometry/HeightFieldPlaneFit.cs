using System.Numerics;
using NoahFitSample = Lib.ThreeD.FeatureExtraction.HeightFieldPlaneFitSample;
using NoahFitResult = Lib.ThreeD.FeatureExtraction.LeastSquaresHeightFieldPlaneFitResult;
using NoahFitTool = Lib.ThreeD.FeatureExtraction.LeastSquaresHeightFieldPlaneFitTool;
using NoahPoint = Lib.ThreeD.FeatureExtraction.ThreeDPoint;

namespace OpenVisionLab.ThreeD.Tools;

public readonly record struct HeightFieldPlaneSample(Vector3 Position, double RawHeight);

public sealed record HeightFieldPlaneFitResult(
    double SlopeX,
    double SlopeZ,
    double Intercept,
    Vector3 Normal,
    double Offset,
    int SampleCount,
    double RootMeanSquareDistance,
    Vector3 Target,
    Vector3 TargetProjection,
    double TargetSignedDistance,
    double TargetAbsoluteDistance,
    double TargetRawHeight,
    double TargetRawReferenceHeight,
    double TargetRawHeightDelta)
{
    public double EvaluateY(double x, double z) => SlopeX * x + SlopeZ * z + Intercept;
}

public static class HeightFieldPlaneFit
{
    public static HeightFieldPlaneFitResult Fit(IReadOnlyList<HeightFieldPlaneSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var result = new NoahFitTool().Execute(samples.Select(ToNoahSample).ToArray());
        return FromNoahResult(result);
    }

    internal static HeightFieldPlaneFitResult FromNoahResult(NoahFitResult result)
    {
        return new HeightFieldPlaneFitResult(
            result.SlopeX,
            result.SlopeZ,
            result.Intercept,
            ToVector3(result.Normal),
            result.Offset,
            result.SampleCount,
            result.RootMeanSquareDistance,
            ToVector3(result.Target),
            ToVector3(result.TargetProjection),
            result.TargetSignedDistance,
            result.TargetAbsoluteDistance,
            result.TargetRawHeight,
            result.TargetRawReferenceHeight,
            result.TargetRawHeightDelta);
    }

    internal static NoahFitSample ToNoahSample(HeightFieldPlaneSample sample) =>
        new(new NoahPoint(sample.Position.X, sample.Position.Y, sample.Position.Z), sample.RawHeight);

    internal static Vector3 ToVector3(NoahPoint point) =>
        new((float)point.X, (float)point.Y, (float)point.Z);
}

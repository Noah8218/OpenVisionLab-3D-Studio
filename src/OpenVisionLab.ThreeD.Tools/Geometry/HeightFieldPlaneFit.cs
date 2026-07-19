using System.Numerics;

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
        if (samples.Count < 3)
        {
            throw new ArgumentException("Plane fitting requires at least three samples.", nameof(samples));
        }

        var meanX = 0.0;
        var meanY = 0.0;
        var meanZ = 0.0;
        var meanRaw = 0.0;
        foreach (var sample in samples)
        {
            if (!IsFinite(sample))
            {
                throw new ArgumentException("Plane fitting samples must contain finite coordinates and raw heights.", nameof(samples));
            }

            meanX += sample.Position.X;
            meanY += sample.Position.Y;
            meanZ += sample.Position.Z;
            meanRaw += sample.RawHeight;
        }

        meanX /= samples.Count;
        meanY /= samples.Count;
        meanZ /= samples.Count;
        meanRaw /= samples.Count;

        var sumXX = 0.0;
        var sumXZ = 0.0;
        var sumZZ = 0.0;
        var sumXY = 0.0;
        var sumZY = 0.0;
        var sumXRaw = 0.0;
        var sumZRaw = 0.0;
        foreach (var sample in samples)
        {
            var x = sample.Position.X - meanX;
            var y = sample.Position.Y - meanY;
            var z = sample.Position.Z - meanZ;
            var raw = sample.RawHeight - meanRaw;
            sumXX += x * x;
            sumXZ += x * z;
            sumZZ += z * z;
            sumXY += x * y;
            sumZY += z * y;
            sumXRaw += x * raw;
            sumZRaw += z * raw;
        }

        var determinant = sumXX * sumZZ - sumXZ * sumXZ;
        var determinantScale = Math.Max(1.0, Math.Abs(sumXX * sumZZ));
        if (Math.Abs(determinant) <= determinantScale * 1e-12)
        {
            throw new ArgumentException("Plane fitting samples must span two horizontal axes.", nameof(samples));
        }

        var slopeX = (sumXY * sumZZ - sumZY * sumXZ) / determinant;
        var slopeZ = (sumZY * sumXX - sumXY * sumXZ) / determinant;
        var intercept = meanY - slopeX * meanX - slopeZ * meanZ;
        var rawSlopeX = (sumXRaw * sumZZ - sumZRaw * sumXZ) / determinant;
        var rawSlopeZ = (sumZRaw * sumXX - sumXRaw * sumXZ) / determinant;
        var rawIntercept = meanRaw - rawSlopeX * meanX - rawSlopeZ * meanZ;

        var normalLength = Math.Sqrt(slopeX * slopeX + 1.0 + slopeZ * slopeZ);
        var normal = new Vector3(
            (float)(-slopeX / normalLength),
            (float)(1.0 / normalLength),
            (float)(-slopeZ / normalLength));
        var offset = -intercept / normalLength;

        var target = samples[0];
        var targetSignedDistance = SignedDistance(target.Position, normal, offset);
        var squaredDistanceSum = targetSignedDistance * targetSignedDistance;
        for (var i = 1; i < samples.Count; i++)
        {
            var signedDistance = SignedDistance(samples[i].Position, normal, offset);
            squaredDistanceSum += signedDistance * signedDistance;
            if (Math.Abs(signedDistance) > Math.Abs(targetSignedDistance))
            {
                target = samples[i];
                targetSignedDistance = signedDistance;
            }
        }

        var projection = target.Position - normal * (float)targetSignedDistance;
        var rawReference = rawSlopeX * target.Position.X + rawSlopeZ * target.Position.Z + rawIntercept;
        return new HeightFieldPlaneFitResult(
            slopeX,
            slopeZ,
            intercept,
            normal,
            offset,
            samples.Count,
            Math.Sqrt(squaredDistanceSum / samples.Count),
            target.Position,
            projection,
            targetSignedDistance,
            Math.Abs(targetSignedDistance),
            target.RawHeight,
            rawReference,
            target.RawHeight - rawReference);
    }

    private static bool IsFinite(HeightFieldPlaneSample sample) =>
        float.IsFinite(sample.Position.X)
        && float.IsFinite(sample.Position.Y)
        && float.IsFinite(sample.Position.Z)
        && double.IsFinite(sample.RawHeight);

    private static double SignedDistance(Vector3 point, Vector3 normal, double offset) =>
        normal.X * point.X + normal.Y * point.Y + normal.Z * point.Z + offset;
}

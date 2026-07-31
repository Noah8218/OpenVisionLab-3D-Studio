namespace OpenVisionLab.ThreeD.Core;

public sealed record RigidSurfacePoseSearchParameterValidityReport(
    bool IsValid,
    long CandidateCount,
    IReadOnlyList<string> Errors,
    string Evidence);

/// <summary>
/// Source-independent validation for a recipe-owned finite rigid-pose search
/// domain. Input sample-count checks remain in the executor.
/// </summary>
public static class RigidSurfacePoseSearchParameterValidator
{
    public const int AbsoluteMaximumCandidateCount = 1_000_000;

    public static RigidSurfacePoseSearchParameterValidityReport Inspect(
        RigidSurfacePoseSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var errors = new List<string>();
        var xCount = AxisCount(
            parameters.MinimumRotationXDegrees,
            parameters.MaximumRotationXDegrees,
            parameters.RotationStepXDegrees,
            "X",
            errors);
        var yCount = AxisCount(
            parameters.MinimumRotationYDegrees,
            parameters.MaximumRotationYDegrees,
            parameters.RotationStepYDegrees,
            "Y",
            errors);
        var zCount = AxisCount(
            parameters.MinimumRotationZDegrees,
            parameters.MaximumRotationZDegrees,
            parameters.RotationStepZDegrees,
            "Z",
            errors);

        var translation = new[]
        {
            parameters.MinimumTranslationX,
            parameters.MaximumTranslationX,
            parameters.MinimumTranslationY,
            parameters.MaximumTranslationY,
            parameters.MinimumTranslationZ,
            parameters.MaximumTranslationZ
        };
        if (translation.Any(value => !double.IsFinite(value))
            || parameters.MinimumTranslationX
                > parameters.MaximumTranslationX
            || parameters.MinimumTranslationY
                > parameters.MaximumTranslationY
            || parameters.MinimumTranslationZ
                > parameters.MaximumTranslationZ)
        {
            errors.Add(
                "Rigid pose translation bounds must be finite and ordered.");
        }

        if (!double.IsFinite(
                parameters.MaximumCorrespondenceDistance)
            || parameters.MaximumCorrespondenceDistance <= 0.0)
        {
            errors.Add(
                "Maximum correspondence distance must be finite and positive.");
        }

        if (parameters.MinimumMatchedSampleCount < 3)
        {
            errors.Add(
                "Minimum matched sample count must be at least three.");
        }

        if (parameters.MaximumCandidateCount <= 0
            || parameters.MaximumCandidateCount
                > AbsoluteMaximumCandidateCount)
        {
            errors.Add(
                $"Maximum candidate count must be from 1 through {AbsoluteMaximumCandidateCount}.");
        }

        var candidateCount = xCount <= 0
                || yCount <= 0
                || zCount <= 0
            ? 0
            : checked(xCount * yCount * zCount);
        if (candidateCount > parameters.MaximumCandidateCount)
        {
            errors.Add(
                $"Rigid pose search candidate count {candidateCount} exceeds the declared maximum {parameters.MaximumCandidateCount}.");
        }

        return new RigidSurfacePoseSearchParameterValidityReport(
            errors.Count == 0,
            candidateCount,
            errors,
            $"candidateCount={candidateCount};maximumCandidateCount={parameters.MaximumCandidateCount};rotationX={xCount};rotationY={yCount};rotationZ={zCount}");
    }

    private static long AxisCount(
        double minimum,
        double maximum,
        double step,
        string axis,
        List<string> errors)
    {
        if (!double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || !double.IsFinite(step)
            || minimum > maximum
            || step <= 0.0
            || minimum < -180.0
            || maximum > 180.0)
        {
            errors.Add(
                $"Rigid pose {axis}-rotation range requires finite ordered bounds in [-180,180] and a positive step.");
            return 0;
        }

        var count = Math.Floor(
            (maximum - minimum) / step
            + 1e-12)
            + 1.0;
        if (!double.IsFinite(count)
            || count < 1.0
            || count > AbsoluteMaximumCandidateCount)
        {
            errors.Add(
                $"Rigid pose {axis}-rotation candidate count exceeds the supported limit {AbsoluteMaximumCandidateCount}.");
            return 0;
        }

        return checked((long)count);
    }
}

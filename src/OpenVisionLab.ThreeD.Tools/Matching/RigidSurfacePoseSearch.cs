using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Deterministic bounded rigid-pose search. Version 1 enumerates an explicit
/// Euler rotation grid, derives translation by sample-centroid alignment, and
/// ranks candidates by model coverage, RMSE, then enumeration order.
/// </summary>
public static class RigidSurfacePoseSearch
{
    public const int AbsoluteMaximumCandidateCount =
        RigidSurfacePoseSearchParameterValidator
            .AbsoluteMaximumCandidateCount;

    public static RigidSurfacePoseSearchResult Execute(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(parameters);

        var modelValidity =
            SurfaceModelArtifactValidator.Inspect(model);
        if (!modelValidity.IsValid)
        {
            throw new InvalidDataException(
                "Rigid pose search requires a valid SurfaceModel.");
        }

        var sceneValidity =
            PreparedSceneArtifactValidator.Inspect(scene);
        if (!sceneValidity.IsValid)
        {
            throw new InvalidDataException(
                "Rigid pose search requires a valid Prepared Scene.");
        }

        if (!string.Equals(
                model.Unit,
                scene.Unit,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Rigid pose search requires identical explicit units.");
        }

        var parameterValidity =
            RigidSurfacePoseSearchParameterValidator.Inspect(
                parameters);
        if (!parameterValidity.IsValid)
        {
            throw new InvalidDataException(
                string.Join(" ", parameterValidity.Errors));
        }

        var rotationXCount = AxisCandidateCount(
            parameters.MinimumRotationXDegrees,
            parameters.MaximumRotationXDegrees,
            parameters.RotationStepXDegrees,
            "X");
        var rotationYCount = AxisCandidateCount(
            parameters.MinimumRotationYDegrees,
            parameters.MaximumRotationYDegrees,
            parameters.RotationStepYDegrees,
            "Y");
        var rotationZCount = AxisCandidateCount(
            parameters.MinimumRotationZDegrees,
            parameters.MaximumRotationZDegrees,
            parameters.RotationStepZDegrees,
            "Z");
        ValidateParameters(
            model,
            scene,
            parameters,
            rotationXCount,
            rotationYCount,
            rotationZCount);
        var rotationX = AxisCandidates(
            parameters.MinimumRotationXDegrees,
            parameters.RotationStepXDegrees,
            rotationXCount);
        var rotationY = AxisCandidates(
            parameters.MinimumRotationYDegrees,
            parameters.RotationStepYDegrees,
            rotationYCount);
        var rotationZ = AxisCandidates(
            parameters.MinimumRotationZDegrees,
            parameters.RotationStepZDegrees,
            rotationZCount);

        var modelCentroid =
            Centroid(
                model.Samples.Select(sample => sample.Position));
        var sceneCentroid =
            Centroid(
                scene.Samples.Select(sample => sample.Position));
        Candidate? best = null;
        var evaluatedCandidateCount = 0;
        var enumerationOrder = 0;
        foreach (var xDegrees in rotationX)
        {
            foreach (var yDegrees in rotationY)
            {
                foreach (var zDegrees in rotationZ)
                {
                    evaluatedCandidateCount++;
                    var rotation = Rotation(
                        xDegrees,
                        yDegrees,
                        zDegrees);
                    var rotatedCentroid =
                        TransformDirection(
                            rotation,
                            modelCentroid);
                    var translation =
                        Subtract(
                            sceneCentroid,
                            rotatedCentroid);
                    if (!InsideTranslationBounds(
                            translation,
                            parameters))
                    {
                        enumerationOrder++;
                        continue;
                    }

                    var pose = new RigidPose3D(
                        model.Unit,
                        model.FrameId,
                        scene.FrameId,
                        rotation.M11,
                        rotation.M12,
                        rotation.M13,
                        rotation.M21,
                        rotation.M22,
                        rotation.M23,
                        rotation.M31,
                        rotation.M32,
                        rotation.M33,
                        translation.X,
                        translation.Y,
                        translation.Z);
                    var coverage =
                        SurfaceCoverageScorer.Evaluate(
                            model,
                            scene,
                            pose,
                            parameters
                                .MaximumCorrespondenceDistance);
                    var candidate = new Candidate(
                        enumerationOrder,
                        xDegrees,
                        yDegrees,
                        zDegrees,
                        pose,
                        coverage);
                    if (IsBetter(candidate, best))
                    {
                        best = candidate;
                    }

                    enumerationOrder++;
                }
            }
        }

        if (best is null
            || best.Coverage.MatchedModelSampleCount
                < parameters.MinimumMatchedSampleCount)
        {
            var emptyCoverage =
                best?.Coverage
                ?? EmptyCoverage(
                    model.Samples.Length,
                    scene.Samples.Length,
                    parameters.MaximumCorrespondenceDistance);
            return RigidSurfacePoseSearchResult.Create(
                model.ContentSha256,
                scene.ContentSha256,
                parameters,
                RigidSurfacePoseSearchState.NoMatch,
                evaluatedCandidateCount,
                null,
                emptyCoverage,
                best is null
                    ? "No rotation candidate produced a translation inside the declared bounds."
                    : $"Best candidate matched {best.Coverage.MatchedModelSampleCount} model samples, below the required {parameters.MinimumMatchedSampleCount}.");
        }

        return RigidSurfacePoseSearchResult.Create(
            model.ContentSha256,
            scene.ContentSha256,
            parameters,
            RigidSurfacePoseSearchState.Matched,
            evaluatedCandidateCount,
            best.Pose,
            best.Coverage,
            string.Empty);
    }

    private static int AxisCandidateCount(
        double minimum,
        double maximum,
        double step,
        string axis)
    {
        if (!double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || !double.IsFinite(step)
            || minimum > maximum
            || step <= 0.0
            || minimum < -180.0
            || maximum > 180.0)
        {
            throw new InvalidDataException(
                $"Rigid pose {axis}-rotation range requires finite ordered bounds in [-180,180] and a positive step.");
        }

        var candidateCount = Math.Floor(
            (maximum - minimum) / step
            + 1e-12)
            + 1.0;
        if (!double.IsFinite(candidateCount)
            || candidateCount < 1.0)
        {
            throw new InvalidDataException(
                $"Rigid pose {axis}-rotation range has no candidate.");
        }

        if (candidateCount > AbsoluteMaximumCandidateCount)
        {
            throw new InvalidDataException(
                $"Rigid pose {axis}-rotation candidate count exceeds the supported limit {AbsoluteMaximumCandidateCount}.");
        }

        return checked((int)candidateCount);
    }

    private static IReadOnlyList<double> AxisCandidates(
        double minimum,
        double step,
        int count)
    {
        var values = new double[count];
        for (var index = 0; index < count; index++)
        {
            values[index] = minimum + index * step;
        }

        return Array.AsReadOnly(values);
    }

    private static void ValidateParameters(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidSurfacePoseSearchParameters parameters,
        int xCount,
        int yCount,
        int zCount)
    {
        if (parameters.MinimumMatchedSampleCount
                > model.Samples.Length
            || parameters.MinimumMatchedSampleCount
                > scene.Samples.Length)
        {
            throw new InvalidDataException(
                "Rigid pose search minimum matched sample count exceeds the available model or scene samples.");
        }

        var candidateCount =
            checked((long)xCount * yCount * zCount);
        if (candidateCount > parameters.MaximumCandidateCount)
        {
            throw new InvalidDataException(
                $"Rigid pose search candidate count {candidateCount} exceeds the declared maximum {parameters.MaximumCandidateCount}.");
        }
    }

    private static bool IsBetter(
        Candidate candidate,
        Candidate? current)
    {
        if (current is null)
        {
            return true;
        }

        if (candidate.Coverage.MatchedModelSampleCount
            != current.Coverage.MatchedModelSampleCount)
        {
            return candidate.Coverage.MatchedModelSampleCount
                > current.Coverage.MatchedModelSampleCount;
        }

        var candidateRmse =
            candidate.Coverage.InlierRmse
            ?? double.PositiveInfinity;
        var currentRmse =
            current.Coverage.InlierRmse
            ?? double.PositiveInfinity;
        if (candidateRmse != currentRmse)
        {
            return candidateRmse < currentRmse;
        }

        return candidate.EnumerationOrder
            < current.EnumerationOrder;
    }

    private static SurfaceCoverageEvaluation EmptyCoverage(
        int modelSampleCount,
        int sceneSampleCount,
        double maximumCorrespondenceDistance) =>
        new(
            SurfaceCoverageEvaluation.CurrentSemantics,
            modelSampleCount,
            sceneSampleCount,
            0,
            modelSampleCount,
            0.0,
            null,
            maximumCorrespondenceDistance,
            [],
            $"semantics={SurfaceCoverageEvaluation.CurrentSemantics};matched=0/{modelSampleCount};scene={sceneSampleCount};coverage=0;rmse=unavailable;maximumDistance={maximumCorrespondenceDistance:G17}");

    private static SurfaceModelPoint3 Centroid(
        IEnumerable<SurfaceModelPoint3> points)
    {
        var x = 0.0;
        var y = 0.0;
        var z = 0.0;
        var count = 0;
        foreach (var point in points)
        {
            x += point.X;
            y += point.Y;
            z += point.Z;
            count++;
        }

        if (count == 0)
        {
            throw new InvalidDataException(
                "Rigid pose search requires at least one sample.");
        }

        return new SurfaceModelPoint3(
            x / count,
            y / count,
            z / count);
    }

    private static Rotation3 Rotation(
        double xDegrees,
        double yDegrees,
        double zDegrees)
    {
        var x = xDegrees * Math.PI / 180.0;
        var y = yDegrees * Math.PI / 180.0;
        var z = zDegrees * Math.PI / 180.0;
        var cx = Math.Cos(x);
        var sx = Math.Sin(x);
        var cy = Math.Cos(y);
        var sy = Math.Sin(y);
        var cz = Math.Cos(z);
        var sz = Math.Sin(z);

        return new Rotation3(
            cz * cy,
            cz * sy * sx - sz * cx,
            cz * sy * cx + sz * sx,
            sz * cy,
            sz * sy * sx + cz * cx,
            sz * sy * cx - cz * sx,
            -sy,
            cy * sx,
            cy * cx);
    }

    private static SurfaceModelPoint3 TransformDirection(
        Rotation3 rotation,
        SurfaceModelPoint3 point) =>
        new(
            rotation.M11 * point.X
                + rotation.M12 * point.Y
                + rotation.M13 * point.Z,
            rotation.M21 * point.X
                + rotation.M22 * point.Y
                + rotation.M23 * point.Z,
            rotation.M31 * point.X
                + rotation.M32 * point.Y
                + rotation.M33 * point.Z);

    private static SurfaceModelPoint3 Subtract(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(
            first.X - second.X,
            first.Y - second.Y,
            first.Z - second.Z);

    private static bool InsideTranslationBounds(
        SurfaceModelPoint3 translation,
        RigidSurfacePoseSearchParameters parameters) =>
        translation.X >= parameters.MinimumTranslationX
        && translation.X <= parameters.MaximumTranslationX
        && translation.Y >= parameters.MinimumTranslationY
        && translation.Y <= parameters.MaximumTranslationY
        && translation.Z >= parameters.MinimumTranslationZ
        && translation.Z <= parameters.MaximumTranslationZ;

    private sealed record Candidate(
        int EnumerationOrder,
        double RotationXDegrees,
        double RotationYDegrees,
        double RotationZDegrees,
        RigidPose3D Pose,
        SurfaceCoverageEvaluation Coverage);

    private sealed record Rotation3(
        double M11,
        double M12,
        double M13,
        double M21,
        double M22,
        double M23,
        double M31,
        double M32,
        double M33);
}

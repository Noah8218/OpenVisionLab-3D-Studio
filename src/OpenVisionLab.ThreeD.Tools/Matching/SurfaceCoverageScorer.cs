using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Decision-free one-way nominal-surface coverage. Model samples are visited
/// in stable order and claim the nearest still-unclaimed scene sample.
/// </summary>
public static class SurfaceCoverageScorer
{
    public static SurfaceCoverageEvaluation Evaluate(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidPose3D pose,
        double maximumCorrespondenceDistance)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(pose);

        var modelValidity =
            SurfaceModelArtifactValidator.Inspect(model);
        if (!modelValidity.IsValid)
        {
            throw new InvalidDataException(
                "Surface coverage requires a valid SurfaceModel.");
        }

        var sceneValidity =
            PreparedSceneArtifactValidator.Inspect(scene);
        if (!sceneValidity.IsValid)
        {
            throw new InvalidDataException(
                "Surface coverage requires a valid Prepared Scene.");
        }

        if (!string.Equals(
                model.Unit,
                scene.Unit,
                StringComparison.Ordinal)
            || !string.Equals(
                model.Unit,
                pose.Unit,
                StringComparison.Ordinal)
            || !string.Equals(
                model.FrameId,
                pose.SourceFrameId,
                StringComparison.Ordinal)
            || !string.Equals(
                scene.FrameId,
                pose.TargetFrameId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Surface coverage requires matching units and explicit model-to-scene frames.");
        }

        if (!pose.IsRigid(1e-9))
        {
            throw new InvalidDataException(
                "Surface coverage requires a finite rigid pose.");
        }

        if (!double.IsFinite(maximumCorrespondenceDistance)
            || maximumCorrespondenceDistance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCorrespondenceDistance),
                "Surface coverage distance must be finite and positive.");
        }

        var claimedSceneSamples =
            new bool[scene.Samples.Length];
        var matches =
            new List<SurfaceCoverageMatch>(
                Math.Min(
                    model.Samples.Length,
                    scene.Samples.Length));
        var maximumDistanceSquared =
            maximumCorrespondenceDistance
            * maximumCorrespondenceDistance;
        var squaredErrorSum = 0.0;
        foreach (var modelSample in model.Samples)
        {
            var transformed =
                pose.TransformPoint(modelSample.Position);
            var bestSceneOrder = -1;
            var bestDistanceSquared = double.PositiveInfinity;
            foreach (var sceneSample in scene.Samples)
            {
                if (claimedSceneSamples[sceneSample.Order])
                {
                    continue;
                }

                var distanceSquared =
                    DistanceSquared(
                        transformed,
                        sceneSample.Position);
                if (distanceSquared
                        < bestDistanceSquared
                    || distanceSquared
                        == bestDistanceSquared
                    && sceneSample.Order < bestSceneOrder)
                {
                    bestDistanceSquared = distanceSquared;
                    bestSceneOrder = sceneSample.Order;
                }
            }

            if (bestSceneOrder < 0
                || bestDistanceSquared
                    > maximumDistanceSquared)
            {
                continue;
            }

            claimedSceneSamples[bestSceneOrder] = true;
            squaredErrorSum += bestDistanceSquared;
            matches.Add(
                new SurfaceCoverageMatch(
                    modelSample.Order,
                    bestSceneOrder,
                    Math.Sqrt(bestDistanceSquared)));
        }

        var matchedCount = matches.Count;
        var coverageRatio =
            matchedCount / (double)model.Samples.Length;
        double? inlierRmse = matchedCount == 0
            ? null
            : Math.Sqrt(squaredErrorSum / matchedCount);
        var evidence =
            $"semantics={SurfaceCoverageEvaluation.CurrentSemantics};"
            + $"matched={matchedCount}/{model.Samples.Length};"
            + $"scene={scene.Samples.Length};"
            + $"coverage={coverageRatio:G17};"
            + $"rmse={(inlierRmse.HasValue ? inlierRmse.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) : "unavailable")};"
            + $"maximumDistance={maximumCorrespondenceDistance:G17}";

        return new SurfaceCoverageEvaluation(
            SurfaceCoverageEvaluation.CurrentSemantics,
            model.Samples.Length,
            scene.Samples.Length,
            matchedCount,
            model.Samples.Length - matchedCount,
            coverageRatio,
            inlierRmse,
            maximumCorrespondenceDistance,
            matches.ToArray(),
            evidence);
    }

    private static double DistanceSquared(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        var z = first.Z - second.Z;
        return x * x + y * y + z * z;
    }
}

using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Decision-free positional score for identified model/scene edge anchors at
/// an already-identified surface pose. It does not alter pose or acceptance.
/// </summary>
public static class SurfaceAndEdgeMatchScorer
{
    public static SurfaceAndEdgeMatchScoreArtifact Evaluate(
        SurfaceMatchExecutionArtifact execution,
        ModelSurfaceEdgeArtifact modelEdges,
        SceneSurfaceEdgeArtifact sceneEdges,
        double maximumCorrespondenceDistance)
    {
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(modelEdges);
        ArgumentNullException.ThrowIfNull(sceneEdges);

        RequireValid(execution, modelEdges, sceneEdges);
        if (!double.IsFinite(maximumCorrespondenceDistance)
            || maximumCorrespondenceDistance <= 0.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumCorrespondenceDistance),
                "Edge score distance must be finite and positive.");
        }

        if (execution.PoseResult.State != RigidSurfacePoseSearchState.Matched
            || execution.PoseResult.Pose is not { } pose)
        {
            throw new InvalidDataException(
                "Edge scoring requires an identified matched surface pose.");
        }

        if (modelEdges.Edges.Length == 0)
        {
            throw new InvalidDataException(
                "Edge scoring requires at least one identified model edge.");
        }

        var claimedSceneEdges = new bool[sceneEdges.Edges.Length];
        var matches = new List<SurfaceEdgeCoverageMatch>();
        var maximumDistanceSquared = maximumCorrespondenceDistance
            * maximumCorrespondenceDistance;
        var squaredErrorSum = 0.0;
        foreach (var modelEdge in modelEdges.Edges)
        {
            var transformedAnchor = pose.TransformPoint(modelEdge.Anchor);
            var bestSceneOrder = -1;
            var bestDistanceSquared = double.PositiveInfinity;
            foreach (var sceneEdge in sceneEdges.Edges)
            {
                if (claimedSceneEdges[sceneEdge.Order])
                {
                    continue;
                }

                var distanceSquared = DistanceSquared(
                    transformedAnchor,
                    sceneEdge.Anchor);
                if (distanceSquared < bestDistanceSquared
                    || distanceSquared == bestDistanceSquared
                    && sceneEdge.Order < bestSceneOrder)
                {
                    bestDistanceSquared = distanceSquared;
                    bestSceneOrder = sceneEdge.Order;
                }
            }

            if (bestSceneOrder < 0
                || bestDistanceSquared > maximumDistanceSquared)
            {
                continue;
            }

            claimedSceneEdges[bestSceneOrder] = true;
            squaredErrorSum += bestDistanceSquared;
            matches.Add(new SurfaceEdgeCoverageMatch(
                modelEdge.Order,
                bestSceneOrder,
                Math.Sqrt(bestDistanceSquared)));
        }

        var matchedCount = matches.Count;
        var coverageRatio = matchedCount / (double)modelEdges.Edges.Length;
        double? inlierRmse = matchedCount == 0
            ? null
            : Math.Sqrt(squaredErrorSum / matchedCount);
        var evidence =
            $"semantics={SurfaceEdgeScoreComponent.CurrentSemantics};"
            + $"matched={matchedCount}/{modelEdges.Edges.Length};"
            + $"scene={sceneEdges.Edges.Length};"
            + $"coverage={coverageRatio.ToString("G17", CultureInfo.InvariantCulture)};"
            + $"rmse={(inlierRmse.HasValue ? inlierRmse.Value.ToString("G17", CultureInfo.InvariantCulture) : "unavailable")};"
            + $"maximumDistance={maximumCorrespondenceDistance.ToString("G17", CultureInfo.InvariantCulture)}";
        var edgeScore = new SurfaceEdgeScoreComponent(
            SurfaceEdgeScoreComponent.CurrentSemantics,
            modelEdges.Edges.Length,
            sceneEdges.Edges.Length,
            matchedCount,
            modelEdges.Edges.Length - matchedCount,
            coverageRatio,
            inlierRmse,
            maximumCorrespondenceDistance,
            matches.ToArray(),
            evidence);
        return SurfaceAndEdgeMatchScoreArtifact.Create(
            execution,
            modelEdges,
            sceneEdges,
            edgeScore);
    }

    private static void RequireValid(
        SurfaceMatchExecutionArtifact execution,
        ModelSurfaceEdgeArtifact modelEdges,
        SceneSurfaceEdgeArtifact sceneEdges)
    {
        if (!SurfaceMatchExecutionArtifactValidator.Inspect(execution).IsValid
            || !SurfaceEdgeArtifactValidator.Inspect(modelEdges).IsValid
            || !SurfaceEdgeArtifactValidator.Inspect(sceneEdges).IsValid)
        {
            throw new InvalidDataException(
                "Edge scoring requires valid identified execution and edge artifacts.");
        }

        var pose = execution.PoseResult.Pose;
        if (!string.Equals(
                execution.ModelContentSha256,
                modelEdges.ModelContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                execution.SceneContentSha256,
                sceneEdges.SceneContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(modelEdges.Unit, sceneEdges.Unit, StringComparison.Ordinal)
            || pose is not null
                && (!string.Equals(modelEdges.Unit, pose.Unit, StringComparison.Ordinal)
                    || !string.Equals(modelEdges.FrameId, pose.SourceFrameId, StringComparison.Ordinal)
                    || !string.Equals(sceneEdges.FrameId, pose.TargetFrameId, StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Edge scoring input identities, units, or model-to-scene frames do not agree.");
        }
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

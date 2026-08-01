using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using NoahEdgeAnchor = Lib.ThreeD.FeatureExtraction.SurfaceEdgeAnchorSample;
using NoahEdgeCoverageResult = Lib.ThreeD.FeatureExtraction.DeterministicSurfaceEdgeCoverageResult;
using NoahEdgeCoverageTool = Lib.ThreeD.FeatureExtraction.DeterministicSurfaceEdgeCoverageTool;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict product adapter for decision-free positional edge coverage at an
/// already-identified surface pose. Library-Noah owns the matching kernel.
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

        var noahResult = new NoahEdgeCoverageTool().Execute(
            modelEdges.Edges
                .Select(edge => new NoahEdgeAnchor(
                    edge.Order,
                    LibraryNoahSurfaceMatching.Point(edge.Anchor)))
                .ToArray(),
            sceneEdges.Edges
                .Select(edge => new NoahEdgeAnchor(
                    edge.Order,
                    LibraryNoahSurfaceMatching.Point(edge.Anchor)))
                .ToArray(),
            LibraryNoahSurfaceMatching.Pose(pose),
            maximumCorrespondenceDistance);
        if (!noahResult.Success)
        {
            throw new InvalidDataException(noahResult.Message);
        }

        if (!string.Equals(
                NoahEdgeCoverageResult.Semantics,
                SurfaceEdgeScoreComponent.CurrentSemantics,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Library-Noah edge coverage semantics do not match the Studio contract.");
        }

        var matches = noahResult.Matches
            .Select(match => new SurfaceEdgeCoverageMatch(
                match.ModelEdgeOrder,
                match.SceneEdgeOrder,
                match.Distance))
            .ToArray();
        var matchedCount = noahResult.MatchedModelEdgeCount;
        var coverageRatio = noahResult.CoverageRatio;
        double? inlierRmse = noahResult.HasInlierRmse
            ? noahResult.InlierRmse
            : null;
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
            matches,
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
}

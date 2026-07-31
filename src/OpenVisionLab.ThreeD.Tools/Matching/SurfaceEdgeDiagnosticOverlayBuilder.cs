using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Builds deterministic display geometry from identified model/scene edge
/// evidence at an already identified pose. It never infers acquisition
/// direction and never changes matching or acceptance.
/// </summary>
public static class SurfaceEdgeDiagnosticOverlayBuilder
{
    public static SurfaceEdgeDiagnosticOverlayArtifact Build(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        ModelSurfaceEdgeArtifact modelEdges,
        SceneSurfaceEdgeArtifact sceneEdges,
        SurfaceAndEdgeMatchScoreArtifact score)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(modelEdges);
        ArgumentNullException.ThrowIfNull(sceneEdges);
        ArgumentNullException.ThrowIfNull(score);
        if (!SurfaceModelArtifactValidator.Inspect(model).IsValid
            || !PreparedSceneArtifactValidator.Inspect(scene).IsValid
            || !SurfaceMatchExecutionArtifactValidator.Inspect(execution).IsValid
            || !SurfaceEdgeArtifactValidator.Inspect(modelEdges).IsValid
            || !SurfaceEdgeArtifactValidator.Inspect(sceneEdges).IsValid
            || !SurfaceEdgeArtifactValidator.Inspect(score, execution).IsValid
            || execution.PoseResult.Pose is not { } pose
            || execution.ModelContentSha256 != model.ContentSha256
            || execution.SceneContentSha256 != scene.ContentSha256
            || modelEdges.ModelContentSha256 != model.ContentSha256
            || sceneEdges.SceneContentSha256 != scene.ContentSha256
            || score.ModelEdgeContentSha256 != modelEdges.ContentSha256
            || score.SceneEdgeContentSha256 != sceneEdges.ContentSha256)
        {
            throw new InvalidDataException(
                "Surface-edge diagnostic overlay requires one exact identified model, scene, pose, edge set, and score chain.");
        }

        var matchesByModel = score.EdgeScore.Matches.ToDictionary(
            match => match.ModelEdgeOrder);
        var matchesByScene = score.EdgeScore.Matches.ToDictionary(
            match => match.SceneEdgeOrder);
        var transformedModel = modelEdges.Edges
            .OrderBy(edge => edge.Order)
            .Select(edge =>
            {
                var direction = Normalize(Subtract(
                    edge.SecondPosition,
                    edge.FirstPosition));
                var declaredNormal = Normalize(Add(
                    model.Normals[edge.FirstPointIndex],
                    model.Normals[edge.SecondPointIndex]));
                var matched = matchesByModel.TryGetValue(
                    edge.Order,
                    out var match);
                return new SurfaceEdgeModelDiagnosticSegment(
                    edge.Order,
                    pose.TransformPoint(edge.FirstPosition),
                    pose.TransformPoint(edge.SecondPosition),
                    pose.TransformPoint(edge.Anchor),
                    Normalize(pose.TransformDirection(direction)),
                    Normalize(pose.TransformDirection(declaredNormal)),
                    edge.Kind,
                    matched,
                    matched ? match!.SceneEdgeOrder : null);
            })
            .ToArray();
        var retainedScene = sceneEdges.Edges
            .OrderBy(edge => edge.Order)
            .Select(edge =>
            {
                var matched = matchesByScene.TryGetValue(
                    edge.Order,
                    out var match);
                return new SurfaceEdgeSceneDiagnosticSegment(
                    edge.Order,
                    edge.FirstPosition,
                    edge.SecondPosition,
                    edge.Anchor,
                    Normalize(Subtract(
                        edge.SecondPosition,
                        edge.FirstPosition)),
                    edge.Axis,
                    matched,
                    matched ? match!.ModelEdgeOrder : null);
            })
            .ToArray();

        var artifact = new SurfaceEdgeDiagnosticOverlayArtifact(
            SurfaceEdgeDiagnosticOverlayArtifact.CurrentSchemaVersion,
            SurfaceEdgeDiagnosticOverlayArtifact.CurrentSemantics,
            execution.ContentSha256,
            model.ContentSha256,
            scene.ContentSha256,
            modelEdges.ContentSha256,
            sceneEdges.ContentSha256,
            score.ContentSha256,
            model.Unit,
            pose.TargetFrameId,
            transformedModel,
            retainedScene,
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = SurfaceEdgeDiagnosticOverlayArtifact
                .CalculateContentSha256(artifact)
        };
        var validity =
            SurfaceEdgeDiagnosticOverlayArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface-edge diagnostic overlay is invalid: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    private static SurfaceModelPoint3 Add(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(first.X + second.X, first.Y + second.Y, first.Z + second.Z);

    private static SurfaceModelPoint3 Subtract(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        new(first.X - second.X, first.Y - second.Y, first.Z - second.Z);

    private static SurfaceModelPoint3 Normalize(SurfaceModelPoint3 value)
    {
        var length = Math.Sqrt(
            value.X * value.X + value.Y * value.Y + value.Z * value.Z);
        if (!double.IsFinite(length) || length <= 1e-12)
        {
            throw new InvalidDataException(
                "Surface-edge diagnostic direction must be finite and non-zero.");
        }

        return new SurfaceModelPoint3(
            value.X / length,
            value.Y / length,
            value.Z / length);
    }
}

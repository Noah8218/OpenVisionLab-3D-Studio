using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Prepares immutable, WPF-neutral geometry for the Surface Match overlay.
/// Validation and source-to-display mapping are kept out of the OpenGL View;
/// this owner never touches a control, ViewModel, or rendering context.
/// </summary>
internal static class SurfaceMatchDisplayPreparation
{
    public static SurfaceMatchDisplayPreparationResult Prepare(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchScoreArtifact? edgeScore,
        SurfaceEdgeDiagnosticOverlayArtifact? edgeDiagnosticOverlay,
        SurfaceEdgeAcquisitionDirectionArtifact? acquisitionDirectionOrientation)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(execution);
        var validity = SurfaceMatchExecutionArtifactValidator.Inspect(execution);
        if (!validity.IsValid
            || execution.Overlay is not { } overlay
            || execution.PoseResult.Pose is not { } pose
            || !string.Equals(
                model.ContentSha256,
                execution.ModelContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                scene.ContentSha256,
                execution.SceneContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Viewer surface-match evidence is invalid, unidentified, or linked to different inputs.");
        }

        var allReferencePoints = overlay.TransformedPoints
            .Concat(scene.Samples.Select(sample => sample.Position))
            .ToArray();
        var displayFrame = SurfaceMatchDisplayFrame.Create(allReferencePoints);
        var overlayPositions = overlay.TransformedPoints
            .Select(displayFrame.Map)
            .ToArray();
        var scenePositions = scene.Samples
            .OrderBy(sample => sample.Order)
            .Select(sample => displayFrame.Map(sample.Position))
            .ToArray();
        var correspondences = execution.PoseResult.Coverage.Matches
            .Select(match =>
            {
                var modelSample = model.Samples.Single(sample =>
                    sample.Order == match.ModelSampleOrder);
                var sceneSample = scene.Samples.Single(sample =>
                    sample.Order == match.SceneSampleOrder);
                return (
                    displayFrame.Map(pose.TransformPoint(modelSample.Position)),
                    displayFrame.Map(sceneSample.Position));
            })
            .ToArray();

        var edgeModelSegments = default(SurfaceEdgeModelRenderSegment[]);
        var edgeSceneSegments = default(SurfaceEdgeSceneRenderSegment[]);
        var acquisitionDirectionMarker = default((Vector3 Start, Vector3 End)?);
        if (edgeDiagnosticOverlay is not null)
        {
            var edgeValidity =
                SurfaceEdgeDiagnosticOverlayArtifactValidator.Inspect(
                    edgeDiagnosticOverlay);
            if (!edgeValidity.IsValid
                || edgeScore is null
                || edgeDiagnosticOverlay.SurfaceMatchExecutionContentSha256
                    != execution.ContentSha256
                || edgeDiagnosticOverlay.ModelContentSha256
                    != model.ContentSha256
                || edgeDiagnosticOverlay.SceneContentSha256
                    != scene.ContentSha256
                || edgeDiagnosticOverlay.ScoreContentSha256
                    != edgeScore.ContentSha256)
            {
                throw new InvalidDataException(
                    "Viewer edge diagnostic overlay is invalid or linked to different evidence.");
            }

            if (acquisitionDirectionOrientation is not null
                && !SurfaceEdgeAcquisitionDirectionArtifactValidator
                    .Inspect(acquisitionDirectionOrientation, edgeDiagnosticOverlay).IsValid)
            {
                throw new InvalidDataException(
                    "Viewer acquisition-direction orientation is invalid or linked to different edge evidence.");
            }

            var orientationByOrder = acquisitionDirectionOrientation?.Items
                .ToDictionary(item => item.ModelEdgeOrder);
            edgeModelSegments = edgeDiagnosticOverlay.ModelSegments
                .Select(segment => new SurfaceEdgeModelRenderSegment(
                    displayFrame.Map(segment.FirstPosition),
                    displayFrame.Map(segment.SecondPosition),
                    displayFrame.Map(segment.Anchor),
                    displayFrame.MapDirectionEnd(
                        segment.Anchor,
                        segment.DeclaredNormal,
                        0.62f),
                    segment.IsMatched,
                    orientationByOrder is not null
                        && orientationByOrder.TryGetValue(
                            segment.ModelEdgeOrder,
                            out var item)
                        ? item.Orientation
                        : null))
                .ToArray();
            edgeSceneSegments = edgeDiagnosticOverlay.SceneSegments
                .Select(segment => new SurfaceEdgeSceneRenderSegment(
                    displayFrame.Map(segment.FirstPosition),
                    displayFrame.Map(segment.SecondPosition),
                    segment.IsMatched))
                .ToArray();
            if (acquisitionDirectionOrientation is not null
                && edgeDiagnosticOverlay.ModelSegments.FirstOrDefault() is { } firstSegment)
            {
                acquisitionDirectionMarker = (
                    displayFrame.Map(firstSegment.Anchor),
                    displayFrame.MapDirectionEnd(
                        firstSegment.Anchor,
                        acquisitionDirectionOrientation.NormalizedSensorToSceneDirection,
                        1.0f));
            }
        }

        return new SurfaceMatchDisplayPreparationResult(
            displayFrame,
            overlayPositions,
            overlay.Triangles.ToArray(),
            scenePositions,
            correspondences,
            edgeModelSegments,
            edgeSceneSegments,
            acquisitionDirectionMarker);
    }
}
internal sealed record SurfaceMatchDisplayPreparationResult(
    SurfaceMatchDisplayFrame DisplayFrame,
    Vector3[] OverlayPositions,
    SurfaceModelTriangle[] OverlayTriangles,
    Vector3[] ScenePositions,
    (Vector3 Model, Vector3 Scene)[] Correspondences,
    SurfaceEdgeModelRenderSegment[]? EdgeModelSegments,
    SurfaceEdgeSceneRenderSegment[]? EdgeSceneSegments,
    (Vector3 Start, Vector3 End)? AcquisitionDirectionMarker);

internal readonly record struct SurfaceEdgeModelRenderSegment(
    Vector3 First,
    Vector3 Second,
    Vector3 Anchor,
    Vector3 NormalEnd,
    bool IsMatched,
    SurfaceEdgeAcquisitionOrientation? Orientation);

internal readonly record struct SurfaceEdgeSceneRenderSegment(
    Vector3 First,
    Vector3 Second,
    bool IsMatched);

internal readonly record struct SurfaceMatchDisplayFrame(
    double CenterX,
    double CenterY,
    double CenterZ,
    double Scale)
{
    public static SurfaceMatchDisplayFrame Create(
        IReadOnlyList<SurfaceModelPoint3> points)
    {
        if (points.Count == 0)
        {
            throw new InvalidDataException(
                "Surface match display requires finite geometry.");
        }

        var minimumX = points.Min(point => point.X);
        var maximumX = points.Max(point => point.X);
        var minimumY = points.Min(point => point.Y);
        var maximumY = points.Max(point => point.Y);
        var minimumZ = points.Min(point => point.Z);
        var maximumZ = points.Max(point => point.Z);
        var maximumSpan = Math.Max(
            1e-12,
            Math.Max(
                maximumX - minimumX,
                Math.Max(
                    maximumY - minimumY,
                    maximumZ - minimumZ)));
        return new SurfaceMatchDisplayFrame(
            (minimumX + maximumX) * 0.5,
            (minimumY + maximumY) * 0.5,
            (minimumZ + maximumZ) * 0.5,
            C3DHeightGrid.ViewerHorizontalSpan
            * 0.62
            / maximumSpan);
    }

    public Vector3 Map(SurfaceModelPoint3 point) =>
        new(
            (float)((point.X - CenterX) * Scale),
            (float)((point.Y - CenterY) * Scale),
            (float)((point.Z - CenterZ) * Scale));

    public Vector3 MapDirectionEnd(
        SurfaceModelPoint3 anchor,
        SurfaceModelPoint3 direction,
        float displayLength)
    {
        var mappedAnchor = Map(anchor);
        return new Vector3(
            mappedAnchor.X + (float)direction.X * displayLength,
            mappedAnchor.Y + (float)direction.Y * displayLength,
            mappedAnchor.Z + (float)direction.Z * displayLength);
    }
}

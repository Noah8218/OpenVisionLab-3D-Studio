using System.Numerics;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private const int SurfaceMatchMaximumRenderedTriangles = 100000;
    private const int SurfaceMatchMaximumRenderedSceneSamples = 100000;
    private SurfaceMatchExecutionArtifact? surfaceMatchRenderExecution;
    private Vector3[]? surfaceMatchOverlayPositions;
    private SurfaceModelTriangle[]? surfaceMatchOverlayTriangles;
    private Vector3[]? surfaceMatchScenePositions;
    private (Vector3 Model, Vector3 Scene)[]? surfaceMatchCorrespondences;
    private SurfaceEdgeModelRenderSegment[]? surfaceEdgeModelSegments;
    private SurfaceEdgeSceneRenderSegment[]? surfaceEdgeSceneSegments;
    private SurfaceMatchDisplayFrame surfaceMatchDisplayFrame;

    public void ShowWorkbenchSurfaceMatch(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact? assessment = null,
        SurfaceMatchRuntimeReport? runtime = null,
        SurfaceAndEdgeMatchScoreArtifact? edgeScore = null,
        SurfaceEdgeDiagnosticOverlayArtifact? edgeDiagnosticOverlay = null,
        SurfaceAndEdgeMatchAssessmentArtifact? edgeAssessment = null,
        SurfaceMatchFalsePositiveReviewArtifact? falsePositiveReview = null)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(execution);
        var validity =
            SurfaceMatchExecutionArtifactValidator.Inspect(execution);
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
        surfaceMatchDisplayFrame =
            SurfaceMatchDisplayFrame.Create(allReferencePoints);
        surfaceMatchOverlayPositions = overlay.TransformedPoints
            .Select(surfaceMatchDisplayFrame.Map)
            .ToArray();
        surfaceMatchOverlayTriangles = overlay.Triangles.ToArray();
        surfaceMatchScenePositions = scene.Samples
            .OrderBy(sample => sample.Order)
            .Select(sample =>
                surfaceMatchDisplayFrame.Map(sample.Position))
            .ToArray();
        surfaceMatchCorrespondences = execution.PoseResult.Coverage.Matches
            .Select(match =>
            {
                var modelSample = model.Samples.Single(sample =>
                    sample.Order == match.ModelSampleOrder);
                var sceneSample = scene.Samples.Single(sample =>
                    sample.Order == match.SceneSampleOrder);
                return (
                    surfaceMatchDisplayFrame.Map(
                        pose.TransformPoint(modelSample.Position)),
                    surfaceMatchDisplayFrame.Map(sceneSample.Position));
            })
            .ToArray();
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

            surfaceEdgeModelSegments = edgeDiagnosticOverlay.ModelSegments
                .Select(segment => new SurfaceEdgeModelRenderSegment(
                    surfaceMatchDisplayFrame.Map(segment.FirstPosition),
                    surfaceMatchDisplayFrame.Map(segment.SecondPosition),
                    surfaceMatchDisplayFrame.Map(segment.Anchor),
                    surfaceMatchDisplayFrame.MapDirectionEnd(
                        segment.Anchor,
                        segment.DeclaredNormal,
                        0.62f),
                    segment.IsMatched))
                .ToArray();
            surfaceEdgeSceneSegments = edgeDiagnosticOverlay.SceneSegments
                .Select(segment => new SurfaceEdgeSceneRenderSegment(
                    surfaceMatchDisplayFrame.Map(segment.FirstPosition),
                    surfaceMatchDisplayFrame.Map(segment.SecondPosition),
                    segment.IsMatched))
                .ToArray();
        }
        else
        {
            surfaceEdgeModelSegments = null;
            surfaceEdgeSceneSegments = null;
        }
        surfaceMatchRenderExecution = execution;

        viewModel.CubeVisible = false;
        viewModel.PointCloudVisible = false;
        viewModel.C3DSampleVisible = false;
        viewModel.GlbSampleVisible = false;
        viewModel.LazSampleVisible = false;
        viewModel.CameraTargetX = 0.0;
        viewModel.CameraTargetY = 0.0;
        viewModel.CameraTargetZ = 0.0;
        viewModel.CameraDistance = 9.2;
        viewModel.SetWorkbenchSurfaceMatch(
            execution,
            assessment,
            runtime,
            edgeScore,
            edgeDiagnosticOverlay,
            edgeAssessment,
            falsePositiveReview);
        RenderNow();
    }

    public void ClearWorkbenchSurfaceMatch()
    {
        surfaceMatchRenderExecution = null;
        surfaceMatchOverlayPositions = null;
        surfaceMatchOverlayTriangles = null;
        surfaceMatchScenePositions = null;
        surfaceMatchCorrespondences = null;
        surfaceEdgeModelSegments = null;
        surfaceEdgeSceneSegments = null;
        surfaceMatchDisplayFrame = default;
        viewModel.ClearWorkbenchSurfaceMatch();
        RenderNow();
    }

    private void DrawWorkbenchSurfaceMatch(OpenGL gl)
    {
        var execution = surfaceMatchRenderExecution;
        var overlayPositions = surfaceMatchOverlayPositions;
        var triangles = surfaceMatchOverlayTriangles;
        var scenePositions = surfaceMatchScenePositions;
        if (execution?.Overlay is null
            || overlayPositions is null
            || triangles is null
            || scenePositions is null)
        {
            return;
        }

        var sceneStride = Math.Max(
            1,
            (int)Math.Ceiling(
                scenePositions.Length
                / (double)SurfaceMatchMaximumRenderedSceneSamples));
        gl.PointSize(7.0f);
        gl.Color(0.62, 0.70, 0.82);
        gl.Begin(OpenGL.GL_POINTS);
        for (var index = 0;
             index < scenePositions.Length;
             index += sceneStride)
        {
            var point = scenePositions[index];
            gl.Vertex(point.X, point.Y, point.Z);
        }

        gl.End();

        if (surfaceMatchCorrespondences is { Length: > 0 } correspondences)
        {
            gl.LineWidth(1.5f);
            gl.Color(1.0, 0.72, 0.10);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var correspondence in correspondences)
            {
                gl.Vertex(
                    correspondence.Model.X,
                    correspondence.Model.Y,
                    correspondence.Model.Z);
                gl.Vertex(
                    correspondence.Scene.X,
                    correspondence.Scene.Y,
                    correspondence.Scene.Z);
            }

            gl.End();
        }

        var triangleStride = Math.Max(
            1,
            (int)Math.Ceiling(
                triangles.Length
                / (double)SurfaceMatchMaximumRenderedTriangles));
        gl.LineWidth(2.4f);
        gl.Color(0.10, 0.90, 0.82);
        gl.Begin(OpenGL.GL_LINES);
        for (var index = 0;
             index < triangles.Length;
             index += triangleStride)
        {
            var triangle = triangles[index];
            DrawSurfaceMatchEdge(
                gl,
                overlayPositions[triangle.FirstPointIndex],
                overlayPositions[triangle.SecondPointIndex]);
            DrawSurfaceMatchEdge(
                gl,
                overlayPositions[triangle.SecondPointIndex],
                overlayPositions[triangle.ThirdPointIndex]);
            DrawSurfaceMatchEdge(
                gl,
                overlayPositions[triangle.ThirdPointIndex],
                overlayPositions[triangle.FirstPointIndex]);
        }

        gl.End();
        gl.PointSize(5.0f);
        gl.Color(0.45, 1.0, 0.92);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var point in overlayPositions)
        {
            gl.Vertex(point.X, point.Y, point.Z);
        }

        gl.End();

        if (surfaceEdgeSceneSegments is { Length: > 0 } sceneEdges)
        {
            gl.LineWidth(3.4f);
            gl.Color(0.98, 0.72, 0.22);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var edge in sceneEdges)
            {
                DrawSurfaceMatchEdge(gl, edge.First, edge.Second);
            }

            gl.End();
        }

        if (surfaceEdgeModelSegments is { Length: > 0 } modelEdges)
        {
            gl.LineWidth(4.6f);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var edge in modelEdges)
            {
                if (edge.IsMatched)
                {
                    gl.Color(0.20, 0.96, 0.62);
                }
                else
                {
                    gl.Color(1.0, 0.34, 0.32);
                }

                DrawSurfaceMatchEdge(gl, edge.First, edge.Second);
            }

            gl.End();
            gl.PointSize(8.0f);
            gl.Begin(OpenGL.GL_POINTS);
            foreach (var edge in modelEdges)
            {
                if (edge.IsMatched)
                {
                    gl.Color(0.20, 0.96, 0.62);
                }
                else
                {
                    gl.Color(1.0, 0.34, 0.32);
                }

                gl.Vertex(edge.Second.X, edge.Second.Y, edge.Second.Z);
            }

            gl.End();
            gl.LineWidth(2.2f);
            gl.Color(0.78, 0.48, 1.0);
            gl.Begin(OpenGL.GL_LINES);
            foreach (var edge in modelEdges)
            {
                DrawSurfaceMatchEdge(gl, edge.Anchor, edge.NormalEnd);
            }

            gl.End();
        }
    }

    private static void DrawSurfaceMatchEdge(
        OpenGL gl,
        Vector3 first,
        Vector3 second)
    {
        gl.Vertex(first.X, first.Y, first.Z);
        gl.Vertex(second.X, second.Y, second.Z);
    }

    private readonly record struct SurfaceEdgeModelRenderSegment(
        Vector3 First,
        Vector3 Second,
        Vector3 Anchor,
        Vector3 NormalEnd,
        bool IsMatched);

    private readonly record struct SurfaceEdgeSceneRenderSegment(
        Vector3 First,
        Vector3 Second,
        bool IsMatched);

    private readonly record struct SurfaceMatchDisplayFrame(
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
}

using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Rendering;
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
    private (Vector3 Start, Vector3 End)? surfaceAcquisitionDirectionMarker;
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
        SurfaceMatchFalsePositiveReviewArtifact? falsePositiveReview = null,
        SurfaceEdgeAcquisitionDirectionArtifact? acquisitionDirectionOrientation = null)
    {
        var displayPreparation = SurfaceMatchDisplayPreparation.Prepare(
            model,
            scene,
            execution,
            edgeScore,
            edgeDiagnosticOverlay,
            acquisitionDirectionOrientation);
        surfaceMatchDisplayFrame = displayPreparation.DisplayFrame;
        surfaceMatchOverlayPositions = displayPreparation.OverlayPositions;
        surfaceMatchOverlayTriangles = displayPreparation.OverlayTriangles;
        surfaceMatchScenePositions = displayPreparation.ScenePositions;
        surfaceMatchCorrespondences = displayPreparation.Correspondences;
        surfaceEdgeModelSegments = displayPreparation.EdgeModelSegments;
        surfaceEdgeSceneSegments = displayPreparation.EdgeSceneSegments;
        surfaceAcquisitionDirectionMarker = displayPreparation.AcquisitionDirectionMarker;
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
            falsePositiveReview,
            acquisitionDirectionOrientation);
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
        surfaceAcquisitionDirectionMarker = null;
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
            if (modelEdges.Any(edge => edge.Orientation.HasValue))
            {
                gl.PointSize(9.0f);
                gl.Begin(OpenGL.GL_POINTS);
                foreach (var edge in modelEdges.Where(edge => edge.Orientation.HasValue))
                {
                    SetAcquisitionOrientationColor(gl, edge.Orientation!.Value);
                    gl.Vertex(edge.NormalEnd.X, edge.NormalEnd.Y, edge.NormalEnd.Z);
                }
                gl.End();
            }
        }

        if (surfaceAcquisitionDirectionMarker is { } marker)
        {
            gl.LineWidth(3.0f);
            gl.Color(0.28, 0.78, 1.0);
            gl.Begin(OpenGL.GL_LINES);
            DrawSurfaceMatchEdge(gl, marker.Start, marker.End);
            gl.End();
            gl.PointSize(10.0f);
            gl.Begin(OpenGL.GL_POINTS);
            gl.Vertex(marker.End.X, marker.End.Y, marker.End.Z);
            gl.End();
        }
    }

    private static void SetAcquisitionOrientationColor(
        OpenGL gl,
        SurfaceEdgeAcquisitionOrientation orientation)
    {
        switch (orientation)
        {
            case SurfaceEdgeAcquisitionOrientation.SensorFacing:
                gl.Color(0.24, 0.92, 0.92);
                break;
            case SurfaceEdgeAcquisitionOrientation.AwayFromSensor:
                gl.Color(1.0, 0.30, 0.64);
                break;
            default:
                gl.Color(1.0, 0.72, 0.18);
                break;
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

}

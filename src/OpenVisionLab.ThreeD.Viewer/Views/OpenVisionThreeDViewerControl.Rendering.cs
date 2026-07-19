using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private void ConfigureProjection(OpenGL gl)
    {
        var width = Math.Max(1, (int)Viewport.ActualWidth);
        var height = Math.Max(1, (int)Viewport.ActualHeight);
        gl.Viewport(0, 0, width, height);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.LoadIdentity();
        var farPlane = Math.Max(100.0, viewModel.CameraDistance + 300.0);
        gl.Perspective(FieldOfViewDegrees, (double)width / height, 0.1, farPlane);
    }

    private void ConfigureCamera(OpenGL gl)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        gl.LoadIdentity();
        gl.LookAt(eye.X, eye.Y, eye.Z, target.X, target.Y, target.Z, 0.0, 1.0, 0.0);
    }

    private Vector3 GetCameraPosition()
    {
        return CameraMath.OrbitCameraPosition(
            GetCameraTarget(),
            viewModel.YawDegrees,
            viewModel.PitchDegrees,
            viewModel.CameraDistance);
    }

    private Vector3 GetCameraTarget()
    {
        return CameraMath.CameraTarget(viewModel.CameraTargetX, viewModel.CameraTargetY, viewModel.CameraTargetZ);
    }

    private void DrawGrid(OpenGL gl)
    {
        gl.LineWidth(1.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(0.25, 0.29, 0.36);

        for (var i = -5; i <= 5; i++)
        {
            gl.Vertex(i, -1.02, -5.0);
            gl.Vertex(i, -1.02, 5.0);
            gl.Vertex(-5.0, -1.02, i);
            gl.Vertex(5.0, -1.02, i);
        }

        gl.End();
    }

    private void DrawAxes(OpenGL gl)
    {
        gl.LineWidth(2.0f);
        gl.Begin(OpenGL.GL_LINES);

        gl.Color(0.95, 0.25, 0.25);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(2.3, 0.0, 0.0);

        gl.Color(0.25, 0.85, 0.35);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(0.0, 2.3, 0.0);

        gl.Color(0.35, 0.55, 1.0);
        gl.Vertex(0.0, 0.0, 0.0);
        gl.Vertex(0.0, 0.0, 2.3);

        gl.End();
    }

    private void DrawTwoPointMeasurement(OpenGL gl)
    {
        Vector3 firstPosition;
        Vector3 secondPosition;
        if (twoPointFirst is { } first && twoPointSecond is { } second)
        {
            firstPosition = TransformC3DPosition(first.Position);
            secondPosition = TransformC3DPosition(second.Position);
        }
        else if (lazTwoPointFirst is { } lazFirst && lazTwoPointSecond is { } lazSecond)
        {
            firstPosition = MapLazPosition(lazFirst.Position);
            secondPosition = MapLazPosition(lazSecond.Position);
        }
        else if (importedMeshTwoPointFirst is { } importedMeshFirst && importedMeshTwoPointSecond is { } importedMeshSecond)
        {
            firstPosition = importedMeshFirst;
            secondPosition = importedMeshSecond;
        }
        else
        {
            return;
        }

        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);

        gl.Color(1.0, 0.72, 0.10);
        gl.Vertex(firstPosition.X, firstPosition.Y, firstPosition.Z);
        gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);

        gl.Color(0.20, 0.95, 0.45);
        gl.Vertex(secondPosition.X, firstPosition.Y, secondPosition.Z);
        gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);

        gl.End();

        gl.PointSize(8.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 1.0, 1.0);
        gl.Vertex(firstPosition.X, firstPosition.Y, firstPosition.Z);
        gl.Color(1.0, 0.72, 0.10);
        gl.Vertex(secondPosition.X, secondPosition.Y, secondPosition.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawPlaneReferenceMeasurement(OpenGL gl)
    {
        if (planeReferenceMeasurement is not { } measurement
            || (!viewModel.PlaneReferenceMeasurementVisible && !viewModel.PlaneFlatnessVisible))
        {
            return;
        }

        gl.LineWidth(2.0f);
        gl.Color(0.68, 0.54, 1.0);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(measurement.A.X, measurement.A.Y, measurement.A.Z);
        gl.Vertex(measurement.B.X, measurement.B.Y, measurement.B.Z);
        gl.Vertex(measurement.C.X, measurement.C.Y, measurement.C.Z);
        gl.Vertex(measurement.D.X, measurement.D.Y, measurement.D.Z);
        gl.End();

        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(1.0, 0.90, 0.20);
        gl.Vertex(measurement.Projection.X, measurement.Projection.Y, measurement.Projection.Z);
        gl.Vertex(measurement.Target.X, measurement.Target.Y, measurement.Target.Z);
        gl.End();

        gl.PointSize(8.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.90, 0.20);
        gl.Vertex(measurement.Target.X, measurement.Target.Y, measurement.Target.Z);
        gl.Color(0.68, 0.54, 1.0);
        gl.Vertex(measurement.Projection.X, measurement.Projection.Y, measurement.Projection.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawPlaneFlatnessExtrema(OpenGL gl)
    {
        if (!viewModel.PlaneFlatnessVisible
            || planeFlatnessEvaluation is not { ReferencePlane: not null } evaluation)
        {
            return;
        }
        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(0.20, 0.80, 1.0);
        gl.Vertex(evaluation.MinimumProjection.X, evaluation.MinimumProjection.Y, evaluation.MinimumProjection.Z);
        gl.Vertex(evaluation.MinimumPoint.X, evaluation.MinimumPoint.Y, evaluation.MinimumPoint.Z);
        gl.Color(1.0, 0.32, 0.20);
        gl.Vertex(evaluation.MaximumProjection.X, evaluation.MaximumProjection.Y, evaluation.MaximumProjection.Z);
        gl.Vertex(evaluation.MaximumPoint.X, evaluation.MaximumPoint.Y, evaluation.MaximumPoint.Z);
        gl.End();

        gl.PointSize(9.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(0.20, 0.80, 1.0);
        gl.Vertex(evaluation.MinimumPoint.X, evaluation.MinimumPoint.Y, evaluation.MinimumPoint.Z);
        gl.Color(1.0, 0.32, 0.20);
        gl.Vertex(evaluation.MaximumPoint.X, evaluation.MaximumPoint.Y, evaluation.MaximumPoint.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawRoiStepMeasurement(OpenGL gl)
    {
        if (roiStepLeftBounds is not { } left)
        {
            return;
        }

        DrawRoiBounds(gl, left, 0.20, 0.95, 0.45);

        if (roiStepRightBounds is not { } right
            || roiStepLeftCenter is not { } leftCenter
            || roiStepRightCenter is not { } rightCenter)
        {
            return;
        }

        DrawRoiBounds(gl, right, 1.0, 0.72, 0.10);

        gl.LineWidth(3.0f);
        gl.Begin(OpenGL.GL_LINES);

        gl.Color(1.0, 0.85, 0.20);
        gl.Vertex(leftCenter.X, leftCenter.Y, leftCenter.Z);
        gl.Vertex(rightCenter.X, rightCenter.Y, rightCenter.Z);

        gl.Color(0.20, 0.95, 0.45);
        gl.Vertex(rightCenter.X, leftCenter.Y, rightCenter.Z);
        gl.Vertex(rightCenter.X, rightCenter.Y, rightCenter.Z);

        gl.End();

        gl.PointSize(8.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(0.20, 0.95, 0.45);
        gl.Vertex(leftCenter.X, leftCenter.Y, leftCenter.Z);
        gl.Color(1.0, 0.72, 0.10);
        gl.Vertex(rightCenter.X, rightCenter.Y, rightCenter.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawThicknessRoi(OpenGL gl)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null || !viewModel.ThicknessConfigured)
        {
            return;
        }

        var roi = viewModel.CreateThicknessRecipeStep().Roi;
        if (!IsC3DGridRoiInside(roi, c3dSample))
        {
            return;
        }

        var height = double.IsFinite(viewModel.ThicknessMean) ? viewModel.ThicknessMean : c3dSample.Mean;
        var lastRow = roi.Row + roi.RowCount - 1;
        var lastColumn = roi.Column + roi.ColumnCount - 1;
        var topLeft = CreateC3DGridDisplayPosition(roi.Row, roi.Column, height);
        var topRight = CreateC3DGridDisplayPosition(roi.Row, lastColumn, height);
        var bottomRight = CreateC3DGridDisplayPosition(lastRow, lastColumn, height);
        var bottomLeft = CreateC3DGridDisplayPosition(lastRow, roi.Column, height);

        gl.LineWidth(3.0f);
        gl.Color(0.10, 0.92, 0.92);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(topRight.X, topRight.Y, topRight.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
        gl.End();

        gl.PointSize(7.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.88, 0.16);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private Vector3 CreateC3DGridDisplayPosition(double row, double column, double rawHeight)
    {
        if (c3dSample is null)
        {
            return Vector3.Zero;
        }

        var centerColumn = (c3dSample.Width - 1) / 2.0f;
        var centerRow = (c3dSample.Height - 1) / 2.0f;
        var position = new Vector3(
            (float)((column - centerColumn) * c3dSample.HorizontalScale),
            (float)((rawHeight - c3dSample.Mean) * C3DHeightGrid.ViewerHeightScale),
            (float)((row - centerRow) * c3dSample.HorizontalScale));
        return TransformC3DPosition(position);
    }

    private static bool IsC3DGridRoiInside(C3DGridRoi roi, C3DHeightGrid grid) =>
        roi.Row >= 0
        && roi.Column >= 0
        && roi.RowCount > 0
        && roi.ColumnCount > 0
        && roi.Row <= grid.Height - roi.RowCount
        && roi.Column <= grid.Width - roi.ColumnCount;

    private static void DrawRoiBounds(OpenGL gl, (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) bounds, double red, double green, double blue)
    {
        gl.LineWidth(2.5f);
        gl.Color(red, green, blue);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(bounds.MinX, bounds.MeanY, bounds.MinZ);
        gl.Vertex(bounds.MaxX, bounds.MeanY, bounds.MinZ);
        gl.Vertex(bounds.MaxX, bounds.MeanY, bounds.MaxZ);
        gl.Vertex(bounds.MinX, bounds.MeanY, bounds.MaxZ);
        gl.End();
    }

    private void DrawCube(OpenGL gl)
    {
        gl.Begin(OpenGL.GL_QUADS);

        gl.Color(0.20, 0.62, 0.86);
        Quad(gl, (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1));

        gl.Color(0.14, 0.48, 0.75);
        Quad(gl, (1, -1, -1), (-1, -1, -1), (-1, 1, -1), (1, 1, -1));

        gl.Color(0.95, 0.72, 0.32);
        Quad(gl, (-1, 1, 1), (1, 1, 1), (1, 1, -1), (-1, 1, -1));

        gl.Color(0.78, 0.46, 0.25);
        Quad(gl, (-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1));

        gl.Color(0.45, 0.72, 0.42);
        Quad(gl, (1, -1, 1), (1, -1, -1), (1, 1, -1), (1, 1, 1));

        gl.Color(0.36, 0.60, 0.36);
        Quad(gl, (-1, -1, -1), (-1, -1, 1), (-1, 1, 1), (-1, 1, -1));

        gl.End();

        DrawCubeWire(gl);
    }

    private void DrawCubeWire(OpenGL gl)
    {
        gl.LineWidth(1.5f);
        gl.Color(1.0, 1.0, 1.0);
        gl.Begin(OpenGL.GL_LINES);

        Edge(gl, (-1, -1, -1), (1, -1, -1));
        Edge(gl, (1, -1, -1), (1, -1, 1));
        Edge(gl, (1, -1, 1), (-1, -1, 1));
        Edge(gl, (-1, -1, 1), (-1, -1, -1));
        Edge(gl, (-1, 1, -1), (1, 1, -1));
        Edge(gl, (1, 1, -1), (1, 1, 1));
        Edge(gl, (1, 1, 1), (-1, 1, 1));
        Edge(gl, (-1, 1, 1), (-1, 1, -1));
        Edge(gl, (-1, -1, -1), (-1, 1, -1));
        Edge(gl, (1, -1, -1), (1, 1, -1));
        Edge(gl, (1, -1, 1), (1, 1, 1));
        Edge(gl, (-1, -1, 1), (-1, 1, 1));

        gl.End();
    }

    private void DrawPointCloud(OpenGL gl, IReadOnlyList<HeightGridPoint> points)
    {
        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);

        foreach (var point in points)
        {
            ApplyPointColor(gl, point);
            gl.Vertex(point.Position.X, point.Position.Y, point.Position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawPointCloudFrame(gl);
    }

    private void DrawC3DHeightGrid(OpenGL gl)
    {
        var renderProxy = GetC3DRenderProxy();
        var positions = GetC3DRenderPositions(renderProxy);
        var geometryStyle = viewModel.Display.EffectiveSettings.GeometryStyle;
        gl.Disable(GlTexture2D);

        if (UsesDynamicC3DColor())
        {
            ReleaseC3DDisplayList(gl);
            DrawC3DGeometry(gl, renderProxy, positions, geometryStyle);
            DrawC3DFrame(gl);
            return;
        }

        var displayListKey = new C3DDisplayListKey(
            c3dSample!,
            c3dRenderPositionsTransform,
            geometryStyle,
            viewModel.Display.EffectiveSettings.ColorMap,
            viewModel.PointSize);
        if (c3dDisplayListId == 0 || c3dDisplayListKey != displayListKey)
        {
            ReleaseC3DDisplayList(gl);
            c3dDisplayListId = gl.GenLists(1);
            if (c3dDisplayListId != 0)
            {
                gl.NewList(c3dDisplayListId, OpenGL.GL_COMPILE);
                DrawC3DGeometry(gl, renderProxy, positions, geometryStyle);
                gl.EndList();
                c3dDisplayListKey = displayListKey;
            }
        }

        if (c3dDisplayListId != 0)
        {
            gl.CallList(c3dDisplayListId);
        }
        else
        {
            DrawC3DGeometry(gl, renderProxy, positions, geometryStyle);
        }

        DrawC3DFrame(gl);
    }

    private void DrawC3DGeometry(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        ViewerGeometryStyle geometryStyle)
    {
        switch (geometryStyle)
        {
            case ViewerGeometryStyle.Wireframe when renderProxy.HasSurface:
                DrawC3DEdges(
                    gl,
                    renderProxy,
                    positions,
                    renderProxy.GridEdgeIndices,
                    usePointColors: true);
                break;
            case ViewerGeometryStyle.Surface when renderProxy.HasSurface:
                DrawC3DSurface(gl, renderProxy, positions, offsetForEdges: false);
                break;
            case ViewerGeometryStyle.SurfaceWithEdges when renderProxy.HasSurface:
                DrawC3DSurface(gl, renderProxy, positions, offsetForEdges: true);
                DrawC3DEdges(
                    gl,
                    renderProxy,
                    positions,
                    renderProxy.SurfaceEdgeIndices,
                    usePointColors: false);
                break;
            default:
                DrawC3DPoints(gl, renderProxy, positions);
                break;
        }
    }

    private bool UsesDynamicC3DColor() =>
        viewModel.SelectedColorMode == "Deviation"
        && viewModel.PlaneFlatnessVisible
        && planeFlatnessEvaluation is { ReferencePlane: not null };

    private void ReleaseC3DDisplayList(OpenGL gl)
    {
        if (c3dDisplayListId != 0)
        {
            gl.DeleteLists(c3dDisplayListId, 1);
        }

        c3dDisplayListId = 0;
        c3dDisplayListKey = null;
    }

    private void DrawC3DPoints(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions)
    {
        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);
        for (var index = 0; index < renderProxy.Points.Length; index++)
        {
            var point = renderProxy.Points[index];
            var position = positions[index];
            ApplyC3DColor(gl, point, position);
            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawC3DSurface(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        bool offsetForEdges)
    {
        if (offsetForEdges)
        {
            gl.Enable(OpenGL.GL_POLYGON_OFFSET_FILL);
            gl.PolygonOffset(1.0f, 1.0f);
        }

        gl.Begin(OpenGL.GL_TRIANGLES);
        foreach (var index in renderProxy.TriangleIndices)
        {
            var point = renderProxy.Points[index];
            var position = positions[index];
            ApplyC3DColor(gl, point, position);
            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        if (offsetForEdges)
        {
            gl.Disable(OpenGL.GL_POLYGON_OFFSET_FILL);
        }
    }

    private void DrawC3DEdges(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> edgeIndices,
        bool usePointColors)
    {
        gl.LineWidth(1.0f);
        if (!usePointColors)
        {
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.Color(0.02, 0.05, 0.08, 0.32);
        }

        gl.Begin(OpenGL.GL_LINES);
        foreach (var index in edgeIndices)
        {
            var position = positions[index];
            if (usePointColors)
            {
                ApplyC3DColor(gl, renderProxy.Points[index], position);
            }

            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        if (!usePointColors)
        {
            gl.Disable(OpenGL.GL_BLEND);
        }

        gl.LineWidth(1.0f);
    }

    private void ApplyC3DColor(OpenGL gl, HeightGridPoint point, Vector3 position)
    {
        if (viewModel.SelectedColorMode == "Deviation"
            && viewModel.PlaneFlatnessVisible
            && planeFlatnessEvaluation is { ReferencePlane: not null } flatness)
        {
            ApplyPlaneFlatnessColor(gl, position, flatness);
            return;
        }

        ApplyPointColor(gl, point);
    }

    private C3DHeightGridRenderProxy GetC3DRenderProxy()
    {
        var sample = c3dSample
            ?? throw new InvalidOperationException("C3D display proxy requires a loaded sample.");
        if (!ReferenceEquals(c3dRenderProxySource, sample) || c3dRenderProxy is null)
        {
            c3dRenderProxySource = sample;
            c3dRenderProxy = C3DHeightGridRenderProxy.Create(sample);
            c3dRenderPositions = null;
        }

        return c3dRenderProxy;
    }

    private Vector3[] GetC3DRenderPositions(C3DHeightGridRenderProxy renderProxy)
    {
        var transform = viewModel.C3DModelTransform;
        if (c3dRenderPositions is null || c3dRenderPositionsTransform != transform)
        {
            c3dRenderPositions = new Vector3[renderProxy.Points.Length];
            for (var index = 0; index < renderProxy.Points.Length; index++)
            {
                c3dRenderPositions[index] = ApplyModelTransform(
                    renderProxy.Points[index].Position,
                    transform);
            }

            c3dRenderPositionsTransform = transform;
        }

        return c3dRenderPositions;
    }

    private void InvalidateC3DRenderProxy()
    {
        c3dRenderProxySource = null;
        c3dRenderProxy = null;
        c3dRenderPositions = null;
        c3dDisplayListKey = null;
    }

    private void DrawNominalActualDeviation(OpenGL gl)
    {
        var result = viewModel.NominalActual.PreviewResult!;
        gl.Disable(GlTexture2D);
        gl.PointSize((float)Math.Max(2.0, viewModel.PointSize));
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var sample in result.DisplaySamples)
        {
            var color = GetSignedDeviationColor(
                sample.SignedDeviation,
                result.Input.LowerTolerance,
                result.Input.UpperTolerance);
            gl.Color(color.Red, color.Green, color.Blue);
            gl.Vertex(sample.Position.X, sample.Position.Y, sample.Position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawNominalActualSelectedDeviation(OpenGL gl)
    {
        if (viewModel.NominalActual.SelectedDeviation is not { } sample)
        {
            return;
        }

        gl.Disable(OpenGL.GL_DEPTH_TEST);
        gl.LineWidth(2.0f);
        gl.Color(1.0, 0.75, 0.10);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(sample.Position.X, sample.Position.Y, sample.Position.Z);
        gl.Vertex(sample.ClosestNominalPoint.X, sample.ClosestNominalPoint.Y, sample.ClosestNominalPoint.Z);
        gl.End();

        gl.PointSize(10.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.85, 0.10);
        gl.Vertex(sample.Position.X, sample.Position.Y, sample.Position.Z);
        gl.Color(0.10, 0.90, 0.90);
        gl.Vertex(sample.ClosestNominalPoint.X, sample.ClosestNominalPoint.Y, sample.ClosestNominalPoint.Z);
        gl.End();
        gl.PointSize(1.0f);
        gl.LineWidth(1.0f);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
    }

    private static (double Red, double Green, double Blue) GetSignedDeviationColor(
        double value,
        double lowerTolerance,
        double upperTolerance)
    {
        const double blueRed = 0.145;
        const double blueGreen = 0.388;
        const double blueBlue = 0.922;
        const double redRed = 0.937;
        const double redGreen = 0.267;
        const double redBlue = 0.267;

        if (value <= lowerTolerance)
        {
            return (blueRed, blueGreen, blueBlue);
        }

        if (value >= upperTolerance)
        {
            return (redRed, redGreen, redBlue);
        }

        if (value < 0)
        {
            var ratio = Math.Clamp(value / lowerTolerance, 0.0, 1.0);
            return (
                1.0 + (blueRed - 1.0) * ratio,
                1.0 + (blueGreen - 1.0) * ratio,
                1.0 + (blueBlue - 1.0) * ratio);
        }

        var positiveRatio = Math.Clamp(value / upperTolerance, 0.0, 1.0);
        return (
            1.0 + (redRed - 1.0) * positiveRatio,
            1.0 + (redGreen - 1.0) * positiveRatio,
            1.0 + (redBlue - 1.0) * positiveRatio);
    }

    private void DrawImportedMesh(OpenGL gl)
    {
        var mesh = importedMesh!;
        var triangleStride = GetImportedMeshRenderTriangleStride();
        var geometryStyle = viewModel.Display.EffectiveSettings.GeometryStyle;
        var useTexture = EnsureImportedMeshTexture(gl);
        if (useTexture)
        {
            gl.Enable(GlTexture2D);
            gl.BindTexture(GlTexture2D, importedMeshTextureId);
            gl.Color(1.0, 1.0, 1.0);
        }

        switch (geometryStyle)
        {
            case ViewerGeometryStyle.Points:
                DrawImportedMeshPoints(gl, mesh, triangleStride, useTexture);
                break;
            case ViewerGeometryStyle.Wireframe:
                DrawImportedMeshEdges(gl, mesh, triangleStride, useSourceColor: true, useTexture: useTexture);
                break;
            case ViewerGeometryStyle.Surface:
                DrawImportedMeshSurface(gl, mesh, triangleStride, useTexture, offsetForEdges: false);
                break;
            default:
                DrawImportedMeshSurface(gl, mesh, triangleStride, useTexture, offsetForEdges: true);
                break;
        }

        if (useTexture)
        {
            gl.Disable(GlTexture2D);
        }

        if (geometryStyle == ViewerGeometryStyle.SurfaceWithEdges)
        {
            DrawImportedMeshEdges(gl, mesh, triangleStride, useSourceColor: false, useTexture: false);
        }

        DrawImportedMeshFrame(gl);
        DrawSelectedGlbPoint(gl);
    }

    private void DrawImportedMeshPoints(
        OpenGL gl,
        ImportedMesh mesh,
        int triangleStride,
        bool useTexture)
    {
        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle += triangleStride)
        {
            var offset = triangle * 3;
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset], useTexture);
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset + 1], useTexture);
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset + 2], useTexture);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private static void DrawImportedMeshSurface(
        OpenGL gl,
        ImportedMesh mesh,
        int triangleStride,
        bool useTexture,
        bool offsetForEdges)
    {
        if (offsetForEdges)
        {
            gl.Enable(OpenGL.GL_POLYGON_OFFSET_FILL);
            gl.PolygonOffset(1.0f, 1.0f);
        }

        gl.Begin(OpenGL.GL_TRIANGLES);
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle += triangleStride)
        {
            var offset = triangle * 3;
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset], useTexture);
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset + 1], useTexture);
            DrawImportedMeshVertex(gl, mesh, mesh.Indices[offset + 2], useTexture);
        }

        gl.End();
        if (offsetForEdges)
        {
            gl.Disable(OpenGL.GL_POLYGON_OFFSET_FILL);
        }
    }

    private static void DrawImportedMeshEdges(
        OpenGL gl,
        ImportedMesh mesh,
        int triangleStride,
        bool useSourceColor,
        bool useTexture)
    {
        gl.LineWidth(1.2f);
        if (!useSourceColor)
        {
            gl.Color(1.0, 0.92, 0.78);
        }

        gl.Begin(OpenGL.GL_LINES);
        for (var triangle = 0; triangle < mesh.TriangleCount; triangle += triangleStride)
        {
            var offset = triangle * 3;
            DrawImportedMeshEdge(gl, mesh, mesh.Indices[offset], mesh.Indices[offset + 1], useSourceColor, useTexture);
            DrawImportedMeshEdge(gl, mesh, mesh.Indices[offset + 1], mesh.Indices[offset + 2], useSourceColor, useTexture);
            DrawImportedMeshEdge(gl, mesh, mesh.Indices[offset + 2], mesh.Indices[offset], useSourceColor, useTexture);
        }

        gl.End();
        gl.LineWidth(1.0f);
    }

    private static void DrawImportedMeshEdge(
        OpenGL gl,
        ImportedMesh mesh,
        int firstIndex,
        int secondIndex,
        bool useSourceColor,
        bool useTexture)
    {
        if (useSourceColor)
        {
            DrawImportedMeshVertex(gl, mesh, firstIndex, useTexture);
            DrawImportedMeshVertex(gl, mesh, secondIndex, useTexture);
            return;
        }

        var first = mesh.Positions[firstIndex];
        var second = mesh.Positions[secondIndex];
        gl.Vertex(first.X, first.Y, first.Z);
        gl.Vertex(second.X, second.Y, second.Z);
    }

    private static void DrawImportedMeshVertex(OpenGL gl, ImportedMesh mesh, int index, bool useTexture)
    {
        if (useTexture)
        {
            var uv = mesh.TextureCoordinates[index];
            gl.Color(1.0, 1.0, 1.0);
            gl.TexCoord(uv.X, 1.0f - uv.Y);
        }
        else if (mesh.HasVertexColors)
        {
            var color = mesh.VertexColors[index];
            gl.Color(color.X, color.Y, color.Z);
        }
        else
        {
            gl.Color(0.88, 0.48, 0.22);
        }

        var position = mesh.Positions[index];
        gl.Vertex(position.X, position.Y, position.Z);
    }

    private int GetImportedMeshRenderTriangleStride()
    {
        if (importedMesh is null || importedMesh.TriangleCount <= viewModel.ImportedMeshMaxRenderedTriangles)
        {
            return 1;
        }

        return Math.Max(1, (int)Math.Ceiling((double)importedMesh.TriangleCount / viewModel.ImportedMeshMaxRenderedTriangles));
    }

    private int GetImportedMeshRenderedTriangleCount()
    {
        if (importedMesh is null)
        {
            return 0;
        }

        var stride = GetImportedMeshRenderTriangleStride();
        return (importedMesh.TriangleCount + stride - 1) / stride;
    }

    private void DrawPointCloudFrame(OpenGL gl)
    {
        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(1.0, -1.18, -2.25);
        gl.Vertex(5.4, -1.18, -2.25);
        gl.Vertex(5.4, -1.18, 2.25);
        gl.Vertex(1.0, -1.18, 2.25);
        gl.End();
    }

    private void DrawC3DFrame(OpenGL gl)
    {
        var x = c3dSample!.XHalfExtent;
        var z = c3dSample.ZHalfExtent;
        var a = TransformC3DPosition(new Vector3(-x, 0.0f, -z));
        var b = TransformC3DPosition(new Vector3(x, 0.0f, -z));
        var c = TransformC3DPosition(new Vector3(x, 0.0f, z));
        var d = TransformC3DPosition(new Vector3(-x, 0.0f, z));

        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(a.X, a.Y, a.Z);
        gl.Vertex(b.X, b.Y, b.Z);
        gl.Vertex(c.X, c.Y, c.Z);
        gl.Vertex(d.X, d.Y, d.Z);
        gl.End();
    }

    private void DrawImportedMeshFrame(OpenGL gl)
    {
        var mesh = importedMesh!;
        var min = mesh.Min;
        var max = mesh.Max;

        gl.LineWidth(1.5f);
        gl.Color(0.78, 0.86, 0.98);
        gl.Begin(OpenGL.GL_LINES);
        Edge(gl, (min.X, min.Y, min.Z), (max.X, min.Y, min.Z));
        Edge(gl, (max.X, min.Y, min.Z), (max.X, min.Y, max.Z));
        Edge(gl, (max.X, min.Y, max.Z), (min.X, min.Y, max.Z));
        Edge(gl, (min.X, min.Y, max.Z), (min.X, min.Y, min.Z));
        Edge(gl, (min.X, max.Y, min.Z), (max.X, max.Y, min.Z));
        Edge(gl, (max.X, max.Y, min.Z), (max.X, max.Y, max.Z));
        Edge(gl, (max.X, max.Y, max.Z), (min.X, max.Y, max.Z));
        Edge(gl, (min.X, max.Y, max.Z), (min.X, max.Y, min.Z));
        Edge(gl, (min.X, min.Y, min.Z), (min.X, max.Y, min.Z));
        Edge(gl, (max.X, min.Y, min.Z), (max.X, max.Y, min.Z));
        Edge(gl, (max.X, min.Y, max.Z), (max.X, max.Y, max.Z));
        Edge(gl, (min.X, min.Y, max.Z), (min.X, max.Y, max.Z));
        gl.End();
    }

    private void DrawSelectedGlbPoint(OpenGL gl)
    {
        if (selectedImportedMeshPoint is not { } point)
        {
            return;
        }

        DrawSelectedGlbTriangle(gl);
        DrawSelectedGlbNormal(gl, point);

        gl.PointSize(9.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 1.0, 0.12);
        gl.Vertex(point.X, point.Y, point.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawSelectedGlbTriangle(OpenGL gl)
    {
        if (importedMesh is not { } mesh || selectedImportedMeshTriangleIndex is not { } triangleIndex)
        {
            return;
        }

        var offset = triangleIndex * 3;
        if (offset < 0 || offset + 2 >= mesh.Indices.Length)
        {
            return;
        }

        var firstIndex = mesh.Indices[offset];
        var secondIndex = mesh.Indices[offset + 1];
        var thirdIndex = mesh.Indices[offset + 2];
        if (!ImportedMeshIndexInRange(mesh, firstIndex) || !ImportedMeshIndexInRange(mesh, secondIndex) || !ImportedMeshIndexInRange(mesh, thirdIndex))
        {
            return;
        }

        var first = mesh.Positions[firstIndex];
        var second = mesh.Positions[secondIndex];
        var third = mesh.Positions[thirdIndex];

        gl.LineWidth(3.0f);
        gl.Color(1.0, 0.95, 0.10);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(first.X, first.Y, first.Z);
        gl.Vertex(second.X, second.Y, second.Z);
        gl.Vertex(third.X, third.Y, third.Z);
        gl.End();
        gl.LineWidth(1.0f);
    }

    private void DrawSelectedGlbNormal(OpenGL gl, Vector3 point)
    {
        if (selectedImportedMeshSurfaceNormal is not { } normal || normal.LengthSquared() <= 0.0f)
        {
            return;
        }

        var end = point + Vector3.Normalize(normal) * GetImportedMeshSurfaceOverlayScale();
        gl.LineWidth(3.0f);
        gl.Color(0.10, 0.95, 1.0);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(point.X, point.Y, point.Z);
        gl.Vertex(end.X, end.Y, end.Z);
        gl.End();
        gl.LineWidth(1.0f);
    }

    private void DrawLazMetadata(OpenGL gl)
    {
        var points = GetLazBoundsCorners(lazSample!);

        gl.LineWidth(1.8f);
        gl.Color(0.94, 0.82, 0.24);
        gl.Begin(OpenGL.GL_LINES);
        DrawBoxEdges(gl, points);
        gl.End();

        gl.PointSize(5.0f);
        gl.Color(0.35, 0.86, 1.0);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var point in points)
        {
            gl.Vertex(point.X, point.Y, point.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
    }

    private void DrawLazPointCloud(OpenGL gl)
    {
        var pointCloud = lazPointCloud!;

        gl.PointSize((float)viewModel.PointSize);
        gl.Begin(OpenGL.GL_POINTS);
        foreach (var point in pointCloud.SampledPoints)
        {
            ApplyLazPointColor(gl, point);
            var position = MapLazPosition(point.Position);
            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawLazMetadata(gl);
        DrawSelectedLazPoint(gl);
    }

    private Vector3[] GetLazBoundsCorners(LazPointCloudMetadata metadata)
    {
        return
        [
            MapLazPosition(metadata.MinX, metadata.MinY, metadata.MinZ),
            MapLazPosition(metadata.MaxX, metadata.MinY, metadata.MinZ),
            MapLazPosition(metadata.MaxX, metadata.MaxY, metadata.MinZ),
            MapLazPosition(metadata.MinX, metadata.MaxY, metadata.MinZ),
            MapLazPosition(metadata.MinX, metadata.MinY, metadata.MaxZ),
            MapLazPosition(metadata.MaxX, metadata.MinY, metadata.MaxZ),
            MapLazPosition(metadata.MaxX, metadata.MaxY, metadata.MaxZ),
            MapLazPosition(metadata.MinX, metadata.MaxY, metadata.MaxZ)
        ];
    }

    private Vector3 MapLazPosition(Vector3 source) =>
        MapLazPosition(source.X, source.Y, source.Z);

    private Vector3 MapLazPosition(double x, double y, double z) =>
        new((float)(x - lazViewerOrigin.X), (float)(z - lazViewerOrigin.Z), (float)(y - lazViewerOrigin.Y));

    private void ApplyLazPointColor(OpenGL gl, LazPointCloudPoint point)
    {
        static double Normalize(ushort value) => value > 255 ? value / 65535.0 : value / 255.0;

        var (r, g, b) = viewModel.SelectedColorMode switch
        {
            "Solid" => (0.72, 0.84, 1.0),
            "Height" => C3DPointMapPalette.Height(NormalizeLazHeight(point.Position.Z)),
            _ => (Normalize(point.Red), Normalize(point.Green), Normalize(point.Blue))
        };

        gl.Color(r, g, b);
    }

    private double NormalizeLazHeight(float sourceZ)
    {
        if (lazSample is null || Math.Abs(lazSample.MaxZ - lazSample.MinZ) < 0.000001)
        {
            return 0.5;
        }

        return (sourceZ - lazSample.MinZ) / (lazSample.MaxZ - lazSample.MinZ);
    }

    private void DrawSelectedLazPoint(OpenGL gl)
    {
        if (selectedLazPoint is not { } point)
        {
            return;
        }

        var position = MapLazPosition(point.Position);
        gl.PointSize((float)Math.Max(8.0, viewModel.PointSize + 6.0));
        gl.Color(1.0, 0.95, 0.10);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Vertex(position.X, position.Y, position.Z);
        gl.End();
        gl.PointSize(1.0f);

        const float markerSize = 2.0f;
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(position.X - markerSize, position.Y, position.Z);
        gl.Vertex(position.X + markerSize, position.Y, position.Z);
        gl.Vertex(position.X, position.Y - markerSize, position.Z);
        gl.Vertex(position.X, position.Y + markerSize, position.Z);
        gl.Vertex(position.X, position.Y, position.Z - markerSize);
        gl.Vertex(position.X, position.Y, position.Z + markerSize);
        gl.End();
    }

    private static void DrawBoxEdges(OpenGL gl, IReadOnlyList<Vector3> points)
    {
        Edge(points[0], points[1]);
        Edge(points[1], points[2]);
        Edge(points[2], points[3]);
        Edge(points[3], points[0]);
        Edge(points[4], points[5]);
        Edge(points[5], points[6]);
        Edge(points[6], points[7]);
        Edge(points[7], points[4]);
        Edge(points[0], points[4]);
        Edge(points[1], points[5]);
        Edge(points[2], points[6]);
        Edge(points[3], points[7]);

        void Edge(Vector3 a, Vector3 b)
        {
            gl.Vertex(a.X, a.Y, a.Z);
            gl.Vertex(b.X, b.Y, b.Z);
        }
    }

    private void ApplyPointColor(OpenGL gl, HeightGridPoint point)
    {
        var (r, g, b) = viewModel.SelectedColorMode switch
        {
            "Solid" => (0.62, 0.82, 1.0),
            "Grayscale" => ViewerColorMapPalette.Grayscale(point.HeightScalar),
            "Thermal" => ViewerColorMapPalette.Thermal(point.HeightScalar),
            "Deviation" => DeviationColor(point.DeviationScalar),
            _ => C3DPointMapPalette.Height(point.HeightScalar)
        };

        gl.Color(r, g, b);
    }

    private static void ApplyPlaneFlatnessColor(OpenGL gl, Vector3 position, PlaneFlatnessEvaluation evaluation)
    {
        var plane = evaluation.ReferencePlane!;
        var signedDistance = plane.Normal.X * position.X
            + plane.Normal.Y * position.Y
            + plane.Normal.Z * position.Z
            + plane.Offset;
        var range = Math.Max(1e-9, Math.Max(Math.Abs(evaluation.MinimumSignedDistance), Math.Abs(evaluation.MaximumSignedDistance)));
        var normalized = Math.Clamp(signedDistance / range, -1.0, 1.0);
        var intensity = Math.Abs(normalized);
        var color = normalized >= 0.0
            ? (R: 1.0, G: 1.0 - 0.78 * intensity, B: 1.0 - 0.88 * intensity)
            : (R: 1.0 - 0.88 * intensity, G: 1.0 - 0.64 * intensity, B: 1.0);
        gl.Color(color.R, color.G, color.B);
    }

}

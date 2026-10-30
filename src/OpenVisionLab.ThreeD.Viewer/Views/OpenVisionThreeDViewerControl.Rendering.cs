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
using OpenVisionLab.ThreeD.Viewer.Recipes;
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
        if (viewModel.IsOrthographicView)
        {
            var halfHeight = Math.Max(0.01, viewModel.OrthographicHeight) * 0.5;
            var halfWidth = halfHeight * width / height;
            gl.Ortho(
                -halfWidth,
                halfWidth,
                -halfHeight,
                halfHeight,
                CameraMath.NearPlane,
                farPlane);
        }
        else
        {
            gl.Perspective(FieldOfViewDegrees, (double)width / height, CameraMath.NearPlane, farPlane);
        }
    }

    private void ConfigureCamera(OpenGL gl)
    {
        var target = GetCameraTarget();
        var eye = GetCameraPosition();
        var up = CameraMath.CameraUp(eye, target);
        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        gl.LoadIdentity();
        gl.LookAt(eye.X, eye.Y, eye.Z, target.X, target.Y, target.Z, up.X, up.Y, up.Z);
    }

    private bool TryProjectWorldPositionToViewport(Vector3 worldPosition, out Point screenPoint)
    {
        return viewModel.IsOrthographicView
            ? CameraMath.TryProjectWorldPositionToOrthographicScreen(
                worldPosition,
                Viewport.ActualWidth,
                Viewport.ActualHeight,
                viewModel.OrthographicHeight,
                GetCameraPosition(),
                GetCameraTarget(),
                out screenPoint)
            : CameraMath.TryProjectWorldPositionToScreen(
                worldPosition,
                Viewport.ActualWidth,
                Viewport.ActualHeight,
                FieldOfViewDegrees,
                GetCameraPosition(),
                GetCameraTarget(),
                out screenPoint);
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

    private void DrawViewportBackground(OpenGL gl)
    {
        var width = Math.Max(1, Viewport.ActualWidth);
        var height = Math.Max(1, Viewport.ActualHeight);
        gl.PushAttrib(OpenGL.GL_ALL_ATTRIB_BITS);
        gl.Disable(OpenGL.GL_DEPTH_TEST);
        gl.Disable(OpenGL.GL_LIGHTING);
        gl.Disable(OpenGL.GL_BLEND);
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.PushMatrix();
        gl.LoadIdentity();
        gl.Ortho(0, width, 0, height, -1, 1);
        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        gl.PushMatrix();
        gl.LoadIdentity();
        var centerX = width * 0.5;
        var centerY = height * 0.62;
        gl.Begin(OpenGL.GL_TRIANGLE_FAN);
        gl.Color(28 / 255.0, 35 / 255.0, 43 / 255.0);
        gl.Vertex(centerX, centerY);
        gl.Color(18 / 255.0, 23 / 255.0, 28 / 255.0);
        for (var i = 0; i <= 48; i++)
        {
            var angle = i * Math.PI * 2 / 48;
            gl.Vertex(centerX + Math.Cos(angle) * width * 1.2 * 0.62, centerY + Math.Sin(angle) * height * 0.9 * 0.62);
        }
        gl.End();
        gl.Begin(OpenGL.GL_TRIANGLE_STRIP);
        for (var i = 0; i <= 48; i++)
        {
            var angle = i * Math.PI * 2 / 48;
            gl.Color(18 / 255.0, 23 / 255.0, 28 / 255.0);
            gl.Vertex(centerX + Math.Cos(angle) * width * 1.2 * 0.62, centerY + Math.Sin(angle) * height * 0.9 * 0.62);
            gl.Color(15 / 255.0, 19 / 255.0, 23 / 255.0);
            gl.Vertex(centerX + Math.Cos(angle) * width * 1.2, centerY + Math.Sin(angle) * height * 0.9);
        }
        gl.End();
        gl.PopMatrix();
        gl.MatrixMode(OpenGL.GL_PROJECTION);
        gl.PopMatrix();
        gl.MatrixMode(OpenGL.GL_MODELVIEW);
        gl.PopAttrib();
    }

    private int C3DFloorTickStep => c3dSample is { } grid ? Math.Max(1, (int)Math.Ceiling(Math.Max(grid.Width, grid.Height) / 80.0) * 10) : 1;

    private double C3DFloorRawHeight => c3dSample is { } grid
        ? grid.Min - Math.Max((grid.Max - grid.Min) * 0.06, Math.Max(grid.Width, grid.Height) * grid.HorizontalScale * 0.01 / (C3DHeightGrid.ViewerHeightScale * viewModel.C3DHeightEmphasis))
        : 0;

    private Vector3 C3DFloorPosition(double row, double column) => CreateC3DGridDisplayPosition(row - 0.5, column - 0.5, C3DFloorRawHeight);

    private void DrawGrid(OpenGL gl)
    {
        if (!viewModel.Display.FloorGridVisible) return;
        gl.LineWidth(1.0f);
        gl.Begin(OpenGL.GL_LINES);
        if (viewModel.C3DSampleVisible && c3dSample is { } grid)
        {
            gl.Color(0.23, 0.30, 0.37);
            // Ticks denote actual cell boundaries; picking still uses zero-based sample indices.
            for (var column = 0; column < grid.Width; column += C3DFloorTickStep) DrawFloorLine(C3DFloorPosition(0, column), C3DFloorPosition(grid.Height, column));
            for (var row = 0; row < grid.Height; row += C3DFloorTickStep) DrawFloorLine(C3DFloorPosition(row, 0), C3DFloorPosition(row, grid.Width));
            DrawFloorLine(C3DFloorPosition(0, grid.Width), C3DFloorPosition(grid.Height, grid.Width));
            DrawFloorLine(C3DFloorPosition(grid.Height, 0), C3DFloorPosition(grid.Height, grid.Width));
            gl.End();
            return;
        }
        gl.Color(0.25, 0.29, 0.36);

        for (var i = -5; i <= 5; i++)
        {
            gl.Vertex(i, -1.02, -5.0);
            gl.Vertex(i, -1.02, 5.0);
            gl.Vertex(-5.0, -1.02, i);
            gl.Vertex(5.0, -1.02, i);
        }

        gl.End();

        void DrawFloorLine(Vector3 first, Vector3 second)
        {
            gl.Vertex(first.X, first.Y, first.Z);
            gl.Vertex(second.X, second.Y, second.Z);
        }
    }

    private void UpdateC3DFloorLabels()
    {
        ViewerFloorLabels.Visibility = viewModel.Display.FloorGridVisible && viewModel.C3DSampleVisible && c3dSample is not null ? Visibility.Visible : Visibility.Collapsed;
        foreach (UIElement label in ViewerFloorLabels.Children) label.Visibility = Visibility.Collapsed;
        if (c3dSample is not { } grid || ViewerFloorLabels.Visibility != Visibility.Visible) return;
        if (!TryProjectWorldPositionToViewport(C3DFloorPosition(0, grid.Width * 0.5), out var firstRowEdge)
            || !TryProjectWorldPositionToViewport(C3DFloorPosition(grid.Height, grid.Width * 0.5), out var lastRowEdge)
            || !TryProjectWorldPositionToViewport(C3DFloorPosition(grid.Height * 0.5, 0), out var firstColumnEdge)
            || !TryProjectWorldPositionToViewport(C3DFloorPosition(grid.Height * 0.5, grid.Width), out var lastColumnEdge)) return;
        var rowSide = firstRowEdge.Y > lastRowEdge.Y ? -1 : 1;
        var columnSide = firstColumnEdge.X < lastColumnEdge.X ? -1 : 1;
        var rowEdge = rowSide < 0 ? 0 : grid.Height;
        var columnEdge = columnSide < 0 ? 0 : grid.Width;
        var inset = Math.Max(grid.Width, grid.Height);
        var occupied = new List<Rect>();
        var chromeBounds = new FrameworkElement[] { OrientationCubePanel, ViewerHeightLegend, OrientationTriadPanel }
            .Where(chrome => chrome.IsVisible).Select(chrome => chrome.TransformToAncestor(ViewportSurface).TransformBounds(new Rect(chrome.RenderSize))).ToArray();
        var index = 2;
        Place(ViewerColumnAxisLabel, rowEdge + rowSide * inset * 0.047, grid.Width * 0.5, 0, 8);
        Place(ViewerRowAxisLabel, grid.Height * 0.5, columnEdge + columnSide * inset * 0.061, -12, 0);
        for (var column = 0; column <= grid.Width; column += C3DFloorTickStep) Tick(rowEdge + rowSide * inset * 0.02, column, column);
        for (var row = 0; row <= grid.Height; row += C3DFloorTickStep) Tick(row, columnEdge + columnSide * inset * 0.027, row);

        void Tick(double row, double column, int value)
        {
            if (index == ViewerFloorLabels.Children.Count)
                ViewerFloorLabels.Children.Add(new TextBlock { FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromArgb(179, 173, 185, 198)) });
            var text = (TextBlock)ViewerFloorLabels.Children[index++];
            text.Text = value.ToString(CultureInfo.InvariantCulture);
            Place(text, row, column);
        }

        void Place(TextBlock text, double row, double column, double offsetX = 0, double offsetY = 0)
        {
            if (!TryProjectWorldPositionToViewport(C3DFloorPosition(row, column), out var point)) return;
            text.Visibility = Visibility.Hidden;
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var bounds = new Rect(point.X + offsetX - Math.Ceiling(text.DesiredSize.Width) * 0.5, point.Y + offsetY - Math.Ceiling(text.DesiredSize.Height) * 0.5,
                Math.Ceiling(text.DesiredSize.Width), Math.Ceiling(text.DesiredSize.Height));
            if (bounds.Left < 2 || bounds.Top < 2 || bounds.Right > Viewport.ActualWidth - 2 || bounds.Bottom > Viewport.ActualHeight - 2) return;
            if (chromeBounds.Any(chrome => chrome.IntersectsWith(bounds)) || occupied.Any(label => label.IntersectsWith(bounds))) return;
            Canvas.SetLeft(text, bounds.Left);
            Canvas.SetTop(text, bounds.Top);
            text.Visibility = Visibility.Visible;
            bounds.Inflate(2, 2);
            occupied.Add(bounds);
        }
    }

    private void DrawAxes(OpenGL gl)
    {
        if (viewModel.C3DSampleVisible) return;
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
            firstPosition = MapLazPosition(lazFirst);
            secondPosition = MapLazPosition(lazSecond);
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
        if (roiEditingSession.LeftBounds is not { } left)
        {
            return;
        }

        DrawRoiBounds(gl, left, 0.20, 0.95, 0.45);

        if (roiEditingSession.RightBounds is not { } right
            || roiEditingSession.LeftCenter is not { } leftCenter
            || roiEditingSession.RightCenter is not { } rightCenter)
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

        return ViewerC3DGridDisplayGeometry.CreatePosition(
            c3dSample.Width,
            c3dSample.Height,
            c3dSample.HorizontalScale,
            C3DHeightGrid.ViewerHeightScale * viewModel.C3DHeightEmphasis,
            c3dSample.Mean,
            row,
            column,
            rawHeight,
            viewModel.C3DModelTransform);
    }

    private static bool IsC3DGridRoiInside(C3DGridRoi roi, C3DHeightGrid grid) =>
        C3DThicknessRecipeLoadPlan.IsRoiInside(roi, grid);

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
        var wireframeLodLevel = interactionWireframeLodActive
            && geometryStyle == ViewerGeometryStyle.Wireframe
            ? interactionWireframeLodLevel
            : C3DWireframeLodLevel.Precise;
        c3dTextureDrawing = TryBindC3DTexture(gl);
        try
        {

            if (TryDrawC3DGpuGeometry(gl, renderProxy, positions, geometryStyle, wireframeLodLevel))
            {
                ReleaseC3DDisplayLists(gl);
                return;
            }

            if (UsesDynamicC3DColor())
            {
                ReleaseC3DDisplayLists(gl);
                DrawC3DGeometry(gl, renderProxy, positions, geometryStyle, wireframeLodLevel);
                return;
            }

            var displayListKey = new C3DDisplayListKey(
                c3dSample!,
                c3dRenderPositionCache.CurrentTransform,
                geometryStyle,
                viewModel.Display.EffectiveSettings.ColorMap,
                viewModel.HeightColorRenderMinimum,
                viewModel.HeightColorRenderMaximum,
                viewModel.PointSize,
                viewModel.C3DHeightEmphasis,
                wireframeLodLevel) { HeightColorOptions = viewModel.HeightColorOptionsKey, TextureImage = viewModel.C3DTexture.Image, TextureVisible = c3dTextureDrawing };
            if (wireframeLodLevel != C3DWireframeLodLevel.Precise)
            {
                DrawCachedC3DGeometry(
                    gl,
                    renderProxy,
                    positions,
                    geometryStyle,
                    wireframeLodLevel,
                    displayListKey,
                    ref c3dRenderResources.InteractionDisplayListId,
                    ref c3dRenderResources.InteractionDisplayListKey);
            }
            else
            {
                DrawCachedC3DGeometry(
                    gl,
                    renderProxy,
                    positions,
                    geometryStyle,
                    C3DWireframeLodLevel.Precise,
                    displayListKey,
                    ref c3dRenderResources.DisplayListId,
                    ref c3dRenderResources.DisplayListKey);
            }

        }
        finally
        {
            c3dTextureDrawing = false;
            gl.Disable(GlTexture2D);
        }
    }

    private void DrawCachedC3DGeometry(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        ViewerGeometryStyle geometryStyle,
        C3DWireframeLodLevel wireframeLodLevel,
        C3DDisplayListKey displayListKey,
        ref uint displayListId,
        ref C3DDisplayListKey? cachedKey)
    {
        if (displayListId == 0 || cachedKey != displayListKey)
        {
            var displayListBuildReason = wireframeLodLevel != C3DWireframeLodLevel.Precise
                ? "interaction-lod"
                : pendingC3DDisplayListBuildReason;
            ReleaseC3DDisplayList(gl, ref displayListId, ref cachedKey);
            displayListId = gl.GenLists(1);
            if (displayListId != 0)
            {
                var displayListBuildStart = Stopwatch.GetTimestamp();
                gl.NewList(displayListId, OpenGL.GL_COMPILE);
                DrawC3DGeometry(
                    gl,
                    renderProxy,
                    positions,
                    geometryStyle,
                    wireframeLodLevel);
                gl.EndList();
                lastC3DDisplayListBuildMilliseconds = Stopwatch.GetElapsedTime(displayListBuildStart).TotalMilliseconds;
                c3dDisplayListBuildCount++;
                lastC3DDisplayListBuildReason = displayListBuildReason;
                if (wireframeLodLevel == C3DWireframeLodLevel.Precise)
                {
                    pendingC3DDisplayListBuildReason = "cache-key-changed";
                }
                if (wireframeLodLevel != C3DWireframeLodLevel.Precise)
                {
                    interactionC3DDisplayListBuildCount++;
                }

                cachedKey = displayListKey;
            }
        }

        if (displayListId != 0)
        {
            gl.CallList(displayListId);
        }
        else
        {
            DrawC3DGeometry(
                gl,
                renderProxy,
                positions,
                geometryStyle,
                wireframeLodLevel);
        }
    }

    private void DrawC3DGeometry(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        ViewerGeometryStyle geometryStyle,
        C3DWireframeLodLevel wireframeLodLevel = C3DWireframeLodLevel.Precise)
    {
        switch (geometryStyle)
        {
            case ViewerGeometryStyle.Wireframe when renderProxy.HasSurface:
                DrawC3DEdges(
                    gl,
                    renderProxy,
                    positions,
                    GetC3DWireframeIndices(renderProxy, wireframeLodLevel),
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

    private bool TryDrawC3DGpuGeometry(
        OpenGL gl,
        C3DHeightGridRenderProxy renderProxy,
        IReadOnlyList<Vector3> positions,
        ViewerGeometryStyle geometryStyle,
        C3DWireframeLodLevel wireframeLodLevel)
    {
        var bufferKey = new C3DGpuBufferKey(
            c3dSample!,
            c3dRenderPositionCache.CurrentTransform,
            viewModel.Display.EffectiveSettings.ColorMap,
            viewModel.HeightColorRenderMinimum,
            viewModel.HeightColorRenderMaximum,
            viewModel.C3DHeightEmphasis,
            UsesDynamicC3DColor() ? planeFlatnessEvaluation : null) { HeightColorOptions = viewModel.HeightColorOptionsKey, TextureImage = viewModel.C3DTexture.Image };
        if (c3dRenderResources.RequiresGpuReplacement(bufferKey))
        {
            if (c3dRenderResources.IsGpuReplacementFailed(bufferKey))
            {
                return false;
            }

            ReleaseC3DGpuBuffers(gl);
            var interleavedVertices = C3DGpuVertexBuilder.Build(
                renderProxy.Points,
                positions,
                GetC3DGpuPointColor);
            var uploadStart = Stopwatch.GetTimestamp();
            if (!C3DGpuBufferSet.TryCreate(
                    gl,
                    interleavedVertices,
                    renderProxy,
                    out var gpuBuffers,
                    out var failure, viewModel.C3DTexture.Image is null ? null : OpenVisionLab.ThreeD.Viewer.Loading.C3DTextureImage.CreateCoordinates(c3dSample!)))
            {
                c3dRenderResources.MarkGpuReplacementFailed(bufferKey);
                c3dGpuTelemetry.RecordFallback(failure);
                return false;
            }

            c3dRenderResources.SetGpuReplacement(bufferKey, gpuBuffers!);
            c3dGpuTelemetry.RecordUpload(
                gpuBuffers!.UploadedBytes,
                Stopwatch.GetElapsedTime(uploadStart).TotalMilliseconds);

            // Keep established source-apply telemetry compatible while the renderer owner changes.
            c3dDisplayListBuildCount++;
            lastC3DDisplayListBuildMilliseconds = c3dGpuTelemetry.LastUploadMilliseconds;
            lastC3DDisplayListBuildReason = pendingC3DDisplayListBuildReason;
            pendingC3DDisplayListBuildReason = "cache-key-changed";
        }

        var activeGpuBuffers = c3dGpuBuffers!;
        switch (geometryStyle)
        {
            case ViewerGeometryStyle.Wireframe when renderProxy.HasSurface:
                activeGpuBuffers.DrawWireframe(gl, wireframeLodLevel, c3dTextureDrawing);
                break;
            case ViewerGeometryStyle.Surface when renderProxy.HasSurface:
                activeGpuBuffers.DrawSurface(gl, withEdges: false, c3dTextureDrawing);
                break;
            case ViewerGeometryStyle.SurfaceWithEdges when renderProxy.HasSurface:
                activeGpuBuffers.DrawSurface(gl, withEdges: true, c3dTextureDrawing);
                break;
            default:
                activeGpuBuffers.DrawPoints(gl, (float)viewModel.PointSize, c3dTextureDrawing);
                break;
        }

        c3dGpuTelemetry.RecordDraw();
        return true;
    }

    private (double R, double G, double B) GetC3DGpuPointColor(
        HeightGridPoint point,
        Vector3 position)
    {
        if (UsesDynamicC3DColor()
            && planeFlatnessEvaluation is { ReferencePlane: not null } flatness)
        {
            return GetPlaneFlatnessColor(position, flatness);
        }

        return viewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Deviation
            ? ViewerColorMapPalette.Deviation(point.DeviationScalar) : viewModel.GetC3DHeightDisplayColor(point.RawValue, point.Row, point.Column);
    }

    private bool TryBindC3DTexture(OpenGL gl)
    {
        gl.Disable(GlTexture2D);
        if (!viewModel.C3DTexture.IsVisible || viewModel.C3DTexture.Image is not { } image) return false;
        if (!ReferenceEquals(c3dRenderResources.TextureImage, image))
        {
            ReleaseC3DTexture(gl);
            try
            {
                // GL error flags survive earlier rendering calls; only this upload may fail the image.
                while (gl.GetError() != OpenGL.GL_NO_ERROR) { }
                var limit = new int[1];
                gl.GetInteger(0x0D33, limit); // GL_MAX_TEXTURE_SIZE for this context.
                if (image.Width > limit[0] || image.Height > limit[0])
                    throw new InvalidOperationException($"Texture exceeds this viewer's {limit[0]} pixel image limit.");
                var ids = new uint[1];
                gl.GenTextures(1, ids);
                c3dRenderResources.TextureId = ids[0];
                if (ids[0] == 0) throw new InvalidOperationException("OpenGL did not allocate a texture.");
                gl.BindTexture(GlTexture2D, ids[0]);
                gl.TexParameter(GlTexture2D, GlTextureMinFilter, (int)GlLinear);
                gl.TexParameter(GlTexture2D, GlTextureMagFilter, (int)GlLinear);
                gl.TexParameter(GlTexture2D, GlTextureWrapS, 0x812F); // GL_CLAMP_TO_EDGE, never tile the image.
                gl.TexParameter(GlTexture2D, GlTextureWrapT, 0x812F);
                gl.PixelStore(GlUnpackAlignment, 1);
                gl.TexImage2D(GlTexture2D, 0, GlRgba, image.Width, image.Height, 0, GlBgra, GlUnsignedByte, image.Pixels);
                var error = gl.GetError();
                if (error != OpenGL.GL_NO_ERROR) throw new InvalidOperationException($"Texture upload failed (OpenGL {error}).");
                c3dRenderResources.TextureImage = image;
                c3dRenderResources.TextureUploadCount++;
            }
            catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException)
            {
                ReleaseC3DTexture(gl);
                viewModel.C3DTexture.ReportRenderFailure(exception.Message);
                return false;
            }
        }
        gl.BindTexture(GlTexture2D, c3dRenderResources.TextureId);
        gl.Enable(GlTexture2D);
        return true;
    }

    private void ReleaseC3DTexture(OpenGL gl)
    {
        if (c3dRenderResources.TextureId != 0)
        {
            try
            {
                gl.DeleteTextures(1, [c3dRenderResources.TextureId]);
                c3dRenderResources.TextureReleaseCount++;
            }
            catch (InvalidOperationException) { c3dRenderResources.TextureReleaseFailureCount++; }
        }
        c3dRenderResources.TextureId = 0;
        c3dRenderResources.TextureImage = null;
    }

    private void ReleaseC3DGpuBuffers(OpenGL gl)
    {
        var buffers = c3dGpuBuffers;
        if (buffers is not null)
        {
            c3dGpuTelemetry.RecordRelease(buffers.Release(gl));
        }

        c3dRenderResources.MarkGpuBuffersReleased();
    }

    private static IReadOnlyList<int> GetC3DWireframeIndices(
        C3DHeightGridRenderProxy renderProxy,
        C3DWireframeLodLevel lodLevel) =>
        lodLevel switch
        {
            C3DWireframeLodLevel.Coarse => renderProxy.CoarseInteractionGridEdgeIndices,
            C3DWireframeLodLevel.Medium => renderProxy.InteractionGridEdgeIndices,
            _ => renderProxy.GridEdgeIndices
        };

    private bool UsesDynamicC3DColor() =>
        viewModel.SelectedColorMode == "Deviation"
        && viewModel.PlaneFlatnessVisible
        && planeFlatnessEvaluation is { ReferencePlane: not null };

    private void ReleaseC3DDisplayLists(OpenGL gl)
    {
        var releaseCount = (c3dDisplayListId != 0 ? 1 : 0)
            + (c3dInteractionDisplayListId != 0 ? 1 : 0);
        try
        {
            ReleaseC3DDisplayList(
                gl,
                ref c3dRenderResources.DisplayListId,
                ref c3dRenderResources.DisplayListKey);
            ReleaseC3DDisplayList(
                gl,
                ref c3dRenderResources.InteractionDisplayListId,
                ref c3dRenderResources.InteractionDisplayListKey);
            c3dDisplayListReleaseCount += releaseCount;
        }
        catch
        {
            c3dDisplayListReleaseFailureCount++;
            throw;
        }
    }

    private static void ReleaseC3DDisplayList(
        OpenGL gl,
        ref uint displayListId,
        ref C3DDisplayListKey? displayListKey)
    {
        if (displayListId != 0)
        {
            gl.DeleteLists(displayListId, 1);
        }

        displayListId = 0;
        displayListKey = null;
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
            gl.Disable(GlTexture2D);
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
            if (c3dTextureDrawing) gl.Enable(GlTexture2D);
        }

        gl.LineWidth(1.0f);
    }

    private void ApplyC3DColor(OpenGL gl, HeightGridPoint point, Vector3 position)
    {
        if (c3dTextureDrawing && c3dSample is { } source)
        {
            var uv = OpenVisionLab.ThreeD.Viewer.Loading.C3DTextureImage.Coordinate(point.Row, point.Column, source.Width, source.Height);
            gl.Color(1.0, 1.0, 1.0);
            gl.TexCoord(uv.X, uv.Y);
            return;
        }
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
        return c3dRenderProxyCache.GetOrCreate(sample);
    }

    private Vector3[] GetC3DRenderPositions(C3DHeightGridRenderProxy renderProxy)
        => c3dRenderPositionCache.GetOrCreate(renderProxy, viewModel.C3DModelTransform, viewModel.C3DHeightEmphasis);

    private void InvalidateC3DRenderProxy()
    {
        c3dRenderProxyCache.Clear();
        c3dRenderPositionCache.Clear();
        c3dRenderResources.InvalidateGpuForRenderProxy();
        c3dDisplayListKey = null;
        c3dInteractionDisplayListKey = null;
        pendingC3DDisplayListBuildReason = "render-proxy-invalidated";
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
        var normalMapSelected = viewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Normal;
        var useTexture = !normalMapSelected && EnsureImportedMeshTexture(gl);
        if (useTexture)
        {
            gl.Enable(GlTexture2D);
            gl.BindTexture(GlTexture2D, importedMeshTextureState.TextureId);
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

    private void DrawImportedMeshSurface(
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

    private void DrawImportedMeshEdges(
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

    private void DrawImportedMeshEdge(
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

    private void DrawImportedMeshVertex(OpenGL gl, ImportedMesh mesh, int index, bool useTexture)
    {
        if (viewModel.Display.EffectiveSettings.ColorMap == ViewerColorMap.Normal
            && viewModel.Display.IsImportedMeshNormalDisplayable
            && index >= 0
            && index < mesh.Normals.Length)
        {
            var normal = mesh.Normals[index];
            var color = ViewerColorMapPalette.NormalMap(normal);
            gl.Color(color.R, color.G, color.B);
        }
        else if (useTexture)
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
        var points = lazSceneTransform.CreateBoundsCorners(lazSample!);

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
        foreach (var point in pointCloud.SampledPointView)
        {
            ApplyLazPointColor(gl, point);
            var position = MapLazPosition(point);
            gl.Vertex(position.X, position.Y, position.Z);
        }

        gl.End();
        gl.PointSize(1.0f);
        DrawLazMetadata(gl);
        DrawSelectedLazPoint(gl);
    }

    private Vector3 MapLazPosition(Vector3 source) =>
        lazSceneTransform.Map(source);

    private Vector3 MapLazPosition(LazPointCloudPoint source) =>
        lazSceneTransform.Map(source);

    private Vector3 MapLazPosition(double x, double y, double z) =>
        lazSceneTransform.Map(x, y, z);

    private void ApplyLazPointColor(OpenGL gl, LazPointCloudPoint point)
    {
        var (r, g, b) = ViewerLazPointCloudColor.Resolve(point, viewModel.SelectedColorMode, lazSample);

        gl.Color(r, g, b);
    }

    private void DrawSelectedLazPoint(OpenGL gl)
    {
        if (selectedLazPoint is not { } point)
        {
            return;
        }

        var position = MapLazPosition(point);
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
        var (r, g, b) = viewModel.SelectedColorMode == "Deviation"
            ? ViewerColorMapPalette.Deviation(point.DeviationScalar) : viewModel.GetC3DHeightDisplayColor(point.RawValue, point.Row, point.Column);

        gl.Color(r, g, b);
    }

    private static void ApplyPlaneFlatnessColor(
        OpenGL gl,
        Vector3 position,
        ViewerPlaneFlatnessDisplayEvaluation evaluation)
    {
        var color = GetPlaneFlatnessColor(position, evaluation);
        gl.Color(color.R, color.G, color.B);
    }

    private static (double R, double G, double B) GetPlaneFlatnessColor(
        Vector3 position,
        ViewerPlaneFlatnessDisplayEvaluation evaluation)
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
        return color;
    }

}

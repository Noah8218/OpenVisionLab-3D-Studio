using System.Numerics;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

public static class InspectionOverlayRenderer
{
    public static void DrawMeasurement(OpenGL gl, bool cubeVisible, bool pointCloudVisible)
    {
        if (cubeVisible)
        {
            DrawCubeWidthMeasurement(gl);
        }

        if (pointCloudVisible)
        {
            DrawPointCloudMeasurement(gl);
        }
    }

    public static void DrawSelectionOverlay(OpenGL gl, string selectionMode)
    {
        switch (selectionMode)
        {
            case "Box ROI":
                DrawBoxRoi(gl);
                break;
            case "Section Plane":
                DrawSectionPlane(gl);
                break;
            default:
                DrawSelectionPoint(gl);
                break;
        }
    }

    public static void DrawResultOverlay(OpenGL gl, bool c3dVisible)
    {
        if (c3dVisible)
        {
            DrawC3DHeightDeviationOverlay(gl);
            return;
        }

        DrawPassToleranceBand(gl);
        DrawResultProfile(gl);
        DrawFailMarkers(gl);
    }

    private static void DrawCubeWidthMeasurement(OpenGL gl)
    {
        gl.LineWidth(3.0f);
        gl.Color(1.0, 0.95, 0.22);
        gl.Begin(OpenGL.GL_LINES);

        gl.Vertex(-1.0, 1.35, 1.15);
        gl.Vertex(1.0, 1.35, 1.15);
        gl.Vertex(-1.0, 1.23, 1.15);
        gl.Vertex(-1.0, 1.47, 1.15);
        gl.Vertex(1.0, 1.23, 1.15);
        gl.Vertex(1.0, 1.47, 1.15);

        gl.End();
    }

    private static void DrawPointCloudMeasurement(OpenGL gl)
    {
        gl.LineWidth(2.5f);
        gl.Color(1.0, 0.95, 0.22);
        gl.Begin(OpenGL.GL_LINES);

        gl.Vertex(4.95, -1.18, -1.75);
        gl.Vertex(4.95, -0.10, -1.75);
        gl.Vertex(4.73, -1.18, -1.75);
        gl.Vertex(5.17, -1.18, -1.75);
        gl.Vertex(4.73, -0.10, -1.75);
        gl.Vertex(5.17, -0.10, -1.75);

        gl.Vertex(2.35, -1.03, 1.24);
        gl.Vertex(4.45, -1.03, 1.24);
        gl.Vertex(2.35, -1.16, 1.24);
        gl.Vertex(2.35, -0.90, 1.24);
        gl.Vertex(4.45, -1.16, 1.24);
        gl.Vertex(4.45, -0.90, 1.24);

        gl.End();
    }

    private static void DrawSelectionPoint(OpenGL gl)
    {
        var point = new Vector3(3.78f, -0.28f, -0.32f);
        gl.PointSize(11.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.25, 0.25);
        gl.Vertex(point.X, point.Y, point.Z);
        gl.End();
        gl.PointSize(1.0f);

        gl.LineWidth(2.0f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Color(1.0, 1.0, 0.25);
        gl.Vertex(point.X - 0.18, point.Y, point.Z);
        gl.Vertex(point.X + 0.18, point.Y, point.Z);
        gl.Vertex(point.X, point.Y - 0.18, point.Z);
        gl.Vertex(point.X, point.Y + 0.18, point.Z);
        gl.Vertex(point.X, point.Y, point.Z - 0.18);
        gl.Vertex(point.X, point.Y, point.Z + 0.18);
        gl.End();
    }

    private static void DrawBoxRoi(OpenGL gl)
    {
        var min = new Vector3(2.35f, -1.10f, -1.05f);
        var max = new Vector3(4.45f, -0.10f, 0.95f);
        gl.LineWidth(2.5f);
        gl.Color(1.0, 0.74, 0.18);
        gl.Begin(OpenGL.GL_LINES);
        BoxEdge(gl, min.X, min.Y, min.Z, max.X, min.Y, min.Z);
        BoxEdge(gl, max.X, min.Y, min.Z, max.X, min.Y, max.Z);
        BoxEdge(gl, max.X, min.Y, max.Z, min.X, min.Y, max.Z);
        BoxEdge(gl, min.X, min.Y, max.Z, min.X, min.Y, min.Z);
        BoxEdge(gl, min.X, max.Y, min.Z, max.X, max.Y, min.Z);
        BoxEdge(gl, max.X, max.Y, min.Z, max.X, max.Y, max.Z);
        BoxEdge(gl, max.X, max.Y, max.Z, min.X, max.Y, max.Z);
        BoxEdge(gl, min.X, max.Y, max.Z, min.X, max.Y, min.Z);
        BoxEdge(gl, min.X, min.Y, min.Z, min.X, max.Y, min.Z);
        BoxEdge(gl, max.X, min.Y, min.Z, max.X, max.Y, min.Z);
        BoxEdge(gl, max.X, min.Y, max.Z, max.X, max.Y, max.Z);
        BoxEdge(gl, min.X, min.Y, max.Z, min.X, max.Y, max.Z);
        gl.End();
    }

    private static void DrawSectionPlane(OpenGL gl)
    {
        const double x = 3.25;
        const double y0 = -1.20;
        const double y1 = 0.20;
        const double z0 = -2.15;
        const double z1 = 2.15;

        gl.LineWidth(2.0f);
        gl.Color(0.96, 0.30, 0.86);
        gl.Begin(OpenGL.GL_LINES);
        for (var i = 0; i <= 8; i++)
        {
            var z = z0 + (z1 - z0) * i / 8.0;
            gl.Vertex(x, y0, z);
            gl.Vertex(x, y1, z);
            var y = y0 + (y1 - y0) * i / 8.0;
            gl.Vertex(x, y, z0);
            gl.Vertex(x, y, z1);
        }
        gl.End();
    }

    private static void DrawPassToleranceBand(OpenGL gl)
    {
        const double y = -0.38;

        gl.LineWidth(2.5f);
        gl.Color(0.16, 0.92, 0.36);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(2.35, y, -1.05);
        gl.Vertex(4.45, y, -1.05);
        gl.Vertex(4.45, y, 0.95);
        gl.Vertex(2.35, y, 0.95);
        gl.End();

        gl.LineWidth(1.5f);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(2.35, y, -1.05);
        gl.Vertex(4.45, y, 0.95);
        gl.Vertex(4.45, y, -1.05);
        gl.Vertex(2.35, y, 0.95);
        gl.End();
    }

    private static void DrawResultProfile(OpenGL gl)
    {
        gl.LineWidth(3.0f);
        gl.Color(1.0, 0.72, 0.18);
        gl.Begin(OpenGL.GL_LINE_STRIP);
        gl.Vertex(2.45, -0.62, -0.90);
        gl.Vertex(3.00, -0.51, -0.72);
        gl.Vertex(3.45, -0.42, -0.52);
        gl.Vertex(3.78, -0.16, -0.32);
        gl.Vertex(4.12, -0.32, 0.06);
        gl.Vertex(4.40, -0.46, 0.55);
        gl.End();
    }

    private static void DrawFailMarkers(OpenGL gl)
    {
        gl.LineWidth(3.0f);
        gl.Color(1.0, 0.18, 0.12);
        gl.Begin(OpenGL.GL_LINES);
        DrawFailMarker(gl, 3.72, -0.14, -0.46);
        DrawFailMarker(gl, 3.78, -0.12, -0.32);
        DrawFailMarker(gl, 3.95, -0.18, -0.20);
        gl.End();

        gl.PointSize(10.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.10, 0.08);
        gl.Vertex(3.72, -0.14, -0.46);
        gl.Vertex(3.78, -0.12, -0.32);
        gl.Vertex(3.95, -0.18, -0.20);
        gl.End();
        gl.PointSize(1.0f);
    }

    private static void DrawC3DHeightDeviationOverlay(OpenGL gl)
    {
        gl.LineWidth(2.5f);
        gl.Color(1.0, 0.74, 0.18);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(-4.4, 0.0, -3.2);
        gl.Vertex(4.4, 0.0, -3.2);
        gl.Vertex(4.4, 0.0, 3.2);
        gl.Vertex(-4.4, 0.0, 3.2);
        gl.End();

        gl.LineWidth(2.0f);
        gl.Color(0.18, 0.95, 0.42);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(-4.4, -0.55, -3.2);
        gl.Vertex(4.4, -0.55, -3.2);
        gl.Vertex(-4.4, 0.55, 3.2);
        gl.Vertex(4.4, 0.55, 3.2);
        gl.End();

        gl.LineWidth(3.0f);
        gl.Color(1.0, 0.18, 0.12);
        gl.Begin(OpenGL.GL_LINES);
        DrawFailMarker(gl, 3.55, 1.05, 2.20);
        DrawFailMarker(gl, 3.80, 1.18, 2.55);
        DrawFailMarker(gl, 4.05, 1.02, 2.90);
        gl.End();

        gl.PointSize(10.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.10, 0.08);
        gl.Vertex(3.55, 1.05, 2.20);
        gl.Vertex(3.80, 1.18, 2.55);
        gl.Vertex(4.05, 1.02, 2.90);
        gl.End();
        gl.PointSize(1.0f);
    }

    private static void DrawFailMarker(OpenGL gl, double x, double y, double z)
    {
        const double size = 0.16;

        gl.Vertex(x - size, y - size, z);
        gl.Vertex(x + size, y + size, z);
        gl.Vertex(x - size, y + size, z);
        gl.Vertex(x + size, y - size, z);
        gl.Vertex(x, y - 0.35, z);
        gl.Vertex(x, y + 0.18, z);
    }

    private static void BoxEdge(OpenGL gl, double x1, double y1, double z1, double x2, double y2, double z2)
    {
        gl.Vertex(x1, y1, z1);
        gl.Vertex(x2, y2, z2);
    }
}

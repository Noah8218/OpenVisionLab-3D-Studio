using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Windows;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Models;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private const double LinkedHeightHoverMinimumIntervalMilliseconds = 24.0;
    private const double LinkedHeightHoverMinimumDistance = 2.0;

    private C3DGridCursor? linkedHeightCursor;
    private C3DGridCursor? lastPublishedThreeDGridHover;
    private long lastLinkedHeightHoverTimestamp;
    private Point lastLinkedHeightHoverPoint;

    public event EventHandler<C3DGridHoverChangedEventArgs>? C3DGridHoverChanged;

    public C3DGridCursor? LinkedHeightCursor => linkedHeightCursor;

    public void SetLinkedHeightCursor(C3DGridCursor? cursor)
    {
        var next = ValidateLinkedHeightCursor(cursor);
        if (linkedHeightCursor == next)
        {
            return;
        }

        linkedHeightCursor = next;
        // The SharpGL host already renders at 60 FPS. Keeping linked-hover
        // updates out of the camera interaction scheduler avoids activating
        // wireframe interaction LOD for a presentation-only cursor.
    }

    public bool TryPublishC3DGridHoverForSmoke(int row, int column)
    {
        if (c3dSample is null)
        {
            return false;
        }

        try
        {
            var point = c3dSample.ReadPoint(row, column);
            lastPublishedThreeDGridHover = null;
            PublishThreeDGridHover(CreateThreeDGridCursor(point));
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentOutOfRangeException
            or IOException
            or InvalidDataException)
        {
            return false;
        }
    }

    private void UpdateThreeDGridHover(Point screenPoint)
    {
        if (C3DGridHoverChanged is null || !ShouldSampleLinkedHeightHover(screenPoint))
        {
            return;
        }

        PublishThreeDGridHover(
            TryPickC3DPoint(screenPoint, out var point)
                ? CreateThreeDGridCursor(point)
                : null);
    }

    private bool ShouldSampleLinkedHeightHover(Point screenPoint)
    {
        var now = Stopwatch.GetTimestamp();
        if (lastLinkedHeightHoverTimestamp != 0)
        {
            var elapsed = Stopwatch.GetElapsedTime(
                lastLinkedHeightHoverTimestamp,
                now).TotalMilliseconds;
            var delta = screenPoint - lastLinkedHeightHoverPoint;
            if (elapsed < LinkedHeightHoverMinimumIntervalMilliseconds
                && delta.Length < LinkedHeightHoverMinimumDistance)
            {
                return false;
            }
        }

        lastLinkedHeightHoverTimestamp = now;
        lastLinkedHeightHoverPoint = screenPoint;
        return true;
    }

    private C3DGridCursor CreateThreeDGridCursor(HeightGridPoint point) =>
        new(
            C3DGridCursorOrigin.ThreeDViewer,
            c3dSample!.ContentSha256,
            point.Row,
            point.Column,
            point.RawValue,
            true);

    private void PublishThreeDGridHover(C3DGridCursor? cursor)
    {
        if (lastPublishedThreeDGridHover == cursor)
        {
            return;
        }

        lastPublishedThreeDGridHover = cursor;
        C3DGridHoverChanged?.Invoke(
            this,
            new C3DGridHoverChangedEventArgs(cursor));
    }

    private C3DGridCursor? ValidateLinkedHeightCursor(C3DGridCursor? cursor)
    {
        if (cursor is not { } value
            || c3dSample is null
            || !string.Equals(
                value.SourceContentSha256,
                c3dSample.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            || value.Row < 0
            || value.Row >= c3dSample.Height
            || value.Column < 0
            || value.Column >= c3dSample.Width
            || value.IsValid && !double.IsFinite(value.RawHeight))
        {
            return null;
        }

        return value;
    }

    private void ResetC3DGridHoverForSourceChange()
    {
        var hadPublishedHover = lastPublishedThreeDGridHover is not null;
        lastPublishedThreeDGridHover = null;
        lastLinkedHeightHoverTimestamp = 0;
        linkedHeightCursor = null;
        if (hadPublishedHover)
        {
            C3DGridHoverChanged?.Invoke(
                this,
                new C3DGridHoverChangedEventArgs(null));
        }
    }

    private void DrawLinkedHeightCursor(OpenGL gl)
    {
        if (linkedHeightCursor is not { IsValid: true } cursor
            || c3dSample is null)
        {
            return;
        }

        var center = CreateC3DGridDisplayPosition(
            cursor.Row,
            cursor.Column,
            cursor.RawHeight);
        var armLength = Math.Clamp(
            c3dSample.HorizontalScale * Math.Max(10, c3dSample.PointStride * 4),
            0.05f,
            0.28f);
        var verticalLength = Math.Clamp(
            armLength * 1.6f,
            0.08f,
            0.42f);

        gl.Disable(OpenGL.GL_DEPTH_TEST);
        gl.LineWidth(2.5f);
        gl.Color(1.0, 0.78, 0.12);
        gl.Begin(OpenGL.GL_LINES);
        gl.Vertex(center.X - armLength, center.Y, center.Z);
        gl.Vertex(center.X + armLength, center.Y, center.Z);
        gl.Vertex(center.X, center.Y, center.Z - armLength);
        gl.Vertex(center.X, center.Y, center.Z + armLength);
        gl.Vertex(center.X, center.Y - verticalLength * 0.25f, center.Z);
        gl.Vertex(center.X, center.Y + verticalLength, center.Z);
        gl.End();

        gl.PointSize(10.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(0.16, 0.94, 0.92);
        gl.Vertex(center.X, center.Y, center.Z);
        gl.End();
        gl.PointSize(1.0f);
        gl.LineWidth(1.0f);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
    }
}

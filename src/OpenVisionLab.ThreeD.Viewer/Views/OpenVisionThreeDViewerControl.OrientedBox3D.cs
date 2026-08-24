using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

/// <summary>
/// View-adapter ownership for projection-correct OrientedBox3D rendering and
/// pointer manipulation. The persisted geometry and explicit Apply boundary
/// remain owned by the Workbench.
/// </summary>
public sealed partial class OpenVisionThreeDViewerControl
{
    private const double OrientedBoxHandleRadius = 18.0;
    private const double OrientedBoxMinimumHalfExtent = 0.001;
    private ToolRecipeSelection? teachingOrientedBoxDraft;
    private ToolRecipeSelection? teachingOrientedBoxDragStart;
    private TeachingOrientedBox3DEditMode teachingOrientedBoxEditMode;
    private TeachingOrientedBox3DEditMode teachingOrientedBoxHoverMode;
    private string? teachingOrientedBoxStatusBeforeHover;
    private Vector3 teachingOrientedBoxMovePlanePoint;
    private Vector3 teachingOrientedBoxMovePlaneNormal;
    private Vector3 teachingOrientedBoxMoveWorldStart;
    private Point teachingOrientedBoxRotationCenter;
    private double teachingOrientedBoxRotationStartAngle;

    public event EventHandler<TeachingOrientedBox3DDraftChangedEventArgs>?
        TeachingOrientedBox3DDraftChanged;

    public void SetTeachingOrientedBox3DDraft(ToolRecipeSelection? selection)
    {
        if (selection is not null
            && (selection.Kind != ToolRecipeSelectionKinds.OrientedBox3D
                || selection.OrientedBox3D is null
                || !IsSelectionForCurrentC3DGrid(selection)
                || ToolRecipeOrientedBox3DGeometry.Validate(selection.OrientedBox3D).Count > 0))
        {
            selection = null;
        }

        if (Equals(teachingOrientedBoxDraft, selection))
        {
            return;
        }

        teachingOrientedBoxDraft = selection;
        ClearTeachingOrientedBoxEdit();
        RenderNow();
    }

    private bool HasVisibleTeachingOrientedBoxDraft =>
        teachingOrientedBoxDraft is
        {
            Kind: ToolRecipeSelectionKinds.OrientedBox3D,
            OrientedBox3D: not null
        } selection
        && IsSelectionForCurrentC3DGrid(selection);

    private bool TryBeginTeachingOrientedBoxEdit(Point screenPoint)
    {
        if (!TryGetTeachingOrientedBoxDraft(out var selection)
            || selection.OrientedBox3D is not { } box)
        {
            return false;
        }

        var mode = GetTeachingOrientedBoxEditMode(screenPoint, box);
        if (mode == TeachingOrientedBox3DEditMode.None)
        {
            return false;
        }

        teachingOrientedBoxEditMode = mode;
        teachingOrientedBoxHoverMode = mode;
        teachingOrientedBoxDragStart = selection;
        if (mode == TeachingOrientedBox3DEditMode.Move)
        {
            teachingOrientedBoxMovePlanePoint = CreateOrientedBoxWorldPosition(box.Center);
            teachingOrientedBoxMovePlaneNormal = Vector3.Normalize(
                GetCameraTarget() - GetCameraPosition());
            if (!TryIntersectScreenWithPlane(
                    screenPoint,
                    teachingOrientedBoxMovePlanePoint,
                    teachingOrientedBoxMovePlaneNormal,
                    out teachingOrientedBoxMoveWorldStart))
            {
                ClearTeachingOrientedBoxEdit();
                return false;
            }
        }
        else if (mode == TeachingOrientedBox3DEditMode.RotateY)
        {
            if (!TryProjectWorldPositionToViewport(
                    CreateOrientedBoxWorldPosition(box.Center),
                    out teachingOrientedBoxRotationCenter))
            {
                ClearTeachingOrientedBoxEdit();
                return false;
            }

            var rotationHandle = GetTeachingOrientedBoxHandles(box).First(
                handle => handle.Mode == TeachingOrientedBox3DEditMode.RotateY);
            if (!TryProjectTeachingOrientedBoxHandle(
                    box,
                    rotationHandle,
                    out var rotationHandleScreen))
            {
                ClearTeachingOrientedBoxEdit();
                return false;
            }

            teachingOrientedBoxRotationStartAngle = Math.Atan2(
                rotationHandleScreen.Y - teachingOrientedBoxRotationCenter.Y,
                rotationHandleScreen.X - teachingOrientedBoxRotationCenter.X);
        }

        Viewport.Cursor = GetTeachingOrientedBoxCursor(mode);
        viewModel.ViewerStatus = GetTeachingOrientedBoxStatus(mode, completed: false);
        return true;
    }

    private bool TryUpdateTeachingOrientedBoxEdit(
        Point screenPoint,
        string source = "Viewer pointer")
    {
        if (teachingOrientedBoxEditMode == TeachingOrientedBox3DEditMode.None
            || teachingOrientedBoxDragStart?.OrientedBox3D is not { } start)
        {
            return false;
        }

        ToolRecipeOrientedBox3D updated;
        if (teachingOrientedBoxEditMode == TeachingOrientedBox3DEditMode.Move)
        {
            if (!TryIntersectScreenWithPlane(
                    screenPoint,
                    teachingOrientedBoxMovePlanePoint,
                    teachingOrientedBoxMovePlaneNormal,
                    out var currentWorld))
            {
                return false;
            }

            var delta = ConvertOrientedBoxWorldDeltaToSource(
                currentWorld - teachingOrientedBoxMoveWorldStart);
            updated = start with
            {
                Center = Add(start.Center, delta)
            };
        }
        else if (teachingOrientedBoxEditMode == TeachingOrientedBox3DEditMode.RotateY)
        {
            var currentAngle = Math.Atan2(
                screenPoint.Y - teachingOrientedBoxRotationCenter.Y,
                screenPoint.X - teachingOrientedBoxRotationCenter.X);
            var delta = NormalizeAngle(currentAngle - teachingOrientedBoxRotationStartAngle);
            var axisX = ToVector3(start.AxisX);
            var axisY = Vector3.Normalize(ToVector3(start.AxisY));
            var axisZ = ToVector3(start.AxisZ);
            var cosine = (float)Math.Cos(delta);
            var sine = (float)Math.Sin(delta);
            updated = start with
            {
                AxisX = ToToolRecipeXyz(Vector3.Normalize(axisX * cosine + Vector3.Cross(axisY, axisX) * sine)),
                AxisZ = ToToolRecipeXyz(Vector3.Normalize(axisZ * cosine + Vector3.Cross(axisY, axisZ) * sine))
            };
        }
        else
        {
            if (!TryGetOrientedBoxResizeScreenGeometry(
                    start,
                    teachingOrientedBoxEditMode,
                    out var center,
                    out var handle,
                    out var startHalfExtent))
            {
                return false;
            }

            var screenAxis = handle - center;
            var axisLength = screenAxis.Length;
            if (axisLength <= 0.001)
            {
                return false;
            }

            var unitAxis = screenAxis / axisLength;
            var pointerFromCenter = screenPoint - center;
            var projected = pointerFromCenter.X * unitAxis.X + pointerFromCenter.Y * unitAxis.Y;
            var halfExtent = Math.Max(
                OrientedBoxMinimumHalfExtent,
                startHalfExtent * projected / axisLength);
            updated = SetOrientedBoxHalfExtent(
                start,
                teachingOrientedBoxEditMode,
                halfExtent);
        }

        if (ToolRecipeOrientedBox3DGeometry.Validate(updated).Count > 0)
        {
            return false;
        }

        var selection = teachingOrientedBoxDragStart with { OrientedBox3D = updated };
        teachingOrientedBoxDraft = selection;
        TeachingOrientedBox3DDraftChanged?.Invoke(
            this,
            new TeachingOrientedBox3DDraftChangedEventArgs(selection, source));
        return true;
    }

    private void CompleteTeachingOrientedBoxEdit()
    {
        if (teachingOrientedBoxEditMode == TeachingOrientedBox3DEditMode.None)
        {
            return;
        }

        viewModel.ViewerStatus =
            GetTeachingOrientedBoxStatus(teachingOrientedBoxEditMode, completed: true);
        ClearTeachingOrientedBoxEdit(preserveCursor: false);
        RenderNow();
    }

    private void ClearTeachingOrientedBoxEdit(bool preserveCursor = false)
    {
        teachingOrientedBoxEditMode = TeachingOrientedBox3DEditMode.None;
        teachingOrientedBoxHoverMode = TeachingOrientedBox3DEditMode.None;
        teachingOrientedBoxStatusBeforeHover = null;
        teachingOrientedBoxDragStart = null;
        teachingOrientedBoxMovePlanePoint = default;
        teachingOrientedBoxMovePlaneNormal = default;
        teachingOrientedBoxMoveWorldStart = default;
        teachingOrientedBoxRotationCenter = default;
        teachingOrientedBoxRotationStartAngle = 0;
        if (!preserveCursor && Viewport is not null)
        {
            Viewport.Cursor = Cursors.Arrow;
        }
    }

    private void UpdateTeachingOrientedBoxHover(Point screenPoint)
    {
        if (!TryGetTeachingOrientedBoxDraft(out var selection)
            || selection.OrientedBox3D is not { } box)
        {
            ClearTeachingOrientedBoxHover(restoreStatus: true);
            return;
        }

        var mode = GetTeachingOrientedBoxEditMode(screenPoint, box);
        if (mode == TeachingOrientedBox3DEditMode.None)
        {
            ClearTeachingOrientedBoxHover(restoreStatus: true);
            return;
        }

        var cursor = GetTeachingOrientedBoxCursor(mode);
        if (mode == teachingOrientedBoxHoverMode && Viewport.Cursor == cursor)
        {
            return;
        }

        if (teachingOrientedBoxHoverMode == TeachingOrientedBox3DEditMode.None)
        {
            teachingOrientedBoxStatusBeforeHover = viewModel.ViewerStatus;
        }

        teachingOrientedBoxHoverMode = mode;
        Viewport.Cursor = cursor;
        viewModel.ViewerStatus = GetTeachingOrientedBoxStatus(mode, completed: false);
    }

    private void ClearTeachingOrientedBoxHover(bool restoreStatus)
    {
        if (teachingOrientedBoxHoverMode == TeachingOrientedBox3DEditMode.None)
        {
            return;
        }

        teachingOrientedBoxHoverMode = TeachingOrientedBox3DEditMode.None;
        Viewport.Cursor = Cursors.Arrow;
        if (restoreStatus && teachingOrientedBoxStatusBeforeHover is { } status)
        {
            viewModel.ViewerStatus = status;
        }
        teachingOrientedBoxStatusBeforeHover = null;
    }

    private TeachingOrientedBox3DEditMode GetTeachingOrientedBoxEditMode(
        Point screenPoint,
        ToolRecipeOrientedBox3D box)
    {
        var handles = GetTeachingOrientedBoxHandles(box)
            .Select(handle =>
                TryProjectTeachingOrientedBoxHandle(box, handle, out var projected)
                    ? (Valid: true, handle.Mode, Screen: projected)
                    : (Valid: false, handle.Mode, Screen: default(Point)))
            .Where(item => item.Valid)
            .OrderBy(item => (screenPoint - item.Screen).LengthSquared)
            .ToArray();
        return handles.FirstOrDefault() is { } closest
            && (screenPoint - closest.Screen).LengthSquared
                <= OrientedBoxHandleRadius * OrientedBoxHandleRadius
                ? closest.Mode
                : TeachingOrientedBox3DEditMode.None;
    }

    private bool TrySelectAppliedTeachingOrientedBox(Point screenPoint)
    {
        if (viewModel.IsTeachingCaptureActive || c3dSample is null)
        {
            return false;
        }

        var selection = viewModel.AppliedTeachingSelections
            .Where(IsSelectionForCurrentC3DGrid)
            .Where(item => item.OrientedBox3D is not null)
            .Select(item => (Selection: item, Distance: GetTeachingOrientedBoxScreenDistance(item.OrientedBox3D!, screenPoint)))
            .Where(item => item.Distance <= 14.0)
            .OrderBy(item => item.Distance)
            .Select(item => item.Selection)
            .FirstOrDefault();
        if (selection is null)
        {
            return false;
        }

        viewModel.SetSelectedTeachingSelection(selection.Id);
        viewModel.SelectedEntity = $"Selected 3D Box ROI: {selection.Name}";
        viewModel.ViewerStatus =
            "3D Box selected. Drag a handle or edit numeric values; Apply remains explicit.";
        TeachingSelectionSelected?.Invoke(
            this,
            new TeachingSelectionSelectedEventArgs(selection.Id));
        return true;
    }

    private double GetTeachingOrientedBoxScreenDistance(
        ToolRecipeOrientedBox3D box,
        Point screenPoint)
    {
        var corners = GetOrientedBoxSourceCorners(box)
            .Select(position => CreateOrientedBoxWorldPosition(ToToolRecipeXyz(position)))
            .Select(position =>
                TryProjectWorldPositionToViewport(position, out var projected)
                    ? projected
                    : new Point(double.NaN, double.NaN))
            .ToArray();
        if (corners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
        {
            return double.PositiveInfinity;
        }

        return OrientedBoxEdgeIndices.Min(edge =>
            DistanceToLineSegment(screenPoint, corners[edge.Start], corners[edge.End]));
    }

    private void DrawTeachingOrientedBox(
        OpenGL gl,
        ToolRecipeOrientedBox3D box,
        double red,
        double green,
        double blue,
        bool showHandles)
    {
        var sourceCorners = GetOrientedBoxSourceCorners(box);
        var corners = sourceCorners
            .Select(position => CreateOrientedBoxWorldPosition(ToToolRecipeXyz(position)))
            .ToArray();
        gl.Disable(OpenGL.GL_DEPTH_TEST);
        gl.Enable(OpenGL.GL_BLEND);
        gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

        if (showHandles)
        {
            gl.Color(red, green, blue, 0.08);
            foreach (var face in OrientedBoxFaceIndices)
            {
                gl.Begin(OpenGL.GL_QUADS);
                foreach (var index in face)
                {
                    var corner = corners[index];
                    gl.Vertex(corner.X, corner.Y, corner.Z);
                }
                gl.End();
            }
        }

        gl.LineWidth(showHandles ? 4.0f : 2.0f);
        gl.Color(red, green, blue, showHandles ? 1.0 : 0.72);
        gl.Begin(OpenGL.GL_LINES);
        foreach (var edge in OrientedBoxEdgeIndices)
        {
            var start = corners[edge.Start];
            var end = corners[edge.End];
            gl.Vertex(start.X, start.Y, start.Z);
            gl.Vertex(end.X, end.Y, end.Z);
        }
        gl.End();

        if (showHandles)
        {
            DrawTeachingOrientedBoxHandles(gl, box);
        }

        gl.Disable(OpenGL.GL_BLEND);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
    }

    private void DrawTeachingOrientedBoxHandles(OpenGL gl, ToolRecipeOrientedBox3D box)
    {
        var axisX = ToVector3(box.AxisX);
        var axisZ = ToVector3(box.AxisZ);
        var rotationRadius = GetOrientedBoxRotationRadius(box);

        gl.LineWidth(2.0f);
        gl.Color(0.96, 0.32, 0.78, 0.82);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        for (var index = 0; index < 48; index++)
        {
            var angle = index * Math.PI * 2.0 / 48.0;
            var point = ToVector3(box.Center)
                        + axisX * (float)(Math.Cos(angle) * rotationRadius)
                        + axisZ * (float)(Math.Sin(angle) * rotationRadius);
            var world = CreateOrientedBoxWorldPosition(ToToolRecipeXyz(point));
            gl.Vertex(world.X, world.Y, world.Z);
        }
        gl.End();
    }

    private IEnumerable<TeachingOrientedBoxHandle> GetTeachingOrientedBoxHandles(
        ToolRecipeOrientedBox3D box)
    {
        var center = ToVector3(box.Center);
        var axisX = ToVector3(box.AxisX);
        var axisY = ToVector3(box.AxisY);
        var axisZ = ToVector3(box.AxisZ);
        yield return new TeachingOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.Move,
            box.Center);
        yield return CreateOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.ResizeXPositive,
            center + axisX * (float)box.HalfExtents.X);
        yield return CreateOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.ResizeXNegative,
            center - axisX * (float)box.HalfExtents.X);
        yield return CreateOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.ResizeYPositive,
            center + axisY * (float)box.HalfExtents.Y);
        yield return CreateOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.ResizeYNegative,
            center - axisY * (float)box.HalfExtents.Y);
        yield return CreateOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.ResizeZPositive,
            center + axisZ * (float)box.HalfExtents.Z);
        yield return CreateOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.ResizeZNegative,
            center - axisZ * (float)box.HalfExtents.Z);
        yield return CreateOrientedBoxHandle(
            TeachingOrientedBox3DEditMode.RotateY,
            ToVector3(GetOrientedBoxRotationHandleSourcePosition(box)));
    }

    private static TeachingOrientedBoxHandle CreateOrientedBoxHandle(
        TeachingOrientedBox3DEditMode mode,
        Vector3 position) =>
        new(mode, ToToolRecipeXyz(position));

    private bool TryGetTeachingOrientedBoxDraft(out ToolRecipeSelection selection)
    {
        if (HasVisibleTeachingOrientedBoxDraft
            && teachingOrientedBoxDraft is { } draft)
        {
            selection = draft;
            return true;
        }

        selection = default!;
        return false;
    }

    private bool TryGetOrientedBoxResizeScreenGeometry(
        ToolRecipeOrientedBox3D box,
        TeachingOrientedBox3DEditMode mode,
        out Point center,
        out Point handle,
        out double halfExtent)
    {
        center = handle = default;
        halfExtent = mode switch
        {
            TeachingOrientedBox3DEditMode.ResizeXNegative
                or TeachingOrientedBox3DEditMode.ResizeXPositive => box.HalfExtents.X,
            TeachingOrientedBox3DEditMode.ResizeYNegative
                or TeachingOrientedBox3DEditMode.ResizeYPositive => box.HalfExtents.Y,
            TeachingOrientedBox3DEditMode.ResizeZNegative
                or TeachingOrientedBox3DEditMode.ResizeZPositive => box.HalfExtents.Z,
            _ => double.NaN
        };
        var sourceHandle = GetTeachingOrientedBoxHandles(box)
            .FirstOrDefault(item => item.Mode == mode);
        return double.IsFinite(halfExtent)
            && halfExtent > 0
            && sourceHandle.Mode != TeachingOrientedBox3DEditMode.None
            && TryProjectWorldPositionToViewport(
                CreateOrientedBoxWorldPosition(box.Center),
                out center)
            && TryProjectTeachingOrientedBoxHandle(box, sourceHandle, out handle);
    }

    private bool TryProjectTeachingOrientedBoxHandle(
        ToolRecipeOrientedBox3D box,
        TeachingOrientedBoxHandle handle,
        out Point screenPoint)
    {
        screenPoint = default;
        if (!TryProjectWorldPositionToViewport(
                CreateOrientedBoxWorldPosition(box.Center),
                out var center))
        {
            return false;
        }

        if (handle.Mode == TeachingOrientedBox3DEditMode.Move)
        {
            screenPoint = center;
            return true;
        }

        if (TryProjectWorldPositionToViewport(
                CreateOrientedBoxWorldPosition(handle.SourcePosition),
                out var projected)
            && (projected - center).Length >= 28.0)
        {
            screenPoint = projected;
            return true;
        }

        var fallback = GetTeachingOrientedBoxFallbackDirection(handle.Mode);
        screenPoint = center + fallback * (
            handle.Mode == TeachingOrientedBox3DEditMode.RotateY ? 82.0 : 62.0);
        return true;
    }

    private void UpdateTeachingOrientedBoxHandleOverlay()
    {
        TeachingOrientedBoxHandleOverlay.Children.Clear();
        if (!TryGetTeachingOrientedBoxDraft(out var selection)
            || selection.OrientedBox3D is not { } box
            || !TryProjectWorldPositionToViewport(
                CreateOrientedBoxWorldPosition(box.Center),
                out var center))
        {
            TeachingOrientedBoxHandleOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var handle in GetTeachingOrientedBoxHandles(box))
        {
            if (!TryProjectTeachingOrientedBoxHandle(box, handle, out var point))
            {
                continue;
            }

            var color = GetTeachingOrientedBoxHandleColor(handle.Mode);
            if (handle.Mode != TeachingOrientedBox3DEditMode.Move)
            {
                TeachingOrientedBoxHandleOverlay.Children.Add(new Line
                {
                    X1 = center.X,
                    Y1 = center.Y,
                    X2 = point.X,
                    Y2 = point.Y,
                    Stroke = color,
                    StrokeThickness =
                        handle.Mode == TeachingOrientedBox3DEditMode.RotateY ? 2.5 : 1.5,
                    Opacity = 0.82
                });
            }

            var diameter = handle.Mode == TeachingOrientedBox3DEditMode.Move ? 20.0 : 16.0;
            var grip = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = color,
                Stroke = Brushes.Black,
                StrokeThickness = 2.5
            };
            Canvas.SetLeft(grip, point.X - diameter * 0.5);
            Canvas.SetTop(grip, point.Y - diameter * 0.5);
            TeachingOrientedBoxHandleOverlay.Children.Add(grip);

            var labelText = GetTeachingOrientedBoxHandleLabel(handle.Mode);
            if (labelText is null)
            {
                continue;
            }

            var label = new TextBlock
            {
                Text = labelText,
                Foreground = color,
                Background = new SolidColorBrush(Color.FromArgb(216, 17, 24, 39)),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(3, 0, 3, 0)
            };
            Canvas.SetLeft(label, point.X + 10.0);
            Canvas.SetTop(label, point.Y - 9.0);
            TeachingOrientedBoxHandleOverlay.Children.Add(label);
        }

        TeachingOrientedBoxHandleOverlay.Visibility = Visibility.Visible;
    }

    private static System.Windows.Vector GetTeachingOrientedBoxFallbackDirection(
        TeachingOrientedBox3DEditMode mode) =>
        mode switch
        {
            TeachingOrientedBox3DEditMode.ResizeXPositive => new System.Windows.Vector(1, 0),
            TeachingOrientedBox3DEditMode.ResizeXNegative => new System.Windows.Vector(-1, 0),
            TeachingOrientedBox3DEditMode.ResizeYPositive => new System.Windows.Vector(0, -1),
            TeachingOrientedBox3DEditMode.ResizeYNegative => new System.Windows.Vector(0, 1),
            TeachingOrientedBox3DEditMode.ResizeZPositive => new System.Windows.Vector(0.707, 0.707),
            TeachingOrientedBox3DEditMode.ResizeZNegative => new System.Windows.Vector(-0.707, -0.707),
            TeachingOrientedBox3DEditMode.RotateY => new System.Windows.Vector(0.707, -0.707),
            _ => default
        };

    private static Brush GetTeachingOrientedBoxHandleColor(
        TeachingOrientedBox3DEditMode mode) =>
        mode switch
        {
            TeachingOrientedBox3DEditMode.Move => Brushes.White,
            TeachingOrientedBox3DEditMode.ResizeXNegative
                or TeachingOrientedBox3DEditMode.ResizeXPositive => Brushes.OrangeRed,
            TeachingOrientedBox3DEditMode.ResizeYNegative
                or TeachingOrientedBox3DEditMode.ResizeYPositive => Brushes.LimeGreen,
            TeachingOrientedBox3DEditMode.ResizeZNegative
                or TeachingOrientedBox3DEditMode.ResizeZPositive => Brushes.DodgerBlue,
            TeachingOrientedBox3DEditMode.RotateY => Brushes.HotPink,
            _ => Brushes.White
        };

    private static string? GetTeachingOrientedBoxHandleLabel(
        TeachingOrientedBox3DEditMode mode) =>
        mode switch
        {
            TeachingOrientedBox3DEditMode.ResizeYPositive => "Y size",
            TeachingOrientedBox3DEditMode.RotateY => "Rotate Y",
            _ => null
        };

    private ToolRecipeOrientedBox3D SetOrientedBoxHalfExtent(
        ToolRecipeOrientedBox3D box,
        TeachingOrientedBox3DEditMode mode,
        double halfExtent)
    {
        var extents = box.HalfExtents;
        return box with
        {
            HalfExtents = mode switch
            {
                TeachingOrientedBox3DEditMode.ResizeXNegative
                    or TeachingOrientedBox3DEditMode.ResizeXPositive =>
                    extents with { X = halfExtent },
                TeachingOrientedBox3DEditMode.ResizeYNegative
                    or TeachingOrientedBox3DEditMode.ResizeYPositive =>
                    extents with { Y = halfExtent },
                TeachingOrientedBox3DEditMode.ResizeZNegative
                    or TeachingOrientedBox3DEditMode.ResizeZPositive =>
                    extents with { Z = halfExtent },
                _ => extents
            }
        };
    }

    private bool TryIntersectScreenWithPlane(
        Point screenPoint,
        Vector3 planePoint,
        Vector3 planeNormal,
        out Vector3 intersection)
    {
        intersection = default;
        var ray = CreatePickRay(screenPoint);
        var denominator = Vector3.Dot(ray.direction, planeNormal);
        if (Math.Abs(denominator) <= 0.000001f)
        {
            return false;
        }

        var distance = Vector3.Dot(planePoint - ray.origin, planeNormal) / denominator;
        if (!float.IsFinite(distance) || distance < 0)
        {
            return false;
        }

        intersection = ray.origin + ray.direction * distance;
        return true;
    }

    private Vector3 ConvertOrientedBoxWorldDeltaToSource(Vector3 worldDelta)
    {
        var origin = CreateOrientedBoxWorldPosition(new ToolRecipeXyz(0, 0, 0));
        var axisX = CreateOrientedBoxWorldPosition(new ToolRecipeXyz(1, 0, 0)) - origin;
        var axisY = CreateOrientedBoxWorldPosition(new ToolRecipeXyz(0, 1, 0)) - origin;
        var axisZ = CreateOrientedBoxWorldPosition(new ToolRecipeXyz(0, 0, 1)) - origin;
        return new Vector3(
            ProjectWorldDeltaOntoSourceAxis(worldDelta, axisX),
            ProjectWorldDeltaOntoSourceAxis(worldDelta, axisY),
            ProjectWorldDeltaOntoSourceAxis(worldDelta, axisZ));
    }

    private static float ProjectWorldDeltaOntoSourceAxis(Vector3 delta, Vector3 axis)
    {
        var lengthSquared = axis.LengthSquared();
        return lengthSquared <= 0.0000001f
            ? 0
            : Vector3.Dot(delta, axis) / lengthSquared;
    }

    private Vector3 CreateOrientedBoxWorldPosition(ToolRecipeXyz sourcePosition) =>
        CreateC3DGridDisplayPosition(
            sourcePosition.Z,
            sourcePosition.X,
            sourcePosition.Y);

    private static IReadOnlyList<Vector3> GetOrientedBoxSourceCorners(
        ToolRecipeOrientedBox3D box)
    {
        var center = ToVector3(box.Center);
        var x = ToVector3(box.AxisX) * (float)box.HalfExtents.X;
        var y = ToVector3(box.AxisY) * (float)box.HalfExtents.Y;
        var z = ToVector3(box.AxisZ) * (float)box.HalfExtents.Z;
        return
        [
            center - x - y - z,
            center + x - y - z,
            center + x + y - z,
            center - x + y - z,
            center - x - y + z,
            center + x - y + z,
            center + x + y + z,
            center - x + y + z
        ];
    }

    private static ToolRecipeXyz GetOrientedBoxRotationHandleSourcePosition(
        ToolRecipeOrientedBox3D box)
    {
        var point = ToVector3(box.Center)
                    + ToVector3(box.AxisX) * (float)GetOrientedBoxRotationRadius(box);
        return ToToolRecipeXyz(point);
    }

    private static double GetOrientedBoxRotationRadius(ToolRecipeOrientedBox3D box) =>
        Math.Max(box.HalfExtents.X, box.HalfExtents.Z)
        + Math.Max(1.0, Math.Max(box.HalfExtents.X, box.HalfExtents.Z) * 0.22);

    private static ToolRecipeXyz Add(ToolRecipeXyz point, Vector3 delta) =>
        new(point.X + delta.X, point.Y + delta.Y, point.Z + delta.Z);

    private static Vector3 ToVector3(ToolRecipeXyz value) =>
        new((float)value.X, (float)value.Y, (float)value.Z);

    private static ToolRecipeXyz ToToolRecipeXyz(Vector3 value) =>
        new(value.X, value.Y, value.Z);

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= Math.PI * 2.0;
        }
        while (angle < -Math.PI)
        {
            angle += Math.PI * 2.0;
        }
        return angle;
    }

    private static Cursor GetTeachingOrientedBoxCursor(
        TeachingOrientedBox3DEditMode mode) =>
        mode switch
        {
            TeachingOrientedBox3DEditMode.Move => Cursors.SizeAll,
            TeachingOrientedBox3DEditMode.ResizeYNegative
                or TeachingOrientedBox3DEditMode.ResizeYPositive => Cursors.SizeNS,
            TeachingOrientedBox3DEditMode.RotateY => Cursors.Hand,
            TeachingOrientedBox3DEditMode.None => Cursors.Arrow,
            _ => Cursors.SizeNWSE
        };

    private static string GetTeachingOrientedBoxStatus(
        TeachingOrientedBox3DEditMode mode,
        bool completed)
    {
        var action = mode switch
        {
            TeachingOrientedBox3DEditMode.Move => "center move",
            TeachingOrientedBox3DEditMode.ResizeYNegative
                or TeachingOrientedBox3DEditMode.ResizeYPositive => "height resize",
            TeachingOrientedBox3DEditMode.ResizeXNegative
                or TeachingOrientedBox3DEditMode.ResizeXPositive => "X size",
            TeachingOrientedBox3DEditMode.ResizeZNegative
                or TeachingOrientedBox3DEditMode.ResizeZPositive => "Z size",
            TeachingOrientedBox3DEditMode.RotateY => "Y-axis rotation",
            _ => "edit"
        };
        return completed
            ? $"3D Box {action} completed in Review; Enter Apply or Esc Cancel."
            : $"3D Box {action}; drag the handle. The recipe and inspection remain unchanged until Apply.";
    }

    private static readonly (int Start, int End)[] OrientedBoxEdgeIndices =
    [
        (0, 1), (1, 2), (2, 3), (3, 0),
        (4, 5), (5, 6), (6, 7), (7, 4),
        (0, 4), (1, 5), (2, 6), (3, 7)
    ];

    private static readonly int[][] OrientedBoxFaceIndices =
    [
        [0, 1, 2, 3],
        [4, 5, 6, 7],
        [0, 1, 5, 4],
        [1, 2, 6, 5],
        [2, 3, 7, 6],
        [3, 0, 4, 7]
    ];

    private readonly record struct TeachingOrientedBoxHandle(
        TeachingOrientedBox3DEditMode Mode,
        ToolRecipeXyz SourcePosition);

    private enum TeachingOrientedBox3DEditMode
    {
        None,
        Move,
        ResizeXNegative,
        ResizeXPositive,
        ResizeYNegative,
        ResizeYPositive,
        ResizeZNegative,
        ResizeZPositive,
        RotateY
    }
}

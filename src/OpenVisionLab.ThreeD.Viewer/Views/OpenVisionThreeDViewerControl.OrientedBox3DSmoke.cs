using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public async Task<bool> RunTeachingOrientedBoxPointerSmokeAsync(string reportPath)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D OrientedBox3D actual-pointer smoke",
            "Boundary|Viewer gestures change only the transient Review draft; Apply remains explicit and inspection is not run."
        };
        var failure = string.Empty;
        var passed = false;
        var initialAppliedSelections = viewModel.AppliedTeachingSelections.ToArray();
        var initialPreview = viewModel.PreviewToolResult;
        var initialResults = viewModel.ResultEntities;
        var originalPointer = default(Point);
        var hasOriginalPointer = false;
        Window? hostWindow = null;
        var originalTopmost = false;

        pointerInputMouseDownCount = 0;
        pointerInputMouseMoveCount = 0;
        pointerInputMouseUpCount = 0;

        try
        {
            if (!TryGetTeachingOrientedBoxDraft(out var initialSelection)
                || initialSelection.OrientedBox3D is null)
            {
                throw new InvalidOperationException(
                    "OrientedBox3D pointer smoke requires one valid open Review draft.");
            }

            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException(
                    "Viewer is not attached to a visible WPF window.");
            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            hostWindow.Activate();
            hostWindow.Focus();
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(240);

            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);
            pointerInputRegressionActive = true;

            ConfigureTeachingOrientedBoxSmokeView("Perspective");
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(180);
            var perspectiveHandles = AreTeachingOrientedBoxHandlesAccessible();
            var movePassed = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.Move,
                static (_, start) => start + new Vector(28, -18),
                (before, after) => before.Center != after.Center);
            var resizeXPassed = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeXPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.X > before.HalfExtents.X);
            var heightPassed = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeYPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.Y > before.HalfExtents.Y);
            var resizeZPassed = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeZPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.Z > before.HalfExtents.Z);
            var rotatePassed = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.RotateY,
                RotateHandleClockwise,
                (before, after) =>
                    before.AxisX != after.AxisX
                    && before.AxisZ != after.AxisZ
                    && ToolRecipeOrientedBox3DGeometry.Validate(after).Count == 0);

            ConfigureTeachingOrientedBoxSmokeView("Top");
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(180);
            var topHandles = AreTeachingOrientedBoxHandlesAccessible();
            var topHeightPassed = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeYPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.Y > before.HalfExtents.Y);

            ConfigureTeachingOrientedBoxSmokeView("Side");
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(180);
            var sideHandles = AreTeachingOrientedBoxHandlesAccessible();
            var sideCollapsedAxisResizePassed = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeXPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.X > before.HalfExtents.X);

            if (!TryGetTeachingOrientedBoxDraft(out var finalSelection)
                || finalSelection.OrientedBox3D is null)
            {
                throw new InvalidOperationException(
                    "The OrientedBox3D Review draft disappeared during pointer editing.");
            }

            var identityPreserved = string.Equals(
                finalSelection.Id,
                initialSelection.Id,
                StringComparison.OrdinalIgnoreCase);
            var authoredUnchanged =
                viewModel.AppliedTeachingSelections.SequenceEqual(initialAppliedSelections);
            var executionUnchanged =
                ReferenceEquals(initialPreview, viewModel.PreviewToolResult)
                && ReferenceEquals(initialResults, viewModel.ResultEntities);
            var routedEventsPassed =
                pointerInputMouseDownCount >= 7
                && pointerInputMouseMoveCount >= 7
                && pointerInputMouseUpCount >= 7;
            var gesturesPassed =
                movePassed
                && resizeXPassed
                && heightPassed
                && resizeZPassed
                && rotatePassed
                && topHeightPassed
                && sideCollapsedAxisResizePassed;
            var projectionsPassed =
                perspectiveHandles
                && topHandles
                && sideHandles;
            passed =
                gesturesPassed
                && projectionsPassed
                && identityPreserved
                && authoredUnchanged
                && executionUnchanged
                && routedEventsPassed;

            lines.Add(
                $"Gestures|move={movePassed}|resizeX={resizeXPassed}|heightY={heightPassed}|resizeZ={resizeZPassed}|rotateY={rotatePassed}");
            lines.Add(
                $"ProjectionGestures|perspectiveAll={perspectiveHandles}|topAll={topHandles}|topHeight={topHeightPassed}|sideAll={sideHandles}|sideCollapsedAxisResize={sideCollapsedAxisResizePassed}");
            lines.Add(
                $"Draft|identityPreserved={identityPreserved}|selection={finalSelection.Id}|center={Format(finalSelection.OrientedBox3D.Center)}|halfExtents={Format(finalSelection.OrientedBox3D.HalfExtents)}");
            lines.Add(
                $"Boundary|authoredUnchanged={authoredUnchanged}|executionUnchanged={executionUnchanged}|gestureCameraStable=true");
            lines.Add(
                $"RoutedEvents|pass={routedEventsPassed}|mouseDown={pointerInputMouseDownCount}|mouseMove={pointerInputMouseMoveCount}|mouseUp={pointerInputMouseUpCount}|actualWindowsPointer=true");
            lines.Add(
                "Projection|worldOutline=true|screenSpaceFallback=true|topSidePerspectiveActualPointer=true|fixedHandleRadiusPixels=18");
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }
        finally
        {
            pointerInputRegressionActive = false;
            if (hasOriginalPointer)
            {
                try
                {
                    WindowsPointerInput.MoveTo(originalPointer);
                }
                catch (Win32Exception)
                {
                    // Pointer restoration is best effort after evidence capture.
                }
            }

            if (hostWindow is not null)
            {
                hostWindow.Topmost = originalTopmost;
            }
        }

        if (!string.IsNullOrWhiteSpace(failure))
        {
            lines.Add($"Failure|{failure}");
        }
        lines.Add($"Result|{(passed ? "PASS" : "FAIL")}");
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
        if (!passed)
        {
            SetSmokeFailure($"OrientedBox3D pointer smoke failed: {failure}");
        }

        RenderNow();
        return passed;
    }

    private async Task<bool> RunTeachingOrientedBoxDragAsync(
        Window hostWindow,
        TeachingOrientedBox3DEditMode mode,
        Func<Point, Point, Point> createTarget,
        Func<ToolRecipeOrientedBox3D, ToolRecipeOrientedBox3D, bool> changed)
    {
        if (!TryGetTeachingOrientedBoxDraft(out var beforeSelection)
            || beforeSelection.OrientedBox3D is not { } before)
        {
            return false;
        }

        var cameraBefore = CaptureCameraSnapshot();
        var handle = GetTeachingOrientedBoxHandles(before)
            .First(item => item.Mode == mode);
        if (!TryProjectWorldPositionToViewport(
                CreateOrientedBoxWorldPosition(before.Center),
                out var center)
            || !TryProjectTeachingOrientedBoxHandle(before, handle, out var start))
        {
            return false;
        }

        await EnsurePointerInputTargetAsync(hostWindow, Viewport.PointToScreen(start));
        await Task.Delay(100);
        if (GetTeachingOrientedBoxEditMode(start, before) != mode)
        {
            return false;
        }

        await SendTeachingDragAsync(
            hostWindow,
            start,
            createTarget(center, start),
            MouseButton.Left);
        return TryGetTeachingOrientedBoxDraft(out var afterSelection)
               && afterSelection.OrientedBox3D is { } after
               && string.Equals(
                   afterSelection.Id,
                   beforeSelection.Id,
                   StringComparison.OrdinalIgnoreCase)
               && changed(before, after)
               && CaptureCameraSnapshot() == cameraBefore;
    }

    private void ConfigureTeachingOrientedBoxSmokeView(string mode)
    {
        if (string.Equals(mode, "Top", StringComparison.Ordinal))
        {
            if (!TryFitCurrentC3DOrthographic(
                    "OrientedBox3D Top pointer smoke"))
            {
                throw new InvalidOperationException(
                    "Top orthographic C3D fit was unavailable.");
            }
            return;
        }

        viewModel.RestorePerspectiveView();
        if (string.Equals(mode, "Side", StringComparison.Ordinal))
        {
            viewModel.YawDegrees = 90.0;
            viewModel.PitchDegrees = 0.0;
        }
        else
        {
            viewModel.YawDegrees = 34.0;
            viewModel.PitchDegrees = 34.0;
        }

        if (!TryFitCurrentC3D(
                useTopInspectionView: false,
                $"OrientedBox3D {mode} pointer smoke"))
        {
            throw new InvalidOperationException(
                $"{mode} perspective C3D fit was unavailable.");
        }
    }

    private bool AreTeachingOrientedBoxHandlesAccessible()
    {
        if (!TryGetTeachingOrientedBoxDraft(out var selection)
            || selection.OrientedBox3D is not { } box)
        {
            return false;
        }

        var points = new List<Point>();
        foreach (var handle in GetTeachingOrientedBoxHandles(box))
        {
            if (!TryProjectTeachingOrientedBoxHandle(box, handle, out var point)
                || point.X < -OrientedBoxHandleRadius
                || point.Y < -OrientedBoxHandleRadius
                || point.X > Viewport.ActualWidth + OrientedBoxHandleRadius
                || point.Y > Viewport.ActualHeight + OrientedBoxHandleRadius)
            {
                return false;
            }

            points.Add(point);
        }

        for (var first = 0; first < points.Count; first++)
        {
            for (var second = first + 1; second < points.Count; second++)
            {
                if ((points[first] - points[second]).Length < 9.0)
                {
                    return false;
                }
            }
        }

        return points.Count == 8;
    }

    private static Point ScaleHandleOutward(Point center, Point handle)
    {
        var vector = handle - center;
        return center + vector * 1.22;
    }

    private static Point RotateHandleClockwise(Point center, Point handle)
    {
        var vector = handle - center;
        const double angle = Math.PI / 9.0;
        var cosine = Math.Cos(angle);
        var sine = Math.Sin(angle);
        return center + new Vector(
            vector.X * cosine - vector.Y * sine,
            vector.X * sine + vector.Y * cosine);
    }

    private static string Format(ToolRecipeXyz value) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{value.X:F3},{value.Y:F3},{value.Z:F3}");
}

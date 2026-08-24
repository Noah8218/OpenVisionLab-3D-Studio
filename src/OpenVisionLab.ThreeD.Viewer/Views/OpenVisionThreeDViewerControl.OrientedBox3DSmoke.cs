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
        var hoverRecovery = default(OrientedBoxHoverRecoverySmokeResult);

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
            hoverRecovery = await RunTeachingOrientedBoxHoverRecoveryAsync(
                hostWindow);
            var moveResult = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.Move,
                static (_, start) => start + new Vector(28, -18),
                (before, after) => before.Center != after.Center,
                "perspective-move",
                lines);
            var resizeXResult = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeXPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.X > before.HalfExtents.X,
                "perspective-resize-x",
                lines);
            var heightResult = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeYPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.Y > before.HalfExtents.Y,
                "perspective-resize-y",
                lines);
            var resizeZResult = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeZPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.Z > before.HalfExtents.Z,
                "perspective-resize-z",
                lines);
            var rotateResult = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.RotateY,
                RotateHandleClockwise,
                (before, after) =>
                    before.AxisX != after.AxisX
                    && before.AxisZ != after.AxisZ
                    && ToolRecipeOrientedBox3DGeometry.Validate(after).Count == 0,
                "perspective-rotate-y",
                lines);

            ConfigureTeachingOrientedBoxSmokeView("Top");
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(180);
            var topHandles = AreTeachingOrientedBoxHandlesAccessible();
            var topHeightResult = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeYPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.Y > before.HalfExtents.Y,
                "top-resize-y",
                lines);

            ConfigureTeachingOrientedBoxSmokeView("Side");
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(180);
            var sideHandles = AreTeachingOrientedBoxHandlesAccessible();
            var sideCollapsedAxisResizeResult = await RunTeachingOrientedBoxDragAsync(
                hostWindow,
                TeachingOrientedBox3DEditMode.ResizeXPositive,
                ScaleHandleOutward,
                (before, after) =>
                    after.HalfExtents.X > before.HalfExtents.X,
                "side-collapsed-resize-x",
                lines);

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
                moveResult.Passed
                && resizeXResult.Passed
                && heightResult.Passed
                && resizeZResult.Passed
                && rotateResult.Passed
                && topHeightResult.Passed
                && sideCollapsedAxisResizeResult.Passed;
            var gestureCameraStable =
                moveResult.CameraStable
                && resizeXResult.CameraStable
                && heightResult.CameraStable
                && resizeZResult.CameraStable
                && rotateResult.CameraStable
                && topHeightResult.CameraStable
                && sideCollapsedAxisResizeResult.CameraStable;
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
                && routedEventsPassed
                && hoverRecovery.Passed;

            lines.Add(
                $"Gestures|move={moveResult.Passed}|resizeX={resizeXResult.Passed}|heightY={heightResult.Passed}|resizeZ={resizeZResult.Passed}|rotateY={rotateResult.Passed}");
            lines.Add(
                $"ProjectionGestures|perspectiveAll={perspectiveHandles}|topAll={topHandles}|topHeight={topHeightResult.Passed}|sideAll={sideHandles}|sideCollapsedAxisResize={sideCollapsedAxisResizeResult.Passed}");
            lines.Add(
                $"Draft|identityPreserved={identityPreserved}|selection={finalSelection.Id}|center={Format(finalSelection.OrientedBox3D.Center)}|halfExtents={Format(finalSelection.OrientedBox3D.HalfExtents)}");
            lines.Add(
                $"Boundary|authoredUnchanged={authoredUnchanged}|executionUnchanged={executionUnchanged}|gestureCameraStable={gestureCameraStable}");
            lines.Add(
                $"RoutedEvents|pass={routedEventsPassed}|mouseDown={pointerInputMouseDownCount}|mouseMove={pointerInputMouseMoveCount}|mouseUp={pointerInputMouseUpCount}|actualWindowsPointer=true");
            lines.Add(
                $"InteractionStates|normal={hoverRecovery.NormalPassed}|hover={hoverRecovery.HoverPassed}|pressedReleased={routedEventsPassed}|mouseLeaveRecovery={hoverRecovery.MouseLeaveRecoveryPassed}|cursorRecovery={hoverRecovery.CursorRecoveryPassed}|statusRecovery={hoverRecovery.StatusRecoveryPassed}");
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
        lines.Add(
            $"OrientedBox3DPointerVerification|{(passed ? "PASS" : "FAIL")}|gestures=7|projections=3|handlesPerProjection=8|actualWindowsPointer=true|hoverLeaveRecovery={hoverRecovery.Passed}");
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

    private async Task<OrientedBoxHoverRecoverySmokeResult> RunTeachingOrientedBoxHoverRecoveryAsync(
        Window hostWindow)
    {
        if (!TryGetTeachingOrientedBoxDraft(out var selection)
            || selection.OrientedBox3D is not { } box)
        {
            return default;
        }

        var viewportTopLeft = Viewport.PointToScreen(new Point(0, 0));
        var outsideViewport = new Point(
            viewportTopLeft.X - OrientedBoxHandleRadius * 2,
            viewportTopLeft.Y - OrientedBoxHandleRadius * 2);
        WindowsPointerInput.MoveTo(outsideViewport);
        await Task.Delay(160);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        var statusBeforeHover = viewModel.ViewerStatus;
        var normalPassed =
            !Viewport.IsMouseOver
            && teachingOrientedBoxHoverMode == TeachingOrientedBox3DEditMode.None
            && Viewport.Cursor == Cursors.Arrow;
        var moveHandle = GetTeachingOrientedBoxHandles(box).First(handle =>
            handle.Mode == TeachingOrientedBox3DEditMode.Move);
        if (!TryProjectTeachingOrientedBoxHandle(box, moveHandle, out var hoverPoint))
        {
            return default;
        }

        var hoverPassed = false;
        for (var attempt = 0; attempt < 3 && !hoverPassed; attempt++)
        {
            await EnsurePointerInputTargetAsync(
                hostWindow,
                Viewport.PointToScreen(hoverPoint));
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            hoverPassed =
                teachingOrientedBoxHoverMode == TeachingOrientedBox3DEditMode.Move
                && Viewport.Cursor == Cursors.SizeAll
                && string.Equals(
                    viewModel.ViewerStatus,
                    GetTeachingOrientedBoxStatus(
                        TeachingOrientedBox3DEditMode.Move,
                        completed: false),
                    StringComparison.Ordinal);
        }
        WindowsPointerInput.MoveTo(outsideViewport);
        await Task.Delay(160);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
        var mouseLeaveRecoveryPassed =
            !Viewport.IsMouseOver
            && teachingOrientedBoxHoverMode == TeachingOrientedBox3DEditMode.None;
        var cursorRecoveryPassed = Viewport.Cursor == Cursors.Arrow;
        var statusRecoveryPassed = string.Equals(
            viewModel.ViewerStatus,
            statusBeforeHover,
            StringComparison.Ordinal);
        return new OrientedBoxHoverRecoverySmokeResult(
            normalPassed,
            hoverPassed,
            mouseLeaveRecoveryPassed,
            cursorRecoveryPassed,
            statusRecoveryPassed);
    }

    private async Task<OrientedBoxDragSmokeResult> RunTeachingOrientedBoxDragAsync(
        Window hostWindow,
        TeachingOrientedBox3DEditMode mode,
        Func<Point, Point, Point> createTarget,
        Func<ToolRecipeOrientedBox3D, ToolRecipeOrientedBox3D, bool> changed,
        string evidenceName,
        ICollection<string> evidence)
    {
        if (!TryGetTeachingOrientedBoxDraft(out var beforeSelection)
            || beforeSelection.OrientedBox3D is not { } before)
        {
            evidence.Add($"Gesture|name={evidenceName}|pass=False|failure=draft-unavailable");
            return new OrientedBoxDragSmokeResult(false, true);
        }

        var cameraBefore = CaptureCameraSnapshot();
        var handle = GetTeachingOrientedBoxHandles(before)
            .First(item => item.Mode == mode);
        if (!TryProjectWorldPositionToViewport(
                CreateOrientedBoxWorldPosition(before.Center),
                out var center)
            || !TryProjectTeachingOrientedBoxHandle(before, handle, out var start))
        {
            evidence.Add($"Gesture|name={evidenceName}|pass=False|failure=projection-unavailable");
            return new OrientedBoxDragSmokeResult(false, true);
        }

        var modeMatched = false;
        var attempts = 0;
        for (attempts = 1; attempts <= 3; attempts++)
        {
            if (attempts > 1)
            {
                var viewportTopLeft = Viewport.PointToScreen(new Point(0, 0));
                WindowsPointerInput.MoveTo(new Point(
                    viewportTopLeft.X - OrientedBoxHandleRadius * 2,
                    viewportTopLeft.Y - OrientedBoxHandleRadius * 2));
                await Task.Delay(100);
            }

            await EnsurePointerInputTargetAsync(
                hostWindow,
                Viewport.PointToScreen(start));
            await Task.Delay(100);
            modeMatched =
                GetTeachingOrientedBoxEditMode(start, before) == mode
                && teachingOrientedBoxHoverMode == mode
                && Viewport.Cursor == GetTeachingOrientedBoxCursor(mode);
            if (modeMatched)
            {
                break;
            }

            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(100);
        }

        if (!modeMatched)
        {
            var cameraStableWithoutInput = CaptureCameraSnapshot() == cameraBefore;
            evidence.Add(
                $"Gesture|name={evidenceName}|pass=False|hit=False|attempts=3|cameraStable={cameraStableWithoutInput}");
            return new OrientedBoxDragSmokeResult(false, cameraStableWithoutInput);
        }

        await SendTeachingDragAsync(
            hostWindow,
            start,
            createTarget(center, start),
            MouseButton.Left);
        var hasAfter = TryGetTeachingOrientedBoxDraft(out var afterSelection)
            && afterSelection.OrientedBox3D is not null;
        var identityPreserved = hasAfter
            && string.Equals(
                afterSelection.Id,
                beforeSelection.Id,
                StringComparison.OrdinalIgnoreCase);
        var geometryChanged = hasAfter
            && changed(before, afterSelection.OrientedBox3D!);
        var cameraStable = CaptureCameraSnapshot() == cameraBefore;
        var passed = hasAfter
            && identityPreserved
            && geometryChanged
            && cameraStable;
        evidence.Add(
            $"Gesture|name={evidenceName}|pass={passed}|hit=True|attempts={attempts}|identityPreserved={identityPreserved}|geometryChanged={geometryChanged}|cameraStable={cameraStable}");
        return new OrientedBoxDragSmokeResult(passed, cameraStable);
    }

    private void ConfigureTeachingOrientedBoxSmokeView(string mode)
    {
        if (c3dSample is not null)
        {
            viewModel.C3DSampleVisible = true;
        }

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
                $"{mode} perspective C3D fit was unavailable "
                + $"(visible={viewModel.C3DSampleVisible}; source={CurrentC3DSourcePath ?? "(none)"}).");
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

    private readonly record struct OrientedBoxDragSmokeResult(
        bool Passed,
        bool CameraStable);

    private readonly record struct OrientedBoxHoverRecoverySmokeResult(
        bool NormalPassed,
        bool HoverPassed,
        bool MouseLeaveRecoveryPassed,
        bool CursorRecoveryPassed,
        bool StatusRecoveryPassed)
    {
        public bool Passed =>
            NormalPassed
            && HoverPassed
            && MouseLeaveRecoveryPassed
            && CursorRecoveryPassed
            && StatusRecoveryPassed;
    }
}

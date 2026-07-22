using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private string? smokeTeachingCapturePointerReportPath;

    public bool HasConfiguredTeachingCapturePointerSmoke =>
        smokeTeachingCapturePointerReportPath is not null;

    public Task<bool> RunTeachingCapturePointerSmokeAsync() =>
        RunTeachingCapturePointerSmokeAsync(
            cancelWhenReady: false,
            smokeTeachingCapturePointerReportPath,
            exerciseNavigationGestures: true);

    public Task<bool> RunTeachingCapturePointerSmokeAsync(bool exerciseNavigationGestures) =>
        RunTeachingCapturePointerSmokeAsync(
            cancelWhenReady: false,
            smokeTeachingCapturePointerReportPath,
            exerciseNavigationGestures);

    public async Task<bool> RunTeachingCapturePointerSmokeAsync(
        bool cancelWhenReady,
        string? reportPath,
        bool exerciseNavigationGestures = true)
    {
        var result = await RunTeachingCapturePointerSmokeCoreAsync(
            cancelWhenReady,
            exerciseNavigationGestures);
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            WriteTeachingCapturePointerSmokeReport(reportPath, result);
        }

        if (!result.Passed)
        {
            SetSmokeFailure($"Teaching-capture pointer smoke failed: {result.Failure}");
        }

        RenderNow();
        return result.Passed;
    }

    private async Task<TeachingCapturePointerSmokeResult> RunTeachingCapturePointerSmokeCoreAsync(
        bool cancelWhenReady,
        bool exerciseNavigationGestures)
    {
        var initialCamera = CaptureCameraSnapshot();
        var orbitCamera = initialCamera;
        var panCamera = initialCamera;
        var firstLocator = "(none)";
        var secondLocator = "(none)";
        var windowActivated = false;
        var firstClickPassed = false;
        var orbitPassed = !exerciseNavigationGestures;
        var panPassed = !exerciseNavigationGestures;
        var contextMenuPassed = !exerciseNavigationGestures;
        var contextMenuBindingsPassed = !exerciseNavigationGestures;
        var secondClickPassed = false;
        var undoPassed = false;
        var repickPassed = false;
        var candidatePassed = false;
        var cancelPassed = !cancelWhenReady;
        var authoredOnlyPassed = false;
        var previewResultUnchanged = false;
        var routedEventsPassed = false;
        var failure = string.Empty;
        var originalPointer = default(Point);
        var hasOriginalPointer = false;
        Window? hostWindow = null;
        var originalTopmost = false;
        var initialAppliedCount = viewModel.AppliedTeachingSelections.Count;
        var initialPreview = viewModel.PreviewToolResult;
        var initialResults = viewModel.ResultEntities;
        var initialPreviewStatus = initialPreview.Status;
        var initialResultCount = initialResults.Count;

        pointerInputMouseDownCount = 0;
        pointerInputMouseMoveCount = 0;
        pointerInputMouseUpCount = 0;
        pointerInputMouseWheelCount = 0;

        try
        {
            var capture = TeachingCaptureSnapshot;
            if (!capture.IsActive
                || capture.Kind is not (ToolRecipeSelectionKinds.GridRectangle or ToolRecipeSelectionKinds.PointSet)
                || capture.RequiredPointCount != 2
                || capture.CapturedPointCount != 0)
            {
                throw new InvalidOperationException(
                    "Pointer smoke requires an active empty GridRectangle(2) or PointSet(2) capture.");
            }

            var captureSourceBinding = viewModel.TeachingCaptureSourceBinding;
            var capturesTransformedHeightField = string.Equals(
                captureSourceBinding?.Format,
                "TransformedHeightField",
                StringComparison.Ordinal);
            if (capturesTransformedHeightField)
            {
                if (regridHeightFieldRenderOutput is null
                    || captureSourceBinding is null
                    || !ToolRecipeSelectionSourceBindingVerifier.Verify(
                        regridHeightFieldRenderOutput,
                        captureSourceBinding).IsCurrent)
                {
                    throw new InvalidOperationException(
                        "Pointer smoke requires the exact owned Published TransformedHeightField to be visible.");
                }
            }
            else if (!viewModel.C3DSampleVisible || c3dSample is null)
            {
                throw new InvalidOperationException("Pointer smoke requires a visible loaded C3D source.");
            }

            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException("Viewer is not attached to a visible WPF window.");
            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            hostWindow.Activate();
            hostWindow.Focus();
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(220);

            windowActivated = hostWindow.IsActive;
            var viewportWidth = Viewport.ActualWidth;
            var viewportHeight = Viewport.ActualHeight;
            if (!Viewport.IsVisible || viewportWidth < 200.0 || viewportHeight < 180.0)
            {
                throw new InvalidOperationException(
                    $"Viewport is not ready for teaching pointer input ({viewportWidth:F0}x{viewportHeight:F0}).");
            }

            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);
            pointerInputRegressionActive = true;

            if (!TryFindTeachingCapturePickPoint(
                    new HashSet<(int Row, int Column)>(),
                    out var firstLocalPoint,
                    out var firstPoint))
            {
                throw new InvalidOperationException("No first pickable rendered C3D cell was found.");
            }

            firstLocator = FormatLocator(firstPoint);
            await SendTeachingLeftClickAsync(hostWindow, firstLocalPoint);
            firstClickPassed = TeachingCaptureSnapshot is
            {
                IsActive: true,
                CapturedPointCount: 1,
                CanApply: false
            };
            if (TeachingCaptureSnapshot.Points is [var firstCapturedPoint])
            {
                firstLocator = FormatLocator(firstCapturedPoint);
            }

            if (exerciseNavigationGestures)
            {
                var pointsBeforeGestures = TeachingCaptureSnapshot.CapturedPointCount;
                initialCamera = CaptureCameraSnapshot();
                var orbitStart = new Point(viewportWidth * 0.72, viewportHeight * 0.60);
                var orbitEnd = new Point(viewportWidth * 0.86, viewportHeight * 0.47);
                await SendTeachingDragAsync(hostWindow, orbitStart, orbitEnd, MouseButton.Left);
                orbitCamera = CaptureCameraSnapshot();
                orbitPassed = IsFinite(orbitCamera)
                    && Math.Abs(orbitCamera.Yaw - initialCamera.Yaw) > 1.0
                    && Math.Abs(orbitCamera.Pitch - initialCamera.Pitch) > 1.0
                    && TeachingCaptureSnapshot.CapturedPointCount == pointsBeforeGestures;

                var panStart = new Point(viewportWidth * 0.80, viewportHeight * 0.70);
                var panEnd = new Point(viewportWidth * 0.68, viewportHeight * 0.61);
                await SendTeachingDragAsync(hostWindow, panStart, panEnd, MouseButton.Middle);
                panCamera = CaptureCameraSnapshot();
                panPassed = IsFinite(panCamera)
                    && TargetChanged(orbitCamera, panCamera)
                    && TeachingCaptureSnapshot.CapturedPointCount == pointsBeforeGestures;

                var menuPoint = new Point(viewportWidth * 0.56, viewportHeight * 0.42);
                await SendTeachingRightClickAsync(hostWindow, menuPoint);
                contextMenuPassed = await Dispatcher.InvokeAsync(
                    () => Viewport.ContextMenu?.IsOpen == true,
                    DispatcherPriority.Input);
                var bindings = await Dispatcher.InvokeAsync(
                    InspectViewerContextMenuBindings,
                    DispatcherPriority.Input);
                contextMenuBindingsPassed = bindings.Passed
                    && TeachingCaptureSnapshot.CapturedPointCount == pointsBeforeGestures;
                await Dispatcher.InvokeAsync(() =>
                {
                    if (Viewport.ContextMenu is { } menu)
                    {
                        menu.IsOpen = false;
                    }
                }, DispatcherPriority.Input);
            }

            var excluded = new HashSet<(int Row, int Column)> { (firstPoint.Row, firstPoint.Column) };
            if (!TryFindTeachingCapturePickPoint(excluded, out var secondLocalPoint, out var secondPoint))
            {
                throw new InvalidOperationException("No distinct second pickable rendered C3D cell was found.");
            }

            secondLocator = FormatLocator(secondPoint);
            await SendTeachingLeftClickAsync(hostWindow, secondLocalPoint);
            secondClickPassed = TeachingCaptureSnapshot is
            {
                IsActive: true,
                CapturedPointCount: 2,
                CanApply: true
            };

            undoPassed = UndoC3DTeachingCapture()
                && TeachingCaptureSnapshot is
                {
                    IsActive: true,
                    CapturedPointCount: 1,
                    CanApply: false
                };

            await SendTeachingLeftClickAsync(hostWindow, secondLocalPoint);
            repickPassed = TeachingCaptureSnapshot is
            {
                IsActive: true,
                CapturedPointCount: 2,
                CanApply: true
            };
            var capturedPoints = TeachingCaptureSnapshot.Points;
            ToolRecipeGridRectangle? expectedRectangle = null;
            if (capturedPoints is [var rectangleFirst, var rectangleSecond])
            {
                firstLocator = FormatLocator(rectangleFirst);
                secondLocator = FormatLocator(rectangleSecond);
                expectedRectangle = new ToolRecipeGridRectangle(
                    Math.Min(rectangleFirst.Locator.Row, rectangleSecond.Locator.Row),
                    Math.Min(rectangleFirst.Locator.Column, rectangleSecond.Locator.Column),
                    Math.Abs(rectangleFirst.Locator.Row - rectangleSecond.Locator.Row) + 1,
                    Math.Abs(rectangleFirst.Locator.Column - rectangleSecond.Locator.Column) + 1);
            }

            candidatePassed = TryGetC3DTeachingCandidate(out var candidate, out _)
                && (capture.Kind == ToolRecipeSelectionKinds.GridRectangle
                    ? expectedRectangle is not null
                        && candidate is { Kind: ToolRecipeSelectionKinds.GridRectangle, GridRectangle: not null }
                        && candidate.GridRectangle == expectedRectangle
                        && candidate.Points is null or { Count: 0 }
                    : candidate is { Kind: ToolRecipeSelectionKinds.PointSet, GridRectangle: null, Points.Count: 2 })
                && candidate.Rows is null or { Count: 0 };

            authoredOnlyPassed = viewModel.AppliedTeachingSelections.Count == initialAppliedCount;
            previewResultUnchanged = ReferenceEquals(initialPreview, viewModel.PreviewToolResult)
                && ReferenceEquals(initialResults, viewModel.ResultEntities)
                && viewModel.PreviewToolResult.Status == initialPreviewStatus
                && viewModel.ResultEntities.Count == initialResultCount;

            if (cancelWhenReady)
            {
                CancelC3DTeachingCapture();
                cancelPassed = !TeachingCaptureSnapshot.IsActive
                    && viewModel.AppliedTeachingSelections.Count == initialAppliedCount;
            }

            var requiredButtonEvents = exerciseNavigationGestures ? 6 : 3;
            routedEventsPassed = pointerInputMouseDownCount >= requiredButtonEvents
                && (!exerciseNavigationGestures || pointerInputMouseMoveCount >= 2)
                && pointerInputMouseUpCount >= requiredButtonEvents;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }
        finally
        {
            pointerInputRegressionActive = false;
            if (Viewport.ContextMenu is { IsOpen: true } menu)
            {
                menu.IsOpen = false;
            }

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

        var passed = firstClickPassed
            && orbitPassed
            && panPassed
            && contextMenuPassed
            && contextMenuBindingsPassed
            && secondClickPassed
            && undoPassed
            && repickPassed
            && candidatePassed
            && cancelPassed
            && authoredOnlyPassed
            && previewResultUnchanged
            && routedEventsPassed;
        if (!passed && string.IsNullOrWhiteSpace(failure))
        {
            failure = "One or more teaching-capture pointer assertions failed.";
        }

        return new TeachingCapturePointerSmokeResult(
            passed,
            cancelWhenReady,
            exerciseNavigationGestures,
            windowActivated,
            firstClickPassed,
            orbitPassed,
            panPassed,
            contextMenuPassed,
            contextMenuBindingsPassed,
            secondClickPassed,
            undoPassed,
            repickPassed,
            candidatePassed,
            cancelPassed,
            authoredOnlyPassed,
            previewResultUnchanged,
            routedEventsPassed,
            pointerInputMouseDownCount,
            pointerInputMouseMoveCount,
            pointerInputMouseUpCount,
            initialAppliedCount,
            initialResultCount,
            initialPreviewStatus.ToString(),
            initialCamera,
            orbitCamera,
            panCamera,
            firstLocator,
            secondLocator,
            failure);
    }

    private bool TryFindTeachingCapturePickPoint(
        IReadOnlySet<(int Row, int Column)> excluded,
        out Point localPoint,
        out HeightGridPoint point)
    {
        var capturesTransformedHeightField = string.Equals(
            viewModel.TeachingCaptureSourceBinding?.Format,
            "TransformedHeightField",
            StringComparison.Ordinal);
        var candidates = new Dictionary<(int Row, int Column), (Point Screen, HeightGridPoint Point)>();
        for (var yIndex = 2; yIndex <= 8; yIndex++)
        {
            for (var xIndex = 1; xIndex <= 9; xIndex++)
            {
                var screen = new Point(
                    Viewport.ActualWidth * xIndex / 10.0,
                    Viewport.ActualHeight * yIndex / 10.0);
                HeightGridPoint candidate;
                var picked = capturesTransformedHeightField
                    ? TryPickTransformedHeightFieldForSmoke(screen, out candidate)
                    : TryPickC3DPoint(screen, out candidate);
                if (picked && !excluded.Contains((candidate.Row, candidate.Column)))
                {
                    candidates.TryAdd((candidate.Row, candidate.Column), (screen, candidate));
                }
            }
        }

        if (candidates.Count == 0)
        {
            localPoint = default;
            point = default;
            return false;
        }

        var selected = candidates.Values
            .OrderByDescending(candidate => excluded.Count == 0
                ? 0
                : excluded.Max(locator =>
                    Math.Abs(candidate.Point.Row - locator.Row)
                    + Math.Abs(candidate.Point.Column - locator.Column)))
            .FirstOrDefault();
        localPoint = selected.Screen;
        point = selected.Point;
        return true;
    }

    private bool TryPickTransformedHeightFieldForSmoke(Point screenPoint, out HeightGridPoint point)
    {
        if (!TryPickRegridHeightFieldPoint(screenPoint, out var regridPoint))
        {
            point = default;
            return false;
        }

        point = new HeightGridPoint(
            regridPoint.ReferencePosition,
            regridPoint.Height,
            0,
            (float)regridPoint.Height,
            regridPoint.Row,
            regridPoint.Column);
        return true;
    }

    private async Task SendTeachingLeftClickAsync(Window hostWindow, Point localPoint)
    {
        var screenPoint = Viewport.PointToScreen(localPoint);
        await EnsurePointerInputTargetAsync(hostWindow, screenPoint);
        WindowsPointerInput.LeftDown();
        try
        {
            await Task.Delay(90);
        }
        finally
        {
            WindowsPointerInput.LeftUp();
        }

        await Task.Delay(160);
    }

    private async Task SendTeachingRightClickAsync(Window hostWindow, Point localPoint)
    {
        var screenPoint = Viewport.PointToScreen(localPoint);
        await EnsurePointerInputTargetAsync(hostWindow, screenPoint);
        WindowsPointerInput.RightDown();
        try
        {
            await Task.Delay(90);
        }
        finally
        {
            WindowsPointerInput.RightUp();
        }

        await Task.Delay(180);
    }

    private async Task SendTeachingDragAsync(
        Window hostWindow,
        Point localStart,
        Point localEnd,
        MouseButton button)
    {
        var start = Viewport.PointToScreen(localStart);
        var end = Viewport.PointToScreen(localEnd);
        await EnsurePointerInputTargetAsync(hostWindow, start);
        if (button == MouseButton.Left)
        {
            WindowsPointerInput.LeftDown();
        }
        else
        {
            WindowsPointerInput.MiddleDown();
        }

        try
        {
            await Task.Delay(90);
            WindowsPointerInput.MoveTo(end);
            await Task.Delay(180);
        }
        finally
        {
            if (button == MouseButton.Left)
            {
                WindowsPointerInput.LeftUp();
            }
            else
            {
                WindowsPointerInput.MiddleUp();
            }
        }

        await Task.Delay(160);
    }

    private void ApplyTeachingCapturePointerSmokeArguments(string[] args)
    {
        var reportIndex = Array.IndexOf(args, "--smoke-teaching-capture-pointer-report");
        if (reportIndex < 0)
        {
            return;
        }

        if (reportIndex + 1 >= args.Length
            || args[reportIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            SetSmokeFailure("Teaching-capture pointer smoke requires a report path.");
            return;
        }

        smokeTeachingCapturePointerReportPath = args[reportIndex + 1];
    }

    private static void WriteTeachingCapturePointerSmokeReport(
        string path,
        TeachingCapturePointerSmokeResult result)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var lines = new[]
        {
            "TeachingCapturePointerSmoke",
            $"Result|pass={result.Passed}|windowActivated={result.WindowActivated}|cancelWhenReady={result.CancelWhenReady}|navigationGestures={result.NavigationGesturesExercised}",
            $"Capture|firstClick={result.FirstClickPassed}|secondClick={result.SecondClickPassed}|undo={result.UndoPassed}|repick={result.RepickPassed}|candidate={result.CandidatePassed}|cancel={result.CancelPassed}",
            $"Gestures|orbit={result.OrbitPassed}|pan={result.PanPassed}|contextMenu={result.ContextMenuPassed}|contextMenuBindings={result.ContextMenuBindingsPassed}",
            $"Boundaries|authoredOnly={result.AuthoredOnlyPassed}|previewResultUnchanged={result.PreviewResultUnchanged}|appliedBefore={result.InitialAppliedCount}|previewStatus={result.InitialPreviewStatus}|resultCount={result.InitialResultCount}",
            $"RoutedEvents|pass={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}",
            $"Points|first={result.FirstLocator}|second={result.SecondLocator}",
            $"OrbitCamera|before={FormatCameraSnapshot(result.InitialCamera)}|after={FormatCameraSnapshot(result.OrbitCamera)}",
            $"PanCamera|before={FormatCameraSnapshot(result.OrbitCamera)}|after={FormatCameraSnapshot(result.PanCamera)}",
            $"Failure|summary={result.Failure}"
        };
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
    }

    private static string FormatLocator(HeightGridPoint point) =>
        string.Create(CultureInfo.InvariantCulture, $"row:{point.Row},column:{point.Column},raw:{point.RawValue:R}");

    private static string FormatLocator(ToolRecipeSelectionPoint point) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"row:{point.Locator.Row},column:{point.Locator.Column},raw:{point.RawHeight:R}");

    private sealed record TeachingCapturePointerSmokeResult(
        bool Passed,
        bool CancelWhenReady,
        bool NavigationGesturesExercised,
        bool WindowActivated,
        bool FirstClickPassed,
        bool OrbitPassed,
        bool PanPassed,
        bool ContextMenuPassed,
        bool ContextMenuBindingsPassed,
        bool SecondClickPassed,
        bool UndoPassed,
        bool RepickPassed,
        bool CandidatePassed,
        bool CancelPassed,
        bool AuthoredOnlyPassed,
        bool PreviewResultUnchanged,
        bool RoutedEventsPassed,
        int MouseDownCount,
        int MouseMoveCount,
        int MouseUpCount,
        int InitialAppliedCount,
        int InitialResultCount,
        string InitialPreviewStatus,
        CameraSnapshot InitialCamera,
        CameraSnapshot OrbitCamera,
        CameraSnapshot PanCamera,
        string FirstLocator,
        string SecondLocator,
        string Failure);
}

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

    public async Task<bool> RunTeachingRectangleDragPointerSmokeAsync(string? reportPath)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D teaching GridRectangle drag pointer smoke",
        };
        var failure = string.Empty;
        var passed = false;
        var firstLocator = "(none)";
        var secondLocator = "(none)";
        var initialAppliedCount = viewModel.AppliedTeachingSelections.Count;
        var initialPreview = viewModel.PreviewToolResult;
        var initialResults = viewModel.ResultEntities;
        var initialCamera = CaptureCameraSnapshot();
        var finalCamera = initialCamera;
        var seededMoveStart = default(Point);
        var seededMoveEnd = default(Point);
        var seededMoveFirstPoint = default(HeightGridPoint);
        var seededMoveSecondPoint = default(HeightGridPoint);
        var hasSeededMovePath = false;
        var originalPointer = default(Point);
        var hasOriginalPointer = false;
        Window? hostWindow = null;
        var originalTopmost = false;

        pointerInputMouseDownCount = 0;
        pointerInputMouseMoveCount = 0;
        pointerInputMouseUpCount = 0;

        try
        {
            var capture = TeachingCaptureSnapshot;
            if (capture is not
                {
                    IsActive: true,
                    Kind: ToolRecipeSelectionKinds.GridRectangle,
                    RequiredPointCount: 2
                })
            {
                throw new InvalidOperationException(
                    "GridRectangle drag smoke requires an active 2-corner capture.");
            }
            var editsExistingCandidate = capture.CapturedPointCount == 2;
            var moveHoverPassed = !editsExistingCandidate;
            var resizeHoverPassed = !editsExistingCandidate;
            var moveHoverMode = editsExistingCandidate ? "(not-evaluated)" : "(not-required)";
            var moveHoverCursor = editsExistingCandidate ? "(not-evaluated)" : "(not-required)";
            var moveHoverStatus = editsExistingCandidate ? "(not-evaluated)" : "(not-required)";
            ToolRecipeGridRectangle? initialRectangle = null;
            ToolRecipeGridRectangle? dragBaselineRectangle = null;
            if (editsExistingCandidate)
            {
                if (!TryGetC3DTeachingCandidate(out var initialCandidate, out _)
                    || initialCandidate?.GridRectangle is not { } existingRectangle)
                {
                    throw new InvalidOperationException("The seeded GridRectangle candidate is unavailable.");
                }

                initialRectangle = existingRectangle;
                var rowInset = Math.Max(1, existingRectangle.RowCount / 5);
                var columnInset = Math.Max(1, existingRectangle.ColumnCount / 5);
                dragBaselineRectangle = new ToolRecipeGridRectangle(
                    existingRectangle.Row + rowInset,
                    existingRectangle.Column + columnInset,
                    Math.Max(1, existingRectangle.RowCount - rowInset * 2),
                    Math.Max(1, existingRectangle.ColumnCount - columnInset * 2));
                if (!TrySetC3DTeachingGridRectangleCandidate(dragBaselineRectangle, out var baselineMessage))
                {
                    throw new InvalidOperationException(
                        $"The transient pointer-edit baseline could not be prepared: {baselineMessage}");
                }
            }
            else if (capture.CapturedPointCount != 0)
            {
                throw new InvalidOperationException(
                    "GridRectangle drag smoke requires either an empty or seeded 2-corner capture.");
            }

            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException("Viewer is not attached to a visible WPF window.");
            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            hostWindow.Activate();
            hostWindow.Focus();
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(220);
            initialCamera = CaptureCameraSnapshot();
            finalCamera = initialCamera;

            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);
            pointerInputRegressionActive = true;
            if (editsExistingCandidate
                && TryGetTeachingGridRectangleScreenCorners(
                    dragBaselineRectangle!,
                    out var hoverTopLeft,
                    out var hoverTopRight,
                    out var hoverBottomRight,
                    out var hoverBottomLeft))
            {
                var baselineRectangle = dragBaselineRectangle!;
                var hoverCenter = new Point(
                    (hoverTopLeft.X + hoverTopRight.X + hoverBottomRight.X + hoverBottomLeft.X) * 0.25,
                    (hoverTopLeft.Y + hoverTopRight.Y + hoverBottomRight.Y + hoverBottomLeft.Y) * 0.25);
                if (TryGetTeachingGridRectangleCenterScreenPoint(
                        capture.SelectionId,
                        baselineRectangle,
                        out var projectedCenter))
                {
                    hoverCenter = projectedCenter;
                    var binding = viewModel.TeachingCaptureSourceBinding;
                    if (binding is not null)
                    {
                        var targetRow = baselineRectangle.Row + baselineRectangle.RowCount
                            <= binding.GridHeight - baselineRectangle.RowCount
                                ? baselineRectangle.Row + baselineRectangle.RowCount
                                : Math.Max(0, baselineRectangle.Row - baselineRectangle.RowCount);
                        var targetColumn = baselineRectangle.Column + baselineRectangle.ColumnCount
                            <= binding.GridWidth - baselineRectangle.ColumnCount
                                ? baselineRectangle.Column + baselineRectangle.ColumnCount
                                : Math.Max(0, baselineRectangle.Column - baselineRectangle.ColumnCount);
                        var targetRectangle = baselineRectangle with
                        {
                            Row = targetRow,
                            Column = targetColumn
                        };
                        hasSeededMovePath = TryGetTeachingGridRectangleCenterScreenPoint(
                                capture.SelectionId,
                                targetRectangle,
                                out seededMoveEnd)
                            && TryPickC3DPoint(hoverCenter, out seededMoveFirstPoint)
                            && TryPickC3DPoint(seededMoveEnd, out seededMoveSecondPoint)
                            && (seededMoveFirstPoint.Row != seededMoveSecondPoint.Row
                                || seededMoveFirstPoint.Column != seededMoveSecondPoint.Column);
                        if (hasSeededMovePath)
                        {
                            seededMoveStart = hoverCenter;
                        }
                    }
                }
                await EnsurePointerInputTargetAsync(hostWindow, Viewport.PointToScreen(hoverCenter));
                await Task.Delay(120);
                moveHoverMode = GetTeachingGridRectangleEditMode(
                        hoverCenter,
                        capture.SelectionId,
                        baselineRectangle)
                    .ToString();
                moveHoverCursor = Viewport.Cursor?.ToString() ?? "(null)";
                moveHoverStatus = viewModel.ViewerStatus;
                moveHoverPassed = Viewport.Cursor == Cursors.SizeAll
                    && viewModel.ViewerStatus.Contains("move area", StringComparison.Ordinal);
            }

            Point firstLocalPoint;
            HeightGridPoint firstPoint;
            var foundFirst = true;
            if (editsExistingCandidate && hasSeededMovePath)
            {
                firstLocalPoint = seededMoveStart;
                firstPoint = seededMoveFirstPoint;
            }
            else
            {
                foundFirst = editsExistingCandidate
                    ? TryFindTeachingCapturePickPointNear(
                        dragBaselineRectangle!.Row + dragBaselineRectangle.RowCount / 2,
                        dragBaselineRectangle.Column + dragBaselineRectangle.ColumnCount / 2,
                        out firstLocalPoint,
                        out firstPoint)
                    : TryFindTeachingCapturePickPoint(
                        new HashSet<(int Row, int Column)>(),
                        out firstLocalPoint,
                        out firstPoint);
            }
            if (!foundFirst)
            {
                throw new InvalidOperationException("No first pickable rendered grid cell was found.");
            }

            Point secondLocalPoint;
            HeightGridPoint secondPoint;
            var foundSecond = true;
            if (editsExistingCandidate && hasSeededMovePath)
            {
                secondLocalPoint = seededMoveEnd;
                secondPoint = seededMoveSecondPoint;
            }
            else
            {
                foundSecond = editsExistingCandidate
                    ? TryFindTeachingCapturePickPoint(
                        new HashSet<(int Row, int Column)> { (firstPoint.Row, firstPoint.Column) },
                        out secondLocalPoint,
                        out secondPoint)
                    : TryFindTeachingCapturePickPoint(
                        new HashSet<(int Row, int Column)> { (firstPoint.Row, firstPoint.Column) },
                        out secondLocalPoint,
                        out secondPoint);
            }
            if (!foundSecond
                || (firstPoint.Row == secondPoint.Row && firstPoint.Column == secondPoint.Column))
            {
                throw new InvalidOperationException("No distinct second pickable rendered grid cell was found.");
            }

            firstLocator = FormatLocator(firstPoint);
            secondLocator = FormatLocator(secondPoint);
            await SendTeachingDragAsync(
                hostWindow,
                firstLocalPoint,
                secondLocalPoint,
                MouseButton.Left);

            var state = TeachingCaptureSnapshot;
            if (state.Points is [var capturedFirst, var capturedSecond])
            {
                firstLocator = FormatLocator(capturedFirst);
                secondLocator = FormatLocator(capturedSecond);
            }

            ToolRecipeSelection? candidate = null;
            var movePassed = state is
                {
                    IsActive: true,
                    CapturedPointCount: 2,
                    CanApply: true
                }
                && TryGetC3DTeachingCandidate(out candidate, out _)
                && candidate is
                {
                    Kind: ToolRecipeSelectionKinds.GridRectangle,
                    GridRectangle: not null
                }
                && (!editsExistingCandidate || candidate.GridRectangle != dragBaselineRectangle);
            var candidateAfterMove = candidate?.GridRectangle;
            var resizePassed = !editsExistingCandidate;
            var heightHoverPassed = !editsExistingCandidate;
            var heightDragPassed = !editsExistingCandidate;
            if (editsExistingCandidate
                && candidateAfterMove is not null
                && TryGetTeachingGridRectangleScreenCorners(
                    candidateAfterMove,
                    out var resizeTopLeft,
                    out _,
                    out var resizeBottomRight,
                    out _))
            {
                var resizeTarget = resizeTopLeft + (resizeBottomRight - resizeTopLeft) * 0.12;
                await EnsurePointerInputTargetAsync(hostWindow, Viewport.PointToScreen(resizeTopLeft));
                await Task.Delay(120);
                resizeHoverPassed = Viewport.Cursor is not null
                    && (Viewport.Cursor == Cursors.SizeNWSE || Viewport.Cursor == Cursors.SizeNESW)
                    && viewModel.ViewerStatus.Contains("corner handle", StringComparison.Ordinal);
                await SendTeachingDragAsync(
                    hostWindow,
                    resizeTopLeft,
                    resizeTarget,
                    MouseButton.Left);
                resizePassed = TeachingCaptureSnapshot is
                    {
                        IsActive: true,
                        CapturedPointCount: 2,
                        CanApply: true
                    }
                    && TryGetC3DTeachingCandidate(out var resizedCandidate, out _)
                    && resizedCandidate?.GridRectangle is { } resizedRectangle
                    && resizedRectangle != candidateAfterMove
                    && (resizedRectangle.RowCount != candidateAfterMove.RowCount
                        || resizedRectangle.ColumnCount != candidateAfterMove.ColumnCount);
                state = TeachingCaptureSnapshot;
            }
            if (editsExistingCandidate
                && TryGetC3DTeachingCandidate(out var candidateBeforeHeight, out _)
                && candidateBeforeHeight?.GridRectangle is { } rectangleBeforeHeight
                && TryGetTeachingGridRectangleScreenCorners(
                    rectangleBeforeHeight,
                    out var heightTopLeft,
                    out var heightTopRight,
                    out var heightBottomRight,
                    out var heightBottomLeft)
                && TryGetTeachingHeightHandleScreenPoint(rectangleBeforeHeight, out var heightHandle))
            {
                var heightCenter = new Point(
                    (heightTopLeft.X + heightTopRight.X + heightBottomRight.X + heightBottomLeft.X) * 0.25,
                    (heightTopLeft.Y + heightTopRight.Y + heightBottomRight.Y + heightBottomLeft.Y) * 0.25);
                var heightAxis = heightHandle - heightCenter;
                var heightTarget = heightHandle + heightAxis * 0.35;
                var offsetBeforeHeight = viewModel.SelectedTeachingRoiDisplayHeightOffset;
                await EnsurePointerInputTargetAsync(hostWindow, Viewport.PointToScreen(heightHandle));
                await Task.Delay(120);
                heightHoverPassed = Viewport.Cursor == Cursors.SizeNS
                    && viewModel.ViewerStatus.Contains("height handle", StringComparison.Ordinal);
                await SendTeachingDragAsync(
                    hostWindow,
                    heightHandle,
                    heightTarget,
                    MouseButton.Left);
                heightDragPassed =
                    Math.Abs(viewModel.SelectedTeachingRoiDisplayHeightOffset - offsetBeforeHeight) > 0.001
                    && TryGetC3DTeachingCandidate(out var candidateAfterHeight, out _)
                    && candidateAfterHeight?.GridRectangle == rectangleBeforeHeight;
                state = TeachingCaptureSnapshot;
            }
            var candidatePassed = movePassed
                && resizePassed
                && moveHoverPassed
                && resizeHoverPassed
                && heightHoverPassed
                && heightDragPassed;
            var authoredUnchanged =
                viewModel.AppliedTeachingSelections.Count == initialAppliedCount;
            var executionUnchanged =
                ReferenceEquals(initialPreview, viewModel.PreviewToolResult)
                && ReferenceEquals(initialResults, viewModel.ResultEntities);
            finalCamera = CaptureCameraSnapshot();
            var cameraUnchanged = finalCamera == initialCamera;
            var routedEventsPassed =
                pointerInputMouseDownCount >= 1
                && pointerInputMouseMoveCount >= 1
                && pointerInputMouseUpCount >= 1;

            passed = candidatePassed
                && authoredUnchanged
                && executionUnchanged
                && cameraUnchanged
                && routedEventsPassed;
            lines.Add(
                $"Capture|active={state.IsActive}|progress={state.CapturedPointCount}/{state.RequiredPointCount}|canApply={state.CanApply}|candidate={candidatePassed}");
            lines.Add($"Edit|seeded={editsExistingCandidate}|authored={initialRectangle}|pointerBaseline={dragBaselineRectangle}|move={movePassed}|resize={resizePassed}");
            lines.Add($"Hover|move={moveHoverPassed}|resize={resizeHoverPassed}|height={heightHoverPassed}|cursorModeFeedback=True");
            lines.Add($"MoveHoverDetail|mode={moveHoverMode}|cursor={moveHoverCursor}|status={moveHoverStatus}");
            lines.Add($"DisplayHeight|drag={heightDragPassed}|offset={viewModel.SelectedTeachingRoiDisplayHeightOffset:F3}|automatic={viewModel.SelectedTeachingRoiAutomaticRawHeight:F3}|effective={viewModel.SelectedTeachingRoiEffectiveRawHeight:F3}");
            lines.Add("InteractionContract|footprintPlane=XZ@local-surface-median+display-offset|surfacePointRequired=False|handleRadiusPixels=18|insideDrag=move|verticalHandle=view-only");
            lines.Add(
                $"Boundary|authoredUnchanged={authoredUnchanged}|executionUnchanged={executionUnchanged}|cameraUnchanged={cameraUnchanged}");
            lines.Add(
                $"RoutedEvents|pass={routedEventsPassed}|mouseDown={pointerInputMouseDownCount}|mouseMove={pointerInputMouseMoveCount}|mouseUp={pointerInputMouseUpCount}");
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

        if (!passed && string.IsNullOrWhiteSpace(failure))
        {
            failure = "The drag did not produce a ready GridRectangle candidate without changing camera or execution state.";
        }

        lines.Add($"Points|first={firstLocator}|second={secondLocator}");
        lines.Add($"Camera|before={FormatCameraSnapshot(initialCamera)}|after={FormatCameraSnapshot(finalCamera)}");
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{failure}");
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        }

        if (!passed)
        {
            SetSmokeFailure($"Teaching GridRectangle drag smoke failed: {failure}");
        }

        RenderNow();
        return passed;
    }

    public async Task<(bool Passed, ToolRecipeGridRectangle? Candidate, string Failure)>
        RunTeachingTargetRectanglePointerSmokeAsync(
            ToolRecipeGridRectangle target,
            string? reportPath)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D target GridRectangle pointer smoke"
        };
        var failure = string.Empty;
        ToolRecipeGridRectangle? candidateRectangle = null;
        var initialAppliedCount = viewModel.AppliedTeachingSelections.Count;
        var initialPreview = viewModel.PreviewToolResult;
        var initialResults = viewModel.ResultEntities;
        var initialCamera = CaptureCameraSnapshot();
        var originalPointer = default(Point);
        var hasOriginalPointer = false;
        Window? hostWindow = null;
        var originalTopmost = false;
        pointerInputMouseDownCount = 0;
        pointerInputMouseMoveCount = 0;
        pointerInputMouseUpCount = 0;

        try
        {
            var capture = TeachingCaptureSnapshot;
            if (capture is not
                {
                    IsActive: true,
                    Kind: ToolRecipeSelectionKinds.GridRectangle,
                    RequiredPointCount: 2,
                    CapturedPointCount: 0
                })
            {
                throw new InvalidOperationException(
                    "Target GridRectangle smoke requires an empty active 2-corner capture.");
            }
            if (!viewModel.IsTopOrthographicView)
            {
                throw new InvalidOperationException(
                    "Target GridRectangle smoke requires the automatic Top orthographic teaching view.");
            }

            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException("Viewer is not attached to a visible WPF window.");
            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            hostWindow.Activate();
            hostWindow.Focus();
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            await Task.Delay(220);
            initialCamera = CaptureCameraSnapshot();

            if (!TryGetTeachingGridRectangleScreenCorners(
                    capture.SelectionId,
                    target,
                    out var topLeft,
                    out _,
                    out var bottomRight,
                    out _))
            {
                throw new InvalidOperationException("The target ROI could not be projected into the current Viewer.");
            }

            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);
            pointerInputRegressionActive = true;
            await SendTeachingDragAsync(hostWindow, topLeft, bottomRight, MouseButton.Left);
            if (TeachingCaptureSnapshot is not
                {
                    IsActive: true,
                    CapturedPointCount: 2,
                    CanApply: true
                }
                || !TryGetC3DTeachingCandidate(out var candidate, out _)
                || candidate?.GridRectangle is not { } rectangle)
            {
                throw new InvalidOperationException("One target drag did not produce an applicable GridRectangle candidate.");
            }

            candidateRectangle = rectangle;
            var firstRow = Math.Max(target.Row, rectangle.Row);
            var lastRow = Math.Min(
                target.Row + target.RowCount,
                rectangle.Row + rectangle.RowCount);
            var firstColumn = Math.Max(target.Column, rectangle.Column);
            var lastColumn = Math.Min(
                target.Column + target.ColumnCount,
                rectangle.Column + rectangle.ColumnCount);
            var intersection = (long)Math.Max(0, lastRow - firstRow)
                * Math.Max(0, lastColumn - firstColumn);
            var targetArea = (long)target.RowCount * target.ColumnCount;
            var targetCoverage = targetArea == 0 ? 0.0 : intersection / (double)targetArea;
            var authoredUnchanged = viewModel.AppliedTeachingSelections.Count == initialAppliedCount;
            var executionUnchanged = ReferenceEquals(initialPreview, viewModel.PreviewToolResult)
                && ReferenceEquals(initialResults, viewModel.ResultEntities);
            var cameraUnchanged = CaptureCameraSnapshot() == initialCamera;
            var routedEventsPassed = pointerInputMouseDownCount == 1
                && pointerInputMouseMoveCount >= 1
                && pointerInputMouseUpCount == 1;
            var passed = targetCoverage >= 0.80
                && authoredUnchanged
                && executionUnchanged
                && cameraUnchanged
                && routedEventsPassed;
            if (!passed)
            {
                failure = "The one-drag candidate did not preserve target coverage, camera, authored state, execution state, or routed input.";
            }

            lines.Add($"Target|{target}");
            lines.Add($"Candidate|{rectangle}|targetCoverage={targetCoverage:F4}");
            lines.Add($"View|topOrthographic={viewModel.IsTopOrthographicView}|cameraUnchanged={cameraUnchanged}");
            lines.Add($"Boundary|authoredUnchanged={authoredUnchanged}|executionUnchanged={executionUnchanged}");
            lines.Add($"RoutedEvents|pass={routedEventsPassed}|mouseDown={pointerInputMouseDownCount}|mouseMove={pointerInputMouseMoveCount}|mouseUp={pointerInputMouseUpCount}");
            lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{failure}");
            WriteTargetRectanglePointerReport(reportPath, lines);
            if (!passed)
            {
                SetSmokeFailure(failure);
            }
            RenderNow();
            return (passed, candidateRectangle, failure);
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            lines.Add($"Target|{target}");
            lines.Add($"Candidate|{candidateRectangle?.ToString() ?? "(none)"}");
            lines.Add($"Result=FAIL|{failure}");
            WriteTargetRectanglePointerReport(reportPath, lines);
            SetSmokeFailure(failure);
            RenderNow();
            return (false, candidateRectangle, failure);
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
    }

    private static void WriteTargetRectanglePointerReport(string? reportPath, IReadOnlyList<string> lines)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
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
        var zoomPassed = !exerciseNavigationGestures;
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
                if (capture.Kind == ToolRecipeSelectionKinds.GridRectangle)
                {
                    // Left-drag is intentionally reserved for ROI rectangle
                    // teaching while this capture kind is active.
                    orbitCamera = initialCamera;
                    orbitPassed = true;
                }
                else
                {
                    var orbitStart = new Point(viewportWidth * 0.72, viewportHeight * 0.60);
                    var orbitEnd = new Point(viewportWidth * 0.86, viewportHeight * 0.47);
                    await SendTeachingDragAsync(hostWindow, orbitStart, orbitEnd, MouseButton.Left);
                    orbitCamera = CaptureCameraSnapshot();
                    orbitPassed = IsFinite(orbitCamera)
                        && Math.Abs(orbitCamera.Yaw - initialCamera.Yaw) > 1.0
                        && Math.Abs(orbitCamera.Pitch - initialCamera.Pitch) > 1.0
                        && TeachingCaptureSnapshot.CapturedPointCount == pointsBeforeGestures;
                }

                var panStart = new Point(viewportWidth * 0.80, viewportHeight * 0.70);
                var panEnd = new Point(viewportWidth * 0.68, viewportHeight * 0.61);
                await SendTeachingDragAsync(hostWindow, panStart, panEnd, MouseButton.Middle);
                panCamera = CaptureCameraSnapshot();
                panPassed = IsFinite(panCamera)
                    && TargetChanged(orbitCamera, panCamera)
                    && TeachingCaptureSnapshot.CapturedPointCount == pointsBeforeGestures;

                var zoomPoint = new Point(viewportWidth * 0.64, viewportHeight * 0.54);
                var orthographicHeightBeforeZoom = viewModel.OrthographicHeight;
                var distanceBeforeZoom = viewModel.CameraDistance;
                await EnsurePointerInputTargetAsync(hostWindow, Viewport.PointToScreen(zoomPoint));
                WindowsPointerInput.Wheel(120);
                await Task.Delay(180);
                zoomPassed = (viewModel.IsTopOrthographicView
                        ? viewModel.OrthographicHeight < orthographicHeightBeforeZoom
                        : viewModel.CameraDistance < distanceBeforeZoom)
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

            var requiredButtonEvents = exerciseNavigationGestures
                ? capture.Kind == ToolRecipeSelectionKinds.GridRectangle ? 5 : 6
                : 3;
            routedEventsPassed = pointerInputMouseDownCount >= requiredButtonEvents
                && (!exerciseNavigationGestures || pointerInputMouseMoveCount >= 2)
                && pointerInputMouseUpCount >= requiredButtonEvents
                && (!exerciseNavigationGestures || pointerInputMouseWheelCount >= 1);
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
            && zoomPassed
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
            zoomPassed,
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
            pointerInputMouseWheelCount,
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
        for (var yIndex = 2; yIndex <= 18; yIndex++)
        {
            for (var xIndex = 1; xIndex <= 19; xIndex++)
            {
                var screen = new Point(
                    Viewport.ActualWidth * xIndex / 20.0,
                    Viewport.ActualHeight * yIndex / 20.0);
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
                    (candidate.Point.Row != locator.Row
                     && candidate.Point.Column != locator.Column
                        ? 1_000_000
                        : 0)
                    + Math.Abs(candidate.Point.Row - locator.Row)
                    + Math.Abs(candidate.Point.Column - locator.Column)))
            .FirstOrDefault();
        localPoint = selected.Screen;
        point = selected.Point;
        return true;
    }

    private bool TryFindTeachingCapturePickPointNear(
        int targetRow,
        int targetColumn,
        out Point localPoint,
        out HeightGridPoint point)
    {
        var candidates = new Dictionary<(int Row, int Column), (Point Screen, HeightGridPoint Point)>();
        for (var yIndex = 1; yIndex <= 19; yIndex++)
        {
            for (var xIndex = 1; xIndex <= 19; xIndex++)
            {
                var screen = new Point(
                    Viewport.ActualWidth * xIndex / 20.0,
                    Viewport.ActualHeight * yIndex / 20.0);
                if (TryPickC3DPoint(screen, out var candidate))
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
            .OrderBy(candidate =>
                Math.Abs(candidate.Point.Row - targetRow)
                + Math.Abs(candidate.Point.Column - targetColumn))
            .First();
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
            const int movementSteps = 6;
            for (var step = 1; step <= movementSteps; step++)
            {
                var progress = step / (double)movementSteps;
                WindowsPointerInput.MoveTo(new Point(
                    start.X + (end.X - start.X) * progress,
                    start.Y + (end.Y - start.Y) * progress));
                await Task.Delay(45);
            }

            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);
            if (!WindowsPointerInput.TryGetPosition(out var actualEnd)
                || Math.Abs(actualEnd.X - end.X) > 2
                || Math.Abs(actualEnd.Y - end.Y) > 2)
            {
                WindowsPointerInput.MoveTo(end);
                await Task.Delay(120);
            }
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
            $"Gestures|orbit={result.OrbitPassed}|pan={result.PanPassed}|zoom={result.ZoomPassed}|contextMenu={result.ContextMenuPassed}|contextMenuBindings={result.ContextMenuBindingsPassed}",
            $"Boundaries|authoredOnly={result.AuthoredOnlyPassed}|previewResultUnchanged={result.PreviewResultUnchanged}|appliedBefore={result.InitialAppliedCount}|previewStatus={result.InitialPreviewStatus}|resultCount={result.InitialResultCount}",
            $"RoutedEvents|pass={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}|mouseWheel={result.MouseWheelCount}",
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
        bool ZoomPassed,
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
        int MouseWheelCount,
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

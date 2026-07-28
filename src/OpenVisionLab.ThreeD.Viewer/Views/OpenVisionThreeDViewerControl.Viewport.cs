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
    private Point? rightPanPointerDownPosition;
    private bool rightPanDragExceeded;
    private bool suppressNextViewerContextMenu;

    private void Viewport_OpenGLInitialized(object sender, OpenGLRoutedEventArgs args)
    {
        c3dGpuBuffers = null;
        c3dGpuBufferKey = null;
        c3dGpuFailedKey = null;
        c3dGpuReleasePending = false;
        c3dGpuBuffersAvailable = false;
        c3dDisplayListId = 0;
        c3dDisplayListKey = null;
        c3dInteractionDisplayListId = 0;
        c3dInteractionDisplayListKey = null;
        pendingC3DDisplayListBuildReason = "opengl-initialized";
        var gl = args.OpenGL;
        openGLVendor = ReadOpenGLString(gl, 0x1F00);
        openGLRenderer = ReadOpenGLString(gl, 0x1F01);
        openGLVersion = ReadOpenGLString(gl, 0x1F02);
        gl.ClearColor(0.08f, 0.10f, 0.13f, 1.0f);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
        gl.DepthFunc(OpenGL.GL_LEQUAL);
        gl.ShadeModel(OpenGL.GL_SMOOTH);
    }

    private void Viewport_Resized(object sender, OpenGLRoutedEventArgs args)
    {
        ConfigureProjection(args.OpenGL);
        BeginInteractionWireframeLod();
        ScheduleInteractionWireframeLodRestore();
    }

    private void Viewport_OpenGLDraw(object sender, OpenGLRoutedEventArgs args)
    {
        var drawStart = Stopwatch.GetTimestamp();
        if (pointerInputRegressionActive && pointerInputLastMouseMoveTimestamp != 0)
        {
            var nextFrameMilliseconds = Stopwatch.GetElapsedTime(pointerInputLastMouseMoveTimestamp, drawStart).TotalMilliseconds;
            pointerInputNextFrameTimingCount++;
            pointerInputNextFrameTotalMilliseconds += nextFrameMilliseconds;
            pointerInputNextFrameMaximumMilliseconds = Math.Max(pointerInputNextFrameMaximumMilliseconds, nextFrameMilliseconds);
            pointerInputLastMouseMoveTimestamp = 0;
        }
        UpdateFrameInterval(drawStart);

        var gl = args.OpenGL;
        if (openGLVersion == "(unavailable)")
        {
            openGLVendor = ReadOpenGLString(gl, 0x1F00);
            openGLRenderer = ReadOpenGLString(gl, 0x1F01);
            openGLVersion = ReadOpenGLString(gl, 0x1F02);
        }

        if (c3dGpuReleasePending)
        {
            ReleaseC3DGpuBuffers(gl);
        }

        gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

        ConfigureProjection(gl);
        ConfigureCamera(gl);
        DrawGrid(gl);
        DrawAxes(gl);

        if (viewModel.CubeVisible)
        {
            DrawCube(gl);
        }

        if (viewModel.PointCloudVisible)
        {
            DrawPointCloud(gl, generatedPointCloud);
        }

        if (viewModel.C3DSampleVisible && c3dSample is not null)
        {
            DrawC3DHeightGrid(gl);
        }

        if (viewModel.GlbSampleVisible
            && importedMesh is not null
            && (viewModel.NominalActualInput is null || viewModel.NominalActual.NominalVisible))
        {
            DrawImportedMesh(gl);
        }

        if (viewModel.NominalActual.PreviewResult is not null
            && viewModel.NominalActual.ActualVisible)
        {
            DrawNominalActualDeviation(gl);
            DrawNominalActualSelectedDeviation(gl);
        }

        if (viewModel.LazSampleVisible && lazSample is not null)
        {
            if (lazPointCloud is null)
            {
                DrawLazMetadata(gl);
            }
            else
            {
                DrawLazPointCloud(gl);
            }
        }

        DrawTeachingSelectionOverlays(gl);
        DrawLinkedHeightCursor(gl);

        if (viewModel.MeasurementVisible)
        {
            InspectionOverlayRenderer.DrawMeasurement(gl, viewModel.CubeVisible, viewModel.PointCloudVisible);
        }

        if (viewModel.SelectionOverlayVisible)
        {
            InspectionOverlayRenderer.DrawSelectionOverlay(gl, viewModel.SelectedSelectionMode);
        }

        DrawTwoPointMeasurement(gl);
        DrawProfileLine(gl);
        DrawPlaneReferenceMeasurement(gl);
        DrawPlaneFlatnessExtrema(gl);
        DrawRoiStepMeasurement(gl);
        DrawThicknessRoi(gl);
        DrawWarpageRoi(gl);

        if (viewModel.ResultOverlayVisible || viewModel.ResultEntities.Count > 0)
        {
            InspectionOverlayRenderer.DrawResultOverlay(gl, viewModel.C3DSampleVisible);
        }

        gl.Flush();
        UpdateDrawPerformance(drawStart);
    }

    private static string ReadOpenGLString(OpenGL gl, uint name)
    {
        var value = gl.GetString(name);
        return string.IsNullOrWhiteSpace(value) ? "(unavailable)" : value;
    }

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseDownCount++;
        }

        lastMousePosition = e.GetPosition(Viewport);
        if (e.ChangedButton == MouseButton.Left
            && e.ClickCount >= 2
            && !viewModel.IsTeachingCaptureActive
            && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            isOrbiting = false;
            isPanning = false;
            Viewport.ReleaseMouseCapture();
            var fitted = viewModel.IsTopOrthographicView
                ? TryFitCurrentC3DOrthographic("Double-click fit Top C3D height grid")
                : TryFitCurrentC3D(
                    useTopInspectionView: false,
                    "Double-click fit C3D height grid");
            if (!fitted)
            {
                viewModel.FitAll();
            }

            RenderNow();
            e.Handled = true;
            return;
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            rightPanPointerDownPosition = lastMousePosition;
            rightPanDragExceeded = false;
            suppressNextViewerContextMenu = false;
            isPanning = true;
            isOrbiting = false;
            return;
        }

        var panRequested = e.ChangedButton == MouseButton.Middle || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);

        if (viewModel.IsTeachingCaptureActive
            && e.ChangedButton == MouseButton.Left
            && !panRequested)
        {
            if (TryBeginTeachingGridRectangleEdit(lastMousePosition))
            {
                isPanning = false;
                isOrbiting = false;
                Viewport.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (!IsTeachingGridRectangleCandidateReview)
            {
                teachingCapturePointerDownPosition = lastMousePosition;
                teachingCaptureDragExceeded = false;
                isPanning = false;
                isOrbiting = !IsTeachingGridRectangleCapture;
                Viewport.CaptureMouse();
                return;
            }
        }

        if (e.ChangedButton == MouseButton.Left && !panRequested)
        {
            if (TryBeginTeachingOrientedBoxEdit(lastMousePosition))
            {
                isPanning = false;
                isOrbiting = false;
                Viewport.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (TryBeginTeachingGridRectangleEdit(lastMousePosition))
            {
                isPanning = false;
                isOrbiting = false;
                Viewport.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (TrySelectAppliedTeachingGridRectangle(lastMousePosition))
            {
                isPanning = false;
                isOrbiting = false;
                RenderNow();
                e.Handled = true;
                return;
            }

            if (TrySelectAppliedTeachingOrientedBox(lastMousePosition))
            {
                isPanning = false;
                isOrbiting = false;
                RenderNow();
                e.Handled = true;
                return;
            }

            if (TryBeginProfileEndpointDrag(lastMousePosition))
            {
                profilePointerDownPosition = null;
                profilePointerDragExceeded = false;
                isPanning = false;
                isOrbiting = false;
                Viewport.CaptureMouse();
                return;
            }

            if (viewModel.SelectedSelectionMode == MainWindowViewModel.ProfileSelectionMode)
            {
                profilePointerDownPosition = lastMousePosition;
                profilePointerDragExceeded = false;
                isPanning = false;
                isOrbiting = true;
                Viewport.CaptureMouse();
                return;
            }

            if (TryHandleThicknessRoiPick(lastMousePosition))
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                RenderNow();
                return;
            }

            if (TryHandleWarpageRoiPick(lastMousePosition))
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                RenderNow();
                return;
            }

            if (TryHandleTwoPointPick(lastMousePosition))
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                RenderNow();
                return;
            }

            if (TryHandleRoiStepPick(lastMousePosition))
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                RenderNow();
                return;
            }

            if (TryPickNominalActualDeviation(lastMousePosition, out var deviationSample))
            {
                SetNominalActualDeviationPick(deviationSample, "Picked nominal/actual deviation point");
            }
            else
            {
                viewModel.NominalActual.ClearSelectedDeviation();
                if (TryPickCube(lastMousePosition, out var hit))
                {
                    var summary = CameraMath.FormatPoint(hit);
                    viewModel.SelectedEntity = "Generated Unit Cube";
                    viewModel.PickCoordinate = summary;
                    viewModel.SelectionSummary = $"Cube pick: {summary}";
                    viewModel.ViewerStatus = "Picked generated cube face";
                }
                else if (TryPickC3DPoint(lastMousePosition, out var c3dPoint))
                {
                    if (viewModel.TrySelectWorkbenchLineFitPoint(c3dPoint.Row, c3dPoint.Column))
                    {
                        if (viewModel.SelectedWorkbenchLineFitPoint is { } selectedLineFitPoint)
                        {
                            RaiseWorkbenchLineFitPointSelected(selectedLineFitPoint);
                        }
                    }
                    else if (!viewModel.TrySelectWorkbenchHeightDifferenceEdgePoint(c3dPoint.Row, c3dPoint.Column))
                    {
                        viewModel.SelectedEntity = "C3D Height Grid";
                        viewModel.PickCoordinate = FormatC3DPoint(c3dPoint);
                        viewModel.ViewerStatus = "Picked C3D height-grid point";
                    }
                }
                else if (TryPickImportedMesh(lastMousePosition, out var importedMeshPoint, out var importedMeshPickKind, out var importedMeshTriangleIndex, out var importedMeshSurfaceNormal))
                {
                    SetImportedMeshPick(importedMeshPoint, $"Picked {viewModel.ImportedMeshFormat} {importedMeshPickKind}", importedMeshPickKind, importedMeshTriangleIndex, importedMeshSurfaceNormal);
                }
                else if (TryPickLazPoint(lastMousePosition, out var lazPoint))
                {
                    SetLazPick(lazPoint, "Picked LAZ/LAS sampled point");
                }
                else
                {
                    viewModel.SelectedEntity = "(none)";
                    viewModel.PickCoordinate = "(none)";
                    viewModel.ViewerStatus = "No pick target under cursor";
                }
            }
        }

        if (panRequested)
        {
            isPanning = true;
            Viewport.CaptureMouse();
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            isOrbiting = true;
            Viewport.CaptureMouse();
        }
    }

    private void UpdateFrameInterval(long timestamp)
    {
        if (lastFrameTimestamp != 0)
        {
            accumulatedFrameIntervalMilliseconds += Stopwatch.GetElapsedTime(lastFrameTimestamp, timestamp).TotalMilliseconds;
            performanceFrameCount++;
        }

        lastFrameTimestamp = timestamp;
    }

    private void UpdateDrawPerformance(long drawStart)
    {
        accumulatedDrawMilliseconds += Stopwatch.GetElapsedTime(drawStart).TotalMilliseconds;
        performanceDrawCount++;

        if (performanceFrameCount < 15 || accumulatedFrameIntervalMilliseconds <= 0.0)
        {
            return;
        }

        var averageFrameInterval = accumulatedFrameIntervalMilliseconds / performanceFrameCount;
        var averageDraw = accumulatedDrawMilliseconds / Math.Max(1, performanceDrawCount);
        viewModel.SetRenderPerformance(1000.0 / averageFrameInterval, averageDraw);

        performanceFrameCount = 0;
        performanceDrawCount = 0;
        accumulatedFrameIntervalMilliseconds = 0.0;
        accumulatedDrawMilliseconds = 0.0;
    }

    private void Viewport_MouseMove(object sender, MouseEventArgs e)
    {
        var mouseMoveStart = Stopwatch.GetTimestamp();
        var measurePointerMove = pointerInputRegressionActive;
        isHandlingPointerMouseMove = measurePointerMove;
        if (pointerInputRegressionActive)
        {
            pointerInputMouseMoveCount++;
            pointerInputLastMouseMoveTimestamp = mouseMoveStart;
        }

        try
        {
            HandleViewportMouseMove(e);
        }
        finally
        {
            isHandlingPointerMouseMove = false;
            if (measurePointerMove)
            {
                var elapsedMilliseconds = Stopwatch.GetElapsedTime(mouseMoveStart).TotalMilliseconds;
                pointerInputMouseMoveTimingCount++;
                pointerInputMouseMoveTotalMilliseconds += elapsedMilliseconds;
                pointerInputMouseMoveMaximumMilliseconds = Math.Max(pointerInputMouseMoveMaximumMilliseconds, elapsedMilliseconds);
            }
        }
    }

    private void HandleViewportMouseMove(MouseEventArgs e)
    {
        if (teachingOrientedBoxEditMode != TeachingOrientedBox3DEditMode.None)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            if (TryUpdateTeachingOrientedBoxEdit(e.GetPosition(Viewport)))
            {
                RequestInteractiveRender();
            }

            return;
        }

        if (teachingGridRectangleEditMode != TeachingGridRectangleEditMode.None)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                // The captured MouseUp owns the final grid pick. Keeping the edit
                // mode here avoids dropping a drag when WPF briefly reports the
                // injected/native button state between pointer messages.
                return;
            }

            if (TryUpdateTeachingGridRectangleEdit(e.GetPosition(Viewport)))
            {
                RequestInteractiveRender();
            }

            return;
        }

        if (IsTeachingGridRectangleCapture
            && teachingCapturePointerDownPosition is { } rectangleStart)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                teachingCapturePointerDownPosition = null;
                teachingCaptureDragExceeded = false;
                HideTeachingCaptureDragOverlay();
                Viewport.ReleaseMouseCapture();
                return;
            }

            var rectangleCurrent = e.GetPosition(Viewport);
            if (!teachingCaptureDragExceeded)
            {
                var rectangleDelta = rectangleCurrent - rectangleStart;
                if (Math.Abs(rectangleDelta.X) < SystemParameters.MinimumHorizontalDragDistance
                    && Math.Abs(rectangleDelta.Y) < SystemParameters.MinimumVerticalDragDistance)
                {
                    return;
                }

                teachingCaptureDragExceeded = true;
            }

            UpdateTeachingCaptureDragOverlay(rectangleStart, rectangleCurrent);
            return;
        }

        if ((IsTeachingGridRectangleCapture || viewModel.SelectedTeachingGridRectangleVisible)
            && e.LeftButton != MouseButtonState.Pressed
            && e.MiddleButton != MouseButtonState.Pressed
            && e.RightButton != MouseButtonState.Pressed)
        {
            UpdateTeachingGridRectangleHover(e.GetPosition(Viewport));
        }

        if (HasVisibleTeachingOrientedBoxDraft
            && e.LeftButton != MouseButtonState.Pressed
            && e.MiddleButton != MouseButtonState.Pressed
            && e.RightButton != MouseButtonState.Pressed)
        {
            UpdateTeachingOrientedBoxHover(e.GetPosition(Viewport));
        }

        if (e.LeftButton != MouseButtonState.Pressed
            && e.MiddleButton != MouseButtonState.Pressed
            && e.RightButton != MouseButtonState.Pressed
            && profileDraggedEndpoint == 0
            && !isOrbiting
            && !isPanning)
        {
            UpdateThreeDGridHover(e.GetPosition(Viewport));
        }

        if (profileDraggedEndpoint != 0)
        {
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                profileDraggedEndpoint = 0;
                Viewport.ReleaseMouseCapture();
                return;
            }

            var profilePoint = e.GetPosition(Viewport);
            if (TryMoveProfileEndpoint(profilePoint))
            {
                RequestInteractiveRender();
            }

            return;
        }

        if (!isOrbiting && !isPanning)
        {
            return;
        }

        var current = e.GetPosition(Viewport);
        if (isPanning
            && rightPanPointerDownPosition is { } rightPanStart
            && !rightPanDragExceeded)
        {
            var rightPanDelta = current - rightPanStart;
            if (Math.Abs(rightPanDelta.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(rightPanDelta.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            rightPanDragExceeded = true;
            suppressNextViewerContextMenu = true;
            Viewport.CaptureMouse();
        }

        if (viewModel.IsTeachingCaptureActive
            && teachingCapturePointerDownPosition is { } captureStart
            && isOrbiting
            && !teachingCaptureDragExceeded)
        {
            var captureDelta = current - captureStart;
            if (Math.Abs(captureDelta.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(captureDelta.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            teachingCaptureDragExceeded = true;
        }

        if (profilePointerDownPosition is { } profileStart
            && isOrbiting
            && !profilePointerDragExceeded)
        {
            var profileDelta = current - profileStart;
            if (Math.Abs(profileDelta.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(profileDelta.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            profilePointerDragExceeded = true;
        }

        var delta = current - lastMousePosition;
        lastMousePosition = current;

        if (isPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed
                && e.RightButton != MouseButtonState.Pressed
                && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                isPanning = false;
                Viewport.ReleaseMouseCapture();
                return;
            }

            PanCamera(delta);
        }
        else
        {
            if (e.LeftButton != MouseButtonState.Pressed && e.RightButton != MouseButtonState.Pressed)
            {
                isOrbiting = false;
                Viewport.ReleaseMouseCapture();
                return;
            }

            viewModel.ExitTopOrthographicForOrbit();
            viewModel.YawDegrees += delta.X * 0.35;
            viewModel.PitchDegrees = Math.Clamp(viewModel.PitchDegrees - delta.Y * 0.35, -80.0, 80.0);
            viewModel.UpdateCameraStatus();
        }

        RequestInteractiveRender();
    }

    private void Viewport_MouseLeave(object sender, MouseEventArgs e) =>
        PublishThreeDGridHover(null);

    private void RequestInteractiveRender()
    {
        if (pointerInputRegressionActive)
        {
            pointerInputScheduledMouseMoveRenderCount++;
        }

        BeginInteractionWireframeLod();
        ScheduleInteractionWireframeLodRestore();

        // SharpGL already owns the Viewport frame schedule. Updating only
        // lightweight WPF state here lets all pointer changes since the previous
        // frame collapse into that next scheduled render instead of calling the
        // synchronous OpenGL path once per MouseMove event.
        UpdateOrientationTriad();
    }

    private void Viewport_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseUpCount++;
        }

        var teachingCaptureStart = teachingCapturePointerDownPosition;
        var teachingCapturePointsBefore = viewModel.TeachingCaptureSnapshot.CapturedPointCount;
        var completedGridRectangleEditMode = teachingGridRectangleEditMode;
        var wasEditingGridRectangle = teachingGridRectangleEditMode != TeachingGridRectangleEditMode.None;
        var wasEditingOrientedBox =
            teachingOrientedBoxEditMode != TeachingOrientedBox3DEditMode.None;
        var captureClick = viewModel.IsTeachingCaptureActive
            && e.ChangedButton == MouseButton.Left
            && teachingCaptureStart is not null
            && !wasEditingGridRectangle
            && !wasEditingOrientedBox
            && !teachingCaptureDragExceeded;
        var captureRectangleDrag = IsTeachingGridRectangleCapture
            && e.ChangedButton == MouseButton.Left
            && teachingCaptureStart is not null
            && !wasEditingGridRectangle
            && !wasEditingOrientedBox
            && teachingCaptureDragExceeded;
        var profileClick = viewModel.SelectedSelectionMode == MainWindowViewModel.ProfileSelectionMode
            && e.ChangedButton == MouseButton.Left
            && profilePointerDownPosition is not null
            && !profilePointerDragExceeded
            && profileDraggedEndpoint == 0;
        var capturePoint = e.GetPosition(Viewport);
        if (e.ChangedButton == MouseButton.Right)
        {
            suppressNextViewerContextMenu = rightPanDragExceeded;
            rightPanPointerDownPosition = null;
            rightPanDragExceeded = false;
        }

        isOrbiting = false;
        isPanning = false;
        ScheduleInteractionWireframeLodRestore();
        if (profileDraggedEndpoint != 0)
        {
            profileDraggedEndpoint = 0;
            viewModel.ViewerStatus = "Profile endpoint move completed";
        }
        if (wasEditingGridRectangle && e.ChangedButton == MouseButton.Left)
        {
            TryUpdateTeachingGridRectangleEdit(capturePoint);
        }
        if (wasEditingOrientedBox && e.ChangedButton == MouseButton.Left)
        {
            TryUpdateTeachingOrientedBoxEdit(
                capturePoint,
                "Viewer pointer completed");
        }
        Viewport.ReleaseMouseCapture();
        ClearTeachingGridRectangleEdit();
        teachingCapturePointerDownPosition = null;
        teachingCaptureDragExceeded = false;
        HideTeachingCaptureDragOverlay();
        profilePointerDownPosition = null;
        profilePointerDragExceeded = false;

        if (wasEditingOrientedBox)
        {
            CompleteTeachingOrientedBoxEdit();
        }
        else if (wasEditingGridRectangle)
        {
            if (completedGridRectangleEditMode == TeachingGridRectangleEditMode.Height)
            {
                viewModel.ViewerStatus =
                    "Surface ROI overlay Y-position drag completed; ROI size, measurement, and recipe stay unchanged.";
                RaiseTeachingRoiDisplayHeightChanged("vertical handle drag");
            }
            RenderNow();
        }
        else if (captureRectangleDrag && teachingCaptureStart is { } rectangleStart)
        {
            if (teachingCapturePointsBefore == 0)
            {
                TryHandleC3DTeachingCapturePick(rectangleStart);
            }

            if (viewModel.IsTeachingCaptureActive
                && viewModel.TeachingCaptureSnapshot.CapturedPointCount
                    < viewModel.TeachingCaptureSnapshot.RequiredPointCount)
            {
                TryHandleC3DTeachingCapturePick(capturePoint);
            }

            RenderNow();
        }
        else if (captureClick)
        {
            TryHandleC3DTeachingCapturePick(capturePoint);
            RenderNow();
        }

        else if (profileClick)
        {
            TryHandleProfilePick(capturePoint);
            RenderNow();
        }
    }

    private void Viewport_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (!suppressNextViewerContextMenu)
        {
            return;
        }

        suppressNextViewerContextMenu = false;
        e.Handled = true;
    }

    private void Viewport_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseWheelCount++;
        }

        var wheelSteps = e.Delta / 120.0;
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)
            && viewModel.SelectedTeachingGridRectangleVisible
            && c3dSample is not null)
        {
            viewModel.SelectedTeachingRoiDisplayHeightOffset +=
                wheelSteps * Math.Max((c3dSample.Max - c3dSample.Min) * 0.01, 10.0);
            viewModel.ViewerStatus =
                "Surface ROI overlay Y position adjusted with Alt+wheel; ROI size, measurement, and recipe stay unchanged.";
            RaiseTeachingRoiDisplayHeightChanged("Alt+wheel");
            RenderNow();
            e.Handled = true;
            return;
        }

        BeginInteractionWireframeLod();
        var zoomScale = Math.Pow(0.80, wheelSteps);
        viewModel.ZoomCamera(zoomScale);
        RequestInteractiveRender();
        ScheduleInteractionWireframeLodRestore();
        e.Handled = true;
    }

}

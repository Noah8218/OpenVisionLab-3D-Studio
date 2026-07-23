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
        c3dDisplayListId = 0;
        c3dDisplayListKey = null;
        var gl = args.OpenGL;
        gl.ClearColor(0.08f, 0.10f, 0.13f, 1.0f);
        gl.Enable(OpenGL.GL_DEPTH_TEST);
        gl.DepthFunc(OpenGL.GL_LEQUAL);
        gl.ShadeModel(OpenGL.GL_SMOOTH);
    }

    private void Viewport_Resized(object sender, OpenGLRoutedEventArgs args)
    {
        ConfigureProjection(args.OpenGL);
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

    private void Viewport_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (pointerInputRegressionActive)
        {
            pointerInputMouseDownCount++;
        }

        lastMousePosition = e.GetPosition(Viewport);

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
            teachingCapturePointerDownPosition = lastMousePosition;
            teachingCaptureDragExceeded = false;
            isPanning = false;
            isOrbiting = true;
            Viewport.CaptureMouse();
            return;
        }

        if (e.ChangedButton == MouseButton.Left && !panRequested)
        {
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

            viewModel.YawDegrees += delta.X * 0.35;
            viewModel.PitchDegrees = Math.Clamp(viewModel.PitchDegrees - delta.Y * 0.35, -80.0, 80.0);
            viewModel.UpdateCameraStatus();
        }

        RequestInteractiveRender();
    }

    private void RequestInteractiveRender()
    {
        if (pointerInputRegressionActive)
        {
            pointerInputScheduledMouseMoveRenderCount++;
        }

        // SharpGL already renders at the Viewport's fixed 30 FPS. Updating only
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

        var captureClick = viewModel.IsTeachingCaptureActive
            && e.ChangedButton == MouseButton.Left
            && teachingCapturePointerDownPosition is not null
            && !teachingCaptureDragExceeded;
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
        if (profileDraggedEndpoint != 0)
        {
            profileDraggedEndpoint = 0;
            viewModel.ViewerStatus = "Profile endpoint move completed";
        }
        Viewport.ReleaseMouseCapture();
        teachingCapturePointerDownPosition = null;
        teachingCaptureDragExceeded = false;
        profilePointerDownPosition = null;
        profilePointerDragExceeded = false;

        if (captureClick)
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

        var zoomScale = e.Delta > 0 ? 0.88 : 1.14;
        viewModel.ZoomCamera(zoomScale);
        RenderNow();
    }

}

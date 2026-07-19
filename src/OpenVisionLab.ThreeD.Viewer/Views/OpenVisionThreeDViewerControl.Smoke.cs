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
    public void EnableSmokeFromCommandLine() => EnableSmokeFromCommandLine(ownsApplicationLifecycle: true);

    public void EnableSmokeFromCommandLine(bool ownsApplicationLifecycle)
    {
        var args = Environment.GetCommandLineArgs();
        var smokeIndex = Array.IndexOf(args, "--smoke-screenshot");
        if (smokeIndex >= 0 && smokeIndex + 1 < args.Length)
        {
            smokeScreenshotPath = args[smokeIndex + 1];
        }

        var screenshotQualityIndex = Array.IndexOf(args, "--smoke-screenshot-quality-report");
        if (screenshotQualityIndex >= 0 && screenshotQualityIndex + 1 < args.Length)
        {
            smokeScreenshotQualityReportPath = args[screenshotQualityIndex + 1];
        }

        ApplySmokeArguments(args);
        if (ownsApplicationLifecycle && smokeScreenshotPath is not null)
        {
            Loaded += SmokeCaptureOnLoaded;
        }
    }

    public bool HasConfiguredSmokeScreenshot => smokeScreenshotPath is not null;

    public async Task<bool> CaptureConfiguredSmokeViewAsync()
    {
        await RunConfiguredSmokeRenderFramesAsync();

        if (smokeContractsPath is not null)
        {
            WriteSceneContracts(smokeContractsPath);
        }

        if (smokeScreenshotPath is null)
        {
            return smokeExitCode == 0;
        }

        if (!await CaptureSmokeViewWithRetryAsync(smokeScreenshotPath, smokeScreenshotQualityReportPath))
        {
            SetSmokeFailure("Viewer screenshot remained blank or invalid after 3 attempts.");
        }

        return smokeExitCode == 0;
    }

    public bool ApplyConfiguredSmokePick()
    {
        switch (smokePickTarget)
        {
            case null:
                return true;
            case "cube":
                ApplySmokePickCube();
                break;
            case "c3d":
                ApplySmokePickC3D();
                break;
            case "laz":
            case "laz-point":
            case "laz-points":
                ApplySmokePickLaz();
                break;
            case "glb":
            case "mesh":
            case "glb-mesh":
                ApplySmokePickGlb();
                break;
            case "nominal-actual":
            case "nominal":
            case "deviation":
                ApplySmokePickNominalActual();
                break;
            default:
                SetSmokeFailure($"Unsupported smoke pick target: {smokePickTarget}");
                break;
        }

        RenderNow();
        return smokeExitCode == 0;
    }

    public bool ApplyConfiguredSmokeNextDensity()
    {
        if (smokeNextRenderDensity is null)
        {
            return true;
        }

        if (!viewModel.RenderDensityModes.Contains(smokeNextRenderDensity, StringComparer.Ordinal))
        {
            SetSmokeFailure($"Unsupported next Preview density: {smokeNextRenderDensity}");
            return false;
        }

        if (viewModel.NominalActual.PreviewResult is null)
        {
            SetSmokeFailure("Next Preview density smoke requires a completed nominal/actual result");
            return false;
        }

        viewModel.SelectedRenderDensity = smokeNextRenderDensity;
        RenderNow();
        return smokeExitCode == 0;
    }

    public async Task<bool> RunConfiguredPointerInputRegressionAsync()
    {
        if (smokePointerInputReportPath is null)
        {
            return true;
        }

        pointerInputRegressionResult = await RunPointerInputRegressionAsync();
        WritePointerInputRegressionReport(smokePointerInputReportPath, pointerInputRegressionResult);
        if (!pointerInputRegressionResult.Passed)
        {
            SetSmokeFailure($"Pointer input regression failed: {pointerInputRegressionResult.Failure}");
        }
        else
        {
            viewModel.ViewerStatus = "Pointer input regression passed: pick, left-orbit, middle/right-pan, zoom, and short-right-click menu";
        }

        RenderNow();
        return pointerInputRegressionResult.Passed;
    }

    private async Task<PointerInputRegressionResult> RunPointerInputRegressionAsync()
    {
        var initialCamera = CaptureCameraSnapshot();
        var orbitCamera = initialCamera;
        var panCamera = initialCamera;
        var rightPanCamera = initialCamera;
        var zoomCamera = initialCamera;
        var pickedEntity = "(none)";
        var pickCoordinate = "(none)";
        var selectionSummary = "(none)";
        var viewportWidth = 0.0;
        var viewportHeight = 0.0;
        var windowActivated = false;
        var pickPassed = false;
        var orbitPassed = false;
        var panPassed = false;
        var rightPanPassed = false;
        var rightPanMenuSuppressed = false;
        var zoomPassed = false;
        var contextMenuPassed = false;
        var contextMenuBindingsPassed = false;
        var contextMenuCommandCount = 0;
        var topViewMenuBindingsPassed = false;
        var topViewMenuCommandCount = 0;
        var failure = string.Empty;
        var originalPointer = default(Point);
        var hasOriginalPointer = false;
        var leftPressed = false;
        var rightPressed = false;
        var middlePressed = false;
        Window? hostWindow = null;
        var originalTopmost = false;

        pointerInputMouseDownCount = 0;
        pointerInputMouseMoveCount = 0;
        pointerInputMouseUpCount = 0;
        pointerInputMouseWheelCount = 0;

        try
        {
            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException("Viewer is not attached to a visible WPF window.");

            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            hostWindow.Activate();
            hostWindow.Focus();
            await Dispatcher.InvokeAsync(() =>
            {
                viewModel.Reset();
                viewModel.CubeVisible = true;
                viewModel.PointCloudVisible = false;
                viewModel.SelectionOverlayVisible = false;
                viewModel.ResultOverlayVisible = false;
                viewModel.MeasurementVisible = true;
                viewModel.SelectedEntity = "Generated Unit Cube";
                viewModel.FitSelection();
                viewModel.PickCoordinate = "(none)";
                viewModel.ViewerStatus = "Pointer input regression ready";
                RenderNow();
            }, DispatcherPriority.Render);
            await Task.Delay(250);

            windowActivated = hostWindow.IsActive;
            viewportWidth = Viewport.ActualWidth;
            viewportHeight = Viewport.ActualHeight;
            if (!Viewport.IsVisible || viewportWidth < 200.0 || viewportHeight < 180.0)
            {
                throw new InvalidOperationException(
                    $"Viewport is not ready for pointer input ({viewportWidth:F0}x{viewportHeight:F0}).");
            }

            initialCamera = CaptureCameraSnapshot();
            orbitCamera = initialCamera;
            panCamera = initialCamera;
            rightPanCamera = initialCamera;
            zoomCamera = initialCamera;
            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);

            var center = Viewport.PointToScreen(new Point(viewportWidth * 0.5, viewportHeight * 0.5));
            await EnsurePointerInputTargetAsync(hostWindow, center);
            pointerInputRegressionActive = true;
            WindowsPointerInput.MoveTo(center);
            await Task.Delay(120);
            WindowsPointerInput.LeftDown();
            leftPressed = true;
            await Task.Delay(100);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Task.Delay(180);

            pickedEntity = viewModel.SelectedEntity;
            pickCoordinate = viewModel.PickCoordinate;
            selectionSummary = viewModel.SelectionSummary;
            pickPassed = pickedEntity == "Generated Unit Cube"
                && !pickCoordinate.Equals("(none)", StringComparison.Ordinal)
                && selectionSummary.StartsWith("Cube pick:", StringComparison.Ordinal);

            var orbitStart = Viewport.PointToScreen(new Point(viewportWidth * 0.72, viewportHeight * 0.60));
            var orbitEnd = Viewport.PointToScreen(new Point(viewportWidth * 0.86, viewportHeight * 0.50));
            await EnsurePointerInputTargetAsync(hostWindow, orbitStart);
            // Orbit remains a left-drag gesture.
            WindowsPointerInput.LeftDown();
            leftPressed = true;
            await Task.Delay(100);
            WindowsPointerInput.MoveTo(orbitEnd);
            await Task.Delay(180);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Task.Delay(160);
            orbitCamera = CaptureCameraSnapshot();
            orbitPassed = IsFinite(orbitCamera)
                && Math.Abs(orbitCamera.Yaw - initialCamera.Yaw) > 1.0
                && Math.Abs(orbitCamera.Pitch - initialCamera.Pitch) > 1.0;

            var panStart = Viewport.PointToScreen(new Point(viewportWidth * 0.82, viewportHeight * 0.70));
            var panEnd = Viewport.PointToScreen(new Point(viewportWidth * 0.70, viewportHeight * 0.62));
            await EnsurePointerInputTargetAsync(hostWindow, panStart);
            WindowsPointerInput.MiddleDown();
            middlePressed = true;
            await Task.Delay(100);
            WindowsPointerInput.MoveTo(panEnd);
            await Task.Delay(180);
            WindowsPointerInput.MiddleUp();
            middlePressed = false;
            await Task.Delay(160);
            panCamera = CaptureCameraSnapshot();
            panPassed = IsFinite(panCamera) && TargetChanged(orbitCamera, panCamera);

            var rightPanStart = Viewport.PointToScreen(new Point(viewportWidth * 0.68, viewportHeight * 0.68));
            var rightPanEnd = Viewport.PointToScreen(new Point(viewportWidth * 0.57, viewportHeight * 0.58));
            await EnsurePointerInputTargetAsync(hostWindow, rightPanStart);
            WindowsPointerInput.RightDown();
            rightPressed = true;
            await Task.Delay(100);
            WindowsPointerInput.MoveTo(rightPanEnd);
            await Task.Delay(180);
            WindowsPointerInput.RightUp();
            rightPressed = false;
            await Task.Delay(160);
            rightPanCamera = CaptureCameraSnapshot();
            rightPanPassed = IsFinite(rightPanCamera) && TargetChanged(panCamera, rightPanCamera);
            rightPanMenuSuppressed = await Dispatcher.InvokeAsync(
                () => Viewport.ContextMenu?.IsOpen != true,
                DispatcherPriority.Input);

            await EnsurePointerInputTargetAsync(hostWindow, center);
            WindowsPointerInput.Wheel(120);
            await Task.Delay(180);
            zoomCamera = CaptureCameraSnapshot();
            zoomPassed = IsFinite(zoomCamera)
                && zoomCamera.Distance < rightPanCamera.Distance - 0.000001;

            var contextMenuPoint = Viewport.PointToScreen(new Point(viewportWidth * 0.56, viewportHeight * 0.42));
            await EnsurePointerInputTargetAsync(hostWindow, contextMenuPoint);
            WindowsPointerInput.RightDown();
            rightPressed = true;
            await Task.Delay(100);
            WindowsPointerInput.RightUp();
            rightPressed = false;
            await Task.Delay(180);
            contextMenuPassed = await Dispatcher.InvokeAsync(
                () => Viewport.ContextMenu?.IsOpen == true,
                DispatcherPriority.Input);
            var contextMenuBindings = await Dispatcher.InvokeAsync(
                InspectViewerContextMenuBindings,
                DispatcherPriority.Input);
            contextMenuBindingsPassed = contextMenuBindings.Passed;
            contextMenuCommandCount = contextMenuBindings.CommandCount;
            var topViewMenuBindings = await Dispatcher.InvokeAsync(
                InspectViewerTopMenuBindings,
                DispatcherPriority.Input);
            topViewMenuBindingsPassed = topViewMenuBindings.Passed;
            topViewMenuCommandCount = topViewMenuBindings.CommandCount;
            await Dispatcher.InvokeAsync(() =>
            {
                if (Viewport.ContextMenu is { } menu)
                {
                    menu.IsOpen = false;
                }
            }, DispatcherPriority.Input);
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }
        finally
        {
            pointerInputRegressionActive = false;
            if (leftPressed)
            {
                WindowsPointerInput.LeftUp();
            }

            if (rightPressed)
            {
                WindowsPointerInput.RightUp();
            }

            if (middlePressed)
            {
                WindowsPointerInput.MiddleUp();
            }

            if (hasOriginalPointer)
            {
                try
                {
                    WindowsPointerInput.MoveTo(originalPointer);
                }
                catch (Win32Exception)
                {
                    // Pointer restoration is best effort after the regression evidence is captured.
                }
            }

            if (hostWindow is not null)
            {
                hostWindow.Topmost = originalTopmost;
            }
        }

        var routedEventsPassed = pointerInputMouseDownCount >= 5
            && pointerInputMouseMoveCount >= 3
            && pointerInputMouseUpCount >= 5
            && pointerInputMouseWheelCount >= 1;
        var passed = pickPassed
            && orbitPassed
            && panPassed
            && rightPanPassed
            && rightPanMenuSuppressed
            && zoomPassed
            && contextMenuPassed
            && contextMenuBindingsPassed
            && topViewMenuBindingsPassed
            && routedEventsPassed;
        if (!passed && string.IsNullOrWhiteSpace(failure))
        {
            failure = CreatePointerInputFailureSummary(
                pickPassed,
                orbitPassed,
                panPassed,
                rightPanPassed,
                rightPanMenuSuppressed,
                zoomPassed,
                contextMenuPassed,
                contextMenuBindingsPassed,
                topViewMenuBindingsPassed,
                routedEventsPassed);
        }

        return new PointerInputRegressionResult(
            passed,
            windowActivated,
            pickPassed,
            orbitPassed,
            panPassed,
            rightPanPassed,
            rightPanMenuSuppressed,
            zoomPassed,
            contextMenuPassed,
            contextMenuBindingsPassed,
            contextMenuCommandCount,
            topViewMenuBindingsPassed,
            topViewMenuCommandCount,
            routedEventsPassed,
            pointerInputMouseDownCount,
            pointerInputMouseMoveCount,
            pointerInputMouseUpCount,
            pointerInputMouseWheelCount,
            viewportWidth,
            viewportHeight,
            initialCamera,
            orbitCamera,
            panCamera,
            rightPanCamera,
            zoomCamera,
            pickedEntity,
            pickCoordinate,
            selectionSummary,
            failure);
    }

    private async Task EnsurePointerInputTargetAsync(Window hostWindow, Point screenPoint)
    {
        const int maximumAttempts = 3;
        var diagnostics = new List<string>(maximumAttempts);

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            hostWindow.Activate();
            hostWindow.Focus();
            Viewport.Focus();
            Keyboard.Focus(Viewport);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Input);

            WindowsPointerInput.BringWindowToInputFront(hostWindow);
            WindowsPointerInput.MoveTo(screenPoint);
            await Task.Delay(120);
            if (WindowsPointerInput.IsScreenPointOverWindow(hostWindow, screenPoint, out var diagnostic))
            {
                return;
            }

            diagnostics.Add($"attempt={attempt}|active={hostWindow.IsActive}|{diagnostic}");
            await Task.Delay(120);
        }

        throw new InvalidOperationException(
            "Viewer host is not the Windows pointer target before pointer input. "
            + string.Join("; ", diagnostics));
    }

    private CameraSnapshot CaptureCameraSnapshot() => new(
        viewModel.YawDegrees,
        viewModel.PitchDegrees,
        viewModel.CameraDistance,
        viewModel.CameraTargetX,
        viewModel.CameraTargetY,
        viewModel.CameraTargetZ);

    private static bool IsFinite(CameraSnapshot camera) =>
        double.IsFinite(camera.Yaw)
        && double.IsFinite(camera.Pitch)
        && double.IsFinite(camera.Distance)
        && double.IsFinite(camera.TargetX)
        && double.IsFinite(camera.TargetY)
        && double.IsFinite(camera.TargetZ);

    private static bool TargetChanged(CameraSnapshot before, CameraSnapshot after)
    {
        var dx = after.TargetX - before.TargetX;
        var dy = after.TargetY - before.TargetY;
        var dz = after.TargetZ - before.TargetZ;
        return (dx * dx) + (dy * dy) + (dz * dz) > 0.00000001;
    }

    private static string CreatePointerInputFailureSummary(
        bool pickPassed,
        bool orbitPassed,
        bool panPassed,
        bool rightPanPassed,
        bool rightPanMenuSuppressed,
        bool zoomPassed,
        bool contextMenuPassed,
        bool contextMenuBindingsPassed,
        bool topViewMenuBindingsPassed,
        bool routedEventsPassed)
    {
        var failures = new List<string>();
        if (!pickPassed) failures.Add("pick state did not change");
        if (!orbitPassed) failures.Add("orbit camera did not change");
        if (!panPassed) failures.Add("middle-button pan target did not change");
        if (!rightPanPassed) failures.Add("right-drag pan target did not change");
        if (!rightPanMenuSuppressed) failures.Add("right-drag opened the Viewer context menu");
        if (!zoomPassed) failures.Add("zoom distance did not change");
        if (!contextMenuPassed) failures.Add("Viewer context menu did not open");
        if (!contextMenuBindingsPassed) failures.Add("Viewer context menu command bindings were incomplete");
        if (!topViewMenuBindingsPassed) failures.Add("Viewer top View menu command bindings were incomplete");
        if (!routedEventsPassed) failures.Add("WPF mouse event counts were incomplete");
        return string.Join("; ", failures);
    }

    private (bool Passed, int CommandCount) InspectViewerContextMenuBindings()
    {
        if (Viewport.ContextMenu is not { } menu)
        {
            return (false, 0);
        }

        var commands = menu.Items
            .OfType<MenuItem>()
            .SelectMany(item => item.Command is null
                ? item.Items.OfType<MenuItem>()
                : [item])
            .Select(item => item.Command)
            .Where(command => command is not null)
            .ToArray();
        var expected = new ICommand[]
        {
            viewModel.FitAllCommand,
            viewModel.FitSelectionCommand,
            viewModel.ResetCommand,
            viewModel.ScreenshotCommand,
            viewModel.ProfileCommand
        };
        return (
            commands.Length == expected.Length
            && expected.All(expectedCommand => commands.Any(command => ReferenceEquals(command, expectedCommand))),
            commands.Length);
    }

    private (bool Passed, int CommandCount) InspectViewerTopMenuBindings()
    {
        var commands = ViewerViewMenuRoot.Items
            .OfType<MenuItem>()
            .Select(item => item.Command)
            .Where(command => command is not null)
            .ToArray();
        var expected = new ICommand[]
        {
            viewModel.FitAllCommand,
            viewModel.FitSelectionCommand,
            viewModel.ResetCommand,
            viewModel.ScreenshotCommand,
            viewModel.ProfileCommand
        };
        return (
            commands.Length == expected.Length
            && expected.All(expectedCommand => commands.Any(command => ReferenceEquals(command, expectedCommand))),
            commands.Length);
    }

    private static void WritePointerInputRegressionReport(
        string path,
        PointerInputRegressionResult result)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var lines = new[]
        {
            "PointerInputRegression",
            $"Result|pass={result.Passed}|windowActivated={result.WindowActivated}|viewport={result.ViewportWidth:F0}x{result.ViewportHeight:F0}",
            $"RoutedEvents|pass={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}|mouseWheel={result.MouseWheelCount}",
            $"Pick|pass={result.PickPassed}|entity={result.PickedEntity}|coordinate={result.PickCoordinate}|summary={result.SelectionSummary}",
            $"Orbit|pass={result.OrbitPassed}|before={FormatCameraSnapshot(result.InitialCamera)}|after={FormatCameraSnapshot(result.OrbitCamera)}",
            $"Pan|pass={result.PanPassed}|before={FormatCameraSnapshot(result.OrbitCamera)}|after={FormatCameraSnapshot(result.PanCamera)}",
            $"RightDragPan|pass={result.RightPanPassed && result.RightPanMenuSuppressed}|targetChanged={result.RightPanPassed}|menuSuppressed={result.RightPanMenuSuppressed}|before={FormatCameraSnapshot(result.PanCamera)}|after={FormatCameraSnapshot(result.RightPanCamera)}",
            $"Zoom|pass={result.ZoomPassed}|before={FormatCameraSnapshot(result.RightPanCamera)}|after={FormatCameraSnapshot(result.ZoomCamera)}",
            $"ContextMenu|pass={result.ContextMenuPassed}",
            $"InputModes|rightDragPan={result.RightPanPassed && result.RightPanMenuSuppressed}|shortRightClick={result.ContextMenuPassed}",
            $"ContextMenuBindings|pass={result.ContextMenuBindingsPassed}|commands={result.ContextMenuCommandCount}/5",
            $"TopViewMenuBindings|pass={result.TopViewMenuBindingsPassed}|commands={result.TopViewMenuCommandCount}/5",
            $"Failure|summary={result.Failure}"
        };
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
    }

    private static string FormatCameraSnapshot(CameraSnapshot camera) => string.Create(
        CultureInfo.InvariantCulture,
        $"yaw:{camera.Yaw:R},pitch:{camera.Pitch:R},distance:{camera.Distance:R},target:({camera.TargetX:R},{camera.TargetY:R},{camera.TargetZ:R})");

    private string CreatePointerInputRegressionContractLine()
    {
        if (smokePointerInputReportPath is null)
        {
            return "PointerInputRegression|configured=False";
        }

        if (pointerInputRegressionResult is null)
        {
            return "PointerInputRegression|configured=True|pass=False|failure=not-run";
        }

        var result = pointerInputRegressionResult;
        return $"PointerInputRegression|configured=True|pass={result.Passed}|pick={result.PickPassed}|orbit={result.OrbitPassed}|pan={result.PanPassed}|middlePan={result.PanPassed}|rightDragPan={result.RightPanPassed && result.RightPanMenuSuppressed}|rightPanMenuSuppressed={result.RightPanMenuSuppressed}|zoom={result.ZoomPassed}|shortRightClick={result.ContextMenuPassed}|contextMenu={result.ContextMenuPassed}|contextMenuBindings={result.ContextMenuBindingsPassed}|contextMenuCommands={result.ContextMenuCommandCount}/5|topViewMenuBindings={result.TopViewMenuBindingsPassed}|topViewMenuCommands={result.TopViewMenuCommandCount}/5|routedEvents={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}|mouseWheel={result.MouseWheelCount}|windowActivated={result.WindowActivated}|viewport={result.ViewportWidth:F0}x{result.ViewportHeight:F0}|failure={CleanContractText(result.Failure)}";
    }

    private void ApplySmokeArguments(string[] args)
    {
        var renderFramesIndex = Array.IndexOf(args, "--smoke-render-frames");
        if (renderFramesIndex >= 0)
        {
            if (renderFramesIndex + 1 >= args.Length
                || !int.TryParse(
                    args[renderFramesIndex + 1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out smokeRenderFrameCount)
                || smokeRenderFrameCount is < 16 or > 200)
            {
                smokeRenderFrameCount = 0;
                SetSmokeFailure("Smoke render frames must be an integer from 16 through 200.");
            }
        }

        var densityIndex = Array.IndexOf(args, "--smoke-density");
        if (densityIndex >= 0 && densityIndex + 1 < args.Length)
        {
            viewModel.SelectedRenderDensity = args[densityIndex + 1];
        }

        var nextDensityIndex = Array.IndexOf(args, "--smoke-next-density");
        if (nextDensityIndex >= 0 && nextDensityIndex + 1 < args.Length)
        {
            smokeNextRenderDensity = args[nextDensityIndex + 1];
        }

        var pointSizeIndex = Array.IndexOf(args, "--smoke-point-size");
        if (pointSizeIndex >= 0
            && pointSizeIndex + 1 < args.Length
            && double.TryParse(args[pointSizeIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var pointSize))
        {
            viewModel.PointSize = pointSize;
        }

        ApplySmokeTolerance(args);

        var sceneIndex = Array.IndexOf(args, "--smoke-scene");
        if (sceneIndex >= 0 && sceneIndex + 1 < args.Length && args[sceneIndex + 1].Equals("pointcloud", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.UsePointCloudSmokeScene();
        }

        var c3dIndex = Array.IndexOf(args, "--smoke-c3d");
        if (c3dIndex >= 0)
        {
            ApplySmokeC3D();
        }

        var glbIndex = Array.IndexOf(args, "--smoke-glb");
        if (glbIndex >= 0)
        {
            var glbPath = glbIndex + 1 < args.Length && !args[glbIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[glbIndex + 1]
                : null;
            ApplySmokeGlb(glbPath);
        }

        var stlIndex = Array.IndexOf(args, "--smoke-stl");
        if (stlIndex >= 0)
        {
            var stlPath = stlIndex + 1 < args.Length && !args[stlIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[stlIndex + 1]
                : null;
            ApplySmokeStl(stlPath);
        }

        var lazIndex = Array.IndexOf(args, "--smoke-laz");
        if (lazIndex >= 0)
        {
            var lazPath = lazIndex + 1 < args.Length && !args[lazIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[lazIndex + 1]
                : null;
            ApplySmokeLaz(lazPath);
        }

        var lazPointsIndex = Array.IndexOf(args, "--smoke-laz-points");
        if (lazPointsIndex >= 0)
        {
            var lazPath = lazPointsIndex + 1 < args.Length && !args[lazPointsIndex + 1].StartsWith("--", StringComparison.Ordinal)
                ? args[lazPointsIndex + 1]
                : null;
            ApplySmokeLazPoints(lazPath);
        }

        ApplySmokeNominalActual(args);

        var actionIndex = Array.IndexOf(args, "--smoke-action");
        if (actionIndex >= 0 && actionIndex + 1 < args.Length)
        {
            ApplySmokeAction(args[actionIndex + 1]);
        }

        var selectionIndex = Array.IndexOf(args, "--smoke-selection");
        if (selectionIndex >= 0 && selectionIndex + 1 < args.Length)
        {
            ApplySmokeSelection(args[selectionIndex + 1]);
        }

        var overlayIndex = Array.IndexOf(args, "--smoke-overlay");
        if (overlayIndex >= 0 && overlayIndex + 1 < args.Length)
        {
            ApplySmokeOverlay(args[overlayIndex + 1]);
        }

        var ruleIndex = Array.IndexOf(args, "--smoke-rule");
        if (ruleIndex >= 0 && ruleIndex + 1 < args.Length)
        {
            ApplySmokeRule(args[ruleIndex + 1]);
        }

        var recipeIndex = Array.IndexOf(args, "--smoke-recipe");
        if (recipeIndex >= 0 && recipeIndex + 1 < args.Length)
        {
            ApplySmokeRecipe(args[recipeIndex + 1]);
        }

        ApplySmokeTolerance(args);

        if (recipeIndex >= 0 && selectionIndex >= 0 && selectionIndex + 1 < args.Length)
        {
            ApplySmokeSelection(args[selectionIndex + 1]);
        }

        var pickIndex = Array.IndexOf(args, "--smoke-pick");
        if (pickIndex >= 0 && pickIndex + 1 < args.Length)
        {
            smokePickTarget = args[pickIndex + 1].ToLowerInvariant();
        }

        var alignmentIndex = Array.IndexOf(args, "--smoke-alignment");
        if (alignmentIndex >= 0 && alignmentIndex + 1 < args.Length)
        {
            ApplySmokeAlignment(args[alignmentIndex + 1]);
        }

        var measureIndex = Array.IndexOf(args, "--smoke-measure");
        if (measureIndex >= 0 && measureIndex + 1 < args.Length)
        {
            smokeMeasureMode = args[measureIndex + 1];
            ApplySmokeMeasure(smokeMeasureMode);
        }

        var hudIndex = Array.IndexOf(args, "--smoke-hud");
        if (hudIndex >= 0 && hudIndex + 1 < args.Length)
        {
            viewModel.HudDetailsVisible = args[hudIndex + 1].Equals("details", StringComparison.OrdinalIgnoreCase);
        }

        var editParametersIndex = Array.IndexOf(args, "--smoke-edit-parameters");
        if (editParametersIndex >= 0 && editParametersIndex + 1 < args.Length)
        {
            ApplySmokeRecipeParameterEdit(args[editParametersIndex + 1]);
        }

        var invalidRoiIndex = Array.IndexOf(args, "--smoke-invalid-roi");
        if (invalidRoiIndex >= 0 && invalidRoiIndex + 1 < args.Length)
        {
            ApplySmokeInvalidRoi(args[invalidRoiIndex + 1]);
        }

        if (Array.IndexOf(args, "--smoke-align-from-roi") >= 0)
        {
            ApplyRoiReferenceAlignment();
        }

        var contractsIndex = Array.IndexOf(args, "--smoke-contracts");
        if (contractsIndex >= 0 && contractsIndex + 1 < args.Length)
        {
            smokeContractsPath = args[contractsIndex + 1];
        }

        var pointerInputReportIndex = Array.IndexOf(args, "--smoke-pointer-input-report");
        if (pointerInputReportIndex >= 0 && pointerInputReportIndex + 1 < args.Length)
        {
            smokePointerInputReportPath = args[pointerInputReportIndex + 1];
        }

        var saveRecipeIndex = Array.IndexOf(args, "--smoke-save-recipe");
        if (saveRecipeIndex >= 0 && saveRecipeIndex + 1 < args.Length)
        {
            smokeSaveRecipePath = args[saveRecipeIndex + 1];
        }

        ApplyNominalActualViewModelVerification(args);
        ApplyDisplayViewModelVerification(args);
        ApplyTeachingCaptureViewModelVerification(args);
        ApplyTeachingCapturePointerSmokeArguments(args);

        smokePublishResult = Array.IndexOf(args, "--smoke-publish-result") >= 0;
        if (smokePublishResult && smokeScreenshotPath is null && !smokeNominalActualPreview)
        {
            PublishCurrentPreviewResult();
        }
    }

    private void ApplyNominalActualViewModelVerification(string[] args)
    {
        var verificationIndex = Array.IndexOf(args, "--verify-nominal-actual-viewmodel");
        if (verificationIndex < 0)
        {
            return;
        }

        if (verificationIndex + 1 >= args.Length
            || args[verificationIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = "Nominal/actual ViewModel verification requires a report path.";
            return;
        }

        if (!NominalActualComparisonViewModelVerification.Verify(args[verificationIndex + 1], out var summary))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = summary;
        }
    }

    private void ApplyDisplayViewModelVerification(string[] args)
    {
        var verificationIndex = Array.IndexOf(args, "--verify-display-viewmodel");
        if (verificationIndex < 0)
        {
            return;
        }

        if (verificationIndex + 1 >= args.Length
            || args[verificationIndex + 1].StartsWith("--", StringComparison.Ordinal))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = "Display-settings ViewModel verification requires a report path.";
            return;
        }

        if (!ViewerDisplaySettingsViewModelVerification.Verify(args[verificationIndex + 1], out var summary))
        {
            smokeExitCode = 1;
            viewModel.ViewerStatus = summary;
        }
    }

    private void ApplySmokeNominalActual(string[] args)
    {
        var comparisonIndex = Array.IndexOf(args, "--smoke-nominal-actual");
        if (comparisonIndex < 0)
        {
            return;
        }

        smokeNominalActualPreview = true;
        if (comparisonIndex + 3 >= args.Length
            || args[comparisonIndex + 1].StartsWith("--", StringComparison.Ordinal)
            || args[comparisonIndex + 2].StartsWith("--", StringComparison.Ordinal)
            || args[comparisonIndex + 3].StartsWith("--", StringComparison.Ordinal))
        {
            SetSmokeFailure(
                "Nominal/actual smoke requires <actual.stl> <validation-query.ply> <nominal.stl>.");
            return;
        }

        try
        {
            var sourceIdentity = ResolveSmokeNominalActualSourceIdentity(args);
            var actual = CaptureComparisonFileIdentity(
                sourceIdentity.ActualId,
                sourceIdentity.ActualName,
                args[comparisonIndex + 1]);
            var query = CaptureComparisonFileIdentity(
                sourceIdentity.QueryId,
                sourceIdentity.QueryName,
                args[comparisonIndex + 2]);
            var nominal = CaptureComparisonFileIdentity(
                "source.nist-overhang-x4-nominal-9x5x5",
                "NIST Overhang X4 nominal 9x5x5 mm",
                args[comparisonIndex + 3]);
            var comparison = viewModel.NominalActual;
            var input = new NominalActualComparisonInput(
                "step.nist-overhang-x4-surface-deviation",
                actual,
                nominal,
                query,
                "mm",
                "frame.nist-overhang-x4-321-part",
                "alignment.identity-source-provided",
                comparison.LowerTolerance,
                comparison.UpperTolerance);

            ApplySmokeStl(nominal.Path);
            if (importedMesh is null)
            {
                throw new InvalidDataException("The nominal comparison mesh could not be loaded for display.");
            }

            viewModel.ConfigureNominalActualComparison(input);
            comparison.PreviewCommand.Execute(null);
        }
        catch (Exception exception)
        {
            viewModel.ClearNominalActualComparison(exception.Message);
            SetSmokeFailure($"Nominal/actual smoke failed: {exception.Message}");
        }
    }

    private static (string ActualId, string ActualName, string QueryId, string QueryName)
        ResolveSmokeNominalActualSourceIdentity(string[] args)
    {
        var datasetIndex = Array.IndexOf(args, "--smoke-nominal-actual-dataset");
        var dataset = datasetIndex < 0
            ? "nist-overhang-x4-part1"
            : datasetIndex + 1 < args.Length
                && !args[datasetIndex + 1].StartsWith("--", StringComparison.Ordinal)
                    ? args[datasetIndex + 1]
                    : throw new ArgumentException(
                        "Nominal/actual smoke dataset requires nist-overhang-x4-part1 or nist-overhang-x4-part2.");

        return dataset.ToLowerInvariant() switch
        {
            "nist-overhang-x4-part1" => (
                "source.nist-overhang-x4-actual-part1",
                "NIST Overhang X4 Part 1 XCT surface",
                "query.nist-overhang-x4-cloudcompare-vertices",
                "NIST Overhang X4 validation vertices"),
            "nist-overhang-x4-part2" => (
                "source.nist-overhang-x4-actual-part2",
                "NIST Overhang X4 Part 2 XCT surface",
                "query.nist-overhang-x4-part2-cloudcompare-vertices",
                "NIST Overhang X4 Part 2 validation vertices"),
            _ => throw new ArgumentException($"Unsupported nominal/actual smoke dataset: {dataset}"),
        };
    }

    private static NominalActualFileIdentity CaptureComparisonFileIdentity(
        string id,
        string name,
        string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        return new NominalActualFileIdentity(
            id,
            name,
            fullPath,
            stream.Length,
            Convert.ToHexString(SHA256.HashData(stream)));
    }

    private void ApplySmokeTolerance(string[] args)
    {
        var toleranceIndex = Array.IndexOf(args, "--smoke-tolerance");
        if (toleranceIndex >= 0
            && toleranceIndex + 1 < args.Length
            && double.TryParse(args[toleranceIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var tolerance))
        {
            viewModel.RecipePeakTolerance = tolerance;
        }

        var flatnessToleranceIndex = Array.IndexOf(args, "--smoke-flatness-tolerance");
        if (flatnessToleranceIndex >= 0
            && flatnessToleranceIndex + 1 < args.Length
            && double.TryParse(args[flatnessToleranceIndex + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var flatnessTolerance))
        {
            viewModel.PlaneFlatnessTolerance = flatnessTolerance;
        }
    }

    private void ApplySmokeAlignment(string mode)
    {
        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        var transform = mode.ToLowerInvariant() switch
        {
            "offset" or "translated" => new ModelTransform(0.350, 0.180, -0.250, 0.0, 0.0, 0.0, 1.0),
            "tilt" or "rotated" => new ModelTransform(0.250, 0.120, -0.180, 0.0, 0.0, 2.5, 1.0),
            _ => ModelTransform.Identity
        };
        var alignmentName = ModelTransformIsIdentity(transform) ? "Identity / not aligned" : $"Smoke {mode} alignment";
        viewModel.SetC3DAlignment(transform, alignmentName, "C3D source frame");
        viewModel.SelectedEntity = "C3D Alignment";
        viewModel.ViewerStatus = $"Smoke alignment: {mode}";
    }

}

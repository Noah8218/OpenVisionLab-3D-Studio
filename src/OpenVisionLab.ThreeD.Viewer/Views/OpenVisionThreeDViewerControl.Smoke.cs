using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Automation;
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
    // The adapter owns only access to this control's native operations. CLI
    // interpretation, ordering and completion state belong to the scenario runner.
    private sealed class SmokeViewAdapter(OpenVisionThreeDViewerControl viewer) : IViewerSmokeHost
    {
        public bool IsDisposed => viewer.IsDisposed;
        public bool IsDispatcherStopping => viewer.Dispatcher.HasShutdownStarted || viewer.Dispatcher.HasShutdownFinished;
        public bool C3DSampleVisible => viewer.viewModel.C3DSampleVisible;
        public bool GlbSampleVisible => viewer.viewModel.GlbSampleVisible;
        public string GlbSampleSourcePath => viewer.viewModel.GlbSampleSourcePath;
        public bool LazSampleVisible => viewer.viewModel.LazSampleVisible;
        public bool RoiStepMeasurementVisible => viewer.viewModel.RoiStepMeasurementVisible;
        public NominalActualComparisonState NominalActualState => viewer.viewModel.NominalActual.State;
        public double NominalActualLowerTolerance => viewer.viewModel.NominalActual.LowerTolerance;
        public double NominalActualUpperTolerance => viewer.viewModel.NominalActual.UpperTolerance;
        public double TwoPointDistance => viewer.viewModel.TwoPointDistance;
        public double TwoPointRawHeightDelta => viewer.viewModel.TwoPointRawHeightDelta;
        public double ViewportFps => viewer.viewModel.ViewportFps;
        public double ViewportDrawMilliseconds => viewer.viewModel.ViewportDrawMilliseconds;
        public double RecipeTransformTranslateX { get => viewer.viewModel.RecipeTransformTranslateX; set => viewer.viewModel.RecipeTransformTranslateX = value; }
        public double RecipeTransformTranslateY { get => viewer.viewModel.RecipeTransformTranslateY; set => viewer.viewModel.RecipeTransformTranslateY = value; }
        public double RecipeRoiLeftCenterX { get => viewer.viewModel.RecipeRoiLeftCenterX; set => viewer.viewModel.RecipeRoiLeftCenterX = value; }
        public double RecipeRoiLeftCenterZ { get => viewer.viewModel.RecipeRoiLeftCenterZ; set => viewer.viewModel.RecipeRoiLeftCenterZ = value; }
        public double RecipeRoiLeftHalfWidth { get => viewer.viewModel.RecipeRoiLeftHalfWidth; set => viewer.viewModel.RecipeRoiLeftHalfWidth = value; }
        public double RecipeRoiRightCenterX { get => viewer.viewModel.RecipeRoiRightCenterX; set => viewer.viewModel.RecipeRoiRightCenterX = value; }
        public double RecipeRoiRightCenterZ { get => viewer.viewModel.RecipeRoiRightCenterZ; set => viewer.viewModel.RecipeRoiRightCenterZ = value; }
        public double RecipeRoiRightHalfDepth { get => viewer.viewModel.RecipeRoiRightHalfDepth; set => viewer.viewModel.RecipeRoiRightHalfDepth = value; }
        public double RecipePeakTolerance { get => viewer.viewModel.RecipePeakTolerance; set => viewer.viewModel.RecipePeakTolerance = value; }
        public double PlaneFlatnessTolerance { get => viewer.viewModel.PlaneFlatnessTolerance; set => viewer.viewModel.PlaneFlatnessTolerance = value; }
        public double LazTwoPointExpectedDistance { get => viewer.viewModel.LazTwoPointExpectedDistance; set => viewer.viewModel.LazTwoPointExpectedDistance = value; }
        public double LazTwoPointExpectedHeightDelta { get => viewer.viewModel.LazTwoPointExpectedHeightDelta; set => viewer.viewModel.LazTwoPointExpectedHeightDelta = value; }
        public double LazTwoPointDistanceTolerance { get => viewer.viewModel.LazTwoPointDistanceTolerance; set => viewer.viewModel.LazTwoPointDistanceTolerance = value; }
        public double LazTwoPointHeightDeltaTolerance { get => viewer.viewModel.LazTwoPointHeightDeltaTolerance; set => viewer.viewModel.LazTwoPointHeightDeltaTolerance = value; }
        public string SelectedColorMode { get => viewer.viewModel.SelectedColorMode; set => viewer.viewModel.SelectedColorMode = value; }
        public string SelectedGeometryStyle { get => viewer.viewModel.Display.SelectedGeometryStyle; set => viewer.viewModel.Display.SelectedGeometryStyle = value; }
        public string SelectedRenderDensity { get => viewer.viewModel.SelectedRenderDensity; set => viewer.viewModel.SelectedRenderDensity = value; }
        public double PointSize { get => viewer.viewModel.PointSize; set => viewer.viewModel.PointSize = value; }
        public bool HudDetailsVisible { get => viewer.viewModel.HudDetailsVisible; set => viewer.viewModel.HudDetailsVisible = value; }
        public string SelectedEntity { get => viewer.viewModel.SelectedEntity; set => viewer.viewModel.SelectedEntity = value; }
        public string ViewerStatus { get => viewer.viewModel.ViewerStatus; set => viewer.viewModel.ViewerStatus = value; }
        public bool HasLazPointCloud => viewer.lazPointCloud is not null;
        public bool HasImportedMesh => viewer.importedMesh is not null;
        public bool HasLazTwoPointMeasurement => viewer.lazTwoPointFirst is not null && viewer.lazTwoPointSecond is not null;
        public int ImportedMeshTextureUploads => viewer.importedMeshTextureState.UploadCount;
        public int ImportedMeshTextureReleases => viewer.importedMeshTextureState.ReleaseCount;

        public void LoadSource(ViewerSmokeSource source, string? path)
        {
            switch (source)
            {
                case ViewerSmokeSource.C3D: viewer.ApplySmokeC3D(); break;
                case ViewerSmokeSource.Glb: viewer.ApplySmokeGlb(path); break;
                case ViewerSmokeSource.Stl: viewer.ApplySmokeStl(path); break;
                case ViewerSmokeSource.LazMetadata: viewer.ApplySmokeLaz(path); break;
                case ViewerSmokeSource.LazPoints: viewer.ApplySmokeLazPoints(path); break;
            }
        }

        public void Measure(ViewerSmokeMeasurement measurement)
        {
            switch (measurement)
            {
                case ViewerSmokeMeasurement.PointPairDimensions: viewer.ApplySmokePointPairDimensions(); break;
                case ViewerSmokeMeasurement.LazTwoPoint: viewer.ApplySmokeLazTwoPointMeasurement(); break;
                case ViewerSmokeMeasurement.MeshTwoPoint: viewer.ApplySmokeImportedMeshTwoPointMeasurement(); break;
                case ViewerSmokeMeasurement.C3DTwoPoint: viewer.ApplySmokeTwoPointMeasurement(); break;
                case ViewerSmokeMeasurement.RoiStep: viewer.roiEditingSession.ApplySmokeRoiStepMeasurement(); break;
                case ViewerSmokeMeasurement.InteractiveRoiStep: viewer.roiEditingSession.ApplySmokeInteractiveRoiStepMeasurement(); break;
                case ViewerSmokeMeasurement.PlaneReference: viewer.ApplySmokePlaneReferenceMeasurement(); break;
                case ViewerSmokeMeasurement.PlaneFlatness: viewer.ApplySmokePlaneFlatness(); break;
                case ViewerSmokeMeasurement.GapFlush: viewer.ApplySmokeGapFlush(); break;
                case ViewerSmokeMeasurement.Volume: viewer.ApplySmokeVolume(); break;
                case ViewerSmokeMeasurement.CrossSection: viewer.ApplySmokeCrossSection(); break;
            }
        }

        public bool LoadRecipe(string path) => viewer.ApplyRecipeFile(path, isSmoke: true);
        public bool SaveRecipe(string path) => viewer.SaveCurrentRecipe(path, isSmoke: true);
        public bool PublishPreview() => viewer.PublishCurrentPreviewResult();
        public void ApplyEditedRoiParameters() => viewer.roiEditingSession.ApplyEditedRoiStepParameters();
        public bool ValidateRoi(out string warning) => viewer.roiEditingSession.ValidateRecipeState(requireRoi: true, out warning);
        public bool AlignRoiReference() => viewer.ApplyRoiReferenceAlignment();
        public void ConfigureTeachingPointer(string[] args) => viewer.ApplyTeachingCapturePointerSmokeArguments(args);
        public void FitSelection() => viewer.viewModel.FitSelection();
        public void Pan(double deltaX, double deltaY, double deltaZ) => viewer.viewModel.Pan(deltaX, deltaY, deltaZ);
        public void UseSelectionSmokeScene(string mode) => viewer.viewModel.UseSelectionSmokeScene(mode);
        public void UsePointCloudSmokeScene() => viewer.viewModel.UsePointCloudSmokeScene();
        public void UseC3DHeightDeviationRuleSmokeScene() => viewer.viewModel.UseC3DHeightDeviationRuleSmokeScene();
        public void UseResultSmokeScene() => viewer.viewModel.UseResultSmokeScene();
        public void ConfigureNominalActualComparison(NominalActualComparisonInput input) => viewer.viewModel.ConfigureNominalActualComparison(input);
        public void PreviewNominalActual() => viewer.viewModel.NominalActual.PreviewCommand.Execute(null);
        public void ClearNominalActualComparison(string validationIssue) => viewer.viewModel.ClearNominalActualComparison(validationIssue);
        public void SetRecipeValidationSummary(string summary) => viewer.viewModel.SetRecipeValidationSummary(summary);
        public void SetC3DAlignment(ModelTransform transform, string alignmentName, string referenceName) => viewer.viewModel.SetC3DAlignment(transform, alignmentName, referenceName);
        public void Render() => viewer.RenderNow();
        public Task RenderAsync(bool atRenderPriority) => atRenderPriority
            ? viewer.Dispatcher.InvokeAsync(viewer.RenderNow, DispatcherPriority.Render).Task
            : viewer.Dispatcher.InvokeAsync(viewer.RenderNow).Task;
        public void ResetRenderPerformance() => viewer.ResetDrawPerformanceTelemetry();
        public void BeginInteractionLod()
        {
            viewer.BeginInteractionWireframeLod();
            viewer.interactionLodRestoreTimer?.Stop();
        }
        public Task ApplyDensityRaceAsync() => viewer.ApplyConfiguredSmokeLazDensityRaceAsync();
        public Task ApplyNextDensityAsync() => viewer.ApplyConfiguredSmokeNextDensityAsync();
        public Task ReloadLazPointCloudAsync() => viewer.ReloadCurrentLazPointCloudAsync();
        public bool ApplyPick() => viewer.ApplyConfiguredSmokePick();
        public Task<bool> RunPointerRegressionAsync() => viewer.RunConfiguredPointerInputRegressionAsync();
        public void WriteSceneContracts(string path) => viewer.WriteSceneContracts(path);
        public Task<bool> CaptureScreenshotAsync(string path, string? qualityReportPath) => viewer.CaptureSmokeViewWithRetryAsync(path, qualityReportPath);
        public void Shutdown(int exitCode, bool requireApplication)
        {
            Environment.ExitCode = exitCode;
            var ownerWindow = Window.GetWindow(viewer);
            if (ownerWindow is null)
            {
                if (requireApplication)
                {
                    throw new InvalidOperationException(
                        "Viewer smoke requires an owner Window to complete application shutdown.");
                }

                return;
            }

            ownerWindow.Close();
        }
    }

    private async Task<bool> CaptureSmokeViewWithRetryAsync(string path, string? qualityReportPath)
    {
        if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return false;
        }

        const int maximumAttempts = 3;
        var fullPath = Path.GetFullPath(path);
        var qualityLines = new List<string>();
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var previousRejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            if (File.Exists(previousRejectedPath))
            {
                File.Delete(previousRejectedPath);
            }
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return false;
            }

            RenderNow();
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return false;
            }

            var result = WpfScreenshotCapture.Capture(this);
            var qualityLine = $"ViewerScreenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return false;
                }

                WpfScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"ViewerScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteScreenshotQualityReport(qualityReportPath, qualityLines);
                viewModel.LastScreenshotPath = fullPath;
                viewModel.ViewerStatus = "Screenshot captured";
                return true;
            }

            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return false;
            }

            WpfScreenshotCapture.Save(result.Bitmap, GetRejectedScreenshotPath(fullPath, attempt));
            await Task.Delay(250, viewerLifetimeToken);
        }

        qualityLines.Add($"ViewerScreenshotResult|accepted=False|attempts={maximumAttempts}|screenshot={fullPath}");
        WriteScreenshotQualityReport(qualityReportPath, qualityLines);
        return false;
    }

    private void ResetDrawPerformanceTelemetry()
    {
        interactionTelemetry.ResetRenderPerformance();
        viewModel.ResetRenderPerformance();
    }

    private static void WriteScreenshotQualityReport(string? path, IReadOnlyList<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, lines);
    }

    private static string GetRejectedScreenshotPath(string fullPath, int attempt) =>
        Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $"{Path.GetFileNameWithoutExtension(fullPath)}.rejected-attempt-{attempt}{Path.GetExtension(fullPath)}");

    public void EnableSmokeFromCommandLine() => EnableSmokeFromCommandLine(ownsApplicationLifecycle: true);

    public void EnableSmokeFromCommandLine(bool ownsApplicationLifecycle)
    {
        if (IsDisposed)
        {
            return;
        }

        smokeScenario.Configure(Environment.GetCommandLineArgs());
        if (ownsApplicationLifecycle && smokeScenario.ScreenshotPath is not null)
        {
            Loaded -= SmokeCaptureOnLoaded;
            Loaded += SmokeCaptureOnLoaded;
        }
    }

    private void SmokeCaptureOnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SmokeCaptureOnLoaded;
        smokeScenario.Start();
    }

    public bool HasConfiguredSmokeScreenshot => smokeScenario.ScreenshotPath is not null;

    public Task<bool> CaptureConfiguredSmokeViewAsync() => smokeScenario.CaptureConfiguredSmokeViewAsync();

    public bool ApplyConfiguredSmokePick()
    {
        if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return false;
        }

        switch (smokeScenario.PickTarget)
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
                SetSmokeFailure($"Unsupported smoke pick target: {smokeScenario.PickTarget}");
                break;
        }

        RenderNow();
        return smokeScenario.ExitCode == 0;
    }

    public async Task<bool> ApplyConfiguredSmokeNextDensityAsync()
    {
        if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return false;
        }

        if (smokeScenario.NextRenderDensity is null)
        {
            return true;
        }

        if (!viewModel.RenderDensityModes.Contains(smokeScenario.NextRenderDensity, StringComparer.Ordinal))
        {
            SetSmokeFailure($"Unsupported next Preview density: {smokeScenario.NextRenderDensity}");
            return false;
        }

        if (lazPointCloud is not null)
        {
            if (viewModel.SelectedRenderDensity == smokeScenario.NextRenderDensity)
            {
                await lazPointCloudLoadTelemetry.ReloadTask;
                return smokeScenario.ExitCode == 0;
            }

            lazPointCloudLoadTelemetry.SetDensityReloadSuppressed(true);
            try
            {
                viewModel.SelectedRenderDensity = smokeScenario.NextRenderDensity;
            }
            finally
            {
                lazPointCloudLoadTelemetry.SetDensityReloadSuppressed(false);
            }

            var reload = lazPointCloudLoadTelemetry.RecordSmokeReload(ReloadCurrentLazPointCloudAsync);
            await reload;
            return smokeScenario.ExitCode == 0;
        }

        if (viewModel.NominalActual.PreviewResult is null)
        {
            SetSmokeFailure("Next Preview density smoke requires a completed nominal/actual result");
            return false;
        }

        viewModel.SelectedRenderDensity = smokeScenario.NextRenderDensity;
        RenderNow();
        return smokeScenario.ExitCode == 0;
    }

    private async Task<bool> ApplyConfiguredSmokeLazDensityRaceAsync()
    {
        if (!smokeScenario.RaceLazDensityLoads)
        {
            return true;
        }

        if (lazPointCloud is null)
        {
            SetSmokeFailure("LAZ/LAS density race requires a loaded point cloud.");
            return false;
        }

        const string cancelledDensity = "Detailed";
        const string finalDensity = "Balanced";
        viewModel.SelectedRenderDensity = cancelledDensity;
        var cancelledLoad = lazPointCloudLoadTelemetry.ReloadTask;
        viewModel.SelectedRenderDensity = finalDensity;
        var finalLoad = lazPointCloudLoadTelemetry.ReloadTask;
        await Task.WhenAll(cancelledLoad, finalLoad);

        if (lazPointCloudLoadTelemetry.CancellationCount < 1
            || viewModel.SelectedRenderDensity != finalDensity
            || lazPointCloud.SampledPointView.Count > viewModel.LazMaxSampledPoints)
        {
            SetSmokeFailure("LAZ/LAS density race did not cancel the superseded load or retain the latest budget.");
            return false;
        }

        return smokeScenario.ExitCode == 0;
    }

    public async Task<bool> RunConfiguredPointerInputRegressionAsync()
    {
        if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return false;
        }

        if (smokeScenario.PointerInputReportPath is null)
        {
            return true;
        }

        pointerInputRegressionResult = await RunPointerInputRegressionAsync();
        if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return false;
        }

        WritePointerInputRegressionReport(smokeScenario.PointerInputReportPath, pointerInputRegressionResult);
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
        // Establish the loaded source's GPU baseline before pointer activity so
        // the regression measures interaction uploads rather than first-frame setup.
        await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
        var initialCamera = CaptureCameraSnapshot();
        var orbitCamera = initialCamera;
        var panCamera = initialCamera;
        var rightPanCamera = initialCamera;
        var zoomCamera = initialCamera;
        var doubleClickFitCamera = initialCamera;
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
        var doubleClickFitPassed = false;
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
        var initialC3DSource = c3dSample;
        var initialC3DGeometryStyle = viewModel.Display.EffectiveGeometryStyle;
        var initialC3DSourceApplyCount = c3dSourceApplyCount;
        var initialInteractionLodActivationCount = interactionLodActivationCount;
        var initialInteractionLodMediumTransitionCount = interactionLodMediumTransitionCount;
        var initialInteractionLodRestoreCount = interactionLodRestoreCount;
        var initialC3DGpuUploadCount = c3dGpuTelemetry.UploadCount;

        interactionTelemetry.ResetPointerInput();

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
            await Task.Delay(250, viewerLifetimeToken);

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
            await Task.Delay(120, viewerLifetimeToken);
            WindowsPointerInput.LeftDown();
            leftPressed = true;
            await Task.Delay(100, viewerLifetimeToken);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Task.Delay(180, viewerLifetimeToken);

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
            await Task.Delay(100, viewerLifetimeToken);
            WindowsPointerInput.MoveTo(orbitEnd);
            await Task.Delay(180, viewerLifetimeToken);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Task.Delay(160, viewerLifetimeToken);
            orbitCamera = CaptureCameraSnapshot();
            orbitPassed = IsFinite(orbitCamera)
                && Math.Abs(orbitCamera.Yaw - initialCamera.Yaw) > 1.0
                && Math.Abs(orbitCamera.Pitch - initialCamera.Pitch) > 1.0;

            var panStart = Viewport.PointToScreen(new Point(viewportWidth * 0.82, viewportHeight * 0.70));
            var panEnd = Viewport.PointToScreen(new Point(viewportWidth * 0.70, viewportHeight * 0.62));
            await EnsurePointerInputTargetAsync(hostWindow, panStart);
            WindowsPointerInput.MiddleDown();
            middlePressed = true;
            await Task.Delay(100, viewerLifetimeToken);
            WindowsPointerInput.MoveTo(panEnd);
            await Task.Delay(180, viewerLifetimeToken);
            WindowsPointerInput.MiddleUp();
            middlePressed = false;
            await Task.Delay(160, viewerLifetimeToken);
            panCamera = CaptureCameraSnapshot();
            panPassed = IsFinite(panCamera) && TargetChanged(orbitCamera, panCamera);

            var rightPanStart = Viewport.PointToScreen(new Point(viewportWidth * 0.68, viewportHeight * 0.68));
            var rightPanEnd = Viewport.PointToScreen(new Point(viewportWidth * 0.57, viewportHeight * 0.58));
            await EnsurePointerInputTargetAsync(hostWindow, rightPanStart);
            WindowsPointerInput.RightDown();
            rightPressed = true;
            await Task.Delay(100, viewerLifetimeToken);
            WindowsPointerInput.MoveTo(rightPanEnd);
            await Task.Delay(180, viewerLifetimeToken);
            WindowsPointerInput.RightUp();
            rightPressed = false;
            await Task.Delay(160, viewerLifetimeToken);
            rightPanCamera = CaptureCameraSnapshot();
            rightPanPassed = IsFinite(rightPanCamera) && TargetChanged(panCamera, rightPanCamera);
            rightPanMenuSuppressed = await Dispatcher.InvokeAsync(
                () => Viewport.ContextMenu?.IsOpen != true,
                DispatcherPriority.Input);

            await EnsurePointerInputTargetAsync(hostWindow, center);
            WindowsPointerInput.Wheel(120);
            await Task.Delay(180, viewerLifetimeToken);
            zoomCamera = CaptureCameraSnapshot();
            zoomPassed = IsFinite(zoomCamera)
                && zoomCamera.Distance < rightPanCamera.Distance - 0.000001;

            await EnsurePointerInputTargetAsync(hostWindow, center);
            WindowsPointerInput.LeftDown();
            leftPressed = true;
            await Task.Delay(60, viewerLifetimeToken);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Task.Delay(80, viewerLifetimeToken);
            WindowsPointerInput.LeftDown();
            leftPressed = true;
            await Task.Delay(60, viewerLifetimeToken);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Task.Delay(180, viewerLifetimeToken);
            doubleClickFitCamera = CaptureCameraSnapshot();
            var expectedDoubleClickStatus = viewModel.C3DSampleVisible && c3dSample is not null
                ? "Double-click fit"
                : "Fit all visible entities";
            doubleClickFitPassed = IsFinite(doubleClickFitCamera)
                && viewModel.ViewerStatus.StartsWith(expectedDoubleClickStatus, StringComparison.Ordinal)
                && (TargetChanged(zoomCamera, doubleClickFitCamera)
                    || Math.Abs(zoomCamera.Distance - doubleClickFitCamera.Distance) > 0.000001);

            var contextMenuPoint = Viewport.PointToScreen(new Point(viewportWidth * 0.56, viewportHeight * 0.42));
            await EnsurePointerInputTargetAsync(hostWindow, contextMenuPoint);
            WindowsPointerInput.RightDown();
            rightPressed = true;
            await Task.Delay(100, viewerLifetimeToken);
            WindowsPointerInput.RightUp();
            rightPressed = false;
            await Task.Delay(180, viewerLifetimeToken);
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
            await Task.Delay(InteractionLodRestoreDelay + TimeSpan.FromMilliseconds(80), viewerLifetimeToken);
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
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

        var routedEventsPassed = interactionTelemetry.MouseDownCount >= 7
            && interactionTelemetry.MouseMoveCount >= 3
            && interactionTelemetry.MouseUpCount >= 7
            && interactionTelemetry.MouseWheelCount >= 1;
        var interactiveRenderPerformancePassed = interactionTelemetry.MouseMoveTimingCount >= 3
            && interactionTelemetry.NextFrameTimingCount >= 1
            && interactionTelemetry.ScheduledMouseMoveRenderCount >= 3
            && interactionTelemetry.ImmediateMouseMoveRenderCount == 0
            && interactionTelemetry.MouseMoveMaximumMilliseconds <= 33.34
            && interactionTelemetry.NextFrameMaximumMilliseconds <= 100.0;
        var interactionLodExpected = initialC3DSource is not null
            && string.Equals(initialC3DGeometryStyle, "Wireframe", StringComparison.Ordinal)
            && c3dRenderProxyCache.Current is { CoarseInteractionGridEdgeCount: > 0 } renderProxy
            && renderProxy.CoarseInteractionGridEdgeCount < renderProxy.InteractionGridEdgeCount
            && renderProxy.InteractionGridEdgeCount < renderProxy.GridEdgeCount;
        var c3dSourceReloadedDuringInteraction = !ReferenceEquals(initialC3DSource, c3dSample)
            || initialC3DSourceApplyCount != c3dSourceApplyCount;
        var interactionLodPassed = !interactionLodExpected
            || (interactionLodActivationCount > initialInteractionLodActivationCount
                && interactionLodMediumTransitionCount > initialInteractionLodMediumTransitionCount
                && interactionLodRestoreCount > initialInteractionLodRestoreCount
                && c3dGpuBuffersAvailable
                && c3dGpuTelemetry.UploadCount == initialC3DGpuUploadCount
                && !interactionWireframeLodActive
                && !c3dSourceReloadedDuringInteraction);
        var passed = pickPassed
            && orbitPassed
            && panPassed
            && rightPanPassed
            && rightPanMenuSuppressed
            && zoomPassed
            && doubleClickFitPassed
            && contextMenuPassed
            && contextMenuBindingsPassed
            && topViewMenuBindingsPassed
            && routedEventsPassed
            && interactiveRenderPerformancePassed
            && interactionLodPassed;
        if (!passed && string.IsNullOrWhiteSpace(failure))
        {
            failure = CreatePointerInputFailureSummary(
                pickPassed,
                orbitPassed,
                panPassed,
                rightPanPassed,
                rightPanMenuSuppressed,
                zoomPassed,
                doubleClickFitPassed,
                contextMenuPassed,
                contextMenuBindingsPassed,
                topViewMenuBindingsPassed,
                routedEventsPassed,
                interactiveRenderPerformancePassed,
                interactionLodPassed);
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
            doubleClickFitPassed,
            contextMenuPassed,
            contextMenuBindingsPassed,
            contextMenuCommandCount,
            topViewMenuBindingsPassed,
            topViewMenuCommandCount,
            routedEventsPassed,
            interactionTelemetry.MouseDownCount,
            interactionTelemetry.MouseMoveCount,
            interactionTelemetry.MouseUpCount,
            interactionTelemetry.MouseWheelCount,
            interactionTelemetry.MouseMoveTimingCount,
            interactionTelemetry.MouseMoveTimingCount == 0 ? 0.0 : interactionTelemetry.MouseMoveTotalMilliseconds / interactionTelemetry.MouseMoveTimingCount,
            interactionTelemetry.MouseMoveMaximumMilliseconds,
            interactionTelemetry.NextFrameTimingCount,
            interactionTelemetry.NextFrameTimingCount == 0 ? 0.0 : interactionTelemetry.NextFrameTotalMilliseconds / interactionTelemetry.NextFrameTimingCount,
            interactionTelemetry.NextFrameMaximumMilliseconds,
            interactionTelemetry.ScheduledMouseMoveRenderCount,
            interactionTelemetry.ImmediateMouseMoveRenderCount,
            interactiveRenderPerformancePassed,
            interactionLodExpected,
            interactionLodPassed,
            interactionLodActivationCount - initialInteractionLodActivationCount,
            interactionLodMediumTransitionCount - initialInteractionLodMediumTransitionCount,
            interactionLodRestoreCount - initialInteractionLodRestoreCount,
            c3dRenderProxyCache.Current?.InteractionGridEdgeCount ?? 0,
            c3dRenderProxyCache.Current?.CoarseInteractionGridEdgeCount ?? 0,
            c3dGpuTelemetry.UploadCount - initialC3DGpuUploadCount,
            c3dGpuBuffersAvailable,
            c3dSourceReloadedDuringInteraction,
            viewModel.C3DSampleVisible,
            c3dRenderProxyCache.Current?.Points.Length ?? 0,
            c3dDisplayListBuildCount,
            lastC3DDisplayListBuildMilliseconds,
            viewportWidth,
            viewportHeight,
            initialCamera,
            orbitCamera,
            panCamera,
            rightPanCamera,
            zoomCamera,
            doubleClickFitCamera,
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
            await Task.Delay(120, viewerLifetimeToken);
            if (WindowsPointerInput.IsScreenPointOverWindow(hostWindow, screenPoint, out var diagnostic))
            {
                return;
            }

            diagnostics.Add($"attempt={attempt}|active={hostWindow.IsActive}|{diagnostic}");
            await Task.Delay(120, viewerLifetimeToken);
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
        bool doubleClickFitPassed,
        bool contextMenuPassed,
        bool contextMenuBindingsPassed,
        bool topViewMenuBindingsPassed,
        bool routedEventsPassed,
        bool interactiveRenderPerformancePassed,
        bool interactionLodPassed)
    {
        var failures = new List<string>();
        if (!pickPassed) failures.Add("pick state did not change");
        if (!orbitPassed) failures.Add("orbit camera did not change");
        if (!panPassed) failures.Add("middle-button pan target did not change");
        if (!rightPanPassed) failures.Add("right-drag pan target did not change");
        if (!rightPanMenuSuppressed) failures.Add("right-drag opened the Viewer context menu");
        if (!zoomPassed) failures.Add("zoom distance did not change");
        if (!doubleClickFitPassed) failures.Add("double-click did not fit the active Viewer scene");
        if (!contextMenuPassed) failures.Add("Viewer context menu did not open");
        if (!contextMenuBindingsPassed) failures.Add("Viewer context menu command bindings were incomplete");
        if (!topViewMenuBindingsPassed) failures.Add("Viewer top View menu command bindings were incomplete");
        if (!routedEventsPassed) failures.Add("WPF mouse event counts were incomplete");
        if (!interactiveRenderPerformancePassed) failures.Add("pointer render coalescing or latency threshold failed");
        if (!interactionLodPassed) failures.Add("interaction wireframe LOD did not activate, restore, cache, or preserve the loaded source");
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
            viewModel.FitRoiCommand,
            viewModel.FitSelectionCommand,
            viewModel.TopViewCommand,
            viewModel.PerspectiveViewCommand,
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
            viewModel.FitRoiCommand,
            viewModel.FitSelectionCommand,
            viewModel.TopViewCommand,
            viewModel.PerspectiveViewCommand,
            viewModel.ResetCommand,
            viewModel.ScreenshotCommand,
            viewModel.ProfileCommand
        };
        var overlayBoundary = InspectGridRectangleOverlayMenuBoundary();
        return (
            commands.Length == expected.Length
            && expected.All(expectedCommand => commands.Any(command => ReferenceEquals(command, expectedCommand)))
            && overlayBoundary.Passed,
            commands.Length);
    }

    private (bool Passed, string Header, string AutomationName) InspectGridRectangleOverlayMenuBoundary()
    {
        var item = ViewerViewMenuRoot.Items
            .OfType<MenuItem>()
            .FirstOrDefault(candidate => string.Equals(
                AutomationProperties.GetAutomationId(candidate),
                "RoiOverlayYPositionMenu",
                StringComparison.Ordinal));
        if (item is null)
        {
            return (false, string.Empty, string.Empty);
        }

        var header = item.Header?.ToString() ?? string.Empty;
        var automationName = AutomationProperties.GetName(item) ?? string.Empty;
        var headerClear = header.Contains("Y", StringComparison.Ordinal)
            && (header.Contains("보기 전용", StringComparison.Ordinal)
                || header.Contains("view only", StringComparison.OrdinalIgnoreCase));
        var automationNameClear = automationName.Contains("Y", StringComparison.Ordinal)
            && (automationName.Contains("보기 전용", StringComparison.Ordinal)
                || automationName.Contains("view only", StringComparison.OrdinalIgnoreCase));
        return (headerClear && automationNameClear, header, automationName);
    }

    private void WritePointerInputRegressionReport(
        string path,
        PointerInputRegressionResult result)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var overlayBoundary = InspectGridRectangleOverlayMenuBoundary();
        var lines = new[]
        {
            "PointerInputRegression",
            $"Result|pass={result.Passed}|windowActivated={result.WindowActivated}|viewport={result.ViewportWidth:F0}x{result.ViewportHeight:F0}",
            $"RoutedEvents|pass={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}|mouseWheel={result.MouseWheelCount}",
            $"PointerLatency|mouseMoveSamples={result.MouseMoveTimingCount}|handlerAverageMs={result.AverageMouseMoveMilliseconds:F3}|handlerMaximumMs={result.MaximumMouseMoveMilliseconds:F3}|nextFrameSamples={result.NextFrameTimingCount}|nextFrameAverageMs={result.AverageNextFrameMilliseconds:F3}|nextFrameMaximumMs={result.MaximumNextFrameMilliseconds:F3}",
            $"InteractiveRender|pass={result.InteractiveRenderPerformancePassed}|scheduledMouseMoveRequests={result.ScheduledMouseMoveRenderCount}|immediateMouseMoveRenders={result.ImmediateMouseMoveRenderCount}|handlerLimitMs=33.340|nextFrameLimitMs=100.000",
            $"InteractionLod|expected={result.InteractionLodExpected}|pass={result.InteractionLodPassed}|activations={result.InteractionLodActivationCount}|mediumTransitions={result.InteractionLodMediumTransitionCount}|restores={result.InteractionLodRestoreCount}|coarseGridEdges={result.CoarseGridEdgeCount}|mediumGridEdges={result.MediumGridEdgeCount}|preciseGridEdges={c3dRenderProxyCache.Current?.GridEdgeCount ?? 0}|gpuUploadDelta={result.InteractionGpuUploadDelta}|gpuBufferReady={result.GpuBufferReady}|sourceReloaded={result.C3DSourceReloadedDuringInteraction}|stepDelayMs={InteractionLodStepDelay.TotalMilliseconds:F0}|restoreDelayMs={InteractionLodRestoreDelay.TotalMilliseconds:F0}",
            $"C3DRender|active={result.C3DSceneActive}|points={result.C3DRenderedPointCount}|gpuBufferReady={result.GpuBufferReady}|gpuUploads={c3dGpuTelemetry.UploadCount}|gpuDraws={c3dGpuTelemetry.DrawCount}|gpuBytes={c3dGpuTelemetry.UploadedBytes}|fallbacks={c3dGpuTelemetry.FallbackCount}|renderCacheBuilds={result.C3DDisplayListBuildCount}|lastRenderCacheBuildMs={result.LastC3DDisplayListBuildMilliseconds:F3}|scheduledFps=60",
            $"Pick|pass={result.PickPassed}|entity={result.PickedEntity}|coordinate={result.PickCoordinate}|summary={result.SelectionSummary}",
            $"Orbit|pass={result.OrbitPassed}|before={FormatCameraSnapshot(result.InitialCamera)}|after={FormatCameraSnapshot(result.OrbitCamera)}",
            $"Pan|pass={result.PanPassed}|before={FormatCameraSnapshot(result.OrbitCamera)}|after={FormatCameraSnapshot(result.PanCamera)}",
            $"RightDragPan|pass={result.RightPanPassed && result.RightPanMenuSuppressed}|targetChanged={result.RightPanPassed}|menuSuppressed={result.RightPanMenuSuppressed}|before={FormatCameraSnapshot(result.PanCamera)}|after={FormatCameraSnapshot(result.RightPanCamera)}",
            $"Zoom|pass={result.ZoomPassed}|before={FormatCameraSnapshot(result.RightPanCamera)}|after={FormatCameraSnapshot(result.ZoomCamera)}",
            $"DoubleClickFit|pass={result.DoubleClickFitPassed}|before={FormatCameraSnapshot(result.ZoomCamera)}|after={FormatCameraSnapshot(result.DoubleClickFitCamera)}",
            $"ContextMenu|pass={result.ContextMenuPassed}",
            $"InputModes|rightDragPan={result.RightPanPassed && result.RightPanMenuSuppressed}|shortRightClick={result.ContextMenuPassed}",
            $"ContextMenuBindings|pass={result.ContextMenuBindingsPassed}|commands={result.ContextMenuCommandCount}/8",
            $"TopViewMenuBindings|pass={result.TopViewMenuBindingsPassed}|commands={result.TopViewMenuCommandCount}/8",
            $"GridRectangleOverlayBoundary|pass={overlayBoundary.Passed}|header={CleanContractText(overlayBoundary.Header)}|automationName={CleanContractText(overlayBoundary.AutomationName)}",
            $"Failure|summary={result.Failure}"
        };
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
    }

    private static string FormatCameraSnapshot(CameraSnapshot camera) => string.Create(
        CultureInfo.InvariantCulture,
        $"yaw:{camera.Yaw:R},pitch:{camera.Pitch:R},distance:{camera.Distance:R},target:({camera.TargetX:R},{camera.TargetY:R},{camera.TargetZ:R})");

    private string CreatePointerInputRegressionContractLine()
    {
        if (smokeScenario.PointerInputReportPath is null)
        {
            return "PointerInputRegression|configured=False";
        }

        if (pointerInputRegressionResult is null)
        {
            return "PointerInputRegression|configured=True|pass=False|failure=not-run";
        }

        var result = pointerInputRegressionResult;
        return $"PointerInputRegression|configured=True|pass={result.Passed}|pick={result.PickPassed}|orbit={result.OrbitPassed}|pan={result.PanPassed}|middlePan={result.PanPassed}|rightDragPan={result.RightPanPassed && result.RightPanMenuSuppressed}|rightPanMenuSuppressed={result.RightPanMenuSuppressed}|zoom={result.ZoomPassed}|doubleClickFit={result.DoubleClickFitPassed}|shortRightClick={result.ContextMenuPassed}|contextMenu={result.ContextMenuPassed}|contextMenuBindings={result.ContextMenuBindingsPassed}|contextMenuCommands={result.ContextMenuCommandCount}/8|topViewMenuBindings={result.TopViewMenuBindingsPassed}|topViewMenuCommands={result.TopViewMenuCommandCount}/8|routedEvents={result.RoutedEventsPassed}|mouseDown={result.MouseDownCount}|mouseMove={result.MouseMoveCount}|mouseUp={result.MouseUpCount}|mouseWheel={result.MouseWheelCount}|mouseMoveHandlerAverageMs={result.AverageMouseMoveMilliseconds:F3}|mouseMoveHandlerMaximumMs={result.MaximumMouseMoveMilliseconds:F3}|nextFrameAverageMs={result.AverageNextFrameMilliseconds:F3}|nextFrameMaximumMs={result.MaximumNextFrameMilliseconds:F3}|scheduledMouseMoveRequests={result.ScheduledMouseMoveRenderCount}|immediateMouseMoveRenders={result.ImmediateMouseMoveRenderCount}|interactiveRenderPass={result.InteractiveRenderPerformancePassed}|interactionLodExpected={result.InteractionLodExpected}|interactionLodPass={result.InteractionLodPassed}|interactionLodActivations={result.InteractionLodActivationCount}|interactionLodMediumTransitions={result.InteractionLodMediumTransitionCount}|interactionLodRestores={result.InteractionLodRestoreCount}|coarseGridEdges={result.CoarseGridEdgeCount}|mediumGridEdges={result.MediumGridEdgeCount}|gpuUploadDelta={result.InteractionGpuUploadDelta}|gpuBufferReady={result.GpuBufferReady}|sourceReloadedDuringInteraction={result.C3DSourceReloadedDuringInteraction}|c3dActive={result.C3DSceneActive}|c3dPoints={result.C3DRenderedPointCount}|renderCacheBuilds={result.C3DDisplayListBuildCount}|lastRenderCacheBuildMs={result.LastC3DDisplayListBuildMilliseconds:F3}|windowActivated={result.WindowActivated}|viewport={result.ViewportWidth:F0}x{result.ViewportHeight:F0}|failure={CleanContractText(result.Failure)}";
    }

}

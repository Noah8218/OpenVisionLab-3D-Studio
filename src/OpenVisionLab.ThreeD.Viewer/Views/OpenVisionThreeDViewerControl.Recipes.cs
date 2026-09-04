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
using OpenVisionLab.ThreeD.Viewer.Loading;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Recipes;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Tools;
using SharpGL;
using SharpGL.WPF;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private void HandleFitAllCommand()
    {
        var fitted = viewModel.IsTopOrthographicView
            ? TryFitCurrentC3DOrthographic("Top view fitted to all C3D data")
            : TryFitCurrentC3D(useTopInspectionView: false, "Fit all C3D height grid");
        if (!fitted)
        {
            viewModel.FitAll();
        }

        RenderNow();
    }

    private void HandleTopViewCommand()
    {
        if (TryFitCurrentC3DOrthographic(
                "Top orthographic view · X=column, Z=row · view only"))
        {
            RenderNow();
        }
    }

    private void HandlePerspectiveViewCommand()
    {
        viewModel.RestorePerspectiveView(
            "Perspective view restored · recipe and inspection unchanged");
        RenderNow();
    }

    private void HandleFitRoiCommand()
    {
        if (!TryGetVisibleTeachingGridRectangle(out var selectionId, out var rectangle))
        {
            viewModel.ViewerStatus = "Fit ROI requires a selected Reference or Measurement ROI";
            return;
        }

        var lastRow = rectangle.Row + rectangle.RowCount - 1;
        var lastColumn = rectangle.Column + rectangle.ColumnCount - 1;
        var rawHeight = GetTeachingGridRectangleDisplayRawHeight(selectionId, rectangle);
        Vector3[] positions =
        [
            CreateC3DGridDisplayPosition(rectangle.Row, rectangle.Column, rawHeight),
            CreateC3DGridDisplayPosition(rectangle.Row, lastColumn, rawHeight),
            CreateC3DGridDisplayPosition(lastRow, lastColumn, rawHeight),
            CreateC3DGridDisplayPosition(lastRow, rectangle.Column, rawHeight)
        ];
        var aspect = Math.Max(1.0, Viewport.ActualWidth) / Math.Max(1.0, Viewport.ActualHeight);
        if (viewModel.IsTopOrthographicView)
        {
            var fit = CameraMath.FitOrthographicPositions(
                positions,
                yawDegrees: 0.0,
                pitchDegrees: 90.0,
                aspect,
                padding: 1.30);
            viewModel.ApplyTopOrthographicFit(
                fit.Target,
                fit.Height,
                fit.Distance,
                "Top view fitted to selected ROI · view only");
        }
        else
        {
            var fit = CameraMath.FitPositions(
                positions,
                viewModel.YawDegrees,
                viewModel.PitchDegrees,
                FieldOfViewDegrees,
                aspect,
                padding: 1.30);
            viewModel.ApplyC3DCameraFit(
                fit.Target,
                fit.Distance,
                useTopInspectionView: false,
                "Perspective view fitted to selected ROI · view only");
        }

        RenderNow();
    }

    private void HandleFitSelectionCommand()
    {
        if (viewModel.SelectedEntity != "C3D Height Grid"
            || !TryFitCurrentC3D(useTopInspectionView: false, "Fit selected C3D height grid"))
        {
            viewModel.FitSelection();
        }

        RenderNow();
    }

    private bool TryFitCurrentC3D(bool useTopInspectionView, string status)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            return false;
        }

        var renderProxy = GetC3DRenderProxy();
        var positions = GetC3DRenderPositions(renderProxy);
        var yaw = useTopInspectionView ? 0.0 : viewModel.YawDegrees;
        var pitch = useTopInspectionView ? 80.0 : viewModel.PitchDegrees;
        var fit = CameraMath.FitPositions(
            positions,
            yaw,
            pitch,
            FieldOfViewDegrees,
            Math.Max(1.0, Viewport.ActualWidth) / Math.Max(1.0, Viewport.ActualHeight));
        viewModel.ApplyC3DCameraFit(fit.Target, fit.Distance, useTopInspectionView, status);
        return true;
    }

    private bool TryFitCurrentC3DOrthographic(string status)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            return false;
        }

        var renderProxy = GetC3DRenderProxy();
        var positions = GetC3DRenderPositions(renderProxy);
        var fit = CameraMath.FitOrthographicPositions(
            positions,
            yawDegrees: 0.0,
            pitchDegrees: 90.0,
            Math.Max(1.0, Viewport.ActualWidth) / Math.Max(1.0, Viewport.ActualHeight));
        viewModel.ApplyTopOrthographicFit(fit.Target, fit.Height, fit.Distance, status);
        return true;
    }

    private void HandleResetCommand()
    {
        viewModel.RecipeOutputEnabled = true;
        viewModel.Reset();
        RenderNow();
    }

    private void HandleScreenshotCommand()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "artifacts", $"sharpgl_viewer_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        CaptureWindow(path);
    }

    private async void HandleOpenRecipeCommand()
    {
        if (IsDisposed)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Open 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            try
            {
                await ApplyRecipeFileAsync(dialog.FileName, isSmoke: false);
                if (!IsDisposed && !viewerLifetimeToken.IsCancellationRequested)
                {
                    RenderNow();
                }
            }
            catch (OperationCanceledException) when (
                IsDisposed
                || viewerLifetimeToken.IsCancellationRequested)
            {
                // The control lifetime owns the open request after the dialog closes.
            }
        }
    }

    private void HandleSaveRecipeCommand()
    {
        SaveCurrentRecipeWithDialog();
    }

    private void HandleApplyRoiAlignmentCommand()
    {
        ApplyRoiReferenceAlignment();
    }

    public void SaveCurrentRecipeWithDialog()
    {
        var savePlan = ResolveCurrentRecipeSavePlan();
        var dialog = new SaveFileDialog
        {
            Title = "Save 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            FileName = savePlan.DefaultFileName,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            SaveCurrentRecipe(dialog.FileName, isSmoke: false);
        }
    }

    public bool SaveCurrentRecipe(string path, bool isSmoke)
    {
        var savePlan = ResolveCurrentRecipeSavePlan();
        return savePlan.Route switch
        {
            ViewerRecipeSaveRoute.NominalActual => SaveCurrentNominalActualRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.LazTwoPoint => SaveCurrentLazTwoPointRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.Warpage => SaveCurrentWarpageRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.Thickness => SaveCurrentThicknessRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.GapFlush => SaveCurrentGapFlushRecipe(path, isSmoke),
            ViewerRecipeSaveRoute.PointPairDimensions => SaveCurrentPointPairDimensionsRecipe(path, isSmoke),
            _ => SaveCurrentHeightDeviationRecipe(path, isSmoke)
        };
    }

    private ViewerRecipeSavePlan ResolveCurrentRecipeSavePlan() =>
        ViewerRecipeSavePlan.Resolve(
            ShouldSaveCurrentNominalActualRecipe(),
            ShouldSaveCurrentLazTwoPointRecipe(),
            ShouldSaveCurrentWarpageRecipe(),
            C3DThicknessRecipeSaveCoordinator.CanSave(c3dSample, viewModel),
            ShouldSaveCurrentGapFlushRecipe(),
            C3DPointPairDimensionsRecipeSaveCoordinator.CanSave(c3dSample, viewModel));

    private bool ShouldSaveCurrentNominalActualRecipe() =>
        viewModel.NominalActualInput is not null
        && (!viewModel.RecipeOutputEnabled
            || (viewModel.NominalActual.PreviewResult is not null
                && viewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
                    or NominalActualComparisonState.Published));

    private bool ShouldSaveCurrentLazTwoPointRecipe() =>
        lazPointCloud is not null
        && (!viewModel.RecipeOutputEnabled
            || (lazTwoPointFirst is not null
                && lazTwoPointSecond is not null
                && viewModel.SelectedEntity.Contains("Two Point Measurement", StringComparison.OrdinalIgnoreCase)))
        && viewModel.LazSampleVisible;

    private bool EnsureRecipeOutputEnabled()
    {
        if (viewModel.RecipeOutputEnabled)
        {
            return true;
        }

        viewModel.ViewerStatus = "Recipe output is disabled; Preview and Publish did not run";
        return false;
    }

    public bool ApplyRoiReferenceAlignment()
    {
        if (!ValidateRecipeState(requireRoi: true, out var warning))
        {
            SetRecipeValidationWarning(warning);
            viewModel.ViewerStatus = warning;
            return false;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null)
        {
            viewModel.ViewerStatus = "ROI alignment requires a visible C3D height grid";
            return false;
        }

        if (!UpdateRoiStepMeasurement()
            || roiStepLeftBounds is not { } leftBounds
            || roiStepRightBounds is not { } rightBounds
            || roiStepLeftCenter is not { } leftCenter
            || roiStepRightCenter is not { } rightCenter)
        {
            SetRecipeValidationWarning("Validation warning: ROI alignment requires valid left and right ROI regions.");
            viewModel.ViewerStatus = "ROI alignment requires left and right ROI regions";
            return false;
        }

        var referenceX = (leftCenter.X + rightCenter.X) * 0.5f;
        var referenceY = (leftCenter.Y + rightCenter.Y) * 0.5f;
        var referenceZ = (leftCenter.Z + rightCenter.Z) * 0.5f;
        var alignedLeft = OffsetRoiRegion(CreateRoiRegion(leftBounds), -referenceX, -referenceZ);
        var alignedRight = OffsetRoiRegion(CreateRoiRegion(rightBounds), -referenceX, -referenceZ);
        var current = viewModel.C3DModelTransform;
        var transform = current with
        {
            TranslateX = current.TranslateX - referenceX,
            TranslateY = current.TranslateY - referenceY,
            TranslateZ = current.TranslateZ - referenceZ
        };

        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = alignedLeft;
        roiStepRightRecipeRegion = alignedRight;
        roiStepLeftAnchor = new Vector3((float)alignedLeft.CenterX, 0.0f, (float)alignedLeft.CenterZ);
        roiStepRightAnchor = new Vector3((float)alignedRight.CenterX, 0.0f, (float)alignedRight.CenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;
        viewModel.SetC3DAlignment(transform, "ROI reference alignment", "ROI step centers");
        SyncRecipeRoiEditFromRegions("Interactive", alignedLeft, alignedRight, viewModel.RecipeRoiMaxSampledPoints);

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SetAlignmentWorkflowSummary(string.Create(
                CultureInfo.InvariantCulture,
                $"ROI alignment: ROI pair centered at origin; dT({-referenceX:F3}, {-referenceY:F3}, {-referenceZ:F3})"));
            SetRecipeValidationOk();
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "ROI alignment applied from selected regions";
            RenderNow();
            return true;
        }

        viewModel.ViewerStatus = "ROI alignment applied, but ROI measurement could not be recalculated";
        RenderNow();
        return false;
    }

    private void HandlePublishResultCommand()
    {
        viewModel.PublishPreviewResult();
        RenderNow();
    }

    private async void SmokeCaptureOnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SmokeCaptureOnLoaded;
        if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await Dispatcher.InvokeAsync(RenderNow);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokeReloadImportedMeshTexture)
            {
                var sourcePath = viewModel.GlbSampleSourcePath;
                ApplySmokeGlb(sourcePath);
                await Dispatcher.InvokeAsync(RenderNow);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }

                if (importedMeshTextureUploadCount < 2 || importedMeshTextureReleaseCount < 1)
                {
                    SetSmokeFailure(
                        $"Imported mesh texture reload failed: uploads={importedMeshTextureUploadCount}, releases={importedMeshTextureReleaseCount}");
                }
            }

            if (smokeNominalActualPreview
                && !await WaitForNominalActualPreviewAsync(TimeSpan.FromMinutes(10)))
            {
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }

                smokeExitCode = 1;
                if (viewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning)
                {
                    viewModel.ViewerStatus = "Nominal/actual Preview timed out before screenshot capture.";
                }

                await Dispatcher.InvokeAsync(RenderNow);
            }

            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokeRaceLazPointCloudDensityLoads)
            {
                await ApplyConfiguredSmokeLazDensityRaceAsync();
            }
            else
            {
                await ApplyConfiguredSmokeNextDensityAsync();
            }
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokeReloadLazPointCloudCache && lazPointCloud is not null)
            {
                await ReloadCurrentLazPointCloudAsync();
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }
            await Dispatcher.InvokeAsync(RenderNow);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            if (smokePickTarget is not null)
            {
                ApplyConfiguredSmokePick();
                await Dispatcher.InvokeAsync(RenderNow);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }

            if (smokePublishResult)
            {
                if (!PublishCurrentPreviewResult())
                {
                    smokeExitCode = 1;
                    viewModel.ViewerStatus = "Smoke Publish failed: current Preview evidence is unavailable";
                }

                await Dispatcher.InvokeAsync(RenderNow);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }

            if (smokeSaveRecipePath is not null)
            {
                if (!SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
                {
                    smokeExitCode = 1;
                }

                await Dispatcher.InvokeAsync(RenderNow);
                if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
                {
                    return;
                }
            }

            await RunConfiguredPointerInputRegressionAsync();
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }
            await Dispatcher.InvokeAsync(RenderNow);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            await Task.Delay(900, viewerLifetimeToken);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }
            await CaptureConfiguredSmokeViewAsync();
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            await Task.Delay(100, viewerLifetimeToken);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            Application.Current.Shutdown(smokeExitCode);
        }
        catch (OperationCanceledException) when (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
        {
            // Control disposal owns cancellation; do not continue smoke work or
            // shut down the host after the View has closed.
        }
        catch (InvalidOperationException) when (IsDisposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            // Dispatcher shutdown can race the final View callback during host close.
        }
    }

    private async Task<bool> WaitForNominalActualPreviewAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (viewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100, viewerLifetimeToken);
        }

        return viewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;
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

    private async Task RunConfiguredSmokeRenderFramesAsync()
    {
        if (smokeRenderFrameCount == 0)
        {
            return;
        }

        ResetDrawPerformanceTelemetry();
        smokeRenderFramesCompleted = 0;
        if (smokeInteractionLodRequested)
        {
            BeginInteractionWireframeLod();
            interactionLodRestoreTimer?.Stop();
        }

        for (var frame = 0; frame < smokeRenderFrameCount; frame++)
        {
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }

            smokeRenderFramesCompleted++;
        }

        if (smokeMeasureMode is not null)
        {
            ApplySmokeMeasure(smokeMeasureMode);
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            if (IsDisposed || viewerLifetimeToken.IsCancellationRequested)
            {
                return;
            }
        }

        if (!double.IsFinite(viewModel.ViewportFps)
            || !double.IsFinite(viewModel.ViewportDrawMilliseconds))
        {
            SetSmokeFailure(
                $"Render performance remained pending after {smokeRenderFramesCompleted} forced frames.");
        }
    }

    private void ResetDrawPerformanceTelemetry()
    {
        lastFrameTimestamp = 0;
        performanceFrameCount = 0;
        performanceDrawCount = 0;
        accumulatedFrameIntervalMilliseconds = 0.0;
        accumulatedDrawMilliseconds = 0.0;
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

    private void CaptureWindow(string path)
    {
        RenderNow();
        var result = WpfScreenshotCapture.Capture(this);
        WpfScreenshotCapture.Save(result.Bitmap, path);

        viewModel.LastScreenshotPath = Path.GetFullPath(path);
        viewModel.ViewerStatus = "Screenshot captured";
    }

    private void ApplySmokeAction(string action)
    {
        if (action.Equals("fit-selection", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.FitSelection();
        }
        else if (action.Equals("color-height", StringComparison.OrdinalIgnoreCase)
            || action.Equals("height-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Height";
        }
        else if (action.Equals("color-rgb", StringComparison.OrdinalIgnoreCase)
            || action.Equals("rgb-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "RGB";
        }
        else if (action.Equals("color-intensity", StringComparison.OrdinalIgnoreCase)
            || action.Equals("intensity-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Intensity";
        }
        else if (action.Equals("color-normal", StringComparison.OrdinalIgnoreCase)
            || action.Equals("normal-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Normal";
        }
        else if (action.Equals("color-solid", StringComparison.OrdinalIgnoreCase)
            || action.Equals("solid-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Solid";
        }
        else if (action.Equals("color-grayscale", StringComparison.OrdinalIgnoreCase)
            || action.Equals("grayscale-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Grayscale";
        }
        else if (action.Equals("color-thermal", StringComparison.OrdinalIgnoreCase)
            || action.Equals("thermal-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Thermal";
        }
        else if (action.Equals("color-deviation", StringComparison.OrdinalIgnoreCase)
            || action.Equals("deviation-color", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.SelectedColorMode = "Deviation";
        }
        else if (action.Equals("geometry-points", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Points";
        }
        else if (action.Equals("geometry-wireframe", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Wireframe";
        }
        else if (action.Equals("geometry-surface", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Surface";
        }
        else if (action.Equals("geometry-surface-edges", StringComparison.OrdinalIgnoreCase)
            || action.Equals("geometry-surface-with-edges", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Display.SelectedGeometryStyle = "Surface + Edges";
        }
        else if (action.Equals("pan", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.Pan(-0.75, 0.35, 0.0);
        }
    }

    private void ApplySmokeSelection(string mode)
    {
        var selectionMode = mode.ToLowerInvariant() switch
        {
            "box" or "box-roi" => "Box ROI",
            "roi" or "roi-step" or "step-height" or "roi-interactive" or "interactive-roi" => RoiStepSelectionMode,
            "section" or "section-plane" => "Section Plane",
            "two-point" or "distance" or "distance-height" => TwoPointSelectionMode,
            _ => "Point"
        };

        if (selectionMode == TwoPointSelectionMode)
        {
            if (viewModel.LazSampleVisible && lazPointCloud is not null)
            {
                ApplySmokeLazTwoPointMeasurement();
            }
            else if (viewModel.GlbSampleVisible && importedMesh is not null)
            {
                ApplySmokeImportedMeshTwoPointMeasurement();
            }
            else
            {
                ApplySmokeTwoPointMeasurement();
            }

            return;
        }

        if (selectionMode == RoiStepSelectionMode)
        {
            if (mode.Equals("roi-interactive", StringComparison.OrdinalIgnoreCase)
                || mode.Equals("interactive-roi", StringComparison.OrdinalIgnoreCase))
            {
                ApplySmokeInteractiveRoiStepMeasurement();
            }
            else
            {
                ApplySmokeRoiStepMeasurement();
            }

            return;
        }

        viewModel.UseSelectionSmokeScene(selectionMode);
    }

    private void ApplySmokeMeasure(string measure)
    {
        if (measure.Equals("dimensions", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("point-pair-dimensions", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("width-distance-angle", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokePointPairDimensions();
        }
        else if (measure.Equals("two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("laz-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("laz-distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("glb-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("mesh-two-point", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("glb-distance-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("mesh-distance-height", StringComparison.OrdinalIgnoreCase))
        {
            if (measure.StartsWith("laz-", StringComparison.OrdinalIgnoreCase)
                || (viewModel.LazSampleVisible && lazPointCloud is not null))
            {
                ApplySmokeLazTwoPointMeasurement();
            }
            else if (measure.StartsWith("glb-", StringComparison.OrdinalIgnoreCase)
                || measure.StartsWith("mesh-", StringComparison.OrdinalIgnoreCase)
                || (viewModel.GlbSampleVisible && importedMesh is not null))
            {
                ApplySmokeImportedMeshTwoPointMeasurement();
            }
            else
            {
                ApplySmokeTwoPointMeasurement();
            }
        }
        else if (measure.Equals("roi-step", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("step-height", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("roi", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeRoiStepMeasurement();
        }
        else if (measure.Equals("roi-interactive", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("interactive-roi", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeInteractiveRoiStepMeasurement();
        }
        else if (measure.Equals("plane-distance", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("distance-to-plane", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("reference-plane", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokePlaneReferenceMeasurement();
        }
        else if (measure.Equals("flatness", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("plane-flatness", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("reference-roi-flatness", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokePlaneFlatness();
        }
        else if (measure.Equals("gap-flush", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("gapflush", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeGapFlush();
        }
        else if (measure.Equals("volume", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeVolume();
        }
        else if (measure.Equals("cross-section", StringComparison.OrdinalIgnoreCase)
            || measure.Equals("cross-section-dimensions", StringComparison.OrdinalIgnoreCase))
        {
            ApplySmokeCrossSection();
        }
    }

    private void ApplySmokeRecipeParameterEdit(string mode)
    {
        if (mode.Equals("laz-acceptance", StringComparison.OrdinalIgnoreCase)
            || mode.Equals("laz-two-point", StringComparison.OrdinalIgnoreCase))
        {
            if (lazTwoPointFirst is null || lazTwoPointSecond is null)
            {
                ApplySmokeLazTwoPointMeasurement();
            }

            if (double.IsFinite(viewModel.TwoPointDistance))
            {
                viewModel.LazTwoPointExpectedDistance = viewModel.TwoPointDistance - 0.001;
            }

            if (double.IsFinite(viewModel.TwoPointRawHeightDelta))
            {
                viewModel.LazTwoPointExpectedHeightDelta = viewModel.TwoPointRawHeightDelta - 0.001;
            }

            viewModel.LazTwoPointDistanceTolerance = 0.020;
            viewModel.LazTwoPointHeightDeltaTolerance = 0.020;
            viewModel.SelectedEntity = "LAZ/LAS Two Point Measurement";
            viewModel.ViewerStatus = "Smoke recipe parameter edit: LAZ/LAS acceptance";
            return;
        }

        if (!mode.Equals("roi-align", StringComparison.OrdinalIgnoreCase)
            && !mode.Equals("roi-alignment", StringComparison.OrdinalIgnoreCase)
            && !mode.Equals("parameters", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        if (!viewModel.RoiStepMeasurementVisible)
        {
            ApplySmokeInteractiveRoiStepMeasurement();
        }

        viewModel.RecipeTransformTranslateX += 0.125;
        viewModel.RecipeTransformTranslateY += 0.025;
        viewModel.RecipeRoiLeftCenterX += 0.120;
        viewModel.RecipeRoiRightCenterZ += 0.080;
        viewModel.RecipeRoiLeftHalfWidth = Math.Max(0.050, viewModel.RecipeRoiLeftHalfWidth * 0.92);
        viewModel.RecipeRoiRightHalfDepth = Math.Max(0.050, viewModel.RecipeRoiRightHalfDepth * 0.96);
        ApplyEditedRoiStepParameters();
        viewModel.ViewerStatus = "Smoke recipe parameter edit: ROI/alignment";
    }

    private void ApplySmokeInvalidRoi(string mode)
    {
        if (!viewModel.C3DSampleVisible)
        {
            ApplySmokeC3D();
        }

        if (!viewModel.RoiStepMeasurementVisible)
        {
            ApplySmokeInteractiveRoiStepMeasurement();
        }

        if (mode.Equals("overlap", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.RecipeRoiRightCenterX = viewModel.RecipeRoiLeftCenterX;
            viewModel.RecipeRoiRightCenterZ = viewModel.RecipeRoiLeftCenterZ;
        }
        else
        {
            viewModel.RecipeRoiLeftCenterX = 1000.0;
            viewModel.RecipeRoiRightCenterX = 1002.0;
        }

        ApplyEditedRoiStepParameters();
        if (!ValidateRecipeState(requireRoi: true, out var warning))
        {
            SetRecipeValidationWarning(warning);
            viewModel.ViewerStatus = "Smoke invalid ROI: validation warning";
        }
    }

    private void ApplySmokeOverlay(string overlay)
    {
        if (overlay.Equals("result", StringComparison.OrdinalIgnoreCase))
        {
            if (viewModel.C3DSampleVisible)
            {
                viewModel.UseC3DHeightDeviationRuleSmokeScene();
            }
            else
            {
                viewModel.UseResultSmokeScene();
            }
        }
    }

    private void ApplySmokeRule(string rule)
    {
        if (rule.Equals("height-deviation", StringComparison.OrdinalIgnoreCase))
        {
            viewModel.UseC3DHeightDeviationRuleSmokeScene();
        }
    }

    private void ApplySmokeRecipe(string path)
    {
        if (!ApplyRecipeFile(path, isSmoke: true))
        {
            smokeExitCode = 1;
        }
    }

    private bool ApplyRecipeFile(string path, bool isSmoke)
        => ViewerRecipeLoadCoordinator.Apply(
            path,
            isSmoke,
            CreateRecipeLoadRoutes(),
            SetRecipeLoadFailure);

    private Task<bool> ApplyRecipeFileAsync(string path, bool isSmoke) =>
        ViewerRecipeLoadCoordinator.ApplyAsync(
            path,
            isSmoke,
            CreateRecipeLoadRoutes(),
            SetRecipeLoadFailure,
            viewerLifetimeToken);

    private ViewerRecipeLoadRoutes CreateRecipeLoadRoutes() =>
        new(
            ApplyNominalActualRecipe,
            ApplyLazTwoPointRecipe,
            ApplyC3DThicknessRecipe,
            ApplyC3DWarpageRecipe,
            ApplyC3DGapFlushRecipe,
            ApplyC3DPointPairDimensionsRecipe,
            ApplyHeightDeviationRecipe,
            ApplyLazTwoPointRecipeAsync);

    private bool ApplyNominalActualRecipe(
        ViewerRecipeFile recipeFile,
        NominalActualComparisonRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = NominalActualComparisonRecipeLoadPlan.Create(recipeFile, recipe);
            return NominalActualComparisonRecipeApplyCoordinator.Apply(
                plan,
                viewModel,
                isSmoke,
                path =>
                {
                    ApplySmokeStl(path);
                    return importedMesh is not null;
                },
                () =>
                {
                    smokeNominalActualPreview = true;
                    viewModel.NominalActual.PreviewCommand.Execute(null);
                });
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            viewModel.ClearNominalActualComparison(ex.Message);
            return SetRecipeLoadFailure(isSmoke ? "Smoke nominal/actual recipe" : "Nominal/actual recipe", ex);
        }
    }

    private bool ApplyHeightDeviationRecipe(
        ViewerRecipeFile recipeFile,
        HeightDeviationRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = HeightDeviationRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                viewModel.C3DMaxRenderedPoints);
            c3dSample = plan.Grid;
            viewModel.RecipeOutputEnabled = recipe.OutputEnabled;
            return HeightDeviationRecipeApplyCoordinator.Apply(
                plan,
                viewModel,
                isSmoke,
                SetC3DSampleStatus,
                ApplyRecipeRoiStep,
                PreviewC3DPlaneFlatness,
                PreviewC3DVolume,
                PreviewC3DCrossSection);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke recipe" : "Recipe", ex);
        }
    }

    private bool ApplyC3DThicknessRecipe(
        ViewerRecipeFile recipeFile,
        C3DThicknessRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = C3DThicknessRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                viewModel.C3DMaxRenderedPoints);
            c3dSample = plan.Grid;
            return C3DThicknessRecipeApplyCoordinator.Apply(
                plan,
                viewModel,
                isSmoke,
                SetC3DSampleStatus,
                () =>
                {
                    planeFlatnessEvaluation = null;
                    planeReferenceMeasurement = null;
                },
                RenderNow);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke Thickness recipe" : "Thickness recipe", ex);
        }
    }

    private bool ApplyC3DPointPairDimensionsRecipe(
        ViewerRecipeFile recipeFile,
        C3DPointPairDimensionsRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = C3DPointPairDimensionsRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                viewModel.C3DMaxRenderedPoints);
            c3dSample = plan.Grid;
            return C3DPointPairDimensionsRecipeApplyCoordinator.Apply(
                plan,
                viewModel,
                isSmoke,
                SetC3DSampleStatus,
                () =>
                {
                    planeFlatnessEvaluation = null;
                    planeReferenceMeasurement = null;
                },
                () => ApplyRecipeRoiStep(null),
                (first, second) => SetTwoPointMeasurement(first, second, updatePointPairReferences: false),
                RenderNow);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke point pair recipe" : "Point pair recipe", ex);
        }
    }

    private bool ApplyLazTwoPointRecipe(
        ViewerRecipeFile recipeFile,
        LazTwoPointMeasurementRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = LazTwoPointRecipeLoadPlan.Create(recipeFile, recipe);
            lazPointCloud = LoadLazPointCloud(plan.SourcePath, recipe.Measurement.MaxSampledPoints);
            lazSample = lazPointCloud?.Metadata;
            if (lazPointCloud is null || lazSample is null)
            {
                throw new InvalidDataException("LAZ/LAS two-point recipe source could not be decoded.");
            }

            return LazTwoPointRecipeApplyCoordinator.Apply(
                plan,
                lazPointCloud,
                viewModel,
                isSmoke,
                clearTransientMeasurement: () =>
                {
                    lazTwoPointFirst = null;
                    lazTwoPointSecond = null;
                    selectedLazPoint = null;
                },
                applySmokeMeasurement: ApplySmokeLazTwoPointMeasurement);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke LAZ/LAS recipe" : "LAZ/LAS recipe", ex);
        }
    }

    private async Task<bool> ApplyLazTwoPointRecipeAsync(
        ViewerRecipeFile recipeFile,
        LazTwoPointMeasurementRecipe recipe,
        bool isSmoke,
        CancellationToken cancellationToken)
    {
        try
        {
            var plan = LazTwoPointRecipeLoadPlan.Create(recipeFile, recipe);
            var loaded = await LoadLazPointCloudAsync(
                plan.SourcePath,
                recipe.Measurement.MaxSampledPoints,
                cancellationToken,
                isCurrent: () => !IsDisposed && !cancellationToken.IsCancellationRequested);
            cancellationToken.ThrowIfCancellationRequested();
            if (loaded is null || IsDisposed)
            {
                return false;
            }

            lazPointCloud = loaded;
            lazSample = loaded.Metadata;
            return LazTwoPointRecipeApplyCoordinator.Apply(
                plan,
                loaded,
                viewModel,
                isSmoke,
                clearTransientMeasurement: () =>
                {
                    lazTwoPointFirst = null;
                    lazTwoPointSecond = null;
                    selectedLazPoint = null;
                },
                applySmokeMeasurement: ApplySmokeLazTwoPointMeasurement);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke LAZ/LAS recipe" : "LAZ/LAS recipe", ex);
        }
    }


    private bool SetRecipeLoadFailure(string label, Exception exception)
    {
        if (IsDisposed)
        {
            return false;
        }

        var message = $"{label} failed: {exception.Message}";
        SetRecipeValidationWarning(message);
        viewModel.ViewerStatus = message;
        return false;
    }

    private bool SaveCurrentNominalActualRecipe(string path, bool isSmoke)
        => NominalActualComparisonRecipeSaveCoordinator.Save(path, isSmoke, viewModel);

    private bool SaveCurrentHeightDeviationRecipe(string path, bool isSmoke)
    {
        try
        {
            if (!ValidateRecipeState(requireRoi: viewModel.SelectedSelectionMode == RoiStepSelectionMode, out var warning))
            {
                SetRecipeValidationWarning(warning);
                viewModel.ViewerStatus = warning;
                return false;
            }

            if (viewModel.PlaneFlatnessConfigured && !ValidatePlaneFlatnessRecipeState(out warning))
            {
                SetRecipeValidationWarning(warning);
                viewModel.ViewerStatus = warning;
                return false;
            }

            var sourcePath = ResolveCurrentRecipeSourcePath();
            var saved = HeightDeviationRecipeSaveCoordinator.Save(
                path,
                isSmoke,
                viewModel,
                sourcePath,
                CreateCurrentRoiStepRecipe(),
                viewModel.RecipeOutputEnabled);
            if (saved)
            {
                SetRecipeValidationOk();
            }

            return saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke recipe save" : "Recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentLazTwoPointRecipe(string path, bool isSmoke)
        => LazTwoPointRecipeSaveCoordinator.Save(
            path,
            isSmoke,
            viewModel,
            lazPointCloud,
            lazTwoPointFirst is not null && lazTwoPointSecond is not null,
            SetRecipeValidationOk);

    private bool SaveCurrentThicknessRecipe(string path, bool isSmoke)
        => C3DThicknessRecipeSaveCoordinator.Save(path, isSmoke, viewModel, c3dSample);

    private bool SaveCurrentPointPairDimensionsRecipe(string path, bool isSmoke)
        => C3DPointPairDimensionsRecipeSaveCoordinator.Save(path, isSmoke, viewModel, c3dSample);

    private HeightDeviationRecipeRoiStep? CreateCurrentRoiStepRecipe()
    {
        if (!viewModel.RoiStepMeasurementVisible)
        {
            return null;
        }

        return new HeightDeviationRecipeRoiStep(
            viewModel.RecipeRoiMode,
            CreateLeftRoiRegionFromViewModel(),
            CreateRightRoiRegionFromViewModel(),
            viewModel.RecipeRoiMaxSampledPoints);
    }

    private static HeightDeviationRecipeRoiRegion CreateRoiRegion((float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) bounds) =>
        new(
            (bounds.MinX + bounds.MaxX) * 0.5,
            (bounds.MinZ + bounds.MaxZ) * 0.5,
            Math.Max(0.0001, (bounds.MaxX - bounds.MinX) * 0.5),
            Math.Max(0.0001, (bounds.MaxZ - bounds.MinZ) * 0.5));

    private static HeightDeviationRecipeRoiRegion OffsetRoiRegion(HeightDeviationRecipeRoiRegion region, double offsetX, double offsetZ) =>
        new(region.CenterX + offsetX, region.CenterZ + offsetZ, region.HalfWidth, region.HalfDepth);

    private HeightDeviationRecipeRoiRegion CreateLeftRoiRegionFromViewModel() =>
        new(
            viewModel.RecipeRoiLeftCenterX,
            viewModel.RecipeRoiLeftCenterZ,
            viewModel.RecipeRoiLeftHalfWidth,
            viewModel.RecipeRoiLeftHalfDepth);

    private HeightDeviationRecipeRoiRegion CreateRightRoiRegionFromViewModel() =>
        new(
            viewModel.RecipeRoiRightCenterX,
            viewModel.RecipeRoiRightCenterZ,
            viewModel.RecipeRoiRightHalfWidth,
            viewModel.RecipeRoiRightHalfDepth);

    private bool ValidateRecipeState(bool requireRoi, out string warning)
    {
        var transform = viewModel.C3DModelTransform;
        if (!double.IsFinite(transform.TranslateX)
            || !double.IsFinite(transform.TranslateY)
            || !double.IsFinite(transform.TranslateZ)
            || !double.IsFinite(transform.RotateXDegrees)
            || !double.IsFinite(transform.RotateYDegrees)
            || !double.IsFinite(transform.RotateZDegrees)
            || !double.IsFinite(transform.Scale)
            || transform.Scale <= 0.0)
        {
            warning = "Validation warning: transform values must be finite and scale must be positive.";
            return false;
        }

        if (!requireRoi)
        {
            warning = "Validation: OK";
            return true;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length == 0)
        {
            warning = "Validation warning: ROI validation requires a visible C3D height grid.";
            return false;
        }

        var left = CreateLeftRoiRegionFromViewModel();
        var right = CreateRightRoiRegionFromViewModel();
        if (!IsValidRegion(left) || !IsValidRegion(right))
        {
            warning = "Validation warning: ROI center and size values must be finite and positive.";
            return false;
        }

        var bounds = GetTransformedC3DBounds();
        if (!RegionIntersectsBounds(left, bounds))
        {
            warning = "Validation warning: left ROI is outside the visible C3D bounds.";
            return false;
        }

        if (!RegionIntersectsBounds(right, bounds))
        {
            warning = "Validation warning: right ROI is outside the visible C3D bounds.";
            return false;
        }

        if (RegionsOverlap(left, right))
        {
            warning = "Validation warning: left and right ROI regions overlap.";
            return false;
        }

        if (!TryCalculateRoiStats(CreateRoiBounds(left, bounds), out var leftStats) || leftStats.Count < 10)
        {
            warning = "Validation warning: left ROI has too few C3D samples.";
            return false;
        }

        if (!TryCalculateRoiStats(CreateRoiBounds(right, bounds), out var rightStats) || rightStats.Count < 10)
        {
            warning = "Validation warning: right ROI has too few C3D samples.";
            return false;
        }

        warning = "Validation: OK";
        return true;
    }

    private bool ValidatePlaneFlatnessRecipeState(out string warning)
    {
        var step = viewModel.CreatePlaneFlatnessRecipeStep();
        if (!IsValidRegion(step.ReferenceRegion)
            || !double.IsFinite(step.Tolerance)
            || step.Tolerance <= 0.0)
        {
            warning = "Validation warning: flatness reference ROI and tolerance must be finite and positive.";
            return false;
        }

        if (!viewModel.C3DSampleVisible || c3dSample is null || c3dSample.Points.Length == 0)
        {
            warning = "Validation warning: plane flatness requires a visible C3D height grid.";
            return false;
        }

        var referenceSampleCount = c3dSample.Points.Count(point => Contains(step.ReferenceRegion, TransformC3DPosition(point.Position)));
        if (referenceSampleCount < 3)
        {
            warning = "Validation warning: flatness reference ROI contains fewer than three C3D samples.";
            return false;
        }

        warning = "Validation: OK";
        return true;
    }

    private void SetRecipeValidationOk() => viewModel.SetRecipeValidationSummary("Validation: OK");

    private void SetRecipeValidationWarning(string warning) => viewModel.SetRecipeValidationSummary(warning);

    private static bool IsValidRegion(HeightDeviationRecipeRoiRegion region) =>
        double.IsFinite(region.CenterX)
        && double.IsFinite(region.CenterZ)
        && double.IsFinite(region.HalfWidth)
        && double.IsFinite(region.HalfDepth)
        && region.HalfWidth > 0.0
        && region.HalfDepth > 0.0;

    private static bool RegionsOverlap(HeightDeviationRecipeRoiRegion left, HeightDeviationRecipeRoiRegion right) =>
        Math.Abs(left.CenterX - right.CenterX) < left.HalfWidth + right.HalfWidth
        && Math.Abs(left.CenterZ - right.CenterZ) < left.HalfDepth + right.HalfDepth;

    private static bool RegionIntersectsBounds(
        HeightDeviationRecipeRoiRegion region,
        (float MinX, float MaxX, float MinZ, float MaxZ) bounds) =>
        region.CenterX + region.HalfWidth >= bounds.MinX
        && region.CenterX - region.HalfWidth <= bounds.MaxX
        && region.CenterZ + region.HalfDepth >= bounds.MinZ
        && region.CenterZ - region.HalfDepth <= bounds.MaxZ;

    private void ApplyEditedRoiStepParameters()
    {
        if (!viewModel.C3DSampleVisible)
        {
            viewModel.UseC3DSmokeScene();
        }

        roiStepInteractiveSelection = true;
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = CreateLeftRoiRegionFromViewModel();
        roiStepRightRecipeRegion = CreateRightRoiRegionFromViewModel();
        roiStepLeftAnchor = new Vector3((float)viewModel.RecipeRoiLeftCenterX, 0.0f, (float)viewModel.RecipeRoiLeftCenterZ);
        roiStepRightAnchor = new Vector3((float)viewModel.RecipeRoiRightCenterX, 0.0f, (float)viewModel.RecipeRoiRightCenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (UpdateRoiStepMeasurement())
        {
            if (ValidateRecipeState(requireRoi: true, out var warning))
            {
                SetRecipeValidationOk();
            }
            else
            {
                SetRecipeValidationWarning(warning);
            }

            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Recipe ROI parameters updated";
        }
        else
        {
            ValidateRecipeState(requireRoi: true, out var warning);
            SetRecipeValidationWarning(warning);
        }
    }

    private void ApplyRecipeRoiStep(HeightDeviationRecipeRoiStep? roiStep)
    {
        ClearRecipeRoiStep();
        if (roiStep is null)
        {
            return;
        }

        if (!viewModel.C3DSampleVisible)
        {
            viewModel.UseC3DSmokeScene();
        }

        roiStepInteractiveSelection = roiStep.Mode.Equals("Interactive", StringComparison.OrdinalIgnoreCase);
        roiStepNextPickSetsRight = false;
        roiStepLeftRecipeRegion = roiStep.Left;
        roiStepRightRecipeRegion = roiStep.Right;
        SyncRecipeRoiEditFromRegions(roiStep.Mode, roiStep.Left, roiStep.Right, roiStep.MaxSampledPoints);
        roiStepLeftAnchor = new Vector3((float)roiStep.Left.CenterX, 0.0f, (float)roiStep.Left.CenterZ);
        roiStepRightAnchor = new Vector3((float)roiStep.Right.CenterX, 0.0f, (float)roiStep.Right.CenterZ);
        viewModel.SelectedSelectionMode = RoiStepSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (UpdateRoiStepMeasurement())
        {
            viewModel.SelectedEntity = "ROI Step Compare";
            viewModel.ViewerStatus = "Recipe ROI step restored";
        }
    }

    private void ClearRecipeRoiStep()
    {
        roiStepLeftRecipeRegion = null;
        roiStepRightRecipeRegion = null;
    }

    private void SyncRecipeRoiEditFromBounds(
        string mode,
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) leftBounds,
        (float MinX, float MaxX, float MinZ, float MaxZ, float MeanY) rightBounds)
    {
        SyncRecipeRoiEditFromRegions(
            mode,
            CreateRoiRegion(leftBounds),
            CreateRoiRegion(rightBounds),
            viewModel.RecipeRoiMaxSampledPoints);
    }

    private void SyncRecipeRoiEditFromRegions(
        string mode,
        HeightDeviationRecipeRoiRegion left,
        HeightDeviationRecipeRoiRegion right,
        int maxSampledPoints)
    {
        suppressRecipeParameterSync = true;
        try
        {
            viewModel.SetRecipeRoiStepEdit(
                mode,
                left.CenterX,
                left.CenterZ,
                left.HalfWidth,
                left.HalfDepth,
                right.CenterX,
                right.CenterZ,
                right.HalfWidth,
                right.HalfDepth,
                maxSampledPoints);
        }
        finally
        {
            suppressRecipeParameterSync = false;
        }
    }

    private string ResolveCurrentRecipeSourcePath()
    {
        var candidate = viewModel.RecipeSourcePath;
        if (!Path.IsPathRooted(candidate))
        {
            candidate = Path.GetFullPath(candidate);
        }

        if (File.Exists(candidate))
        {
            return candidate;
        }

        var defaultSample = ViewerSamplePathLocator.Find(DefaultC3DSamplePath);
        return defaultSample is not null ? Path.GetFullPath(defaultSample) : candidate;
    }

}

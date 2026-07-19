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
    private void HandleFitAllCommand()
    {
        viewModel.FitAll();
        RenderNow();
    }

    private void HandleFitSelectionCommand()
    {
        viewModel.FitSelection();
        RenderNow();
    }

    private void HandleResetCommand()
    {
        viewModel.Reset();
        RenderNow();
    }

    private void HandleScreenshotCommand()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "artifacts", $"sharpgl_viewer_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        CaptureWindow(path);
    }

    private void HandleOpenRecipeCommand()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            ApplyRecipeFile(dialog.FileName, isSmoke: false);
            RenderNow();
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
        var dialog = new SaveFileDialog
        {
            Title = "Save 3D Recipe",
            Filter = "OpenVisionLab 3D recipe (*.json)|*.json|All files (*.*)|*.*",
            FileName = ShouldSaveCurrentNominalActualRecipe()
                ? "nominal-actual-surface-deviation.recipe.json"
                : ShouldSaveCurrentLazTwoPointRecipe()
                ? "laz-two-point-measurement.recipe.json"
                : ShouldSaveCurrentWarpageRecipe()
                    ? "c3d-warpage.recipe.json"
                : ShouldSaveCurrentThicknessRecipe()
                    ? "c3d-thickness.recipe.json"
                : ShouldSaveCurrentGapFlushRecipe()
                    ? "c3d-gap-flush.recipe.json"
                : ShouldSaveCurrentPointPairDimensionsRecipe()
                    ? "c3d-point-pair-dimensions.recipe.json"
                    : "c3d-height-deviation.recipe.json",
            OverwritePrompt = true
        };

        if (dialog.ShowDialog(Window.GetWindow(this)) == true)
        {
            SaveCurrentRecipe(dialog.FileName, isSmoke: false);
        }
    }

    public bool SaveCurrentRecipe(string path, bool isSmoke) =>
        ShouldSaveCurrentNominalActualRecipe()
            ? SaveCurrentNominalActualRecipe(path, isSmoke)
            : ShouldSaveCurrentLazTwoPointRecipe()
            ? SaveCurrentLazTwoPointRecipe(path, isSmoke)
            : ShouldSaveCurrentWarpageRecipe()
                ? SaveCurrentWarpageRecipe(path, isSmoke)
            : ShouldSaveCurrentThicknessRecipe()
                ? SaveCurrentThicknessRecipe(path, isSmoke)
            : ShouldSaveCurrentGapFlushRecipe()
                ? SaveCurrentGapFlushRecipe(path, isSmoke)
            : ShouldSaveCurrentPointPairDimensionsRecipe()
                ? SaveCurrentPointPairDimensionsRecipe(path, isSmoke)
                : SaveCurrentHeightDeviationRecipe(path, isSmoke);

    private bool ShouldSaveCurrentNominalActualRecipe() =>
        viewModel.NominalActualInput is not null
        && viewModel.NominalActual.PreviewResult is not null
        && viewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;

    private bool ShouldSaveCurrentLazTwoPointRecipe() =>
        lazPointCloud is not null
        && lazTwoPointFirst is not null
        && lazTwoPointSecond is not null
        && viewModel.SelectedEntity.Contains("Two Point Measurement", StringComparison.OrdinalIgnoreCase)
        && viewModel.LazSampleVisible;

    private bool ShouldSaveCurrentPointPairDimensionsRecipe() =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.PointPairDimensionsConfigured
        && viewModel.HasPointPairReferences;

    private bool ShouldSaveCurrentThicknessRecipe() =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.ThicknessConfigured
        && viewModel.ThicknessVisible
        && viewModel.PreviewToolResult.ToolName.Equals(C3DThicknessRule.ToolName, StringComparison.Ordinal)
        && viewModel.PreviewToolResult.Status != ResultStatus.Error;

    private bool ShouldSaveCurrentGapFlushRecipe() =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.GapFlushConfigured
        && viewModel.GapFlushVisible;

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
        await Dispatcher.InvokeAsync(RenderNow);
        if (smokeNominalActualPreview
            && !await WaitForNominalActualPreviewAsync(TimeSpan.FromMinutes(10)))
        {
            smokeExitCode = 1;
            if (viewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning)
            {
                viewModel.ViewerStatus = "Nominal/actual Preview timed out before screenshot capture.";
            }

            await Dispatcher.InvokeAsync(RenderNow);
        }

        ApplyConfiguredSmokeNextDensity();
        await Dispatcher.InvokeAsync(RenderNow);

        if (smokePickTarget is not null)
        {
            ApplyConfiguredSmokePick();
            await Dispatcher.InvokeAsync(RenderNow);
        }

        if (smokePublishResult)
        {
            if (!PublishCurrentPreviewResult())
            {
                smokeExitCode = 1;
                viewModel.ViewerStatus = "Smoke Publish failed: current Preview evidence is unavailable";
            }

            await Dispatcher.InvokeAsync(RenderNow);
        }

        if (smokeSaveRecipePath is not null)
        {
            if (!SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
            {
                smokeExitCode = 1;
            }

            await Dispatcher.InvokeAsync(RenderNow);
        }

        await RunConfiguredPointerInputRegressionAsync();
        await Dispatcher.InvokeAsync(RenderNow);

        await Task.Delay(900);
        await CaptureConfiguredSmokeViewAsync();

        await Task.Delay(100);
        Application.Current.Shutdown(smokeExitCode);
    }

    private async Task<bool> WaitForNominalActualPreviewAsync(TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (viewModel.NominalActual.State == NominalActualComparisonState.PreviewRunning
            && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        return viewModel.NominalActual.State is NominalActualComparisonState.PreviewReady
            or NominalActualComparisonState.Published;
    }

    private async Task<bool> CaptureSmokeViewWithRetryAsync(string path, string? qualityReportPath)
    {
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
            RenderNow();
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            var result = WpfScreenshotCapture.Capture(this);
            var qualityLine = $"ViewerScreenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                WpfScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"ViewerScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteScreenshotQualityReport(qualityReportPath, qualityLines);
                viewModel.LastScreenshotPath = fullPath;
                viewModel.ViewerStatus = "Screenshot captured";
                return true;
            }

            WpfScreenshotCapture.Save(result.Bitmap, GetRejectedScreenshotPath(fullPath, attempt));
            await Task.Delay(250);
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
        for (var frame = 0; frame < smokeRenderFrameCount; frame++)
        {
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
            smokeRenderFramesCompleted++;
        }

        if (smokeMeasureMode is not null)
        {
            ApplySmokeMeasure(smokeMeasureMode);
            await Dispatcher.InvokeAsync(RenderNow, DispatcherPriority.Render);
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
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipeType = ReadRecipeType(fullRecipePath);
            if (recipeType.Equals(NominalActualComparisonRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyNominalActualRecipe(fullRecipePath, isSmoke);
            }

            if (recipeType.Equals(LazTwoPointMeasurementRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyLazTwoPointRecipe(fullRecipePath, isSmoke);
            }

            if (recipeType.Equals(C3DThicknessRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyC3DThicknessRecipe(fullRecipePath, isSmoke);
            }

            if (recipeType.Equals(C3DWarpageRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyC3DWarpageRecipe(fullRecipePath, isSmoke);
            }

            if (recipeType.Equals(C3DGapFlushRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return ApplyC3DGapFlushRecipe(fullRecipePath, isSmoke);
            }

            return recipeType.Equals(C3DPointPairDimensionsRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
                ? ApplyC3DPointPairDimensionsRecipe(fullRecipePath, isSmoke)
                : ApplyHeightDeviationRecipe(fullRecipePath, isSmoke);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke recipe" : "Recipe", ex);
        }
    }

    private bool ApplyNominalActualRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = NominalActualComparisonRecipe.Load(fullRecipePath);
            var input = recipe.ToInput(fullRecipePath);
            ApplySmokeStl(input.NominalSource.Path);
            if (importedMesh is null)
            {
                throw new InvalidDataException("The nominal comparison mesh could not be loaded for display.");
            }

            viewModel.ConfigureNominalActualComparison(input);
            viewModel.SetNominalActualRecipeLoaded(fullRecipePath);
            smokeNominalActualPreview |= isSmoke;
            viewModel.NominalActual.PreviewCommand.Execute(null);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke nominal/actual recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Nominal/actual recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            viewModel.ClearNominalActualComparison(ex.Message);
            return SetRecipeLoadFailure(isSmoke ? "Smoke nominal/actual recipe" : "Nominal/actual recipe", ex);
        }
    }

    private bool ApplyHeightDeviationRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = HeightDeviationRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            var grid = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            c3dSample = grid;
            SetC3DSampleStatus();
            var result = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
                recipe.Source.EntityId,
                recipe.Source.Name,
                grid.Min,
                grid.Max,
                grid.Mean,
                grid.ValidSampleCount,
                recipe.Rule.PeakTolerance,
                recipe.Source.Unit));

            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.SetC3DHeightDeviationPreview(result);
            viewModel.UseC3DHeightDeviationRuleSmokeScene();
            viewModel.SetRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit, recipe.Rule.PeakTolerance);
            viewModel.SetC3DAlignment(recipe.Transform ?? ModelTransform.Identity, recipe.Transform is null ? "Recipe identity alignment" : "Recipe alignment", recipe.Source.Name);
            ApplyRecipeRoiStep(recipe.RoiStep);
            if (recipe.PlaneFlatness is { } planeFlatness)
            {
                viewModel.SetPlaneFlatnessRecipeStep(planeFlatness);
                if (planeFlatness.Enabled)
                {
                    PreviewC3DPlaneFlatness();
                }
            }
            if (recipe.Volume is { } volume)
            {
                viewModel.SetVolumeRecipeStep(volume);
                if (volume.Enabled)
                {
                    PreviewC3DVolume();
                }
            }
            if (recipe.CrossSection is { } crossSection)
            {
                viewModel.SetCrossSectionRecipeStep(crossSection);
                if (crossSection.Enabled)
                {
                    PreviewC3DCrossSection();
                }
            }
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke recipe" : "Recipe", ex);
        }
    }

    private bool ApplyC3DThicknessRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = C3DThicknessRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            c3dSample = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            if (!IsC3DGridRoiInside(recipe.Step.Roi, c3dSample))
            {
                throw new InvalidDataException("Thickness recipe ROI is outside the loaded C3D grid.");
            }

            SetC3DSampleStatus();
            planeFlatnessEvaluation = null;
            planeReferenceMeasurement = null;
            viewModel.ClearThicknessPreview();
            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(ModelTransform.Identity, "C3D grid-index scalar frame", recipe.Source.Name);
            viewModel.SetThicknessRecipeStep(recipe.Step);
            viewModel.SetThicknessRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit);
            viewModel.SelectedSelectionMode = MainWindowViewModel.ThicknessRoiSelectionMode;
            viewModel.SelectionOverlayVisible = true;

            if (recipe.Step.Enabled && !PreviewC3DThickness())
            {
                throw new InvalidDataException("Thickness preview failed for the configured grid ROI.");
            }

            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Thickness recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Thickness recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke Thickness recipe" : "Thickness recipe", ex);
        }
    }

    private bool ApplyC3DPointPairDimensionsRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = C3DPointPairDimensionsRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            var grid = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            var first = grid.ReadPoint(recipe.Step.First.Row, recipe.Step.First.Column);
            var second = grid.ReadPoint(recipe.Step.Second.Row, recipe.Step.Second.Column);

            c3dSample = grid;
            SetC3DSampleStatus();
            planeFlatnessEvaluation = null;
            planeReferenceMeasurement = null;
            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(
                recipe.Transform ?? ModelTransform.Identity,
                recipe.Transform is null ? "Recipe identity alignment" : "Recipe alignment",
                recipe.Source.Name);
            ApplyRecipeRoiStep(null);
            viewModel.SetPointPairDimensionsRecipeStep(recipe.Step);
            viewModel.SetPointPairRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit);
            SetTwoPointMeasurement(first, second, updatePointPairReferences: false);
            viewModel.SelectedSelectionMode = TwoPointSelectionMode;
            viewModel.SelectionOverlayVisible = true;

            if (recipe.Step.Enabled && !PreviewC3DPointPairDimensions())
            {
                throw new InvalidDataException("Point pair dimensions preview failed for the configured source cells.");
            }

            viewModel.ViewerStatus = isSmoke
                ? $"Smoke point pair recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Point pair recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke point pair recipe" : "Point pair recipe", ex);
        }
    }

    private bool ApplyC3DGapFlushRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = C3DGapFlushRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            c3dSample = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            SetC3DSampleStatus();
            planeFlatnessEvaluation = null;
            planeReferenceMeasurement = null;
            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(
                recipe.Transform ?? ModelTransform.Identity,
                recipe.Transform is null ? "Recipe identity alignment" : "Recipe alignment",
                recipe.Source.Name);
            viewModel.SetGapFlushRecipeStep(recipe.Step);
            viewModel.SetPointPairRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit);
            roiStepLeftRecipeRegion = recipe.Step.LeftRegion;
            roiStepRightRecipeRegion = recipe.Step.RightRegion;
            roiStepInteractiveSelection = false;
            roiStepNextPickSetsRight = false;

            if (recipe.Step.Enabled && !PreviewC3DGapFlush())
            {
                throw new InvalidDataException("Gap / Flush preview failed for the configured regions.");
            }

            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Gap / Flush recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Gap / Flush recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke Gap / Flush recipe" : "Gap / Flush recipe", ex);
        }
    }

    private bool ApplyLazTwoPointRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = LazTwoPointMeasurementRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            lazPointCloud = LoadLazPointCloud(sourcePath, recipe.Measurement.MaxSampledPoints);
            lazSample = lazPointCloud?.Metadata;
            if (lazPointCloud is null || lazSample is null)
            {
                throw new InvalidDataException("LAZ/LAS two-point recipe source could not be decoded.");
            }

            viewModel.SetLazSampleSource(sourcePath, recipe.Source.Name);
            viewModel.LazTwoPointExpectedDistance = recipe.Acceptance.ExpectedDistance;
            viewModel.LazTwoPointDistanceTolerance = recipe.Acceptance.DistanceTolerance;
            viewModel.LazTwoPointExpectedHeightDelta = recipe.Acceptance.ExpectedHeightDelta;
            viewModel.LazTwoPointHeightDeltaTolerance = recipe.Acceptance.HeightDeltaTolerance;
            ApplySmokeLazTwoPointMeasurement(recipe.Measurement.HeightUnit);
            viewModel.SetLazRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke LAZ/LAS recipe: {Path.GetFileName(fullRecipePath)}"
                : $"LAZ/LAS recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke LAZ/LAS recipe" : "LAZ/LAS recipe", ex);
        }
    }

    private bool SetRecipeLoadFailure(string label, Exception exception)
    {
        var message = $"{label} failed: {exception.Message}";
        SetRecipeValidationWarning(message);
        viewModel.ViewerStatus = message;
        return false;
    }

    private bool SaveCurrentNominalActualRecipe(string path, bool isSmoke)
    {
        try
        {
            var comparison = viewModel.NominalActual;
            if (comparison.PreviewResult is not { } result
                || comparison.State is not (NominalActualComparisonState.PreviewReady
                    or NominalActualComparisonState.Published)
                || !result.Input.ExecutionFingerprint.Equals(
                    comparison.CompletedPreviewFingerprint,
                    StringComparison.Ordinal))
            {
                viewModel.ViewerStatus =
                    "Nominal/actual recipe save requires a current completed Preview";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipe = NominalActualComparisonRecipe.FromInput(result.Input, fullRecipePath);
            recipe.Save(fullRecipePath);
            viewModel.SetNominalActualRecipeSaved(fullRecipePath);
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke nominal/actual recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Nominal/actual recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus =
                $"{(isSmoke ? "Smoke nominal/actual recipe save" : "Nominal/actual recipe save")} failed: {ex.Message}";
            return false;
        }
    }

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

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = ResolveCurrentRecipeSourcePath();
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new HeightDeviationRecipe(
                HeightDeviationRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                new HeightDeviationRecipeRule(viewModel.RecipePeakTolerance),
                viewModel.C3DModelTransform,
                CreateCurrentRoiStepRecipe(),
                viewModel.PlaneFlatnessConfigured ? viewModel.CreatePlaneFlatnessRecipeStep() : null,
                viewModel.VolumeConfigured ? viewModel.CreateVolumeRecipeStep() : null,
                viewModel.CrossSectionConfigured ? viewModel.CreateCrossSectionRecipeStep() : null);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke recipe save" : "Recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentLazTwoPointRecipe(string path, bool isSmoke)
    {
        try
        {
            if (lazPointCloud is null || lazTwoPointFirst is null || lazTwoPointSecond is null)
            {
                viewModel.ViewerStatus = "LAZ/LAS two-point recipe save requires a measured LAZ/LAS pair";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(lazPointCloud.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new LazTwoPointMeasurementRecipe(
                LazTwoPointMeasurementRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.LazEntityId,
                    viewModel.LazSampleName,
                    sourceRecipePath,
                    "source-units"),
                new LazTwoPointMeasurementRecipeMeasurement(
                    "sample-extreme-x",
                    Math.Max(2, lazPointCloud.SampledPoints.Length),
                    "source-z-units"),
                new LazTwoPointMeasurementRecipeAcceptance(
                    viewModel.LazTwoPointExpectedDistance,
                    viewModel.LazTwoPointDistanceTolerance,
                    viewModel.LazTwoPointExpectedHeightDelta,
                    viewModel.LazTwoPointHeightDeltaTolerance));

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke LAZ recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"LAZ recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke LAZ recipe save" : "LAZ recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentThicknessRecipe(string path, bool isSmoke)
    {
        try
        {
            if (c3dSample is null || !ShouldSaveCurrentThicknessRecipe())
            {
                viewModel.ViewerStatus = "Thickness recipe save requires a current non-error Thickness Preview";
                return false;
            }

            var step = viewModel.CreateThicknessRecipeStep();
            if (!IsC3DGridRoiInside(step.Roi, c3dSample))
            {
                viewModel.ViewerStatus = "Thickness recipe save requires an ROI inside the loaded C3D grid";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(c3dSample.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DThicknessRecipe(
                C3DThicknessRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                step);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Thickness recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Thickness recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke Thickness recipe save" : "Thickness recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentPointPairDimensionsRecipe(string path, bool isSmoke)
    {
        try
        {
            var step = viewModel.CreatePointPairDimensionsRecipeStep();
            if (c3dSample is null || step is null)
            {
                viewModel.ViewerStatus = "Point pair recipe save requires two selected C3D source cells";
                return false;
            }

            c3dSample.ReadPoint(step.First.Row, step.First.Column);
            c3dSample.ReadPoint(step.Second.Row, step.Second.Column);
            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(c3dSample.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DPointPairDimensionsRecipe(
                C3DPointPairDimensionsRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                viewModel.C3DModelTransform,
                step);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke point pair recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Point pair recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke point pair recipe save" : "Point pair recipe save")} failed: {ex.Message}";
            return false;
        }
    }

    private bool SaveCurrentGapFlushRecipe(string path, bool isSmoke)
    {
        try
        {
            if (c3dSample is null || !viewModel.GapFlushVisible)
            {
                viewModel.ViewerStatus = "Gap / Flush recipe save requires a successful preview";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(c3dSample.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DGapFlushRecipe(
                C3DGapFlushRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                viewModel.C3DModelTransform,
                viewModel.CreateGapFlushRecipeStep());

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Gap / Flush recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Gap / Flush recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke Gap / Flush recipe save" : "Gap / Flush recipe save")} failed: {ex.Message}";
            return false;
        }
    }

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

        var defaultSample = FindDefaultC3DSamplePath();
        return defaultSample is not null ? Path.GetFullPath(defaultSample) : candidate;
    }

}

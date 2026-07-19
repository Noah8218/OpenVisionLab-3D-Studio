using System.IO;
using System.Text.Json;
using System.Windows;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using SharpGL;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    public bool LoadInspectionTaskRecipe(string recipeFileName)
    {
        var path = FindInspectionTaskRecipePath(recipeFileName);
        if (path is null)
        {
            viewModel.ViewerStatus = $"Inspection task recipe was not found: {recipeFileName}";
            return false;
        }

        return ApplyRecipeFile(path, isSmoke: false);
    }

    public bool PreviewC3DWarpage()
    {
        if (c3dSample is null || !viewModel.C3DSampleVisible)
        {
            viewModel.ViewerStatus = "Warpage requires a visible C3D height grid";
            return false;
        }

        if (!viewModel.WarpageConfigured)
        {
            viewModel.ViewerStatus = "Warpage requires one taught C3D grid ROI";
            return false;
        }

        var step = viewModel.CreateWarpageRecipeStep();
        C3DWarpageEvaluation evaluation;
        try
        {
            evaluation = C3DWarpageRule.Evaluate(new C3DWarpageInput(
                step.SourceEntityId,
                c3dSample.Height,
                c3dSample.Width,
                c3dSample.ReadHeightMapValues(),
                step.Roi,
                step.Acceptance,
                step.Unit,
                step.FrameId,
                step.MinimumValidSamples));
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or OverflowException)
        {
            viewModel.ViewerStatus = $"Warpage sample load failed: {exception.Message}";
            return false;
        }

        viewModel.SetWarpagePreview(evaluation);
        RenderNow();
        return evaluation.Result.Status != ResultStatus.Error;
    }

    private bool ApplyC3DWarpageRecipe(string path, bool isSmoke)
    {
        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipe = C3DWarpageRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            c3dSample = C3DHeightGrid.Load(sourcePath, viewModel.C3DMaxRenderedPoints);
            if (!IsC3DGridRoiInside(recipe.Step.Roi, c3dSample))
            {
                throw new InvalidDataException("Warpage recipe ROI is outside the loaded C3D grid.");
            }

            SetC3DSampleStatus();
            planeFlatnessEvaluation = null;
            planeReferenceMeasurement = null;
            ClearWarpageTransientInspectionState();
            viewModel.ClearWarpagePreview();
            viewModel.ClearThicknessPreview();
            viewModel.ClearPlaneFlatnessRecipeStep();
            viewModel.ClearPointPairDimensionsRecipeStep();
            viewModel.ClearGapFlushRecipeStep();
            viewModel.ClearVolumeRecipeStep();
            viewModel.ClearCrossSectionRecipeStep();
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DAlignment(ModelTransform.Identity, "C3D grid-index scalar frame", recipe.Source.Name);
            viewModel.SetWarpageRecipeStep(recipe.Step);
            viewModel.SetWarpageRecipeLoaded(fullRecipePath, recipe.Source.Name, sourcePath, recipe.Source.Unit);
            viewModel.SelectedSelectionMode = MainWindowViewModel.WarpageRoiSelectionMode;
            viewModel.SelectionOverlayVisible = true;

            if (recipe.Step.Enabled && !PreviewC3DWarpage())
            {
                throw new InvalidDataException("Warpage preview failed for the configured grid ROI.");
            }

            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Warpage recipe: {Path.GetFileName(fullRecipePath)}"
                : $"Warpage recipe loaded: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke Warpage recipe" : "Warpage recipe", exception);
        }
    }

    private bool ShouldSaveCurrentWarpageRecipe() =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.WarpageConfigured
        && viewModel.WarpageVisible
        && viewModel.PreviewToolResult.ToolName.Equals(C3DWarpageRule.ToolName, StringComparison.Ordinal)
        && viewModel.PreviewToolResult.Status != ResultStatus.Error;

    private bool SaveCurrentWarpageRecipe(string path, bool isSmoke)
    {
        try
        {
            if (c3dSample is null || !ShouldSaveCurrentWarpageRecipe())
            {
                viewModel.ViewerStatus = "Warpage recipe save requires a current non-error Warpage Preview";
                return false;
            }

            var step = viewModel.CreateWarpageRecipeStep();
            if (!IsC3DGridRoiInside(step.Roi, c3dSample))
            {
                viewModel.ViewerStatus = "Warpage recipe save requires an ROI inside the loaded C3D grid";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(c3dSample.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DWarpageRecipe(
                C3DWarpageRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    step.SourceEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                step);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            SetRecipeValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Warpage recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Warpage recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke Warpage recipe save" : "Warpage recipe save")} failed: {exception.Message}";
            return false;
        }
    }

    private bool TryHandleWarpageRoiPick(Point screenPoint)
    {
        if (viewModel.SelectedSelectionMode != MainWindowViewModel.WarpageRoiSelectionMode)
        {
            return false;
        }

        if (!TryPickC3DPoint(screenPoint, out var point) || c3dSample is null)
        {
            viewModel.SelectedEntity = "C3D Warpage ROI";
            viewModel.PickCoordinate = "(none)";
            viewModel.ViewerStatus = "Warpage ROI pick missed C3D height grid";
            return true;
        }

        viewModel.SetWarpageRoiFromCenter(point.Row, point.Column, c3dSample.Height, c3dSample.Width);
        viewModel.PickCoordinate = FormatC3DPoint(point);
        return true;
    }

    private void DrawWarpageRoi(OpenGL gl)
    {
        if (!viewModel.C3DSampleVisible || c3dSample is null || !viewModel.WarpageConfigured)
        {
            return;
        }

        var roi = viewModel.CreateWarpageRecipeStep().Roi;
        if (!IsC3DGridRoiInside(roi, c3dSample))
        {
            return;
        }

        var height = c3dSample.Mean;
        var lastRow = roi.Row + roi.RowCount - 1;
        var lastColumn = roi.Column + roi.ColumnCount - 1;
        var topLeft = CreateC3DGridDisplayPosition(roi.Row, roi.Column, height);
        var topRight = CreateC3DGridDisplayPosition(roi.Row, lastColumn, height);
        var bottomRight = CreateC3DGridDisplayPosition(lastRow, lastColumn, height);
        var bottomLeft = CreateC3DGridDisplayPosition(lastRow, roi.Column, height);

        gl.LineWidth(3.0f);
        gl.Color(0.96, 0.36, 0.72);
        gl.Begin(OpenGL.GL_LINE_LOOP);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(topRight.X, topRight.Y, topRight.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.Vertex(bottomLeft.X, bottomLeft.Y, bottomLeft.Z);
        gl.End();

        gl.PointSize(7.0f);
        gl.Begin(OpenGL.GL_POINTS);
        gl.Color(1.0, 0.78, 0.16);
        gl.Vertex(topLeft.X, topLeft.Y, topLeft.Z);
        gl.Vertex(bottomRight.X, bottomRight.Y, bottomRight.Z);
        gl.End();
        gl.PointSize(1.0f);
    }

    private void ClearWarpageTransientInspectionState()
    {
        twoPointFirst = null;
        twoPointSecond = null;
        roiStepLeftBounds = null;
        roiStepRightBounds = null;
        roiStepLeftCenter = null;
        roiStepRightCenter = null;
        roiStepLeftAnchor = null;
        roiStepRightAnchor = null;
        ClearRecipeRoiStep();
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
        viewModel.ClearTwoPointMeasurement();
        viewModel.ClearPlaneReferenceMeasurement();
        viewModel.ClearRoiStepMeasurement();
    }

    private static string? FindInspectionTaskRecipePath(string recipeFileName)
    {
        foreach (var root in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(root);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "recipes", recipeFileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}

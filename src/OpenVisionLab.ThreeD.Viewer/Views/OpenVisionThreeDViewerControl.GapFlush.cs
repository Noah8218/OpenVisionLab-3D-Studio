using System.IO;
using System.Numerics;
using System.Text.Json;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.Recipes;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private bool ApplyC3DGapFlushRecipe(
        ViewerRecipeFile recipeFile,
        C3DGapFlushRecipe recipe,
        bool isSmoke)
    {
        try
        {
            var plan = C3DGapFlushRecipeLoadPlan.Create(
                recipeFile,
                recipe,
                viewModel.C3DMaxRenderedPoints);
            c3dSample = plan.Grid;
            return C3DGapFlushRecipeApplyCoordinator.Apply(
                plan,
                viewModel,
                isSmoke,
                SetC3DSampleStatus,
                () =>
                {
                    planeFlatnessEvaluation = null;
                    planeReferenceMeasurement = null;
                },
                ApplyGapFlushRecipeRoiState,
                ApplyGapFlushPreviewOverlay,
                RenderNow);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
        {
            return SetRecipeLoadFailure(isSmoke ? "Smoke Gap / Flush recipe" : "Gap / Flush recipe", exception);
        }
    }

    private bool ShouldSaveCurrentGapFlushRecipe() =>
        C3DGapFlushRecipeSaveCoordinator.CanSave(c3dSample, viewModel);

    private bool SaveCurrentGapFlushRecipe(string path, bool isSmoke) =>
        C3DGapFlushRecipeSaveCoordinator.Save(path, isSmoke, viewModel, c3dSample);

    public bool PreviewC3DGapFlush() =>
        C3DGapFlushRuleCoordinator.Preview(
            c3dSample,
            viewModel,
            ApplyGapFlushPreviewOverlay,
            RenderNow);

    private void ApplyGapFlushRecipeRoiState(C3DGapFlushStep step)
    {
        roiStepLeftRecipeRegion = step.LeftRegion;
        roiStepRightRecipeRegion = step.RightRegion;
        roiStepInteractiveSelection = false;
        roiStepNextPickSetsRight = false;
    }

    private void ApplyGapFlushPreviewOverlay(
        C3DGapFlushStep step,
        GapFlushRegionStats left,
        GapFlushRegionStats right)
    {
        ApplyGapFlushRecipeRoiState(step);
        roiStepLeftBounds = (
            (float)(step.LeftRegion.CenterX - step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterX + step.LeftRegion.HalfWidth),
            (float)(step.LeftRegion.CenterZ - step.LeftRegion.HalfDepth),
            (float)(step.LeftRegion.CenterZ + step.LeftRegion.HalfDepth),
            (float)left.ModelYMean);
        roiStepRightBounds = (
            (float)(step.RightRegion.CenterX - step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterX + step.RightRegion.HalfWidth),
            (float)(step.RightRegion.CenterZ - step.RightRegion.HalfDepth),
            (float)(step.RightRegion.CenterZ + step.RightRegion.HalfDepth),
            (float)right.ModelYMean);
        roiStepLeftCenter = new Vector3(
            (float)step.LeftRegion.CenterX,
            (float)left.ModelYMean,
            (float)step.LeftRegion.CenterZ);
        roiStepRightCenter = new Vector3(
            (float)step.RightRegion.CenterX,
            (float)right.ModelYMean,
            (float)step.RightRegion.CenterZ);
    }
}

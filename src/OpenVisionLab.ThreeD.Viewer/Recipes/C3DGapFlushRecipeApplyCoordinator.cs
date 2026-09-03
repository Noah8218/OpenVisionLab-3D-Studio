using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns the C3D Gap / Flush recipe state/application sequence. The Viewer
/// supplies only source-status, ROI-overlay, and render bridges.
/// </summary>
public static class C3DGapFlushRecipeApplyCoordinator
{
    public static bool Apply(
        C3DGapFlushRecipeLoadPlan plan,
        MainWindowViewModel viewModel,
        bool isSmoke,
        Action applySampleStatus,
        Action clearTransientInspectionState,
        Action<C3DGapFlushStep> applyRecipeRoiState,
        Action<C3DGapFlushStep, GapFlushRegionStats, GapFlushRegionStats> applyPreviewOverlay,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applySampleStatus);
        ArgumentNullException.ThrowIfNull(clearTransientInspectionState);
        ArgumentNullException.ThrowIfNull(applyRecipeRoiState);
        ArgumentNullException.ThrowIfNull(applyPreviewOverlay);
        ArgumentNullException.ThrowIfNull(requestPreviewRender);

        var recipe = plan.Recipe;
        viewModel.RecipeOutputEnabled = recipe.OutputEnabled;
        applySampleStatus();
        clearTransientInspectionState();
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
        viewModel.SetPointPairRecipeLoaded(
            plan.FullRecipePath,
            recipe.Source.Name,
            plan.SourcePath,
            recipe.Source.Unit);
        applyRecipeRoiState(recipe.Step);

        if (isSmoke && recipe.OutputEnabled && recipe.Step.Enabled
            && !C3DGapFlushRuleCoordinator.Preview(
                plan.Grid,
                viewModel,
                applyPreviewOverlay,
                requestPreviewRender))
        {
            throw new InvalidDataException("Gap / Flush preview failed for the configured regions.");
        }

        viewModel.ViewerStatus = isSmoke
            ? $"Smoke Gap / Flush recipe: {Path.GetFileName(plan.FullRecipePath)}"
            : $"Gap / Flush recipe loaded: {Path.GetFileName(plan.FullRecipePath)}";
        return true;
    }
}

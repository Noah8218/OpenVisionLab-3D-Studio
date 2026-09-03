using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns the C3D Thickness recipe state/application sequence. The Viewer only
/// supplies source-status and transient-render bridges.
/// </summary>
public static class C3DThicknessRecipeApplyCoordinator
{
    public static bool Apply(
        C3DThicknessRecipeLoadPlan plan,
        MainWindowViewModel viewModel,
        bool isSmoke,
        Action applySampleStatus,
        Action clearTransientInspectionState,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applySampleStatus);
        ArgumentNullException.ThrowIfNull(clearTransientInspectionState);
        ArgumentNullException.ThrowIfNull(requestPreviewRender);

        var recipe = plan.Recipe;
        viewModel.RecipeOutputEnabled = recipe.OutputEnabled;
        applySampleStatus();
        clearTransientInspectionState();
        viewModel.ClearThicknessPreview();
        viewModel.ClearPlaneFlatnessRecipeStep();
        viewModel.ClearPointPairDimensionsRecipeStep();
        viewModel.ClearGapFlushRecipeStep();
        viewModel.ClearVolumeRecipeStep();
        viewModel.ClearCrossSectionRecipeStep();
        viewModel.UseC3DSmokeScene();
        viewModel.SetC3DAlignment(
            ModelTransform.Identity,
            "C3D grid-index scalar frame",
            recipe.Source.Name);
        viewModel.SetThicknessRecipeStep(recipe.Step);
        viewModel.SetThicknessRecipeLoaded(
            plan.FullRecipePath,
            recipe.Source.Name,
            plan.SourcePath,
            recipe.Source.Unit);
        viewModel.SelectedSelectionMode = MainWindowViewModel.ThicknessRoiSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (isSmoke && recipe.OutputEnabled && recipe.Step.Enabled
            && !C3DThicknessRuleCoordinator.Preview(plan.Grid, viewModel, requestPreviewRender))
        {
            throw new InvalidDataException("Thickness preview failed for the configured grid ROI.");
        }

        viewModel.ViewerStatus = isSmoke
            ? $"Smoke Thickness recipe: {Path.GetFileName(plan.FullRecipePath)}"
            : $"Thickness recipe loaded: {Path.GetFileName(plan.FullRecipePath)}";
        return true;
    }
}

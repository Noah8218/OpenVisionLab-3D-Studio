using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns the C3D Point Pair Dimensions recipe state/application sequence. The
/// Viewer supplies only source-status, transient-state, point-measurement, ROI,
/// and rendering bridges.
/// </summary>
public static class C3DPointPairDimensionsRecipeApplyCoordinator
{
    public static bool Apply(
        C3DPointPairDimensionsRecipeLoadPlan plan,
        MainWindowViewModel viewModel,
        bool isSmoke,
        Action applySampleStatus,
        Action clearTransientInspectionState,
        Action clearRecipeRoiStep,
        Action<HeightGridPoint, HeightGridPoint> applyPointPairMeasurement,
        Action requestPreviewRender)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applySampleStatus);
        ArgumentNullException.ThrowIfNull(clearTransientInspectionState);
        ArgumentNullException.ThrowIfNull(clearRecipeRoiStep);
        ArgumentNullException.ThrowIfNull(applyPointPairMeasurement);
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
        clearRecipeRoiStep();
        viewModel.SetPointPairDimensionsRecipeStep(recipe.Step);
        viewModel.SetPointPairRecipeLoaded(
            plan.FullRecipePath,
            recipe.Source.Name,
            plan.SourcePath,
            recipe.Source.Unit);
        applyPointPairMeasurement(plan.First, plan.Second);
        viewModel.SelectedSelectionMode = MainWindowViewModel.PointPairSelectionMode;
        viewModel.SelectionOverlayVisible = true;

        if (isSmoke && recipe.OutputEnabled && recipe.Step.Enabled
            && !C3DPointPairDimensionsRuleCoordinator.Preview(
                plan.Grid,
                viewModel,
                applyPointPairMeasurement,
                requestPreviewRender))
        {
            throw new InvalidDataException("Point pair dimensions preview failed for the configured source cells.");
        }

        viewModel.ViewerStatus = isSmoke
            ? $"Smoke point pair recipe: {Path.GetFileName(plan.FullRecipePath)}"
            : $"Point pair recipe loaded: {Path.GetFileName(plan.FullRecipePath)}";
        return true;
    }
}

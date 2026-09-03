using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

public static class HeightDeviationRecipeApplyCoordinator
{
    public static bool Apply(
        HeightDeviationRecipeLoadPlan plan,
        MainWindowViewModel viewModel,
        bool isSmoke,
        Action applySampleStatus,
        Action<HeightDeviationRecipeRoiStep?> applyRoiStep,
        Func<bool> previewPlaneFlatness,
        Func<bool> previewVolume,
        Func<bool> previewCrossSection)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applySampleStatus);
        ArgumentNullException.ThrowIfNull(applyRoiStep);
        ArgumentNullException.ThrowIfNull(previewPlaneFlatness);
        ArgumentNullException.ThrowIfNull(previewVolume);
        ArgumentNullException.ThrowIfNull(previewCrossSection);

        applySampleStatus();
        var recipe = plan.Recipe;
        viewModel.ClearPlaneFlatnessRecipeStep();
        viewModel.ClearPointPairDimensionsRecipeStep();
        viewModel.ClearGapFlushRecipeStep();
        viewModel.ClearVolumeRecipeStep();
        viewModel.ClearCrossSectionRecipeStep();
        viewModel.ClearC3DHeightDeviationPreview();
        if (isSmoke && recipe.OutputEnabled)
        {
            viewModel.UseC3DHeightDeviationRuleSmokeScene();
            viewModel.SetC3DHeightDeviationPreview(
                HeightDeviationRuleCoordinator.CreatePreviewResult(
                    plan.Grid,
                    recipe.Source.Name,
                    recipe.Rule.PeakTolerance,
                    recipe.Source.Unit));
        }
        else
        {
            viewModel.UseC3DSmokeScene();
        }

        viewModel.SetRecipeLoaded(
            plan.FullRecipePath,
            recipe.Source.Name,
            plan.SourcePath,
            recipe.Source.Unit,
            recipe.Rule.PeakTolerance);
        viewModel.SetC3DAlignment(
            recipe.Transform ?? ModelTransform.Identity,
            recipe.Transform is null ? "Recipe identity alignment" : "Recipe alignment",
            recipe.Source.Name);
        applyRoiStep(recipe.RoiStep);

        if (recipe.PlaneFlatness is { } planeFlatness)
        {
            viewModel.SetPlaneFlatnessRecipeStep(planeFlatness);
            if (isSmoke && recipe.OutputEnabled && planeFlatness.Enabled)
            {
                previewPlaneFlatness();
            }
        }

        if (recipe.Volume is { } volume)
        {
            viewModel.SetVolumeRecipeStep(volume);
            if (isSmoke && recipe.OutputEnabled && volume.Enabled)
            {
                previewVolume();
            }
        }

        if (recipe.CrossSection is { } crossSection)
        {
            viewModel.SetCrossSectionRecipeStep(crossSection);
            if (isSmoke && recipe.OutputEnabled && crossSection.Enabled)
            {
                previewCrossSection();
            }
        }

        viewModel.ViewerStatus = isSmoke
            ? $"Smoke recipe: {Path.GetFileName(plan.FullRecipePath)}"
            : $"Recipe loaded: {Path.GetFileName(plan.FullRecipePath)}";
        return true;
    }
}

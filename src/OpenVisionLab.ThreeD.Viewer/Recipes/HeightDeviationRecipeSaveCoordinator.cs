using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns Height Deviation recipe construction and persistence without depending on WPF.
/// </summary>
internal static class HeightDeviationRecipeSaveCoordinator
{
    public static bool Save(
        string path,
        bool isSmoke,
        MainWindowViewModel viewModel,
        string sourcePath,
        HeightDeviationRecipeRoiStep? roiStep,
        bool outputEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        try
        {
            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var fullSourcePath = Path.GetFullPath(sourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, fullSourcePath).Replace('\\', '/');
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
                roiStep,
                viewModel.PlaneFlatnessConfigured ? viewModel.CreatePlaneFlatnessRecipeStep() : null,
                viewModel.VolumeConfigured ? viewModel.CreateVolumeRecipeStep() : null,
                viewModel.CrossSectionConfigured ? viewModel.CreateCrossSectionRecipeStep() : null,
                outputEnabled);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
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
}

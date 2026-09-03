using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Point Pair Dimensions save eligibility, recipe construction,
/// relative source mapping, persistence, and saved-state projection without
/// WPF dependencies.
/// </summary>
public static class C3DPointPairDimensionsRecipeSaveCoordinator
{
    public static bool CanSave(C3DHeightGrid? grid, MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return grid is not null
            && viewModel.C3DSampleVisible
            && viewModel.PointPairDimensionsConfigured
            && viewModel.HasPointPairReferences;
    }

    public static bool Save(
        string path,
        bool isSmoke,
        MainWindowViewModel viewModel,
        C3DHeightGrid? grid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(viewModel);

        try
        {
            var step = viewModel.CreatePointPairDimensionsRecipeStep();
            if (grid is null || step is null)
            {
                viewModel.ViewerStatus = "Point pair recipe save requires two selected C3D source cells";
                return false;
            }

            grid.ReadPoint(step.First.Row, step.First.Column);
            grid.ReadPoint(step.Second.Row, step.Second.Column);
            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(grid.SourcePath);
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
                step,
                viewModel.RecipeOutputEnabled);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            viewModel.SetRecipeValidationSummary("Validation: OK");
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
}

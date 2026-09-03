using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Gap / Flush save eligibility, recipe construction, relative
/// source mapping, persistence, and saved-state projection without WPF.
/// </summary>
public static class C3DGapFlushRecipeSaveCoordinator
{
    public static bool CanSave(C3DHeightGrid? grid, MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return grid is not null
            && viewModel.C3DSampleVisible
            && viewModel.GapFlushConfigured
            && (!viewModel.RecipeOutputEnabled || viewModel.GapFlushVisible);
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
            if (grid is null || !CanSave(grid, viewModel))
            {
                viewModel.ViewerStatus = viewModel.RecipeOutputEnabled
                    ? "Gap / Flush recipe save requires a successful preview"
                    : "Gap / Flush recipe save requires configured regions when output is disabled";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(grid.SourcePath);
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
                viewModel.CreateGapFlushRecipeStep(),
                viewModel.RecipeOutputEnabled);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            viewModel.SetRecipeValidationSummary("Validation: OK");
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Gap / Flush recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Gap / Flush recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke Gap / Flush recipe save" : "Gap / Flush recipe save")} failed: {exception.Message}";
            return false;
        }
    }
}

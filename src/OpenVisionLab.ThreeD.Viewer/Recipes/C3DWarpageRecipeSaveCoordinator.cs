using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Warpage save eligibility, recipe construction, relative source
/// mapping, persistence, and saved-state projection without WPF dependencies.
/// </summary>
public static class C3DWarpageRecipeSaveCoordinator
{
    public static bool CanSave(C3DHeightGrid? grid, MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return grid is not null
            && viewModel.C3DSampleVisible
            && viewModel.WarpageConfigured
            && (!viewModel.RecipeOutputEnabled
                || (viewModel.WarpageVisible
                    && viewModel.PreviewToolResult.ToolName.Equals(C3DWarpageRule.ToolName, StringComparison.Ordinal)
                    && viewModel.PreviewToolResult.Status != ResultStatus.Error));
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
                    ? "Warpage recipe save requires a current non-error Warpage Preview"
                    : "Warpage recipe save requires a taught Warpage ROI when output is disabled";
                return false;
            }

            var step = viewModel.CreateWarpageRecipeStep();
            if (!C3DWarpageRecipeLoadPlan.IsRoiInside(step.Roi, grid))
            {
                viewModel.ViewerStatus = "Warpage recipe save requires an ROI inside the loaded C3D grid";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(grid.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DWarpageRecipe(
                C3DWarpageRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    step.SourceEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                step,
                viewModel.RecipeOutputEnabled);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            viewModel.SetRecipeValidationSummary("Validation: OK");
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
}

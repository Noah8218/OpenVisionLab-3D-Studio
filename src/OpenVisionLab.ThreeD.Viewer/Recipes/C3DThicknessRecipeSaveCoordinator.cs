using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns C3D Thickness save eligibility, recipe construction, relative source
/// mapping, persistence, and saved-state projection without WPF dependencies.
/// </summary>
public static class C3DThicknessRecipeSaveCoordinator
{
    public static bool CanSave(C3DHeightGrid? grid, MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return grid is not null
            && viewModel.C3DSampleVisible
            && viewModel.ThicknessConfigured
            && (!viewModel.RecipeOutputEnabled
                || (viewModel.ThicknessVisible
                    && viewModel.PreviewToolResult.ToolName.Equals(C3DThicknessRule.ToolName, StringComparison.Ordinal)
                    && viewModel.PreviewToolResult.Status != OpenVisionLab.ThreeD.Core.ResultStatus.Error));
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
                    ? "Thickness recipe save requires a current non-error Thickness Preview"
                    : "Thickness recipe save requires a taught ROI when output is disabled";
                return false;
            }

            var step = viewModel.CreateThicknessRecipeStep();
            if (!C3DThicknessRecipeLoadPlan.IsRoiInside(step.Roi, grid))
            {
                viewModel.ViewerStatus = "Thickness recipe save requires an ROI inside the loaded C3D grid";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(grid.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new C3DThicknessRecipe(
                C3DThicknessRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.C3DEntityId,
                    viewModel.RecipeSourceName,
                    sourceRecipePath,
                    viewModel.RecipeSourceUnit),
                step,
                viewModel.RecipeOutputEnabled);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            viewModel.SetRecipeValidationSummary("Validation: OK");
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke Thickness recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"Thickness recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke Thickness recipe save" : "Thickness recipe save")} failed: {ex.Message}";
            return false;
        }
    }
}

using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns LAZ/LAS recipe save eligibility, construction, persistence, and
/// saved-state projection without depending on the WPF Viewer control.
/// </summary>
public static class LazTwoPointRecipeSaveCoordinator
{
    public static bool CanSave(
        LazPointCloud? pointCloud,
        bool hasMeasuredPair,
        MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        return pointCloud is not null
            && viewModel.LazSampleVisible
            && (!viewModel.RecipeOutputEnabled
                || hasMeasuredPair);
    }

    public static bool Save(
        string path,
        bool isSmoke,
        MainWindowViewModel viewModel,
        LazPointCloud? pointCloud,
        bool hasMeasuredPair,
        Action setValidationOk)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(setValidationOk);

        try
        {
            if (!CanSave(pointCloud, hasMeasuredPair, viewModel))
            {
                viewModel.ViewerStatus = viewModel.RecipeOutputEnabled
                    ? "LAZ/LAS two-point recipe save requires a measured LAZ/LAS pair"
                    : "LAZ/LAS two-point recipe save requires a loaded point cloud when output is disabled";
                return false;
            }

            var fullRecipePath = Path.GetFullPath(path);
            var recipeDirectory = Path.GetDirectoryName(fullRecipePath)!;
            var sourcePath = Path.GetFullPath(pointCloud!.SourcePath);
            var sourceRecipePath = Path.GetRelativePath(recipeDirectory, sourcePath).Replace('\\', '/');
            var recipe = new LazTwoPointMeasurementRecipe(
                LazTwoPointMeasurementRecipe.SupportedRecipeType,
                "1.0",
                new HeightDeviationRecipeSource(
                    MainWindowViewModel.LazEntityId,
                    viewModel.LazSampleName,
                    sourceRecipePath,
                    "source-units"),
                new LazTwoPointMeasurementRecipeMeasurement(
                    "sample-extreme-x",
                    Math.Max(2, pointCloud.SampledPoints.Length),
                    "source-z-units"),
                new LazTwoPointMeasurementRecipeAcceptance(
                    viewModel.LazTwoPointExpectedDistance,
                    viewModel.LazTwoPointDistanceTolerance,
                    viewModel.LazTwoPointExpectedHeightDelta,
                    viewModel.LazTwoPointHeightDeltaTolerance),
                viewModel.RecipeOutputEnabled);

            recipe.Save(fullRecipePath);
            viewModel.SetRecipeSaved(fullRecipePath);
            setValidationOk();
            viewModel.ViewerStatus = isSmoke
                ? $"Smoke LAZ recipe saved: {Path.GetFileName(fullRecipePath)}"
                : $"LAZ recipe saved: {Path.GetFileName(fullRecipePath)}";
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            viewModel.ViewerStatus = $"{(isSmoke ? "Smoke LAZ recipe save" : "LAZ recipe save")} failed: {ex.Message}";
            return false;
        }
    }
}

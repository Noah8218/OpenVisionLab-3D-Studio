using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns LAZ/LAS recipe state/application policy. The Viewer supplies the
/// decoded point cloud and transient point/Smoke measurement bridges.
/// </summary>
public static class LazTwoPointRecipeApplyCoordinator
{
    public static bool Apply(
        LazTwoPointRecipeLoadPlan plan,
        LazPointCloud pointCloud,
        MainWindowViewModel viewModel,
        bool isSmoke,
        Action clearTransientMeasurement,
        Action<string> applySmokeMeasurement)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(pointCloud);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(clearTransientMeasurement);
        ArgumentNullException.ThrowIfNull(applySmokeMeasurement);

        var recipe = plan.Recipe;
        viewModel.SetLazSampleSource(plan.SourcePath, recipe.Source.Name);
        viewModel.RecipeOutputEnabled = recipe.OutputEnabled;
        viewModel.LazTwoPointExpectedDistance = recipe.Acceptance.ExpectedDistance;
        viewModel.LazTwoPointDistanceTolerance = recipe.Acceptance.DistanceTolerance;
        viewModel.LazTwoPointExpectedHeightDelta = recipe.Acceptance.ExpectedHeightDelta;
        viewModel.LazTwoPointHeightDeltaTolerance = recipe.Acceptance.HeightDeltaTolerance;
        if (isSmoke && recipe.OutputEnabled)
        {
            applySmokeMeasurement(recipe.Measurement.HeightUnit);
        }
        else
        {
            clearTransientMeasurement();
            viewModel.ClearTwoPointMeasurement();
            viewModel.UseLazPointSmokeScene();
        }

        viewModel.SetLazRecipeLoaded(
            plan.FullRecipePath,
            recipe.Source.Name,
            plan.SourcePath);
        viewModel.ViewerStatus = isSmoke
            ? $"Smoke LAZ/LAS recipe: {Path.GetFileName(plan.FullRecipePath)}"
            : $"LAZ/LAS recipe loaded: {Path.GetFileName(plan.FullRecipePath)}";
        return true;
    }
}

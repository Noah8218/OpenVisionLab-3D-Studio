using System.IO;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns Nominal/Actual recipe state/application policy. The Viewer supplies
/// only the mesh-display and explicit Smoke Preview bridges.
/// </summary>
public static class NominalActualComparisonRecipeApplyCoordinator
{
    public static bool Apply(
        NominalActualComparisonRecipeLoadPlan plan,
        MainWindowViewModel viewModel,
        bool isSmoke,
        Func<string, bool> loadNominalMesh,
        Action requestPreview)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(loadNominalMesh);
        ArgumentNullException.ThrowIfNull(requestPreview);

        var recipe = plan.Recipe;
        viewModel.RecipeOutputEnabled = recipe.OutputEnabled;
        if (!loadNominalMesh(plan.Input.NominalSource.Path))
        {
            throw new InvalidDataException("The nominal comparison mesh could not be loaded for display.");
        }

        viewModel.ConfigureNominalActualComparison(plan.Input);
        viewModel.SetNominalActualRecipeLoaded(plan.FullRecipePath);
        if (isSmoke && recipe.OutputEnabled)
        {
            requestPreview();
        }

        viewModel.ViewerStatus = isSmoke
            ? $"Smoke nominal/actual recipe: {Path.GetFileName(plan.FullRecipePath)}"
            : $"Nominal/actual recipe loaded: {Path.GetFileName(plan.FullRecipePath)}";
        return true;
    }
}

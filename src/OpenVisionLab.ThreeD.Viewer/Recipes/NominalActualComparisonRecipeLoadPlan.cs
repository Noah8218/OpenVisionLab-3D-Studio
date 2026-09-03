using System.IO;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns Nominal/Actual recipe-relative input resolution without depending on
/// the WPF Viewer control.
/// </summary>
public sealed record NominalActualComparisonRecipeLoadPlan(
    string FullRecipePath,
    NominalActualComparisonRecipe Recipe,
    OpenVisionLab.ThreeD.Core.NominalActualComparisonInput Input)
{
    public static NominalActualComparisonRecipeLoadPlan Create(
        ViewerRecipeFile recipeFile,
        NominalActualComparisonRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipeFile);
        ArgumentNullException.ThrowIfNull(recipe);

        var fullRecipePath = Path.GetFullPath(recipeFile.Path);
        var input = recipe.ToInput(fullRecipePath);
        return new(fullRecipePath, recipe, input);
    }
}

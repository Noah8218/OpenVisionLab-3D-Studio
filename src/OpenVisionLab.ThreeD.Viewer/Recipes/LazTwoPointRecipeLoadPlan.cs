using System.IO;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns LAZ/LAS recipe-relative source resolution without depending on the
/// Viewer data loader or WPF control.
/// </summary>
public sealed record LazTwoPointRecipeLoadPlan(
    string FullRecipePath,
    string SourcePath,
    LazTwoPointMeasurementRecipe Recipe)
{
    public static LazTwoPointRecipeLoadPlan Create(
        ViewerRecipeFile recipeFile,
        LazTwoPointMeasurementRecipe recipe)
    {
        ArgumentNullException.ThrowIfNull(recipeFile);
        ArgumentNullException.ThrowIfNull(recipe);

        return new(
            Path.GetFullPath(recipeFile.Path),
            recipeFile.ResolveSourcePath(recipe.Source.Path),
            recipe);
    }
}

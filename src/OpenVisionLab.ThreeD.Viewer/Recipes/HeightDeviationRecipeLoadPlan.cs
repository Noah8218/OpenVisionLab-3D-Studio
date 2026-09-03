using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

public sealed record HeightDeviationRecipeLoadPlan(
    string FullRecipePath,
    string SourcePath,
    HeightDeviationRecipe Recipe,
    C3DHeightGrid Grid)
{
    public static HeightDeviationRecipeLoadPlan Create(
        ViewerRecipeFile recipeFile,
        HeightDeviationRecipe recipe,
        int maxRenderedPoints)
    {
        ArgumentNullException.ThrowIfNull(recipeFile);
        ArgumentNullException.ThrowIfNull(recipe);

        var sourcePath = recipeFile.ResolveSourcePath(recipe.Source.Path);
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints);

        return new(
            recipeFile.Path,
            sourcePath,
            recipe,
            grid);
    }
}

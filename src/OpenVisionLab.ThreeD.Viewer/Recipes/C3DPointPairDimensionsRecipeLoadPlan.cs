using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns recipe-relative C3D source loading and authored point-reference reads
/// for a legacy Point Pair Dimensions recipe without depending on WPF or the
/// Viewer control.
/// </summary>
public sealed record C3DPointPairDimensionsRecipeLoadPlan(
    string FullRecipePath,
    string SourcePath,
    C3DPointPairDimensionsRecipe Recipe,
    C3DHeightGrid Grid,
    HeightGridPoint First,
    HeightGridPoint Second)
{
    public static C3DPointPairDimensionsRecipeLoadPlan Create(
        ViewerRecipeFile recipeFile,
        C3DPointPairDimensionsRecipe recipe,
        int maxRenderedPoints)
    {
        ArgumentNullException.ThrowIfNull(recipeFile);
        ArgumentNullException.ThrowIfNull(recipe);

        var sourcePath = recipeFile.ResolveSourcePath(recipe.Source.Path);
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints);
        var first = grid.ReadPoint(recipe.Step.First.Row, recipe.Step.First.Column);
        var second = grid.ReadPoint(recipe.Step.Second.Row, recipe.Step.Second.Column);

        return new(
            recipeFile.Path,
            sourcePath,
            recipe,
            grid,
            first,
            second);
    }
}

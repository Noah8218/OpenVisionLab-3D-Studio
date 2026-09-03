using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns recipe-relative C3D source resolution and grid loading for a legacy
/// Gap / Flush recipe without depending on WPF or the Viewer control.
/// </summary>
public sealed record C3DGapFlushRecipeLoadPlan(
    string FullRecipePath,
    string SourcePath,
    C3DGapFlushRecipe Recipe,
    C3DHeightGrid Grid)
{
    public static C3DGapFlushRecipeLoadPlan Create(
        ViewerRecipeFile recipeFile,
        C3DGapFlushRecipe recipe,
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

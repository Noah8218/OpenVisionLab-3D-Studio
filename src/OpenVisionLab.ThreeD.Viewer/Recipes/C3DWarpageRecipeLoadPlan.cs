using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Owns recipe-relative C3D source loading and authored ROI validation for a
/// legacy Warpage recipe without depending on WPF or the Viewer control.
/// </summary>
public sealed record C3DWarpageRecipeLoadPlan(
    string FullRecipePath,
    string SourcePath,
    C3DWarpageRecipe Recipe,
    C3DHeightGrid Grid)
{
    public static C3DWarpageRecipeLoadPlan Create(
        ViewerRecipeFile recipeFile,
        C3DWarpageRecipe recipe,
        int maxRenderedPoints)
    {
        ArgumentNullException.ThrowIfNull(recipeFile);
        ArgumentNullException.ThrowIfNull(recipe);

        var sourcePath = recipeFile.ResolveSourcePath(recipe.Source.Path);
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints);
        if (!IsRoiInside(recipe.Step.Roi, grid))
        {
            throw new InvalidDataException("Warpage recipe ROI is outside the loaded C3D grid.");
        }

        return new(
            recipeFile.Path,
            sourcePath,
            recipe,
            grid);
    }

    public static bool IsRoiInside(C3DGridRoi roi, C3DHeightGrid grid)
    {
        ArgumentNullException.ThrowIfNull(roi);
        ArgumentNullException.ThrowIfNull(grid);

        return roi.Row >= 0
            && roi.Column >= 0
            && roi.RowCount > 0
            && roi.ColumnCount > 0
            && roi.Row <= grid.Height - roi.RowCount
            && roi.Column <= grid.Width - roi.ColumnCount;
    }
}

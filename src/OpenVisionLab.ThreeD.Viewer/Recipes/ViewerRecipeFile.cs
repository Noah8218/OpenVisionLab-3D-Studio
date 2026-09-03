using System.IO;
using System.Text.Json;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

public sealed record ViewerRecipeFile(string Path, string RecipeType)
{
    public static ViewerRecipeFile Open(string path)
    {
        var fullPath = System.IO.Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        using var document = JsonDocument.Parse(stream);
        var recipeType = document.RootElement.TryGetProperty("recipeType", out var value)
            ? value.GetString() ?? throw new InvalidDataException($"Recipe type is empty: {fullPath}")
            : throw new InvalidDataException($"Recipe type is missing: {fullPath}");
        return new ViewerRecipeFile(fullPath, recipeType);
    }

    public object LoadDocument()
    {
        if (RecipeType.Equals(NominalActualComparisonRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return NominalActualComparisonRecipe.Load(Path);
        }

        if (RecipeType.Equals(LazTwoPointMeasurementRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return LazTwoPointMeasurementRecipe.Load(Path);
        }

        if (RecipeType.Equals(C3DThicknessRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return C3DThicknessRecipe.Load(Path);
        }

        if (RecipeType.Equals(C3DWarpageRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return C3DWarpageRecipe.Load(Path);
        }

        if (RecipeType.Equals(C3DGapFlushRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return C3DGapFlushRecipe.Load(Path);
        }

        if (RecipeType.Equals(C3DPointPairDimensionsRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            return C3DPointPairDimensionsRecipe.Load(Path);
        }

        return HeightDeviationRecipe.Load(Path);
    }

    public string ResolveSourcePath(string sourcePath) =>
        System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(sourcePath)
            ? sourcePath
            : System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path)!, sourcePath));
}

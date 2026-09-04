using System.IO;
using System.Text.Json;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Viewer.Recipes;

/// <summary>
/// Routes loaded recipe documents to the Viewer-owned state adapters and
/// normalizes file/load failures without depending on WPF state.
/// </summary>
internal sealed record ViewerRecipeLoadRoutes(
    Func<ViewerRecipeFile, NominalActualComparisonRecipe, bool, bool> ApplyNominalActual,
    Func<ViewerRecipeFile, LazTwoPointMeasurementRecipe, bool, bool> ApplyLazTwoPoint,
    Func<ViewerRecipeFile, C3DThicknessRecipe, bool, bool> ApplyC3DThickness,
    Func<ViewerRecipeFile, C3DWarpageRecipe, bool, bool> ApplyC3DWarpage,
    Func<ViewerRecipeFile, C3DGapFlushRecipe, bool, bool> ApplyC3DGapFlush,
    Func<ViewerRecipeFile, C3DPointPairDimensionsRecipe, bool, bool> ApplyC3DPointPairDimensions,
    Func<ViewerRecipeFile, HeightDeviationRecipe, bool, bool> ApplyHeightDeviation,
    Func<ViewerRecipeFile, LazTwoPointMeasurementRecipe, bool, CancellationToken, Task<bool>> ApplyLazTwoPointAsync);

internal static class ViewerRecipeLoadCoordinator
{
    public static bool Apply(
        string path,
        bool isSmoke,
        ViewerRecipeLoadRoutes routes,
        Func<string, Exception, bool> handleFailure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(handleFailure);

        ViewerRecipeFile recipeFile;
        try
        {
            recipeFile = ViewerRecipeFile.Open(path);
        }
        catch (Exception exception) when (IsRecipeLoadFailure(exception))
        {
            return handleFailure(isSmoke ? "Smoke recipe" : "Recipe", exception);
        }

        try
        {
            return ApplyDocument(recipeFile, recipeFile.LoadDocument(), isSmoke, routes);
        }
        catch (Exception exception) when (IsRecipeLoadFailure(exception))
        {
            return handleFailure(GetFailureLabel(recipeFile.RecipeType, isSmoke), exception);
        }
    }

    public static async Task<bool> ApplyAsync(
        string path,
        bool isSmoke,
        ViewerRecipeLoadRoutes routes,
        Func<string, Exception, bool> handleFailure,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(routes);
        ArgumentNullException.ThrowIfNull(handleFailure);
        cancellationToken.ThrowIfCancellationRequested();

        ViewerRecipeFile recipeFile;
        try
        {
            recipeFile = ViewerRecipeFile.Open(path);
        }
        catch (Exception exception) when (IsRecipeLoadFailure(exception))
        {
            return handleFailure(isSmoke ? "Smoke recipe" : "Recipe", exception);
        }

        try
        {
            var document = recipeFile.LoadDocument();
            cancellationToken.ThrowIfCancellationRequested();
            return document is LazTwoPointMeasurementRecipe lazRecipe
                ? await routes.ApplyLazTwoPointAsync(recipeFile, lazRecipe, isSmoke, cancellationToken)
                : ApplyDocument(recipeFile, document, isSmoke, routes);
        }
        catch (Exception exception) when (IsRecipeLoadFailure(exception))
        {
            return handleFailure(GetFailureLabel(recipeFile.RecipeType, isSmoke), exception);
        }
    }

    private static bool ApplyDocument(
        ViewerRecipeFile recipeFile,
        object document,
        bool isSmoke,
        ViewerRecipeLoadRoutes routes) =>
        document switch
        {
            NominalActualComparisonRecipe recipe => routes.ApplyNominalActual(recipeFile, recipe, isSmoke),
            LazTwoPointMeasurementRecipe recipe => routes.ApplyLazTwoPoint(recipeFile, recipe, isSmoke),
            C3DThicknessRecipe recipe => routes.ApplyC3DThickness(recipeFile, recipe, isSmoke),
            C3DWarpageRecipe recipe => routes.ApplyC3DWarpage(recipeFile, recipe, isSmoke),
            C3DGapFlushRecipe recipe => routes.ApplyC3DGapFlush(recipeFile, recipe, isSmoke),
            C3DPointPairDimensionsRecipe recipe => routes.ApplyC3DPointPairDimensions(recipeFile, recipe, isSmoke),
            HeightDeviationRecipe recipe => routes.ApplyHeightDeviation(recipeFile, recipe, isSmoke),
            _ => throw new InvalidDataException($"Unsupported recipe type: {recipeFile.RecipeType}")
        };

    private static string GetFailureLabel(string recipeType, bool isSmoke)
    {
        var label = recipeType.Equals(NominalActualComparisonRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
            ? "nominal/actual recipe"
            : recipeType.Equals(LazTwoPointMeasurementRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
                ? "LAZ/LAS recipe"
                : recipeType.Equals(C3DThicknessRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
                    ? "Thickness recipe"
                    : recipeType.Equals(C3DWarpageRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
                        ? "Warpage recipe"
                        : recipeType.Equals(C3DGapFlushRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
                            ? "Gap / Flush recipe"
                            : recipeType.Equals(C3DPointPairDimensionsRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
                                ? "point pair recipe"
                                : "recipe";
        return isSmoke
            ? $"Smoke {label}"
            : char.ToUpperInvariant(label[0]) + label[1..];
    }

    private static bool IsRecipeLoadFailure(Exception exception) =>
        exception is IOException
            or InvalidDataException
            or UnauthorizedAccessException
            or JsonException
            or ArgumentException
            or NotSupportedException;
}

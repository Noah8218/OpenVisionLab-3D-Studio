namespace OpenVisionLab.ThreeD.Viewer.Recipes;

internal enum ViewerRecipeSaveRoute
{
    HeightDeviation,
    NominalActual,
    LazTwoPoint,
    Warpage,
    Thickness,
    GapFlush,
    PointPairDimensions
}

/// <summary>
/// Resolves the current recipe save route and dialog file name from a single
/// View-provided capability snapshot. It owns no WPF state or persistence.
/// </summary>
internal sealed record ViewerRecipeSavePlan(
    ViewerRecipeSaveRoute Route,
    string DefaultFileName)
{
    public static ViewerRecipeSavePlan Resolve(
        bool canSaveNominalActual,
        bool canSaveLazTwoPoint,
        bool canSaveWarpage,
        bool canSaveThickness,
        bool canSaveGapFlush,
        bool canSavePointPairDimensions)
    {
        if (canSaveNominalActual)
        {
            return new(ViewerRecipeSaveRoute.NominalActual, "nominal-actual-surface-deviation.recipe.json");
        }

        if (canSaveLazTwoPoint)
        {
            return new(ViewerRecipeSaveRoute.LazTwoPoint, "laz-two-point-measurement.recipe.json");
        }

        if (canSaveWarpage)
        {
            return new(ViewerRecipeSaveRoute.Warpage, "c3d-warpage.recipe.json");
        }

        if (canSaveThickness)
        {
            return new(ViewerRecipeSaveRoute.Thickness, "c3d-thickness.recipe.json");
        }

        if (canSaveGapFlush)
        {
            return new(ViewerRecipeSaveRoute.GapFlush, "c3d-gap-flush.recipe.json");
        }

        if (canSavePointPairDimensions)
        {
            return new(ViewerRecipeSaveRoute.PointPairDimensions, "c3d-point-pair-dimensions.recipe.json");
        }

        return new(ViewerRecipeSaveRoute.HeightDeviation, "c3d-height-deviation.recipe.json");
    }
}

using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DGapFlushRecipe(
    string RecipeType,
    string Version,
    HeightDeviationRecipeSource Source,
    ModelTransform? Transform,
    C3DGapFlushStep Step)
{
    public const string SupportedRecipeType = "c3d-gap-flush";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static C3DGapFlushRecipe Load(string path)
    {
        using var stream = File.OpenRead(path);
        var recipe = JsonSerializer.Deserialize<C3DGapFlushRecipe>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Recipe is empty: {path}");
        Validate(recipe);
        return recipe;
    }

    public void Save(string path)
    {
        Validate(this);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    private static void Validate(C3DGapFlushRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.RecipeType)
            || !recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(recipe.Version))
        {
            throw new InvalidDataException("Gap / Flush recipe type and version are required.");
        }

        if (recipe.Source is null
            || string.IsNullOrWhiteSpace(recipe.Source.EntityId)
            || string.IsNullOrWhiteSpace(recipe.Source.Name)
            || string.IsNullOrWhiteSpace(recipe.Source.Path)
            || string.IsNullOrWhiteSpace(recipe.Source.Unit))
        {
            throw new InvalidDataException("Gap / Flush source entity, name, path, and unit are required.");
        }

        var step = recipe.Step ?? throw new InvalidDataException("Gap / Flush step is required.");
        if (string.IsNullOrWhiteSpace(step.Id)
            || string.IsNullOrWhiteSpace(step.SourceEntityId)
            || !step.SourceEntityId.Equals(recipe.Source.EntityId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(step.LeftReferenceId)
            || string.IsNullOrWhiteSpace(step.RightReferenceId)
            || step.LeftReferenceId.Equals(step.RightReferenceId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(step.GapUnit)
            || string.IsNullOrWhiteSpace(step.FlushUnit)
            || step.MaxSampledPoints < 2)
        {
            throw new InvalidDataException("Gap / Flush step IDs, distinct references, units, and sample budget are required.");
        }

        ValidateRegion(step.LeftRegion, "left");
        ValidateRegion(step.RightRegion, "right");
        ValidateAcceptance(step.Acceptance);
        if (recipe.Transform is { } transform
            && (!double.IsFinite(transform.TranslateX)
                || !double.IsFinite(transform.TranslateY)
                || !double.IsFinite(transform.TranslateZ)
                || !double.IsFinite(transform.RotateXDegrees)
                || !double.IsFinite(transform.RotateYDegrees)
                || !double.IsFinite(transform.RotateZDegrees)
                || !double.IsFinite(transform.Scale)
                || transform.Scale <= 0.0))
        {
            throw new InvalidDataException("Gap / Flush transform values must be finite and scale must be positive.");
        }
    }

    private static void ValidateRegion(HeightDeviationRecipeRoiRegion? region, string name)
    {
        if (region is null
            || !double.IsFinite(region.CenterX)
            || !double.IsFinite(region.CenterZ)
            || !double.IsFinite(region.HalfWidth)
            || !double.IsFinite(region.HalfDepth)
            || region.HalfWidth <= 0.0
            || region.HalfDepth <= 0.0)
        {
            throw new InvalidDataException($"Gap / Flush {name} ROI values must be finite and half sizes positive.");
        }
    }

    private static void ValidateAcceptance(C3DGapFlushAcceptance? acceptance)
    {
        if (acceptance is null
            || !double.IsFinite(acceptance.ExpectedGap)
            || !double.IsFinite(acceptance.GapTolerance)
            || acceptance.GapTolerance < 0.0
            || !double.IsFinite(acceptance.ExpectedFlush)
            || !double.IsFinite(acceptance.FlushTolerance)
            || acceptance.FlushTolerance < 0.0)
        {
            throw new InvalidDataException("Gap / Flush expected values must be finite and tolerances non-negative.");
        }
    }
}

public sealed record C3DGapFlushStep(
    string Id,
    string SourceEntityId,
    string LeftReferenceId,
    string RightReferenceId,
    HeightDeviationRecipeRoiRegion LeftRegion,
    HeightDeviationRecipeRoiRegion RightRegion,
    C3DGapFlushAcceptance Acceptance,
    string GapUnit,
    string FlushUnit,
    int MaxSampledPoints,
    bool Enabled = true);

public sealed record C3DGapFlushAcceptance(
    double ExpectedGap,
    double GapTolerance,
    double ExpectedFlush,
    double FlushTolerance);

using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record HeightDeviationRecipe(
    string RecipeType,
    string Version,
    HeightDeviationRecipeSource Source,
    HeightDeviationRecipeRule Rule,
    ModelTransform? Transform = null,
    HeightDeviationRecipeRoiStep? RoiStep = null)
{
    public const string SupportedRecipeType = "c3d-height-deviation";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static HeightDeviationRecipe Load(string path)
    {
        using var stream = File.OpenRead(path);
        var recipe = JsonSerializer.Deserialize<HeightDeviationRecipe>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Recipe is empty: {path}");

        if (string.IsNullOrWhiteSpace(recipe.RecipeType)
            || !recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported recipe type: {recipe.RecipeType}");
        }

        if (recipe.Source is null)
        {
            throw new InvalidDataException("Recipe source is required.");
        }

        if (recipe.Rule is null)
        {
            throw new InvalidDataException("Recipe rule is required.");
        }

        if (recipe.Rule.PeakTolerance <= 0.0 || !double.IsFinite(recipe.Rule.PeakTolerance))
        {
            throw new InvalidDataException("Peak tolerance must be a positive finite value.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Source.Path))
        {
            throw new InvalidDataException("Recipe source path is required.");
        }

        if (recipe.Transform is { } transform)
        {
            ValidateTransform(transform);
        }

        if (recipe.RoiStep is { } roiStep)
        {
            ValidateRoiStep(roiStep);
        }

        return recipe;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    private static void ValidateTransform(ModelTransform transform)
    {
        if (!double.IsFinite(transform.TranslateX)
            || !double.IsFinite(transform.TranslateY)
            || !double.IsFinite(transform.TranslateZ)
            || !double.IsFinite(transform.RotateXDegrees)
            || !double.IsFinite(transform.RotateYDegrees)
            || !double.IsFinite(transform.RotateZDegrees)
            || !double.IsFinite(transform.Scale)
            || transform.Scale <= 0.0)
        {
            throw new InvalidDataException("Recipe transform values must be finite and scale must be positive.");
        }
    }

    private static void ValidateRoiStep(HeightDeviationRecipeRoiStep roiStep)
    {
        if (string.IsNullOrWhiteSpace(roiStep.Mode))
        {
            throw new InvalidDataException("ROI step mode is required.");
        }

        if (roiStep.MaxSampledPoints <= 0)
        {
            throw new InvalidDataException("ROI step max sampled points must be positive.");
        }

        ValidateRoiRegion(roiStep.Left, "left");
        ValidateRoiRegion(roiStep.Right, "right");
    }

    private static void ValidateRoiRegion(HeightDeviationRecipeRoiRegion? region, string name)
    {
        if (region is null)
        {
            throw new InvalidDataException($"ROI step {name} region is required.");
        }

        if (!double.IsFinite(region.CenterX)
            || !double.IsFinite(region.CenterZ)
            || !double.IsFinite(region.HalfWidth)
            || !double.IsFinite(region.HalfDepth)
            || region.HalfWidth <= 0.0
            || region.HalfDepth <= 0.0)
        {
            throw new InvalidDataException($"ROI step {name} region values must be finite and half sizes must be positive.");
        }
    }
}

public sealed record HeightDeviationRecipeSource(
    string EntityId,
    string Name,
    string Path,
    string Unit);

public sealed record HeightDeviationRecipeRule(double PeakTolerance);

public sealed record HeightDeviationRecipeRoiStep(
    string Mode,
    HeightDeviationRecipeRoiRegion Left,
    HeightDeviationRecipeRoiRegion Right,
    int MaxSampledPoints);

public sealed record HeightDeviationRecipeRoiRegion(
    double CenterX,
    double CenterZ,
    double HalfWidth,
    double HalfDepth);

public sealed record LazTwoPointMeasurementRecipe(
    string RecipeType,
    string Version,
    HeightDeviationRecipeSource Source,
    LazTwoPointMeasurementRecipeMeasurement Measurement,
    LazTwoPointMeasurementRecipeAcceptance Acceptance)
{
    public const string SupportedRecipeType = "laz-two-point-measurement";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static LazTwoPointMeasurementRecipe Load(string path)
    {
        using var stream = File.OpenRead(path);
        var recipe = JsonSerializer.Deserialize<LazTwoPointMeasurementRecipe>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Recipe is empty: {path}");

        if (string.IsNullOrWhiteSpace(recipe.RecipeType)
            || !recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported recipe type: {recipe.RecipeType}");
        }

        if (recipe.Source is null)
        {
            throw new InvalidDataException("Recipe source is required.");
        }

        if (recipe.Measurement is null)
        {
            throw new InvalidDataException("LAZ/LAS two-point measurement settings are required.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Source.Path))
        {
            throw new InvalidDataException("Recipe source path is required.");
        }

        if (!recipe.Measurement.Selection.Equals("sample-extreme-x", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("LAZ/LAS two-point recipe currently supports only sample-extreme-x selection.");
        }

        if (recipe.Measurement.MaxSampledPoints <= 1)
        {
            throw new InvalidDataException("LAZ/LAS two-point max sampled points must be greater than one.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Measurement.HeightUnit))
        {
            throw new InvalidDataException("LAZ/LAS two-point height unit is required.");
        }

        ValidateAcceptance(recipe.Acceptance);
        return recipe;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    private static void ValidateAcceptance(LazTwoPointMeasurementRecipeAcceptance? acceptance)
    {
        if (acceptance is null)
        {
            throw new InvalidDataException("LAZ/LAS two-point acceptance criteria are required.");
        }

        if (!double.IsFinite(acceptance.ExpectedDistance)
            || !double.IsFinite(acceptance.DistanceTolerance)
            || acceptance.DistanceTolerance < 0.0
            || !double.IsFinite(acceptance.ExpectedHeightDelta)
            || !double.IsFinite(acceptance.HeightDeltaTolerance)
            || acceptance.HeightDeltaTolerance < 0.0)
        {
            throw new InvalidDataException("LAZ/LAS two-point acceptance values must be finite and tolerances must be non-negative.");
        }
    }
}

public sealed record LazTwoPointMeasurementRecipeMeasurement(
    string Selection,
    int MaxSampledPoints,
    string HeightUnit);

public sealed record LazTwoPointMeasurementRecipeAcceptance(
    double ExpectedDistance,
    double DistanceTolerance,
    double ExpectedHeightDelta,
    double HeightDeltaTolerance);

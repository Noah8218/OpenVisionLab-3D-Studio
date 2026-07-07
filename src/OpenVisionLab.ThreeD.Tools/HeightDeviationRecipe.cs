using System.Text.Json;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record HeightDeviationRecipe(
    string RecipeType,
    string Version,
    HeightDeviationRecipeSource Source,
    HeightDeviationRecipeRule Rule)
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

        if (!recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported recipe type: {recipe.RecipeType}");
        }

        if (recipe.Rule.PeakTolerance <= 0.0 || !double.IsFinite(recipe.Rule.PeakTolerance))
        {
            throw new InvalidDataException("Peak tolerance must be a positive finite value.");
        }

        if (string.IsNullOrWhiteSpace(recipe.Source.Path))
        {
            throw new InvalidDataException("Recipe source path is required.");
        }

        return recipe;
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }
}

public sealed record HeightDeviationRecipeSource(
    string EntityId,
    string Name,
    string Path,
    string Unit);

public sealed record HeightDeviationRecipeRule(double PeakTolerance);

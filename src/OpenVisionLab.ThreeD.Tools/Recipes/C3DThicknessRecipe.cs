using System.Text.Json;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// A single taught C3D grid ROI evaluated as declared thickness scalar values.
/// The recipe intentionally records grid coordinates, not a visual viewport transform.
/// </summary>
public sealed record C3DThicknessRecipe(
    string RecipeType,
    string Version,
    HeightDeviationRecipeSource Source,
    C3DThicknessStep Step)
{
    public const string SupportedRecipeType = "c3d-thickness";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static C3DThicknessRecipe Load(string path)
    {
        using var stream = File.OpenRead(path);
        var recipe = JsonSerializer.Deserialize<C3DThicknessRecipe>(stream, JsonOptions)
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

    private static void Validate(C3DThicknessRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.RecipeType)
            || !recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(recipe.Version))
        {
            throw new InvalidDataException("C3D Thickness recipe type and version are required.");
        }

        if (recipe.Source is null
            || string.IsNullOrWhiteSpace(recipe.Source.EntityId)
            || string.IsNullOrWhiteSpace(recipe.Source.Name)
            || string.IsNullOrWhiteSpace(recipe.Source.Path)
            || string.IsNullOrWhiteSpace(recipe.Source.Unit))
        {
            throw new InvalidDataException("C3D Thickness source entity, name, path, and unit are required.");
        }

        var step = recipe.Step ?? throw new InvalidDataException("C3D Thickness step is required.");
        if (string.IsNullOrWhiteSpace(step.Id)
            || string.IsNullOrWhiteSpace(step.SourceEntityId)
            || !step.SourceEntityId.Equals(recipe.Source.EntityId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(step.RoiReferenceId)
            || string.IsNullOrWhiteSpace(step.Unit)
            || string.IsNullOrWhiteSpace(step.FrameId)
            || step.MinimumValidSamples <= 0)
        {
            throw new InvalidDataException("C3D Thickness step IDs, unit, frame, and minimum sample count are required.");
        }

        if (step.Roi is null
            || step.Roi.Row < 0
            || step.Roi.Column < 0
            || step.Roi.RowCount <= 0
            || step.Roi.ColumnCount <= 0)
        {
            throw new InvalidDataException("C3D Thickness ROI requires non-negative grid origin and positive row/column counts.");
        }

        if (step.Acceptance is null
            || !double.IsFinite(step.Acceptance.MinimumThickness)
            || !double.IsFinite(step.Acceptance.MaximumThickness)
            || step.Acceptance.MinimumThickness > step.Acceptance.MaximumThickness)
        {
            throw new InvalidDataException("C3D Thickness lower and upper limits must be finite and ordered.");
        }
    }
}

public sealed record C3DThicknessStep(
    string Id,
    string SourceEntityId,
    string RoiReferenceId,
    C3DGridRoi Roi,
    C3DThicknessAcceptance Acceptance,
    string Unit,
    string FrameId,
    int MinimumValidSamples = 1,
    bool Enabled = true);

public sealed record C3DGridRoi(int Row, int Column, int RowCount, int ColumnCount);

public sealed record C3DThicknessAcceptance(double MinimumThickness, double MaximumThickness);

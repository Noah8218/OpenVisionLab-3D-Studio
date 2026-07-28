using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DPointPairDimensionsRecipe(
    string RecipeType,
    string Version,
    HeightDeviationRecipeSource Source,
    ModelTransform? Transform,
    C3DPointPairDimensionsStep Step)
{
    public const string SupportedRecipeType = "c3d-point-pair-dimensions";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static C3DPointPairDimensionsRecipe Load(string path)
    {
        using var stream = File.OpenRead(path);
        var recipe = JsonSerializer.Deserialize<C3DPointPairDimensionsRecipe>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Recipe is empty: {path}");

        ValidateRecipe(recipe);
        return recipe;
    }

    public void Save(string path)
    {
        ValidateRecipe(this);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    private static void ValidateRecipe(C3DPointPairDimensionsRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.RecipeType)
            || !recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Unsupported recipe type: {recipe.RecipeType}");
        }

        if (string.IsNullOrWhiteSpace(recipe.Version))
        {
            throw new InvalidDataException("Point pair recipe version is required.");
        }

        if (recipe.Source is null
            || string.IsNullOrWhiteSpace(recipe.Source.EntityId)
            || string.IsNullOrWhiteSpace(recipe.Source.Name)
            || string.IsNullOrWhiteSpace(recipe.Source.Path)
            || string.IsNullOrWhiteSpace(recipe.Source.Unit))
        {
            throw new InvalidDataException("Point pair recipe source entity, name, path, and unit are required.");
        }

        ValidateStep(recipe.Step, recipe.Source.EntityId);
        if (recipe.Transform is { } transform)
        {
            ValidateTransform(transform);
        }
    }

    private static void ValidateStep(C3DPointPairDimensionsStep? step, string sourceEntityId)
    {
        if (step is null || string.IsNullOrWhiteSpace(step.Id))
        {
            throw new InvalidDataException("Point pair dimensions step ID is required.");
        }

        if (string.IsNullOrWhiteSpace(step.SourceEntityId)
            || !step.SourceEntityId.Equals(sourceEntityId, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Point pair step source entity ID must match the recipe source entity ID.");
        }

        ValidateReference(step.First, "first");
        ValidateReference(step.Second, "second");
        if (step.First.Id.Equals(step.Second.Id, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Point pair reference IDs must be distinct.");
        }

        if (step.First.Row == step.Second.Row && step.First.Column == step.Second.Column)
        {
            throw new InvalidDataException("Point pair references must select different C3D cells.");
        }

        if (string.IsNullOrWhiteSpace(step.Unit))
        {
            throw new InvalidDataException("Point pair dimensions unit is required.");
        }

        ValidateAcceptance(step.Acceptance);
    }

    private static void ValidateReference(C3DGridPointReference? reference, string name)
    {
        if (reference is null
            || string.IsNullOrWhiteSpace(reference.Id)
            || reference.Row < 0
            || reference.Column < 0)
        {
            throw new InvalidDataException($"Point pair {name} reference requires an ID and non-negative row/column.");
        }
    }

    private static void ValidateAcceptance(C3DPointPairDimensionsAcceptance? acceptance)
    {
        if (acceptance is null
            || !double.IsFinite(acceptance.ExpectedDistance)
            || acceptance.ExpectedDistance < 0.0
            || !double.IsFinite(acceptance.DistanceTolerance)
            || acceptance.DistanceTolerance < 0.0
            || !double.IsFinite(acceptance.ExpectedWidth)
            || acceptance.ExpectedWidth < 0.0
            || !double.IsFinite(acceptance.WidthTolerance)
            || acceptance.WidthTolerance < 0.0
            || !double.IsFinite(acceptance.ExpectedElevationAngleDegrees)
            || acceptance.ExpectedElevationAngleDegrees < -90.0
            || acceptance.ExpectedElevationAngleDegrees > 90.0
            || !double.IsFinite(acceptance.ElevationAngleToleranceDegrees)
            || acceptance.ElevationAngleToleranceDegrees < 0.0)
        {
            throw new InvalidDataException("Point pair expected values must be finite, length values non-negative, angle within -90..90 degrees, and tolerances non-negative.");
        }
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
            throw new InvalidDataException("Point pair recipe transform values must be finite and scale must be positive.");
        }
    }
}

public sealed record C3DPointPairDimensionsStep(
    string Id,
    string SourceEntityId,
    C3DGridPointReference First,
    C3DGridPointReference Second,
    C3DPointPairDimensionsAcceptance Acceptance,
    string Unit,
    bool Enabled = true);

public sealed record C3DGridPointReference(string Id, int Row, int Column);

public sealed record C3DPointPairDimensionsAcceptance(
    double ExpectedDistance,
    double DistanceTolerance,
    double ExpectedWidth,
    double WidthTolerance,
    double ExpectedElevationAngleDegrees,
    double ElevationAngleToleranceDegrees);

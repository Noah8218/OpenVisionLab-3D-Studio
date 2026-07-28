using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record NominalActualComparisonRecipe(
    string RecipeType,
    string Version,
    NominalActualComparisonRecipeStep Step)
{
    public const string SupportedRecipeType = "nominal-actual-surface-deviation";
    public const string FullQuerySampling = "full-query";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static NominalActualComparisonRecipe FromInput(
        NominalActualComparisonInput input,
        string recipePath)
    {
        ArgumentNullException.ThrowIfNull(input);
        var recipeDirectory = Path.GetDirectoryName(Path.GetFullPath(recipePath))!;
        return new NominalActualComparisonRecipe(
            SupportedRecipeType,
            "1.0",
            new NominalActualComparisonRecipeStep(
                input.StepId,
                NominalActualComparisonInput.Direction,
                ToRecipeFile(input.ActualSource, recipeDirectory),
                ToRecipeFile(input.NominalSource, recipeDirectory),
                ToRecipeFile(input.QuerySource, recipeDirectory),
                input.Unit,
                input.FrameId,
                input.AlignmentId,
                FullQuerySampling,
                input.LowerTolerance,
                input.UpperTolerance));
    }

    public static NominalActualComparisonRecipe Load(string path)
    {
        using var stream = File.OpenRead(path);
        var recipe = JsonSerializer.Deserialize<NominalActualComparisonRecipe>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Recipe is empty: {path}");
        Validate(recipe);
        return recipe;
    }

    public NominalActualComparisonInput ToInput(string recipePath)
    {
        Validate(this);
        var recipeDirectory = Path.GetDirectoryName(Path.GetFullPath(recipePath))!;
        return new NominalActualComparisonInput(
            Step.Id,
            ToIdentity(Step.ActualSource, recipeDirectory),
            ToIdentity(Step.NominalSource, recipeDirectory),
            ToIdentity(Step.QuerySource, recipeDirectory),
            Step.Unit,
            Step.FrameId,
            Step.AlignmentId,
            Step.LowerTolerance,
            Step.UpperTolerance);
    }

    public void Save(string path)
    {
        Validate(this);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, this, JsonOptions);
    }

    private static void Validate(NominalActualComparisonRecipe recipe)
    {
        if (string.IsNullOrWhiteSpace(recipe.RecipeType)
            || !recipe.RecipeType.Equals(SupportedRecipeType, StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(recipe.Version))
        {
            throw new InvalidDataException("Nominal/actual recipe type and version are required.");
        }

        var step = recipe.Step ?? throw new InvalidDataException("Nominal/actual recipe step is required.");
        if (string.IsNullOrWhiteSpace(step.Id)
            || string.IsNullOrWhiteSpace(step.Direction)
            || !step.Direction.Equals(NominalActualComparisonInput.Direction, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(step.Unit)
            || string.IsNullOrWhiteSpace(step.FrameId)
            || string.IsNullOrWhiteSpace(step.AlignmentId)
            || string.IsNullOrWhiteSpace(step.EvaluationSampling)
            || !step.EvaluationSampling.Equals(FullQuerySampling, StringComparison.Ordinal)
            || !double.IsFinite(step.LowerTolerance)
            || !double.IsFinite(step.UpperTolerance)
            || step.LowerTolerance >= 0.0
            || step.UpperTolerance <= 0.0
            || step.LowerTolerance >= step.UpperTolerance)
        {
            throw new InvalidDataException(
                "Nominal/actual step requires an ID, ActualToNominal direction, units, frame, alignment, full-query sampling, and ordered zero-centred tolerances.");
        }

        ValidateFile(step.ActualSource, "actual");
        ValidateFile(step.NominalSource, "nominal");
        ValidateFile(step.QuerySource, "query");
        if (new[] { step.ActualSource.Id, step.NominalSource.Id, step.QuerySource.Id }
            .Distinct(StringComparer.Ordinal).Count() != 3)
        {
            throw new InvalidDataException("Nominal/actual recipe file IDs must be distinct.");
        }
    }

    private static void ValidateFile(NominalActualComparisonRecipeFile? file, string role)
    {
        if (file is null
            || string.IsNullOrWhiteSpace(file.Id)
            || string.IsNullOrWhiteSpace(file.Name)
            || string.IsNullOrWhiteSpace(file.Path)
            || file.ByteLength <= 0
            || file.Sha256.Length != 64
            || file.Sha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                $"Nominal/actual {role} file requires an ID, name, path, positive byte length, and SHA-256.");
        }
    }

    private static NominalActualComparisonRecipeFile ToRecipeFile(
        NominalActualFileIdentity identity,
        string recipeDirectory) =>
        new(
            identity.Id,
            identity.Name,
            Path.GetRelativePath(recipeDirectory, Path.GetFullPath(identity.Path)).Replace('\\', '/'),
            identity.ByteLength,
            identity.Sha256);

    private static NominalActualFileIdentity ToIdentity(
        NominalActualComparisonRecipeFile file,
        string recipeDirectory)
    {
        var path = Path.IsPathRooted(file.Path)
            ? Path.GetFullPath(file.Path)
            : Path.GetFullPath(Path.Combine(recipeDirectory, file.Path));
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            throw new FileNotFoundException($"Nominal/actual recipe source is missing: {file.Id}", path);
        }

        if (info.Length != file.ByteLength)
        {
            throw new InvalidDataException(
                $"Nominal/actual recipe source byte length does not match: {file.Id}");
        }

        return new NominalActualFileIdentity(file.Id, file.Name, path, file.ByteLength, file.Sha256);
    }
}

public sealed record NominalActualComparisonRecipeStep(
    string Id,
    string Direction,
    NominalActualComparisonRecipeFile ActualSource,
    NominalActualComparisonRecipeFile NominalSource,
    NominalActualComparisonRecipeFile QuerySource,
    string Unit,
    string FrameId,
    string AlignmentId,
    string EvaluationSampling,
    double LowerTolerance,
    double UpperTolerance);

public sealed record NominalActualComparisonRecipeFile(
    string Id,
    string Name,
    string Path,
    long ByteLength,
    string Sha256);

using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record NominalActualComparisonRecipe(
    string RecipeType,
    string Version,
    NominalActualComparisonRecipeStep Step,
    bool OutputEnabled = true)
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
        var recipeDirectory = GetRecipeDirectory(recipePath);
        return new NominalActualComparisonRecipe(
            SupportedRecipeType,
            input.Alignment is null ? "1.0" : "1.1",
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
                input.UpperTolerance,
                ToRecipeAlignment(input.Alignment, recipeDirectory)));
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
        var recipeDirectory = GetRecipeDirectory(recipePath);
        var input = new NominalActualComparisonInput(
            Step.Id,
            ToIdentity(Step.ActualSource, recipeDirectory),
            ToIdentity(Step.NominalSource, recipeDirectory),
            ToIdentity(Step.QuerySource, recipeDirectory),
            Step.Unit,
            Step.FrameId,
            Step.AlignmentId,
            Step.LowerTolerance,
            Step.UpperTolerance,
            ToRuntimeAlignment(Step.Alignment, recipeDirectory));
        if (input.Alignment is not null)
        {
            NominalActualComparisonExecutor.ValidateAlignment(input);
        }

        return input;
    }

    public void Save(string path)
    {
        Validate(this);
        LegacyRecipeJsonStore.Save(path, this, JsonOptions);
    }

    private static string GetRecipeDirectory(string recipePath) =>
        Path.GetDirectoryName(Path.GetFullPath(recipePath))!;

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

        if (step.Alignment is null)
        {
            if (!NominalActualComparisonInput.IsIdentityAlignmentId(step.AlignmentId))
            {
                throw new InvalidDataException(
                    "A non-identity Nominal/Actual alignment requires exact Surface Match artifact references.");
            }
        }
        else
        {
            ValidateAlignment(step.Alignment);
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

    private static NominalActualAlignmentReference? ToRecipeAlignment(
        NominalActualAlignmentReference? alignment,
        string recipeDirectory) =>
        alignment is null
            ? null
            : new NominalActualAlignmentReference(
                ToRecipeArtifact(alignment.Model, recipeDirectory),
                ToRecipeArtifact(alignment.Scene, recipeDirectory),
                ToRecipeArtifact(alignment.Execution, recipeDirectory),
                ToRecipeArtifact(alignment.Assessment, recipeDirectory));

    private static NominalActualAlignmentReference? ToRuntimeAlignment(
        NominalActualAlignmentReference? alignment,
        string recipeDirectory) =>
        alignment is null
            ? null
            : new NominalActualAlignmentReference(
                ToRuntimeArtifact(alignment.Model, recipeDirectory),
                ToRuntimeArtifact(alignment.Scene, recipeDirectory),
                ToRuntimeArtifact(alignment.Execution, recipeDirectory),
                ToRuntimeArtifact(alignment.Assessment, recipeDirectory));

    private static NominalActualArtifactReference ToRecipeArtifact(
        NominalActualArtifactReference reference,
        string recipeDirectory) =>
        reference with
        {
            Path = Path.GetRelativePath(recipeDirectory, Path.GetFullPath(reference.Path)).Replace('\\', '/')
        };

    private static NominalActualArtifactReference ToRuntimeArtifact(
        NominalActualArtifactReference reference,
        string recipeDirectory) =>
        reference with
        {
            Path = Path.IsPathRooted(reference.Path)
                ? Path.GetFullPath(reference.Path)
                : Path.GetFullPath(Path.Combine(recipeDirectory, reference.Path))
        };

    private static void ValidateAlignment(NominalActualAlignmentReference alignment)
    {
        ValidateArtifact(alignment.Model, "model");
        ValidateArtifact(alignment.Scene, "scene");
        ValidateArtifact(alignment.Execution, "execution");
        ValidateArtifact(alignment.Assessment, "assessment");
        if (new[]
            {
                Path.GetFullPath(alignment.Model.Path),
                Path.GetFullPath(alignment.Scene.Path),
                Path.GetFullPath(alignment.Execution.Path),
                Path.GetFullPath(alignment.Assessment.Path)
            }.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 4)
        {
            throw new InvalidDataException("Nominal/actual alignment artifact paths must be distinct.");
        }
    }

    private static void ValidateArtifact(NominalActualArtifactReference? reference, string role)
    {
        if (reference is null
            || string.IsNullOrWhiteSpace(reference.Path)
            || reference.ContentSha256?.Length != 64
            || reference.ContentSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                $"Nominal/actual alignment {role} requires a path and SHA-256 content identity.");
        }
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
    double UpperTolerance,
    NominalActualAlignmentReference? Alignment = null);

public sealed record NominalActualComparisonRecipeFile(
    string Id,
    string Name,
    string Path,
    long ByteLength,
    string Sha256);

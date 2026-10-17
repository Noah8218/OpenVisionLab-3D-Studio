using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

public static class ToolRecipeValidationSetDefinitionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static string GetPathForRecipe(string recipePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        return $"{Path.GetFullPath(recipePath)}.validation-set.json";
    }

    private static string GetRecipeDirectory(string fullRecipePath) =>
        Path.GetDirectoryName(fullRecipePath) ?? Environment.CurrentDirectory;

    public static void SaveForRecipe(
        string recipePath,
        ToolRecipeValidationSetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        definition = definition with
        {
            SchemaVersion = ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
            Samples = definition.Samples.Select(sample => sample with { Expectation = sample.EffectiveExpectation, Usage = sample.EffectiveUsage, Role = sample.DevelopmentRole }).ToArray()
        };
        Validate(definition);
        var fullRecipePath = Path.GetFullPath(recipePath);
        var recipeDirectory = GetRecipeDirectory(fullRecipePath);
        var portable = definition with
        {
            SchemaVersion = ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
            Samples = definition.Samples
                .OrderBy(sample => sample.Order)
                .Select(sample => sample with
                {
                    Role = sample.DevelopmentRole,
                    Expectation = sample.EffectiveExpectation,
                    Usage = sample.EffectiveUsage,
                    SourcePath = Path.GetRelativePath(
                        recipeDirectory,
                        Path.GetFullPath(sample.SourcePath))
                })
                .ToArray()
        };
        WriteAtomic(GetPathForRecipe(fullRecipePath), portable);
    }

    public static ToolRecipeValidationSetDefinition? LoadForRecipe(
        string recipePath)
    {
        var fullRecipePath = Path.GetFullPath(recipePath);
        var manifestPath = GetPathForRecipe(fullRecipePath);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        using var stream = File.OpenRead(manifestPath);
        var definition =
            JsonSerializer.Deserialize<ToolRecipeValidationSetDefinition>(
                stream,
                JsonOptions)
            ?? throw new InvalidDataException(
                "Validation Set role manifest JSON is empty.");
        Validate(definition);
        var recipeDirectory = GetRecipeDirectory(fullRecipePath);
        return definition with
        {
            SchemaVersion = ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
            Samples = definition.Samples
                .OrderBy(sample => sample.Order)
                .Select(sample => sample with
                {
                    Role = sample.DevelopmentRole,
                    Expectation = sample.EffectiveExpectation,
                    Usage = sample.EffectiveUsage,
                    SourcePath = Path.IsPathFullyQualified(sample.SourcePath)
                        ? Path.GetFullPath(sample.SourcePath)
                        : Path.GetFullPath(
                            Path.Combine(recipeDirectory, sample.SourcePath))
                })
                .ToArray()
        };
    }

    public static string CreateCalculationContextSha256(ToolRecipeDocument document) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", document.Steps.Select(step => document.CreateStepExecutionContext(step.Id))))));

    public static void Validate(ToolRecipeValidationSetDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.SchemaVersion is not ("1.0" or ToolRecipeValidationSetDefinition.CurrentSchemaVersion)
            || string.IsNullOrWhiteSpace(definition.RecipeName)
            || string.IsNullOrWhiteSpace(definition.RecipeSourceSha256)
            || definition.Samples is null
            || definition.RecipeContentSha256 is { } recipeSha && (recipeSha.Length != 64 || !recipeSha.All(Uri.IsHexDigit))
            || definition.CalculationContextSha256 is { } contextSha && (contextSha.Length != 64 || !contextSha.All(Uri.IsHexDigit)))
        {
            throw new InvalidDataException(
                "Validation Set role manifest identity is incomplete or unsupported.");
        }

        var expectedOrder = 1;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sample in definition.Samples.OrderBy(sample => sample.Order))
        {
            if (sample.Order != expectedOrder++
                || string.IsNullOrWhiteSpace(sample.SourcePath)
                || !Enum.IsDefined(sample.Role)
                || sample.Expectation is { } expectation && !Enum.IsDefined(expectation)
                || sample.Usage is { } usage && !Enum.IsDefined(usage)
                || definition.SchemaVersion == ToolRecipeValidationSetDefinition.CurrentSchemaVersion && (sample.Expectation is null || sample.Usage is null)
                || !paths.Add(sample.SourcePath))
            {
                throw new InvalidDataException(
                    "Validation Set samples require contiguous order, unique paths, and a Good, Bad, or HeldOut role.");
            }
        }
    }

    private static void WriteAtomic(
        string path,
        ToolRecipeValidationSetDefinition definition)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporaryPath = $"{path}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(
                JsonSerializer.Serialize(definition, JsonOptions));
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

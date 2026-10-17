using System.Text.Json.Serialization;

namespace OpenVisionLab.ThreeD.Core;

public enum ToolRecipeValidationSampleRole
{
    Good,
    Bad,
    HeldOut
}

public enum ToolRecipeValidationExpectation { Unset, Ok, Ng }
public enum ToolRecipeValidationUsage { Adjustment, HeldOut }

public sealed record ToolRecipeValidationSampleDefinition(
    int Order,
    string SourcePath,
    ToolRecipeValidationSampleRole Role)
{
    public ToolRecipeValidationExpectation? Expectation { get; init; }
    public ToolRecipeValidationUsage? Usage { get; init; }
    [JsonIgnore] public ToolRecipeValidationExpectation EffectiveExpectation => ResolveExpectation(Role, Expectation);
    [JsonIgnore] public ToolRecipeValidationUsage EffectiveUsage => ResolveUsage(Role, Usage);
    [JsonIgnore] public ToolRecipeValidationSampleRole DevelopmentRole => ResolveDevelopmentRole(EffectiveExpectation, EffectiveUsage);

    public static ToolRecipeValidationExpectation ResolveExpectation(ToolRecipeValidationSampleRole role, ToolRecipeValidationExpectation? expectation) => expectation ?? role switch
    {
        ToolRecipeValidationSampleRole.Good => ToolRecipeValidationExpectation.Ok,
        ToolRecipeValidationSampleRole.Bad => ToolRecipeValidationExpectation.Ng,
        _ => ToolRecipeValidationExpectation.Unset
    };
    public static ToolRecipeValidationUsage ResolveUsage(ToolRecipeValidationSampleRole role, ToolRecipeValidationUsage? usage) => usage
        ?? (role == ToolRecipeValidationSampleRole.HeldOut ? ToolRecipeValidationUsage.HeldOut : ToolRecipeValidationUsage.Adjustment);
    public static ToolRecipeValidationSampleRole ResolveDevelopmentRole(ToolRecipeValidationExpectation expectation, ToolRecipeValidationUsage usage) =>
        usage == ToolRecipeValidationUsage.HeldOut || expectation == ToolRecipeValidationExpectation.Unset ? ToolRecipeValidationSampleRole.HeldOut
            : expectation == ToolRecipeValidationExpectation.Ok ? ToolRecipeValidationSampleRole.Good : ToolRecipeValidationSampleRole.Bad;
}

/// <summary>
/// Durable sample-role manifest stored beside a recipe. It references sample
/// files but never changes their bytes or the authored recipe graph.
/// </summary>
public sealed record ToolRecipeValidationSetDefinition(
    string SchemaVersion,
    string RecipeName,
    string RecipeSourceSha256,
    IReadOnlyList<ToolRecipeValidationSampleDefinition> Samples)
{
    public const string CurrentSchemaVersion = "2.0";
    public string? RecipeContentSha256 { get; init; }
    public string? CalculationContextSha256 { get; init; }
}

public enum ToolRecipeEvidenceScope
{
    StepMetric,
    RegionMetric
}

public sealed record ToolRecipeRoleMetricStatistics(
    ToolRecipeValidationSampleRole Role,
    int SampleCount,
    int ValueCount,
    double? Minimum,
    double? Maximum,
    double? Mean,
    double? StandardDeviation,
    bool IncludedInDevelopment);

public sealed record ToolRecipeLabeledMetricDistribution(
    ToolRecipeEvidenceScope Scope,
    string OwnerId,
    string OwnerName,
    string MetricName,
    string Unit,
    IReadOnlyList<ToolRecipeRoleMetricStatistics> RoleStatistics);

public sealed record ToolRecipeLabeledEvidenceReport(
    string ContractVersion,
    ResultStatus Status,
    string Message,
    int GoodSampleCount,
    int BadSampleCount,
    int HeldOutSampleCount,
    IReadOnlyList<ToolRecipeLabeledMetricDistribution> Distributions,
    IReadOnlyList<string> Warnings)
{
    public const string CurrentContractVersion = "1.0";
}

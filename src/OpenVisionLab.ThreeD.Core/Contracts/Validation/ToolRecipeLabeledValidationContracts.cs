namespace OpenVisionLab.ThreeD.Core;

public enum ToolRecipeValidationSampleRole
{
    Good,
    Bad,
    HeldOut
}

public sealed record ToolRecipeValidationSampleDefinition(
    int Order,
    string SourcePath,
    ToolRecipeValidationSampleRole Role);

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
    public const string CurrentSchemaVersion = "1.0";
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

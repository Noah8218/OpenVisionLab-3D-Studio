namespace OpenVisionLab.ThreeD.Core;

public sealed record ToolRecipeThresholdParameterChange(
    string ParameterName,
    string BeforeValue,
    string ProposedValue);

public sealed record ToolRecipeThresholdParameterProposal(
    string ContractVersion,
    string CandidateId,
    string StepId,
    string ToolId,
    string ToolName,
    string MetricName,
    ToolRecipeThresholdLimitKind LimitKind,
    IReadOnlyList<ToolRecipeThresholdParameterChange> Changes,
    ToolRecipeThresholdCandidate Candidate)
{
    public const string CurrentContractVersion = "1.0";
}

public sealed record ToolRecipeThresholdHeldOutMetricEvidence(
    string StepId,
    string StepName,
    string MetricName,
    string Unit,
    double Value,
    ResultStatus Status);

public sealed record ToolRecipeThresholdHeldOutSampleEvidence(
    int SampleOrder,
    string SampleIdentity,
    string SourcePath,
    ResultStatus Status,
    string Message,
    IReadOnlyList<ToolRecipeThresholdHeldOutMetricEvidence> Metrics);

public sealed record ToolRecipeThresholdManualParameterChange(
    string ParameterName,
    string SuggestedValue,
    string ManualValue);

public sealed record ToolRecipeThresholdDevelopmentSampleEvidence(
    int SampleOrder,
    string SampleIdentity,
    string SourcePath,
    ToolRecipeValidationSampleRole Role,
    ResultStatus Status,
    bool ExpectedMatch,
    string Message,
    IReadOnlyList<ToolRecipeThresholdHeldOutMetricEvidence> Metrics);

/// <summary>
/// Optional extension for one genuine development-set mismatch corrected by
/// an operator through the ordinary typed PropertyGrid. The before and after
/// collections contain development samples only; Held-out evidence remains in
/// the parent contract and is produced by a separate explicit replay.
/// </summary>
public sealed record ToolRecipeThresholdManualCorrectionEvidence(
    string ContractVersion,
    IReadOnlyList<ToolRecipeThresholdManualParameterChange> ParameterChanges,
    int BeforeMismatchCount,
    IReadOnlyList<ToolRecipeThresholdDevelopmentSampleEvidence>
        BeforeDevelopmentSamples,
    int AfterMismatchCount,
    IReadOnlyList<ToolRecipeThresholdDevelopmentSampleEvidence>
        AfterDevelopmentSamples)
{
    public const string CurrentContractVersion = "1.0";
}

/// <summary>
/// Durable evidence for one explicitly applied threshold proposal and one
/// explicit Held-out replay. Development decisions remain embedded in the
/// selected candidate and Held-out samples remain a separate collection.
/// </summary>
public sealed record ToolRecipeThresholdCorrectionEvidence(
    string ContractVersion,
    string RecipeName,
    string RecipeSourceSha256,
    ToolRecipeThresholdParameterProposal Proposal,
    ResultStatus Status,
    string Message,
    IReadOnlyList<ToolRecipeThresholdHeldOutSampleEvidence> HeldOutSamples,
    ToolRecipeThresholdManualCorrectionEvidence? ManualCorrection = null)
{
    public const string CurrentContractVersion = "1.0";
}

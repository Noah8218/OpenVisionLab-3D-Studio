namespace OpenVisionLab.ThreeD.Core;

public enum ToolRecipeThresholdLimitKind
{
    Minimum,
    Maximum,
    Range
}

public enum ToolRecipeThresholdDecisionKind
{
    CorrectGood,
    FalseReject,
    CorrectBad,
    FalseAccept
}

public enum ToolRecipeThresholdEvidenceWarningKind
{
    MissingGoodSamples,
    MissingBadSamples,
    InsufficientGoodSamples,
    InsufficientBadSamples,
    ImbalancedSamples,
    OverlappingDistributions
}

public sealed record ToolRecipeThresholdEvidenceWarning(
    ToolRecipeThresholdEvidenceWarningKind Kind,
    ToolRecipeEvidenceScope? Scope,
    string OwnerId,
    string OwnerName,
    string MetricName,
    string Unit,
    int GoodSampleCount,
    int BadSampleCount,
    IReadOnlyList<string> DevelopmentSampleIdentities,
    string Message);

public sealed record ToolRecipeMetricObservation(
    int SampleOrder,
    string SampleIdentity,
    string SourcePath,
    ToolRecipeValidationSampleRole Role,
    ToolRecipeEvidenceScope Scope,
    string OwnerId,
    string OwnerName,
    string MetricName,
    string Unit,
    double Value,
    string EvidenceLocator = "");

public sealed record ToolRecipeThresholdSampleDecision(
    int SampleOrder,
    string SampleIdentity,
    string SourcePath,
    ToolRecipeValidationSampleRole ExpectedRole,
    ToolRecipeValidationSampleRole PredictedRole,
    ToolRecipeThresholdDecisionKind Decision,
    double Value,
    string EvidenceLocator = "");

public sealed record ToolRecipeThresholdCandidate(
    string CandidateId,
    ToolRecipeEvidenceScope Scope,
    string OwnerId,
    string OwnerName,
    string MetricName,
    string Unit,
    ToolRecipeThresholdLimitKind LimitKind,
    double? Minimum,
    double? Maximum,
    int GoodAcceptedCount,
    int GoodRejectedCount,
    int BadRejectedCount,
    int BadAcceptedCount,
    IReadOnlyList<ToolRecipeThresholdSampleDecision> Decisions)
{
    public int DevelopmentSampleCount =>
        GoodAcceptedCount
        + GoodRejectedCount
        + BadRejectedCount
        + BadAcceptedCount;

    public int CorrectCount => GoodAcceptedCount + BadRejectedCount;

    public int ErrorCount => GoodRejectedCount + BadAcceptedCount;
}

public sealed record ToolRecipeThresholdCandidateReport(
    string ContractVersion,
    ResultStatus Status,
    string Message,
    int DevelopmentSampleCount,
    int HeldOutSampleCount,
    int HeldOutObservationCount,
    IReadOnlyList<string> HeldOutSampleIdentities,
    IReadOnlyList<ToolRecipeThresholdCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ToolRecipeThresholdEvidenceWarning> EvidenceWarnings)
{
    public const string CurrentContractVersion = "2.1";
}

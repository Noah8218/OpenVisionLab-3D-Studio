namespace OpenVisionLab.ThreeD.Core;

public readonly record struct AlignedPointRepeatabilityReferencePoint(
    string CorrespondenceId,
    double AlignedX,
    double AlignedY,
    double AlignedZ);

public readonly record struct AlignedPointRepeatabilityObservation(
    string CorrespondenceId,
    double Value);

public sealed record AlignedPointRepeatabilityRun(
    string RunId,
    string SourceEntityId,
    long SourceByteLength,
    string SourceSha256,
    DateTimeOffset CapturedAt,
    string Unit,
    string FrameId,
    string AlignmentReferenceId,
    string AlignmentMethodId,
    string AlignmentEvidenceId,
    IReadOnlyList<AlignedPointRepeatabilityObservation>? Observations);

public sealed record AlignedPointRepeatabilityAcceptance(
    int MinimumRunCount,
    int MinimumCorrespondenceCount,
    double MaximumSampleStandardDeviation,
    double MaximumRange);

public sealed record AlignedPointRepeatabilityInput(
    string StudyId,
    string MeasurementDefinitionId,
    string ReferenceRoiId,
    string Unit,
    string FrameId,
    string AlignmentReferenceId,
    string CorrespondenceDefinitionId,
    IReadOnlyList<AlignedPointRepeatabilityReferencePoint>? ReferencePoints,
    IReadOnlyList<AlignedPointRepeatabilityRun>? Runs,
    AlignedPointRepeatabilityAcceptance? Acceptance);

public enum AlignedPointRepeatabilityInputState
{
    Ready,
    InvalidInput,
    InvalidAcceptancePolicy,
    InsufficientRuns,
    InsufficientCorrespondences
}

public sealed record AlignedPointRepeatabilityInputValidation(
    bool IsReady,
    AlignedPointRepeatabilityInputState State,
    string Message,
    AlignedPointRepeatabilityInput? Input,
    IReadOnlyList<AlignedPointRepeatabilityReferencePoint> ReferencePoints,
    IReadOnlyList<AlignedPointRepeatabilityRun> Runs,
    int RunCount,
    int CorrespondenceCount);

public enum AlignedPointRepeatabilityDecision
{
    InvalidInput,
    InvalidAcceptancePolicy,
    InsufficientRuns,
    InsufficientCorrespondences,
    Accepted,
    SampleStandardDeviationExceeded,
    RangeExceeded,
    SampleStandardDeviationAndRangeExceeded
}

public sealed record AlignedPointRepeatabilityPointEvaluation(
    AlignedPointRepeatabilityReferencePoint ReferencePoint,
    int RunCount,
    double Mean,
    double Minimum,
    double Maximum,
    double SampleStandardDeviation,
    double SixSigmaSpread,
    double Range,
    ResultStatus Status,
    bool SampleStandardDeviationPassed,
    bool RangePassed);

public sealed record AlignedPointRepeatabilityEvaluation(
    ToolResult Result,
    AlignedPointRepeatabilityDecision Decision,
    AlignedPointRepeatabilityInput? Input,
    IReadOnlyList<AlignedPointRepeatabilityReferencePoint> ReferencePoints,
    IReadOnlyList<AlignedPointRepeatabilityRun> Runs,
    IReadOnlyList<AlignedPointRepeatabilityPointEvaluation> PointEvaluations,
    int RunCount,
    int CorrespondenceCount,
    int FailingCorrespondenceCount,
    double MaximumSampleStandardDeviation,
    double MaximumRange);

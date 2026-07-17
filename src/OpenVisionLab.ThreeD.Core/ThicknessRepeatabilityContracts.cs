namespace OpenVisionLab.ThreeD.Core;

public sealed record ThicknessRepeatabilityRun(
    string RunId,
    string SourceEntityId,
    DateTimeOffset CapturedAt,
    string Unit,
    string FrameId,
    double Thickness);

public sealed record ThicknessRepeatabilityAcceptance(
    int MinimumRunCount,
    double MaximumSampleStandardDeviation,
    double MaximumRange);

public sealed record ThicknessRepeatabilityInput(
    string StudyId,
    string MeasurementDefinitionId,
    string ReferenceRoiId,
    string Unit,
    string FrameId,
    IReadOnlyList<ThicknessRepeatabilityRun>? Runs,
    ThicknessRepeatabilityAcceptance? Acceptance);

public enum ThicknessRepeatabilityInputState
{
    Ready,
    InvalidInput,
    InvalidAcceptancePolicy,
    InsufficientRuns
}

public sealed record ThicknessRepeatabilityInputValidation(
    bool IsReady,
    ThicknessRepeatabilityInputState State,
    string Message,
    ThicknessRepeatabilityInput? Input,
    IReadOnlyList<ThicknessRepeatabilityRun> Runs,
    int RunCount);

public enum ThicknessRepeatabilityDecision
{
    InvalidInput,
    InvalidAcceptancePolicy,
    InsufficientRuns,
    Accepted,
    SampleStandardDeviationExceeded,
    RangeExceeded,
    SampleStandardDeviationAndRangeExceeded
}

public sealed record ThicknessRepeatabilityEvaluation(
    ToolResult Result,
    ThicknessRepeatabilityDecision Decision,
    ThicknessRepeatabilityInput? Input,
    IReadOnlyList<ThicknessRepeatabilityRun> Runs,
    int RunCount,
    double Mean,
    double Minimum,
    double Maximum,
    double SampleStandardDeviation,
    double SixSigmaSpread,
    double Range);

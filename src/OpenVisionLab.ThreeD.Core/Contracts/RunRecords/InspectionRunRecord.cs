namespace OpenVisionLab.ThreeD.Core;

public sealed record InspectionRunRecord(
    string SchemaVersion,
    string RunId,
    DateTimeOffset RecordedAtUtc,
    InspectionRunRecipe Recipe,
    InspectionRunSource Source,
    string ToolName,
    ResultStatus Status,
    string Message,
    double ElapsedMilliseconds,
    IReadOnlyList<InspectionRunMetric> Metrics,
    IReadOnlyList<InspectionRunOverlay> Overlays,
    string ViewerRunnerMatchState,
    InspectionRunArtifacts Artifacts)
{
    public InspectionRunEnvironment? ExecutionEnvironment { get; init; }
    public InspectionRunStep? Step { get; init; }
    public IReadOnlyList<InspectionRunStepResult>? Steps { get; init; }
    public InspectionRunThresholdCorrectionEvidence? ThresholdCorrectionEvidence
    {
        get;
        init;
    }
    public InspectionRunSurfaceMatchEvidence? SurfaceMatchEvidence
    {
        get;
        init;
    }
}

/// <summary>
/// Read-only projection of already identified Surface Match artifacts. The
/// reporting layer retains their exact identities and never executes pose
/// search, scoring, or acceptance evaluation.
/// </summary>
public sealed record InspectionRunSurfaceMatchEvidence(
    string SchemaVersion,
    string Semantics,
    string ModelArtifactId,
    string ModelContentSha256,
    string SceneArtifactId,
    string SceneContentSha256,
    SurfaceMatchExecutionArtifact Execution,
    SurfaceAndEdgeMatchScoreArtifact? Score,
    SurfaceAndEdgeMatchAssessmentArtifact? Assessment)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "identified-pose-separate-surface-edge-score-assessment-export-v1";
}

public enum InspectionRunThresholdCorrectionEvidenceState
{
    Unavailable,
    Available,
    Stale,
    Mismatch,
    Invalid
}

/// <summary>
/// Read-only snapshot of the recipe-side threshold-correction sidecar at Run
/// Record creation time. The reporting layer never creates, applies, or
/// replays threshold evidence.
/// </summary>
public sealed record InspectionRunThresholdCorrectionEvidence(
    InspectionRunThresholdCorrectionEvidenceState State,
    string Message,
    string SidecarPath,
    string? SidecarSha256,
    ToolRecipeThresholdCorrectionEvidence? Evidence);

public sealed record InspectionRunEnvironment(
    string ApplicationName,
    string ApplicationVersion,
    string ViewerHostApiVersion,
    string GitCommit,
    string GitWorkingTree,
    string DotNetRuntime,
    string OperatingSystem,
    string ProcessArchitecture);

public sealed record InspectionRunRecipe(
    string RecipeType,
    string Version,
    string Path,
    string Sha256);

public sealed record InspectionRunSource(
    string EntityId,
    string Path,
    string Sha256,
    long ByteLength,
    string Unit);

public sealed record InspectionRunStep(
    string Id,
    string SourceEntityId,
    IReadOnlyList<string> ReferenceIds,
    IReadOnlyList<string> MeasurementIds);

public sealed record InspectionRunStepResult(
    int RecipeIndex,
    string Id,
    string ToolId,
    string ToolName,
    IReadOnlyList<string> InputEntityIds,
    string OutputEntityId,
    ResultStatus Status,
    string Message,
    double ElapsedMilliseconds,
    IReadOnlyList<InspectionRunMetric> Metrics,
    IReadOnlyList<InspectionRunOverlay> Overlays)
{
    public string? OutputContentSha256 { get; init; }
}

public sealed record InspectionRunMetric(
    string Name,
    MetricKind Kind,
    double Value,
    string Unit,
    ResultStatus? Status);

public sealed record InspectionRunOverlay(
    string Id,
    OverlayKind Kind,
    string Label,
    ResultStatus? Status,
    string? SourceEntityId);

public sealed record InspectionRunArtifacts(
    string RunnerTextReport,
    string? ViewerContract,
    string? ViewerScreenshot,
    string? RunRecordJson,
    string? HtmlReport,
    string? CsvReport);

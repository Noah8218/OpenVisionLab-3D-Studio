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
    public InspectionRunTiming? Timing { get; init; }
    public InspectionRunSourceQualityEvidence? SourceQualityEvidence
    {
        get;
        init;
    }
    public InspectionRunThresholdCorrectionEvidence? ThresholdCorrectionEvidence
    {
        get;
        init;
    }
    public InspectionRunIntegrationContext? IntegrationContext
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
/// Exact identity captured when a 3D Run Record is produced for an external
/// Machine Studio Handoff. Records without this context cannot be published as
/// an integration Result because their project, acquisition, and consumer
/// build cannot be proven to match the request.
/// </summary>
public sealed record InspectionRunIntegrationContext(
    string ProjectId,
    string ProjectSchema,
    string SequenceId,
    string StepId,
    string CameraId,
    string AcquisitionId,
    string FrameId,
    string Unit,
    string Modality,
    string InputKind,
    string ConsumerApplicationId,
    string ConsumerApplicationVersion,
    string ConsumerSourceCommit,
    string ConsumerSourceState);

public enum InspectionRunSourceQualityEvidenceState
{
    Available,
    Unavailable
}

/// <summary>
/// Read-only snapshot of source quality observed for the exact Run Record
/// source. Reporting never reloads the source or recalculates this report.
/// </summary>
public sealed record InspectionRunSourceQualityEvidence(
    string SchemaVersion,
    InspectionRunSourceQualityEvidenceState State,
    string Message,
    string SourceQualitySha256,
    SourceQualityReport? Report)
{
    public const string CurrentSchemaVersion = "1.0";

    public static InspectionRunSourceQualityEvidence Available(
        InspectionRunSource source,
        SourceQualityReport report)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(report);
        var evidence = new InspectionRunSourceQualityEvidence(
            CurrentSchemaVersion,
            InspectionRunSourceQualityEvidenceState.Available,
            "Source Quality was captured from the exact source used by this run.",
            SourceQualityReportContentIdentity.CalculateSha256(report),
            report);
        if (!evidence.TryValidate(source, out var message))
        {
            throw new InvalidDataException(
                $"Run Record Source Quality is incompatible: {message}");
        }

        return evidence;
    }

    public static InspectionRunSourceQualityEvidence Unavailable(
        string message) => new(
            CurrentSchemaVersion,
            InspectionRunSourceQualityEvidenceState.Unavailable,
            message,
            string.Empty,
            null);

    public bool TryValidate(
        InspectionRunSource source,
        out string validationMessage)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (SchemaVersion != CurrentSchemaVersion)
        {
            validationMessage =
                $"Unsupported Source Quality evidence schema '{SchemaVersion}'.";
            return false;
        }

        if (State == InspectionRunSourceQualityEvidenceState.Unavailable)
        {
            var valid = Report is null
                && string.IsNullOrEmpty(SourceQualitySha256)
                && !string.IsNullOrWhiteSpace(Message);
            validationMessage = valid
                ? "Source Quality is explicitly unavailable."
                : "Unavailable Source Quality must contain only a reason.";
            return valid;
        }

        if (Report is null
            || !Report.TryValidateGridDiagnostics(out _)
            || string.IsNullOrWhiteSpace(Message)
            || !string.Equals(
                SourceQualitySha256,
                SourceQualityReportContentIdentity.CalculateSha256(Report),
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                source.EntityId,
                Report.Source.EntityId,
                StringComparison.OrdinalIgnoreCase)
            || source.ByteLength != Report.Source.ByteLength
            || !string.Equals(
                source.Sha256,
                Report.Source.RootSourceSha256,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                source.Unit,
                Report.Coordinates.Unit,
                StringComparison.Ordinal))
        {
            validationMessage =
                "Source entity, byte length, SHA-256, unit, or report identity does not match.";
            return false;
        }

        validationMessage =
            $"Source Quality contains {Report.Coverage.ValidSampleCount} valid and {Report.Coverage.MissingSampleCount} missing sample(s).";
        return true;
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
    public InspectionRunTiming? Timing { get; init; }
    public C3DCompletenessGridMetricOutput? CompletenessGrid { get; init; }
    public C3DPresenceCheckOutput? PresenceCheck { get; init; }
}

public enum InspectionRunTimingState
{
    Available,
    Unavailable
}

/// <summary>
/// Observational execution timing. It is report evidence only and must never
/// participate in deterministic artifact identity or acceptance decisions.
/// </summary>
public sealed record InspectionRunTiming(
    string SchemaVersion,
    InspectionRunTimingState State,
    string Clock,
    string Message,
    double? TotalElapsedMilliseconds,
    IReadOnlyList<InspectionRunStageTiming> Stages)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string StopwatchClock = "System.Diagnostics.Stopwatch";
    public const string ToolExecutionStage = "tool-execution";

    public static InspectionRunTiming Available(
        string clock,
        double totalElapsedMilliseconds,
        IEnumerable<InspectionRunStageTiming> stages,
        string message)
    {
        var timing = new InspectionRunTiming(
            CurrentSchemaVersion,
            InspectionRunTimingState.Available,
            clock,
            message,
            totalElapsedMilliseconds,
            stages.ToArray());
        if (!timing.TryValidate(out var validationMessage))
        {
            throw new InvalidDataException(
                $"Run timing is invalid: {validationMessage}");
        }

        return timing;
    }

    public static InspectionRunTiming Unavailable(string message) => new(
        CurrentSchemaVersion,
        InspectionRunTimingState.Unavailable,
        string.Empty,
        message,
        null,
        []);

    public bool TryValidate(out string validationMessage)
    {
        if (SchemaVersion != CurrentSchemaVersion)
        {
            validationMessage = $"Unsupported timing schema '{SchemaVersion}'.";
            return false;
        }

        if (State == InspectionRunTimingState.Unavailable)
        {
            var valid = string.IsNullOrEmpty(Clock)
                && TotalElapsedMilliseconds is null
                && Stages is { Count: 0 }
                && !string.IsNullOrWhiteSpace(Message);
            validationMessage = valid
                ? "Timing is explicitly unavailable."
                : "Unavailable timing must contain only a reason.";
            return valid;
        }

        var stages = Stages ?? [];
        var stageIds = stages.Select(stage => stage.StageId).ToArray();
        var total = TotalElapsedMilliseconds;
        if (string.IsNullOrWhiteSpace(Clock)
            || string.IsNullOrWhiteSpace(Message)
            || total is null
            || !double.IsFinite(total.Value)
            || total.Value < 0.0
            || stages.Count == 0
            || stageIds.Any(string.IsNullOrWhiteSpace)
            || stageIds.Distinct(StringComparer.Ordinal).Count() != stageIds.Length
            || stages.Any(stage => !double.IsFinite(stage.ElapsedMilliseconds)
                                   || stage.ElapsedMilliseconds < 0.0))
        {
            validationMessage = "Available timing fields are missing or invalid.";
            return false;
        }

        var stageTotal = stages.Sum(stage => stage.ElapsedMilliseconds);
        var tolerance = Math.Max(1e-6, total.Value * 1e-9);
        var totalsMatch = Math.Abs(stageTotal - total.Value) <= tolerance;
        validationMessage = totalsMatch
            ? $"Timing contains {stages.Count} stage(s)."
            : $"Stage total {stageTotal:R} does not match observed total {total.Value:R}.";
        return totalsMatch;
    }
}

public sealed record InspectionRunStageTiming(
    string StageId,
    double ElapsedMilliseconds);

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

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
}

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

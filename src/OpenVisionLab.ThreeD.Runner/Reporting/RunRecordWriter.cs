using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal sealed record RunArtifactOptions(
    string? JsonPath,
    string? HtmlPath,
    string? CsvPath,
    string? ViewerScreenshotPath)
{
    public bool Requested => JsonPath is not null || HtmlPath is not null || CsvPath is not null;
}

internal static class RunRecordWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static void Write(
        RunArtifactOptions options,
        string recipePath,
        string recipeType,
        string recipeVersion,
        string sourcePath,
        string sourceEntityId,
        string sourceUnit,
        InspectionRunStep? step,
        ToolResult result,
        string runnerReportPath,
        string? viewerContractPath)
    {
        if (!options.Requested) return;

        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var sourceHash = HashFile(sourcePath);
        var record = new InspectionRunRecord(
            "1.2",
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe(recipeType, recipeVersion, Path.GetFullPath(recipePath), recipeHash),
            new InspectionRunSource(sourceEntityId, Path.GetFullPath(sourcePath), sourceHash, new FileInfo(sourcePath).Length, sourceUnit),
            result.ToolName,
            result.Status,
            result.Message,
            result.Elapsed.TotalMilliseconds,
            ToMetrics(result.Metrics),
            result.Overlays.Select(overlay => new InspectionRunOverlay(overlay.Id, overlay.Kind, overlay.Label, overlay.Status, overlay.SourceEntityId)).ToArray(),
            viewerContractPath is null ? "NotCompared" : "Matched",
            new InspectionRunArtifacts(
                Path.GetFullPath(runnerReportPath),
                FullOptionalPath(viewerContractPath),
                FullOptionalPath(options.ViewerScreenshotPath),
                FullOptionalPath(options.JsonPath),
                FullOptionalPath(options.HtmlPath),
                FullOptionalPath(options.CsvPath)))
        {
            ExecutionEnvironment = CreateExecutionEnvironment(recipePath),
            Step = step
        };

        WriteOutputs(options, record);
    }

    public static void WriteOrdered(
        RunArtifactOptions options,
        string recipePath,
        ToolRecipeDocument document,
        string sourcePath,
        string regridStepId,
        ToolResult result,
        ToolRecipeOrderedTransformedHeightFieldExecutionOutput output,
        string runnerReportPath,
        string? viewerContractPath)
    {
        if (!options.Requested) return;

        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var sourceHash = HashFile(sourcePath);
        var regridIndex = document.Steps.ToList().FindIndex(step =>
            string.Equals(step.Id, regridStepId, StringComparison.OrdinalIgnoreCase));
        if (regridIndex < 0) throw new InvalidDataException($"Ordered Run Record cannot find Re-grid step '{regridStepId}'.");

        var regridStep = document.Steps[regridIndex];
        var steps = new List<InspectionRunStepResult>
        {
            ToStepResult(regridIndex, regridStep, output.RegridResult)
        };
        steps.AddRange(output.Measurements.Select(item =>
            ToStepResult(
                item.RecipeIndex,
                document.Steps[item.RecipeIndex],
                item.Output.Result)));

        var runSource = new InspectionRunSource(
            document.Source.Id,
            Path.GetFullPath(sourcePath),
            sourceHash,
            new FileInfo(sourcePath).Length,
            document.Source.Unit);
        var record = new InspectionRunRecord(
            "1.9",
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe("tool-recipe", document.SchemaVersion, Path.GetFullPath(recipePath), recipeHash),
            runSource,
            result.ToolName,
            result.Status,
            result.Message,
            result.Elapsed.TotalMilliseconds,
            ToMetrics(result.Metrics),
            ToOverlays(result.Overlays),
            viewerContractPath is null ? "NotCompared" : "Matched",
            new InspectionRunArtifacts(
                Path.GetFullPath(runnerReportPath),
                FullOptionalPath(viewerContractPath),
                FullOptionalPath(options.ViewerScreenshotPath),
                FullOptionalPath(options.JsonPath),
                FullOptionalPath(options.HtmlPath),
                FullOptionalPath(options.CsvPath)))
        {
            ExecutionEnvironment = CreateExecutionEnvironment(recipePath),
            Steps = steps,
            SourceQualityEvidence =
                InspectionRunSourceQualityEvidence.Unavailable(
                    "This ordered execution path did not supply Source Quality evidence.")
        };

        WriteOutputs(options, record);
    }

    public static void WriteOrderedGraph(
        RunArtifactOptions options,
        string recipePath,
        ToolRecipeDocument document,
        string sourcePath,
        ToolRecipeOrderedGraphExecutionResult execution,
        string runnerReportPath,
        string? viewerContractPath)
    {
        if (!options.Requested) return;

        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var sourceHash = HashFile(sourcePath);
        var steps = ToolRecipeOrderedGraphRunRecordProjection.Create(
            document,
            execution);
        var metrics = steps.SelectMany(step => step.Metrics).ToArray();
        var overlays = steps.SelectMany(step => step.Overlays).ToArray();
        var thresholdCorrectionEvidence =
            ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                recipePath,
                document);
        var runSource = new InspectionRunSource(
            document.Source.Id,
            Path.GetFullPath(sourcePath),
            sourceHash,
            new FileInfo(sourcePath).Length,
            document.Source.Unit);
        var sourceQuality = execution.SourceQuality is null
            ? InspectionRunSourceQualityEvidence.Unavailable(
                "Source Quality was unavailable because the ordered source could not be analyzed.")
            : InspectionRunSourceQualityEvidence.Available(
                runSource,
                execution.SourceQuality);
        var record = new InspectionRunRecord(
            "1.9",
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe("tool-recipe", document.SchemaVersion, Path.GetFullPath(recipePath), recipeHash),
            runSource,
            "Ordered Tool Recipe Replay",
            execution.Status,
            execution.Message,
            execution.Duration.TotalMilliseconds,
            metrics,
            overlays,
            viewerContractPath is null ? "NotCompared" : "Matched",
            new InspectionRunArtifacts(
                Path.GetFullPath(runnerReportPath),
                FullOptionalPath(viewerContractPath),
                FullOptionalPath(options.ViewerScreenshotPath),
                FullOptionalPath(options.JsonPath),
                FullOptionalPath(options.HtmlPath),
                FullOptionalPath(options.CsvPath)))
        {
            ExecutionEnvironment = CreateExecutionEnvironment(recipePath),
            Steps = steps,
            SourceQualityEvidence = sourceQuality,
            ThresholdCorrectionEvidence = thresholdCorrectionEvidence
        };

        WriteOutputs(options, record);
    }

    public static void WriteSurfaceMatch(
        RunArtifactOptions options,
        string recipePath,
        ToolRecipeDocument document,
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchScoreArtifact? score,
        SurfaceAndEdgeMatchAssessmentArtifact? assessment,
        SurfaceMatchRuntimeReport? runtime,
        string runnerReportPath)
    {
        if (!options.Requested) return;

        var evidence = SurfaceMatchRunRecordProjection.Create(
            model,
            scene,
            execution,
            score,
            assessment);
        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var source = scene.SourceQuality.Source;
        var runSource = new InspectionRunSource(
            source.EntityId,
            source.Path,
            source.RootSourceSha256,
            source.ByteLength,
            scene.Unit);
        var status = assessment?.Decision == SurfaceMatchDecision.Pass
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var message = assessment is null
            ? execution.PoseResult.RejectionReason
            : $"Surface/edge assessment: {assessment.Decision} ({assessment.Reason}).";
        var timing = CreateSurfaceMatchTiming(execution, assessment, runtime);
        var record = new InspectionRunRecord(
            "1.9",
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe(
                "tool-recipe",
                document.SchemaVersion,
                Path.GetFullPath(recipePath),
                recipeHash),
            runSource,
            "Surface Match",
            status,
            string.IsNullOrWhiteSpace(message)
                ? "Identified Surface Match evidence exported without recomputation."
                : message,
            timing.TotalElapsedMilliseconds ?? 0.0,
            [],
            [],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.GetFullPath(runnerReportPath),
                null,
                FullOptionalPath(options.ViewerScreenshotPath),
                FullOptionalPath(options.JsonPath),
                FullOptionalPath(options.HtmlPath),
                FullOptionalPath(options.CsvPath)))
        {
            ExecutionEnvironment = CreateExecutionEnvironment(recipePath),
            SourceQualityEvidence =
                InspectionRunSourceQualityEvidence.Available(
                    runSource,
                    scene.SourceQuality),
            SurfaceMatchEvidence = evidence,
            Timing = timing
        };

        WriteOutputs(options, record);
    }

    private static InspectionRunStepResult ToStepResult(
        int recipeIndex,
        ToolRecipeStep step,
        ToolResult result,
        string? outputContentSha256 = null) =>
        new InspectionRunStepResult(
            recipeIndex,
            step.Id,
            step.ToolId,
            result.ToolName,
            step.InputEntityIds,
            step.OutputEntityId,
            result.Status,
            result.Message,
            result.Elapsed.TotalMilliseconds,
            ToMetrics(result.Metrics),
            ToOverlays(result.Overlays))
        {
            OutputContentSha256 = outputContentSha256,
            Timing = CreateToolTiming(result.Elapsed.TotalMilliseconds)
        };

    private static InspectionRunTiming CreateToolTiming(
        double elapsedMilliseconds) =>
        InspectionRunTiming.Available(
            InspectionRunTiming.StopwatchClock,
            elapsedMilliseconds,
            [
                new InspectionRunStageTiming(
                    InspectionRunTiming.ToolExecutionStage,
                    elapsedMilliseconds)
            ],
            "Existing ToolResult elapsed observation; no additional execution.");

    private static InspectionRunTiming CreateSurfaceMatchTiming(
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchAssessmentArtifact? assessment,
        SurfaceMatchRuntimeReport? runtime)
    {
        if (runtime is null)
        {
            return InspectionRunTiming.Unavailable(
                "No persisted Surface Match runtime evidence was supplied.");
        }

        if (!SurfaceMatchAssessmentArtifactValidator.InspectRuntime(
                runtime,
                out var runtimeEvidence)
            || !string.Equals(
                runtime.ExecutionContentSha256,
                execution.ContentSha256,
                StringComparison.Ordinal)
            || assessment is null
            || !string.Equals(
                runtime.AssessmentContentSha256,
                assessment.ContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Surface Match runtime does not match the identified execution and assessment: "
                + runtimeEvidence);
        }

        return InspectionRunTiming.Available(
            runtime.Clock,
            runtime.TotalMilliseconds,
            runtime.Stages.Select(stage => new InspectionRunStageTiming(
                stage.StageId,
                TimeSpan.FromTicks(stage.ElapsedTicks).TotalMilliseconds)),
            "Persisted Surface Match runtime evidence; matching was not recomputed.");
    }

    private static InspectionRunMetric[] ToMetrics(IEnumerable<Metric> metrics) =>
        metrics
            .Where(metric => double.IsFinite(metric.Value))
            .Select(metric => new InspectionRunMetric(metric.Name, metric.Kind, metric.Value, metric.Unit, metric.Status))
            .ToArray();

    private static InspectionRunOverlay[] ToOverlays(IEnumerable<Overlay> overlays) =>
        overlays.Select(overlay => new InspectionRunOverlay(overlay.Id, overlay.Kind, overlay.Label, overlay.Status, overlay.SourceEntityId)).ToArray();

    private static void WriteOutputs(RunArtifactOptions options, InspectionRunRecord record)
    {
        if (options.JsonPath is not null) WriteJson(options.JsonPath, record);
        if (options.HtmlPath is not null) WriteHtml(options.HtmlPath, record);
        if (options.CsvPath is not null) WriteCsv(options.CsvPath, record);
    }

    private static void WriteJson(string path, InspectionRunRecord record)
    {
        EnsureDirectory(path);
        File.WriteAllText(path, JsonSerializer.Serialize(record, JsonOptions), new UTF8Encoding(false));
    }

    private static void WriteHtml(string path, InspectionRunRecord record)
    {
        EnsureDirectory(path);
        var hasSteps = record.Steps is { Count: > 0 };
        var rows = hasSteps
            ? string.Join(Environment.NewLine, record.Steps!.SelectMany(step =>
                step.Metrics.Count == 0
                    ? [FormatHtmlStepRow(step, null)]
                    : step.Metrics.Select(metric => FormatHtmlStepRow(step, metric))))
            : string.Join(Environment.NewLine, record.Metrics.Select(metric =>
                $"<tr><td>{Encode(metric.Name)}</td><td>{Encode(metric.Kind.ToString())}</td><td>{Format(metric.Value)}</td><td>{Encode(metric.Unit)}</td><td>{Encode(metric.Status?.ToString() ?? string.Empty)}</td></tr>"));
        var tableHeader = hasSteps
            ? "<tr><th>Order</th><th>Step ID</th><th>Tool</th><th>Route</th><th>Step status</th><th>Elapsed ms</th><th>Stage timing</th><th>Output SHA-256</th><th>Metric</th><th>Kind</th><th>Value</th><th>Unit</th><th>Metric status</th><th>Overlays</th></tr>"
            : "<tr><th>Metric</th><th>Kind</th><th>Value</th><th>Unit</th><th>Status</th></tr>";
        var timingSection = FormatTimingHtml(record.Timing);
        var sourceQualitySection =
            FormatSourceQualityHtml(record.SourceQualityEvidence);
        var completenessSection = FormatCompletenessHtml(record.Steps);
        var thresholdCorrectionSection =
            record.SurfaceMatchEvidence is null
                ? FormatThresholdCorrectionHtml(
                    record.ThresholdCorrectionEvidence)
                : string.Empty;
        var surfaceMatchSection =
            FormatSurfaceMatchHtml(record.SurfaceMatchEvidence);
        var html = $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>OpenVisionLab 3D Inspection Run</title>
          <style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#111827}h1{font-size:22px}dl{display:grid;grid-template-columns:150px 1fr;gap:6px 12px}dt{font-weight:600}dd{margin:0;overflow-wrap:anywhere}.table-scroll{overflow-x:auto}table{border-collapse:collapse;width:100%;margin-top:16px}th,td{border:1px solid #d1d5db;padding:7px;text-align:left}th{background:#f3f4f6}.Pass{color:#047857}.Fail,.Error{color:#b91c1c}</style>
        </head>
        <body>
          <h1>OpenVisionLab 3D Inspection Run</h1>
          <dl>
            <dt>Run ID</dt><dd>{{Encode(record.RunId)}}</dd>
            <dt>Recorded UTC</dt><dd>{{Encode(record.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture))}}</dd>
            <dt>Tool</dt><dd>{{Encode(record.ToolName)}}</dd>
            <dt>Step</dt><dd>{{Encode(FormatStepSummary(record))}}</dd>
            <dt>Status</dt><dd class="{{record.Status}}">{{record.Status}}</dd>
            <dt>Recipe</dt><dd>{{Encode(record.Recipe.Path)}}<br>SHA-256 {{record.Recipe.Sha256}}</dd>
            <dt>Source</dt><dd>{{Encode(record.Source.Path)}}<br>SHA-256 {{record.Source.Sha256}}</dd>
            <dt>Viewer/Runner</dt><dd>{{Encode(record.ViewerRunnerMatchState)}}</dd>
            <dt>Application</dt><dd>{{Encode(FormatApplication(record.ExecutionEnvironment))}}</dd>
            <dt>Viewer Host API</dt><dd>{{Encode(record.ExecutionEnvironment?.ViewerHostApiVersion ?? "unknown")}}</dd>
            <dt>Git</dt><dd>{{Encode(FormatGit(record.ExecutionEnvironment))}}</dd>
            <dt>.NET Runtime</dt><dd>{{Encode(record.ExecutionEnvironment?.DotNetRuntime ?? "unknown")}}</dd>
            <dt>Platform</dt><dd>{{Encode(FormatPlatform(record.ExecutionEnvironment))}}</dd>
          </dl>
          <p>{{Encode(record.Message)}}</p>
          {{timingSection}}
          {{sourceQualitySection}}
          {{completenessSection}}
          {{thresholdCorrectionSection}}
          {{surfaceMatchSection}}
          <table><thead>{{tableHeader}}</thead><tbody>
          {{rows}}
          </tbody></table>
        </body>
        </html>
        """;
        File.WriteAllText(path, html, new UTF8Encoding(false));
    }

    private static string FormatCompletenessHtml(
        IReadOnlyList<InspectionRunStepResult>? steps)
    {
        var outputs = (steps ?? [])
            .Where(step => step.CompletenessGrid is not null)
            .Select(step => (Step: step, Output: step.CompletenessGrid!))
            .ToArray();
        if (outputs.Length == 0)
        {
            return string.Empty;
        }

        var rows = string.Join(
            Environment.NewLine,
            outputs.SelectMany(item => item.Output.Cells.Select(cell =>
                $"<tr><td>{Encode(item.Step.Id)}</td><td>{Encode(cell.CellId)}</td>"
                + $"<td>{cell.GridRow}, {cell.GridColumn}</td>"
                + $"<td>{cell.Region.Row}, {cell.Region.Column} · {cell.Region.RowCount} × {cell.Region.ColumnCount}</td>"
                + $"<td>{cell.TotalCellCount} / {cell.FiniteCellCount} / {cell.MissingCellCount}</td>"
                + $"<td>{Format(cell.FiniteCoverageRatio)}</td>"
                + $"<td>{FormatNullable(cell.MeanRawHeight)}</td>"
                + $"<td>{Format(cell.ReferenceMeanRawHeight)}</td>"
                + $"<td>{FormatNullable(cell.ReferenceRelativeMeanRawHeight)}</td>"
                + $"<td>{Encode(item.Output.Unit)} / {Encode(item.Output.FrameId)}</td>"
                + $"<td class=\"{cell.Decision}\">{Encode(cell.Decision?.ToString() ?? string.Empty)}</td>"
                + $"<td>{Encode(cell.DecisionReason)}</td>"
                + $"<td>{Encode(item.Output.ContentSha256)}</td></tr>")));

        return $"""
               <section>
                 <h2>Completeness cell results</h2>
                 <p>Grid and source-region coordinates are zero-based. Nullable heights remain empty.</p>
                 <div class="table-scroll"><table>
                   <thead><tr><th>Step ID</th><th>Cell ID</th><th>Grid row, column</th><th>Region row, column · size</th><th>Total / finite / missing</th><th>Finite coverage</th><th>Mean raw height</th><th>Reference mean raw height</th><th>Reference-relative mean raw height</th><th>Unit / frame</th><th>Decision</th><th>Reason</th><th>Completeness SHA-256</th></tr></thead>
                   <tbody>{rows}</tbody>
                 </table></div>
               </section>
               """;
    }

    private static string FormatThresholdCorrectionHtml(
        InspectionRunThresholdCorrectionEvidence? snapshot)
    {
        if (snapshot is null)
        {
            return """
            <section>
              <h2>Threshold correction evidence</h2>
              <p><strong>State:</strong> Unavailable</p>
              <p>This legacy Run Record did not record threshold-correction evidence.</p>
            </section>
            """;
        }

        var evidence = snapshot.Evidence;
        var identity = evidence is null
            ? string.Empty
            : $"""
               <dt>Candidate</dt><dd>{Encode(evidence.Proposal.CandidateId)}</dd>
               <dt>Step / Tool</dt><dd>{Encode(evidence.Proposal.StepId)} / {Encode(evidence.Proposal.ToolId)}</dd>
               <dt>Metric / Limit</dt><dd>{Encode(evidence.Proposal.MetricName)} / {Encode(evidence.Proposal.LimitKind.ToString())}</dd>
               """;
        var parameters = evidence is null
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                evidence.Proposal.Changes.Select(change =>
                {
                    var manual = evidence.ManualCorrection?.ParameterChanges
                        .FirstOrDefault(item => string.Equals(
                            item.ParameterName,
                            change.ParameterName,
                            StringComparison.Ordinal));
                    return
                        $"<tr><td>{Encode(change.ParameterName)}</td>"
                        + $"<td>{Encode(change.BeforeValue)}</td>"
                        + $"<td>{Encode(change.ProposedValue)}</td>"
                        + $"<td>{Encode(manual?.ManualValue ?? change.ProposedValue)}</td></tr>";
                }));
        var development = evidence?.ManualCorrection is not { } correction
            ? string.Empty
            : $"""
               <p><strong>Development mismatch:</strong> {correction.BeforeMismatchCount} -&gt; {correction.AfterMismatchCount}</p>
               <p><strong>Before development identities:</strong> {Encode(FormatDevelopmentIdentities(correction.BeforeDevelopmentSamples))}</p>
               <p><strong>Corrected development identities:</strong> {Encode(FormatDevelopmentIdentities(correction.AfterDevelopmentSamples))}</p>
               """;
        var heldOut = evidence is null
            ? string.Empty
            : $"<p><strong>Held-out identities:</strong> {Encode(FormatHeldOutIdentities(evidence.HeldOutSamples))}</p>";
        var parameterTable = evidence is null
            ? string.Empty
            : $"""
               <table>
                 <thead><tr><th>Parameter</th><th>Before</th><th>Suggested</th><th>Committed</th></tr></thead>
                 <tbody>{parameters}</tbody>
               </table>
               """;

        return $"""
               <section>
                 <h2>Threshold correction evidence</h2>
                 <dl>
                   <dt>State</dt><dd>{Encode(snapshot.State.ToString())}</dd>
                   <dt>Sidecar</dt><dd>{Encode(snapshot.SidecarPath)}</dd>
                   <dt>Sidecar SHA-256</dt><dd>{Encode(snapshot.SidecarSha256 ?? "(unavailable)")}</dd>
                   {identity}
                 </dl>
                 <p>{Encode(snapshot.Message)}</p>
                 {parameterTable}
                 {development}
                 {heldOut}
               </section>
               """;
    }

    private static string FormatSourceQualityHtml(
        InspectionRunSourceQualityEvidence? evidence)
    {
        if (evidence?.Report is not { } report)
        {
            return $"""
                   <section>
                     <h2>Source Quality evidence</h2>
                     <p><strong>State:</strong> {Encode(evidence?.State.ToString() ?? "Unavailable")}</p>
                     <p>{Encode(evidence?.Message ?? "This legacy Run Record did not record Source Quality evidence.")}</p>
                   </section>
                   """;
        }

        var channels = string.Join(
            Environment.NewLine,
            report.Channels.Select(channel =>
                $"<tr><td>{Encode(channel.Channel.ToString())}</td><td>{Encode(channel.State.ToString())}</td><td>{Encode(channel.Evidence)}</td></tr>"));
        return $"""
               <section>
                 <h2>Source Quality evidence</h2>
                 <dl>
                   <dt>State</dt><dd>{evidence.State}</dd>
                   <dt>Report SHA-256</dt><dd>{evidence.SourceQualitySha256}</dd>
                   <dt>Source entity</dt><dd>{Encode(report.Source.EntityId)}</dd>
                   <dt>Grid</dt><dd>{report.Grid.Width} x {report.Grid.Height} ({report.Grid.CellCount} cells)</dd>
                   <dt>Coverage</dt><dd>{report.Coverage.ValidSampleCount} valid ({Format(report.Coverage.ValidRatio)}); {report.Coverage.MissingSampleCount} missing ({Format(report.Coverage.MissingRatio)})</dd>
                   <dt>Invalid-cell mask</dt><dd>{Encode(report.Coverage.InvalidCellMask.Encoding)}; {report.Coverage.InvalidCellMask.ByteLength} bytes; SHA-256 {report.Coverage.InvalidCellMask.Sha256}</dd>
                   <dt>Height</dt><dd>{Encode(report.Height.ScalarMeaning)}; min {FormatNullable(report.Height.Minimum)}; max {FormatNullable(report.Height.Maximum)}; mean {FormatNullable(report.Height.Mean)}</dd>
                   <dt>Coordinates</dt><dd>{Encode(report.Coordinates.FrameId)}; {Encode(report.Coordinates.Unit)}; {Encode(report.Coordinates.CoordinateConvention)}</dd>
                   <dt>Provenance</dt><dd>{Encode(report.Provenance)}</dd>
                   <dt>Derived</dt><dd>{report.IsDerived}</dd>
                 </dl>
                 <table><thead><tr><th>Channel</th><th>State</th><th>Evidence</th></tr></thead><tbody>{channels}</tbody></table>
               </section>
               """;
    }

    private static string FormatTimingHtml(InspectionRunTiming? timing)
    {
        if (timing is null)
        {
            return string.Empty;
        }

        var stages = timing.State == InspectionRunTimingState.Available
            ? string.Join(
                Environment.NewLine,
                timing.Stages.Select(stage =>
                    $"<tr><td>{Encode(stage.StageId)}</td><td>{Format(stage.ElapsedMilliseconds)}</td></tr>"))
            : string.Empty;
        var table = stages.Length == 0
            ? string.Empty
            : $"<table><thead><tr><th>Stage</th><th>Elapsed ms</th></tr></thead><tbody>{stages}</tbody></table>";
        return $"""
               <section>
                 <h2>Execution timing</h2>
                 <p><strong>State:</strong> {timing.State}</p>
                 <p>{Encode(timing.Message)}</p>
                 <p><strong>Clock:</strong> {Encode(timing.Clock)}</p>
                 <p><strong>Total:</strong> {FormatNullable(timing.TotalElapsedMilliseconds)} ms</p>
                 {table}
               </section>
               """;
    }

    private static string FormatSurfaceMatchHtml(
        InspectionRunSurfaceMatchEvidence? evidence)
    {
        if (evidence is null)
        {
            return string.Empty;
        }

        var execution = evidence.Execution;
        var poseResult = execution.PoseResult;
        var pose = poseResult.Pose;
        var poseRows = pose is null
            ? "<p>No pose was produced.</p>"
            : $"""
               <table>
                 <thead><tr><th>Unit</th><th>Source frame</th><th>Target frame</th><th>Rotation matrix (row-major)</th><th>Translation XYZ</th></tr></thead>
                 <tbody><tr><td>{Encode(pose.Unit)}</td><td>{Encode(pose.SourceFrameId)}</td><td>{Encode(pose.TargetFrameId)}</td><td>{Format(pose.M11)} {Format(pose.M12)} {Format(pose.M13)}<br>{Format(pose.M21)} {Format(pose.M22)} {Format(pose.M23)}<br>{Format(pose.M31)} {Format(pose.M32)} {Format(pose.M33)}</td><td>{Format(pose.TranslationX)}, {Format(pose.TranslationY)}, {Format(pose.TranslationZ)}</td></tr></tbody>
               </table>
               """;
        var scoreRows = evidence.Score is not { } score
            ? "<p>No score components were produced for this result.</p>"
            : $"""
               <table>
                 <thead><tr><th>Component</th><th>Semantics</th><th>Model count</th><th>Scene count</th><th>Matched</th><th>Unmatched</th><th>Coverage</th><th>Inlier RMSE</th><th>Maximum distance</th></tr></thead>
                 <tbody>
                   <tr><td>Surface</td><td>{Encode(score.SurfaceScore.Semantics)}</td><td>{score.SurfaceScore.ModelSampleCount}</td><td>{score.SurfaceScore.SceneSampleCount}</td><td>{score.SurfaceScore.MatchedModelSampleCount}</td><td>{poseResult.Coverage.UnmatchedModelSampleCount}</td><td>{Format(score.SurfaceScore.CoverageRatio)}</td><td>{FormatNullable(score.SurfaceScore.InlierRmse)}</td><td>{Format(score.SurfaceScore.MaximumCorrespondenceDistance)}</td></tr>
                   <tr><td>Edge</td><td>{Encode(score.EdgeScore.Semantics)}</td><td>{score.EdgeScore.ModelEdgeCount}</td><td>{score.EdgeScore.SceneEdgeCount}</td><td>{score.EdgeScore.MatchedModelEdgeCount}</td><td>{score.EdgeScore.UnmatchedModelEdgeCount}</td><td>{Format(score.EdgeScore.CoverageRatio)}</td><td>{FormatNullable(score.EdgeScore.InlierRmse)}</td><td>{Format(score.EdgeScore.MaximumCorrespondenceDistance)}</td></tr>
                 </tbody>
               </table>
               """;
        var assessmentRows = evidence.Assessment is not { } assessment
            ? "<p>No assessment was produced for this result.</p>"
            : $"""
               <table>
                 <thead><tr><th>Component</th><th>Decision</th><th>Reason</th><th>Raw coverage</th><th>Raw RMSE</th><th>Minimum coverage</th><th>Maximum RMSE</th></tr></thead>
                 <tbody>
                   <tr><td>Surface</td><td>{assessment.Surface.Decision}</td><td>{assessment.Surface.Reason}</td><td>{Format(assessment.Surface.RawCoverageRatio)}</td><td>{FormatNullable(assessment.Surface.RawInlierRmse)}</td><td>{Format(assessment.Surface.MinimumCoverageRatio)}</td><td>{Format(assessment.Surface.MaximumInlierRmse)}</td></tr>
                   <tr><td>Edge</td><td>{assessment.Edge.Decision}</td><td>{assessment.Edge.Reason}</td><td>{Format(assessment.Edge.RawCoverageRatio)}</td><td>{FormatNullable(assessment.Edge.RawInlierRmse)}</td><td>{Format(assessment.Edge.MinimumCoverageRatio)}</td><td>{Format(assessment.Edge.MaximumInlierRmse)}</td></tr>
                 </tbody>
               </table>
               <p><strong>Overall assessment:</strong> {assessment.Decision} ({assessment.Reason})</p>
               """;

        return $"""
               <section>
                 <h2>Surface Match evidence</h2>
                 <dl>
                   <dt>Semantics</dt><dd>{Encode(evidence.Semantics)}</dd>
                   <dt>Model</dt><dd>{Encode(evidence.ModelArtifactId)}<br>SHA-256 {evidence.ModelContentSha256}</dd>
                   <dt>Scene</dt><dd>{Encode(evidence.SceneArtifactId)}<br>SHA-256 {evidence.SceneContentSha256}</dd>
                   <dt>Execution SHA-256</dt><dd>{execution.ContentSha256}</dd>
                   <dt>Pose SHA-256</dt><dd>{poseResult.ContentSha256}</dd>
                   <dt>Score SHA-256</dt><dd>{Encode(evidence.Score?.ContentSha256 ?? "(none)")}</dd>
                   <dt>Assessment SHA-256</dt><dd>{Encode(evidence.Assessment?.ContentSha256 ?? "(none)")}</dd>
                   <dt>Pose state</dt><dd>{poseResult.State}</dd>
                   <dt>Evaluated candidates</dt><dd>{poseResult.EvaluatedCandidateCount}</dd>
                 </dl>
                 {poseRows}
                 {scoreRows}
                 {assessmentRows}
               </section>
               """;
    }

    private static string FormatDevelopmentIdentities(
        IReadOnlyList<ToolRecipeThresholdDevelopmentSampleEvidence> samples) =>
        string.Join(
            "; ",
            samples.Select(sample =>
                $"{sample.SampleOrder}:{sample.Role}:{sample.SampleIdentity}:{sample.Status}:expectedMatch={sample.ExpectedMatch}"));

    private static string FormatHeldOutIdentities(
        IReadOnlyList<ToolRecipeThresholdHeldOutSampleEvidence> samples) =>
        string.Join(
            "; ",
            samples.Select(sample =>
                $"{sample.SampleOrder}:{sample.SampleIdentity}:{sample.Status}"));

    private static void WriteCsv(string path, InspectionRunRecord record)
    {
        EnsureDirectory(path);
        if (record.SurfaceMatchEvidence is not null)
        {
            WriteSurfaceMatchCsv(path, record);
            return;
        }
        if (record.Steps is { Count: > 0 })
        {
            WriteMultiStepCsv(path, record);
            return;
        }
        var lines = new List<string> { "runId,recordedAtUtc,tool,stepId,stepSourceEntityId,stepReferenceIds,stepMeasurementIds,status,metric,kind,value,unit,metricStatus,recipeSha256,sourceSha256,viewerRunnerMatch,applicationName,applicationVersion,viewerHostApiVersion,gitCommit,gitWorkingTree,dotNetRuntime,operatingSystem,processArchitecture" };
        lines.AddRange(record.Metrics.Select(metric => string.Join(',',
            Csv(record.RunId),
            Csv(record.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            Csv(record.ToolName),
            Csv(record.Step?.Id ?? string.Empty),
            Csv(record.Step?.SourceEntityId ?? string.Empty),
            Csv(record.Step is null ? string.Empty : FormatIds(record.Step.ReferenceIds)),
            Csv(record.Step is null ? string.Empty : FormatIds(record.Step.MeasurementIds)),
            Csv(record.Status.ToString()),
            Csv(metric.Name),
            Csv(metric.Kind.ToString()),
            Csv(Format(metric.Value)),
            Csv(metric.Unit),
            Csv(metric.Status?.ToString() ?? string.Empty),
            Csv(record.Recipe.Sha256),
            Csv(record.Source.Sha256),
            Csv(record.ViewerRunnerMatchState),
            Csv(record.ExecutionEnvironment?.ApplicationName ?? "unknown"),
            Csv(record.ExecutionEnvironment?.ApplicationVersion ?? "unknown"),
            Csv(record.ExecutionEnvironment?.ViewerHostApiVersion ?? "unknown"),
            Csv(record.ExecutionEnvironment?.GitCommit ?? "unknown"),
            Csv(record.ExecutionEnvironment?.GitWorkingTree ?? "unknown"),
            Csv(record.ExecutionEnvironment?.DotNetRuntime ?? "unknown"),
            Csv(record.ExecutionEnvironment?.OperatingSystem ?? "unknown"),
            Csv(record.ExecutionEnvironment?.ProcessArchitecture ?? "unknown"))));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static void WriteSurfaceMatchCsv(
        string path,
        InspectionRunRecord record)
    {
        var evidence = record.SurfaceMatchEvidence!;
        var execution = evidence.Execution;
        var poseResult = execution.PoseResult;
        var lines = new List<string>
        {
            "runId,component,field,value,unit,sourceContentSha256"
        };
        void Add(
            string component,
            string field,
            string value,
            string unit,
            string sourceSha256) =>
            lines.Add(string.Join(',',
                Csv(record.RunId),
                Csv(component),
                Csv(field),
                Csv(value),
                Csv(unit),
                Csv(sourceSha256)));

        AddSourceQualityCsvRows(Add, record.SourceQualityEvidence);

        Add("identity", "modelArtifactId", evidence.ModelArtifactId, string.Empty, evidence.ModelContentSha256);
        Add("identity", "sceneArtifactId", evidence.SceneArtifactId, string.Empty, evidence.SceneContentSha256);
        Add("identity", "executionContentSha256", execution.ContentSha256, string.Empty, execution.ContentSha256);
        Add("pose", "state", poseResult.State.ToString(), string.Empty, poseResult.ContentSha256);
        Add("pose", "evaluatedCandidateCount", poseResult.EvaluatedCandidateCount.ToString(CultureInfo.InvariantCulture), "count", poseResult.ContentSha256);
        Add("pose", "rejectionReason", poseResult.RejectionReason ?? string.Empty, string.Empty, poseResult.ContentSha256);
        var timing = record.Timing;
        Add("timing", "state", timing?.State.ToString() ?? "Unavailable", string.Empty, execution.ContentSha256);
        Add("timing", "clock", timing?.Clock ?? string.Empty, string.Empty, execution.ContentSha256);
        Add("timing", "totalElapsedMilliseconds", timing?.TotalElapsedMilliseconds is { } total ? Format(total) : string.Empty, "ms", execution.ContentSha256);
        if (timing is not null)
        {
            foreach (var stage in timing.Stages)
            {
                Add("timing", stage.StageId, Format(stage.ElapsedMilliseconds), "ms", execution.ContentSha256);
            }
        }
        if (poseResult.Pose is { } pose)
        {
            Add("pose", "sourceFrameId", pose.SourceFrameId, string.Empty, poseResult.ContentSha256);
            Add("pose", "targetFrameId", pose.TargetFrameId, string.Empty, poseResult.ContentSha256);
            Add("pose", "m11", Format(pose.M11), "ratio", poseResult.ContentSha256);
            Add("pose", "m12", Format(pose.M12), "ratio", poseResult.ContentSha256);
            Add("pose", "m13", Format(pose.M13), "ratio", poseResult.ContentSha256);
            Add("pose", "m21", Format(pose.M21), "ratio", poseResult.ContentSha256);
            Add("pose", "m22", Format(pose.M22), "ratio", poseResult.ContentSha256);
            Add("pose", "m23", Format(pose.M23), "ratio", poseResult.ContentSha256);
            Add("pose", "m31", Format(pose.M31), "ratio", poseResult.ContentSha256);
            Add("pose", "m32", Format(pose.M32), "ratio", poseResult.ContentSha256);
            Add("pose", "m33", Format(pose.M33), "ratio", poseResult.ContentSha256);
            Add("pose", "translationX", Format(pose.TranslationX), pose.Unit, poseResult.ContentSha256);
            Add("pose", "translationY", Format(pose.TranslationY), pose.Unit, poseResult.ContentSha256);
            Add("pose", "translationZ", Format(pose.TranslationZ), pose.Unit, poseResult.ContentSha256);
        }

        if (evidence.Score is { } score)
        {
            AddScoreRows(Add, "surface-score", score.SurfaceScore.Semantics, score.SurfaceScore.ModelSampleCount, score.SurfaceScore.SceneSampleCount, score.SurfaceScore.MatchedModelSampleCount, poseResult.Coverage.UnmatchedModelSampleCount, score.SurfaceScore.CoverageRatio, score.SurfaceScore.InlierRmse, score.SurfaceScore.MaximumCorrespondenceDistance, poseResult.Pose?.Unit ?? string.Empty, score.ContentSha256);
            AddScoreRows(Add, "edge-score", score.EdgeScore.Semantics, score.EdgeScore.ModelEdgeCount, score.EdgeScore.SceneEdgeCount, score.EdgeScore.MatchedModelEdgeCount, score.EdgeScore.UnmatchedModelEdgeCount, score.EdgeScore.CoverageRatio, score.EdgeScore.InlierRmse, score.EdgeScore.MaximumCorrespondenceDistance, poseResult.Pose?.Unit ?? string.Empty, score.ContentSha256);
            Add("edge-score", "matchCount", score.EdgeScore.Matches.Length.ToString(CultureInfo.InvariantCulture), "count", score.ContentSha256);
            Add("edge-score", "evidence", score.EdgeScore.Evidence, string.Empty, score.ContentSha256);
        }

        if (evidence.Assessment is { } assessment)
        {
            AddAssessmentRows(Add, "surface-assessment", assessment.Surface, assessment.ContentSha256);
            AddAssessmentRows(Add, "edge-assessment", assessment.Edge, assessment.ContentSha256);
            Add("assessment", "decision", assessment.Decision.ToString(), string.Empty, assessment.ContentSha256);
            Add("assessment", "reason", assessment.Reason.ToString(), string.Empty, assessment.ContentSha256);
            Add("assessment", "policyContentSha256", assessment.Policy.ContentSha256, string.Empty, assessment.ContentSha256);
        }

        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static void AddScoreRows(
        Action<string, string, string, string, string> add,
        string component,
        string semantics,
        int modelCount,
        int sceneCount,
        int matchedCount,
        int unmatchedCount,
        double coverage,
        double? rmse,
        double maximumDistance,
        string unit,
        string sourceSha256)
    {
        add(component, "semantics", semantics, string.Empty, sourceSha256);
        add(component, "modelCount", modelCount.ToString(CultureInfo.InvariantCulture), "count", sourceSha256);
        add(component, "sceneCount", sceneCount.ToString(CultureInfo.InvariantCulture), "count", sourceSha256);
        add(component, "matchedCount", matchedCount.ToString(CultureInfo.InvariantCulture), "count", sourceSha256);
        add(component, "unmatchedCount", unmatchedCount.ToString(CultureInfo.InvariantCulture), "count", sourceSha256);
        add(component, "coverageRatio", Format(coverage), "ratio", sourceSha256);
        add(component, "inlierRmse", FormatNullable(rmse), unit, sourceSha256);
        add(component, "maximumCorrespondenceDistance", Format(maximumDistance), unit, sourceSha256);
    }

    private static void AddSourceQualityCsvRows(
        Action<string, string, string, string, string> add,
        InspectionRunSourceQualityEvidence? evidence)
    {
        var reportSha = evidence?.SourceQualitySha256 ?? string.Empty;
        add("source-quality", "state", evidence?.State.ToString() ?? "Unavailable", string.Empty, reportSha);
        if (evidence?.Report is not { } report)
        {
            add("source-quality", "message", evidence?.Message ?? "Legacy Run Record", string.Empty, reportSha);
            return;
        }

        add("source-quality", "reportSha256", evidence.SourceQualitySha256, string.Empty, reportSha);
        add("source-quality", "gridWidth", report.Grid.Width.ToString(CultureInfo.InvariantCulture), "cells", reportSha);
        add("source-quality", "gridHeight", report.Grid.Height.ToString(CultureInfo.InvariantCulture), "cells", reportSha);
        add("source-quality", "cellCount", report.Grid.CellCount.ToString(CultureInfo.InvariantCulture), "count", reportSha);
        add("source-quality", "validSampleCount", report.Coverage.ValidSampleCount.ToString(CultureInfo.InvariantCulture), "count", reportSha);
        add("source-quality", "missingSampleCount", report.Coverage.MissingSampleCount.ToString(CultureInfo.InvariantCulture), "count", reportSha);
        add("source-quality", "validRatio", Format(report.Coverage.ValidRatio), "ratio", reportSha);
        add("source-quality", "missingRatio", Format(report.Coverage.MissingRatio), "ratio", reportSha);
        add("source-quality", "invalidCellMaskSha256", report.Coverage.InvalidCellMask.Sha256, string.Empty, reportSha);
        add("source-quality", "frameId", report.Coordinates.FrameId, string.Empty, reportSha);
        add("source-quality", "unit", report.Coordinates.Unit, string.Empty, reportSha);
        add("source-quality", "coordinateConvention", report.Coordinates.CoordinateConvention, string.Empty, reportSha);
        add("source-quality", "provenance", report.Provenance, string.Empty, reportSha);
        add("source-quality", "isDerived", report.IsDerived.ToString(CultureInfo.InvariantCulture), string.Empty, reportSha);
        foreach (var channel in report.Channels)
        {
            add("source-quality-channel", $"{channel.Channel}.state", channel.State.ToString(), string.Empty, reportSha);
            add("source-quality-channel", $"{channel.Channel}.evidence", channel.Evidence, string.Empty, reportSha);
        }
    }

    private static void AddAssessmentRows(
        Action<string, string, string, string, string> add,
        string component,
        SurfaceAndEdgeComponentAssessment assessment,
        string sourceSha256)
    {
        add(component, "decision", assessment.Decision.ToString(), string.Empty, sourceSha256);
        add(component, "reason", assessment.Reason.ToString(), string.Empty, sourceSha256);
        add(component, "rawCoverageRatio", Format(assessment.RawCoverageRatio), "ratio", sourceSha256);
        add(component, "rawInlierRmse", FormatNullable(assessment.RawInlierRmse), string.Empty, sourceSha256);
        add(component, "minimumCoverageRatio", Format(assessment.MinimumCoverageRatio), "ratio", sourceSha256);
        add(component, "maximumInlierRmse", Format(assessment.MaximumInlierRmse), string.Empty, sourceSha256);
    }

    private static void WriteMultiStepCsv(string path, InspectionRunRecord record)
    {
        var lines = new List<string> { "runId,recordedAtUtc,recipeIndex,stepId,toolId,toolName,inputEntityIds,outputEntityId,stepStatus,elapsedMilliseconds,timingState,timingClock,stageTimings,outputContentSha256,overlayIds,metric,kind,value,unit,metricStatus,recipeSha256,sourceSha256,viewerRunnerMatch,sourceQualityState,sourceQualitySha256,sourceQualityGrid,sourceQualityValidCount,sourceQualityMissingCount,sourceQualityValidRatio,sourceQualityMissingRatio,sourceQualityInvalidMaskSha256,sourceQualityFrame,sourceQualityUnit,sourceQualityProvenance,sourceQualityChannels,rowType,completenessContentSha256,completenessUnit,completenessFrame,cellId,gridRow,gridColumn,regionRow,regionColumn,regionRowCount,regionColumnCount,totalCellCount,finiteCellCount,missingCellCount,finiteCoverageRatio,meanRawHeight,referenceMeanRawHeight,referenceRelativeMeanRawHeight,decision,decisionReason" };
        lines.AddRange(record.Steps!.SelectMany(step =>
        {
            var stepRows = step.Metrics.Count == 0
                ? [FormatMultiStepCsvRow(record, step, null, null)]
                : step.Metrics.Select(metric =>
                    FormatMultiStepCsvRow(record, step, metric, null));
            var cellRows = step.CompletenessGrid?.Cells.Select(cell =>
                FormatMultiStepCsvRow(record, step, null, cell)) ?? [];
            return stepRows.Concat(cellRows);
        }));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static string FormatMultiStepCsvRow(
        InspectionRunRecord record,
        InspectionRunStepResult step,
        InspectionRunMetric? metric,
        C3DCompletenessCellMetric? cell) =>
        string.Join(',',
            Csv(record.RunId),
            Csv(record.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            Csv(step.RecipeIndex.ToString(CultureInfo.InvariantCulture)),
            Csv(step.Id),
            Csv(step.ToolId),
            Csv(step.ToolName),
            Csv(FormatIds(step.InputEntityIds)),
            Csv(step.OutputEntityId),
            Csv(step.Status.ToString()),
            Csv(Format(step.ElapsedMilliseconds)),
            Csv(step.Timing?.State.ToString() ?? "Unavailable"),
            Csv(step.Timing?.Clock ?? string.Empty),
            Csv(FormatStageTimings(step.Timing)),
            Csv(step.OutputContentSha256 ?? string.Empty),
            Csv(FormatIds(step.Overlays.Select(overlay => overlay.Id).ToArray())),
            Csv(metric?.Name ?? string.Empty),
            Csv(metric?.Kind.ToString() ?? string.Empty),
            Csv(metric is null ? string.Empty : Format(metric.Value)),
            Csv(metric?.Unit ?? string.Empty),
            Csv(metric?.Status?.ToString() ?? string.Empty),
            Csv(record.Recipe.Sha256),
            Csv(record.Source.Sha256),
            Csv(record.ViewerRunnerMatchState),
            Csv(record.SourceQualityEvidence?.State.ToString() ?? "Unavailable"),
            Csv(record.SourceQualityEvidence?.SourceQualitySha256 ?? string.Empty),
            Csv(FormatSourceQualityGrid(record.SourceQualityEvidence)),
            Csv(record.SourceQualityEvidence?.Report?.Coverage.ValidSampleCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(record.SourceQualityEvidence?.Report?.Coverage.MissingSampleCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(record.SourceQualityEvidence?.Report is { } quality ? Format(quality.Coverage.ValidRatio) : string.Empty),
            Csv(record.SourceQualityEvidence?.Report is { } missingQuality ? Format(missingQuality.Coverage.MissingRatio) : string.Empty),
            Csv(record.SourceQualityEvidence?.Report?.Coverage.InvalidCellMask.Sha256 ?? string.Empty),
            Csv(record.SourceQualityEvidence?.Report?.Coordinates.FrameId ?? string.Empty),
            Csv(record.SourceQualityEvidence?.Report?.Coordinates.Unit ?? string.Empty),
            Csv(record.SourceQualityEvidence?.Report?.Provenance ?? string.Empty),
            Csv(FormatSourceQualityChannels(record.SourceQualityEvidence)),
            Csv(cell is not null ? "completenessCell" : metric is not null ? "stepMetric" : "step"),
            Csv(step.CompletenessGrid?.ContentSha256 ?? string.Empty),
            Csv(step.CompletenessGrid?.Unit ?? string.Empty),
            Csv(step.CompletenessGrid?.FrameId ?? string.Empty),
            Csv(cell?.CellId ?? string.Empty),
            Csv(cell?.GridRow.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.GridColumn.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.Region.Row.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.Region.Column.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.Region.RowCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.Region.ColumnCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.TotalCellCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.FiniteCellCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell?.MissingCellCount.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
            Csv(cell is null ? string.Empty : Format(cell.FiniteCoverageRatio)),
            Csv(cell?.MeanRawHeight is { } mean ? Format(mean) : string.Empty),
            Csv(cell is null ? string.Empty : Format(cell.ReferenceMeanRawHeight)),
            Csv(cell?.ReferenceRelativeMeanRawHeight is { } relative
                ? Format(relative)
                : string.Empty),
            Csv(cell?.Decision?.ToString() ?? string.Empty),
            Csv(cell?.DecisionReason ?? string.Empty));

    private static string FormatHtmlStepRow(
        InspectionRunStepResult step,
        InspectionRunMetric? metric) =>
        $"<tr><td>{step.RecipeIndex + 1}</td><td>{Encode(step.Id)}</td><td>{Encode(step.ToolName)}</td>"
        + $"<td>{Encode($"{FormatIds(step.InputEntityIds)} -> {step.OutputEntityId}")}</td>"
        + $"<td class=\"{step.Status}\">{step.Status}</td><td>{Format(step.ElapsedMilliseconds)}</td>"
        + $"<td>{Encode(FormatStageTimings(step.Timing))}</td>"
        + $"<td>{Encode(step.OutputContentSha256 ?? string.Empty)}</td><td>{Encode(metric?.Name ?? string.Empty)}</td>"
        + $"<td>{Encode(metric?.Kind.ToString() ?? string.Empty)}</td><td>{(metric is null ? string.Empty : Format(metric.Value))}</td>"
        + $"<td>{Encode(metric?.Unit ?? string.Empty)}</td><td>{Encode(metric?.Status?.ToString() ?? string.Empty)}</td>"
        + $"<td>{Encode(FormatIds(step.Overlays.Select(overlay => overlay.Id).ToArray()))}</td></tr>";

    private static string FormatStageTimings(InspectionRunTiming? timing) =>
        timing is not { State: InspectionRunTimingState.Available }
            ? "Unavailable"
            : string.Join(
                "; ",
                timing.Stages.Select(stage =>
                    $"{stage.StageId}={Format(stage.ElapsedMilliseconds)} ms"));

    private static string FormatSourceQualityGrid(
        InspectionRunSourceQualityEvidence? evidence) =>
        evidence?.Report is { } report
            ? $"{report.Grid.Width}x{report.Grid.Height}"
            : string.Empty;

    private static string FormatSourceQualityChannels(
        InspectionRunSourceQualityEvidence? evidence) =>
        evidence?.Report is { } report
            ? string.Join(
                "; ",
                report.Channels.Select(channel =>
                    $"{channel.Channel}={channel.State}:{channel.Evidence}"))
            : string.Empty;

    private static InspectionRunEnvironment CreateExecutionEnvironment(string recipePath)
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(RunRecordWriter).Assembly;
        var metadata = assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value, StringComparer.Ordinal);
        var applicationVersion = metadata.GetValueOrDefault("OpenVisionLabProductVersion")
            ?? assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "unknown";
        var viewerHostApiVersion = metadata.GetValueOrDefault("OpenVisionLabViewerHostApiVersion") ?? "unknown";
        var (gitCommit, gitWorkingTree) = ReadGitIdentity(recipePath);

        return new InspectionRunEnvironment(
            assembly.GetName().Name ?? "OpenVisionLab.ThreeD.Runner",
            applicationVersion,
            viewerHostApiVersion,
            gitCommit,
            gitWorkingTree,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.OSDescription,
            RuntimeInformation.ProcessArchitecture.ToString());
    }

    private static (string Commit, string WorkingTree) ReadGitIdentity(string recipePath)
    {
        var workingDirectory = FindGitWorkingDirectory(Path.GetDirectoryName(Path.GetFullPath(recipePath)))
            ?? FindGitWorkingDirectory(Environment.CurrentDirectory);
        if (workingDirectory is null)
        {
            return (Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown", "unknown");
        }

        var commit = RunGit(workingDirectory, "rev-parse", "HEAD");
        var status = RunGit(workingDirectory, "status", "--porcelain");
        return (
            string.IsNullOrWhiteSpace(commit) ? Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown" : commit,
            status is null ? "unknown" : string.IsNullOrWhiteSpace(status) ? "clean" : "dirty");
    }

    private static string? FindGitWorkingDirectory(string? startPath)
    {
        var directory = string.IsNullOrWhiteSpace(startPath) ? null : new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static string? RunGit(string workingDirectory, params string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("git")
                {
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
            if (!process.Start()) return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (!process.WaitForExit(3000) || process.ExitCode != 0) return null;
            return output;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    private static string FormatApplication(InspectionRunEnvironment? environment) =>
        environment is null ? "unknown" : $"{environment.ApplicationName} {environment.ApplicationVersion}";

    private static string FormatGit(InspectionRunEnvironment? environment) =>
        environment is null ? "unknown" : $"{environment.GitCommit} ({environment.GitWorkingTree})";

    private static string FormatPlatform(InspectionRunEnvironment? environment) =>
        environment is null ? "unknown" : $"{environment.OperatingSystem} / {environment.ProcessArchitecture}";

    private static string FormatStep(InspectionRunStep? step) => step is null
        ? "Not recorded"
        : $"{step.Id} | Source {step.SourceEntityId} | References {FormatIds(step.ReferenceIds)} | Measurements {FormatIds(step.MeasurementIds)}";

    private static string FormatStepSummary(InspectionRunRecord record) => record.Steps is { Count: > 0 } steps
        ? $"{steps.Count} ordered steps"
        : FormatStep(record.Step);

    private static string FormatIds(IReadOnlyList<string>? ids) =>
        ids is null || ids.Count == 0 ? "(none)" : string.Join(";", ids);

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string? FullOptionalPath(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    private static void EnsureDirectory(string path) => Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string FormatNullable(double? value) =>
        value.HasValue ? Format(value.Value) : string.Empty;
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

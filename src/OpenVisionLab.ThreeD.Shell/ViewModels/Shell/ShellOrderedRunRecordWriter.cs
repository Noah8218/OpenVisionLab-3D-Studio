using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal sealed record ShellOrderedRunRecordArtifact(
    InspectionRunRecord Record,
    string JsonPath,
    string ReportPath);

internal static class ShellOrderedRunRecordWriter
{
    internal const string EvidenceState = "SameOrderedGraphEngine";

    public static ShellOrderedRunRecordArtifact Write(
        string recipePath,
        ToolRecipeDocument document,
        string sourcePath,
        ToolRecipeOrderedGraphExecutionResult execution,
        string? outputRoot = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(execution);

        var identity = OrderedRunRecordIdentity.Create(recipePath, sourcePath);
        var root = Path.GetFullPath(outputRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "ThreeDStudio",
            "Runs"));
        Directory.CreateDirectory(root);

        var baseRunId = identity.RunId;
        var runId = baseRunId;
        var runDirectory = Path.Combine(root, runId);
        for (var suffix = 2; Directory.Exists(runDirectory); suffix++)
        {
            runId = $"{baseRunId}-{suffix}";
            runDirectory = Path.Combine(root, runId);
        }

        Directory.CreateDirectory(runDirectory);
        var jsonPath = Path.Combine(runDirectory, "run-record.json");
        var reportPath = Path.Combine(runDirectory, "ordered-run.txt");
        identity = identity with { RunId = runId };
        var record = OrderedRunRecordFactory.Create(
            identity,
            document,
            execution,
            EvidenceState,
            new InspectionRunArtifacts(
                reportPath,
                null,
                null,
                jsonPath,
                null,
                null));

        File.WriteAllText(
            reportPath,
            CreateReport(record, execution),
            new UTF8Encoding(false));
        InspectionRunRecordJson.Write(jsonPath, record);
        return new ShellOrderedRunRecordArtifact(record, jsonPath, reportPath);
    }

    private static string CreateReport(
        InspectionRunRecord record,
        ToolRecipeOrderedGraphExecutionResult execution)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Studio ordered recipe Run",
            $"RunId|{record.RunId}",
            $"RecordedAtUtc|{record.RecordedAtUtc:O}",
            $"Recipe|{record.Recipe.Path}|sha256={record.Recipe.Sha256}",
            $"Source|{record.Source.Path}|sha256={record.Source.Sha256}",
            $"SourceQuality|{FormatSourceQuality(record.SourceQualityEvidence)}",
            $"ToolResult|{record.ToolName}|{record.Status}|{record.Message}",
            $"ElapsedMilliseconds|{record.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}"
        };
        foreach (var step in execution.Steps)
        {
            var recordedStep = record.Steps![step.Order - 1];
            lines.Add(
                $"Step|order={step.Order}|id={step.StepId}|tool={step.ToolId}|status={step.Result.Status}|output={step.OutputEntityId}|sha256={step.OutputContentSha256 ?? "(none)"}|elapsedMs={recordedStep.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}|stages={FormatStages(recordedStep.Timing)}");
            lines[^1] += $"|levelFrameSha256={step.LevelFrameContentSha256 ?? "(none)"}|levelFrameQualitySha256={step.LevelFrameQualityContentSha256 ?? "(none)"}|frameChainSha256={step.FrameChainContentSha256 ?? "(none)"}";
            lines.Add($"Evidence|order={step.Order}|{step.Evidence}");
            lines.AddRange(step.Result.Metrics
                .Where(metric => double.IsFinite(metric.Value))
                .Select(metric =>
                    $"Metric|order={step.Order}|name={metric.Name}|kind={metric.Kind}|value={metric.Value.ToString("G17", CultureInfo.InvariantCulture)}|unit={metric.Unit}|status={metric.Status?.ToString() ?? "(none)"}"));
        }

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static string FormatStages(InspectionRunTiming? timing) =>
        timing is not { State: InspectionRunTimingState.Available }
            ? "Unavailable"
            : string.Join(
                ';',
                timing.Stages.Select(stage =>
                    $"{stage.StageId}:{stage.ElapsedMilliseconds.ToString("F3", CultureInfo.InvariantCulture)}"));

    private static string FormatSourceQuality(
        InspectionRunSourceQualityEvidence? evidence) =>
        evidence?.Report is not { } report
            ? $"state={evidence?.State.ToString() ?? "Unavailable"}|message={evidence?.Message ?? "Legacy Run Record"}"
            : string.Create(
                CultureInfo.InvariantCulture,
                $"state={evidence.State}|sha256={evidence.SourceQualitySha256}|grid={report.Grid.Width}x{report.Grid.Height}|valid={report.Coverage.ValidSampleCount}|missing={report.Coverage.MissingSampleCount}|validRatio={report.Coverage.ValidRatio:R}|missingRatio={report.Coverage.MissingRatio:R}|maskSha256={report.Coverage.InvalidCellMask.Sha256}|frame={report.Coordinates.FrameId}|unit={report.Coordinates.Unit}|provenance={report.Provenance}|channels={string.Join(";", report.Channels.Select(channel => $"{channel.Channel}:{channel.State}:{channel.Evidence}"))}|gridDiagnostics={FormatSourceQualityGridDiagnostics(report)}");

    private static string FormatSourceQualityGridDiagnostics(
        SourceQualityReport report) =>
        report.GridDiagnostics is { } diagnostics
            ? JsonSerializer.Serialize(diagnostics)
            : string.Empty;

}

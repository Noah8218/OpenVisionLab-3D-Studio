using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal sealed record ShellOrderedRunRecordArtifact(
    InspectionRunRecord Record,
    string JsonPath,
    string ReportPath);

internal static class ShellOrderedRunRecordWriter
{
    internal const string EvidenceState = "SameOrderedGraphEngine";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

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

        var fullRecipePath = Path.GetFullPath(recipePath);
        var fullSourcePath = Path.GetFullPath(sourcePath);
        var recipeHash = HashFile(fullRecipePath);
        var sourceHash = HashFile(fullSourcePath);
        var recordedAt = DateTimeOffset.UtcNow;
        var root = Path.GetFullPath(outputRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "OpenVisionLab",
            "ThreeDStudio",
            "Runs"));
        Directory.CreateDirectory(root);

        var baseRunId =
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}";
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
        var steps = ToolRecipeOrderedGraphRunRecordProjection.Create(
            document,
            execution);
        var metrics = steps.SelectMany(step => step.Metrics).ToArray();
        var overlays = steps.SelectMany(step => step.Overlays).ToArray();
        var source = new InspectionRunSource(
            document.Source.Id,
            fullSourcePath,
            sourceHash,
            new FileInfo(fullSourcePath).Length,
            document.Source.Unit);
        var record = new InspectionRunRecord(
            "1.9",
            runId,
            recordedAt,
            new InspectionRunRecipe(
                "tool-recipe",
                document.SchemaVersion,
                fullRecipePath,
                recipeHash),
            source,
            "Ordered Tool Recipe Replay",
            execution.Status,
            execution.Message,
            execution.Duration.TotalMilliseconds,
            metrics,
            overlays,
            EvidenceState,
            new InspectionRunArtifacts(
                reportPath,
                null,
                null,
                jsonPath,
                null,
                null))
        {
            Steps = steps,
            SourceQualityEvidence = execution.SourceQuality is null
                ? InspectionRunSourceQualityEvidence.Unavailable(
                    "Source Quality was unavailable because the ordered source could not be analyzed.")
                : InspectionRunSourceQualityEvidence.Available(
                    source,
                    execution.SourceQuality),
            ThresholdCorrectionEvidence =
                ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                    fullRecipePath,
                    document)
        };

        File.WriteAllText(
            reportPath,
            CreateReport(record, execution),
            new UTF8Encoding(false));
        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(record, JsonOptions),
            new UTF8Encoding(false));
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
                $"state={evidence.State}|sha256={evidence.SourceQualitySha256}|grid={report.Grid.Width}x{report.Grid.Height}|valid={report.Coverage.ValidSampleCount}|missing={report.Coverage.MissingSampleCount}|validRatio={report.Coverage.ValidRatio:R}|missingRatio={report.Coverage.MissingRatio:R}|maskSha256={report.Coverage.InvalidCellMask.Sha256}|frame={report.Coordinates.FrameId}|unit={report.Coordinates.Unit}|provenance={report.Provenance}|channels={string.Join(";", report.Channels.Select(channel => $"{channel.Channel}:{channel.State}:{channel.Evidence}"))}");

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}

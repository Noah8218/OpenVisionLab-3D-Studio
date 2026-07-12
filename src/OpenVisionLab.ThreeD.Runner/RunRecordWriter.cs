using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

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
        ToolResult result,
        string runnerReportPath,
        string? viewerContractPath)
    {
        if (!options.Requested) return;

        var recordedAt = DateTimeOffset.UtcNow;
        var recipeHash = HashFile(recipePath);
        var sourceHash = HashFile(sourcePath);
        var record = new InspectionRunRecord(
            "1.0",
            $"run-{recordedAt:yyyyMMddTHHmmssfffZ}-{recipeHash[..12].ToLowerInvariant()}",
            recordedAt,
            new InspectionRunRecipe(recipeType, recipeVersion, Path.GetFullPath(recipePath), recipeHash),
            new InspectionRunSource(sourceEntityId, Path.GetFullPath(sourcePath), sourceHash, new FileInfo(sourcePath).Length, sourceUnit),
            result.ToolName,
            result.Status,
            result.Message,
            result.Elapsed.TotalMilliseconds,
            result.Metrics.Select(metric => new InspectionRunMetric(metric.Name, metric.Kind, metric.Value, metric.Unit, metric.Status)).ToArray(),
            result.Overlays.Select(overlay => new InspectionRunOverlay(overlay.Id, overlay.Kind, overlay.Label, overlay.Status, overlay.SourceEntityId)).ToArray(),
            viewerContractPath is null ? "NotCompared" : "Matched",
            new InspectionRunArtifacts(
                Path.GetFullPath(runnerReportPath),
                FullOptionalPath(viewerContractPath),
                FullOptionalPath(options.ViewerScreenshotPath),
                FullOptionalPath(options.JsonPath),
                FullOptionalPath(options.HtmlPath),
                FullOptionalPath(options.CsvPath)));

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
        var rows = string.Join(Environment.NewLine, record.Metrics.Select(metric =>
            $"<tr><td>{Encode(metric.Name)}</td><td>{Encode(metric.Kind.ToString())}</td><td>{Format(metric.Value)}</td><td>{Encode(metric.Unit)}</td><td>{Encode(metric.Status?.ToString() ?? string.Empty)}</td></tr>"));
        var html = $$"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <title>OpenVisionLab 3D Inspection Run</title>
          <style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#111827}h1{font-size:22px}dl{display:grid;grid-template-columns:150px 1fr;gap:6px 12px}dt{font-weight:600}dd{margin:0;overflow-wrap:anywhere}table{border-collapse:collapse;width:100%;margin-top:16px}th,td{border:1px solid #d1d5db;padding:7px;text-align:left}th{background:#f3f4f6}.Pass{color:#047857}.Fail,.Error{color:#b91c1c}</style>
        </head>
        <body>
          <h1>OpenVisionLab 3D Inspection Run</h1>
          <dl>
            <dt>Run ID</dt><dd>{{Encode(record.RunId)}}</dd>
            <dt>Recorded UTC</dt><dd>{{Encode(record.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture))}}</dd>
            <dt>Tool</dt><dd>{{Encode(record.ToolName)}}</dd>
            <dt>Status</dt><dd class="{{record.Status}}">{{record.Status}}</dd>
            <dt>Recipe</dt><dd>{{Encode(record.Recipe.Path)}}<br>SHA-256 {{record.Recipe.Sha256}}</dd>
            <dt>Source</dt><dd>{{Encode(record.Source.Path)}}<br>SHA-256 {{record.Source.Sha256}}</dd>
            <dt>Viewer/Runner</dt><dd>{{Encode(record.ViewerRunnerMatchState)}}</dd>
          </dl>
          <p>{{Encode(record.Message)}}</p>
          <table><thead><tr><th>Metric</th><th>Kind</th><th>Value</th><th>Unit</th><th>Status</th></tr></thead><tbody>
          {{rows}}
          </tbody></table>
        </body>
        </html>
        """;
        File.WriteAllText(path, html, new UTF8Encoding(false));
    }

    private static void WriteCsv(string path, InspectionRunRecord record)
    {
        EnsureDirectory(path);
        var lines = new List<string> { "runId,recordedAtUtc,tool,status,metric,kind,value,unit,metricStatus,recipeSha256,sourceSha256,viewerRunnerMatch" };
        lines.AddRange(record.Metrics.Select(metric => string.Join(',',
            Csv(record.RunId),
            Csv(record.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            Csv(record.ToolName),
            Csv(record.Status.ToString()),
            Csv(metric.Name),
            Csv(metric.Kind.ToString()),
            Csv(Format(metric.Value)),
            Csv(metric.Unit),
            Csv(metric.Status?.ToString() ?? string.Empty),
            Csv(record.Recipe.Sha256),
            Csv(record.Source.Sha256),
            Csv(record.ViewerRunnerMatchState))));
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string? FullOptionalPath(string? path) => string.IsNullOrWhiteSpace(path) ? null : Path.GetFullPath(path);
    private static void EnsureDirectory(string path) => Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
    private static string Encode(string value) => WebUtility.HtmlEncode(value);
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

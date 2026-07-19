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
            result.Metrics.Select(metric => new InspectionRunMetric(metric.Name, metric.Kind, metric.Value, metric.Unit, metric.Status)).ToArray(),
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
            <dt>Step</dt><dd>{{Encode(FormatStep(record.Step))}}</dd>
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
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

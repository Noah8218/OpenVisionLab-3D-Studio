using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Services;

internal sealed record ShellRunRecordReadResult(
    InspectionRunRecord? Record,
    string FailureReason,
    long BytesRead);

/// <summary>
/// Owns file-system operations for Shell Run Records.
/// The ViewModel keeps bindable state and translates failures into operator text;
/// this owner keeps JSON, bundle, and recent-path persistence deterministic.
/// </summary>
internal sealed class ShellRunRecordPersistence
{
    public const int MaximumRecentRecords = RecipeRecentFileStore.MaximumEntries;
    public const int MaximumRecordBytes = 4 * 1024 * 1024;
    public const long MaximumRecentRecordBytes = 40L * 1024 * 1024;
    public const int MaximumSteps = 4096;
    public const int MaximumMetrics = 32768;

    private static readonly JsonSerializerOptions RunRecordJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string recentRunRecordsPath;

    public ShellRunRecordPersistence(string recentRunRecordsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recentRunRecordsPath);
        this.recentRunRecordsPath = recentRunRecordsPath;
    }

    public InspectionRunRecord? Read(string? path) => ReadDetailed(path).Record;

    public ShellRunRecordReadResult ReadDetailed(
        string? path,
        CancellationToken cancellationToken = default) =>
        ReadAsync(path, cancellationToken).GetAwaiter().GetResult();

    public async Task<ShellRunRecordReadResult> ReadAsync(
        string? path,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new(null, "The Run Record file is missing.", 0);
        }

        long bytesRead = 0;
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
            if (stream.Length > MaximumRecordBytes)
            {
                return new(null, "Run Records larger than 4 MiB cannot be opened. The file was not changed.", 0);
            }
            using var content = new MemoryStream((int)stream.Length);
            var buffer = new byte[65536];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stream.ReadAsync(buffer.AsMemory(0,
                    (int)Math.Min(buffer.Length, MaximumRecordBytes + 1L - bytesRead)),
                    cancellationToken).ConfigureAwait(false);
                if (read == 0) break;
                bytesRead += read;
                if (bytesRead > MaximumRecordBytes)
                {
                    return new(null, "Run Records larger than 4 MiB cannot be opened. The file was not changed.", bytesRead);
                }
                content.Write(buffer, 0, read);
            }
            content.Position = 0;
            // Preserve BOM detection from the former ReadAllText path.
            using var reader = new StreamReader(content, Encoding.UTF8, true);
            string json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var record = JsonSerializer.Deserialize<InspectionRunRecord>(
                json,
                RunRecordJsonOptions);
            cancellationToken.ThrowIfCancellationRequested();
            if (record is not null)
            {
                if ((record.Steps?.Count ?? (record.Step is null ? 0 : 1)) > MaximumSteps)
                {
                    return new(null, "Run Records support at most 4,096 steps. The file was not changed.", bytesRead);
                }
                long metrics = record.Metrics?.Count ?? 0;
                if (record.Steps is not null)
                {
                    foreach (var step in record.Steps)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        metrics += step?.Metrics?.Count ?? 0;
                        if (metrics > MaximumMetrics) break;
                    }
                }
                if (metrics > MaximumMetrics)
                {
                    return new(null, "Run Records support at most 32,768 metrics. The file was not changed.", bytesRead);
                }
            }
            InspectionRunRecordJson.Validate(record);
            cancellationToken.ThrowIfCancellationRequested();
            return new(record, string.Empty, bytesRead);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new(null, exception.Message, bytesRead);
        }
    }

    public async Task<IReadOnlyList<(string Path, ShellRunRecordReadResult Result)>> ReadRecentAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var results = new List<(string, ShellRunRecordReadResult)>();
        long totalBytes = 0;
        foreach (string path in LoadRecentPaths().Take(MaximumRecentRecords))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            totalBytes += result.BytesRead;
            if (totalBytes > MaximumRecentRecordBytes)
            {
                result = new(null, "The recent Run Records exceed 40 MiB. Files were not changed.", result.BytesRead);
            }
            results.Add((path, result));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return results;
    }

    public IReadOnlyList<string> LoadRecentPaths()
    {
        if (!File.Exists(recentRunRecordsPath)) return [];
        // Hold a read-only, non-write-sharing lease through the existing parser:
        // the index cannot grow or be replaced after the size admission check.
        using var guard = new FileStream(recentRunRecordsPath,
            FileMode.Open, FileAccess.Read, FileShare.Read);
        if (guard.Length > MaximumRecordBytes)
        {
            throw new IOException("The recent Run Record index is larger than 4 MiB. Files were not changed.");
        }
        return RecipeRecentFileStore.Load(recentRunRecordsPath);
    }

    public void SaveRecentPaths(IEnumerable<string> paths) =>
        RecipeRecentFileStore.Save(recentRunRecordsPath, paths);

    public string ExportRunRecordBundle(
        string runRecordPath,
        InspectionRunRecord record,
        string? htmlReportPath,
        string? csvReportPath,
        string targetRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRecordPath);
        ArgumentNullException.ThrowIfNull(record);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);

        var safeRunId = string.Concat(record.RunId.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
        var exportDirectory = Path.Combine(Path.GetFullPath(targetRoot), $"RunRecord-{safeRunId}");
        for (var suffix = 2; Directory.Exists(exportDirectory); suffix++)
        {
            exportDirectory = Path.Combine(Path.GetFullPath(targetRoot), $"RunRecord-{safeRunId}-{suffix}");
        }

        var stagingDirectory = $"{exportDirectory}.staging.{Guid.NewGuid():N}";
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            foreach (var sourcePath in new[] { runRecordPath, htmlReportPath, csvReportPath }
                         .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path)))
            {
                var source = sourcePath!;
                File.Copy(source, Path.Combine(stagingDirectory, Path.GetFileName(source)), overwrite: false);
            }

            Directory.Move(stagingDirectory, exportDirectory);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }

        return exportDirectory;
    }

    public string ExportPrivacySafeSupportBundle(
        string runRecordPath,
        string targetRoot,
        IReadOnlyList<ToolWorkbenchLogItem> sessionLog)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runRecordPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetRoot);
        ArgumentNullException.ThrowIfNull(sessionLog);

        return PrivacySafeSupportBundleWriter.Write(
            runRecordPath,
            targetRoot,
            sessionLog);
    }

    public void ExportInspectionQueueCsv(
        IReadOnlyList<InspectionQueueExportRow> rows,
        string targetPath)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var fullPath = Path.GetFullPath(targetPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("CSV export path must include a parent directory.", nameof(targetPath));
        }

        Directory.CreateDirectory(directory);
        var stagingPath = $"{fullPath}.staging.{Guid.NewGuid():N}";
        var lines = new List<string>
        {
            "Order,InputPath,Status,Message,RunId,RecordedAtUtc,RecipePath,RecipeVersion,RecipeSha256,InputSha256,InputId,RecordSaved,StepIndex,StepId,ToolId,ToolName,StepStatus,StepMessage,Metric,Value,Unit,MetricStatus,LowerLimit,UpperLimit,OutputId,OutputSha256,ElapsedMilliseconds"
        };
        foreach (var row in rows)
        {
            string[] identity = [row.Order.ToString(CultureInfo.InvariantCulture), row.SourcePath, row.Status, row.Message,
                row.RunId, row.RecordedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty, row.RecipePath,
                row.RecipeVersion, row.RecipeSha256, row.SourceSha256, row.Record?.Source.EntityId ?? string.Empty, row.RecordSaved.ToString()];
            var steps = row.Record?.Steps?.Cast<InspectionRunStepResult?>().ToArray() ?? [];
            if (steps.Length == 0) steps = [null];
            foreach (var step in steps)
            {
                var metrics = (step?.Metrics ?? row.Record?.Metrics ?? []).Cast<InspectionRunMetric?>().ToArray();
                if (metrics.Length == 0) metrics = [null];
                foreach (var metric in metrics)
                {
                    string[] tool = [step?.RecipeIndex.ToString(CultureInfo.InvariantCulture) ?? string.Empty, step?.Id ?? string.Empty,
                        step?.ToolId ?? string.Empty, step?.ToolName ?? row.Record?.ToolName ?? string.Empty,
                        step?.Message == ToolRecipeStep.UnconfiguredAcceptanceMessage ? "Unjudged" : step?.Status.ToString() ?? string.Empty,
                        step?.Message ?? string.Empty, metric?.Name ?? string.Empty];
                    var cells = identity.Concat(tool).Select(value => CsvCell(value)).Concat(new[]
                    {
                        CsvCell(metric?.Value.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty, protectFormula: false),
                        CsvCell(metric?.Unit ?? string.Empty), CsvCell(metric?.Status?.ToString() ?? string.Empty),
                        CsvCell(step?.Metrics.FirstOrDefault(value => value.Name == "LowerLimit")?.Value.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty, protectFormula: false),
                        CsvCell(step?.Metrics.FirstOrDefault(value => value.Name == "UpperLimit")?.Value.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty, protectFormula: false),
                        CsvCell(step?.OutputEntityId ?? string.Empty), CsvCell(step?.OutputContentSha256 ?? string.Empty),
                        CsvCell(step?.ElapsedMilliseconds.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty, protectFormula: false)
                    });
                    lines.Add(string.Join(',', cells));
                }
            }
        }

        try
        {
            File.WriteAllText(stagingPath, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            File.Move(stagingPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(stagingPath)) File.Delete(stagingPath);
        }
    }

    private static string CsvCell(string value, bool protectFormula = true)
    {
        if (protectFormula && value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
        {
            value = "'" + value;
        }

        return '"' + value.Replace("\"", "\"\"", StringComparison.Ordinal) + '"';
    }
}

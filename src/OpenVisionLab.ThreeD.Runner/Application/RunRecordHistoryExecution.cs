using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

internal static class RunRecordHistoryExecution
{
    public static int Run(
        string rootPath,
        string reportPath,
        RunRecordHistoryQueryOptions? options = null,
        string? csvReportPath = null,
        string? jsonReportPath = null)
    {
        try
        {
            EnsureDistinctReportPaths(reportPath, csvReportPath, jsonReportPath);
            var result = RunRecordHistoryQuery.Read(rootPath, options);
            WriteReport(reportPath, result);
            if (csvReportPath is not null)
            {
                WriteCsvReport(csvReportPath, result);
            }

            if (jsonReportPath is not null)
            {
                WriteJsonReport(jsonReportPath, result);
            }

            Console.WriteLine(
                $"Run Record history: {result.Records.Count} record(s), "
                + $"{result.Issues.Count} issue(s), "
                + $"{result.StagingPathCount} staging path(s) ignored.");
            return 0;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or NotSupportedException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static void EnsureDistinctReportPaths(
        string reportPath,
        string? csvReportPath,
        string? jsonReportPath)
    {
        var fullPaths = new[] { reportPath, csvReportPath, jsonReportPath }
            .Where(path => path is not null)
            .Select(path => Path.GetFullPath(path!))
            .ToArray();
        if (fullPaths.Length != fullPaths.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new ArgumentException(
                "Run Record history report output paths must be different.",
                nameof(csvReportPath));
        }
    }

    private static void WriteReport(
        string reportPath,
        RunRecordHistoryQueryResult result) =>
        WriteAtomically(reportPath, CreateLines(result));

    private static void WriteCsvReport(
        string reportPath,
        RunRecordHistoryQueryResult result) =>
        WriteAtomically(reportPath, CreateCsvLines(result));

    private static void WriteJsonReport(
        string reportPath,
        RunRecordHistoryQueryResult result) =>
        WriteAtomically(reportPath, [CreateJson(result)]);

    private static void WriteAtomically(
        string reportPath,
        IEnumerable<string> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentNullException.ThrowIfNull(lines);

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        var temporaryPath = $"{fullReportPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            using (var writer = new StreamWriter(
                       stream,
                       new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                       bufferSize: 4096,
                       leaveOpen: true))
            {
                foreach (var line in lines)
                {
                    writer.WriteLine(line);
                }

                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullReportPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static IEnumerable<string> CreateCsvLines(
        RunRecordHistoryQueryResult result)
    {
        yield return string.Join(",", new[]
        {
            "RunId",
            "RecordedAtUtc",
            "Status",
            "ToolName",
            "RecipePath",
            "SourceEntityId",
            "SourcePath",
            "ElapsedMilliseconds",
            "StepCount",
            "JsonPath"
        }.Select(CsvEscape));

        foreach (var record in result.Records)
        {
            yield return string.Join(",", new[]
            {
                record.RunId,
                record.RecordedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                record.Status.ToString(),
                record.ToolName,
                record.RecipePath,
                record.SourceEntityId,
                record.SourcePath,
                record.ElapsedMilliseconds.ToString("G17", CultureInfo.InvariantCulture),
                record.StepCount.ToString(CultureInfo.InvariantCulture),
                record.JsonPath
            }.Select(CsvEscape));
        }
    }

    private static string CreateJson(RunRecordHistoryQueryResult result)
    {
        var summary = RunRecordHistoryQuery.Summarize(result);
        var projection = new
        {
            schemaVersion = 1,
            rootPath = result.RootPath,
            candidateCount = result.CandidateCount,
            stagingPathCount = result.StagingPathCount,
            filteredOutRecordCount = result.FilteredOutRecordCount,
            filter = new
            {
                status = result.Filter.Status?.ToString(),
                toolName = result.Filter.ToolName,
                fromUtc = FormatJsonTimestamp(result.Filter.FromUtc),
                toUtc = FormatJsonTimestamp(result.Filter.ToUtc)
            },
            summary = new
            {
                recordCount = summary.RecordCount,
                totalElapsedMilliseconds = summary.TotalElapsedMilliseconds,
                minimumElapsedMilliseconds = summary.MinimumElapsedMilliseconds,
                maximumElapsedMilliseconds = summary.MaximumElapsedMilliseconds,
                averageElapsedMilliseconds = summary.AverageElapsedMilliseconds,
                earliestRecordedAtUtc = FormatJsonTimestamp(summary.EarliestRecordedAtUtc),
                latestRecordedAtUtc = FormatJsonTimestamp(summary.LatestRecordedAtUtc),
                distinctToolCount = summary.DistinctToolCount,
                distinctRecipeCount = summary.DistinctRecipeCount,
                distinctSourceCount = summary.DistinctSourceCount,
                elapsedSaturated = summary.ElapsedSaturated
            },
            records = result.Records.Select(record => new
            {
                jsonPath = record.JsonPath,
                schemaVersion = record.SchemaVersion,
                runId = record.RunId,
                recordedAtUtc = record.RecordedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
                status = record.Status.ToString(),
                toolName = record.ToolName,
                recipePath = record.RecipePath,
                sourceEntityId = record.SourceEntityId,
                sourcePath = record.SourcePath,
                elapsedMilliseconds = record.ElapsedMilliseconds,
                stepCount = record.StepCount
            }).ToArray(),
            issues = result.Issues.Select(issue => new
            {
                jsonPath = issue.JsonPath,
                reason = issue.Reason
            }).ToArray()
        };

        return JsonSerializer.Serialize(
            projection,
            new JsonSerializerOptions { WriteIndented = true });
    }

    private static IEnumerable<string> CreateLines(
        RunRecordHistoryQueryResult result)
    {
        var summary = RunRecordHistoryQuery.Summarize(result);
        yield return "OpenVisionLab 3D Run Record history";
        yield return $"Root|{Escape(result.RootPath)}";
        yield return $"CandidateJson|{result.CandidateCount.ToString(CultureInfo.InvariantCulture)}";
        yield return $"StagingIgnored|{result.StagingPathCount.ToString(CultureInfo.InvariantCulture)}";
        yield return $"Records|{result.Records.Count.ToString(CultureInfo.InvariantCulture)}";
        yield return $"Issues|{result.Issues.Count.ToString(CultureInfo.InvariantCulture)}";
        yield return $"Filter|status={FormatFilter(result.Filter.Status?.ToString())}|tool={FormatFilter(result.Filter.ToolName)}|fromUtc={FormatFilter(result.Filter.FromUtc)}|toUtc={FormatFilter(result.Filter.ToUtc)}";
        yield return $"FilteredOut|{result.FilteredOutRecordCount.ToString(CultureInfo.InvariantCulture)}";
        yield return string.Create(
            CultureInfo.InvariantCulture,
            $"ElapsedSummary|records={summary.RecordCount}|totalMs={Format(summary.TotalElapsedMilliseconds)}|minMs={FormatNullable(summary.MinimumElapsedMilliseconds)}|maxMs={FormatNullable(summary.MaximumElapsedMilliseconds)}|averageMs={FormatNullable(summary.AverageElapsedMilliseconds)}|saturated={summary.ElapsedSaturated}");
        yield return $"RecordedAtRange|earliestUtc={FormatNullable(summary.EarliestRecordedAtUtc)}|latestUtc={FormatNullable(summary.LatestRecordedAtUtc)}";
        yield return $"Distinct|tools={summary.DistinctToolCount.ToString(CultureInfo.InvariantCulture)}|recipes={summary.DistinctRecipeCount.ToString(CultureInfo.InvariantCulture)}|sources={summary.DistinctSourceCount.ToString(CultureInfo.InvariantCulture)}";
        foreach (var status in Enum.GetValues<ResultStatus>())
        {
            var count = result.Records.Count(record => record.Status == status);
            yield return $"Status|{status}|{count.ToString(CultureInfo.InvariantCulture)}";
        }

        foreach (var record in result.Records)
        {
            yield return string.Create(
                CultureInfo.InvariantCulture,
                $"Record|runId={Escape(record.RunId)}|schema={Escape(record.SchemaVersion)}|recordedAtUtc={record.RecordedAtUtc:O}|status={record.Status}|tool={Escape(record.ToolName)}|recipe={Escape(record.RecipePath)}|sourceEntity={Escape(record.SourceEntityId)}|source={Escape(record.SourcePath)}|elapsedMs={record.ElapsedMilliseconds:G17}|steps={record.StepCount}|json={Escape(record.JsonPath)}");
        }

        foreach (var issue in result.Issues)
        {
            yield return $"Issue|json={Escape(issue.JsonPath)}|reason={Escape(issue.Reason)}";
        }
    }

    private static string Escape(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);

    private static string CsvEscape(string value) =>
        $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Format(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);

    private static string FormatNullable(double? value) =>
        value is { } number ? Format(number) : "none";

    private static string FormatNullable(DateTimeOffset? value) =>
        value is { } timestamp
            ? timestamp.ToString("O", CultureInfo.InvariantCulture)
            : "none";

    private static string? FormatJsonTimestamp(DateTimeOffset? value) =>
        value is { } timestamp
            ? timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            : null;

    private static string FormatFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Any" : Escape(value);

    private static string FormatFilter(DateTimeOffset? value) =>
        value is { } timestamp
            ? timestamp.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
            : "Any";
}

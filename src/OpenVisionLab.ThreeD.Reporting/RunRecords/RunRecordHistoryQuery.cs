using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Reporting.RunRecords;

/// <summary>
/// Reads already persisted Run Records without loading their recipe/source
/// files or executing inspection. The query is intentionally a projection so
/// a history view does not retain every metric and overlay in memory.
/// </summary>
public static class RunRecordHistoryQuery
{
    private static readonly char[] PathSeparators =
    [
        Path.DirectorySeparatorChar,
        Path.AltDirectorySeparatorChar
    ];

    public static RunRecordHistoryQueryResult Read(string rootPath) =>
        Read(rootPath, options: null);

    public static RunRecordHistoryQueryResult Read(
        string rootPath,
        RunRecordHistoryQueryOptions? options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        var queryOptions = options ?? new RunRecordHistoryQueryOptions();
        queryOptions.Validate();

        var fullRootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(fullRootPath))
        {
            throw new DirectoryNotFoundException(
                $"Run Record history directory was not found: {fullRootPath}");
        }

        var records = new List<RunRecordHistoryEntry>();
        var issues = new List<RunRecordHistoryIssue>();
        var stagingCount = 0;
        var candidatePaths = Directory
            .EnumerateFiles(fullRootPath, "run-record.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(path => path, StringComparer.Ordinal)
            .ToArray();

        foreach (var candidatePath in candidatePaths)
        {
            var fullCandidatePath = Path.GetFullPath(candidatePath);
            if (IsTransientPath(fullRootPath, fullCandidatePath))
            {
                stagingCount++;
                continue;
            }

            try
            {
                var record = InspectionRunRecordJson.Read(fullCandidatePath);
                records.Add(CreateEntry(fullCandidatePath, record));
            }
            catch (Exception exception) when (IsRecordReadFailure(exception))
            {
                issues.Add(new RunRecordHistoryIssue(
                    fullCandidatePath,
                    $"{exception.GetType().Name}: {exception.Message}"));
            }
        }

        records.Sort(CompareEntries);
        var filteredRecords = records
            .Where(queryOptions.Matches)
            .ToList();
        issues.Sort((left, right) =>
        {
            var pathComparison = StringComparer.OrdinalIgnoreCase.Compare(
                left.JsonPath,
                right.JsonPath);
            return pathComparison != 0
                ? pathComparison
                : StringComparer.Ordinal.Compare(left.JsonPath, right.JsonPath);
        });

        return new RunRecordHistoryQueryResult(
            fullRootPath,
            candidatePaths.Length,
            stagingCount,
            filteredRecords.ToArray(),
            issues.ToArray(),
            records.Count - filteredRecords.Count,
            queryOptions);
    }

    public static RunRecordHistorySummary Summarize(
        RunRecordHistoryQueryResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var records = result.Records ?? [];
        if (records.Count == 0)
        {
            return new RunRecordHistorySummary(
                0,
                0.0,
                null,
                null,
                null,
                null,
                null,
                0,
                0,
                0,
                false);
        }

        var totalElapsedMilliseconds = 0.0;
        var elapsedSaturated = false;
        var averageElapsedMilliseconds = 0.0;
        var sampleIndex = 0;
        foreach (var record in records)
        {
            totalElapsedMilliseconds = AddElapsedMilliseconds(
                totalElapsedMilliseconds,
                record.ElapsedMilliseconds,
                ref elapsedSaturated);
            sampleIndex++;
            averageElapsedMilliseconds +=
                (record.ElapsedMilliseconds - averageElapsedMilliseconds)
                / sampleIndex;
        }

        return new RunRecordHistorySummary(
            records.Count,
            totalElapsedMilliseconds,
            records.Min(record => record.ElapsedMilliseconds),
            records.Max(record => record.ElapsedMilliseconds),
            double.IsFinite(averageElapsedMilliseconds)
                ? averageElapsedMilliseconds
                : null,
            records.Min(record => record.RecordedAtUtc),
            records.Max(record => record.RecordedAtUtc),
            records.Select(record => record.ToolName)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            records.Select(record => record.RecipePath)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            records.Select(record => record.SourcePath)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            elapsedSaturated);
    }

    private static double AddElapsedMilliseconds(
        double current,
        double value,
        ref bool saturated)
    {
        if (saturated)
        {
            return double.MaxValue;
        }

        var sum = current + value;
        if (double.IsFinite(sum))
        {
            return sum;
        }

        saturated = true;
        return double.MaxValue;
    }

    private static RunRecordHistoryEntry CreateEntry(
        string jsonPath,
        InspectionRunRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(record.SchemaVersion)
            || string.IsNullOrWhiteSpace(record.RunId)
            || record.Recipe is null
            || record.Source is null
            || record.Artifacts is null
            || string.IsNullOrWhiteSpace(record.ToolName)
            || !Enum.IsDefined(record.Status)
            || !double.IsFinite(record.ElapsedMilliseconds)
            || record.ElapsedMilliseconds < 0.0
            || string.IsNullOrWhiteSpace(record.Recipe.Path)
            || string.IsNullOrWhiteSpace(record.Source.EntityId)
            || string.IsNullOrWhiteSpace(record.Source.Path))
        {
            throw new InvalidDataException(
                "Run Record is missing a required identity, source, tool, status, or elapsed-time field.");
        }

        var stepCount = record.Steps?.Count
            ?? (record.Step is null ? 0 : 1);
        return new RunRecordHistoryEntry(
            jsonPath,
            record.SchemaVersion,
            record.RunId,
            record.RecordedAtUtc,
            record.Status,
            record.ToolName,
            record.Recipe.Path,
            record.Source.EntityId,
            record.Source.Path,
            record.ElapsedMilliseconds,
            stepCount);
    }

    private static bool IsTransientPath(string rootPath, string candidatePath)
    {
        var relativePath = Path.GetRelativePath(rootPath, candidatePath);
        var segments = relativePath.Split(
            PathSeparators,
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
                   segment.Contains(".staging.", StringComparison.OrdinalIgnoreCase)
                   || segment.Contains(".tmp.", StringComparison.OrdinalIgnoreCase))
               || Path.GetFileName(candidatePath).Contains(
                   ".tmp.",
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecordReadFailure(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException
            or NotSupportedException
            or ArgumentException;

    private static int CompareEntries(
        RunRecordHistoryEntry left,
        RunRecordHistoryEntry right)
    {
        var recordedAtComparison = right.RecordedAtUtc.CompareTo(left.RecordedAtUtc);
        if (recordedAtComparison != 0)
        {
            return recordedAtComparison;
        }

        var runIdComparison = StringComparer.Ordinal.Compare(left.RunId, right.RunId);
        if (runIdComparison != 0)
        {
            return runIdComparison;
        }

        var pathComparison = StringComparer.OrdinalIgnoreCase.Compare(
            left.JsonPath,
            right.JsonPath);
        return pathComparison != 0
            ? pathComparison
            : StringComparer.Ordinal.Compare(left.JsonPath, right.JsonPath);
    }
}

public sealed record RunRecordHistoryQueryResult(
    string RootPath,
    int CandidateCount,
    int StagingPathCount,
    IReadOnlyList<RunRecordHistoryEntry> Records,
    IReadOnlyList<RunRecordHistoryIssue> Issues,
    int FilteredOutRecordCount,
    RunRecordHistoryQueryOptions Filter);

public sealed record RunRecordHistoryQueryOptions(
    ResultStatus? Status = null,
    string? ToolName = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null)
{
    public void Validate()
    {
        if (ToolName is not null && string.IsNullOrWhiteSpace(ToolName))
        {
            throw new ArgumentException(
                "Run Record history tool filter cannot be blank.",
                nameof(ToolName));
        }

        if (FromUtc is { } from
            && ToUtc is { } to
            && from.ToUniversalTime() > to.ToUniversalTime())
        {
            throw new ArgumentException(
                "Run Record history filter start must not be after its end.",
                nameof(FromUtc));
        }
    }

    public bool Matches(RunRecordHistoryEntry record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var recordedAtUtc = record.RecordedAtUtc.ToUniversalTime();
        return (Status is null || record.Status == Status)
            && (ToolName is null
                || string.Equals(record.ToolName, ToolName, StringComparison.Ordinal))
            && (FromUtc is null || recordedAtUtc >= FromUtc.Value.ToUniversalTime())
            && (ToUtc is null || recordedAtUtc <= ToUtc.Value.ToUniversalTime());
    }
}

public sealed record RunRecordHistorySummary(
    int RecordCount,
    double TotalElapsedMilliseconds,
    double? MinimumElapsedMilliseconds,
    double? MaximumElapsedMilliseconds,
    double? AverageElapsedMilliseconds,
    DateTimeOffset? EarliestRecordedAtUtc,
    DateTimeOffset? LatestRecordedAtUtc,
    int DistinctToolCount,
    int DistinctRecipeCount,
    int DistinctSourceCount,
    bool ElapsedSaturated);

public sealed record RunRecordHistoryEntry(
    string JsonPath,
    string SchemaVersion,
    string RunId,
    DateTimeOffset RecordedAtUtc,
    ResultStatus Status,
    string ToolName,
    string RecipePath,
    string SourceEntityId,
    string SourcePath,
    double ElapsedMilliseconds,
    int StepCount);

public sealed record RunRecordHistoryIssue(string JsonPath, string Reason);

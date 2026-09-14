using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

internal static class RunRecordHistoryQueryVerification
{
    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var fixtureRoot = Path.Combine(
            reportDirectory,
            $"run-record-history-query-fixture-{Guid.NewGuid():N}");
        var checks = new List<VerificationCase>();
        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var newRecord = WriteRecord(
                fixtureRoot,
                "run-new",
                new DateTimeOffset(2026, 9, 10, 6, 4, 0, TimeSpan.Zero),
                ResultStatus.Pass,
                "Tool|new",
                "recipe|new.json",
                "source\nnew.C3D",
                5.0);
            var tieB = WriteRecord(
                fixtureRoot,
                "run-tie-b",
                new DateTimeOffset(2026, 9, 10, 6, 3, 0, TimeSpan.Zero),
                ResultStatus.Warning,
                "Tool B",
                elapsedMilliseconds: 2.5);
            var tieA = WriteRecord(
                fixtureRoot,
                "run-tie-a",
                new DateTimeOffset(2026, 9, 10, 6, 3, 0, TimeSpan.Zero),
                ResultStatus.Fail,
                "Tool A",
                elapsedMilliseconds: 7.5);
            WriteRecord(
                fixtureRoot,
                "run-old",
                new DateTimeOffset(2026, 9, 10, 6, 2, 0, TimeSpan.Zero),
                ResultStatus.Pass,
                "Tool old",
                elapsedMilliseconds: 0.5);
            WriteRecord(
                fixtureRoot,
                "run-old.staging.fixture",
                new DateTimeOffset(2026, 9, 10, 6, 5, 0, TimeSpan.Zero),
                ResultStatus.Error,
                "Staging tool",
                elapsedMilliseconds: 11.0);
            var invalidDirectory = Path.Combine(fixtureRoot, "invalid");
            Directory.CreateDirectory(invalidDirectory);
            var invalidPath = Path.Combine(invalidDirectory, "run-record.json");
            File.WriteAllText(invalidPath, "{ invalid", new UTF8Encoding(false));

            var query = RunRecordHistoryQuery.Read(fixtureRoot);
            checks.Add(Check(
                "valid-record-count-excludes-staging-and-invalid",
                query.Records.Count == 4
                && query.CandidateCount == 6
                && query.StagingPathCount == 1
                && query.Issues.Count == 1,
                $"records={query.Records.Count};candidates={query.CandidateCount};staging={query.StagingPathCount};issues={query.Issues.Count}"));
            checks.Add(Check(
                "record-order-is-newest-then-run-id",
                query.Records.Select(record => record.RunId).SequenceEqual(
                [
                    "run-new",
                    "run-tie-a",
                    "run-tie-b",
                    "run-old"
                ]),
                string.Join(",", query.Records.Select(record => record.RunId))));
            checks.Add(Check(
                "projection-retains-status-tool-and-step-count",
                query.Records[0].Status == ResultStatus.Pass
                && query.Records[0].ToolName == "Tool|new"
                && query.Records[0].StepCount == 1
                && query.Records[0].RecipePath.EndsWith(
                    "recipe|new.json",
                    StringComparison.Ordinal)
                && query.Records[0].SourcePath.Contains(
                    "source\nnew.C3D",
                    StringComparison.Ordinal),
                $"status={query.Records[0].Status};tool={query.Records[0].ToolName};steps={query.Records[0].StepCount}"));
            checks.Add(Check(
                "query-does-not-load-recipe-or-source-files",
                query.Records.All(record =>
                    !File.Exists(record.RecipePath)
                    && !File.Exists(record.SourcePath)),
                "all projected source and recipe paths remain absent"));
            checks.Add(Check(
                "malformed-record-is-reported-with-path",
                query.Issues[0].JsonPath == Path.GetFullPath(invalidPath)
                && query.Issues[0].Reason.Contains(
                    "JsonException",
                    StringComparison.Ordinal),
                query.Issues[0].Reason));
            checks.Add(Check(
                "status-filter-is-inclusive-and-retains-issues",
                RunRecordHistoryQuery.Read(
                    fixtureRoot,
                    new RunRecordHistoryQueryOptions(Status: ResultStatus.Pass)) is { Records.Count: 2, FilteredOutRecordCount: 2 } statusQuery
                && statusQuery.Records.All(record => record.Status == ResultStatus.Pass)
                && statusQuery.Issues.Count == 1,
                "status=Pass returns both valid pass records and preserves malformed issues"));
            checks.Add(Check(
                "exact-tool-filter-is-ordinal",
                RunRecordHistoryQuery.Read(
                    fixtureRoot,
                    new RunRecordHistoryQueryOptions(ToolName: "Tool A")) is { Records.Count: 1, FilteredOutRecordCount: 3 } toolQuery
                && toolQuery.Records[0].RunId == "run-tie-a",
                "tool=Tool A returns only run-tie-a"));
            checks.Add(Check(
                "utc-window-is-inclusive",
                RunRecordHistoryQuery.Read(
                    fixtureRoot,
                    new RunRecordHistoryQueryOptions(
                        FromUtc: new DateTimeOffset(2026, 9, 10, 6, 3, 0, TimeSpan.Zero),
                        ToUtc: new DateTimeOffset(2026, 9, 10, 6, 4, 0, TimeSpan.Zero))) is { Records.Count: 3, FilteredOutRecordCount: 1 } timeQuery
                && timeQuery.Records.Select(record => record.RunId).SequenceEqual(
                ["run-new", "run-tie-a", "run-tie-b"]),
                "from/to boundaries include both endpoints"));
            checks.Add(Check(
                "invalid-utc-window-fails-closed",
                Throws<ArgumentException>(() => RunRecordHistoryQuery.Read(
                    fixtureRoot,
                    new RunRecordHistoryQueryOptions(
                        FromUtc: new DateTimeOffset(2026, 9, 10, 6, 4, 0, TimeSpan.Zero),
                        ToUtc: new DateTimeOffset(2026, 9, 10, 6, 3, 0, TimeSpan.Zero)))),
                "from after to is rejected"));
            var batchSummary = RunRecordHistoryQuery.Summarize(query);
            checks.Add(Check(
                "batch-summary-is-deterministic-and-excludes-invalid-records",
                batchSummary.RecordCount == 4
                && batchSummary.TotalElapsedMilliseconds == 15.5
                && batchSummary.MinimumElapsedMilliseconds == 0.5
                && batchSummary.MaximumElapsedMilliseconds == 7.5
                && batchSummary.AverageElapsedMilliseconds == 3.875
                && batchSummary.EarliestRecordedAtUtc == new DateTimeOffset(2026, 9, 10, 6, 2, 0, TimeSpan.Zero)
                && batchSummary.LatestRecordedAtUtc == new DateTimeOffset(2026, 9, 10, 6, 4, 0, TimeSpan.Zero)
                && batchSummary.DistinctToolCount == 4
                && batchSummary.DistinctRecipeCount == 4
                && batchSummary.DistinctSourceCount == 4
                && !batchSummary.ElapsedSaturated,
                $"records={batchSummary.RecordCount};total={batchSummary.TotalElapsedMilliseconds};average={batchSummary.AverageElapsedMilliseconds};tools={batchSummary.DistinctToolCount}"));
            var emptySummary = RunRecordHistoryQuery.Summarize(
                new RunRecordHistoryQueryResult(
                    fixtureRoot,
                    0,
                    0,
                    [],
                    [],
                    0,
                    new RunRecordHistoryQueryOptions()));
            checks.Add(Check(
                "empty-batch-summary-is-explicit",
                emptySummary.RecordCount == 0
                && emptySummary.TotalElapsedMilliseconds == 0.0
                && emptySummary.MinimumElapsedMilliseconds is null
                && emptySummary.MaximumElapsedMilliseconds is null
                && emptySummary.AverageElapsedMilliseconds is null
                && emptySummary.EarliestRecordedAtUtc is null
                && emptySummary.LatestRecordedAtUtc is null
                && !emptySummary.ElapsedSaturated,
                "empty summary has no fabricated range or timing"));

            var missingRoot = Path.Combine(fixtureRoot, "missing-root");
            checks.Add(Check(
                "missing-root-fails-closed",
                Throws<DirectoryNotFoundException>(
                    () => RunRecordHistoryQuery.Read(missingRoot)),
                missingRoot));

            var summaryPath = Path.Combine(reportDirectory, "run-record-history-query-summary.txt");
            var firstExitCode = RunRecordHistoryExecution.Run(fixtureRoot, summaryPath);
            var firstSummary = File.ReadAllBytes(summaryPath);
            var secondExitCode = RunRecordHistoryExecution.Run(fixtureRoot, summaryPath);
            var secondSummary = File.ReadAllBytes(summaryPath);
            var summaryText = Encoding.UTF8.GetString(secondSummary);
            checks.Add(Check(
                "runner-command-writes-repeatable-summary",
                firstExitCode == 0
                && secondExitCode == 0
                && firstSummary.AsSpan().SequenceEqual(secondSummary)
                && summaryText.Contains("Records|4", StringComparison.Ordinal)
                && summaryText.Contains("StagingIgnored|1", StringComparison.Ordinal)
                && summaryText.Contains(
                    "Filter|status=Any|tool=Any|fromUtc=Any|toUtc=Any",
                    StringComparison.Ordinal)
                && summaryText.Contains("FilteredOut|0", StringComparison.Ordinal)
                && summaryText.Contains(
                    "ElapsedSummary|records=4|totalMs=15.5|minMs=0.5|maxMs=7.5|averageMs=3.875|saturated=False",
                    StringComparison.Ordinal)
                && summaryText.Contains(
                    "RecordedAtRange|earliestUtc=2026-09-10T06:02:00.0000000+00:00|latestUtc=2026-09-10T06:04:00.0000000+00:00",
                    StringComparison.Ordinal)
                && summaryText.Contains(
                    "Distinct|tools=4|recipes=4|sources=4",
                    StringComparison.Ordinal)
                && summaryText.Contains("Status|Pass|2", StringComparison.Ordinal)
                && summaryText.Contains("Status|Fail|1", StringComparison.Ordinal)
                && summaryText.Contains("Status|Warning|1", StringComparison.Ordinal),
                $"firstExit={firstExitCode};secondExit={secondExitCode};bytes={secondSummary.Length}"));
            var filteredSummaryPath = Path.Combine(
                reportDirectory,
                "run-record-history-query-filtered-summary.txt");
            var filteredExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                filteredSummaryPath,
                new RunRecordHistoryQueryOptions(Status: ResultStatus.Pass));
            var filteredSummary = File.ReadAllText(filteredSummaryPath);
            checks.Add(Check(
                "runner-filtered-summary-is-deterministic",
                filteredExitCode == 0
                && filteredSummary.Contains("Filter|status=Pass", StringComparison.Ordinal)
                && filteredSummary.Contains("FilteredOut|2", StringComparison.Ordinal)
                && filteredSummary.Contains("Records|2", StringComparison.Ordinal)
                && filteredSummary.Contains("Status|Pass|2", StringComparison.Ordinal)
                && !filteredSummary.Contains("run-tie-a", StringComparison.Ordinal),
                $"exit={filteredExitCode};bytes={filteredSummary.Length}"));
            var csvPath = Path.Combine(
                reportDirectory,
                "run-record-history.csv");
            var firstCsvExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                summaryPath,
                csvReportPath: csvPath);
            var firstCsv = File.ReadAllBytes(csvPath);
            var secondCsvExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                summaryPath,
                csvReportPath: csvPath);
            var secondCsv = File.ReadAllBytes(csvPath);
            var csvText = Encoding.UTF8.GetString(secondCsv);
            checks.Add(Check(
                "runner-history-csv-is-repeatable-and-escaped",
                firstCsvExitCode == 0
                && secondCsvExitCode == 0
                && firstCsv.AsSpan().SequenceEqual(secondCsv)
                && csvText.StartsWith("\"RunId\",\"RecordedAtUtc\",\"Status\"", StringComparison.Ordinal)
                && csvText.Contains("\"Tool|new\"", StringComparison.Ordinal)
                && csvText.Contains("source\nnew.C3D\",\"5\"", StringComparison.Ordinal)
                && csvText.Contains("\"run-new\"", StringComparison.Ordinal),
                $"firstExit={firstCsvExitCode};secondExit={secondCsvExitCode};bytes={secondCsv.Length}"));
            var filteredCsvPath = Path.Combine(
                reportDirectory,
                "run-record-history-filtered.csv");
            var filteredCsvExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                summaryPath,
                new RunRecordHistoryQueryOptions(Status: ResultStatus.Pass),
                filteredCsvPath);
            var filteredCsvText = File.ReadAllText(filteredCsvPath);
            checks.Add(Check(
                "runner-history-csv-respects-filter",
                filteredCsvExitCode == 0
                && filteredCsvText.Contains("\"run-new\"", StringComparison.Ordinal)
                && filteredCsvText.Contains("\"run-old\"", StringComparison.Ordinal)
                && !filteredCsvText.Contains("\"run-tie-a\"", StringComparison.Ordinal),
                $"exit={filteredCsvExitCode};bytes={filteredCsvText.Length}"));
            var jsonPath = Path.Combine(
                reportDirectory,
                "run-record-history.json");
            var firstJsonExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                summaryPath,
                jsonReportPath: jsonPath);
            var firstJson = File.ReadAllBytes(jsonPath);
            var secondJsonExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                summaryPath,
                jsonReportPath: jsonPath);
            var secondJson = File.ReadAllBytes(jsonPath);
            using var jsonDocument = JsonDocument.Parse(secondJson);
            var jsonRoot = jsonDocument.RootElement;
            var jsonRecords = jsonRoot.GetProperty("records");
            var jsonIssues = jsonRoot.GetProperty("issues");
            var jsonSummary = jsonRoot.GetProperty("summary");
            checks.Add(Check(
                "runner-history-json-is-repeatable-and-complete",
                firstJsonExitCode == 0
                && secondJsonExitCode == 0
                && firstJson.AsSpan().SequenceEqual(secondJson)
                && jsonRoot.GetProperty("schemaVersion").GetInt32() == 1
                && jsonRoot.GetProperty("candidateCount").GetInt32() == 6
                && jsonRoot.GetProperty("stagingPathCount").GetInt32() == 1
                && jsonRoot.GetProperty("filteredOutRecordCount").GetInt32() == 0
                && jsonRecords.GetArrayLength() == 4
                && jsonRecords[0].GetProperty("status").GetString() == "Pass"
                && jsonRecords[0].GetProperty("recordedAtUtc").GetString() == "2026-09-10T06:04:00.0000000+00:00"
                && jsonIssues.GetArrayLength() == 1
                && jsonSummary.GetProperty("recordCount").GetInt32() == 4
                && jsonSummary.GetProperty("distinctToolCount").GetInt32() == 4,
                $"firstExit={firstJsonExitCode};secondExit={secondJsonExitCode};bytes={secondJson.Length}"));
            var filteredJsonPath = Path.Combine(
                reportDirectory,
                "run-record-history-filtered.json");
            var filteredJsonExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                summaryPath,
                new RunRecordHistoryQueryOptions(Status: ResultStatus.Pass),
                jsonReportPath: filteredJsonPath);
            using var filteredJsonDocument = JsonDocument.Parse(
                File.ReadAllBytes(filteredJsonPath));
            var filteredJsonRoot = filteredJsonDocument.RootElement;
            var filteredJsonRecords = filteredJsonRoot.GetProperty("records");
            checks.Add(Check(
                "runner-history-json-respects-filter",
                filteredJsonExitCode == 0
                && filteredJsonRoot.GetProperty("filter").GetProperty("status").GetString() == "Pass"
                && filteredJsonRoot.GetProperty("filteredOutRecordCount").GetInt32() == 2
                && filteredJsonRecords.GetArrayLength() == 2
                && filteredJsonRecords.EnumerateArray().Any(
                    record => record.GetProperty("runId").GetString() == "run-new")
                && !filteredJsonRecords.EnumerateArray().Any(
                    record => record.GetProperty("runId").GetString() == "run-tie-a"),
                $"exit={filteredJsonExitCode};records={filteredJsonRecords.GetArrayLength()}"));
            var collisionPath = Path.Combine(
                reportDirectory,
                "run-record-history-collision.txt");
            const string collisionSentinel = "existing text report must remain";
            File.WriteAllText(collisionPath, collisionSentinel);
            var collisionExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                collisionPath,
                csvReportPath: collisionPath);
            var collisionText = File.ReadAllText(collisionPath);
            checks.Add(Check(
                "runner-history-rejects-text-csv-path-collision",
                collisionExitCode == 1
                && collisionText == collisionSentinel
                && !Directory.GetFiles(
                    reportDirectory,
                    "run-record-history-collision.txt.tmp.*")
                    .Any(),
                $"exit={collisionExitCode};textPreserved={collisionText == collisionSentinel}"));
            var jsonCollisionPath = Path.Combine(
                reportDirectory,
                "run-record-history-json-collision.txt");
            File.WriteAllText(jsonCollisionPath, collisionSentinel);
            var jsonCollisionExitCode = RunRecordHistoryExecution.Run(
                fixtureRoot,
                jsonCollisionPath,
                jsonReportPath: jsonCollisionPath);
            var jsonCollisionText = File.ReadAllText(jsonCollisionPath);
            checks.Add(Check(
                "runner-history-rejects-json-path-collision",
                jsonCollisionExitCode == 1
                && jsonCollisionText == collisionSentinel
                && !Directory.GetFiles(
                    reportDirectory,
                    "run-record-history-json-collision.txt.tmp.*")
                    .Any(),
                $"exit={jsonCollisionExitCode};textPreserved={jsonCollisionText == collisionSentinel}"));
            checks.Add(Check(
                "runner-summary-escapes-delimiters-and-newlines",
                summaryText.Contains("tool=Tool\\|new", StringComparison.Ordinal)
                && summaryText.Contains("source\\nnew.C3D", StringComparison.Ordinal),
                "escaped tool delimiter and source newline are present"));
            checks.Add(Check(
                "query-result-is-repeatable",
                query.Records.SequenceEqual(RunRecordHistoryQuery.Read(fixtureRoot).Records)
                && query.Issues.SequenceEqual(RunRecordHistoryQuery.Read(fixtureRoot).Issues),
                "two independent reads have the same projection order"));
        }
        catch (Exception exception)
        {
            checks.Add(new VerificationCase(
                "unexpected-exception",
                false,
                $"{exception.GetType().Name}: {exception.Message}"));
        }

        var passed = checks.Count(check => check.Passed);
        var lines = checks.Select(check =>
            $"{(check.Passed ? "PASS" : "FAIL")} | {check.Name} | {Clean(check.Evidence)}").ToList();
        lines.Add($"Run Record history query verification: {passed}/{checks.Count} passed");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(reportPath, lines, new UTF8Encoding(false));
        Console.WriteLine(lines[^1]);
        return passed == checks.Count ? 0 : 5;
    }

    private static string WriteRecord(
        string root,
        string runId,
        DateTimeOffset recordedAtUtc,
        ResultStatus status,
        string toolName,
        string? recipeFileName = null,
        string? sourceFileName = null,
        double elapsedMilliseconds = 1.25)
    {
        var directory = Path.Combine(root, runId);
        Directory.CreateDirectory(directory);
        var jsonPath = Path.Combine(directory, "run-record.json");
        var record = new InspectionRunRecord(
            "1.9",
            runId,
            recordedAtUtc,
            new InspectionRunRecipe(
                "tool-recipe",
                "1.9",
                Path.Combine(directory, recipeFileName ?? "recipe.json"),
                new string('A', 64)),
            new InspectionRunSource(
                "source.c3d",
                Path.Combine(directory, sourceFileName ?? "source.C3D"),
                new string('B', 64),
                12,
                "mm"),
            toolName,
            status,
            "fixture",
            elapsedMilliseconds,
            [],
            [],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.Combine(directory, "ordered-run.txt"),
                null,
                null,
                jsonPath,
                null,
                null))
        {
            Steps =
            [
                new InspectionRunStepResult(
                    0,
                    "step",
                    "tool",
                    toolName,
                    ["source.c3d"],
                    "output",
                    status,
                    "fixture",
                    elapsedMilliseconds,
                    [],
                    [])
            ]
        };
        InspectionRunRecordJson.Write(jsonPath, record);
        return jsonPath;
    }

    private static VerificationCase Check(
        string name,
        bool passed,
        string evidence) =>
        new(name, passed, evidence);

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(
        string Name,
        bool Passed,
        string Evidence);
}

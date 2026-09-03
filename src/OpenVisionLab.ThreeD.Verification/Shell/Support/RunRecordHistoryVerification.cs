using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Verification.Shell.Support;

internal static class RunRecordHistoryVerification
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Run Record history verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            var reportDirectory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            var fixtureRoot = Path.Combine(reportDirectory, $"run-record-history-fixture-{Guid.NewGuid():N}");
            Directory.CreateDirectory(fixtureRoot);
            var recentPath = Path.Combine(fixtureRoot, "recent.json");
            var thresholdFixture =
                CreateThresholdCorrectionFixture(fixtureRoot);

            var first = WriteFixture(
                fixtureRoot,
                "first",
                "1.5",
                ResultStatus.Pass,
                includeOutputHash: true,
                thresholdFixture.Available);
            var viewModel = new ShellMainWindowViewModel(
                runRecordPath: first.Json,
                htmlReportPath: first.Html,
                csvReportPath: first.Csv,
                recentRunRecordsPath: recentPath);

            Check(
                "schema 1.5 record loads into ordered Run Record view",
                viewModel.InspectionSteps.Count == 1
                && viewModel.InspectionStepSummary.Contains("1.5", StringComparison.Ordinal)
                && viewModel.InspectionStepSummary.Contains("Pass", StringComparison.Ordinal)
                && viewModel.InspectionSteps[0].Timing is "사용 불가" or "Unavailable"
                && viewModel.SourceQualityState == "Unavailable"
                && (viewModel.SourceQualitySummary.Contains(
                        "legacy",
                        StringComparison.OrdinalIgnoreCase)
                    || viewModel.SourceQualitySummary.Contains(
                        "이전",
                        StringComparison.Ordinal)),
                $"{viewModel.InspectionStepSummary};timing={viewModel.InspectionSteps.FirstOrDefault()?.Timing};sourceQuality={viewModel.SourceQualityState}:{viewModel.SourceQualitySummary}");
            Check(
                "available threshold correction preserves before suggested manual development and Held-out identities",
                viewModel.ThresholdCorrectionState == "Available"
                && viewModel.ThresholdCorrectionItems.Any(item =>
                    item.Label == "Before"
                    && item.Evidence.Contains(
                        "MinimumThickness=0",
                        StringComparison.Ordinal))
                && viewModel.ThresholdCorrectionItems.Any(item =>
                    item.Label == "Suggested"
                    && item.Evidence.Contains(
                        "MaximumThickness=4",
                        StringComparison.Ordinal))
                && viewModel.ThresholdCorrectionItems.Any(item =>
                    item.Label == "Manual"
                    && item.Evidence.Contains(
                        "MinimumThickness=1.5",
                        StringComparison.Ordinal))
                && viewModel.ThresholdCorrectionItems.Any(item =>
                    item.Label == "Development"
                    && item.Evidence.Contains(
                        "Mismatch 1->0",
                        StringComparison.Ordinal)
                    && item.Evidence.Contains(
                        new string('D', 64),
                        StringComparison.Ordinal))
                && viewModel.ThresholdCorrectionItems.Any(item =>
                    item.Label == "Held-out"
                    && item.Evidence.Contains(
                        new string('H', 64),
                        StringComparison.Ordinal)),
                string.Join(
                    " | ",
                    viewModel.ThresholdCorrectionItems.Select(item =>
                        $"{item.Label}:{item.Evidence}")));
            Check(
                "projection states fail closed for missing stale mismatched and invalid sidecars",
                thresholdFixture.Missing.State
                    == InspectionRunThresholdCorrectionEvidenceState.Unavailable
                && thresholdFixture.Stale.State
                    == InspectionRunThresholdCorrectionEvidenceState.Stale
                && thresholdFixture.Mismatch.State
                    == InspectionRunThresholdCorrectionEvidenceState.Mismatch
                && thresholdFixture.Invalid.State
                    == InspectionRunThresholdCorrectionEvidenceState.Invalid,
                $"missing={thresholdFixture.Missing.State}; stale={thresholdFixture.Stale.State}; mismatch={thresholdFixture.Mismatch.State}; invalid={thresholdFixture.Invalid.State}");
            Check(
                "loaded record becomes the first persisted recent item",
                viewModel.RecentRunRecords.Count == 1
                && PathsEqual(viewModel.RecentRunRecords[0].Path, first.Json)
                && RecipeRecentFileStore.Load(recentPath).Count == 1,
                string.Join(";", viewModel.RecentRunRecords.Select(item => item.Path)));
            Check(
                "current JSON HTML CSV folder and export commands are enabled",
                viewModel.OpenRunRecordCommand.CanExecute(null)
                && viewModel.OpenHtmlReportCommand.CanExecute(null)
                && viewModel.OpenCsvReportCommand.CanExecute(null)
                && viewModel.OpenRunRecordFolderCommand.CanExecute(null)
                && viewModel.ExportRunRecordCommand.CanExecute(null),
                "all current artifact commands enabled");

            var exportRoot = Path.Combine(fixtureRoot, "exports");
            var exported = viewModel.ExportCurrentRunRecordBundle(exportRoot, out var exportDirectory);
            var exportedJson = Path.Combine(exportDirectory, Path.GetFileName(first.Json));
            var exportedHtml = Path.Combine(exportDirectory, Path.GetFileName(first.Html));
            var exportedCsv = Path.Combine(exportDirectory, Path.GetFileName(first.Csv));
            Check(
                "export creates a collision-safe folder with byte-identical JSON HTML CSV",
                exported
                && File.Exists(exportedJson)
                && File.Exists(exportedHtml)
                && File.Exists(exportedCsv)
                && File.ReadAllBytes(exportedJson).SequenceEqual(File.ReadAllBytes(first.Json))
                && File.ReadAllBytes(exportedHtml).SequenceEqual(File.ReadAllBytes(first.Html))
                && File.ReadAllBytes(exportedCsv).SequenceEqual(File.ReadAllBytes(first.Csv)),
                exportDirectory);

            var second = WriteFixture(
                fixtureRoot,
                "second",
                "1.3",
                ResultStatus.Fail,
                includeOutputHash: false,
                thresholdCorrection: null);
            var loadedSecond = viewModel.LoadRunRecord(second.Json, out var secondMessage);
            Check(
                "schema 1.3 record remains readable and moves to recent first",
                loadedSecond
                && viewModel.InspectionSteps.Count == 1
                && viewModel.InspectionStepSummary.Contains("1.3", StringComparison.Ordinal)
                && viewModel.InspectionStepSummary.Contains("Fail", StringComparison.Ordinal)
                && viewModel.ThresholdCorrectionState == "Unavailable"
                && viewModel.SourceQualityState == "Unavailable"
                && viewModel.RecentRunRecords.Count == 2
                && PathsEqual(viewModel.RecentRunRecords[0].Path, second.Json),
                secondMessage);

            var invalidPath = Path.Combine(fixtureRoot, "invalid.json");
            File.WriteAllText(invalidPath, "{ invalid");
            var previousPath = viewModel.SelectedRecentRunRecord?.Path;
            var invalidLoaded = viewModel.LoadRunRecord(invalidPath, out var invalidMessage);
            Check(
                "invalid JSON is rejected without replacing the current record",
                !invalidLoaded
                && PathsEqual(viewModel.SelectedRecentRunRecord?.Path, previousPath)
                && viewModel.InspectionStepSummary.Contains("1.3", StringComparison.Ordinal),
                invalidMessage);

            var firstRecent = viewModel.RecentRunRecords.Single(item => PathsEqual(item.Path, first.Json));
            viewModel.OpenRecentRunRecordCommand.Execute(firstRecent);
            Check(
                "recent selection reopens the exact record without executing inspection",
                PathsEqual(viewModel.SelectedRecentRunRecord?.Path, first.Json)
                && viewModel.InspectionStepSummary.Contains("1.5", StringComparison.Ordinal)
                && viewModel.ThresholdCorrectionState == "Available"
                && viewModel.RecentRunRecords.Count == 2,
                viewModel.InspectionStepSummary);
            Check(
                "recent list persists newest-first and stays bounded",
                RecipeRecentFileStore.Load(recentPath).Count == 2
                && PathsEqual(RecipeRecentFileStore.Load(recentPath)[0], first.Json)
                && RecipeRecentFileStore.Load(recentPath).Count <= RecipeRecentFileStore.MaximumEntries,
                string.Join(";", RecipeRecentFileStore.Load(recentPath)));

            var normalStartup = new ShellMainWindowViewModel(
                recentRunRecordsPath: recentPath);
            Check(
                "normal startup keeps recent records available without selecting stale evidence",
                normalStartup.RecentRunRecords.Count == 2
                && normalStartup.SelectedRecentRunRecord is null
                && normalStartup.InspectionSteps.Count == 0
                && !normalStartup.OpenRunRecordCommand.CanExecute(null)
                && normalStartup.ThresholdCorrectionState == "Unavailable"
                && normalStartup.SourceQualityState == "Unavailable",
                normalStartup.InspectionStepSummary);

            normalStartup.LoadRunRecord(first.Json, out _);
            normalStartup.ClearCurrentRunEvidenceForRecipeContext();
            Check(
                "recipe or source context reset clears current evidence but preserves history",
                normalStartup.RecentRunRecords.Count == 2
                && normalStartup.SelectedRecentRunRecord is null
                && normalStartup.InspectionSteps.Count == 0
                && !normalStartup.OpenRunRecordCommand.CanExecute(null)
                && !normalStartup.ExportRunRecordCommand.CanExecute(null)
                && normalStartup.SourceQualityState == "Unavailable",
                $"recent={normalStartup.RecentRunRecords.Count}; steps={normalStartup.InspectionSteps.Count}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var outputDirectory = Path.GetDirectoryName(reportPath);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Run Record history verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static FixturePaths WriteFixture(
        string root,
        string name,
        string schema,
        ResultStatus status,
        bool includeOutputHash,
        InspectionRunThresholdCorrectionEvidence? thresholdCorrection)
    {
        var json = Path.Combine(root, $"{name}.json");
        var html = Path.Combine(root, $"{name}.html");
        var csv = Path.Combine(root, $"{name}.csv");
        File.WriteAllText(html, $"<html><body>{name}</body></html>");
        File.WriteAllText(csv, $"name,status{Environment.NewLine}{name},{status}");

        var step = new InspectionRunStepResult(
            0,
            $"step.{name}",
            "filter",
            "C3D Median Filter",
            ["source.c3d"],
            $"output.{name}",
            status,
            "fixture",
            1.25,
            [],
            [])
        {
            OutputContentSha256 = includeOutputHash ? new string('A', 64) : null
        };
        var record = new InspectionRunRecord(
            schema,
            $"run-{name}",
            new DateTimeOffset(2026, 7, 23, 12, name == "first" ? 1 : 2, 0, TimeSpan.Zero),
            new InspectionRunRecipe("tool-recipe", "1.3", Path.Combine(root, "recipe.json"), new string('B', 64)),
            new InspectionRunSource("source.c3d", Path.Combine(root, "source.c3d"), new string('C', 64), 1, "raw-height"),
            "Ordered Tool Recipe",
            status,
            "fixture",
            1.25,
            [],
            [],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.Combine(root, "runner.txt"),
                null,
                null,
                json,
                html,
                csv))
        {
            Steps = [step],
            ThresholdCorrectionEvidence = thresholdCorrection
        };
        File.WriteAllText(json, JsonSerializer.Serialize(record, JsonOptions));
        return new FixturePaths(json, html, csv);
    }

    private static ThresholdFixture CreateThresholdCorrectionFixture(
        string root)
    {
        var recipePath = Path.Combine(root, "threshold.recipe.json");
        File.WriteAllText(recipePath, "{}");
        var sourceHash = new string('S', 64);
        var step = new ToolRecipeStep(
            "step.thickness",
            "thickness",
            "Thickness",
            2,
            ["source", "reference", "measurement"],
            "output.thickness",
            [
                new ToolRecipeParameter("MinimumThickness", "1.5"),
                new ToolRecipeParameter("MaximumThickness", "4.5")
            ]);
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Threshold fixture",
            new ToolRecipeSource(
                "source",
                "Source",
                "C3D",
                "raw-height",
                "frame",
                Path.Combine(root, "source.C3D"),
                ContentSha256: sourceHash,
                GridWidth: 2,
                GridHeight: 2),
            [],
            [step]);
        var candidate = new ToolRecipeThresholdCandidate(
            "candidate.fixture",
            ToolRecipeEvidenceScope.StepMetric,
            step.Id,
            step.ToolName,
            "Mean",
            "raw-height",
            ToolRecipeThresholdLimitKind.Range,
            2,
            4,
            1,
            0,
            1,
            0,
            []);
        var proposal = new ToolRecipeThresholdParameterProposal(
            ToolRecipeThresholdParameterProposal.CurrentContractVersion,
            candidate.CandidateId,
            step.Id,
            step.ToolId,
            step.ToolName,
            candidate.MetricName,
            candidate.LimitKind,
            [
                new ToolRecipeThresholdParameterChange(
                    "MinimumThickness",
                    "0",
                    "2"),
                new ToolRecipeThresholdParameterChange(
                    "MaximumThickness",
                    "20",
                    "4")
            ],
            candidate);
        var developmentIdentity = new string('D', 64);
        var developmentPath = Path.Combine(root, "development.C3D");
        var before = new ToolRecipeThresholdDevelopmentSampleEvidence(
            1,
            developmentIdentity,
            developmentPath,
            ToolRecipeValidationSampleRole.Good,
            ResultStatus.Fail,
            false,
            "Before correction",
            []);
        var after = before with
        {
            Status = ResultStatus.Pass,
            ExpectedMatch = true,
            Message = "Corrected development"
        };
        var evidence = new ToolRecipeThresholdCorrectionEvidence(
            ToolRecipeThresholdCorrectionEvidence.CurrentContractVersion,
            document.Name,
            sourceHash,
            proposal,
            ResultStatus.Pass,
            "Held-out replay passed.",
            [
                new ToolRecipeThresholdHeldOutSampleEvidence(
                    1,
                    new string('H', 64),
                    Path.Combine(root, "held-out.C3D"),
                    ResultStatus.Pass,
                    "Held-out pass",
                    [])
            ],
            new ToolRecipeThresholdManualCorrectionEvidence(
                ToolRecipeThresholdManualCorrectionEvidence
                    .CurrentContractVersion,
                [
                    new ToolRecipeThresholdManualParameterChange(
                        "MinimumThickness",
                        "2",
                        "1.5"),
                    new ToolRecipeThresholdManualParameterChange(
                        "MaximumThickness",
                        "4",
                        "4.5")
                ],
                1,
                [before],
                0,
                [after]));
        ToolRecipeThresholdCorrectionEvidenceStore.SaveForRecipe(
            recipePath,
            evidence);

        var available =
            ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                recipePath,
                document);
        var stale =
            ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                recipePath,
                document with
                {
                    Steps =
                    [
                        step with
                        {
                            Parameters =
                            [
                                new ToolRecipeParameter(
                                    "MinimumThickness",
                                    "9"),
                                new ToolRecipeParameter(
                                    "MaximumThickness",
                                    "4.5")
                            ]
                        }
                    ]
                });
        var mismatch =
            ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                recipePath,
                document with { Name = "Different recipe" });

        var missingPath = Path.Combine(root, "missing.recipe.json");
        File.WriteAllText(missingPath, "{}");
        var missing =
            ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                missingPath,
                document);

        var invalidPath = Path.Combine(root, "invalid.recipe.json");
        File.WriteAllText(invalidPath, "{}");
        File.WriteAllText(
            ToolRecipeThresholdCorrectionEvidenceStore.GetPathForRecipe(
                invalidPath),
            "{ invalid");
        var invalid =
            ToolRecipeThresholdCorrectionRunRecordProjection.Create(
                invalidPath,
                document);

        return new ThresholdFixture(
            available,
            missing,
            stale,
            mismatch,
            invalid);
    }

    private static bool PathsEqual(string? left, string? right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);

    private sealed record FixturePaths(string Json, string Html, string Csv);

    private sealed record ThresholdFixture(
        InspectionRunThresholdCorrectionEvidence Available,
        InspectionRunThresholdCorrectionEvidence Missing,
        InspectionRunThresholdCorrectionEvidence Stale,
        InspectionRunThresholdCorrectionEvidence Mismatch,
        InspectionRunThresholdCorrectionEvidence Invalid);
}

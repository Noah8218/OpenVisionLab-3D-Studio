using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DPresenceCheckGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory =
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var source = CreateFixture();
        var sourceValuesBefore = source.Values.ToArray();
        var sourcePath = Path.Combine(
            directory,
            "known-presence-check-fixture.c3d");
        source.SaveC3D(sourcePath);

        var policy = new C3DPresenceCheckPolicy(0.95d, 9d, 11d);
        var goodRecipe = CreateRecipe(
            source,
            Path.GetFileName(sourcePath),
            new ToolRecipeGridRectangle(0, 0, 1, 2),
            "step.presence.good",
            "output.presence.good",
            policy);
        var goodRecipePath = Path.Combine(
            directory,
            "known-presence-check-good.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(goodRecipePath, goodRecipe);
        goodRecipe = ToolRecipeDocumentStore.Load(goodRecipePath);

        var missingRecipe = CreateRecipe(
            source,
            Path.GetFileName(sourcePath),
            new ToolRecipeGridRectangle(1, 0, 1, 2),
            "step.presence.missing",
            "output.presence.missing",
            policy);
        var missingRecipePath = Path.Combine(
            directory,
            "known-presence-check-missing.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(missingRecipePath, missingRecipe);
        missingRecipe = ToolRecipeDocumentStore.Load(missingRecipePath);

        var mixedRecipe = CreateRecipe(
            source,
            Path.GetFileName(sourcePath),
            new ToolRecipeGridRectangle(2, 0, 1, 2),
            "step.presence.mixed",
            "output.presence.mixed",
            policy);
        var mixed = ToolRecipeHeightMeasurementExecution.Execute(
            mixedRecipe,
            "step.presence.mixed",
            recipeDirectory: directory);

        var good = ToolRecipeHeightMeasurementExecution.Execute(
            goodRecipe,
            "step.presence.good",
            recipeDirectory: directory);
        var repeated = ToolRecipeHeightMeasurementExecution.Execute(
            goodRecipe,
            "step.presence.good",
            recipeDirectory: directory);
        var goodOutput = good.Output?.PresenceCheck;
        var goodGraph = ToolRecipeOrderedGraphExecution.Execute(
            goodRecipe,
            sourcePath);
        var goodRunSteps = ToolRecipeOrderedGraphRunRecordProjection.Create(
            goodRecipe,
            goodGraph);

        var missing = ToolRecipeHeightMeasurementExecution.Execute(
            missingRecipe,
            "step.presence.missing",
            recipeDirectory: directory);
        var missingGraph = ToolRecipeOrderedGraphExecution.Execute(
            missingRecipe,
            sourcePath);
        var missingOutput = missing.Output?.PresenceCheck;

        var invalidPolicyRecipe = goodRecipe with
        {
            Steps =
            [
                goodRecipe.Steps[0] with
                {
                    Parameters = new C3DPresenceCheckPolicy(1.1d, 9d, 11d)
                        .ToRecipeParameters()
                }
            ]
        };
        var invalidPolicy = ToolRecipeHeightMeasurementExecution.Execute(
            invalidPolicyRecipe,
            "step.presence.good",
            recipeDirectory: directory);

        var invalidSelectionRecipe = goodRecipe with
        {
            Selections =
            [
                goodRecipe.Selections![0] with
                {
                    GridRectangle = new ToolRecipeGridRectangle(0, 3, 1, 2)
                }
            ]
        };
        var invalidSelection = ToolRecipeHeightMeasurementExecution.Execute(
            invalidSelectionRecipe,
            "step.presence.good",
            recipeDirectory: directory);

        var mismatchedBinding = C3DPresenceCheckRule.Evaluate(
            new C3DPresenceCheckInput(
                "output.presence.bad-binding",
                source.EntityId,
                source.EntityId,
                "0000000000000000000000000000000000000000000000000000000000000000",
                source.Unit,
                source.FrameId,
                source.Width,
                source.Height,
                source.Values.ToArray(),
                goodRecipe.Selections![0],
                policy));

        var runRecordJsonPath = Path.Combine(
            directory,
            "known-presence-check-run-record.json");
        var runRecordHtmlPath = Path.Combine(
            directory,
            "known-presence-check-run-record.html");
        var runRecordCsvPath = Path.Combine(
            directory,
            "known-presence-check-run-record.csv");
        var runnerReportPath = Path.Combine(
            directory,
            "known-presence-check-runner.txt");
        File.WriteAllText(runnerReportPath, "Presence Check export fixture.");
        RunRecordWriter.WriteOrderedGraph(
            new RunArtifactOptions(
                runRecordJsonPath,
                runRecordHtmlPath,
                runRecordCsvPath,
                null),
            goodRecipePath,
            goodRecipe,
            sourcePath,
            goodGraph,
            runnerReportPath,
            null);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
        var exportedRecord = JsonSerializer.Deserialize<InspectionRunRecord>(
            File.ReadAllText(runRecordJsonPath),
            jsonOptions);
        var exportedPresence = exportedRecord?.Steps?[0].PresenceCheck;
        var runRecordHtml = File.ReadAllText(runRecordHtmlPath);
        var runRecordCsvLines = File.ReadAllLines(runRecordCsvPath);
        var presenceCsvRows = runRecordCsvLines
            .Where(line => line.Contains(
                "\"presenceFeature\"",
                StringComparison.Ordinal))
            .ToArray();

        var missingEvidenceRejected = ProjectionFails(
            goodRecipe,
            goodGraph with
            {
                Steps =
                [
                    goodGraph.Steps[0] with { PresenceCheck = null }
                ]
            });
        var malformedEvidenceRejected = ProjectionFails(
            goodRecipe,
            goodGraph with
            {
                Steps =
                [
                    goodGraph.Steps[0] with
                    {
                        PresenceCheck = goodOutput is null
                            ? null
                            : goodOutput with
                            {
                                Feature = goodOutput.Feature with
                                {
                                    MissingCellCount = 99
                                }
                            }
                    }
                ]
            });

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "policy-round-trip",
                C3DPresenceCheckPolicy.FromRecipeParameters(
                    policy.ToRecipeParameters()) == policy,
                policy.ToString()),
            Check(
                "good-feature-present",
                good.Result.Status == ResultStatus.Pass
                && goodOutput is
                {
                    Feature.Decision: ResultStatus.Pass,
                    Feature.TotalCellCount: 2,
                    Feature.FiniteCellCount: 2,
                    Feature.MissingCellCount: 0,
                    Feature.FiniteCoverageRatio: 1d,
                    Feature.MeanRawHeight: 10d
                },
                $"status={good.Result.Status};output={goodOutput}"),
            Check(
                "missing-feature-fails-closed",
                missing.Result.Status == ResultStatus.Fail
                && missingOutput is
                {
                    Feature.Decision: ResultStatus.Fail,
                    Feature.TotalCellCount: 2,
                    Feature.FiniteCellCount: 0,
                    Feature.MissingCellCount: 2,
                    Feature.FiniteCoverageRatio: 0d,
                    Feature.MeanRawHeight: null
                }
                && missingOutput.Feature.DecisionReason.Contains(
                    "no finite samples",
                    StringComparison.OrdinalIgnoreCase),
                $"status={missing.Result.Status};output={missingOutput}"),
            Check(
                "partial-coverage-fails-closed",
                mixed.Result.Status == ResultStatus.Fail
                && mixed.Output?.PresenceCheck?.Feature is
                {
                    Decision: ResultStatus.Fail,
                    FiniteCellCount: 1,
                    MissingCellCount: 1,
                    FiniteCoverageRatio: 0.5d,
                    MeanRawHeight: 10d
                }
                && mixed.Output.PresenceCheck.Feature.DecisionReason.Contains(
                    "below minimum",
                    StringComparison.OrdinalIgnoreCase),
                $"status={mixed.Result.Status};output={mixed.Output?.PresenceCheck}"),
            Check(
                "deterministic-content-identity",
                goodOutput?.ContentSha256 == repeated.Output?.PresenceCheck?.ContentSha256
                && !string.IsNullOrWhiteSpace(goodOutput?.ContentSha256),
                goodOutput?.ContentSha256 ?? "missing"),
            Check(
                "source-immutable",
                source.ContentSha256 == goodOutput?.InputContentSha256
                && source.Values.ToArray().SequenceEqual(sourceValuesBefore),
                $"sourceSha={source.ContentSha256};inputSha={goodOutput?.InputContentSha256}"),
            Check(
                "ordered-runner-parity",
                goodGraph.Status == ResultStatus.Pass
                && goodGraph.Steps.Count == 1
                && goodGraph.Steps[0].PresenceCheck?.ContentSha256
                    == goodOutput?.ContentSha256
                && goodGraph.Steps[0].Result.Status == good.Result.Status
                && missingGraph.Status == ResultStatus.Fail
                && missingGraph.Steps[0].PresenceCheck?.Feature.Decision
                    == ResultStatus.Fail,
                $"good={goodGraph.Status};missing={missingGraph.Status}"),
            Check(
                "ordered-projection-reuses-typed-evidence",
                ReferenceEquals(
                    goodRunSteps[0].PresenceCheck,
                    goodGraph.Steps[0].PresenceCheck)
                && goodRunSteps[0].PresenceCheck?.ContentSha256
                    == goodOutput?.ContentSha256,
                $"sameInstance={ReferenceEquals(goodRunSteps[0].PresenceCheck, goodGraph.Steps[0].PresenceCheck)}"),
            Check(
                "run-record-json-preserves-presence",
                exportedRecord?.SchemaVersion == "1.9"
                && exportedPresence?.ContentSha256 == goodOutput?.ContentSha256
                && exportedPresence?.Feature == goodOutput?.Feature,
                $"schema={exportedRecord?.SchemaVersion};sha={exportedPresence?.ContentSha256}"),
            Check(
                "run-record-html-preserves-presence",
                runRecordHtml.Contains(
                    "<h2>Presence Check feature results</h2>",
                    StringComparison.Ordinal)
                && runRecordHtml.Contains(
                    goodOutput?.FeatureSelectionId ?? "missing-feature",
                    StringComparison.Ordinal)
                && runRecordHtml.Contains(
                    goodOutput?.ContentSha256 ?? "missing-hash",
                    StringComparison.Ordinal),
                $"bytes={new FileInfo(runRecordHtmlPath).Length}"),
            Check(
                "run-record-csv-preserves-presence",
                runRecordCsvLines[0].Contains(
                    "rowType,completenessContentSha256,completenessUnit,completenessFrame",
                    StringComparison.Ordinal)
                && runRecordCsvLines[0].Contains(
                    "presenceContentSha256,presenceUnit,presenceFrame,presenceFeatureId",
                    StringComparison.Ordinal)
                && presenceCsvRows.Length == 1
                && presenceCsvRows[0].Contains(
                    $"\"{goodOutput?.ContentSha256}\",\"{goodOutput?.Unit}\",\"{goodOutput?.FrameId}\",\"{goodOutput?.FeatureSelectionId}\"",
                    StringComparison.Ordinal),
                $"rows={presenceCsvRows.Length};lines={runRecordCsvLines.Length}"),
            Check(
                "invalid-policy-fails-closed",
                invalidPolicy.Result.Status == ResultStatus.Error
                && invalidPolicy.Output is null
                && invalidPolicy.Result.Message.Contains(
                    "between zero and one",
                    StringComparison.OrdinalIgnoreCase),
                $"status={invalidPolicy.Result.Status};message={invalidPolicy.Result.Message}"),
            Check(
                "invalid-selection-fails-closed",
                invalidSelection.Result.Status == ResultStatus.Error
                && invalidSelection.Output is null,
                $"status={invalidSelection.Result.Status};message={invalidSelection.Result.Message}"),
            Check(
                "adapter-binding-mismatch-fails-closed",
                mismatchedBinding.Result.Status == ResultStatus.Error
                && mismatchedBinding.Output is null,
                $"status={mismatchedBinding.Result.Status};message={mismatchedBinding.Result.Message}"),
            Check(
                "missing-projection-evidence-rejected",
                missingEvidenceRejected,
                $"rejected={missingEvidenceRejected}"),
            Check(
                "malformed-projection-evidence-rejected",
                malformedEvidenceRejected,
                $"rejected={malformedEvidenceRejected}")
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DPresenceCheckGoldenVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "Contract|feature=explicit-GridRectangle|coverage=inclusive|meanRawHeight=inclusive|noFiniteMean=Fail|sourceMutation=false|calibratedMetrology=false",
            $"Fixture|path={sourcePath}|goodRecipe={goodRecipePath}|missingRecipe={missingRecipePath}|width={source.Width}|height={source.Height}|sourceSha256={source.ContentSha256}",
            $"Good|sha256={goodOutput?.ContentSha256}|feature={goodOutput?.FeatureSelectionId}|coverage={goodOutput?.Feature.FiniteCoverageRatio}|mean={goodOutput?.Feature.MeanRawHeight}|decision={goodOutput?.Feature.Decision}",
            $"Missing|sha256={missingOutput?.ContentSha256}|feature={missingOutput?.FeatureSelectionId}|coverage={missingOutput?.Feature.FiniteCoverageRatio}|mean={missingOutput?.Feature.MeanRawHeight?.ToString() ?? "missing"}|decision={missingOutput?.Feature.Decision}",
            $"RunRecord|json={runRecordJsonPath}|html={runRecordHtmlPath}|csv={runRecordCsvPath}|schema={exportedRecord?.SchemaVersion}",
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);

        Console.WriteLine(
            $"Presence Check golden verification: "
            + $"{(passed == cases.Count ? "PASS" : "FAIL")} "
            + $"({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        const int width = 4;
        const int height = 3;
        var values =
            new double[]
            {
                10d, 10d, 12d, 12d,
                double.NaN, double.NaN, double.NaN, double.NaN,
                10d, double.NaN, 12d, 12d
            };
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.presence-check",
            width,
            height,
            values);
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        ToolRecipeGridRectangle region,
        string stepId,
        string outputEntityId,
        C3DPresenceCheckPolicy policy)
    {
        var binding = new ToolRecipeSelectionSourceBinding(
            "C3D",
            source.ContentSha256,
            source.Width,
            source.Height);
        var selectionId = $"selection.{stepId[5..]}.feature";
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Known Presence Check Fixture",
            new ToolRecipeSource(
                source.EntityId,
                "Known Presence Check Fixture",
                "C3D",
                source.Unit,
                source.FrameId,
                sourcePath,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [
                new ToolRecipeStep(
                    stepId,
                    "presence-check",
                    "Presence Check",
                    2,
                    [source.EntityId, selectionId],
                    outputEntityId,
                    policy.ToRecipeParameters())
            ],
            [
                new ToolRecipeSelection(
                    selectionId,
                    "Presence feature",
                    ToolRecipeSelectionKinds.GridRectangle,
                    source.EntityId,
                    source.FrameId,
                    binding,
                    region,
                    null,
                    null)
            ]);
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);

    private static bool ProjectionFails(
        ToolRecipeDocument document,
        ToolRecipeOrderedGraphExecutionResult execution)
    {
        try
        {
            _ = ToolRecipeOrderedGraphRunRecordProjection.Create(
                document,
                execution);
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }
}

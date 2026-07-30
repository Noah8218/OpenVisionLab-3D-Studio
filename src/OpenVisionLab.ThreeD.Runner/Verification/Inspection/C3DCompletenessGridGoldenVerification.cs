using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DCompletenessGridGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory =
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var source = CreateFixture();
        var sourcePath = Path.Combine(
            directory,
            "known-completeness-grid-fixture.c3d");
        source.SaveC3D(sourcePath);
        var recipe = CreateRecipe(source, Path.GetFileName(sourcePath));
        var recipePath = Path.Combine(
            directory,
            "known-completeness-grid-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, recipe);
        recipe = ToolRecipeDocumentStore.Load(recipePath);
        var policy = new C3DCompletenessPresencePolicy(0.5d, -3d, 3d);
        var policyRecipe = WithPolicy(recipe, policy);
        var policyRecipePath = Path.Combine(
            directory,
            "known-completeness-grid-policy-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(policyRecipePath, policyRecipe);
        policyRecipe = ToolRecipeDocumentStore.Load(policyRecipePath);

        var direct = ToolRecipeHeightMeasurementExecution.Execute(
            recipe,
            "step.completeness.01",
            recipeDirectory: directory);
        var repeated = ToolRecipeHeightMeasurementExecution.Execute(
            recipe,
            "step.completeness.01",
            recipeDirectory: directory);
        var output = direct.Output?.CompletenessGrid;
        var graph = ToolRecipeOrderedGraphExecution.Execute(recipe, sourcePath);
        var policyDirect = ToolRecipeHeightMeasurementExecution.Execute(
            policyRecipe,
            "step.completeness.01",
            recipeDirectory: directory);
        var policyRepeated = ToolRecipeHeightMeasurementExecution.Execute(
            policyRecipe,
            "step.completeness.01",
            recipeDirectory: directory);
        var policyOutput = policyDirect.Output?.CompletenessGrid;
        var policyGraph = ToolRecipeOrderedGraphExecution.Execute(
            policyRecipe,
            sourcePath);
        var allValidSource = CreateAllValidFixture();
        var allValidSourcePath = Path.Combine(
            directory,
            "known-completeness-grid-all-valid-fixture.c3d");
        allValidSource.SaveC3D(allValidSourcePath);
        var allPass = ToolRecipeHeightMeasurementExecution.Execute(
            WithPolicy(
                CreateRecipe(
                    allValidSource,
                    Path.GetFileName(allValidSourcePath)),
                new C3DCompletenessPresencePolicy(1d, -10d, 10d)),
            "step.completeness.01",
            recipeDirectory: directory);
        var allMissingSource = CreateAllMissingInspectionFixture();
        var allMissingSourcePath = Path.Combine(
            directory,
            "known-completeness-grid-all-missing-fixture.c3d");
        allMissingSource.SaveC3D(allMissingSourcePath);
        var allMissing = ToolRecipeHeightMeasurementExecution.Execute(
            WithPolicy(
                CreateRecipe(
                    allMissingSource,
                    Path.GetFileName(allMissingSourcePath)),
                policy),
            "step.completeness.01",
            recipeDirectory: directory);
        var invalidPolicy = ToolRecipeHeightMeasurementExecution.Execute(
            WithPolicy(
                recipe,
                new C3DCompletenessPresencePolicy(1.1d, -3d, 3d)),
            "step.completeness.01",
            recipeDirectory: directory);
        var invalidRecipe = recipe with
        {
            Steps =
            [
                recipe.Steps[0] with
                {
                    Parameters = new C3DCompletenessGridProfile(
                        3,
                        2,
                        2,
                        2,
                        2,
                        2,
                        C3DCompletenessCellShape.GridRectangle)
                        .ToRecipeParameters()
                }
            ]
        };
        var invalid = ToolRecipeHeightMeasurementExecution.Execute(
            invalidRecipe,
            "step.completeness.01",
            recipeDirectory: directory);

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "typed-profile-round-trip",
                output?.Profile == new C3DCompletenessGridProfile(
                    2, 2, 2, 2, 2, 2,
                    C3DCompletenessCellShape.GridRectangle),
                output?.Profile.ToString() ?? "no output"),
            Check(
                "deterministic-cell-order-and-identities",
                output?.Cells.Select(cell => cell.CellId).SequenceEqual(
                    ["r001.c001", "r001.c002", "r002.c001", "r002.c002"])
                    == true,
                string.Join(",", output?.Cells.Select(cell => cell.CellId) ?? [])),
            Check(
                "deterministic-cell-geometry",
                output?.Cells.Select(cell => cell.Region).SequenceEqual(
                    [
                        new ToolRecipeGridRectangle(2, 0, 2, 2),
                        new ToolRecipeGridRectangle(2, 2, 2, 2),
                        new ToolRecipeGridRectangle(4, 0, 2, 2),
                        new ToolRecipeGridRectangle(4, 2, 2, 2)
                    ]) == true,
                string.Join(";", output?.Cells.Select(cell => cell.Region) ?? [])),
            Check(
                "exact-reference-mean",
                output?.ReferenceFiniteCellCount == 4
                && output.ReferenceMeanRawHeight == 10d,
                $"finite={output?.ReferenceFiniteCellCount};mean={output?.ReferenceMeanRawHeight}"),
            Check(
                "known-finite-coverage",
                output?.Cells.Select(cell => cell.FiniteCoverageRatio)
                    .SequenceEqual([1d, 0.75d, 0.5d, 0d]) == true,
                string.Join(
                    ",",
                    output?.Cells.Select(cell => cell.FiniteCoverageRatio) ?? [])),
            Check(
                "known-missing-counts",
                output?.Cells.Select(cell => cell.MissingCellCount)
                    .SequenceEqual([0, 1, 2, 4]) == true,
                string.Join(
                    ",",
                    output?.Cells.Select(cell => cell.MissingCellCount) ?? [])),
            Check(
                "known-reference-relative-heights",
                output?.Cells[0].ReferenceRelativeMeanRawHeight == 2d
                && output.Cells[1].ReferenceRelativeMeanRawHeight == 4d
                && output.Cells[2].ReferenceRelativeMeanRawHeight == -2d
                && output.Cells[3].ReferenceRelativeMeanRawHeight is null,
                string.Join(
                    ",",
                    output?.Cells.Select(cell =>
                        cell.ReferenceRelativeMeanRawHeight?.ToString() ?? "missing")
                    ?? [])),
            Check(
                "all-missing-cell-remains-explicit",
                output?.Cells[3] is
                {
                    FiniteCellCount: 0,
                    MissingCellCount: 4,
                    MeanRawHeight: null,
                    ReferenceRelativeMeanRawHeight: null
                },
                output?.Cells[3].ToString() ?? "no cell"),
            Check(
                "evidence-only-no-acceptance",
                direct.Result.Status == ResultStatus.Warning
                && direct.Result.Message.Contains(
                    "no acceptance policy",
                    StringComparison.OrdinalIgnoreCase)
                && direct.Result.Overlays.Count == 0,
                $"status={direct.Result.Status};message={direct.Result.Message}"),
            Check(
                "deterministic-content-identity",
                output?.ContentSha256
                    == repeated.Output?.CompletenessGrid?.ContentSha256,
                output?.ContentSha256 ?? "no hash"),
            Check(
                "source-immutable",
                source.ContentSha256 == output?.InputContentSha256
                && source.Values.Span[2 * source.Width] == 12d
                && double.IsNaN(source.Values.Span[3 * source.Width + 2]),
                $"source={source.ContentSha256};outputInput={output?.InputContentSha256}"),
            Check(
                "adapter-preserves-typed-output",
                direct.Output?.ContentSha256 == output?.ContentSha256
                && direct.Output?.SelectionId
                    == "selection.reference;selection.inspection-grid",
                $"adapter={direct.Output?.ContentSha256};typed={output?.ContentSha256}"),
            Check(
                "ordered-runner-parity",
                graph.Status == ResultStatus.Warning
                && graph.Steps.Count == 1
                && graph.Steps[0].OutputContentSha256 == output?.ContentSha256
                && graph.Steps[0].Evidence.Contains(
                    "minimum finite coverage 0.0",
                    StringComparison.Ordinal),
                $"status={graph.Status};sha={graph.Steps.FirstOrDefault()?.OutputContentSha256};evidence={graph.Steps.FirstOrDefault()?.Evidence}"),
            Check(
                "inclusive-policy-cell-decisions",
                policyOutput?.Cells.Select(cell => cell.Decision).SequenceEqual(
                    [
                        ResultStatus.Pass,
                        ResultStatus.Fail,
                        ResultStatus.Pass,
                        ResultStatus.Fail
                    ]) == true,
                string.Join(
                    ",",
                    policyOutput?.Cells.Select(cell => cell.Decision) ?? [])),
            Check(
                "all-missing-cell-fails-closed",
                policyOutput?.Cells[3] is
                {
                    MeanRawHeight: null,
                    ReferenceRelativeMeanRawHeight: null,
                    Decision: ResultStatus.Fail
                } missingCell
                && missingCell.DecisionReason.Contains(
                    "finite mean missing",
                    StringComparison.Ordinal),
                policyOutput?.Cells[3].ToString() ?? "no cell"),
            Check(
                "aggregate-failed-cell-count",
                policyOutput is
                {
                    PassedCellCount: 2,
                    FailedCellCount: 2,
                    AggregateStatus: ResultStatus.Fail
                }
                && policyDirect.Result.Status == ResultStatus.Fail,
                $"pass={policyOutput?.PassedCellCount};fail={policyOutput?.FailedCellCount};aggregate={policyOutput?.AggregateStatus}"),
            Check(
                "stable-colored-overlay-contract",
                policyOutput?.CellOverlays is { Count: 4 } overlays
                && overlays.Select(overlay => overlay.CellId).SequenceEqual(
                    ["r001.c001", "r001.c002", "r002.c001", "r002.c002"])
                && overlays.Select(overlay => overlay.Status).SequenceEqual(
                    [
                        ResultStatus.Pass,
                        ResultStatus.Fail,
                        ResultStatus.Pass,
                        ResultStatus.Fail
                    ])
                && policyDirect.Result.Overlays.Count == 4,
                string.Join(
                    ";",
                    policyOutput?.CellOverlays?.Select(overlay =>
                        $"{overlay.OverlayId}:{overlay.Status}") ?? [])),
            Check(
                "policy-content-identity",
                policyOutput?.ContentSha256
                    == policyRepeated.Output?.CompletenessGrid?.ContentSha256
                && policyOutput?.ContentSha256 != output?.ContentSha256,
                $"policy={policyOutput?.ContentSha256};evidenceOnly={output?.ContentSha256}"),
            Check(
                "policy-runner-parity",
                policyGraph.Status == ResultStatus.Fail
                && policyGraph.Steps.Count == 1
                && policyGraph.Steps[0].OutputContentSha256
                    == policyOutput?.ContentSha256
                && policyGraph.Steps[0].Evidence.Contains(
                    "pass 2 | fail 2 | aggregate Fail",
                    StringComparison.Ordinal),
                $"status={policyGraph.Status};sha={policyGraph.Steps.FirstOrDefault()?.OutputContentSha256};evidence={policyGraph.Steps.FirstOrDefault()?.Evidence}"),
            Check(
                "all-pass-aggregate",
                allPass.Output?.CompletenessGrid is
                {
                    PassedCellCount: 4,
                    FailedCellCount: 0,
                    AggregateStatus: ResultStatus.Pass
                }
                && allPass.Result.Status == ResultStatus.Pass,
                $"status={allPass.Result.Status};aggregate={allPass.Output?.CompletenessGrid?.AggregateStatus}"),
            Check(
                "all-missing-inspection-aggregate-fails-closed",
                allMissing.Output?.CompletenessGrid is
                {
                    PassedCellCount: 0,
                    FailedCellCount: 4,
                    AggregateStatus: ResultStatus.Fail,
                    CellOverlays.Count: 4
                } allMissingOutput
                && allMissingOutput.Cells.All(cell =>
                    cell.FiniteCellCount == 0
                    && cell.Decision == ResultStatus.Fail)
                && allMissing.Result.Status == ResultStatus.Fail,
                $"status={allMissing.Result.Status};pass={allMissing.Output?.CompletenessGrid?.PassedCellCount};fail={allMissing.Output?.CompletenessGrid?.FailedCellCount}"),
            Check(
                "invalid-policy-fails-closed",
                invalidPolicy.Result.Status == ResultStatus.Error
                && invalidPolicy.Output is null
                && invalidPolicy.Result.Message.Contains(
                    "between zero and one",
                    StringComparison.OrdinalIgnoreCase),
                $"status={invalidPolicy.Result.Status};message={invalidPolicy.Result.Message}"),
            Check(
                "out-of-footprint-profile-fails-closed",
                invalid.Result.Status == ResultStatus.Error
                && invalid.Output is null
                && invalid.Result.Message.Contains(
                    "does not fit",
                    StringComparison.OrdinalIgnoreCase),
                $"status={invalid.Result.Status};message={invalid.Result.Message}")
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DCompletenessGridGoldenVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "Contract|coordinates=X-column,Z-row|shape=GridRectangle|cellOverlap=false|inclusivePolicy=true|allMissing=Fail|aggregate=all-cells-pass|sourceMutation=false",
            $"Fixture|path={sourcePath}|evidenceRecipe={recipePath}|policyRecipe={policyRecipePath}|width={source.Width}|height={source.Height}|sourceSha256={source.ContentSha256}",
            $"Output|sha256={output?.ContentSha256}|cells={output?.Cells.Count}|referenceMean={output?.ReferenceMeanRawHeight}|coverage=1,0.75,0.5,0",
            $"PolicyOutput|sha256={policyOutput?.ContentSha256}|pass={policyOutput?.PassedCellCount}|fail={policyOutput?.FailedCellCount}|aggregate={policyOutput?.AggregateStatus}",
            $"Runner|evidenceStatus={graph.Status}|policyStatus={policyGraph.Status}|policySha256={policyGraph.Steps.FirstOrDefault()?.OutputContentSha256}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine(
            $"Completeness Grid golden verification: "
            + $"{(passed == cases.Count ? "PASS" : "FAIL")} "
            + $"({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static ToolRecipeDocument WithPolicy(
        ToolRecipeDocument recipe,
        C3DCompletenessPresencePolicy policy) =>
        recipe with
        {
            Steps =
            [
                recipe.Steps[0] with
                {
                    Parameters = recipe.Steps[0].Parameters
                        .Where(parameter =>
                            !C3DCompletenessPresencePolicy.ParameterNames.Contains(
                                parameter.Name,
                                StringComparer.Ordinal))
                        .Concat(policy.ToRecipeParameters())
                        .ToArray()
                }
            ]
        };

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        const int width = 8;
        const int height = 8;
        var values = Enumerable.Repeat(10d, width * height).ToArray();

        SetCell(values, width, 2, 0, [12d, 12d, 12d, 12d]);
        SetCell(values, width, 2, 2, [14d, 14d, double.NaN, 14d]);
        SetCell(values, width, 4, 0, [8d, double.NaN, 8d, double.NaN]);
        SetCell(
            values,
            width,
            4,
            2,
            [double.NaN, double.NaN, double.NaN, double.NaN]);
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.completeness",
            width,
            height,
            values);
    }

    private static C3DHeightFieldSnapshot CreateAllValidFixture() =>
        C3DHeightFieldSnapshot.CreateForVerification(
            "source.completeness.all-valid",
            8,
            8,
            Enumerable.Repeat(10d, 8 * 8).ToArray());

    private static C3DHeightFieldSnapshot CreateAllMissingInspectionFixture()
    {
        const int width = 8;
        const int height = 8;
        var values = Enumerable.Repeat(10d, width * height).ToArray();
        for (var row = 2; row < 6; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                values[row * width + column] = double.NaN;
            }
        }

        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.completeness.all-missing",
            width,
            height,
            values);
    }

    private static void SetCell(
        double[] values,
        int width,
        int row,
        int column,
        IReadOnlyList<double> cell)
    {
        values[row * width + column] = cell[0];
        values[row * width + column + 1] = cell[1];
        values[(row + 1) * width + column] = cell[2];
        values[(row + 1) * width + column + 1] = cell[3];
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath)
    {
        var binding = new ToolRecipeSelectionSourceBinding(
            "C3D",
            source.ContentSha256,
            source.Width,
            source.Height);
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Known Completeness Grid Fixture",
            new ToolRecipeSource(
                source.EntityId,
                "Known Completeness Grid Fixture",
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
                    "step.completeness.01",
                    "completeness-grid",
                    "Completeness Grid",
                    3,
                    [
                        source.EntityId,
                        "selection.reference",
                        "selection.inspection-grid"
                    ],
                    "metrics.completeness.01",
                    new C3DCompletenessGridProfile(
                        2,
                        2,
                        2,
                        2,
                        2,
                        2,
                        C3DCompletenessCellShape.GridRectangle)
                        .ToRecipeParameters(),
                    new ToolRecipeDualRoiRouting(
                        "selection.reference",
                        "selection.inspection-grid"))
            ],
            [
                new ToolRecipeSelection(
                    "selection.reference",
                    "Reference ROI",
                    ToolRecipeSelectionKinds.GridRectangle,
                    source.EntityId,
                    source.FrameId,
                    binding,
                    new ToolRecipeGridRectangle(0, 0, 2, 2),
                    null,
                    null),
                new ToolRecipeSelection(
                    "selection.inspection-grid",
                    "Inspection Grid ROI",
                    ToolRecipeSelectionKinds.GridRectangle,
                    source.EntityId,
                    source.FrameId,
                    binding,
                    new ToolRecipeGridRectangle(2, 0, 4, 4),
                    null,
                    null)
            ]);
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}

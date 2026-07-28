using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRemoveOutlierPixelsGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory =
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var fixture = CreateFixture();
        var direct = Evaluate(fixture);
        var repeated = Evaluate(fixture);
        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "known-outlier-count",
                direct.OutlierMask?.OutlierCellCount == 3,
                $"removed={direct.OutlierMask?.OutlierCellCount}"),
            Check(
                "coordinate-true-mask",
                IsOutlier(direct, 3, 3)
                && IsOutlier(direct, 8, 6)
                && IsOutlier(direct, 0, 4)
                && !IsOutlier(direct, 5, 5),
                $"mask={direct.OutlierMask?.Sha256}"),
            Check(
                "before-after-counts",
                direct.Output?.ValidCount == fixture.ValidCount - 3
                && direct.Output?.MissingCount == fixture.MissingCount + 3,
                $"beforeValid={fixture.ValidCount};beforeMissing={fixture.MissingCount};afterValid={direct.Output?.ValidCount};afterMissing={direct.Output?.MissingCount}"),
            Check(
                "source-immutable",
                fixture.ContentSha256 == direct.Output?.RootSourceSha256
                && fixture.ValidCount == 119
                && fixture.MissingCount == 1,
                $"source={fixture.ContentSha256};root={direct.Output?.RootSourceSha256}"),
            Check(
                "deterministic-output-and-mask",
                direct.Output?.ContentSha256 == repeated.Output?.ContentSha256
                && direct.OutlierMask?.Sha256 == repeated.OutlierMask?.Sha256,
                $"output={direct.Output?.ContentSha256};mask={direct.OutlierMask?.Sha256}"),
            VerifyStrictThreshold(),
            VerifyInsufficientNeighbors(),
            VerifyInvalidThreshold()
        };

        var fixturePath = Path.Combine(directory, "known-outlier-fixture.c3d");
        fixture.SaveC3D(fixturePath);
        var recipe = CreateRecipe(fixture, Path.GetFileName(fixturePath));
        var recipePath = Path.Combine(
            directory,
            "known-outlier-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, recipe);
        var adapter = ToolRecipeRemoveOutlierPixelsExecution.Execute(
            ToolRecipeDocumentStore.Load(recipePath),
            "step.remove-outliers.01",
            directory);
        cases.Add(
            Check(
                "recipe-adapter-parity",
                adapter.Result.Status == ResultStatus.Pass
                && adapter.Output?.ContentSha256 == direct.Output?.ContentSha256
                && adapter.OutlierMask?.Sha256 == direct.OutlierMask?.Sha256,
                $"status={adapter.Result.Status};output={adapter.Output?.ContentSha256};mask={adapter.OutlierMask?.Sha256}"));

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"C3DRemoveOutlierPixelsGoldenVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "Contract|rule=LocalMedianAbsoluteDeviation|comparison=strict-greater-than|center=excluded|missing=PreserveMask|boundary=AvailableNeighbors|outlier=SetMissing|sourceMutation=false",
            $"Fixture|path={fixturePath}|recipe={recipePath}|width={fixture.Width}|height={fixture.Height}|valid={fixture.ValidCount}|missing={fixture.MissingCount}",
            $"Expected|removed=3|outputValid={fixture.ValidCount - 3}|outputMissing={fixture.MissingCount + 3}",
            $"Output|sha256={direct.Output?.ContentSha256}|rootSourceSha256={direct.Output?.RootSourceSha256}",
            $"Mask|sha256={direct.OutlierMask?.Sha256}|count={direct.OutlierMask?.OutlierCellCount}|encoding={C3DOutlierCellMap.Encoding}"
        };
        lines.AddRange(
            cases.Select(
                item =>
                    $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine(
            $"Remove Outlier Pixels golden verification: {(passed == cases.Count ? "PASS" : "FAIL")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static C3DHeightFieldSnapshot CreateFixture()
    {
        const int width = 12;
        const int height = 10;
        var values = new double[width * height];
        for (var row = 0; row < height; row++)
        {
            for (var column = 0; column < width; column++)
            {
                values[row * width + column] = 100d + row * 0.5d + column * 0.2d;
            }
        }

        values[3 * width + 3] += 50d;
        values[6 * width + 8] -= 60d;
        values[4 * width] += 45d;
        values[5 * width + 5] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.known-outliers",
            width,
            height,
            values);
    }

    private static C3DRemoveOutlierPixelsEvaluation Evaluate(
        C3DHeightFieldSnapshot source) =>
        C3DRemoveOutlierPixelsRule.Evaluate(
            new C3DRemoveOutlierPixelsInput(
                "step.remove-outliers.01",
                source,
                "derived.outlier-removed.01",
                3,
                20d,
                3));

    private static bool IsOutlier(
        C3DRemoveOutlierPixelsEvaluation evaluation,
        int column,
        int row) =>
        evaluation.OutlierMask?.TryIsOutlier(column, row, out var outlier) == true
        && outlier;

    private static (string Name, bool Passed, string Evidence)
        VerifyStrictThreshold()
    {
        var values = Enumerable.Repeat(100d, 9).ToArray();
        values[4] = 120d;
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.strict-threshold",
            3,
            3,
            values);
        var evaluation = C3DRemoveOutlierPixelsRule.Evaluate(
            new C3DRemoveOutlierPixelsInput(
                "step.strict",
                source,
                "derived.strict",
                3,
                20d,
                3));
        return Check(
            "strict-threshold-retained",
            evaluation.OutlierMask?.OutlierCellCount == 0,
            $"removed={evaluation.OutlierMask?.OutlierCellCount}");
    }

    private static (string Name, bool Passed, string Evidence)
        VerifyInsufficientNeighbors()
    {
        var values = new[]
        {
            100d, double.NaN, double.NaN,
            double.NaN, 200d, double.NaN,
            double.NaN, double.NaN, 100d
        };
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.insufficient",
            3,
            3,
            values);
        var evaluation = C3DRemoveOutlierPixelsRule.Evaluate(
            new C3DRemoveOutlierPixelsInput(
                "step.insufficient",
                source,
                "derived.insufficient",
                3,
                20d,
                3));
        return Check(
            "insufficient-neighbors-retained",
            evaluation.OutlierMask?.OutlierCellCount == 0,
            $"removed={evaluation.OutlierMask?.OutlierCellCount}");
    }

    private static (string Name, bool Passed, string Evidence)
        VerifyInvalidThreshold()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.invalid-threshold",
            3,
            3,
            Enumerable.Repeat(100d, 9).ToArray());
        var evaluation = C3DRemoveOutlierPixelsRule.Evaluate(
            new C3DRemoveOutlierPixelsInput(
                "step.invalid",
                source,
                "derived.invalid",
                3,
                0d,
                3));
        return Check(
            "invalid-threshold-fails-closed",
            evaluation.Result.Status == ResultStatus.Error
            && evaluation.Output is null
            && evaluation.OutlierMask is null,
            $"status={evaluation.Result.Status};message={evaluation.Result.Message}");
    }

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Known Outlier Fixture",
            new ToolRecipeSource(
                Id: source.EntityId,
                Name: "Known Outlier Fixture",
                Format: "C3D",
                Unit: source.Unit,
                FrameId: source.FrameId,
                Path: sourcePath,
                ByteLength: source.ByteLength,
                ContentSha256: source.ContentSha256,
                GridWidth: source.Width,
                GridHeight: source.Height),
            [],
            [
                new ToolRecipeStep(
                    "step.remove-outliers.01",
                    "remove-outlier-pixels",
                    "Remove Outlier Pixels",
                    1,
                    [source.EntityId],
                    "derived.outlier-removed.01",
                    [
                        new("Rule", "LocalMedianAbsoluteDeviation"),
                        new("WindowSize", "3"),
                        new(
                            "MaximumAbsoluteDeviation",
                            20d.ToString("G17", CultureInfo.InvariantCulture)),
                        new("MinimumValidNeighbors", "3"),
                        new("MissingValuePolicy", "PreserveMask"),
                        new("BoundaryPolicy", "AvailableNeighbors"),
                        new("OutlierPolicy", "SetMissing")
                    ])
            ],
            []);

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}

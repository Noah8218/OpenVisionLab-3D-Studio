using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class ToolRecipeThresholdCorrectionRunnerVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory =
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var lines = new List<string> { "Threshold Correction Runner report verification" };
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
            var fixture = CreateFixture(directory);
            var runnerReportPath = Path.Combine(directory, "threshold-correction-runner.json");
            var firstExitCode = ToolRecipeThresholdCorrectionRunnerExecution.Run(
                fixture.RecipePath,
                fixture.CandidateId,
                runnerReportPath);
            var firstBytes = File.ReadAllBytes(runnerReportPath);
            var overwriteExitCode = ToolRecipeThresholdCorrectionRunnerExecution.Run(
                fixture.RecipePath,
                fixture.CandidateId,
                runnerReportPath);
            var overwriteBytes = File.ReadAllBytes(runnerReportPath);
            using var report = JsonDocument.Parse(overwriteBytes);
            var root = report.RootElement;
            var developmentSamples = root.GetProperty("developmentExecution").GetProperty("samples");
            var heldOutSamples = root.GetProperty("heldOutExecution").GetProperty("samples");
            var candidates = root.GetProperty("candidates").GetProperty("candidates");
            var evidence = root.GetProperty("evidence");
            var temporaryFilesRemain = Directory.EnumerateFiles(
                    directory,
                    "threshold-correction-runner.json.tmp.*")
                .Any();

            Check(
                "automatic correction preserves existing report contract",
                firstExitCode == 0
                && overwriteExitCode == 0
                && root.GetProperty("schemaVersion").GetString() == "1.0"
                && developmentSamples.GetArrayLength() == 4
                && heldOutSamples.GetArrayLength() == 1
                && candidates.GetArrayLength() > 0
                && evidence.GetProperty("heldOutSamples").GetArrayLength() == 1,
                $"firstExit={firstExitCode};overwriteExit={overwriteExitCode};development={developmentSamples.GetArrayLength()};heldOut={heldOutSamples.GetArrayLength()};candidates={candidates.GetArrayLength()}");
            Check(
                "overwrite is complete and UTF-8 has no BOM",
                firstBytes.Length > 0
                && overwriteBytes.Length > 0
                && root.ValueKind == JsonValueKind.Object
                && !HasUtf8Bom(overwriteBytes),
                $"firstBytes={firstBytes.Length};overwriteBytes={overwriteBytes.Length};bom={HasUtf8Bom(overwriteBytes)}");
            Check(
                "temporary report files are cleaned",
                !temporaryFilesRemain,
                $"temporaryFilesRemain={temporaryFilesRemain}");

            var lockedPath = Path.Combine(directory, "threshold-correction-locked.json");
            var lockedSentinel = Encoding.UTF8.GetBytes("pre-existing-locked-report");
            File.WriteAllBytes(lockedPath, lockedSentinel);
            var lockedExitCode = -1;
            using (var lockedStream = new FileStream(
                       lockedPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                lockedExitCode = ToolRecipeThresholdCorrectionRunnerExecution.Run(
                    fixture.RecipePath,
                    fixture.CandidateId,
                    lockedPath);
            }

            var invalidParent = Path.Combine(directory, "threshold-correction-parent-marker");
            File.WriteAllText(invalidParent, "parent-marker", new UTF8Encoding(false));
            var invalidParentReport = Path.Combine(invalidParent, "report.json");
            var invalidParentExitCode = ToolRecipeThresholdCorrectionRunnerExecution.Run(
                fixture.RecipePath,
                fixture.CandidateId,
                invalidParentReport);
            Check(
                "locked and invalid-parent failures preserve existing outputs",
                lockedExitCode == 1
                && File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel)
                && invalidParentExitCode == 1
                && File.ReadAllText(invalidParent) == "parent-marker"
                && !Directory.EnumerateFiles(directory, "threshold-correction-locked.json.tmp.*").Any()
                && !Directory.EnumerateFiles(directory, "threshold-correction-parent-marker.tmp.*").Any(),
                $"lockedExit={lockedExitCode};invalidParentExit={invalidParentExitCode};lockedPreserved={File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel)};invalidParentPreserved={File.ReadAllText(invalidParent) == "parent-marker"}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unhandled verification exception | {exception}");
        }

        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"Threshold Correction Runner verification: {passed}/{total} passed | {fullReportPath}");
        return total > 0 && passed == total ? 0 : 1;
    }

    private static Fixture CreateFixture(string directory)
    {
        var taughtPath = Path.Combine(directory, "threshold-correction-taught.C3D");
        var goodLowPath = Path.Combine(directory, "threshold-correction-good-low.C3D");
        var goodHighPath = Path.Combine(directory, "threshold-correction-good-high.C3D");
        var badLowPath = Path.Combine(directory, "threshold-correction-bad-low.C3D");
        var badHighPath = Path.Combine(directory, "threshold-correction-bad-high.C3D");
        var heldOutPath = Path.Combine(directory, "threshold-correction-heldout.C3D");
        CreateThicknessFixture(taughtPath, 3d);
        CreateThicknessFixture(goodLowPath, 2d);
        CreateThicknessFixture(goodHighPath, 4d);
        CreateThicknessFixture(badLowPath, -10d);
        CreateThicknessFixture(badHighPath, 20d);
        CreateThicknessFixture(heldOutPath, 3d);

        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(taughtPath);
        var sourceInfo = new FileInfo(taughtPath);
        var source = new ToolRecipeSource(
            "source.validation.threshold-runner",
            "Threshold Correction Runner source",
            "C3D",
            "model",
            "frame.c3d-grid-index",
            taughtPath,
            sourceInfo.Length,
            binding.ContentSha256,
            binding.GridWidth,
            binding.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.validation.threshold-runner.reference",
            "Reference ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(0, 0, 2, 4),
            null,
            null);
        var measurement = new ToolRecipeSelection(
            "selection.validation.threshold-runner.measurement",
            "Measurement ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(2, 0, 2, 4),
            null,
            null);
        var step = new ToolRecipeStep(
            "step.validation.threshold-runner.thickness",
            "thickness",
            "Thickness",
            3,
            [source.Id, reference.Id, measurement.Id],
            "result.validation.threshold-runner.thickness",
            [
                new ToolRecipeParameter("MinimumThickness", "0"),
                new ToolRecipeParameter("MaximumThickness", "10"),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Threshold Correction Runner fixture",
            source,
            [],
            [step],
            [reference, measurement]);
        var recipePath = Path.Combine(
            directory,
            "threshold-correction-runner-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, document);
        var samples = new[]
        {
            new ToolRecipeValidationSampleDefinition(1, goodLowPath, ToolRecipeValidationSampleRole.Good),
            new ToolRecipeValidationSampleDefinition(2, goodHighPath, ToolRecipeValidationSampleRole.Good),
            new ToolRecipeValidationSampleDefinition(3, badLowPath, ToolRecipeValidationSampleRole.Bad),
            new ToolRecipeValidationSampleDefinition(4, badHighPath, ToolRecipeValidationSampleRole.Bad),
            new ToolRecipeValidationSampleDefinition(5, heldOutPath, ToolRecipeValidationSampleRole.HeldOut)
        };
        ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
            recipePath,
            new ToolRecipeValidationSetDefinition(
                ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                document.Name,
                document.Source.ContentSha256!,
                samples));
        var result = ToolRecipeValidationSetExecution.Execute(
            document,
            samples.Select(sample => new ToolRecipeValidationSampleInput(sample.SourcePath, sample.Role)).ToArray());
        var candidates = ToolRecipeThresholdCandidateAnalyzer.Analyze(document, result).Candidates;
        var candidate = candidates.First(item =>
            ToolRecipeThresholdCandidateParameterMapper.TryCreateProposal(
                document,
                item,
                out _,
                out _));
        return new Fixture(recipePath, candidate.CandidateId);
    }

    private static void CreateThicknessFixture(string path, double thickness)
    {
        var values = new double[16];
        for (var row = 0; row < 4; row++)
        for (var column = 0; column < 4; column++)
        {
            var plane = 10d + (0.5d * row) + (0.25d * column);
            values[row * 4 + column] = row < 2 ? plane : plane + thickness;
        }

        C3DHeightFieldSnapshot.CreateForVerification(
            "source.validation.threshold-runner",
            4,
            4,
            values).SaveC3D(path);
    }

    private static bool HasUtf8Bom(byte[] bytes)
        => bytes.Length >= 3
        && bytes[0] == 0xEF
        && bytes[1] == 0xBB
        && bytes[2] == 0xBF;

    private sealed record Fixture(string RecipePath, string CandidateId);
}

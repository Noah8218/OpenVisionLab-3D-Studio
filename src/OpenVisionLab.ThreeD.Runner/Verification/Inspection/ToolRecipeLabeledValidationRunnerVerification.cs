using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

internal static class ToolRecipeLabeledValidationRunnerVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory =
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var lines = new List<string> { "Labeled Validation Runner report verification" };
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
            var runnerReportPath = Path.Combine(directory, "labeled-validation-runner.json");
            var firstExitCode = ToolRecipeLabeledValidationRunnerExecution.Run(
                fixture.RecipePath,
                runnerReportPath);
            var firstBytes = File.ReadAllBytes(runnerReportPath);
            var overwriteExitCode = ToolRecipeLabeledValidationRunnerExecution.Run(
                fixture.RecipePath,
                runnerReportPath);
            var overwriteBytes = File.ReadAllBytes(runnerReportPath);
            using var report = JsonDocument.Parse(overwriteBytes);
            var root = report.RootElement;
            var executionSamples = root.GetProperty("execution").GetProperty("samples");
            var evidence = root.GetProperty("evidence");
            var validationSet = root.GetProperty("validationSet");
            var temporaryFilesRemain = Directory.EnumerateFiles(
                    directory,
                    "labeled-validation-runner.json.tmp.*")
                .Any();

            Check(
                "success execution preserves existing report contract",
                firstExitCode == 0
                && overwriteExitCode == 0
                && root.GetProperty("schemaVersion").GetString() == "1.1"
                && validationSet.GetProperty("sampleCount").GetInt32() == 3
                && executionSamples.GetArrayLength() == 3
                && evidence.GetProperty("goodSampleCount").GetInt32() == 1
                && evidence.GetProperty("badSampleCount").GetInt32() == 1
                && evidence.GetProperty("heldOutSampleCount").GetInt32() == 1,
                $"firstExit={firstExitCode};overwriteExit={overwriteExitCode};samples={executionSamples.GetArrayLength()};good={evidence.GetProperty("goodSampleCount").GetInt32()};bad={evidence.GetProperty("badSampleCount").GetInt32()};heldOut={evidence.GetProperty("heldOutSampleCount").GetInt32()}");
            Check(
                "overwrite is complete and UTF-8 has no BOM",
                firstBytes.Length > 0
                && overwriteBytes.Length > 0
                && !HasUtf8Bom(overwriteBytes)
                && root.ValueKind == JsonValueKind.Object,
                $"firstBytes={firstBytes.Length};overwriteBytes={overwriteBytes.Length};bom={HasUtf8Bom(overwriteBytes)}");
            Check(
                "temporary report files are cleaned",
                !temporaryFilesRemain,
                $"temporaryFilesRemain={temporaryFilesRemain}");

            var lockedPath = Path.Combine(directory, "labeled-validation-locked.json");
            var lockedSentinel = Encoding.UTF8.GetBytes("pre-existing-locked-report");
            File.WriteAllBytes(lockedPath, lockedSentinel);
            var lockedExitCode = -1;
            using (var lockedStream = new FileStream(
                       lockedPath,
                       FileMode.Open,
                       FileAccess.Read,
                       FileShare.Read))
            {
                lockedExitCode = ToolRecipeLabeledValidationRunnerExecution.Run(
                    fixture.RecipePath,
                    lockedPath);
            }

            var invalidParent = Path.Combine(directory, "labeled-validation-parent-marker");
            File.WriteAllText(invalidParent, "parent-marker", new UTF8Encoding(false));
            var invalidParentReport = Path.Combine(invalidParent, "report.json");
            var invalidParentExitCode = ToolRecipeLabeledValidationRunnerExecution.Run(
                fixture.RecipePath,
                invalidParentReport);
            Check(
                "locked and invalid-parent failures preserve existing outputs",
                lockedExitCode == 1
                && File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel)
                && invalidParentExitCode == 1
                && File.ReadAllText(invalidParent) == "parent-marker"
                && !Directory.EnumerateFiles(directory, "labeled-validation-locked.json.tmp.*").Any()
                && !Directory.EnumerateFiles(directory, "labeled-validation-parent-marker.tmp.*").Any(),
                $"lockedExit={lockedExitCode};invalidParentExit={invalidParentExitCode};lockedPreserved={File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel)};invalidParentPreserved={File.ReadAllText(invalidParent) == "parent-marker"}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unhandled verification exception | {exception}");
        }

        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"Labeled Validation Runner verification: {passed}/{total} passed | {fullReportPath}");
        return total > 0 && passed == total ? 0 : 1;
    }

    private static Fixture CreateFixture(string directory)
    {
        var values = new double[]
        {
            10, 11, 12, 13,
            11, 12, 13, 14,
            12, 13, 14, 15,
            13, 14, 15, 16
        };
        var taughtPath = Path.Combine(directory, "labeled-validation-taught.C3D");
        var goodPath = Path.Combine(directory, "labeled-validation-good.C3D");
        var badPath = Path.Combine(directory, "labeled-validation-bad.C3D");
        var heldOutPath = Path.Combine(directory, "labeled-validation-heldout.C3D");
        foreach (var path in new[] { taughtPath, goodPath, badPath, heldOutPath })
        {
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.validation.runner",
                4,
                4,
                values).SaveC3D(path);
        }

        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(taughtPath);
        var sourceInfo = new FileInfo(taughtPath);
        var source = new ToolRecipeSource(
            "source.validation.runner",
            "Labeled Validation Runner source",
            "C3D",
            "model",
            "frame.c3d-grid-index",
            taughtPath,
            sourceInfo.Length,
            binding.ContentSha256,
            binding.GridWidth,
            binding.GridHeight);
        var reference = new ToolRecipeSelection(
            "selection.validation.runner.reference",
            "Reference ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(0, 0, 2, 4),
            null,
            null);
        var measurement = new ToolRecipeSelection(
            "selection.validation.runner.measurement",
            "Measurement ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.Id,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(2, 0, 2, 4),
            null,
            null);
        var step = new ToolRecipeStep(
            "step.validation.runner.thickness",
            "thickness",
            "Thickness",
            3,
            [source.Id, reference.Id, measurement.Id],
            "result.validation.runner.thickness",
            [
                new ToolRecipeParameter("MinimumThickness", "0"),
                new ToolRecipeParameter("MaximumThickness", "10"),
                new ToolRecipeParameter("MinimumValidSampleCount", "1")
            ]);
        var document = new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Labeled Validation Runner fixture",
            source,
            [],
            [step],
            [reference, measurement]);
        var recipePath = Path.Combine(
            directory,
            "labeled-validation-runner-fixture.ov3d-recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, document);
        ToolRecipeValidationSetDefinitionStore.SaveForRecipe(
            recipePath,
            new ToolRecipeValidationSetDefinition(
                ToolRecipeValidationSetDefinition.CurrentSchemaVersion,
                document.Name,
                document.Source.ContentSha256!,
                [
                    new ToolRecipeValidationSampleDefinition(1, goodPath, ToolRecipeValidationSampleRole.Good),
                    new ToolRecipeValidationSampleDefinition(2, badPath, ToolRecipeValidationSampleRole.Bad),
                    new ToolRecipeValidationSampleDefinition(3, heldOutPath, ToolRecipeValidationSampleRole.HeldOut)
                ]));
        return new Fixture(recipePath);
    }

    private static bool HasUtf8Bom(byte[] bytes)
        => bytes.Length >= 3
        && bytes[0] == 0xEF
        && bytes[1] == 0xBB
        && bytes[2] == 0xBF;

    private sealed record Fixture(string RecipePath);
}

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DMedianFilterGoldenVerification
{
    public static int Run(string reportPath)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"ovl3d-filter-golden-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);
        try
        {
            var cases = new[]
            {
                Check("constant-preserved", VerifyConstant),
                Check("isolated-spike-removed", VerifySpike),
                Check("missing-center-preserved", VerifyMissingCenter),
                Check("missing-neighbors-ignored", VerifyMissingNeighbors),
                Check("border-available-neighbors", VerifyBorder),
                Check("finite-zero-output-rejected", VerifyFiniteZeroOutput),
                Check("kernel-3-5-7", VerifyKernelChoices),
                Check("invalid-kernel-controlled", VerifyInvalidKernel),
                Check("all-missing-controlled", VerifyAllMissing),
                Check("deterministic-output-hash", VerifyDeterminism),
                Check("unknown-parameter-preserved", () => VerifyStrictParameters(tempDirectory)),
                Check("same-byte-source-identity", () => VerifySourceIdentity(tempDirectory)),
                Check("recipe-adapter-output-roundtrip-and-source-immutability", () => VerifyRecipeAdapter(tempDirectory)),
                Check("runner-report-atomicity", () => VerifyRunnerReportAtomicity(reportPath)),
                Check("runner-path-collision-admission", () => VerifyRunnerPathCollisions(reportPath))
            };

            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DMedianFilterGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                "Definition|raw-height-only|method=Median|kernels=3,5,7|missing=PreserveMask|boundary=AvailableNeighbors|roi=separate"
            };
            lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(GetReportDirectory(reportPath)!);
            File.WriteAllLines(reportPath, lines);
            Console.WriteLine($"C3D Median Filter golden verification: {status} ({passed}/{cases.Length})");
            return passed == cases.Length ? 0 : 5;
        }
        finally
        {
            Directory.Delete(tempDirectory, true);
        }
    }

    private static (bool Passed, string Evidence) VerifyConstant()
    {
        var evaluation = Evaluate(3, 3, Enumerable.Repeat(4.25, 9).ToArray(), 3);
        return (evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Output!.Values.Span.ToArray().All(value => value == 4.25),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifySpike()
    {
        var evaluation = Evaluate(3, 3, [1, 1, 1, 1, 100, 1, 1, 1, 1], 3);
        return (evaluation.Output is not null
            && evaluation.Output.Values.Span.ToArray().All(value => value == 1),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissingCenter()
    {
        var evaluation = Evaluate(3, 3, [1, 2, 3, 4, double.NaN, 6, 7, 8, 9], 3);
        return (evaluation.Output is not null
            && double.IsNaN(evaluation.Output.Values.Span[4])
            && evaluation.Output.MissingCount == 1,
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissingNeighbors()
    {
        var evaluation = Evaluate(3, 1, [1, double.NaN, 5], 3);
        var output = evaluation.Output!.Values.Span;
        return (output[0] == 1 && double.IsNaN(output[1]) && output[2] == 5, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyBorder()
    {
        var evaluation = Evaluate(2, 2, [1, 2, 3, 4], 3);
        return (evaluation.Output!.Values.Span.ToArray().All(value => value == 2.5), Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyFiniteZeroOutput()
    {
        var evaluation = Evaluate(2, 1, [-1, 1], 3);
        return (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Output is null
            && evaluation.Result.Message.Contains("finite zero", StringComparison.OrdinalIgnoreCase),
            Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyKernelChoices()
    {
        var source = Enumerable.Range(1, 49).Select(value => (double)value).ToArray();
        var hashes = new[] { 3, 5, 7 }
            .Select(kernel => Evaluate(7, 7, source, kernel))
            .Select(evaluation => evaluation.Output?.ContentSha256)
            .ToArray();
        return (hashes.All(hash => hash?.Length == 64), string.Join(",", hashes));
    }

    private static (bool Passed, string Evidence) VerifyInvalidKernel()
    {
        var evaluation = Evaluate(2, 2, [1, 2, 3, 4], 9);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyAllMissing()
    {
        var evaluation = Evaluate(2, 2, [double.NaN, double.NaN, double.NaN, double.NaN], 3);
        return (evaluation.Result.Status == ResultStatus.Error && evaluation.Output is null, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var values = new double[] { 1, 2, 9, 4, 5, 6, 7, 8, double.NaN };
        var first = Evaluate(3, 3, values, 5).Output!;
        var second = Evaluate(3, 3, values, 5).Output!;
        return (first.ContentSha256 == second.ContentSha256, $"first={first.ContentSha256},second={second.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyStrictParameters(string tempDirectory)
    {
        var document = CreateRecipe(tempDirectory);
        var invalid = document with
        {
            Steps = [document.Steps[0] with
            {
                Parameters = [.. document.Steps[0].Parameters, new ToolRecipeParameter("Sigma", "1")]
            }]
        };
        var result = ToolRecipeFilterExecution.Execute(invalid, invalid.Steps[0].Id, tempDirectory);
        return (result.Result.Status == ResultStatus.Pass && result.Output is not null, result.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifySourceIdentity(string tempDirectory)
    {
        var document = CreateRecipe(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, document.Source.Path);
        using (var stream = File.Open(sourcePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            stream.Position = stream.Length - 1;
            var original = stream.ReadByte();
            stream.Position = stream.Length - 1;
            stream.WriteByte((byte)(original ^ 0x01));
        }

        var result = ToolRecipeFilterExecution.Execute(document, document.Steps[0].Id, tempDirectory);
        return (result.Result.Status == ResultStatus.Error && result.Result.Message.Contains("identity", StringComparison.OrdinalIgnoreCase), result.Result.Message);
    }

    private static (bool Passed, string Evidence) VerifyRecipeAdapter(string tempDirectory)
    {
        var document = CreateRecipe(tempDirectory);
        var sourcePath = Path.Combine(tempDirectory, document.Source.Path);
        var sourceBytesBefore = File.ReadAllBytes(sourcePath);
        var sourceSha256Before = Convert.ToHexString(SHA256.HashData(sourceBytesBefore));
        var evaluation = ToolRecipeFilterExecution.Execute(document, document.Steps[0].Id, tempDirectory);
        if (evaluation.Output is null)
        {
            return (false, evaluation.Result.Message);
        }

        var outputPath = Path.Combine(tempDirectory, "output.c3d");
        evaluation.Output.SaveC3D(outputPath);
        var reloaded = C3DHeightFieldSnapshot.LoadVerified(
            outputPath,
            evaluation.Output.EntityId,
            evaluation.Output.Unit,
            evaluation.Output.FrameId,
            evaluation.Output.ByteLength,
            evaluation.Output.ContentSha256,
            evaluation.Output.Width,
            evaluation.Output.Height);
        var sourceBytesAfter = File.ReadAllBytes(sourcePath);
        var sourceSha256After = Convert.ToHexString(SHA256.HashData(sourceBytesAfter));
        var outputPathSeparate = !string.Equals(
            Path.GetFullPath(outputPath),
            Path.GetFullPath(sourcePath),
            StringComparison.OrdinalIgnoreCase);
        var passed = sourceBytesBefore.LongLength == sourceBytesAfter.LongLength
            && string.Equals(sourceSha256Before, sourceSha256After, StringComparison.Ordinal)
            && sourceBytesBefore.SequenceEqual(sourceBytesAfter)
            && evaluation.Output.RootSourceSha256 == sourceSha256Before
            && evaluation.Output.ContentSha256.Length == 64
            && evaluation.Output.IsDerived
            && !string.Equals(evaluation.Output.EntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
            && outputPathSeparate
            && reloaded.ContentSha256 == evaluation.Output.ContentSha256
            && reloaded.Values.Span.SequenceEqual(evaluation.Output.Values.Span);
        return (
            passed,
            $"sourcePath={sourcePath};sourceBefore={sourceSha256Before};sourceAfter={sourceSha256After};bytesBefore={sourceBytesBefore.LongLength};bytesAfter={sourceBytesAfter.LongLength};output={evaluation.Output.ContentSha256};outputEntity={evaluation.Output.EntityId};outputPath={outputPath};isDerived={evaluation.Output.IsDerived};root={evaluation.Output.RootSourceSha256};outputPathSeparate={outputPathSeparate}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerReportAtomicity(string reportPath)
    {
        var reportDirectory = GetReportDirectory(reportPath) ?? Environment.CurrentDirectory;
        var directory = Path.Combine(reportDirectory, $"atomic-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var document = CreateRecipe(directory);
            var recipePath = Path.Combine(directory, "filter.recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);
            var direct = ToolRecipeFilterExecution.Execute(document, document.Steps[0].Id, directory);
            if (direct.Result.Status != ResultStatus.Pass || direct.Output is null)
            {
                return (false, $"direct={direct.Result.Status}:{direct.Result.Message}");
            }

            var runnerReportPath = Path.Combine(directory, "runner-report.json");
            var firstOutputPath = Path.Combine(directory, "first-output.c3d");
            var secondOutputPath = Path.Combine(directory, "second-output.c3d");
            var firstExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                firstOutputPath,
                runnerReportPath);
            var firstBytes = File.Exists(runnerReportPath) ? File.ReadAllBytes(runnerReportPath) : [];
            var firstText = Encoding.UTF8.GetString(firstBytes);
            var firstOutputBytes = File.Exists(firstOutputPath) ? File.ReadAllBytes(firstOutputPath) : [];

            File.WriteAllText(runnerReportPath, "pre-existing-output", new UTF8Encoding(false));
            var overwriteExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                secondOutputPath,
                runnerReportPath);
            var overwriteBytes = File.Exists(runnerReportPath) ? File.ReadAllBytes(runnerReportPath) : [];
            var overwriteText = Encoding.UTF8.GetString(overwriteBytes);
            var overwriteOutputBytes = File.Exists(secondOutputPath) ? File.ReadAllBytes(secondOutputPath) : [];

            var lockedPath = Path.Combine(directory, "locked.json");
            var lockedSentinel = Encoding.UTF8.GetBytes("locked-output");
            File.WriteAllBytes(lockedPath, lockedSentinel);
            int lockedExit;
            using (var lockStream = new FileStream(
                       lockedPath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                lockedExit = ToolRecipeFilterRunnerExecution.Run(
                    recipePath,
                    document.Steps[0].Id,
                    Path.Combine(directory, "locked-output.c3d"),
                    lockedPath);
            }
            var lockedPreserved = File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel);

            var invalidParentMarker = Path.Combine(directory, "parent-file");
            File.WriteAllText(invalidParentMarker, "parent-file", new UTF8Encoding(false));
            var invalidParentExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                Path.Combine(directory, "invalid-parent-output.c3d"),
                Path.Combine(invalidParentMarker, "report.json"));
            var invalidParentPreserved = File.ReadAllText(invalidParentMarker) == "parent-file";
            var lockedArtifactPath = Path.Combine(directory, "locked-artifact.c3d");
            var lockedArtifactSentinel = Encoding.UTF8.GetBytes("locked-artifact");
            File.WriteAllBytes(lockedArtifactPath, lockedArtifactSentinel);
            int lockedArtifactExit;
            using (var lockStream = new FileStream(
                       lockedArtifactPath,
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                lockedArtifactExit = ToolRecipeFilterRunnerExecution.Run(
                    recipePath,
                    document.Steps[0].Id,
                    lockedArtifactPath,
                    Path.Combine(directory, "locked-artifact-report.json"));
            }
            var lockedArtifactPreserved = File.ReadAllBytes(lockedArtifactPath).SequenceEqual(lockedArtifactSentinel);

            var invalidArtifactParentMarker = Path.Combine(directory, "artifact-parent-file");
            File.WriteAllText(invalidArtifactParentMarker, "artifact-parent-file", new UTF8Encoding(false));
            var invalidArtifactExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                Path.Combine(invalidArtifactParentMarker, "output.c3d"),
                Path.Combine(directory, "invalid-artifact-report.txt"));
            var invalidArtifactParentPreserved = File.ReadAllText(invalidArtifactParentMarker) == "artifact-parent-file";
            var committedPair = VerifyRunnerCommittedPairFailureBoundaries(
                directory,
                recipePath,
                document);
            var temporaryFilesRemain = Directory.GetFiles(directory, "*.tmp.*").Length != 0;
            var noBom = !HasUtf8Bom(overwriteBytes);
            var sentinelAbsent = !overwriteText.Contains("pre-existing-output", StringComparison.Ordinal);
            var artifactStable = firstOutputBytes.Length > 0
                && overwriteOutputBytes.Length > 0
                && firstOutputBytes.SequenceEqual(overwriteOutputBytes);
            var reportShape = firstText.Contains(direct.Output.ContentSha256, StringComparison.Ordinal)
                && firstText.Contains("\"status\": \"Pass\"", StringComparison.Ordinal)
                && overwriteText.Contains(direct.Output.ContentSha256, StringComparison.Ordinal)
                && overwriteText.Contains("\"status\": \"Pass\"", StringComparison.Ordinal);
            var passed = firstExit == 0
                && overwriteExit == 0
                && firstBytes.Length > 0
                && overwriteBytes.Length > 0
                && artifactStable
                && reportShape
                && noBom
                && sentinelAbsent
                && lockedExit == 5
                && lockedPreserved
                && invalidParentExit == 5
                && invalidParentPreserved
                && lockedArtifactExit == 5
                && lockedArtifactPreserved
                && invalidArtifactExit == 5
                && invalidArtifactParentPreserved
                && committedPair.Passed
                && !temporaryFilesRemain;
            return (
                passed,
                $"firstExit={firstExit};overwriteExit={overwriteExit};bytes={firstBytes.Length}/{overwriteBytes.Length};artifactBytes={firstOutputBytes.Length}/{overwriteOutputBytes.Length};artifactStable={artifactStable};reportShape={reportShape};noBom={noBom};sentinelAbsent={sentinelAbsent};lockedExit={lockedExit};lockedPreserved={lockedPreserved};invalidParentExit={invalidParentExit};invalidParentPreserved={invalidParentPreserved};lockedArtifactExit={lockedArtifactExit};lockedArtifactPreserved={lockedArtifactPreserved};invalidArtifactExit={invalidArtifactExit};invalidArtifactParentPreserved={invalidArtifactParentPreserved};committedPair={Clean(committedPair.Evidence)};temporaryFiles={temporaryFilesRemain}");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static (bool Passed, string Evidence) VerifyRunnerPathCollisions(string reportPath)
    {
        var reportDirectory = GetReportDirectory(reportPath) ?? Environment.CurrentDirectory;
        var directory = Path.Combine(reportDirectory, $"path-collision-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var cases = new[]
            {
                Check("output-source", () => VerifyRunnerPathCollision(directory, "output-source")),
                Check("output-recipe", () => VerifyRunnerPathCollision(directory, "output-recipe")),
                Check("report-source", () => VerifyRunnerPathCollision(directory, "report-source")),
                Check("report-recipe", () => VerifyRunnerPathCollision(directory, "report-recipe")),
                Check("report-output", () => VerifyRunnerPathCollision(directory, "report-output")),
                Check("relative-source-alias", () => VerifyRunnerPathCollision(directory, "relative-source-alias")),
                Check("case-source-alias", () => VerifyRunnerPathCollision(directory, "case-source-alias"))
            };
            var passed = cases.Count(item => item.Passed);
            return (
                passed == cases.Length,
                string.Join('|', cases.Select(item => $"{item.Name}={(item.Passed ? "Pass" : "Fail")}:{Clean(item.Evidence)}")));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static (bool Passed, string Evidence) VerifyRunnerPathCollision(string rootDirectory, string caseName)
    {
        if (caseName == "case-source-alias" && !OperatingSystem.IsWindows())
        {
            return (true, "not-applicable-on-case-sensitive-platform");
        }

        var directory = Path.Combine(rootDirectory, caseName);
        Directory.CreateDirectory(directory);
        var document = CreateRecipe(directory);
        var recipePath = Path.Combine(directory, "filter.recipe.json");
        ToolRecipeDocumentStore.Save(recipePath, document);
        var sourcePath = Path.Combine(directory, document.Source.Path);
        var outputPath = Path.Combine(directory, "output.c3d");
        var reportPath = Path.Combine(directory, "report.json");
        switch (caseName)
        {
            case "output-source":
                outputPath = sourcePath;
                break;
            case "output-recipe":
                outputPath = recipePath;
                break;
            case "report-source":
                reportPath = sourcePath;
                break;
            case "report-recipe":
                reportPath = recipePath;
                break;
            case "report-output":
                reportPath = outputPath;
                break;
            case "relative-source-alias":
                outputPath = Path.Combine(directory, "nested", "..", Path.GetFileName(sourcePath));
                break;
            case "case-source-alias":
                outputPath = Path.Combine(directory, Path.GetFileName(sourcePath).ToUpperInvariant());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown Runner path-collision case.");
        }

        var sourceBefore = File.ReadAllBytes(sourcePath);
        var recipeBefore = File.ReadAllBytes(recipePath);
        var exitCode = ToolRecipeFilterRunnerExecution.Run(
            recipePath,
            document.Steps[0].Id,
            outputPath,
            reportPath);
        var sourcePreserved = File.ReadAllBytes(sourcePath).SequenceEqual(sourceBefore);
        var recipePreserved = File.ReadAllBytes(recipePath).SequenceEqual(recipeBefore);
        var onlyInputsRemain = Directory.GetFiles(directory).Select(Path.GetFileName).OrderBy(name => name)
            .SequenceEqual(
                new[] { Path.GetFileName(recipePath), Path.GetFileName(sourcePath) }.OrderBy(name => name),
                StringComparer.OrdinalIgnoreCase);
        return (
            exitCode == 5 && sourcePreserved && recipePreserved && onlyInputsRemain,
            $"exit={exitCode};sourcePreserved={sourcePreserved};recipePreserved={recipePreserved};onlyInputsRemain={onlyInputsRemain}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerCommittedPairFailureBoundaries(
        string parentDirectory,
        string recipePath,
        ToolRecipeDocument document)
    {
        var directory = Path.Combine(parentDirectory, "committed-pair");
        Directory.CreateDirectory(directory);
        var sourcePath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(recipePath) ?? Environment.CurrentDirectory,
            document.Source.Path));
        var sourceBefore = File.ReadAllBytes(sourcePath);
        var recipeBefore = File.ReadAllBytes(recipePath);
        try
        {
            var firstTempParent = Path.Combine(directory, "first-temp-parent");
            File.WriteAllText(firstTempParent, "first-temp-parent", new UTF8Encoding(false));
            var firstTempOutput = Path.Combine(firstTempParent, "output.c3d");
            var firstTempReport = Path.Combine(directory, "first-temp-report.json");
            var firstTempExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                firstTempOutput,
                firstTempReport);
            var firstTempPreserved = File.ReadAllText(firstTempParent) == "first-temp-parent"
                && !File.Exists(firstTempReport)
                && NoTemporaryFiles(directory);

            var firstReplacementOutput = Path.Combine(directory, "first-replacement-output.c3d");
            Directory.CreateDirectory(firstReplacementOutput);
            var firstReplacementReport = Path.Combine(directory, "first-replacement-report.json");
            var firstReplacementExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                firstReplacementOutput,
                firstReplacementReport);
            var firstReplacementPreserved = Directory.Exists(firstReplacementOutput)
                && !File.Exists(firstReplacementReport)
                && NoTemporaryFiles(directory);

            var secondWriteParent = Path.Combine(directory, "second-write-parent");
            File.WriteAllText(secondWriteParent, "second-write-parent", new UTF8Encoding(false));
            var secondWriteOutput = Path.Combine(directory, "second-write-output.c3d");
            var secondWriteReport = Path.Combine(secondWriteParent, "report.json");
            var secondWriteExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                secondWriteOutput,
                secondWriteReport);
            var secondWritePreserved = File.ReadAllText(secondWriteParent) == "second-write-parent"
                && !File.Exists(secondWriteOutput)
                && NoTemporaryFiles(directory);

            var pairOutput = Path.Combine(directory, "pair-output.c3d");
            var pairReport = Path.Combine(directory, "pair-report.json");
            var normalExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                pairOutput,
                pairReport);
            var normalPair = VerifyCommittedPair(pairOutput, pairReport);
            var previousOutputBytes = File.ReadAllBytes(pairOutput);
            File.Delete(pairReport);
            Directory.CreateDirectory(pairReport);
            var secondReplacementExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                pairOutput,
                pairReport);
            var secondReplacementPreserved = File.ReadAllBytes(pairOutput).SequenceEqual(previousOutputBytes)
                && Directory.Exists(pairReport)
                && NoTemporaryFiles(directory);
            Directory.Delete(pairReport, recursive: true);

            var retryExit = ToolRecipeFilterRunnerExecution.Run(
                recipePath,
                document.Steps[0].Id,
                pairOutput,
                pairReport);
            var retryPair = VerifyCommittedPair(pairOutput, pairReport);
            var sourcePreserved = File.ReadAllBytes(sourcePath).SequenceEqual(sourceBefore);
            var recipePreserved = File.ReadAllBytes(recipePath).SequenceEqual(recipeBefore);
            var temporaryFilesRemain = !NoTemporaryFiles(directory);
            var passed = firstTempExit == 5
                && firstTempPreserved
                && firstReplacementExit == 5
                && firstReplacementPreserved
                && secondWriteExit == 5
                && secondWritePreserved
                && normalExit == 0
                && normalPair.Passed
                && secondReplacementExit == 5
                && secondReplacementPreserved
                && retryExit == 0
                && retryPair.Passed
                && sourcePreserved
                && recipePreserved
                && !temporaryFilesRemain;
            return (
                passed,
                $"firstTempExit={firstTempExit};firstTempPreserved={firstTempPreserved};firstReplacementExit={firstReplacementExit};firstReplacementPreserved={firstReplacementPreserved};secondWriteExit={secondWriteExit};secondWritePreserved={secondWritePreserved};normalExit={normalExit};normalPair={Clean(normalPair.Evidence)};secondReplacementExit={secondReplacementExit};secondReplacementPreserved={secondReplacementPreserved};retryExit={retryExit};retryPair={Clean(retryPair.Evidence)};sourcePreserved={sourcePreserved};recipePreserved={recipePreserved};temporaryFiles={temporaryFilesRemain}");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static (bool Passed, string Evidence) VerifyCommittedPair(
        string outputPath,
        string reportPath)
    {
        if (!File.Exists(outputPath) || !File.Exists(reportPath))
        {
            return (false, "output-or-report-missing");
        }

        var outputSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(outputPath)));
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath, Encoding.UTF8));
        var publication = report.RootElement.GetProperty("publication");
        var state = publication.GetProperty("state").GetString();
        var publishedOutput = publication.GetProperty("output");
        var publishedOutputPath = publishedOutput.GetProperty("path").GetString();
        var publishedOutputSha256 = publishedOutput.GetProperty("contentSha256").GetString();
        var publishedReportPath = publication.GetProperty("report").GetProperty("path").GetString();
        var reportSha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(reportPath)));
        var pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var passed = string.Equals(state, "Committed", StringComparison.Ordinal)
            && pathComparer.Equals(Path.GetFullPath(outputPath), Path.GetFullPath(publishedOutputPath ?? string.Empty))
            && string.Equals(outputSha256, publishedOutputSha256, StringComparison.OrdinalIgnoreCase)
            && pathComparer.Equals(Path.GetFullPath(reportPath), Path.GetFullPath(publishedReportPath ?? string.Empty));
        return (
            passed,
            $"state={state};outputSha={outputSha256};publishedOutputSha={publishedOutputSha256};reportSha={reportSha256};outputRef={publishedOutputPath};reportRef={publishedReportPath}");
    }

    private static bool NoTemporaryFiles(string directory) =>
        !Directory.EnumerateFiles(directory, "*.tmp.*", SearchOption.AllDirectories).Any();

    private static string? GetReportDirectory(string reportPath) => Path.GetDirectoryName(Path.GetFullPath(reportPath));

    private static bool HasUtf8Bom(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });

    private static ToolRecipeDocument CreateRecipe(string tempDirectory)
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.synthetic",
            3,
            3,
            [1, 1, 1, 1, 9, 1, 1, 1, 1]);
        var fileName = $"source-{Guid.NewGuid():N}.c3d";
        source.SaveC3D(Path.Combine(tempDirectory, fileName));
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Filter Golden",
            new ToolRecipeSource(
                source.EntityId,
                "Synthetic",
                "C3D",
                source.Unit,
                source.FrameId,
                fileName,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [new ToolRecipeStep(
                "step.filter.01",
                "filter",
                "Filter",
                1,
                [source.EntityId],
                "derived.filtered.01",
                [
                    new("Method", "Median"),
                    new("KernelSize", "3"),
                    new("MissingValuePolicy", "PreserveMask"),
                    new("BoundaryPolicy", "AvailableNeighbors")
                ])],
            []);
    }

    private static C3DMedianFilterEvaluation Evaluate(int width, int height, IReadOnlyList<double> values, int kernel) =>
        C3DMedianFilterRule.Evaluate(new C3DMedianFilterInput(
            "step.filter.01",
            C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", width, height, values),
            "derived.filtered.01",
            kernel));

    private static VerificationCase Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return new VerificationCase(name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return new VerificationCase(name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Evidence(C3DMedianFilterEvaluation evaluation) =>
        $"status={evaluation.Result.Status},valid={evaluation.Output?.ValidCount},missing={evaluation.Output?.MissingCount},hash={evaluation.Output?.ContentSha256},message={evaluation.Result.Message}";

    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

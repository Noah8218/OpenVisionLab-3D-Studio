using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightThresholdBackgroundRemovalGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(
            reportDirectory,
            $"height-threshold-background-removal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var cases = new[]
            {
                Check("inclusive-above-and-below-preserve-boundary-and-missing", VerifyInclusiveModes),
                Check("all-removed-returns-controlled-warning", VerifyAllRemovedWarning),
                Check("deterministic-output-and-evidence-identity", VerifyDeterminism),
                Check("invalid-input-and-cancellation-fail-closed", VerifyGuards),
                Check("runner-replay-and-source-identity-parity", () => VerifyRunnerParity(fixtureDirectory)),
                Check("runner-report-atomicity", () => VerifyRunnerReportAtomicity(fixtureDirectory))
            };

            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DHeightThresholdBackgroundRemovalGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                "Definition|source=raw-height-only|comparison=inclusive-finite-sample-predicate|missing=preserve-existing-missing|background=fail-predicate-to-missing|output=same-grid-derived|physical-calibration=not-claimed"
            };
            lines.AddRange(cases.Select(item =>
                $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(reportDirectory);
            File.WriteAllLines(fullReportPath, lines);
            Console.WriteLine(
                $"C3D Height-Threshold Background Removal golden verification: {status} ({passed}/{cases.Length})");
            return passed == cases.Length ? 0 : 5;
        }
        finally
        {
            if (Directory.Exists(fixtureDirectory))
            {
                Directory.Delete(fixtureDirectory, true);
            }
        }
    }

    private static (bool Passed, string Evidence) VerifyInclusiveModes()
    {
        var source = CreateFixture("source.height-threshold.inclusive");
        var sourceBefore = source.Values.ToArray();
        var above = Evaluate(
            source,
            "derived.height-threshold.above",
            3d,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold);
        var below = Evaluate(
            source,
            "derived.height-threshold.below",
            3d,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrBelowThreshold);

        var expectedAbove = new double[]
        {
            double.NaN, double.NaN, 3d, 5d,
            double.NaN, 4d, 6d, double.NaN,
            double.NaN, double.NaN, 7d, 8d
        };
        var expectedBelow = new double[]
        {
            double.NaN, 1d, 3d, double.NaN,
            2d, double.NaN, double.NaN, double.NaN,
            -1d, 0.5d, double.NaN, double.NaN
        };
        var passed = IsPass(above)
            && IsPass(below)
            && above.Output is not null
            && below.Output is not null
            && above.Evidence is not null
            && below.Evidence is not null
            && above.Evidence.Mode == C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold
            && below.Evidence.Mode == C3DHeightThresholdBackgroundRemovalMode.KeepAtOrBelowThreshold
            && above.Evidence.InputValidSampleCount == 10
            && above.Evidence.InputMissingSampleCount == 2
            && above.Evidence.RetainedValidSampleCount == 6
            && above.Evidence.RemovedBackgroundSampleCount == 4
            && below.Evidence.RetainedValidSampleCount == 5
            && below.Evidence.RemovedBackgroundSampleCount == 5
            && above.Output.ValidCount == 6
            && above.Output.MissingCount == 6
            && below.Output.ValidCount == 5
            && below.Output.MissingCount == 7
            && SameValues(above.Output.Values.Span, expectedAbove)
            && SameValues(below.Output.Values.Span, expectedBelow)
            && above.Output.Width == source.Width
            && above.Output.Height == source.Height
            && above.Output.Unit == source.Unit
            && above.Output.FrameId == source.FrameId
            && above.Output.RootSourceSha256 == source.RootSourceSha256
            && below.Output.RootSourceSha256 == source.RootSourceSha256
            && source.Values.Span.SequenceEqual(sourceBefore);
        return (
            passed,
            $"above={Evidence(above)};below={Evidence(below)};sourceUnchanged={source.Values.Span.SequenceEqual(sourceBefore)}");
    }

    private static (bool Passed, string Evidence) VerifyAllRemovedWarning()
    {
        var evaluation = Evaluate(
            CreateFixture("source.height-threshold.warning"),
            "derived.height-threshold.warning",
            100d,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold);
        var passed = evaluation.Result.Status == ResultStatus.Warning
            && evaluation.Output is not null
            && evaluation.Evidence is not null
            && !evaluation.Evidence.HasForeground
            && evaluation.Output.ValidCount == 0
            && evaluation.Output.MissingCount == 12
            && evaluation.Evidence.RemovedBackgroundSampleCount == 10;
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var first = Evaluate(
            CreateFixture("source.height-threshold.determinism"),
            "derived.height-threshold.determinism",
            3d,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold);
        var second = Evaluate(
            CreateFixture("source.height-threshold.determinism"),
            "derived.height-threshold.determinism",
            3d,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold);
        var passed = IsPass(first)
            && IsPass(second)
            && first.Output?.ContentSha256 == second.Output?.ContentSha256
            && first.Evidence?.ContentSha256 == second.Evidence?.ContentSha256
            && first.Output?.Provenance == second.Output?.Provenance;
        return (
            passed,
            $"outputFirst={first.Output?.ContentSha256};outputSecond={second.Output?.ContentSha256};evidenceFirst={first.Evidence?.ContentSha256};evidenceSecond={second.Evidence?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyGuards()
    {
        var source = CreateFixture("source.height-threshold.guards");
        var invalidThreshold = Evaluate(
            source,
            "derived.height-threshold.invalid-threshold",
            double.NaN,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold);
        var invalidMode = Evaluate(
            source,
            "derived.height-threshold.invalid-mode",
            3d,
            (C3DHeightThresholdBackgroundRemovalMode)99);
        var wrongUnit = Evaluate(
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.height-threshold.wrong-unit",
                source.Width,
                source.Height,
                source.Values.ToArray(),
                "millimetre",
                source.FrameId),
            "derived.height-threshold.wrong-unit",
            3d,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold);
        var allMissing = Evaluate(
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.height-threshold.all-missing",
                2,
                2,
                [double.NaN, double.NaN, double.NaN, double.NaN]),
            "derived.height-threshold.all-missing",
            3d,
            C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = C3DHeightThresholdBackgroundRemovalRule.Evaluate(
                new C3DHeightThresholdBackgroundRemovalInput(
                    "step.height-threshold.cancel",
                    source,
                    "derived.height-threshold.cancel",
                    3d,
                    C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold),
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        var passed = invalidThreshold.Result.Status == ResultStatus.Error
            && invalidMode.Result.Status == ResultStatus.Error
            && wrongUnit.Result.Status == ResultStatus.Error
            && allMissing.Result.Status == ResultStatus.Error
            && invalidThreshold.Output is null
            && invalidMode.Output is null
            && wrongUnit.Output is null
            && allMissing.Output is null
            && canceled;
        return (
            passed,
            $"threshold={invalidThreshold.Result.Status};mode={invalidMode.Result.Status};unit={wrongUnit.Result.Status};allMissing={allMissing.Result.Status};canceled={canceled}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerParity(string fixtureDirectory)
    {
        var source = CreateFixture("source.height-threshold.runner");
        var direct = C3DHeightThresholdBackgroundRemovalRule.Evaluate(
            new C3DHeightThresholdBackgroundRemovalInput(
                "step.height-threshold.runner",
                source,
                "derived.height-threshold.runner",
                3d,
                C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold));
        if (!IsPass(direct) || direct.Output is null || direct.Evidence is null)
        {
            return (false, $"direct={Evidence(direct)}");
        }

        var sourcePath = Path.Combine(fixtureDirectory, "source.c3d");
        var outputPath = Path.Combine(fixtureDirectory, "output.c3d");
        var specificationPath = Path.Combine(fixtureDirectory, "height-threshold.json");
        var runnerReportPath = Path.Combine(fixtureDirectory, "runner-report.json");
        source.SaveC3D(sourcePath);
        var sourceBytesBefore = File.ReadAllBytes(sourcePath);
        var specification = new C3DHeightThresholdBackgroundRemovalRunnerSpecification
        {
            StepId = "step.height-threshold.runner",
            SourcePath = sourcePath,
            SourceEntityId = source.EntityId,
            SourceUnit = source.Unit,
            SourceFrameId = source.FrameId,
            SourceByteLength = source.ByteLength,
            SourceContentSha256 = source.ContentSha256,
            SourceWidth = source.Width,
            SourceHeight = source.Height,
            OutputEntityId = direct.Output.EntityId,
            OutputPath = outputPath,
            Threshold = 3d,
            Mode = C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold.ToString()
        };
        File.WriteAllText(
            specificationPath,
            JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }));

        var runnerExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(
            specificationPath,
            runnerReportPath);
        var sourceBytesAfter = File.ReadAllBytes(sourcePath);
        var reloaded = File.Exists(outputPath)
            ? C3DHeightFieldSnapshot.LoadVerified(
                outputPath,
                direct.Output.EntityId,
                direct.Output.Unit,
                direct.Output.FrameId,
                direct.Output.ByteLength,
                direct.Output.ContentSha256,
                direct.Output.Width,
                direct.Output.Height)
            : null;
        var report = File.Exists(runnerReportPath)
            ? JsonDocument.Parse(File.ReadAllText(runnerReportPath))
            : null;
        var outputHash = report?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
        var evidenceHash = report?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
        var sourceMutation = report?.RootElement.GetProperty("sourceMutation").GetBoolean();
        var rootSourceHash = report?.RootElement.GetProperty("output").GetProperty("rootSourceSha256").GetString();
        var parity = runnerExit == 0
            && reloaded is not null
            && report is not null
            && outputHash == direct.Output.ContentSha256
            && evidenceHash == direct.Evidence.ContentSha256
            && rootSourceHash == source.RootSourceSha256
            && sourceMutation == false
            && SameValues(reloaded.Values.Span, direct.Output.Values.ToArray())
            && sourceBytesBefore.SequenceEqual(sourceBytesAfter);

        var invalidIdentitySpecification = new C3DHeightThresholdBackgroundRemovalRunnerSpecification
        {
            StepId = specification.StepId,
            SourcePath = specification.SourcePath,
            SourceEntityId = specification.SourceEntityId,
            SourceUnit = specification.SourceUnit,
            SourceFrameId = specification.SourceFrameId,
            SourceByteLength = specification.SourceByteLength,
            SourceContentSha256 = new string('0', 64),
            SourceWidth = specification.SourceWidth,
            SourceHeight = specification.SourceHeight,
            OutputEntityId = specification.OutputEntityId,
            OutputPath = Path.Combine(fixtureDirectory, "identity-rejected.c3d"),
            Threshold = specification.Threshold,
            Mode = specification.Mode
        };
        var invalidIdentityPath = Path.Combine(fixtureDirectory, "identity-rejected.json");
        var invalidIdentityReportPath = Path.Combine(fixtureDirectory, "identity-rejected-report.txt");
        File.WriteAllText(
            invalidIdentityPath,
            JsonSerializer.Serialize(invalidIdentitySpecification, new JsonSerializerOptions { WriteIndented = true }));
        var invalidIdentityExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(
            invalidIdentityPath,
            invalidIdentityReportPath);
        var invalidIdentityReport = File.ReadAllText(invalidIdentityReportPath);
        var identityRejected = invalidIdentityExit == 5
            && invalidIdentityReport.Contains("identity", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(invalidIdentitySpecification.OutputPath);
        report?.Dispose();
        return (
            parity && identityRejected,
            $"runnerExit={runnerExit};outputHash={outputHash};evidenceHash={evidenceHash};rootSource={rootSourceHash};sourceMutation={sourceMutation};sourceUnchanged={sourceBytesBefore.SequenceEqual(sourceBytesAfter)};identityExit={invalidIdentityExit};identityRejected={identityRejected}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerReportAtomicity(string fixtureDirectory)
    {
        var directory = Path.Combine(fixtureDirectory, $"atomic-report-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var source = CreateFixture("source.height-threshold.atomic");
            var direct = C3DHeightThresholdBackgroundRemovalRule.Evaluate(
                new C3DHeightThresholdBackgroundRemovalInput(
                    "step.height-threshold.atomic",
                    source,
                    "derived.height-threshold.atomic",
                    3d,
                    C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold));
            if (!IsPass(direct) || direct.Output is null || direct.Evidence is null)
            {
                return (false, $"direct={Evidence(direct)}");
            }

            var sourcePath = Path.Combine(directory, "source.c3d");
            var outputPath = Path.Combine(directory, "output.c3d");
            var specificationPath = Path.Combine(directory, "height-threshold.json");
            source.SaveC3D(sourcePath);
            var specification = new C3DHeightThresholdBackgroundRemovalRunnerSpecification
            {
                StepId = "step.height-threshold.atomic",
                SourcePath = sourcePath,
                SourceEntityId = source.EntityId,
                SourceUnit = source.Unit,
                SourceFrameId = source.FrameId,
                SourceByteLength = source.ByteLength,
                SourceContentSha256 = source.ContentSha256,
                SourceWidth = source.Width,
                SourceHeight = source.Height,
                OutputEntityId = direct.Output.EntityId,
                OutputPath = outputPath,
                Threshold = 3d,
                Mode = C3DHeightThresholdBackgroundRemovalMode.KeepAtOrAboveThreshold.ToString()
            };
            File.WriteAllText(
                specificationPath,
                JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

            var reportOutputPath = Path.Combine(directory, "runner.json");
            var firstExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(specificationPath, reportOutputPath);
            var firstBytes = File.Exists(reportOutputPath)
                ? File.ReadAllBytes(reportOutputPath)
                : [];
            using var firstDocument = File.Exists(reportOutputPath)
                ? JsonDocument.Parse(firstBytes)
                : null;
            var firstOutputHash = firstDocument?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
            var firstEvidenceHash = firstDocument?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
            var firstOutputBytes = File.Exists(outputPath)
                ? File.ReadAllBytes(outputPath)
                : [];
            File.WriteAllText(reportOutputPath, "pre-existing-output", new UTF8Encoding(false));
            var overwriteExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(specificationPath, reportOutputPath);
            var overwriteBytes = File.Exists(reportOutputPath)
                ? File.ReadAllBytes(reportOutputPath)
                : [];
            using var overwriteDocument = File.Exists(reportOutputPath)
                ? JsonDocument.Parse(overwriteBytes)
                : null;
            var overwriteOutputHash = overwriteDocument?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
            var overwriteEvidenceHash = overwriteDocument?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
            var overwriteOutputBytes = File.Exists(outputPath)
                ? File.ReadAllBytes(outputPath)
                : [];

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
                lockedExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(specificationPath, lockedPath);
            }
            var lockedPreserved = File.ReadAllBytes(lockedPath).SequenceEqual(lockedSentinel);

            var invalidParentMarker = Path.Combine(directory, "parent-file");
            File.WriteAllText(invalidParentMarker, "parent-file", new UTF8Encoding(false));
            var invalidParentExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(
                specificationPath,
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
                specification.OutputPath = lockedArtifactPath;
                File.WriteAllText(
                    specificationPath,
                    JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                lockedArtifactExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(
                    specificationPath,
                    Path.Combine(directory, "locked-artifact-report.json"));
            }
            var lockedArtifactPreserved = File.ReadAllBytes(lockedArtifactPath).SequenceEqual(lockedArtifactSentinel);

            var invalidArtifactParentMarker = Path.Combine(directory, "artifact-parent-file");
            File.WriteAllText(invalidArtifactParentMarker, "artifact-parent-file", new UTF8Encoding(false));
            specification.OutputPath = Path.Combine(invalidArtifactParentMarker, "output.c3d");
            File.WriteAllText(
                specificationPath,
                JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            var invalidArtifactExit = C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(
                specificationPath,
                Path.Combine(directory, "invalid-artifact-report.txt"));
            var invalidArtifactParentPreserved = File.ReadAllText(invalidArtifactParentMarker) == "artifact-parent-file";
            var temporaryFilesRemain = Directory.GetFiles(directory, "*.tmp.*").Length != 0;
            var noBom = !HasUtf8Bom(overwriteBytes);
            var sentinelAbsent = !Encoding.UTF8.GetString(overwriteBytes).Contains("pre-existing-output", StringComparison.Ordinal);
            var artifactStable = firstOutputBytes.Length > 0
                && overwriteOutputBytes.Length > 0
                && firstOutputBytes.SequenceEqual(overwriteOutputBytes);
            var passed = firstExit == 0
                && overwriteExit == 0
                && firstBytes.Length > 0
                && overwriteBytes.Length > 0
                && artifactStable
                && firstOutputHash == direct.Output.ContentSha256
                && firstEvidenceHash == direct.Evidence.ContentSha256
                && overwriteOutputHash == firstOutputHash
                && overwriteEvidenceHash == firstEvidenceHash
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
                && !temporaryFilesRemain;
            return (
                passed,
                $"firstExit={firstExit};overwriteExit={overwriteExit};bytes={firstBytes.Length}/{overwriteBytes.Length};artifactBytes={firstOutputBytes.Length}/{overwriteOutputBytes.Length};artifactStable={artifactStable};hashes={firstOutputHash}/{overwriteOutputHash};evidence={firstEvidenceHash}/{overwriteEvidenceHash};noBom={noBom};sentinelAbsent={sentinelAbsent};lockedExit={lockedExit};lockedPreserved={lockedPreserved};invalidParentExit={invalidParentExit};invalidParentPreserved={invalidParentPreserved};lockedArtifactExit={lockedArtifactExit};lockedArtifactPreserved={lockedArtifactPreserved};invalidArtifactExit={invalidArtifactExit};invalidArtifactParentPreserved={invalidArtifactParentPreserved};temporaryFiles={temporaryFilesRemain}");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    private static bool HasUtf8Bom(byte[] bytes) => bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF });

    private static C3DHeightThresholdBackgroundRemovalEvaluation Evaluate(
        C3DHeightFieldSnapshot source,
        string outputEntityId,
        double threshold,
        C3DHeightThresholdBackgroundRemovalMode mode) =>
        C3DHeightThresholdBackgroundRemovalRule.Evaluate(
            new C3DHeightThresholdBackgroundRemovalInput(
                "step.height-threshold.01",
                source,
                outputEntityId,
                threshold,
                mode));

    private static C3DHeightFieldSnapshot CreateFixture(string entityId) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            entityId,
            4,
            3,
            [double.NaN, 1d, 3d, 5d, 2d, 4d, 6d, double.NaN, -1d, 0.5d, 7d, 8d],
            "raw-height",
            "frame.c3d-grid-index");

    private static bool IsPass(C3DHeightThresholdBackgroundRemovalEvaluation evaluation) =>
        evaluation.Result.Status == ResultStatus.Pass
        && evaluation.Output is not null
        && evaluation.Evidence is not null;

    private static bool SameValues(ReadOnlySpan<double> actual, IReadOnlyList<double> expected)
    {
        if (actual.Length != expected.Count)
        {
            return false;
        }

        for (var index = 0; index < actual.Length; index++)
        {
            if (double.IsNaN(actual[index]) || double.IsNaN(expected[index]))
            {
                if (!double.IsNaN(actual[index]) || !double.IsNaN(expected[index]))
                {
                    return false;
                }
            }
            else if (actual[index] != expected[index])
            {
                return false;
            }
        }

        return true;
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return (name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return (name, false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Evidence(C3DHeightThresholdBackgroundRemovalEvaluation evaluation) =>
        $"status={evaluation.Result.Status};valid={evaluation.Output?.ValidCount};missing={evaluation.Output?.MissingCount};retained={evaluation.Evidence?.RetainedValidSampleCount};removed={evaluation.Evidence?.RemovedBackgroundSampleCount};output={evaluation.Output?.ContentSha256};evidence={evaluation.Evidence?.ContentSha256};message={evaluation.Result.Message}";

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}

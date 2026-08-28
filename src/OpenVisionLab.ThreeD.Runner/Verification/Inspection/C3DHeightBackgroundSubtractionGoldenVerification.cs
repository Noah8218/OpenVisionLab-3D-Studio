using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DHeightBackgroundSubtractionGoldenVerification
{
    public static int Run(string reportPath)
    {
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureDirectory = Path.Combine(
            reportDirectory,
            $"height-background-subtraction-{Guid.NewGuid():N}");
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            var cases = new[]
            {
                Check("signed-delta-and-missing-pair-preservation", VerifySignedDeltaAndMissing),
                Check("deterministic-output-and-evidence-identity", VerifyDeterminism),
                Check("identity-grid-unit-and-zero-guards", VerifyGuards),
                Check("runner-replay-and-background-identity-parity", () => VerifyRunnerParity(fixtureDirectory)),
                Check("cancellation-fails-closed", VerifyCancellation)
            };
            var passed = cases.Count(item => item.Passed);
            var status = passed == cases.Length ? "Pass" : "Fail";
            var lines = new List<string>
            {
                $"C3DHeightBackgroundSubtractionGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
                $"Definition|subtraction={C3DHeightBackgroundSubtractionEvidence.SubtractionPolicyName}|grid={C3DHeightBackgroundSubtractionEvidence.GridPolicyName}|missing={C3DHeightBackgroundSubtractionEvidence.MissingValuePolicyName}|zero={C3DHeightBackgroundSubtractionEvidence.ZeroDeltaPolicyName}|output=separate-derived-c3d|physical-calibration=not-claimed"
            };
            lines.AddRange(cases.Select(item =>
                $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
            Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
            File.WriteAllLines(fullReportPath, lines);
            Console.WriteLine(
                $"C3D Saved-Background Subtraction golden verification: {status} ({passed}/{cases.Length})");
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

    private static (bool Passed, string Evidence) VerifySignedDeltaAndMissing()
    {
        var current = CreateCurrent("current.background.signed");
        var background = CreateBackground("background.saved.signed");
        var currentBefore = current.Values.ToArray();
        var backgroundBefore = background.Values.ToArray();
        var evaluation = Evaluate(current, background, "delta.background.signed");
        var expected = new[] { 3d, 3d, double.NaN, double.NaN, 3d, -1d };
        var passed = IsPass(evaluation)
            && evaluation.Evidence!.SubtractionPolicy == C3DHeightBackgroundSubtractionEvidence.SubtractionPolicyName
            && evaluation.Evidence.GridPolicy == C3DHeightBackgroundSubtractionEvidence.GridPolicyName
            && evaluation.Evidence.MissingValuePolicy == C3DHeightBackgroundSubtractionEvidence.MissingValuePolicyName
            && evaluation.Evidence.ZeroDeltaPolicy == C3DHeightBackgroundSubtractionEvidence.ZeroDeltaPolicyName
            && evaluation.Evidence.CurrentSourceEntityId == current.EntityId
            && evaluation.Evidence.CurrentSourceContentSha256 == current.ContentSha256
            && evaluation.Evidence.BackgroundEntityId == background.EntityId
            && evaluation.Evidence.BackgroundContentSha256 == background.ContentSha256
            && evaluation.Evidence.CurrentValidSampleCount == 5
            && evaluation.Evidence.BackgroundValidSampleCount == 5
            && evaluation.Evidence.PairedValidSampleCount == 4
            && evaluation.Evidence.MissingEitherSampleCount == 2
            && evaluation.Evidence.PositiveDeltaSampleCount == 3
            && evaluation.Evidence.NegativeDeltaSampleCount == 1
            && evaluation.Evidence.ZeroDeltaSampleCount == 0
            && evaluation.Output!.RootSourceSha256 == current.RootSourceSha256
            && evaluation.Output.Width == current.Width
            && evaluation.Output.Height == current.Height
            && evaluation.Output.Unit == current.Unit
            && evaluation.Output.FrameId == current.FrameId
            && SameValues(evaluation.Output.Values.Span, expected)
            && SameValues(current.Values.Span, currentBefore)
            && SameValues(background.Values.Span, backgroundBefore);
        return (
            passed,
            $"status={evaluation.Result.Status};current={current.ContentSha256};background={background.ContentSha256};output={evaluation.Output?.ContentSha256};evidence={evaluation.Evidence?.ContentSha256};sourceUnchanged={SameValues(current.Values.Span, currentBefore)};backgroundUnchanged={SameValues(background.Values.Span, backgroundBefore)}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminism()
    {
        var first = Evaluate(
            CreateCurrent("current.background.determinism"),
            CreateBackground("background.saved.determinism"),
            "delta.background.determinism");
        var second = Evaluate(
            CreateCurrent("current.background.determinism"),
            CreateBackground("background.saved.determinism"),
            "delta.background.determinism");
        var passed = IsPass(first)
            && IsPass(second)
            && first.Output!.ContentSha256 == second.Output!.ContentSha256
            && first.Evidence!.ContentSha256 == second.Evidence!.ContentSha256
            && first.Output.Provenance == second.Output.Provenance
            && first.Evidence.BackgroundContentSha256 == second.Evidence.BackgroundContentSha256;
        return (
            passed,
            $"outputFirst={first.Output?.ContentSha256};outputSecond={second.Output?.ContentSha256};evidenceFirst={first.Evidence?.ContentSha256};evidenceSecond={second.Evidence?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyGuards()
    {
        var current = CreateCurrent("current.background.guards");
        var background = CreateBackground("background.saved.guards");
        var wrongDimensions = Evaluate(
            current,
            C3DHeightFieldSnapshot.CreateForVerification(
                "background.saved.wrong-dimensions",
                1,
                6,
                new[] { 2d, 1d, 4d, 9d, 5d, 4d }),
            "delta.background.wrong-dimensions");
        var wrongUnit = Evaluate(
            current,
            C3DHeightFieldSnapshot.CreateForVerification(
                "background.saved.wrong-unit",
                3,
                2,
                new[] { 2d, 1d, 4d, 9d, 5d, 4d },
                "millimetre",
                current.FrameId),
            "delta.background.wrong-unit");
        var wrongFrame = Evaluate(
            current,
            C3DHeightFieldSnapshot.CreateForVerification(
                "background.saved.wrong-frame",
                3,
                2,
                new[] { 2d, 1d, 4d, 9d, 5d, 4d },
                current.Unit,
                "other-frame"),
            "delta.background.wrong-frame");
        var noPairs = Evaluate(
            C3DHeightFieldSnapshot.CreateForVerification(
                "current.background.empty",
                3,
                2,
                new[] { double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN }),
            background,
            "delta.background.empty");
        var zeroDelta = Evaluate(
            C3DHeightFieldSnapshot.CreateForVerification(
                "current.background.zero",
                3,
                2,
                new[] { 2d, 4d, 6d, 8d, 10d, 12d }),
            C3DHeightFieldSnapshot.CreateForVerification(
                "background.saved.zero",
                3,
                2,
                new[] { 2d, 1d, 5d, 8d, 9d, 11d }),
            "delta.background.zero");
        var outputCollision = Evaluate(current, background, current.EntityId);
        var passed = wrongDimensions.Result.Status == ResultStatus.Error
            && wrongDimensions.Output is null
            && wrongUnit.Result.Status == ResultStatus.Error
            && wrongUnit.Output is null
            && wrongFrame.Result.Status == ResultStatus.Error
            && wrongFrame.Output is null
            && noPairs.Result.Status == ResultStatus.Error
            && noPairs.Output is null
            && zeroDelta.Result.Status == ResultStatus.Error
            && zeroDelta.Output is null
            && zeroDelta.Result.Message.Contains("zero", StringComparison.OrdinalIgnoreCase)
            && outputCollision.Result.Status == ResultStatus.Error
            && outputCollision.Output is null;
        return (
            passed,
            $"dimensions={wrongDimensions.Result.Status};unit={wrongUnit.Result.Status};frame={wrongFrame.Result.Status};noPairs={noPairs.Result.Status};zero={zeroDelta.Result.Status};collision={outputCollision.Result.Status}");
    }

    private static (bool Passed, string Evidence) VerifyRunnerParity(string fixtureDirectory)
    {
        var current = CreateCurrent("current.background.runner");
        var background = CreateBackground("background.saved.runner");
        var direct = Evaluate(current, background, "delta.background.runner", "step.background.runner");
        if (!IsPass(direct) || direct.Output is null || direct.Evidence is null)
        {
            return (false, $"direct={Evidence(direct)}");
        }

        var currentPath = Path.Combine(fixtureDirectory, "current.c3d");
        var backgroundPath = Path.Combine(fixtureDirectory, "background.c3d");
        var outputPath = Path.Combine(fixtureDirectory, "delta.c3d");
        var specificationPath = Path.Combine(fixtureDirectory, "background-subtraction.json");
        var runnerReportPath = Path.Combine(fixtureDirectory, "runner-report.json");
        current.SaveC3D(currentPath);
        background.SaveC3D(backgroundPath);
        var currentBytesBefore = File.ReadAllBytes(currentPath);
        var backgroundBytesBefore = File.ReadAllBytes(backgroundPath);
        var specification = new C3DHeightBackgroundSubtractionRunnerSpecification
        {
            StepId = "step.background.runner",
            CurrentPath = currentPath,
            CurrentEntityId = current.EntityId,
            CurrentUnit = current.Unit,
            CurrentFrameId = current.FrameId,
            CurrentByteLength = current.ByteLength,
            CurrentContentSha256 = current.ContentSha256,
            CurrentWidth = current.Width,
            CurrentHeight = current.Height,
            BackgroundPath = backgroundPath,
            BackgroundEntityId = background.EntityId,
            BackgroundUnit = background.Unit,
            BackgroundFrameId = background.FrameId,
            BackgroundByteLength = background.ByteLength,
            BackgroundContentSha256 = background.ContentSha256,
            BackgroundWidth = background.Width,
            BackgroundHeight = background.Height,
            OutputEntityId = direct.Output.EntityId,
            OutputPath = outputPath
        };
        File.WriteAllText(
            specificationPath,
            JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }));

        var runnerExit = C3DHeightBackgroundSubtractionRunnerExecution.Run(
            specificationPath,
            runnerReportPath);
        var currentBytesAfter = File.ReadAllBytes(currentPath);
        var backgroundBytesAfter = File.ReadAllBytes(backgroundPath);
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
        using var report = File.Exists(runnerReportPath)
            ? JsonDocument.Parse(File.ReadAllText(runnerReportPath))
            : null;
        var outputHash = report?.RootElement.GetProperty("output").GetProperty("contentSha256").GetString();
        var evidenceHash = report?.RootElement.GetProperty("evidence").GetProperty("contentSha256").GetString();
        var currentMutation = report?.RootElement.GetProperty("currentMutation").GetBoolean();
        var backgroundMutation = report?.RootElement.GetProperty("backgroundMutation").GetBoolean();
        var backgroundHash = report?.RootElement.GetProperty("savedBackground").GetProperty("contentSha256").GetString();
        var parity = runnerExit == 0
            && reloaded is not null
            && report is not null
            && outputHash == direct.Output.ContentSha256
            && evidenceHash == direct.Evidence.ContentSha256
            && backgroundHash == background.ContentSha256
            && currentMutation == false
            && backgroundMutation == false
            && SameValues(reloaded.Values.Span, direct.Output.Values.ToArray())
            && currentBytesBefore.SequenceEqual(currentBytesAfter)
            && backgroundBytesBefore.SequenceEqual(backgroundBytesAfter);

        var invalidIdentitySpecification = new C3DHeightBackgroundSubtractionRunnerSpecification
        {
            StepId = specification.StepId,
            CurrentPath = specification.CurrentPath,
            CurrentEntityId = specification.CurrentEntityId,
            CurrentUnit = specification.CurrentUnit,
            CurrentFrameId = specification.CurrentFrameId,
            CurrentByteLength = specification.CurrentByteLength,
            CurrentContentSha256 = specification.CurrentContentSha256,
            CurrentWidth = specification.CurrentWidth,
            CurrentHeight = specification.CurrentHeight,
            BackgroundPath = specification.BackgroundPath,
            BackgroundEntityId = specification.BackgroundEntityId,
            BackgroundUnit = specification.BackgroundUnit,
            BackgroundFrameId = specification.BackgroundFrameId,
            BackgroundByteLength = specification.BackgroundByteLength,
            BackgroundContentSha256 = new string('0', 64),
            BackgroundWidth = specification.BackgroundWidth,
            BackgroundHeight = specification.BackgroundHeight,
            OutputEntityId = "delta.background.identity-rejected",
            OutputPath = Path.Combine(fixtureDirectory, "identity-rejected.c3d")
        };
        var invalidIdentityPath = Path.Combine(fixtureDirectory, "identity-rejected.json");
        var invalidIdentityReportPath = Path.Combine(fixtureDirectory, "identity-rejected-report.txt");
        File.WriteAllText(
            invalidIdentityPath,
            JsonSerializer.Serialize(invalidIdentitySpecification, new JsonSerializerOptions { WriteIndented = true }));
        var invalidIdentityExit = C3DHeightBackgroundSubtractionRunnerExecution.Run(
            invalidIdentityPath,
            invalidIdentityReportPath);
        var identityRejected = invalidIdentityExit == 5
            && File.ReadAllText(invalidIdentityReportPath).Contains("identity", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(invalidIdentitySpecification.OutputPath);
        return (
            parity && identityRejected,
            $"runnerExit={runnerExit};outputHash={outputHash};evidenceHash={evidenceHash};backgroundHash={backgroundHash};currentMutation={currentMutation};backgroundMutation={backgroundMutation};inputUnchanged={currentBytesBefore.SequenceEqual(currentBytesAfter) && backgroundBytesBefore.SequenceEqual(backgroundBytesAfter)};identityExit={invalidIdentityExit};identityRejected={identityRejected}");
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = Evaluate(
                CreateCurrent("current.background.cancel"),
                CreateBackground("background.saved.cancel"),
                "step.background.cancel",
                "delta.background.cancel",
                cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }

        return (canceled, $"canceled={canceled}");
    }

    private static C3DHeightBackgroundSubtractionEvaluation Evaluate(
        C3DHeightFieldSnapshot current,
        C3DHeightFieldSnapshot background,
        string outputEntityId,
        string stepId = "step.background.01",
        CancellationToken cancellationToken = default) =>
        C3DHeightBackgroundSubtractionRule.Evaluate(
            new C3DHeightBackgroundSubtractionInput(
                stepId,
                current,
                background,
                outputEntityId),
            cancellationToken);

    private static C3DHeightFieldSnapshot CreateCurrent(string entityId) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            entityId,
            3,
            2,
            new[] { 5d, 4d, double.NaN, -2d, 8d, 3d },
            "raw-height",
            "frame.c3d-grid-index");

    private static C3DHeightFieldSnapshot CreateBackground(string entityId) =>
        C3DHeightFieldSnapshot.CreateForVerification(
            entityId,
            3,
            2,
            new[] { 2d, 1d, 4d, double.NaN, 5d, 4d },
            "raw-height",
            "frame.c3d-grid-index");

    private static bool IsPass(C3DHeightBackgroundSubtractionEvaluation evaluation) =>
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

    private static string Evidence(C3DHeightBackgroundSubtractionEvaluation evaluation) =>
        $"status={evaluation.Result.Status};paired={evaluation.Evidence?.PairedValidSampleCount};missing={evaluation.Evidence?.MissingEitherSampleCount};positive={evaluation.Evidence?.PositiveDeltaSampleCount};negative={evaluation.Evidence?.NegativeDeltaSampleCount};output={evaluation.Output?.ContentSha256};evidence={evaluation.Evidence?.ContentSha256};message={evaluation.Result.Message}";

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}

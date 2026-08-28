using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DRigidPointPairAlignmentGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("known-rigid-pose", VerifyKnownPose),
            Check("deterministic-hash-and-runner-parity", () => VerifyDeterminismAndRunnerParity(reportPath)),
            Check("invalid-identities-and-policies-fail-closed", VerifyInvalidIdentityAndPolicy),
            Check("degenerate-and-distance-mismatch-fail-closed", VerifyDegenerateAndDistanceMismatch),
            Check("cancellation-propagates", VerifyCancellation)
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath,
        [
            $"C3DRigidPointPairAlignmentGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|input=three ordered source/reference full-XYZ point pairs|output=RigidPointPairAlignmentArtifact|policy=ExactlyThreeOrdered|bestFit=excluded|cloudApply=excluded",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
        ]);
        Console.WriteLine($"3D Rigid Point Pair Alignment golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyKnownPose()
    {
        var evaluation = C3DRigidPointPairAlignmentAdapter.Evaluate(KnownInput());
        var output = evaluation.Output;
        var pose = output?.Pose;
        var pass = evaluation.Result.Status == ResultStatus.Pass
            && output is not null
            && pose is not null
            && pose.Value.IsRigid(1e-10)
            && Nearly(pose.Value.M11, 0d)
            && Nearly(pose.Value.M12, -1d)
            && Nearly(pose.Value.M21, 1d)
            && Nearly(pose.Value.M22, 0d)
            && Nearly(pose.Value.M33, 1d)
            && Nearly(pose.Value.TranslationX, 10d)
            && Nearly(pose.Value.TranslationY, -4d)
            && Nearly(pose.Value.TranslationZ, 2d)
            && output.Pairs.Count == 3
            && output.Residuals.Count == 3
            && output.MaximumResidual <= 1e-12;
        var poseText = pose is null
            ? string.Empty
            : string.Join(',', pose.Value.Values.Select(value => value.ToString("G8")));
        return (pass, $"status={evaluation.Result.Status};pose={poseText};maxResidual={output?.MaximumResidual:G8};sha256={output?.ContentSha256}");
    }

    private static (bool Passed, string Evidence) VerifyDeterminismAndRunnerParity(string reportPath)
    {
        var input = KnownInput();
        var first = C3DRigidPointPairAlignmentAdapter.Evaluate(input);
        var second = C3DRigidPointPairAlignmentAdapter.Evaluate(input);
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var specificationPath = Path.Combine(directory, "rigid-point-pair-alignment-spec.json");
        var runnerReportPath = Path.Combine(directory, "rigid-point-pair-alignment-runner.txt");
        var specification = new C3DRigidPointPairAlignmentRunnerSpecification
        {
            StepId = input.StepId,
            OutputEntityId = input.OutputEntityId,
            SourceEntityId = input.SourceEntityId,
            SourceContentSha256 = input.SourceContentSha256,
            ReferenceEntityId = input.ReferenceEntityId,
            ReferenceContentSha256 = input.ReferenceContentSha256,
            SourceUnit = input.SourceUnit,
            SourceFrameId = input.SourceFrameId,
            ReferenceUnit = input.ReferenceUnit,
            ReferenceFrameId = input.ReferenceFrameId,
            MaximumPairLengthError = input.MaximumPairLengthError,
            MinimumNormalizedCrossMagnitude = input.MinimumNormalizedCrossMagnitude,
            Pairs = input.Pairs.Select(pair => new C3DRigidPointPairAlignmentRunnerPair
            {
                SourcePointId = pair.SourcePointId,
                ReferencePointId = pair.ReferencePointId,
                SourceX = pair.SourceX,
                SourceY = pair.SourceY,
                SourceZ = pair.SourceZ,
                ReferenceX = pair.ReferenceX,
                ReferenceY = pair.ReferenceY,
                ReferenceZ = pair.ReferenceZ
            }).ToList()
        };
        File.WriteAllText(specificationPath, JsonSerializer.Serialize(specification, new JsonSerializerOptions { WriteIndented = true }));
        var exitCode = C3DRigidPointPairAlignmentRunnerExecution.Run(specificationPath, runnerReportPath);
        var runnerText = File.ReadAllText(runnerReportPath);
        var alignmentLine = runnerText
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => value.StartsWith("Alignment|", StringComparison.Ordinal));
        var runnerHash = alignmentLine?
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => value.StartsWith("sha256=", StringComparison.Ordinal))?
            .Substring("sha256=".Length);
        var pass = first.Output is not null
            && second.Output is not null
            && first.Output.ContentSha256 == second.Output.ContentSha256
            && exitCode == 0
            && runnerHash == first.Output.ContentSha256
            && runnerText.Contains("policy=ExactlyThreeOrdered", StringComparison.Ordinal);
        return (pass, $"direct1={first.Output?.ContentSha256};direct2={second.Output?.ContentSha256};runner={runnerHash};exit={exitCode}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidIdentityAndPolicy()
    {
        var sameSource = KnownInput() with
        {
            ReferenceEntityId = KnownInput().SourceEntityId,
            ReferenceContentSha256 = KnownInput().SourceContentSha256
        };
        var mismatchedUnit = KnownInput() with { ReferenceUnit = "um" };
        var invalidTolerance = KnownInput() with { MaximumPairLengthError = -1d };
        var duplicatePoint = KnownInput() with
        {
            Pairs = KnownInput().Pairs.Select((pair, index) => index == 1
                ? pair with { SourcePointId = "source.p0" }
                : pair).ToArray()
        };
        var one = C3DRigidPointPairAlignmentAdapter.Evaluate(sameSource);
        var two = C3DRigidPointPairAlignmentAdapter.Evaluate(mismatchedUnit);
        var three = C3DRigidPointPairAlignmentAdapter.Evaluate(invalidTolerance);
        var four = C3DRigidPointPairAlignmentAdapter.Evaluate(duplicatePoint);
        var pass = one.Result.Status == ResultStatus.Error && one.Output is null
            && two.Result.Status == ResultStatus.Error && two.Output is null
            && three.Result.Status == ResultStatus.Error && three.Output is null
            && four.Result.Status == ResultStatus.Error && four.Output is null;
        return (pass, $"same={one.Result.Message};unit={two.Result.Message};tolerance={three.Result.Message};duplicate={four.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyDegenerateAndDistanceMismatch()
    {
        var collinear = KnownInput() with
        {
            Pairs = new[]
            {
                KnownInput().Pairs[0],
                KnownInput().Pairs[1] with { SourceY = 0d },
                KnownInput().Pairs[2] with { SourceX = 2d, SourceY = 0d }
            }
        };
        var mismatch = KnownInput() with
        {
            Pairs = KnownInput().Pairs.Select((pair, index) => index == 1
                ? pair with { ReferenceX = 11d }
                : pair).ToArray()
        };
        var one = C3DRigidPointPairAlignmentAdapter.Evaluate(collinear);
        var two = C3DRigidPointPairAlignmentAdapter.Evaluate(mismatch);
        var pass = one.Result.Status == ResultStatus.Error
            && one.Output is null
            && one.Result.Message.Contains("collinear", StringComparison.OrdinalIgnoreCase)
            && two.Result.Status == ResultStatus.Error
            && two.Output is null
            && two.Result.Message.Contains("lengths differ", StringComparison.OrdinalIgnoreCase);
        return (pass, $"collinear={one.Result.Message};mismatch={two.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = C3DRigidPointPairAlignmentAdapter.Evaluate(KnownInput(), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        return (canceled, $"canceled={canceled}");
    }

    private static C3DRigidPointPairAlignmentInput KnownInput() => new(
        "step.rigid-point-pair.01",
        "derived.rigid-point-pair.01",
        "source.manual-alignment",
        new string('A', 64),
        "reference.fixture",
        new string('B', 64),
        "mm",
        "frame.source",
        "mm",
        "frame.reference",
        [
            new("source.p0", "reference.p0", 0d, 0d, 0d, 10d, -4d, 2d),
            new("source.p1", "reference.p1", 1d, 0d, 0d, 10d, -3d, 2d),
            new("source.p2", "reference.p2", 0d, 1d, 0d, 9d, -4d, 2d)
        ],
        1e-9,
        1e-12);

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

    private static bool Nearly(double actual, double expected) => Math.Abs(actual - expected) <= 1e-10;

    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
}

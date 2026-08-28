using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DConstrainedBestFitRigidAlignmentGoldenVerification
{
    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            VerifyKnownNoisyPose(),
            VerifyDeterminismAndRunnerParity(reportPath),
            VerifyNegativeInput(),
            VerifyCancellation()
        };
        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "PASS" : "FAIL";
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath,
        [
            $"C3DConstrainedBestFitRigidAlignmentGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|input=4-64 ordered source/reference full-XYZ pairs|output=ConstrainedBestFitRigidAlignmentArtifact|pose=proper-rigid-no-scale-no-shear-no-reflection|outlierRejection=excluded|cloudApply=excluded",
            .. cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}")
        ]);
        Console.WriteLine($"3D Constrained Best-Fit Rigid Alignment golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Name, string Evidence) VerifyKnownNoisyPose()
    {
        var evaluation = C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(KnownInput());
        var output = evaluation.Output;
        var pose = output?.Pose;
        var pass = evaluation.Result.Status == ResultStatus.Pass
            && output is not null
            && output.Pairs.Count == 6
            && output.UsedAllCorrespondences
            && output.Residuals.Count == 6
            && output.ArithmeticResidualWarningExceeded
            && pose is not null
            && pose.Value.IsRigid(1e-8)
            && Math.Abs(pose.Value.M12 + 1d) <= 0.02d
            && Math.Abs(pose.Value.M21 - 1d) <= 0.02d
            && Math.Abs(pose.Value.TranslationX - 10d) <= 0.05d
            && Math.Abs(pose.Value.TranslationY + 4d) <= 0.05d
            && Math.Abs(pose.Value.TranslationZ - 2d) <= 0.05d;
        return (pass, "known-noisy-pose", Evidence(evaluation));
    }

    private static (bool Passed, string Name, string Evidence) VerifyDeterminismAndRunnerParity(string reportPath)
    {
        var input = KnownInput();
        var first = C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(input);
        var second = C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(input);
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var specificationPath = Path.Combine(directory, "constrained-best-fit-rigid-alignment-spec.json");
        var runnerReportPath = Path.Combine(directory, "constrained-best-fit-rigid-alignment-runner.txt");
        var specification = new C3DConstrainedBestFitRigidAlignmentRunnerSpecification
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
            MaximumCorrespondenceCount = input.MaximumCorrespondenceCount,
            MinimumNormalizedLineSpread = input.MinimumNormalizedLineSpread,
            ArithmeticResidualWarning = input.ArithmeticResidualWarning,
            Pairs = input.Pairs.Select(pair => new C3DConstrainedBestFitRigidAlignmentRunnerPair
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
        var exitCode = C3DConstrainedBestFitRigidAlignmentRunnerExecution.Run(specificationPath, runnerReportPath);
        var runnerText = File.ReadAllText(runnerReportPath);
        var alignmentLine = runnerText
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => value.StartsWith("Alignment|", StringComparison.Ordinal));
        var runnerHash = alignmentLine?
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => value.StartsWith("sha256=", StringComparison.Ordinal))?
            .Substring("sha256=".Length);
        var pass = first.Result.Status == ResultStatus.Pass
            && second.Result.Status == ResultStatus.Pass
            && first.Output is not null
            && second.Output is not null
            && first.Output.ContentSha256 == second.Output.ContentSha256
            && exitCode == 0
            && string.Equals(first.Output.ContentSha256, runnerHash, StringComparison.OrdinalIgnoreCase);
        return (pass, "determinism-and-runner-parity", $"direct1={first.Output?.ContentSha256};direct2={second.Output?.ContentSha256};runner={runnerHash};exit={exitCode}");
    }

    private static (bool Passed, string Name, string Evidence) VerifyNegativeInput()
    {
        var known = KnownInput();
        var unitMismatch = known with { SourceUnit = "inch" };
        var belowMinimum = known with { Pairs = known.Pairs.Take(3).ToArray() };
        var capExceeded = known with { MaximumCorrespondenceCount = 4 };
        var duplicate = known with
        {
            Pairs = known.Pairs.Select((pair, index) => index == 1
                ? pair with { SourcePointId = known.Pairs[0].SourcePointId }
                : pair).ToArray()
        };
        var collinear = known with
        {
            Pairs = known.Pairs.Take(4).Select((pair, index) => pair with
            {
                SourceX = index,
                SourceY = 0d,
                SourceZ = 0d,
                ReferenceX = 10d + index,
                ReferenceY = -4d,
                ReferenceZ = 2d
            }).ToArray()
        };
        var nonFinite = known with
        {
            Pairs = known.Pairs.Select((pair, index) => index == 2
                ? pair with { ReferenceZ = double.NaN }
                : pair).ToArray()
        };
        var evaluations = new[]
        {
            C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(unitMismatch),
            C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(belowMinimum),
            C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(capExceeded),
            C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(duplicate),
            C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(collinear),
            C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(nonFinite)
        };
        var pass = evaluations.All(item => item.Result.Status == ResultStatus.Error && item.Output is null);
        return (pass, "negative-fail-closed", string.Join(';', evaluations.Select(item => $"{item.Result.Status}:{Clean(item.Result.Message)}")));
    }

    private static (bool Passed, string Name, string Evidence) VerifyCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var canceled = false;
        try
        {
            _ = C3DConstrainedBestFitRigidAlignmentAdapter.Evaluate(KnownInput(), cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        return (canceled, "cancellation", $"canceled={canceled}");
    }

    private static C3DConstrainedBestFitRigidAlignmentInput KnownInput() => new(
        "step.constrained-best-fit-rigid.01",
        "derived.constrained-best-fit-rigid.01",
        "source.best-fit-noisy",
        new string('A', 64),
        "reference.best-fit-nominal",
        new string('B', 64),
        "mm",
        "frame.source",
        "mm",
        "frame.reference",
        [
            new("source.p0", "reference.p0", 0d, 0d, 0d, 10d, -4d, 2d),
            new("source.p1", "reference.p1", 1d, 0d, 0d, 10d, -3d, 2d),
            new("source.p2", "reference.p2", 0d, 2d, 0d, 8d, -4d, 2d),
            new("source.p3", "reference.p3", 0d, 0d, 3d, 10d, -4d, 5d),
            new("source.p4", "reference.p4", 2d, 1d, 1d, 9.02d, -2.01d, 3.03d),
            new("source.p5", "reference.p5", -1d, 1d, 2d, 9d, -5d, 4d)
        ],
        64,
        1e-12,
        0.001);

    private static string Evidence(C3DConstrainedBestFitRigidAlignmentEvaluation evaluation)
        => $"status={evaluation.Result.Status};message={Clean(evaluation.Result.Message)};hash={evaluation.Output?.ContentSha256};pairs={evaluation.Output?.Pairs.Count};rms={evaluation.Output?.RmsResidual:R};max={evaluation.Output?.MaximumResidual:R};warning={evaluation.Output?.ArithmeticResidualWarningExceeded}";

    private static string Clean(string value)
        => value.Replace('\r', ' ').Replace('\n', ' ').Replace('|', '/');
}

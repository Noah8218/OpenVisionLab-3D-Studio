using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class RegistrationAcceptanceGoldenVerification
{
    private static readonly RegistrationAcceptancePolicy Policy = new(
        "model",
        MinimumCorrespondenceCount: 500,
        MinimumFitness: 0.5,
        MaximumInlierRmse: 0.02,
        MaximumTranslation: 2.0,
        MaximumRotationDegrees: 30.0,
        RigidTransformTolerance: 1e-9);

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("accepted-known-transform", () => VerifyAccepted(Evaluate(transform: Transform(20.0, 1.0, 0.0, 0.0)))),
            Check("exact-thresholds-pass", () => VerifyAccepted(Evaluate(
                correspondenceCount: 500,
                fitness: 0.5,
                inlierRmse: 0.02,
                transform: Transform(30.0, 2.0, 0.0, 0.0)))),
            Check("metric-order-and-recording", VerifyMetricOrderAndRecording),
            Check("zero-correspondence-rmse-zero-rejected", VerifyZeroCorrespondenceFalseSuccess),
            Check("minimum-correspondence-before-fitness", () => VerifyOutcome(
                Evaluate(correspondenceCount: 499, fitness: 1.0, inlierRmse: 0.0),
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.InsufficientCorrespondences,
                ("Correspondence count", ResultStatus.Fail),
                ("Fitness", ResultStatus.NotRun),
                ("Inlier RMSE", ResultStatus.NotRun))),
            Check("fitness-before-rmse", () => VerifyOutcome(
                Evaluate(fitness: 0.499, inlierRmse: 0.0),
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.InsufficientFitness,
                ("Correspondence count", ResultStatus.Pass),
                ("Fitness", ResultStatus.Fail),
                ("Inlier RMSE", ResultStatus.NotRun))),
            Check("rmse-after-guards", () => VerifyOutcome(
                Evaluate(inlierRmse: 0.0201),
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.ExcessiveInlierRmse,
                ("Correspondence count", ResultStatus.Pass),
                ("Fitness", ResultStatus.Pass),
                ("Inlier RMSE", ResultStatus.Fail),
                ("Homogeneous row max error", ResultStatus.NotRun))),
            Check("nonhomogeneous-transform-rejected", () => VerifyNonRigid(Modify(Transform(), 12, 0.01))),
            Check("scaled-transform-rejected", () => VerifyNonRigid(
                [2.0, 0.0, 0.0, 0.0, 0.0, 2.0, 0.0, 0.0, 0.0, 0.0, 2.0, 0.0, 0.0, 0.0, 0.0, 1.0])),
            Check("reflected-transform-rejected", () => VerifyNonRigid(
                [-1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0, 0.0, 0.0, 0.0, 0.0, 1.0])),
            Check("translation-limit-rejected", () => VerifyOutcome(
                Evaluate(transform: Transform(0.0, 2.001, 0.0, 0.0)),
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.TranslationLimitExceeded,
                ("Translation magnitude", ResultStatus.Fail),
                ("Rotation angle", ResultStatus.NotRun))),
            Check("rotation-limit-rejected", () => VerifyOutcome(
                Evaluate(transform: Transform(30.01, 0.0, 0.0, 0.0)),
                ResultStatus.Fail,
                RegistrationAcceptanceDecision.RotationLimitExceeded,
                ("Translation magnitude", ResultStatus.Pass),
                ("Rotation angle", ResultStatus.Fail))),
            Check("nonfinite-transform-rejected", VerifyInvalidTransformNumbers),
            Check("wrong-transform-size-rejected", () => VerifyOutcome(
                Evaluate(transform: [1.0, 0.0, 0.0, 0.0]),
                ResultStatus.Error,
                RegistrationAcceptanceDecision.InvalidEvidence,
                ("Inlier RMSE", ResultStatus.Pass),
                ("Rotation determinant", ResultStatus.Error))),
            Check("invalid-point-count-rejected", VerifyInvalidPointCounts),
            Check("correspondence-range-rejected", () => VerifyOutcome(
                Evaluate(sourcePointCount: 999, correspondenceCount: 1000),
                ResultStatus.Error,
                RegistrationAcceptanceDecision.InvalidEvidence,
                ("Correspondence count", ResultStatus.Error),
                ("Inlier RMSE", ResultStatus.Error))),
            Check("nonfinite-fitness-rejected", () => VerifyOutcome(
                Evaluate(fitness: double.NaN),
                ResultStatus.Error,
                RegistrationAcceptanceDecision.InvalidEvidence,
                ("Fitness", ResultStatus.Error))),
            Check("invalid-rmse-rejected", VerifyInvalidRmse),
            Check("unit-mismatch-rejected", () => VerifyOutcome(
                Evaluate(unit: "mm"),
                ResultStatus.Error,
                RegistrationAcceptanceDecision.InvalidEvidence,
                ("Correspondence count", ResultStatus.Error))),
            Check("invalid-policy-guards-rejected", VerifyInvalidPolicyGuards)
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"RegistrationAcceptanceGoldenVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
            $"Contract|order={RegistrationAcceptanceRule.EvaluationOrder}|transform=row-major-float64-4x4|unit=explicit|thresholds=scenario-specific",
            "FalseSuccessGuard|zeroCorrespondences=True|rmseZeroAccepted=False|fitnessCheckedBeforeRmse=True"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        Console.WriteLine($"Registration acceptance golden verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyAccepted(RegistrationAcceptanceEvaluation evaluation)
    {
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Decision == RegistrationAcceptanceDecision.Accepted
            && Status(evaluation, "Correspondence count") == ResultStatus.Pass
            && Status(evaluation, "Fitness") == ResultStatus.Pass
            && Status(evaluation, "Inlier RMSE") == ResultStatus.Pass
            && Status(evaluation, "Homogeneous row max error") == ResultStatus.Pass
            && Status(evaluation, "Rotation orthogonality max error") == ResultStatus.Pass
            && Status(evaluation, "Rotation determinant") == ResultStatus.Pass
            && Status(evaluation, "Translation magnitude") == ResultStatus.Pass
            && Status(evaluation, "Rotation angle") == ResultStatus.Pass;
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyMetricOrderAndRecording()
    {
        var evaluation = Evaluate();
        var expectedNames = new[]
        {
            "Source point count",
            "Target point count",
            "Correspondence count",
            "Minimum correspondence count",
            "Fitness",
            "Minimum fitness",
            "Inlier RMSE",
            "Maximum inlier RMSE",
            "Homogeneous row max error",
            "Rotation orthogonality max error",
            "Rotation determinant",
            "Rigid transform tolerance",
            "Translation magnitude",
            "Maximum translation",
            "Rotation angle",
            "Maximum rotation"
        };
        var passed = evaluation.Result.Metrics.Select(metric => metric.Name).SequenceEqual(expectedNames, StringComparer.Ordinal)
            && Metric(evaluation, "Correspondence count").Value == 600.0
            && Metric(evaluation, "Fitness").Value == 0.6
            && Metric(evaluation, "Inlier RMSE").Value == 0.01
            && Metric(evaluation, "Minimum correspondence count").Value == 500.0
            && Metric(evaluation, "Minimum fitness").Value == 0.5
            && Metric(evaluation, "Maximum inlier RMSE").Value == 0.02;
        return Evidence(evaluation, passed, $"metricCount={evaluation.Result.Metrics.Count}");
    }

    private static (bool Passed, string Evidence) VerifyZeroCorrespondenceFalseSuccess()
    {
        var evaluation = Evaluate(correspondenceCount: 0, fitness: 0.0, inlierRmse: 0.0);
        var verification = VerifyOutcome(
            evaluation,
            ResultStatus.Fail,
            RegistrationAcceptanceDecision.NoCorrespondences,
            ("Correspondence count", ResultStatus.Fail),
            ("Fitness", ResultStatus.NotRun),
            ("Inlier RMSE", ResultStatus.NotRun));
        var passed = verification.Passed
            && Metric(evaluation, "Inlier RMSE").Value == 0.0
            && evaluation.Result.Message.Contains("RMSE were not evaluated", StringComparison.Ordinal);
        return Evidence(evaluation, passed, "rmse=0,accepted=False");
    }

    private static (bool Passed, string Evidence) VerifyNonRigid(IReadOnlyList<double> transform)
    {
        var evaluation = Evaluate(transform: transform);
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evaluation.Decision == RegistrationAcceptanceDecision.NonRigidTransform
            && Status(evaluation, "Inlier RMSE") == ResultStatus.Pass
            && new[]
            {
                Status(evaluation, "Homogeneous row max error"),
                Status(evaluation, "Rotation orthogonality max error"),
                Status(evaluation, "Rotation determinant")
            }.Contains(ResultStatus.Fail);
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyInvalidPointCounts()
    {
        var source = Evaluate(sourcePointCount: 0);
        var target = Evaluate(targetPointCount: 0);
        var passed = IsOutcome(source, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidEvidence)
            && IsOutcome(target, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidEvidence);
        return (passed, $"source={source.Decision},target={target.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidRmse()
    {
        var negative = Evaluate(inlierRmse: -0.001);
        var nonfinite = Evaluate(inlierRmse: double.PositiveInfinity);
        var passed = IsOutcome(negative, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidEvidence)
            && IsOutcome(nonfinite, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidEvidence);
        return (passed, $"negative={negative.Decision},nonfinite={nonfinite.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidTransformNumbers()
    {
        var nonfinite = Evaluate(transform: Modify(Transform(), 0, double.PositiveInfinity));
        var overflow = Evaluate(transform: Modify(Transform(), 3, 1e308));
        var passed = IsOutcome(nonfinite, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidEvidence)
            && IsOutcome(overflow, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidEvidence)
            && Status(nonfinite, "Inlier RMSE") == ResultStatus.Pass
            && Status(nonfinite, "Homogeneous row max error") == ResultStatus.Error
            && Status(overflow, "Translation magnitude") == ResultStatus.NotRun;
        return (passed, $"nonfinite={nonfinite.Decision},finiteOverflow={overflow.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidPolicyGuards()
    {
        var zeroCorrespondenceGuard = Evaluate(policy: Policy with { MinimumCorrespondenceCount = 0 });
        var zeroFitnessGuard = Evaluate(policy: Policy with { MinimumFitness = 0.0 });
        var zeroRigidTolerance = Evaluate(policy: Policy with { RigidTransformTolerance = 0.0 });
        var passed = IsOutcome(zeroCorrespondenceGuard, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidPolicy)
            && IsOutcome(zeroFitnessGuard, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidPolicy)
            && IsOutcome(zeroRigidTolerance, ResultStatus.Error, RegistrationAcceptanceDecision.InvalidPolicy);
        return (
            passed,
            $"minimumCorrespondence={zeroCorrespondenceGuard.Decision},minimumFitness={zeroFitnessGuard.Decision},rigidTolerance={zeroRigidTolerance.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyOutcome(
        RegistrationAcceptanceEvaluation evaluation,
        ResultStatus expectedStatus,
        RegistrationAcceptanceDecision expectedDecision,
        params (string Name, ResultStatus Status)[] metricStatuses)
    {
        var passed = IsOutcome(evaluation, expectedStatus, expectedDecision)
            && metricStatuses.All(expected => Status(evaluation, expected.Name) == expected.Status);
        return Evidence(evaluation, passed);
    }

    private static bool IsOutcome(
        RegistrationAcceptanceEvaluation evaluation,
        ResultStatus expectedStatus,
        RegistrationAcceptanceDecision expectedDecision) =>
        evaluation.Result.Status == expectedStatus && evaluation.Decision == expectedDecision;

    private static (bool Passed, string Evidence) Evidence(
        RegistrationAcceptanceEvaluation evaluation,
        bool passed,
        string? suffix = null)
    {
        var text = $"status={evaluation.Result.Status},decision={evaluation.Decision}"
            + (suffix is null ? string.Empty : $",{suffix}");
        return (passed, text);
    }

    private static RegistrationAcceptanceEvaluation Evaluate(
        RegistrationAcceptancePolicy? policy = null,
        string unit = "model",
        long sourcePointCount = 1000,
        long targetPointCount = 1200,
        long correspondenceCount = 600,
        double fitness = 0.6,
        double inlierRmse = 0.01,
        IReadOnlyList<double>? transform = null) =>
        RegistrationAcceptanceRule.Evaluate(
            policy ?? Policy,
            new RegistrationResultEvidence(
                unit,
                sourcePointCount,
                targetPointCount,
                correspondenceCount,
                fitness,
                inlierRmse,
                transform ?? Transform()));

    private static double[] Transform(
        double rotationDegrees = 0.0,
        double translateX = 0.0,
        double translateY = 0.0,
        double translateZ = 0.0)
    {
        var radians = rotationDegrees * Math.PI / 180.0;
        var cosine = Math.Cos(radians);
        var sine = Math.Sin(radians);
        return
        [
            cosine, -sine, 0.0, translateX,
            sine, cosine, 0.0, translateY,
            0.0, 0.0, 1.0, translateZ,
            0.0, 0.0, 0.0, 1.0
        ];
    }

    private static double[] Modify(double[] values, int index, double value)
    {
        values[index] = value;
        return values;
    }

    private static Metric Metric(RegistrationAcceptanceEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.Single(metric => metric.Name == name);

    private static ResultStatus? Status(RegistrationAcceptanceEvaluation evaluation, string name) =>
        Metric(evaluation, name).Status;

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

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

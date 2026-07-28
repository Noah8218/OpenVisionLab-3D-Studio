using System.Globalization;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class VolumeGoldenVerification
{
    private static readonly HeightFieldPlaneSample[] Reference =
    [
        Sample(-1, 0, -1), Sample(1, 0, -1), Sample(-1, 0, 1), Sample(1, 0, 1)
    ];

    public static int Run(string reportPath)
    {
        var balanced = new[] { Sample(0, 1, 0), Sample(1, 2, 0), Sample(2, -1, 0), Sample(3, -2, 0) };
        var cases = new[]
        {
            Check("known-balanced-pass", () => Verify(Evaluate(Reference, balanced, 0.5, 0.0, 1e-6), ResultStatus.Pass, 1.5, 1.5, 0.0)),
            Check("net-tolerance-fail", () => Verify(Evaluate(Reference, balanced, 0.5, 1.0, 0.1), ResultStatus.Fail, 1.5, 1.5, 0.0)),
            Check("signed-positive-net", () => Verify(Evaluate(Reference, [Sample(0, 3, 0), Sample(1, -1, 0)], 2.0, 4.0, 1e-6), ResultStatus.Pass, 6.0, 2.0, 4.0)),
            Check("insufficient-reference-error", () => VerifyError(Evaluate(Reference[..2], balanced, 1.0, 0.0, 1.0), "at least three")),
            Check("empty-measurement-error", () => VerifyError(Evaluate(Reference, [], 1.0, 0.0, 1.0), "at least one")),
            Check("invalid-area-error", () => VerifyError(Evaluate(Reference, balanced, 0.0, 0.0, 1.0), "area positive")),
            Check("nonfinite-measurement-error", () => VerifyError(Evaluate(Reference, [Sample(0, double.NaN, 0)], 1.0, 0.0, 1.0), "non-finite")),
            Check("invalid-tolerance-error", () => VerifyError(Evaluate(Reference, balanced, 1.0, 0.0, -1.0), "non-negative")),
            Check("missing-unit-error", () => VerifyError(Evaluate(Reference, balanced, 1.0, 0.0, 1.0, string.Empty), "required"))
        };

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"VolumeGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|reference=least-squares-height-field-plane|above=sum(max(verticalDelta,0)*sampleArea)|below=sum(max(-verticalDelta,0)*sampleArea)|net=above-below"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Volume golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static VolumeEvaluation Evaluate(
        IReadOnlyList<HeightFieldPlaneSample> reference,
        IReadOnlyList<HeightFieldPlaneSample> measurement,
        double area,
        double expected,
        double tolerance,
        string unit = "model^3") =>
        VolumeRule.Evaluate(new VolumeRuleInput("source.synthetic-volume", reference, measurement, area, expected, tolerance, unit));

    private static (bool Passed, string Evidence) Verify(
        VolumeEvaluation evaluation,
        ResultStatus status,
        double above,
        double below,
        double net) =>
        (evaluation.Result.Status == status
            && Approximately(evaluation.AboveVolume, above)
            && Approximately(evaluation.BelowVolume, below)
            && Approximately(evaluation.NetVolume, net),
        $"status={evaluation.Result.Status},above={Format(evaluation.AboveVolume)},below={Format(evaluation.BelowVolume)},net={Format(evaluation.NetVolume)}");

    private static (bool Passed, string Evidence) VerifyError(VolumeEvaluation evaluation, string fragment) =>
        (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase),
        $"status={evaluation.Result.Status},message={evaluation.Result.Message}");

    private static HeightFieldPlaneSample Sample(double x, double y, double z) =>
        new(new Vector3((float)x, (float)y, (float)z), y);

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

    private static bool Approximately(double actual, double expected) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= 1e-6;

    private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

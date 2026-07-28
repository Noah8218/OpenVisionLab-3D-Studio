using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class GapFlushGoldenVerification
{
    private static readonly HeightDeviationRecipeRoiRegion Left = new(0.0, 0.0, 1.0, 1.0);
    private static readonly HeightDeviationRecipeRoiRegion Right = new(3.0, 0.0, 1.0, 1.0);
    private static readonly GapFlushRegionStats LeftStats = new(20, 100.0, 1.0);
    private static readonly GapFlushRegionStats RightStats = new(30, 104.0, 1.4);

    public static int Run(string reportPath)
    {
        var acceptance = new C3DGapFlushAcceptance(1.0, 1e-6, 4.0, 1e-6);
        var cases = new[]
        {
            Check("known-signed-pass", () => Verify(Evaluate(Left, Right, LeftStats, RightStats, acceptance), ResultStatus.Pass, 1.0, 4.0)),
            Check("gap-tolerance-fail", () => VerifyMetricFailure(Evaluate(Left, Right, LeftStats, RightStats, acceptance with { ExpectedGap = 1.1 }), "Signed gap")),
            Check("flush-tolerance-fail", () => VerifyMetricFailure(Evaluate(Left, Right, LeftStats, RightStats, acceptance with { ExpectedFlush = 4.1 }), "Signed flush")),
            Check("signed-overlap-gap", () => Verify(Evaluate(Left, Right with { CenterX = 1.5 }, LeftStats, RightStats, acceptance with { ExpectedGap = -0.5 }), ResultStatus.Pass, -0.5, 4.0)),
            Check("empty-left-error", () => VerifyError(Evaluate(Left, Right, LeftStats with { PointCount = 0 }, RightStats, acceptance), "at least one point")),
            Check("nonfinite-stat-error", () => VerifyError(Evaluate(Left, Right, LeftStats with { RawMean = double.NaN }, RightStats, acceptance), "finite")),
            Check("invalid-tolerance-error", () => VerifyError(Evaluate(Left, Right, LeftStats, RightStats, acceptance with { GapTolerance = -1.0 }), "non-negative")),
            Check("missing-unit-error", () => VerifyError(Evaluate(Left, Right, LeftStats, RightStats, acceptance, gapUnit: string.Empty), "required"))
        };

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"GapFlushGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|gap=right.leftEdge-left.rightEdge|flush=right.rawMean-left.rawMean|direction=left-to-right"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Gap / Flush golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) Verify(GapFlushEvaluation evaluation, ResultStatus status, double gap, double flush) =>
        (evaluation.Result.Status == status
            && Approximately(evaluation.SignedGap, gap)
            && Approximately(evaluation.SignedFlush, flush)
            && Approximately(evaluation.ModelFlush, 0.4),
        $"status={evaluation.Result.Status},gap={Format(evaluation.SignedGap)},flush={Format(evaluation.SignedFlush)},modelFlush={Format(evaluation.ModelFlush)}");

    private static (bool Passed, string Evidence) VerifyMetricFailure(GapFlushEvaluation evaluation, string failedMetric)
    {
        var metric = evaluation.Result.Metrics.Single(item => item.Name == failedMetric);
        return (evaluation.Result.Status == ResultStatus.Fail && metric.Status == ResultStatus.Fail,
            $"status={evaluation.Result.Status},{failedMetric}={metric.Status}");
    }

    private static (bool Passed, string Evidence) VerifyError(GapFlushEvaluation evaluation, string fragment) =>
        (evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase),
        $"status={evaluation.Result.Status},message={evaluation.Result.Message}");

    private static GapFlushEvaluation Evaluate(
        HeightDeviationRecipeRoiRegion left,
        HeightDeviationRecipeRoiRegion right,
        GapFlushRegionStats leftStats,
        GapFlushRegionStats rightStats,
        C3DGapFlushAcceptance acceptance,
        string gapUnit = "model") =>
        GapFlushRule.Evaluate(new GapFlushInput(
            "source.synthetic-gap-flush",
            left,
            right,
            leftStats,
            rightStats,
            acceptance,
            gapUnit,
            "raw-height"));

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

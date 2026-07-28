using System.Globalization;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class CrossSectionDimensionsGoldenVerification
{
    private static readonly CrossSectionSample[] Known =
    [
        new(0, new Vector3(-2, 0, 0), 10),
        new(1, new Vector3(0, 1, 0), 15),
        new(2, new Vector3(3, -1, 0), 5)
    ];

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("known-width-height-pass", () => Verify(Evaluate(Known, 0, 2, 5, 1e-6, 10, 1e-6), ResultStatus.Pass, 5, 10)),
            Check("width-tolerance-fail", () => VerifyMetricFailure(Evaluate(Known, 0, 2, 4, 0.1, 10, 1e-6), "Section width")),
            Check("height-tolerance-fail", () => VerifyMetricFailure(Evaluate(Known, 0, 2, 5, 1e-6, 9, 0.1), "Raw-height range")),
            Check("unordered-columns-error", () => VerifyError(Evaluate(Known, 2, 1, 5, 1, 10, 1), "ordered")),
            Check("insufficient-samples-error", () => VerifyError(Evaluate(Known[..1], 0, 2, 5, 1, 10, 1), "at least two")),
            Check("nonfinite-sample-error", () => VerifyError(Evaluate([Known[0] with { RawHeight = double.NaN }, Known[1]], 0, 2, 5, 1, 10, 1), "non-finite")),
            Check("out-of-range-column-error", () => VerifyError(Evaluate([Known[0], Known[1] with { Column = 3 }], 0, 2, 5, 1, 10, 1), "out-of-range")),
            Check("invalid-tolerance-error", () => VerifyError(Evaluate(Known, 0, 2, 5, -1, 10, 1), "non-negative")),
            Check("missing-unit-error", () => VerifyError(Evaluate(Known, 0, 2, 5, 1, 10, 1, string.Empty), "required"))
        };

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"CrossSectionDimensionsGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|width=max(alignedX)-min(alignedX)|heightRange=max(rawHeight)-min(rawHeight)|selectors=exact source row and inclusive columns"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Cross-section Dimensions golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static CrossSectionEvaluation Evaluate(
        IReadOnlyList<CrossSectionSample> samples,
        int start,
        int end,
        double expectedWidth,
        double widthTolerance,
        double expectedHeight,
        double heightTolerance,
        string widthUnit = "model") =>
        CrossSectionDimensionsRule.Evaluate(new CrossSectionDimensionsInput(
            "source.synthetic-cross-section", 4, start, end, samples,
            expectedWidth, widthTolerance, expectedHeight, heightTolerance, widthUnit, "raw-height"));

    private static (bool Passed, string Evidence) Verify(CrossSectionEvaluation evaluation, ResultStatus status, double width, double height) =>
        (evaluation.Result.Status == status && Approximately(evaluation.Width, width) && Approximately(evaluation.HeightRange, height),
        $"status={evaluation.Result.Status},width={Format(evaluation.Width)},heightRange={Format(evaluation.HeightRange)}");

    private static (bool Passed, string Evidence) VerifyMetricFailure(CrossSectionEvaluation evaluation, string metricName)
    {
        var metric = evaluation.Result.Metrics.Single(item => item.Name == metricName);
        return (evaluation.Result.Status == ResultStatus.Fail && metric.Status == ResultStatus.Fail,
            $"status={evaluation.Result.Status},{metricName}={metric.Status}");
    }

    private static (bool Passed, string Evidence) VerifyError(CrossSectionEvaluation evaluation, string fragment) =>
        (evaluation.Result.Status == ResultStatus.Error && evaluation.Result.Message.Contains(fragment, StringComparison.OrdinalIgnoreCase),
        $"status={evaluation.Result.Status},message={evaluation.Result.Message}");

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

    private static bool Approximately(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) <= 1e-6;
    private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

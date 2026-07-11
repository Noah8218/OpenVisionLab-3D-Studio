using System.Globalization;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class PointPairDimensionsGoldenVerification
{
    private static readonly Vector3 First = new(1.0f, 2.0f, 3.0f);
    private static readonly Vector3 Second = new(4.0f, 6.0f, 7.0f);
    private static readonly double ExpectedDistance = Math.Sqrt(41.0);
    private const double ExpectedWidth = 5.0;
    private static readonly double ExpectedAngle = Math.Atan2(4.0, 5.0) * 180.0 / Math.PI;

    public static int Run(string reportPath)
    {
        var passingAcceptance = Acceptance(ExpectedDistance, ExpectedWidth, ExpectedAngle);
        var cases = new[]
        {
            Check("known-vector-pass", () => VerifyKnownVector(Evaluate(First, Second, passingAcceptance), ResultStatus.Pass, ExpectedAngle)),
            Check("distance-tolerance-fail", () => VerifyDistanceFailure(Evaluate(First, Second, passingAcceptance with { ExpectedDistance = ExpectedDistance + 0.1 }))),
            Check("signed-descending-angle", () => VerifyKnownVector(Evaluate(First, new Vector3(4.0f, -2.0f, 7.0f), Acceptance(ExpectedDistance, ExpectedWidth, -ExpectedAngle)), ResultStatus.Pass, -ExpectedAngle)),
            Check("coincident-point-error", () => VerifyError(Evaluate(First, First, passingAcceptance), "different positions")),
            Check("nonfinite-point-error", () => VerifyError(Evaluate(new Vector3(float.NaN, 2.0f, 3.0f), Second, passingAcceptance), "finite")),
            Check("nonfinite-raw-height-error", () => VerifyError(Evaluate(First, Second, passingAcceptance, firstRawHeight: double.NaN), "finite")),
            Check("invalid-distance-tolerance-error", () => VerifyError(Evaluate(First, Second, passingAcceptance with { DistanceTolerance = -0.1 }), "invalid")),
            Check("invalid-angle-error", () => VerifyError(Evaluate(First, Second, passingAcceptance with { ExpectedElevationAngleDegrees = 91.0 }), "invalid")),
            Check("missing-unit-error", () => VerifyError(Evaluate(First, Second, passingAcceptance, unit: string.Empty), "required"))
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"PointPairDimensionsGoldenVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
            $"GoldenVector|first=(1,2,3)|second=(4,6,7)|delta=(3,4,4)|expectedDistance={Format(ExpectedDistance)}|expectedWidth={Format(ExpectedWidth)}|expectedAngleDegrees={Format(ExpectedAngle)}"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Point pair dimensions golden verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyKnownVector(
        PointPairDimensionsEvaluation evaluation,
        ResultStatus expectedStatus,
        double expectedAngle)
    {
        var passed = evaluation.Result.Status == expectedStatus
            && Approximately(evaluation.Delta.X, 3.0)
            && Approximately(Math.Abs(evaluation.Delta.Y), 4.0)
            && Approximately(evaluation.Delta.Z, 4.0)
            && Approximately(evaluation.Distance, ExpectedDistance)
            && Approximately(evaluation.PlanarWidth, ExpectedWidth)
            && Approximately(evaluation.ElevationAngleDegrees, expectedAngle);
        return (
            passed,
            $"status={evaluation.Result.Status},distance={Format(evaluation.Distance)},width={Format(evaluation.PlanarWidth)},angle={Format(evaluation.ElevationAngleDegrees)}");
    }

    private static (bool Passed, string Evidence) VerifyDistanceFailure(PointPairDimensionsEvaluation evaluation)
    {
        var distance = evaluation.Result.Metrics.Single(metric => metric.Name == "3D distance");
        var width = evaluation.Result.Metrics.Single(metric => metric.Name == "XZ planar width");
        var angle = evaluation.Result.Metrics.Single(metric => metric.Name == "Elevation angle");
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && distance.Status == ResultStatus.Fail
            && width.Status == ResultStatus.Pass
            && angle.Status == ResultStatus.Pass;
        return (passed, $"status={evaluation.Result.Status},distance={distance.Status},width={width.Status},angle={angle.Status}");
    }

    private static (bool Passed, string Evidence) VerifyError(
        PointPairDimensionsEvaluation evaluation,
        string expectedMessageFragment)
    {
        var passed = evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains(expectedMessageFragment, StringComparison.OrdinalIgnoreCase);
        return (passed, $"status={evaluation.Result.Status},message={evaluation.Result.Message}");
    }

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

    private static PointPairDimensionsEvaluation Evaluate(
        Vector3 first,
        Vector3 second,
        C3DPointPairDimensionsAcceptance acceptance,
        double firstRawHeight = 100.0,
        string unit = "model") =>
        PointPairDimensionsRule.Evaluate(new PointPairDimensionsInput(
            "source.synthetic-point-pair",
            first,
            second,
            firstRawHeight,
            104.0,
            acceptance,
            unit,
            "raw-height"));

    private static C3DPointPairDimensionsAcceptance Acceptance(double distance, double width, double angle) =>
        new(distance, 1e-5, width, 1e-5, angle, 1e-5);

    private static bool Approximately(double actual, double expected, double tolerance = 1e-5) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Format(double value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

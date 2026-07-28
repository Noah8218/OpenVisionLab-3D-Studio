using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DThicknessGoldenVerification
{
    private static readonly C3DGridRoi Roi = new(0, 0, 2, 2);
    private static readonly C3DThicknessAcceptance PassingLimits = new(0.9, 1.2);

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("known-pass", VerifyPass),
            Check("outside-limits-fail", VerifyFailure),
            Check("outside-grid-error", VerifyInvalidRoi),
            Check("inverted-limits-error", VerifyInvalidLimits),
            Check("insufficient-valid-samples-error", VerifyInsufficientSamples)
        };

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DThicknessGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|one-taught-grid-roi|scalar=declared-height-map|physicalCalibration=not-inferred"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"C3D Thickness golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyPass()
    {
        var evaluation = Evaluate(PassingLimits);
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && evaluation.Result.ToolName == C3DThicknessRule.ToolName
            && Approximately(evaluation.Mean, 1.0875)
            && Approximately(evaluation.Range, 0.2)
            && evaluation.ValidSampleCount == 4
            && evaluation.Result.Overlays.SingleOrDefault()?.Id == "overlay.c3d-thickness-roi";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyFailure()
    {
        var evaluation = Evaluate(new C3DThicknessAcceptance(1.02, 1.12));
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evaluation.HasMeasurement
            && evaluation.BelowLowerLimitCount == 1
            && evaluation.AboveUpperLimitCount == 1
            && Approximately(evaluation.Mean, 1.0875);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInvalidRoi()
    {
        var evaluation = Evaluate(PassingLimits, new C3DGridRoi(1, 1, 2, 2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InvalidRoi"
            && evaluation.Result.Overlays.Count == 0;
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInvalidLimits()
    {
        var evaluation = Evaluate(new C3DThicknessAcceptance(1.2, 0.9));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageErrorCode == "InvalidParameter";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInsufficientSamples()
    {
        var evaluation = Evaluate(PassingLimits, values: [double.NaN, double.NaN, double.NaN, 1.0], minimumValidSamples: 2);
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InsufficientData";
        return (passed, Evidence(evaluation));
    }

    private static C3DThicknessEvaluation Evaluate(
        C3DThicknessAcceptance acceptance,
        C3DGridRoi? roi = null,
        IReadOnlyList<double>? values = null,
        int minimumValidSamples = 1) =>
        C3DThicknessRule.Evaluate(new C3DThicknessInput(
            "source.synthetic-c3d-thickness",
            2,
            2,
            values ?? [1.0, 1.1, 1.05, 1.2],
            roi ?? Roi,
            acceptance,
            "raw-height",
            "frame.synthetic-c3d-grid",
            minimumValidSamples));

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

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Evidence(C3DThicknessEvaluation evaluation) =>
        $"status={evaluation.Result.Status},hasMeasurement={evaluation.HasMeasurement},packageStatus={evaluation.PackageResultStatus},error={evaluation.PackageErrorCode},mean={Format(evaluation.Mean)},range={Format(evaluation.Range)},valid={evaluation.ValidSampleCount},below={evaluation.BelowLowerLimitCount},above={evaluation.AboveUpperLimitCount}";

    private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

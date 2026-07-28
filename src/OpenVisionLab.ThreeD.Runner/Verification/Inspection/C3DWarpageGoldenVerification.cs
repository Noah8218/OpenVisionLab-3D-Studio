using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DWarpageGoldenVerification
{
    private static readonly C3DGridRoi Roi = new(0, 0, 3, 3);

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("best-fit-plane-pass", VerifyBestFitPlane),
            Check("peak-to-valley-fail", VerifyPeakToValleyFailure),
            Check("rms-fail", VerifyRmsFailure),
            Check("outside-grid-error", VerifyInvalidRoi),
            Check("insufficient-valid-samples-error", VerifyInsufficientSamples)
        };

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DWarpageGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|best-fit-inspection-roi|scalar=declared-raw-height|physicalCalibration=not-inferred"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"C3D Warpage golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyBestFitPlane()
    {
        var evaluation = Evaluate(new C3DWarpageAcceptance(0.000001, 0.000001));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && evaluation.Result.ToolName == C3DWarpageRule.ToolName
            && Approximately(evaluation.PeakToValley, 0.0)
            && Approximately(evaluation.Rms, 0.0)
            && Approximately(evaluation.PlaneSlopeX, 2.0)
            && Approximately(evaluation.PlaneSlopeY, 3.0)
            && evaluation.ValidSampleCount == 9
            && evaluation.Result.Overlays.SingleOrDefault()?.Id == "overlay.c3d-warpage-roi";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyPeakToValleyFailure()
    {
        var values = CreatePlanarValues();
        values[^1] += 0.1;
        var evaluation = Evaluate(new C3DWarpageAcceptance(0.001), values: values);
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evaluation.HasMeasurement
            && evaluation.PeakToValley > 0.001
            && evaluation.Rms > 0.0;
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyRmsFailure()
    {
        var values = CreatePlanarValues();
        values[^1] += 0.1;
        var evaluation = Evaluate(new C3DWarpageAcceptance(1.0, 0.01), values: values);
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evaluation.HasMeasurement
            && evaluation.PeakToValley < 1.0
            && evaluation.Rms > 0.01;
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInvalidRoi()
    {
        var evaluation = Evaluate(new C3DWarpageAcceptance(1.0), new C3DGridRoi(2, 2, 2, 2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InvalidRoi"
            && evaluation.Result.Overlays.Count == 0;
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInsufficientSamples()
    {
        var evaluation = Evaluate(
            new C3DWarpageAcceptance(1.0),
            values: [double.NaN, double.NaN, double.NaN, 1.0],
            rows: 2,
            columns: 2,
            roi: new C3DGridRoi(0, 0, 2, 2),
            minimumValidSamples: 3);
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InsufficientData";
        return (passed, Evidence(evaluation));
    }

    private static C3DWarpageEvaluation Evaluate(
        C3DWarpageAcceptance acceptance,
        C3DGridRoi? roi = null,
        IReadOnlyList<double>? values = null,
        int rows = 3,
        int columns = 3,
        int minimumValidSamples = 3) =>
        C3DWarpageRule.Evaluate(new C3DWarpageInput(
            "source.synthetic-c3d-warpage",
            rows,
            columns,
            values ?? CreatePlanarValues(),
            roi ?? Roi,
            acceptance,
            "raw-height",
            "frame.synthetic-c3d-grid",
            minimumValidSamples));

    private static double[] CreatePlanarValues()
    {
        var values = new double[9];
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                values[row * 3 + column] = 2.0 * column + 3.0 * row + 5.0;
            }
        }

        return values;
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

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Evidence(C3DWarpageEvaluation evaluation) =>
        $"status={evaluation.Result.Status},hasMeasurement={evaluation.HasMeasurement},packageStatus={evaluation.PackageResultStatus},error={evaluation.PackageErrorCode},peakToValley={Format(evaluation.PeakToValley)},rms={Format(evaluation.Rms)},minimumResidual={Format(evaluation.MinimumResidual)},maximumResidual={Format(evaluation.MaximumResidual)},valid={evaluation.ValidSampleCount}";

    private static string Format(double value) => value.ToString("F6", CultureInfo.InvariantCulture);
    private static string Clean(string value) => value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');
    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class LibraryNoahThreeDPackageVerification
{
    private const string Unit = "mm";
    private const string FrameId = "frame.synthetic-library-noah";
    private const string SourceId = "source.synthetic-library-noah";

    public static int Run(string reportPath)
    {
        var thicknessSource = CreateThicknessSource();
        var cases = new (string Name, Func<(bool Passed, string Evidence)> Verify)[]
        {
            ("package-identity", VerifyPackageIdentity),
            ("thickness-pass-metrics", () => VerifyThicknessPass(thicknessSource)),
            ("thickness-fail-retains-measurement", () => VerifyThicknessFailure(thicknessSource)),
            ("invalid-roi-is-controlled", () => VerifyInvalidRoi(thicknessSource)),
            ("missing-unit-is-bridge-error", () => VerifyMissingUnit(thicknessSource)),
            ("warpage-analytic-plane-pass", VerifyWarpagePlane),
            ("warpage-fail-and-insufficient-data", VerifyWarpageFailureAndInsufficientData)
        };

        var results = cases
            .Select(item =>
            {
                var verification = Check(item.Name, item.Verify);
                return (item.Name, verification.Passed, verification.Evidence);
            })
            .ToArray();

        var passed = results.Count(item => item.Passed);
        var status = passed == results.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"LibraryNoahThreeDPackageVerification|{status}|cases={results.Length}|passed={passed}|failed={results.Length - passed}",
            $"Package|id={LibraryNoahHeightMapInspection.PackageId}|version={LibraryNoahHeightMapInspection.PackageVersion}|assembly={LibraryNoahHeightMapInspection.PackageAssemblyName}|sourceCommit={LibraryNoahHeightMapInspection.PackageSourceCommit}|target=netstandard2.0"
        };
        lines.AddRange(results.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{item.Evidence}"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Library-Noah 3D package verification: {status} ({passed}/{results.Length})");
        return passed == results.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyPackageIdentity()
    {
        var passed = LibraryNoahHeightMapInspection.PackageAssemblyName == "Lib.ThreeD"
            && LibraryNoahHeightMapInspection.PackageId == "Lib.ThreeD"
            && LibraryNoahHeightMapInspection.PackageVersion == "2.3.0"
            && LibraryNoahHeightMapInspection.PackageSourceCommit == "630e37b9111f3223217c815e19c480546fde8ad7";
        return (passed, $"assembly={LibraryNoahHeightMapInspection.PackageAssemblyName},version={LibraryNoahHeightMapInspection.PackageVersion},commit={LibraryNoahHeightMapInspection.PackageSourceCommit}");
    }

    private static (bool Passed, string Evidence) VerifyThicknessPass(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source, null, 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "Passed"
            && Approximately(Metric(evaluation, "ValidSampleCount"), 4.0)
            && Approximately(Metric(evaluation, "Mean"), 1.0875)
            && Approximately(Metric(evaluation, "Range"), 0.2);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyThicknessFailure(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source, null, 1.02, 1.12));
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "Failed"
            && Approximately(Metric(evaluation, "BelowLowerLimitCount"), 1.0)
            && Approximately(Metric(evaluation, "AboveUpperLimitCount"), 1.0);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInvalidRoi(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source, new LibraryNoahGridRoi(1, 1, 2, 2), 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InvalidRoi"
            && evaluation.PackageErrorCode == "InvalidRoi";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissingUnit(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source with { Unit = string.Empty }, null, 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "BridgeError"
            && evaluation.Result.Message.Contains("unit", StringComparison.OrdinalIgnoreCase);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyWarpagePlane()
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateWarpage(
            new LibraryNoahWarpageInspectionInput(CreatePlanarSource(), null, 0.000001, 0.000001));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && Approximately(Metric(evaluation, "PeakToValley"), 0.0)
            && Approximately(Metric(evaluation, "Rms"), 0.0)
            && Approximately(Metric(evaluation, "PlaneSlopeX"), 2.0)
            && Approximately(Metric(evaluation, "PlaneSlopeY"), 3.0);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyWarpageFailureAndInsufficientData()
    {
        var residualValues = CreatePlanarValues();
        residualValues[^1] += 0.1;
        var failure = LibraryNoahHeightMapInspection.EvaluateWarpage(
            new LibraryNoahWarpageInspectionInput(CreateSource(3, 3, residualValues), null, 0.001));
        var insufficient = LibraryNoahHeightMapInspection.EvaluateWarpage(
            new LibraryNoahWarpageInspectionInput(
                CreateSource(2, 2, [double.NaN, double.NaN, double.NaN, 1.0]),
                null,
                0.001,
                MinimumValidSamples: 3));
        var passed = failure.Result.Status == ResultStatus.Fail
            && failure.HasMeasurement
            && Metric(failure, "PeakToValley") > 0.001
            && insufficient.Result.Status == ResultStatus.Error
            && !insufficient.HasMeasurement
            && insufficient.PackageResultStatus == "InsufficientData";
        return (passed, $"failure=({Evidence(failure)}),insufficient=({Evidence(insufficient)})");
    }

    private static LibraryNoahHeightMapInput CreateThicknessSource() =>
        CreateSource(2, 2, [1.0, 1.1, 1.05, 1.2]);

    private static LibraryNoahHeightMapInput CreatePlanarSource() =>
        CreateSource(3, 3, CreatePlanarValues());

    private static LibraryNoahHeightMapInput CreateSource(int rows, int columns, IReadOnlyList<double> values) =>
        new(SourceId, rows, columns, 0.0, 0.0, 1.0, 1.0, values, Unit, FrameId);

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

    private static double Metric(LibraryNoahInspectionEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.Single(metric => metric.Name == name).Value;

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Evidence(LibraryNoahInspectionEvaluation evaluation) =>
        $"status={evaluation.Result.Status},hasMeasurement={evaluation.HasMeasurement},packageStatus={evaluation.PackageResultStatus},error={evaluation.PackageErrorCode},metrics={string.Join(',', evaluation.Result.Metrics.Select(metric => $"{metric.Name}={metric.Value.ToString("R", CultureInfo.InvariantCulture)}"))}";

    private static (bool Passed, string Evidence) Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            return verify();
        }
        catch (Exception exception)
        {
            return (false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }
}

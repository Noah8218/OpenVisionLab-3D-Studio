using System.Globalization;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class C3DThicknessAxisGoldenVerification
{
    private const string Unit = "synthetic-h-unit";
    private const double Intercept = 10.0;

    public static int Run(string reportPath)
    {
        var cases = new[]
        {
            Check("horizontal-h-axis-baseline", VerifyHorizontalBaseline),
            Check("positive-slope-h-axis-not-normal", VerifyPositiveSlope),
            Check("negative-slope-signed-h-axis-not-normal", VerifyNegativeSlope),
            Check("roi-movement-preserves-h-gap", VerifyRoiMovement),
            Check("residual-and-nodata-count", VerifyResidualAndNoData),
            Check("all-nodata-error", VerifyAllNoData),
            Check("collinear-reference-error", VerifyCollinearReference)
        };

        var passed = cases.Count(item => item.Passed);
        var status = passed == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"C3DThicknessAxisGoldenVerification|{status}|cases={cases.Length}|passed={passed}|failed={cases.Length - passed}",
            "Definition|owner=DualSurfaceThicknessRule|scalar=raw-height-minus-fitted-reference-H|sign=measurement-minus-reference|normalDistance=not-computed|physicalCalibration=not-inferred"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        Console.WriteLine($"C3D thickness H-axis golden verification: {status} ({passed}/{cases.Length})");
        return passed == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyHorizontalBaseline()
    {
        const double slopeX = 0.0;
        const double slopeZ = 0.0;
        const double hGap = 2.0;
        var evaluation = Evaluate(
            slopeX,
            slopeZ,
            hGap,
            Grid(-1.0, 1.0, -1.0, 1.0),
            Grid(-1.0, 1.0, -1.0, 1.0),
            hGap - 0.1,
            hGap + 0.1);
        var normalDistance = NormalDistance(hGap, slopeX, slopeZ);
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Result.Message.Contains("H-axis", StringComparison.Ordinal)
            && evaluation.Result.Overlays.Any(item => item.Id == "overlay.c3d-thickness-height-axis")
            && Approximately(evaluation.Mean, hGap)
            && Approximately(normalDistance, hGap)
            && evaluation.MeasurementSampleCount == 4;
        return (passed, Evidence(evaluation, hGap, normalDistance));
    }

    private static (bool Passed, string Evidence) VerifyPositiveSlope()
    {
        const double slopeX = 0.5;
        const double slopeZ = 0.25;
        const double hGap = 3.0;
        var positions = new (double X, double Z)[]
        {
            (-2.0, -1.0),
            (0.0, 2.0),
            (2.0, -2.0),
            (3.0, 3.0)
        };
        var evaluation = Evaluate(slopeX, slopeZ, hGap, positions, positions, hGap - 0.1, hGap + 0.1);
        var normalDistance = NormalDistance(hGap, slopeX, slopeZ);
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && Approximately(evaluation.ReferencePlane?.SlopeX ?? double.NaN, slopeX)
            && Approximately(evaluation.ReferencePlane?.SlopeZ ?? double.NaN, slopeZ)
            && Approximately(evaluation.Mean, hGap)
            && !Approximately(evaluation.Mean, normalDistance)
            && evaluation.MeasurementSampleCount == positions.Length;
        return (passed, Evidence(evaluation, hGap, normalDistance));
    }

    private static (bool Passed, string Evidence) VerifyNegativeSlope()
    {
        const double slopeX = -0.75;
        const double slopeZ = -0.5;
        const double hGap = -2.5;
        var positions = new (double X, double Z)[]
        {
            (-2.0, -1.0),
            (0.0, 2.0),
            (2.0, -2.0),
            (3.0, 3.0)
        };
        var evaluation = Evaluate(slopeX, slopeZ, hGap, positions, positions, hGap - 0.1, hGap + 0.1);
        var normalDistance = NormalDistance(hGap, slopeX, slopeZ);
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && Approximately(evaluation.Mean, hGap)
            && evaluation.Mean < 0.0
            && !Approximately(evaluation.Mean, normalDistance)
            && evaluation.Result.Message.Contains("H-axis", StringComparison.Ordinal)
            && evaluation.MeasurementSampleCount == positions.Length;
        return (passed, Evidence(evaluation, hGap, normalDistance));
    }

    private static (bool Passed, string Evidence) VerifyRoiMovement()
    {
        const double slopeX = 0.4;
        const double slopeZ = -0.3;
        const double hGap = 1.75;
        var reference = Grid(-2.0, 2.0, -2.0, 2.0);
        var firstRoi = Grid(-1.0, 1.0, -1.0, 1.0);
        var movedRoi = Grid(5.0, 7.0, 4.0, 6.0);
        var first = Evaluate(slopeX, slopeZ, hGap, reference, firstRoi, hGap - 0.1, hGap + 0.1);
        var moved = Evaluate(slopeX, slopeZ, hGap, reference, movedRoi, hGap - 0.1, hGap + 0.1);
        var normalDistance = NormalDistance(hGap, slopeX, slopeZ);
        var passed = first.Result.Status == ResultStatus.Pass
            && moved.Result.Status == ResultStatus.Pass
            && Approximately(first.Mean, hGap, 1e-5)
            && Approximately(moved.Mean, hGap, 1e-5)
            && Approximately(first.Mean, moved.Mean, 1e-5)
            && !Approximately(moved.Mean, normalDistance, 1e-5)
            && first.MeasurementSampleCount == 4
            && moved.MeasurementSampleCount == 4;
        return (passed, $"first={Evidence(first, hGap, normalDistance)},moved={Evidence(moved, hGap, normalDistance)}");
    }

    private static (bool Passed, string Evidence) VerifyResidualAndNoData()
    {
        const double slopeX = 0.25;
        const double slopeZ = -0.2;
        const double hGap = 1.5;
        var referencePositions = Grid(-1.0, 1.0, -1.0, 1.0);
        var reference = referencePositions
            .Select((position, index) => CreateSample(
                slopeX,
                slopeZ,
                position.X,
                position.Z,
                index switch
                {
                    0 => 0.20,
                    1 => -0.10,
                    2 => 0.05,
                    _ => -0.15
                }))
            .ToArray();
        var measurement = referencePositions
            .Select((position, index) => index == 3
                ? new HeightFieldPlaneSample(
                    new Vector3((float)position.X, (float)EvaluateReferenceHeight(slopeX, slopeZ, position.X, position.Z), (float)position.Z),
                    double.NaN)
                : CreateSample(slopeX, slopeZ, position.X, position.Z, hGap))
            .ToArray();
        var evaluation = Evaluate(reference, measurement, hGap - 0.75, hGap + 0.75, 3);
        var residual = Metric(evaluation, "Reference fit H RMS");
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && double.IsFinite(residual)
            && residual > 0.0
            && evaluation.MeasurementSampleCount == 3
            && double.IsFinite(evaluation.RootMeanSquareSpread);
        return (passed, $"{Evidence(evaluation, hGap, NormalDistance(hGap, slopeX, slopeZ))},referenceFitHrm={Format(residual)}");
    }

    private static (bool Passed, string Evidence) VerifyAllNoData()
    {
        const double slopeX = 0.25;
        const double slopeZ = -0.2;
        var reference = Grid(-1.0, 1.0, -1.0, 1.0)
            .Select(position => CreateSample(slopeX, slopeZ, position.X, position.Z, 0.0))
            .ToArray();
        var measurement = Grid(-1.0, 1.0, -1.0, 1.0)
            .Select(position => new HeightFieldPlaneSample(
                new Vector3((float)position.X, (float)EvaluateReferenceHeight(slopeX, slopeZ, position.X, position.Z), (float)position.Z),
                double.NaN))
            .ToArray();
        var evaluation = Evaluate(reference, measurement, -1.0, 1.0, 1);
        var passed = evaluation.Result.Status == ResultStatus.Error
            && evaluation.Result.Message.Contains("0 usable", StringComparison.Ordinal)
            && evaluation.Result.Overlays.Count == 0
            && evaluation.MeasurementSampleCount == measurement.Length;
        return (passed, Evidence(evaluation, double.NaN, double.NaN));
    }

    private static (bool Passed, string Evidence) VerifyCollinearReference()
    {
        const double slopeX = 0.25;
        const double slopeZ = -0.2;
        var reference = new[]
        {
            CreateSample(slopeX, slopeZ, -1.0, 0.0, 0.0),
            CreateSample(slopeX, slopeZ, 0.0, 0.0, 0.0),
            CreateSample(slopeX, slopeZ, 1.0, 0.0, 0.0)
        };
        var measurement = Grid(-1.0, 1.0, -1.0, 1.0)
            .Select(position => CreateSample(slopeX, slopeZ, position.X, position.Z, 1.0))
            .ToArray();
        var evaluation = Evaluate(reference, measurement, 0.0, 2.0, 1);
        var passed = evaluation.Result.Status == ResultStatus.Error
            && evaluation.ReferencePlane is null
            && evaluation.Result.Message.Contains("span two horizontal axes", StringComparison.OrdinalIgnoreCase)
            && evaluation.Result.Overlays.Count == 0;
        return (passed, Evidence(evaluation, 1.0, double.NaN));
    }

    private static DualSurfaceThicknessEvaluation Evaluate(
        double slopeX,
        double slopeZ,
        double hGap,
        IReadOnlyList<(double X, double Z)> referencePositions,
        IReadOnlyList<(double X, double Z)> measurementPositions,
        double minimum,
        double maximum,
        int minimumValidSamples = 1) =>
        Evaluate(
            referencePositions.Select(position => CreateSample(slopeX, slopeZ, position.X, position.Z, 0.0)).ToArray(),
            measurementPositions.Select(position => CreateSample(slopeX, slopeZ, position.X, position.Z, hGap)).ToArray(),
            minimum,
            maximum,
            minimumValidSamples);

    private static DualSurfaceThicknessEvaluation Evaluate(
        IReadOnlyList<HeightFieldPlaneSample> reference,
        IReadOnlyList<HeightFieldPlaneSample> measurement,
        double minimum,
        double maximum,
        int minimumValidSamples) =>
        DualSurfaceThicknessRule.Evaluate(new DualSurfaceThicknessInput(
            "source.synthetic-thickness-h-axis",
            reference,
            measurement,
            minimum,
            maximum,
            minimumValidSamples,
            Unit));

    private static HeightFieldPlaneSample CreateSample(
        double slopeX,
        double slopeZ,
        double x,
        double z,
        double hOffset)
    {
        var rawHeight = EvaluateReferenceHeight(slopeX, slopeZ, x, z) + hOffset;
        return new HeightFieldPlaneSample(
            new Vector3((float)x, (float)rawHeight, (float)z),
            rawHeight);
    }

    private static double EvaluateReferenceHeight(double slopeX, double slopeZ, double x, double z) =>
        (slopeX * x) + (slopeZ * z) + Intercept;

    private static (double X, double Z)[] Grid(double minX, double maxX, double minZ, double maxZ) =>
        new[]
        {
            (minX, minZ),
            (maxX, minZ),
            (minX, maxZ),
            (maxX, maxZ)
        };

    private static double NormalDistance(double hGap, double slopeX, double slopeZ) =>
        hGap / Math.Sqrt(1.0 + (slopeX * slopeX) + (slopeZ * slopeZ));

    private static double Metric(DualSurfaceThicknessEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.FirstOrDefault(item => item.Name.Equals(name, StringComparison.Ordinal))?.Value ?? double.NaN;

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

    private static string Evidence(DualSurfaceThicknessEvaluation evaluation, double hGap, double normalDistance) =>
        $"status={evaluation.Result.Status},mean={Format(evaluation.Mean)},min={Format(evaluation.Minimum)},max={Format(evaluation.Maximum)},range={Format(evaluation.Range)},measurementCount={evaluation.MeasurementSampleCount},reference={evaluation.ReferenceSampleCount},message={evaluation.Result.Message},expectedH={Format(hGap)},expectedNormal={Format(normalDistance)}";

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && double.IsFinite(expected) && Math.Abs(actual - expected) <= tolerance;

    private static string Format(double value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

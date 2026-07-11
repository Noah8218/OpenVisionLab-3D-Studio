using System.Globalization;
using System.Numerics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class PlaneFlatnessGoldenVerification
{
    private const double SlopeX = 0.5;
    private const double SlopeZ = -0.25;
    private const double Intercept = 2.0;
    private const double ExpectedMinimum = -0.4;
    private const double ExpectedMaximum = 0.6;
    private const double ExpectedFlatness = 1.0;
    private static readonly double ExpectedRms = Math.Sqrt(0.114);

    public static int Run(string reportPath)
    {
        var referenceSamples = CreateReferenceSamples();
        var measurementSamples = CreateMeasurementSamples();
        var passingEvaluation = Evaluate(referenceSamples, measurementSamples, tolerance: 1.01);
        var failingEvaluation = Evaluate(referenceSamples, measurementSamples, tolerance: 0.99);
        var cases = new[]
        {
            Check("exact-plane-fit", () => VerifyPlaneFit(passingEvaluation)),
            Check("known-flatness-pass", () => VerifyKnownFlatness(passingEvaluation, ResultStatus.Pass)),
            Check("known-flatness-fail", () => VerifyKnownFlatness(failingEvaluation, ResultStatus.Fail)),
            Check("empty-reference-error", () => VerifyError(Evaluate([], measurementSamples, 1.01), "at least three")),
            Check("insufficient-reference-error", () => VerifyError(Evaluate(referenceSamples[..2], measurementSamples, 1.01), "at least three")),
            Check("degenerate-reference-error", () => VerifyError(Evaluate(CreateDegenerateReferenceSamples(), measurementSamples, 1.01), "span two horizontal axes")),
            Check("nonfinite-reference-error", () => VerifyError(Evaluate(CreateNonFiniteReferenceSamples(), measurementSamples, 1.01), "finite coordinates")),
            Check("nonfinite-measurement-error", () => VerifyError(Evaluate(referenceSamples, CreateNonFiniteMeasurementSamples(), 1.01), "non-finite")),
            Check("invalid-tolerance-error", () => VerifyError(Evaluate(referenceSamples, measurementSamples, 0.0), "positive finite"))
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"PlaneFlatnessGoldenVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
            $"GoldenPlane|equation=y={Format(SlopeX)}x{FormatSigned(SlopeZ)}z{FormatSigned(Intercept)}|expectedMin={Format(ExpectedMinimum)}|expectedMax={Format(ExpectedMaximum)}|expectedFlatness={Format(ExpectedFlatness)}|expectedRms={Format(ExpectedRms)}"
        };
        lines.AddRange(cases.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Plane flatness golden verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyPlaneFit(PlaneFlatnessEvaluation evaluation)
    {
        var plane = evaluation.ReferencePlane;
        var passed = plane is not null
            && Approximately(plane.SlopeX, SlopeX)
            && Approximately(plane.SlopeZ, SlopeZ)
            && Approximately(plane.Intercept, Intercept)
            && Approximately(plane.RootMeanSquareDistance, 0.0);
        var evidence = plane is null
            ? "reference plane missing"
            : $"slopeX={Format(plane.SlopeX)},slopeZ={Format(plane.SlopeZ)},intercept={Format(plane.Intercept)},fitRms={Format(plane.RootMeanSquareDistance)}";
        return (passed, evidence);
    }

    private static (bool Passed, string Evidence) VerifyKnownFlatness(
        PlaneFlatnessEvaluation evaluation,
        ResultStatus expectedStatus)
    {
        var passed = evaluation.Result.Status == expectedStatus
            && Approximately(evaluation.MinimumSignedDistance, ExpectedMinimum)
            && Approximately(evaluation.MaximumSignedDistance, ExpectedMaximum)
            && Approximately(evaluation.Flatness, ExpectedFlatness)
            && Approximately(evaluation.RootMeanSquareDistance, ExpectedRms)
            && evaluation.ReferenceSampleCount == 25
            && evaluation.MeasurementSampleCount == 5;
        return (
            passed,
            $"status={evaluation.Result.Status},min={Format(evaluation.MinimumSignedDistance)},max={Format(evaluation.MaximumSignedDistance)},flatness={Format(evaluation.Flatness)},rms={Format(evaluation.RootMeanSquareDistance)},reference={evaluation.ReferenceSampleCount},measured={evaluation.MeasurementSampleCount}");
    }

    private static (bool Passed, string Evidence) VerifyError(
        PlaneFlatnessEvaluation evaluation,
        string expectedMessageFragment)
    {
        var passed = evaluation.Result.Status == ResultStatus.Error
            && evaluation.ReferencePlane is null
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

    private static PlaneFlatnessEvaluation Evaluate(
        IReadOnlyList<HeightFieldPlaneSample> referenceSamples,
        IReadOnlyList<HeightFieldPlaneSample> measurementSamples,
        double tolerance) =>
        PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
            "source.synthetic-plane",
            referenceSamples,
            measurementSamples,
            tolerance,
            "model"));

    private static HeightFieldPlaneSample[] CreateReferenceSamples()
    {
        var samples = new List<HeightFieldPlaneSample>(25);
        for (var z = -2; z <= 2; z++)
        {
            for (var x = -2; x <= 2; x++)
            {
                var point = PointOnPlane(x, z);
                samples.Add(new HeightFieldPlaneSample(point, point.Y));
            }
        }

        return samples.ToArray();
    }

    private static HeightFieldPlaneSample[] CreateMeasurementSamples()
    {
        var normal = Vector3.Normalize(new Vector3((float)-SlopeX, 1.0f, (float)-SlopeZ));
        var definitions = new (double X, double Z, double Offset)[]
        {
            (-1.5, -1.5, ExpectedMinimum),
            (-0.5, 0.5, -0.1),
            (0.0, 0.0, 0.0),
            (0.75, -0.5, 0.2),
            (1.5, 1.25, ExpectedMaximum)
        };
        return definitions
            .Select(item =>
            {
                var point = PointOnPlane(item.X, item.Z) + normal * (float)item.Offset;
                return new HeightFieldPlaneSample(point, point.Y);
            })
            .ToArray();
    }

    private static HeightFieldPlaneSample[] CreateDegenerateReferenceSamples() =>
        new[] { -1.0, 0.0, 1.0 }
            .Select(x =>
            {
                var point = PointOnPlane(x, 0.0);
                return new HeightFieldPlaneSample(point, point.Y);
            })
            .ToArray();

    private static HeightFieldPlaneSample[] CreateNonFiniteReferenceSamples()
    {
        var samples = CreateReferenceSamples();
        samples[0] = new HeightFieldPlaneSample(new Vector3(float.NaN, 0.0f, 0.0f), 0.0);
        return samples;
    }

    private static HeightFieldPlaneSample[] CreateNonFiniteMeasurementSamples() =>
        CreateMeasurementSamples()
            .Append(new HeightFieldPlaneSample(new Vector3(float.PositiveInfinity, 0.0f, 0.0f), 0.0))
            .ToArray();

    private static Vector3 PointOnPlane(double x, double z) =>
        new((float)x, (float)(SlopeX * x + SlopeZ * z + Intercept), (float)z);

    private static bool Approximately(double actual, double expected, double tolerance = 1e-5) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Format(double value) =>
        value.ToString("F6", CultureInfo.InvariantCulture);

    private static string FormatSigned(double value) =>
        value.ToString("+0.######;-0.######;+0", CultureInfo.InvariantCulture);

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

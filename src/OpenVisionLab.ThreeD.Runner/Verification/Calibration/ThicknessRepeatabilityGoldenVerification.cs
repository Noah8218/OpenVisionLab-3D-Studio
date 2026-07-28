using System.Globalization;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class ThicknessRepeatabilityGoldenVerification
{
    private const string Unit = "mm";
    private const string FrameId = "frame.synthetic-repeatability";
    private static readonly DateTimeOffset FirstCapture =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
    private static readonly double[] KnownValues = [10.0, 12.0, 14.0, 16.0];
    private static readonly double ExpectedSampleStandardDeviation = Math.Sqrt(20.0 / 3.0);

    public static int Run(string reportPath)
    {
        var passing = Evaluate(KnownValues, Acceptance(2.6, 6.0));
        var cases = new[]
        {
            Check("known-statistics-pass", () => VerifyKnownStatistics(passing)),
            Check("sample-standard-deviation-n-minus-one", () => VerifySampleStandardDeviation(passing)),
            Check("six-sigma-spread", () => VerifySixSigmaSpread(passing)),
            Check("exact-thresholds-pass", VerifyExactThresholds),
            Check("zero-variation-zero-thresholds-pass", VerifyZeroVariation),
            Check("standard-deviation-only-fail", () => VerifyDecision(
                Evaluate(KnownValues, Acceptance(2.5, 6.0)),
                ResultStatus.Fail,
                ThicknessRepeatabilityDecision.SampleStandardDeviationExceeded,
                ResultStatus.Fail,
                ResultStatus.Pass)),
            Check("range-only-fail", () => VerifyDecision(
                Evaluate(KnownValues, Acceptance(2.6, 5.9)),
                ResultStatus.Fail,
                ThicknessRepeatabilityDecision.RangeExceeded,
                ResultStatus.Pass,
                ResultStatus.Fail)),
            Check("both-limits-fail", () => VerifyDecision(
                Evaluate(KnownValues, Acceptance(2.5, 5.9)),
                ResultStatus.Fail,
                ThicknessRepeatabilityDecision.SampleStandardDeviationAndRangeExceeded,
                ResultStatus.Fail,
                ResultStatus.Fail)),
            Check("metric-order-and-status", () => VerifyMetrics(passing)),
            Check("stable-large-offset-statistics", VerifyLargeOffset),
            Check("same-setup-is-not-gauge-rr", () => VerifyNotGaugeRrMessage(passing)),
            Check("validated-run-order-is-preserved", VerifyRunOrder),
            Check("null-input-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(null),
                ThicknessRepeatabilityDecision.InvalidInput,
                "input is required")),
            Check("missing-study-id-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(CreateInput(KnownValues) with { StudyId = " " }),
                ThicknessRepeatabilityDecision.InvalidInput,
                "Study ID")),
            Check("missing-measurement-definition-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(CreateInput(KnownValues) with { MeasurementDefinitionId = "" }),
                ThicknessRepeatabilityDecision.InvalidInput,
                "Measurement definition")),
            Check("missing-reference-roi-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(CreateInput(KnownValues) with { ReferenceRoiId = "" }),
                ThicknessRepeatabilityDecision.InvalidInput,
                "Reference ROI")),
            Check("missing-study-unit-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(CreateInput(KnownValues) with { Unit = "" }),
                ThicknessRepeatabilityDecision.InvalidInput,
                "unit is required")),
            Check("missing-study-frame-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(CreateInput(KnownValues) with { FrameId = "" }),
                ThicknessRepeatabilityDecision.InvalidInput,
                "frame ID")),
            Check("missing-acceptance-policy-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(CreateInput(KnownValues) with { Acceptance = null }),
                ThicknessRepeatabilityDecision.InvalidAcceptancePolicy,
                "policy is required")),
            Check("invalid-minimum-run-count-error", () => VerifyError(
                Evaluate(KnownValues, new ThicknessRepeatabilityAcceptance(1, 2.6, 6.0)),
                ThicknessRepeatabilityDecision.InvalidAcceptancePolicy,
                "at least two")),
            Check("invalid-standard-deviation-limits-error", VerifyInvalidStandardDeviationLimits),
            Check("invalid-range-limits-error", VerifyInvalidRangeLimits),
            Check("null-runs-error", () => VerifyError(
                ThicknessRepeatabilityRule.Evaluate(CreateInput(KnownValues) with { Runs = null }),
                ThicknessRepeatabilityDecision.InvalidInput,
                "runs are required")),
            Check("insufficient-runs-error", VerifyInsufficientRuns),
            Check("null-run-error", VerifyNullRun),
            Check("missing-run-id-error", () => VerifyChangedRunError(
                run => run with { RunId = "" },
                "no run ID")),
            Check("missing-source-id-error", () => VerifyChangedRunError(
                run => run with { SourceEntityId = " " },
                "no source entity ID")),
            Check("duplicate-run-id-error", VerifyDuplicateRunId),
            Check("duplicate-source-id-error", VerifyDuplicateSourceId),
            Check("missing-capture-time-error", () => VerifyChangedRunError(
                run => run with { CapturedAt = default },
                "no capture timestamp")),
            Check("run-unit-mismatch-error", () => VerifyChangedRunError(
                run => run with { Unit = "um" },
                "unit does not match")),
            Check("run-frame-mismatch-error", () => VerifyChangedRunError(
                run => run with { FrameId = "frame.other" },
                "frame does not match")),
            Check("nonfinite-thickness-error", VerifyNonFiniteThickness),
            Check("statistics-overflow-error", () => VerifyError(
                Evaluate([1e308, -1e308], Acceptance(double.MaxValue, double.MaxValue, 2)),
                ThicknessRepeatabilityDecision.InvalidInput,
                "overflow"))
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"ThicknessRepeatabilityGoldenVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
            $"Contract|order={ThicknessRepeatabilityRule.EvaluationOrder}|standardDeviation=sample-n-minus-one|sixSigmaSpread=6xs|sourceReuseRejected=True|gaugeRrClaim=False",
            $"Golden|values={string.Join(',', KnownValues.Select(Format))}|mean=13|minimum=10|maximum=16|sampleStandardDeviation={Format(ExpectedSampleStandardDeviation)}|range=6"
        };
        lines.AddRange(cases.Select(item =>
            $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        Console.WriteLine($"Thickness repeatability golden verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyKnownStatistics(
        ThicknessRepeatabilityEvaluation evaluation)
    {
        var validation = ThicknessRepeatabilityRule.Validate(evaluation.Input);
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Decision == ThicknessRepeatabilityDecision.Accepted
            && validation.IsReady
            && validation.State == ThicknessRepeatabilityInputState.Ready
            && evaluation.RunCount == 4
            && Approximately(evaluation.Mean, 13.0)
            && Approximately(evaluation.Minimum, 10.0)
            && Approximately(evaluation.Maximum, 16.0)
            && Approximately(evaluation.Range, 6.0);
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifySampleStandardDeviation(
        ThicknessRepeatabilityEvaluation evaluation)
    {
        var populationStandardDeviation = Math.Sqrt(5.0);
        var passed = Approximately(evaluation.SampleStandardDeviation, ExpectedSampleStandardDeviation)
            && !Approximately(evaluation.SampleStandardDeviation, populationStandardDeviation);
        return Evidence(
            evaluation,
            passed,
            $"expectedSample={Format(ExpectedSampleStandardDeviation)},population={Format(populationStandardDeviation)}");
    }

    private static (bool Passed, string Evidence) VerifySixSigmaSpread(
        ThicknessRepeatabilityEvaluation evaluation)
    {
        var expected = 6.0 * ExpectedSampleStandardDeviation;
        return Evidence(
            evaluation,
            Approximately(evaluation.SixSigmaSpread, expected),
            $"expectedSixSigma={Format(expected)}");
    }

    private static (bool Passed, string Evidence) VerifyExactThresholds()
    {
        var evaluation = Evaluate(
            KnownValues,
            Acceptance(ExpectedSampleStandardDeviation, 6.0));
        return VerifyDecision(
            evaluation,
            ResultStatus.Pass,
            ThicknessRepeatabilityDecision.Accepted,
            ResultStatus.Pass,
            ResultStatus.Pass);
    }

    private static (bool Passed, string Evidence) VerifyZeroVariation()
    {
        var evaluation = Evaluate([5.0, 5.0, 5.0], Acceptance(0.0, 0.0, 3));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Decision == ThicknessRepeatabilityDecision.Accepted
            && evaluation.SampleStandardDeviation == 0.0
            && evaluation.SixSigmaSpread == 0.0
            && evaluation.Range == 0.0;
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyMetrics(
        ThicknessRepeatabilityEvaluation evaluation)
    {
        var expectedNames = new[]
        {
            "Run count",
            "Minimum run count",
            "Mean thickness",
            "Minimum thickness",
            "Maximum thickness",
            "Sample standard deviation",
            "Maximum sample standard deviation",
            "Six-sigma spread",
            "Range",
            "Maximum range"
        };
        var passed = evaluation.Result.Metrics.Select(metric => metric.Name)
                .SequenceEqual(expectedNames, StringComparer.Ordinal)
            && Status(evaluation, "Run count") == ResultStatus.Pass
            && Status(evaluation, "Sample standard deviation") == ResultStatus.Pass
            && Status(evaluation, "Range") == ResultStatus.Pass
            && Metric(evaluation, "Mean thickness").Unit == Unit
            && evaluation.Result.Overlays.Count == 0;
        return Evidence(evaluation, passed, $"metrics={evaluation.Result.Metrics.Count},overlays={evaluation.Result.Overlays.Count}");
    }

    private static (bool Passed, string Evidence) VerifyLargeOffset()
    {
        var evaluation = Evaluate(
            [1_000_000_000_000.0, 1_000_000_000_001.0, 1_000_000_000_002.0],
            Acceptance(1.0, 2.0, 3));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && Approximately(evaluation.Mean, 1_000_000_000_001.0)
            && Approximately(evaluation.SampleStandardDeviation, 1.0)
            && Approximately(evaluation.Range, 2.0);
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyNotGaugeRrMessage(
        ThicknessRepeatabilityEvaluation evaluation)
    {
        var failed = Evaluate(KnownValues, Acceptance(2.5, 5.9));
        var passed = evaluation.Result.Message.Contains("not Gauge R&R", StringComparison.Ordinal)
            && failed.Result.Message.Contains("not Gauge R&R", StringComparison.Ordinal);
        return (passed, $"passMessage={evaluation.Result.Message},failMessage={failed.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyRunOrder()
    {
        var input = CreateInput(KnownValues);
        var evaluation = ThicknessRepeatabilityRule.Evaluate(input);
        var expected = new[] { "run.001", "run.002", "run.003", "run.004" };
        var mutableInputRuns = (ThicknessRepeatabilityRun[])input.Runs!;
        mutableInputRuns[0] = mutableInputRuns[0] with { RunId = "run.mutated" };
        var passed = evaluation.Runs.Select(run => run.RunId).SequenceEqual(expected, StringComparer.Ordinal)
            && evaluation.Input!.Runs!.Select(run => run.RunId).SequenceEqual(expected, StringComparer.Ordinal)
            && !ReferenceEquals(evaluation.Runs, input.Runs)
            && !ReferenceEquals(evaluation.Input.Runs, input.Runs);
        return Evidence(evaluation, passed, string.Join(',', evaluation.Runs.Select(run => run.RunId)));
    }

    private static (bool Passed, string Evidence) VerifyInvalidStandardDeviationLimits()
    {
        var negative = Evaluate(KnownValues, Acceptance(-0.001, 6.0));
        var nonfinite = Evaluate(KnownValues, Acceptance(double.NaN, 6.0));
        var passed = IsError(negative, ThicknessRepeatabilityDecision.InvalidAcceptancePolicy, "non-negative finite")
            && IsError(nonfinite, ThicknessRepeatabilityDecision.InvalidAcceptancePolicy, "non-negative finite");
        return (passed, $"negative={negative.Decision},nonfinite={nonfinite.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidRangeLimits()
    {
        var negative = Evaluate(KnownValues, Acceptance(2.6, -0.001));
        var nonfinite = Evaluate(KnownValues, Acceptance(2.6, double.PositiveInfinity));
        var passed = IsError(negative, ThicknessRepeatabilityDecision.InvalidAcceptancePolicy, "non-negative finite")
            && IsError(nonfinite, ThicknessRepeatabilityDecision.InvalidAcceptancePolicy, "non-negative finite");
        return (passed, $"negative={negative.Decision},nonfinite={nonfinite.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyInsufficientRuns()
    {
        var input = CreateInput([10.0, 10.1], Acceptance(1.0, 1.0, 3));
        var validation = ThicknessRepeatabilityRule.Validate(input);
        var evaluation = ThicknessRepeatabilityRule.Evaluate(input);
        var passed = IsError(evaluation, ThicknessRepeatabilityDecision.InsufficientRuns, "At least 3")
            && !validation.IsReady
            && validation.State == ThicknessRepeatabilityInputState.InsufficientRuns
            && evaluation.RunCount == 2
            && double.IsNaN(evaluation.Mean)
            && double.IsNaN(evaluation.SampleStandardDeviation);
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyNullRun()
    {
        var input = CreateInput(KnownValues);
        var runs = input.Runs!.ToArray();
        runs[1] = null!;
        return VerifyError(
            ThicknessRepeatabilityRule.Evaluate(input with { Runs = runs }),
            ThicknessRepeatabilityDecision.InvalidInput,
            "Run 2 is null");
    }

    private static (bool Passed, string Evidence) VerifyChangedRunError(
        Func<ThicknessRepeatabilityRun, ThicknessRepeatabilityRun> change,
        string expectedMessage)
    {
        var input = CreateInput(KnownValues);
        var runs = input.Runs!.ToArray();
        runs[0] = change(runs[0]);
        return VerifyError(
            ThicknessRepeatabilityRule.Evaluate(input with { Runs = runs }),
            ThicknessRepeatabilityDecision.InvalidInput,
            expectedMessage);
    }

    private static (bool Passed, string Evidence) VerifyDuplicateRunId()
    {
        var input = CreateInput(KnownValues);
        var runs = input.Runs!.ToArray();
        runs[1] = runs[1] with { RunId = runs[0].RunId };
        return VerifyError(
            ThicknessRepeatabilityRule.Evaluate(input with { Runs = runs }),
            ThicknessRepeatabilityDecision.InvalidInput,
            "Duplicate run ID");
    }

    private static (bool Passed, string Evidence) VerifyDuplicateSourceId()
    {
        var input = CreateInput(KnownValues);
        var runs = input.Runs!.ToArray();
        runs[1] = runs[1] with { SourceEntityId = runs[0].SourceEntityId };
        return VerifyError(
            ThicknessRepeatabilityRule.Evaluate(input with { Runs = runs }),
            ThicknessRepeatabilityDecision.InvalidInput,
            "cannot count as more than one acquisition");
    }

    private static (bool Passed, string Evidence) VerifyNonFiniteThickness()
    {
        var nan = VerifyChangedRunError(run => run with { Thickness = double.NaN }, "must be finite");
        var infinity = VerifyChangedRunError(run => run with { Thickness = double.PositiveInfinity }, "must be finite");
        return (nan.Passed && infinity.Passed, $"nan={nan.Evidence},infinity={infinity.Evidence}");
    }

    private static (bool Passed, string Evidence) VerifyDecision(
        ThicknessRepeatabilityEvaluation evaluation,
        ResultStatus expectedStatus,
        ThicknessRepeatabilityDecision expectedDecision,
        ResultStatus expectedStandardDeviationStatus,
        ResultStatus expectedRangeStatus)
    {
        var passed = evaluation.Result.Status == expectedStatus
            && evaluation.Decision == expectedDecision
            && Status(evaluation, "Sample standard deviation") == expectedStandardDeviationStatus
            && Status(evaluation, "Range") == expectedRangeStatus;
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyError(
        ThicknessRepeatabilityEvaluation evaluation,
        ThicknessRepeatabilityDecision expectedDecision,
        string expectedMessage)
    {
        var passed = IsError(evaluation, expectedDecision, expectedMessage)
            && evaluation.Result.Metrics.All(metric => metric.Status == ResultStatus.Error)
            && double.IsNaN(evaluation.Mean)
            && double.IsNaN(evaluation.SampleStandardDeviation)
            && double.IsNaN(evaluation.Range);
        return Evidence(evaluation, passed);
    }

    private static bool IsError(
        ThicknessRepeatabilityEvaluation evaluation,
        ThicknessRepeatabilityDecision expectedDecision,
        string expectedMessage) =>
        evaluation.Result.Status == ResultStatus.Error
        && evaluation.Decision == expectedDecision
        && evaluation.Result.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase);

    private static ThicknessRepeatabilityEvaluation Evaluate(
        IReadOnlyList<double> values,
        ThicknessRepeatabilityAcceptance? acceptance = null) =>
        ThicknessRepeatabilityRule.Evaluate(CreateInput(values, acceptance));

    private static ThicknessRepeatabilityInput CreateInput(
        IReadOnlyList<double> values,
        ThicknessRepeatabilityAcceptance? acceptance = null)
    {
        var runs = values
            .Select((value, index) => new ThicknessRepeatabilityRun(
                $"run.{index + 1:000}",
                $"source.synthetic.{index + 1:000}",
                FirstCapture.AddMinutes(index),
                Unit,
                FrameId,
                value))
            .ToArray();
        return new ThicknessRepeatabilityInput(
            "study.synthetic-thickness-repeatability",
            "measurement.synthetic-thickness",
            "roi.synthetic-reference",
            Unit,
            FrameId,
            runs,
            acceptance ?? Acceptance(2.6, 6.0, Math.Max(2, values.Count)));
    }

    private static ThicknessRepeatabilityAcceptance Acceptance(
        double maximumStandardDeviation,
        double maximumRange,
        int minimumRunCount = 4) =>
        new(minimumRunCount, maximumStandardDeviation, maximumRange);

    private static Metric Metric(ThicknessRepeatabilityEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.Single(metric => metric.Name == name);

    private static ResultStatus? Status(ThicknessRepeatabilityEvaluation evaluation, string name) =>
        Metric(evaluation, name).Status;

    private static (bool Passed, string Evidence) Evidence(
        ThicknessRepeatabilityEvaluation evaluation,
        bool passed,
        string? suffix = null)
    {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"status={evaluation.Result.Status},decision={evaluation.Decision},count={evaluation.RunCount},mean={evaluation.Mean:R},sampleStandardDeviation={evaluation.SampleStandardDeviation:R},range={evaluation.Range:R}");
        return (passed, suffix is null ? text : $"{text},{suffix}");
    }

    private static VerificationCase Check(
        string name,
        Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            var result = verify();
            return new VerificationCase(name, result.Passed, result.Evidence);
        }
        catch (Exception exception)
        {
            return new VerificationCase(
                name,
                false,
                $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }

    private static bool Approximately(double actual, double expected, double tolerance = 1e-12) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Format(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

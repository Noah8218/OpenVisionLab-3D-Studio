using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class AlignedPointRepeatabilityGoldenVerification
{
    private const string Unit = "mm";
    private const string FrameId = "frame.synthetic-aligned-repeatability";
    private const string AlignmentReferenceId = "alignment.synthetic-fixture";
    private const string CorrespondenceDefinitionId = "correspondence.synthetic-grid";
    private static readonly DateTimeOffset FirstCapture =
        new(2026, 7, 17, 2, 0, 0, TimeSpan.Zero);
    private static readonly double[][] KnownRunValues =
    [
        [10.0, 20.0, 30.0],
        [12.0, 20.0, 31.0],
        [14.0, 20.0, 33.0]
    ];

    public static int Run(string reportPath)
    {
        var passing = Evaluate(KnownRunValues, Acceptance(2.0, 4.0));
        var cases = new[]
        {
            Check("known-per-point-statistics-pass", () => VerifyKnownStatistics(passing)),
            Check("exact-thresholds-pass", () => VerifyDecision(
                Evaluate(KnownRunValues, Acceptance(2.0, 4.0)),
                ResultStatus.Pass,
                AlignedPointRepeatabilityDecision.Accepted,
                ResultStatus.Pass,
                ResultStatus.Pass)),
            Check("standard-deviation-only-fail", () => VerifyDecision(
                Evaluate(KnownRunValues, Acceptance(1.99, 4.0)),
                ResultStatus.Fail,
                AlignedPointRepeatabilityDecision.SampleStandardDeviationExceeded,
                ResultStatus.Fail,
                ResultStatus.Pass)),
            Check("range-only-fail", () => VerifyDecision(
                Evaluate(KnownRunValues, Acceptance(2.0, 3.99)),
                ResultStatus.Fail,
                AlignedPointRepeatabilityDecision.RangeExceeded,
                ResultStatus.Pass,
                ResultStatus.Fail)),
            Check("both-limits-fail", () => VerifyDecision(
                Evaluate(KnownRunValues, Acceptance(1.99, 3.99)),
                ResultStatus.Fail,
                AlignedPointRepeatabilityDecision.SampleStandardDeviationAndRangeExceeded,
                ResultStatus.Fail,
                ResultStatus.Fail)),
            Check("point-result-order-coordinates-and-snapshots", VerifyPointOrderCoordinatesAndSnapshots),
            Check("metrics-and-failing-point-status", VerifyMetricsAndFailingPointStatus),
            Check("same-setup-is-not-gauge-rr", () => VerifyNotGaugeRrMessage(passing)),
            Check("null-input-error", () => VerifyError(
                AlignedPointRepeatabilityRule.Evaluate(null),
                AlignedPointRepeatabilityDecision.InvalidInput,
                "input is required")),
            Check("missing-study-id-error", () => VerifyError(
                AlignedPointRepeatabilityRule.Evaluate(CreateInput(KnownRunValues) with { StudyId = " " }),
                AlignedPointRepeatabilityDecision.InvalidInput,
                "Study ID")),
            Check("missing-alignment-reference-error", () => VerifyError(
                AlignedPointRepeatabilityRule.Evaluate(CreateInput(KnownRunValues) with { AlignmentReferenceId = "" }),
                AlignedPointRepeatabilityDecision.InvalidInput,
                "Alignment reference ID")),
            Check("missing-correspondence-definition-error", () => VerifyError(
                AlignedPointRepeatabilityRule.Evaluate(CreateInput(KnownRunValues) with { CorrespondenceDefinitionId = "" }),
                AlignedPointRepeatabilityDecision.InvalidInput,
                "Correspondence definition ID")),
            Check("missing-acceptance-policy-error", () => VerifyError(
                AlignedPointRepeatabilityRule.Evaluate(CreateInput(KnownRunValues) with { Acceptance = null }),
                AlignedPointRepeatabilityDecision.InvalidAcceptancePolicy,
                "policy is required")),
            Check("invalid-minimum-counts-error", VerifyInvalidMinimumCounts),
            Check("invalid-limits-error", VerifyInvalidLimits),
            Check("null-reference-points-error", () => VerifyError(
                AlignedPointRepeatabilityRule.Evaluate(CreateInput(KnownRunValues) with { ReferencePoints = null }),
                AlignedPointRepeatabilityDecision.InvalidInput,
                "Reference correspondence points")),
            Check("duplicate-reference-point-error", VerifyDuplicateReferencePoint),
            Check("nonfinite-reference-coordinate-error", VerifyNonFiniteReferenceCoordinate),
            Check("null-runs-error", () => VerifyError(
                AlignedPointRepeatabilityRule.Evaluate(CreateInput(KnownRunValues) with { Runs = null }),
                AlignedPointRepeatabilityDecision.InvalidInput,
                "runs are required")),
            Check("insufficient-runs-error", VerifyInsufficientRuns),
            Check("insufficient-correspondences-error", VerifyInsufficientCorrespondences),
            Check("duplicate-run-id-error", () => VerifyChangedRunError(
                run => run with { RunId = "run.synthetic.001" },
                "Run ID is duplicated")),
            Check("duplicate-source-id-error", () => VerifyChangedRunError(
                run => run with { SourceEntityId = "source.synthetic.001" },
                "Source entity ID is duplicated")),
            Check("invalid-source-hash-error", () => VerifyChangedRunError(
                run => run with { SourceSha256 = "not-a-sha" },
                "64-character hexadecimal")),
            Check("byte-identical-source-error", () => VerifyChangedRunError(
                run => run with { SourceSha256 = SourceHash(1) },
                "Byte-identical source SHA-256")),
            Check("source-length-error", () => VerifyChangedRunError(
                run => run with { SourceByteLength = 0 },
                "positive source byte length")),
            Check("unit-frame-alignment-mismatch-errors", VerifyUnitFrameAndAlignmentMismatch),
            Check("alignment-method-evidence-required", VerifyAlignmentMethodAndEvidenceRequired),
            Check("duplicate-observation-error", VerifyDuplicateObservation),
            Check("incomplete-correspondence-coverage-error", VerifyIncompleteCoverage),
            Check("unexpected-correspondence-coverage-error", VerifyUnexpectedCoverage),
            Check("nonfinite-observation-error", VerifyNonFiniteObservation),
            Check("statistics-overflow-error", VerifyStatisticsOverflow)
        };

        var passedCount = cases.Count(item => item.Passed);
        var status = passedCount == cases.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"AlignedPointRepeatabilityGoldenVerification|{status}|cases={cases.Length}|passed={passedCount}|failed={cases.Length - passedCount}",
            $"Contract|order={AlignedPointRepeatabilityRule.EvaluationOrder}|coverage=all-declared-reference-points|sourceSha256Distinct=True|alignmentEvidenceRequired=True|gaugeRrClaim=False|physicalCalibrationClaim=False",
            $"Golden|maxPointSampleStandardDeviation=2|maxPointRange=4|runs={KnownRunValues.Length}|correspondences=3|unit={Unit}"
        };
        lines.AddRange(cases.Select(item =>
            $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{Clean(item.Evidence)}"));

        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        Console.WriteLine($"Aligned point repeatability golden verification: {status} ({passedCount}/{cases.Length})");
        return passedCount == cases.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyKnownStatistics(
        AlignedPointRepeatabilityEvaluation evaluation)
    {
        var alpha = Point(evaluation, "point.alpha");
        var beta = Point(evaluation, "point.beta");
        var gamma = Point(evaluation, "point.gamma");
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.Decision == AlignedPointRepeatabilityDecision.Accepted
            && evaluation.RunCount == 3
            && evaluation.CorrespondenceCount == 3
            && evaluation.FailingCorrespondenceCount == 0
            && Approximately(alpha.Mean, 12.0)
            && Approximately(alpha.SampleStandardDeviation, 2.0)
            && Approximately(alpha.Range, 4.0)
            && Approximately(beta.Mean, 20.0)
            && Approximately(beta.SampleStandardDeviation, 0.0)
            && Approximately(gamma.Mean, 94.0 / 3.0)
            && Approximately(gamma.SampleStandardDeviation, Math.Sqrt(7.0 / 3.0))
            && Approximately(gamma.Range, 3.0)
            && Approximately(evaluation.MaximumSampleStandardDeviation, 2.0)
            && Approximately(evaluation.MaximumRange, 4.0);
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyPointOrderCoordinatesAndSnapshots()
    {
        var input = CreateInput(KnownRunValues);
        var evaluation = AlignedPointRepeatabilityRule.Evaluate(input);
        var referencePoints = (AlignedPointRepeatabilityReferencePoint[])input.ReferencePoints!;
        var runs = (AlignedPointRepeatabilityRun[])input.Runs!;
        var observations = (AlignedPointRepeatabilityObservation[])runs[0].Observations!;
        referencePoints[0] = referencePoints[0] with { CorrespondenceId = "point.mutated" };
        runs[0] = runs[0] with { RunId = "run.mutated" };
        observations[0] = observations[0] with { Value = 999.0 };

        var snapshotInput = evaluation.Input!;
        var snapshotRuns = snapshotInput.Runs!;
        var orderedIds = evaluation.PointEvaluations
            .Select(point => point.ReferencePoint.CorrespondenceId)
            .ToArray();
        var alpha = Point(evaluation, "point.alpha");
        var passed = orderedIds.SequenceEqual(
                ["point.alpha", "point.beta", "point.gamma"],
                StringComparer.Ordinal)
            && Approximately(alpha.ReferencePoint.AlignedX, 0.0)
            && Approximately(alpha.ReferencePoint.AlignedY, 0.0)
            && Approximately(alpha.ReferencePoint.AlignedZ, 0.0)
            && snapshotInput.ReferencePoints!.Any(point => point.CorrespondenceId == "point.gamma")
            && snapshotRuns.First().RunId == "run.synthetic.001"
            && snapshotRuns.First().Observations!.First().Value == 30.0;
        return Evidence(evaluation, passed, string.Join(',', orderedIds));
    }

    private static (bool Passed, string Evidence) VerifyMetricsAndFailingPointStatus()
    {
        var evaluation = Evaluate(KnownRunValues, Acceptance(1.99, 3.99));
        var expectedNames = new[]
        {
            "Run count",
            "Minimum run count",
            "Correspondence count",
            "Minimum correspondence count",
            "Maximum point sample standard deviation",
            "Maximum allowed point sample standard deviation",
            "Maximum point range",
            "Maximum allowed point range",
            "Failing correspondence count"
        };
        var alpha = Point(evaluation, "point.alpha");
        var beta = Point(evaluation, "point.beta");
        var gamma = Point(evaluation, "point.gamma");
        var passed = evaluation.Result.Metrics.Select(metric => metric.Name)
                .SequenceEqual(expectedNames, StringComparer.Ordinal)
            && Status(evaluation, "Maximum point sample standard deviation") == ResultStatus.Fail
            && Status(evaluation, "Maximum point range") == ResultStatus.Fail
            && Status(evaluation, "Failing correspondence count") == ResultStatus.Fail
            && alpha.Status == ResultStatus.Fail
            && beta.Status == ResultStatus.Pass
            && gamma.Status == ResultStatus.Pass
            && evaluation.FailingCorrespondenceCount == 1;
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyNotGaugeRrMessage(
        AlignedPointRepeatabilityEvaluation passing)
    {
        var failed = Evaluate(KnownRunValues, Acceptance(1.99, 3.99));
        var passed = passing.Result.Message.Contains("not Gauge R&R", StringComparison.Ordinal)
            && passing.Result.Message.Contains("physical calibration", StringComparison.Ordinal)
            && failed.Result.Message.Contains("physical calibration", StringComparison.Ordinal);
        return (passed, $"passMessage={passing.Result.Message},failMessage={failed.Result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidMinimumCounts()
    {
        var minimumRuns = Evaluate(KnownRunValues, new AlignedPointRepeatabilityAcceptance(1, 3, 2.0, 4.0));
        var minimumCorrespondences = Evaluate(KnownRunValues, new AlignedPointRepeatabilityAcceptance(3, 0, 2.0, 4.0));
        var passed = IsError(minimumRuns, AlignedPointRepeatabilityDecision.InvalidAcceptancePolicy, "at least two")
            && IsError(minimumCorrespondences, AlignedPointRepeatabilityDecision.InvalidAcceptancePolicy, "at least one");
        return (passed, $"runs={minimumRuns.Decision},correspondences={minimumCorrespondences.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyInvalidLimits()
    {
        var standardDeviation = Evaluate(KnownRunValues, Acceptance(double.NaN, 4.0));
        var range = Evaluate(KnownRunValues, Acceptance(2.0, double.PositiveInfinity));
        var passed = IsError(standardDeviation, AlignedPointRepeatabilityDecision.InvalidAcceptancePolicy, "non-negative finite")
            && IsError(range, AlignedPointRepeatabilityDecision.InvalidAcceptancePolicy, "non-negative finite");
        return (passed, $"standardDeviation={standardDeviation.Decision},range={range.Decision}");
    }

    private static (bool Passed, string Evidence) VerifyDuplicateReferencePoint()
    {
        var input = CreateInput(KnownRunValues);
        var points = input.ReferencePoints!.ToArray();
        points[1] = points[1] with { CorrespondenceId = points[0].CorrespondenceId };
        return VerifyError(
            AlignedPointRepeatabilityRule.Evaluate(input with { ReferencePoints = points }),
            AlignedPointRepeatabilityDecision.InvalidInput,
            "Reference correspondence ID is duplicated");
    }

    private static (bool Passed, string Evidence) VerifyNonFiniteReferenceCoordinate()
    {
        var input = CreateInput(KnownRunValues);
        var points = input.ReferencePoints!.ToArray();
        points[0] = points[0] with { AlignedY = double.NaN };
        return VerifyError(
            AlignedPointRepeatabilityRule.Evaluate(input with { ReferencePoints = points }),
            AlignedPointRepeatabilityDecision.InvalidInput,
            "non-finite aligned coordinate");
    }

    private static (bool Passed, string Evidence) VerifyInsufficientRuns()
    {
        var input = CreateInput(KnownRunValues[..2], Acceptance(2.0, 4.0, minimumRunCount: 3));
        var validation = AlignedPointRepeatabilityRule.Validate(input);
        var evaluation = AlignedPointRepeatabilityRule.Evaluate(input);
        var passed = !validation.IsReady
            && validation.State == AlignedPointRepeatabilityInputState.InsufficientRuns
            && IsError(evaluation, AlignedPointRepeatabilityDecision.InsufficientRuns, "At least 3")
            && evaluation.RunCount == 2
            && double.IsNaN(evaluation.MaximumSampleStandardDeviation);
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyInsufficientCorrespondences()
    {
        var input = CreateInput(KnownRunValues, Acceptance(2.0, 4.0, minimumCorrespondenceCount: 4));
        var validation = AlignedPointRepeatabilityRule.Validate(input);
        var evaluation = AlignedPointRepeatabilityRule.Evaluate(input);
        var passed = !validation.IsReady
            && validation.State == AlignedPointRepeatabilityInputState.InsufficientCorrespondences
            && IsError(evaluation, AlignedPointRepeatabilityDecision.InsufficientCorrespondences, "At least 4")
            && evaluation.CorrespondenceCount == 3;
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyUnitFrameAndAlignmentMismatch()
    {
        var unit = VerifyChangedRunError(run => run with { Unit = "um" }, "unit does not match");
        var frame = VerifyChangedRunError(run => run with { FrameId = "frame.other" }, "frame does not match");
        var alignment = VerifyChangedRunError(run => run with { AlignmentReferenceId = "alignment.other" }, "alignment reference does not match");
        return (unit.Passed && frame.Passed && alignment.Passed, $"unit={unit.Evidence},frame={frame.Evidence},alignment={alignment.Evidence}");
    }

    private static (bool Passed, string Evidence) VerifyAlignmentMethodAndEvidenceRequired()
    {
        var method = VerifyChangedRunError(run => run with { AlignmentMethodId = "" }, "alignment method and evidence IDs");
        var evidence = VerifyChangedRunError(run => run with { AlignmentEvidenceId = "" }, "alignment method and evidence IDs");
        return (method.Passed && evidence.Passed, $"method={method.Evidence},evidence={evidence.Evidence}");
    }

    private static (bool Passed, string Evidence) VerifyDuplicateObservation()
    {
        var input = CreateInput(KnownRunValues);
        var runs = input.Runs!.ToArray();
        var observations = runs[1].Observations!.ToArray();
        observations[1] = observations[1] with { CorrespondenceId = observations[0].CorrespondenceId };
        runs[1] = runs[1] with { Observations = observations };
        return VerifyError(
            AlignedPointRepeatabilityRule.Evaluate(input with { Runs = runs }),
            AlignedPointRepeatabilityDecision.InvalidInput,
            "duplicates correspondence ID");
    }

    private static (bool Passed, string Evidence) VerifyIncompleteCoverage()
    {
        var input = CreateInput(KnownRunValues);
        var runs = input.Runs!.ToArray();
        runs[1] = runs[1] with { Observations = runs[1].Observations!.Take(2).ToArray() };
        return VerifyError(
            AlignedPointRepeatabilityRule.Evaluate(input with { Runs = runs }),
            AlignedPointRepeatabilityDecision.InvalidInput,
            "coverage does not exactly match");
    }

    private static (bool Passed, string Evidence) VerifyUnexpectedCoverage()
    {
        var input = CreateInput(KnownRunValues);
        var runs = input.Runs!.ToArray();
        runs[1] = runs[1] with
        {
            Observations = runs[1].Observations!
                .Append(new AlignedPointRepeatabilityObservation("point.unexpected", 0.0))
                .ToArray()
        };
        return VerifyError(
            AlignedPointRepeatabilityRule.Evaluate(input with { Runs = runs }),
            AlignedPointRepeatabilityDecision.InvalidInput,
            "coverage does not exactly match");
    }

    private static (bool Passed, string Evidence) VerifyNonFiniteObservation()
    {
        var input = CreateInput(KnownRunValues);
        var runs = input.Runs!.ToArray();
        var observations = runs[1].Observations!.ToArray();
        observations[0] = observations[0] with { Value = double.NaN };
        runs[1] = runs[1] with { Observations = observations };
        return VerifyError(
            AlignedPointRepeatabilityRule.Evaluate(input with { Runs = runs }),
            AlignedPointRepeatabilityDecision.InvalidInput,
            "non-finite value");
    }

    private static (bool Passed, string Evidence) VerifyStatisticsOverflow()
    {
        double[][] values =
        [
            [1e308, 1e308, 1e308],
            [-1e308, -1e308, -1e308],
            [1e308, 1e308, 1e308]
        ];
        return VerifyError(
            Evaluate(values, Acceptance(double.MaxValue, double.MaxValue)),
            AlignedPointRepeatabilityDecision.InvalidInput,
            "non-finite value or overflow");
    }

    private static (bool Passed, string Evidence) VerifyChangedRunError(
        Func<AlignedPointRepeatabilityRun, AlignedPointRepeatabilityRun> change,
        string expectedMessage)
    {
        var input = CreateInput(KnownRunValues);
        var runs = input.Runs!.ToArray();
        runs[1] = change(runs[1]);
        return VerifyError(
            AlignedPointRepeatabilityRule.Evaluate(input with { Runs = runs }),
            AlignedPointRepeatabilityDecision.InvalidInput,
            expectedMessage);
    }

    private static (bool Passed, string Evidence) VerifyDecision(
        AlignedPointRepeatabilityEvaluation evaluation,
        ResultStatus expectedStatus,
        AlignedPointRepeatabilityDecision expectedDecision,
        ResultStatus expectedStandardDeviationStatus,
        ResultStatus expectedRangeStatus)
    {
        var passed = evaluation.Result.Status == expectedStatus
            && evaluation.Decision == expectedDecision
            && Status(evaluation, "Maximum point sample standard deviation") == expectedStandardDeviationStatus
            && Status(evaluation, "Maximum point range") == expectedRangeStatus;
        return Evidence(evaluation, passed);
    }

    private static (bool Passed, string Evidence) VerifyError(
        AlignedPointRepeatabilityEvaluation evaluation,
        AlignedPointRepeatabilityDecision expectedDecision,
        string expectedMessage)
    {
        var passed = IsError(evaluation, expectedDecision, expectedMessage)
            && evaluation.Result.Metrics.All(metric => metric.Status == ResultStatus.Error)
            && evaluation.PointEvaluations.Count == 0
            && double.IsNaN(evaluation.MaximumSampleStandardDeviation)
            && double.IsNaN(evaluation.MaximumRange);
        return Evidence(evaluation, passed);
    }

    private static bool IsError(
        AlignedPointRepeatabilityEvaluation evaluation,
        AlignedPointRepeatabilityDecision expectedDecision,
        string expectedMessage) =>
        evaluation.Result.Status == ResultStatus.Error
        && evaluation.Decision == expectedDecision
        && evaluation.Result.Message.Contains(expectedMessage, StringComparison.OrdinalIgnoreCase);

    private static AlignedPointRepeatabilityEvaluation Evaluate(
        IReadOnlyList<double[]> runValues,
        AlignedPointRepeatabilityAcceptance? acceptance = null) =>
        AlignedPointRepeatabilityRule.Evaluate(CreateInput(runValues, acceptance));

    private static AlignedPointRepeatabilityInput CreateInput(
        IReadOnlyList<double[]> runValues,
        AlignedPointRepeatabilityAcceptance? acceptance = null)
    {
        var references = new[]
        {
            new AlignedPointRepeatabilityReferencePoint("point.gamma", 2.0, 0.0, 0.0),
            new AlignedPointRepeatabilityReferencePoint("point.alpha", 0.0, 0.0, 0.0),
            new AlignedPointRepeatabilityReferencePoint("point.beta", 1.0, 0.0, 0.0)
        };
        var runs = runValues.Select((values, index) =>
        {
            if (values.Length != 3)
            {
                throw new ArgumentException("Each synthetic aligned repeatability run requires exactly three values.", nameof(runValues));
            }

            return new AlignedPointRepeatabilityRun(
                $"run.synthetic.{index + 1:000}",
                $"source.synthetic.{index + 1:000}",
                1024 + index,
                SourceHash(index + 1),
                FirstCapture.AddMinutes(index),
                Unit,
                FrameId,
                AlignmentReferenceId,
                "alignment.synthetic-fixture-method",
                $"alignment-evidence.synthetic.{index + 1:000}",
                new[]
                {
                    new AlignedPointRepeatabilityObservation("point.gamma", values[2]),
                    new AlignedPointRepeatabilityObservation("point.alpha", values[0]),
                    new AlignedPointRepeatabilityObservation("point.beta", values[1])
                });
        }).ToArray();
        return new AlignedPointRepeatabilityInput(
            "study.synthetic-aligned-repeatability",
            "measurement.synthetic-thickness",
            "roi.synthetic-reference",
            Unit,
            FrameId,
            AlignmentReferenceId,
            CorrespondenceDefinitionId,
            references,
            runs,
            acceptance ?? Acceptance(2.0, 4.0, minimumRunCount: Math.Max(2, runValues.Count)));
    }

    private static AlignedPointRepeatabilityAcceptance Acceptance(
        double maximumSampleStandardDeviation,
        double maximumRange,
        int minimumRunCount = 3,
        int minimumCorrespondenceCount = 3) =>
        new(minimumRunCount, minimumCorrespondenceCount, maximumSampleStandardDeviation, maximumRange);

    private static AlignedPointRepeatabilityPointEvaluation Point(
        AlignedPointRepeatabilityEvaluation evaluation,
        string correspondenceId) =>
        evaluation.PointEvaluations.Single(point =>
            string.Equals(point.ReferencePoint.CorrespondenceId, correspondenceId, StringComparison.Ordinal));

    private static Metric Metric(AlignedPointRepeatabilityEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.Single(metric => metric.Name == name);

    private static ResultStatus? Status(AlignedPointRepeatabilityEvaluation evaluation, string name) =>
        Metric(evaluation, name).Status;

    private static string SourceHash(int index) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"synthetic-aligned-source-{index}")));

    private static (bool Passed, string Evidence) Evidence(
        AlignedPointRepeatabilityEvaluation evaluation,
        bool passed,
        string? suffix = null)
    {
        var text = string.Create(
            CultureInfo.InvariantCulture,
            $"status={evaluation.Result.Status},decision={evaluation.Decision},runs={evaluation.RunCount},correspondences={evaluation.CorrespondenceCount},failing={evaluation.FailingCorrespondenceCount},maximumSampleStandardDeviation={evaluation.MaximumSampleStandardDeviation:R},maximumRange={evaluation.MaximumRange:R}");
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

    private static string Clean(string value) =>
        value.Replace('|', '/').Replace('\r', ' ').Replace('\n', ' ');

    private sealed record VerificationCase(string Name, bool Passed, string Evidence);
}

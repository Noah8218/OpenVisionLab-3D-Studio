using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public static class ThicknessRepeatabilityRule
{
    public const string ToolName = "Thickness Repeatability";
    public const string EvaluationOrder =
        "StudyIdentity -> UnitFrame -> AcceptancePolicy -> RunEvidence -> MinimumRunCount -> Statistics -> Acceptance";

    public static ThicknessRepeatabilityInputValidation Validate(ThicknessRepeatabilityInput? input)
    {
        if (input is null)
        {
            return ValidationError(
                null,
                ThicknessRepeatabilityInputState.InvalidInput,
                "Repeatability input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.StudyId))
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidInput,
                "Study ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.MeasurementDefinitionId))
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidInput,
                "Measurement definition ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ReferenceRoiId))
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidInput,
                "Reference ROI ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Unit))
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidInput,
                "Repeatability unit is required.");
        }

        if (string.IsNullOrWhiteSpace(input.FrameId))
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidInput,
                "Coordinate frame ID is required.");
        }

        if (!TryValidateAcceptance(input.Acceptance, out var acceptanceMessage))
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidAcceptancePolicy,
                acceptanceMessage);
        }

        if (input.Runs is null)
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidInput,
                "Repeatability runs are required.");
        }

        var runs = input.Runs.ToArray();
        if (!TryValidateRuns(input, runs, out var runMessage))
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InvalidInput,
                runMessage,
                runs);
        }

        var acceptance = input.Acceptance!;
        if (runs.Length < acceptance.MinimumRunCount)
        {
            return ValidationError(
                input,
                ThicknessRepeatabilityInputState.InsufficientRuns,
                $"At least {acceptance.MinimumRunCount} valid runs are required; received {runs.Length}.",
                runs);
        }

        var runSnapshot = Array.AsReadOnly(runs);
        return new ThicknessRepeatabilityInputValidation(
            true,
            ThicknessRepeatabilityInputState.Ready,
            "Repeatability study is ready to calculate.",
            input with { Runs = runSnapshot },
            runSnapshot,
            runSnapshot.Count);
    }

    public static ThicknessRepeatabilityEvaluation Evaluate(ThicknessRepeatabilityInput? input)
    {
        var stopwatch = Stopwatch.StartNew();
        var validation = Validate(input);
        if (!validation.IsReady)
        {
            return Error(
                validation.Input,
                ToDecision(validation.State),
                validation.Message,
                stopwatch,
                validation.Runs);
        }

        var inputSnapshot = validation.Input!;
        var runs = validation.Runs;
        var acceptance = inputSnapshot.Acceptance!;

        if (!TryCalculateStatistics(runs, out var statistics))
        {
            return Error(
                inputSnapshot,
                ThicknessRepeatabilityDecision.InvalidInput,
                "Repeatability statistics produced a non-finite value or overflow.",
                stopwatch,
                runs);
        }

        var standardDeviationPassed =
            statistics.SampleStandardDeviation <= acceptance.MaximumSampleStandardDeviation;
        var rangePassed = statistics.Range <= acceptance.MaximumRange;
        var decision = (standardDeviationPassed, rangePassed) switch
        {
            (true, true) => ThicknessRepeatabilityDecision.Accepted,
            (false, true) => ThicknessRepeatabilityDecision.SampleStandardDeviationExceeded,
            (true, false) => ThicknessRepeatabilityDecision.RangeExceeded,
            _ => ThicknessRepeatabilityDecision.SampleStandardDeviationAndRangeExceeded
        };
        var status = decision == ThicknessRepeatabilityDecision.Accepted
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        stopwatch.Stop();

        var standardDeviationStatus = standardDeviationPassed ? ResultStatus.Pass : ResultStatus.Fail;
        var rangeStatus = rangePassed ? ResultStatus.Pass : ResultStatus.Fail;
        var result = new ToolResult(
            ToolName,
            status,
            CreateMessage(decision),
            stopwatch.Elapsed,
            CreateMetrics(
                runs.Count,
                acceptance,
                inputSnapshot.Unit,
                statistics,
                standardDeviationStatus,
                rangeStatus),
            []);

        return new ThicknessRepeatabilityEvaluation(
            result,
            decision,
            inputSnapshot,
            runs,
            runs.Count,
            statistics.Mean,
            statistics.Minimum,
            statistics.Maximum,
            statistics.SampleStandardDeviation,
            statistics.SixSigmaSpread,
            statistics.Range);
    }

    private static bool TryValidateAcceptance(
        ThicknessRepeatabilityAcceptance? acceptance,
        out string message)
    {
        if (acceptance is null)
        {
            message = "Repeatability acceptance policy is required.";
            return false;
        }

        if (acceptance.MinimumRunCount < 2)
        {
            message = "Minimum run count must be at least two for sample standard deviation.";
            return false;
        }

        if (!double.IsFinite(acceptance.MaximumSampleStandardDeviation)
            || acceptance.MaximumSampleStandardDeviation < 0.0)
        {
            message = "Maximum sample standard deviation must be a non-negative finite value.";
            return false;
        }

        if (!double.IsFinite(acceptance.MaximumRange) || acceptance.MaximumRange < 0.0)
        {
            message = "Maximum range must be a non-negative finite value.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryValidateRuns(
        ThicknessRepeatabilityInput input,
        IReadOnlyList<ThicknessRepeatabilityRun> runs,
        out string message)
    {
        var runIds = new HashSet<string>(StringComparer.Ordinal);
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            if (run is null)
            {
                message = $"Run {index + 1} is null.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(run.RunId))
            {
                message = $"Run {index + 1} has no run ID.";
                return false;
            }

            if (!runIds.Add(run.RunId))
            {
                message = $"Duplicate run ID is not allowed: {run.RunId}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(run.SourceEntityId))
            {
                message = $"Run {run.RunId} has no source entity ID.";
                return false;
            }

            if (!sourceIds.Add(run.SourceEntityId))
            {
                message = $"A source entity cannot count as more than one acquisition: {run.SourceEntityId}.";
                return false;
            }

            if (run.CapturedAt == default)
            {
                message = $"Run {run.RunId} has no capture timestamp.";
                return false;
            }

            if (!string.Equals(run.Unit, input.Unit, StringComparison.Ordinal))
            {
                message = $"Run {run.RunId} unit does not match the study unit.";
                return false;
            }

            if (!string.Equals(run.FrameId, input.FrameId, StringComparison.Ordinal))
            {
                message = $"Run {run.RunId} coordinate frame does not match the study frame.";
                return false;
            }

            if (!double.IsFinite(run.Thickness))
            {
                message = $"Run {run.RunId} thickness must be finite.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool TryCalculateStatistics(
        IReadOnlyList<ThicknessRepeatabilityRun> runs,
        out RepeatabilityStatistics statistics)
    {
        var count = 0;
        var mean = 0.0;
        var sumSquaredDifferences = 0.0;
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        foreach (var run in runs)
        {
            count++;
            var delta = run.Thickness - mean;
            mean += delta / count;
            var deltaAfterMean = run.Thickness - mean;
            sumSquaredDifferences += delta * deltaAfterMean;
            minimum = Math.Min(minimum, run.Thickness);
            maximum = Math.Max(maximum, run.Thickness);
        }

        var variance = Math.Max(0.0, sumSquaredDifferences / (count - 1));
        var sampleStandardDeviation = Math.Sqrt(variance);
        var sixSigmaSpread = 6.0 * sampleStandardDeviation;
        var range = maximum - minimum;
        if (!double.IsFinite(mean)
            || !double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || !double.IsFinite(sumSquaredDifferences)
            || !double.IsFinite(sampleStandardDeviation)
            || !double.IsFinite(sixSigmaSpread)
            || !double.IsFinite(range))
        {
            statistics = default;
            return false;
        }

        statistics = new RepeatabilityStatistics(
            mean,
            minimum,
            maximum,
            sampleStandardDeviation,
            sixSigmaSpread,
            range);
        return true;
    }

    private static IReadOnlyList<Metric> CreateMetrics(
        int runCount,
        ThicknessRepeatabilityAcceptance acceptance,
        string unit,
        RepeatabilityStatistics statistics,
        ResultStatus standardDeviationStatus,
        ResultStatus rangeStatus) =>
    [
        new Metric("Run count", MetricKind.Count, runCount, "count", ResultStatus.Pass),
        new Metric("Minimum run count", MetricKind.Count, acceptance.MinimumRunCount, "count", ResultStatus.Pass),
        new Metric("Mean thickness", MetricKind.Length, statistics.Mean, unit),
        new Metric("Minimum thickness", MetricKind.Length, statistics.Minimum, unit),
        new Metric("Maximum thickness", MetricKind.Length, statistics.Maximum, unit),
        new Metric("Sample standard deviation", MetricKind.Deviation, statistics.SampleStandardDeviation, unit, standardDeviationStatus),
        new Metric("Maximum sample standard deviation", MetricKind.Deviation, acceptance.MaximumSampleStandardDeviation, unit, standardDeviationStatus),
        new Metric("Six-sigma spread", MetricKind.Deviation, statistics.SixSigmaSpread, unit),
        new Metric("Range", MetricKind.Deviation, statistics.Range, unit, rangeStatus),
        new Metric("Maximum range", MetricKind.Deviation, acceptance.MaximumRange, unit, rangeStatus)
    ];

    private static ThicknessRepeatabilityInputValidation ValidationError(
        ThicknessRepeatabilityInput? input,
        ThicknessRepeatabilityInputState state,
        string message,
        IReadOnlyList<ThicknessRepeatabilityRun>? runs = null)
    {
        var runArray = runs?.ToArray() ?? input?.Runs?.ToArray() ?? [];
        var runSnapshot = Array.AsReadOnly(runArray);
        var inputSnapshot = input is null || input.Runs is null
            ? input
            : input with { Runs = runSnapshot };
        return new ThicknessRepeatabilityInputValidation(
            false,
            state,
            message,
            inputSnapshot,
            runSnapshot,
            runSnapshot.Count);
    }

    private static ThicknessRepeatabilityDecision ToDecision(ThicknessRepeatabilityInputState state) => state switch
    {
        ThicknessRepeatabilityInputState.InvalidAcceptancePolicy =>
            ThicknessRepeatabilityDecision.InvalidAcceptancePolicy,
        ThicknessRepeatabilityInputState.InsufficientRuns =>
            ThicknessRepeatabilityDecision.InsufficientRuns,
        _ => ThicknessRepeatabilityDecision.InvalidInput
    };

    private static ThicknessRepeatabilityEvaluation Error(
        ThicknessRepeatabilityInput? input,
        ThicknessRepeatabilityDecision decision,
        string message,
        Stopwatch stopwatch,
        IReadOnlyList<ThicknessRepeatabilityRun>? runs = null)
    {
        stopwatch.Stop();
        var runArray = runs?.ToArray() ?? input?.Runs?.ToArray() ?? [];
        var runSnapshot = Array.AsReadOnly(runArray);
        var inputSnapshot = input is null || input.Runs is null
            ? input
            : input with { Runs = runSnapshot };
        var acceptance = input?.Acceptance;
        var unit = input?.Unit ?? string.Empty;
        var metrics = new[]
        {
            new Metric("Run count", MetricKind.Count, runArray.Length, "count", ResultStatus.Error),
            new Metric("Minimum run count", MetricKind.Count, acceptance?.MinimumRunCount ?? double.NaN, "count", ResultStatus.Error),
            new Metric("Mean thickness", MetricKind.Length, double.NaN, unit, ResultStatus.Error),
            new Metric("Minimum thickness", MetricKind.Length, double.NaN, unit, ResultStatus.Error),
            new Metric("Maximum thickness", MetricKind.Length, double.NaN, unit, ResultStatus.Error),
            new Metric("Sample standard deviation", MetricKind.Deviation, double.NaN, unit, ResultStatus.Error),
            new Metric("Maximum sample standard deviation", MetricKind.Deviation, acceptance?.MaximumSampleStandardDeviation ?? double.NaN, unit, ResultStatus.Error),
            new Metric("Six-sigma spread", MetricKind.Deviation, double.NaN, unit, ResultStatus.Error),
            new Metric("Range", MetricKind.Deviation, double.NaN, unit, ResultStatus.Error),
            new Metric("Maximum range", MetricKind.Deviation, acceptance?.MaximumRange ?? double.NaN, unit, ResultStatus.Error)
        };
        return new ThicknessRepeatabilityEvaluation(
            new ToolResult(ToolName, ResultStatus.Error, message, stopwatch.Elapsed, metrics, []),
            decision,
            inputSnapshot,
            runSnapshot,
            runArray.Length,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN,
            double.NaN);
    }

    private static string CreateMessage(ThicknessRepeatabilityDecision decision) => decision switch
    {
        ThicknessRepeatabilityDecision.Accepted =>
            "Thickness repeatability is within the configured limits. This same-setup study is not Gauge R&R.",
        ThicknessRepeatabilityDecision.SampleStandardDeviationExceeded =>
            "Sample standard deviation exceeds its limit. This same-setup study is not Gauge R&R.",
        ThicknessRepeatabilityDecision.RangeExceeded =>
            "Thickness range exceeds its limit. This same-setup study is not Gauge R&R.",
        _ =>
            "Sample standard deviation and thickness range exceed their limits. This same-setup study is not Gauge R&R."
    };

    private readonly record struct RepeatabilityStatistics(
        double Mean,
        double Minimum,
        double Maximum,
        double SampleStandardDeviation,
        double SixSigmaSpread,
        double Range);
}

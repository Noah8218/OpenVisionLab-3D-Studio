using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public static class AlignedPointRepeatabilityRule
{
    public const string ToolName = "Aligned Point Repeatability";
    public const string EvaluationOrder =
        "StudyIdentity -> UnitFrameAlignment -> AcceptancePolicy -> SourceEvidence -> CorrespondenceCoverage -> MinimumCounts -> PerPointStatistics -> Acceptance";

    public static AlignedPointRepeatabilityInputValidation Validate(AlignedPointRepeatabilityInput? input)
    {
        if (input is null)
        {
            return ValidationError(
                null,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Aligned point repeatability input is required.");
        }

        if (string.IsNullOrWhiteSpace(input.StudyId))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Study ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.MeasurementDefinitionId))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Measurement definition ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.ReferenceRoiId))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Reference ROI ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.Unit))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Repeatability unit is required.");
        }

        if (string.IsNullOrWhiteSpace(input.FrameId))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Coordinate frame ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.AlignmentReferenceId))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Alignment reference ID is required.");
        }

        if (string.IsNullOrWhiteSpace(input.CorrespondenceDefinitionId))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Correspondence definition ID is required.");
        }

        if (!TryValidateAcceptance(input.Acceptance, out var acceptanceMessage))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidAcceptancePolicy,
                acceptanceMessage);
        }

        if (input.ReferencePoints is null)
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Reference correspondence points are required.");
        }

        var referencePoints = input.ReferencePoints.ToArray();
        if (!TryValidateReferencePoints(referencePoints, out var referenceMessage))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                referenceMessage,
                referencePoints);
        }

        if (input.Runs is null)
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                "Aligned point repeatability runs are required.",
                referencePoints);
        }

        var runs = SnapshotRuns(input.Runs);
        if (!TryValidateRuns(input, referencePoints, runs, out var runMessage))
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InvalidInput,
                runMessage,
                referencePoints,
                runs);
        }

        var acceptance = input.Acceptance!;
        if (runs.Count < acceptance.MinimumRunCount)
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InsufficientRuns,
                $"At least {acceptance.MinimumRunCount} valid runs are required; received {runs.Count}.",
                referencePoints,
                runs);
        }

        if (referencePoints.Length < acceptance.MinimumCorrespondenceCount)
        {
            return ValidationError(
                input,
                AlignedPointRepeatabilityInputState.InsufficientCorrespondences,
                $"At least {acceptance.MinimumCorrespondenceCount} full-coverage correspondence points are required; received {referencePoints.Length}.",
                referencePoints,
                runs);
        }

        var referenceSnapshot = Array.AsReadOnly(referencePoints);
        var inputSnapshot = input with { ReferencePoints = referenceSnapshot, Runs = runs };
        return new AlignedPointRepeatabilityInputValidation(
            true,
            AlignedPointRepeatabilityInputState.Ready,
            "Aligned point repeatability study is ready to calculate.",
            inputSnapshot,
            referenceSnapshot,
            runs,
            runs.Count,
            referenceSnapshot.Count);
    }

    public static AlignedPointRepeatabilityEvaluation Evaluate(AlignedPointRepeatabilityInput? input)
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
                validation.ReferencePoints,
                validation.Runs);
        }

        var inputSnapshot = validation.Input!;
        var acceptance = inputSnapshot.Acceptance!;
        if (!TryEvaluatePoints(
                validation.ReferencePoints,
                validation.Runs,
                acceptance,
                out var pointEvaluations,
                out var failingCount,
                out var maximumSampleStandardDeviation,
                out var maximumRange))
        {
            return Error(
                inputSnapshot,
                AlignedPointRepeatabilityDecision.InvalidInput,
                "Aligned point statistics produced a non-finite value or overflow.",
                stopwatch,
                validation.ReferencePoints,
                validation.Runs);
        }

        var standardDeviationPassed = pointEvaluations.All(point => point.SampleStandardDeviationPassed);
        var rangePassed = pointEvaluations.All(point => point.RangePassed);
        var decision = (standardDeviationPassed, rangePassed) switch
        {
            (true, true) => AlignedPointRepeatabilityDecision.Accepted,
            (false, true) => AlignedPointRepeatabilityDecision.SampleStandardDeviationExceeded,
            (true, false) => AlignedPointRepeatabilityDecision.RangeExceeded,
            _ => AlignedPointRepeatabilityDecision.SampleStandardDeviationAndRangeExceeded
        };
        var status = decision == AlignedPointRepeatabilityDecision.Accepted
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        stopwatch.Stop();

        var result = new ToolResult(
            ToolName,
            status,
            CreateMessage(decision),
            stopwatch.Elapsed,
            CreateMetrics(
                validation.RunCount,
                validation.CorrespondenceCount,
                failingCount,
                acceptance,
                inputSnapshot.Unit,
                maximumSampleStandardDeviation,
                maximumRange,
                standardDeviationPassed,
                rangePassed),
            []);
        return new AlignedPointRepeatabilityEvaluation(
            result,
            decision,
            inputSnapshot,
            validation.ReferencePoints,
            validation.Runs,
            pointEvaluations,
            validation.RunCount,
            validation.CorrespondenceCount,
            failingCount,
            maximumSampleStandardDeviation,
            maximumRange);
    }

    private static bool TryValidateAcceptance(
        AlignedPointRepeatabilityAcceptance? acceptance,
        out string message)
    {
        if (acceptance is null)
        {
            message = "Aligned point repeatability acceptance policy is required.";
            return false;
        }

        if (acceptance.MinimumRunCount < 2)
        {
            message = "Minimum run count must be at least two for sample standard deviation.";
            return false;
        }

        if (acceptance.MinimumCorrespondenceCount < 1)
        {
            message = "Minimum correspondence count must be at least one.";
            return false;
        }

        if (!double.IsFinite(acceptance.MaximumSampleStandardDeviation)
            || acceptance.MaximumSampleStandardDeviation < 0.0)
        {
            message = "Maximum point sample standard deviation must be a non-negative finite value.";
            return false;
        }

        if (!double.IsFinite(acceptance.MaximumRange) || acceptance.MaximumRange < 0.0)
        {
            message = "Maximum point range must be a non-negative finite value.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static bool TryValidateReferencePoints(
        IReadOnlyList<AlignedPointRepeatabilityReferencePoint> referencePoints,
        out string message)
    {
        if (referencePoints.Count == 0)
        {
            message = "At least one reference correspondence point is required.";
            return false;
        }

        var correspondenceIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < referencePoints.Count; index++)
        {
            var point = referencePoints[index];
            if (string.IsNullOrWhiteSpace(point.CorrespondenceId))
            {
                message = $"Reference correspondence point {index + 1} has no correspondence ID.";
                return false;
            }

            if (!correspondenceIds.Add(point.CorrespondenceId))
            {
                message = $"Reference correspondence ID is duplicated: {point.CorrespondenceId}.";
                return false;
            }

            if (!double.IsFinite(point.AlignedX)
                || !double.IsFinite(point.AlignedY)
                || !double.IsFinite(point.AlignedZ))
            {
                message = $"Reference correspondence point {point.CorrespondenceId} has a non-finite aligned coordinate.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool TryValidateRuns(
        AlignedPointRepeatabilityInput input,
        IReadOnlyList<AlignedPointRepeatabilityReferencePoint> referencePoints,
        IReadOnlyList<AlignedPointRepeatabilityRun> runs,
        out string message)
    {
        var runIds = new HashSet<string>(StringComparer.Ordinal);
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var sourceHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referenceIds = referencePoints
            .Select(point => point.CorrespondenceId)
            .ToHashSet(StringComparer.Ordinal);

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
                message = $"Run ID is duplicated: {run.RunId}.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(run.SourceEntityId))
            {
                message = $"Run {run.RunId} has no source entity ID.";
                return false;
            }

            if (!sourceIds.Add(run.SourceEntityId))
            {
                message = $"Source entity ID is duplicated: {run.SourceEntityId}.";
                return false;
            }

            if (run.SourceByteLength <= 0)
            {
                message = $"Run {run.RunId} has no positive source byte length.";
                return false;
            }

            if (!IsSha256(run.SourceSha256))
            {
                message = $"Run {run.RunId} requires a 64-character hexadecimal source SHA-256.";
                return false;
            }

            if (!sourceHashes.Add(run.SourceSha256))
            {
                message = $"Byte-identical source SHA-256 cannot count as a separate acquisition: {run.RunId}.";
                return false;
            }

            if (run.CapturedAt == default)
            {
                message = $"Run {run.RunId} has no capture timestamp.";
                return false;
            }

            if (!string.Equals(run.Unit, input.Unit, StringComparison.Ordinal))
            {
                message = $"Run {run.RunId} unit does not match study unit.";
                return false;
            }

            if (!string.Equals(run.FrameId, input.FrameId, StringComparison.Ordinal))
            {
                message = $"Run {run.RunId} frame does not match study frame.";
                return false;
            }

            if (!string.Equals(run.AlignmentReferenceId, input.AlignmentReferenceId, StringComparison.Ordinal))
            {
                message = $"Run {run.RunId} alignment reference does not match the study alignment reference.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(run.AlignmentMethodId)
                || string.IsNullOrWhiteSpace(run.AlignmentEvidenceId))
            {
                message = $"Run {run.RunId} requires alignment method and evidence IDs.";
                return false;
            }

            if (run.Observations is null)
            {
                message = $"Run {run.RunId} observations are required.";
                return false;
            }

            var observationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var observation in run.Observations)
            {
                if (string.IsNullOrWhiteSpace(observation.CorrespondenceId))
                {
                    message = $"Run {run.RunId} contains an observation without a correspondence ID.";
                    return false;
                }

                if (!observationIds.Add(observation.CorrespondenceId))
                {
                    message = $"Run {run.RunId} duplicates correspondence ID {observation.CorrespondenceId}.";
                    return false;
                }

                if (!double.IsFinite(observation.Value))
                {
                    message = $"Run {run.RunId} correspondence {observation.CorrespondenceId} has a non-finite value.";
                    return false;
                }
            }

            if (!observationIds.SetEquals(referenceIds))
            {
                message = $"Run {run.RunId} correspondence coverage does not exactly match the declared reference points.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool TryEvaluatePoints(
        IReadOnlyList<AlignedPointRepeatabilityReferencePoint> referencePoints,
        IReadOnlyList<AlignedPointRepeatabilityRun> runs,
        AlignedPointRepeatabilityAcceptance acceptance,
        out IReadOnlyList<AlignedPointRepeatabilityPointEvaluation> pointEvaluations,
        out int failingCount,
        out double maximumSampleStandardDeviation,
        out double maximumRange)
    {
        var valuesByCorrespondence = new Dictionary<string, List<double>>(StringComparer.Ordinal);
        foreach (var referencePoint in referencePoints)
        {
            valuesByCorrespondence.Add(referencePoint.CorrespondenceId, new List<double>(runs.Count));
        }

        foreach (var run in runs)
        {
            foreach (var observation in run.Observations!)
            {
                valuesByCorrespondence[observation.CorrespondenceId].Add(observation.Value);
            }
        }

        var evaluations = new List<AlignedPointRepeatabilityPointEvaluation>(referencePoints.Count);
        failingCount = 0;
        maximumSampleStandardDeviation = double.NegativeInfinity;
        maximumRange = double.NegativeInfinity;
        foreach (var referencePoint in referencePoints.OrderBy(point => point.CorrespondenceId, StringComparer.Ordinal))
        {
            var values = valuesByCorrespondence[referencePoint.CorrespondenceId];
            if (values.Count != runs.Count || !TryCalculateStatistics(values, out var statistics))
            {
                pointEvaluations = [];
                failingCount = 0;
                maximumSampleStandardDeviation = double.NaN;
                maximumRange = double.NaN;
                return false;
            }

            var standardDeviationPassed =
                statistics.SampleStandardDeviation <= acceptance.MaximumSampleStandardDeviation;
            var rangePassed = statistics.Range <= acceptance.MaximumRange;
            var status = standardDeviationPassed && rangePassed ? ResultStatus.Pass : ResultStatus.Fail;
            if (status == ResultStatus.Fail)
            {
                failingCount++;
            }

            maximumSampleStandardDeviation = Math.Max(
                maximumSampleStandardDeviation,
                statistics.SampleStandardDeviation);
            maximumRange = Math.Max(maximumRange, statistics.Range);
            evaluations.Add(new AlignedPointRepeatabilityPointEvaluation(
                referencePoint,
                values.Count,
                statistics.Mean,
                statistics.Minimum,
                statistics.Maximum,
                statistics.SampleStandardDeviation,
                statistics.SixSigmaSpread,
                statistics.Range,
                status,
                standardDeviationPassed,
                rangePassed));
        }

        pointEvaluations = Array.AsReadOnly(evaluations.ToArray());
        return double.IsFinite(maximumSampleStandardDeviation) && double.IsFinite(maximumRange);
    }

    private static bool TryCalculateStatistics(
        IReadOnlyList<double> values,
        out RepeatabilityStatistics statistics)
    {
        if (values.Count < 2)
        {
            statistics = default;
            return false;
        }

        var count = 0;
        var mean = 0.0;
        var sumSquaredDelta = 0.0;
        var minimum = double.PositiveInfinity;
        var maximum = double.NegativeInfinity;
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
            {
                statistics = default;
                return false;
            }

            count++;
            var delta = value - mean;
            mean += delta / count;
            sumSquaredDelta += delta * (value - mean);
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
        }

        var variance = sumSquaredDelta / (count - 1);
        if (variance < 0.0 && variance > -1e-12)
        {
            variance = 0.0;
        }

        var sampleStandardDeviation = Math.Sqrt(variance);
        var sixSigmaSpread = 6.0 * sampleStandardDeviation;
        var range = maximum - minimum;
        if (!double.IsFinite(mean)
            || !double.IsFinite(minimum)
            || !double.IsFinite(maximum)
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
        int correspondenceCount,
        int failingCount,
        AlignedPointRepeatabilityAcceptance acceptance,
        string unit,
        double maximumSampleStandardDeviation,
        double maximumRange,
        bool standardDeviationPassed,
        bool rangePassed) =>
    [
        new Metric("Run count", MetricKind.Count, runCount, "count", ResultStatus.Pass),
        new Metric("Minimum run count", MetricKind.Count, acceptance.MinimumRunCount, "count", ResultStatus.Pass),
        new Metric("Correspondence count", MetricKind.Count, correspondenceCount, "count", ResultStatus.Pass),
        new Metric("Minimum correspondence count", MetricKind.Count, acceptance.MinimumCorrespondenceCount, "count", ResultStatus.Pass),
        new Metric("Maximum point sample standard deviation", MetricKind.Deviation, maximumSampleStandardDeviation, unit, standardDeviationPassed ? ResultStatus.Pass : ResultStatus.Fail),
        new Metric("Maximum allowed point sample standard deviation", MetricKind.Deviation, acceptance.MaximumSampleStandardDeviation, unit, standardDeviationPassed ? ResultStatus.Pass : ResultStatus.Fail),
        new Metric("Maximum point range", MetricKind.Deviation, maximumRange, unit, rangePassed ? ResultStatus.Pass : ResultStatus.Fail),
        new Metric("Maximum allowed point range", MetricKind.Deviation, acceptance.MaximumRange, unit, rangePassed ? ResultStatus.Pass : ResultStatus.Fail),
        new Metric("Failing correspondence count", MetricKind.Count, failingCount, "count", failingCount == 0 ? ResultStatus.Pass : ResultStatus.Fail)
    ];

    private static AlignedPointRepeatabilityInputValidation ValidationError(
        AlignedPointRepeatabilityInput? input,
        AlignedPointRepeatabilityInputState state,
        string message,
        IReadOnlyList<AlignedPointRepeatabilityReferencePoint>? referencePoints = null,
        IReadOnlyList<AlignedPointRepeatabilityRun>? runs = null)
    {
        var referenceArray = referencePoints?.ToArray() ?? input?.ReferencePoints?.ToArray() ?? [];
        var runSnapshot = runs ?? SnapshotRuns(input?.Runs);
        var referenceSnapshot = Array.AsReadOnly(referenceArray);
        var inputSnapshot = input is null
            ? null
            : input with { ReferencePoints = referenceSnapshot, Runs = runSnapshot };
        return new AlignedPointRepeatabilityInputValidation(
            false,
            state,
            message,
            inputSnapshot,
            referenceSnapshot,
            runSnapshot,
            runSnapshot.Count,
            referenceSnapshot.Count);
    }

    private static IReadOnlyList<AlignedPointRepeatabilityRun> SnapshotRuns(
        IReadOnlyList<AlignedPointRepeatabilityRun>? runs)
    {
        if (runs is null)
        {
            return Array.Empty<AlignedPointRepeatabilityRun>();
        }

        var snapshot = new AlignedPointRepeatabilityRun[runs.Count];
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            snapshot[index] = run is null || run.Observations is null
                ? run!
                : run with { Observations = Array.AsReadOnly(run.Observations.ToArray()) };
        }

        return Array.AsReadOnly(snapshot);
    }

    private static AlignedPointRepeatabilityDecision ToDecision(AlignedPointRepeatabilityInputState state) => state switch
    {
        AlignedPointRepeatabilityInputState.InvalidAcceptancePolicy =>
            AlignedPointRepeatabilityDecision.InvalidAcceptancePolicy,
        AlignedPointRepeatabilityInputState.InsufficientRuns =>
            AlignedPointRepeatabilityDecision.InsufficientRuns,
        AlignedPointRepeatabilityInputState.InsufficientCorrespondences =>
            AlignedPointRepeatabilityDecision.InsufficientCorrespondences,
        _ => AlignedPointRepeatabilityDecision.InvalidInput
    };

    private static AlignedPointRepeatabilityEvaluation Error(
        AlignedPointRepeatabilityInput? input,
        AlignedPointRepeatabilityDecision decision,
        string message,
        Stopwatch stopwatch,
        IReadOnlyList<AlignedPointRepeatabilityReferencePoint>? referencePoints = null,
        IReadOnlyList<AlignedPointRepeatabilityRun>? runs = null)
    {
        stopwatch.Stop();
        var referenceArray = referencePoints?.ToArray() ?? input?.ReferencePoints?.ToArray() ?? [];
        var referenceSnapshot = Array.AsReadOnly(referenceArray);
        var runSnapshot = runs ?? SnapshotRuns(input?.Runs);
        var inputSnapshot = input is null
            ? null
            : input with { ReferencePoints = referenceSnapshot, Runs = runSnapshot };
        var acceptance = input?.Acceptance;
        var unit = input?.Unit ?? string.Empty;
        var metrics = new[]
        {
            new Metric("Run count", MetricKind.Count, runSnapshot.Count, "count", ResultStatus.Error),
            new Metric("Minimum run count", MetricKind.Count, acceptance?.MinimumRunCount ?? double.NaN, "count", ResultStatus.Error),
            new Metric("Correspondence count", MetricKind.Count, referenceSnapshot.Count, "count", ResultStatus.Error),
            new Metric("Minimum correspondence count", MetricKind.Count, acceptance?.MinimumCorrespondenceCount ?? double.NaN, "count", ResultStatus.Error),
            new Metric("Maximum point sample standard deviation", MetricKind.Deviation, double.NaN, unit, ResultStatus.Error),
            new Metric("Maximum allowed point sample standard deviation", MetricKind.Deviation, acceptance?.MaximumSampleStandardDeviation ?? double.NaN, unit, ResultStatus.Error),
            new Metric("Maximum point range", MetricKind.Deviation, double.NaN, unit, ResultStatus.Error),
            new Metric("Maximum allowed point range", MetricKind.Deviation, acceptance?.MaximumRange ?? double.NaN, unit, ResultStatus.Error),
            new Metric("Failing correspondence count", MetricKind.Count, double.NaN, "count", ResultStatus.Error)
        };
        return new AlignedPointRepeatabilityEvaluation(
            new ToolResult(ToolName, ResultStatus.Error, message, stopwatch.Elapsed, metrics, []),
            decision,
            inputSnapshot,
            referenceSnapshot,
            runSnapshot,
            Array.Empty<AlignedPointRepeatabilityPointEvaluation>(),
            runSnapshot.Count,
            referenceSnapshot.Count,
            0,
            double.NaN,
            double.NaN);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static string CreateMessage(AlignedPointRepeatabilityDecision decision) => decision switch
    {
        AlignedPointRepeatabilityDecision.Accepted =>
            "All declared aligned correspondence points are within the configured limits. This is not Gauge R&R, physical calibration, or metrology certification.",
        AlignedPointRepeatabilityDecision.SampleStandardDeviationExceeded =>
            "One or more aligned correspondence point sample standard deviations exceed their limit. This is not Gauge R&R, physical calibration, or metrology certification.",
        AlignedPointRepeatabilityDecision.RangeExceeded =>
            "One or more aligned correspondence point ranges exceed their limit. This is not Gauge R&R, physical calibration, or metrology certification.",
        _ =>
            "One or more aligned correspondence point sample standard deviations and ranges exceed their limits. This is not Gauge R&R, physical calibration, or metrology certification."
    };

    private readonly record struct RepeatabilityStatistics(
        double Mean,
        double Minimum,
        double Maximum,
        double SampleStandardDeviation,
        double SixSigmaSpread,
        double Range);
}

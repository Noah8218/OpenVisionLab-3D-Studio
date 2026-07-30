using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeThresholdCandidateAnalyzer
{
    public static ToolRecipeThresholdCandidateReport Analyze(
        ToolRecipeDocument document,
        ToolRecipeValidationSetResult result)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(result);
        var observations =
            ToolRecipeLabeledEvidenceAnalyzer.CollectObservations(
                document,
                result);
        var development = observations
            .Where(observation =>
                observation.Role is ToolRecipeValidationSampleRole.Good
                    or ToolRecipeValidationSampleRole.Bad)
            .ToArray();
        var heldOut = observations
            .Where(observation =>
                observation.Role == ToolRecipeValidationSampleRole.HeldOut)
            .ToArray();
        var candidates = new List<ToolRecipeThresholdCandidate>();
        var evidenceWarnings = new List<ToolRecipeThresholdEvidenceWarning>();

        var groups = development
                     .GroupBy(observation => new
                     {
                         observation.Scope,
                         observation.OwnerId,
                         observation.OwnerName,
                         observation.MetricName,
                         observation.Unit
                     })
                     .OrderBy(group => group.Key.Scope)
                     .ThenBy(group => group.Key.OwnerId, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.MetricName, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.Unit, StringComparer.Ordinal)
                     .ToArray();
        foreach (var group in groups)
        {
            var groupObservations = group
                .OrderBy(observation => observation.SampleOrder)
                .ToArray();
            var goodIdentities = DistinctSampleIdentities(
                groupObservations,
                ToolRecipeValidationSampleRole.Good);
            var badIdentities = DistinctSampleIdentities(
                groupObservations,
                ToolRecipeValidationSampleRole.Bad);
            var assistantMetric = IsThresholdAssistantMetric(
                document,
                group.Key.Scope,
                group.Key.OwnerId,
                group.Key.MetricName);
            if (assistantMetric)
            {
                AddBalanceWarnings(
                    group.Key.Scope,
                    group.Key.OwnerId,
                    group.Key.OwnerName,
                    group.Key.MetricName,
                    group.Key.Unit,
                    goodIdentities,
                    badIdentities,
                    evidenceWarnings);
            }
            if (goodIdentities.Length == 0 || badIdentities.Length == 0)
            {
                continue;
            }

            var groupCandidates = new List<ToolRecipeThresholdCandidate>();
            foreach (var kind in
                     Enum.GetValues<ToolRecipeThresholdLimitKind>())
            {
                groupCandidates.Add(SelectBest(
                    group.Key.Scope,
                    group.Key.OwnerId,
                    group.Key.OwnerName,
                    group.Key.MetricName,
                    group.Key.Unit,
                    kind,
                    groupObservations));
            }
            candidates.AddRange(groupCandidates);
            if (assistantMetric
                && groupCandidates.All(candidate => candidate.ErrorCount > 0))
            {
                evidenceWarnings.Add(new ToolRecipeThresholdEvidenceWarning(
                    ToolRecipeThresholdEvidenceWarningKind
                        .OverlappingDistributions,
                    group.Key.Scope,
                    group.Key.OwnerId,
                    group.Key.OwnerName,
                    group.Key.MetricName,
                    group.Key.Unit,
                    goodIdentities.Length,
                    badIdentities.Length,
                    goodIdentities.Concat(badIdentities).ToArray(),
                    $"{group.Key.OwnerName} / {group.Key.MetricName}: "
                    + "Good and Bad observations cannot be separated by any "
                    + "supported Minimum, Maximum, or Range candidate."));
            }
        }

        var heldOutIdentities = result.Samples
            .Where(sample =>
                sample.Role == ToolRecipeValidationSampleRole.HeldOut)
            .OrderBy(sample => sample.Order)
            .Select(sample =>
                $"{sample.Order}:"
                + (string.IsNullOrWhiteSpace(sample.SourceContentSha256)
                    ? sample.SourcePath
                    : sample.SourceContentSha256))
            .ToArray();
        var developmentSampleCount = result.Samples.Count(sample =>
            sample.Role is ToolRecipeValidationSampleRole.Good
                or ToolRecipeValidationSampleRole.Bad);
        AddGlobalRoleWarnings(result, evidenceWarnings);
        var warnings = new List<string>();
        if (candidates.Count == 0)
        {
            warnings.Add(
                "No metric has both finite Good and Bad development observations.");
        }
        if (heldOutIdentities.Length == 0)
        {
            warnings.Add(
                "No Held-out observation is available to prove exclusion.");
        }
        warnings.AddRange(evidenceWarnings.Select(warning => warning.Message));
        var orderedCandidates = candidates
            .OrderBy(candidate => candidate.ErrorCount)
            .ThenBy(candidate => candidate.BadAcceptedCount)
            .ThenBy(candidate => candidate.GoodRejectedCount)
            .ThenBy(candidate => candidate.Scope)
            .ThenBy(candidate => candidate.OwnerId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.MetricName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.LimitKind)
            .ToArray();
        var status = candidates.Count == 0 || evidenceWarnings.Count > 0
            ? ResultStatus.Warning
            : ResultStatus.Pass;

        return new ToolRecipeThresholdCandidateReport(
            ToolRecipeThresholdCandidateReport.CurrentContractVersion,
            status,
            $"{candidates.Count} deterministic candidate(s) from "
            + $"{developmentSampleCount} development sample(s); "
            + $"{heldOutIdentities.Length} Held-out sample(s) excluded; "
            + $"{evidenceWarnings.Count} evidence warning(s)",
            developmentSampleCount,
            heldOutIdentities.Length,
            heldOut.Length,
            heldOutIdentities,
            orderedCandidates,
            warnings,
            evidenceWarnings
                .OrderBy(warning => warning.Scope)
                .ThenBy(warning => warning.OwnerId, StringComparer.Ordinal)
                .ThenBy(warning => warning.MetricName, StringComparer.Ordinal)
                .ThenBy(warning => warning.Kind)
                .ToArray());
    }

    private static bool IsThresholdAssistantMetric(
        ToolRecipeDocument document,
        ToolRecipeEvidenceScope scope,
        string ownerId,
        string metricName)
    {
        if (scope != ToolRecipeEvidenceScope.StepMetric)
        {
            return false;
        }

        var toolId = document.Steps.FirstOrDefault(step =>
            string.Equals(step.Id, ownerId, StringComparison.Ordinal))?.ToolId;
        return toolId is not null
               && ToolRecipeThresholdCandidateParameterMapper
                   .SupportedMappings.Any(mapping =>
                       string.Equals(
                           mapping.ToolId,
                           toolId,
                           StringComparison.Ordinal)
                       && string.Equals(
                           mapping.MetricName,
                           metricName,
                           StringComparison.Ordinal));
    }

    private static string[] DistinctSampleIdentities(
        IReadOnlyList<ToolRecipeMetricObservation> observations,
        ToolRecipeValidationSampleRole role) =>
        observations
            .Where(observation => observation.Role == role)
            .OrderBy(observation => observation.SampleOrder)
            .Select(observation => observation.SampleIdentity)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private static void AddBalanceWarnings(
        ToolRecipeEvidenceScope scope,
        string ownerId,
        string ownerName,
        string metricName,
        string unit,
        IReadOnlyList<string> goodIdentities,
        IReadOnlyList<string> badIdentities,
        ICollection<ToolRecipeThresholdEvidenceWarning> warnings)
    {
        var goodCount = goodIdentities.Count;
        var badCount = badIdentities.Count;
        var identities = goodIdentities.Concat(badIdentities).ToArray();
        if (goodCount == 0)
        {
            Add(
                ToolRecipeThresholdEvidenceWarningKind.MissingGoodSamples,
                "has no finite Good development observation.");
        }
        else if (goodCount < 2)
        {
            Add(
                ToolRecipeThresholdEvidenceWarningKind.InsufficientGoodSamples,
                $"has only {goodCount} Good development sample; at least 2 are required for repeat evidence.");
        }
        if (badCount == 0)
        {
            Add(
                ToolRecipeThresholdEvidenceWarningKind.MissingBadSamples,
                "has no finite Bad development observation.");
        }
        else if (badCount < 2)
        {
            Add(
                ToolRecipeThresholdEvidenceWarningKind.InsufficientBadSamples,
                $"has only {badCount} Bad development sample; at least 2 are required for repeat evidence.");
        }
        if (goodCount > 0 && badCount > 0 && goodCount != badCount)
        {
            Add(
                ToolRecipeThresholdEvidenceWarningKind.ImbalancedSamples,
                $"is imbalanced ({goodCount} Good / {badCount} Bad).");
        }
        return;

        void Add(
            ToolRecipeThresholdEvidenceWarningKind kind,
            string detail) =>
            warnings.Add(new ToolRecipeThresholdEvidenceWarning(
                kind,
                scope,
                ownerId,
                ownerName,
                metricName,
                unit,
                goodCount,
                badCount,
                identities,
                $"{ownerName} / {metricName}: {detail}"));
    }

    private static void AddGlobalRoleWarnings(
        ToolRecipeValidationSetResult result,
        ICollection<ToolRecipeThresholdEvidenceWarning> warnings)
    {
        var good = result.Samples
            .Where(sample => sample.Role == ToolRecipeValidationSampleRole.Good)
            .ToArray();
        var bad = result.Samples
            .Where(sample => sample.Role == ToolRecipeValidationSampleRole.Bad)
            .ToArray();
        if (good.Length > 0 && bad.Length > 0)
        {
            return;
        }

        var kind = good.Length == 0
            ? ToolRecipeThresholdEvidenceWarningKind.MissingGoodSamples
            : ToolRecipeThresholdEvidenceWarningKind.MissingBadSamples;
        var missingRole = good.Length == 0 ? "Good" : "Bad";
        warnings.Add(new ToolRecipeThresholdEvidenceWarning(
            kind,
            null,
            "validation-set",
            "Validation Set",
            "All metrics",
            string.Empty,
            good.Length,
            bad.Length,
            good.Concat(bad)
                .OrderBy(sample => sample.Order)
                .Select(sample => sample.SourceContentSha256)
                .Where(identity => !string.IsNullOrWhiteSpace(identity))
                .Cast<string>()
                .ToArray(),
            $"Validation Set: no {missingRole} development sample is staged."));
    }

    private static ToolRecipeThresholdCandidate SelectBest(
        ToolRecipeEvidenceScope scope,
        string ownerId,
        string ownerName,
        string metricName,
        string unit,
        ToolRecipeThresholdLimitKind kind,
        IReadOnlyList<ToolRecipeMetricObservation> observations)
    {
        var values = observations
            .Select(observation => observation.Value)
            .Distinct()
            .Order()
            .ToArray();
        var evaluated = kind switch
        {
            ToolRecipeThresholdLimitKind.Minimum =>
                MinimumCandidates(values)
                    .Select(minimum => Evaluate(
                        scope,
                        ownerId,
                        ownerName,
                        metricName,
                        unit,
                        kind,
                        minimum,
                        null,
                        observations)),
            ToolRecipeThresholdLimitKind.Maximum =>
                MaximumCandidates(values)
                    .Select(maximum => Evaluate(
                        scope,
                        ownerId,
                        ownerName,
                        metricName,
                        unit,
                        kind,
                        null,
                        maximum,
                        observations)),
            ToolRecipeThresholdLimitKind.Range =>
                values.SelectMany(minimum =>
                    values
                        .Where(maximum => maximum >= minimum)
                        .Select(maximum => Evaluate(
                            scope,
                            ownerId,
                            ownerName,
                            metricName,
                            unit,
                            kind,
                            minimum,
                            maximum,
                            observations))),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                null)
        };

        var ordered = evaluated
            .OrderBy(candidate => candidate.ErrorCount)
            .ThenBy(candidate => candidate.BadAcceptedCount)
            .ThenBy(candidate => candidate.GoodRejectedCount);
        return kind switch
        {
            ToolRecipeThresholdLimitKind.Minimum =>
                ordered.ThenByDescending(candidate => candidate.Minimum).First(),
            ToolRecipeThresholdLimitKind.Maximum =>
                ordered.ThenBy(candidate => candidate.Maximum).First(),
            ToolRecipeThresholdLimitKind.Range =>
                ordered
                    .ThenBy(candidate =>
                        candidate.Maximum!.Value - candidate.Minimum!.Value)
                    .ThenByDescending(candidate => candidate.Minimum)
                    .ThenBy(candidate => candidate.Maximum)
                    .First(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                null)
        };
    }

    private static IEnumerable<double> MinimumCandidates(
        IReadOnlyList<double> values)
    {
        foreach (var value in values)
        {
            yield return value;
        }

        var rejectAll = Math.BitIncrement(values[^1]);
        if (double.IsFinite(rejectAll))
        {
            yield return rejectAll;
        }
    }

    private static IEnumerable<double> MaximumCandidates(
        IReadOnlyList<double> values)
    {
        var rejectAll = Math.BitDecrement(values[0]);
        if (double.IsFinite(rejectAll))
        {
            yield return rejectAll;
        }
        foreach (var value in values)
        {
            yield return value;
        }
    }

    private static ToolRecipeThresholdCandidate Evaluate(
        ToolRecipeEvidenceScope scope,
        string ownerId,
        string ownerName,
        string metricName,
        string unit,
        ToolRecipeThresholdLimitKind kind,
        double? minimum,
        double? maximum,
        IReadOnlyList<ToolRecipeMetricObservation> observations)
    {
        var decisions = observations
            .OrderBy(observation => observation.SampleOrder)
            .Select(observation =>
            {
                var accepted = kind switch
                {
                    ToolRecipeThresholdLimitKind.Minimum =>
                        observation.Value >= minimum,
                    ToolRecipeThresholdLimitKind.Maximum =>
                        observation.Value <= maximum,
                    ToolRecipeThresholdLimitKind.Range =>
                        observation.Value >= minimum
                        && observation.Value <= maximum,
                    _ => false
                };
                var predicted = accepted
                    ? ToolRecipeValidationSampleRole.Good
                    : ToolRecipeValidationSampleRole.Bad;
                var decision = (observation.Role, predicted) switch
                {
                    (ToolRecipeValidationSampleRole.Good,
                        ToolRecipeValidationSampleRole.Good) =>
                        ToolRecipeThresholdDecisionKind.CorrectGood,
                    (ToolRecipeValidationSampleRole.Good,
                        ToolRecipeValidationSampleRole.Bad) =>
                        ToolRecipeThresholdDecisionKind.FalseReject,
                    (ToolRecipeValidationSampleRole.Bad,
                        ToolRecipeValidationSampleRole.Bad) =>
                        ToolRecipeThresholdDecisionKind.CorrectBad,
                    _ => ToolRecipeThresholdDecisionKind.FalseAccept
                };
                return new ToolRecipeThresholdSampleDecision(
                    observation.SampleOrder,
                    observation.SampleIdentity,
                    observation.SourcePath,
                    observation.Role,
                    predicted,
                    decision,
                    observation.Value,
                    observation.EvidenceLocator);
            })
            .ToArray();
        var canonical =
            $"{scope}|{ownerId}|{metricName}|{unit}|{kind}|"
            + $"{minimum?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "-"}|"
            + $"{maximum?.ToString("R", System.Globalization.CultureInfo.InvariantCulture) ?? "-"}";
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        return new ToolRecipeThresholdCandidate(
            $"threshold.{hash[..16].ToLowerInvariant()}",
            scope,
            ownerId,
            ownerName,
            metricName,
            unit,
            kind,
            minimum,
            maximum,
            decisions.Count(decision =>
                decision.Decision
                == ToolRecipeThresholdDecisionKind.CorrectGood),
            decisions.Count(decision =>
                decision.Decision
                == ToolRecipeThresholdDecisionKind.FalseReject),
            decisions.Count(decision =>
                decision.Decision
                == ToolRecipeThresholdDecisionKind.CorrectBad),
            decisions.Count(decision =>
                decision.Decision
                == ToolRecipeThresholdDecisionKind.FalseAccept),
            decisions);
    }
}

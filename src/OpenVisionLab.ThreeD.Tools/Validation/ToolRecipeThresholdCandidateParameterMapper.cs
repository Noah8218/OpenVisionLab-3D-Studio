using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record ToolRecipeThresholdMappingCoverage(
    string ToolId,
    string MetricName,
    ToolRecipeThresholdLimitKind LimitKind,
    IReadOnlyList<string> ParameterNames);

/// <summary>
/// Explicit fail-closed mapping from reviewed metric candidates to existing
/// typed recipe parameters. Display names and fuzzy matching are forbidden.
/// </summary>
public static class ToolRecipeThresholdCandidateParameterMapper
{
    private static readonly ToolRecipeThresholdMappingCoverage[] mappings =
    [
        new(
            "thickness",
            "Mean",
            ToolRecipeThresholdLimitKind.Minimum,
            ["MinimumThickness"]),
        new(
            "thickness",
            "Mean",
            ToolRecipeThresholdLimitKind.Maximum,
            ["MaximumThickness"]),
        new(
            "thickness",
            "Mean",
            ToolRecipeThresholdLimitKind.Range,
            ["MinimumThickness", "MaximumThickness"]),
        new(
            "warpage",
            "PeakToValley",
            ToolRecipeThresholdLimitKind.Maximum,
            ["MaximumPeakToValley"]),
        new(
            "warpage",
            "Rms",
            ToolRecipeThresholdLimitKind.Maximum,
            ["MaximumRms"]),
        new(
            "completeness-grid",
            C3DCompletenessMetricNames.MinimumFiniteCoverage,
            ToolRecipeThresholdLimitKind.Minimum,
            ["MinimumFiniteCoverageRatio"]),
        new(
            "completeness-grid",
            C3DCompletenessMetricNames.MinimumReferenceRelativeMean,
            ToolRecipeThresholdLimitKind.Minimum,
            ["MinimumReferenceRelativeMeanRawHeight"]),
        new(
            "completeness-grid",
            C3DCompletenessMetricNames.MaximumReferenceRelativeMean,
            ToolRecipeThresholdLimitKind.Maximum,
            ["MaximumReferenceRelativeMeanRawHeight"])
    ];

    public static IReadOnlyList<ToolRecipeThresholdMappingCoverage>
        SupportedMappings => mappings;

    public static bool TryCreateProposal(
        ToolRecipeDocument document,
        ToolRecipeThresholdCandidate candidate,
        out ToolRecipeThresholdParameterProposal? proposal,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(candidate);
        proposal = null;

        if (candidate.Scope != ToolRecipeEvidenceScope.StepMetric)
        {
            message =
                "Only a step metric with an explicit typed parameter mapping can be applied.";
            return false;
        }

        var step = document.Steps.FirstOrDefault(item =>
            string.Equals(item.Id, candidate.OwnerId, StringComparison.Ordinal));
        if (step is null)
        {
            message =
                $"Candidate owner step '{candidate.OwnerId}' is not present in the current recipe.";
            return false;
        }

        if (!TryMap(step, candidate, out var proposedValues, out message))
        {
            return false;
        }

        var changes = new List<ToolRecipeThresholdParameterChange>();
        foreach (var pair in proposedValues)
        {
            var parameter = step.Parameters.FirstOrDefault(item =>
                string.Equals(item.Name, pair.Key, StringComparison.Ordinal));
            if (parameter is null)
            {
                message =
                    $"Mapped parameter '{pair.Key}' is missing from step '{step.Id}'.";
                return false;
            }

            changes.Add(new ToolRecipeThresholdParameterChange(
                pair.Key,
                parameter.Value,
                pair.Value));
        }

        proposal = new ToolRecipeThresholdParameterProposal(
            ToolRecipeThresholdParameterProposal.CurrentContractVersion,
            candidate.CandidateId,
            step.Id,
            step.ToolId,
            step.ToolName,
            candidate.MetricName,
            candidate.LimitKind,
            changes,
            candidate);
        message =
            $"{step.ToolName} {candidate.MetricName} maps to {changes.Count} typed parameter draft value(s).";
        return true;
    }

    public static ToolRecipeDocument ApplyProposal(
        ToolRecipeDocument document,
        ToolRecipeThresholdParameterProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(proposal);
        var stepFound = false;
        var steps = document.Steps.Select(step =>
        {
            if (!string.Equals(
                    step.Id,
                    proposal.StepId,
                    StringComparison.Ordinal))
            {
                return step;
            }

            stepFound = true;
            if (!string.Equals(step.ToolId, proposal.ToolId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Threshold proposal tool '{proposal.ToolId}' does not match current step tool '{step.ToolId}'.");
            }

            var values = proposal.Changes.ToDictionary(
                change => change.ParameterName,
                change => change.ProposedValue,
                StringComparer.Ordinal);
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            var parameters = step.Parameters.Select(parameter =>
            {
                if (!values.TryGetValue(parameter.Name, out var value))
                {
                    return parameter;
                }

                consumed.Add(parameter.Name);
                return parameter with { Value = value };
            }).ToArray();
            if (consumed.Count != values.Count)
            {
                var missing = values.Keys.Except(consumed, StringComparer.Ordinal);
                throw new InvalidDataException(
                    $"Threshold proposal parameters are missing from the current step: {string.Join(", ", missing)}.");
            }

            return step with { Parameters = parameters };
        }).ToArray();

        if (!stepFound)
        {
            throw new InvalidDataException(
                $"Threshold proposal step '{proposal.StepId}' is not present in the current recipe.");
        }

        return document with { Steps = steps };
    }

    private static bool TryMap(
        ToolRecipeStep step,
        ToolRecipeThresholdCandidate candidate,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        message = string.Empty;
        var mapping = mappings.SingleOrDefault(item =>
            string.Equals(item.ToolId, step.ToolId, StringComparison.Ordinal)
            && string.Equals(
                item.MetricName,
                candidate.MetricName,
                StringComparison.Ordinal)
            && item.LimitKind == candidate.LimitKind);
        if (mapping is null)
        {
            message =
                $"No explicit threshold mapping exists for tool '{step.ToolId}', metric '{candidate.MetricName}', and rule '{candidate.LimitKind}'.";
            return false;
        }

        if (string.Equals(step.ToolId, "thickness", StringComparison.Ordinal)
            && string.Equals(candidate.MetricName, "Mean", StringComparison.Ordinal))
        {
            return TryMapThickness(candidate, out values, out message);
        }

        if (string.Equals(step.ToolId, "warpage", StringComparison.Ordinal)
            && candidate.LimitKind == ToolRecipeThresholdLimitKind.Maximum)
        {
            var parameterName = candidate.MetricName switch
            {
                "PeakToValley" => "MaximumPeakToValley",
                "Rms" => "MaximumRms",
                _ => null
            };
            if (parameterName is not null
                && TryFormat(candidate.Maximum, out var maximum))
            {
                values = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [parameterName] = maximum
                };
                return true;
            }
        }

        if (string.Equals(
                step.ToolId,
                "completeness-grid",
                StringComparison.Ordinal))
        {
            return TryMapCompleteness(candidate, out values, out message);
        }

        message =
            $"Declared threshold mapping for tool '{step.ToolId}', metric '{candidate.MetricName}', and rule '{candidate.LimitKind}' could not produce a finite typed value.";
        return false;
    }

    private static bool TryMapCompleteness(
        ToolRecipeThresholdCandidate candidate,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        values = new Dictionary<string, string>(StringComparer.Ordinal);
        var parameterName = candidate.MetricName switch
        {
            C3DCompletenessMetricNames.MinimumFiniteCoverage
                when candidate.LimitKind
                    == ToolRecipeThresholdLimitKind.Minimum =>
                "MinimumFiniteCoverageRatio",
            C3DCompletenessMetricNames.MinimumReferenceRelativeMean
                when candidate.LimitKind
                    == ToolRecipeThresholdLimitKind.Minimum =>
                "MinimumReferenceRelativeMeanRawHeight",
            C3DCompletenessMetricNames.MaximumReferenceRelativeMean
                when candidate.LimitKind
                    == ToolRecipeThresholdLimitKind.Maximum =>
                "MaximumReferenceRelativeMeanRawHeight",
            _ => null
        };
        var candidateValue = candidate.LimitKind
            == ToolRecipeThresholdLimitKind.Minimum
                ? candidate.Minimum
                : candidate.Maximum;
        if (parameterName is null
            || !TryFormat(candidateValue, out var formatted))
        {
            message =
                "The Completeness candidate does not contain the finite bound required by its exact typed mapping.";
            return false;
        }
        if (parameterName == "MinimumFiniteCoverageRatio"
            && candidateValue is not (>= 0d and <= 1d))
        {
            message =
                "Minimum finite coverage must remain between zero and one inclusive.";
            return false;
        }

        values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [parameterName] = formatted
        };
        message = string.Empty;
        return true;
    }

    private static bool TryMapThickness(
        ToolRecipeThresholdCandidate candidate,
        out IReadOnlyDictionary<string, string> values,
        out string message)
    {
        var mapped = new Dictionary<string, string>(StringComparer.Ordinal);
        if (candidate.LimitKind
                is ToolRecipeThresholdLimitKind.Minimum
                or ToolRecipeThresholdLimitKind.Range)
        {
            if (!TryFormat(candidate.Minimum, out var minimum))
            {
                values = mapped;
                message = "The selected candidate has no finite minimum.";
                return false;
            }
            mapped["MinimumThickness"] = minimum;
        }
        if (candidate.LimitKind
                is ToolRecipeThresholdLimitKind.Maximum
                or ToolRecipeThresholdLimitKind.Range)
        {
            if (!TryFormat(candidate.Maximum, out var maximum))
            {
                values = mapped;
                message = "The selected candidate has no finite maximum.";
                return false;
            }
            mapped["MaximumThickness"] = maximum;
        }

        values = mapped;
        message = string.Empty;
        return mapped.Count > 0;
    }

    private static bool TryFormat(double? value, out string formatted)
    {
        if (value is null || !double.IsFinite(value.Value))
        {
            formatted = string.Empty;
            return false;
        }

        formatted = value.Value.ToString("G17", CultureInfo.InvariantCulture);
        return true;
    }
}

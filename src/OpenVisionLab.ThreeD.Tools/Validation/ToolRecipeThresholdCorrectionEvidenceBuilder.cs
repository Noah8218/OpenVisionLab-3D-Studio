using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeThresholdCorrectionEvidenceBuilder
{
    public static ToolRecipeThresholdCorrectionEvidence Build(
        ToolRecipeDocument document,
        ToolRecipeThresholdParameterProposal proposal,
        ToolRecipeValidationSetResult heldOutResult)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(heldOutResult);
        if (heldOutResult.Samples.Any(sample =>
                sample.Role != ToolRecipeValidationSampleRole.HeldOut))
        {
            throw new InvalidDataException(
                "Threshold correction evidence accepts Held-out replay samples only.");
        }

        var samples = heldOutResult.Samples.Select(sample =>
            new ToolRecipeThresholdHeldOutSampleEvidence(
                sample.Order,
                sample.SourceContentSha256,
                sample.SourcePath,
                sample.Status,
                sample.Message,
                sample.Steps.SelectMany(step => step.Metrics.Select(metric =>
                    new ToolRecipeThresholdHeldOutMetricEvidence(
                        step.StepId,
                        step.ToolName,
                        metric.Name,
                        metric.Unit,
                        metric.Value,
                        metric.Status ?? step.Status))).ToArray())).ToArray();
        return new ToolRecipeThresholdCorrectionEvidence(
            ToolRecipeThresholdCorrectionEvidence.CurrentContractVersion,
            document.Name,
            document.Source.ContentSha256 ?? string.Empty,
            proposal,
            heldOutResult.Status,
            heldOutResult.Message,
            samples);
    }

    public static ToolRecipeThresholdCorrectionEvidence BuildManualCorrection(
        ToolRecipeDocument document,
        ToolRecipeThresholdParameterProposal proposal,
        IReadOnlyList<ToolRecipeThresholdManualParameterChange> manualChanges,
        ToolRecipeValidationSetResult beforeDevelopmentResult,
        ToolRecipeValidationSetResult afterDevelopmentResult,
        ToolRecipeValidationSetResult heldOutResult)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(manualChanges);
        ArgumentNullException.ThrowIfNull(beforeDevelopmentResult);
        ArgumentNullException.ThrowIfNull(afterDevelopmentResult);
        ArgumentNullException.ThrowIfNull(heldOutResult);

        var before = BuildDevelopmentSamples(
            beforeDevelopmentResult,
            "Before-development");
        var after = BuildDevelopmentSamples(
            afterDevelopmentResult,
            "After-development");
        var beforeMismatchCount = before.Count(sample => !sample.ExpectedMatch);
        var afterMismatchCount = after.Count(sample => !sample.ExpectedMatch);
        if (beforeMismatchCount == 0)
        {
            throw new InvalidDataException(
                "Manual threshold correction requires at least one genuine development-set mismatch before correction.");
        }
        if (afterMismatchCount != 0)
        {
            throw new InvalidDataException(
                $"Manual threshold correction still has {afterMismatchCount} development-set mismatch(es).");
        }
        if (manualChanges.Count == 0
            || manualChanges.All(change => string.Equals(
                change.SuggestedValue,
                change.ManualValue,
                StringComparison.Ordinal)))
        {
            throw new InvalidDataException(
                "Manual threshold correction must differ from the deterministic suggestion.");
        }
        if (!before.Select(SampleKey).SequenceEqual(
                after.Select(SampleKey),
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Before and after development evidence must contain the same ordered sample identities and roles.");
        }

        var legacy = Build(document, proposal, heldOutResult);
        return legacy with
        {
            ManualCorrection = new ToolRecipeThresholdManualCorrectionEvidence(
                ToolRecipeThresholdManualCorrectionEvidence
                    .CurrentContractVersion,
                manualChanges.ToArray(),
                beforeMismatchCount,
                before,
                afterMismatchCount,
                after)
        };
    }

    public static bool IsExpectedMatch(
        ToolRecipeValidationSampleResult sample) =>
        sample.Role switch
        {
            ToolRecipeValidationSampleRole.Good =>
                sample.Status == ResultStatus.Pass,
            ToolRecipeValidationSampleRole.Bad =>
                sample.Status == ResultStatus.Fail,
            _ => false
        };

    private static IReadOnlyList<ToolRecipeThresholdDevelopmentSampleEvidence>
        BuildDevelopmentSamples(
            ToolRecipeValidationSetResult result,
            string stage)
    {
        if (result.Samples.Count == 0
            || result.Samples.Any(sample =>
                sample.Role is not (
                    ToolRecipeValidationSampleRole.Good
                    or ToolRecipeValidationSampleRole.Bad)))
        {
            throw new InvalidDataException(
                $"{stage} threshold evidence accepts non-empty Good/Bad development samples only.");
        }

        return result.Samples.Select(sample =>
            new ToolRecipeThresholdDevelopmentSampleEvidence(
                sample.Order,
                sample.SourceContentSha256,
                sample.SourcePath,
                sample.Role,
                sample.Status,
                IsExpectedMatch(sample),
                sample.Message,
                BuildMetrics(sample))).ToArray();
    }

    private static IReadOnlyList<ToolRecipeThresholdHeldOutMetricEvidence>
        BuildMetrics(ToolRecipeValidationSampleResult sample) =>
        sample.Steps.SelectMany(step => step.Metrics.Select(metric =>
            new ToolRecipeThresholdHeldOutMetricEvidence(
                step.StepId,
                step.ToolName,
                metric.Name,
                metric.Unit,
                metric.Value,
                metric.Status ?? step.Status))).ToArray();

    private static string SampleKey(
        ToolRecipeThresholdDevelopmentSampleEvidence sample) =>
        $"{sample.SampleOrder}|{sample.SampleIdentity}|{sample.Role}";
}

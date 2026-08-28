using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Ordered-recipe adapter for D-07. The connected-region artifact is the
/// complete validated domain; per-region selection remains E-16.
/// </summary>
public static class ToolRecipeDomainMaskExecution
{
    public static C3DDomainMaskEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot source,
        C3DConnectedRegionArtifact domain,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(domain);

        var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
        if (!validation.IsValid)
        {
            return Error(string.Join(" ", validation.Errors));
        }

        var step = document.Steps.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (step is null)
        {
            return Error($"Recipe must contain exactly one step with ID '{stepId}'.");
        }
        if (!string.Equals(step.ToolId, "domain-mask", StringComparison.Ordinal))
        {
            return Error($"Step '{step.Id}' is not the Domain / Mask v1 adapter.");
        }
        if (step.InputEntityIds.Count != 2
            || !string.Equals(step.InputEntityIds[0], source.EntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(step.InputEntityIds[1], domain.ArtifactId, StringComparison.OrdinalIgnoreCase))
        {
            return Error("Domain / Mask v1 requires the supplied HeightField followed by its complete ConnectedRegionArtifact domain.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return C3DDomainMaskRule.Evaluate(
            new C3DDomainMaskInput(step.Id, source, domain, step.OutputEntityId),
            cancellationToken);
    }

    private static C3DDomainMaskEvaluation Error(string message) => new(
        new ToolResult(C3DDomainMaskRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []),
        null);
}

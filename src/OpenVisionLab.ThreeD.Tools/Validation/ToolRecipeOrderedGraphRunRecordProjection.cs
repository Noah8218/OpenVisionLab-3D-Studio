using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Projects one completed ordered graph execution into the shared Run Record
/// step contract. It does not execute or mutate the recipe.
/// </summary>
public static class ToolRecipeOrderedGraphRunRecordProjection
{
    public static IReadOnlyList<InspectionRunStepResult> Create(
        ToolRecipeDocument document,
        ToolRecipeOrderedGraphExecutionResult execution)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(execution);

        return execution.Steps.Select(step =>
        {
            var recipeIndex = step.Order - 1;
            if (recipeIndex < 0 || recipeIndex >= document.Steps.Count)
            {
                throw new InvalidDataException(
                    $"Ordered Run step {step.Order} is outside the recipe step range.");
            }

            var recipeStep = document.Steps[recipeIndex];
            if (!string.Equals(recipeStep.Id, step.StepId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Ordered Run step {step.Order} identity mismatch: recipe '{recipeStep.Id}', execution '{step.StepId}'.");
            }

            return new InspectionRunStepResult(
                recipeIndex,
                recipeStep.Id,
                recipeStep.ToolId,
                step.Result.ToolName,
                recipeStep.InputEntityIds,
                recipeStep.OutputEntityId,
                step.Result.Status,
                step.Result.Message,
                step.Result.Elapsed.TotalMilliseconds,
                step.Result.Metrics
                    .Where(metric => double.IsFinite(metric.Value))
                    .Select(metric => new InspectionRunMetric(
                        metric.Name,
                        metric.Kind,
                        metric.Value,
                        metric.Unit,
                        metric.Status))
                    .ToArray(),
                step.Result.Overlays
                    .Select(overlay => new InspectionRunOverlay(
                        overlay.Id,
                        overlay.Kind,
                        overlay.Label,
                        overlay.Status,
                        overlay.SourceEntityId))
                    .ToArray())
            {
                OutputContentSha256 = step.OutputContentSha256
            };
        }).ToArray();
    }
}

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
                OutputContentSha256 = step.OutputContentSha256,
                Timing = CreateToolTiming(step.Result.Elapsed.TotalMilliseconds),
                CompletenessGrid = ValidateCompletenessGrid(recipeStep, step),
                PresenceCheck = ValidatePresenceCheck(recipeStep, step)
            };
        }).ToArray();
    }

    private static C3DPresenceCheckOutput? ValidatePresenceCheck(
        ToolRecipeStep recipeStep,
        ToolRecipeOrderedGraphStepResult step)
    {
        var output = step.PresenceCheck;
        if (output is null)
        {
            if (string.Equals(
                    recipeStep.ToolId,
                    "presence-check",
                    StringComparison.Ordinal)
                && step.Result.Status is not (ResultStatus.Error or ResultStatus.NotRun))
            {
                throw new InvalidDataException(
                    $"Presence Check output for successful step '{recipeStep.Id}' is missing.");
            }

            return null;
        }

        var feature = output.Feature;
        var valid = string.Equals(
                recipeStep.ToolId,
                "presence-check",
                StringComparison.Ordinal)
            && string.Equals(
                recipeStep.OutputEntityId,
                output.OutputEntityId,
                StringComparison.Ordinal)
            && string.Equals(
                step.OutputContentSha256,
                output.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(output.RootSourceEntityId)
            && !string.IsNullOrWhiteSpace(output.InputEntityId)
            && !string.IsNullOrWhiteSpace(output.InputContentSha256)
            && !string.IsNullOrWhiteSpace(output.Unit)
            && !string.IsNullOrWhiteSpace(output.FrameId)
            && !string.IsNullOrWhiteSpace(output.FeatureSelectionId)
            && !string.IsNullOrWhiteSpace(output.ContentSha256)
            && output.Policy is not null
            && feature is not null
            && string.Equals(
                output.FeatureSelectionId,
                feature.FeatureId,
                StringComparison.Ordinal)
            && output.FeatureRegion == feature.Region
            && feature.TotalCellCount > 0
            && feature.FiniteCellCount >= 0
            && feature.MissingCellCount >= 0
            && feature.FiniteCellCount + feature.MissingCellCount
                == feature.TotalCellCount
            && double.IsFinite(feature.FiniteCoverageRatio)
            && feature.FiniteCoverageRatio is >= 0d and <= 1d
            && (feature.MeanRawHeight is not { } mean
                || double.IsFinite(mean))
            && feature.Decision is ResultStatus.Pass or ResultStatus.Fail
            && !string.IsNullOrWhiteSpace(feature.DecisionReason)
            && (output.Policy.MinimumFiniteCoverageRatio is >= 0d and <= 1d)
            && double.IsFinite(output.Policy.MinimumMeanRawHeight)
            && double.IsFinite(output.Policy.MaximumMeanRawHeight)
            && output.Policy.MinimumMeanRawHeight
                <= output.Policy.MaximumMeanRawHeight
            && ((feature.Decision == ResultStatus.Pass
                    && feature.MeanRawHeight is { } passMean
                    && feature.FiniteCoverageRatio
                        >= output.Policy.MinimumFiniteCoverageRatio
                    && passMean >= output.Policy.MinimumMeanRawHeight
                    && passMean <= output.Policy.MaximumMeanRawHeight)
                || (feature.Decision == ResultStatus.Fail));
        if (!valid)
        {
            throw new InvalidDataException(
                $"Presence Check output for step '{recipeStep.Id}' is incomplete or incompatible with its ordered Run evidence.");
        }

        return output;
    }

    private static C3DCompletenessGridMetricOutput? ValidateCompletenessGrid(
        ToolRecipeStep recipeStep,
        ToolRecipeOrderedGraphStepResult step)
    {
        var output = step.CompletenessGrid;
        if (output is null)
        {
            if (string.Equals(
                    recipeStep.ToolId,
                    "completeness-grid",
                    StringComparison.Ordinal)
                && step.Result.Status is not (ResultStatus.Error or ResultStatus.NotRun))
            {
                throw new InvalidDataException(
                    $"Completeness output for successful step '{recipeStep.Id}' is missing.");
            }

            return null;
        }

        var cells = output.Cells ?? [];
        var valid = string.Equals(
                recipeStep.ToolId,
                "completeness-grid",
                StringComparison.Ordinal)
            && string.Equals(
                recipeStep.OutputEntityId,
                output.OutputEntityId,
                StringComparison.Ordinal)
            && string.Equals(
                step.OutputContentSha256,
                output.ContentSha256,
                StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(output.Unit)
            && !string.IsNullOrWhiteSpace(output.FrameId)
            && !string.IsNullOrWhiteSpace(output.ContentSha256)
            && double.IsFinite(output.ReferenceMeanRawHeight)
            && cells.Count == output.Profile.Rows * output.Profile.Columns
            && cells.Select(cell => cell.CellId)
                .Distinct(StringComparer.Ordinal).Count() == cells.Count
            && cells.All(cell =>
                !string.IsNullOrWhiteSpace(cell.CellId)
                && cell.GridRow >= 0
                && cell.GridColumn >= 0
                && cell.Region.Row >= 0
                && cell.Region.Column >= 0
                && cell.Region.RowCount > 0
                && cell.Region.ColumnCount > 0
                && cell.TotalCellCount > 0
                && cell.FiniteCellCount >= 0
                && cell.MissingCellCount >= 0
                && cell.FiniteCellCount + cell.MissingCellCount
                    == cell.TotalCellCount
                && double.IsFinite(cell.FiniteCoverageRatio)
                && cell.FiniteCoverageRatio is >= 0.0 and <= 1.0
                && (cell.MeanRawHeight is not { } mean || double.IsFinite(mean))
                && (cell.ReferenceRelativeMeanRawHeight is not { } relative
                    || double.IsFinite(relative))
                && (cell.Decision is null
                    || !string.IsNullOrWhiteSpace(cell.DecisionReason)));
        if (!valid)
        {
            throw new InvalidDataException(
                $"Completeness output for step '{recipeStep.Id}' is incomplete or incompatible with its ordered Run evidence.");
        }

        return output;
    }

    private static InspectionRunTiming CreateToolTiming(
        double elapsedMilliseconds) =>
        InspectionRunTiming.Available(
            InspectionRunTiming.StopwatchClock,
            elapsedMilliseconds,
            [
                new InspectionRunStageTiming(
                    InspectionRunTiming.ToolExecutionStage,
                    elapsedMilliseconds)
            ],
            "Existing ToolResult elapsed observation; no additional execution.");
}

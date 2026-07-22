using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record ToolRecipeTransformedHeightFieldMeasurementSequenceOutput(
    C3DTransformedHeightField HeightField,
    ToolRecipeHeightMeasurementOutput Measurement);

public sealed record ToolRecipeTransformedHeightFieldMeasurementSequenceEvaluation(
    ToolResult Result,
    ToolRecipeTransformedHeightFieldMeasurementSequenceOutput? Output);

public sealed record ToolRecipeOrderedHeightMeasurementStepOutput(
    int RecipeIndex,
    string StepId,
    string ToolId,
    ToolRecipeHeightMeasurementOutput Output);

public sealed record ToolRecipeOrderedTransformedHeightFieldExecutionOutput(
    C3DTransformedHeightField HeightField,
    IReadOnlyList<ToolRecipeOrderedHeightMeasurementStepOutput> Measurements);

public sealed record ToolRecipeOrderedTransformedHeightFieldExecutionEvaluation(
    ToolResult Result,
    ToolRecipeOrderedTransformedHeightFieldExecutionOutput? Output);

/// <summary>
/// Minimal reusable ordered Runner slice. It consumes one explicit Published
/// A2 cloud, then executes the authored A3 and measurement adapters in recipe
/// order. It never invents or implicitly publishes an upstream artifact.
/// </summary>
public static class ToolRecipeTransformedHeightFieldMeasurementSequence
{
    /// <summary>
    /// Executes A3 and every following supported measurement step in authored recipe
    /// order. V1 deliberately rejects every other downstream tool instead of
    /// pretending to be an arbitrary graph executor.
    /// </summary>
    public static ToolRecipeOrderedTransformedHeightFieldExecutionEvaluation ExecuteOrdered(
        ToolRecipeDocument document,
        string regridStepId,
        C3DTransformedPointCloud publishedTransformedPointCloud,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var regridIndex = IndexOf(document, regridStepId);
        if (regridIndex < 0)
        {
            return OrderedError("Ordered execution requires an existing Re-grid step.");
        }

        var downstream = document.Steps
            .Select((step, index) => (Step: step, Index: index))
            .Where(candidate => candidate.Index > regridIndex)
            .ToArray();
        if (downstream.Length == 0)
        {
            return OrderedError("Ordered execution requires at least one measurement step after Re-grid.");
        }
        var unsupported = downstream.FirstOrDefault(candidate => candidate.Step.ToolId is not ("thickness" or "warpage" or "plane-flatness" or "point-pair-dimensions" or "gap-flush" or "volume"));
        if (unsupported.Step is not null)
        {
            return OrderedError($"Ordered execution v1 does not support downstream tool '{unsupported.Step.ToolId}' at step '{unsupported.Step.Id}'.");
        }

        var regrid = ToolRecipeRegridHeightFieldExecution.Execute(
            document, regridStepId, publishedTransformedPointCloud, cancellationToken);
        if (regrid.Output is null || regrid.Result.Status == ResultStatus.Error)
        {
            return OrderedError($"A3 Re-grid failed: {regrid.Result.Message}");
        }
        if (!regrid.Output.MeetsMinimumCoverage)
        {
            return OrderedError("A3 Re-grid output did not meet its authored Publish coverage gate.");
        }

        var measurements = new List<ToolRecipeOrderedHeightMeasurementStepOutput>(downstream.Length);
        foreach (var candidate in downstream)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var measurement = ToolRecipeHeightMeasurementExecution.Execute(
                document, candidate.Step.Id, regrid.Output, recipeDirectory, cancellationToken);
            if (measurement.Output is null || measurement.Result.Status is ResultStatus.Error or ResultStatus.NotRun)
            {
                return OrderedError($"Measurement step '{candidate.Step.Id}' failed: {measurement.Result.Message}");
            }
            measurements.Add(new ToolRecipeOrderedHeightMeasurementStepOutput(
                candidate.Index, candidate.Step.Id, candidate.Step.ToolId, measurement.Output));
        }

        var status = measurements.Any(item => item.Output.Result.Status == ResultStatus.Fail)
            ? ResultStatus.Fail
            : measurements.Any(item => item.Output.Result.Status == ResultStatus.Warning)
                ? ResultStatus.Warning
                : ResultStatus.Pass;
        var elapsed = regrid.Result.Elapsed + measurements.Aggregate(
            TimeSpan.Zero, (total, item) => total + item.Output.Result.Elapsed);
        var result = new ToolResult(
            "Ordered TransformedHeightField measurements",
            status,
            $"Executed A3 and {measurements.Count} measurement step(s) in authored recipe order.",
            elapsed,
            measurements.SelectMany(item => item.Output.Result.Metrics).ToArray(),
            measurements.SelectMany(item => item.Output.Result.Overlays).ToArray());
        return new ToolRecipeOrderedTransformedHeightFieldExecutionEvaluation(
            result,
            new ToolRecipeOrderedTransformedHeightFieldExecutionOutput(regrid.Output, measurements));
    }

    public static ToolRecipeTransformedHeightFieldMeasurementSequenceEvaluation Execute(
        ToolRecipeDocument document,
        string regridStepId,
        string measurementStepId,
        C3DTransformedPointCloud publishedTransformedPointCloud,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        var regridIndex = IndexOf(document, regridStepId);
        var measurementIndex = IndexOf(document, measurementStepId);
        if (regridIndex < 0 || measurementIndex <= regridIndex)
        {
            return Error("Ordered execution requires an existing Re-grid step before the requested measurement step.");
        }

        var regrid = ToolRecipeRegridHeightFieldExecution.Execute(
            document, regridStepId, publishedTransformedPointCloud, cancellationToken);
        if (regrid.Output is null || regrid.Result.Status == ResultStatus.Error)
        {
            return Error($"A3 Re-grid failed: {regrid.Result.Message}");
        }
        if (!regrid.Output.MeetsMinimumCoverage)
        {
            return Error("A3 Re-grid output did not meet its authored Publish coverage gate.");
        }

        var measurement = ToolRecipeHeightMeasurementExecution.Execute(
            document, measurementStepId, regrid.Output, recipeDirectory, cancellationToken);
        if (measurement.Output is null || measurement.Result.Status == ResultStatus.Error)
        {
            return Error($"Measurement failed: {measurement.Result.Message}");
        }

        return new ToolRecipeTransformedHeightFieldMeasurementSequenceEvaluation(
            measurement.Result,
            new ToolRecipeTransformedHeightFieldMeasurementSequenceOutput(regrid.Output, measurement.Output));
    }

    private static int IndexOf(ToolRecipeDocument document, string stepId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        for (var index = 0; index < document.Steps.Count; index++)
        {
            if (string.Equals(document.Steps[index].Id, stepId, StringComparison.OrdinalIgnoreCase)) return index;
        }
        return -1;
    }

    private static ToolRecipeTransformedHeightFieldMeasurementSequenceEvaluation Error(string message) =>
        new(new ToolResult("Ordered TransformedHeightField measurement", ResultStatus.Error, message, TimeSpan.Zero, [], []), null);

    private static ToolRecipeOrderedTransformedHeightFieldExecutionEvaluation OrderedError(string message) =>
        new(new ToolResult("Ordered TransformedHeightField measurements", ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
}

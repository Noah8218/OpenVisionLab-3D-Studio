using System.Diagnostics;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DGridPolygonDomainEvaluation(
    ToolResult Result,
    C3DGridDomainArtifact? Output);

/// <summary>
/// Typed recipe producer for an authored GridPolygon domain. It reuses the
/// Core rasterizer and publishes a source-bound artifact; it does not perform
/// mask arithmetic or infer physical coordinates.
/// </summary>
public static class ToolRecipeGridPolygonDomainExecution
{
    public static C3DGridPolygonDomainEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot source,
        ToolRecipeSelection selection,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selection);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
            if (!validation.IsValid)
            {
                return Error(string.Join(" ", validation.Errors), stopwatch);
            }

            var step = document.Steps.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase));
            if (step is null)
            {
                return Error($"Recipe must contain exactly one step with ID '{stepId}'.", stopwatch);
            }
            if (!string.Equals(step.ToolId, "grid-polygon-domain", StringComparison.Ordinal))
            {
                return Error($"Step '{step.Id}' is not the Grid Polygon Domain v1 adapter.", stopwatch);
            }
            if (step.InputEntityIds.Count != 2
                || !string.Equals(step.InputEntityIds[0], source.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(step.InputEntityIds[1], selection.Id, StringComparison.OrdinalIgnoreCase))
            {
                return Error(
                    "Grid Polygon Domain v1 requires the supplied HeightField followed by its recipe-owned GridPolygon selection.",
                    stopwatch);
            }

            var documentSelection = document.Selections?.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, selection.Id, StringComparison.OrdinalIgnoreCase));
            if (documentSelection is null)
            {
                return Error($"Selection '{selection.Id}' is not present in the recipe document.", stopwatch);
            }
            if (documentSelection != selection)
            {
                selection = documentSelection;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var artifact = C3DGridDomainArtifactFactory.Create(
                step.OutputEntityId,
                step.ToolName,
                source,
                selection,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            stopwatch.Stop();
            return new(
                new ToolResult(
                    "Grid Polygon Domain",
                    ResultStatus.Pass,
                    "Completed - the authored GridPolygon was published as a source-bound domain artifact; source data remains unchanged.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Domain cell count", MetricKind.Count, artifact.Cells.Count, "cells"),
                        new Metric("Grid width", MetricKind.Count, artifact.GridWidth, "cells"),
                        new Metric("Grid height", MetricKind.Count, artifact.GridHeight, "cells")
                    ],
                    [
                        new Overlay(
                            $"grid-polygon-domain.{artifact.ArtifactId}",
                            OverlayKind.ColorMap,
                            $"GridPolygon domain: {artifact.Cells.Count:N0} foreground cell(s)",
                            ResultStatus.Pass,
                            source.EntityId)
                    ]),
                artifact);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            return Error(exception.Message, stopwatch);
        }
    }

    private static C3DGridPolygonDomainEvaluation Error(
        string message,
        Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new(
            new ToolResult("Grid Polygon Domain", ResultStatus.Error, message, stopwatch.Elapsed, [], []),
            null);
    }
}

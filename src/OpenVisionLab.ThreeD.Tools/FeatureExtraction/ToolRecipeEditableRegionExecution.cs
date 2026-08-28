using System.Diagnostics;
using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record C3DEditableRegionEvaluation(
    ToolResult Result,
    C3DEditableRegionArtifact? Output);

/// <summary>
/// Product adapter for E-16. It selects one validated region from the
/// upstream ConnectedRegionArtifact; detection remains owned by E-11.
/// </summary>
public static class ToolRecipeEditableRegionExecution
{
    public const string SelectedRegionIndexParameter = "SelectedRegionIndex";

    public static C3DEditableRegionEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DConnectedRegionArtifact source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
        ArgumentNullException.ThrowIfNull(source);
        var stopwatch = Stopwatch.StartNew();
        var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
        if (!validation.IsValid) return Error(string.Join(" ", validation.Errors));

        var step = document.Steps.SingleOrDefault(candidate =>
            string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase));
        if (step is null) return Error($"Recipe must contain exactly one step with ID '{stepId}'.");
        if (!string.Equals(step.ToolId, "editable-region", StringComparison.Ordinal))
        {
            return Error($"Step '{step.Id}' is not the Editable Region v1 adapter.");
        }

        try
        {
            if (step.InputEntityIds.Count != 1
                || !string.Equals(step.InputEntityIds[0], source.ArtifactId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Editable Region v1 requires the supplied ConnectedRegionArtifact to match its first input.");
            }

            var indexText = step.Parameters?.Single(parameter =>
                string.Equals(parameter.Name, SelectedRegionIndexParameter, StringComparison.Ordinal)).Value;
            if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
                || index < 0)
            {
                throw new InvalidDataException(
                    "Editable Region v1 SelectedRegionIndex must be a non-negative invariant integer.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var artifact = C3DEditableRegionArtifactFactory.Create(
                step.OutputEntityId,
                step.ToolName,
                source,
                index);
            stopwatch.Stop();
            return new(
                new ToolResult(
                    "Editable Region",
                    ResultStatus.Pass,
                    $"Selected connected region {index} as a typed editable artifact; source data remains unchanged.",
                    stopwatch.Elapsed,
                    [
                        new Metric("Selected region index", MetricKind.Count, index, "index"),
                        new Metric("Selected region cells", MetricKind.Count, artifact.Cells.Count, "cells"),
                        new Metric("Selected region width", MetricKind.Count, artifact.Bounding.Width, "cells"),
                        new Metric("Selected region height", MetricKind.Count, artifact.Bounding.Height, "cells")
                    ],
                    [
                        new Overlay(
                            $"editable-region.{artifact.ArtifactId}",
                            OverlayKind.Box,
                            $"Editable region {index}: {artifact.Cells.Count:N0} exact source-grid cell(s)",
                            ResultStatus.Pass,
                            artifact.SourceEntityId)
                    ]),
                artifact);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            stopwatch.Stop();
            return Error(exception.Message, stopwatch.Elapsed);
        }
    }

    private static C3DEditableRegionEvaluation Error(string message, TimeSpan? duration = null) => new(
        new ToolResult(
            "Editable Region",
            ResultStatus.Error,
            message,
            duration ?? TimeSpan.Zero,
            [],
            []),
        null);
}

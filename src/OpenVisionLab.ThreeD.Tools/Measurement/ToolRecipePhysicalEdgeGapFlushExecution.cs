using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipePhysicalEdgeGapFlushExecution
{
    public static C3DPhysicalEdgeGapFlushEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DLineFeature firstLine,
        C3DLineFeature secondLine,
        C3DHeightDifferenceEdgePointSet firstEdge,
        C3DHeightDifferenceEdgePointSet secondEdge,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(
                document,
                stepId,
                firstLine,
                secondLine,
                firstEdge,
                secondEdge,
                out var input,
                out var message))
        {
            return new C3DPhysicalEdgeGapFlushEvaluation(
                new ToolResult(PhysicalEdgeGapFlushRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []),
                null);
        }

        return PhysicalEdgeGapFlushRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DLineFeature firstLine,
        C3DLineFeature secondLine,
        C3DHeightDifferenceEdgePointSet firstEdge,
        C3DHeightDifferenceEdgePointSet secondEdge,
        out C3DPhysicalEdgeGapFlushInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(firstLine);
            ArgumentNullException.ThrowIfNull(secondLine);
            ArgumentNullException.ThrowIfNull(firstEdge);
            ArgumentNullException.ThrowIfNull(secondEdge);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

            var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(string.Join(" ", validation.Errors));
            }

            var matches = document.Steps
                .Where(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            }

            var step = matches[0];
            if (!string.Equals(step.ToolId, "physical-edge-gap-flush", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Physical Edge Gap / Flush v1 adapter.");
            }
            if (step.InputEntityIds.Count != 4
                || !string.Equals(step.InputEntityIds[0], firstLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(step.InputEntityIds[1], secondLine.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Physical Edge Gap / Flush v1 requires the authored FirstLine, SecondLine, FirstEdgePointSet, and SecondEdgePointSet inputs.");
            }
            if (!string.Equals(step.InputEntityIds[2], firstEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(step.InputEntityIds[3], secondEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Physical Edge Gap / Flush v1 requires each authored line to be followed by its exact EdgePointSet input.");
            }
            if (string.Equals(firstLine.OutputEntityId, secondLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(firstEdge.OutputEntityId, secondEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(step.OutputEntityId, firstLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(step.OutputEntityId, secondLine.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Physical Edge Gap / Flush input and output entity IDs must be distinct.");
            }
            if (!string.Equals(firstLine.InputEdgePointSetEntityId, firstEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(secondLine.InputEdgePointSetEntityId, secondEdge.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Physical Edge Gap / Flush requires each fitted line's exact published EdgePointSet lineage.");
            }
            if (!string.Equals(firstLine.RootSourceEntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(secondLine.RootSourceEntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(firstLine.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(secondLine.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(firstEdge.RootSourceEntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(secondEdge.RootSourceEntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(firstEdge.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(secondEdge.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(firstLine.FrameId, document.Source.FrameId, StringComparison.Ordinal)
                || !string.Equals(secondLine.FrameId, document.Source.FrameId, StringComparison.Ordinal)
                || !string.Equals(firstLine.Unit, document.Source.Unit, StringComparison.Ordinal)
                || !string.Equals(secondLine.Unit, document.Source.Unit, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Published line root source identity does not match the teaching recipe.");
            }
            if (!C3DPhysicalEdgeGapFlushProfile.TryParse(step.Parameters, out var profile, out message)
                || profile is null)
            {
                throw new InvalidDataException(message);
            }

            input = new C3DPhysicalEdgeGapFlushInput(
                step.Id,
                step.OutputEntityId,
                firstLine,
                secondLine,
                firstEdge,
                secondEdge,
                profile);
            message = "Physical Edge Gap / Flush v1 is ready from the authored published line pair.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }
}

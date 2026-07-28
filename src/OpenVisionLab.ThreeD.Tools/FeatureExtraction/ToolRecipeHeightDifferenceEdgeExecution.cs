using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeHeightDifferenceEdgeExecution
{
    private static readonly string[] ParameterNames =
    [
        "ComparisonAxis",
        "Polarity",
        "MinimumDelta",
        "CandidatePolicy",
        "PointPolicy",
        "MissingValuePolicy",
        "BoundaryPolicy"
    ];

    public static C3DHeightDifferenceEdgeEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot publishedInput,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedInput, out var input, out var message))
        {
            return Error(message);
        }

        return C3DHeightDifferenceEdgeRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot publishedInput,
        out C3DHeightDifferenceEdgeInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(publishedInput);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid)
            {
                throw new InvalidDataException(string.Join(" ", validation.Errors));
            }

            var matching = document.Steps
                .Where(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matching.Length != 1)
            {
                throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            }

            var step = matching[0];
            if (!string.Equals(step.ToolId, "height-difference-edge", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Height Difference Edge v1 adapter.");
            }
            if (step.InputEntityIds.Count != 2
                || !string.Equals(step.InputEntityIds[0], publishedInput.EntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Height Difference Edge v1 requires the current published height field first and one GridRectangle second.");
            }
            if (string.Equals(step.OutputEntityId, step.InputEntityIds[0], StringComparison.OrdinalIgnoreCase)
                || string.Equals(step.OutputEntityId, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Height Difference Edge output must differ from both inputs.");
            }
            if (!publishedInput.IsDerived)
            {
                throw new InvalidDataException("Height Difference Edge input is not a published derived height field.");
            }
            if (!string.Equals(publishedInput.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || publishedInput.Width != document.Source.GridWidth
                || publishedInput.Height != document.Source.GridHeight
                || !string.Equals(publishedInput.FrameId, document.Source.FrameId, StringComparison.Ordinal)
                || !string.Equals(publishedInput.Unit, document.Source.Unit, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Published height-field root source identity does not match the teaching recipe.");
            }

            var selection = (document.Selections ?? [])
                .SingleOrDefault(item => string.Equals(item.Id, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase));
            if (selection is null
                || !string.Equals(selection.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal)
                || selection.GridRectangle is null)
            {
                throw new InvalidDataException("Height Difference Edge second input must resolve to one recipe-owned GridRectangle.");
            }
            if (!string.Equals(selection.RootSourceId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.SourceBinding.ContentSha256, publishedInput.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || selection.SourceBinding.GridWidth != publishedInput.Width
                || selection.SourceBinding.GridHeight != publishedInput.Height
                || !string.Equals(selection.FrameId, publishedInput.FrameId, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Height Difference Edge selection source identity does not match the published height field root source.");
            }

            var (axis, polarity, minimumDelta) = ParseParameters(step);
            input = new C3DHeightDifferenceEdgeInput(
                step.Id,
                publishedInput,
                document.Source.Id,
                selection.Id,
                selection.GridRectangle,
                step.OutputEntityId,
                axis,
                polarity,
                minimumDelta);
            message = "Height Difference Edge v1 is ready.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static (C3DHeightDifferenceComparisonAxis Axis, C3DHeightDifferencePolarity Polarity, double MinimumDelta) ParseParameters(
        ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("Height Difference Edge v1 requires one value for every recognized parameter.");
        }

        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        var axis = Value("ComparisonAxis") switch
        {
            "AcrossColumns" => C3DHeightDifferenceComparisonAxis.AcrossColumns,
            "AcrossRows" => C3DHeightDifferenceComparisonAxis.AcrossRows,
            _ => throw new InvalidDataException("ComparisonAxis must be AcrossColumns or AcrossRows.")
        };
        var polarity = Value("Polarity") switch
        {
            "Rising" => C3DHeightDifferencePolarity.Rising,
            "Falling" => C3DHeightDifferencePolarity.Falling,
            "Absolute" => C3DHeightDifferencePolarity.Absolute,
            _ => throw new InvalidDataException("Polarity must be Rising, Falling, or Absolute.")
        };
        var minimumDeltaText = Value("MinimumDelta");
        if (minimumDeltaText != minimumDeltaText.Trim()
            || minimumDeltaText.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(minimumDeltaText, NumberStyles.Float, CultureInfo.InvariantCulture, out var minimumDelta)
            || !double.IsFinite(minimumDelta)
            || minimumDelta <= 0)
        {
            throw new InvalidDataException("MinimumDelta must be an explicit invariant finite number greater than zero.");
        }
        if (Value("CandidatePolicy") != "StrongestPerScanline"
            || Value("PointPolicy") != "PairMidpoint"
            || Value("MissingValuePolicy") != "SkipPair"
            || Value("BoundaryPolicy") != "WithinSelection")
        {
            throw new InvalidDataException("Height Difference Edge v1 fixed policies do not match the approved contract.");
        }

        return (axis, polarity, minimumDelta);
    }

    private static C3DHeightDifferenceEdgeEvaluation Error(string message) => new(
        new ToolResult("C3D Height Difference Edge", ResultStatus.Error, message, TimeSpan.Zero, [], []),
        null);
}

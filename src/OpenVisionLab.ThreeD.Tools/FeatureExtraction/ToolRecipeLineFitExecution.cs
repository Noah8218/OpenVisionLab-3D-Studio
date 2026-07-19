using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict typed recipe adapter. It consumes the exact already-published edge
/// snapshot supplied by its caller and never invokes Filter or Edge.
/// </summary>
public static class ToolRecipeLineFitExecution
{
    private static readonly string[] ParameterNames =
    [
        "FitMethod",
        "MaximumOrthogonalResidual",
        "MinimumInlierCount",
        "MinimumInlierRatio",
        "MinimumInlierScanlineSpan",
        "HypothesisPolicy",
        "MaximumHypotheses",
        "RefinementPolicy",
        "DirectionPolicy",
        "EndpointPolicy"
    ];

    public static C3DLineFitEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightDifferenceEdgePointSet publishedInput,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedInput, out var input, out var message))
        {
            return new C3DLineFitEvaluation(new ToolResult("3D Line Fit", ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DLineFitRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightDifferenceEdgePointSet publishedInput,
        out C3DLineFitInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(publishedInput);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var matches = document.Steps.Where(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1) throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            var step = matches[0];
            if (!string.Equals(step.ToolId, "three-d-line-fit", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the 3D Line Fit v1 adapter.");
            }
            if (step.InputEntityIds.Count != 1 || !string.Equals(step.InputEntityIds[0], publishedInput.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("3D Line Fit v1 requires exactly the current published EdgePointSet input.");
            }
            if (string.IsNullOrWhiteSpace(step.OutputEntityId) || string.Equals(step.OutputEntityId, publishedInput.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("3D Line Fit output ID must be explicit and differ from its EdgePointSet input.");
            }
            if (!string.Equals(publishedInput.RootSourceEntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(publishedInput.RootSourceSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(publishedInput.FrameId, document.Source.FrameId, StringComparison.Ordinal)
                || !string.Equals(publishedInput.Unit, document.Source.Unit, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Published EdgePointSet root source identity does not match the teaching recipe.");
            }
            var (maximumResidual, minimumCount, minimumRatio, minimumSpan) = ParseParameters(step);
            input = new C3DLineFitInput(step.Id, publishedInput, step.OutputEntityId, maximumResidual, minimumCount, minimumRatio, minimumSpan);
            message = "3D Line Fit v1 is ready from the current Published EdgePointSet.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static (double MaximumResidual, int MinimumCount, double MinimumRatio, int MinimumSpan) ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length || ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("3D Line Fit v1 requires exactly one value for every recognized parameter and no unknown parameters.");
        }
        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (Value("FitMethod") != "DeterministicConsensusOrthogonalTls"
            || Value("HypothesisPolicy") != "Sha256PairSchedule"
            || Value("MaximumHypotheses") != "256"
            || Value("RefinementPolicy") != "OrthogonalTlsUntilStable10"
            || Value("DirectionPolicy") != "PositiveScanlineAxis"
            || Value("EndpointPolicy") != "InlierProjectionExtents")
        {
            throw new InvalidDataException("3D Line Fit v1 fixed policies do not match the approved contract.");
        }

        var maximumResidual = ParseFinitePositive(Value("MaximumOrthogonalResidual"), "MaximumOrthogonalResidual");
        var minimumRatio = ParseFinitePositive(Value("MinimumInlierRatio"), "MinimumInlierRatio");
        if (minimumRatio > 1) throw new InvalidDataException("MinimumInlierRatio must be no greater than one.");
        var minimumCount = ParseInteger(Value("MinimumInlierCount"), "MinimumInlierCount");
        var minimumSpan = ParseInteger(Value("MinimumInlierScanlineSpan"), "MinimumInlierScanlineSpan");
        return (maximumResidual, minimumCount, minimumRatio, minimumSpan);
    }

    private static double ParseFinitePositive(string value, string name)
    {
        if (value != value.Trim() || value.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed) || parsed <= 0)
        {
            throw new InvalidDataException($"{name} must be an explicit invariant finite number greater than zero.");
        }
        return parsed;
    }

    private static int ParseInteger(string value, string name)
    {
        if (value != value.Trim() || value.Contains(',', StringComparison.Ordinal)
            || !int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidDataException($"{name} must be an explicit invariant integer.");
        }
        return parsed;
    }
}

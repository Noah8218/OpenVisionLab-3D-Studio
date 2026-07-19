using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict typed adapter. It consumes the exact two published lines supplied by
/// the caller and never invokes Filter, Edge, or Line Fit.
/// </summary>
public static class ToolRecipeLineIntersectionExecution
{
    private static readonly string[] ParameterNames =
    [
        "MaximumClosestApproachDistance", "MinimumAcuteAngleDegrees", "MaximumSupportExtension",
        "OutputRole", "ClosestApproachPolicy", "ParallelPolicy", "SupportPolicy"
    ];

    public static C3DLineIntersectionEvaluation Execute(
        ToolRecipeDocument document, string stepId, C3DLineFeature firstPublishedLine,
        C3DLineFeature secondPublishedLine, CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, firstPublishedLine, secondPublishedLine, out var input, out var message))
        {
            return new C3DLineIntersectionEvaluation(new ToolResult("Line Intersection", ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DLineIntersectionRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document, string stepId, C3DLineFeature firstPublishedLine,
        C3DLineFeature secondPublishedLine, out C3DLineIntersectionInput? input, out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(firstPublishedLine);
            ArgumentNullException.ThrowIfNull(secondPublishedLine);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var step = document.Steps.SingleOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "line-intersection", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Line Intersection v1 adapter.");
            }
            if (step.InputEntityIds.Count != 2
                || !string.Equals(step.InputEntityIds[0], firstPublishedLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(step.InputEntityIds[1], secondPublishedLine.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Line Intersection v1 requires exactly the authored first and second published LineFeature inputs.");
            }
            if (string.IsNullOrWhiteSpace(step.OutputEntityId)
                || string.Equals(step.OutputEntityId, firstPublishedLine.OutputEntityId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(step.OutputEntityId, secondPublishedLine.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Line Intersection output ID must be explicit and differ from both LineFeature inputs.");
            }
            ValidateRecipeSource(document.Source, firstPublishedLine, secondPublishedLine);
            var values = ParseParameters(step);
            var duplicateRole = document.Steps
                .Where(candidate => string.Equals(candidate.ToolId, "line-intersection", StringComparison.Ordinal) && !string.Equals(candidate.Id, step.Id, StringComparison.OrdinalIgnoreCase))
                .Select(candidate => candidate.Parameters.SingleOrDefault(parameter => parameter.Name == "OutputRole")?.Value)
                .Any(role => string.Equals(role, values.OutputRole, StringComparison.OrdinalIgnoreCase));
            if (duplicateRole) throw new InvalidDataException($"Line Intersection OutputRole '{values.OutputRole}' must be unique within the recipe.");
            input = new C3DLineIntersectionInput(step.Id, firstPublishedLine, secondPublishedLine, step.OutputEntityId,
                values.MaximumGap, values.MinimumAngle, values.MaximumExtension, values.OutputRole);
            message = "Line Intersection v1 is ready from the two current Published LineFeature inputs.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static void ValidateRecipeSource(ToolRecipeSource source, C3DLineFeature first, C3DLineFeature second)
    {
        foreach (var line in new[] { first, second })
        {
            if (!string.Equals(line.RootSourceEntityId, source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(line.RootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(line.FrameId, source.FrameId, StringComparison.Ordinal)
                || !string.Equals(line.Unit, source.Unit, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Published LineFeature root source identity does not match the teaching recipe.");
            }
        }
    }

    private static (double MaximumGap, double MinimumAngle, double MaximumExtension, string OutputRole) ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length || ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("Line Intersection v1 requires exactly one value for every recognized parameter and no unknown parameters.");
        }
        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (Value("ClosestApproachPolicy") != "MidpointOfClosestPoints"
            || Value("ParallelPolicy") != "RejectBelowMinimumAcuteAngle"
            || Value("SupportPolicy") != "WithinInlierProjectionExtentsWithMaximumExtension")
        {
            throw new InvalidDataException("Line Intersection v1 fixed policies do not match the approved contract.");
        }
        var maximumGap = ParseFinite(Value("MaximumClosestApproachDistance"), "MaximumClosestApproachDistance", strictlyPositive: true);
        var minimumAngle = ParseFinite(Value("MinimumAcuteAngleDegrees"), "MinimumAcuteAngleDegrees", strictlyPositive: true);
        if (minimumAngle > 90) throw new InvalidDataException("MinimumAcuteAngleDegrees must be no greater than 90.");
        var maximumExtension = ParseFinite(Value("MaximumSupportExtension"), "MaximumSupportExtension", strictlyPositive: false);
        var outputRole = Value("OutputRole");
        if (string.IsNullOrWhiteSpace(outputRole) || outputRole != outputRole.Trim()) throw new InvalidDataException("OutputRole must be an explicit non-empty identifier without surrounding whitespace.");
        return (maximumGap, minimumAngle, maximumExtension, outputRole);
    }

    private static double ParseFinite(string value, string name, bool strictlyPositive)
    {
        if (value != value.Trim() || value.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed) || (strictlyPositive ? parsed <= 0 : parsed < 0))
        {
            throw new InvalidDataException($"{name} must be an explicit invariant finite number {(strictlyPositive ? "greater than zero" : "no less than zero")}.");
        }
        return parsed;
    }
}

using System.Globalization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Recipe adapter for the A1 solve boundary. The caller supplies the current
/// Published correspondence artifact; this adapter never rebuilds upstream
/// anchors and never applies the solved matrix.
/// </summary>
public static class ToolRecipeXYZAffineSolveExecution
{
    private static readonly string[] ParameterNames =
    [
        "SolvePolicy", "MaximumConditionEstimate", "ArithmeticResidualWarning"
    ];

    public static C3DAffineSolveEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DLandmarkCorrespondenceSet publishedCorrespondence,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedCorrespondence, out var input, out var message))
        {
            return new C3DAffineSolveEvaluation(
                new ToolResult("XYZ Affine Solve", ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DAffineSolveRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DLandmarkCorrespondenceSet publishedCorrespondence,
        out C3DAffineSolveInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            ArgumentNullException.ThrowIfNull(publishedCorrespondence);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            if (!string.Equals(document.SchemaVersion, ToolRecipeDocument.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"XYZ Affine Solve v1 requires teaching recipe schema {ToolRecipeDocument.CurrentSchemaVersion}.");
            }
            var step = document.Steps.SingleOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "xyz-affine-solve", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the XYZ Affine Solve v1 adapter.");
            }
            if (step.InputEntityIds.Count != 1
                || !string.Equals(step.InputEntityIds[0], publishedCorrespondence.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("XYZ Affine Solve v1 requires one current Published CorrespondenceSet routed as its exact input.");
            }
            if (string.IsNullOrWhiteSpace(step.OutputEntityId)
                || string.Equals(step.OutputEntityId, publishedCorrespondence.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("XYZ Affine Solve output ID must be explicit and differ from its CorrespondenceSet input.");
            }
            ValidateSource(document.Source, publishedCorrespondence);
            var parameters = ReadParameters(step);
            input = new C3DAffineSolveInput(
                step.Id,
                step.OutputEntityId,
                publishedCorrespondence,
                parameters.MaximumConditionEstimate,
                parameters.ArithmeticResidualWarning);
            message = "XYZ Affine Solve v1 is ready from the current Published CorrespondenceSet.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException or FormatException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static (double MaximumConditionEstimate, double ArithmeticResidualWarning) ReadParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length || ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("XYZ Affine Solve v1 requires exactly one value for every recognized parameter and no unknown parameters.");
        }
        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (!string.Equals(Value("SolvePolicy"), "ExactFourPartialPivot", StringComparison.Ordinal))
        {
            throw new InvalidDataException("XYZ Affine Solve v1 requires SolvePolicy ExactFourPartialPivot.");
        }
        if (!double.TryParse(Value("MaximumConditionEstimate"), NumberStyles.Float, CultureInfo.InvariantCulture, out var maximum)
            || !double.IsFinite(maximum) || maximum <= 0d)
        {
            throw new InvalidDataException("XYZ Affine Solve MaximumConditionEstimate must be a finite positive invariant number.");
        }
        if (!double.TryParse(Value("ArithmeticResidualWarning"), NumberStyles.Float, CultureInfo.InvariantCulture, out var warning)
            || !double.IsFinite(warning) || warning < 0d)
        {
            throw new InvalidDataException("XYZ Affine Solve ArithmeticResidualWarning must be a finite non-negative invariant number.");
        }
        return (maximum, warning);
    }

    private static void ValidateSource(ToolRecipeSource source, C3DLandmarkCorrespondenceSet correspondence)
    {
        if (!string.Equals(source.Id, correspondence.RootSourceEntityId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.ContentSha256, correspondence.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(source.FrameId, correspondence.SourceFrameId, StringComparison.Ordinal)
            || !string.Equals(source.Unit, correspondence.SourceUnit, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Published CorrespondenceSet root source identity does not match the teaching recipe.");
        }
    }
}

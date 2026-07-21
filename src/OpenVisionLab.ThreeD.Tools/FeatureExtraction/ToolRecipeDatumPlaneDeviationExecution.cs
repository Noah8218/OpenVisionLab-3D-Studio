using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict typed adapter for one raw C3D source, one already published manual
/// PlaneFeature, and one recipe-owned GridRectangle measurement selection.
/// It cannot refit a plane or recover stale/preview inputs.
/// </summary>
public static class ToolRecipeDatumPlaneDeviationExecution
{
    private static readonly string[] ParameterNames =
    [
        "MaximumPeakToValleyRawHeight", "OutputRole", "ResidualPolicy",
        "MinimumValidSampleCount", "MinimumAbsoluteNormalY"
    ];

    public static C3DDatumPlaneDeviationEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DThreePointPlaneFeature publishedPlane,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedPlane, recipeDirectory, out var input, out var message))
        {
            return new C3DDatumPlaneDeviationEvaluation(new ToolResult(C3DDatumPlaneDeviationRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DDatumPlaneDeviationRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DThreePointPlaneFeature publishedPlane,
        string? recipeDirectory,
        out C3DDatumPlaneDeviationInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(publishedPlane);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var step = document.Steps.SingleOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "datum-plane-raw-height-deviation", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Datum Plane Raw-Height Deviation v1 adapter.");
            }
            if (step.InputEntityIds.Count != 3
                || !string.Equals(step.InputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(step.InputEntityIds[1], publishedPlane.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Datum Plane Raw-Height Deviation v1 requires raw C3D, then the exact Published PlaneFeature, then one GridRectangle.");
            }
            var selection = (document.Selections ?? []).SingleOrDefault(item => string.Equals(item.Id, step.InputEntityIds[2], StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Datum Plane Raw-Height Deviation v1 requires one recipe-owned GridRectangle third input.");
            if (selection.Kind != ToolRecipeSelectionKinds.GridRectangle || selection.GridRectangle is null)
            {
                throw new InvalidDataException("Datum Plane Raw-Height Deviation v1 third input must be a GridRectangle.");
            }
            if (string.IsNullOrWhiteSpace(step.OutputEntityId)
                || step.InputEntityIds.Any(inputId => string.Equals(step.OutputEntityId, inputId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Datum Plane Raw-Height Deviation output ID must be explicit and differ from every input.");
            }
            var values = ParseParameters(step);
            var source = document.Source;
            if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || source.ByteLength is null || string.IsNullOrWhiteSpace(source.ContentSha256)
                || source.GridWidth is null || source.GridHeight is null)
            {
                throw new InvalidDataException("Datum Plane Raw-Height Deviation v1 requires a complete recipe-bound C3D source identity.");
            }
            var snapshot = C3DHeightFieldSnapshot.LoadVerified(
                ResolveSourcePath(source.Path, recipeDirectory), source.Id, source.Unit, source.FrameId,
                source.ByteLength.Value, source.ContentSha256, source.GridWidth.Value, source.GridHeight.Value);
            input = new C3DDatumPlaneDeviationInput(
                step.Id, snapshot, publishedPlane, selection, step.OutputEntityId,
                values.MaximumPeakToValleyRawHeight, values.MinimumValidSampleCount,
                values.MinimumAbsoluteNormalY, values.OutputRole);
            C3DDatumPlaneDeviationRule.ValidateInput(input);
            message = "Datum Plane Raw-Height Deviation v1 is ready from the current raw C3D, Published PlaneFeature, and GridRectangle.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static (double MaximumPeakToValleyRawHeight, int MinimumValidSampleCount, double MinimumAbsoluteNormalY, string OutputRole) ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length || ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("Datum Plane Raw-Height Deviation v1 requires exactly one value for every recognized parameter and no unknown parameters.");
        }
        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (!string.Equals(Value("ResidualPolicy"), C3DDatumPlaneDeviationFeature.ResidualPolicyName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("ResidualPolicy must be RawHeightMinusDatumPlanePredictedRawHeight.");
        }
        var maximum = ParseFinite(Value("MaximumPeakToValleyRawHeight"), "MaximumPeakToValleyRawHeight", strictlyPositive: true);
        if (!int.TryParse(Value("MinimumValidSampleCount"), NumberStyles.None, CultureInfo.InvariantCulture, out var minimum) || minimum < 3)
        {
            throw new InvalidDataException("MinimumValidSampleCount must be an invariant integer no less than three.");
        }
        var minimumNormalY = ParseFinite(Value("MinimumAbsoluteNormalY"), "MinimumAbsoluteNormalY", strictlyPositive: true);
        if (minimumNormalY > 1d) throw new InvalidDataException("MinimumAbsoluteNormalY must be no greater than one.");
        var outputRole = Value("OutputRole");
        if (string.IsNullOrWhiteSpace(outputRole) || outputRole != outputRole.Trim())
        {
            throw new InvalidDataException("OutputRole must be an explicit non-empty identifier without surrounding whitespace.");
        }
        return (maximum, minimum, minimumNormalY, outputRole);
    }

    private static double ParseFinite(string value, string name, bool strictlyPositive)
    {
        if (value != value.Trim() || value.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed) || (strictlyPositive ? parsed <= 0d : parsed < 0d))
        {
            throw new InvalidDataException($"{name} must be an explicit invariant finite number {(strictlyPositive ? "greater than zero" : "no less than zero")}.");
        }
        return parsed;
    }

    private static string ResolveSourcePath(string path, string? recipeDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, path));
}

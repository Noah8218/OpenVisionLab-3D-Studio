using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeLevelSurfaceExecution
{
    private static readonly string[] ParameterNames =
    [
        "ReferenceFitPolicy",
        "LevelingPolicy",
        "MissingValuePolicy",
        "GridPolicy",
        "MinimumValidSampleCount",
        "MaximumReferenceRmsResidual"
    ];

    public static C3DLevelSurfaceEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, recipeDirectory, out var input, out var message))
        {
            return new C3DLevelSurfaceEvaluation(
                new ToolResult(C3DLevelSurfaceRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []),
                null,
                null,
                double.NaN,
                double.NaN);
        }
        return C3DLevelSurfaceRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory,
        out C3DLevelSurfaceInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var step = document.Steps.SingleOrDefault(candidate =>
                string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "level-surface", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Level Surface v1 adapter.");
            }
            if (step.InputEntityIds.Count < 2
                || !string.Equals(step.InputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || step.InputEntityIds.Skip(1).Distinct(StringComparer.OrdinalIgnoreCase).Count() != step.InputEntityIds.Count - 1)
            {
                throw new InvalidDataException("Level Surface requires the raw C3D source followed by one or more unique GridRectangle inputs.");
            }
            if (string.IsNullOrWhiteSpace(step.OutputEntityId)
                || step.InputEntityIds.Any(id => string.Equals(id, step.OutputEntityId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Level Surface output ID must be explicit and differ from every input.");
            }

            var selections = step.InputEntityIds.Skip(1).Select(inputId =>
                (document.Selections ?? []).SingleOrDefault(selection =>
                    string.Equals(selection.Id, inputId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Level Surface reference selection '{inputId}' is missing."))
                .ToArray();
            var parameters = ParseParameters(step);
            var source = document.Source;
            if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || source.ByteLength is null
                || string.IsNullOrWhiteSpace(source.ContentSha256)
                || source.GridWidth is null
                || source.GridHeight is null)
            {
                throw new InvalidDataException("Level Surface requires a complete recipe-bound C3D source identity.");
            }
            var snapshot = C3DHeightFieldSnapshot.LoadVerified(
                ResolveSourcePath(source.Path, recipeDirectory),
                source.Id,
                source.Unit,
                source.FrameId,
                source.ByteLength.Value,
                source.ContentSha256,
                source.GridWidth.Value,
                source.GridHeight.Value);
            input = new C3DLevelSurfaceInput(
                step.Id,
                snapshot,
                selections,
                step.OutputEntityId,
                parameters.MinimumValidSampleCount,
                parameters.MaximumReferenceRmsResidual);
            C3DLevelSurfaceRule.ValidateInput(input);
            message = $"Level Surface v1 is ready from {selections.Length} explicit reference region(s).";
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidDataException
            or InvalidOperationException
            or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    public static bool CanRunWholeRecipe(
        ToolRecipeDocument document,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Steps.Count != 1
            || !string.Equals(
                document.Steps[0].ToolId,
                "level-surface",
                StringComparison.Ordinal))
        {
            message =
                "Run is blocked: Level Surface v1 currently executes as one explicit preparation step.";
            return false;
        }
        var validation = ToolRecipeValidator.Validate(document);
        message = validation.IsValid
            ? "Level Surface v1 recipe is executable."
            : string.Join(" ", validation.Errors);
        return validation.IsValid;
    }

    private static (int MinimumValidSampleCount, double MaximumReferenceRmsResidual)
        ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length
            || ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("Level Surface v1 requires exactly its six typed parameters and no unknown parameters.");
        }
        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (Value("ReferenceFitPolicy") != C3DLevelingTransform.ReferenceFitPolicy
            || Value("LevelingPolicy") != C3DLevelingTransform.LevelingPolicy
            || Value("MissingValuePolicy") != C3DLevelingTransform.MissingValuePolicy
            || Value("GridPolicy") != C3DLevelingTransform.GridPolicy)
        {
            throw new InvalidDataException("Level Surface fixed fit, leveling, missing-value, or grid policy is invalid.");
        }
        if (!int.TryParse(Value("MinimumValidSampleCount"), NumberStyles.None, CultureInfo.InvariantCulture, out var minimum)
            || minimum < 3)
        {
            throw new InvalidDataException("MinimumValidSampleCount must be an invariant integer no less than three.");
        }
        var maximum = Value("MaximumReferenceRmsResidual");
        if (maximum != maximum.Trim()
            || maximum.Contains(',', StringComparison.Ordinal)
            || !double.TryParse(maximum, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            || !double.IsFinite(parsed)
            || parsed <= 0)
        {
            throw new InvalidDataException("MaximumReferenceRmsResidual must be an invariant finite number greater than zero.");
        }
        return (minimum, parsed);
    }

    private static string ResolveSourcePath(string path, string? recipeDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, path));
}

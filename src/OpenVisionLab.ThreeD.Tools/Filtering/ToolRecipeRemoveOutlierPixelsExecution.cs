using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeRemoveOutlierPixelsExecution
{
    private static readonly string[] ParameterNames =
    [
        "Rule",
        "WindowSize",
        "MaximumAbsoluteDeviation",
        "MinimumValidNeighbors",
        "MissingValuePolicy",
        "BoundaryPolicy",
        "OutlierPolicy"
    ];

    public static C3DRemoveOutlierPixelsEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        var validation = ToolRecipeValidator.Validate(document);
        if (!validation.IsValid)
        {
            return Error(string.Join(" ", validation.Errors));
        }

        var matching = document.Steps
            .Where(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matching.Length != 1)
        {
            return Error($"Recipe must contain exactly one step with ID '{stepId}'.");
        }

        var step = matching[0];
        if (!string.Equals(step.ToolId, "remove-outlier-pixels", StringComparison.Ordinal))
        {
            return Error($"Step '{step.Id}' is not the Remove Outlier Pixels v1 adapter.");
        }

        try
        {
            var (windowSize, maximumDeviation, minimumNeighbors) = ParseParameters(step);
            var sourcePath = ResolveSourcePath(document.Source.Path, recipeDirectory);
            var source = C3DHeightFieldSnapshot.LoadVerified(
                sourcePath,
                document.Source.Id,
                document.Source.Unit,
                document.Source.FrameId,
                document.Source.ByteLength!.Value,
                document.Source.ContentSha256!,
                document.Source.GridWidth!.Value,
                document.Source.GridHeight!.Value);
            return C3DRemoveOutlierPixelsRule.Evaluate(
                new C3DRemoveOutlierPixelsInput(
                    step.Id,
                    source,
                    step.OutputEntityId,
                    windowSize,
                    maximumDeviation,
                    minimumNeighbors),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or OverflowException)
        {
            return Error(exception.Message);
        }
    }

    public static bool CanRunWholeRecipe(ToolRecipeDocument document, out string message)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Steps.Count != 1
            || !string.Equals(
                document.Steps[0].ToolId,
                "remove-outlier-pixels",
                StringComparison.Ordinal))
        {
            message =
                "Run is blocked: Remove Outlier Pixels v1 currently executes as one explicit preparation step.";
            return false;
        }

        var validation = ToolRecipeValidator.Validate(document);
        message = validation.IsValid
            ? "Remove Outlier Pixels v1 recipe is executable."
            : string.Join(" ", validation.Errors);
        return validation.IsValid;
    }

    internal static (int WindowSize, double MaximumDeviation, int MinimumNeighbors)
        ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (ParameterNames.Any(
                name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException(
                "Remove Outlier Pixels v1 requires one value for every recognized parameter.");
        }

        string Value(string name) =>
            parameters.Single(parameter => parameter.Name == name).Value;

        if (Value("Rule") != C3DRemoveOutlierPixelsRule.Rule
            || Value("MissingValuePolicy") != C3DRemoveOutlierPixelsRule.MissingValuePolicy
            || Value("BoundaryPolicy") != C3DRemoveOutlierPixelsRule.BoundaryPolicy
            || Value("OutlierPolicy") != C3DRemoveOutlierPixelsRule.OutlierPolicy
            || !int.TryParse(
                Value("WindowSize"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var windowSize)
            || windowSize is not (3 or 5 or 7)
            || !double.TryParse(
                Value("MaximumAbsoluteDeviation"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var maximumDeviation)
            || !int.TryParse(
                Value("MinimumValidNeighbors"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var minimumNeighbors))
        {
            throw new InvalidDataException(
                "Remove Outlier Pixels parameters do not match the approved v1 contract.");
        }

        return (windowSize, maximumDeviation, minimumNeighbors);
    }

    private static string ResolveSourcePath(string path, string? recipeDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(
                Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, path));

    private static C3DRemoveOutlierPixelsEvaluation Error(string message) => new(
        new ToolResult(
            "Remove Outlier Pixels",
            ResultStatus.Error,
            message,
            TimeSpan.Zero,
            [],
            []),
        null,
        null);
}

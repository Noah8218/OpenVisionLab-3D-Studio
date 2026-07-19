using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

public static class ToolRecipeFilterExecution
{
    private static readonly string[] ParameterNames =
        ["Method", "KernelSize", "MissingValuePolicy", "BoundaryPolicy"];

    public static C3DMedianFilterEvaluation Execute(
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

        var matching = document.Steps.Where(step => string.Equals(step.Id, stepId, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (matching.Length != 1)
        {
            return Error($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
        }

        var step = matching[0];
        if (!string.Equals(step.ToolId, "filter", StringComparison.Ordinal))
        {
            return Error($"Step '{step.Id}' is not the Filter v1 adapter.");
        }

        try
        {
            var kernelSize = ParseParameters(step);
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
            return C3DMedianFilterRule.Evaluate(
                new C3DMedianFilterInput(step.Id, source, step.OutputEntityId, kernelSize),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or OverflowException)
        {
            return Error(exception.Message);
        }
    }

    public static bool CanRunWholeRecipe(ToolRecipeDocument document, out string message)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.Steps.Count != 1 || !string.Equals(document.Steps[0].ToolId, "filter", StringComparison.Ordinal))
        {
            message = "Run is blocked: Filter v1 can execute only a one-step Filter recipe until downstream typed adapters exist.";
            return false;
        }

        var validation = ToolRecipeValidator.Validate(document);
        message = validation.IsValid ? "Filter v1 recipe is executable." : string.Join(" ", validation.Errors);
        return validation.IsValid;
    }

    private static int ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("Filter v1 requires one value for every recognized parameter.");
        }

        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (Value("Method") != "Median"
            || Value("MissingValuePolicy") != "PreserveMask"
            || Value("BoundaryPolicy") != "AvailableNeighbors"
            || !int.TryParse(Value("KernelSize"), out var kernelSize)
            || kernelSize is not (3 or 5 or 7))
        {
            throw new InvalidDataException("Filter v1 parameters do not match the approved Median contract.");
        }

        return (kernelSize);
    }

    private static string ResolveSourcePath(string path, string? recipeDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, path));

    private static C3DMedianFilterEvaluation Error(string message) => new(
        new ToolResult("C3D Median Filter", ResultStatus.Error, message, TimeSpan.Zero, [], []),
        null);
}

using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Recipe adapter for the existing SDK-backed raw-height threshold rule.
/// Recipe persistence owns the authored policy; calculation remains in
/// <see cref="C3DHeightThresholdBackgroundRemovalRule"/>.
/// </summary>
public static class ToolRecipeHeightThresholdBackgroundRemovalExecution
{
    private static readonly string[] ParameterNames =
    [
        "Threshold",
        "Mode",
        "ComparisonPolicy",
        "MissingValuePolicy",
        "BackgroundPolicy"
    ];

    public static C3DHeightThresholdBackgroundRemovalEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(stepId);

        var validation = ToolRecipeValidator.ValidateForStepExecution(document, stepId);
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
        if (!string.Equals(step.ToolId, "height-threshold-background-removal", StringComparison.Ordinal))
        {
            return Error($"Step '{step.Id}' is not the Height Threshold Background Removal v1 adapter.");
        }

        try
        {
            var (threshold, mode) = ParseParameters(step);
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
            return C3DHeightThresholdBackgroundRemovalRule.Evaluate(
                new C3DHeightThresholdBackgroundRemovalInput(
                    step.Id,
                    source,
                    step.OutputEntityId,
                    threshold,
                    mode),
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
                "height-threshold-background-removal",
                StringComparison.Ordinal))
        {
            message =
                "Run is blocked: Height Threshold Background Removal v1 currently executes as one explicit preparation step.";
            return false;
        }

        var validation = ToolRecipeValidator.Validate(document);
        message = validation.IsValid
            ? "Height Threshold Background Removal v1 recipe is executable."
            : string.Join(" ", validation.Errors);
        return validation.IsValid;
    }

    internal static (double Threshold, C3DHeightThresholdBackgroundRemovalMode Mode)
        ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (ParameterNames.Any(
                name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException(
                "Height Threshold Background Removal v1 requires one value for every recognized parameter.");
        }

        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (!double.TryParse(
                Value("Threshold"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var threshold)
            || !double.IsFinite(threshold)
            || !Enum.TryParse<C3DHeightThresholdBackgroundRemovalMode>(
                Value("Mode"),
                ignoreCase: false,
                out var mode)
            || !Enum.IsDefined(mode)
            || Value("ComparisonPolicy") != C3DHeightThresholdBackgroundRemovalEvidence.ComparisonPolicyName
            || Value("MissingValuePolicy") != C3DHeightThresholdBackgroundRemovalEvidence.MissingValuePolicyName
            || Value("BackgroundPolicy") != C3DHeightThresholdBackgroundRemovalEvidence.BackgroundPolicyName)
        {
            throw new InvalidDataException(
                "Height Threshold Background Removal parameters do not match the approved v1 contract.");
        }

        return (threshold, mode);
    }

    private static string ResolveSourcePath(string path, string? recipeDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, path));

    private static C3DHeightThresholdBackgroundRemovalEvaluation Error(string message) => new(
        new ToolResult(
            C3DHeightThresholdBackgroundRemovalRule.ToolName,
            ResultStatus.Error,
            message,
            TimeSpan.Zero,
            [],
            []),
        null,
        null);
}

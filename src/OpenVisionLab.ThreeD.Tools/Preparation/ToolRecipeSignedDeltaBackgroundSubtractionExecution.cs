using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Recipe adapter for the missing-safe signed current-minus-background route.
/// The current source is the canonical recipe source; the saved background is
/// an explicitly identity-bound auxiliary reference in the step parameters.
/// </summary>
public static class ToolRecipeSignedDeltaBackgroundSubtractionExecution
{
    public static C3DSignedHeightDeltaEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DHeightFieldSnapshot? current = null,
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
        if (!string.Equals(step.ToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.Ordinal))
        {
            return Error($"Step '{step.Id}' is not the Signed Delta Background Subtraction adapter.");
        }

        try
        {
            if (!C3DSignedHeightDeltaRecipePolicy.TryRead(step.Parameters, out var reference, out var policyMessage))
            {
                throw new InvalidDataException(policyMessage);
            }

            var source = current ?? LoadSource(document, recipeDirectory);
            if (!string.Equals(source.EntityId, document.Source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(source.Unit, document.Source.Unit, StringComparison.Ordinal)
                || !string.Equals(source.FrameId, document.Source.FrameId, StringComparison.Ordinal)
                || (document.Source.ContentSha256 is not null
                    && !string.Equals(source.ContentSha256, document.Source.ContentSha256, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidDataException("Signed delta current source does not match the recipe source identity.");
            }

            var backgroundPath = ResolvePath(reference.Path, recipeDirectory ?? Path.GetDirectoryName(document.Source.Path));
            var background = C3DHeightFieldSnapshot.LoadVerified(
                backgroundPath,
                reference.EntityId,
                reference.Unit,
                reference.FrameId,
                reference.ByteLength,
                reference.ContentSha256,
                reference.Width,
                reference.Height,
                cancellationToken);
            if (!string.Equals(background.Unit, source.Unit, StringComparison.Ordinal)
                || !string.Equals(background.FrameId, source.FrameId, StringComparison.Ordinal)
                || background.Width != source.Width
                || background.Height != source.Height
                || background.GridOriginColumn != source.GridOriginColumn
                || background.GridOriginRow != source.GridOriginRow)
            {
                throw new InvalidDataException(
                    "Signed delta current and saved-background grids must match dimensions, origin, unit, and frame exactly.");
            }

            return C3DHeightBackgroundSubtractionRule.EvaluateSignedDelta(
                new C3DHeightBackgroundSubtractionInput(
                    step.Id,
                    source,
                    background,
                    step.OutputEntityId),
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
            || !string.Equals(document.Steps[0].ToolId, C3DSignedHeightDeltaRecipePolicy.ToolId, StringComparison.Ordinal))
        {
            message = "Run is blocked: Signed Delta Background Subtraction currently executes as one explicit preparation step.";
            return false;
        }

        var validation = ToolRecipeValidator.Validate(document);
        message = validation.IsValid
            ? "Signed Delta Background Subtraction recipe is executable."
            : string.Join(" ", validation.Errors);
        return validation.IsValid;
    }

    private static C3DHeightFieldSnapshot LoadSource(ToolRecipeDocument document, string? recipeDirectory)
    {
        if (document.Source.ByteLength is not { } byteLength
            || document.Source.ContentSha256 is not { } contentSha256
            || document.Source.GridWidth is not { } width
            || document.Source.GridHeight is not { } height)
        {
            throw new InvalidDataException("Signed delta recipe source identity is incomplete.");
        }

        return C3DHeightFieldSnapshot.LoadVerified(
            ResolvePath(document.Source.Path, recipeDirectory ?? Path.GetDirectoryName(document.Source.Path)),
            document.Source.Id,
            document.Source.Unit,
            document.Source.FrameId,
            byteLength,
            contentSha256,
            width,
            height);
    }

    private static string ResolvePath(string path, string? baseDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(baseDirectory ?? Environment.CurrentDirectory, path));

    private static C3DSignedHeightDeltaEvaluation Error(string message) => new(
        new ToolResult(C3DHeightBackgroundSubtractionRule.SignedDeltaToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []),
        null);
}

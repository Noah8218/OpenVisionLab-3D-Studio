using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict A2 recipe adapter. The raw C3D is an explicit first route and the
/// caller supplies the current Published A1 output; neither is rebuilt here.
/// </summary>
public static class ToolRecipeXYZAffineApplyExecution
{
    public static C3DAffineApplyEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DAffineTransform3D publishedAffineTransform,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedAffineTransform, recipeDirectory, out var input, out var message))
        {
            return new C3DAffineApplyEvaluation(new ToolResult(C3DAffineApplyRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DAffineApplyRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DAffineTransform3D publishedAffineTransform,
        string? recipeDirectory,
        out C3DAffineApplyInput? input,
        out string message)
    {
        input = null;
        try
        {
            if (!TryValidateRoute(document, stepId, publishedAffineTransform, out var step, out message)) return false;
            var source = document.Source;
            var byteLength = source.ByteLength ?? throw new InvalidDataException("Apply XYZ Affine v1 source byte length is missing.");
            var contentSha256 = source.ContentSha256 ?? throw new InvalidDataException("Apply XYZ Affine v1 source SHA-256 is missing.");
            var gridWidth = source.GridWidth ?? throw new InvalidDataException("Apply XYZ Affine v1 source width is missing.");
            var gridHeight = source.GridHeight ?? throw new InvalidDataException("Apply XYZ Affine v1 source height is missing.");
            var snapshot = C3DHeightFieldSnapshot.LoadVerified(
                ResolveSourcePath(source.Path, recipeDirectory),
                source.Id,
                source.Unit,
                source.FrameId,
                byteLength,
                contentSha256,
                gridWidth,
                gridHeight);
            input = new C3DAffineApplyInput(step!.Id, snapshot, publishedAffineTransform, step.OutputEntityId);
            message = "Apply XYZ Affine v1 is ready from the verified raw C3D source and the current Published AffineTransform3D.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    /// <summary>
    /// Checks recipe routing and immutable A1 provenance without parsing the
    /// potentially large C3D. This is safe to call from command enablement.
    /// </summary>
    public static bool TryValidateRoute(
        ToolRecipeDocument document,
        string stepId,
        C3DAffineTransform3D publishedAffineTransform,
        out ToolRecipeStep? step,
        out string message)
    {
        step = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            ArgumentNullException.ThrowIfNull(publishedAffineTransform);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            step = document.Steps.SingleOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Apply XYZ Affine v1 adapter.");
            }
            var source = document.Source;
            if (step.InputEntityIds.Count != 2
                || !string.Equals(step.InputEntityIds[0], source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(step.InputEntityIds[1], publishedAffineTransform.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Apply XYZ Affine v1 requires the verified raw C3D source first and its current Published AffineTransform3D second.");
            }
            if (step.Parameters.Count != 0)
            {
                throw new InvalidDataException("Apply XYZ Affine v1 has no authored parameters.");
            }
            if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(source.Unit, "raw-height", StringComparison.Ordinal)
                || source.ByteLength is null || string.IsNullOrWhiteSpace(source.ContentSha256)
                || source.GridWidth is null || source.GridHeight is null)
            {
                throw new InvalidDataException("Apply XYZ Affine v1 requires a complete verified raw-height C3D source identity.");
            }
            message = "Apply XYZ Affine v1 route is valid; Preview will verify and parse the C3D once.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            step = null;
            message = exception.Message;
            return false;
        }
    }

    private static string ResolveSourcePath(string path, string? recipeDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, path));
}

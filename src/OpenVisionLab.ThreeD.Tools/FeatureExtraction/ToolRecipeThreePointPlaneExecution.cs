using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict recipe adapter for one raw C3D source plus one source-bound
/// PointSet(3). Source bytes are verified before current height values are
/// resolved; captured historical heights are never substituted.
/// </summary>
public static class ToolRecipeThreePointPlaneExecution
{
    private static readonly string[] ParameterNames = ["OutputRole", "ConstructionPolicy"];

    public static C3DThreePointPlaneEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, recipeDirectory, out var input, out var message))
        {
            return new C3DThreePointPlaneEvaluation(new ToolResult("3-Point Plane", ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DThreePointPlaneRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        string? recipeDirectory,
        out C3DThreePointPlaneInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            var step = document.Steps.SingleOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "three-point-plane", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the 3-Point Plane v1 adapter.");
            }
            if (step.InputEntityIds.Count != 2 || !string.Equals(step.InputEntityIds[0], document.Source.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("3-Point Plane v1 requires the recipe-bound raw C3D source first and one PointSet second.");
            }
            var selection = (document.Selections ?? []).SingleOrDefault(item => string.Equals(item.Id, step.InputEntityIds[1], StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("3-Point Plane v1 requires one recipe-owned PointSet second input.");
            var outputRole = ParseParameters(step);
            var source = document.Source;
            if (!string.Equals(source.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || source.ByteLength is null || string.IsNullOrWhiteSpace(source.ContentSha256)
                || source.GridWidth is null || source.GridHeight is null)
            {
                throw new InvalidDataException("3-Point Plane v1 requires a complete recipe-bound C3D source identity.");
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
            if (!string.Equals(selection.RootSourceId, snapshot.EntityId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.FrameId, snapshot.FrameId, StringComparison.Ordinal)
                || !string.Equals(selection.SourceBinding.Format, "C3D", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(selection.SourceBinding.ContentSha256, snapshot.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
                || selection.SourceBinding.GridWidth != snapshot.Width
                || selection.SourceBinding.GridHeight != snapshot.Height)
            {
                throw new InvalidDataException("3-Point Plane PointSet source identity does not match the current raw C3D source.");
            }
            input = new C3DThreePointPlaneInput(step.Id, snapshot, selection, step.OutputEntityId, outputRole);
            message = "3-Point Plane v1 is ready from the current raw C3D source and its recipe-owned PointSet.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static string ParseParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length || ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("3-Point Plane v1 requires exactly OutputRole and ConstructionPolicy, with no unknown parameters.");
        }
        var outputRole = parameters.Single(parameter => parameter.Name == "OutputRole").Value;
        if (string.IsNullOrWhiteSpace(outputRole) || outputRole != outputRole.Trim())
        {
            throw new InvalidDataException("OutputRole must be an explicit non-empty identifier without surrounding whitespace.");
        }
        var policy = parameters.Single(parameter => parameter.Name == "ConstructionPolicy").Value;
        if (!string.Equals(policy, C3DThreePointPlaneFeature.ConstructionPolicyName, StringComparison.Ordinal))
        {
            throw new InvalidDataException("ConstructionPolicy must be OrderedPointsDefineOrientedPlane.");
        }
        return outputRole;
    }

    private static string ResolveSourcePath(string path, string? recipeDirectory) =>
        Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(recipeDirectory ?? Environment.CurrentDirectory, path));
}

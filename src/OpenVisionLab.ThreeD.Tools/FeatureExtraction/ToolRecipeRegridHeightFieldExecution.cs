using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict A3 recipe adapter. It accepts only one current Published A2 cloud
/// and parses the complete recipe-owned ReferenceGridProfile before Preview.
/// </summary>
public static class ToolRecipeRegridHeightFieldExecution
{
    public static C3DRegridHeightFieldEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        C3DTransformedPointCloud publishedTransformedPointCloud,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedTransformedPointCloud, out var input, out var message))
        {
            return new C3DRegridHeightFieldEvaluation(new ToolResult(C3DRegridHeightFieldRule.ToolName, ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DRegridHeightFieldRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        C3DTransformedPointCloud publishedTransformedPointCloud,
        out C3DRegridHeightFieldInput? input,
        out string message)
    {
        input = null;
        try
        {
            if (!TryValidateRoute(document, stepId, publishedTransformedPointCloud, out var step, out var profile, out message)) return false;
            input = new C3DRegridHeightFieldInput(step!.Id, publishedTransformedPointCloud, profile!, step.OutputEntityId);
            message = "Re-grid Height Map v1 is ready from the current Published TransformedPointCloud and its typed recipe-owned ReferenceGridProfile.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    public static bool TryValidateRoute(
        ToolRecipeDocument document,
        string stepId,
        C3DTransformedPointCloud publishedTransformedPointCloud,
        out ToolRecipeStep? step,
        out C3DReferenceGridProfile? profile,
        out string message)
    {
        step = null;
        profile = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            ArgumentNullException.ThrowIfNull(publishedTransformedPointCloud);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            step = document.Steps.SingleOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "re-grid-height-map", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Re-grid Height Map v1 adapter.");
            }
            if (step.InputEntityIds.Count != 1 || !string.Equals(step.InputEntityIds[0], publishedTransformedPointCloud.OutputEntityId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Re-grid Height Map v1 requires exactly the current Published TransformedPointCloud as its only input.");
            }
            profile = C3DReferenceGridProfile.FromRecipeParameters(step.Parameters);
            if (!string.Equals(profile.ReferenceFrameId, publishedTransformedPointCloud.ReferenceFrameId, StringComparison.Ordinal)
                || !string.Equals(profile.ReferenceUnit, publishedTransformedPointCloud.ReferenceUnit, StringComparison.Ordinal)
                || !string.Equals(profile.ReferenceProvenance, publishedTransformedPointCloud.ReferenceProvenance, StringComparison.Ordinal)
                || !string.Equals(profile.ReferenceRevision, publishedTransformedPointCloud.ReferenceRevision, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Re-grid Height Map v1 ReferenceGridProfile does not match the current Published TransformedPointCloud reference identity/frame/unit/provenance.");
            }
            message = "Re-grid Height Map v1 route and typed ReferenceGridProfile are valid; Preview does not modify the A2 cloud.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            step = null;
            profile = null;
            message = exception.Message;
            return false;
        }
    }
}

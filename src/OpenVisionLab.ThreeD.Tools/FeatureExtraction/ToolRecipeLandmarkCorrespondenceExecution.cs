using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict typed adapter for the authored correspondence selection. It accepts
/// only the exact current Published CornerAnchors supplied by the caller and
/// never invokes an upstream feature tool or an affine transform.
/// </summary>
public static class ToolRecipeLandmarkCorrespondenceExecution
{
    private static readonly string[] ParameterNames =
    [
        "PairCountPolicy", "SourceArtifactPolicy", "AffineIndependencePolicy"
    ];

    public static C3DLandmarkCorrespondenceEvaluation Execute(
        ToolRecipeDocument document,
        string stepId,
        IReadOnlyList<C3DLineIntersectionFeature> publishedCornerAnchors,
        CancellationToken cancellationToken = default)
    {
        if (!TryPrepare(document, stepId, publishedCornerAnchors, out var input, out var message))
        {
            return new C3DLandmarkCorrespondenceEvaluation(
                new ToolResult("Landmark Correspondence", ResultStatus.Error, message, TimeSpan.Zero, [], []), null);
        }
        return C3DLandmarkCorrespondenceRule.Evaluate(input!, cancellationToken);
    }

    public static bool TryPrepare(
        ToolRecipeDocument document,
        string stepId,
        IReadOnlyList<C3DLineIntersectionFeature> publishedCornerAnchors,
        out C3DLandmarkCorrespondenceInput? input,
        out string message)
    {
        input = null;
        try
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentException.ThrowIfNullOrWhiteSpace(stepId);
            ArgumentNullException.ThrowIfNull(publishedCornerAnchors);
            var validation = ToolRecipeValidator.Validate(document);
            if (!validation.IsValid) throw new InvalidDataException(string.Join(" ", validation.Errors));
            if (!string.Equals(document.SchemaVersion, ToolRecipeDocument.CurrentSchemaVersion, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Landmark Correspondence v1 requires teaching recipe schema {ToolRecipeDocument.CurrentSchemaVersion}.");
            }
            var step = document.Steps.SingleOrDefault(candidate => string.Equals(candidate.Id, stepId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"Teaching recipe must contain exactly one step with ID '{stepId}'.");
            if (!string.Equals(step.ToolId, "landmark-correspondence", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Step '{step.Id}' is not the Landmark Correspondence v1 adapter.");
            }
            if (step.InputEntityIds.Count != 1)
            {
                throw new InvalidDataException("Landmark Correspondence v1 requires exactly one structured correspondence-selection input.");
            }
            var selection = (document.Selections ?? []).SingleOrDefault(candidate => string.Equals(candidate.Id, step.InputEntityIds[0], StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("Landmark Correspondence v1 input must be an authored structured correspondence selection.");
            if (!string.Equals(selection.Kind, ToolRecipeSelectionKinds.LandmarkCorrespondenceSet, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Landmark Correspondence v1 input selection must be landmark-correspondence-set.");
            }
            if (string.IsNullOrWhiteSpace(step.OutputEntityId)
                || string.Equals(step.OutputEntityId, selection.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Landmark Correspondence output ID must be explicit and differ from its selection input.");
            }

            ValidateParameters(step);
            var descriptor = selection.CorrespondenceDescriptor
                ?? throw new InvalidDataException("Landmark Correspondence v1 descriptor is required.");
            var rows = selection.Rows ?? [];
            if (rows.Count != 4 || publishedCornerAnchors.Count != 4)
            {
                throw new InvalidDataException("Landmark Correspondence v1 requires exactly four authored rows and four current Published CornerAnchor inputs.");
            }
            var anchorsByEntityId = publishedCornerAnchors.ToDictionary(anchor => anchor.OutputEntityId, StringComparer.OrdinalIgnoreCase);
            if (anchorsByEntityId.Count != 4)
            {
                throw new InvalidDataException("Landmark Correspondence v1 requires four distinct current Published CornerAnchor output IDs.");
            }
            var anchors = rows.Select(row => anchorsByEntityId.TryGetValue(row.SourceEntityId, out var anchor)
                    ? anchor
                    : throw new InvalidDataException($"Correspondence source '{row.SourceEntityId}' is not a current Published CornerAnchor."))
                .ToArray();
            ValidateRecipeSource(document.Source, anchors);
            input = new C3DLandmarkCorrespondenceInput(
                step.Id,
                step.OutputEntityId,
                anchors,
                rows.Select(row => new C3DReferenceLandmark(
                    row.ReferenceLandmarkId,
                    row.ReferencePosition.X,
                    row.ReferencePosition.Y,
                    row.ReferencePosition.Z)).ToArray(),
                descriptor.ReferenceFrameId,
                descriptor.ReferenceUnit,
                descriptor.ReferenceProvenance,
                descriptor.ReferenceRevision,
                descriptor.MinimumNormalizedTetrahedronVolume!.Value);
            message = "Landmark Correspondence v1 is ready from four current Published CornerAnchors.";
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            message = exception.Message;
            return false;
        }
    }

    private static void ValidateParameters(ToolRecipeStep step)
    {
        var parameters = step.Parameters ?? [];
        if (parameters.Count != ParameterNames.Length || ParameterNames.Any(name => parameters.Count(parameter => parameter.Name == name) != 1))
        {
            throw new InvalidDataException("Landmark Correspondence v1 requires exactly one value for every recognized parameter and no unknown parameters.");
        }
        string Value(string name) => parameters.Single(parameter => parameter.Name == name).Value;
        if (!string.Equals(Value("PairCountPolicy"), "ExactlyFour", StringComparison.Ordinal)
            || !string.Equals(Value("SourceArtifactPolicy"), "CurrentPublishedCornerAnchor", StringComparison.Ordinal)
            || !string.Equals(Value("AffineIndependencePolicy"), "RequireNonDegenerateTetrahedra", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Landmark Correspondence v1 fixed policies do not match the approved contract.");
        }
    }

    private static void ValidateRecipeSource(ToolRecipeSource source, IReadOnlyList<C3DLineIntersectionFeature> anchors)
    {
        foreach (var anchor in anchors)
        {
            if (!string.Equals(anchor.RootSourceEntityId, source.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(anchor.RootSourceSha256, source.ContentSha256, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(anchor.FrameId, source.FrameId, StringComparison.Ordinal)
                || !string.Equals(anchor.Unit, source.Unit, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Published CornerAnchor root source identity does not match the teaching recipe.");
            }
        }
    }
}


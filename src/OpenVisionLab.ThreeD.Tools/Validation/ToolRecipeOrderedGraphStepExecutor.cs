using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Tools;

internal sealed record ToolRecipeOrderedGraphStepExecution(
    ToolResult Result,
    object? Output,
    C3DOutlierCellMap? OutlierMask = null,
    C3DLevelFrameArtifact? LevelFrame = null,
    C3DLevelFrameQualityEvidence? LevelFrameQuality = null,
    C3DLevelSurfaceCoordinateFrameChain? FrameChain = null);

/// <summary>
/// Selects and invokes the typed adapter for one ordered recipe step.
/// Artifact lookup and route-specific result details stay at this boundary so
/// the ordered replay owner can focus on source identity and lifecycle policy.
/// </summary>
internal static class ToolRecipeOrderedGraphStepExecutor
{
    private static readonly HashSet<string> SupportedToolIds = new(StringComparer.Ordinal)
    {
        "filter",
        "remove-outlier-pixels",
        "connected-region",
        "editable-region",
        "domain-mask",
        "level-surface",
        "roi-crop",
        "height-difference-edge",
        "two-point-line",
        "three-point-plane",
        "datum-plane-raw-height-deviation",
        "three-d-line-fit",
        "line-intersection",
        "landmark-correspondence",
        "xyz-affine-solve",
        "xyz-affine-apply",
        "re-grid-height-map",
        "thickness",
        "warpage",
        "plane-flatness",
        "point-pair-dimensions",
        "gap-flush",
        "volume",
        "cross-section-dimensions",
        "completeness-grid"
    };

    private static readonly HashSet<string> MeasurementToolIds = new(StringComparer.Ordinal)
    {
        "thickness",
        "warpage",
        "plane-flatness",
        "point-pair-dimensions",
        "gap-flush",
        "volume",
        "cross-section-dimensions",
        "completeness-grid"
    };

    public static bool IsSupported(string toolId) => SupportedToolIds.Contains(toolId);

    public static ToolRecipeOrderedGraphStepExecution Execute(
        ToolRecipeDocument document,
        ToolRecipeStep step,
        IReadOnlyDictionary<string, object> artifacts,
        CancellationToken cancellationToken)
    {
        switch (step.ToolId)
        {
            case "filter":
            {
                var evaluation = ToolRecipeFilterExecution.Execute(document, step.Id, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "remove-outlier-pixels":
            {
                var evaluation = ToolRecipeRemoveOutlierPixelsExecution.Execute(
                    document,
                    step.Id,
                    cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output, evaluation.OutlierMask);
            }
            case "connected-region":
            {
                var input = Required<C3DHeightFieldSnapshot>(
                    artifacts,
                    step.InputEntityIds[0],
                    step);
                var mask = Required<C3DOutlierCellMap>(
                    artifacts,
                    GetOutlierMaskArtifactId(step.InputEntityIds[0]),
                    step);
                var evaluation = ToolRecipeConnectedRegionExecution.Execute(
                    document,
                    step.Id,
                    input,
                    mask,
                    cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "editable-region":
            {
                var input = Required<C3DConnectedRegionArtifact>(
                    artifacts,
                    step.InputEntityIds[0],
                    step);
                var evaluation = ToolRecipeEditableRegionExecution.Execute(
                    document,
                    step.Id,
                    input,
                    cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "domain-mask":
            {
                var source = Required<C3DHeightFieldSnapshot>(
                    artifacts,
                    step.InputEntityIds[0],
                    step);
                var domain = Required<C3DConnectedRegionArtifact>(
                    artifacts,
                    step.InputEntityIds[1],
                    step);
                var evaluation = ToolRecipeDomainMaskExecution.Execute(
                    document,
                    step.Id,
                    source,
                    domain,
                    cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "level-surface":
            {
                var evaluation = ToolRecipeLevelSurfaceExecution.Execute(
                    document,
                    step.Id,
                    cancellationToken: cancellationToken);
                return new(
                    evaluation.Result,
                    evaluation.Output,
                    null,
                    evaluation.LevelFrame,
                    evaluation.QualityEvidence,
                    evaluation.FrameChain);
            }
            case "roi-crop":
            {
                var evaluation = ToolRecipeRoiCropExecution.Execute(
                    document,
                    step.Id,
                    cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "height-difference-edge":
            {
                var input = Required<C3DHeightFieldSnapshot>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeHeightDifferenceEdgeExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "two-point-line":
            {
                var evaluation = ToolRecipeTwoPointLineExecution.Execute(document, step.Id, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "three-point-plane":
            {
                var evaluation = ToolRecipeThreePointPlaneExecution.Execute(document, step.Id, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "datum-plane-raw-height-deviation":
            {
                var plane = Required<C3DThreePointPlaneFeature>(artifacts, step.InputEntityIds[1], step);
                var evaluation = ToolRecipeDatumPlaneDeviationExecution.Execute(document, step.Id, plane, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "three-d-line-fit":
            {
                var input = Required<C3DHeightDifferenceEdgePointSet>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeLineFitExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "line-intersection":
            {
                var first = Required<IC3DLineGeometry>(artifacts, step.InputEntityIds[0], step);
                var second = Required<IC3DLineGeometry>(artifacts, step.InputEntityIds[1], step);
                var evaluation = ToolRecipeLineIntersectionExecution.Execute(document, step.Id, first, second, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "landmark-correspondence":
            {
                var anchors = artifacts.Values.OfType<C3DLineIntersectionFeature>().ToArray();
                var evaluation = ToolRecipeLandmarkCorrespondenceExecution.Execute(document, step.Id, anchors, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "xyz-affine-solve":
            {
                var input = Required<C3DLandmarkCorrespondenceSet>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeXYZAffineSolveExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "xyz-affine-apply":
            {
                var input = Required<C3DAffineTransform3D>(artifacts, step.InputEntityIds[1], step);
                var evaluation = ToolRecipeXYZAffineApplyExecution.Execute(document, step.Id, input, cancellationToken: cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case "re-grid-height-map":
            {
                var input = Required<C3DTransformedPointCloud>(artifacts, step.InputEntityIds[0], step);
                var evaluation = ToolRecipeRegridHeightFieldExecution.Execute(document, step.Id, input, cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            case var measurementToolId when MeasurementToolIds.Contains(measurementToolId):
            {
                C3DHeightFieldSnapshot? heightField = null;
                C3DTransformedHeightField? field = null;
                C3DEditableRegionArtifact? editableRegion = null;
                if (step.InputEntityIds.Count > 0
                    && artifacts.TryGetValue(step.InputEntityIds[0], out var inputArtifact))
                {
                    heightField = inputArtifact as C3DHeightFieldSnapshot;
                    field = inputArtifact as C3DTransformedHeightField;
                }
                if (step.ToolId == "completeness-grid"
                    && step.InputEntityIds.Count > 2
                    && artifacts.TryGetValue(step.InputEntityIds[2], out var inspectionArtifact))
                {
                    editableRegion = inspectionArtifact as C3DEditableRegionArtifact
                        ?? throw new InvalidDataException(
                            $"Step '{step.Id}' requires '{step.InputEntityIds[2]}' as EditableRegionArtifact.");
                }
                var evaluation = ToolRecipeHeightMeasurementExecution.Execute(
                    document,
                    step.Id,
                    heightField,
                    field,
                    editableRegion,
                    recipeDirectory: null,
                    cancellationToken);
                return new(evaluation.Result, evaluation.Output);
            }
            default:
                return new(
                    new ToolResult(
                        step.ToolName,
                        ResultStatus.Error,
                        $"No executable typed adapter is registered for tool '{step.ToolId}'.",
                        TimeSpan.Zero,
                        [],
                        []),
                    null);
        }
    }

    public static string GetOutlierMaskArtifactId(string outputEntityId) =>
        $"{outputEntityId}::outlier-mask";

    private static T Required<T>(
        IReadOnlyDictionary<string, object> artifacts,
        string entityId,
        ToolRecipeStep step)
        where T : class
    {
        if (!artifacts.TryGetValue(entityId, out var artifact))
        {
            throw new InvalidDataException(
                $"Step '{step.Id}' is waiting for input entity '{entityId}', which was not published by an earlier step.");
        }
        if (artifact is not T typed)
        {
            throw new InvalidDataException(
                $"Step '{step.Id}' requires input '{entityId}' as {typeof(T).Name}, "
                + $"but the published artifact is {artifact.GetType().Name}.");
        }
        return typed;
    }
}

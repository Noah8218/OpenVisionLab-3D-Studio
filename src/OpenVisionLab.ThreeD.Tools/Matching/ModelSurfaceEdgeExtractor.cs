using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict product adapter for boundary/crease extraction from immutable
/// SurfaceModel topology. Library-Noah owns the geometry calculation.
/// </summary>
public static class ModelSurfaceEdgeExtractor
{
    public static ModelSurfaceEdgeArtifact Extract(
        SurfaceModelArtifact model,
        ModelSurfaceEdgeExtractionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(parameters);
        var validity = SurfaceModelArtifactValidator.Inspect(model);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Model edge extraction requires a valid SurfaceModel.");
        }

        if (parameters.Method
            != ModelSurfaceEdgeExtractionParameters
                .TopologyBoundaryAndCreaseMethod)
        {
            throw new InvalidDataException(
                "Model edge extraction method is unsupported.");
        }

        var noahResult = new DeterministicModelSurfaceEdgeExtractionTool()
            .Execute(
                model.Points
                    .Select(LibraryNoahSurfaceMatching.Point)
                    .ToArray(),
                model.Triangles
                    .Select(triangle => new SurfaceModelTriangleInput(
                        triangle.FirstPointIndex,
                        triangle.SecondPointIndex,
                        triangle.ThirdPointIndex))
                    .ToArray(),
                new DeterministicModelSurfaceEdgeExtractionOptions
                {
                    MinimumEdgeLength = parameters.MinimumEdgeLength,
                    MinimumCreaseAngleDegrees =
                        parameters.MinimumCreaseAngleDegrees,
                    IncludeBoundaryEdges = parameters.IncludeBoundaryEdges
                });
        if (!noahResult.Success)
        {
            throw new InvalidDataException(noahResult.Message);
        }

        var extracted = noahResult.Edges
            .Select(edge => new ModelSurfaceEdgeSample(
                edge.Order,
                edge.FirstPointIndex,
                edge.SecondPointIndex,
                Point(edge.FirstPosition),
                Point(edge.SecondPosition),
                Point(edge.Anchor),
                edge.Length,
                edge.StrengthDegrees,
                edge.Kind == ExtractedModelSurfaceEdgeKind.Boundary
                    ? ModelSurfaceEdgeKind.Boundary
                    : ModelSurfaceEdgeKind.Crease))
            .ToArray();

        return ModelSurfaceEdgeArtifact.Create(model, parameters, extracted);
    }

    private static SurfaceModelPoint3 Point(ThreeDPoint point) =>
        new(point.X, point.Y, point.Z);
}

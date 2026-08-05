using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict product adapter for height-step extraction over one complete
/// organized XYZ grid. OpenVisionLab Vision SDK owns the neighbor calculation.
/// </summary>
public static class SceneSurfaceEdgeExtractor
{
    public static SceneSurfaceEdgeArtifact Extract(
        PreparedSceneArtifact scene,
        SceneSurfaceEdgeExtractionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(parameters);
        var validity = PreparedSceneArtifactValidator.Inspect(scene);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Scene edge extraction requires a valid Prepared Scene.");
        }

        if (parameters.Method
            != SceneSurfaceEdgeExtractionParameters
                .OrganizedHeightStepMethod)
        {
            throw new InvalidDataException(
                "Scene edge extraction method is unsupported.");
        }

        var grid = scene.SourceQuality.Grid;
        var coverage = scene.SourceQuality.Coverage;
        var expectedPointCount = checked((long)grid.Width * grid.Height);
        if (coverage.MissingSampleCount != 0
            || coverage.ValidSampleCount != expectedPointCount
            || coverage.SampleCount != expectedPointCount
            || scene.Points.LongLength != expectedPointCount)
        {
            throw new InvalidDataException(
                "Scene edge extraction version 1 requires a complete organized XYZ grid.");
        }

        var sdkResult =
            new DeterministicOrganizedSceneSurfaceEdgeExtractionTool()
                .Execute(
                    scene.Points
                        .Select(VisionSdkSurfaceMatching.Point)
                        .ToArray(),
                    new DeterministicOrganizedSceneSurfaceEdgeExtractionOptions
                    {
                        Width = grid.Width,
                        Height = grid.Height,
                        MinimumAbsoluteHeightStep =
                            parameters.MinimumAbsoluteHeightStep,
                        IncludeColumnNeighbors =
                            parameters.IncludeColumnNeighbors,
                        IncludeRowNeighbors =
                            parameters.IncludeRowNeighbors
                    });
        if (!sdkResult.Success)
        {
            throw new InvalidDataException(sdkResult.Message);
        }

        var edges = sdkResult.Edges
            .Select(edge => new SceneSurfaceEdgeSample(
                edge.Order,
                edge.FirstPointIndex,
                edge.SecondPointIndex,
                edge.AnchorPointIndex,
                Point(edge.FirstPosition),
                Point(edge.SecondPosition),
                Point(edge.Anchor),
                edge.AbsoluteHeightStep,
                edge.Axis == ExtractedSceneSurfaceEdgeAxis.AcrossColumns
                    ? SceneSurfaceEdgeAxis.AcrossColumns
                    : SceneSurfaceEdgeAxis.AcrossRows))
            .ToArray();
        return SceneSurfaceEdgeArtifact.Create(scene, parameters, edges);
    }

    private static SurfaceModelPoint3 Point(ThreeDPoint point) =>
        new(point.X, point.Y, point.Z);
}

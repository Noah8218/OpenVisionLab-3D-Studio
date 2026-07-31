using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Deterministic height-step extraction over one complete organized XYZ
/// grid. Missing cells and ambiguous point-to-cell mappings fail closed.
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
                    .OrganizedHeightStepMethod
            || !double.IsFinite(parameters.MinimumAbsoluteHeightStep)
            || parameters.MinimumAbsoluteHeightStep <= 0.0
            || !parameters.IncludeColumnNeighbors
                && !parameters.IncludeRowNeighbors)
        {
            throw new InvalidDataException(
                "Scene edge extraction parameters are invalid.");
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

        var candidates = new List<SceneEdgeCandidate>();
        for (var row = 0; row < grid.Height; row++)
        {
            for (var column = 0; column < grid.Width; column++)
            {
                var firstIndex = checked(row * grid.Width + column);
                if (parameters.IncludeColumnNeighbors
                    && column + 1 < grid.Width)
                {
                    AddCandidate(
                        scene.Points,
                        firstIndex,
                        firstIndex + 1,
                        SceneSurfaceEdgeAxis.AcrossColumns,
                        parameters.MinimumAbsoluteHeightStep,
                        candidates);
                }

                if (parameters.IncludeRowNeighbors
                    && row + 1 < grid.Height)
                {
                    AddCandidate(
                        scene.Points,
                        firstIndex,
                        firstIndex + grid.Width,
                        SceneSurfaceEdgeAxis.AcrossRows,
                        parameters.MinimumAbsoluteHeightStep,
                        candidates);
                }
            }
        }

        var edges = candidates
            .OrderBy(candidate => candidate.FirstPointIndex)
            .ThenBy(candidate => candidate.SecondPointIndex)
            .Select((candidate, order) => new SceneSurfaceEdgeSample(
                order,
                candidate.FirstPointIndex,
                candidate.SecondPointIndex,
                candidate.AnchorPointIndex,
                candidate.FirstPosition,
                candidate.SecondPosition,
                candidate.Anchor,
                candidate.AbsoluteHeightStep,
                candidate.Axis))
            .ToArray();
        return SceneSurfaceEdgeArtifact.Create(scene, parameters, edges);
    }

    private static void AddCandidate(
        IReadOnlyList<SurfaceModelPoint3> points,
        int firstIndex,
        int secondIndex,
        SceneSurfaceEdgeAxis axis,
        double threshold,
        ICollection<SceneEdgeCandidate> candidates)
    {
        var first = points[firstIndex];
        var second = points[secondIndex];
        var step = Math.Abs(first.Z - second.Z);
        if (step < threshold)
        {
            return;
        }

        var anchorIndex = first.Z > second.Z ? firstIndex : secondIndex;
        candidates.Add(new SceneEdgeCandidate(
            firstIndex,
            secondIndex,
            anchorIndex,
            first,
            second,
            anchorIndex == firstIndex ? first : second,
            step,
            axis));
    }

    private sealed record SceneEdgeCandidate(
        int FirstPointIndex,
        int SecondPointIndex,
        int AnchorPointIndex,
        SurfaceModelPoint3 FirstPosition,
        SurfaceModelPoint3 SecondPosition,
        SurfaceModelPoint3 Anchor,
        double AbsoluteHeightStep,
        SceneSurfaceEdgeAxis Axis);
}

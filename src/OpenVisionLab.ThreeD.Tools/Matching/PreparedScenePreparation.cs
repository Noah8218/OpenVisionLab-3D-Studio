using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

public sealed record PreparedScenePreparationRequest(
    string ArtifactId,
    string Name,
    string CoordinateConvention,
    SourceQualityReport SourceQuality,
    IReadOnlyList<SurfaceModelPoint3> FinitePoints,
    PreparedScenePreparationParameters Parameters);

/// <summary>
/// Strict product adapter for measured-scene preparation. Library-Noah owns
/// deterministic even-index sampling; Studio owns Source Quality identity,
/// source preservation, and the persisted artifact.
/// </summary>
public static class PreparedScenePreparation
{
    public static PreparedSceneArtifact Prepare(
        PreparedScenePreparationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourceQuality);
        ArgumentNullException.ThrowIfNull(request.FinitePoints);
        ArgumentNullException.ThrowIfNull(request.Parameters);

        if (request.Parameters.SamplingPolicy
            != PreparedScenePreparationParameters
                .DeterministicEvenPointSampling)
        {
            throw new InvalidDataException(
                "Prepared Scene preparation sampling policy is unsupported.");
        }

        if (request.Parameters.MaximumSampleCount <= 0)
        {
            throw new InvalidDataException(
                "Prepared Scene preparation requires a positive maximum sample count.");
        }

        var points = request.FinitePoints.ToArray();
        var noahResult = new DeterministicPreparedScenePreparationTool()
            .Execute(
                points.Select(LibraryNoahSurfaceMatching.Point).ToArray(),
                new DeterministicPreparedScenePreparationOptions
                {
                    MaximumSampleCount =
                        request.Parameters.MaximumSampleCount
                });
        if (!noahResult.Success)
        {
            throw new InvalidDataException(noahResult.Message);
        }

        var samples = noahResult.Samples
            .Select(sample => new PreparedSceneSample(
                sample.Order,
                sample.SourcePointIndex,
                points[sample.SourcePointIndex]))
            .ToArray();

        return PreparedSceneArtifact.Create(
            request.ArtifactId,
            request.Name,
            request.CoordinateConvention,
            request.SourceQuality,
            request.Parameters,
            points,
            samples);
    }
}

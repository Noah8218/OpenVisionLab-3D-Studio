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
/// Pure measured-scene preparation. Version 1 admits only complete finite
/// point evidence tied to SourceQualityReport and never repairs source data.
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
        var sampleCount = Math.Min(
            request.Parameters.MaximumSampleCount,
            points.Length);
        var samples = new PreparedSceneSample[sampleCount];
        for (var sampleOrder = 0;
             sampleOrder < sampleCount;
             sampleOrder++)
        {
            var sourcePointIndex =
                PreparedSceneSampling.GetEvenPointIndex(
                    sampleOrder,
                    sampleCount,
                    points.Length);
            samples[sampleOrder] = new PreparedSceneSample(
                sampleOrder,
                sourcePointIndex,
                points[sourcePointIndex]);
        }

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

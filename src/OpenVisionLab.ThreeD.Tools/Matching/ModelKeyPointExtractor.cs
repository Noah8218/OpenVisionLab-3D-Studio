using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Tools;

/// <summary>
/// Strict identity adapter from prepared SurfaceModel samples to the public
/// Library-Noah key-point Tool. Studio performs no selection arithmetic.
/// </summary>
public static class ModelKeyPointExtractor
{
    public static ModelKeyPointArtifact Extract(
        SurfaceModelArtifact model,
        ModelKeyPointExtractionParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!SurfaceModelArtifactValidator.Inspect(model).IsValid)
        {
            throw new InvalidDataException(
                "Model key-point extraction requires a valid SurfaceModel.");
        }

        if (parameters.Method
            != ModelKeyPointExtractionParameters
                .DeterministicFarthestModelSampleMethod)
        {
            throw new InvalidDataException(
                "Model key-point extraction method is unsupported.");
        }

        var result = new DeterministicModelKeyPointExtractionTool().Execute(
            model.Samples
                .Select(sample => new ModelKeyPointInput(
                    sample.Order,
                    Point(sample.Position),
                    Point(sample.Normal)))
                .ToArray(),
            new DeterministicModelKeyPointExtractionOptions
            {
                MaximumKeyPointCount = parameters.MaximumKeyPointCount,
                MinimumSeparation = parameters.MinimumSeparation
            });
        if (!result.Success)
        {
            throw new InvalidDataException(result.Message);
        }

        var samplesByOrder = model.Samples.ToDictionary(
            sample => sample.Order);
        var keyPoints = result.KeyPoints
            .Select(point =>
            {
                var source = samplesByOrder[point.SourceSampleOrder];
                return new ModelKeyPointSample(
                    point.Order,
                    $"kp.sample.{point.SourceSampleOrder:D8}",
                    point.SourceSampleOrder,
                    source.SourceTriangleIndex,
                    Point(point.Position),
                    Point(point.Normal),
                    point.NearestSelectedDistance);
            })
            .ToArray();
        return ModelKeyPointArtifact.Create(model, parameters, keyPoints);
    }

    private static ThreeDPoint Point(SurfaceModelPoint3 point) =>
        new(point.X, point.Y, point.Z);

    private static SurfaceModelPoint3 Point(ThreeDPoint point) =>
        new(point.X, point.Y, point.Z);
}

/// <summary>
/// Creates display-only WPF-neutral position/normal markers without changing
/// a model, pose search, score, or acceptance artifact.
/// </summary>
public static class ModelKeyPointDebugOverlayBuilder
{
    public static ModelKeyPointDebugOverlayArtifact Build(
        SurfaceModelArtifact model,
        ModelKeyPointArtifact keyPoints)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(keyPoints);
        if (!ModelKeyPointArtifactValidator.Inspect(
                keyPoints,
                model).IsValid)
        {
            throw new InvalidDataException(
                "Model key-point debug overlay requires one exact identified model and key-point chain.");
        }

        return ModelKeyPointDebugOverlayArtifact.Create(model, keyPoints);
    }
}

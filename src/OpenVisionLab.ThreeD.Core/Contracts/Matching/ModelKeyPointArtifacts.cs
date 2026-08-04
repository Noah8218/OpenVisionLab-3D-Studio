using System.Security.Cryptography;
using System.Text;

namespace OpenVisionLab.ThreeD.Core;

public sealed record ModelKeyPointExtractionParameters(
    string Method,
    int MaximumKeyPointCount,
    double MinimumSeparation)
{
    public const string DeterministicFarthestModelSampleMethod =
        "deterministic-farthest-model-sample-v1";
}

public sealed record ModelKeyPointSample(
    int Order,
    string KeyPointId,
    int SourceSampleOrder,
    int SourceTriangleIndex,
    SurfaceModelPoint3 Position,
    SurfaceModelPoint3 Normal,
    double NearestSelectedDistance);

/// <summary>
/// Identified representative points derived from one immutable SurfaceModel
/// sample set. This evidence does not execute or change surface matching.
/// </summary>
public sealed record ModelKeyPointArtifact(
    string SchemaVersion,
    string Semantics,
    string ArtifactId,
    string ModelContentSha256,
    string Unit,
    string FrameId,
    int SourceSampleCount,
    ModelKeyPointExtractionParameters Parameters,
    ModelKeyPointSample[] KeyPoints,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "identified-model-key-points-no-matching-effect-v1";

    public static ModelKeyPointArtifact Create(
        SurfaceModelArtifact model,
        ModelKeyPointExtractionParameters parameters,
        IReadOnlyList<ModelKeyPointSample> keyPoints)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(keyPoints);
        var artifact = new ModelKeyPointArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            $"key-points.model.{model.ArtifactId}",
            model.ContentSha256,
            model.Unit,
            model.FrameId,
            model.Samples.Length,
            parameters,
            keyPoints.ToArray(),
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
        var validity = ModelKeyPointArtifactValidator.Inspect(
            artifact,
            model);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Model key-point artifact is invalid: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        ModelKeyPointArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Parameters);
        ArgumentNullException.ThrowIfNull(artifact.KeyPoints);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.ModelKeyPointArtifact");
            WriteText(writer, artifact.SchemaVersion);
            WriteText(writer, artifact.Semantics);
            WriteText(writer, artifact.ArtifactId);
            WriteText(writer, artifact.ModelContentSha256);
            WriteText(writer, artifact.Unit);
            WriteText(writer, artifact.FrameId);
            writer.Write(artifact.SourceSampleCount);
            WriteText(writer, artifact.Parameters.Method);
            writer.Write(artifact.Parameters.MaximumKeyPointCount);
            writer.Write(artifact.Parameters.MinimumSeparation);
            writer.Write(artifact.KeyPoints.Length);
            foreach (var point in artifact.KeyPoints)
            {
                writer.Write(point.Order);
                WriteText(writer, point.KeyPointId);
                writer.Write(point.SourceSampleOrder);
                writer.Write(point.SourceTriangleIndex);
                WritePoint(writer, point.Position);
                WritePoint(writer, point.Normal);
                writer.Write(point.NearestSelectedDistance);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    internal static void WriteText(BinaryWriter writer, string? value) =>
        writer.Write(value ?? string.Empty);

    internal static void WritePoint(
        BinaryWriter writer,
        SurfaceModelPoint3 point)
    {
        ArgumentNullException.ThrowIfNull(point);
        writer.Write(point.X);
        writer.Write(point.Y);
        writer.Write(point.Z);
    }
}

public sealed record ModelKeyPointDebugMarker(
    int Order,
    string KeyPointId,
    int SourceSampleOrder,
    int SourceTriangleIndex,
    SurfaceModelPoint3 Position,
    SurfaceModelPoint3 Normal,
    double NearestSelectedDistance);

/// <summary>
/// WPF-neutral display-only markers in the model frame. The Viewer may choose
/// marker size and normal-vector length without changing this evidence.
/// </summary>
public sealed record ModelKeyPointDebugOverlayArtifact(
    string SchemaVersion,
    string Semantics,
    string ArtifactId,
    string ModelContentSha256,
    string KeyPointContentSha256,
    string Unit,
    string FrameId,
    ModelKeyPointDebugMarker[] Markers,
    string ContentSha256)
{
    public const string CurrentSchemaVersion = "1.0";
    public const string CurrentSemantics =
        "model-key-point-position-and-normal-debug-overlay-v1";

    public static ModelKeyPointDebugOverlayArtifact Create(
        SurfaceModelArtifact model,
        ModelKeyPointArtifact keyPoints)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(keyPoints);
        var markers = keyPoints.KeyPoints
            .Select(point => new ModelKeyPointDebugMarker(
                point.Order,
                point.KeyPointId,
                point.SourceSampleOrder,
                point.SourceTriangleIndex,
                point.Position,
                point.Normal,
                point.NearestSelectedDistance))
            .ToArray();
        var artifact = new ModelKeyPointDebugOverlayArtifact(
            CurrentSchemaVersion,
            CurrentSemantics,
            $"overlay.{keyPoints.ArtifactId}",
            model.ContentSha256,
            keyPoints.ContentSha256,
            model.Unit,
            model.FrameId,
            markers,
            string.Empty);
        artifact = artifact with
        {
            ContentSha256 = CalculateContentSha256(artifact)
        };
        var validity = ModelKeyPointDebugOverlayArtifactValidator.Inspect(
            artifact,
            keyPoints,
            model);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Model key-point debug overlay is invalid: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    public static string CalculateContentSha256(
        ModelKeyPointDebugOverlayArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(artifact.Markers);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write("OpenVisionLab.ModelKeyPointDebugOverlayArtifact");
            ModelKeyPointArtifact.WriteText(writer, artifact.SchemaVersion);
            ModelKeyPointArtifact.WriteText(writer, artifact.Semantics);
            ModelKeyPointArtifact.WriteText(writer, artifact.ArtifactId);
            ModelKeyPointArtifact.WriteText(
                writer,
                artifact.ModelContentSha256);
            ModelKeyPointArtifact.WriteText(
                writer,
                artifact.KeyPointContentSha256);
            ModelKeyPointArtifact.WriteText(writer, artifact.Unit);
            ModelKeyPointArtifact.WriteText(writer, artifact.FrameId);
            writer.Write(artifact.Markers.Length);
            foreach (var marker in artifact.Markers)
            {
                writer.Write(marker.Order);
                ModelKeyPointArtifact.WriteText(writer, marker.KeyPointId);
                writer.Write(marker.SourceSampleOrder);
                writer.Write(marker.SourceTriangleIndex);
                ModelKeyPointArtifact.WritePoint(writer, marker.Position);
                ModelKeyPointArtifact.WritePoint(writer, marker.Normal);
                writer.Write(marker.NearestSelectedDistance);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}

public sealed record ModelKeyPointValidityReport(
    string SchemaVersion,
    bool IsValid,
    bool ContentIdentityValid,
    IReadOnlyList<string> Errors,
    string Evidence)
{
    public const string CurrentSchemaVersion = "1.0";
}

public static class ModelKeyPointArtifactValidator
{
    public static ModelKeyPointValidityReport Inspect(
        ModelKeyPointArtifact artifact,
        SurfaceModelArtifact? model = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var parameters = artifact.Parameters;
        var points = artifact.KeyPoints ?? [];
        if (artifact.SchemaVersion != ModelKeyPointArtifact.CurrentSchemaVersion
            || artifact.Semantics != ModelKeyPointArtifact.CurrentSemantics)
        {
            errors.Add("Model key-point schema or semantics are unsupported.");
        }

        if (string.IsNullOrWhiteSpace(artifact.ArtifactId)
            || !CanonicalSha256(artifact.ModelContentSha256)
            || string.IsNullOrWhiteSpace(artifact.Unit)
            || string.IsNullOrWhiteSpace(artifact.FrameId)
            || artifact.SourceSampleCount <= 0)
        {
            errors.Add("Model key-point identity, unit, frame, or source sample count is invalid.");
        }

        if (parameters is null
            || parameters.Method
                != ModelKeyPointExtractionParameters
                    .DeterministicFarthestModelSampleMethod
            || parameters.MaximumKeyPointCount <= 0
            || !double.IsFinite(parameters.MinimumSeparation)
            || parameters.MinimumSeparation < 0.0)
        {
            errors.Add("Model key-point extraction parameters are invalid.");
        }

        if (points.Length == 0
            || points.Length > artifact.SourceSampleCount
            || parameters is not null
               && points.Length > parameters.MaximumKeyPointCount
            || !points.Select(point => point.Order)
                .SequenceEqual(Enumerable.Range(0, points.Length))
            || points.Select(point => point.SourceSampleOrder)
                .Distinct()
                .Count() != points.Length)
        {
            errors.Add("Model key points require a bounded, unique, contiguous order.");
        }

        for (var index = 0; index < points.Length; index++)
        {
            var point = points[index];
            var expectedId = $"kp.sample.{point.SourceSampleOrder:D8}";
            if (point.SourceSampleOrder < 0
                || point.SourceSampleOrder >= artifact.SourceSampleCount
                || point.SourceTriangleIndex < 0
                || point.KeyPointId != expectedId
                || !Finite(point.Position)
                || !Unit(point.Normal)
                || !double.IsFinite(point.NearestSelectedDistance)
                || index == 0 && point.NearestSelectedDistance != 0.0
                || index > 0
                   && parameters is not null
                   && point.NearestSelectedDistance
                       <= parameters.MinimumSeparation)
            {
                errors.Add(
                    $"Model key point {index} has invalid locator, geometry, or separation evidence.");
            }
        }

        if (model is not null)
        {
            if (!SurfaceModelArtifactValidator.Inspect(model).IsValid
                || artifact.ModelContentSha256 != model.ContentSha256
                || artifact.Unit != model.Unit
                || artifact.FrameId != model.FrameId
                || artifact.SourceSampleCount != model.Samples.Length)
            {
                errors.Add("Model key-point artifact does not match the identified SurfaceModel.");
            }
            else
            {
                foreach (var point in points)
                {
                    var source = model.Samples[point.SourceSampleOrder];
                    if (source.Order != point.SourceSampleOrder
                        || source.SourceTriangleIndex
                            != point.SourceTriangleIndex
                        || source.Position != point.Position
                        || source.Normal != point.Normal)
                    {
                        errors.Add(
                            $"Model key point {point.Order} does not preserve its source sample locator.");
                    }
                }
            }
        }

        var identityValid = false;
        try
        {
            identityValid = artifact.ContentSha256
                == ModelKeyPointArtifact.CalculateContentSha256(artifact);
        }
        catch
        {
            identityValid = false;
        }

        if (!identityValid)
        {
            errors.Add("Model key-point content identity is invalid.");
        }

        return new ModelKeyPointValidityReport(
            ModelKeyPointValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            identityValid,
            errors,
            $"sourceSamples={artifact.SourceSampleCount};keyPoints={points.Length};identity={identityValid};matchingEffect=false");
    }

    internal static bool CanonicalSha256(string? value) =>
        value?.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9' or >= 'A' and <= 'F');

    internal static bool Finite(SurfaceModelPoint3? point) =>
        point is not null
        && double.IsFinite(point.X)
        && double.IsFinite(point.Y)
        && double.IsFinite(point.Z);

    internal static bool Unit(SurfaceModelPoint3? point)
    {
        if (!Finite(point))
        {
            return false;
        }

        var length = Math.Sqrt(
            point!.X * point.X
            + point.Y * point.Y
            + point.Z * point.Z);
        return Math.Abs(length - 1.0) <= 1e-6;
    }
}

public static class ModelKeyPointDebugOverlayArtifactValidator
{
    public static ModelKeyPointValidityReport Inspect(
        ModelKeyPointDebugOverlayArtifact artifact,
        ModelKeyPointArtifact? keyPoints = null,
        SurfaceModelArtifact? model = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var errors = new List<string>();
        var markers = artifact.Markers ?? [];
        if (artifact.SchemaVersion
                != ModelKeyPointDebugOverlayArtifact.CurrentSchemaVersion
            || artifact.Semantics
                != ModelKeyPointDebugOverlayArtifact.CurrentSemantics)
        {
            errors.Add("Model key-point debug overlay schema or semantics are unsupported.");
        }

        if (string.IsNullOrWhiteSpace(artifact.ArtifactId)
            || !ModelKeyPointArtifactValidator.CanonicalSha256(
                artifact.ModelContentSha256)
            || !ModelKeyPointArtifactValidator.CanonicalSha256(
                artifact.KeyPointContentSha256)
            || string.IsNullOrWhiteSpace(artifact.Unit)
            || string.IsNullOrWhiteSpace(artifact.FrameId)
            || markers.Length == 0
            || !markers.Select(marker => marker.Order)
                .SequenceEqual(Enumerable.Range(0, markers.Length))
            || markers.Any(marker =>
                string.IsNullOrWhiteSpace(marker.KeyPointId)
                || !ModelKeyPointArtifactValidator.Finite(marker.Position)
                || !ModelKeyPointArtifactValidator.Unit(marker.Normal)
                || !double.IsFinite(marker.NearestSelectedDistance)))
        {
            errors.Add("Model key-point debug overlay identity or marker geometry is invalid.");
        }

        if (keyPoints is not null
            && (artifact.KeyPointContentSha256 != keyPoints.ContentSha256
                || markers.Length != keyPoints.KeyPoints.Length
                || !markers.SequenceEqual(keyPoints.KeyPoints.Select(point =>
                    new ModelKeyPointDebugMarker(
                        point.Order,
                        point.KeyPointId,
                        point.SourceSampleOrder,
                        point.SourceTriangleIndex,
                        point.Position,
                        point.Normal,
                        point.NearestSelectedDistance)))))
        {
            errors.Add("Model key-point debug overlay does not preserve its identified key-point artifact.");
        }

        if (model is not null
            && (artifact.ModelContentSha256 != model.ContentSha256
                || artifact.Unit != model.Unit
                || artifact.FrameId != model.FrameId))
        {
            errors.Add("Model key-point debug overlay does not match the identified SurfaceModel.");
        }

        var identityValid = false;
        try
        {
            identityValid = artifact.ContentSha256
                == ModelKeyPointDebugOverlayArtifact
                    .CalculateContentSha256(artifact);
        }
        catch
        {
            identityValid = false;
        }

        if (!identityValid)
        {
            errors.Add("Model key-point debug overlay content identity is invalid.");
        }

        return new ModelKeyPointValidityReport(
            ModelKeyPointValidityReport.CurrentSchemaVersion,
            errors.Count == 0,
            identityValid,
            errors,
            $"markers={markers.Length};identity={identityValid};displayOnly=true;matchingEffect=false");
    }
}

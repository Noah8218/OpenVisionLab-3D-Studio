using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic persistence for diagnostic overlay, independent assessment, and
/// retained false-positive review evidence.
/// </summary>
public static class SurfaceEdgeDiagnosticReviewArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static void SaveOverlay(
        string path,
        SurfaceEdgeDiagnosticOverlayArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var validity =
            SurfaceEdgeDiagnosticOverlayArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface-edge diagnostic overlay validation failed before save.");
        }

        SaveAtomic(path, artifact);
    }

    public static SurfaceEdgeDiagnosticOverlayArtifact LoadOverlay(
        string path)
    {
        var artifact = Load<SurfaceEdgeDiagnosticOverlayArtifact>(
            path,
            "surface-edge diagnostic overlay");
        var validity =
            SurfaceEdgeDiagnosticOverlayArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface-edge diagnostic overlay validation failed after load.");
        }

        return artifact;
    }

    public static void SaveAssessment(
        string path,
        SurfaceAndEdgeMatchAssessmentArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var validity =
            SurfaceAndEdgeAssessmentArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface/edge assessment validation failed before save.");
        }

        SaveAtomic(path, artifact);
    }

    public static SurfaceAndEdgeMatchAssessmentArtifact LoadAssessment(
        string path)
    {
        var artifact = Load<SurfaceAndEdgeMatchAssessmentArtifact>(
            path,
            "surface/edge assessment");
        var validity =
            SurfaceAndEdgeAssessmentArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface/edge assessment validation failed after load.");
        }

        return artifact;
    }

    public static void SaveReview(
        string path,
        SurfaceMatchFalsePositiveReviewArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var validity =
            SurfaceMatchFalsePositiveReviewArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface-match review validation failed before save.");
        }

        SaveAtomic(path, artifact);
    }

    public static SurfaceMatchFalsePositiveReviewArtifact LoadReview(
        string path)
    {
        var artifact = Load<SurfaceMatchFalsePositiveReviewArtifact>(
            path,
            "surface-match false-positive review");
        var validity =
            SurfaceMatchFalsePositiveReviewArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface-match review validation failed after load.");
        }

        return artifact;
    }

    private static void SaveAtomic<T>(string path, T artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(
                JsonSerializer.Serialize(artifact, JsonOptions));
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static T Load<T>(string path, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        try
        {
            using var stream = new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            return JsonSerializer.Deserialize<T>(stream, JsonOptions)
                ?? throw new InvalidDataException($"{name} JSON is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"{name} JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"{name} JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }
}

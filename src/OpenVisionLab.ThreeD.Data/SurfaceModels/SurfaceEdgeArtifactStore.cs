using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for identified model/scene edge artifacts and
/// their separate diagnostic score.
/// </summary>
public static class SurfaceEdgeArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static void SaveModel(
        string path,
        ModelSurfaceEdgeArtifact artifact) =>
        Save(
            path,
            artifact,
            value => SurfaceEdgeArtifactValidator.Inspect(value).IsValid,
            "model edge");

    public static ModelSurfaceEdgeArtifact LoadModel(string path) =>
        Load<ModelSurfaceEdgeArtifact>(
            path,
            value => SurfaceEdgeArtifactValidator.Inspect(value).IsValid,
            "model edge");

    public static void SaveScene(
        string path,
        SceneSurfaceEdgeArtifact artifact) =>
        Save(
            path,
            artifact,
            value => SurfaceEdgeArtifactValidator.Inspect(value).IsValid,
            "scene edge");

    public static SceneSurfaceEdgeArtifact LoadScene(string path) =>
        Load<SceneSurfaceEdgeArtifact>(
            path,
            value => SurfaceEdgeArtifactValidator.Inspect(value).IsValid,
            "scene edge");

    public static void SaveScore(
        string path,
        SurfaceAndEdgeMatchScoreArtifact artifact) =>
        Save(
            path,
            artifact,
            value => SurfaceEdgeArtifactValidator.Inspect(value).IsValid,
            "surface/edge score");

    public static SurfaceAndEdgeMatchScoreArtifact LoadScore(string path) =>
        Load<SurfaceAndEdgeMatchScoreArtifact>(
            path,
            value => SurfaceEdgeArtifactValidator.Inspect(value).IsValid,
            "surface/edge score");

    private static void Save<T>(
        string path,
        T artifact,
        Func<T, bool> isValid,
        string name)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(artifact);
        if (!isValid(artifact))
        {
            throw new InvalidDataException(
                $"{name} validation failed before save.");
        }

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

    private static T Load<T>(
        string path,
        Func<T, bool> isValid,
        string name)
        where T : class
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
            var artifact = JsonSerializer.Deserialize<T>(stream, JsonOptions)
                ?? throw new InvalidDataException($"{name} JSON is empty.");
            if (!isValid(artifact))
            {
                throw new InvalidDataException(
                    $"{name} validation failed after load.");
            }

            return artifact;
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

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for an identified multiple-match collection.
/// Viewer selection is presentation state and is never persisted here.
/// </summary>
public static class SurfaceMatchCollectionArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static void Save(
        string path,
        SurfaceMatchCollectionArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(artifact);
        RequireValid(artifact);
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? Environment.CurrentDirectory);
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

    public static SurfaceMatchCollectionArtifact Load(string path)
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
            var artifact =
                JsonSerializer.Deserialize<SurfaceMatchCollectionArtifact>(
                    stream,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "Surface match collection JSON is empty.");
            RequireValid(artifact);
            return artifact;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Surface match collection JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"Surface match collection JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }

    private static void RequireValid(
        SurfaceMatchCollectionArtifact artifact)
    {
        var validity = SurfaceMatchCollectionArtifactValidator.Inspect(
            artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface match collection validation failed: "
                + string.Join(" ", validity.Errors));
        }
    }
}

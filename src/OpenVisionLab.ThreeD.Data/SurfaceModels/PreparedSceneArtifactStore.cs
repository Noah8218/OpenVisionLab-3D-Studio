using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for one identified Prepared Scene artifact.
/// </summary>
public static class PreparedSceneArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static void Save(
        string path,
        PreparedSceneArtifact scene)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(scene);
        RequireValid(scene);

        var fullPath = Path.GetFullPath(path);
        var directory =
            Path.GetDirectoryName(fullPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(
                JsonSerializer.Serialize(scene, JsonOptions));
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

    public static PreparedSceneArtifact Load(string path)
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
            var scene =
                JsonSerializer.Deserialize<PreparedSceneArtifact>(
                    stream,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "Prepared Scene JSON is empty.");
            RequireValid(scene);
            return scene;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Prepared Scene JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"Prepared Scene JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }

    private static void RequireValid(
        PreparedSceneArtifact scene)
    {
        var validity = PreparedSceneArtifactValidator.Inspect(scene);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Prepared Scene validation failed: "
                + string.Join(" ", validity.Errors));
        }
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for one identified SurfaceModel artifact. Load and
/// save both reject unsupported, malformed, or content-mismatched artifacts.
/// </summary>
public static class SurfaceModelArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static void Save(
        string path,
        SurfaceModelArtifact model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(model);
        RequireValid(model);

        var fullPath = Path.GetFullPath(path);
        var directory =
            Path.GetDirectoryName(fullPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(
                JsonSerializer.Serialize(model, JsonOptions));
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

    public static SurfaceModelArtifact Load(string path)
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
            var model =
                JsonSerializer.Deserialize<SurfaceModelArtifact>(
                    stream,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "SurfaceModel JSON is empty.");
            RequireValid(model);
            return model;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"SurfaceModel JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"SurfaceModel JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }

    private static void RequireValid(SurfaceModelArtifact model)
    {
        var validity = SurfaceModelArtifactValidator.Inspect(model);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"SurfaceModel validation failed: "
                + string.Join(" ", validity.Errors));
        }
    }
}

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for one linked, decision-free surface match
/// execution artifact.
/// </summary>
public static class SurfaceMatchExecutionArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static void Save(
        string path,
        SurfaceMatchExecutionArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(artifact);
        RequireValid(artifact);

        var fullPath = Path.GetFullPath(path);
        var directory =
            Path.GetDirectoryName(fullPath)
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

    public static SurfaceMatchExecutionArtifact Load(string path)
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
                JsonSerializer.Deserialize<SurfaceMatchExecutionArtifact>(
                    stream,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "Surface match execution JSON is empty.");
            RequireValid(artifact);
            return artifact;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Surface match execution JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"Surface match execution JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }

    private static void RequireValid(
        SurfaceMatchExecutionArtifact artifact)
    {
        var validity =
            SurfaceMatchExecutionArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                $"Surface match execution validation failed: "
                + string.Join(" ", validity.Errors));
        }
    }
}

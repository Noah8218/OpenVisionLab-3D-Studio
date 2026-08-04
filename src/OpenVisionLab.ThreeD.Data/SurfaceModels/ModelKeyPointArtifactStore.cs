using System.Text;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for one identified model key-point artifact.
/// </summary>
public static class ModelKeyPointArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static void Save(string path, ModelKeyPointArtifact artifact)
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

    public static ModelKeyPointArtifact Load(string path)
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
            var artifact = JsonSerializer.Deserialize<ModelKeyPointArtifact>(
                    stream,
                    JsonOptions)
                ?? throw new InvalidDataException(
                    "Model key-point JSON is empty.");
            RequireValid(artifact);
            return artifact;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Model key-point JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"Model key-point JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }

    private static void RequireValid(ModelKeyPointArtifact artifact)
    {
        var validity = ModelKeyPointArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Model key-point validation failed: "
                + string.Join(" ", validity.Errors));
        }
    }
}

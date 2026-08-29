using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for one validated transformed-region artifact.
/// </summary>
public static class C3DTransformedRegionArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = true
    };

    public static void Save(
        string path,
        C3DTransformedRegionArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        RequireValid(artifact);

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

    public static C3DTransformedRegionArtifact Load(string path)
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
            var artifact = JsonSerializer.Deserialize<C3DTransformedRegionArtifact>(
                stream,
                JsonOptions)
                ?? throw new InvalidDataException(
                    "Transformed-region artifact JSON is empty.");
            RequireValid(artifact);
            return artifact;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Transformed-region artifact JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"Transformed-region artifact JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }

    private static void RequireValid(
        C3DTransformedRegionArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        var validity = C3DTransformedRegionArtifactValidator.Inspect(artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Transformed-region artifact validation failed: "
                + string.Join(" ", validity.Errors));
        }
    }
}

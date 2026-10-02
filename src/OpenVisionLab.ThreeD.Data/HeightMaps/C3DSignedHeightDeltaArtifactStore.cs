using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON sidecar persistence for a signed height-delta artifact.
/// The sidecar is intentionally separate from ordinary C3D because JSON
/// named floating-point literals preserve NaN while finite zero remains a
/// valid measured delta.
/// </summary>
public static class C3DSignedHeightDeltaArtifactStore
{
    public const string ContractVersion = C3DSignedHeightDeltaArtifact.CurrentSchemaVersion;
    public const string FileExtension = ".sdelta.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        WriteIndented = true
    };

    public static void Save(string path, C3DSignedHeightDeltaArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(artifact);
        RequireValid(artifact);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(JsonSerializer.Serialize(artifact, JsonOptions));
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

    public static C3DSignedHeightDeltaArtifact Load(string path)
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
            var artifact = JsonSerializer.Deserialize<C3DSignedHeightDeltaArtifact>(stream, JsonOptions)
                ?? throw new InvalidDataException("Signed height-delta sidecar is empty.");
            RequireValid(artifact);
            return artifact;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Signed height-delta sidecar JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"Signed height-delta sidecar JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }

    private static void RequireValid(C3DSignedHeightDeltaArtifact artifact)
    {
        if (!string.Equals(artifact.SchemaVersion, ContractVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Signed height-delta sidecar schema '{artifact.SchemaVersion}' is not supported.");
        }

        if (!artifact.TryValidate(out var message))
        {
            throw new InvalidDataException(
                "Signed height-delta sidecar validation failed: " + message);
        }
    }
}

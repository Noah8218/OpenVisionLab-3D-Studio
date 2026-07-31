using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Atomic JSON persistence for deterministic surface-match acceptance and
/// separate observational runtime evidence.
/// </summary>
public static class SurfaceMatchAssessmentArtifactStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static void Save(
        string path,
        SurfaceMatchAssessmentArtifact artifact)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(artifact);
        var validity =
            SurfaceMatchAssessmentArtifactValidator.Inspect(
                artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface match assessment validation failed: "
                + string.Join(" ", validity.Errors));
        }

        SaveAtomic(path, artifact);
    }

    public static SurfaceMatchAssessmentArtifact Load(string path)
    {
        var artifact =
            Load<SurfaceMatchAssessmentArtifact>(
                path,
                "surface match assessment");
        var validity =
            SurfaceMatchAssessmentArtifactValidator.Inspect(
                artifact);
        if (!validity.IsValid)
        {
            throw new InvalidDataException(
                "Surface match assessment validation failed: "
                + string.Join(" ", validity.Errors));
        }

        return artifact;
    }

    public static void SaveRuntime(
        string path,
        SurfaceMatchRuntimeReport report)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(report);
        if (!SurfaceMatchAssessmentArtifactValidator
                .InspectRuntime(report, out var evidence))
        {
            throw new InvalidDataException(
                $"Surface match runtime validation failed: {evidence}");
        }

        SaveAtomic(path, report);
    }

    public static SurfaceMatchRuntimeReport LoadRuntime(string path)
    {
        var report = Load<SurfaceMatchRuntimeReport>(
            path,
            "surface match runtime");
        if (!SurfaceMatchAssessmentArtifactValidator
                .InspectRuntime(report, out var evidence))
        {
            throw new InvalidDataException(
                $"Surface match runtime validation failed: {evidence}");
        }

        return report;
    }

    private static void SaveAtomic<T>(string path, T value)
    {
        var fullPath = Path.GetFullPath(path);
        var directory =
            Path.GetDirectoryName(fullPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var temporaryPath = $"{fullPath}.tmp.{Guid.NewGuid():N}";
        try
        {
            var bytes = new UTF8Encoding(false).GetBytes(
                JsonSerializer.Serialize(value, JsonOptions));
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

    private static T Load<T>(string path, string artifactName)
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
                ?? throw new InvalidDataException(
                    $"{artifactName} JSON is empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"{artifactName} JSON is malformed: {fullPath}",
                exception);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException(
                $"{artifactName} JSON uses an unsupported shape: {fullPath}",
                exception);
        }
    }
}

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

public sealed record ThicknessRepeatabilityStudyDocument(
    string? StudyType,
    string? SchemaVersion,
    string? StudyId,
    string? MeasurementDefinitionId,
    string? ReferenceRoiId,
    string? Unit,
    string? FrameId,
    ThicknessRepeatabilityAcceptance? Acceptance,
    ThicknessRepeatabilityStudyRunDocument[]? Runs);

public sealed record ThicknessRepeatabilityStudyRunDocument(
    string? RunId,
    string? SourceEntityId,
    string? SourcePath,
    long SourceByteLength,
    string? SourceSha256,
    DateTimeOffset CapturedAt,
    string? Unit,
    string? FrameId,
    double Thickness);

public sealed record ThicknessRepeatabilitySourceIdentity(
    string RunId,
    string SourceEntityId,
    string Name,
    string Path,
    long ByteLength,
    string Sha256);

public sealed record LoadedThicknessRepeatabilityStudy(
    string Path,
    ThicknessRepeatabilityInput Input,
    IReadOnlyList<ThicknessRepeatabilitySourceIdentity> Sources);

public static class ThicknessRepeatabilityStudyLoader
{
    public const string SupportedStudyType = "thickness-repeatability";
    public const string SupportedSchemaVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16
    };

    public static LoadedThicknessRepeatabilityStudy Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var document = JsonSerializer.Deserialize<ThicknessRepeatabilityStudyDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException($"Thickness repeatability study is empty: {fullPath}");
        ValidateHeader(document);

        var studyDirectory = Path.GetDirectoryName(fullPath)!;
        var sourcePaths = new HashSet<string>(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var sourceHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runs = new ThicknessRepeatabilityRun[document.Runs!.Length];
        var sources = new ThicknessRepeatabilitySourceIdentity[document.Runs.Length];

        for (var index = 0; index < document.Runs.Length; index++)
        {
            var run = document.Runs[index]
                ?? throw new InvalidDataException($"Thickness repeatability run {index + 1} is null.");
            ValidateSource(run, index);
            var runId = run.RunId!;
            var sourceEntityId = run.SourceEntityId!;
            var sourceDocumentPath = run.SourcePath!;

            var sourcePath = Path.IsPathRooted(sourceDocumentPath)
                ? Path.GetFullPath(sourceDocumentPath)
                : Path.GetFullPath(Path.Combine(studyDirectory, sourceDocumentPath));
            if (!sourcePaths.Add(sourcePath))
            {
                throw new InvalidDataException(
                    $"A source path cannot count as more than one acquisition: {sourceDocumentPath}");
            }

            using var sourceStream = new FileStream(
                sourcePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            if (sourceStream.Length != run.SourceByteLength)
            {
                throw new InvalidDataException(
                    $"Source byte length does not match for run {runId}.");
            }

            var actualSha256 = Convert.ToHexString(SHA256.HashData(sourceStream));
            if (!actualSha256.Equals(run.SourceSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Source SHA-256 does not match for run {runId}.");
            }

            if (!sourceHashes.Add(actualSha256))
            {
                throw new InvalidDataException(
                    $"Byte-identical sources cannot count as separate acquisitions: {runId}.");
            }

            runs[index] = new ThicknessRepeatabilityRun(
                runId,
                sourceEntityId,
                run.CapturedAt,
                run.Unit!,
                run.FrameId!,
                run.Thickness);
            sources[index] = new ThicknessRepeatabilitySourceIdentity(
                runId,
                sourceEntityId,
                Path.GetFileName(sourcePath),
                sourcePath,
                sourceStream.Length,
                actualSha256);
        }

        var runSnapshot = Array.AsReadOnly(runs);
        return new LoadedThicknessRepeatabilityStudy(
            fullPath,
            new ThicknessRepeatabilityInput(
                document.StudyId!,
                document.MeasurementDefinitionId!,
                document.ReferenceRoiId!,
                document.Unit!,
                document.FrameId!,
                runSnapshot,
                document.Acceptance),
            Array.AsReadOnly(sources));
    }

    private static void ValidateHeader(ThicknessRepeatabilityStudyDocument document)
    {
        if (!string.Equals(document.StudyType, SupportedStudyType, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported thickness repeatability study type: {document.StudyType}");
        }

        if (!string.Equals(document.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported thickness repeatability schema version: {document.SchemaVersion}");
        }

        if (document.Runs is null)
        {
            throw new InvalidDataException("Thickness repeatability study runs are required.");
        }
    }

    private static void ValidateSource(ThicknessRepeatabilityStudyRunDocument run, int index)
    {
        if (string.IsNullOrWhiteSpace(run.RunId)
            || string.IsNullOrWhiteSpace(run.SourceEntityId)
            || string.IsNullOrWhiteSpace(run.SourcePath)
            || run.SourceByteLength <= 0
            || string.IsNullOrWhiteSpace(run.SourceSha256)
            || run.SourceSha256.Length != 64
            || run.SourceSha256.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new InvalidDataException(
                $"Run {index + 1} requires run/source IDs, a source path, positive byte length, and SHA-256.");
        }
    }
}

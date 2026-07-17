using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

public sealed record AlignedPointRepeatabilityStudyDocument(
    string? StudyType,
    string? SchemaVersion,
    string? StudyId,
    string? MeasurementDefinitionId,
    string? ReferenceRoiId,
    string? Unit,
    string? FrameId,
    string? AlignmentReferenceId,
    string? CorrespondenceDefinitionId,
    AlignedPointRepeatabilityAcceptance? Acceptance,
    AlignedPointRepeatabilityReferencePoint[]? ReferencePoints,
    AlignedPointRepeatabilityStudyRunDocument[]? Runs);

public sealed record AlignedPointRepeatabilityStudyRunDocument(
    string? RunId,
    string? SourceEntityId,
    string? SourcePath,
    long SourceByteLength,
    string? SourceSha256,
    DateTimeOffset CapturedAt,
    string? MappingPath,
    long MappingByteLength,
    string? MappingSha256);

public sealed record AlignedPointRepeatabilityMappingDocument(
    string? MappingType,
    string? SchemaVersion,
    string? RunId,
    string? SourceEntityId,
    long SourceByteLength,
    string? SourceSha256,
    string? Unit,
    string? FrameId,
    string? AlignmentReferenceId,
    string? CorrespondenceDefinitionId,
    string? AlignmentMethodId,
    string? AlignmentEvidenceId,
    AlignedPointRepeatabilityObservation[]? Observations);

public sealed record AlignedPointRepeatabilitySourceIdentity(
    string RunId,
    string SourceEntityId,
    string Name,
    string Path,
    long ByteLength,
    string Sha256);

public sealed record AlignedPointRepeatabilityMappingIdentity(
    string RunId,
    string SourceEntityId,
    string Name,
    string Path,
    long ByteLength,
    string Sha256,
    string AlignmentMethodId,
    string AlignmentEvidenceId);

public sealed record AlignedPointRepeatabilityStudyIdentity(
    string Name,
    string Path,
    long ByteLength,
    string Sha256);

public sealed record LoadedAlignedPointRepeatabilityStudy(
    AlignedPointRepeatabilityStudyIdentity Study,
    AlignedPointRepeatabilityInput Input,
    IReadOnlyList<AlignedPointRepeatabilitySourceIdentity> Sources,
    IReadOnlyList<AlignedPointRepeatabilityMappingIdentity> Mappings)
{
    public string Path => Study.Path;
}

public static class AlignedPointRepeatabilityStudyLoader
{
    public const string SupportedStudyType = "aligned-point-repeatability";
    public const string SupportedSchemaVersion = "1.0";
    public const string SupportedMappingType = "aligned-point-repeatability-mapping";
    public const string SupportedMappingSchemaVersion = "1.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 32
    };

    public static LoadedAlignedPointRepeatabilityStudy Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var studyDocument = ReadDocument<AlignedPointRepeatabilityStudyDocument>(
            fullPath,
            "Aligned point repeatability study");
        var document = studyDocument.Document;
        ValidateStudyHeader(document);

        var referencePoints = document.ReferencePoints!.ToArray();
        var referenceIds = ValidateReferencePoints(referencePoints);
        var studyDirectory = Path.GetDirectoryName(fullPath)!;
        var sourcePaths = new HashSet<string>(PathComparer);
        var sourceHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var mappingPaths = new HashSet<string>(PathComparer);
        var mappingHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runIds = new HashSet<string>(StringComparer.Ordinal);
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var runs = new AlignedPointRepeatabilityRun[document.Runs!.Length];
        var sources = new AlignedPointRepeatabilitySourceIdentity[document.Runs.Length];
        var mappings = new AlignedPointRepeatabilityMappingIdentity[document.Runs.Length];

        for (var index = 0; index < document.Runs.Length; index++)
        {
            var run = document.Runs[index]
                ?? throw new InvalidDataException($"Aligned point repeatability run {index + 1} is null.");
            ValidateRunDocument(run, index);

            var runId = run.RunId!;
            var sourceEntityId = run.SourceEntityId!;
            if (!runIds.Add(runId))
            {
                throw new InvalidDataException($"Run ID is duplicated: {runId}.");
            }

            if (!sourceIds.Add(sourceEntityId))
            {
                throw new InvalidDataException($"Source entity ID is duplicated: {sourceEntityId}.");
            }

            var sourcePath = ResolvePath(studyDirectory, run.SourcePath!);
            if (!sourcePaths.Add(sourcePath))
            {
                throw new InvalidDataException(
                    $"A source path cannot count as more than one acquisition: {run.SourcePath}");
            }

            var sourceFile = VerifyFileIdentity(
                sourcePath,
                run.SourceByteLength,
                run.SourceSha256!,
                $"Source for run {runId}");
            if (!sourceHashes.Add(sourceFile.Sha256))
            {
                throw new InvalidDataException(
                    $"Byte-identical source cannot count as a separate acquisition: {runId}.");
            }

            var mappingPath = ResolvePath(studyDirectory, run.MappingPath!);
            if (!mappingPaths.Add(mappingPath))
            {
                throw new InvalidDataException(
                    $"A correspondence mapping path cannot count as more than one acquisition: {run.MappingPath}");
            }

            var mappingDocument = ReadDocument<AlignedPointRepeatabilityMappingDocument>(
                mappingPath,
                $"Correspondence mapping for run {runId}");
            var mappingFile = mappingDocument.File;
            VerifyExpectedFileIdentity(
                mappingFile,
                run.MappingByteLength,
                run.MappingSha256!,
                $"Correspondence mapping for run {runId}");
            if (!mappingHashes.Add(mappingFile.Sha256))
            {
                throw new InvalidDataException(
                    $"Byte-identical correspondence mapping cannot count as a separate acquisition: {runId}.");
            }

            var mapping = mappingDocument.Document;
            ValidateMapping(
                document,
                run,
                sourceFile,
                mapping,
                referenceIds);

            var observations = Array.AsReadOnly(mapping.Observations!.ToArray());
            runs[index] = new AlignedPointRepeatabilityRun(
                runId,
                sourceEntityId,
                sourceFile.ByteLength,
                sourceFile.Sha256,
                run.CapturedAt,
                mapping.Unit!,
                mapping.FrameId!,
                mapping.AlignmentReferenceId!,
                mapping.AlignmentMethodId!,
                mapping.AlignmentEvidenceId!,
                observations);
            sources[index] = new AlignedPointRepeatabilitySourceIdentity(
                runId,
                sourceEntityId,
                sourceFile.Name,
                sourceFile.Path,
                sourceFile.ByteLength,
                sourceFile.Sha256);
            mappings[index] = new AlignedPointRepeatabilityMappingIdentity(
                runId,
                sourceEntityId,
                mappingFile.Name,
                mappingFile.Path,
                mappingFile.ByteLength,
                mappingFile.Sha256,
                mapping.AlignmentMethodId!,
                mapping.AlignmentEvidenceId!);
        }

        var referenceSnapshot = Array.AsReadOnly(referencePoints);
        var runSnapshot = Array.AsReadOnly(runs);
        return new LoadedAlignedPointRepeatabilityStudy(
            new AlignedPointRepeatabilityStudyIdentity(
                studyDocument.File.Name,
                studyDocument.File.Path,
                studyDocument.File.ByteLength,
                studyDocument.File.Sha256),
            new AlignedPointRepeatabilityInput(
                document.StudyId!,
                document.MeasurementDefinitionId!,
                document.ReferenceRoiId!,
                document.Unit!,
                document.FrameId!,
                document.AlignmentReferenceId!,
                document.CorrespondenceDefinitionId!,
                referenceSnapshot,
                runSnapshot,
                document.Acceptance),
            Array.AsReadOnly(sources),
            Array.AsReadOnly(mappings));
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static void ValidateStudyHeader(AlignedPointRepeatabilityStudyDocument document)
    {
        if (!string.Equals(document.StudyType, SupportedStudyType, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported aligned point repeatability study type: {document.StudyType}");
        }

        if (!string.Equals(document.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported aligned point repeatability study schema version: {document.SchemaVersion}");
        }

        if (string.IsNullOrWhiteSpace(document.Unit)
            || string.IsNullOrWhiteSpace(document.FrameId)
            || string.IsNullOrWhiteSpace(document.AlignmentReferenceId)
            || string.IsNullOrWhiteSpace(document.CorrespondenceDefinitionId))
        {
            throw new InvalidDataException(
                "Aligned point repeatability study requires unit, frame, alignment reference, and correspondence definition IDs.");
        }

        if (document.ReferencePoints is null)
        {
            throw new InvalidDataException("Aligned point repeatability study reference points are required.");
        }

        if (document.Runs is null)
        {
            throw new InvalidDataException("Aligned point repeatability study runs are required.");
        }
    }

    private static HashSet<string> ValidateReferencePoints(
        IReadOnlyList<AlignedPointRepeatabilityReferencePoint> referencePoints)
    {
        var correspondenceIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < referencePoints.Count; index++)
        {
            var point = referencePoints[index];
            if (string.IsNullOrWhiteSpace(point.CorrespondenceId))
            {
                throw new InvalidDataException(
                    $"Reference correspondence point {index + 1} has no correspondence ID.");
            }

            if (!correspondenceIds.Add(point.CorrespondenceId))
            {
                throw new InvalidDataException(
                    $"Reference correspondence ID is duplicated: {point.CorrespondenceId}.");
            }

            if (!double.IsFinite(point.AlignedX)
                || !double.IsFinite(point.AlignedY)
                || !double.IsFinite(point.AlignedZ))
            {
                throw new InvalidDataException(
                    $"Reference correspondence point {point.CorrespondenceId} has a non-finite aligned coordinate.");
            }
        }

        return correspondenceIds;
    }

    private static void ValidateRunDocument(
        AlignedPointRepeatabilityStudyRunDocument run,
        int index)
    {
        if (string.IsNullOrWhiteSpace(run.RunId)
            || string.IsNullOrWhiteSpace(run.SourceEntityId)
            || string.IsNullOrWhiteSpace(run.SourcePath)
            || run.SourceByteLength <= 0
            || !IsSha256(run.SourceSha256)
            || run.CapturedAt == default
            || string.IsNullOrWhiteSpace(run.MappingPath)
            || run.MappingByteLength <= 0
            || !IsSha256(run.MappingSha256))
        {
            throw new InvalidDataException(
                $"Run {index + 1} requires run/source IDs, source and mapping paths, positive byte lengths, SHA-256 values, and a capture timestamp.");
        }
    }

    private static void ValidateMapping(
        AlignedPointRepeatabilityStudyDocument study,
        AlignedPointRepeatabilityStudyRunDocument run,
        VerifiedFile sourceFile,
        AlignedPointRepeatabilityMappingDocument mapping,
        IReadOnlySet<string> referenceIds)
    {
        if (!string.Equals(mapping.MappingType, SupportedMappingType, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported aligned point repeatability mapping type: {mapping.MappingType}");
        }

        if (!string.Equals(mapping.SchemaVersion, SupportedMappingSchemaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Unsupported aligned point repeatability mapping schema version: {mapping.SchemaVersion}");
        }

        if (!string.Equals(mapping.RunId, run.RunId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Mapping run ID does not match study run {run.RunId}.");
        }

        if (!string.Equals(mapping.SourceEntityId, run.SourceEntityId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Mapping source entity ID does not match study run {run.RunId}.");
        }

        if (mapping.SourceByteLength != sourceFile.ByteLength)
        {
            throw new InvalidDataException($"Mapping source byte length does not match study run {run.RunId}.");
        }

        if (!string.Equals(mapping.SourceSha256, sourceFile.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Mapping source SHA-256 does not match study run {run.RunId}.");
        }

        if (!string.Equals(mapping.Unit, study.Unit, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Mapping unit does not match study unit for run {run.RunId}.");
        }

        if (!string.Equals(mapping.FrameId, study.FrameId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Mapping frame does not match study frame for run {run.RunId}.");
        }

        if (!string.Equals(mapping.AlignmentReferenceId, study.AlignmentReferenceId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Mapping alignment reference does not match study alignment reference for run {run.RunId}.");
        }

        if (!string.Equals(mapping.CorrespondenceDefinitionId, study.CorrespondenceDefinitionId, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Mapping correspondence definition does not match study correspondence definition for run {run.RunId}.");
        }

        if (string.IsNullOrWhiteSpace(mapping.AlignmentMethodId)
            || string.IsNullOrWhiteSpace(mapping.AlignmentEvidenceId))
        {
            throw new InvalidDataException($"Mapping alignment method and evidence IDs are required for run {run.RunId}.");
        }

        if (mapping.Observations is null)
        {
            throw new InvalidDataException($"Mapping observations are required for run {run.RunId}.");
        }

        var observationIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var observation in mapping.Observations)
        {
            if (string.IsNullOrWhiteSpace(observation.CorrespondenceId))
            {
                throw new InvalidDataException(
                    $"Mapping observation for run {run.RunId} has no correspondence ID.");
            }

            if (!observationIds.Add(observation.CorrespondenceId))
            {
                throw new InvalidDataException(
                    $"Mapping observation duplicates correspondence ID {observation.CorrespondenceId} for run {run.RunId}.");
            }

            if (!double.IsFinite(observation.Value))
            {
                throw new InvalidDataException(
                    $"Mapping observation {observation.CorrespondenceId} has a non-finite value for run {run.RunId}.");
            }
        }

        if (!observationIds.SetEquals(referenceIds))
        {
            throw new InvalidDataException(
                $"Mapping correspondence coverage does not exactly match study reference points for run {run.RunId}.");
        }
    }

    private static LoadedDocument<TDocument> ReadDocument<TDocument>(string path, string label)
        where TDocument : class
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var byteLength = stream.Length;
        var bytes = new byte[checked((int)byteLength)];
        stream.ReadExactly(bytes);
        using var documentStream = new MemoryStream(bytes, writable: false);
        var document = JsonSerializer.Deserialize<TDocument>(documentStream, JsonOptions)
            ?? throw new InvalidDataException($"{label} is empty: {path}");
        return new LoadedDocument<TDocument>(
            new VerifiedFile(
                Path.GetFileName(path),
                path,
                byteLength,
                Convert.ToHexString(SHA256.HashData(bytes))),
            document);
    }

    private static string ResolvePath(string studyDirectory, string documentPath) =>
        Path.IsPathRooted(documentPath)
            ? Path.GetFullPath(documentPath)
            : Path.GetFullPath(Path.Combine(studyDirectory, documentPath));

    private static VerifiedFile VerifyFileIdentity(
        string path,
        long expectedByteLength,
        string expectedSha256,
        string label)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var file = new VerifiedFile(
            Path.GetFileName(path),
            path,
            stream.Length,
            Convert.ToHexString(SHA256.HashData(stream)));
        VerifyExpectedFileIdentity(file, expectedByteLength, expectedSha256, label);
        return file;
    }

    private static void VerifyExpectedFileIdentity(
        VerifiedFile file,
        long expectedByteLength,
        string expectedSha256,
        string label)
    {
        if (file.ByteLength != expectedByteLength)
        {
            throw new InvalidDataException($"{label} byte length does not match.");
        }

        if (!file.Sha256.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"{label} SHA-256 does not match.");
        }
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private sealed record VerifiedFile(
        string Name,
        string Path,
        long ByteLength,
        string Sha256);

    private sealed record LoadedDocument<TDocument>(VerifiedFile File, TDocument Document)
        where TDocument : class;
}

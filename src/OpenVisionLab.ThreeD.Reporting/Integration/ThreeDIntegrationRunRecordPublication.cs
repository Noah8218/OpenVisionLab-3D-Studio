using System.Security.Cryptography;
using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Reporting.RunRecords;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

public sealed record ThreeDIntegrationEvidenceArtifact(
    string Role,
    string ArtifactId,
    string SourcePath,
    string RelativePath);

/// <summary>
/// Owns the 3D-side projection of an existing Run Record into an Integration
/// transaction. Exchange adapters keep protocol ordering; this owner keeps the
/// copied Run Record and evidence artifacts alive only after the Result message
/// has been committed.
/// </summary>
public sealed class ThreeDIntegrationRunRecordPublication : IDisposable
{
    private readonly IReadOnlyList<string> evidencePaths;
    private bool committed;
    private bool disposed;

    private ThreeDIntegrationRunRecordPublication(
        InspectionRunRecord runRecord,
        IntegrationArtifactReference artifact,
        string artifactPath,
        IntegrationInspectionOutcome outcome,
        IntegrationInspectionDisposition disposition,
        IReadOnlyList<IntegrationMetric> metrics,
        IReadOnlyList<IntegrationArtifactReference> evidence,
        IReadOnlyList<string> evidencePaths)
    {
        RunRecord = runRecord;
        Artifact = artifact;
        ArtifactPath = artifactPath;
        Outcome = outcome;
        Disposition = disposition;
        Metrics = metrics;
        Evidence = evidence;
        this.evidencePaths = evidencePaths;
    }

    public InspectionRunRecord RunRecord { get; }

    public IntegrationArtifactReference Artifact { get; }

    public string ArtifactPath { get; }

    public string RunId => RunRecord.RunId;

    public IntegrationInspectionOutcome Outcome { get; }

    public IntegrationInspectionDisposition Disposition { get; }

    public IReadOnlyList<IntegrationMetric> Metrics { get; }

    public IReadOnlyList<IntegrationArtifactReference> Evidence { get; }

    public static ThreeDIntegrationRunRecordPublication Create(
        string existingRunRecordPath,
        string transactionDirectory,
        IntegrationInspectionContextV2? correlationContext = null,
        IReadOnlyList<ThreeDIntegrationEvidenceArtifact>? additionalEvidence = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(existingRunRecordPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionDirectory);
        EnsureNoReparsePointsForExternalFile(existingRunRecordPath, "Run Record");

        var runRecord = InspectionRunRecordJson.Read(existingRunRecordPath);
        if (string.IsNullOrWhiteSpace(runRecord.RunId))
        {
            throw new InvalidDataException("Run Record identity is required.");
        }

        if (correlationContext is not null)
        {
            EnsureRunRecordCorrelation(correlationContext, runRecord);
        }

        var (outcome, disposition) = MapStatus(runRecord.Status);
        var artifactsDirectory = Path.Combine(
            Path.GetFullPath(transactionDirectory),
            IntegrationTransactionLayout.ArtifactsDirectoryName);
        EnsureNoReparsePoints(
            transactionDirectory,
            $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/3d-run-record.json");
        Directory.CreateDirectory(artifactsDirectory);
        var artifactPath = Path.Combine(artifactsDirectory, "3d-run-record.json");
        var temporaryArtifactPath = $"{artifactPath}.tmp.{Guid.NewGuid():N}";
        var artifactPublished = false;
        var publishedEvidencePaths = new List<string>();
        var temporaryEvidencePaths = new List<string>();
        try
        {
            File.Copy(
                Path.GetFullPath(existingRunRecordPath),
                temporaryArtifactPath,
                overwrite: false);
            File.Move(temporaryArtifactPath, artifactPath);
            artifactPublished = true;
            var artifact = CreateArtifactReference(
                IntegrationArtifactRoles.RunRecord,
                runRecord.RunId,
                artifactPath,
                $"{IntegrationTransactionLayout.ArtifactsDirectoryName}/3d-run-record.json");
            var evidence = CopyAdditionalEvidence(
                transactionDirectory,
                additionalEvidence,
                publishedEvidencePaths,
                temporaryEvidencePaths);
            return new ThreeDIntegrationRunRecordPublication(
                runRecord,
                artifact,
                artifactPath,
                outcome,
                disposition,
                CreateMetrics(runRecord),
                evidence,
                publishedEvidencePaths.ToArray());
        }
        catch
        {
            if (artifactPublished)
            {
                TryDeleteFile(artifactPath);
            }
            foreach (var evidencePath in publishedEvidencePaths)
            {
                TryDeleteFile(evidencePath);
            }

            throw;
        }
        finally
        {
            TryDeleteFile(temporaryArtifactPath);
            foreach (var temporaryEvidencePath in temporaryEvidencePaths)
            {
                TryDeleteFile(temporaryEvidencePath);
            }
        }
    }

    /// <summary>
    /// Marks the Result write as successful. Dispose then preserves the copied
    /// artifact for the consumer; an uncommitted publication removes it.
    /// </summary>
    public void Commit()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        committed = true;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!committed)
        {
            TryDeleteFile(ArtifactPath);
            foreach (var evidencePath in evidencePaths)
            {
                TryDeleteFile(evidencePath);
            }
        }
    }

    private static IReadOnlyList<IntegrationArtifactReference> CopyAdditionalEvidence(
        string transactionDirectory,
        IReadOnlyList<ThreeDIntegrationEvidenceArtifact>? additionalEvidence,
        List<string> publishedEvidencePaths,
        List<string> temporaryEvidencePaths)
    {
        if (additionalEvidence is null || additionalEvidence.Count == 0)
        {
            return [];
        }

        var references = new List<IntegrationArtifactReference>(additionalEvidence.Count);
        foreach (var evidence in additionalEvidence)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(evidence.Role);
            ArgumentException.ThrowIfNullOrWhiteSpace(evidence.ArtifactId);
            ArgumentException.ThrowIfNullOrWhiteSpace(evidence.SourcePath);
            ArgumentException.ThrowIfNullOrWhiteSpace(evidence.RelativePath);
            EnsureNoReparsePointsForExternalFile(evidence.SourcePath, "Result evidence");

            var targetPath = ResolveEvidenceTargetPath(
                transactionDirectory,
                evidence.RelativePath);
            if (File.Exists(targetPath))
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.InvalidState,
                    $"The Result evidence artifact already exists: {evidence.RelativePath}");
            }

            var temporaryPath = Path.Combine(
                Path.GetDirectoryName(targetPath)!,
                $".{Path.GetFileName(targetPath)}.{Guid.NewGuid():N}.tmp");
            temporaryEvidencePaths.Add(temporaryPath);
            File.Copy(Path.GetFullPath(evidence.SourcePath), temporaryPath, overwrite: false);
            File.Move(temporaryPath, targetPath);
            publishedEvidencePaths.Add(targetPath);
            references.Add(CreateArtifactReference(
                evidence.Role,
                evidence.ArtifactId,
                targetPath,
                evidence.RelativePath));
        }

        return references;
    }

    private static string ResolveEvidenceTargetPath(
        string transactionDirectory,
        string relativePath)
    {
        var transactionRoot = Path.GetFullPath(transactionDirectory);
        var artifactsRoot = Path.GetFullPath(Path.Combine(
            transactionRoot,
            IntegrationTransactionLayout.ArtifactsDirectoryName));
        var targetPath = Path.GetFullPath(Path.Combine(
            transactionRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var artifactsPrefix = artifactsRoot.TrimEnd(Path.DirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;
        if (!targetPath.StartsWith(artifactsPrefix, StringComparison.OrdinalIgnoreCase)
            || !Directory.Exists(Path.GetDirectoryName(targetPath)))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.UnsafeArtifactPath,
                "Additional Result evidence must target an existing directory inside the transaction artifacts directory.");
        }
        EnsureNoReparsePoints(
            transactionRoot,
            relativePath.Replace('\\', '/'));

        return targetPath;
    }

    private static (IntegrationInspectionOutcome Outcome, IntegrationInspectionDisposition Disposition) MapStatus(
        ResultStatus status) => status switch
        {
            ResultStatus.Pass => (
                IntegrationInspectionOutcome.Pass,
                IntegrationInspectionDisposition.Pass),
            ResultStatus.Fail => (
                IntegrationInspectionOutcome.Ng,
                IntegrationInspectionDisposition.Fail),
            ResultStatus.Warning => (
                IntegrationInspectionOutcome.Indeterminate,
                IntegrationInspectionDisposition.Indeterminate),
            _ => throw new InvalidDataException(
                $"Run Record status '{status}' is not a completed inspection state.")
        };

    private static IReadOnlyList<IntegrationMetric> CreateMetrics(
        InspectionRunRecord runRecord)
    {
        var metrics = new List<IntegrationMetric>();
        if (double.IsFinite(runRecord.ElapsedMilliseconds))
        {
            metrics.Add(new(
                "elapsedMilliseconds",
                runRecord.ElapsedMilliseconds,
                "ms"));
        }

        var runMetrics = runRecord.Metrics ?? [];
        for (var index = 0; index < runMetrics.Count; index++)
        {
            var metric = runMetrics[index];
            if (double.IsFinite(metric.Value))
            {
                metrics.Add(new(
                    $"metric.{index}.{metric.Name}",
                    metric.Value,
                    string.IsNullOrWhiteSpace(metric.Unit) ? "unitless" : metric.Unit));
            }
        }

        return metrics;
    }

    private static void EnsureRunRecordCorrelation(
        IntegrationInspectionContextV2 context,
        InspectionRunRecord runRecord)
    {
        if (runRecord.Source is null
            || !string.Equals(
                runRecord.Source.Sha256,
                context.InputSha256,
                StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                runRecord.Source.Unit,
                context.Unit,
                StringComparison.Ordinal))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record source SHA-256 does not match the Handoff input.");
        }

        if (runRecord.Recipe is null
            || !string.Equals(
                runRecord.Recipe.Sha256,
                context.RecipeSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record recipe SHA-256 does not match the Handoff recipe.");
        }

        if (!string.Equals(runRecord.Step?.Id, context.StepId, StringComparison.Ordinal)
            && runRecord.Steps?.Any(step => string.Equals(
                step.Id,
                context.StepId,
                StringComparison.Ordinal)) != true)
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.CorrelationMismatch,
                "Run Record step identity does not match the Handoff step.");
        }
    }

    private static IntegrationArtifactReference CreateArtifactReference(
        string role,
        string artifactId,
        string fullPath,
        string relativePath)
    {
        using var stream = File.OpenRead(fullPath);
        return new(
            role,
            artifactId,
            relativePath,
            stream.Length,
            Convert.ToHexString(SHA256.HashData(stream)));
    }

    private static void EnsureNoReparsePoints(
        string transactionDirectory,
        string relativePath)
    {
        var current = Path.GetFullPath(transactionDirectory);
        var root = new DirectoryInfo(current);
        if (root.Exists && root.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.UnsafeArtifactPath,
                "The transaction directory cannot be a symbolic link or reparse point.");
        }

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            current = Path.Combine(current, segments[index]);
            FileSystemInfo entry = index == segments.Length - 1
                ? new FileInfo(current)
                : new DirectoryInfo(current);
            if (entry.Exists && entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                throw new IntegrationContractException(
                    IntegrationErrorCode.UnsafeArtifactPath,
                    "Artifact paths cannot traverse symbolic links or reparse points.");
            }
        }
    }

    private static void EnsureNoReparsePointsForExternalFile(
        string path,
        string artifactLabel)
    {
        var file = new FileInfo(Path.GetFullPath(path));
        if (file.Exists && file.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.UnsafeArtifactPath,
                $"A {artifactLabel} source cannot be a symbolic link or reparse point.");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Preserve the original Run Record, contract, or I/O failure.
        }
    }
}

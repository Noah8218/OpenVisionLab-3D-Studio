using OpenVisionLab.Integration.Contracts;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Reporting.Integration;

/// <summary>
/// Creates the display-only image that the 3D Height Image viewer already
/// renders from <see cref="C3DHeightImageFrame"/>. The C3D artifact remains
/// the inspection input; this artifact only makes the viewer pixels portable
/// across the process boundary to Machine Studio.
/// </summary>
public static class ThreeDHeightImagePreviewArtifact
{
    public const string ResultEvidenceArtifactId = "three-d-height-image-preview";
    public const string ResultEvidenceRelativePath = "artifacts/three-d-height-image-preview.png";

    public static ThreeDIntegrationEvidenceArtifact CreateTemporary(
        IntegrationHandoffV2 handoff,
        string transactionDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var transactionRoot = Path.GetFullPath(transactionDirectory);
        var sourceArtifact = handoff.Context.Artifacts.Single(artifact =>
            string.Equals(
                artifact.Role,
                IntegrationArtifactRoles.InspectionSource,
                StringComparison.Ordinal));
        var sourcePath = ResolvePathInsideTransaction(
            transactionRoot,
            sourceArtifact.RelativePath);
        if (!File.Exists(sourcePath))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.ArtifactMissing,
                "The 3D HeightMap source artifact is unavailable for the image preview.");
        }

        var snapshot = C3DHeightFieldSnapshot.LoadIdentified(
            sourcePath,
            sourceArtifact.ArtifactId,
            handoff.Context.Unit,
            handoff.Context.FrameId,
            cancellationToken);
        if (snapshot.ByteLength != sourceArtifact.ByteLength
            || !string.Equals(
                snapshot.ContentSha256,
                sourceArtifact.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.ArtifactHashMismatch,
                "The 3D HeightMap source identity does not match the Handoff artifact.");
        }

        var frame = C3DHeightImageFrame.Create(snapshot, cancellationToken);
        var previewPath = Path.Combine(
            transactionRoot,
            $".3d-height-image-preview-{Guid.NewGuid():N}.png");
        try
        {
            C3DHeightImagePngWriter.Write(frame, previewPath, cancellationToken);
            return new(
                IntegrationArtifactRoles.ResultEvidence,
                ResultEvidenceArtifactId,
                previewPath,
                ResultEvidenceRelativePath);
        }
        catch
        {
            TryDelete(previewPath);
            throw;
        }
    }

    private static string ResolvePathInsideTransaction(
        string transactionRoot,
        string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(
            transactionRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var transactionPrefix = transactionRoot.TrimEnd(Path.DirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(transactionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new IntegrationContractException(
                IntegrationErrorCode.UnsafeArtifactPath,
                "The 3D HeightMap source artifact must remain inside the transaction directory.");
        }

        return fullPath;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

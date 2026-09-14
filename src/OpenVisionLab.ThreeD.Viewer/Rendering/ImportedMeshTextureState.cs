using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Owns the managed identity and observations for one imported-mesh texture.
/// The View still performs every context-bound OpenGL call; this type keeps
/// source matching, replacement admission, and lifetime counters together.
/// </summary>
internal sealed class ImportedMeshTextureState
{
    public ImportedMesh? Source { get; private set; }

    public uint TextureId { get; private set; }

    public bool ReleasePending { get; private set; }

    public int UploadCount { get; private set; }

    public int ReleaseCount { get; private set; }

    public int ReleaseFailureCount { get; private set; }

    public bool UploadFailed { get; private set; }

    public string UploadSummary { get; private set; } = "texture none";

    public bool MatchesSource(ImportedMesh mesh) => ReferenceEquals(Source, mesh);

    public void RecordAllocation(uint textureId) => TextureId = textureId;

    public void ResetForSourceChange()
    {
        ReleasePending |= TextureId != 0;
        Source = null;
        UploadFailed = false;
        UploadSummary = "texture none";
    }

    public void RecordUpload(ImportedMesh mesh, uint textureId, string summary)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        Source = mesh;
        TextureId = textureId;
        UploadCount++;
        UploadFailed = false;
        UploadSummary = summary;
    }

    public void RecordUploadFailure(string summary)
    {
        UploadFailed = true;
        UploadSummary = summary;
    }

    public void RecordRelease(bool succeeded)
    {
        if (succeeded)
        {
            ReleaseCount++;
        }
        else
        {
            ReleaseFailureCount++;
        }
    }

    public void ClearAfterRelease()
    {
        TextureId = 0;
        Source = null;
        ReleasePending = false;
    }

    public void ResetForOpenGLInitialization()
    {
        TextureId = 0;
        Source = null;
        ReleasePending = false;
        UploadFailed = false;
        UploadSummary = "texture none";
    }

    public void ClearManagedReferencesAfterDispose()
    {
        TextureId = 0;
        Source = null;
        ReleasePending = false;
    }
}

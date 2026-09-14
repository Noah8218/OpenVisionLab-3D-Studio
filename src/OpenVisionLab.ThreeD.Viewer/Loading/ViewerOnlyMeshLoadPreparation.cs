using System.IO;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Performs CPU-only GLB/STL decoding for Viewer-only imports. The control
/// retains source-operation admission, status projection, and scene apply.
/// </summary>
internal static class ViewerOnlyMeshLoadPreparation
{
    public static async Task<ViewerOnlyMeshLoadResult> LoadAsync(
        string path,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var extension = Path.GetExtension(fullPath).ToLowerInvariant();
        if (extension is not ".glb" and not ".stl")
        {
            throw new NotSupportedException($"The '{extension}' format is not available in 3D Import.");
        }

        var mesh = await Task.Run(
            () => extension == ".glb"
                ? GlbMesh.Load(fullPath, cancellationToken, progress)
                : StlMesh.Load(fullPath, cancellationToken, progress),
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(90.0);
        return new ViewerOnlyMeshLoadResult(mesh, extension == ".glb" ? "GLB" : "STL");
    }
}

internal readonly record struct ViewerOnlyMeshLoadResult(ImportedMesh Mesh, string Format);

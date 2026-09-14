using System.IO;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns WPF/OpenGL-neutral format dispatch for Viewer-only source imports.
/// The control retains operation admission, status projection, and scene or
/// GPU application of the decoded result.
/// </summary>
internal sealed class ViewerOnlySourceLoadCoordinator
{
    private readonly LazPointCloudLoadCoordinator lazPointCloudLoadCoordinator;

    public ViewerOnlySourceLoadCoordinator(LazPointCloudLoadCoordinator lazPointCloudLoadCoordinator)
    {
        this.lazPointCloudLoadCoordinator = lazPointCloudLoadCoordinator
            ?? throw new ArgumentNullException(nameof(lazPointCloudLoadCoordinator));
    }

    public static ViewerOnlySourceLoadRequest Prepare(string path, int maxSampledPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        return new ViewerOnlySourceLoadRequest(
            fullPath,
            Path.GetFileName(fullPath),
            Path.GetExtension(fullPath).ToLowerInvariant(),
            Math.Max(2, maxSampledPoints),
            File.Exists(fullPath));
    }

    public async Task<ViewerOnlySourceLoadResult?> LoadAsync(
        ViewerOnlySourceLoadRequest request,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null,
        Func<bool>? isCurrent = null,
        Func<string, int, CancellationToken, IProgress<double>?, Func<bool>?, Task<LazPointCloudLoadResult?>>? lazLoadAsync = null)
    {
        request.ThrowIfFileMissing();
        switch (request.Extension)
        {
            case ".glb":
            case ".stl":
            {
                var preparedMesh = await ViewerOnlyMeshLoadPreparation.LoadAsync(
                    request.FullPath,
                    cancellationToken,
                    progress);
                cancellationToken.ThrowIfCancellationRequested();
                return new ViewerOnlySourceLoadResult(
                    preparedMesh.Format,
                    preparedMesh.Mesh,
                    null,
                    0.0,
                    Reused: false);
            }
            case ".las":
            case ".laz":
            {
                var lazRequest = ViewerLazPointCloudLoadPreparation.Prepare(
                    request.FullPath,
                    request.MaxSampledPoints);
                if (!lazRequest.FileExists)
                {
                    return null;
                }

                var loadResult = lazLoadAsync is null
                    ? await ViewerLazPointCloudLoadPreparation.LoadAsync(
                        lazPointCloudLoadCoordinator,
                        lazRequest,
                        cancellationToken,
                        progress,
                        isCurrent)
                    : await lazLoadAsync(
                        request.FullPath,
                        request.MaxSampledPoints,
                        cancellationToken,
                        progress,
                        isCurrent);
                if (loadResult is not { PointCloud: { } pointCloud } completedLoad)
                {
                    return null;
                }

                return new ViewerOnlySourceLoadResult(
                    request.Extension == ".las" ? "LAS" : "LAZ",
                    null,
                    pointCloud,
                    completedLoad.LoadMilliseconds,
                    completedLoad.Reused);
            }
            default:
                throw new NotSupportedException(
                    $"The '{request.Extension}' format is not available in 3D Import.");
        }
    }
}

internal readonly record struct ViewerOnlySourceLoadRequest(
    string FullPath,
    string SourceName,
    string Extension,
    int MaxSampledPoints,
    bool FileExists)
{
    public void ThrowIfFileMissing()
    {
        if (!FileExists)
        {
            throw new FileNotFoundException("Viewer-only source was not found.", FullPath);
        }
    }
}

internal readonly record struct ViewerOnlySourceLoadResult(
    string Format,
    ImportedMesh? Mesh,
    LazPointCloud? PointCloud,
    double LoadMilliseconds,
    bool Reused);

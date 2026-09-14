using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Creates the WPF/OpenGL-neutral request used by the Viewer LAZ/LAS load
/// path. Status projection remains with the control; decoding, cache access,
/// and operation lifetime remain with <see cref="LazPointCloudLoadCoordinator" />.
/// </summary>
internal static class ViewerLazPointCloudLoadPreparation
{
    public static ViewerLazPointCloudLoadRequest Prepare(string path, int maxSampledPoints)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        return new ViewerLazPointCloudLoadRequest(
            fullPath,
            Path.GetFileName(fullPath),
            Math.Max(2, maxSampledPoints),
            File.Exists(fullPath));
    }

    public static LazPointCloudLoadResult Load(
        LazPointCloudLoadCoordinator coordinator,
        ViewerLazPointCloudLoadRequest request)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        request.ThrowIfFileMissing();
        return coordinator.Load(request);
    }

    public static async Task<LazPointCloudLoadResult?> LoadAsync(
        LazPointCloudLoadCoordinator coordinator,
        ViewerLazPointCloudLoadRequest request,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null,
        Func<bool>? isCurrent = null)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        request.ThrowIfFileMissing();
        return await coordinator.LoadAsync(
            request,
            cancellationToken,
            progress,
            isCurrent);
    }
}

internal readonly record struct ViewerLazPointCloudLoadRequest(
    string FullPath,
    string SourceName,
    int MaxSampledPoints,
    bool FileExists)
{
    public void ThrowIfFileMissing()
    {
        if (!FileExists)
        {
            throw new FileNotFoundException("LAZ/LAS source was not found.", FullPath);
        }
    }
}

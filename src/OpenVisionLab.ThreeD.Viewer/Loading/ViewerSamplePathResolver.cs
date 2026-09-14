using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Default sample resolver for repository and published Viewer assets. The
/// process-root policy is isolated here so external hosts can inject their own
/// packaged-asset resolver without changing source-loading workflows.
/// </summary>
internal sealed class ViewerSamplePathResolver : IViewerSamplePathResolver
{
    private readonly Func<IEnumerable<string>> rootsProvider;

    public ViewerSamplePathResolver()
        : this(() => [Environment.CurrentDirectory, AppContext.BaseDirectory])
    {
    }

    internal ViewerSamplePathResolver(Func<IEnumerable<string>> rootsProvider)
    {
        this.rootsProvider = rootsProvider ?? throw new ArgumentNullException(nameof(rootsProvider));
    }

    public string? Resolve(string relativePath) =>
        ViewerSamplePathLocator.Find(relativePath, rootsProvider());
}

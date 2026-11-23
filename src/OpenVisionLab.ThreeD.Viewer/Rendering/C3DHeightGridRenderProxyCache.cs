using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

/// <summary>
/// Keeps the render proxy for one immutable C3D display snapshot. The cache is
/// deliberately independent from WPF, OpenGL, and Viewer presentation state.
/// </summary>
internal sealed class C3DHeightGridRenderProxyCache
{
    private C3DHeightGrid? source;
    private C3DHeightGridRenderProxy? proxy;

    public C3DHeightGrid? Source => source;

    public C3DHeightGridRenderProxy? Current => proxy;

    public bool HasValue => source is not null || proxy is not null;

    public C3DHeightGridRenderProxy GetOrCreate(C3DHeightGrid sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (!ReferenceEquals(source, sample) || proxy is null)
        {
            // 같은 snapshot의 위상은 뷰어끼리 공유해 탭을 오갈 때 다시 만들지 않는다.
            source = sample;
            proxy = OpenVisionLab.ThreeD.Viewer.Loading.C3DSourceLoadCache.GetOrCreateRenderProxy(sample);
        }

        return proxy;
    }

    public void Set(C3DHeightGrid sample, C3DHeightGridRenderProxy renderProxy)
    {
        ArgumentNullException.ThrowIfNull(sample);
        ArgumentNullException.ThrowIfNull(renderProxy);
        source = sample;
        proxy = renderProxy;
    }

    public void Clear()
    {
        source = null;
        proxy = null;
    }
}

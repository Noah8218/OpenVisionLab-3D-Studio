namespace OpenVisionLab.ThreeD.Viewer;

/// <summary>
/// 탭 전환처럼 한 번의 동작이 여러 뷰어의 속성을 연달아 바꿀 때, 속성 변경마다 장면을 다시 그리지 않고
/// 동작이 끝난 뒤 뷰어마다 한 번만 그리게 한다. 뷰어 한 번 렌더가 약 40ms라 같은 동작에서 4번 그리면
/// 그것만으로 0.2초가 걸린다. UI 스레드에서만 호출한다.
/// </summary>
public static class ViewerRenderBatch
{
    private static int depth;
    private static readonly List<OpenVisionThreeDViewerControl> Pending = [];

    public static IDisposable Begin()
    {
        depth++;
        return new Scope();
    }

    internal static bool TryDefer(OpenVisionThreeDViewerControl viewer)
    {
        if (depth == 0) return false;
        if (!Pending.Contains(viewer)) Pending.Add(viewer);
        return true;
    }

    private static void End()
    {
        if (depth == 0 || --depth != 0 || Pending.Count == 0) return;
        var viewers = Pending.ToArray();
        Pending.Clear();
        foreach (var viewer in viewers) viewer.RenderDeferredByBatch();
    }

    private readonly struct Scope : IDisposable
    {
        public void Dispose() => End();
    }
}

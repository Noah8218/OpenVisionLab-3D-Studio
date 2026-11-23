using System.IO;
using System.Numerics;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// 최근에 읽은 C3D 높이 격자를 파일 신원(경로·크기·수정 시각·렌더 점 상한)으로 다시 쓴다.
/// 탭을 오가면 같은 파일이 뷰어에 계속 다시 표시되는데, 그때마다 11MB를 읽어 통계·해시·표시 점을
/// 다시 만들면 UI 스레드가 수백 ms 멈춘다. <see cref="C3DHeightGrid"/>는 불변 snapshot이라
/// 여러 뷰어가 같은 인스턴스를 공유해도 값이 바뀌지 않는다.
/// 같은 경로의 파일이 바뀌면 크기나 수정 시각이 달라져 새로 읽는다.
/// </summary>
internal static class C3DSourceLoadCache
{
    private const int Capacity = 4;

    // 이보다 큰 파일은 메모리를 오래 잡지 않도록 캐시하지 않는다.
    private const long MaximumCachedFileBytes = 96L * 1024 * 1024;

    private static readonly object Gate = new();
    private static readonly List<(Key Key, C3DHeightGrid Grid)> Entries = [];
    private static readonly ConditionalWeakTable<C3DHeightGrid, C3DHeightGridRenderProxy> Proxies = new();
    private static readonly ConditionalWeakTable<C3DHeightGridRenderProxy, Vector3[]> Positions = new();

    private readonly record struct Key(string Path, long Length, long LastWriteTicksUtc, int MaximumRenderedPoints);

    public static C3DHeightGrid GetOrLoad(
        string fullPath,
        int maximumRenderedPoints,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null)
    {
        var info = new FileInfo(fullPath);
        if (!info.Exists || info.Length > MaximumCachedFileBytes)
        {
            // 없는 파일은 읽기 코드가 원래 예외를 던지게 둔다.
            return C3DHeightGrid.Load(fullPath, maximumRenderedPoints, cancellationToken, progress);
        }

        var key = new Key(fullPath.ToUpperInvariant(), info.Length, info.LastWriteTimeUtc.Ticks, maximumRenderedPoints);
        lock (Gate)
        {
            var index = Entries.FindIndex(entry => entry.Key == key);
            if (index >= 0)
            {
                var hit = Entries[index];
                Entries.RemoveAt(index);
                Entries.Insert(0, hit);
                progress?.Report(100.0);
                return hit.Grid;
            }
        }

        var grid = C3DHeightGrid.Load(fullPath, maximumRenderedPoints, cancellationToken, progress);
        lock (Gate)
        {
            Entries.RemoveAll(entry => entry.Key == key);
            Entries.Insert(0, (key, grid));
            while (Entries.Count > Capacity) Entries.RemoveAt(Entries.Count - 1);
        }

        return grid;
    }

    /// <summary>같은 격자 snapshot에서 만든 표시용 위상(삼각형·모서리)을 뷰어끼리 공유한다.</summary>
    public static C3DHeightGridRenderProxy GetOrCreateRenderProxy(
        C3DHeightGrid grid,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grid);
        if (Proxies.TryGetValue(grid, out var existing)) return existing;
        var created = C3DHeightGridRenderProxy.Create(grid, cancellationToken);
        return Proxies.GetValue(grid, _ => created);
    }

    /// <summary>항등 변환·높이 배율 1일 때의 점 위치. 읽기 전용으로만 쓴다.</summary>
    public static Vector3[] GetOrCreateIdentityPositions(C3DHeightGridRenderProxy proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        if (Positions.TryGetValue(proxy, out var existing)) return existing;
        var created = proxy.Points.Select(point => point.Position).ToArray();
        return Positions.GetValue(proxy, _ => created);
    }

    /// <summary>검증이 파일을 바꿔 쓴 뒤 오래된 격자를 다시 쓰지 않도록 비운다.</summary>
    public static void Clear()
    {
        lock (Gate) Entries.Clear();
    }
}

using System.Diagnostics;
using System.IO;
using System.Numerics;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer.Loading;

/// <summary>
/// Owns the WPF-neutral CPU work required before a C3D source can be applied
/// to the Viewer. OpenGL upload, ViewModel state, and UI-thread application
/// remain in the Viewer control.
/// </summary>
internal static class C3DSourceLoadPreparation
{
    public static string GetExistingPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("C3D source was not found.", fullPath);
        }

        return fullPath;
    }

    public static async Task<C3DSourceLoadPreparationResult> LoadAsync(
        string path,
        int maxRenderedPoints,
        CancellationToken cancellationToken,
        IProgress<double>? progress = null)
    {
        var fullPath = GetExistingPath(path);
        var gridProgress = progress is null
            ? null
            : new ForwardingProgress(value => progress.Report(value * 0.82));
        var prepared = await Task.Run(
            () =>
            {
                var workerStart = Stopwatch.GetTimestamp();
                var grid = C3DHeightGrid.Load(
                    fullPath,
                    maxRenderedPoints,
                    cancellationToken,
                    gridProgress);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(84.0);
                var topologyStart = Stopwatch.GetTimestamp();
                var renderProxy = C3DHeightGridRenderProxy.Create(grid, cancellationToken);
                var topologyMilliseconds = Stopwatch.GetElapsedTime(topologyStart).TotalMilliseconds;
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(96.0);
                var positionsStart = Stopwatch.GetTimestamp();
                var positions = renderProxy.Points
                    .Select(point => point.Position)
                    .ToArray();
                var positionsMilliseconds = Stopwatch.GetElapsedTime(positionsStart).TotalMilliseconds;
                return new C3DSourceLoadPreparationResult(
                    fullPath,
                    grid,
                    renderProxy,
                    positions,
                    topologyMilliseconds,
                    positionsMilliseconds,
                    Stopwatch.GetElapsedTime(workerStart).TotalMilliseconds);
            },
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return prepared;
    }

    private sealed class ForwardingProgress(Action<double> callback) : IProgress<double>
    {
        public void Report(double value) => callback(value);
    }
}

internal sealed record C3DSourceLoadPreparationResult(
    string FullPath,
    C3DHeightGrid Grid,
    C3DHeightGridRenderProxy RenderProxy,
    Vector3[] Positions,
    double TopologyMilliseconds,
    double PositionsMilliseconds,
    double WorkerMilliseconds);

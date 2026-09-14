using System.IO;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Coordinates the file barrier and process snapshot used after a Viewer
/// closes, without owning any WPF or Viewer lifecycle state.
/// </summary>
internal sealed class ViewerConsumerGpuObservationBarrierCoordinator
{
    private readonly string readyPath;
    private readonly int processId;
    private readonly Func<long> readPrivateMemoryBytes;
    private readonly Func<NativeResourceSnapshot> readNativeResources;

    public ViewerConsumerGpuObservationBarrierCoordinator(
        string readyPath,
        int processId,
        Func<long> readPrivateMemoryBytes,
        Func<NativeResourceSnapshot> readNativeResources)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(readyPath);
        ArgumentNullException.ThrowIfNull(readPrivateMemoryBytes);
        ArgumentNullException.ThrowIfNull(readNativeResources);
        this.readyPath = Path.GetFullPath(readyPath);
        this.processId = processId;
        this.readPrivateMemoryBytes = readPrivateMemoryBytes;
        this.readNativeResources = readNativeResources;
    }

    public async Task<ViewerConsumerGpuObservationBarrierResult> WaitAsync()
    {
        var continuePath = readyPath + ".continue";
        if (File.Exists(continuePath))
        {
            return new ViewerConsumerGpuObservationBarrierResult(
                false,
                "ready=False|continued=False|reason=stale-continue-file");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(readyPath)!);
        var native = readNativeResources();
        File.WriteAllText(
            readyPath,
            $"pid={processId}|privateBytes={readPrivateMemoryBytes()}|native={native}");
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (!File.Exists(continuePath) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        var continued = File.Exists(continuePath);
        return new ViewerConsumerGpuObservationBarrierResult(
            continued,
            $"ready=True|continued={continued}|pid={processId}|native={native}");
    }
}

internal sealed record ViewerConsumerGpuObservationBarrierResult(
    bool Passed,
    string Details);

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Captures process-level observations for the independent consumer harness.
/// It does not own Viewer, Window, or OpenGL resources.
/// </summary>
internal static class ViewerConsumerProcessResourceObserver
{
    private const uint GdiObjectCount = 0;
    private const uint UserObjectCount = 1;

    [DllImport("user32.dll")]
    private static extern uint GetGuiResources(IntPtr processHandle, uint flags);

    public static int ProcessId => Environment.ProcessId;

    public static long ReadPrivateMemoryBytes()
    {
        using var current = Process.GetCurrentProcess();
        current.Refresh();
        return current.PrivateMemorySize64;
    }

    public static long ReadManagedMemoryBytes() => GC.GetTotalMemory(forceFullCollection: false);

    public static NativeResourceSnapshot ReadNativeResources()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            var processHandle = process.Handle;
            return new NativeResourceSnapshot(
                process.HandleCount,
                GetGuiResources(processHandle, GdiObjectCount),
                GetGuiResources(processHandle, UserObjectCount));
        }
        catch
        {
            return new NativeResourceSnapshot(-1, -1, -1);
        }
    }

    public static void CollectForObservation()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

internal sealed record NativeResourceSnapshot(
    long HandleCount,
    long GdiObjects,
    long UserObjects)
{
    public NativeResourceSnapshot Min(NativeResourceSnapshot other) =>
        new(
            Math.Min(HandleCount, other.HandleCount),
            Math.Min(GdiObjects, other.GdiObjects),
            Math.Min(UserObjects, other.UserObjects));

    public NativeResourceSnapshot Max(NativeResourceSnapshot other) =>
        new(
            Math.Max(HandleCount, other.HandleCount),
            Math.Max(GdiObjects, other.GdiObjects),
            Math.Max(UserObjects, other.UserObjects));

    public string DeltaFrom(NativeResourceSnapshot baseline) =>
        $"handles={HandleCount - baseline.HandleCount},gdi={GdiObjects - baseline.GdiObjects},user={UserObjects - baseline.UserObjects}";
}

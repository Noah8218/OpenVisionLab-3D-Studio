using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the Shell's narrow desktop interop boundary used by automated Smoke
/// evidence. High-level scenario order and WPF visual ownership remain with
/// <see cref="MainWindow"/>; this type keeps user32/Shcore calls, monitor
/// selection, and window evidence out of the Window code-behind.
/// </summary>
internal static class ShellWindowNativeInterop
{
    private const uint MonitorDefaultToNearest = 0x00000002;
    private const uint LeftButtonDown = 0x0002;
    private const uint LeftButtonUp = 0x0004;
    private const uint KeyUp = 0x0002;
    private const uint WmMouseMove = 0x0200;
    private const int WmGetMinMaxInfo = 0x0024;
    private const int EffectiveDpi = 0;

    public static IntPtr ConstrainMaximizeToWorkArea(
        IntPtr windowHandle,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message != WmGetMinMaxInfo || lParam == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        var monitor = MonitorFromWindow(
            windowHandle,
            MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
        if (monitor == IntPtr.Zero
            || !GetMonitorInfo(monitor, ref monitorInfo))
        {
            return IntPtr.Zero;
        }

        var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        minMaxInfo.MaxPosition.X =
            monitorInfo.WorkArea.Left - monitorInfo.MonitorArea.Left;
        minMaxInfo.MaxPosition.Y =
            monitorInfo.WorkArea.Top - monitorInfo.MonitorArea.Top;
        minMaxInfo.MaxSize.X =
            monitorInfo.WorkArea.Right - monitorInfo.WorkArea.Left;
        minMaxInfo.MaxSize.Y =
            monitorInfo.WorkArea.Bottom - monitorInfo.WorkArea.Top;
        Marshal.StructureToPtr(minMaxInfo, lParam, false);
        return IntPtr.Zero;
    }

    public static bool TryGetLeftmostWorkAreaOrigin(
        out double left,
        out double top)
    {
        var leftmostMonitor = MonitorFromPoint(
            new NativePoint
            {
                X = (int)SystemParameters.VirtualScreenLeft,
                Y = (int)SystemParameters.VirtualScreenTop
            },
            MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
        if (leftmostMonitor != IntPtr.Zero
            && GetMonitorInfo(leftmostMonitor, ref monitorInfo))
        {
            var dpiX = 96u;
            var dpiY = 96u;
            GetDpiForMonitor(
                leftmostMonitor,
                EffectiveDpi,
                out dpiX,
                out dpiY);
            left = monitorInfo.WorkArea.Left * 96.0 / Math.Max(96u, dpiX);
            top = monitorInfo.WorkArea.Top * 96.0 / Math.Max(96u, dpiY);
            return true;
        }

        left = SystemParameters.VirtualScreenLeft;
        top = SystemParameters.VirtualScreenTop;
        return false;
    }

    public static void AppendWindowMonitorEvidence(
        Window window,
        string? reportPath)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };
        if (handle == IntPtr.Zero
            || monitor == IntPtr.Zero
            || !GetMonitorInfo(monitor, ref monitorInfo)
            || !GetWindowRect(handle, out var windowRect))
        {
            return;
        }

        var intersects = windowRect.Left < monitorInfo.MonitorArea.Right
            && windowRect.Right > monitorInfo.MonitorArea.Left
            && windowRect.Top < monitorInfo.MonitorArea.Bottom
            && windowRect.Bottom > monitorInfo.MonitorArea.Top;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(window);
        File.AppendAllLines(
            Path.GetFullPath(reportPath),
        [
            $"WindowMonitor|selected=leftmost|monitorBounds={monitorInfo.MonitorArea.Left},{monitorInfo.MonitorArea.Top},{monitorInfo.MonitorArea.Right},{monitorInfo.MonitorArea.Bottom}|workingArea={monitorInfo.WorkArea.Left},{monitorInfo.WorkArea.Top},{monitorInfo.WorkArea.Right},{monitorInfo.WorkArea.Bottom}|windowRect={windowRect.Left},{windowRect.Top},{windowRect.Right},{windowRect.Bottom}|intersects={intersects}",
            $"WindowDpi|scaleX={dpi.DpiScaleX:F2}|scaleY={dpi.DpiScaleY:F2}|pixelsPerInchX={dpi.PixelsPerInchX:F0}|pixelsPerInchY={dpi.PixelsPerInchY:F0}"
        ]);
    }

    public static bool SetCursorPos(int x, int y) =>
        SetCursorPosNative(x, y);

    public static bool GetCursorPos(out NativePoint point) =>
        GetCursorPosNative(out point);

    public static bool GetWindowRect(
        IntPtr windowHandle,
        out NativeRectangle rectangle) =>
        GetWindowRectNative(windowHandle, out rectangle);

    public static bool SetForegroundWindow(IntPtr windowHandle) =>
        SetForegroundWindowNative(windowHandle);

    public static bool PostClientMouseMove(
        IntPtr windowHandle,
        Point devicePoint)
    {
        var x = Math.Clamp((int)Math.Round(devicePoint.X), 0, short.MaxValue);
        var y = Math.Clamp((int)Math.Round(devicePoint.Y), 0, short.MaxValue);
        var packed = (IntPtr)((y << 16) | (x & 0xFFFF));
        return PostMessage(
            windowHandle,
            WmMouseMove,
            UIntPtr.Zero,
            packed);
    }

    public static void SendMouseEvent(
        uint flags,
        uint deltaX,
        uint deltaY,
        uint data,
        UIntPtr extraInfo) =>
        SendMouseEventNative(flags, deltaX, deltaY, data, extraInfo);

    public static void SendKeyboardEvent(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo) =>
        SendKeyboardEventNative(virtualKey, scanCode, flags, extraInfo);

    public static void SendLeftButtonDown() =>
        SendMouseEvent(LeftButtonDown, 0, 0, 0, UIntPtr.Zero);

    public static void SendLeftButtonUp() =>
        SendMouseEvent(LeftButtonUp, 0, 0, 0, UIntPtr.Zero);

    public static void SendKeyUp(byte virtualKey) =>
        SendKeyboardEvent(virtualKey, 0, KeyUp, UIntPtr.Zero);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(
        IntPtr windowHandle,
        uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(
        NativePoint point,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitor,
        ref MonitorInfo monitorInfo);

    [DllImport("Shcore.dll")]
    private static extern int GetDpiForMonitor(
        IntPtr monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY);

    [DllImport("user32.dll", EntryPoint = "SetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPosNative(int x, int y);

    [DllImport("user32.dll", EntryPoint = "GetCursorPos")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPosNative(out NativePoint point);

    [DllImport("user32.dll", EntryPoint = "GetWindowRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRectNative(
        IntPtr windowHandle,
        out NativeRectangle rectangle);

    [DllImport("user32.dll", EntryPoint = "SetForegroundWindow")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindowNative(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr windowHandle,
        uint message,
        UIntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void SendMouseEventNative(
        uint flags,
        uint deltaX,
        uint deltaY,
        uint data,
        UIntPtr extraInfo);

    [DllImport("user32.dll", EntryPoint = "keybd_event")]
    private static extern void SendKeyboardEventNative(
        byte virtualKey,
        byte scanCode,
        uint flags,
        UIntPtr extraInfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeRectangle
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRectangle MonitorArea;
        public NativeRectangle WorkArea;
        public uint Flags;
    }
}

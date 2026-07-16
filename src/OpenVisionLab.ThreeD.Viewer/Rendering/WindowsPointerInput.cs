using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;

namespace OpenVisionLab.ThreeD.Viewer.Rendering;

internal static class WindowsPointerInput
{
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventWheel = 0x0800;

    public static bool TryGetPosition(out Point position)
    {
        if (GetCursorPos(out var nativePoint))
        {
            position = new Point(nativePoint.X, nativePoint.Y);
            return true;
        }

        position = default;
        return false;
    }

    public static void MoveTo(Point screenPoint)
    {
        var x = checked((int)Math.Round(screenPoint.X));
        var y = checked((int)Math.Round(screenPoint.Y));
        if (!SetCursorPos(x, y))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not move the Windows pointer.");
        }
    }

    public static void LeftDown() => MouseEvent(MouseEventLeftDown, 0, 0, 0, UIntPtr.Zero);

    public static void LeftUp() => MouseEvent(MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);

    public static void RightDown() => MouseEvent(MouseEventRightDown, 0, 0, 0, UIntPtr.Zero);

    public static void RightUp() => MouseEvent(MouseEventRightUp, 0, 0, 0, UIntPtr.Zero);

    public static void MiddleDown() => MouseEvent(MouseEventMiddleDown, 0, 0, 0, UIntPtr.Zero);

    public static void MiddleUp() => MouseEvent(MouseEventMiddleUp, 0, 0, 0, UIntPtr.Zero);

    public static void Wheel(int delta) =>
        MouseEvent(MouseEventWheel, 0, 0, unchecked((uint)delta), UIntPtr.Zero);

    public static void BringWindowToInputFront(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero)
        {
            // Smoke-only: share the foreground queue long enough to activate this host.
            var currentThread = GetCurrentThreadId();
            var foreground = GetForegroundWindow();
            var foregroundThread = foreground == IntPtr.Zero
                ? 0
                : GetWindowThreadProcessId(foreground, out _);
            var attached = foregroundThread != 0
                && foregroundThread != currentThread
                && AttachThreadInput(currentThread, foregroundThread, true);

            try
            {
                SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpShowWindow);
                BringWindowToTop(handle);
                SetForegroundWindow(handle);
                SetFocus(handle);
            }
            finally
            {
                if (attached)
                {
                    AttachThreadInput(currentThread, foregroundThread, false);
                }
            }
        }
    }

    public static bool IsScreenPointOverWindow(Window window, Point screenPoint, out string diagnostic)
    {
        ArgumentNullException.ThrowIfNull(window);

        var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            diagnostic = "Viewer host window handle is unavailable.";
            return false;
        }

        var point = new NativePoint
        {
            X = checked((int)Math.Round(screenPoint.X)),
            Y = checked((int)Math.Round(screenPoint.Y))
        };
        var targetHandle = WindowFromPoint(point);
        var targetRoot = targetHandle == IntPtr.Zero
            ? IntPtr.Zero
            : GetAncestor(targetHandle, GaRoot);
        var hostRoot = GetAncestor(handle, GaRoot);
        if (hostRoot == IntPtr.Zero)
        {
            hostRoot = handle;
        }

        var matches = targetRoot == hostRoot;
        var cursor = GetCursorPos(out var cursorPoint)
            ? $"({cursorPoint.X},{cursorPoint.Y})"
            : "unavailable";
        var hostBounds = GetWindowRect(handle, out var hostRect)
            ? $"({hostRect.Left},{hostRect.Top})..({hostRect.Right},{hostRect.Bottom})"
            : "unavailable";
        diagnostic = $"requested=({point.X},{point.Y})|cursor={cursor}|hostBounds={hostBounds}|visible={IsWindowVisible(handle)}|enabled={IsWindowEnabled(handle)}|target=0x{targetHandle.ToInt64():X}|root=0x{targetRoot.ToInt64():X}|host=0x{handle.ToInt64():X}|hostRoot=0x{hostRoot.ToInt64():X}|matches={matches}";
        return matches;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll", EntryPoint = "mouse_event")]
    private static extern void MouseEvent(
        uint flags,
        uint dx,
        uint dy,
        uint data,
        UIntPtr extraInfo);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attachThreadId, uint attachToThreadId, bool attach);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr SetFocus(IntPtr hWnd);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowEnabled(IntPtr hWnd);

    private const uint GaRoot = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}

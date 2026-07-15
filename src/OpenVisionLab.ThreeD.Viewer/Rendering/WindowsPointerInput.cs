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

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}

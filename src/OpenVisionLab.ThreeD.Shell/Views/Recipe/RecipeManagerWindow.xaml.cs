using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace OpenVisionLab.ThreeD.Shell.Views.Recipe;

public partial class RecipeManagerWindow : Window
{
    private const uint MonitorDefaultToNearest = 2;
    private bool allowClose;
    private readonly double configuredMaxWidth;
    private readonly double configuredMaxHeight;

    public RecipeManagerWindow()
    {
        InitializeComponent();
        configuredMaxWidth = MaxWidth;
        configuredMaxHeight = MaxHeight;
        Closing += OnClosing;
    }

    protected override void OnSourceInitialized(EventArgs args)
    {
        base.OnSourceInitialized(args);
        UpdateOwnerWindowBounds();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        UpdateOwnerWindowBounds();
    }

    protected override void OnActivated(EventArgs args)
    {
        base.OnActivated(args);
        UpdateOwnerWindowBounds();
    }

    private void UpdateOwnerWindowBounds()
    {
        if (Owner is not { ActualWidth: > 0, ActualHeight: > 0 } owner)
        {
            return;
        }

        var maxWidth = configuredMaxWidth;
        var maxHeight = configuredMaxHeight;
        if (TryGetCurrentMonitorWorkArea(out var workAreaWidth, out var workAreaHeight))
        {
            var dpi = GetDpiForWindow(new WindowInteropHelper(this).Handle);
            var scale = dpi > 0 ? dpi / 96d : 1d;
            maxWidth = Math.Min(maxWidth, workAreaWidth / scale - 36);
            maxHeight = Math.Min(maxHeight, workAreaHeight / scale - 36);
        }
        else
        {
            maxWidth = Math.Min(maxWidth, owner.ActualWidth - 36);
            maxHeight = Math.Min(maxHeight, owner.ActualHeight - 36);
        }

        MaxWidth = Math.Max(MinWidth, maxWidth);
        MaxHeight = Math.Max(MinHeight, maxHeight);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
    }

    private bool TryGetCurrentMonitorWorkArea(out int width, out int height)
    {
        var monitor = MonitorFromWindow(new WindowInteropHelper(this).Handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            width = 0;
            height = 0;
            return false;
        }

        width = info.Work.Right - info.Work.Left;
        height = info.Work.Bottom - info.Work.Top;
        return width > 0 && height > 0;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public void CloseForOwner()
    {
        allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (!allowClose && Owner?.IsVisible == true)
        {
            args.Cancel = true;
            Hide();
        }
    }
}

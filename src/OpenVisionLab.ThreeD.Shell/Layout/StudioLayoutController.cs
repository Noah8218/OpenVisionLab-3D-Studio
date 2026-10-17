using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using AvalonDock.Controls;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Layout;

/// <summary>
/// Owns the presentation-only Studio layout lifecycle. It never owns or
/// mutates recipe, source, ROI, Preview, Publish, Run, or result state.
/// </summary>
internal sealed class StudioLayoutController : IDisposable
{
    private const uint MonitorDefaultToNearest = 2;
    private readonly Window window;
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly OpenVisionDockWorkspaceView advancedWorkspace;
    private readonly ShellMainWindowViewModel viewModel;
    private readonly string? smokeReportPath;
    private StudioLayoutProfileStore? store;
    private StudioLayoutProfile? pendingProfile;
    private StudioLayoutLoadStatus? loadStatus;
    private bool canAutoSave;
    private bool disposed;
    private readonly AvalonDock.DockingManager workbenchDockingManager;
    private readonly Dictionary<LayoutFloatingWindowControl, DispatcherOperation?> initialValidationFloats = new();
    private readonly Dictionary<LayoutFloatingWindowControl, (HwndSource Source, HwndSourceHook Hook)> initialFloatingSources = new();
    private static readonly DependencyPropertyDescriptor FloatingDragState = DependencyPropertyDescriptor.FromProperty(LayoutFloatingWindowControl.IsDraggingProperty, typeof(LayoutFloatingWindowControl))!;

    public StudioLayoutController(
        Window window,
        ToolRecipeWorkbenchView workbenchView,
        OpenVisionDockWorkspaceView advancedWorkspace,
        ShellMainWindowViewModel viewModel,
        bool isAutomatedRun,
        string? explicitProfilePath,
        string? smokeReportPath)
    {
        this.window = window;
        this.workbenchView = workbenchView;
        this.advancedWorkspace = advancedWorkspace;
        this.viewModel = viewModel;
        this.smokeReportPath = smokeReportPath;

        if (!isAutomatedRun || !string.IsNullOrWhiteSpace(explicitProfilePath))
        {
            Configure(explicitProfilePath);
        }

        workbenchDockingManager = (AvalonDock.DockingManager)workbenchView.DockWorkspace.FindName("workspaceDockingManager");
        workbenchDockingManager.LayoutFloatingWindowControlCreated += OnFloatingWindowCreated;
        window.Loaded += OnWindowLoaded;
        window.StateChanged += OnWindowStateChanged;
        window.LocationChanged += OnWindowLocationChanged;
    }

    public void Save()
    {
        if (store is null || !canAutoSave)
        {
            return;
        }

        try
        {
            var bounds = window.WindowState == WindowState.Normal
                ? new Rect(window.Left, window.Top, window.Width, window.Height)
                : window.RestoreBounds;
            var placement = bounds.IsEmpty
                ? null
                : new StudioWindowPlacement(
                    bounds.Left,
                    bounds.Top,
                    bounds.Width,
                    bounds.Height,
                    window.WindowState == WindowState.Maximized);
            store.Save(new StudioLayoutProfile(
                StudioLayoutProfile.CurrentSchemaVersion,
                placement,
                workbenchView.CaptureDockPresentationState(),
                advancedWorkspace.CapturePresentationState())
            {
                PaneDisplays = workbenchView.CaptureViewerDisplayProfiles(),
                HeightDisplay = (workbenchView.ViewerContent as IOpenVisionThreeDViewerHost)?.HostState.Presentation.HeightDisplayOptions,
                HeightImageDisplay = viewModel.Workbench.HeightImageViewer.HeightDisplayOptions,
                HeightDisplayModes = (workbenchView.ViewerContent as IOpenVisionThreeDViewerHost)?.CaptureHeightDisplayModes(),
                HeightImageDisplayModes = viewModel.Workbench.HeightImageViewer.CaptureHeightDisplayModes()
            });
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Info,
                $"Studio layout saved | path={store.Path} | presentationOnly=true | inspectionRun=false");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Warning,
                $"Studio layout save failed | path={store.Path} | {exception}");
        }
    }

    public void Reset()
    {
        workbenchView.ResetDockPresentationState();
        advancedWorkspace.ResetPresentationState();
        (workbenchView.ViewerContent as IOpenVisionThreeDViewerHost)?.RestoreHeightDisplayModes(new Dictionary<string, string>());
        viewModel.Workbench.HeightImageViewer.RestoreHeightDisplayModes(new Dictionary<string, string>());
        (workbenchView.ViewerContent as IOpenVisionThreeDViewerHost)?.TryApplyHeightDisplayOptions(new HeightDisplayOptions());
        viewModel.Workbench.HeightImageViewer.TryApplyHeightDisplayOptions(new HeightDisplayOptions());
        workbenchView.RestoreViewerDisplayProfiles(new[] { "main", "auxiliary", "section", "height" }
            .SelectMany(slot => new[] { "3d", "input", "output" }.Select(kind => new KeyValuePair<string, HeightDisplayProfile>(slot + "/" + kind, new(new(), new Dictionary<string, string>(), string.Empty))))
            .ToDictionary(entry => entry.Key, entry => entry.Value));
        window.WindowState = WindowState.Maximized;
        canAutoSave = true;
        try
        {
            store?.Reset();
            viewModel.ReportLayoutStatus(FormatLayoutResetStatus());
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Info,
                "Studio layout reset | presentationOnly=true | recipeChanged=false | inspectionRun=false");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            viewModel.ReportLayoutStatus(FormatLayoutResetFailureStatus(exception));
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Warning,
                $"Studio layout reset file removal failed | {exception}");
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        workbenchDockingManager.LayoutFloatingWindowControlCreated -= OnFloatingWindowCreated;
        foreach (var child in initialValidationFloats.Keys.ToArray()) ReleaseInitialValidationFloat(child);
        window.Loaded -= OnWindowLoaded;
        window.StateChanged -= OnWindowStateChanged;
        window.LocationChanged -= OnWindowLocationChanged;
    }

    private void OnFloatingWindowCreated(object? sender, AvalonDock.LayoutFloatingWindowControlCreatedEventArgs args) =>
        TrackInitialValidationFloat(args.LayoutFloatingWindowControl);

    internal int PendingInitialValidationFloatCount => initialValidationFloats.Count;

    internal void TrackInitialValidationFloat(LayoutFloatingWindowControl child)
    {
        if (disposed || initialValidationFloats.ContainsKey(child) || !workbenchView.DockWorkspace.IsValidationEvidenceFloatingWindow(child.Model)) return;
        // AvalonDock's default floating chrome reads system keys outside the docked View tree.
        foreach (var (systemKey, semanticKey) in new[]
        {
            (SystemColors.ControlBrushKey, "ThreeD.CommandBarBrush"),
            (SystemColors.ControlDarkBrushKey, "ThreeD.DividerBrush"),
            (SystemColors.ControlTextBrushKey, "ThreeD.TextBrush"),
            (SystemColors.GrayTextBrushKey, "ThreeD.DisabledBrush"),
            (SystemColors.HighlightBrushKey, "ThreeD.AccentBrush")
        })
        {
            if (window.TryFindResource(semanticKey) is System.Windows.Media.Brush brush) child.Resources[systemKey] = brush;
        }
        child.SetResourceReference(System.Windows.Controls.Control.ForegroundProperty, "ThreeD.TextBrush");
        initialValidationFloats.Add(child, null);
        child.SourceInitialized += OnInitialFloatingSourceInitialized;
        child.Loaded += OnInitialFloatingLoaded;
        if (new WindowInteropHelper(child).Handle != IntPtr.Zero) OnInitialFloatingSourceInitialized(child, EventArgs.Empty);
        child.Closed += OnInitialFloatingClosed;
        FloatingDragState.AddValueChanged(child, OnInitialFloatingDragChanged);
        if (child.IsLoaded)
        {
            ApplyFloatingCaptionGlyphs(child);
            QueueInitialValidationFloat(child);
        }
    }

    private void OnInitialFloatingSourceInitialized(object? sender, EventArgs args)
    {
        var child = (LayoutFloatingWindowControl)sender!;
        if (!initialValidationFloats.ContainsKey(child) || initialFloatingSources.ContainsKey(child)) return;
        var source = HwndSource.FromHwnd(new WindowInteropHelper(child).Handle);
        if (source is null) return;
        HwndSourceHook hook = (IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            if (message == 0x0232) QueueInitialValidationFloat(child, dragCompleted: true);
            return IntPtr.Zero;
        };
        initialFloatingSources.Add(child, (source, hook));
        source.AddHook(hook);
    }

    private void OnInitialFloatingLoaded(object sender, RoutedEventArgs args)
    {
        var child = (LayoutFloatingWindowControl)sender;
        ApplyFloatingCaptionGlyphs(child);
        QueueInitialValidationFloat(child);
    }

    internal static void ApplyFloatingCaptionGlyphs(LayoutFloatingWindowControl child)
    {
        child.ApplyTemplate();
        if (child.Template?.FindName("WindowBorder", child) is not System.Windows.Controls.Border { Child: System.Windows.Controls.Grid chrome }) return;
        var caption = chrome.Children.OfType<System.Windows.Controls.Grid>().FirstOrDefault();
        if (caption is null) return;
        foreach (var button in caption.Children.OfType<System.Windows.Controls.Primitives.ButtonBase>())
        {
            if (button.Content is not System.Windows.Controls.Image { Source: { } source }) continue;
            var glyph = new System.Windows.Controls.Border
            {
                Width = source.Width, Height = source.Height,
                OpacityMask = new System.Windows.Media.ImageBrush(source) { Stretch = System.Windows.Media.Stretch.Uniform }
            };
            var glyphStyle = new Style(typeof(System.Windows.Controls.Border));
            glyphStyle.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, new System.Windows.Data.Binding(nameof(System.Windows.Controls.Control.Foreground)) { Source = button }));
            var disabled = new DataTrigger { Binding = new System.Windows.Data.Binding(nameof(UIElement.IsEnabled)) { Source = button }, Value = false };
            disabled.Setters.Add(new Setter(System.Windows.Controls.Border.BackgroundProperty, new DynamicResourceExtension("ThreeD.DisabledBrush")));
            glyphStyle.Triggers.Add(disabled);
            glyph.Style = glyphStyle;
            button.Content = glyph;
        }
    }

    private void OnInitialFloatingDragChanged(object? sender, EventArgs args)
    {
        var child = (LayoutFloatingWindowControl)sender!;
        QueueInitialValidationFloat(child, dragCompleted: !child.IsDragging);
    }

    private void OnInitialFloatingClosed(object? sender, EventArgs args) => ReleaseInitialValidationFloat((LayoutFloatingWindowControl)sender!);

    private void QueueInitialValidationFloat(LayoutFloatingWindowControl child, bool dragCompleted = false)
    {
        if (!initialValidationFloats.TryGetValue(child, out var previous)) return;
        previous?.Abort();
        initialValidationFloats[child] = null;
        if (child.IsDragging || !child.IsLoaded) return;
        initialValidationFloats[child] = child.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (disposed || child.IsDragging || (!dragCompleted && System.Windows.Input.Mouse.LeftButton == System.Windows.Input.MouseButtonState.Pressed) || !initialValidationFloats.ContainsKey(child)) return;
            var handle = new WindowInteropHelper(child).Handle;
            var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (handle == IntPtr.Zero || monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                ReleaseInitialValidationFloat(child);
                return;
            }
            var dpi = GetDpiForWindow(handle);
            var scale = dpi > 0 ? dpi / 96d : 1d;
            CompleteInitialValidationFloat(child, new Rect(info.Work.Left / scale, info.Work.Top / scale, (info.Work.Right - info.Work.Left) / scale, (info.Work.Bottom - info.Work.Top) / scale));
        }));
    }

    internal void CompleteInitialValidationFloat(LayoutFloatingWindowControl child, Rect workArea)
    {
        if (disposed || child.IsDragging || !initialValidationFloats.ContainsKey(child)) return;
        var eligible = workbenchView.DockWorkspace.IsValidationEvidenceFloatingWindow(child.Model);
        ReleaseInitialValidationFloat(child);
        if (!eligible) return;
        child.Width = Math.Min(child.Width, Math.Max(child.MinWidth, workArea.Width));
        child.Height = Math.Min(child.Height, Math.Max(child.MinHeight, workArea.Height));
        child.Left = Math.Clamp(child.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - child.Width));
        child.Top = Math.Clamp(child.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - child.Height));
    }

    private void ReleaseInitialValidationFloat(LayoutFloatingWindowControl child)
    {
        if (!initialValidationFloats.Remove(child, out var operation)) return;
        operation?.Abort();
        child.SourceInitialized -= OnInitialFloatingSourceInitialized;
        if (initialFloatingSources.Remove(child, out var native) && !native.Source.IsDisposed) native.Source.RemoveHook(native.Hook);
        child.Loaded -= OnInitialFloatingLoaded;
        child.Closed -= OnInitialFloatingClosed;
        FloatingDragState.RemoveValueChanged(child, OnInitialFloatingDragChanged);
    }

    private void Configure(string? explicitProfilePath)
    {
        store = new StudioLayoutProfileStore(
            string.IsNullOrWhiteSpace(explicitProfilePath)
                ? GetPersistentPath()
                : explicitProfilePath);
        var result = store.Load();
        loadStatus = result.Status;
        pendingProfile = result.Profile;
        canAutoSave = result.CanAutoSave;
        ApplyWindowPlacement(result.Profile.Window);
        viewModel.ReportLayoutStatus(FormatLayoutLoadStatus(result));
        OVLog.Write(
            LogCategory.UI,
            result.Status is StudioLayoutLoadStatus.Corrupt
                or StudioLayoutLoadStatus.Incompatible
                ? LogLevel.Warning
                : LogLevel.Info,
            $"Studio layout | status={result.Status} | path={store.Path} | {result.Message}");
    }

    private static string FormatLayoutLoadStatus(StudioLayoutLoadResult result) =>
        result.Status switch
        {
            StudioLayoutLoadStatus.Missing => ThreeDLocalization.Shared.Resolve(
                "ThreeD.Shell.Layout.Missing",
                "저장된 화면 배치가 없어 기본 화면을 사용합니다.",
                "No saved layout was found; the default layout is active."),
            StudioLayoutLoadStatus.Restored => ThreeDLocalization.Shared.Resolve(
                "ThreeD.Shell.Layout.Restored",
                "저장한 화면 배치를 불러왔습니다.",
                "Saved layout restored."),
            StudioLayoutLoadStatus.RestoredWithFallback => ThreeDLocalization.Shared.Resolve(
                "ThreeD.Shell.Layout.RestoredWithFallback",
                "안전하지 않은 화면 배치라 기본값으로 열었습니다.",
                "The saved layout was unsafe, so the default layout is active."),
            StudioLayoutLoadStatus.Corrupt => ThreeDLocalization.Shared.Resolve(
                "ThreeD.Shell.Layout.Corrupt",
                "저장한 화면 배치를 읽지 못해 기본 화면을 사용합니다.",
                "The saved layout could not be read; the default layout is active."),
            StudioLayoutLoadStatus.Incompatible => ThreeDLocalization.Shared.Resolve(
                "ThreeD.Shell.Layout.Incompatible",
                "지원하지 않는 화면 배치 버전이라 기본 화면을 사용합니다.",
                "The saved layout version is unsupported; the default layout is active."),
            _ => string.Empty,
        };

    private static string FormatLayoutResetStatus() => ThreeDLocalization.Shared.Resolve(
        "ThreeD.Shell.Layout.Reset",
        "화면 배치를 기본값으로 되돌렸습니다. 레시피와 실행 상태는 유지했습니다.",
        "Layout reset to defaults. Recipe and run state were unchanged.");

    private static string FormatLayoutResetFailureStatus(Exception exception) =>
        ThreeDLocalization.Shared.Resolve(
            "ThreeD.Shell.Layout.ResetFailed",
            $"기본 화면을 사용 중이지만 저장한 화면 배치를 삭제하지 못했습니다: {exception.Message}",
            $"The default layout is active, but the saved layout could not be removed: {exception.Message}");

    private void OnWindowLoaded(object sender, RoutedEventArgs args)
    {
        window.Loaded -= OnWindowLoaded;
        ConstrainNormalWindowToWorkArea();
        if (pendingProfile is not { } profile)
        {
            return;
        }

        workbenchView.ApplyDockPresentationState(profile.Workbench);
        advancedWorkspace.ApplyPresentationState(profile.Advanced);
        if (profile.HeightDisplay is { } heightDisplay)
            (workbenchView.ViewerContent as IOpenVisionThreeDViewerHost)?.TryApplyHeightDisplayOptions(heightDisplay);
        if (profile.HeightImageDisplay is { } heightImageDisplay)
            viewModel.Workbench.HeightImageViewer.TryApplyHeightDisplayOptions(heightImageDisplay);
        if (profile.HeightDisplayModes is { } heightModes)
            (workbenchView.ViewerContent as IOpenVisionThreeDViewerHost)?.RestoreHeightDisplayModes(heightModes);
        if (profile.HeightImageDisplayModes is { } imageModes)
            viewModel.Workbench.HeightImageViewer.RestoreHeightDisplayModes(imageModes);
        if (profile.PaneDisplays is { } paneDisplays) workbenchView.RestoreViewerDisplayProfiles(paneDisplays);
        WriteSmokeReport(profile);
        pendingProfile = null;
    }

    private void OnWindowStateChanged(object? sender, EventArgs args) =>
        ConstrainNormalWindowToWorkArea();

    private void OnWindowLocationChanged(object? sender, EventArgs args) =>
        ConstrainNormalWindowToWorkArea();

    private void ConstrainNormalWindowToWorkArea()
    {
        if (window.WindowState != WindowState.Normal)
        {
            return;
        }

        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var dpi = GetDpiForWindow(handle);
        var scale = dpi > 0 ? dpi / 96d : 1d;
        var workLeft = info.Work.Left / scale;
        var workTop = info.Work.Top / scale;
        var workRight = info.Work.Right / scale;
        var workBottom = info.Work.Bottom / scale;
        var maxWidth = Math.Max(window.MinWidth, workRight - workLeft);
        var maxHeight = Math.Max(window.MinHeight, workBottom - workTop);
        window.MaxWidth = maxWidth;
        window.MaxHeight = maxHeight;
        window.Width = Math.Min(window.Width, maxWidth);
        window.Height = Math.Min(window.Height, maxHeight);

        window.Left = Math.Clamp(
            window.Left,
            workLeft,
            Math.Max(workLeft, workRight - window.Width));
        window.Top = Math.Clamp(
            window.Top,
            workTop,
            Math.Max(workTop, workBottom - window.Height));
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

    private void WriteSmokeReport(StudioLayoutProfile profile)
    {
        if (string.IsNullOrWhiteSpace(smokeReportPath))
        {
            return;
        }

        var fullPath = Path.GetFullPath(smokeReportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullPath,
            [
                "OpenVisionLab 3D Studio layout restore smoke",
                $"LoadStatus={loadStatus}",
                $"SchemaVersion={profile.SchemaVersion}",
                $"Stage={workbenchView.OperatorStage}",
                $"StartPageSelected={viewModel.IsStartPageSelected}",
                $"NavigationRailVisible={viewModel.IsNavigationRailVisible}",
                $"RecipeDirty={viewModel.Workbench.IsDirty}",
                $"ParameterDraft={viewModel.Workbench.HasPendingStepParameterChanges}",
                $"RoiCapture={viewModel.Workbench.IsSelectionCandidateActive}",
                $"PreviewRunning={viewModel.Workbench.IsSelectedStepPreviewRunning}",
                $"ValidationRunning={viewModel.Workbench.IsValidationSetRunning}",
                $"WorkbenchPrimary={profile.Workbench.PrimaryContentId}",
                $"WorkbenchSupport={profile.Workbench.SupportContentId}",
                "RestoreContract=presentation-only; recipeChanged=false; inspectionRun=false",
            ]);
    }

    private void ApplyWindowPlacement(StudioWindowPlacement? placement)
    {
        if (placement is null)
        {
            return;
        }

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.WindowState = WindowState.Normal;
        window.Left = placement.Left;
        window.Top = placement.Top;
        window.Width = placement.Width;
        window.Height = placement.Height;
        if (placement.IsMaximized)
        {
            window.WindowState = WindowState.Maximized;
        }
    }

    private static string GetPersistentPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OpenVisionLab",
        "ThreeDStudio",
        "studio-layout-v1.json");
}

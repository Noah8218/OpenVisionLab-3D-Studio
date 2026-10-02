using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Layout;

/// <summary>
/// Owns the presentation-only Studio layout lifecycle. It never owns or
/// mutates recipe, source, ROI, Preview, Publish, Run, or result state.
/// </summary>
internal sealed class StudioLayoutController : IDisposable
{
    private const uint MonitorDefaultToNearest = 2;
    private readonly MainWindow window;
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly OpenVisionDockWorkspaceView advancedWorkspace;
    private readonly ShellMainWindowViewModel viewModel;
    private readonly string? smokeReportPath;
    private StudioLayoutProfileStore? store;
    private StudioLayoutProfile? pendingProfile;
    private StudioLayoutLoadStatus? loadStatus;
    private bool canAutoSave;

    public StudioLayoutController(
        MainWindow window,
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
                advancedWorkspace.CapturePresentationState()));
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
        window.Loaded -= OnWindowLoaded;
        window.StateChanged -= OnWindowStateChanged;
        window.LocationChanged -= OnWindowLocationChanged;
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

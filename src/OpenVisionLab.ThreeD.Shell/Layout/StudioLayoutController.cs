using System.IO;
using System.Windows;
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
            viewModel.ReportLayoutStatus(
                "Saved layout reset to safe defaults. Recipe and run state were unchanged.");
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Info,
                "Studio layout reset | presentationOnly=true | recipeChanged=false | inspectionRun=false");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException)
        {
            viewModel.ReportLayoutStatus(
                $"Layout defaults are active, but the saved file could not be removed: {exception.Message}");
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Warning,
                $"Studio layout reset file removal failed | {exception}");
        }
    }

    public void Dispose() => window.Loaded -= OnWindowLoaded;

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
        viewModel.ReportLayoutStatus(result.Message);
        OVLog.Write(
            LogCategory.UI,
            result.Status is StudioLayoutLoadStatus.Corrupt
                or StudioLayoutLoadStatus.Incompatible
                ? LogLevel.Warning
                : LogLevel.Info,
            $"Studio layout | status={result.Status} | path={store.Path} | {result.Message}");
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs args)
    {
        window.Loaded -= OnWindowLoaded;
        if (pendingProfile is not { } profile)
        {
            return;
        }

        workbenchView.ApplyDockPresentationState(profile.Workbench);
        advancedWorkspace.ApplyPresentationState(profile.Advanced);
        WriteSmokeReport(profile);
        pendingProfile = null;
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

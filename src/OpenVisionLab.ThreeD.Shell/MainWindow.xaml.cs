using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Shell;

public partial class MainWindow : Window
{
    private readonly OpenVisionThreeDViewerControl _viewer = new();
    private readonly ShellMainWindowViewModel _viewModel;
    private readonly EventHandler<ViewerHostStateChangedEventArgs> _viewerHostStateChangedHandler;
    private readonly EventHandler _refreshRecipeComparisonRequestedHandler;
    private readonly EventHandler _saveRecipeRequestedHandler;
    private readonly EventHandler _applyRoiAlignmentRequestedHandler;
    private readonly EventHandler _fitPlaneRequestedHandler;
    private readonly EventHandler<EvidenceArtifactOpenRequestEventArgs> _openEvidenceArtifactRequestedHandler;
    private RoutedEventHandler _shellSmokeLoadedHandler = (_, _) => { };

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ShellMainWindowViewModel(
            GetCommandLineValue("--recipe-comparison-contract"),
            GetCommandLineValue("--recipe-comparison-report"),
            GetCommandLineValue("--shell-smoke-screenshot"),
            GetCommandLineValue("--run-record"),
            GetCommandLineValue("--html-report"),
            GetCommandLineValue("--csv-report"));
        _viewModel.SelectedEvidenceTabIndex = GetEvidenceTabIndex(GetCommandLineValue("--shell-evidence-tab"));
        DataContext = _viewModel;
        _viewer.SidePanelsVisible = false;
        Workspace.ViewerContent = _viewer;
        _viewModel.UpdateC3DSampleVisible(_viewer.HostState.C3DSampleVisible);

        _viewerHostStateChangedHandler = OnViewerHostStateChanged;
        _viewer.HostStateChanged += _viewerHostStateChangedHandler;
        _viewer.EnableSmokeFromCommandLine();

        _refreshRecipeComparisonRequestedHandler = (_, _) => _viewModel.RefreshRecipeComparison();
        _saveRecipeRequestedHandler = (_, _) => _viewer.SaveCurrentRecipeWithDialog();
        _applyRoiAlignmentRequestedHandler = (_, _) => _viewer.ApplyRoiReferenceAlignment();
        _fitPlaneRequestedHandler = (_, _) => _viewer.FitC3DReferencePlane();
        _openEvidenceArtifactRequestedHandler = OnOpenEvidenceArtifactRequested;
        _viewModel.RefreshRecipeComparisonRequested += _refreshRecipeComparisonRequestedHandler;
        _viewModel.SaveRecipeRequested += _saveRecipeRequestedHandler;
        _viewModel.ApplyRoiAlignmentRequested += _applyRoiAlignmentRequestedHandler;
        _viewModel.FitPlaneRequested += _fitPlaneRequestedHandler;
        _viewModel.OpenEvidenceArtifactRequested += _openEvidenceArtifactRequestedHandler;

        EnableShellSmokeFromCommandLine();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewer.HostStateChanged -= _viewerHostStateChangedHandler;
        _viewModel.RefreshRecipeComparisonRequested -= _refreshRecipeComparisonRequestedHandler;
        _viewModel.SaveRecipeRequested -= _saveRecipeRequestedHandler;
        _viewModel.ApplyRoiAlignmentRequested -= _applyRoiAlignmentRequestedHandler;
        _viewModel.FitPlaneRequested -= _fitPlaneRequestedHandler;
        _viewModel.OpenEvidenceArtifactRequested -= _openEvidenceArtifactRequestedHandler;
        Loaded -= _shellSmokeLoadedHandler;
        base.OnClosed(e);
    }

    private void EnableShellSmokeFromCommandLine()
    {
        var shellScreenshotPath = GetCommandLineValue("--shell-smoke-screenshot");
        var screenshotQualityReportPath = GetCommandLineValue("--shell-screenshot-quality-report");
        var smokeSaveRecipePath = GetCommandLineValue("--smoke-save-recipe");
        if (shellScreenshotPath is not null)
        {
            _shellSmokeLoadedHandler = async (_, _) =>
            {
                await Dispatcher.InvokeAsync(() => { });
                await Task.Delay(900);
                if (smokeSaveRecipePath is not null && !_viewer.SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                if (_viewer.SmokeExitCode != 0)
                {
                    _viewModel.SetViewerSmokeFailed(_viewer.HostState.ViewerStatus);
                }

                UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(100);
                if (!await CaptureShellWindowWithRetryAsync(shellScreenshotPath, screenshotQualityReportPath))
                {
                    _viewModel.SetViewerSmokeFailed("Shell screenshot remained blank or invalid after 3 attempts.");
                    Application.Current.Shutdown(1);
                    return;
                }

                await Task.Delay(100);
                Application.Current.Shutdown(_viewer.SmokeExitCode);
            };

            Loaded += _shellSmokeLoadedHandler;
        }
    }

    private async Task<bool> CaptureShellWindowWithRetryAsync(string path, string? qualityReportPath)
    {
        const int maximumAttempts = 3;
        var fullPath = Path.GetFullPath(path);
        var qualityLines = new List<string>();
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var previousRejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            if (File.Exists(previousRejectedPath))
            {
                File.Delete(previousRejectedPath);
            }
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var result = ShellScreenshotCapture.Capture(this);
            var qualityLine = $"ShellScreenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                ShellScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"ShellScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteScreenshotQualityReport(qualityReportPath, qualityLines);
                return true;
            }

            var rejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            ShellScreenshotCapture.Save(result.Bitmap, rejectedPath);
            await Task.Delay(250);
        }

        qualityLines.Add($"ShellScreenshotResult|accepted=False|attempts={maximumAttempts}|screenshot={fullPath}");
        WriteScreenshotQualityReport(qualityReportPath, qualityLines);
        return false;
    }

    private static void WriteScreenshotQualityReport(string? path, IReadOnlyList<string> lines)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllLines(path, lines);
    }

    private static string GetRejectedScreenshotPath(string fullPath, int attempt) =>
        Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $"{Path.GetFileNameWithoutExtension(fullPath)}.rejected-attempt-{attempt}{Path.GetExtension(fullPath)}");

    private static string? GetCommandLineValue(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    private static int GetEvidenceTabIndex(string? tabName)
    {
        return tabName?.Trim().ToLowerInvariant() switch
        {
            "runner" or "runner-report" => 1,
            "snapshot" or "run" or "run-record" => 2,
            "steps" or "timeline" => 3,
            "history" => 4,
            _ => 0
        };
    }

    private void OnOpenEvidenceArtifactRequested(object? sender, EvidenceArtifactOpenRequestEventArgs args)
    {
        if (!File.Exists(args.Path))
        {
            MessageBox.Show(
                this,
                $"{args.Label} artifact was not found.\n\n{args.Path}",
                "Open Evidence Artifact",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(args.Path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Could not open {args.Label} artifact.\n\n{args.Path}\n\n{ex.Message}",
                "Open Evidence Artifact",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OnViewerHostStateChanged(object? sender, ViewerHostStateChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewerHostState.C3DSampleVisible))
        {
            _viewModel.UpdateC3DSampleVisible(args.State.C3DSampleVisible);
        }
    }
}

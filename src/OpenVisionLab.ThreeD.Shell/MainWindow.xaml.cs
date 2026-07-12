using OpenVisionLab.ThreeD.Viewer;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace OpenVisionLab.ThreeD.Shell;

public partial class MainWindow : Window
{
    private readonly OpenVisionThreeDViewerControl _viewer = new();
    private readonly ShellMainWindowViewModel _viewModel;
    private readonly PropertyChangedEventHandler _viewerPropertyChangedHandler;
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
        _viewModel.UpdateC3DSampleVisible(_viewer.ViewModel.C3DSampleVisible);

        _viewerPropertyChangedHandler = OnViewerPropertyChanged;
        _viewer.ViewModel.PropertyChanged += _viewerPropertyChangedHandler;
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
        _viewer.ViewModel.PropertyChanged -= _viewerPropertyChangedHandler;
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
                    _viewModel.SetViewerSmokeFailed(_viewer.ViewModel.ViewerStatus);
                }

                UpdateLayout();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
                await Task.Delay(100);
                CaptureShellWindow(shellScreenshotPath);
                await Task.Delay(100);
                Application.Current.Shutdown(_viewer.SmokeExitCode);
            };

            Loaded += _shellSmokeLoadedHandler;
        }
    }

    private void CaptureShellWindow(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);

        var width = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(this);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        using var stream = File.Create(path);
        encoder.Save(stream);
    }

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

    private void OnViewerPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(OpenVisionLab.ThreeD.Viewer.ViewModels.MainWindowViewModel.C3DSampleVisible))
        {
            _viewModel.UpdateC3DSampleVisible(_viewer.ViewModel.C3DSampleVisible);
        }
    }
}

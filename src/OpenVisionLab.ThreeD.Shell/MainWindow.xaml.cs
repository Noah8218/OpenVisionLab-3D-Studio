using OpenVisionLab.ThreeD.Viewer;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OpenVisionLab.ThreeD.Shell;

public partial class MainWindow : Window
{
    private readonly OpenVisionThreeDViewerControl _viewer = new();
    private readonly ShellMainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new ShellMainWindowViewModel(
            GetCommandLineValue("--recipe-comparison-contract"),
            GetCommandLineValue("--recipe-comparison-report"));
        DataContext = _viewModel;
        _viewer.SidePanelsVisible = false;
        Workspace.ViewerContent = _viewer;
        _viewer.EnableSmokeFromCommandLine();
        EnableShellSmokeFromCommandLine();
    }

    private void RefreshRecipeComparison_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.RefreshRecipeComparison();
    }

    private void SaveRecipe_Click(object sender, RoutedEventArgs e)
    {
        _viewer.SaveCurrentRecipeWithDialog();
    }

    private void EnableShellSmokeFromCommandLine()
    {
        var shellScreenshotPath = GetCommandLineValue("--shell-smoke-screenshot");
        var smokeSaveRecipePath = GetCommandLineValue("--smoke-save-recipe");
        if (shellScreenshotPath is not null)
        {
            Loaded += async (_, _) =>
            {
                await Dispatcher.InvokeAsync(() => { });
                await Task.Delay(900);
                if (smokeSaveRecipePath is not null && !_viewer.SaveCurrentRecipe(smokeSaveRecipePath, isSmoke: true))
                {
                    Application.Current.Shutdown(1);
                    return;
                }

                CaptureShellWindow(shellScreenshotPath);
                await Task.Delay(100);
                Application.Current.Shutdown();
            };
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
}

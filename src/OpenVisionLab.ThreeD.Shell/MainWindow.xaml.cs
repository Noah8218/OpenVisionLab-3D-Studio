using OpenVisionLab.ThreeD.Viewer;
using System.Windows;

namespace OpenVisionLab.ThreeD.Shell;

public partial class MainWindow : Window
{
    private readonly OpenVisionThreeDViewerControl _viewer = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = new ShellMainWindowViewModel();
        Workspace.ViewerContent = _viewer;
        _viewer.EnableSmokeFromCommandLine();
    }
}

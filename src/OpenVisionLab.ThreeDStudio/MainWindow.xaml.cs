using System.Windows;

namespace OpenVisionLab.ThreeDStudio;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Viewer.EnableSmokeFromCommandLine();
    }
}

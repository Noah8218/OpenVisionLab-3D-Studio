using System.Windows;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Shell.Views.Integration;

public partial class ThreeDIntegrationExchangeView
{
    public ThreeDIntegrationExchangeView()
    {
        InitializeComponent();
    }

    private void OnResetSetupClicked(object sender, RoutedEventArgs e)
    {
        IntegrationSharedKeyBox.Clear();
    }
}

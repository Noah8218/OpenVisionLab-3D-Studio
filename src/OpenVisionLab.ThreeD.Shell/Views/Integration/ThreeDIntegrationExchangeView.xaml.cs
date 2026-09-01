using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell;

namespace OpenVisionLab.ThreeD.Shell.Views.Integration;

public partial class ThreeDIntegrationExchangeView
{
    public ThreeDIntegrationExchangeView()
    {
        InitializeComponent();
    }

    private void OnSharedKeyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox
            && DataContext is ShellMainWindowViewModel shell)
        {
            shell.IntegrationExchange.SetSessionSharedKey(passwordBox.Password);
        }
    }

    private void OnResetSetupClicked(object sender, RoutedEventArgs e)
    {
        IntegrationSharedKeyBox.Clear();
    }
}

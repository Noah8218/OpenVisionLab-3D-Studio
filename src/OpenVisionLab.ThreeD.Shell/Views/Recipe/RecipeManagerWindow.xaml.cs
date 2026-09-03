using System.ComponentModel;
using System.Windows;

namespace OpenVisionLab.ThreeD.Shell.Views.Recipe;

public partial class RecipeManagerWindow : Window
{
    private bool allowClose;

    public RecipeManagerWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    public void CloseForOwner()
    {
        allowClose = true;
        Close();
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (!allowClose && Owner?.IsVisible == true)
        {
            args.Cancel = true;
            Hide();
        }
    }
}

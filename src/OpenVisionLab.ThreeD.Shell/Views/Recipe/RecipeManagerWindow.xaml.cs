using System.ComponentModel;
using System.Windows;

namespace OpenVisionLab.ThreeD.Shell.Views.Recipe;

public partial class RecipeManagerWindow : Window
{
    public RecipeManagerWindow()
    {
        InitializeComponent();
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, CancelEventArgs args)
    {
        if (Owner?.IsVisible == true)
        {
            args.Cancel = true;
            Hide();
        }
    }
}

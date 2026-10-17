using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

/// <summary>
/// Global recipe context and menus. Window requests are forwarded to the shell's existing owners.
/// </summary>
public partial class StudioHeaderView : UserControl
{
    public StudioHeaderView()
    {
        InitializeComponent();
    }

    public event EventHandler? RecipeManagerRequested;
    public event EventHandler? LayoutResetRequested;

    private void OpenRecipeManager_Click(object? sender, EventArgs args) =>
        RecipeManagerRequested?.Invoke(this, EventArgs.Empty);

    private void ResetLayout_Click(object sender, RoutedEventArgs args) =>
        LayoutResetRequested?.Invoke(this, EventArgs.Empty);
}

using System.Windows;
using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

/// <summary>
/// Visual composition for the application's top title, context, workspace, and tool commands.
/// Command handling remains in the owning Shell window.
/// </summary>
public partial class StudioHeaderView : UserControl
{
    public StudioHeaderView()
    {
        InitializeComponent();
    }

    public event EventHandler? RecipeManagerRequested;
    public event EventHandler? FilterToolLabRequested;
    public event EventHandler? EdgeToolLabRequested;
    public event EventHandler? TwoPointLineToolLabRequested;
    public event EventHandler? ThreePointPlaneToolLabRequested;
    public event EventHandler? DatumPlaneDeviationToolLabRequested;
    public event EventHandler? LineIntersectionToolLabRequested;
    public event EventHandler? LandmarkCorrespondenceToolLabRequested;
    public event EventHandler? XYZAffineSolveToolLabRequested;

    private void OpenRecipeManagerButton_Click(object sender, RoutedEventArgs args) =>
        RecipeManagerRequested?.Invoke(this, EventArgs.Empty);

    private void OpenToolLabsMenuButton_Click(object sender, RoutedEventArgs args)
    {
        if (ToolLabsMenuButton.ContextMenu is { } menu)
        {
            menu.PlacementTarget = ToolLabsMenuButton;
            menu.IsOpen = true;
        }
    }

    private void OpenFilterToolLabButton_Click(object sender, RoutedEventArgs args) =>
        FilterToolLabRequested?.Invoke(this, EventArgs.Empty);

    private void OpenEdgeToolLabButton_Click(object sender, RoutedEventArgs args) =>
        EdgeToolLabRequested?.Invoke(this, EventArgs.Empty);

    private void OpenTwoPointLineToolLabButton_Click(object sender, RoutedEventArgs args) =>
        TwoPointLineToolLabRequested?.Invoke(this, EventArgs.Empty);

    private void OpenThreePointPlaneToolLabButton_Click(object sender, RoutedEventArgs args) =>
        ThreePointPlaneToolLabRequested?.Invoke(this, EventArgs.Empty);

    private void OpenDatumPlaneDeviationToolLabButton_Click(object sender, RoutedEventArgs args) =>
        DatumPlaneDeviationToolLabRequested?.Invoke(this, EventArgs.Empty);

    private void OpenLineIntersectionToolLabButton_Click(object sender, RoutedEventArgs args) =>
        LineIntersectionToolLabRequested?.Invoke(this, EventArgs.Empty);

    private void OpenLandmarkCorrespondenceToolLabButton_Click(object sender, RoutedEventArgs args) =>
        LandmarkCorrespondenceToolLabRequested?.Invoke(this, EventArgs.Empty);

    private void OpenXYZAffineSolveToolLabButton_Click(object sender, RoutedEventArgs args) =>
        XYZAffineSolveToolLabRequested?.Invoke(this, EventArgs.Empty);
}

using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

/// <summary>
/// Dockable projection of existing typed artifacts. Commands stay in the
/// workbench ViewModel so the view cannot execute or rewire a recipe.
/// </summary>
public partial class DisplayedOutputsView : UserControl
{
    public DisplayedOutputsView()
    {
        InitializeComponent();
    }
}

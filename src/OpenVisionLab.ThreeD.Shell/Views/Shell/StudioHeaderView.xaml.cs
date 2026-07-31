using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Shell;

/// <summary>
/// The single global title and job-context bar.
/// Workspace navigation is owned by <see cref="StudioNavigationRailView"/>.
/// </summary>
public partial class StudioHeaderView : UserControl
{
    public StudioHeaderView()
    {
        InitializeComponent();
    }
}

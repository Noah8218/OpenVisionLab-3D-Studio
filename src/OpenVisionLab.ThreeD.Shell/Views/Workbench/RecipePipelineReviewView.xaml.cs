using System.Windows.Controls;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class RecipePipelineReviewView : UserControl
{
    public RecipePipelineReviewView()
    {
        InitializeComponent();
    }

    public void ActivateFlowMap() => ReviewTabs.SelectedIndex = 1;

    public bool IsFlowMapSelected => ReviewTabs.SelectedIndex == 1;
}

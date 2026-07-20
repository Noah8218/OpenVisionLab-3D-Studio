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

    public void ActivateProblems() => ReviewTabs.SelectedIndex = 2;

    public bool IsProblemsSelected => ReviewTabs.SelectedIndex == 2;
}

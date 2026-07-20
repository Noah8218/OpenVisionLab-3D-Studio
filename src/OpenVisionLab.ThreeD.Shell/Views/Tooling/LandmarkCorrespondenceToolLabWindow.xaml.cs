using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class LandmarkCorrespondenceToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public LandmarkCorrespondenceToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        SetLabStep(step);
        InitializeComponent();
        DataContext = workbench;
        SourceViewerHost.Content = sourceViewer;
        Loaded += (_, _) => RefreshViews();
        Activated += (_, _) => ActivateLabStep();
    }

    public void SetLabStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!string.Equals(step.ToolId, "landmark-correspondence", StringComparison.Ordinal))
        {
            throw new ArgumentException("Landmark Correspondence Tool Lab requires a Landmark Correspondence step.", nameof(step));
        }

        labStepId = step.Id;
        ActivateLabStep();
    }

    public void ActivateLabStep()
    {
        if (!string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.Ordinal))
        {
            workbench.SelectPipelineStep(labStepId);
        }
    }

    public void RefreshViews()
    {
        ActivateLabStep();
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        sourceViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | current published CornerAnchor evidence");
        if (workbench.TryGetCurrentLandmarkCorrespondenceInputs(out var anchors)
            && workbench.CurrentLandmarkCorrespondenceOutput is { } output)
        {
            sourceViewer.ShowWorkbenchLandmarkCorrespondence(anchors, output, workbench.IsLandmarkCorrespondencePreviewPublished);
        }
        else
        {
            sourceViewer.ClearWorkbenchLandmarkCorrespondence();
        }
    }

    public void ShowLandmarkCorrespondenceResult(ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (string.IsNullOrWhiteSpace(workbench.Source.Path) || !File.Exists(workbench.Source.Path)) return;
        sourceViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | current published CornerAnchor evidence");
        sourceViewer.ShowWorkbenchLandmarkCorrespondence(args.Anchors, args.Output, args.IsPublished);
    }

    private void RefreshViewsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

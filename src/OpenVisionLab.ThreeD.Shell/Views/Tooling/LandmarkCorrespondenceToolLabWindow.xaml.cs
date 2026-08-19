using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class LandmarkCorrespondenceToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };

    public LandmarkCorrespondenceToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "landmark-correspondence", "Landmark Correspondence Tool Lab requires a Landmark Correspondence step.")
    {
        InitializeComponent();
        DataContext = Workbench;
        SourceViewerHost.Content = sourceViewer;
    }

    public override void RefreshViews()
    {
        if (!HasSourcePath(Workbench.Source.Path)) return;
        sourceViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | current published CornerAnchor evidence");
        if (Workbench.TryGetCurrentLandmarkCorrespondenceInputs(out var anchors)
            && Workbench.CurrentLandmarkCorrespondenceOutput is { } output)
        {
            sourceViewer.ShowWorkbenchLandmarkCorrespondence(anchors, output, Workbench.IsLandmarkCorrespondencePreviewPublished);
        }
        else
        {
            sourceViewer.ClearWorkbenchLandmarkCorrespondence();
        }
    }

    public void ShowLandmarkCorrespondenceResult(ToolWorkbenchLandmarkCorrespondenceDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (!HasSourcePath(Workbench.Source.Path)) return;
        sourceViewer.ShowC3DWorkbenchResult(Workbench.Source.Path, "Source C3D | current published CornerAnchor evidence");
        sourceViewer.ShowWorkbenchLandmarkCorrespondence(args.Anchors, args.Output, args.IsPublished);
    }

    private void RefreshViewsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
}

using System.ComponentModel;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class RegridHeightMapToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public RegridHeightMapToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "re-grid-height-map", "Re-grid Height Map Tool Lab requires a Re-grid Height Map step.")
    {
        InitializeComponent();
        DataContext = Workbench;
        SourceViewerHost.Content = sourceViewer;
        OutputViewerHost.Content = outputViewer;
        Closed += OnClosed;
        Workbench.PropertyChanged += OnWorkbenchPropertyChanged;
    }

    public override void RefreshViews()
    {
        if (Workbench.IsAffineApplyPreviewPublished
            && Workbench.CurrentAffineApplyOutput is { } source)
        {
            sourceViewer.ShowWorkbenchAffineApply(source, Workbench.IsAffineApplyPreviewPublished, standaloneReferenceDisplay: true);
        }
        else
        {
            sourceViewer.ClearWorkbenchAffineApply();
            sourceViewer.ClearC3DTeachingSource("A2 Publish required | input remains empty until A2 Preview and Publish succeed.");
        }
        UpdateOutputViewer();
    }

    private void UpdateOutputViewer()
    {
        if (Workbench.CurrentRegridHeightFieldOutput is { } output)
        {
            outputViewer.ShowWorkbenchRegridHeightField(output, Workbench.IsRegridHeightFieldPreviewPublished, standaloneReferenceDisplay: true);
            return;
        }
        outputViewer.ClearWorkbenchRegridHeightField();
        outputViewer.ClearC3DTeachingSource("A2 Publish + authored ReferenceGridProfile required | output remains empty until A3 Preview succeeds.");
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.CurrentAffineApplyOutput)
            or nameof(ToolWorkbenchViewModel.IsAffineApplyPreviewPublished)
            or nameof(ToolWorkbenchViewModel.CurrentRegridHeightFieldOutput)
            or nameof(ToolWorkbenchViewModel.IsRegridHeightFieldPreviewPublished)
            or nameof(ToolWorkbenchViewModel.RegridHeightFieldExecutionSummary))
        {
            Dispatcher.BeginInvoke(RefreshViews);
        }
    }

    private void RefreshViewsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();
    private void OnClosed(object? sender, EventArgs args) => Workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
}

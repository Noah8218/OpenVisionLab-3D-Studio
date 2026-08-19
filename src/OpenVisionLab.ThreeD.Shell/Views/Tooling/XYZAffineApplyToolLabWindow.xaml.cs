using System.ComponentModel;
using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class XYZAffineApplyToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string displayedSourcePath = string.Empty;

    public XYZAffineApplyToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "xyz-affine-apply", "Apply XYZ Affine Tool Lab requires an Apply XYZ Affine step.")
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
        if (!HasSourcePath(Workbench.Source.Path)) return;
        var sourcePath = Path.GetFullPath(Workbench.Source.Path);
        if (!string.Equals(displayedSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            sourceViewer.ShowC3DWorkbenchResult(sourcePath, "Raw C3D | A2 source (column / raw-height / row)");
            displayedSourcePath = sourcePath;
        }
        UpdateOutputViewer();
    }

    private void UpdateOutputViewer()
    {
        if (Workbench.CurrentAffineApplyOutput is { } output)
        {
            outputViewer.ShowWorkbenchAffineApply(output, Workbench.IsAffineApplyPreviewPublished, standaloneReferenceDisplay: true);
            return;
        }

        // An A2 output is a separately owned cloud. Do not preload another raw
        // C3D surface into the output viewer: this keeps waiting/stale states
        // explicit and avoids an unnecessary second full C3D load.
        outputViewer.ClearWorkbenchAffineApply();
        outputViewer.ClearC3DTeachingSource("A1 Publish required | output remains empty until A2 Preview succeeds.");
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.CurrentAffineApplyOutput)
            or nameof(ToolWorkbenchViewModel.IsAffineApplyPreviewPublished)
            or nameof(ToolWorkbenchViewModel.AffineApplyExecutionSummary))
        {
            Dispatcher.BeginInvoke(UpdateOutputViewer);
        }
    }

    private void RefreshViewsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();

    private void OnClosed(object? sender, EventArgs args) =>
        Workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
}

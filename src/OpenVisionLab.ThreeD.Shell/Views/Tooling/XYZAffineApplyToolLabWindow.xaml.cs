using System.ComponentModel;
using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class XYZAffineApplyToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;
    private string displayedSourcePath = string.Empty;

    public XYZAffineApplyToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        SetLabStep(step);
        InitializeComponent();
        DataContext = workbench;
        SourceViewerHost.Content = sourceViewer;
        OutputViewerHost.Content = outputViewer;
        Loaded += (_, _) => RefreshViews();
        Activated += (_, _) => ActivateLabStep();
        Closed += OnClosed;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
    }

    public void SetLabStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!string.Equals(step.ToolId, "xyz-affine-apply", StringComparison.Ordinal))
        {
            throw new ArgumentException("Apply XYZ Affine Tool Lab requires an Apply XYZ Affine step.", nameof(step));
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
        var sourcePath = Path.GetFullPath(workbench.Source.Path);
        if (!string.Equals(displayedSourcePath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            sourceViewer.ShowC3DWorkbenchResult(sourcePath, "Raw C3D | A2 source (column / raw-height / row)");
            displayedSourcePath = sourcePath;
        }
        UpdateOutputViewer();
    }

    private void UpdateOutputViewer()
    {
        if (workbench.CurrentAffineApplyOutput is { } output)
        {
            outputViewer.ShowWorkbenchAffineApply(output, workbench.IsAffineApplyPreviewPublished, standaloneReferenceDisplay: true);
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

    private void OnPropertyGridValueChanged(object sender, EventArgs args) =>
        workbench.MarkSelectedStepParameterDraftDirty();

    private void OnClosed(object? sender, EventArgs args) =>
        workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
}

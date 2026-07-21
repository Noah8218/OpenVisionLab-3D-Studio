using System.ComponentModel;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class RegridHeightMapToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public RegridHeightMapToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
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
        if (!string.Equals(step.ToolId, "re-grid-height-map", StringComparison.Ordinal))
        {
            throw new ArgumentException("Re-grid Height Map Tool Lab requires a Re-grid Height Map step.", nameof(step));
        }
        labStepId = step.Id;
        ActivateLabStep();
    }

    public void ActivateLabStep()
    {
        if (!string.Equals(workbench.SelectedPipelineStep?.Id, labStepId, StringComparison.Ordinal)) workbench.SelectPipelineStep(labStepId);
    }

    public void RefreshViews()
    {
        ActivateLabStep();
        if (workbench.IsAffineApplyPreviewPublished
            && workbench.CurrentAffineApplyOutput is { } source)
        {
            sourceViewer.ShowWorkbenchAffineApply(source, workbench.IsAffineApplyPreviewPublished, standaloneReferenceDisplay: true);
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
        if (workbench.CurrentRegridHeightFieldOutput is { } output)
        {
            outputViewer.ShowWorkbenchRegridHeightField(output, workbench.IsRegridHeightFieldPreviewPublished, standaloneReferenceDisplay: true);
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
    private void OnPropertyGridValueChanged(object sender, EventArgs args) => workbench.MarkSelectedStepParameterDraftDirty();
    private void OnClosed(object? sender, EventArgs args) => workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
}

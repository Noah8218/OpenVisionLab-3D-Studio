using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class XYZAffineSolveToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public XYZAffineSolveToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
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
        if (!string.Equals(step.ToolId, "xyz-affine-solve", StringComparison.Ordinal))
        {
            throw new ArgumentException("XYZ Affine Solve Tool Lab requires an XYZ Affine Solve step.", nameof(step));
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
        sourceViewer.ShowC3DWorkbenchResult(workbench.Source.Path, "Source C3D | XYZ Affine Solve matrix evidence; no transformed surface");
    }

    private void RefreshViewsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();

    private void OnPropertyGridValueChanged(object sender, EventArgs args) =>
        workbench.MarkSelectedStepParameterDraftDirty();

    private void OnDiscardParametersClick(object sender, RoutedEventArgs args) =>
        workbench.DiscardSelectedStepParameterDraft();

    private void OnApplyParametersClick(object sender, RoutedEventArgs args)
    {
        if (!AffineStepPropertyGrid.CommitPendingEdit(out var message))
        {
            workbench.ReportParameterDraftCommitError(message);
            return;
        }
        workbench.TryApplySelectedStepParameterDraft(out _);
    }
}

using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class FilterToolLabWindow : Window
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private string labStepId = string.Empty;

    public FilterToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        SetLabStep(step);
        InitializeComponent();
        DataContext = workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        Loaded += (_, _) => RefreshViews();
        Activated += (_, _) => ActivateLabStep();
    }

    public void SetLabStep(ToolWorkbenchPipelineStepItem step)
    {
        ArgumentNullException.ThrowIfNull(step);
        if (!string.Equals(step.ToolId, "filter", StringComparison.Ordinal))
        {
            throw new ArgumentException("Filter Tool Lab requires a Filter step.", nameof(step));
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
        if (File.Exists(workbench.Source.Path))
        {
            inputViewer.LoadC3DSource(workbench.Source.Path);
        }

        if (!string.IsNullOrWhiteSpace(workbench.CurrentFilterPreviewPath)
            && File.Exists(workbench.CurrentFilterPreviewPath))
        {
            outputViewer.ShowC3DWorkbenchResult(
                workbench.CurrentFilterPreviewPath,
                workbench.CurrentFilterPreviewOutputSummary);
        }
    }

    public void ShowFilterResult(ToolWorkbenchFilterDisplayRequestEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        if (args.IsSource)
        {
            inputViewer.LoadC3DSource(args.C3DPath);
            return;
        }

        outputViewer.ShowC3DWorkbenchResult(
            args.C3DPath,
            $"Filter Preview | {args.ContentSha256[..Math.Min(12, args.ContentSha256.Length)]}");
    }
}

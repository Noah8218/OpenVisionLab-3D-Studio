using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class FilterToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };

    public FilterToolLabWindow(ToolWorkbenchViewModel workbench, ToolWorkbenchPipelineStepItem step)
        : base(workbench, step, "filter", "Filter Tool Lab requires a Filter step.")
    {
        ArgumentNullException.ThrowIfNull(workbench);
        InitializeComponent();
        DataContext = Workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
    }

    public override void RefreshViews()
    {
        ActivateLabStep();
        if (HasSourcePath(Workbench.Source.Path))
        {
            inputViewer.LoadC3DSource(Workbench.Source.Path);
        }

        if (!string.IsNullOrWhiteSpace(Workbench.CurrentFilterPreviewPath)
            && File.Exists(Workbench.CurrentFilterPreviewPath))
        {
            outputViewer.ShowC3DWorkbenchResult(
                Workbench.CurrentFilterPreviewPath,
                Workbench.CurrentFilterPreviewOutputSummary);
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
            $"{args.DisplayLabel} | {args.ContentSha256[..Math.Min(12, args.ContentSha256.Length)]}");
    }
}

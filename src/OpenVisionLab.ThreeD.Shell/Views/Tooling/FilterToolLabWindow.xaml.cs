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

    public FilterToolLabWindow(ToolWorkbenchViewModel workbench)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        InitializeComponent();
        DataContext = workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        Loaded += (_, _) => RefreshViews();
    }

    public void RefreshViews()
    {
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

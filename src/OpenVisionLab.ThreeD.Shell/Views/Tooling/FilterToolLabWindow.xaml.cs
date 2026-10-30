using System.IO;
using System.Windows;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class FilterToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl inputViewer = new(loadDefaultSamples: false) { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new(loadDefaultSamples: false) { SidePanelsVisible = false };

    public FilterToolLabWindow(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        Func<string, bool> selectPipelineStep)
        : base(workbench, step, selectPipelineStep, "filter", "Filter Tool Lab requires a Filter step.")
    {
        ArgumentNullException.ThrowIfNull(workbench);
        InitializeComponent();
        DataContext = Workbench;
        InputViewerHost.Content = inputViewer;
        OutputViewerHost.Content = outputViewer;
        OwnViewer(inputViewer);
        OwnViewer(outputViewer);
    }

    public override void RefreshViews()
    {
        ActivateLabStep();
        if (HasSourcePath(Workbench.Source.Path))
        {
            inputViewer.LoadC3DSource(Workbench.Source.Path);
        }
        else
        {
            inputViewer.ClearC3DTeachingSource(Workbench.Localization.Resolve(
                "ThreeD.ToolLab.Filter.NoInput", "입력 없음", "No input"));
        }

        if (!string.IsNullOrWhiteSpace(Workbench.CurrentFilterPreviewPath)
            && File.Exists(Workbench.CurrentFilterPreviewPath))
        {
            outputViewer.ShowC3DWorkbenchResult(
                Workbench.CurrentFilterPreviewPath,
                Workbench.CurrentFilterPreviewOutputSummary);
        }
        else
        {
            outputViewer.ClearC3DTeachingSource(Workbench.Localization.Resolve(
                "ThreeD.ToolLab.Filter.NoOutput", "결과 없음", "No result"));
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

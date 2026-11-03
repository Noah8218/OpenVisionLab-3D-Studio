using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Tooling;

public partial class XYZAffineApplyToolLabWindow : ToolLabWindowBase
{
    private readonly OpenVisionThreeDViewerControl sourceViewer = new() { SidePanelsVisible = false };
    private readonly OpenVisionThreeDViewerControl outputViewer = new() { SidePanelsVisible = false };
    private readonly object refreshViewsOperationGate = new();
    private DispatcherOperation? refreshViewsOperation;
    private bool refreshSourceRequested;
    private LoadedSourceDisplay? displayedSource;

    private sealed record SourceDisplayInput(string Path, string EntityId, string Format,
        string Unit, string FrameId, ToolRecipeSelectionSourceBinding? Binding,
        long Length, long LastWriteTicks);
    private sealed record LoadedSourceDisplay(SourceDisplayInput Input, ToolRecipeSelectionSourceBinding Binding);

    public XYZAffineApplyToolLabWindow(
        ToolWorkbenchViewModel workbench,
        ToolWorkbenchPipelineStepItem step,
        Func<string, bool> selectPipelineStep)
        : base(workbench, step, selectPipelineStep, "xyz-affine-apply", "Apply XYZ Affine Tool Lab requires an Apply XYZ Affine step.")
    {
        InitializeComponent();
        DataContext = Workbench;
        SourceViewerHost.Content = sourceViewer;
        OutputViewerHost.Content = outputViewer;
        OwnViewer(sourceViewer);
        OwnViewer(outputViewer);
        Workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        Workbench.Source.PropertyChanged += OnSourcePropertyChanged;
        Workbench.CompareCandidates.CollectionChanged += OnCompareCandidatesChanged;
        Workbench.ArtifactRegistry.CollectionChanged += OnCompareCandidatesChanged;
    }

    public override void RefreshViews()
    {
        if (IsDisposed) return;
        RefreshSourceViewer(forceReload: true);
        UpdateOutputViewer();
    }

    private void RefreshSourceViewer(bool forceReload = false)
    {
        if (IsDisposed) return;
        if (!TryCaptureSourceInput(out var input))
        {
            ClearSourceViewer();
            return;
        }
        var cacheHit = !forceReload && displayedSource is { } previous && previous.Input == input
            && sourceViewer.TryGetCurrentC3DSourceBinding(input.Path, out var currentBinding)
            && currentBinding == previous.Binding;
        if (!cacheHit)
        {
            displayedSource = null;
            var loaded = sourceViewer.ShowC3DWorkbenchResult(input.Path, Workbench.Localization.Resolve(
                "ThreeD.ToolLab.AffineApply.SourceStatus", "원본 높이 데이터 · 열/원시 높이/행", "Raw height data · column/raw height/row"));
            if (!loaded || !sourceViewer.TryGetCurrentC3DSourceBinding(input.Path, out var binding)
                || binding.ContentSha256.Length != 64
                || !TryCaptureSourceInput(out var afterLoad) || afterLoad != input
                || input.Binding is { } admitted
                    && (!string.Equals(admitted.ContentSha256, binding.ContentSha256, StringComparison.OrdinalIgnoreCase)
                        || admitted.GridWidth != binding.GridWidth || admitted.GridHeight != binding.GridHeight))
            {
                ClearSourceViewer();
                return;
            }
            displayedSource = new(input, binding);
        }
        SourceViewerHost.Content = sourceViewer;
    }

    private bool TryCaptureSourceInput(out SourceDisplayInput input)
    {
        input = null!;
        try
        {
            var path = Path.GetFullPath(Workbench.Source.Path);
            var file = new FileInfo(path);
            if (!file.Exists) return false;
            input = new(path, Workbench.Source.Id, Workbench.Source.Format, Workbench.Source.Unit,
                Workbench.Source.FrameId, Workbench.SourceSession.SourceBinding, file.Length, file.LastWriteTimeUtc.Ticks);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private void ClearSourceViewer()
    {
        displayedSource = null;
        if (sourceViewer.CurrentC3DSourcePath is not null)
            sourceViewer.ClearC3DTeachingSource(Workbench.Localization.ViewerAuxiliaryNoOutput);
        SourceViewerHost.Content = null;
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
        outputViewer.ClearC3DTeachingSource(Workbench.Localization.Resolve(
            "ThreeD.ToolLab.AffineApply.NoOutput", "변환된 점군 결과 없음", "No transformed point cloud result"));
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        if (args.PropertyName is nameof(ToolWorkbenchViewModel.CurrentAffineApplyOutput)
            or nameof(ToolWorkbenchViewModel.IsAffineApplyPreviewPublished)
            or nameof(ToolWorkbenchViewModel.AffineApplyExecutionSummary))
        {
            QueueRefreshViews();
        }
        else if (args.PropertyName == nameof(ToolWorkbenchViewModel.SourceContextSummary))
        {
            QueueSourceRefreshViews();
        }
    }

    private void OnSourcePropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        QueueSourceRefreshViews();

    private void OnCompareCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        QueueSourceRefreshViews();

    private void QueueSourceRefreshViews()
    {
        if (IsDisposed) return;
        lock (refreshViewsOperationGate) refreshSourceRequested = true;
        QueueRefreshViews();
    }

    private void QueueRefreshViews()
    {
        if (IsDisposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        lock (refreshViewsOperationGate)
        {
            if (refreshViewsOperation?.Status is DispatcherOperationStatus.Pending or DispatcherOperationStatus.Executing)
            {
                return;
            }

            try
            {
                refreshViewsOperation = Dispatcher.BeginInvoke(
                    DispatcherPriority.DataBind,
                    new Action(RefreshViewsFromDispatcher));
            }
            catch (InvalidOperationException)
            {
                refreshViewsOperation = null;
            }
        }
    }

    private void RefreshViewsFromDispatcher()
    {
        bool refreshSource;
        lock (refreshViewsOperationGate)
        {
            refreshViewsOperation = null;
            refreshSource = refreshSourceRequested;
            refreshSourceRequested = false;
        }

        if (!IsDisposed)
        {
            if (refreshSource) RefreshSourceViewer();
            UpdateOutputViewer();
        }
    }

    private void CancelPendingRefreshViews()
    {
        DispatcherOperation? operation;
        lock (refreshViewsOperationGate)
        {
            operation = refreshViewsOperation;
            refreshViewsOperation = null;
            refreshSourceRequested = false;
        }

        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }

    private void RefreshViewsButton_Click(object sender, RoutedEventArgs args) => RefreshViews();

    public override void Dispose()
    {
        if (IsDisposed) return;
        CancelPendingRefreshViews();
        Workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
        Workbench.Source.PropertyChanged -= OnSourcePropertyChanged;
        Workbench.CompareCandidates.CollectionChanged -= OnCompareCandidatesChanged;
        Workbench.ArtifactRegistry.CollectionChanged -= OnCompareCandidatesChanged;
        displayedSource = null;
        SourceViewerHost.Content = null;
        base.Dispose();
    }
}

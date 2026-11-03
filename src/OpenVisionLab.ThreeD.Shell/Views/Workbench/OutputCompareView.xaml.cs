using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class OutputCompareView : UserControl, IDisposable
{
    private ToolWorkbenchViewModel? workbench;
    private OpenVisionThreeDViewerControl? slotAViewer;
    private OpenVisionThreeDViewerControl? slotBViewer;
    private OpenVisionThreeDViewerControl? slotCViewer;
    private LoadedDisplay? slotALoadedDisplay;
    private LoadedDisplay? slotBLoadedDisplay;
    private LoadedDisplay? slotCLoadedDisplay;
    private readonly object refreshOperationGate = new();
    private DispatcherOperation? refreshOperation;
    private int disposalState;

    private sealed record DisplayInput(
        string Path, string CandidateId, string Contract, bool IsSource,
        ToolRecipeSelectionSourceBinding? SourceBinding, string RootSourceId,
        string Unit, string FrameId, string? ArtifactHash, int? Width, int? Height,
        long Length, long LastWriteTicks)
    {
        public bool HasAdmittedIdentity => SourceBinding?.ContentSha256.Length == 64 || ArtifactHash?.Length == 64;
    }

    private sealed record LoadedDisplay(DisplayInput Input, ToolRecipeSelectionSourceBinding Binding);

    public OutputCompareView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    /// <summary>
    /// Releases the compare-slot Viewer controls owned by this view. Unloaded
    /// remains a reversible WPF event; the composition owner calls this only
    /// when the Shell closes.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        CancelPendingRefresh();
        DetachWorkbench();
        DataContextChanged -= OnDataContextChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        DisposeSlot(SlotAViewerHost, ref slotAViewer, ref slotALoadedDisplay);
        DisposeSlot(SlotBViewerHost, ref slotBViewer, ref slotBLoadedDisplay);
        DisposeSlot(SlotCViewerHost, ref slotCViewer, ref slotCLoadedDisplay);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!IsDisposed)
        {
            CancelPendingRefresh();
            RefreshCompareViews(revalidateUnknown: true);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) => CancelPendingRefresh();

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        CancelPendingRefresh();
        DetachWorkbench();
        workbench = args.NewValue as ToolWorkbenchViewModel;
        if (workbench is null)
        {
            RefreshCompareViews();
            return;
        }

        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        workbench.CompareCandidates.CollectionChanged += OnCompareCandidatesChanged;
        workbench.ArtifactRegistry.CollectionChanged += OnCompareCandidatesChanged;
        RefreshCompareViews(revalidateUnknown: true);
    }

    private void DetachWorkbench()
    {
        if (workbench is null)
        {
            return;
        }

        workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
        workbench.CompareCandidates.CollectionChanged -= OnCompareCandidatesChanged;
        workbench.ArtifactRegistry.CollectionChanged -= OnCompareCandidatesChanged;
        workbench = null;
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        if (args.PropertyName is nameof(ToolWorkbenchViewModel.CompareSlotAArtifactId)
            or nameof(ToolWorkbenchViewModel.CompareSlotBArtifactId)
            or nameof(ToolWorkbenchViewModel.CompareSlotCArtifactId)
            or nameof(ToolWorkbenchViewModel.SourceContextSummary))
        {
            QueueRefresh();
        }
    }

    private void OnCompareCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (!IsDisposed)
        {
            QueueRefresh();
        }
    }

    private void QueueRefresh()
    {
        if (IsDisposed || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
        lock (refreshOperationGate)
        {
            if (refreshOperation?.Status is DispatcherOperationStatus.Pending or DispatcherOperationStatus.Executing) return;
            try
            {
                // Wait until the model has restored the pins after collection Reset.
                refreshOperation = Dispatcher.BeginInvoke(DispatcherPriority.DataBind, new Action(RefreshFromDispatcher));
            }
            catch (InvalidOperationException) { refreshOperation = null; }
        }
    }

    private void RefreshFromDispatcher()
    {
        lock (refreshOperationGate) refreshOperation = null;
        if (!IsDisposed && IsLoaded) RefreshCompareViews();
    }

    private void CancelPendingRefresh()
    {
        DispatcherOperation? operation;
        lock (refreshOperationGate)
        {
            operation = refreshOperation;
            refreshOperation = null;
        }
        if (operation?.Status == DispatcherOperationStatus.Pending) operation.Abort();
    }

    private void RefreshCompareViews(bool revalidateUnknown = false)
    {
        if (IsDisposed)
        {
            return;
        }

        RefreshSlot(SlotAViewerHost, SlotAEmptyText, workbench?.CompareSlotAArtifactId, ref slotAViewer, ref slotALoadedDisplay, revalidateUnknown);
        RefreshSlot(SlotBViewerHost, SlotBEmptyText, workbench?.CompareSlotBArtifactId, ref slotBViewer, ref slotBLoadedDisplay, revalidateUnknown);
        RefreshSlot(SlotCViewerHost, SlotCEmptyText, workbench?.CompareSlotCArtifactId, ref slotCViewer, ref slotCLoadedDisplay, revalidateUnknown);
    }

    private void RefreshSlot(
        ContentControl host,
        TextBlock emptyText,
        string? artifactId,
        ref OpenVisionThreeDViewerControl? viewer,
        ref LoadedDisplay? loadedDisplay,
        bool revalidateUnknown)
    {
        if (IsDisposed)
        {
            return;
        }

        var candidate = workbench?.GetCompareCandidate(artifactId);
        if (candidate is null || !TryCaptureInput(candidate, out var input))
        {
            ClearSlot(host, emptyText, viewer, ref loadedDisplay,
                workbench?.Localization.OutputCompareNoSelection ?? "No output pinned");
            return;
        }

        viewer ??= new OpenVisionThreeDViewerControl { SidePanelsVisible = false };
        var cacheHit = loadedDisplay is { } previous && previous.Input == input
            && (!revalidateUnknown || input.HasAdmittedIdentity)
            && viewer.TryGetCurrentC3DSourceBinding(input.Path, out var currentBinding)
            && currentBinding == previous.Binding;
        if (!cacheHit)
        {
            loadedDisplay = null;
            var loaded = candidate.IsSource
                ? viewer.LoadC3DSource(input.Path)
                : viewer.ShowC3DWorkbenchResult(input.Path, $"{candidate.DisplayName} | {candidate.State}");
            var currentCandidate = workbench?.GetCompareCandidate(artifactId);
            if (!loaded
                || !viewer.TryGetCurrentC3DSourceBinding(input.Path, out var binding)
                || binding.ContentSha256.Length != 64
                || currentCandidate is null
                || !TryCaptureInput(currentCandidate, out var afterLoad)
                || afterLoad != input
                || input.SourceBinding is { } admitted
                    && (!string.Equals(admitted.ContentSha256, binding.ContentSha256, StringComparison.OrdinalIgnoreCase)
                        || admitted.GridWidth != binding.GridWidth || admitted.GridHeight != binding.GridHeight))
            {
                ClearSlot(host, emptyText, viewer, ref loadedDisplay,
                    workbench?.Localization.ViewerAuxiliaryNoOutput ?? "No real 3D output is available");
                return;
            }

            // The artifact's logical hash is distinct from the loaded C3D byte identity.
            loadedDisplay = new(input, binding);
        }

        viewer.TrySetHudDetailsVisible(false);
        host.Content = viewer;
        emptyText.Visibility = Visibility.Collapsed;
    }

    private bool TryCaptureInput(ToolWorkbenchCompareCandidateItem candidate, out DisplayInput input)
    {
        input = null!;
        try
        {
            var path = Path.GetFullPath(candidate.C3DPath);
            var file = new FileInfo(path);
            if (!file.Exists) return false;
            var artifact = workbench?.ArtifactRegistry.FirstOrDefault(item =>
                string.Equals(item.Id, candidate.Id, StringComparison.OrdinalIgnoreCase));
            var source = candidate.IsSource ? workbench?.SourceSession.SourceBinding : null;
            input = new(path, candidate.Id, candidate.Contract, candidate.IsSource, source,
                candidate.IsSource ? workbench?.Source.Id ?? string.Empty : artifact?.RootSourceId ?? string.Empty,
                candidate.IsSource ? workbench?.Source.Unit ?? string.Empty : artifact?.Unit ?? string.Empty,
                candidate.IsSource ? workbench?.Source.FrameId ?? string.Empty : artifact?.FrameId ?? string.Empty,
                artifact?.ContentSha256, source?.GridWidth ?? artifact?.GridWidth, source?.GridHeight ?? artifact?.GridHeight,
                file.Length, file.LastWriteTimeUtc.Ticks);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static void ClearSlot(ContentControl host, TextBlock emptyText,
        OpenVisionThreeDViewerControl? viewer, ref LoadedDisplay? loadedDisplay, string status)
    {
        loadedDisplay = null;
        if (viewer?.CurrentC3DSourcePath is not null) viewer.ClearC3DTeachingSource(status);
        host.Content = null;
        emptyText.Text = status;
        emptyText.Visibility = Visibility.Visible;
    }

    private static void DisposeSlot(
        ContentControl host,
        ref OpenVisionThreeDViewerControl? viewer,
        ref LoadedDisplay? loadedDisplay)
    {
        host.Content = null;
        viewer?.Dispose();
        viewer = null;
        loadedDisplay = null;
    }
}

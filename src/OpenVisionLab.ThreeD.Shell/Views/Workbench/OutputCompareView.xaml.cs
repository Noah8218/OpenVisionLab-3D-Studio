using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class OutputCompareView : UserControl, IDisposable
{
    private ToolWorkbenchViewModel? workbench;
    private OpenVisionThreeDViewerControl? slotAViewer;
    private OpenVisionThreeDViewerControl? slotBViewer;
    private OpenVisionThreeDViewerControl? slotCViewer;
    private string slotALoadedPath = string.Empty;
    private string slotBLoadedPath = string.Empty;
    private string slotCLoadedPath = string.Empty;
    private int disposalState;

    public OutputCompareView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
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

        DetachWorkbench();
        DataContextChanged -= OnDataContextChanged;
        Loaded -= OnLoaded;
        DisposeSlot(SlotAViewerHost, ref slotAViewer, ref slotALoadedPath);
        DisposeSlot(SlotBViewerHost, ref slotBViewer, ref slotBLoadedPath);
        DisposeSlot(SlotCViewerHost, ref slotCViewer, ref slotCLoadedPath);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (!IsDisposed)
        {
            RefreshCompareViews();
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        DetachWorkbench();
        workbench = args.NewValue as ToolWorkbenchViewModel;
        if (workbench is null)
        {
            RefreshCompareViews();
            return;
        }

        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        workbench.CompareCandidates.CollectionChanged += OnCompareCandidatesChanged;
        RefreshCompareViews();
    }

    private void DetachWorkbench()
    {
        if (workbench is null)
        {
            return;
        }

        workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
        workbench.CompareCandidates.CollectionChanged -= OnCompareCandidatesChanged;
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
            or nameof(ToolWorkbenchViewModel.CompareSlotCArtifactId))
        {
            RefreshCompareViews();
        }
    }

    private void OnCompareCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (!IsDisposed)
        {
            RefreshCompareViews();
        }
    }

    private void RefreshCompareViews()
    {
        if (IsDisposed)
        {
            return;
        }

        RefreshSlot(SlotAViewerHost, SlotAEmptyText, workbench?.CompareSlotAArtifactId, ref slotAViewer, ref slotALoadedPath);
        RefreshSlot(SlotBViewerHost, SlotBEmptyText, workbench?.CompareSlotBArtifactId, ref slotBViewer, ref slotBLoadedPath);
        RefreshSlot(SlotCViewerHost, SlotCEmptyText, workbench?.CompareSlotCArtifactId, ref slotCViewer, ref slotCLoadedPath);
    }

    private void RefreshSlot(
        ContentControl host,
        TextBlock emptyText,
        string? artifactId,
        ref OpenVisionThreeDViewerControl? viewer,
        ref string loadedPath)
    {
        if (IsDisposed)
        {
            return;
        }

        var candidate = workbench?.GetCompareCandidate(artifactId);
        if (candidate is null || !File.Exists(candidate.C3DPath))
        {
            host.Content = null;
            loadedPath = string.Empty;
            emptyText.Text = workbench?.Localization.OutputCompareNoSelection ?? "No output pinned";
            emptyText.Visibility = Visibility.Visible;
            return;
        }

        viewer ??= new OpenVisionThreeDViewerControl { SidePanelsVisible = false };
        host.Content = viewer;
        emptyText.Visibility = Visibility.Collapsed;
        if (string.Equals(loadedPath, candidate.C3DPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (candidate.IsSource)
        {
            viewer.LoadC3DSource(candidate.C3DPath);
        }
        else
        {
            viewer.ShowC3DWorkbenchResult(candidate.C3DPath, $"{candidate.DisplayName} | {candidate.State}");
        }

        viewer.ViewModel.HudDetailsVisible = false;
        loadedPath = candidate.C3DPath;
    }

    private static void DisposeSlot(
        ContentControl host,
        ref OpenVisionThreeDViewerControl? viewer,
        ref string loadedPath)
    {
        host.Content = null;
        viewer?.Dispose();
        viewer = null;
        loadedPath = string.Empty;
    }
}

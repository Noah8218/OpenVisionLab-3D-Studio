using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class OutputCompareView : UserControl
{
    private ToolWorkbenchViewModel? workbench;
    private OpenVisionThreeDViewerControl? slotAViewer;
    private OpenVisionThreeDViewerControl? slotBViewer;
    private OpenVisionThreeDViewerControl? slotCViewer;
    private string slotALoadedPath = string.Empty;
    private string slotBLoadedPath = string.Empty;
    private string slotCLoadedPath = string.Empty;

    public OutputCompareView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RefreshCompareViews();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
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
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.CompareSlotAArtifactId)
            or nameof(ToolWorkbenchViewModel.CompareSlotBArtifactId)
            or nameof(ToolWorkbenchViewModel.CompareSlotCArtifactId))
        {
            RefreshCompareViews();
        }
    }

    private void OnCompareCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        RefreshCompareViews();

    private void RefreshCompareViews()
    {
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
}

using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerWorkspaceView : UserControl, IDisposable
{
    public static readonly DependencyProperty MainViewerContentProperty =
        DependencyProperty.Register(
            nameof(MainViewerContent),
            typeof(object),
            typeof(ViewerWorkspaceView),
            new PropertyMetadata(null, OnMainViewerContentChanged));

    private ToolWorkbenchViewModel? workbench;
    private OpenVisionThreeDViewerControl? mainViewer;
    private OpenVisionThreeDViewerControl? auxiliaryViewer;
    private HeightImageViewerView? heightImageViewer;
    private ViewerWorkspacePopoutWindow? popout;
    private Window? ownerWindow;
    private readonly ViewerWorkspaceLinkCoordinator linkCoordinator = new();
    private string loadedAuxiliaryPath = string.Empty;
    private bool subscriptionsAttached;
    private bool roiFocusRatioApplied;
    private ViewerWorkspaceLayout roiFocusLayout;
    private GridLength roiFocusFirstLength;
    private GridLength roiFocusSecondLength;
    private int disposalState;

    public ViewerWorkspaceView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public object? MainViewerContent
    {
        get => GetValue(MainViewerContentProperty);
        set => SetValue(MainViewerContentProperty, value);
    }

    /// <summary>
    /// Releases resources created by this workspace at the owning Window close
    /// boundary. Transient UserControl unloads intentionally remain reversible.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        DetachSubscriptions();
        AttachMainViewer(null);

        var currentOwner = ownerWindow;
        ownerWindow = null;
        if (currentOwner is not null)
        {
            currentOwner.Closed -= OnOwnerClosed;
        }

        var currentPopout = popout;
        popout = null;
        if (currentPopout is not null)
        {
            currentPopout.Dismissed -= OnPopoutDismissed;
            currentPopout.ReleaseViewerContent();
            try
            {
                currentPopout.CloseForOwner();
            }
            catch (InvalidOperationException)
            {
                // The owner may have already closed the child Window.
            }
        }

        AuxiliaryViewerHost.Content = null;
        AuxiliaryViewerPresentationBar.ViewerHost = null;
        AuxiliaryEmptyText.Visibility = Visibility.Visible;
        heightImageViewer = null;
        loadedAuxiliaryPath = string.Empty;

        var currentAuxiliaryViewer = auxiliaryViewer;
        auxiliaryViewer = null;
        currentAuxiliaryViewer?.Dispose();

        workbench = null;
        DataContextChanged -= OnDataContextChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        linkCoordinator.Dispose();
    }

    public bool IsPopoutVisible => popout?.IsVisible == true;

    public Window? PopoutWindow => popout;

    internal bool HasAttachedMainViewer => mainViewer is not null;

    public bool HasLayoutToolbarAndTwoSlots =>
        ViewerLayoutToolbar is not null
        && MainSlot is not null
        && AuxiliarySlot is not null;

    public bool IsAuxiliaryInlineVisible =>
        AuxiliarySlot.Visibility == Visibility.Visible
        && workbench?.ViewerWorkspace.IsInlineSplit == true;

    public bool IsInputFirstActionVisible =>
        ViewerInputFirstAction.Visibility == Visibility.Visible;

    public bool HasCoordinateTrueHeightImage =>
        heightImageViewer?.HasNativeCoordinateImage == true;

    public bool VerifyLinkedCameraPropagationForSmoke(out string summary)
    {
        var currentWorkbench = workbench;
        if (currentWorkbench?.ViewerWorkspace.IsCameraLinked != true
            || mainViewer is null
            || auxiliaryViewer is null)
        {
            summary = "CameraLinkPropagation|failure=linked-viewers-unavailable";
            return false;
        }

        var originalMain = mainViewer.CaptureCameraState();
        var originalAuxiliary = auxiliaryViewer.CaptureCameraState();
        var originalLinked = currentWorkbench.ViewerWorkspace.IsCameraLinked;
        var linkedState = new ViewerCameraState(
            41.0,
            27.0,
            8.5,
            0.5,
            0.6,
            0.7,
            ViewerProjectionMode.Perspective,
            10.0);
        var unlinkedState = linkedState with { YawDegrees = 63.0 };
        var linkedCopyPassed = false;
        var unlinkDivergencePassed = false;
        var relinkCopyPassed = false;
        try
        {
            linkedCopyPassed = mainViewer.TryApplyCameraState(linkedState)
                && auxiliaryViewer.CaptureCameraState() == linkedState;

            currentWorkbench.ViewerWorkspace.SetCameraLinked(false);
            var auxiliaryBeforeUnlinkedChange = auxiliaryViewer.CaptureCameraState();
            unlinkDivergencePassed = mainViewer.TryApplyCameraState(unlinkedState)
                && auxiliaryViewer.CaptureCameraState() == auxiliaryBeforeUnlinkedChange
                && auxiliaryViewer.CaptureCameraState() != unlinkedState;

            currentWorkbench.ViewerWorkspace.SetCameraLinked(true);
            relinkCopyPassed = auxiliaryViewer.CaptureCameraState()
                == mainViewer.CaptureCameraState();

            summary =
                $"CameraLinkPropagation|linkedCopy={linkedCopyPassed}|unlinkDivergence={unlinkDivergencePassed}|relinkCopy={relinkCopyPassed}";
            return linkedCopyPassed && unlinkDivergencePassed && relinkCopyPassed;
        }
        finally
        {
            currentWorkbench.ViewerWorkspace.SetCameraLinked(originalLinked);
            if (originalLinked)
            {
                mainViewer.TryApplyCameraState(originalMain);
            }
            else
            {
                mainViewer.TryApplyCameraState(originalMain);
                auxiliaryViewer.TryApplyCameraState(originalAuxiliary);
            }
        }
    }

    public bool ReactivateMainViewer(object? requestedContent)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return false;
        }

        var viewer = requestedContent as OpenVisionThreeDViewerControl;
        if (viewer is null)
        {
            return false;
        }

        SetCurrentValue(MainViewerContentProperty, viewer);
        AttachMainViewer(viewer);
        viewer.RequestVisibleFrame();
        return true;
    }

    public bool ReleaseMainViewer(object? requestedContent)
    {
        if (requestedContent is not OpenVisionThreeDViewerControl viewer)
        {
            return false;
        }

        var ownsViewer = ReferenceEquals(mainViewer, viewer)
            || ReferenceEquals(MainViewerContent, viewer)
            || ReferenceEquals(MainViewerPresenter.Content, viewer);
        if (!ownsViewer)
        {
            return false;
        }

        SetCurrentValue(MainViewerContentProperty, null);
        MainViewerPresenter.SetCurrentValue(
            ContentPresenter.ContentProperty,
            null);
        AttachMainViewer(null);
        return mainViewer is null
            && MainViewerContent is null
            && MainViewerPresenter.Content is null;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        DetachSubscriptions();
        workbench = args.NewValue as ToolWorkbenchViewModel;
        AttachSubscriptions();
        RefreshWorkspace();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        AttachSubscriptions();
        RefreshWorkspace();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        DetachSubscriptions();

    private void AttachSubscriptions()
    {
        if (Volatile.Read(ref disposalState) != 0
            || subscriptionsAttached
            || workbench is null)
        {
            return;
        }

        workbench.ViewerWorkspace.PropertyChanged += OnViewerWorkspacePropertyChanged;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        workbench.CompareCandidates.CollectionChanged += OnCompareCandidatesChanged;
        linkCoordinator.SetWorkbench(workbench);
        AttachMainViewer(MainViewerContent as OpenVisionThreeDViewerControl);
        subscriptionsAttached = true;
    }

    private void DetachSubscriptions()
    {
        if (workbench is null && !subscriptionsAttached)
        {
            linkCoordinator.SetWorkbench(null);
            linkCoordinator.SetMainViewer(null);
            return;
        }

        if (workbench is not null)
        {
            workbench.ViewerWorkspace.PropertyChanged -= OnViewerWorkspacePropertyChanged;
            workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
            workbench.CompareCandidates.CollectionChanged -= OnCompareCandidatesChanged;
        }

        linkCoordinator.SetWorkbench(null);
        AttachMainViewer(null);
        subscriptionsAttached = false;
    }

    private static void OnMainViewerContentChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var view = (ViewerWorkspaceView)dependencyObject;
        if (Volatile.Read(ref view.disposalState) != 0)
        {
            return;
        }

        view.AttachMainViewer(args.NewValue as OpenVisionThreeDViewerControl);
    }

    private void AttachMainViewer(OpenVisionThreeDViewerControl? viewer)
    {
        if (ReferenceEquals(mainViewer, viewer))
        {
            if (!ReferenceEquals(MainViewerPresenter.Content, viewer))
            {
                MainViewerPresenter.Content = viewer;
            }

            linkCoordinator.SetMainViewer(viewer);
            return;
        }

        mainViewer = viewer;
        MainViewerPresenter.Content = viewer;
        linkCoordinator.SetMainViewer(mainViewer);

        RefreshMainViewer();
    }

    private void OnViewerWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ViewerWorkspaceSession.Layout)
            or nameof(ViewerWorkspaceSession.MainContentId)
            or nameof(ViewerWorkspaceSession.AuxiliaryContentId)
            or nameof(ViewerWorkspaceSession.IsMainContentExplicitlyCleared)
            or nameof(ViewerWorkspaceSession.IsAuxiliaryContentExplicitlyCleared)
            or nameof(ViewerWorkspaceSession.IsCameraLinked))
        {
            RefreshWorkspace();
        }
    }

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ToolWorkbenchViewModel.IsTeachingSelectionCaptureActive))
        {
            UpdateRoiFocusRatio();
        }
    }

    private void OnCompareCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        RefreshWorkspace();

    private void RefreshWorkspace()
    {
        ApplyLayout(workbench?.ViewerWorkspace.Layout ?? ViewerWorkspaceLayout.Single);
        RefreshMainViewer();
        RefreshAuxiliaryViewer();
        UpdateRoiFocusRatio();
        linkCoordinator.Refresh();
    }

    private void RefreshMainViewer()
    {
        var currentWorkbench = workbench;
        var currentViewer = mainViewer;
        var candidate = currentWorkbench?.GetMainViewerCandidate(
            currentWorkbench.ViewerWorkspace.MainContentId);
        if (currentWorkbench is null
            || currentViewer is null
            || candidate is null
            || !File.Exists(candidate.SourcePath))
        {
            return;
        }

        var currentPath = currentViewer.CurrentC3DSourcePath;
        if (string.Equals(
                currentPath,
                Path.GetFullPath(candidate.SourcePath),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (candidate.IsSource)
        {
            currentViewer.LoadC3DSource(candidate.SourcePath);
        }
        else
        {
            currentViewer.ShowC3DWorkbenchResult(
                candidate.SourcePath,
                $"{candidate.DisplayName} | {candidate.State}");
        }
    }

    private void ApplyLayout(ViewerWorkspaceLayout layout)
    {
        roiFocusRatioApplied = false;
        FirstRow.Height = new GridLength(1, GridUnitType.Star);
        MiddleRow.Height = new GridLength(0);
        SecondRow.Height = new GridLength(0);
        FirstColumn.Width = new GridLength(1, GridUnitType.Star);
        MiddleColumn.Width = new GridLength(0);
        SecondColumn.Width = new GridLength(0);
        Grid.SetRow(MainSlot, 0);
        Grid.SetColumn(MainSlot, 0);
        Grid.SetRowSpan(MainSlot, 3);
        Grid.SetColumnSpan(MainSlot, 3);
        AuxiliarySlot.Visibility = Visibility.Collapsed;
        ViewerSplitter.Visibility = Visibility.Collapsed;
        AuxiliaryViewerHost.Content = null;

        switch (layout)
        {
            case ViewerWorkspaceLayout.SplitVertical:
                HidePopout();
                FirstColumn.Width = new GridLength(1, GridUnitType.Star);
                MiddleColumn.Width = new GridLength(6);
                SecondColumn.Width = new GridLength(1, GridUnitType.Star);
                Grid.SetRowSpan(MainSlot, 3);
                Grid.SetColumnSpan(MainSlot, 1);
                Grid.SetRow(AuxiliarySlot, 0);
                Grid.SetColumn(AuxiliarySlot, 2);
                Grid.SetRowSpan(AuxiliarySlot, 3);
                Grid.SetColumnSpan(AuxiliarySlot, 1);
                Grid.SetRow(ViewerSplitter, 0);
                Grid.SetColumn(ViewerSplitter, 1);
                Grid.SetRowSpan(ViewerSplitter, 3);
                ViewerSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                ViewerSplitter.VerticalAlignment = VerticalAlignment.Stretch;
                ViewerSplitter.ResizeDirection = GridResizeDirection.Columns;
                ViewerSplitter.Width = 6;
                ViewerSplitter.Height = double.NaN;
                AuxiliarySlot.Visibility = Visibility.Visible;
                ViewerSplitter.Visibility = Visibility.Visible;
                break;

            case ViewerWorkspaceLayout.SplitHorizontal:
                HidePopout();
                FirstRow.Height = new GridLength(1, GridUnitType.Star);
                MiddleRow.Height = new GridLength(6);
                SecondRow.Height = new GridLength(1, GridUnitType.Star);
                Grid.SetRowSpan(MainSlot, 1);
                Grid.SetColumnSpan(MainSlot, 3);
                Grid.SetRow(AuxiliarySlot, 2);
                Grid.SetColumn(AuxiliarySlot, 0);
                Grid.SetRowSpan(AuxiliarySlot, 1);
                Grid.SetColumnSpan(AuxiliarySlot, 3);
                Grid.SetRow(ViewerSplitter, 1);
                Grid.SetColumn(ViewerSplitter, 0);
                Grid.SetColumnSpan(ViewerSplitter, 3);
                ViewerSplitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                ViewerSplitter.VerticalAlignment = VerticalAlignment.Stretch;
                ViewerSplitter.ResizeDirection = GridResizeDirection.Rows;
                ViewerSplitter.Width = double.NaN;
                ViewerSplitter.Height = 6;
                AuxiliarySlot.Visibility = Visibility.Visible;
                ViewerSplitter.Visibility = Visibility.Visible;
                break;

            case ViewerWorkspaceLayout.PopOut:
                ShowPopout();
                break;

            default:
                HidePopout();
                break;
        }
    }

    private void UpdateRoiFocusRatio()
    {
        var layout = workbench?.ViewerWorkspace.Layout ?? ViewerWorkspaceLayout.Single;
        var auxiliary = workbench?.GetViewerWorkspaceCandidate(
            workbench.ViewerWorkspace.AuxiliaryContentId);
        var shouldFocus = workbench?.IsTeachingSelectionCaptureActive == true
                          && auxiliary?.Kind == ViewerWorkspaceCandidateKind.HeightImage
                          && layout is ViewerWorkspaceLayout.SplitVertical
                              or ViewerWorkspaceLayout.SplitHorizontal;
        if (!shouldFocus)
        {
            RestoreRoiFocusRatio(layout);
            return;
        }

        if (!roiFocusRatioApplied || roiFocusLayout != layout)
        {
            roiFocusLayout = layout;
            if (layout == ViewerWorkspaceLayout.SplitVertical)
            {
                roiFocusFirstLength = FirstColumn.Width;
                roiFocusSecondLength = SecondColumn.Width;
            }
            else
            {
                roiFocusFirstLength = FirstRow.Height;
                roiFocusSecondLength = SecondRow.Height;
            }

            roiFocusRatioApplied = true;
        }

        if (layout == ViewerWorkspaceLayout.SplitVertical)
        {
            FirstColumn.Width = new GridLength(35, GridUnitType.Star);
            SecondColumn.Width = new GridLength(65, GridUnitType.Star);
        }
        else
        {
            FirstRow.Height = new GridLength(35, GridUnitType.Star);
            SecondRow.Height = new GridLength(65, GridUnitType.Star);
        }
    }

    private void RestoreRoiFocusRatio(ViewerWorkspaceLayout layout)
    {
        if (!roiFocusRatioApplied)
        {
            return;
        }

        if (layout == roiFocusLayout)
        {
            if (layout == ViewerWorkspaceLayout.SplitVertical)
            {
                FirstColumn.Width = roiFocusFirstLength;
                SecondColumn.Width = roiFocusSecondLength;
            }
            else if (layout == ViewerWorkspaceLayout.SplitHorizontal)
            {
                FirstRow.Height = roiFocusFirstLength;
                SecondRow.Height = roiFocusSecondLength;
            }
        }

        roiFocusRatioApplied = false;
    }

    private void RefreshAuxiliaryViewer()
    {
        var currentWorkbench = workbench;
        var candidate = currentWorkbench?.GetViewerWorkspaceCandidate(
            currentWorkbench.ViewerWorkspace.AuxiliaryContentId);
        if (currentWorkbench is null || candidate is null || !File.Exists(candidate.SourcePath))
        {
            loadedAuxiliaryPath = string.Empty;
            AuxiliaryViewerHost.Content = null;
            AuxiliaryViewerPresentationBar.ViewerHost = null;
            AuxiliaryEmptyText.Visibility = Visibility.Visible;
            popout?.SetViewerContent(
                null,
                currentWorkbench?.Localization.ViewerAuxiliaryNoOutput ?? "No real 3D output is available");
            return;
        }

        if (candidate.Kind == ViewerWorkspaceCandidateKind.HeightImage)
        {
            linkCoordinator.SetAuxiliaryViewer(null);
            heightImageViewer ??= new HeightImageViewerView
            {
                DataContext = currentWorkbench.HeightImageViewer
            };
            currentWorkbench.BeginHeightImageSourceLoad();
            PresentAuxiliaryContent(heightImageViewer, currentWorkbench);
            return;
        }

        auxiliaryViewer ??= new OpenVisionThreeDViewerControl
        {
            SidePanelsVisible = false
        };
        linkCoordinator.SetAuxiliaryViewer(auxiliaryViewer);
        auxiliaryViewer.TrySetHudDetailsVisible(false);
        if (!string.Equals(loadedAuxiliaryPath, candidate.SourcePath, StringComparison.OrdinalIgnoreCase))
        {
            if (candidate.IsSource)
            {
                auxiliaryViewer.LoadC3DSource(candidate.SourcePath);
            }
            else
            {
                auxiliaryViewer.ShowC3DWorkbenchResult(
                    candidate.SourcePath,
                    $"{candidate.DisplayName} | {candidate.State}");
            }

            loadedAuxiliaryPath = candidate.SourcePath;
        }

        PresentAuxiliaryContent(auxiliaryViewer, currentWorkbench);
    }

    private void PresentAuxiliaryContent(object content, ToolWorkbenchViewModel currentWorkbench)
    {
        AuxiliaryEmptyText.Visibility = Visibility.Collapsed;
        AuxiliaryViewerPresentationBar.ViewerHost =
            content is OpenVisionThreeDViewerControl viewer
                ? viewer
                : null;
        if (currentWorkbench.ViewerWorkspace.IsPopOut)
        {
            AuxiliaryViewerHost.Content = null;
            popout?.SetViewerContent(content, currentWorkbench.Localization.ViewerAuxiliaryNoOutput);
        }
        else if (currentWorkbench.ViewerWorkspace.IsInlineSplit)
        {
            popout?.ReleaseViewerContent();
            AuxiliaryViewerHost.Content = content;
        }
    }

    public Task<HeightImageRoiPointerSmokeResult> RunHeightImageRoiPointerSmokeAsync()
    {
        if (heightImageViewer is null || !heightImageViewer.IsVisible)
        {
            return Task.FromResult(new HeightImageRoiPointerSmokeResult(
                false,
                "The inline Height Image viewer is not visible.",
                null,
                null,
                default,
                default,
                string.Empty));
        }

        return heightImageViewer.RunRoiPointerSmokeAsync();
    }

    private void ShowPopout()
    {
        if (workbench is null)
        {
            return;
        }

        if (popout is null)
        {
            popout = new ViewerWorkspacePopoutWindow
            {
                DataContext = workbench
            };
            popout.Dismissed += OnPopoutDismissed;
            ownerWindow = Window.GetWindow(this);
            if (ownerWindow is not null)
            {
                popout.Owner = ownerWindow;
                ownerWindow.Closed += OnOwnerClosed;
            }
        }

        AuxiliaryViewerHost.Content = null;
        if (!popout.IsVisible)
        {
            popout.Show();
        }

        popout.Activate();
    }

    private void HidePopout()
    {
        if (popout?.IsVisible == true)
        {
            popout.Hide();
        }

        popout?.ReleaseViewerContent();
    }

    private void OnPopoutDismissed(object? sender, EventArgs args)
    {
        var command = (sender as ViewerWorkspacePopoutWindow)?.DismissedCommand;
        if (command?.CanExecute(null) == true)
        {
            command.Execute(null);
        }
    }

    private void OnOwnerClosed(object? sender, EventArgs args)
    {
        if (ownerWindow is not null)
        {
            ownerWindow.Closed -= OnOwnerClosed;
            ownerWindow = null;
        }

        if (popout is null)
        {
            return;
        }

        popout.Dismissed -= OnPopoutDismissed;
        popout.CloseForOwner();
        popout = null;
    }
}

using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerWorkspaceView : UserControl, IDisposable
{
    public static readonly DependencyProperty IsCompactChromeProperty = DependencyProperty.Register(nameof(IsCompactChrome), typeof(bool), typeof(ViewerWorkspaceView), new PropertyMetadata(true));
    public static readonly DependencyProperty SelectionContextProperty = DependencyProperty.Register(nameof(SelectionContext), typeof(string), typeof(ViewerWorkspaceView), new PropertyMetadata(string.Empty));
    public string SelectionContext => (string)GetValue(SelectionContextProperty);
    public static readonly DependencyProperty MainViewerContentProperty =
        DependencyProperty.Register(
            nameof(MainViewerContent),
            typeof(object),
            typeof(ViewerWorkspaceView),
            new PropertyMetadata(null, OnMainViewerContentChanged));

    private readonly ContextMenu workspaceContextMenu;
    private readonly ContextMenu heightImageContextMenu;
    private ToolWorkbenchViewModel? workbench;
    private OpenVisionThreeDViewerControl? mainViewer;
    private OpenVisionThreeDViewerControl? auxiliaryViewer;
    private HeightImageViewerView? heightImageViewer;
    private OpenVisionThreeDViewerControl? sectionPaneViewer;
    private OpenVisionThreeDViewerControl? heightPaneViewer;
    private ViewerWorkspacePopoutWindow? popout;
    private HeightProfileView? popoutProfile;
    private Window? ownerWindow;
    private readonly ViewerWorkspaceLinkCoordinator linkCoordinator = new();
    private string loadedAuxiliaryPath = string.Empty;
    private bool subscriptionsAttached;
    private bool roiFocusRatioApplied;
    private ViewerWorkspaceLayout roiFocusLayout;
    private GridLength roiFocusFirstLength;
    private GridLength roiFocusSecondLength;
    private int disposalState;
    private (GridLength FirstRow, GridLength SecondRow, GridLength FirstColumn, GridLength SecondColumn)? paneRestoreLengths;

    public ViewerWorkspaceView()
    {
        InitializeComponent();
        var selectionContextBinding = new MultiBinding { StringFormat = "{0} · {1}" };
        selectionContextBinding.Bindings.Add(new Binding("SelectedPipelineStep.ToolName"));
        selectionContextBinding.Bindings.Add(new Binding("HeightImageViewer.RoiWorkspace.ContextSummary"));
        SetBinding(SelectionContextProperty, selectionContextBinding);
        workspaceContextMenu = (ContextMenu)Resources["WorkbenchViewerContextMenu"];
        heightImageContextMenu = (ContextMenu)Resources["HeightImageContextMenu"];
        heightImageContextMenu.SetBinding(DataContextProperty, new Binding("DataContext") { Source = this });
        heightImageContextMenu.SetBinding(TagProperty, new Binding(nameof(ViewerPresentationBar.Presentation)) { Source = MainViewerPresentationBar });
        workspaceContextMenu.SetBinding(DataContextProperty, new Binding("DataContext") { Source = this });
        workspaceContextMenu.SetBinding(TagProperty, new Binding(nameof(ViewerPresentationBar.Presentation)) { Source = MainViewerPresentationBar });
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnChromeSizeChanged;
    }

    public object? MainViewerContent
    {
        get => GetValue(MainViewerContentProperty);
        set => SetValue(MainViewerContentProperty, value);
    }

    internal IReadOnlyDictionary<string, OpenVisionLab.ThreeD.Data.HeightDisplayProfile> CaptureDisplayProfiles() => linkCoordinator.CaptureDisplayProfiles();
    internal void RestoreDisplayProfiles(IReadOnlyDictionary<string, OpenVisionLab.ThreeD.Data.HeightDisplayProfile> profiles) => linkCoordinator.RestoreDisplayProfiles(profiles);

    public bool IsCompactChrome => (bool)GetValue(IsCompactChromeProperty);

    private void OnChromeSizeChanged(object sender, SizeChangedEventArgs args)
    {
        SetCurrentValue(IsCompactChromeProperty, (Window.GetWindow(this)?.ActualWidth ?? ActualWidth) < 1500);
        UpdateEmbeddedViewerChrome();
    }

    private void UpdateEmbeddedViewerChrome()
    {
        if (MainPaneMenuHost is null) return;
        mainViewer?.SetWorkspaceHeaderHost(MainPaneMenuHost, IsCompactChrome);
        auxiliaryViewer?.SetWorkspaceHeaderHost(IsAuxiliaryInlineVisible ? AuxiliaryPaneMenuHost : null, IsCompactChrome);
        sectionPaneViewer?.SetWorkspaceHeaderHost(FourSectionMenuHost, IsCompactChrome);
        heightPaneViewer?.SetWorkspaceHeaderHost(FourHeightMenuHost, IsCompactChrome);
    }

    private void OnViewerHelpOpened(object sender, EventArgs args) => ViewerHelpCloseButton.Focus();

    private void OnViewerHelpClosed(object sender, EventArgs args) => ViewerHelpButton.Focus();

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

        heightImageContextMenu.IsOpen = false;
        if (heightImageViewer is not null) heightImageViewer.ContextMenu = null;
        workbench = null;
        DataContextChanged -= OnDataContextChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnChromeSizeChanged;
        linkCoordinator.Dispose();
        popoutProfile?.Dispose();
        popoutProfile = null;
        FourSectionProfile.Dispose();
        FourHeightProfile.Dispose();
        MainPaneProfile.Dispose();
        AuxiliaryPaneProfile.Dispose();
        InlineHeightProfile.Dispose();
        FourSectionSlotThreeDHost.Content = null;
        FourHeightSlotThreeDHost.Content = null;
        sectionPaneViewer?.Dispose();
        heightPaneViewer?.Dispose();
        sectionPaneViewer = heightPaneViewer = null;
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
        && workbench?.EffectiveViewerLayout is ViewerWorkspaceLayout.SplitVertical
            or ViewerWorkspaceLayout.SplitHorizontal or ViewerWorkspaceLayout.Four;

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
        workbench.MainViewerPane.PropertyChanged += OnPanePropertyChanged;
        workbench.AuxiliaryViewerPane.PropertyChanged += OnPanePropertyChanged;
        workbench.SectionViewerPane.PropertyChanged += OnPanePropertyChanged;
        workbench.HeightViewerPane.PropertyChanged += OnPanePropertyChanged;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        workbench.AppliedTeachingSelectionsChanged += OnAppliedSelectionsChanged;
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
            workbench.MainViewerPane.PropertyChanged -= OnPanePropertyChanged;
            workbench.AuxiliaryViewerPane.PropertyChanged -= OnPanePropertyChanged;
            workbench.SectionViewerPane.PropertyChanged -= OnPanePropertyChanged;
            workbench.HeightViewerPane.PropertyChanged -= OnPanePropertyChanged;
            workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
            workbench.AppliedTeachingSelectionsChanged -= OnAppliedSelectionsChanged;
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

        if (workspaceContextMenu is not null) workspaceContextMenu.IsOpen = false;
        mainViewer?.SetCurrentValue(OpenVisionThreeDViewerControl.WorkspaceContextMenuProperty, null);
        mainViewer?.SetWorkspaceHeaderHost(null, false);
        if (mainViewer is not null) BindingOperations.ClearBinding(mainViewer, OpenVisionThreeDViewerControl.WorkspaceSelectionCaptionProperty);
        InlineHeightProfile.SetCurrentValue(HeightProfileView.ViewerHostProperty, viewer);
        mainViewer = viewer;
        if (mainViewer is not null) mainViewer.SetBinding(OpenVisionThreeDViewerControl.WorkspaceSelectionCaptionProperty, new Binding(nameof(SelectionContext)) { Source = this });
        MainViewerPresenter.Content = viewer;
        linkCoordinator.SetMainViewer(mainViewer);

        RefreshMainViewer();
        SyncRoiDisplay();
    }

    private void OnPanePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ViewerPaneViewModel.Kind))
        {
            RefreshAuxiliaryViewer();
            RefreshLowerViewers();
            SyncRoiDisplay();
        }
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
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.ViewerRoiScope)
            or nameof(ToolWorkbenchViewModel.SelectedPipelineStep)
            or nameof(ToolWorkbenchViewModel.SelectedStepTeachingSelection)) SyncRoiDisplay();
        if (args.PropertyName == nameof(ToolWorkbenchViewModel.IsTeachingSelectionCaptureActive))
        {
            UpdateRoiFocusRatio();
        }
        else if (args.PropertyName is nameof(ToolWorkbenchViewModel.EffectiveViewerLayout)
            or nameof(ToolWorkbenchViewModel.EffectiveMainViewerContentId)
            or nameof(ToolWorkbenchViewModel.EffectiveAuxiliaryViewerContentId))
        {
            RefreshWorkspace();
        }
        else if (args.PropertyName == nameof(ToolWorkbenchViewModel.MaximizedViewerSlotId))
        {
            ApplyPaneMaximization();
        }
    }

    private void OnCompareCandidatesChanged(object? sender, NotifyCollectionChangedEventArgs args) =>
        RefreshWorkspace();

    private void RefreshWorkspace()
    {
        ApplyLayout(workbench?.EffectiveViewerLayout ?? ViewerWorkspaceLayout.Single);
        RefreshMainViewer();
        RefreshAuxiliaryViewer();
        RefreshLowerViewers();
        SyncRoiDisplay();
        UpdateRoiFocusRatio();
        linkCoordinator.Refresh();
        ApplyPaneMaximization();
    }

    private void ApplyPaneMaximization()
    {
        if (workbench?.MaximizedViewerSlotId is not { Length: > 0 } slotId)
        {
            if (paneRestoreLengths is not { } lengths) return;
            paneRestoreLengths = null;
            var auxiliaryContent = AuxiliaryViewerHost.Content;
            ApplyLayout(workbench?.EffectiveViewerLayout ?? ViewerWorkspaceLayout.Single);
            AuxiliaryViewerHost.Content = auxiliaryContent;
            FirstRow.Height = lengths.FirstRow;
            SecondRow.Height = lengths.SecondRow;
            FirstColumn.Width = lengths.FirstColumn;
            SecondColumn.Width = lengths.SecondColumn;
            return;
        }

        paneRestoreLengths ??= (FirstRow.Height, SecondRow.Height, FirstColumn.Width, SecondColumn.Width);
        var target = slotId switch { "viewer.auxiliary" => AuxiliarySlot, "viewer.section" => FourSectionSlot, "viewer.height" => FourHeightSlot, _ => MainSlot };
        foreach (var pane in new[] { MainSlot, AuxiliarySlot, FourSectionSlot, FourHeightSlot }) pane.Visibility = ReferenceEquals(pane, target) ? Visibility.Visible : Visibility.Collapsed;
        ViewerSplitter.Visibility = Visibility.Collapsed;
        FirstRow.Height = FirstColumn.Width = new GridLength(1, GridUnitType.Star);
        MiddleRow.Height = SecondRow.Height = MiddleColumn.Width = SecondColumn.Width = new GridLength(0);
        Grid.SetRow(target, 0);
        Grid.SetColumn(target, 0);
        Grid.SetRowSpan(target, 3);
        Grid.SetColumnSpan(target, 3);
    }

    private void OnAppliedSelectionsChanged(object? sender, EventArgs args) => SyncRoiDisplay();

    private void SyncRoiDisplay()
    {
        if (workbench is null) return;
        var selections = workbench.GetCurrentAppliedTeachingSelections();
        var filter = workbench.ViewerDisplayedRoiIds;
        mainViewer?.SetTeachingSelectionDisplayFilter(filter);
        foreach (var viewer in new[] { auxiliaryViewer, sectionPaneViewer, heightPaneViewer })
        {
            if (viewer is null) continue;
            viewer.SetAppliedTeachingSelections(selections);
            viewer.SetSelectedTeachingSelection(workbench.SelectedStepTeachingSelection?.Id);
            viewer.SetTeachingSelectionDisplayFilter(filter);
        }
    }

    private void RefreshMainViewer()
    {
        UpdateEmbeddedViewerChrome();
        if (mainViewer is not null && workspaceContextMenu is not null)
        {
            workspaceContextMenu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, "OrbitHere")).SetBinding(MenuItem.CommandProperty, new Binding("DataContext.OrbitHereCommand") { Source = mainViewer });
            mainViewer.SetCurrentValue(OpenVisionThreeDViewerControl.WorkspaceContextMenuProperty, workspaceContextMenu);
        }
        var currentWorkbench = workbench;
        var currentViewer = mainViewer;
        var candidate = currentWorkbench?.GetMainViewerCandidate(
            currentWorkbench.EffectiveMainViewerContentId);
        if (currentWorkbench is null
            || currentViewer is null
            || currentWorkbench.IsC3DSourceLoading)
        {
            return;
        }

        if (candidate is null || !File.Exists(candidate.SourcePath))
        {
            if (currentViewer.CurrentC3DSourcePath is not null)
            {
                currentViewer.ClearC3DTeachingSource(currentWorkbench.MainViewerSummary);
            }
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
        var focusedViewerElement = System.Windows.Input.Keyboard.FocusedElement as UIElement;
        var hadViewerKeyboardFocus = IsKeyboardFocusWithin;
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
        MainSlot.Visibility = Visibility.Visible;
        FourSectionSlot.Visibility = FourHeightSlot.Visibility = Visibility.Collapsed;
        AuxiliarySlot.Visibility = Visibility.Collapsed;
        ViewerSplitter.Visibility = Visibility.Collapsed;
        AuxiliaryViewerHost.Content = null;

        switch (layout)
        {
            case ViewerWorkspaceLayout.HeightImageOnly:
                HidePopout();
                MainSlot.Visibility = Visibility.Collapsed;
                Grid.SetRow(AuxiliarySlot, 0);
                Grid.SetColumn(AuxiliarySlot, 0);
                Grid.SetRowSpan(AuxiliarySlot, 3);
                Grid.SetColumnSpan(AuxiliarySlot, 3);
                AuxiliarySlot.Visibility = Visibility.Visible;
                break;

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

            case ViewerWorkspaceLayout.Four:
                HidePopout();
                FirstRow.Height = SecondRow.Height = new GridLength(1, GridUnitType.Star);
                MiddleRow.Height = new GridLength(4);
                FirstColumn.Width = SecondColumn.Width = new GridLength(1, GridUnitType.Star);
                MiddleColumn.Width = new GridLength(4);
                Grid.SetRowSpan(MainSlot, 1); Grid.SetColumnSpan(MainSlot, 1);
                Grid.SetRow(AuxiliarySlot, 0); Grid.SetColumn(AuxiliarySlot, 2);
                Grid.SetRowSpan(AuxiliarySlot, 1); Grid.SetColumnSpan(AuxiliarySlot, 1);
                Grid.SetRow(FourSectionSlot, 2); Grid.SetColumn(FourSectionSlot, 0);
                Grid.SetRowSpan(FourSectionSlot, 1); Grid.SetColumnSpan(FourSectionSlot, 1);
                Grid.SetRow(FourHeightSlot, 2); Grid.SetColumn(FourHeightSlot, 2);
                Grid.SetRowSpan(FourHeightSlot, 1); Grid.SetColumnSpan(FourHeightSlot, 1);
                AuxiliarySlot.Visibility = FourSectionSlot.Visibility = FourHeightSlot.Visibility = Visibility.Visible;
                break;

            case ViewerWorkspaceLayout.PopOut:
                ShowPopout();
                break;

            default:
                HidePopout();
                break;
        }
        if (hadViewerKeyboardFocus && focusedViewerElement?.IsVisible == false) ThreeDViewerMode.Focus();
    }

    private void UpdateRoiFocusRatio()
    {
        var layout = workbench?.EffectiveViewerLayout ?? ViewerWorkspaceLayout.Single;
        var auxiliary = workbench?.GetViewerWorkspaceCandidate(
            workbench.EffectiveAuxiliaryViewerContentId);
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
            currentWorkbench.EffectiveAuxiliaryViewerContentId);
        if (currentWorkbench is null || candidate is null
            || !File.Exists(candidate.SourcePath)
                && !(candidate.Kind == ViewerWorkspaceCandidateKind.HeightImage
                    && currentWorkbench.HasEffectiveSignedDeltaOutput
                    && !currentWorkbench.IsInspectionInputView))
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

        if (currentWorkbench.AuxiliaryViewerPane.IsHeight)
        {
            linkCoordinator.SetAuxiliaryViewer(null);
            heightImageViewer ??= new HeightImageViewerView
            {
                DataContext = currentWorkbench.AuxiliaryViewerPane.HeightImage,
                UseEmbeddedChrome = true
            };
            heightImageViewer.DataContext = currentWorkbench.AuxiliaryViewerPane.HeightImage;
            heightImageViewer.ContextMenu = heightImageContextMenu;
            heightImageViewer.SetBinding(HeightImageViewerView.ProfileStateProperty, new Binding("Presentation.ProfileState") { Source = MainViewerPresentationBar });
            heightImageViewer.SetBinding(HeightImageViewerView.ProfilePickCommandProperty, new Binding("Presentation.ProfilePickCommand") { Source = MainViewerPresentationBar });
            heightImageViewer.SetBinding(HeightImageViewerView.PresentationProperty, new Binding(nameof(ViewerPresentationBar.Presentation)) { Source = MainViewerPresentationBar });
            heightImageViewer.SetBinding(HeightImageViewerView.DisplayPresentationProperty, new Binding(nameof(ViewerPresentationBar.Presentation)) { Source = AuxiliaryViewerPresentationBar });
            heightImageViewer.SetBinding(HeightImageViewerView.SelectionContextProperty, new Binding(nameof(SelectionContext)) { Source = this });
            if (ReferenceEquals(currentWorkbench.AuxiliaryViewerPane.HeightImage, currentWorkbench.HeightImageViewer)
                && (!currentWorkbench.HasEffectiveSignedDeltaOutput || currentWorkbench.IsInspectionInputView))
            {
                currentWorkbench.BeginHeightImageSourceLoad();
            }
            PresentAuxiliaryContent(heightImageViewer, currentWorkbench);
            return;
        }

        auxiliaryViewer ??= new OpenVisionThreeDViewerControl
        {
            SidePanelsVisible = false
        };
        auxiliaryViewer.SetWorkspaceHeaderHost(IsAuxiliaryInlineVisible ? AuxiliaryPaneMenuHost : null, IsCompactChrome);
        linkCoordinator.SetAuxiliaryViewer(auxiliaryViewer);
        auxiliaryViewer.TrySetHudDetailsVisible(false);
        var signedFrame = currentWorkbench.AuxiliaryViewerPane.HeightImage.Frame is { IsSignedDelta: true } delta ? delta : null;
        var displayPath = signedFrame?.SourcePath ?? candidate.SourcePath;
        if (!string.Equals(loadedAuxiliaryPath, displayPath, StringComparison.OrdinalIgnoreCase))
        {
            if (signedFrame is not null)
            {
                if (!auxiliaryViewer.ShowSignedDeltaFrame(signedFrame)) return;
            }
            else if (candidate.IsSource)
            {
                auxiliaryViewer.LoadC3DSource(candidate.SourcePath);
            }
            else
            {
                auxiliaryViewer.ShowC3DWorkbenchResult(
                    candidate.SourcePath,
                    $"{candidate.DisplayName} | {candidate.State}");
            }

            loadedAuxiliaryPath = displayPath;
        }

        PresentAuxiliaryContent(currentWorkbench.AuxiliaryViewerPane.IsSection ? AuxiliaryPaneProfile : auxiliaryViewer, currentWorkbench);
    }

    private void RefreshLowerViewers()
    {
        if (workbench?.EffectiveViewerLayout != ViewerWorkspaceLayout.Four) return;
        RefreshLowerViewer(workbench.SectionViewerPane, FourSectionSlotThreeDHost, FourSectionMenuHost, FourSectionProfile, ref sectionPaneViewer);
        RefreshLowerViewer(workbench.HeightViewerPane, FourHeightSlotThreeDHost, FourHeightMenuHost, FourHeightProfile, ref heightPaneViewer);
        FourSectionSlotPresentationBar.ViewerHost = sectionPaneViewer ?? mainViewer;
        FourHeightSlotPresentationBar.ViewerHost = heightPaneViewer ?? mainViewer;
        FourSectionSlotPresentationBar.Visibility = workbench.SectionViewerPane.IsThreeD ? Visibility.Visible : Visibility.Collapsed;
        FourHeightSlotPresentationBar.Visibility = workbench.HeightViewerPane.IsThreeD ? Visibility.Visible : Visibility.Collapsed;
        linkCoordinator.SetLowerViewers(sectionPaneViewer, heightPaneViewer);
    }

    private void RefreshLowerViewer(ViewerPaneViewModel pane, ContentControl host, ContentControl menuHost, HeightProfileView profile,
        ref OpenVisionThreeDViewerControl? viewer)
    {
        if (pane.Source is not { } source)
        {
            viewer?.ClearC3DTeachingSource(pane.SourceName);
            host.Content = null;
            profile.ViewerHost = null;
            return;
        }
        if (pane.IsThreeD || viewer is not null)
        {
            viewer ??= new OpenVisionThreeDViewerControl { SidePanelsVisible = false };
            viewer.SetWorkspaceHeaderHost(menuHost, IsCompactChrome);
            viewer.TrySetHudDetailsVisible(false);
            if (!string.Equals(viewer.CurrentC3DSourcePath, source.SourcePath, StringComparison.OrdinalIgnoreCase))
            {
                if (source.IsSource) viewer.LoadC3DSource(source.SourcePath);
                else viewer.ShowC3DWorkbenchResult(source.SourcePath, source.DisplayName);
            }
            host.Content = viewer;
        }
        profile.ViewerHost = viewer ?? mainViewer;
    }

    private void PresentAuxiliaryContent(object content, ToolWorkbenchViewModel currentWorkbench)
    {
        AuxiliaryEmptyText.Visibility = Visibility.Collapsed;
        AuxiliaryViewerPresentationBar.ViewerHost =
            content is OpenVisionThreeDViewerControl viewer
                ? viewer
                : currentWorkbench.AuxiliaryViewerPane.IsSection ? auxiliaryViewer : mainViewer;
        AuxiliaryViewerPresentationBar.Visibility = currentWorkbench.AuxiliaryViewerPane.IsHeight ? Visibility.Collapsed
            : AuxiliaryViewerPresentationBar.Presentation is null ? Visibility.Collapsed : Visibility.Visible;
        AuxiliaryViewerPresentationBar.DialogPlacementTarget = content is HeightImageViewerView heightView
            ? heightView.DisplaySettingsAnchor : null;
        if (currentWorkbench.EffectiveViewerLayout == ViewerWorkspaceLayout.PopOut)
        {
            AuxiliaryViewerHost.Content = null;
            if (content is HeightProfileView)
            {
                popoutProfile ??= new HeightProfileView { UseEmbeddedChrome = true };
                popoutProfile.ViewerHost = auxiliaryViewer;
                content = popoutProfile;
            }
            popout?.SetViewerContent(content, currentWorkbench.Localization.ViewerAuxiliaryNoOutput);
        }
        else if (currentWorkbench.EffectiveViewerLayout is ViewerWorkspaceLayout.SplitVertical
                     or ViewerWorkspaceLayout.SplitHorizontal
                     or ViewerWorkspaceLayout.HeightImageOnly or ViewerWorkspaceLayout.Four)
        {
            popout?.ReleaseViewerContent();
            AuxiliaryViewerHost.Content = content is HeightProfileView ? null : content;
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

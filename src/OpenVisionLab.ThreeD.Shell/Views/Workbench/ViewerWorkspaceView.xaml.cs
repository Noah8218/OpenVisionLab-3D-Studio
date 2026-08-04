using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class ViewerWorkspaceView : UserControl
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
    private string loadedAuxiliaryPath = string.Empty;
    private bool subscriptionsAttached;
    private bool synchronizingLinkedHeightDisplayRange;
    private bool roiFocusRatioApplied;
    private ViewerWorkspaceLayout roiFocusLayout;
    private GridLength roiFocusFirstLength;
    private GridLength roiFocusSecondLength;

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

    public bool IsPopoutVisible => popout?.IsVisible == true;

    public Window? PopoutWindow => popout;

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

    public bool ReactivateMainViewer(object? requestedContent)
    {
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
        DetachSubscriptions();
        workbench = args.NewValue as ToolWorkbenchViewModel;
        AttachSubscriptions();
        RefreshWorkspace();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachSubscriptions();
        RefreshWorkspace();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        DetachSubscriptions();

    private void AttachSubscriptions()
    {
        if (subscriptionsAttached || workbench is null)
        {
            return;
        }

        workbench.ViewerWorkspace.PropertyChanged += OnViewerWorkspacePropertyChanged;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        workbench.CompareCandidates.CollectionChanged += OnCompareCandidatesChanged;
        workbench.SharedHeightCursor.PropertyChanged += OnSharedHeightCursorChanged;
        workbench.HeightImageViewer.PropertyChanged += OnHeightImageViewerPropertyChanged;
        AttachMainViewer(MainViewerContent as OpenVisionThreeDViewerControl);
        subscriptionsAttached = true;
    }

    private void DetachSubscriptions()
    {
        if (!subscriptionsAttached || workbench is null)
        {
            return;
        }

        workbench.ViewerWorkspace.PropertyChanged -= OnViewerWorkspacePropertyChanged;
        workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
        workbench.CompareCandidates.CollectionChanged -= OnCompareCandidatesChanged;
        workbench.SharedHeightCursor.PropertyChanged -= OnSharedHeightCursorChanged;
        workbench.HeightImageViewer.PropertyChanged -= OnHeightImageViewerPropertyChanged;
        AttachMainViewer(null);
        subscriptionsAttached = false;
    }

    private static void OnMainViewerContentChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args) =>
        ((ViewerWorkspaceView)dependencyObject).AttachMainViewer(
            args.NewValue as OpenVisionThreeDViewerControl);

    private void AttachMainViewer(OpenVisionThreeDViewerControl? viewer)
    {
        if (ReferenceEquals(mainViewer, viewer))
        {
            if (!ReferenceEquals(MainViewerPresenter.Content, viewer))
            {
                MainViewerPresenter.Content = viewer;
            }

            ApplySharedHeightCursorToMainViewer();
            SynchronizeLinkedHeightDisplayRangeFromMainViewer();
            return;
        }

        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged -= OnMainViewerC3DGridHoverChanged;
            mainViewer.ViewModel.PropertyChanged -= OnMainViewerPropertyChanged;
            mainViewer.SetLinkedHeightCursor(null);
        }

        mainViewer = viewer;
        MainViewerPresenter.Content = viewer;
        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged += OnMainViewerC3DGridHoverChanged;
            mainViewer.ViewModel.PropertyChanged += OnMainViewerPropertyChanged;
        }

        ApplySharedHeightCursorToMainViewer();
        SynchronizeLinkedHeightDisplayRangeFromMainViewer();
    }

    private void OnMainViewerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(MainWindowViewModel.C3DHeightColorRangeRevision)
            or nameof(MainWindowViewModel.C3DHeightDistributionSourceSha256))
        {
            SynchronizeLinkedHeightDisplayRangeFromMainViewer();
        }
    }

    private void OnHeightImageViewerPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(HeightImageViewerViewModel.Frame))
        {
            SynchronizeLinkedHeightDisplayRangeFromMainViewer();
        }
        else if (args.PropertyName == nameof(HeightImageViewerViewModel.DisplayRangeRevision))
        {
            SynchronizeLinkedHeightDisplayRangeToMainViewer();
        }
    }

    private void SynchronizeLinkedHeightDisplayRangeFromMainViewer()
    {
        var viewerViewModel = mainViewer?.ViewModel;
        var heightImage = workbench?.HeightImageViewer;
        if (synchronizingLinkedHeightDisplayRange
            || viewerViewModel is null
            || heightImage?.Frame is null
            || !HasMatchingHeightSource(viewerViewModel, heightImage))
        {
            return;
        }

        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            if (viewerViewModel.C3DHeightColorRangeAuto)
            {
                heightImage.UseAutoRange();
            }
            else
            {
                heightImage.TryApplyLinkedDisplayRange(
                    viewerViewModel.C3DHeightColorMinimumRaw,
                    viewerViewModel.C3DHeightColorMaximumRaw);
            }
        }
        finally
        {
            synchronizingLinkedHeightDisplayRange = false;
        }
    }

    private void SynchronizeLinkedHeightDisplayRangeToMainViewer()
    {
        var viewerViewModel = mainViewer?.ViewModel;
        var heightImage = workbench?.HeightImageViewer;
        if (synchronizingLinkedHeightDisplayRange
            || viewerViewModel is null
            || heightImage?.DisplayFrame is not { } displayFrame
            || !HasMatchingHeightSource(viewerViewModel, heightImage))
        {
            return;
        }

        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            if (heightImage.IsAutoRange)
            {
                viewerViewModel.ResetC3DHeightColorRange();
            }
            else
            {
                viewerViewModel.TryApplyLinkedC3DHeightColorRange(
                    displayFrame.Minimum,
                    displayFrame.Maximum);
            }
        }
        finally
        {
            synchronizingLinkedHeightDisplayRange = false;
        }
    }

    private static bool HasMatchingHeightSource(
        MainWindowViewModel viewerViewModel,
        HeightImageViewerViewModel heightImage) =>
        heightImage.Frame is { } frame
        && string.Equals(
            viewerViewModel.C3DHeightDistributionSourceSha256,
            frame.SourceContentSha256,
            StringComparison.OrdinalIgnoreCase);

    private void OnMainViewerC3DGridHoverChanged(
        object? sender,
        C3DGridHoverChangedEventArgs args)
    {
        if (workbench is null)
        {
            return;
        }

        if (args.Cursor is not { } cursor)
        {
            workbench.SharedHeightCursor.Clear(
                SharedHeightCursorOrigin.ThreeDViewer);
            return;
        }

        workbench.SharedHeightCursor.Update(
            SharedHeightCursorOrigin.ThreeDViewer,
            cursor.SourceContentSha256,
            cursor.Row,
            cursor.Column,
            cursor.RawHeight,
            cursor.IsValid);
    }

    private void OnSharedHeightCursorChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SharedHeightCursorSession.Cursor)
            or nameof(SharedHeightCursorSession.HasCursor)
            or nameof(SharedHeightCursorSession.Revision))
        {
            ApplySharedHeightCursorToMainViewer();
        }
    }

    private void ApplySharedHeightCursorToMainViewer()
    {
        if (mainViewer is null)
        {
            return;
        }

        mainViewer.SetLinkedHeightCursor(
            workbench?.SharedHeightCursor.Cursor is { } cursor
                ? new C3DGridCursor(
                    cursor.Origin == SharedHeightCursorOrigin.ThreeDViewer
                        ? C3DGridCursorOrigin.ThreeDViewer
                        : C3DGridCursorOrigin.HeightImage,
                    cursor.SourceContentSha256,
                    cursor.Row,
                    cursor.Column,
                    cursor.RawHeight,
                    cursor.IsValid)
                : null);
    }

    private void OnViewerWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ViewerWorkspaceSession.Layout)
            or nameof(ViewerWorkspaceSession.AuxiliaryContentId))
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
        RefreshAuxiliaryViewer();
        UpdateRoiFocusRatio();
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
            AuxiliaryEmptyText.Visibility = Visibility.Visible;
            popout?.SetViewerContent(
                null,
                currentWorkbench?.Localization.ViewerAuxiliaryNoOutput ?? "No real 3D output is available");
            return;
        }

        if (candidate.Kind == ViewerWorkspaceCandidateKind.HeightImage)
        {
            heightImageViewer ??= new HeightImageViewerView
            {
                DataContext = currentWorkbench.HeightImageViewer
            };
            _ = currentWorkbench.HeightImageViewer.EnsureSourceAsync(
                candidate.SourcePath,
                currentWorkbench.Source.Id,
                currentWorkbench.Source.Unit,
                currentWorkbench.Source.FrameId);
            PresentAuxiliaryContent(heightImageViewer, currentWorkbench);
            return;
        }

        auxiliaryViewer ??= new OpenVisionThreeDViewerControl
        {
            SidePanelsVisible = false
        };
        auxiliaryViewer.ViewModel.HudDetailsVisible = false;
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
            popout.AuxiliarySlotFocused += OnAuxiliarySlotFocused;
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
        if (workbench?.SetSingleViewerLayoutCommand.CanExecute(null) == true)
        {
            workbench.SetSingleViewerLayoutCommand.Execute(null);
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
        popout.AuxiliarySlotFocused -= OnAuxiliarySlotFocused;
        popout.CloseForOwner();
        popout = null;
    }

    private void MainSlot_PreviewMouseDown(object sender, MouseButtonEventArgs args) =>
        FocusSlot(ViewerWorkspaceSession.MainSlotId);

    private void AuxiliarySlot_PreviewMouseDown(object sender, MouseButtonEventArgs args) =>
        FocusSlot(ViewerWorkspaceSession.AuxiliarySlotId);

    private void OnAuxiliarySlotFocused(object? sender, EventArgs args) =>
        FocusSlot(ViewerWorkspaceSession.AuxiliarySlotId);

    private void FocusSlot(string slotId)
    {
        if (workbench?.FocusViewerWorkspaceSlotCommand.CanExecute(slotId) == true)
        {
            workbench.FocusViewerWorkspaceSlotCommand.Execute(slotId);
        }
    }
}

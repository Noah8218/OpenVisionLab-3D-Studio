using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public partial class HeightImageViewerView : UserControl
{
    public static readonly DependencyProperty UseEmbeddedChromeProperty =
        DependencyProperty.Register(
            nameof(UseEmbeddedChrome),
            typeof(bool),
            typeof(HeightImageViewerView),
            new PropertyMetadata(false, OnUseEmbeddedChromeChanged));

    public static readonly DependencyProperty ProfileStateProperty = DependencyProperty.Register(nameof(ProfileState), typeof(ViewerHostProfileState), typeof(HeightImageViewerView), new PropertyMetadata(ViewerHostProfileState.Empty, OnProfileStateChanged));
    public static readonly DependencyProperty ProfilePickCommandProperty = DependencyProperty.Register(nameof(ProfilePickCommand), typeof(ICommand), typeof(HeightImageViewerView));
    public ViewerHostProfileState ProfileState { get => (ViewerHostProfileState)GetValue(ProfileStateProperty); set => SetValue(ProfileStateProperty, value); }
    public ICommand? ProfilePickCommand { get => (ICommand?)GetValue(ProfilePickCommandProperty); set => SetValue(ProfilePickCommandProperty, value); }
    public static readonly DependencyProperty PresentationProperty = DependencyProperty.Register(nameof(Presentation), typeof(ViewerPresentationBarViewModel), typeof(HeightImageViewerView));
    public static readonly DependencyProperty DisplayPresentationProperty = DependencyProperty.Register(nameof(DisplayPresentation), typeof(ViewerPresentationBarViewModel), typeof(HeightImageViewerView));
    public static readonly DependencyProperty RoiCaptureCommandProperty = DependencyProperty.Register(nameof(RoiCaptureCommand), typeof(ICommand), typeof(HeightImageViewerView));
    public static readonly DependencyProperty SelectionContextProperty = DependencyProperty.Register(nameof(SelectionContext), typeof(string), typeof(HeightImageViewerView), new PropertyMetadata(string.Empty));
    public ViewerPresentationBarViewModel? Presentation { get => (ViewerPresentationBarViewModel?)GetValue(PresentationProperty); set => SetValue(PresentationProperty, value); }
    public ViewerPresentationBarViewModel? DisplayPresentation { get => (ViewerPresentationBarViewModel?)GetValue(DisplayPresentationProperty); set => SetValue(DisplayPresentationProperty, value); }
    public FrameworkElement DisplaySettingsAnchor => HeightImageDisplaySettingsToggle;
    public ICommand? RoiCaptureCommand { get => (ICommand?)GetValue(RoiCaptureCommandProperty); set => SetValue(RoiCaptureCommandProperty, value); }
    public string SelectionContext { get => (string)GetValue(SelectionContextProperty); set => SetValue(SelectionContextProperty, value); }
    private int profileDraggedEndpoint;
    private Point? profileDragStart;
    private HeightImageViewerViewModel? viewModel;
    private double imageScale = 1.0;
    private bool isPanning;
    private Point panStart;
    private double panHorizontalOffset;
    private double panVerticalOffset;
    private DispatcherOperation? fitImageOperation;
    private C3DInvalidCellMap? displayedMissingMap;

    public HeightImageViewerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        HeightImage.LostMouseCapture += OnImageLostMouseCapture;
        ImageScrollViewer.LostMouseCapture += OnScrollLostMouseCapture;
    }

    public bool UseEmbeddedChrome
    {
        get => (bool)GetValue(UseEmbeddedChromeProperty);
        set => SetValue(UseEmbeddedChromeProperty, value);
    }

    public bool HasNativeCoordinateImage =>
        viewModel?.DisplayFrame is { } frame
        && HeightImage.Source is BitmapSource bitmap
        && bitmap.PixelWidth == frame.Width
        && bitmap.PixelHeight == frame.Height;

    private static void OnUseEmbeddedChromeChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        var view = (HeightImageViewerView)target;
        view.HeightImageDisplaySettings.SetCurrentValue(Expander.IsExpandedProperty, !(bool)args.NewValue);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        CancelPendingFitImage();
        ReleasePointerGestures();
        DetachViewModel();
        viewModel = args.NewValue as HeightImageViewerViewModel;
        AttachViewModel();
        RefreshFrame();
        QueueFitImage(DispatcherPriority.Loaded);
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachViewModel();
        RefreshFrame();
        QueueFitImage(DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object sender, RoutedEventArgs args) =>
        OnUnloadedCore();

    private void OnUnloadedCore()
    {
        CloseDisplayPopup();
        CancelPendingFitImage();
        ReleasePointerGestures();
        DetachViewModel();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is false) CloseDisplayPopup();
        else
        {
            if (viewModel?.Frame is not null && HeightImage.Width <= 0) QueueFitImage(DispatcherPriority.Loaded);
            UpdateHeightAxes();
        }
    }

    private void CloseDisplayPopup()
    {
        HeightImageDisplaySettings.SetCurrentValue(Expander.IsExpandedProperty, false);
        HeightImageDisplaySettingsPopup.SetCurrentValue(System.Windows.Controls.Primitives.Popup.IsOpenProperty, false);
    }

    private void ReleasePointerGestures()
    {
        viewModel?.RoiWorkspace.HandlePointer(new HeightImageRoiPointerInput(HeightImageRoiPointerAction.Cancel));
        profileDraggedEndpoint = 0;
        profileDragStart = null;
        if (HeightImage.IsMouseCaptured) HeightImage.ReleaseMouseCapture();
        if (isPanning) EndPan();
    }

    private void OnImageLostMouseCapture(object sender, MouseEventArgs args)
    {
        profileDraggedEndpoint = 0;
        profileDragStart = null;
        viewModel?.RoiWorkspace.HandlePointer(new HeightImageRoiPointerInput(HeightImageRoiPointerAction.Cancel));
    }

    private void OnScrollLostMouseCapture(object sender, MouseEventArgs args)
    {
        if (isPanning && !ImageScrollViewer.IsMouseCaptured) EndPan();
    }

    private void AttachViewModel()
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.DisplayRequest -= OnDisplayRequest;
        viewModel.RoiWorkspace.PropertyChanged -= OnRoiWorkspacePropertyChanged;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        viewModel.DisplayRequest += OnDisplayRequest;
        viewModel.RoiWorkspace.PropertyChanged += OnRoiWorkspacePropertyChanged;
    }

    private void DetachViewModel()
    {
        if (viewModel is null)
        {
            return;
        }

        viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        viewModel.DisplayRequest -= OnDisplayRequest;
        viewModel.RoiWorkspace.PropertyChanged -= OnRoiWorkspacePropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(HeightImageViewerViewModel.Frame))
        {
            ReleasePointerGestures();
            QueueFitImage(DispatcherPriority.Loaded);
        }
        else if (args.PropertyName == nameof(HeightImageViewerViewModel.DisplayFrame))
        {
            RefreshFrame();
        }
        else if (args.PropertyName is nameof(HeightImageViewerViewModel.CompletenessCellOverlays)
            or nameof(HeightImageViewerViewModel.SelectedCompletenessCellId))
        {
            UpdateRoiOverlay();
        }

        if (args.PropertyName is nameof(HeightImageViewerViewModel.HasLinkedCursor)
            or nameof(HeightImageViewerViewModel.LinkedCursorRow)
            or nameof(HeightImageViewerViewModel.LinkedCursorColumn)
            or nameof(HeightImageViewerViewModel.LinkedCursorOrigin)
            or nameof(HeightImageViewerViewModel.LinkedCursorIsValid))
        {
            UpdateLinkedCursorOverlay();
        }
        if (args.PropertyName == nameof(HeightImageViewerViewModel.Localization)) { UpdateHeightAxes(); UpdateRoiOverlay(); }
    }

    private void OnRoiWorkspacePropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (profileDraggedEndpoint == 0 && profileDragStart is null && viewModel?.RoiWorkspace.IsGestureActive != true && HeightImage.IsMouseCaptured)
            HeightImage.ReleaseMouseCapture();
        UpdateRoiOverlay();
    }

    private static void OnProfileStateChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var view = (HeightImageViewerView)owner;
        if (view.profileDragStart is not null && args.NewValue is ViewerHostProfileState state
            && (!state.IsActive || !state.IsPicking || !string.Equals(state.SourceSha256, view.viewModel?.Frame?.SourceContentSha256, StringComparison.OrdinalIgnoreCase)))
        {
            view.profileDragStart = null;
            if (view.HeightImage.IsMouseCaptured) view.HeightImage.ReleaseMouseCapture();
        }
        if (args.OldValue is ViewerHostProfileState before && args.NewValue is ViewerHostProfileState after
            && before.Visible == after.Visible && before.SourceSha256 == after.SourceSha256
            && before.FirstRow == after.FirstRow && before.FirstColumn == after.FirstColumn
            && before.SecondRow == after.SecondRow && before.SecondColumn == after.SecondColumn
            && ReferenceEquals(before.Trace, after.Trace)) return;
        ((HeightImageViewerView)owner).UpdateProfileOverlay();
    }

    private Point? ProfilePosition(int? row, int? column) => row is { } r && column is { } c
        ? new Point((c + 0.5) * imageScale, (r + 0.5) * imageScale) : null;

    private void UpdateProfileOverlay()
    {
        ProfileOverlay.Children.Clear();
        var profile = ProfileState;
        if (!profile.Visible || viewModel?.Frame is not { } frame
            || !string.Equals(profile.SourceSha256, frame.SourceContentSha256, StringComparison.OrdinalIgnoreCase)) return;
        var profileBrush = (Brush)FindResource("HeightProfile.LineBrush");
        Point? previous = null;
        ViewerHeightProfileTraceCell? previousCell = null;
        foreach (var cell in profile.Trace)
        {
            var point = ProfilePosition(cell.Row, cell.Column)!.Value;
            if (previous is { } before && previousCell is { } prior && Math.Max(Math.Abs(prior.Row - cell.Row), Math.Abs(prior.Column - cell.Column)) == 1)
                ProfileOverlay.Children.Add(new Line { X1 = before.X, Y1 = before.Y, X2 = point.X, Y2 = point.Y, Stroke = profileBrush, StrokeThickness = 2 });
            previous = point;
            previousCell = cell;
        }
        var first = ProfilePosition(profile.FirstRow, profile.FirstColumn);
        var second = ProfilePosition(profile.SecondRow, profile.SecondColumn);
        if (first is { } start) DrawEndpoint(start, "A");
        if (second is { } end) DrawEndpoint(end, "B");

        void DrawEndpoint(Point point, string text)
        {
            var marker = new Grid { Width = 17, Height = 17 };
            marker.Children.Add(new Ellipse { Fill = profileBrush, Stroke = (Brush)FindResource("HeightProfile.OutlineBrush"), StrokeThickness = 2 });
            marker.Children.Add(new TextBlock
            {
                Text = text, Foreground = (Brush)FindResource("HeightProfile.TextBrush"), FontFamily = new FontFamily("Segoe UI"),
                FontWeight = FontWeights.Bold, FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 1, 0, 0)
            });
            Canvas.SetLeft(marker, point.X - 8.5);
            Canvas.SetTop(marker, point.Y - 8.5);
            ProfileOverlay.Children.Add(marker);
        }
    }

    private void RefreshFrame()
    {
        if (viewModel?.DisplayFrame is not { } displayFrame)
        {
            HeightImage.Source = null;
            MissingCellHatch.OpacityMask = null;
            displayedMissingMap = null;
            HeightImage.Width = 0;
            HeightImage.Height = 0;
            RoiOverlay.Children.Clear();
            HeightAxes.Children.Clear();
            UpdateLinkedCursorOverlay();
            return;
        }

        HeightImage.Source = HeightImageFrameConverter.CreateBitmap(displayFrame);
        if (viewModel.Frame is { } sourceFrame && !ReferenceEquals(displayedMissingMap, sourceFrame.InvalidCellMap))
        {
            var map = sourceFrame.InvalidCellMap;
            var maskPixels = new byte[checked(map.Width * map.Height * 4)];
            for (var index = 0; index < map.Width * map.Height; index++)
            {
                map.TryIsMissing(index % map.Width, index / map.Width, out var isMissing);
                if (!isMissing) continue;
                var offset = index * 4;
                maskPixels[offset] = maskPixels[offset + 1] = maskPixels[offset + 2] = maskPixels[offset + 3] = 255;
            }
            var mask = BitmapSource.Create(map.Width, map.Height, 96, 96, PixelFormats.Bgra32, null, maskPixels, map.Width * 4);
            mask.Freeze();
            MissingCellHatch.OpacityMask = new ImageBrush(mask) { Stretch = Stretch.Fill };
            displayedMissingMap = map;
        }
        UpdateRoiOverlay();
        UpdateLinkedCursorOverlay();
    }

    private void OnDisplayRequest(object? sender, HeightImageDisplayRequest request)
    {
        switch (request)
        {
            case HeightImageDisplayRequest.Fit:
                QueueFitImage(DispatcherPriority.Loaded);
                break;
            case HeightImageDisplayRequest.FitRoi:
                FitRoi();
                break;
            case HeightImageDisplayRequest.ActualPixels:
                SetImageScale(1.0);
                break;
            case HeightImageDisplayRequest.ZoomIn:
                ZoomAtViewportCenter(1.25);
                break;
            case HeightImageDisplayRequest.ZoomOut:
                ZoomAtViewportCenter(0.8);
                break;
        }
    }

    private void FitImage()
    {
        if (viewModel?.Frame is not { } frame
            || ImageScrollViewer.ViewportWidth <= 0
            || ImageScrollViewer.ViewportHeight <= 0)
        {
            return;
        }

        var horizontalScale = Math.Max(0.01, ImageScrollViewer.ViewportWidth / frame.Width);
        var verticalScale = Math.Max(0.01, ImageScrollViewer.ViewportHeight / frame.Height);
        SetImageScale(Math.Min(horizontalScale, verticalScale) * 0.95);
        ImageScrollViewer.ScrollToHorizontalOffset(0);
        ImageScrollViewer.ScrollToVerticalOffset(0);
    }

    private void QueueFitImage(DispatcherPriority priority)
    {
        CancelPendingFitImage();
        if (!IsLoaded || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        try
        {
            fitImageOperation = Dispatcher.BeginInvoke(
                priority,
                new Action(ApplyQueuedFitImage));
        }
        catch (InvalidOperationException)
        {
            fitImageOperation = null;
        }
    }

    private void FitRoi()
    {
        if (viewModel?.FitRoiRectangle is not { } roi || ImageScrollViewer.ViewportWidth <= 0 || ImageScrollViewer.ViewportHeight <= 0) return;
        var verticalPadding = UseEmbeddedChrome ? Math.Max(24, HeightImageChrome.TranslatePoint(new Point(0, HeightImageChrome.ActualHeight), ImageScrollViewer).Y + 8) : 24;
        SetImageScale(Math.Min(Math.Max(1, ImageScrollViewer.ViewportWidth - 48) / roi.ColumnCount, Math.Max(1, ImageScrollViewer.ViewportHeight - 2 * verticalPadding) / roi.RowCount));
        UpdateLayout();
        ImageScrollViewer.ScrollToHorizontalOffset((roi.Column + roi.ColumnCount / 2.0) * imageScale - ImageScrollViewer.ViewportWidth / 2);
        ImageScrollViewer.ScrollToVerticalOffset((roi.Row + roi.RowCount / 2.0) * imageScale - ImageScrollViewer.ViewportHeight / 2);
    }

    private void ApplyQueuedFitImage()
    {
        fitImageOperation = null;
        if (IsLoaded)
        {
            FitImage();
        }
    }

    private void CancelPendingFitImage()
    {
        var operation = fitImageOperation;
        fitImageOperation = null;
        if (operation?.Status == DispatcherOperationStatus.Pending)
        {
            operation.Abort();
        }
    }

    private void SetImageScale(double scale)
    {
        if (viewModel?.Frame is not { } frame)
        {
            return;
        }

        imageScale = Math.Clamp(scale, 0.01, 32.0);
        HeightImage.Width = Math.Max(1, frame.Width * imageScale);
        HeightImage.Height = Math.Max(1, frame.Height * imageScale);
        ImageSurface.Width = HeightImage.Width;
        ImageSurface.Height = HeightImage.Height;
        viewModel.SetZoom(imageScale * 100.0);
        UpdateRoiOverlay();
        UpdateProfileOverlay();
        UpdateLinkedCursorOverlay();
        UpdateHeightAxes();
    }

    private void ZoomAtViewportCenter(double factor)
    {
        var center = new Point(
            ImageScrollViewer.ViewportWidth / 2.0,
            ImageScrollViewer.ViewportHeight / 2.0);
        ZoomAt(center, factor);
    }

    private void ZoomAt(Point viewportPoint, double factor)
    {
        if (viewModel?.Frame is null || HeightImage.ActualWidth <= 0 || HeightImage.ActualHeight <= 0)
        {
            return;
        }

        var contentX = ImageScrollViewer.HorizontalOffset + viewportPoint.X;
        var contentY = ImageScrollViewer.VerticalOffset + viewportPoint.Y;
        var relativeX = contentX / HeightImage.ActualWidth;
        var relativeY = contentY / HeightImage.ActualHeight;
        SetImageScale(imageScale * factor);
        UpdateLayout();
        ImageScrollViewer.ScrollToHorizontalOffset(relativeX * HeightImage.Width - viewportPoint.X);
        ImageScrollViewer.ScrollToVerticalOffset(relativeY * HeightImage.Height - viewportPoint.Y);
    }

    private void ImageScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs args)
    {
        var point = args.GetPosition(ImageScrollViewer);
        ZoomAt(point, args.Delta > 0 ? 1.2 : 1.0 / 1.2);
        args.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs args)
    {
        if (args.ChangedButton != MouseButton.Middle && !(args.ChangedButton == MouseButton.Left && Presentation?.IsMoveMode == true && viewModel?.RoiWorkspace.IsCaptureActive == false))
        {
            return;
        }

        isPanning = true;
        panStart = args.GetPosition(ImageScrollViewer);
        panHorizontalOffset = ImageScrollViewer.HorizontalOffset;
        panVerticalOffset = ImageScrollViewer.VerticalOffset;
        ImageScrollViewer.CaptureMouse();
        Mouse.OverrideCursor = Cursors.Hand;
        args.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseMove(object sender, MouseEventArgs args)
    {
        if (!isPanning)
        {
            return;
        }

        var current = args.GetPosition(ImageScrollViewer);
        ImageScrollViewer.ScrollToHorizontalOffset(panHorizontalOffset - (current.X - panStart.X));
        ImageScrollViewer.ScrollToVerticalOffset(panVerticalOffset - (current.Y - panStart.Y));
        args.Handled = true;
    }

    private void ImageScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs args)
    {
        if (!isPanning || args.ChangedButton is not (MouseButton.Middle or MouseButton.Left))
        {
            return;
        }

        EndPan();
        args.Handled = true;
    }

    private void ImageScrollViewer_MouseLeave(object sender, MouseEventArgs args)
    {
        if (isPanning && args.MiddleButton != MouseButtonState.Pressed && args.LeftButton != MouseButtonState.Pressed)
        {
            EndPan();
        }
    }

    private void EndPan()
    {
        isPanning = false;
        ImageScrollViewer.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
    }

    private void HeightImage_MouseMove(object sender, MouseEventArgs args)
    {
        if (!TryGetNativeCell(args.GetPosition(HeightImage), out var row, out var column))
        {
            return;
        }

        if (viewModel!.RoiWorkspace.IsGestureActive
            && args.LeftButton == MouseButtonState.Pressed)
        {
            viewModel.RoiWorkspace.HandlePointer(
                new HeightImageRoiPointerInput(
                    HeightImageRoiPointerAction.Update,
                    row,
                    column));
        }

        if (profileDraggedEndpoint != 0 && args.LeftButton == MouseButtonState.Pressed)
        {
            var move = new ViewerHeightProfileCell(ProfileState.SourceSha256, row, column, profileDraggedEndpoint);
            if (ProfilePickCommand?.CanExecute(move) == true) ProfilePickCommand.Execute(move);
        }
        viewModel.UpdateHover(column, row);
        UpdateLinkedCursorOverlay();
    }

    private void HeightImage_MouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs args)
    {
        Focus();
        if (viewModel?.Frame is not { } frame
            || !TryGetNativeCell(args.GetPosition(HeightImage), out var row, out var column))
        {
            return;
        }

        if (ProfileState.IsActive && string.Equals(ProfileState.SourceSha256, frame.SourceContentSha256, StringComparison.OrdinalIgnoreCase))
        {
            var position = args.GetPosition(HeightImage);
            if (!ProfileState.IsPicking)
            {
                var first = ProfilePosition(ProfileState.FirstRow, ProfileState.FirstColumn);
                var second = ProfilePosition(ProfileState.SecondRow, ProfileState.SecondColumn);
                profileDraggedEndpoint = first is { } a && (position - a).Length <= 12 ? 1 : second is { } b && (position - b).Length <= 12 ? 2 : 0;
                if (profileDraggedEndpoint != 0) HeightImage.CaptureMouse();
            }
            var pick = new ViewerHeightProfileCell(ProfileState.SourceSha256, row, column, profileDraggedEndpoint);
            if (ProfilePickCommand?.CanExecute(pick) == true)
            {
                ProfilePickCommand.Execute(pick);
                if (ProfileState.IsPicking && ProfileState.FirstRow == row && ProfileState.FirstColumn == column && ProfileState.SecondColumn is null)
                {
                    profileDragStart = position;
                    if (!HeightImage.CaptureMouse()) profileDragStart = null;
                }
            }
            args.Handled = true;
            return;
        }

        var rowTolerance = Math.Max(
            1,
            (int)Math.Ceiling(8.0 / Math.Max(1.0, HeightImage.ActualHeight) * frame.Height));
        var columnTolerance = Math.Max(
            1,
            (int)Math.Ceiling(8.0 / Math.Max(1.0, HeightImage.ActualWidth) * frame.Width));
        var pointerResult = viewModel.RoiWorkspace.HandlePointer(
            new HeightImageRoiPointerInput(
                HeightImageRoiPointerAction.Begin,
                row,
                column,
                rowTolerance,
                columnTolerance));
        if (!pointerResult.CaptureMouse)
        {
            return;
        }

        HeightImage.CaptureMouse();
        args.Handled = true;
    }

    private void HeightImage_MouseLeftButtonUp(
        object sender,
        MouseButtonEventArgs args)
    {
        if (profileDragStart is { } start)
        {
            profileDragStart = null;
            args.Handled = true;
            try
            {
                var position = args.GetPosition(HeightImage);
                if (ProfileState.IsActive && ProfileState.IsPicking
                    && string.Equals(ProfileState.SourceSha256, viewModel?.Frame?.SourceContentSha256, StringComparison.OrdinalIgnoreCase)
                    && (Math.Abs(position.X - start.X) >= SystemParameters.MinimumHorizontalDragDistance || Math.Abs(position.Y - start.Y) >= SystemParameters.MinimumVerticalDragDistance)
                    && TryGetNativeCell(position, out var dragRow, out var dragColumn))
                {
                    var pick = new ViewerHeightProfileCell(ProfileState.SourceSha256, dragRow, dragColumn);
                    if (ProfilePickCommand?.CanExecute(pick) == true) ProfilePickCommand.Execute(pick);
                }
            }
            finally
            {
                if (HeightImage.IsMouseCaptured) HeightImage.ReleaseMouseCapture();
            }
            return;
        }
        if (profileDraggedEndpoint != 0)
        {
            profileDraggedEndpoint = 0;
            HeightImage.ReleaseMouseCapture();
            args.Handled = true;
            return;
        }
        if (viewModel?.RoiWorkspace.IsGestureActive != true)
        {
            return;
        }

        if (TryGetNativeCell(args.GetPosition(HeightImage), out var row, out var column))
        {
            viewModel.RoiWorkspace.HandlePointer(
                new HeightImageRoiPointerInput(
                    HeightImageRoiPointerAction.Update,
                    row,
                    column));
        }

        var pointerResult = viewModel.RoiWorkspace.HandlePointer(
            new HeightImageRoiPointerInput(HeightImageRoiPointerAction.End));
        if (pointerResult.ReleaseMouse)
        {
            HeightImage.ReleaseMouseCapture();
        }

        args.Handled = true;
    }

    private void HeightImage_MouseLeave(object sender, MouseEventArgs args)
    {
        if (viewModel?.RoiWorkspace.IsGestureActive != true)
        {
            viewModel?.ClearHover();
        }
        UpdateLinkedCursorOverlay();
    }

    private bool TryGetNativeCell(Point point, out int row, out int column)
    {
        row = 0;
        column = 0;
        if (viewModel?.Frame is not { } frame
            || HeightImage.ActualWidth <= 0
            || HeightImage.ActualHeight <= 0)
        {
            return false;
        }

        column = Math.Clamp(
            (int)Math.Floor(point.X / HeightImage.ActualWidth * frame.Width),
            0,
            frame.Width - 1);
        row = Math.Clamp(
            (int)Math.Floor(point.Y / HeightImage.ActualHeight * frame.Height),
            0,
            frame.Height - 1);
        return true;
    }

    private void UpdateRoiOverlay()
    {
        RoiOverlay.Children.Clear();
        if (viewModel?.Frame is not { } frame
            || HeightImage.Width <= 0
            || HeightImage.Height <= 0)
        {
            return;
        }

        RoiOverlay.Width = HeightImage.Width;
        RoiOverlay.Height = HeightImage.Height;
        foreach (var overlay in viewModel.CompletenessCellOverlays)
        {
            AddCompletenessCellOverlay(frame.Width, frame.Height, overlay);
        }
        foreach (var overlay in viewModel.RoiWorkspace.VisibleOverlays)
        {
            AddRoiOverlay(frame.Width, frame.Height, overlay);
        }
    }

    private void AddCompletenessCellOverlay(
        int frameWidth,
        int frameHeight,
        C3DCompletenessCellOverlay overlay)
    {
        var rectangle = overlay.Region;
        var left = rectangle.Column / (double)frameWidth * HeightImage.Width;
        var top = rectangle.Row / (double)frameHeight * HeightImage.Height;
        var right = (rectangle.Column + rectangle.ColumnCount)
                    / (double)frameWidth
                    * HeightImage.Width;
        var bottom = (rectangle.Row + rectangle.RowCount)
                     / (double)frameHeight
                     * HeightImage.Height;
        var color = overlay.Status switch
        {
            ResultStatus.Pass => Color.FromRgb(31, 232, 92),
            ResultStatus.Fail => Color.FromRgb(255, 46, 41),
            _ => Color.FromRgb(255, 184, 31)
        };
        var brush = new SolidColorBrush(color);
        var isSelected = string.Equals(
            overlay.CellId,
            viewModel?.SelectedCompletenessCellId,
            StringComparison.OrdinalIgnoreCase);
        var shape = new Rectangle
        {
            Width = Math.Max(1, right - left),
            Height = Math.Max(1, bottom - top),
            Fill = new SolidColorBrush(Color.FromArgb(
                isSelected
                    ? (byte)105
                    : overlay.Status == ResultStatus.Fail ? (byte)70 : (byte)42,
                color.R,
                color.G,
                color.B)),
            Stroke = brush,
            StrokeThickness = isSelected ? 5.0 : 3.0
        };
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        RoiOverlay.Children.Add(shape);

        var label = new Border
        {
            Padding = new Thickness(4, 1, 4, 1),
            Background = new SolidColorBrush(Color.FromArgb(220, 17, 24, 39)),
            BorderBrush = brush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Child = new TextBlock
            {
                Text = $"{(isSelected ? "▶ " : string.Empty)}{overlay.CellId} {overlay.Status}",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            }
        };
        Canvas.SetLeft(label, Math.Max(0, left + 2));
        Canvas.SetTop(label, Math.Max(0, top + 2));
        RoiOverlay.Children.Add(label);
    }

    private void AddRoiOverlay(
        int frameWidth,
        int frameHeight,
        HeightImageRoiOverlayItem overlay)
    {
        var rectangle = overlay.Rectangle;
        var left = rectangle.Column / (double)frameWidth * HeightImage.Width;
        var top = rectangle.Row / (double)frameHeight * HeightImage.Height;
        var right = (rectangle.Column + rectangle.ColumnCount)
                    / (double)frameWidth
                    * HeightImage.Width;
        var bottom = (rectangle.Row + rectangle.RowCount)
                     / (double)frameHeight
                     * HeightImage.Height;
        var roleBrush = GetRoiRoleBrush(overlay.Role);
        var outline = new Rectangle
        {
            Width = Math.Max(1, right - left),
            Height = Math.Max(1, bottom - top),
            Stroke = new SolidColorBrush(Color.FromArgb(184, 8, 11, 14)),
            StrokeThickness = (overlay.IsActive ? 2.0 : 1.2) + 2.5,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(outline, left);
        Canvas.SetTop(outline, top);
        RoiOverlay.Children.Add(outline);
        var shape = new Rectangle
        {
            Width = Math.Max(1, right - left),
            Height = Math.Max(1, bottom - top),
            Fill = new SolidColorBrush(Color.FromArgb(
                overlay.IsActive ? (byte)33 : (byte)13,
                ((SolidColorBrush)roleBrush).Color.R,
                ((SolidColorBrush)roleBrush).Color.G,
                ((SolidColorBrush)roleBrush).Color.B)),
            Stroke = roleBrush,
            StrokeThickness = overlay.IsActive ? 2.0 : 1.2,
            StrokeDashArray = overlay.IsCandidate
                ? new DoubleCollection([5, 3])
                : null
        };
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        RoiOverlay.Children.Add(shape);

        if (!overlay.IsActive && overlay.Role is not (InspectionWorkspaceRegionRole.Reference or InspectionWorkspaceRegionRole.Measurement))
        {
            return;
        }

        var label = new Border
        {
            Padding = new Thickness(3, 1, 3, 1),
            Background = new SolidColorBrush(Color.FromArgb(220, 18, 23, 28)),
            BorderBrush = roleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Child = new TextBlock
            {
                Text = overlay.Role switch { InspectionWorkspaceRegionRole.Reference => viewModel!.Localization.ViewerReferenceChip, InspectionWorkspaceRegionRole.Measurement => viewModel!.Localization.ViewerMeasurementChip, _ => overlay.Name },
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = roleBrush
            }
        };
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var labelLeft = overlay.Role == InspectionWorkspaceRegionRole.Measurement ? right - label.DesiredSize.Width : left;
        Canvas.SetLeft(label, Math.Clamp(labelLeft, 0, Math.Max(0, HeightImage.Width - label.DesiredSize.Width)));
        Canvas.SetTop(label, Math.Clamp(bottom + 4, 0, Math.Max(0, HeightImage.Height - label.DesiredSize.Height)));
        RoiOverlay.Children.Add(label);

        if (!overlay.IsCandidate)
        {
            return;
        }

        AddRoiHandle(left, top, roleBrush, isCenter: false);
        AddRoiHandle(right, top, roleBrush, isCenter: false);
        AddRoiHandle(left, bottom, roleBrush, isCenter: false);
        AddRoiHandle(right, bottom, roleBrush, isCenter: false);
        AddRoiHandle((left + right) / 2.0, (top + bottom) / 2.0, roleBrush, isCenter: true);
    }

    private void AddRoiHandle(double x, double y, Brush brush, bool isCenter)
    {
        const double size = 10;
        Shape handle = isCenter
            ? new Ellipse()
            : new Rectangle();
        handle.Width = size;
        handle.Height = size;
        handle.Fill = brush;
        handle.Stroke = Brushes.White;
        handle.StrokeThickness = 1.5;
        Canvas.SetLeft(handle, x - size / 2.0);
        Canvas.SetTop(handle, y - size / 2.0);
        RoiOverlay.Children.Add(handle);
    }

    private Brush GetRoiRoleBrush(InspectionWorkspaceRegionRole role) =>
        role switch
        {
            InspectionWorkspaceRegionRole.Reference or InspectionWorkspaceRegionRole.First =>
                FrozenBrush(Color.FromRgb(92, 200, 224)),
            InspectionWorkspaceRegionRole.Measurement or InspectionWorkspaceRegionRole.Second =>
                FrozenBrush(Color.FromRgb(242, 179, 94)),
            _ => FrozenBrush(Color.FromRgb(181, 156, 242))
        };

    private string FormatRoiLifecycle(InspectionWorkspaceRegionLifecycleState lifecycle) =>
        lifecycle switch
        {
            InspectionWorkspaceRegionLifecycleState.Drawing => viewModel!.Localization.RoiDrawing,
            InspectionWorkspaceRegionLifecycleState.Review => viewModel!.Localization.RoiReview,
            InspectionWorkspaceRegionLifecycleState.Applied => viewModel!.Localization.RoiApplied,
            _ => viewModel!.Localization.RoiMissing
        };

    private static Brush FrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    public async Task<HeightImageRoiPointerSmokeResult> RunRoiPointerSmokeAsync()
    {
        var before = viewModel?.RoiWorkspace.Candidate;
        var originalPointer = default(Point);
        var hasOriginalPointer = false;
        var leftPressed = false;
        Window? hostWindow = null;
        var originalTopmost = false;
        var targetDiagnostic = string.Empty;
        try
        {
            if (viewModel?.Frame is not { } frame
                || !viewModel.RoiWorkspace.IsCaptureActive
                || HeightImage.ActualWidth < 40
                || HeightImage.ActualHeight < 40)
            {
                throw new InvalidOperationException(
                    $"Height Image ROI pointer smoke requires a visible native-grid image and active ROI capture (actual={HeightImage.ActualWidth:F0}x{HeightImage.ActualHeight:F0}).");
            }

            hostWindow = Window.GetWindow(this)
                ?? throw new InvalidOperationException(
                    "Height Image is not attached to a visible WPF window.");
            originalTopmost = hostWindow.Topmost;
            hostWindow.Topmost = true;
            WindowsPointerInput.BringWindowToInputFront(hostWindow);
            hostWindow.Activate();
            Focus();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(180);

            var startRow = before is null
                ? Math.Clamp(frame.Height / 4, 0, frame.Height - 1)
                : before.Row + before.RowCount - 1;
            var startColumn = before is null
                ? Math.Clamp(frame.Width / 4, 0, frame.Width - 1)
                : before.Column + before.ColumnCount - 1;
            var rowDelta = Math.Max(2, frame.Height / 24);
            var columnDelta = Math.Max(2, frame.Width / 24);
            var endRow = Math.Clamp(startRow + rowDelta, 0, frame.Height - 1);
            var endColumn = Math.Clamp(startColumn + columnDelta, 0, frame.Width - 1);
            if (endRow == startRow)
            {
                endRow = Math.Max(0, startRow - rowDelta);
            }
            if (endColumn == startColumn)
            {
                endColumn = Math.Max(0, startColumn - columnDelta);
            }

            var start = HeightImage.PointToScreen(new Point(
                (startColumn + 0.5) / frame.Width * HeightImage.ActualWidth,
                (startRow + 0.5) / frame.Height * HeightImage.ActualHeight));
            var end = HeightImage.PointToScreen(new Point(
                (endColumn + 0.5) / frame.Width * HeightImage.ActualWidth,
                (endRow + 0.5) / frame.Height * HeightImage.ActualHeight));
            hasOriginalPointer = WindowsPointerInput.TryGetPosition(out originalPointer);
            WindowsPointerInput.MoveTo(start);
            await Task.Delay(120);
            if (!WindowsPointerInput.IsScreenPointOverWindow(
                    hostWindow,
                    start,
                    out targetDiagnostic))
            {
                throw new InvalidOperationException(
                    $"Height Image pointer target is not over the host window: {targetDiagnostic}");
            }

            WindowsPointerInput.LeftDown();
            leftPressed = true;
            await Task.Delay(100);
            WindowsPointerInput.MoveTo(end);
            await Task.Delay(180);
            WindowsPointerInput.LeftUp();
            leftPressed = false;
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(180);

            var after = viewModel.RoiWorkspace.Candidate;
            var passed = after is not null
                && after != before
                && !viewModel.RoiWorkspace.IsGestureActive
                && viewModel.RoiWorkspace.Lifecycle
                    == InspectionWorkspaceRegionLifecycleState.Review;
            return new HeightImageRoiPointerSmokeResult(
                passed,
                passed ? string.Empty : "Actual pointer drag did not produce a distinct Review candidate.",
                before,
                after,
                start,
                end,
                targetDiagnostic);
        }
        catch (Exception exception)
        {
            return new HeightImageRoiPointerSmokeResult(
                false,
                exception.Message,
                before,
                viewModel?.RoiWorkspace.Candidate,
                default,
                default,
                targetDiagnostic);
        }
        finally
        {
            if (leftPressed)
            {
                WindowsPointerInput.LeftUp();
            }
            if (hasOriginalPointer)
            {
                try
                {
                    WindowsPointerInput.MoveTo(originalPointer);
                }
                catch (Win32Exception)
                {
                    // Pointer restoration is best-effort smoke cleanup.
                }
            }
            if (hostWindow is not null)
            {
                hostWindow.Topmost = originalTopmost;
            }
        }
    }

    private void UpdateLinkedCursorOverlay()
    {
        if (viewModel?.Frame is not { } frame
            || !viewModel.HasLinkedCursor
            || viewModel.LinkedCursorColumn < 0
            || viewModel.LinkedCursorColumn >= frame.Width
            || viewModel.LinkedCursorRow < 0
            || viewModel.LinkedCursorRow >= frame.Height
            || !double.IsFinite(HeightImage.Width)
            || !double.IsFinite(HeightImage.Height)
            || HeightImage.Width <= 0
            || HeightImage.Height <= 0)
        {
            LinkedCursorOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        LinkedCursorOverlay.Width = HeightImage.Width;
        LinkedCursorOverlay.Height = HeightImage.Height;
        var x = (viewModel.LinkedCursorColumn + 0.5) / frame.Width * HeightImage.Width;
        var y = (viewModel.LinkedCursorRow + 0.5) / frame.Height * HeightImage.Height;
        var isOwnCursor = viewModel.LinkedCursorOrigin == SharedHeightCursorOrigin.HeightImage && HeightImage.IsMouseOver;
        LinkedCursorMarker.Visibility = isOwnCursor ? Visibility.Visible : Visibility.Collapsed;
        LinkedCursorPeerMarker.Visibility = isOwnCursor ? Visibility.Collapsed : Visibility.Visible;
        Canvas.SetLeft(LinkedCursorMarker, x - LinkedCursorMarker.Width / 2.0);
        Canvas.SetTop(LinkedCursorMarker, y - LinkedCursorMarker.Height / 2.0);
        Canvas.SetLeft(LinkedCursorPeerMarker, x - LinkedCursorPeerMarker.Width / 2.0);
        Canvas.SetTop(LinkedCursorPeerMarker, y - LinkedCursorPeerMarker.Height / 2.0);
        LinkedCursorOverlay.Visibility = Visibility.Visible;
    }

    private void Viewer_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        UpdateHeightAxes();
        if (IsLoaded && viewModel?.Frame is not null && imageScale < 1.0)
        {
            QueueFitImage(DispatcherPriority.Background);
        }
    }

    private void HeightBody_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        var height = ((FrameworkElement)sender).ActualHeight;
        var toolbarHeight = UseEmbeddedChrome ? HeightImageChrome.Margin.Top + HeightImageChrome.ActualHeight : 0;
        // 도구막대와 범례 머리글·정보 영역을 제외해 숫자가 막대 뒤로 들어가지 않게 한다.
        var plotRoom = height - toolbarHeight - 132;
        HeightLegendTrack.IsCompactLayout = plotRoom < 90;
        HeightLegendTrack.PlotHeight = new GridLength(Math.Max(90, Math.Min(Math.Clamp(height * 0.32, 90, 200), plotRoom)));
    }

    private void ImageScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs args) => UpdateHeightAxes();
    private void HeightImage_SizeChanged(object sender, SizeChangedEventArgs args) => UpdateHeightAxes();

    private void UpdateHeightAxes()
    {
        if (HeightAxes is null) return;
        HeightAxes.Children.Clear();
        if (viewModel?.Frame is not { } frame || HeightImage.ActualWidth <= 0) return;
        var origin = HeightImage.TranslatePoint(new Point(), HeightAxes);
        var width = Math.Max(0, ImageScrollViewer.ActualWidth);
        var height = Math.Max(0, ImageScrollViewer.ActualHeight);
        var text = new SolidColorBrush(Color.FromRgb(139, 152, 166));
        var grid = new SolidColorBrush(Color.FromRgb(42, 50, 59));
        HeightAxes.Children.Add(new Rectangle { Width = HeightAxes.ActualWidth, Height = 22, Fill = new SolidColorBrush(Color.FromRgb(20, 25, 32)) });
        HeightAxes.Children.Add(new Rectangle { Width = 44, Height = HeightAxes.ActualHeight, Fill = new SolidColorBrush(Color.FromRgb(20, 25, 32)) });
        HeightAxes.Children.Add(new Line { X1 = 0, X2 = HeightAxes.ActualWidth, Y1 = 21.5, Y2 = 21.5, Stroke = grid });
        HeightAxes.Children.Add(new Line { X1 = 43.5, X2 = 43.5, Y1 = 0, Y2 = HeightAxes.ActualHeight, Stroke = grid });
        AddLabel(viewModel.Localization.ViewerColumnAxis, 4, 5, 34);
        var rawStep = 70 / Math.Max(0.01, imageScale);
        var power = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var multiple = rawStep / power;
        var step = Math.Max(1, (multiple < 1.5 ? 1 : multiple < 3.5 ? 2 : multiple < 7.5 ? 5 : 10) * power);
        for (var column = Math.Max(0, Math.Ceiling((44 - origin.X) / imageScale / step) * step); column < frame.Width; column += step)
        {
            var x = origin.X + column * imageScale;
            if (x > 44 + width - 18) break;
            AddLabel(column.ToString("0", CultureInfo.InvariantCulture), x + 3, 3, 36, TextAlignment.Left);
            HeightAxes.Children.Add(new Line { X1 = x + 0.5, X2 = x + 0.5, Y1 = 16, Y2 = 22, Stroke = grid });
        }
        for (var row = Math.Max(0, Math.Ceiling((22 - origin.Y) / imageScale / step) * step); row < frame.Height; row += step)
        {
            var y = origin.Y + row * imageScale;
            if (y > 22 + height - 8) break;
            AddLabel(row.ToString("0", CultureInfo.InvariantCulture), 0, Math.Max(22, y - 6), 36, TextAlignment.Right);
            HeightAxes.Children.Add(new Line { X1 = 38, X2 = 44, Y1 = y + 0.5, Y2 = y + 0.5, Stroke = grid });
        }
        void AddLabel(string value, double x, double y, double labelWidth, TextAlignment alignment = TextAlignment.Center)
        {
            var label = new TextBlock { Text = value, Width = labelWidth, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Foreground = text, TextAlignment = alignment };
            Canvas.SetLeft(label, x);
            Canvas.SetTop(label, y);
            HeightAxes.Children.Add(label);
        }
    }
}

public sealed record HeightImageRoiPointerSmokeResult(
    bool Passed,
    string Failure,
    OpenVisionLab.ThreeD.Core.ToolRecipeGridRectangle? Before,
    OpenVisionLab.ThreeD.Core.ToolRecipeGridRectangle? After,
    Point StartScreenPoint,
    Point EndScreenPoint,
    string TargetDiagnostic);

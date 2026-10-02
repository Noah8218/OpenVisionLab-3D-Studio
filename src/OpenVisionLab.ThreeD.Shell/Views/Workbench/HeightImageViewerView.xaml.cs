using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
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
    private int profileDraggedEndpoint;
    private HeightImageViewerViewModel? viewModel;
    private double imageScale = 1.0;
    private bool isPanning;
    private Point panStart;
    private double panHorizontalOffset;
    private double panVerticalOffset;
    private DispatcherOperation? fitImageOperation;

    public HeightImageViewerView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
        CancelPendingFitImage();
        ReleasePointerGestures();
        DetachViewModel();
    }

    private void ReleasePointerGestures()
    {
        viewModel?.RoiWorkspace.HandlePointer(new HeightImageRoiPointerInput(HeightImageRoiPointerAction.Cancel));
        profileDraggedEndpoint = 0;
        if (HeightImage.IsMouseCaptured) HeightImage.ReleaseMouseCapture();
        if (isPanning) EndPan();
    }

    private void OnImageLostMouseCapture(object sender, MouseEventArgs args)
    {
        profileDraggedEndpoint = 0;
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
            or nameof(HeightImageViewerViewModel.LinkedCursorIsValid))
        {
            UpdateLinkedCursorOverlay();
        }
    }

    private void OnRoiWorkspacePropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (profileDraggedEndpoint == 0 && viewModel?.RoiWorkspace.IsGestureActive != true && HeightImage.IsMouseCaptured)
            HeightImage.ReleaseMouseCapture();
        UpdateRoiOverlay();
    }

    private static void OnProfileStateChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
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
        Point? previous = null;
        ViewerHeightProfileTraceCell? previousCell = null;
        foreach (var cell in profile.Trace)
        {
            var point = ProfilePosition(cell.Row, cell.Column)!.Value;
            if (previous is { } before && previousCell is { } prior && Math.Max(Math.Abs(prior.Row - cell.Row), Math.Abs(prior.Column - cell.Column)) == 1)
                ProfileOverlay.Children.Add(new Line { X1 = before.X, Y1 = before.Y, X2 = point.X, Y2 = point.Y, Stroke = Brushes.Goldenrod, StrokeThickness = 4 });
            previous = point;
            previousCell = cell;
        }
        var first = ProfilePosition(profile.FirstRow, profile.FirstColumn);
        var second = ProfilePosition(profile.SecondRow, profile.SecondColumn);
        var direction = first is { } a && second is { } b ? b - a : new Vector(1, 0);
        var side = direction.Length < 0.001 ? new Vector(0, 1) : new Vector(-direction.Y, direction.X);
        side.Normalize();
        if (first is { } start) DrawBar(start, true);
        if (second is { } end) DrawBar(end, false);

        void DrawBar(Point point, bool isFirst)
        {
            var offset = side * 12;
            var color = isFirst ? Brushes.Cyan : Brushes.OrangeRed;
            ProfileOverlay.Children.Add(new Line { X1 = point.X - offset.X, Y1 = point.Y - offset.Y, X2 = point.X + offset.X, Y2 = point.Y + offset.Y, Stroke = color, StrokeThickness = 4 });
            var label = new TextBlock { Text = isFirst ? "A" : "B", Foreground = Brushes.White, Background = Brushes.Black, FontWeight = FontWeights.Bold, FontSize = 12, Padding = new Thickness(2) };
            Canvas.SetLeft(label, point.X + offset.X + 3);
            Canvas.SetTop(label, point.Y + offset.Y);
            ProfileOverlay.Children.Add(label);
        }
    }

    private void RefreshFrame()
    {
        if (viewModel?.DisplayFrame is not { } displayFrame)
        {
            HeightImage.Source = null;
            HeightImage.Width = 0;
            HeightImage.Height = 0;
            RoiOverlay.Children.Clear();
            UpdateLinkedCursorOverlay();
            return;
        }

        var pixels = displayFrame.Bgra32Pixels.ToArray();
        var bitmap = BitmapSource.Create(
            displayFrame.Width,
            displayFrame.Height,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            pixels,
            displayFrame.Stride);
        bitmap.Freeze();
        HeightImage.Source = bitmap;
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

        var horizontalScale = Math.Max(0.01, (ImageScrollViewer.ViewportWidth - 8) / frame.Width);
        var verticalScale = Math.Max(0.01, (ImageScrollViewer.ViewportHeight - 8) / frame.Height);
        SetImageScale(Math.Min(horizontalScale, verticalScale));
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
        UpdateLinkedCursorOverlay();
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
        if (args.ChangedButton != MouseButton.Middle)
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
        if (!isPanning || args.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        EndPan();
        args.Handled = true;
    }

    private void ImageScrollViewer_MouseLeave(object sender, MouseEventArgs args)
    {
        if (isPanning && args.MiddleButton != MouseButtonState.Pressed)
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
            if (ProfilePickCommand?.CanExecute(pick) == true) ProfilePickCommand.Execute(pick);
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
        var shape = new Rectangle
        {
            Width = Math.Max(1, right - left),
            Height = Math.Max(1, bottom - top),
            Fill = new SolidColorBrush(Color.FromArgb(
                overlay.IsCandidate ? (byte)35 : (byte)18,
                ((SolidColorBrush)roleBrush).Color.R,
                ((SolidColorBrush)roleBrush).Color.G,
                ((SolidColorBrush)roleBrush).Color.B)),
            Stroke = roleBrush,
            StrokeThickness = overlay.IsActive ? 3.0 : 2.0,
            StrokeDashArray = overlay.IsCandidate
                ? new DoubleCollection([5, 3])
                : null
        };
        Canvas.SetLeft(shape, left);
        Canvas.SetTop(shape, top);
        RoiOverlay.Children.Add(shape);

        if (!overlay.IsActive)
        {
            return;
        }

        var label = new Border
        {
            Padding = new Thickness(5, 2, 5, 2),
            Background = new SolidColorBrush(Color.FromArgb(220, 17, 24, 39)),
            BorderBrush = roleBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(2),
            Child = new TextBlock
            {
                Text = $"{overlay.Name} · {FormatRoiLifecycle(overlay.Lifecycle)}",
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            }
        };
        Canvas.SetLeft(label, Math.Max(0, left));
        Canvas.SetTop(label, Math.Max(0, top - 23));
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
                FrozenBrush(Color.FromRgb(0, 240, 232)),
            InspectionWorkspaceRegionRole.Measurement or InspectionWorkspaceRegionRole.Second =>
                FrozenBrush(Color.FromRgb(255, 159, 28)),
            _ => FrozenBrush(Color.FromRgb(232, 121, 249))
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
        var cursorBrush = viewModel.LinkedCursorIsValid
            ? Brushes.Cyan
            : Brushes.Orange;
        var guideBrush = viewModel.LinkedCursorIsValid
            ? new SolidColorBrush(Color.FromArgb(150, 0, 240, 232))
            : new SolidColorBrush(Color.FromArgb(180, 251, 146, 60));
        if (guideBrush.CanFreeze)
        {
            guideBrush.Freeze();
        }

        SetLine(
            LinkedCursorVerticalLine,
            x,
            0,
            x,
            HeightImage.Height,
            guideBrush);
        SetLine(
            LinkedCursorHorizontalLine,
            0,
            y,
            HeightImage.Width,
            y,
            guideBrush);
        LinkedCursorMarker.Stroke = cursorBrush;
        LinkedCursorMarker.Fill = new SolidColorBrush(
            viewModel.LinkedCursorIsValid
                ? Color.FromArgb(70, 0, 240, 232)
                : Color.FromArgb(75, 251, 146, 60));
        Canvas.SetLeft(LinkedCursorMarker, x - LinkedCursorMarker.Width / 2.0);
        Canvas.SetTop(LinkedCursorMarker, y - LinkedCursorMarker.Height / 2.0);
        LinkedCursorOverlay.Visibility = Visibility.Visible;
    }

    private static void SetLine(
        Line line,
        double x1,
        double y1,
        double x2,
        double y2,
        Brush stroke)
    {
        line.X1 = x1;
        line.Y1 = y1;
        line.X2 = x2;
        line.Y2 = y2;
        line.Stroke = stroke;
    }

    private void Viewer_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (IsLoaded && viewModel?.Frame is not null && imageScale < 1.0)
        {
            QueueFitImage(DispatcherPriority.Background);
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

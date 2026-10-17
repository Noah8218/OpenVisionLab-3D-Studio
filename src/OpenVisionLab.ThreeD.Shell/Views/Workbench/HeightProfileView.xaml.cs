using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

public sealed partial class HeightProfileView : UserControl, IDisposable
{
    public static readonly DependencyProperty UseEmbeddedChromeProperty = DependencyProperty.Register(nameof(UseEmbeddedChrome), typeof(bool), typeof(HeightProfileView), new PropertyMetadata(false));
    public bool UseEmbeddedChrome { get => (bool)GetValue(UseEmbeddedChromeProperty); set => SetValue(UseEmbeddedChromeProperty, value); }

    public static readonly DependencyProperty UseTeachingDrawerLayoutProperty = DependencyProperty.Register(nameof(UseTeachingDrawerLayout), typeof(bool), typeof(HeightProfileView), new PropertyMetadata(false, OnProfileStateChanged));
    public bool UseTeachingDrawerLayout { get => (bool)GetValue(UseTeachingDrawerLayoutProperty); set => SetValue(UseTeachingDrawerLayoutProperty, value); }

    private static readonly DependencyPropertyKey IsTeachingDrawerCompactPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsTeachingDrawerCompact), typeof(bool), typeof(HeightProfileView), new PropertyMetadata(false));
    public static readonly DependencyProperty IsTeachingDrawerCompactProperty = IsTeachingDrawerCompactPropertyKey.DependencyProperty;
    public bool IsTeachingDrawerCompact => (bool)GetValue(IsTeachingDrawerCompactProperty);

    private static readonly DependencyPropertyKey IsTeachingDrawerNarrowPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsTeachingDrawerNarrow), typeof(bool), typeof(HeightProfileView), new PropertyMetadata(false));
    public static readonly DependencyProperty IsTeachingDrawerNarrowProperty = IsTeachingDrawerNarrowPropertyKey.DependencyProperty;
    public bool IsTeachingDrawerNarrow => (bool)GetValue(IsTeachingDrawerNarrowProperty);

    public static readonly DependencyProperty ShowCoordinateFooterProperty = DependencyProperty.Register(nameof(ShowCoordinateFooter), typeof(bool), typeof(HeightProfileView), new PropertyMetadata(true));
    public bool ShowCoordinateFooter { get => (bool)GetValue(ShowCoordinateFooterProperty); set => SetValue(ShowCoordinateFooterProperty, value); }

    public static readonly DependencyProperty ViewerHostProperty =
        DependencyProperty.Register(
            nameof(ViewerHost),
            typeof(IOpenVisionThreeDViewerHost),
            typeof(HeightProfileView),
            new PropertyMetadata(null, OnViewerHostChanged));

    public static readonly DependencyProperty TraceCursorCommandProperty = DependencyProperty.Register(nameof(TraceCursorCommand), typeof(ICommand), typeof(HeightProfileView));
    public ICommand? TraceCursorCommand { get => (ICommand?)GetValue(TraceCursorCommandProperty); set => SetValue(TraceCursorCommandProperty, value); }
    public static readonly DependencyProperty ProfileStateProperty = DependencyProperty.Register(nameof(ProfileState), typeof(ViewerHostProfileState), typeof(HeightProfileView), new PropertyMetadata(ViewerHostProfileState.Empty, OnProfileStateChanged));
    public static readonly DependencyProperty SelectionContextProperty = DependencyProperty.Register(nameof(SelectionContext), typeof(string), typeof(HeightProfileView), new PropertyMetadata(string.Empty));
    public ViewerHostProfileState ProfileState { get => (ViewerHostProfileState)GetValue(ProfileStateProperty); set => SetValue(ProfileStateProperty, value); }
    public string SelectionContext { get => (string)GetValue(SelectionContextProperty); set => SetValue(SelectionContextProperty, value); }
    public static readonly DependencyProperty DrawerCursorSummaryProperty = DependencyProperty.Register(nameof(DrawerCursorSummary), typeof(string), typeof(HeightProfileView), new PropertyMetadata(string.Empty, OnProfileStateChanged));
    public string DrawerCursorSummary { get => (string)GetValue(DrawerCursorSummaryProperty); set => SetValue(DrawerCursorSummaryProperty, value); }
    private HeightProfileViewModel? viewModel;
    private string renderedPath = string.Empty;
    private PathGeometry? normalizedGeometry;
    private double plotLeft = 58;

    public HeightProfileView()
    {
        InitializeComponent();
        SetBinding(TraceCursorCommandProperty, new Binding(nameof(HeightProfileViewModel.TraceCursorCommand)));
        SetBinding(ProfileStateProperty, new Binding(nameof(HeightProfileViewModel.ProfileState)));
        SetBinding(DrawerCursorSummaryProperty, new Binding(nameof(HeightProfileViewModel.ProfileDrawerLinkedCursorSummary)));
    }

    public IOpenVisionThreeDViewerHost? ViewerHost
    {
        get => (IOpenVisionThreeDViewerHost?)GetValue(ViewerHostProperty);
        set => SetValue(ViewerHostProperty, value);
    }

    private void OnTraceMouseMove(object sender, MouseEventArgs args)
    {
        if (sender is not Canvas chart || chart.ActualWidth <= 0) return;
        var progress = (args.GetPosition(chart).X - plotLeft) / Math.Max(1, chart.ActualWidth - plotLeft - 16);
        progress = UseTeachingDrawerLayout && (progress < 0 || progress > 1) ? double.NaN : Math.Clamp(progress, 0, 1);
        if (TraceCursorCommand?.CanExecute(progress) == true) TraceCursorCommand.Execute(progress);
    }

    private void OnTraceMouseLeave(object sender, MouseEventArgs args)
    {
        if (UseTeachingDrawerLayout && TraceCursorCommand?.CanExecute(double.NaN) == true) TraceCursorCommand.Execute(double.NaN);
    }

    private static void OnProfileStateChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args) => ((HeightProfileView)owner).RenderChart();
    private void OnChartSizeChanged(object sender, SizeChangedEventArgs args) => RenderChart();

    private void RenderChart()
    {
        if (ProfileChartCanvas is null) return;
        SetValue(IsTeachingDrawerCompactPropertyKey, UseTeachingDrawerLayout && ActualWidth < 650);
        SetValue(IsTeachingDrawerNarrowPropertyKey, UseTeachingDrawerLayout && ActualWidth > 0 && ActualWidth <= 490);
        var canvas = ProfileChartCanvas;
        canvas.Children.Clear();
        var state = ProfileState;
        plotLeft = 58;
        var width = Math.Max(0, canvas.ActualWidth - plotLeft - 16);
        var height = Math.Max(0, canvas.ActualHeight - 28);
        if (width < 1 || height < 1 || !state.HasEndpoints || state.IsCollapsed || state.ValidSampleCount <= 0
            || !double.IsFinite(state.MinimumRaw) || !double.IsFinite(state.MaximumRaw)) return;
        var textBrush = (Brush)FindResource("ThreeD.MutedTextBrush");
        var gridBrush = (Brush)FindResource("ThreeD.DividerBrush");
        var lineBrush = (Brush)FindResource("ThreeD.WarningBrush");
        var yIntervals = ProfileChartBody.ActualHeight > 180 ? 5 : 3;
        var span = Math.Max(0.001, state.MaximumRaw - state.MinimumRaw);
        var rawRange = state.MaximumRaw - state.MinimumRaw;
        var axisPad = Math.Max(rawRange * 0.08, 0.05);
        var axisRange = rawRange + 2 * axisPad;
        if (UseTeachingDrawerLayout)
        {
            for (var index = 0; index <= yIntervals; index++)
            {
                var tick = new TextBlock { Text = (state.MaximumRaw + axisPad - axisRange * index / yIntervals).ToString("0.00", CultureInfo.InvariantCulture), FontFamily = new FontFamily("Consolas"), FontSize = 10.5 };
                tick.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                plotLeft = Math.Max(plotLeft, Math.Ceiling(tick.DesiredSize.Width) + 8);
            }
        }
        ProfileWideInfo.Margin = ProfileCompactInfo.Margin = new Thickness(plotLeft, 0, 16, 0);
        width = Math.Max(0, canvas.ActualWidth - plotLeft - 16);
        if (width < 1) return;
        var yScale = UseTeachingDrawerLayout ? height * span / (48 * axisRange) : height / 54;
        var yOffset = UseTeachingDrawerLayout ? height * (state.MaximumRaw + axisPad - state.MinimumRaw - span * 51 / 48) / axisRange : 0;
        for (var index = 0; index <= yIntervals; index++)
        {
            var y = height * index / yIntervals;
            AddLine(plotLeft, y, plotLeft + width, y, gridBrush);
            // The existing 240x54 path reserves three units at each vertical edge.
            var value = UseTeachingDrawerLayout ? state.MaximumRaw + axisPad - axisRange * index / yIntervals : state.MaximumRaw + span * 3 / 48 - span * 54 / 48 * index / yIntervals;
            AddLabel(value.ToString("0.00", CultureInfo.InvariantCulture), 2, UseTeachingDrawerLayout ? y - 6 : Math.Clamp(y - 6, 0, Math.Max(0, height - 12)), plotLeft - 8, TextAlignment.Right);
        }
        var xIntervals = Math.Max(2, (int)(width / 110));
        for (var index = 0; index <= xIntervals; index++)
        {
            var x = plotLeft + width * index / xIntervals;
            AddLine(x, 0, x, height, gridBrush);
            AddLabel((state.GridDistanceCells * index / xIntervals).ToString("0", CultureInfo.InvariantCulture), UseTeachingDrawerLayout ? x - 24 : Math.Clamp(x - 24, 34, Math.Max(34, canvas.ActualWidth - 48)), height + 6, 48, TextAlignment.Center);
        }
        if (renderedPath != state.PathData)
        {
            renderedPath = state.PathData;
            normalizedGeometry = string.IsNullOrWhiteSpace(renderedPath) ? null : Geometry.Parse(renderedPath).GetFlattenedPathGeometry();
        }
        if (normalizedGeometry is { } normalized)
        {
            for (var index = 1; index < normalized.Figures.Count; index++)
            {
                var left = plotLeft + LastPoint(normalized.Figures[index - 1]).X / 240 * width;
                var right = plotLeft + normalized.Figures[index].StartPoint.X / 240 * width;
                var bandWidth = Math.Max(0, right - left);
                if (bandWidth < 1) continue;
                var hatch = new Canvas { Width = bandWidth, Height = height, ClipToBounds = true, Background = UseTeachingDrawerLayout ? new SolidColorBrush(Color.FromRgb(241, 194, 122)) : lineBrush, Opacity = UseTeachingDrawerLayout ? 0.12 : 0.14, IsHitTestVisible = false };
                if (!UseTeachingDrawerLayout)
                    for (var offset = -height; offset < bandWidth; offset += 8)
                        hatch.Children.Add(new Line { X1 = offset, Y1 = height, X2 = offset + height, Y2 = 0, Stroke = textBrush, StrokeThickness = 1 });
                Canvas.SetLeft(hatch, left);
                canvas.Children.Add(hatch);
                if (UseTeachingDrawerLayout)
                {
                    var caption = new TextBlock { Text = ThreeDLocalization.Shared.Resolve("ThreeD.Profile.Missing", "결측", "Missing"), Foreground = hatch.Background, FontSize = 11, TextAlignment = TextAlignment.Center, Width = bandWidth, IsHitTestVisible = false };
                    Canvas.SetLeft(caption, left);
                    Canvas.SetTop(caption, 4);
                    canvas.Children.Add(caption);
                }
            }
            var transform = new MatrixTransform(width / 240, 0, 0, yScale, plotLeft, yOffset);
            var curve = normalized.Clone();
            curve.Transform = transform;
            var area = normalized.Clone();
            foreach (var figure in area.Figures)
            {
                var last = LastPoint(figure);
                var baseline = (height - yOffset) / yScale;
                figure.Segments.Add(new LineSegment(new Point(last.X, baseline), true));
                figure.Segments.Add(new LineSegment(new Point(figure.StartPoint.X, baseline), true));
                figure.IsClosed = true;
            }
            area.Transform = transform;
            var fill = new LinearGradientBrush(Color.FromArgb(56, 255, 209, 102), Colors.Transparent, new Point(0, 0), new Point(0, 1));
            canvas.Children.Add(new Path { Data = area, Fill = fill, IsHitTestVisible = false });
            canvas.Children.Add(new Path { Data = curve, Stroke = lineBrush, StrokeThickness = 1.8, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false });
        }
        AddEndpoint("A", plotLeft);
        AddEndpoint("B", plotLeft + width);
        if (state.MissingSampleCount > 0 && !UseTeachingDrawerLayout)
            AddLabel($"{state.ValidSampleCount} / {state.ValidSampleCount + state.MissingSampleCount}", 64, 8, 100, TextAlignment.Left);
        if (state.LinkedCursorVisible)
        {
            var x = plotLeft + Math.Clamp(state.LinkedCursorX / 240, 0, 1) * width;
            var y = Math.Clamp(state.LinkedCursorY * yScale + yOffset, 0, height);
            var cursorBrush = UseTeachingDrawerLayout ? new SolidColorBrush(Color.FromArgb(153, 228, 233, 239)) : (Brush)FindResource("ThreeD.InfoBrush");
            var cursor = new Line { X1 = x, X2 = x, Y1 = 0, Y2 = height, Stroke = cursorBrush, StrokeThickness = 1, StrokeDashArray = new DoubleCollection(UseTeachingDrawerLayout ? [3, 3] : [4, 3]), IsHitTestVisible = false };
            canvas.Children.Add(cursor);
            var markerSize = UseTeachingDrawerLayout ? 8 : 7;
            var marker = new Ellipse { Width = markerSize, Height = markerSize, Fill = lineBrush, Stroke = UseTeachingDrawerLayout ? null : cursorBrush, StrokeThickness = 1, IsHitTestVisible = false };
            Canvas.SetLeft(marker, x - markerSize / 2.0);
            Canvas.SetTop(marker, y - markerSize / 2.0);
            canvas.Children.Add(marker);
            var readout = new Border { Background = (Brush)FindResource("ThreeD.CommandBarBrush"), BorderBrush = gridBrush, BorderThickness = new Thickness(1), Padding = new Thickness(6, 3, 6, 3), MaxWidth = Math.Min(280, width), IsHitTestVisible = false,
                Child = new TextBlock { Text = UseTeachingDrawerLayout ? DrawerCursorSummary : state.LinkedCursorSummary, FontSize = UseTeachingDrawerLayout ? 11.5 : 11, FontFamily = new FontFamily("Consolas"), Foreground = UseTeachingDrawerLayout ? new SolidColorBrush(Color.FromRgb(243, 217, 174)) : cursorBrush, TextWrapping = TextWrapping.Wrap } };
            if (UseTeachingDrawerLayout)
            {
                ((TextBlock)readout.Child).FontWeight = FontWeights.SemiBold;
                readout.Background = new SolidColorBrush(Color.FromArgb(235, 17, 22, 28));
                readout.BorderBrush = new SolidColorBrush(Color.FromRgb(107, 86, 52));
                readout.CornerRadius = new CornerRadius(4);
                readout.Padding = new Thickness(6, 0, 6, 0);
                readout.MinHeight = 19;
                ((TextBlock)readout.Child).FontWeight = FontWeights.SemiBold;
                ((TextBlock)readout.Child).VerticalAlignment = VerticalAlignment.Center;
            }
            readout.Measure(new Size(Math.Min(280, width), double.PositiveInfinity));
            Canvas.SetLeft(readout, Math.Clamp(x + (UseTeachingDrawerLayout ? 8 : 12), plotLeft, Math.Max(plotLeft, plotLeft + width - readout.DesiredSize.Width)));
            Canvas.SetTop(readout, Math.Clamp(y - readout.DesiredSize.Height - 8, 0, Math.Max(0, height - readout.DesiredSize.Height)));
            System.Windows.Automation.AutomationProperties.SetAutomationId(readout, "ProfileCursorReadout");
            canvas.Children.Add(readout);
        }

        void AddLine(double x1, double y1, double x2, double y2, Brush brush)
        {
            var offsetX = UseTeachingDrawerLayout && x1 == x2 ? 0.5 : 0;
            var offsetY = UseTeachingDrawerLayout && y1 == y2 ? 0.5 : 0;
            canvas.Children.Add(new Line { X1 = x1 + offsetX, Y1 = y1 + offsetY, X2 = x2 + offsetX, Y2 = y2 + offsetY, Stroke = brush, StrokeThickness = UseTeachingDrawerLayout ? 1 : 0.7, IsHitTestVisible = false });
        }
        void AddLabel(string text, double left, double top, double labelWidth, TextAlignment alignment)
        {
            var label = new TextBlock { Text = text, Width = labelWidth, FontFamily = new FontFamily("Consolas"), FontSize = 10.5, Foreground = textBrush, TextAlignment = alignment, IsHitTestVisible = false };
            if (UseTeachingDrawerLayout) label.FontWeight = FontWeights.Normal;
            Canvas.SetLeft(label, left);
            Canvas.SetTop(label, top);
            canvas.Children.Add(label);
        }
        void AddEndpoint(string name, double x)
        {
            var marker = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = lineBrush, IsHitTestVisible = false,
                Child = new TextBlock { Text = name, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = UseTeachingDrawerLayout ? new SolidColorBrush(Color.FromRgb(26, 20, 8)) : (Brush)FindResource("ThreeD.ViewportBrush"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            Canvas.SetLeft(marker, x - 7);
            Canvas.SetTop(marker, height - 7);
            canvas.Children.Add(marker);
        }
    }

    private static Point LastPoint(PathFigure figure) => figure.Segments.LastOrDefault() switch
    {
        PolyLineSegment segment when segment.Points.Count > 0 => segment.Points[^1],
        LineSegment segment => segment.Point,
        _ => figure.StartPoint
    };

    public void Dispose()
    {
        ViewerHost = null;
        DataContext = null;
    }

    private static void OnViewerHostChanged(
        DependencyObject owner,
        DependencyPropertyChangedEventArgs args)
    {
        if (owner is not HeightProfileView view)
        {
            return;
        }

        view.viewModel?.Dispose();
        view.viewModel = args.NewValue is IOpenVisionThreeDViewerHost host
            ? new HeightProfileViewModel(host)
            : null;
        view.DataContext = view.viewModel;
    }
}

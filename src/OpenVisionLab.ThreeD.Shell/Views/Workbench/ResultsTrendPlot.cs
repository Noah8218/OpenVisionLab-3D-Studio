using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

/// <summary>Draws saved measurements and forwards explicit record-open gestures; owns no inspection state.</summary>
public sealed class ResultsTrendPlot : FrameworkElement
{
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<ResultsTrendPoint>), typeof(ResultsTrendPlot),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnPointsChanged));
    public static readonly DependencyProperty OpenRecordCommandProperty = DependencyProperty.Register(
        nameof(OpenRecordCommand), typeof(ICommand), typeof(ResultsTrendPlot));
    private readonly List<Point> renderedPoints = [];
    private int selectedIndex;
    private int hoverIndex = -1;
    private int pressedIndex = -1;

    public ResultsTrendPlot()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        MinHeight = 180;
    }

    public IReadOnlyList<ResultsTrendPoint>? Points
    {
        get => (IReadOnlyList<ResultsTrendPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    public ICommand? OpenRecordCommand
    {
        get => (ICommand?)GetValue(OpenRecordCommandProperty);
        set => SetValue(OpenRecordCommandProperty, value);
    }

    private static void OnPointsChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var plot = (ResultsTrendPlot)sender;
        plot.renderedPoints.Clear();
        plot.hoverIndex = plot.pressedIndex = -1;
        plot.selectedIndex = 0;
        plot.ToolTip = null;
        if (plot.IsMouseCaptured) plot.ReleaseMouseCapture();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        drawing.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        renderedPoints.Clear();
        var points = Points;
        if (points is not { Count: > 0 } || ActualWidth < 110 || ActualHeight < 90) return;
        var plot = new Rect(58, 30, ActualWidth - 76, ActualHeight - 66);
        var minimum = points.Min(point => point.Metric.Value);
        var maximum = points.Max(point => point.Metric.Value);
        var magnitude = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
        if (magnitude == 0) magnitude = 1;
        var low = minimum / magnitude;
        var high = maximum / magnitude;
        var span = high - low;
        if (span < 1e-12) { low -= .05; high += .05; span = high - low; }
        var gridPen = new Pen(Brush("ThreeD.SoftDividerBrush"), 1);
        for (var tick = 0; tick <= 5; tick++)
        {
            var fraction = tick / 5.0;
            var y = plot.Bottom - plot.Height * fraction;
            drawing.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            var value = (low * (1 - fraction) + high * fraction) * magnitude;
            if (!double.IsFinite(value)) value = fraction < .5 ? minimum : maximum;
            Text(drawing, value.ToString("0.##", CultureInfo.CurrentCulture), new Point(0, y - 7), 11, Brush("ThreeD.MutedTextBrush"));
        }
        Text(drawing, points[0].Metric.Unit, new Point(plot.Left, 5), 11, Brush("ThreeD.SecondaryTextBrush"));
        if (IsKeyboardFocusWithin)
        {
            var selected = points[Math.Clamp(selectedIndex, 0, points.Count - 1)];
            Text(drawing, $"{selected.Metric.ValueText} {selected.Unit} · {selected.Status}",
                new Point(plot.Left + 100, 5), 11, Brush("ThreeD.TextBrush"));
        }
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = points.Count == 1 ? plot.Left + plot.Width / 2 : plot.Left + plot.Width * index / (points.Count - 1);
            var y = plot.Bottom - plot.Height * (point.Metric.Value / magnitude - low) / span;
            var position = new Point(x, y);
            renderedPoints.Add(position);
            var sameSettings = index > 0 && point.Metric.SettingsIdentity == points[index - 1].Metric.SettingsIdentity;
            if (sameSettings) drawing.DrawLine(new Pen(Brush("ThreeD.AccentBrush"), 1), renderedPoints[index - 1], position);
            else if (index > 0)
                drawing.DrawLine(new Pen(Brush("ThreeD.DividerBrush"), 1) { DashStyle = DashStyles.Dash },
                    new Point(x - 4, plot.Top), new Point(x - 4, plot.Bottom));
            var isFailure = point.Metric.Status is ResultStatus.Fail or ResultStatus.Error;
            var pointBrush = Brush(isFailure ? "ThreeD.FailBrush" : point.Metric.Status == ResultStatus.Pass
                ? "ThreeD.PassBrush" : "ThreeD.SecondaryTextBrush");
            var radius = index == hoverIndex || index == pressedIndex ? 5 : 3.5;
            if (isFailure)
            {
                var pen = new Pen(pointBrush, index == pressedIndex ? 3 : 2);
                drawing.DrawLine(pen, new Point(x - radius, y - radius), new Point(x + radius, y + radius));
                drawing.DrawLine(pen, new Point(x - radius, y + radius), new Point(x + radius, y - radius));
            }
            else drawing.DrawEllipse(pointBrush, null, position, radius, radius);
            if (IsKeyboardFocusWithin && index == Math.Clamp(selectedIndex, 0, points.Count - 1))
                drawing.DrawEllipse(null, new Pen(Brush("ThreeD.FocusBrush"), 1), position, 8, 8);
        }
        Text(drawing, points[0].Snapshot.RecordedAt.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.CurrentCulture),
            new Point(plot.Left, plot.Bottom + 10), 11, Brush("ThreeD.MutedTextBrush"));
        if (points.Count > 1)
            Text(drawing, points[^1].Snapshot.RecordedAt.ToLocalTime().ToString("MM-dd HH:mm", CultureInfo.CurrentCulture),
                new Point(Math.Max(plot.Left, plot.Right - 85), plot.Bottom + 10), 11, Brush("ThreeD.MutedTextBrush"));
    }

    protected override void OnMouseMove(MouseEventArgs args)
    {
        base.OnMouseMove(args);
        hoverIndex = FindPoint(args.GetPosition(this));
        ToolTip = hoverIndex >= 0 && Points is { } points
            ? $"{points[hoverIndex].Snapshot.SourceName} · {points[hoverIndex].Metric.ValueText} {points[hoverIndex].Unit}\n{points[hoverIndex].Status} · {points[hoverIndex].RecordedAt}\n{points[hoverIndex].RecipeIdentity}"
            : null;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs args)
    {
        base.OnMouseLeave(args);
        hoverIndex = -1;
        ToolTip = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);
        pressedIndex = FindPoint(args.GetPosition(this));
        if (pressedIndex < 0) return;
        selectedIndex = pressedIndex;
        Focus();
        CaptureMouse();
        args.Handled = true;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);
        var index = pressedIndex;
        pressedIndex = -1;
        if (IsMouseCaptured) ReleaseMouseCapture();
        if (index >= 0 && index == FindPoint(args.GetPosition(this))) Open(index);
        InvalidateVisual();
    }

    protected override void OnLostMouseCapture(MouseEventArgs args)
    {
        base.OnLostMouseCapture(args);
        pressedIndex = -1;
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (Points is not { Count: > 0 } points) return;
        if (args.Key is Key.Left or Key.Right)
        {
            selectedIndex = Math.Clamp(selectedIndex + (args.Key == Key.Left ? -1 : 1), 0, points.Count - 1);
            args.Handled = true;
        }
        else if (args.Key is Key.Enter or Key.Space) { Open(Math.Clamp(selectedIndex, 0, points.Count - 1)); args.Handled = true; }
        InvalidateVisual();
    }

    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs args) { base.OnGotKeyboardFocus(args); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs args) { base.OnLostKeyboardFocus(args); InvalidateVisual(); }

    private void Open(int index)
    {
        if (Points is { } points && index >= 0 && index < points.Count
            && OpenRecordCommand?.CanExecute(points[index].Record) == true)
            OpenRecordCommand.Execute(points[index].Record);
    }

    private int FindPoint(Point pointer)
    {
        for (var index = renderedPoints.Count - 1; index >= 0; index--)
            if ((renderedPoints[index] - pointer).Length <= 10) return index;
        return -1;
    }

    private Brush Brush(string key) => TryFindResource(key) as Brush ?? Brushes.Gray;
    private void Text(DrawingContext drawing, string text, Point origin, double size, Brush brush) =>
        drawing.DrawText(new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip), origin);
}

using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

/// <summary>Displays measured validation values; sample selection is a two-way View contract.</summary>
public sealed class ValidationDistributionPlot : FrameworkElement
{
    public static readonly DependencyProperty OutputProperty = DependencyProperty.Register(
        nameof(Output), typeof(ValidationDistributionOutput), typeof(ValidationDistributionPlot),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnOutputChanged));
    public static readonly DependencyProperty CandidateProperty = DependencyProperty.Register(
        nameof(Candidate), typeof(ValidationThresholdCandidateRow), typeof(ValidationDistributionPlot),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty SelectedSampleProperty = DependencyProperty.Register(
        nameof(SelectedSample), typeof(ValidationSetSampleRow), typeof(ValidationDistributionPlot),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    private readonly List<(Point Position, ValidationDistributionPoint Value)> renderedPoints = [];
    private int hoveredIndex = -1;
    private int pressedIndex = -1;

    public ValidationDistributionPlot()
    {
        Focusable = true;
        Cursor = Cursors.Hand;
        Height = 156;
    }

    public ValidationDistributionOutput? Output
    {
        get => (ValidationDistributionOutput?)GetValue(OutputProperty);
        set => SetValue(OutputProperty, value);
    }
    public ValidationThresholdCandidateRow? Candidate
    {
        get => (ValidationThresholdCandidateRow?)GetValue(CandidateProperty);
        set => SetValue(CandidateProperty, value);
    }
    public ValidationSetSampleRow? SelectedSample
    {
        get => (ValidationSetSampleRow?)GetValue(SelectedSampleProperty);
        set => SetValue(SelectedSampleProperty, value);
    }

    private static void OnOutputChanged(DependencyObject owner, DependencyPropertyChangedEventArgs args)
    {
        var plot = (ValidationDistributionPlot)owner;
        plot.renderedPoints.Clear();
        plot.hoveredIndex = plot.pressedIndex = -1;
        plot.ToolTip = null;
        if (plot.IsMouseCaptured) plot.ReleaseMouseCapture();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        drawing.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        renderedPoints.Clear();
        if (Output is not { } output || ActualWidth < 120) return;
        var values = output.Points.Select(point => point.Value).Where(double.IsFinite).ToList();
        if (output.Minimum is { } minimum) values.Add(minimum);
        if (output.Maximum is { } maximum) values.Add(maximum);
        if (values.Count == 0)
        {
            Text(drawing, "—", new Point(40, 60), Brush("ThreeD.MutedTextBrush"));
            return;
        }
        var magnitude = values.Max(value => Math.Abs(value));
        if (magnitude == 0) magnitude = 1;
        var low = values.Min() / magnitude;
        var high = values.Max() / magnitude;
        var padding = Math.Max((high - low) * .12, .02);
        low -= padding;
        high += padding;
        var bounds = new Rect(38, 24, ActualWidth - 58, 106);
        double X(double value) => bounds.Left + (value / magnitude - low) / (high - low) * bounds.Width;
        var gridPen = new Pen(Brush("ThreeD.SoftDividerBrush"), 1);
        for (var index = 0; index <= 5; index++)
        {
            var fraction = index / 5.0;
            var x = bounds.Left + bounds.Width * fraction;
            drawing.DrawLine(gridPen, new Point(x, bounds.Top), new Point(x, bounds.Bottom));
            var value = (low * (1 - fraction) + high * fraction) * magnitude;
            if (double.IsFinite(value)) Text(drawing, value.ToString("G4", CultureInfo.CurrentCulture), new Point(x - 17, 138), Brush("ThreeD.MutedTextBrush"));
        }
        if (output.Minimum is not null || output.Maximum is not null)
        {
            var left = output.Minimum is { } lower ? X(lower) : bounds.Left;
            var right = output.Maximum is { } upper ? X(upper) : bounds.Right;
            var rangeBrush = Brush("ThreeD.PassSurfaceBrush");
            drawing.DrawRectangle(rangeBrush, new Pen(Brush("ThreeD.PassBrush"), 1) { DashStyle = DashStyles.Dash }, new Rect(left, bounds.Top, Math.Max(1, right - left), bounds.Height));
            Text(drawing, output.RangeText, new Point(bounds.Left, 5), Brush("ThreeD.PassBrush"));
        }
        if (Candidate?.Candidate is { } candidate && candidate.Scope == ToolRecipeEvidenceScope.StepMetric
            && candidate.OwnerId == output.Column.StepId && candidate.MetricName == output.Column.MetricName && candidate.Unit == output.Column.Unit)
        {
            var left = candidate.Minimum is { } lower ? Math.Clamp(X(lower), bounds.Left, bounds.Right) : bounds.Left;
            var right = candidate.Maximum is { } upper ? Math.Clamp(X(upper), bounds.Left, bounds.Right) : bounds.Right;
            drawing.DrawRectangle(null, new Pen(Brush("ThreeD.InfoBrush"), 1.4) { DashStyle = DashStyles.Dash }, new Rect(left, bounds.Top + 5, Math.Max(1, right - left), bounds.Height - 10));
        }
        Text(drawing, "OK ○", new Point(0, 44), Brush("ThreeD.PassBrush"));
        Text(drawing, "NG ◆", new Point(0, 94), Brush("ThreeD.FailBrush"));
        Text(drawing, "Ⅱ", new Point(4, 118), Brush("ThreeD.SecondaryTextBrush"));
        for (var index = 0; index < output.Points.Count; index++)
        {
            var value = output.Points[index];
            var y = value.Sample.Role switch { ToolRecipeValidationSampleRole.Good => 52, ToolRecipeValidationSampleRole.Bad => 104, _ => 122 };
            // Deterministic display jitter exposes coincident samples without changing their measured X value.
            y += (value.Sample.Order % 5 - 2) * 3;
            var position = new Point(X(value.Value), y);
            renderedPoints.Add((position, value));
            var held = value.Sample.Role == ToolRecipeValidationSampleRole.HeldOut;
            var brush = Brush(value.Sample.Role == ToolRecipeValidationSampleRole.Good ? "ThreeD.PassBrush" : held ? "ThreeD.SecondaryTextBrush" : "ThreeD.FailBrush");
            if (value.Sample.IsMismatch) drawing.DrawEllipse(null, new Pen(Brush("ThreeD.WarningBrush"), 1.4), position, 9, 9);
            if (ReferenceEquals(value.Sample, SelectedSample)) drawing.DrawEllipse(null, new Pen(Brush("ThreeD.TextBrush"), 1), position, 12, 12);
            var radius = index == hoveredIndex || index == pressedIndex ? 6 : 5;
            if (value.Sample.Role == ToolRecipeValidationSampleRole.Good)
                drawing.DrawEllipse(held ? null : brush, new Pen(brush, 1.5), position, radius, radius);
            else
            {
                var diamond = new StreamGeometry();
                using (var context = diamond.Open())
                {
                    context.BeginFigure(new Point(position.X, position.Y - radius), true, true);
                    context.LineTo(new Point(position.X + radius, position.Y), true, false);
                    context.LineTo(new Point(position.X, position.Y + radius), true, false);
                    context.LineTo(new Point(position.X - radius, position.Y), true, false);
                }
                drawing.DrawGeometry(held ? null : brush, new Pen(brush, 1.5), diamond);
            }
        }
        if (IsKeyboardFocusWithin) drawing.DrawRectangle(null, new Pen(Brush("ThreeD.FocusBrush"), 1), new Rect(1, 1, Math.Max(0, ActualWidth - 2), ActualHeight - 2));
    }

    protected override void OnMouseMove(MouseEventArgs args)
    {
        base.OnMouseMove(args);
        hoveredIndex = Hit(args.GetPosition(this));
        ToolTip = hoveredIndex < 0 ? null : $"{renderedPoints[hoveredIndex].Value.Sample.FileName} · {renderedPoints[hoveredIndex].Value.ValueText} {Output?.Column.Unit} · {renderedPoints[hoveredIndex].Value.Sample.ExpectedVerdictText} → {renderedPoints[hoveredIndex].Value.Sample.StatusText}";
        InvalidateVisual();
    }
    protected override void OnMouseLeave(MouseEventArgs args)
    {
        base.OnMouseLeave(args);
        hoveredIndex = -1;
        ToolTip = null;
        InvalidateVisual();
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonDown(args);
        Focus();
        pressedIndex = Hit(args.GetPosition(this));
        if (pressedIndex >= 0) CaptureMouse();
        InvalidateVisual();
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs args)
    {
        base.OnMouseLeftButtonUp(args);
        var index = Hit(args.GetPosition(this));
        if (index >= 0 && index == pressedIndex) SetCurrentValue(SelectedSampleProperty, renderedPoints[index].Value.Sample);
        pressedIndex = -1;
        if (IsMouseCaptured) ReleaseMouseCapture();
        InvalidateVisual();
    }
    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (Output is not { Points.Count: > 0 } output || args.Key is not (Key.Left or Key.Right or Key.Home or Key.End)) return;
        var index = output.Points.ToList().FindIndex(point => ReferenceEquals(point.Sample, SelectedSample));
        index = args.Key switch { Key.Home => 0, Key.End => output.Points.Count - 1, Key.Left => Math.Max(0, index - 1), _ => Math.Min(output.Points.Count - 1, index + 1) };
        SetCurrentValue(SelectedSampleProperty, output.Points[index].Sample);
        args.Handled = true;
    }
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs args) { base.OnGotKeyboardFocus(args); InvalidateVisual(); }
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs args) { base.OnLostKeyboardFocus(args); InvalidateVisual(); }
    private int Hit(Point point) => renderedPoints.FindIndex(value => (value.Position - point).Length <= 12);
    private Brush Brush(string key) => (Brush)(TryFindResource(key) ?? Brushes.Gray);
    private void Text(DrawingContext drawing, string value, Point position, Brush foreground) => drawing.DrawText(
        new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 11, foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip), position);
}

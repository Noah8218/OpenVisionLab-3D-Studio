using System.Windows;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

/// <summary>Draws the read-only distribution of recorded measurements; owns no verdict or inspection state.</summary>
public sealed class InspectionSummaryPlot : FrameworkElement
{
    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
        nameof(Summary), typeof(InspectionQueueMeasurementSummary), typeof(InspectionSummaryPlot),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public InspectionSummaryPlot() => ClipToBounds = true;

    public InspectionQueueMeasurementSummary? Summary
    {
        get => (InspectionQueueMeasurementSummary?)GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    protected override void OnRender(DrawingContext drawing)
    {
        base.OnRender(drawing);
        if (Summary is not { } summary || ActualWidth < 8 || ActualHeight < 8) return;
        var values = summary.Points.Where(point => point.Value.HasValue).Select(point => point.Value!.Value).ToList();
        if (summary.LowerLimit is { } lower) values.Add(lower);
        if (summary.UpperLimit is { } upper) values.Add(upper);
        if (values.Count == 0) return;
        var magnitude = values.Max(value => Math.Abs(value));
        if (magnitude == 0) magnitude = 1;
        var low = values.Min() / magnitude;
        var high = values.Max() / magnitude;
        var padding = (high - low) * .15;
        if (padding == 0) padding = .5;
        low -= padding;
        high += padding;
        double Y(double value) => ActualHeight - 2 - (value / magnitude - low) / (high - low) * (ActualHeight - 4);
        var passBrush = (Brush)FindResource("ThreeD.PassBrush");
        if (summary.LowerLimit.HasValue || summary.UpperLimit.HasValue)
        {
            var top = summary.UpperLimit is { } maximum ? Y(maximum) : 0;
            var bottom = summary.LowerLimit is { } minimum ? Y(minimum) : ActualHeight;
            var band = passBrush.Clone();
            band.Opacity = .12;
            drawing.DrawRectangle(band, null, new Rect(0, top, ActualWidth, Math.Max(1, bottom - top)));
            var limitPen = new Pen(passBrush, 1) { DashStyle = new DashStyle([3d, 2d], 0) };
            limitPen.Brush = passBrush.Clone();
            limitPen.Brush.Opacity = .4;
            if (summary.LowerLimit is { } min) drawing.DrawLine(limitPen, new Point(0, Y(min)), new Point(ActualWidth, Y(min)));
            if (summary.UpperLimit is { } max) drawing.DrawLine(limitPen, new Point(0, Y(max)), new Point(ActualWidth, Y(max)));
        }
        for (var index = 0; index < summary.Points.Count; index++)
        {
            var point = summary.Points[index];
            if (point.Value is not { } value) continue;
            var position = new Point(4 + index * (ActualWidth - 8) / Math.Max(summary.Points.Count - 1, 1), Y(value));
            var brush = (Brush)FindResource(point.Status == ResultStatus.Pass ? "ThreeD.PassBrush" : point.Status == ResultStatus.Fail ? "ThreeD.FailBrush" : point.Status == ResultStatus.Warning ? "ThreeD.WarningBrush" : "ThreeD.InspectionErrorTextBrush");
            if (point.Status == ResultStatus.Pass) drawing.DrawEllipse(brush, null, position, 2.2, 2.2);
            else
            {
                var diamond = new StreamGeometry();
                using (var geometry = diamond.Open())
                {
                    geometry.BeginFigure(new Point(position.X, position.Y - 2.2), true, true);
                    geometry.LineTo(new Point(position.X + 2.2, position.Y), true, false);
                    geometry.LineTo(new Point(position.X, position.Y + 2.2), true, false);
                    geometry.LineTo(new Point(position.X - 2.2, position.Y), true, false);
                }
                drawing.DrawGeometry(brush, null, diamond);
            }
        }
    }
}

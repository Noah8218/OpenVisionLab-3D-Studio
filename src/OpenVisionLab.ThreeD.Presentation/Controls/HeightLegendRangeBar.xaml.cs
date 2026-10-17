using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Presentation.Models;

namespace OpenVisionLab.ThreeD.Presentation.Controls;

/// <summary>Pointer geometry and keyboard plumbing only; bound commands own ranges and cancellation.</summary>
public partial class HeightLegendRangeBar : UserControl
{
    public static readonly DependencyProperty MinimumProperty = Register(nameof(Minimum), typeof(double), 0d);
    public static readonly DependencyProperty MaximumProperty = Register(nameof(Maximum), typeof(double), 1d);
    public static readonly DependencyProperty DataMinimumProperty = Register(nameof(DataMinimum), typeof(double), 0d);
    public static readonly DependencyProperty DataMaximumProperty = Register(nameof(DataMaximum), typeof(double), 1d);
    public static readonly DependencyProperty HistogramProperty = Register(nameof(Histogram), typeof(IReadOnlyList<int>), null);
    public static readonly DependencyProperty GradientProperty = Register(nameof(Gradient), typeof(Brush), null);
    public static readonly DependencyProperty EditCommandProperty = Register(nameof(EditCommand), typeof(ICommand), null);
    public static readonly DependencyProperty ColorStyleProperty = Register(nameof(ColorStyle), typeof(string), "continuous");
    public static readonly DependencyProperty BelowCaptionProperty = Register(nameof(BelowCaption), typeof(string), "Below");
    public static readonly DependencyProperty WithinCaptionProperty = Register(nameof(WithinCaption), typeof(string), "Within limits");
    public static readonly DependencyProperty AboveCaptionProperty = Register(nameof(AboveCaption), typeof(string), "Above");
    public static readonly DependencyProperty UnitProperty = Register(nameof(Unit), typeof(string), string.Empty);
    public static readonly DependencyProperty ModeCaptionProperty = Register(nameof(ModeCaption), typeof(string), "Range");
    public static readonly DependencyProperty AutoCaptionProperty = Register(nameof(AutoCaption), typeof(string), "Auto");
    public static readonly DependencyProperty LowerCaptionProperty = Register(nameof(LowerCaption), typeof(string), "Lower limit");
    public static readonly DependencyProperty UpperCaptionProperty = Register(nameof(UpperCaption), typeof(string), "Upper limit");
    public static readonly DependencyProperty ErrorTextProperty = Register(nameof(ErrorText), typeof(string), string.Empty);
    public static readonly DependencyProperty InformationProperty = Register(nameof(Information), typeof(string), string.Empty);
    public static readonly DependencyProperty PlotHeightProperty = Register(nameof(PlotHeight), typeof(GridLength), new GridLength(118));
    public static readonly DependencyProperty IsCompactLayoutProperty = Register(nameof(IsCompactLayout), typeof(bool), false);
    public static readonly DependencyProperty CollapseCaptionProperty = Register(nameof(CollapseCaption), typeof(string), "Collapse or expand legend");
    public static readonly DependencyProperty IsCollapsedProperty = DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(HeightLegendRangeBar),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));
    private static readonly DependencyPropertyKey IsEffectivelyCollapsedPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsEffectivelyCollapsed), typeof(bool), typeof(HeightLegendRangeBar), new PropertyMetadata(false));
    public static readonly DependencyProperty IsEffectivelyCollapsedProperty = IsEffectivelyCollapsedPropertyKey.DependencyProperty;
    public static readonly DependencyProperty SpaceConstraintCaptionProperty = Register(nameof(SpaceConstraintCaption), typeof(string), "Maximize pane to show legend");
    private bool editingUpper;
    private string numericInputInitialText = string.Empty;

    public double Minimum { get => (double)GetValue(MinimumProperty); set => SetValue(MinimumProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public double DataMinimum { get => (double)GetValue(DataMinimumProperty); set => SetValue(DataMinimumProperty, value); }
    public double DataMaximum { get => (double)GetValue(DataMaximumProperty); set => SetValue(DataMaximumProperty, value); }
    public IReadOnlyList<int>? Histogram { get => (IReadOnlyList<int>?)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }
    public Brush? Gradient { get => (Brush?)GetValue(GradientProperty); set => SetValue(GradientProperty, value); }
    public ICommand? EditCommand { get => (ICommand?)GetValue(EditCommandProperty); set => SetValue(EditCommandProperty, value); }
    public string ColorStyle { get => (string)GetValue(ColorStyleProperty); set => SetValue(ColorStyleProperty, value); }
    public string BelowCaption { get => (string)GetValue(BelowCaptionProperty); set => SetValue(BelowCaptionProperty, value); }
    public string WithinCaption { get => (string)GetValue(WithinCaptionProperty); set => SetValue(WithinCaptionProperty, value); }
    public string AboveCaption { get => (string)GetValue(AboveCaptionProperty); set => SetValue(AboveCaptionProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string ModeCaption { get => (string)GetValue(ModeCaptionProperty); set => SetValue(ModeCaptionProperty, value); }
    public string AutoCaption { get => (string)GetValue(AutoCaptionProperty); set => SetValue(AutoCaptionProperty, value); }
    public string LowerCaption { get => (string)GetValue(LowerCaptionProperty); set => SetValue(LowerCaptionProperty, value); }
    public string UpperCaption { get => (string)GetValue(UpperCaptionProperty); set => SetValue(UpperCaptionProperty, value); }
    public string ErrorText { get => (string)GetValue(ErrorTextProperty); set => SetValue(ErrorTextProperty, value); }
    public string Information { get => (string)GetValue(InformationProperty); set => SetValue(InformationProperty, value); }
    public GridLength PlotHeight { get => (GridLength)GetValue(PlotHeightProperty); set => SetValue(PlotHeightProperty, value); }
    public bool IsCompactLayout { get => (bool)GetValue(IsCompactLayoutProperty); set => SetValue(IsCompactLayoutProperty, value); }
    public string CollapseCaption { get => (string)GetValue(CollapseCaptionProperty); set => SetValue(CollapseCaptionProperty, value); }
    public bool IsCollapsed { get => (bool)GetValue(IsCollapsedProperty); set => SetValue(IsCollapsedProperty, value); }
    public bool IsEffectivelyCollapsed => (bool)GetValue(IsEffectivelyCollapsedProperty);
    public string SpaceConstraintCaption { get => (string)GetValue(SpaceConstraintCaptionProperty); set => SetValue(SpaceConstraintCaptionProperty, value); }

    public HeightLegendRangeBar() { InitializeComponent(); Unloaded += OnUnloaded; }
    private static DependencyProperty Register(string name, Type type, object? value) => DependencyProperty.Register(name, type, typeof(HeightLegendRangeBar), new PropertyMetadata(value, OnValueChanged));
    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((HeightLegendRangeBar)sender).UpdateGeometry();
    private double TrackSpan => Math.Max(1, Track.ActualHeight - 12);
    private double ScaleMinimum => Math.Min(DataMinimum, Minimum);
    private double ScaleMaximum => Math.Max(DataMaximum, Maximum);
    private double ValuePerPixel => (ScaleMaximum - ScaleMinimum) / TrackSpan;
    private double TopFor(double value) => double.IsFinite(value) && ScaleMaximum > ScaleMinimum ? Math.Clamp((ScaleMaximum - value) / (ScaleMaximum - ScaleMinimum) * TrackSpan, 0, TrackSpan) : 0;

    private void UpdateGeometry()
    {
        if (Track is null || !double.IsFinite(Minimum) || !double.IsFinite(Maximum)) return;
        // 공간 때문에 접은 표시는 저장된 사용자 설정을 변경하지 않는다.
        SetValue(IsEffectivelyCollapsedPropertyKey, IsCompactLayout || IsCollapsed);
        RangePlotRow.Height = IsEffectivelyCollapsed ? new GridLength(0) : ColorStyle == "tolerance" ? new GridLength(60) : PlotHeight;
        GradientTrack.Height = TrackSpan;
        Canvas.SetTop(GradientTrack, 6);
        Canvas.SetTop(UpperHandle, TopFor(Maximum));
        Canvas.SetTop(LowerHandle, TopFor(Minimum));
        UpperHandle.ToolTip = Maximum.ToString("0.##", CultureInfo.CurrentCulture);
        LowerHandle.ToolTip = Minimum.ToString("0.##", CultureInfo.CurrentCulture);
        Canvas.SetTop(RangeBody, TopFor(Maximum) + 6);
        RangeBody.Height = Math.Max(1, TopFor(Minimum) - TopFor(Maximum));
        AutomaticUpperLine.Y1 = AutomaticUpperLine.Y2 = TopFor(DataMaximum) + 6;
        AutomaticLowerLine.Y1 = AutomaticLowerLine.Y2 = TopFor(DataMinimum) + 6;
        var histogram = new System.Text.StringBuilder();
        if (Histogram is { Count: > 0 } bins && bins.Max() is var peak && peak > 0)
            for (var index = 0; index < bins.Count; index++)
            {
                if (bins[index] <= 0) continue;
                var low = DataMinimum + (DataMaximum - DataMinimum) * index / bins.Count;
                var high = DataMinimum + (DataMaximum - DataMinimum) * (index + 1) / bins.Count;
                var top = TopFor(high) + 6;
                var bottom = Math.Max(top + 1, TopFor(low) + 6);
                var width = 26d * bins[index] / peak;
                histogram.Append(string.Create(CultureInfo.InvariantCulture, $"M 0,{top:R} H {width:R} V {bottom:R} H 0 Z "));
            }
        HeightHistogram.Data = histogram.Length > 0 ? Geometry.Parse(histogram.ToString()) : Geometry.Empty;
        var upperTop = Math.Clamp(TopFor(Maximum), 0, Math.Max(0, Track.ActualHeight - 28));
        var lowerTop = Math.Clamp(Math.Max(TopFor(Minimum), upperTop + 14), 14, Math.Max(14, Track.ActualHeight - 14));
        Canvas.SetTop(UpperValueLabel, upperTop);
        Canvas.SetTop(LowerValueLabel, lowerTop);
        var intermediateVisible = TopFor(Minimum) - TopFor(Maximum) >= 64;
        var labels = new[] { UpperQuarterLabel, MiddleLabel, LowerQuarterLabel };
        for (var index = 0; index < labels.Length; index++)
        {
            var fraction = (index + 1) / 4.0;
            var value = Maximum * (1 - fraction) + Minimum * fraction;
            labels[index].Text = value.ToString("0.##", CultureInfo.CurrentCulture);
            labels[index].Visibility = intermediateVisible ? Visibility.Visible : Visibility.Collapsed;
            Canvas.SetTop(labels[index], TopFor(value));
        }
    }

    private void Send(HeightLegendEditKind kind, double value = 0, double pivot = 0, string? text = null)
    {
        var request = new HeightLegendEditRequest(kind, value, pivot, text);
        if (EditCommand?.CanExecute(request) == true) EditCommand.Execute(request);
    }
    private void OnTrackSizeChanged(object sender, SizeChangedEventArgs args) => UpdateGeometry();
    private void OnTrackVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (Track.IsVisible) Dispatcher.InvokeAsync(() => { if (Track.IsVisible) UpdateGeometry(); }, System.Windows.Threading.DispatcherPriority.Loaded);
    }
    private void OnDragStarted(object sender, DragStartedEventArgs args) { if (sender is Thumb thumb) thumb.Focus(); Send(HeightLegendEditKind.Begin); }
    private void OnUpperDrag(object sender, DragDeltaEventArgs args) => Send(HeightLegendEditKind.Upper, Maximum - args.VerticalChange * ValuePerPixel);
    private void OnLowerDrag(object sender, DragDeltaEventArgs args) => Send(HeightLegendEditKind.Lower, Minimum - args.VerticalChange * ValuePerPixel);
    private void OnBodyDrag(object sender, DragDeltaEventArgs args) => Send(HeightLegendEditKind.Shift, -args.VerticalChange * ValuePerPixel);
    private void OnDragCompleted(object sender, DragCompletedEventArgs args) => Send(args.Canceled ? HeightLegendEditKind.Cancel : HeightLegendEditKind.Commit);
    private void OnAuto(object sender, RoutedEventArgs args) { Send(HeightLegendEditKind.Reset); args.Handled = true; }
    private void OnToggleCollapsed(object sender, RoutedEventArgs args) { if (!IsCompactLayout) SetCurrentValue(IsCollapsedProperty, !IsCollapsed); args.Handled = true; }
    private void OnTrackDoubleClick(object sender, MouseButtonEventArgs args) { if (args.ClickCount == 2) OnAuto(sender, args); }
    private void OnLegendMouseDown(object sender, MouseButtonEventArgs args) => args.Handled = true;
    private void OnWheel(object sender, MouseWheelEventArgs args)
    {
        if (LegendInformationScroll.IsMouseOver) return;
        if (ColorStyle == "tolerance") { args.Handled = true; return; }
        var pivot = ScaleMaximum - Math.Clamp(args.GetPosition(Track).Y / Math.Max(1, Track.ActualHeight), 0, 1) * (ScaleMaximum - ScaleMinimum);
        Send(HeightLegendEditKind.Scale, args.Delta > 0 ? 0.9 : 1.1, pivot);
        args.Handled = true;
    }
    private void OnHandleKeyDown(object sender, KeyEventArgs args)
    {
        var upper = ReferenceEquals(sender, UpperHandle);
        if (args.Key == Key.Escape) { Send(HeightLegendEditKind.Cancel); if (IsMouseCaptureWithin) Mouse.Capture(null); }
        else if (args.Key == Key.Enter) OpenNumericInput(upper);
        else if (args.Key is Key.Home or Key.End) Send(upper ? HeightLegendEditKind.Upper : HeightLegendEditKind.Lower, args.Key == Key.Home ? DataMinimum : DataMaximum);
        else if (args.Key is Key.Up or Key.Down or Key.Left or Key.Right)
        {
            var change = (Maximum - Minimum) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 0.1 : 0.01) * (args.Key is Key.Up or Key.Right ? 1 : -1);
            Send(upper ? HeightLegendEditKind.Upper : HeightLegendEditKind.Lower, (upper ? Maximum : Minimum) + change);
        }
        else return;
        args.Handled = true;
    }
    private void OnNumericInput(object sender, MouseButtonEventArgs args) { OpenNumericInput(ReferenceEquals(sender, UpperHandle)); args.Handled = true; }
    private void OnBodyKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key != Key.Escape) return;
        Send(HeightLegendEditKind.Cancel);
        if (IsMouseCaptureWithin) Mouse.Capture(null);
        args.Handled = true;
    }
    private void OpenNumericInput(bool upper)
    {
        editingUpper = upper;
        Send(HeightLegendEditKind.Begin);
        InputCaption.Text = upper ? UpperCaption : LowerCaption;
        numericInputInitialText = (upper ? Maximum : Minimum).ToString("0.##", CultureInfo.CurrentCulture);
        NumericInput.Text = numericInputInitialText;
        NumericPopup.IsOpen = true;
    }
    private void OnNumericOpened(object? sender, EventArgs args)
    {
        // Popup activation must finish before focus leaves the handle that opened it.
        Dispatcher.InvokeAsync(() =>
        {
            if (!NumericPopup.IsOpen) return;
            NumericInput.Focus();
            NumericInput.SelectAll();
        }, System.Windows.Threading.DispatcherPriority.Input);
    }
    private void OnNumericClosed(object? sender, EventArgs args) { Send(HeightLegendEditKind.Cancel); (editingUpper ? UpperHandle : LowerHandle).Focus(); }
    private void OnNumericKeyDown(object sender, KeyEventArgs args)
    {
        if (args.Key == Key.Escape) { Send(HeightLegendEditKind.Cancel); NumericPopup.IsOpen = false; }
        else if (args.Key == Key.Enter)
        {
            // Confirming the displayed value without editing must preserve the original range precision.
            if (string.Equals(NumericInput.Text, numericInputInitialText, StringComparison.Ordinal))
            {
                Send(HeightLegendEditKind.Cancel);
                NumericPopup.IsOpen = false;
            }
            else
            {
                Send(editingUpper ? HeightLegendEditKind.InputUpper : HeightLegendEditKind.InputLower, text: NumericInput.Text);
                if (string.IsNullOrEmpty(ErrorText)) { Send(HeightLegendEditKind.Commit); NumericPopup.IsOpen = false; }
            }
        }
        else return;
        args.Handled = true;
    }
    private void OnUnloaded(object sender, RoutedEventArgs args) { Send(HeightLegendEditKind.Cancel); NumericPopup.IsOpen = false; }
}

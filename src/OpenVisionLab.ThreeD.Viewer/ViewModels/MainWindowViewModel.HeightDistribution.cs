using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Presentation.Models;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed partial class MainWindowViewModel
{
    public const double C3DHeightDistributionLabelColumnWidth = 60.0;

    internal const double C3DHeightDistributionPlotHeight = 118.0;
    internal const double C3DHeightDistributionLabelHeight = 12.0;
    internal const double C3DHeightDistributionLabelGap = 2.0;

    private C3DHeightDistribution? c3DHeightDistribution;
    private bool c3DHeightDistributionVisible;
    private string c3DHeightDistributionHighLabel = "H --";
    private string c3DHeightDistributionMeanLabel = "μ --";
    private double c3DHeightDistributionMeanLabelTop = 53.0;
    private string c3DHeightDistributionLowLabel = "L --";
    private string c3DHeightDistributionPeakLabel = "Peak: not loaded";
    private string c3DHeightDistributionCountLabel = "0 valid";
    private string c3DHeightDistributionPaletteLabel = "Height · full source";
    private string c3DHeightDistributionHistogramPathData = "M 0,118 L 0,118";
    private string c3DHeightDistributionPeakPathData = "M 0,118 L 0,118";
    private string c3DHeightDistributionSourceSha256 = "not loaded";
    private LinearGradientBrush c3DHeightDistributionGradient = CreateHeightGradient("Height");
    private double c3DHeightColorMinimumRaw = double.NaN;
    private double c3DHeightColorMaximumRaw = double.NaN;
    private bool c3DHeightColorRangeAuto = true;
    private int c3DHeightEmphasis = 1;
    private int c3DHeightColorRangeRevision;
    private OpenVisionLab.ThreeD.Presentation.Commands.RelayCommand? c3DHeightLegendEditCommand;
    private (double Minimum, double Maximum, bool Auto, string Source)? c3DHeightLegendEditStart;
    private string c3DHeightLegendError = string.Empty;
    private HeightDisplayOptions heightDisplayOptions = new();
    private ReadOnlyMemory<double> heightDisplayValues;
    private bool heightDisplayIsSignedDelta;
    private string heightDisplayUnit = "raw-height";
    private double[] sortedHeightDisplaySamples = [];
    private int heightDisplayWidth;
    private int heightDisplayHeight;
    private string heightDisplayWorkspace = "teach";
    private string heightDisplayMapId = "source";
    private string heightDisplayMapName = string.Empty;
    private readonly Dictionary<string, (string Source, string Mode, double Minimum, double Maximum)> heightDisplayRanges = new();
    private HeightDisplayOptions? heightLegendOptionsAtStart;
    private (double Minimum, double Maximum, HeightDisplayOptions Options)? heightColorRenderingBeforeDrag;
    internal double HeightColorRenderMinimum => heightColorRenderingBeforeDrag?.Minimum ?? C3DHeightColorMinimumRaw;
    internal double HeightColorRenderMaximum => heightColorRenderingBeforeDrag?.Maximum ?? C3DHeightColorMaximumRaw;
    private HeightDisplayOptions HeightColorRenderOptions => heightColorRenderingBeforeDrag?.Options ?? heightDisplayOptions;

    public IReadOnlyList<int> HeightLegendHistogram => c3DHeightDistribution?.Bins ?? [];
    public HeightDisplayOptions HeightDisplayOptions => heightDisplayOptions with { Palette = SelectedColorMode };
    internal (string Style, int Bands, bool Outside, string Measurement) HeightColorOptionsKey => (HeightColorRenderOptions.ColorStyle,
        HeightColorRenderOptions.ColorStyle == "bands" ? HeightColorRenderOptions.BandCount : 0, HeightColorRenderOptions.HighlightOutside,
        HeightColorRenderOptions.Measurement?.ExecutionIdentity ?? string.Empty);
    public string HeightDisplayWorkspace => heightDisplayWorkspace;
    public string HeightDisplayMapId => heightDisplayMapId;
    public string C3DHeightLegendUnit => heightDisplayUnit;
    public bool CanUseMeasurementColor => heightDisplayOptions.Measurement?.MatchesSource(C3DHeightDistributionSourceSha256,
        heightDisplayWidth, heightDisplayHeight) == true;
    public string MeasurementColorSummary => CanUseMeasurementColor && heightDisplayOptions.Measurement is { } measurement
        ? $"{measurement.ToolName} · {measurement.LowerLimit:0.##} – {measurement.UpperLimit:0.##} {measurement.Unit}" : string.Empty;

    public void SetHeightMeasurementColor(HeightMeasurementColorSnapshot? snapshot)
    {
        if (snapshot is not null && !snapshot.MatchesSource(C3DHeightDistributionSourceSha256, heightDisplayWidth, heightDisplayHeight)) snapshot = null;
        if (ReferenceEquals(heightDisplayOptions.Measurement, snapshot)) return;
        c3DHeightLegendEditStart = null;
        heightLegendOptionsAtStart = null;
        heightColorRenderingBeforeDrag = null;
        heightDisplayOptions = heightDisplayOptions with { Measurement = snapshot, IsRangeDragActive = false,
            ColorStyle = snapshot is null && heightDisplayOptions.ColorStyle == "tolerance" ? "continuous" : heightDisplayOptions.ColorStyle };
        NotifyHeightDisplayOptions();
    }

    public bool HasHeightDisplayRegion => C3DHeightImageFrame.TryGetRegionDisplayRange(heightDisplayValues.Span, heightDisplayWidth,
        heightDisplayHeight, GetVisibleTeachingGridRectangle(), out _, out _);
    public string C3DHeightLegendMode => heightDisplayOptions.ColorStyle == "tolerance"
        ? OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve("ThreeD.Legend.Mode.tolerance", "허용 범위 색", "Tolerance colours")
        : OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve(
        "ThreeD.Legend.Mode." + heightDisplayOptions.RangeMode,
        heightDisplayOptions.RangeMode switch { "robust" => "자동·분포", "roi" => "영역 기준", "manual" => "수동", _ => "자동·전체" },
        heightDisplayOptions.RangeMode switch { "robust" => "Auto · distribution", "roi" => "Region", "manual" => "Manual", _ => "Auto · full" });
    public bool IsHeightLegendCollapsed
    {
        get => heightDisplayOptions.IsCollapsed;
        set { if (value != heightDisplayOptions.IsCollapsed) TryApplyHeightDisplayOptions(HeightDisplayOptions with { IsCollapsed = value }); }
    }
    public string C3DHeightLegendInformation
    {
        get
        {
            if (c3DHeightDistribution is not { } distribution) return string.Empty;
            if (heightDisplayOptions.ColorStyle == "tolerance" && CanUseMeasurementColor && heightDisplayOptions.Measurement is { } measurement)
            {
                var measuredOutside = C3DHeightImageFrame.GetOutsideDisplayPercentages(C3DHeightImageFrame.SortDisplaySamples(measurement.Values.Span), measurement.LowerLimit, measurement.UpperLimit);
                return OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve("ThreeD.Legend.MeasuredInformation",
                    $"{MeasurementColorSummary}\n측정 영역 · 실행 당시 허용 범위\n미만 {measuredOutside.Below:0.0}% · 초과 {measuredOutside.Above:0.0}%",
                    $"{MeasurementColorSummary}\nMeasured region · limits at execution\nBelow {measuredOutside.Below:0.0}% · above {measuredOutside.Above:0.0}%");
            }
            var outside = C3DHeightImageFrame.GetOutsideDisplayPercentages(sortedHeightDisplaySamples, C3DHeightColorMinimumRaw, C3DHeightColorMaximumRaw);
            var missing = distribution.MissingSampleCount * 100.0 / Math.Max(1, distribution.ValidSampleCount + distribution.MissingSampleCount);
            return OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve("ThreeD.Legend.Information",
                $"결측 {missing:0.0}%\n표시 범위 미만 ↓{outside.Below:0.0}% · 초과 ↑{outside.Above:0.0}%",
                $"Missing {missing:0.0}%\nBelow display range ↓{outside.Below:0.0}% · above ↑{outside.Above:0.0}%");
        }
    }

    public ICommand C3DHeightLegendEditCommand => c3DHeightLegendEditCommand ??= new OpenVisionLab.ThreeD.Presentation.Commands.RelayCommand(
        HandleC3DHeightLegendEdit, _ => C3DHeightDistributionVisible && c3DHeightDistribution is not null);
    public string C3DHeightLegendError { get => c3DHeightLegendError; private set => SetField(ref c3DHeightLegendError, value); }

    public bool C3DHeightDistributionVisible
    {
        get => c3DHeightDistributionVisible;
        private set => SetField(ref c3DHeightDistributionVisible, value);
    }

    public string C3DHeightDistributionHighLabel
    {
        get => c3DHeightDistributionHighLabel;
        private set => SetField(ref c3DHeightDistributionHighLabel, value);
    }

    public string C3DHeightDistributionMeanLabel
    {
        get => c3DHeightDistributionMeanLabel;
        private set => SetField(ref c3DHeightDistributionMeanLabel, value);
    }

    public double C3DHeightDistributionMeanLabelTop
    {
        get => c3DHeightDistributionMeanLabelTop;
        private set => SetField(ref c3DHeightDistributionMeanLabelTop, value);
    }

    public string C3DHeightDistributionLowLabel
    {
        get => c3DHeightDistributionLowLabel;
        private set => SetField(ref c3DHeightDistributionLowLabel, value);
    }

    public string C3DHeightDistributionPeakLabel
    {
        get => c3DHeightDistributionPeakLabel;
        private set => SetField(ref c3DHeightDistributionPeakLabel, value);
    }

    public string C3DHeightDistributionCountLabel
    {
        get => c3DHeightDistributionCountLabel;
        private set => SetField(ref c3DHeightDistributionCountLabel, value);
    }

    public string C3DHeightDistributionPaletteLabel
    {
        get => c3DHeightDistributionPaletteLabel;
        private set => SetField(ref c3DHeightDistributionPaletteLabel, value);
    }

    public string C3DHeightDistributionHistogramPathData
    {
        get => c3DHeightDistributionHistogramPathData;
        private set => SetField(ref c3DHeightDistributionHistogramPathData, value);
    }

    public string C3DHeightDistributionPeakPathData
    {
        get => c3DHeightDistributionPeakPathData;
        private set => SetField(ref c3DHeightDistributionPeakPathData, value);
    }

    public LinearGradientBrush C3DHeightDistributionGradient
    {
        get => c3DHeightDistributionGradient;
        private set => SetField(ref c3DHeightDistributionGradient, value);
    }

    public string C3DHeightDistributionSourceSha256
    {
        get => c3DHeightDistributionSourceSha256;
        private set => SetField(ref c3DHeightDistributionSourceSha256, value);
    }

    public int C3DHeightDistributionBinCount => c3DHeightDistribution?.BinCount ?? 0;

    public int C3DHeightDistributionBinSum => c3DHeightDistribution?.Bins.Sum() ?? 0;

    public int C3DHeightDistributionValidSampleCount => c3DHeightDistribution?.ValidSampleCount ?? 0;

    public int C3DHeightDistributionMissingSampleCount => c3DHeightDistribution?.MissingSampleCount ?? 0;

    public double C3DHeightDistributionMinimumRaw => heightDisplayIsSignedDelta && sortedHeightDisplaySamples.Length > 0
        ? sortedHeightDisplaySamples[0] : c3DHeightDistribution?.Minimum ?? double.NaN;

    public double C3DHeightDistributionMaximumRaw => heightDisplayIsSignedDelta && sortedHeightDisplaySamples.Length > 0
        ? sortedHeightDisplaySamples[^1] : c3DHeightDistribution?.Maximum ?? double.NaN;

    public double C3DHeightDistributionMeanRaw => c3DHeightDistribution?.Mean ?? double.NaN;

    public double C3DHeightDistributionPeakLowerRaw => c3DHeightDistribution?.PeakLowerBound ?? double.NaN;

    public double C3DHeightDistributionPeakUpperRaw => c3DHeightDistribution?.PeakUpperBound ?? double.NaN;

    public double C3DHeightDistributionPeakFraction => c3DHeightDistribution?.PeakFraction ?? double.NaN;

    public bool C3DHeightDistributionIsConstant => c3DHeightDistribution?.IsConstant == true;

    public double C3DHeightColorMinimumRaw
    {
        get => c3DHeightColorMinimumRaw;
        set => SetC3DHeightColorRange(value, c3DHeightColorMaximumRaw);
    }

    public double C3DHeightColorMaximumRaw
    {
        get => c3DHeightColorMaximumRaw;
        set => SetC3DHeightColorRange(c3DHeightColorMinimumRaw, value);
    }

    public IReadOnlyList<string> C3DHeightColorTickLabels => !double.IsFinite(C3DHeightColorMinimumRaw) || !double.IsFinite(C3DHeightColorMaximumRaw)
        ? [] : Enumerable.Range(0, 5).Select(index =>
            (C3DHeightColorMaximumRaw * (1 - index / 4.0) + C3DHeightColorMinimumRaw * (index / 4.0)).ToString("F2", System.Globalization.CultureInfo.CurrentCulture)).ToArray();

    public bool C3DHeightColorRangeAuto
    {
        get => c3DHeightColorRangeAuto;
        private set => SetField(ref c3DHeightColorRangeAuto, value);
    }

    public int C3DHeightColorRangeRevision
    {
        get => c3DHeightColorRangeRevision;
        private set => SetField(ref c3DHeightColorRangeRevision, value);
    }

    public int C3DHeightEmphasis
    {
        get => c3DHeightEmphasis;
        private set => SetField(ref c3DHeightEmphasis, value);
    }

    public string C3DHeightColorRangeSummary
    {
        get
        {
            if (c3DHeightDistribution is null
                || !double.IsFinite(C3DHeightColorMinimumRaw)
                || !double.IsFinite(C3DHeightColorMaximumRaw))
            {
                return "Height color range: not loaded";
            }

            var span = c3DHeightDistribution.Maximum - c3DHeightDistribution.Minimum;
            return string.Create(
                CultureInfo.InvariantCulture,
                $"Height color range {FormatRawValue(C3DHeightColorMinimumRaw, span)}..{FormatRawValue(C3DHeightColorMaximumRaw, span)} {C3DHeightLegendUnit} · {(C3DHeightColorRangeAuto ? "Auto" : "Manual")} · view only");
        }
    }

    public void SetHeightDisplayContext(string workspace, string mapId, string mapName)
    {
        if (heightDisplayWorkspace == workspace && heightDisplayMapId == mapId && heightDisplayMapName == mapName) return;
        RememberHeightDisplayRange();
        c3DHeightLegendEditStart = null;
        heightLegendOptionsAtStart = null;
        C3DHeightLegendError = string.Empty;
        heightDisplayWorkspace = workspace;
        heightDisplayMapId = mapId;
        heightDisplayMapName = mapName;
        RestoreHeightDisplayRange();
    }

    public void SetHeightDisplayLinked(bool linked)
    {
        if (heightDisplayOptions.IsLinked == linked) return;
        heightDisplayOptions = heightDisplayOptions with { IsLinked = linked };
        NotifyHeightDisplayOptions();
    }

    public IReadOnlyDictionary<string, string> CaptureHeightDisplayModes()
    {
        RememberHeightDisplayRange();
        return heightDisplayRanges.Where(entry => entry.Value.Mode is "robust" or "roi").ToDictionary(entry => entry.Key, entry => entry.Value.Mode);
    }

    public void RestoreHeightDisplayModes(IReadOnlyDictionary<string, string> modes)
    {
        heightDisplayRanges.Clear();
        foreach (var entry in modes.Where(HeightDisplayOptions.IsStoredMapMode))
            heightDisplayRanges[entry.Key] = (string.Empty, entry.Value, double.NaN, double.NaN);
        heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto" };
        RestoreHeightDisplayRange();
    }

    public bool TryApplyLinkedHeightDisplayState(HeightDisplayOptions options, double minimum, double maximum)
    {
        if (c3DHeightDistribution is null || !double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum > maximum) return false;
        if (HeightDisplayOptions == options && C3DHeightColorMinimumRaw == minimum && C3DHeightColorMaximumRaw == maximum) return true;
        if (options.IsRangeDragActive && !heightDisplayOptions.IsRangeDragActive)
            heightColorRenderingBeforeDrag = (C3DHeightColorMinimumRaw, C3DHeightColorMaximumRaw, heightDisplayOptions);
        if (!options.IsRangeDragActive) heightColorRenderingBeforeDrag = null;
        var measurement = options.Measurement;
        if (measurement?.MatchesSource(C3DHeightDistributionSourceSha256, heightDisplayWidth, heightDisplayHeight) != true) measurement = null;
        heightDisplayOptions = options.Sanitize() with { Measurement = measurement,
            ColorStyle = measurement is null && options.ColorStyle == "tolerance" ? "continuous" : options.ColorStyle };
        if (Display.AvailableColorMaps.Contains(options.Palette)) Display.SelectedColorMap = options.Palette;
        ApplyC3DHeightColorRange(minimum, maximum, options.RangeMode != "manual");
        NotifyHeightDisplayOptions();
        return true;
    }

    public bool TryApplyHeightDisplayOptions(HeightDisplayOptions options, double? minimum = null, double? maximum = null)
    {
        var next = options.Sanitize() with { Measurement = heightDisplayOptions.Measurement };
        if (c3DHeightDistribution is not null && next.RangeMode == "roi" && !HasHeightDisplayRegion
            || next.ColorStyle == "tolerance" && !CanUseMeasurementColor) return false;
        if (next.RangeMode == "manual" && minimum.HasValue && (!maximum.HasValue || !double.IsFinite(minimum.Value)
            || !double.IsFinite(maximum.Value) || minimum.Value >= maximum.Value)) return false;
        var wasPinned = heightDisplayOptions.KeepsRange(heightDisplayWorkspace);
        heightDisplayOptions = next;
        if (Display.AvailableColorMaps.Contains(next.Palette)) Display.SelectedColorMap = next.Palette;
        if (wasPinned && !next.KeepsRange(heightDisplayWorkspace) && next.RangeMode == "manual")
            heightDisplayOptions = next with { RangeMode = "auto" };
        if (heightDisplayOptions.RangeMode == "manual")
            ApplyC3DHeightColorRange(minimum ?? C3DHeightColorMinimumRaw, maximum ?? C3DHeightColorMaximumRaw, false);
        else ApplyAutomaticHeightDisplayRange();
        NotifyHeightDisplayOptions();
        return true;
    }

    private void RememberHeightDisplayRange()
    {
        if (!double.IsFinite(C3DHeightColorMinimumRaw) || !double.IsFinite(C3DHeightColorMaximumRaw)) return;
        var key = heightDisplayWorkspace + "|" + heightDisplayMapId;
        heightDisplayRanges[key] = (C3DHeightDistributionSourceSha256, heightDisplayOptions.RangeMode, C3DHeightColorMinimumRaw, C3DHeightColorMaximumRaw);
    }

    private void RestoreHeightDisplayRange()
    {
        if (c3DHeightDistribution is null) return;
        if (heightDisplayRanges.TryGetValue(heightDisplayWorkspace + "|" + heightDisplayMapId, out var saved)
            && (saved.Mode != "manual" || saved.Source == C3DHeightDistributionSourceSha256 || heightDisplayOptions.KeepsRange(heightDisplayWorkspace)))
        {
            heightDisplayOptions = heightDisplayOptions with { RangeMode = saved.Mode };
            if (saved.Mode == "manual") ApplyC3DHeightColorRange(saved.Minimum, saved.Maximum, false);
            else ApplyAutomaticHeightDisplayRange();
        }
        else
        {
            if (heightDisplayOptions.RangeMode == "manual") heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto" };
            ApplyAutomaticHeightDisplayRange();
        }
        NotifyHeightDisplayOptions();
    }

    private void ApplyAutomaticHeightDisplayRange()
    {
        if (c3DHeightDistribution is not { } distribution) return;
        var minimum = heightDisplayIsSignedDelta && sortedHeightDisplaySamples.Length > 0 ? sortedHeightDisplaySamples[0] : distribution.Minimum;
        var maximum = heightDisplayIsSignedDelta && sortedHeightDisplaySamples.Length > 0 ? sortedHeightDisplaySamples[^1] : distribution.Maximum;
        if (heightDisplayOptions.RangeMode == "robust" && sortedHeightDisplaySamples.Length > 0)
            (minimum, maximum) = C3DHeightImageFrame.GetDistributionDisplayRange(sortedHeightDisplaySamples);
        else if (heightDisplayOptions.RangeMode == "roi"
            && !C3DHeightImageFrame.TryGetRegionDisplayRange(heightDisplayValues.Span, heightDisplayWidth, heightDisplayHeight,
                GetVisibleTeachingGridRectangle(), out minimum, out maximum))
        {
            heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto" };
            (minimum, maximum) = (distribution.Minimum, distribution.Maximum);
        }
        ApplyC3DHeightColorRange(minimum, maximum, true);
    }

    private void NotifyHeightDisplayOptions()
    {
        OnPropertyChanged(nameof(HeightLegendHistogram));
        OnPropertyChanged(nameof(HeightDisplayOptions));
        OnPropertyChanged(nameof(CanUseMeasurementColor));
        OnPropertyChanged(nameof(MeasurementColorSummary));
        OnPropertyChanged(nameof(HeightDisplayWorkspace));
        OnPropertyChanged(nameof(HeightDisplayMapId));
        OnPropertyChanged(nameof(HasHeightDisplayRegion));
        OnPropertyChanged(nameof(C3DHeightLegendMode));
        OnPropertyChanged(nameof(C3DHeightLegendInformation));
        OnPropertyChanged(nameof(IsHeightLegendCollapsed));
        C3DHeightColorRangeRevision = unchecked(C3DHeightColorRangeRevision + 1);
        RefreshC3DHeightDistributionLegend();
    }

    private void HandleC3DHeightLegendEdit(object? parameter)
    {
        if (parameter is not HeightLegendEditRequest request || c3DHeightDistribution is not { } distribution) return;
        C3DHeightLegendError = string.Empty;
        var minimum = C3DHeightColorMinimumRaw;
        var maximum = C3DHeightColorMaximumRaw;
        switch (request.Kind)
        {
            case HeightLegendEditKind.Begin:
                heightLegendOptionsAtStart = heightDisplayOptions;
                c3DHeightLegendEditStart = (minimum, maximum, C3DHeightColorRangeAuto, C3DHeightDistributionSourceSha256);
                heightColorRenderingBeforeDrag = (minimum, maximum, heightDisplayOptions);
                heightDisplayOptions = heightDisplayOptions with { IsRangeDragActive = true };
                NotifyHeightDisplayOptions();
                return;
            case HeightLegendEditKind.Cancel:
                if (c3DHeightLegendEditStart is { } start && start.Source == C3DHeightDistributionSourceSha256)
                {
                    heightDisplayOptions = (heightLegendOptionsAtStart ?? heightDisplayOptions) with { IsRangeDragActive = false };
                    heightColorRenderingBeforeDrag = null;
                    ApplyC3DHeightColorRange(start.Minimum, start.Maximum, start.Auto);
                    NotifyHeightDisplayOptions();
                }
                c3DHeightLegendEditStart = null;
                return;
            case HeightLegendEditKind.Commit:
                c3DHeightLegendEditStart = null;
                heightDisplayOptions = heightDisplayOptions with { IsRangeDragActive = false };
                heightColorRenderingBeforeDrag = null;
                NotifyHeightDisplayOptions();
                return;
            case HeightLegendEditKind.Reset:
                ResetC3DHeightColorRange();
                c3DHeightLegendEditStart = null;
                return;
            case HeightLegendEditKind.InputLower:
            case HeightLegendEditKind.InputUpper:
                if (c3DHeightLegendEditStart?.Source != C3DHeightDistributionSourceSha256)
                { C3DHeightLegendError = OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve("ThreeD.Legend.InputChanged", "입력이 바뀌었습니다. 값을 다시 여세요.", "Input changed. Reopen the value editor."); return; }
                if (!(double.TryParse(request.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var number)
                    || double.TryParse(request.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out number)) || !double.IsFinite(number))
                { C3DHeightLegendError = OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve("ThreeD.Legend.FiniteNumber", "유한한 숫자를 입력하세요.", "Enter a finite number."); return; }
                if (request.Kind == HeightLegendEditKind.InputLower) minimum = number; else maximum = number;
                if (heightDisplayIsSignedDelta)
                {
                    if (request.Kind == HeightLegendEditKind.InputLower) maximum = -minimum; else minimum = -maximum;
                }
                var dataRadius = Math.Max(Math.Abs(C3DHeightDistributionMinimumRaw), Math.Abs(C3DHeightDistributionMaximumRaw));
                if (dataRadius == 0) dataRadius = 1;
                if (minimum >= maximum || !heightDisplayOptions.KeepsRange(heightDisplayWorkspace) && (heightDisplayIsSignedDelta
                    ? minimum < -dataRadius || maximum > dataRadius : minimum < distribution.Minimum || maximum > distribution.Maximum))
                { C3DHeightLegendError = OpenVisionLab.ThreeD.Viewer.Localization.ViewerLocalization.Shared.Resolve("ThreeD.Legend.InvalidRange", "데이터 범위 안에서 하한 < 상한으로 입력하세요.", "Enter lower < upper within the data range."); return; }
                break;
            case HeightLegendEditKind.Lower: minimum = request.Value; break;
            case HeightLegendEditKind.Upper: maximum = request.Value; break;
            case HeightLegendEditKind.Shift:
                if (heightDisplayIsSignedDelta) return;
                var firstOffset = distribution.Minimum - minimum;
                var lastOffset = distribution.Maximum - maximum;
                var offset = heightDisplayOptions.KeepsRange(heightDisplayWorkspace) ? request.Value : firstOffset <= lastOffset ? Math.Clamp(request.Value, firstOffset, lastOffset) : 0;
                minimum += offset; maximum += offset;
                break;
            case HeightLegendEditKind.Scale:
                if (!double.IsFinite(request.Value) || request.Value <= 0 || !double.IsFinite(request.Pivot)) return;
                if (heightDisplayIsSignedDelta) { maximum *= request.Value; minimum = -maximum; }
                else
                {
                    minimum = request.Pivot - (request.Pivot - minimum) * request.Value;
                    maximum = request.Pivot + (maximum - request.Pivot) * request.Value;
                }
                break;
        }
        if (heightDisplayIsSignedDelta && request.Kind == HeightLegendEditKind.Lower) maximum = -minimum;
        if (heightDisplayIsSignedDelta && request.Kind == HeightLegendEditKind.Upper) minimum = -maximum;
        if (heightDisplayIsSignedDelta && minimum >= maximum) return;
        SetC3DHeightColorRange(minimum, maximum);
    }

    internal void SetC3DHeightDistribution(C3DHeightDistribution distribution, string sourceSha256, ReadOnlyMemory<double> values = default, int width = 0, int height = 0, bool isSignedDelta = false, string unit = "raw-height")
    {
        ArgumentNullException.ThrowIfNull(distribution);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);

        RememberHeightDisplayRange();
        var sourceChanged = C3DHeightDistributionSourceSha256 != sourceSha256 || heightDisplayIsSignedDelta != isSignedDelta;
        heightDisplayIsSignedDelta = isSignedDelta;
        heightDisplayUnit = unit;
        OnPropertyChanged(nameof(C3DHeightLegendUnit));
        c3DHeightLegendEditStart = null;
        C3DHeightLegendError = string.Empty;
        c3DHeightDistribution = distribution;
        heightColorRenderingBeforeDrag = null;
        heightDisplayOptions = heightDisplayOptions with { IsRangeDragActive = false };
        C3DHeightDistributionSourceSha256 = sourceSha256;
        if (heightDisplayOptions.Measurement?.MatchesSource(sourceSha256, width, height) != true)
            heightDisplayOptions = heightDisplayOptions with { Measurement = null,
                ColorStyle = heightDisplayOptions.ColorStyle == "tolerance" ? "continuous" : heightDisplayOptions.ColorStyle };
        if (sourceChanged || heightDisplayValues.IsEmpty)
        {
            heightDisplayValues = values;
            sortedHeightDisplaySamples = C3DHeightImageFrame.SortDisplaySamples(values.Span);
            heightDisplayWidth = width;
            heightDisplayHeight = height;
        }
        var displaySpan = distribution.Maximum - distribution.Minimum;
        C3DHeightDistributionHighLabel = $"H {FormatRawValue(distribution.Maximum, displaySpan)}";
        C3DHeightDistributionMeanLabel = $"μ {FormatRawValue(distribution.Mean, displaySpan)}";
        C3DHeightDistributionMeanLabelTop = CalculateMeanLabelTop(distribution);
        C3DHeightDistributionLowLabel = $"L {FormatRawValue(distribution.Minimum, displaySpan)}";
        C3DHeightDistributionPeakLabel = distribution.IsConstant
            ? $"Peak {FormatRawValue(distribution.PeakCenter, displaySpan)} | {distribution.PeakFraction.ToString("P1", CultureInfo.InvariantCulture)}"
            : $"Peak {FormatRawValue(distribution.PeakLowerBound, displaySpan)}–{FormatRawValue(distribution.PeakUpperBound, displaySpan)} | {distribution.PeakFraction.ToString("P1", CultureInfo.InvariantCulture)}";
        C3DHeightDistributionCountLabel =
            $"{FormatCompactCount(distribution.ValidSampleCount)} valid · {FormatCompactCount(distribution.MissingSampleCount)} missing";
        C3DHeightDistributionHistogramPathData = CreateHistogramPath(distribution, peakOnly: false);
        C3DHeightDistributionPeakPathData = CreateHistogramPath(distribution, peakOnly: true);
        RestoreHeightDisplayRange();
        NotifyC3DHeightDistributionContractProperties();
        RefreshC3DHeightDistributionLegend();
        RefreshCommandCanExecute();
    }

    internal void ClearC3DHeightDistribution()
    {
        RememberHeightDisplayRange();
        heightDisplayValues = default;
        heightDisplayIsSignedDelta = false;
        heightDisplayUnit = "raw-height";
        OnPropertyChanged(nameof(C3DHeightLegendUnit));
        heightColorRenderingBeforeDrag = null;
        heightDisplayOptions = heightDisplayOptions with { IsRangeDragActive = false };
        sortedHeightDisplaySamples = [];
        c3DHeightLegendEditStart = null;
        C3DHeightLegendError = string.Empty;
        c3DHeightDistribution = null;
        C3DHeightDistributionVisible = false;
        C3DHeightDistributionHighLabel = "H --";
        C3DHeightDistributionMeanLabel = "μ --";
        C3DHeightDistributionMeanLabelTop = 53.0;
        C3DHeightDistributionLowLabel = "L --";
        C3DHeightDistributionPeakLabel = "Peak: not loaded";
        C3DHeightDistributionCountLabel = "0 valid";
        C3DHeightDistributionHistogramPathData = "M 0,118 L 0,118";
        C3DHeightDistributionPeakPathData = "M 0,118 L 0,118";
        C3DHeightDistributionSourceSha256 = "not loaded";
        c3DHeightColorMinimumRaw = double.NaN;
        c3DHeightColorMaximumRaw = double.NaN;
        C3DHeightColorRangeAuto = true;
        C3DHeightColorRangeRevision = unchecked(C3DHeightColorRangeRevision + 1);
        NotifyHeightColorRangeProperties();
        NotifyC3DHeightDistributionContractProperties();
        RefreshCommandCanExecute();
    }

    public void ResetC3DHeightColorRange()
    {
        if (c3DHeightDistribution is null)
        {
            return;
        }

        heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto", IsRangeDragActive = false,
            ColorStyle = heightDisplayOptions.ColorStyle == "tolerance" ? "continuous" : heightDisplayOptions.ColorStyle };
        heightColorRenderingBeforeDrag = null;
        ApplyAutomaticHeightDisplayRange();
        NotifyHeightDisplayOptions();
    }

    public bool TryApplyLinkedC3DHeightColorRange(double minimum, double maximum)
    {
        if (c3DHeightDistribution is null
            || !double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || minimum >= maximum)
        {
            return false;
        }

        ApplyC3DHeightColorRange(minimum, maximum, isAuto: false);
        return true;
    }

    public bool TrySetC3DHeightEmphasis(int emphasis)
    {
        if (!C3DHeightDistributionVisible || emphasis is not (1 or 2 or 3 or 5))
        {
            return false;
        }

        C3DHeightEmphasis = emphasis;
        return true;
    }

    public void ShiftC3DHeightColorMinimum(int direction)
    {
        if (c3DHeightDistribution is null || direction == 0)
        {
            return;
        }

        SetC3DHeightColorRange(
            C3DHeightColorMinimumRaw + Math.Sign(direction) * GetHeightColorRangeStep(),
            C3DHeightColorMaximumRaw);
    }

    public void ShiftC3DHeightColorMaximum(int direction)
    {
        if (c3DHeightDistribution is null || direction == 0)
        {
            return;
        }

        SetC3DHeightColorRange(
            C3DHeightColorMinimumRaw,
            C3DHeightColorMaximumRaw + Math.Sign(direction) * GetHeightColorRangeStep());
    }

    public double NormalizeC3DHeightColor(double rawHeight)
    {
        if (!double.IsFinite(rawHeight)
            || !double.IsFinite(C3DHeightColorMinimumRaw)
            || !double.IsFinite(C3DHeightColorMaximumRaw)
            || C3DHeightColorMaximumRaw <= C3DHeightColorMinimumRaw)
        {
            return 0.0;
        }

        return Math.Clamp(
            (rawHeight - C3DHeightColorMinimumRaw)
            / (C3DHeightColorMaximumRaw - C3DHeightColorMinimumRaw),
            0.0,
            1.0);
    }

    internal (double R, double G, double B) GetC3DHeightDisplayColor(double rawHeight, int row = -1, int column = -1)
    {
        var index = row >= 0 && column >= 0 ? checked(row * heightDisplayWidth + column) : -1;
        if (heightDisplayIsSignedDelta && index >= 0 && index < heightDisplayValues.Length) rawHeight = heightDisplayValues.Span[index];
        return C3DPointMapPalette.DisplayColor(rawHeight, HeightColorRenderMinimum, HeightColorRenderMaximum, SelectedColorMode, HeightColorRenderOptions, index);
    }

    private void SetC3DHeightColorRange(double minimum, double maximum)
    {
        if (c3DHeightDistribution is null
            || !double.IsFinite(minimum)
            || !double.IsFinite(maximum))
        {
            return;
        }

        var sourceMinimum = C3DHeightDistributionMinimumRaw;
        var sourceMaximum = C3DHeightDistributionMaximumRaw;
        var gap = GetHeightColorRangeMinimumGap();
        if (heightDisplayIsSignedDelta)
        {
            var radius = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            var sourceRadius = Math.Max(Math.Abs(sourceMinimum), Math.Abs(sourceMaximum));
            if (sourceRadius == 0) sourceRadius = 1;
            radius = heightDisplayOptions.KeepsRange(heightDisplayWorkspace) ? Math.Max(gap, radius) : Math.Clamp(radius, gap, sourceRadius);
            ApplyC3DHeightColorRange(-radius, radius, isAuto: false);
            return;
        }
        if (sourceMaximum - sourceMinimum <= gap && !heightDisplayOptions.KeepsRange(heightDisplayWorkspace))
        {
            ApplyC3DHeightColorRange(sourceMinimum, sourceMaximum, isAuto: true);
            return;
        }

        if (!heightDisplayOptions.KeepsRange(heightDisplayWorkspace))
        {
            minimum = Math.Clamp(minimum, sourceMinimum, sourceMaximum - gap);
            maximum = Math.Clamp(maximum, sourceMinimum + gap, sourceMaximum);
        }
        if (minimum >= maximum)
        {
            if (!NearHeightColorValue(minimum, C3DHeightColorMinimumRaw))
            {
                minimum = maximum - gap;
            }
            else
            {
                maximum = minimum + gap;
            }
        }

        ApplyC3DHeightColorRange(minimum, maximum, isAuto: false);
    }

    private void ApplyC3DHeightColorRange(double minimum, double maximum, bool isAuto)
    {
        if (heightDisplayIsSignedDelta)
        {
            var radius = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            if (radius == 0) radius = 1;
            (minimum, maximum) = (-radius, radius);
        }
        var changed = !NearHeightColorValue(c3DHeightColorMinimumRaw, minimum)
            || !NearHeightColorValue(c3DHeightColorMaximumRaw, maximum)
            || C3DHeightColorRangeAuto != isAuto;
        if (!changed)
        {
            return;
        }

        c3DHeightColorMinimumRaw = minimum;
        c3DHeightColorMaximumRaw = maximum;
        if (!isAuto) heightDisplayOptions = heightDisplayOptions with { RangeMode = "manual" };
        C3DHeightColorRangeAuto = isAuto;
        C3DHeightColorRangeRevision = unchecked(C3DHeightColorRangeRevision + 1);
        NotifyHeightColorRangeProperties();
        RefreshC3DHeightDistributionLegend();
        ViewerStatus = C3DHeightColorRangeSummary;
    }

    private double GetHeightColorRangeStep()
    {
        if (c3DHeightDistribution is null)
        {
            return 1.0;
        }

        var span = c3DHeightDistribution.Maximum - c3DHeightDistribution.Minimum;
        return span > 0.0 && double.IsFinite(span)
            ? Math.Max(span / 20.0, GetHeightColorRangeMinimumGap())
            : Math.Max(Math.Abs(c3DHeightDistribution.Minimum) * 0.05, 1.0);
    }

    private double GetHeightColorRangeMinimumGap()
    {
        if (c3DHeightDistribution is null)
        {
            return 1e-9;
        }

        var span = Math.Abs(c3DHeightDistribution.Maximum - c3DHeightDistribution.Minimum);
        return Math.Max(span * 1e-6, 1e-9);
    }

    private static bool NearHeightColorValue(double left, double right) =>
        left.Equals(right)
        || double.IsFinite(left)
        && double.IsFinite(right)
        && Math.Abs(left - right) <= Math.Max(Math.Abs(left), Math.Abs(right)) * 1e-12;

    private void NotifyHeightColorRangeProperties()
    {
        OnPropertyChanged(nameof(C3DHeightColorMinimumRaw));
        OnPropertyChanged(nameof(C3DHeightColorMaximumRaw));
        OnPropertyChanged(nameof(C3DHeightColorRangeSummary));
        OnPropertyChanged(nameof(HeightDisplayOptions));
        OnPropertyChanged(nameof(C3DHeightLegendMode));
        OnPropertyChanged(nameof(C3DHeightLegendInformation));
    }

    private void RefreshC3DHeightDistributionLegend()
    {
        OnPropertyChanged(nameof(C3DHeightColorTickLabels));
        var palette = SelectedColorMode;
        var scalarPalette = palette is "Height" or "Grayscale" or "Thermal" or "ColorVisionAssist";
        C3DHeightDistributionPaletteLabel =
            $"{palette} · {(C3DHeightColorRangeAuto ? "auto" : "manual")} range";
        if (scalarPalette)
        {
            C3DHeightDistributionGradient = CreateHeightGradient(palette);
            if (c3DHeightDistribution is { } distribution && (heightDisplayOptions.ColorStyle != "continuous"
                || heightDisplayOptions.HighlightOutside || C3DHeightColorMinimumRaw != distribution.Minimum || C3DHeightColorMaximumRaw != distribution.Maximum))
            {
                var brush = new LinearGradientBrush { StartPoint = new Point(.5, 1), EndPoint = new Point(.5, 0) };
                foreach (var stop in C3DPointMapPalette.DisplayStops(C3DHeightColorMinimumRaw, C3DHeightColorMaximumRaw,
                    C3DHeightDistributionMinimumRaw, C3DHeightDistributionMaximumRaw, palette, heightDisplayOptions)) brush.GradientStops.Add(CreateGradientStop(stop.Color, stop.Offset));
                brush.Freeze();
                C3DHeightDistributionGradient = brush;
            }
        }

        C3DHeightDistributionVisible = c3DHeightDistribution is not null
            && C3DSampleVisible
            && Display.EffectiveSettings.Source == ViewerDisplaySourceKind.C3DHeightGrid
            && scalarPalette && !C3DTexture.IsVisible;
    }

    private static string CreateHistogramPath(C3DHeightDistribution distribution, bool peakOnly)
    {
        const double width = 44.0;
        const double height = C3DHeightDistributionPlotHeight;
        var maximumCount = distribution.Bins.Max();
        var binHeight = height / distribution.BinCount;
        var path = new StringBuilder();
        for (var index = 0; index < distribution.BinCount; index++)
        {
            if (peakOnly != (index == distribution.PeakBinIndex))
            {
                continue;
            }

            var count = distribution.Bins[index];
            if (count == 0)
            {
                continue;
            }

            var y = height - (index + 1) * binHeight;
            var barWidth = width * count / maximumCount;
            path.Append(string.Create(
                CultureInfo.InvariantCulture,
                $"M 0,{y:F2} H {barWidth:F2} V {y + binHeight:F2} H 0 Z "));
        }

        return path.Length == 0 ? "M 0,118 L 0,118" : path.ToString();
    }

    private static string FormatCompactCount(int count) => count switch
    {
        >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{count / 1_000_000.0:F2}M"),
        >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{count / 1_000.0:F1}K"),
        _ => count.ToString(CultureInfo.InvariantCulture)
    };

    private static string FormatRawValue(double value, double displaySpan)
    {
        if (!double.IsFinite(value))
        {
            return "--";
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static double CalculateMeanLabelTop(C3DHeightDistribution distribution)
    {
        if (distribution.IsConstant)
        {
            return (C3DHeightDistributionPlotHeight - C3DHeightDistributionLabelHeight) * 0.5;
        }

        var normalized = (distribution.Mean - distribution.Minimum)
            / (distribution.Maximum - distribution.Minimum);
        var desiredTop = (1.0 - normalized) * C3DHeightDistributionPlotHeight
            - C3DHeightDistributionLabelHeight * 0.5;
        var minimumTop = C3DHeightDistributionLabelHeight + C3DHeightDistributionLabelGap;
        var maximumTop = C3DHeightDistributionPlotHeight
            - 2.0 * C3DHeightDistributionLabelHeight
            - C3DHeightDistributionLabelGap;
        return Math.Clamp(
            desiredTop,
            minimumTop,
            maximumTop);
    }

    private static LinearGradientBrush CreateHeightGradient(string palette)
    {
        var stops = palette switch
        {
            "Grayscale" => new[]
            {
                CreateGradientStop(ViewerColorMapPalette.Grayscale(0.0), 0.0),
                CreateGradientStop(ViewerColorMapPalette.Grayscale(0.5), 0.5),
                CreateGradientStop(ViewerColorMapPalette.Grayscale(1.0), 1.0)
            },
            "ColorVisionAssist" => new[]
            {
                CreateGradientStop(C3DPointMapPalette.ColorVisionAssist(0), 0),
                CreateGradientStop(C3DPointMapPalette.ColorVisionAssist(0.25), 0.25),
                CreateGradientStop(C3DPointMapPalette.ColorVisionAssist(0.5), 0.5),
                CreateGradientStop(C3DPointMapPalette.ColorVisionAssist(0.75), 0.75),
                CreateGradientStop(C3DPointMapPalette.ColorVisionAssist(1), 1)
            },
            "Thermal" => new[]
            {
                CreateGradientStop(ViewerColorMapPalette.Thermal(0.0), 0.0),
                CreateGradientStop(ViewerColorMapPalette.Thermal(1.0 / 3.0), 1.0 / 3.0),
                CreateGradientStop(ViewerColorMapPalette.Thermal(2.0 / 3.0), 2.0 / 3.0),
                CreateGradientStop(ViewerColorMapPalette.Thermal(1.0), 1.0)
            },
            _ => new[]
            {
                CreateGradientStop(C3DPointMapPalette.Height(0.0), 0.0),
                CreateGradientStop(C3DPointMapPalette.Height(0.5), 0.5),
                CreateGradientStop(C3DPointMapPalette.Height(1.0), 1.0)
            }
        };

        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0.5, 1.0),
            EndPoint = new Point(0.5, 0.0)
        };
        foreach (var stop in stops)
        {
            brush.GradientStops.Add(stop);
        }

        brush.Freeze();
        return brush;
    }

    private static GradientStop CreateGradientStop(
        (double R, double G, double B) color,
        double offset) =>
        new(
            Color.FromRgb(ToColorByte(color.R), ToColorByte(color.G), ToColorByte(color.B)),
            offset);

    private static byte ToColorByte(double value) =>
        (byte)(Math.Clamp(value, 0.0, 1.0) * byte.MaxValue);

    private void NotifyC3DHeightDistributionContractProperties()
    {
        OnPropertyChanged(nameof(C3DHeightDistributionBinCount));
        OnPropertyChanged(nameof(C3DHeightDistributionBinSum));
        OnPropertyChanged(nameof(C3DHeightDistributionValidSampleCount));
        OnPropertyChanged(nameof(C3DHeightDistributionMissingSampleCount));
        OnPropertyChanged(nameof(C3DHeightDistributionMinimumRaw));
        OnPropertyChanged(nameof(C3DHeightDistributionMaximumRaw));
        OnPropertyChanged(nameof(C3DHeightDistributionMeanRaw));
        OnPropertyChanged(nameof(C3DHeightDistributionPeakLowerRaw));
        OnPropertyChanged(nameof(C3DHeightDistributionPeakUpperRaw));
        OnPropertyChanged(nameof(C3DHeightDistributionPeakFraction));
        OnPropertyChanged(nameof(C3DHeightDistributionIsConstant));
    }
}

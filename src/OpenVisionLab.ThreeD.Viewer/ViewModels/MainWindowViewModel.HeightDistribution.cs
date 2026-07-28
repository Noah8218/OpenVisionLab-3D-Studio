using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Models;

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

    public double C3DHeightDistributionMinimumRaw => c3DHeightDistribution?.Minimum ?? double.NaN;

    public double C3DHeightDistributionMaximumRaw => c3DHeightDistribution?.Maximum ?? double.NaN;

    public double C3DHeightDistributionMeanRaw => c3DHeightDistribution?.Mean ?? double.NaN;

    public double C3DHeightDistributionPeakLowerRaw => c3DHeightDistribution?.PeakLowerBound ?? double.NaN;

    public double C3DHeightDistributionPeakUpperRaw => c3DHeightDistribution?.PeakUpperBound ?? double.NaN;

    public double C3DHeightDistributionPeakFraction => c3DHeightDistribution?.PeakFraction ?? double.NaN;

    public bool C3DHeightDistributionIsConstant => c3DHeightDistribution?.IsConstant == true;

    internal void SetC3DHeightDistribution(C3DHeightDistribution distribution, string sourceSha256)
    {
        ArgumentNullException.ThrowIfNull(distribution);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceSha256);

        c3DHeightDistribution = distribution;
        C3DHeightDistributionSourceSha256 = sourceSha256;
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
        NotifyC3DHeightDistributionContractProperties();
        RefreshC3DHeightDistributionLegend();
    }

    internal void ClearC3DHeightDistribution()
    {
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
        NotifyC3DHeightDistributionContractProperties();
    }

    private void RefreshC3DHeightDistributionLegend()
    {
        var palette = SelectedColorMode;
        var scalarPalette = palette is "Height" or "Grayscale" or "Thermal";
        C3DHeightDistributionPaletteLabel = $"{palette} · full source";
        if (scalarPalette)
        {
            C3DHeightDistributionGradient = CreateHeightGradient(palette);
        }

        C3DHeightDistributionVisible = c3DHeightDistribution is not null
            && C3DSampleVisible
            && Display.EffectiveSettings.Source == ViewerDisplaySourceKind.C3DHeightGrid
            && scalarPalette;
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

        var absoluteSpan = Math.Abs(displaySpan);
        return absoluteSpan switch
        {
            0.0 => value.ToString("G9", CultureInfo.InvariantCulture),
            < 0.01 => value.ToString("G9", CultureInfo.InvariantCulture),
            < 1.0 => value.ToString("F4", CultureInfo.InvariantCulture),
            < 100.0 => value.ToString("F2", CultureInfo.InvariantCulture),
            _ => value.ToString("F1", CultureInfo.InvariantCulture)
        };
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

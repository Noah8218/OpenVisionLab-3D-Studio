using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public static class C3DHeightDistributionVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D C3D height-distribution verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var fixtureRoot = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "C3DHeightDistributionVerification",
            Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "known.C3D");
            WriteC3D(sourcePath, 3, 2, [10.0f, 20.0f, 30.0f, 0.0f, 40.0f, 50.0f]);
            var dense = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 1000);
            var sparse = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 1);
            var distribution = dense.HeightDistribution;

            var progressValues = new List<double>();
            var progressGrid = C3DHeightGrid.Load(
                sourcePath,
                maxRenderedPoints: 1000,
                CancellationToken.None,
                new CallbackProgress<double>(progressValues.Add));
            Check(
                "C3D load progress is monotonic and completes at 100 percent",
                progressGrid.ContentSha256 == dense.ContentSha256
                && progressValues.Count >= 4
                && Near(progressValues[0], 0.0)
                && Near(progressValues[^1], 100.0)
                && progressValues.Zip(progressValues.Skip(1), (left, right) => left <= right).All(value => value),
                string.Join(",", progressValues.Select(value => value.ToString("F1", CultureInfo.InvariantCulture))));

            using var cancellation = new CancellationTokenSource();
            var cancellationProgress = new CallbackProgress<double>(value =>
            {
                if (value >= 5.0)
                {
                    cancellation.Cancel();
                }
            });
            Check(
                "C3D load observes cancellation before returning a replacement grid",
                Throws<OperationCanceledException>(() => C3DHeightGrid.Load(
                    sourcePath,
                    maxRenderedPoints: 1000,
                    cancellation.Token,
                    cancellationProgress)),
                $"cancelled={cancellation.IsCancellationRequested}");

            Check(
                "full-source statistics are exact",
                Near(distribution.Minimum, 10.0)
                && Near(distribution.Mean, 30.0)
                && Near(distribution.Maximum, 50.0)
                && distribution.ValidSampleCount == 5
                && distribution.MissingSampleCount == 1,
                Describe(distribution));

            var tinySpanPath = Path.Combine(fixtureRoot, "tiny-span.C3D");
            WriteC3D(tinySpanPath, 2, 1, [1.0f, 1.00005f]);
            var tinySpanGrid = C3DHeightGrid.Load(tinySpanPath, maxRenderedPoints: 1000);
            var tinySpanViewModel = new MainWindowViewModel();
            tinySpanViewModel.UseC3DSmokeScene();
            tinySpanViewModel.SetC3DHeightDistribution(
                tinySpanGrid.HeightDistribution,
                tinySpanGrid.ContentSha256);
            var tinyLowLabel = tinySpanViewModel.C3DHeightDistributionLowLabel[2..];
            var tinyMeanLabel = tinySpanViewModel.C3DHeightDistributionMeanLabel[2..];
            var tinyHighLabel = tinySpanViewModel.C3DHeightDistributionHighLabel[2..];
            var tinyLabelWidths = new[]
            {
                tinySpanViewModel.C3DHeightDistributionLowLabel,
                tinySpanViewModel.C3DHeightDistributionMeanLabel,
                tinySpanViewModel.C3DHeightDistributionHighLabel
            }.Select(MeasureLabelWidth).ToArray();
            Check(
                "tiny non-constant span shares full 0-to-1 normalization with distinct fitting labels",
                Near(tinySpanGrid.Points.Min(point => point.HeightScalar), 0.0)
                && Near(tinySpanGrid.Points.Max(point => point.HeightScalar), 1.0)
                && tinyHighLabel != tinyMeanLabel
                && tinyMeanLabel != tinyLowLabel
                && tinyLabelWidths.All(width => width <= MainWindowViewModel.C3DHeightDistributionLabelColumnWidth)
                && tinySpanViewModel.C3DHeightDistributionPeakLabel.Contains('–'),
                $"scalars={tinySpanGrid.Points.Min(point => point.HeightScalar):R}..{tinySpanGrid.Points.Max(point => point.HeightScalar):R}|labels={tinySpanViewModel.C3DHeightDistributionLowLabel},{tinySpanViewModel.C3DHeightDistributionMeanLabel},{tinySpanViewModel.C3DHeightDistributionHighLabel}|widths={string.Join(',', tinyLabelWidths.Select(width => width.ToString("F2", CultureInfo.InvariantCulture)))}|column={MainWindowViewModel.C3DHeightDistributionLabelColumnWidth:F1}|{tinySpanViewModel.C3DHeightDistributionPeakLabel}");

            var wideSpanPath = Path.Combine(fixtureRoot, "wide-span.C3D");
            WriteC3D(wideSpanPath, 2, 1, [-float.MaxValue, float.MaxValue]);
            var wideSpanGrid = C3DHeightGrid.Load(wideSpanPath, maxRenderedPoints: 1000);
            Check(
                "finite float-wide source produces finite endpoint scalars",
                wideSpanGrid.Points.Length == 2
                && wideSpanGrid.Points.All(point => double.IsFinite(point.HeightScalar))
                && Near(wideSpanGrid.Points.Min(point => point.HeightScalar), 0.0)
                && Near(wideSpanGrid.Points.Max(point => point.HeightScalar), 1.0),
                $"minRaw={wideSpanGrid.Min:R}|maxRaw={wideSpanGrid.Max:R}|scalars={wideSpanGrid.Points.Min(point => point.HeightScalar):R}..{wideSpanGrid.Points.Max(point => point.HeightScalar):R}");
            Check(
                "32 bins preserve every valid source sample",
                distribution.BinCount == C3DHeightDistribution.DefaultBinCount
                && distribution.Bins.Sum() == distribution.ValidSampleCount
                && distribution.Bins[0] == 1
                && distribution.Bins[8] == 1
                && distribution.Bins[16] == 1
                && distribution.Bins[24] == 1
                && distribution.Bins[31] == 1,
                string.Join(",", distribution.Bins));
            Check(
                "render density cannot change the distribution",
                sparse.Points.Length != dense.Points.Length
                && sparse.HeightDistribution.Bins.SequenceEqual(distribution.Bins)
                && Near(sparse.HeightDistribution.Mean, distribution.Mean),
                $"sparsePoints={sparse.Points.Length}|densePoints={dense.Points.Length}");
            Check(
                "peak tie is deterministic at the lowest raw-height bin",
                distribution.PeakBinIndex == 0
                && Near(distribution.PeakLowerBound, 10.0)
                && Near(distribution.PeakUpperBound, 11.25)
                && Near(distribution.PeakFraction, 0.2),
                $"peak={distribution.PeakBinIndex}|range={distribution.PeakLowerBound:R}..{distribution.PeakUpperBound:R}");

            var constantPath = Path.Combine(fixtureRoot, "constant.C3D");
            WriteC3D(constantPath, 2, 2, [42.0f, 42.0f, 0.0f, float.NaN]);
            var constant = C3DHeightGrid.Load(constantPath, maxRenderedPoints: 1).HeightDistribution;
            Check(
                "constant source uses one finite bin without division by zero",
                constant.IsConstant
                && constant.Bins[0] == 2
                && constant.Bins.Skip(1).All(count => count == 0)
                && Near(constant.PeakCenter, 42.0)
                && Near(constant.PeakFraction, 1.0),
                Describe(constant));

            var skewPath = Path.Combine(fixtureRoot, "skew.C3D");
            WriteC3D(skewPath, 2, 2, [10.0f, 10.0f, 10.0f, 50.0f]);
            var skewGrid = C3DHeightGrid.Load(skewPath, maxRenderedPoints: 1000);
            var skewViewModel = new MainWindowViewModel();
            skewViewModel.UseC3DSmokeScene();
            skewViewModel.SetC3DHeightDistribution(skewGrid.HeightDistribution, skewGrid.ContentSha256);

            var edgeSkewPath = Path.Combine(fixtureRoot, "edge-skew.C3D");
            var edgeSkewValues = Enumerable.Repeat(10.0f, 99).Append(50.0f).ToArray();
            WriteC3D(edgeSkewPath, 100, 1, edgeSkewValues);
            var edgeSkewGrid = C3DHeightGrid.Load(edgeSkewPath, maxRenderedPoints: 1000);
            var edgeSkewViewModel = new MainWindowViewModel();
            edgeSkewViewModel.UseC3DSmokeScene();
            edgeSkewViewModel.SetC3DHeightDistribution(
                edgeSkewGrid.HeightDistribution,
                edgeSkewGrid.ContentSha256);
            var minimumSafeTop = MainWindowViewModel.C3DHeightDistributionLabelHeight
                + MainWindowViewModel.C3DHeightDistributionLabelGap;
            var maximumSafeTop = MainWindowViewModel.C3DHeightDistributionPlotHeight
                - 2.0 * MainWindowViewModel.C3DHeightDistributionLabelHeight
                - MainWindowViewModel.C3DHeightDistributionLabelGap;
            Check(
                "mean label follows skewed data and remains separated from endpoint labels",
                Near(skewGrid.HeightDistribution.Mean, 20.0)
                && Near(skewViewModel.C3DHeightDistributionMeanLabelTop, 82.5)
                && Near(edgeSkewViewModel.C3DHeightDistributionMeanLabelTop, maximumSafeTop)
                && skewViewModel.C3DHeightDistributionMeanLabelTop >= minimumSafeTop
                && skewViewModel.C3DHeightDistributionMeanLabelTop <= maximumSafeTop,
                $"skewTop={skewViewModel.C3DHeightDistributionMeanLabelTop:R}|edgeTop={edgeSkewViewModel.C3DHeightDistributionMeanLabelTop:R}|safe={minimumSafeTop:R}..{maximumSafeTop:R}");

            var invalidPath = Path.Combine(fixtureRoot, "invalid.C3D");
            WriteC3D(invalidPath, 2, 2, [0.0f, float.NaN, float.PositiveInfinity, 0.0f]);
            Check(
                "source without valid heights is rejected",
                Throws<InvalidDataException>(() => C3DHeightGrid.Load(invalidPath)),
                "all values are zero or non-finite");

            var viewModel = new MainWindowViewModel();
            Check(
                "empty legend state is hidden",
                !viewModel.C3DHeightDistributionVisible
                && viewModel.C3DHeightDistributionBinCount == 0
                && viewModel.C3DHeightDistributionSourceSha256 == "not loaded",
                $"visible={viewModel.C3DHeightDistributionVisible}|bins={viewModel.C3DHeightDistributionBinCount}");

            var previewBefore = viewModel.PreviewToolResult;
            var resultsBefore = viewModel.ResultEntities;
            viewModel.UseC3DSmokeScene();
            viewModel.SetC3DHeightDistribution(distribution, dense.ContentSha256);
            Check(
                "C3D Height palette shows full-source raw-height state",
                viewModel.C3DHeightDistributionVisible
                && viewModel.C3DHeightDistributionBinSum == 5
                && Near(viewModel.C3DHeightDistributionMinimumRaw, 10.0)
                && Near(viewModel.C3DHeightDistributionMeanRaw, 30.0)
                && Near(viewModel.C3DHeightDistributionMaximumRaw, 50.0)
                && Near(viewModel.C3DHeightDistributionMeanLabelTop, 53.0),
                $"visible={viewModel.C3DHeightDistributionVisible}|{viewModel.C3DHeightDistributionPeakLabel}");
            Check(
                "histogram and peak marker paths are present",
                viewModel.C3DHeightDistributionHistogramPathData.Contains(" V ", StringComparison.Ordinal)
                && viewModel.C3DHeightDistributionPeakPathData.Contains(" V ", StringComparison.Ordinal),
                $"histogramLength={viewModel.C3DHeightDistributionHistogramPathData.Length}|peakLength={viewModel.C3DHeightDistributionPeakPathData.Length}");
            Check(
                "Height gradient is generated from the rendering palette",
                HasStops(viewModel.C3DHeightDistributionGradient, 3)
                && viewModel.C3DHeightDistributionGradient.GradientStops[0].Color == ToColor(C3DPointMapPalette.Height(0.0))
                && viewModel.C3DHeightDistributionGradient.GradientStops[^1].Color == ToColor(C3DPointMapPalette.Height(1.0)),
                DescribeStops(viewModel.C3DHeightDistributionGradient));

            viewModel.SelectedColorMode = "Grayscale";
            Check(
                "Grayscale keeps the distribution visible and changes only its palette",
                viewModel.C3DHeightDistributionVisible
                && HasStops(viewModel.C3DHeightDistributionGradient, 3)
                && viewModel.C3DHeightDistributionGradient.GradientStops[0].Color == Colors.Black
                && viewModel.C3DHeightDistributionGradient.GradientStops[^1].Color == Colors.White,
                DescribeStops(viewModel.C3DHeightDistributionGradient));

            viewModel.SelectedColorMode = "Thermal";
            Check(
                "Thermal keeps the distribution visible with four rendering stops",
                viewModel.C3DHeightDistributionVisible
                && HasStops(viewModel.C3DHeightDistributionGradient, 4)
                && viewModel.C3DHeightDistributionGradient.GradientStops[1].Color == Colors.Red
                && viewModel.C3DHeightDistributionGradient.GradientStops[2].Color == Colors.Yellow,
                DescribeStops(viewModel.C3DHeightDistributionGradient));

            viewModel.SelectedColorMode = "Solid";
            Check(
                "Solid hides a height legend that would be misleading",
                !viewModel.C3DHeightDistributionVisible,
                viewModel.C3DHeightDistributionPaletteLabel);

            viewModel.Display.ConfigureC3DHeightGrid(deviationAvailable: true);
            viewModel.SelectedColorMode = "Deviation";
            Check(
                "Deviation reserves the right-side scale for result deviation",
                !viewModel.C3DHeightDistributionVisible,
                viewModel.C3DHeightDistributionPaletteLabel);

            viewModel.LazSampleVisible = true;
            Check(
                "higher-priority non-C3D source hides stale C3D distribution state",
                viewModel.C3DSampleVisible
                && viewModel.Display.ActiveSource == "LAZ/LAS point cloud"
                && !viewModel.C3DHeightDistributionVisible,
                $"active={viewModel.Display.ActiveSource}|visible={viewModel.C3DHeightDistributionVisible}");
            Check(
                "legend operations do not run Preview or publish results",
                ReferenceEquals(previewBefore, viewModel.PreviewToolResult)
                && ReferenceEquals(resultsBefore, viewModel.ResultEntities),
                $"preview={viewModel.PreviewToolResult.Status}|results={viewModel.ResultEntities.Count}");

            viewModel.ClearC3DHeightDistribution();
            Check(
                "clear removes source identity, bins, and visibility",
                !viewModel.C3DHeightDistributionVisible
                && viewModel.C3DHeightDistributionBinCount == 0
                && viewModel.C3DHeightDistributionSourceSha256 == "not loaded",
                $"visible={viewModel.C3DHeightDistributionVisible}|bins={viewModel.C3DHeightDistributionBinCount}|source={viewModel.C3DHeightDistributionSourceSha256}");

            summary = $"C3D height-distribution verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"C3D height-distribution verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(fixtureRoot))
                {
                    Directory.Delete(fixtureRoot, recursive: true);
                }
            }
            catch (IOException exception)
            {
                lines.Add($"Fixture cleanup warning: {exception.Message}");
            }
        }

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS|{name}|{detail}");
        }
    }

    private static bool HasStops(LinearGradientBrush brush, int count) =>
        brush.GradientStops.Count == count;

    private static double MeasureLabelWidth(string text)
    {
        var textBlock = new TextBlock
        {
            FontSize = 9.0,
            Text = text
        };
        textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return textBlock.DesiredSize.Width;
    }

    private static bool Near(double actual, double expected) =>
        Math.Abs(actual - expected) <= 1e-12;

    private static string Describe(C3DHeightDistribution distribution) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"min={distribution.Minimum:R}|mean={distribution.Mean:R}|max={distribution.Maximum:R}|valid={distribution.ValidSampleCount}|missing={distribution.MissingSampleCount}|peak={distribution.PeakBinIndex}");

    private static string DescribeStops(LinearGradientBrush brush) =>
        string.Join(",", brush.GradientStops.Select(stop => $"{stop.Offset:R}:{stop.Color}"));

    private static Color ToColor((double R, double G, double B) color) =>
        Color.FromRgb(ToByte(color.R), ToByte(color.G), ToByte(color.B));

    private static byte ToByte(double value) =>
        (byte)(Math.Clamp(value, 0.0, 1.0) * byte.MaxValue);

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private sealed class CallbackProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }

    private static void WriteC3D(string path, int width, int height, IReadOnlyList<float> values)
    {
        if (values.Count != width * height)
        {
            throw new ArgumentException("C3D fixture value count does not match its dimensions.", nameof(values));
        }

        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(width);
        writer.Write(height);
        foreach (var value in values)
        {
            writer.Write(value);
        }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}

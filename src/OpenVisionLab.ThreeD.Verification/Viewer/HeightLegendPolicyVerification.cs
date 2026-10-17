using System.IO;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Presentation.Models;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer.ViewModels;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class HeightLegendPolicyVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")}|{name}");
        }
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(directory);
        try
        {
            var values = Enumerable.Range(1, 1000).Select(value => (double)value).ToArray();
            var sorted = C3DHeightImageFrame.SortDisplaySamples(values.Concat([double.NaN, double.PositiveInfinity]).ToArray());
            Check("distribution uses all finite cells and trims exactly 0.3 percent at either end",
                sorted.Length == 1000 && C3DHeightImageFrame.GetDistributionDisplayRange(sorted) == (4d, 997d));
            Check("small and constant display grids retain full range",
                C3DHeightImageFrame.GetDistributionDisplayRange([1, 2, 7]) == (1d, 7d)
                && C3DHeightImageFrame.GetDistributionDisplayRange(Enumerable.Repeat(5d, 1000).ToArray()) == (5d, 5d));
            Check("region range uses the selected full-resolution cell rectangle",
                C3DHeightImageFrame.TryGetRegionDisplayRange(values, 100, 10, new(2, 3, 2, 4), out var low, out var high) && low == 204 && high == 307);
            Check("absent, out-of-map and missing-only regions do not return usable bounds",
                !C3DHeightImageFrame.TryGetRegionDisplayRange(values, 100, 10, null, out _, out _)
                && !C3DHeightImageFrame.TryGetRegionDisplayRange([], 0, 0, new(0, 0, 2, 2), out _, out _)
                && !C3DHeightImageFrame.TryGetRegionDisplayRange(values, 100, 10, new(9, 0, 2, 1), out _, out _)
                && !C3DHeightImageFrame.TryGetRegionDisplayRange([double.NaN, double.NaN], 2, 1, new(0, 0, 1, 2), out _, out _));
            Check("outside percentages exclude equality at both limits and ignore missing cells",
                C3DHeightImageFrame.GetOutsideDisplayPercentages(sorted, 101, 900) == (10d, 10d));
            var defaults = new HeightDisplayOptions();
            Check("pin defaults are teaching off, validation and inspection on",
                !defaults.KeepsRange("teach") && defaults.KeepsRange("validate") && defaults.KeepsRange("inspect"));
            var bands = defaults with { ColorStyle = "bands", BandCount = 10 };
            Check("band colours remain constant within a band and change at its boundary",
                C3DPointMapPalette.DisplayColor(11, 0, 100, "Height", bands) == C3DPointMapPalette.DisplayColor(19, 0, 100, "Height", bands)
                && C3DPointMapPalette.DisplayColor(19, 0, 100, "Height", bands) != C3DPointMapPalette.DisplayColor(21, 0, 100, "Height", bands));
            Check("outside highlighting differs from clamping and keeps the two tails distinct",
                C3DPointMapPalette.DisplayColor(-1, 0, 100, "Height", defaults with { HighlightOutside = true }) != C3DPointMapPalette.DisplayColor(-1, 0, 100, "Height", defaults)
                && C3DPointMapPalette.DisplayColor(-1, 0, 100, "Height", defaults with { HighlightOutside = true }) != C3DPointMapPalette.DisplayColor(101, 0, 100, "Height", defaults with { HighlightOutside = true }));
            var firstPath = Path.Combine(directory, "legend-first.C3D");
            var secondPath = Path.Combine(directory, "legend-second.C3D");
            Write(firstPath, values);
            Write(secondPath, values.Select(value => value + 1000));
            var first = C3DHeightGrid.Load(firstPath);
            var second = C3DHeightGrid.Load(secondPath);
            var cacheKey = new C3DDisplayListKey(first, default, ViewerGeometryStyle.Surface, ViewerColorMap.Height, 1, 1000, 2, 1, C3DWireframeLodLevel.Precise);
            var gpuKey = new C3DGpuBufferKey(first, default, ViewerColorMap.Height, 1, 1000, 1, null);
            Check("both 3D caches invalidate on colour-style changes without a numeric range change",
                cacheKey != (cacheKey with { HeightColorOptions = ("bands", 10, false, string.Empty) })
                && gpuKey != (gpuKey with { HeightColorOptions = ("continuous", 0, true, string.Empty) }));
            var threeD = new MainWindowViewModel { C3DSampleVisible = true };
            threeD.SetC3DDisplayCapabilities(true);
            threeD.SetC3DHeightDistribution(first.HeightDistribution, first.ContentSha256, first.ReadHeightMapValues(), 100, 10);
            var sdkValues = Enumerable.Repeat(double.NaN, 1000).ToArray();
            sdkValues[0] = 4; sdkValues[1] = 5; sdkValues[2] = 6;
            var measurement = new HeightMeasurementColorSnapshot("source", first.ContentSha256, "execution.first", 100, 10,
                sdkValues, 5, 5, "raw-height", "Thickness");
            sdkValues[1] = 100;
            Check("measurement snapshot owns values and preserves finite zero/range identity", measurement.Values.Span[1] == 5
                && measurement.MatchesSource(first.ContentSha256, 100, 10) && !measurement.MatchesSource(second.ContentSha256, 100, 10));
            var measuredOptions = defaults with { ColorStyle = "tolerance", Measurement = measurement };
            Check("three tolerance colours use executed SDK cell values and inclusive equal limits",
                C3DPointMapPalette.DisplayColor(900, 1, 1000, "Height", measuredOptions, 0) == (79 / 255d, 139 / 255d, 232 / 255d)
                && C3DPointMapPalette.DisplayColor(900, 1, 1000, "Height", measuredOptions, 1) == (145 / 255d, 209 / 255d, 168 / 255d)
                && C3DPointMapPalette.DisplayColor(900, 1, 1000, "Height", measuredOptions, 2) == (229 / 255d, 83 / 255d, 75 / 255d));
            Check("outside the actual measured region remains the input palette", C3DPointMapPalette.DisplayColor(500, 1, 1000, "Height", measuredOptions, 3)
                == C3DPointMapPalette.DisplayColor(500, 1, 1000, "Height", defaults, 3));
            Check("persistent display preferences exclude all measurement values and executed limits", measuredOptions.Sanitize(forPersistence: true).Measurement is null
                && !JsonSerializer.Serialize(measuredOptions).Contains("execution.first", StringComparison.Ordinal));
            Check("3D rejects tolerance without an actual matching execution", !threeD.TryApplyHeightDisplayOptions(defaults with { ColorStyle = "tolerance" }));
            threeD.SetHeightMeasurementColor(measurement);
            Check("3D accepts current snapshot and reads cells by row/column", threeD.TryApplyHeightDisplayOptions(measuredOptions)
                && threeD.CanUseMeasurementColor && threeD.GetC3DHeightDisplayColor(900, 0, 1) == (145 / 255d, 209 / 255d, 168 / 255d));
            var measuredKey = cacheKey with { HeightColorOptions = ("tolerance", 0, false, "execution.first") };
            Check("new executed limits invalidate caches even with unchanged display range", measuredKey != (measuredKey with { HeightColorOptions = ("tolerance", 0, false, "execution.second") })
                && (gpuKey with { HeightColorOptions = measuredKey.HeightColorOptions }) != (gpuKey with { HeightColorOptions = ("tolerance", 0, false, "execution.second") }));
            threeD.ResetC3DHeightColorRange();
            Check("Auto exits tolerance colours even when the numeric auto range is unchanged", threeD.HeightDisplayOptions.ColorStyle == "continuous"
                && threeD.C3DHeightLegendMode != "Tolerance colours");
            threeD.TryApplyHeightDisplayOptions(measuredOptions);
            threeD.SetHeightMeasurementColor(null);
            Check("stale or absent execution falls back without retaining tolerance", !threeD.CanUseMeasurementColor && threeD.HeightDisplayOptions.ColorStyle == "continuous");
            using (var measuredImage = new HeightImageViewerViewModel(ThreeDLocalization.Shared, new SharedHeightCursorSession()))
            {
                measuredImage.EnsureSourceAsync(firstPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
                measuredImage.SetHeightMeasurementColor(measurement);
                Check("2D current snapshot changes actual BGRA cells", measuredImage.TryApplyHeightDisplayOptions(measuredOptions)
                    && measuredImage.DisplayFrame!.Bgra32Pixels.Span[4] == 168
                    && measuredImage.DisplayFrame.Bgra32Pixels.Span[5] == 209 && measuredImage.DisplayFrame.Bgra32Pixels.Span[6] == 145);
                measuredImage.EnsureSourceAsync(secondPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
                Check("file replacement cannot reuse a previous measurement colour snapshot", !measuredImage.CanUseMeasurementColor
                    && measuredImage.HeightDisplayOptions.ColorStyle == "continuous" && !measuredImage.TryApplyHeightDisplayOptions(measuredOptions));
            }
            var renderRange = (threeD.HeightColorRenderMinimum, threeD.HeightColorRenderMaximum);
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, 800));
            Check("3D range drag updates labels while preserving the previous render cache range", threeD.HeightDisplayOptions.IsRangeDragActive
                && threeD.C3DHeightColorMaximumRaw == 800 && (threeD.HeightColorRenderMinimum, threeD.HeightColorRenderMaximum) == renderRange);
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Commit));
            Check("release commits the latest colour range to 3D", !threeD.HeightDisplayOptions.IsRangeDragActive && threeD.HeightColorRenderMaximum == 800);
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, 700));
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Cancel));
            Check("Escape restores both legend and 3D render range", !threeD.HeightDisplayOptions.IsRangeDragActive
                && threeD.HeightColorRenderMaximum == 800 && threeD.C3DHeightColorMaximumRaw == 800);
            threeD.ResetC3DHeightColorRange();
            var preview = threeD.PreviewToolResult;
            var results = threeD.ResultEntities;
            Check("3D distribution mode preserves the requested mode and exact display bounds",
                threeD.TryApplyHeightDisplayOptions(defaults with { RangeMode = "robust" })
                && threeD.HeightDisplayOptions.RangeMode == "robust" && threeD.C3DHeightColorMinimumRaw == 4 && threeD.C3DHeightColorMaximumRaw == 997);
            Check("region without selected ROI is rejected without changing the range",
                !threeD.TryApplyHeightDisplayOptions(defaults with { RangeMode = "roi" }) && threeD.C3DHeightColorMinimumRaw == 4);
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, 800));
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Cancel));
            Check("Esc restores distribution mode as well as both bounds", threeD.HeightDisplayOptions.RangeMode == "robust" && threeD.C3DHeightColorMaximumRaw == 997);
            threeD.SetHeightDisplayContext("inspect", "source", "Input");
            threeD.TryApplyHeightDisplayOptions(defaults with { RangeMode = "manual" }, 20, 800);
            threeD.SetC3DHeightDistribution(second.HeightDistribution, second.ContentSha256, second.ReadHeightMapValues(), 100, 10);
            Check("inspection pin retains bounds outside the next file's data", threeD.C3DHeightColorMinimumRaw == 20 && threeD.C3DHeightColorMaximumRaw == 800);
            threeD.SetHeightDisplayContext("inspect", "output.filtered", "Output");
            Check("another map has its own automatic range", threeD.C3DHeightColorRangeAuto && threeD.C3DHeightColorMinimumRaw == 1001);
            threeD.TryApplyHeightDisplayOptions(defaults with { RangeMode = "manual" }, 1100, 1500);
            threeD.SetHeightDisplayContext("inspect", "source", "Input");
            Check("returning to the original map restores that map only", threeD.C3DHeightColorMinimumRaw == 20 && threeD.C3DHeightColorMaximumRaw == 800);
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            threeD.SetHeightDisplayContext("inspect", "output.filtered", "Output");
            threeD.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "1900"));
            Check("changing map identity rejects a numeric editor even when source bytes match", threeD.C3DHeightLegendError.Length > 0 && threeD.C3DHeightColorMaximumRaw == 1500);
            threeD.SetHeightDisplayContext("inspect", "source", "Input");
            threeD.TryApplyHeightDisplayOptions(threeD.HeightDisplayOptions with { KeepInspectionRange = false });
            Check("turning pin off restores the current file's automatic bounds", threeD.C3DHeightColorRangeAuto && threeD.C3DHeightColorMinimumRaw == 1001 && threeD.C3DHeightColorMaximumRaw == 2000);
            Check("display policy leaves execution and results untouched", ReferenceEquals(preview, threeD.PreviewToolResult) && ReferenceEquals(results, threeD.ResultEntities));
            var constantPath = Path.Combine(directory, "legend-constant.C3D");
            Write(constantPath, Enumerable.Repeat(10d, 1000));
            var constantGrid = C3DHeightGrid.Load(constantPath);
            var constantView = new MainWindowViewModel { C3DSampleVisible = true };
            constantView.SetC3DDisplayCapabilities(true);
            constantView.SetHeightDisplayContext("inspect", "source", "Constant input");
            constantView.SetC3DHeightDistribution(constantGrid.HeightDistribution, constantGrid.ContentSha256, constantGrid.ReadHeightMapValues(), 100, 10);
            constantView.TryApplyHeightDisplayOptions(defaults with { RangeMode = "manual" }, 0, 20);
            constantView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, 18));
            Check("pinned range remains editable on a constant-height file", !constantView.C3DHeightColorRangeAuto
                && constantView.C3DHeightColorMinimumRaw == 0 && constantView.C3DHeightColorMaximumRaw == 18);

            using var image = new HeightImageViewerViewModel(ThreeDLocalization.Shared, new SharedHeightCursorSession());
            image.SetHeightDisplayContext("validate", "source", "Input");
            image.EnsureSourceAsync(firstPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
            Check("2D distribution uses the same full-resolution quantiles", image.TryApplyHeightDisplayOptions(defaults with { RangeMode = "robust" })
                && image.HeightLegendMinimum == 4 && image.HeightLegendMaximum == 997);
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "NaN"));
            Check("invalid direct input preserves the current range", image.HasRangeError && image.HeightLegendMaximum == 997);
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "800"));
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Commit));
            Check("valid direct input recovers after invalid input", !image.HasRangeError && image.HeightLegendMaximum == 800);
            image.EnsureSourceAsync(secondPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
            Check("2D validation pin survives file replacement", image.HeightLegendMinimum == 4 && image.HeightLegendMaximum == 800);
            image.TryApplyHeightDisplayOptions(image.HeightDisplayOptions with { KeepValidationRange = false });
            Check("2D unpin returns to the current full range", image.IsAutoRange && image.HeightLegendMinimum == 1001 && image.HeightLegendMaximum == 2000);
            image.TryApplyHeightDisplayOptions(image.HeightDisplayOptions with { ColorStyle = "bands", Palette = "ColorVisionAssist" });
            var pixels = image.DisplayPixelSha256;
            image.TryApplyHeightDisplayOptions(image.HeightDisplayOptions with { ColorStyle = "continuous" });
            Check("banded palette is applied to actual BGRA bytes", pixels != image.DisplayPixelSha256);
            Check("linked state preserves auto-distribution semantics", image.TryApplyLinkedDisplayState(defaults with { RangeMode = "robust" }, 1004, 1997)
                && image.IsAutoRange && image.HeightDisplayOptions.RangeMode == "robust" && image.HeightLegendMinimum == 1004);
            var delta = C3DSignedHeightDeltaArtifact.Create("step.delta", "output.delta", "source", first.ContentSha256, first.ContentSha256, 4008,
                "background", second.ContentSha256, second.ContentSha256, 4008, "raw-height", "frame.c3d-grid-index", 5, 1, 0, 0,
                [-8, 0, 2, 4, double.NaN], 5, 4, 4, 1, 1, 2, 1, "Isolated display-policy verification");
            image.SetHeightDisplayContext("teach", "output.delta", "Delta");
            Check("signed delta defaults to a zero-centred range", image.ShowSignedDeltaArtifact(delta) && image.HeightLegendMinimum == -8 && image.HeightLegendMaximum == 8);
            Check("signed histogram keeps finite zero and excludes missing values", image.HeightLegendHistogram.Sum() == 4);
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, 5));
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Lower, -3));
            Check("either deviation handle moves its opposite symmetrically", image.HeightLegendMinimum == -3 && image.HeightLegendMaximum == 3);
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Scale, 0.5, 1));
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Shift, 2));
            Check("deviation wheel and body keep the zero centre", image.HeightLegendMinimum == -1.5 && image.HeightLegendMaximum == 1.5);
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "-2"));
            Check("negative deviation upper bound is rejected visibly", image.HasRangeError && image.HeightLegendMaximum == 1.5);
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "2"));
            image.HeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Commit));
            Check("deviation numeric input recovers with a positive bound", !image.HasRangeError && image.HeightLegendMinimum == -2 && image.HeightLegendMaximum == 2);

            var signedFrame = C3DHeightImageFrame.Create(delta);
            var signedGrid = C3DHeightGrid.CreateSignedDelta(signedFrame, 1000);
            Check("signed 3D projection keeps zero valid in SDK histogram, vertices, cell and profile reads", signedGrid.IsSignedDelta
                && signedGrid.HeightDistribution.ValidSampleCount == 4 && signedGrid.HeightDistribution.MissingSampleCount == 1
                && signedGrid.HeightDistribution.Bins.Sum() == 4 && signedGrid.Points.Length == 4
                && signedGrid.ReadPoint(0, 1).RawValue == 0 && signedGrid.ReadPoint(0, 1).HeightScalar == .5 && signedGrid.ReadRowRange(0, 0, 4).Length == 4
                && signedGrid.ReadLineProfile(0, 0, 0, 4).Length == 4 && signedGrid.ReadHeightMapValues()[1] == 0
                && double.IsNaN(signedGrid.ReadHeightMapValues()[4]));
            var redensity = signedGrid.WithMaxRenderedPoints(2).WithMaxRenderedPoints(1000);
            Check("signed 3D density changes retain artifact identity and finite zero", redensity.IsSignedDelta
                && redensity.SourcePath == signedFrame.SourcePath && redensity.ContentSha256 == delta.ContentSha256
                && redensity.Points.Length == 4 && redensity.ReadPoint(0, 1).RawValue == 0);
            var signedView = new MainWindowViewModel { C3DSampleVisible = true };
            signedView.SetC3DDisplayCapabilities(true);
            signedView.SetHeightDisplayContext("teach", delta.OutputEntityId, "Signed delta");
            signedView.SetC3DHeightDistribution(signedGrid.HeightDistribution, signedGrid.ContentSha256,
                signedGrid.ReadHeightMapValues(), signedGrid.Width, signedGrid.Height, signedGrid.IsSignedDelta);
            Check("3D signed auto range and visible legend are symmetric with a zero midpoint", signedView.C3DHeightDistributionVisible
                && signedView.C3DHeightColorMinimumRaw == -8 && signedView.C3DHeightColorMaximumRaw == 8
                && double.Parse(signedView.C3DHeightColorTickLabels[2], System.Globalization.CultureInfo.CurrentCulture) == 0);
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, 5));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Lower, -3));
            Check("3D deviation handles both move symmetrically while drag defers the renderer", signedView.C3DHeightColorMinimumRaw == -3
                && signedView.C3DHeightColorMaximumRaw == 3 && signedView.HeightColorRenderMinimum == -8 && signedView.HeightColorRenderMaximum == 8);
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Cancel));
            Check("3D signed Escape restores auto mode and both render bounds", signedView.HeightDisplayOptions.RangeMode == "auto"
                && !signedView.HeightDisplayOptions.IsRangeDragActive && signedView.HeightColorRenderMinimum == -8 && signedView.HeightColorRenderMaximum == 8);
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, 3));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Scale, .5, 1));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Shift, 2));
            Check("3D signed scale ignores cursor pivot and body drag cannot shift zero", signedView.C3DHeightColorMinimumRaw == -1.5
                && signedView.C3DHeightColorMaximumRaw == 1.5);
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Upper, -2));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Lower, 2));
            Check("3D deviation handles cannot cross zero and flip the active interval", signedView.C3DHeightColorMinimumRaw == -1.5
                && signedView.C3DHeightColorMaximumRaw == 1.5);
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "-2"));
            Check("3D negative deviation upper input is visibly rejected", signedView.C3DHeightLegendError.Length > 0 && signedView.C3DHeightColorMaximumRaw == 1.5);
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "2"));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Commit));
            Check("3D valid deviation input recovers and releases the latest symmetric render range", signedView.C3DHeightLegendError.Length == 0
                && signedView.HeightColorRenderMinimum == -2 && signedView.HeightColorRenderMaximum == 2);
            signedView.TryApplyHeightDisplayOptions(signedView.HeightDisplayOptions with { RangeMode = "robust" });
            Check("3D signed distribution remains symmetric", signedView.HeightDisplayOptions.RangeMode == "robust"
                && signedView.C3DHeightColorMinimumRaw == -signedView.C3DHeightColorMaximumRaw);
            signedView.ResetC3DHeightColorRange();
            Check("3D signed Auto restores full amplitude and matching 2D zero colour", signedView.C3DHeightColorMinimumRaw == -8
                && signedView.C3DHeightColorMaximumRaw == 8 && signedView.GetC3DHeightDisplayColor(0, 0, 1)
                    == C3DPointMapPalette.DisplayColor(0, -8, 8, "Height", defaults));
            signedView.SetHeightDisplayContext("teach", "source", "Input");
            signedView.SetC3DHeightDistribution(first.HeightDistribution, first.ContentSha256, first.ReadHeightMapValues(), 100, 10);
            signedView.ResetC3DHeightColorRange();
            Check("ordinary source after signed map restores ordinary bounds and zero-missing semantics", signedView.C3DHeightColorMinimumRaw == 1
                && signedView.C3DHeightColorMaximumRaw == 1000 && !first.IsSignedDelta);
            var zeroDelta = C3DSignedHeightDeltaArtifact.Create("step.zero", "output.zero", "source", first.ContentSha256, first.ContentSha256, 4008,
                "background", second.ContentSha256, second.ContentSha256, 4008, "raw-height", "frame.c3d-grid-index", 2, 2, 0, 0,
                [0, 0, 0, double.NaN], 4, 3, 3, 1, 3, 0, 0, "Zero-only display boundary");
            var zeroGrid = C3DHeightGrid.CreateSignedDelta(C3DHeightImageFrame.Create(zeroDelta));
            signedView.SetHeightDisplayContext("teach", zeroDelta.OutputEntityId, "Zero-only delta");
            signedView.SetC3DHeightDistribution(zeroGrid.HeightDistribution, zeroGrid.ContentSha256, zeroGrid.ReadHeightMapValues(), 2, 2, true);
            signedView.ResetC3DHeightColorRange();
            Check("zero-only signed 3D keeps all zero vertices and the defined plus-minus-one display range", zeroGrid.Points.Length == 3
                && signedView.C3DHeightColorMinimumRaw == -1 && signedView.C3DHeightColorMaximumRaw == 1);

            var preciseDelta = C3DSignedHeightDeltaArtifact.Create("step.precise", "output.precise", "source", first.ContentSha256, first.ContentSha256, 4008,
                "background", second.ContentSha256, second.ContentSha256, 4008, "raw-height", "frame.c3d-grid-index", 2, 2, 0, 0,
                [-8.0000001, 0, 4, double.NaN], 4, 3, 3, 1, 1, 1, 1, "Signed double display bounds");
            var preciseGrid = C3DHeightGrid.CreateSignedDelta(C3DHeightImageFrame.Create(preciseDelta));
            signedView.SetHeightDisplayContext("teach", preciseDelta.OutputEntityId, "Precise delta");
            signedView.SetC3DHeightDistribution(preciseGrid.HeightDistribution, preciseGrid.ContentSha256, preciseGrid.ReadHeightMapValues(), 2, 2, true);
            signedView.ResetC3DHeightColorRange();
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Begin));
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.InputUpper, Text: "8.00000005"));
            Check("signed manual input and legend bounds use authoritative double values rather than rounded float geometry", signedView.C3DHeightLegendError.Length == 0
                && signedView.C3DHeightColorMinimumRaw == -8.00000005 && signedView.C3DHeightColorMaximumRaw == 8.00000005
                && signedView.C3DHeightDistributionMinimumRaw == -8.0000001 && signedView.C3DHeightDistributionMaximumRaw == 4);
            signedView.C3DHeightLegendEditCommand.Execute(new HeightLegendEditRequest(HeightLegendEditKind.Commit));

            using (var workbench = new ToolWorkbenchViewModel(Path.Combine(directory, "link-recent.json")))
            using (var links = new ViewerWorkspaceLinkCoordinator())
            {
                var sourceCandidate = new ViewerWorkspaceCandidateItem("source", "Input", ViewerWorkspaceCandidateKind.HeightImage, firstPath, "C3D", "Ready", true);
                workbench.MainViewerPane.UpdateSource(sourceCandidate, ViewerPaneKind.Height, true);
                var central = workbench.HeightImageViewer;
                var paneImage = workbench.MainViewerPane.HeightImage;
                central.EnsureSourceAsync(firstPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
                paneImage.EnsureSourceAsync(firstPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
                links.SetWorkbench(workbench);
                central.TryApplyHeightDisplayOptions(defaults with { RangeMode = "manual", HighlightOutside = true }, 300, 700);
                Check("existing pane instances receive same-map range and colour options", paneImage.HeightLegendMinimum == 300 && paneImage.HeightLegendMaximum == 700 && paneImage.HeightDisplayOptions.HighlightOutside);
                paneImage.TryApplyHeightDisplayOptions(paneImage.HeightDisplayOptions with { IsLinked = false });
                paneImage.TryApplyManualRange(400, 500);
                Check("unlink preserves each window's independent range", paneImage.HeightLegendMinimum == 400 && central.HeightLegendMinimum == 300 && !central.HeightDisplayOptions.IsLinked);
                paneImage.TryApplyHeightDisplayOptions(paneImage.HeightDisplayOptions with { IsLinked = true });
                Check("relink uses the edited window's current bounds", central.HeightLegendMinimum == 400 && central.HeightLegendMaximum == 500);
                paneImage.SetHeightDisplayContext("teach", "output.other", "Output");
                paneImage.TryApplyManualRange(600, 800);
                Check("same bytes under a different map identity remain independent", central.HeightLegendMinimum == 400 && central.HeightLegendMaximum == 500);
                paneImage.SetHeightDisplayContext("teach", "source", "Input");
                paneImage.EnsureSourceAsync(secondPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
                paneImage.TryApplyManualRange(1200, 1500);
                Check("different file hashes remain independent", central.HeightLegendMinimum == 400 && paneImage.HeightLegendMinimum == 1200);
                links.Dispose();
                central.TryApplyManualRange(200, 600);
                Check("coordinator disposal detaches pane subscriptions", paneImage.HeightLegendMinimum == 1200);
            }

            var profilePath = Path.Combine(directory, "legend-layout.json");
            var store = new StudioLayoutProfileStore(profilePath);
            threeD.SetHeightDisplayContext("teach", "source", "Input");
            threeD.TryApplyHeightDisplayOptions(defaults with { RangeMode = "robust" });
            threeD.SetHeightDisplayContext("teach", "output.other", "Output");
            threeD.ResetC3DHeightColorRange();
            var mapModes = threeD.CaptureHeightDisplayModes();
            store.Save(StudioLayoutProfile.Default with
            {
                HeightDisplay = bands with { RangeMode = "manual", IsLinked = false, Palette = "Thermal", KeepTeachingRange = true },
                HeightDisplayModes = mapModes
            });
            var reopened = new StudioLayoutProfileStore(profilePath).Load();
            Check("save/reload/reopen retains preferences and discards manual numbers", reopened.Profile.HeightDisplay is
                { RangeMode: "auto", IsLinked: false, Palette: "Thermal", KeepTeachingRange: true, ColorStyle: "bands", BandCount: 10 }
                && !File.ReadAllText(profilePath).Contains("minimum", StringComparison.OrdinalIgnoreCase));
            Check("stored modes are independent by workspace and map", reopened.Profile.HeightDisplayModes?.GetValueOrDefault("teach|source") == "robust"
                && reopened.Profile.HeightDisplayModes?.ContainsKey("teach|output.other") == false);
            threeD.SetHeightDisplayContext("teach", "source", "Input");
            threeD.RestoreHeightDisplayModes(reopened.Profile.HeightDisplayModes!);
            Check("3D reopen restores the stored map mode using current input values", threeD.HeightDisplayOptions.RangeMode == "robust"
                && threeD.C3DHeightColorMinimumRaw == 1004 && threeD.C3DHeightColorMaximumRaw == 1997);
            using (var reopenedImage = new HeightImageViewerViewModel(ThreeDLocalization.Shared, new SharedHeightCursorSession()))
            {
                reopenedImage.RestoreHeightDisplayModes(reopened.Profile.HeightDisplayModes!);
                reopenedImage.EnsureSourceAsync(firstPath, "source", "raw-height", "frame.c3d-grid-index").GetAwaiter().GetResult();
                Check("2D preferences restore before input load without reusing old numeric limits", reopenedImage.HeightDisplayOptions.RangeMode == "robust"
                    && reopenedImage.HeightLegendMinimum == 4 && reopenedImage.HeightLegendMaximum == 997);
            }
            using (var workbench = new ToolWorkbenchViewModel(Path.Combine(directory, "profile-recent.json")))
            using (var links = new ViewerWorkspaceLinkCoordinator())
            {
                links.SetWorkbench(workbench);
                var panes = new[] { workbench.MainViewerPane, workbench.AuxiliaryViewerPane, workbench.SectionViewerPane, workbench.HeightViewerPane };
                for (var index = 0; index < panes.Length; index++)
                {
                    panes[index].InputDisplay.TryApplyHeightDisplayOptions(defaults with { IsLinked = false, Palette = "Thermal", BandCount = new[] { 5, 8, 16, 20 }[index], ColorStyle = "bands" });
                    panes[index].OutputDisplay.TryApplyHeightDisplayOptions(defaults with { IsLinked = false, Palette = "Grayscale", IsCollapsed = index % 2 == 0 });
                }
                panes[0].InputDisplay.RestoreHeightDisplayModes(new Dictionary<string, string> { ["teach|source"] = "robust" });
                var profiles = links.CaptureDisplayProfiles();
                Check("all four panes capture input and output display preferences independently", profiles.Count == 8
                    && profiles["main/input"].Options.BandCount == 5 && profiles["height/input"].Options.BandCount == 20
                    && profiles["section/output"].Options.IsCollapsed && !profiles["height/output"].Options.IsCollapsed);
                store.Save(StudioLayoutProfile.Default with { PaneDisplays = profiles });
                var saved = new StudioLayoutProfileStore(profilePath).Load().Profile.PaneDisplays!;
                links.RestoreDisplayProfiles(saved);
                Check("pane save/reload/reopen preserves unlink, palette, bands and collapse", panes.Select((pane, index) =>
                    pane.InputDisplay.HeightDisplayOptions is { IsLinked: false, Palette: "Thermal", ColorStyle: "bands" }
                    && pane.InputDisplay.HeightDisplayOptions.BandCount == new[] { 5, 8, 16, 20 }[index]
                    && pane.OutputDisplay.HeightDisplayOptions.Palette == "Grayscale"
                    && pane.OutputDisplay.HeightDisplayOptions.IsCollapsed == (index % 2 == 0)).All(value => value));
                Check("matching recipe restores map modes without storing numeric ranges", panes[0].InputDisplay.CaptureHeightDisplayModes().GetValueOrDefault("teach|source") == "robust"
                    && !File.ReadAllText(profilePath).Contains("minimum", StringComparison.OrdinalIgnoreCase));
                workbench.RecipeName = "Other recipe";
                Check("recipe switch drops old per-map modes while retaining presentation preferences", panes[0].InputDisplay.CaptureHeightDisplayModes().Count == 0
                    && panes[0].InputDisplay.HeightDisplayOptions.Palette == "Thermal" && !workbench.IsValidationSetRunning);
                links.RestoreDisplayProfiles(saved);
                Check("another recipe cannot restore the saved map modes", panes[0].InputDisplay.CaptureHeightDisplayModes().Count == 0);
                var malformed = saved.ToDictionary(entry => entry.Key, entry => entry.Value);
                malformed["foreign/input"] = saved["main/input"];
                malformed["auxiliary/output"] = new(invalidOptions(), new Dictionary<string, string> { ["unknown|map"] = "manual" }, string.Empty);
                store.Save(StudioLayoutProfile.Default with { PaneDisplays = malformed });
                var safePanes = store.Load().Profile.PaneDisplays!;
                Check("pane profile ignores unknown slots and sanitizes corrupt preferences", !safePanes.ContainsKey("foreign/input")
                    && safePanes["auxiliary/output"].Options.Palette == "Height" && safePanes["auxiliary/output"].MapModes.Count == 0);
                HeightDisplayOptions invalidOptions() => defaults with { Palette = "unknown", BandCount = -1 };
            }
            store.Save(StudioLayoutProfile.Default);
            Check("legacy profile without display fields opens with defaults", store.Load().Profile.HeightDisplay is null);
            var invalid = bands with { RangeMode = "wrong", BandCount = -1, Palette = "unknown", ColorStyle = "broken" };
            File.WriteAllText(profilePath, JsonSerializer.Serialize(StudioLayoutProfile.Default with { HeightDisplay = invalid }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
            Check("invalid saved preferences are reset and surfaced", store.Load() is { Status: StudioLayoutLoadStatus.RestoredWithFallback, Profile.HeightDisplay: { RangeMode: "auto", BandCount: 10, Palette: "Height", ColorStyle: "continuous" } });
        }
        catch (Exception exception) { Check("execution: " + exception, false); }
        summary = $"HeightLegendPolicy|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    private static void Write(string path, IEnumerable<double> values)
    {
        using var writer = new BinaryWriter(File.Create(path));
        writer.Write(100);
        writer.Write(10);
        foreach (var value in values) writer.Write((float)value);
    }
}

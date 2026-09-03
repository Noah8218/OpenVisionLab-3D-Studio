using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the view-only Height Image display-range and palette Smoke scenario.
/// MainWindow supplies explicit Workbench, Viewer, and Dispatcher inputs and
/// retains only command-line sequencing plus failure/shutdown policy.
/// </summary>
internal static class ShellHeightImageDisplayRangeSmoke
{
    public static async Task<bool> RunAsync(
        string? paletteText,
        double? minimum,
        double? maximum,
        string? reportPath,
        ToolWorkbenchViewModel workbench,
        OpenVisionThreeDViewerControl viewer,
        Dispatcher dispatcher)
    {
        var heightImage = workbench.HeightImageViewer;
        var source = workbench.Source;
        if (string.IsNullOrWhiteSpace(source.Path)
            || minimum is not { } requestedMinimum
            || maximum is not { } requestedMaximum
            || !Enum.TryParse<C3DHeightImagePalette>(
                paletteText,
                ignoreCase: true,
                out var requestedPalette)
            || !Enum.IsDefined(requestedPalette))
        {
            WriteFailure(
                reportPath,
                source,
                "The source or requested palette/range was unavailable.");
            return false;
        }

        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeLogCount = workbench.RunLog.Count;
        var beforePreviewRunning = workbench.IsSelectedStepPreviewRunning;
        var beforeOutput = workbench.CurrentMeasurementOutput;

        await heightImage.EnsureSourceAsync(
            source.Path,
            source.Id,
            source.Unit,
            source.FrameId);
        if (!heightImage.HasImage || heightImage.HasError)
        {
            WriteFailure(
                reportPath,
                source,
                heightImage.Error);
            return false;
        }

        var nativePixelSha256 = heightImage.Frame!.PixelSha256;
        heightImage.SelectedPalette = requestedPalette;
        var rangeApplied = heightImage.TryApplyManualRange(
            requestedMinimum,
            requestedMaximum);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

        var heightImageToThreeDPassed =
            !viewer.ViewModel.C3DHeightColorRangeAuto
            && viewer.ViewModel.C3DHeightColorMinimumRaw == requestedMinimum
            && viewer.ViewModel.C3DHeightColorMaximumRaw == requestedMaximum;

        var mismatchedSourcePath = Path.GetFullPath(Path.Combine(
            Environment.CurrentDirectory,
            "3D",
            "SyntheticValidation",
            "AffineInspectionPlateV1",
            "source-affine-inspection-plate-v1.C3D"));
        var mismatchedSourceIsolationPassed = false;
        if (File.Exists(mismatchedSourcePath))
        {
            await heightImage.EnsureSourceAsync(
                mismatchedSourcePath,
                "source.c3d.display-range-mismatch",
                source.Unit,
                source.FrameId);
            var mismatchedRangeApplied = heightImage.TryApplyManualRange(-10.0, 10.0);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            mismatchedSourceIsolationPassed = mismatchedRangeApplied
                && heightImage.Frame is { } mismatchedFrame
                && !string.Equals(
                    mismatchedFrame.SourceContentSha256,
                    viewer.ViewModel.C3DHeightDistributionSourceSha256,
                    StringComparison.OrdinalIgnoreCase)
                && viewer.ViewModel.C3DHeightColorMinimumRaw == requestedMinimum
                && viewer.ViewModel.C3DHeightColorMaximumRaw == requestedMaximum;

            await heightImage.EnsureSourceAsync(
                source.Path,
                source.Id,
                source.Unit,
                source.FrameId);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        }

        var nativeSpan = heightImage.Frame.Maximum - heightImage.Frame.Minimum;
        var reciprocalMinimum = heightImage.Frame.Minimum + nativeSpan * 0.25;
        var reciprocalMaximum = heightImage.Frame.Maximum - nativeSpan * 0.25;
        var reciprocalApplied = viewer.ViewModel.TryApplyLinkedC3DHeightColorRange(
            reciprocalMinimum,
            reciprocalMaximum);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var threeDToHeightImagePassed = reciprocalApplied
            && !heightImage.IsAutoRange
            && heightImage.DisplayFrame?.Minimum == reciprocalMinimum
            && heightImage.DisplayFrame?.Maximum == reciprocalMaximum;

        viewer.ViewModel.ResetC3DHeightColorRange();
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var autoRangePassed = viewer.ViewModel.C3DHeightColorRangeAuto
            && heightImage.IsAutoRange
            && heightImage.DisplayFrame?.Minimum == heightImage.Frame.Minimum
            && heightImage.DisplayFrame?.Maximum == heightImage.Frame.Maximum;

        rangeApplied = heightImage.TryApplyManualRange(
            requestedMinimum,
            requestedMaximum);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var finalLinkedRangePassed = heightImageToThreeDPassed
            && mismatchedSourceIsolationPassed
            && threeDToHeightImagePassed
            && autoRangePassed
            && !viewer.ViewModel.C3DHeightColorRangeAuto
            && viewer.ViewModel.C3DHeightColorMinimumRaw == requestedMinimum
            && viewer.ViewModel.C3DHeightColorMaximumRaw == requestedMaximum;

        var passed = rangeApplied
                     && finalLinkedRangePassed
                     && !heightImage.IsAutoRange
                     && heightImage.SelectedPalette == requestedPalette
                     && heightImage.DisplayFrame is
                     {
                         Minimum: var actualMinimum,
                         Maximum: var actualMaximum,
                         Palette: var actualPalette
                     }
                     && actualMinimum == requestedMinimum
                     && actualMaximum == requestedMaximum
                     && actualPalette == requestedPalette
                     && heightImage.DisplayPixelSha256 != nativePixelSha256
                     && workbench.IsDirty == beforeDirty
                     && workbench.PipelineSteps.Count == beforeStepCount
                     && workbench.Selections.Count == beforeSelectionCount
                     && workbench.RunLog.Count == beforeLogCount
                     && workbench.IsSelectedStepPreviewRunning == beforePreviewRunning
                     && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput);

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"HeightImageDisplayRangeSmoke|{(passed ? "Pass" : "Fail")}|viewOnly=true|recipeChanged=false|inspectionRun=false",
                    $"Source|path={source.Path}|entity={source.Id}|frame={source.FrameId}|unit={source.Unit}",
                    $"Native|width={heightImage.Frame.Width}|height={heightImage.Frame.Height}|min={heightImage.Frame.Minimum:R}|max={heightImage.Frame.Maximum:R}|pixelSha256={nativePixelSha256}|maskSha256={heightImage.Frame.InvalidCellMap.Sha256}",
                    $"Display|mode={heightImage.DisplayRangeMode}|palette={heightImage.SelectedPalette}|min={heightImage.DisplayFrame?.Minimum:R}|max={heightImage.DisplayFrame?.Maximum:R}|pixelSha256={heightImage.DisplayPixelSha256}",
                    $"LinkedRange|sourceMatch={string.Equals(viewer.ViewModel.C3DHeightDistributionSourceSha256, heightImage.Frame.SourceContentSha256, StringComparison.OrdinalIgnoreCase)}|heightImageToThreeD={heightImageToThreeDPassed}|mismatchedSourceIsolated={mismatchedSourceIsolationPassed}|threeDToHeightImage={threeDToHeightImagePassed}|auto={autoRangePassed}|finalShared={finalLinkedRangePassed}|threeDMin={viewer.ViewModel.C3DHeightColorMinimumRaw:R}|threeDMax={viewer.ViewModel.C3DHeightColorMaximumRaw:R}",
                    $"Boundary|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|logs={beforeLogCount}->{workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{workbench.IsSelectedStepPreviewRunning}|outputSame={ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)}",
                    $"Error|{heightImage.RangeError}"
                ]);
        }

        return passed;
    }

    private static void WriteFailure(
        string? reportPath,
        ToolWorkbenchSourceItem source,
        string? failure)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
        {
            return;
        }

        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            [
                "HeightImageDisplayRangeSmoke|Fail|viewOnly=true|recipeChanged=false|inspectionRun=false",
                $"Source|path={source.Path}|entity={source.Id}|frame={source.FrameId}|unit={source.Unit}",
                $"Error|{failure}"
            ]);
    }
}

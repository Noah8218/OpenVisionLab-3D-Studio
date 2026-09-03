using System.IO;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the view-only shared Height Image/3D Viewer cursor Smoke scenario.
/// MainWindow supplies explicit Workbench, Viewer, and Dispatcher inputs and
/// retains only command-line sequencing plus failure/shutdown policy.
/// </summary>
internal static class ShellSharedHeightHoverSmoke
{

    public static async Task<bool> RunAsync(
        ToolWorkbenchViewModel workbench,
        OpenVisionThreeDViewerControl viewer,
        Dispatcher dispatcher,
        int? row,
        int? column,
        string? reportPath)
    {
        var heightImage = workbench.HeightImageViewer;
        var source = workbench.Source;
        if (string.IsNullOrWhiteSpace(source.Path)
            || row is not { } requestedRow
            || column is not { } requestedColumn)
        {
            return false;
        }

        var beforeDirty = workbench.IsDirty;
        var beforeStepCount = workbench.PipelineSteps.Count;
        var beforeSelectionCount = workbench.Selections.Count;
        var beforeLogCount = workbench.RunLog.Count;
        var beforePreviewRunning = workbench.IsSelectedStepPreviewRunning;
        var beforeOutput = workbench.CurrentMeasurementOutput;
        var beforeCamera = (
            viewer.ViewModel.YawDegrees,
            viewer.ViewModel.PitchDegrees,
            viewer.ViewModel.CameraDistance,
            viewer.ViewModel.CameraTargetX,
            viewer.ViewModel.CameraTargetY,
            viewer.ViewModel.CameraTargetZ);

        await heightImage.EnsureSourceAsync(
            source.Path,
            source.Id,
            source.Unit,
            source.FrameId);
        if (heightImage.Frame is not { } frame
            || !frame.TryGetCell(
                requestedColumn,
                requestedRow,
                out var requestedCell)
            || !requestedCell.IsValid)
        {
            return false;
        }

        heightImage.UpdateHover(requestedColumn, requestedRow);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var fromHeightImagePassed =
            workbench.SharedHeightCursor.Cursor is
            {
                Origin: SharedHeightCursorOrigin.HeightImage,
                Row: var heightRow,
                Column: var heightColumn,
                IsValid: true
            }
            && heightRow == requestedRow
            && heightColumn == requestedColumn
            && viewer.LinkedHeightCursor is
            {
                Origin: C3DGridCursorOrigin.HeightImage,
                Row: var viewerHeightRow,
                Column: var viewerHeightColumn,
                IsValid: true
            }
            && viewerHeightRow == requestedRow
            && viewerHeightColumn == requestedColumn;

        var viewerPublished = viewer.TryPublishC3DGridHoverForSmoke(
            requestedRow,
            requestedColumn);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var fromThreeDPassed =
            viewerPublished
            && workbench.SharedHeightCursor.Cursor is
            {
                Origin: SharedHeightCursorOrigin.ThreeDViewer,
                Row: var threeDRow,
                Column: var threeDColumn,
                IsValid: true
            }
            && threeDRow == requestedRow
            && threeDColumn == requestedColumn
            && heightImage.HasLinkedCursor
            && heightImage.LinkedCursorRow == requestedRow
            && heightImage.LinkedCursorColumn == requestedColumn
            && heightImage.HoverSummary.Contains("3D", StringComparison.Ordinal);

        var missingCell = FindFirstMissingCell(frame);
        var missingPassed = false;
        if (missingCell is { } missing)
        {
            heightImage.UpdateHover(missing.Column, missing.Row);
            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            missingPassed =
                workbench.SharedHeightCursor.Cursor is
                {
                    Origin: SharedHeightCursorOrigin.HeightImage,
                    IsValid: false,
                    Row: var missingRow,
                    Column: var missingColumn
                }
                && missingRow == missing.Row
                && missingColumn == missing.Column
                && heightImage.HasLinkedCursor
                && !heightImage.LinkedCursorIsValid
                && heightImage.HoverSummary.Contains(
                    workbench.Localization.HeightImageMissingValue,
                    StringComparison.Ordinal);
        }

        viewerPublished = viewer.TryPublishC3DGridHoverForSmoke(
            requestedRow,
            requestedColumn);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(120);

        var afterCamera = (
            viewer.ViewModel.YawDegrees,
            viewer.ViewModel.PitchDegrees,
            viewer.ViewModel.CameraDistance,
            viewer.ViewModel.CameraTargetX,
            viewer.ViewModel.CameraTargetY,
            viewer.ViewModel.CameraTargetZ);
        var boundaryPassed =
            workbench.IsDirty == beforeDirty
            && workbench.PipelineSteps.Count == beforeStepCount
            && workbench.Selections.Count == beforeSelectionCount
            && workbench.RunLog.Count == beforeLogCount
            && workbench.IsSelectedStepPreviewRunning == beforePreviewRunning
            && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)
            && beforeCamera == afterCamera;
        var passed =
            fromHeightImagePassed
            && fromThreeDPassed
            && missingPassed
            && viewerPublished
            && boundaryPassed;

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullReportPath = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullReportPath)
                ?? Environment.CurrentDirectory);
            File.WriteAllLines(
                fullReportPath,
                [
                    $"SharedHeightHoverSmoke|{(passed ? "Pass" : "Fail")}|viewOnly=true|recipeChanged=false|inspectionRun=false",
                    $"Source|path={source.Path}|entity={source.Id}|frame={source.FrameId}|unit={source.Unit}|sha256={frame.SourceContentSha256}",
                    $"FromHeightImage|pass={fromHeightImagePassed}|row={requestedRow}|column={requestedColumn}|rawHeight={requestedCell.RawHeight:R}|viewerMarker={viewer.LinkedHeightCursor is not null}",
                    $"FromThreeD|pass={fromThreeDPassed}|row={heightImage.LinkedCursorRow}|column={heightImage.LinkedCursorColumn}|summary={heightImage.HoverSummary}",
                    $"Missing|pass={missingPassed}|row={missingCell?.Row}|column={missingCell?.Column}|state={workbench.Localization.HeightImageMissingValue}",
                    $"Boundary|pass={boundaryPassed}|dirty={beforeDirty}->{workbench.IsDirty}|steps={beforeStepCount}->{workbench.PipelineSteps.Count}|selections={beforeSelectionCount}->{workbench.Selections.Count}|logs={beforeLogCount}->{workbench.RunLog.Count}|previewRunning={beforePreviewRunning}->{workbench.IsSelectedStepPreviewRunning}|outputSame={ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)}|cameraSame={beforeCamera == afterCamera}"
                ]);
        }

        return passed;
    }

    private static (int Row, int Column)? FindFirstMissingCell(
        C3DHeightImageFrame frame)
    {
        var packedBits = frame.InvalidCellMap.PackedBits.Span;
        for (var byteIndex = 0; byteIndex < packedBits.Length; byteIndex++)
        {
            var value = packedBits[byteIndex];
            if (value == 0)
            {
                continue;
            }

            for (var bit = 0; bit < 8; bit++)
            {
                if ((value & (1 << bit)) == 0)
                {
                    continue;
                }

                var index = checked(byteIndex * 8 + bit);
                if (index >= frame.Width * frame.Height)
                {
                    return null;
                }

                return (index / frame.Width, index % frame.Width);
            }
        }

        return null;
    }
}

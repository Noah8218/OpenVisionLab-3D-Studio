using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the view-only shared Height Image/3D Viewer cursor Smoke scenario.
/// MainWindow supplies explicit Workbench, Viewer, and Dispatcher inputs and
/// retains only command-line sequencing plus failure/shutdown policy. Camera
/// invariance is observed through the public Viewer Host snapshot operation.
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
        var beforeCamera = viewer.CaptureCameraState();
        var viewport = (FrameworkElement)viewer.FindName("Viewport");
        var footer = (Border)viewer.FindName("ViewerBottomStatus");
        var viewerCoordinates = ShellSmokeArtifacts.FindVisualDescendants<TextBlock>(viewer)
            .Single(candidate => System.Windows.Automation.AutomationProperties.GetAutomationId(candidate) == "ViewerCoordinateStatus");
        var viewportOwner = Window.GetWindow(viewer) as System.Windows.Media.Visual ?? viewer;
        var beforeViewport = viewport.TransformToAncestor(viewportOwner).TransformBounds(new Rect(viewport.RenderSize));

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

        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var beforeViewerCoordinates = viewerCoordinates.Text;
        var beforePickCoordinate = viewer.HostState.Selection.PickCoordinate;
        var coordinateConverter = (System.Windows.Data.IValueConverter)viewer.Resources["ViewerRuntimeTextConverter"];
        var expectedFallbackCoordinate = coordinateConverter.Convert(beforePickCoordinate, typeof(string), null!, System.Globalization.CultureInfo.InvariantCulture).ToString()!;
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
        var coordinatePassed = heightImage.CursorCoordinateSummary.Contains($"X {requestedColumn:N0}", StringComparison.Ordinal)
            && heightImage.CursorCoordinateSummary.Contains($"Z {requestedRow:N0}", StringComparison.Ordinal)
            && heightImage.CursorCoordinateSummary.Contains(requestedCell.RawHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        var viewerCoordinatePassed = viewerCoordinates.Text.Contains($"X {requestedColumn:N0}", StringComparison.Ordinal)
            && viewerCoordinates.Text.Contains($"Z {requestedRow:N0}", StringComparison.Ordinal)
            && viewerCoordinates.Text.Contains(requestedCell.RawHeight.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);

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
                && heightImage.CursorCoordinateSummary.Contains("Y —", StringComparison.Ordinal)
                && viewerCoordinates.Text.Contains("Y —", StringComparison.Ordinal)
                && heightImage.HoverSummary.Contains(
                    workbench.Localization.HeightImageMissingValue,
                    StringComparison.Ordinal);
        }

        viewerPublished = viewer.TryPublishC3DGridHoverForSmoke(
            requestedRow,
            requestedColumn);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        workbench.SharedHeightCursor.Clear();
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var clearPassed = !heightImage.HasLinkedCursor
            && heightImage.CursorCoordinateSummary == "X — · Y — · Z —"
            && viewer.LinkedHeightCursor is null
            && viewerCoordinates.Text.EndsWith(expectedFallbackCoordinate, StringComparison.Ordinal)
            && viewport.TransformToAncestor(viewportOwner).TransformBounds(new Rect(viewport.RenderSize)) == beforeViewport;
        var clearedViewerCoordinates = viewerCoordinates.Text;
        var clearedViewerCursor = viewer.LinkedHeightCursor;
        workbench.SharedHeightCursor.Update(SharedHeightCursorOrigin.HeightImage, "other-source", requestedRow, requestedColumn, requestedCell.RawHeight, true);
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        var staleSourcePassed = !heightImage.HasLinkedCursor
            && heightImage.CursorCoordinateSummary == "X — · Y — · Z —"
            && viewer.LinkedHeightCursor is null
            && viewerCoordinates.Text.EndsWith(expectedFallbackCoordinate, StringComparison.Ordinal);
        var staleViewerCoordinates = viewerCoordinates.Text;
        viewerPublished = viewer.TryPublishC3DGridHoverForSmoke(requestedRow, requestedColumn) && viewerPublished;
        await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
        await Task.Delay(120);

        var workspace = ShellSmokeArtifacts.FindVisualDescendants<ViewerWorkspaceView>(viewportOwner)
            .Single(candidate => ReferenceEquals(candidate.MainViewerContent, viewer));
        var workspaceScroll = (ScrollViewer)workspace.FindName("ViewerWorkspaceScroll");
        var heightView = ShellSmokeArtifacts.FindVisualDescendants<HeightImageViewerView>(workspace)
            .Single(candidate => ReferenceEquals(candidate.DataContext, heightImage));
        var heightScroll = (ScrollViewer)heightView.FindName("ImageScrollViewer");
        var heightFooter = (Border)heightView.FindName("HeightImageCursorStatus");
        var workspaceBounds = workspace.TransformToAncestor(viewportOwner).TransformBounds(new Rect(workspace.RenderSize));
        heightImage.ActualPixelsCommand.Execute(null);
        await dispatcher.InvokeAsync(() => workspace.UpdateLayout(), DispatcherPriority.ContextIdle);
        var pixelExtentWidth = heightScroll.ExtentWidth;
        var pixelViewportWidth = heightScroll.ViewportWidth;
        var actualPixelsLayoutPassed = pixelExtentWidth == frame.Width
            && pixelViewportWidth < frame.Width
            && workspaceScroll.ScrollableHeight <= 1
            && viewport.TransformToAncestor(viewportOwner).TransformBounds(new Rect(viewport.RenderSize)) == beforeViewport
            && footer.TransformToAncestor(viewportOwner).TransformBounds(new Rect(footer.RenderSize)).Bottom <= workspaceBounds.Bottom + 1
            && heightFooter.TransformToAncestor(viewportOwner).TransformBounds(new Rect(heightFooter.RenderSize)).Bottom <= workspaceBounds.Bottom + 1;
        heightImage.FitCommand.Execute(null);
        await dispatcher.InvokeAsync(() => workspace.UpdateLayout(), DispatcherPriority.ContextIdle);
        var fitRecoveryPassed = workspaceScroll.ScrollableHeight <= 1
            && heightScroll.ExtentWidth <= heightScroll.ViewportWidth + 1
            && heightScroll.ExtentHeight <= heightScroll.ViewportHeight + 1
            && viewport.TransformToAncestor(viewportOwner).TransformBounds(new Rect(viewport.RenderSize)) == beforeViewport;

        var afterCamera = viewer.CaptureCameraState();
        var afterViewport = viewport.TransformToAncestor(viewportOwner).TransformBounds(new Rect(viewport.RenderSize));
        var layoutPassed = beforeViewport == afterViewport && footer.ActualHeight == 52;
        var boundaryPassed =
            workbench.IsDirty == beforeDirty
            && workbench.PipelineSteps.Count == beforeStepCount
            && workbench.Selections.Count == beforeSelectionCount
            && workbench.RunLog.Count == beforeLogCount
            && workbench.IsSelectedStepPreviewRunning == beforePreviewRunning
            && ReferenceEquals(workbench.CurrentMeasurementOutput, beforeOutput)
            && beforeCamera == afterCamera;
        boundaryPassed &= viewer.HostState.Selection.PickCoordinate == beforePickCoordinate;
        var passed =
            fromHeightImagePassed
            && fromThreeDPassed
            && missingPassed
            && viewerPublished
            && coordinatePassed
            && viewerCoordinatePassed
            && layoutPassed
            && clearPassed
            && staleSourcePassed
            && actualPixelsLayoutPassed
            && fitRecoveryPassed
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
                    $"CursorLayout|pass={layoutPassed}|before={beforeViewport}|after={afterViewport}|footerHeight={footer.ActualHeight}|coordinates={heightImage.CursorCoordinateSummary}|coordinatePass={coordinatePassed}|viewerCoordinates={viewerCoordinates.Text}|viewerCoordinatePass={viewerCoordinatePassed}",
                    $"CursorRecovery|clearPass={clearPassed}|staleSourcePass={staleSourcePassed}|restored={viewerPublished}|before={beforeViewerCoordinates}|expectedFallback={expectedFallbackCoordinate}|cleared={clearedViewerCoordinates}|clearedCursor={clearedViewerCursor}|stale={staleViewerCoordinates}|pickPreserved={viewer.HostState.Selection.PickCoordinate == beforePickCoordinate}",
                    $"ImageZoomLayout|actualPixelsPass={actualPixelsLayoutPassed}|pixelExtentWidth={pixelExtentWidth}|pixelViewportWidth={pixelViewportWidth}|fitRecoveryPass={fitRecoveryPassed}|outerScrollHeight={workspaceScroll.ScrollableHeight}|viewport={afterViewport}",
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

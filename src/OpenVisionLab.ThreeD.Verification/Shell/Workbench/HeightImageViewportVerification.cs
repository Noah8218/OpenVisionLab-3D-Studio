using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class HeightImageViewportVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        reportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    await VerifyOnStaAsync(reportPath, checks);
                }
                catch (Exception exception)
                {
                    checks.Add(("Execution", false, exception.ToString()));
                }
                finally
                {
                    dispatcher.InvokeShutdown();
                }
            }));
            Dispatcher.Run();
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        var passed = checks.Count > 0 && checks.All(check => check.Passed);
        summary = $"HeightImageViewport|pass={passed}|checks={checks.Count(check => check.Passed)}/{checks.Count}";
        File.WriteAllLines(reportPath, checks.Select(check => $"{(check.Passed ? "PASS" : "FAIL")}|{check.Name}|{check.Detail}").Append(summary));
        return passed;
    }

    private static async Task VerifyOnStaAsync(string reportPath, List<(string Name, bool Passed, string Detail)> checks)
    {
        var application = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        application.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
        application.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
        await VerifyLinkedCursorOverlayAsync(reportPath, checks);
        using var model = new HeightImageViewerViewModel(ThreeDLocalization.Shared, new SharedHeightCursorSession());
        var view = new HeightImageViewerView { DataContext = model };
        var window = new Window { Title = "Height Image viewport verification", Width = 900, Height = 700, Content = view };
        ShellWindowNativeInterop.TryGetLeftmostWorkAreaOrigin(out var left, out var top);
        window.Left = left;
        window.Top = top;
        var sourcePath = Path.ChangeExtension(reportPath, ".c3d");
        File.WriteAllBytes(sourcePath, [1]);
        var values = Enumerable.Range(0, 640 * 480).Select(index => (double)(index % 640)).ToArray();
        values[0] = double.NaN;
        var image = (Image)view.FindName("HeightImage");
        var scroll = (ScrollViewer)view.FindName("ImageScrollViewer");
        var overlay = (Canvas)view.FindName("RoiOverlay");

        async Task Settle()
        {
            window.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
        }

        async Task Load(string id)
        {
            await model.EnsureSourceAsync(sourcePath, id, "raw-height", "frame.c3d-grid-index",
                _ => Task.FromResult(C3DHeightFieldSnapshot.CreateForVerification(id, 640, 480, values)));
            await Settle();
        }

        async Task ZoomAndPan()
        {
            model.ActualPixelsCommand.Execute(null);
            model.ZoomInCommand.Execute(null);
            model.ZoomInCommand.Execute(null);
            await Settle();
            scroll.ScrollToHorizontalOffset(120);
            scroll.ScrollToVerticalOffset(80);
            await Settle();
        }

        (double Zoom, double X, double Y) Position() => (model.ZoomPercent, scroll.HorizontalOffset, scroll.VerticalOffset);

        async Task Preserve(string name, Action change, bool redraw)
        {
            await ZoomAndPan();
            var before = Position();
            var bitmap = image.Source;
            change();
            await Settle();
            var after = Position();
            checks.Add((name, before == after && before.X > 0 && before.Y > 0
                && ReferenceEquals(bitmap, image.Source) != redraw, $"before={before};after={after};redrawn={!ReferenceEquals(bitmap, image.Source)}"));
        }

        void CheckFit(string name)
        {
            checks.Add((name, image.Width > 0 && image.Width <= scroll.ViewportWidth && image.Height <= scroll.ViewportHeight
                && scroll.HorizontalOffset == 0 && scroll.VerticalOffset == 0 && view.HasNativeCoordinateImage,
                $"actual={Position()};image={image.Width}x{image.Height};viewport={scroll.ViewportWidth}x{scroll.ViewportHeight}"));
        }

        try
        {
            window.Show();
            await Load("source.viewport.first");
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(window, reportPath + ".monitor.txt");
            CheckFit("NewInputFits");
            var legendContainer = (Grid)view.FindName("HeightImageHeightLegend");
            var legendControl = legendContainer.Children.OfType<OpenVisionLab.ThreeD.Presentation.Controls.HeightLegendRangeBar>().Single();
            var legendTrack = (Canvas)legendControl.FindName("Track");
            var legendHandle = (System.Windows.Controls.Primitives.Thumb)legendControl.FindName("UpperHandle");
            checks.Add(("InteractiveLegendFitsReservedColumn", legendControl.ActualWidth <= legendContainer.ActualWidth + 0.1
                && legendTrack.ActualWidth == 26 && legendTrack.ActualHeight > 0 && legendHandle.IsVisible && legendHandle.IsEnabled,
                $"container={legendContainer.ActualWidth};control={legendControl.ActualWidth};track={legendTrack.RenderSize};handle={legendHandle.RenderSize}"));
            await Preserve("OverlayRefreshPreservesViewport", () => model.SetCompletenessCellOverlays(
                [new C3DCompletenessCellOverlay("overlay.cell", "A1", new ToolRecipeGridRectangle(100, 120, 80, 100), ResultStatus.Pass)]), redraw: false);
            await Preserve("SelectionPreservesViewport", () => model.SetSelectedCompletenessCellId("A1"), redraw: false);
            checks.Add(("SelectedOverlayUpdated", overlay.Children.OfType<Rectangle>().Any(shape => shape.StrokeThickness == 5), "selected cell has the existing selection stroke"));
            await Preserve("PalettePreservesViewport", () => model.SelectedPalette = C3DHeightImagePalette.Grayscale, redraw: true);
            var frameBeforeMonochrome = model.Frame;
            var pixelsBeforeMonochrome = model.DisplayFrame!.Bgra32Pixels.ToArray();
            await Preserve("MonochromePreservesViewport", () => model.SelectedPalette = C3DHeightImagePalette.Monochrome, redraw: true);
            var monochromePixels = model.DisplayFrame!.Bgra32Pixels.ToArray();
            checks.Add(("MonochromePreservesSourceAndMissingPixels", ReferenceEquals(frameBeforeMonochrome, model.Frame)
                && Enumerable.Range(0, values.Length).All(index => double.IsFinite(values[index]) && values[index] != 0.0
                    ? monochromePixels[index * 4] == 194 && monochromePixels[index * 4 + 1] == 155
                        && monochromePixels[index * 4 + 2] == 115 && monochromePixels[index * 4 + 3] == 255
                    : monochromePixels.AsSpan(index * 4, 4).SequenceEqual(pixelsBeforeMonochrome.AsSpan(index * 4, 4))),
                $"palette={model.DisplayFrame.Palette};source={model.Frame!.SourceContentSha256};missing={model.Frame.MissingCount}"));
            await Preserve("GrayscaleRecoversAfterMonochrome", () => model.SelectedPalette = C3DHeightImagePalette.Grayscale, redraw: true);
            await Preserve("ManualRangePreservesViewport", () => model.TryApplyManualRange(100, 400), redraw: true);
            await Preserve("InvalidMaskPreservesViewport", () => model.ShowInvalidCells = !model.ShowInvalidCells, redraw: true);
            await Preserve("AutoRangePreservesViewport", () => model.AutoRangeCommand.Execute(null), redraw: true);
            window.Width = 470;
            await Settle();
            model.FitCommand.Execute(null);
            await Settle();
            var beforeRangeError = Position();
            model.RangeMinimumText = "1000";
            await Settle();
            checks.Add(("RangeValidationLayoutPreservesViewport", beforeRangeError.Zoom < 100 && Position() == beforeRangeError
                && model.HasRangeError && !model.ApplyManualRangeCommand.CanExecute(null), $"before={beforeRangeError};after={Position()};error={model.RangeError}"));
            model.AutoRangeCommand.Execute(null);
            await Settle();
            checks.Add(("RangeValidationRecoveryPreservesViewport", !model.HasRangeError && Position() == beforeRangeError, $"before={beforeRangeError};after={Position()}"));
            window.Width = 900;
            await Settle();
            var beforeInvalid = Position();
            var beforeInvalidBitmap = image.Source;
            var invalidAccepted = model.TryApplyManualRange(400, 100);
            await Settle();
            checks.Add(("RejectedRangePreservesViewport", !invalidAccepted && beforeInvalid == Position()
                && ReferenceEquals(beforeInvalidBitmap, image.Source), $"accepted={invalidAccepted};position={Position()}"));
            await Preserve("ValidRangeRecovers", () => model.TryApplyManualRange(100, 500), redraw: true);
            model.FitCommand.Execute(null);
            await Settle();
            CheckFit("ExplicitFitResetsViewport");
            model.ActualPixelsCommand.Execute(null);
            await Settle();
            checks.Add(("ActualPixels", model.ZoomPercent == 100 && image.Width == 640 && image.Height == 480, $"zoom={model.ZoomPercent};image={image.Width}x{image.Height}"));
            model.UpdateHover(2, 1);
            model.ZoomInCommand.Execute(null);
            await Settle();
            var peerCursor = (System.Windows.Shapes.Path)view.FindName("LinkedCursorPeerMarker");
            checks.Add(("CursorZoomPreservesNativeCellAndGlyphSize", model.ZoomPercent == 125 && Canvas.GetLeft(peerCursor) + peerCursor.Width / 2.0 == 3.125
                && Canvas.GetTop(peerCursor) + peerCursor.Height / 2.0 == 1.875 && peerCursor.Width == 18 && peerCursor.Height == 18 && peerCursor.StrokeThickness == 1.2,
                $"zoom={model.ZoomPercent};native={model.LinkedCursorColumn},{model.LinkedCursorRow};peer={peerCursor.Width}x{peerCursor.Height}"));
            model.ClearHover();
            await ZoomAndPan();
            var oldBitmap = image.Source;
            await Load("source.viewport.second");
            CheckFit("ReplacedInputFits");
            checks.Add(("ReplacedInputRedraws", !ReferenceEquals(oldBitmap, image.Source), "same dimensions, different input identity"));
            await ZoomAndPan();
            model.FitCommand.Execute(null);
            window.Content = null;
            await Settle();
            checks.Add(("UnloadedCancelsPendingFit", !view.IsLoaded && model.ZoomPercent == 156.25, $"loaded={view.IsLoaded};zoom={model.ZoomPercent}"));
            window.Content = view;
            await Settle();
            CheckFit("ReloadedViewFits");
            var projection = new HeightImageRoiProjection(true, 640, 480, InspectionWorkspaceRegionRole.Selection,
                InspectionWorkspaceRegionLifecycleState.Drawing, true, null, [], false);
            model.RoiWorkspace.SetProjection(projection);
            model.RoiWorkspace.HandlePointer(new HeightImageRoiPointerInput(HeightImageRoiPointerAction.Begin, 20, 30));
            image.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount) { RoutedEvent = Mouse.LostMouseCaptureEvent });
            checks.Add(("ViewCaptureLossStopsGesture", !model.RoiWorkspace.IsGestureActive, "routed WPF capture-loss event; not physical pointer evidence"));
            model.RoiWorkspace.HandlePointer(new HeightImageRoiPointerInput(HeightImageRoiPointerAction.Begin, 20, 30));
            window.Content = null;
            await Settle();
            checks.Add(("UnloadStopsGesture", !model.RoiWorkspace.IsGestureActive && !image.IsMouseCaptured, "view unload cleans the pointer contract"));
            window.Content = view;
            window.Width = 470;
            await Settle();
            var status = FindText(view, "HeightImageStatus");
            checks.Add(("NarrowHeightStatusHasDedicatedRow", status is { ActualWidth: > 400, FontSize: 12, TextWrapping: TextWrapping.Wrap },
                $"width={status?.ActualWidth};text={status?.Text}"));
            view.UseEmbeddedChrome = true;
            view.SelectionContext = "Pad 1 Thickness · 기준 평면 ROI 열 128, 행 156, 너비 50, 높이 144 · 긴 선택 문맥";
            var coordinateText = (TextBlock)view.FindName("HeightImageCoordinateText");
            var cursorScroll = (ScrollViewer)view.FindName("HeightImageCursorScroller");
            var cursorFooter = (Border)view.FindName("HeightImageCursorStatus");
            var selectionContext = (TextBlock)view.FindName("HeightImageSelectionContext");
            foreach (var width in new[] { 320.0, 486.0 })
            {
                view.Width = width;
                window.Width = width + 32;
                model.UpdateHover(639, 479);
                await Settle();
                checks.Add(($"FooterNormalSingleLine{width:0}", coordinateText.Text == "X 639 · Y 639 · Z 479"
                    && coordinateText.ActualHeight <= 14.5 && cursorScroll.ScrollableHeight <= 1 && cursorFooter.ActualHeight == 24,
                    $"view={view.ActualWidth};text={coordinateText.Text};height={coordinateText.ActualHeight};scroll={cursorScroll.ScrollableHeight}"));
                checks.Add(($"FooterKeepsFullUnitContract{width:0}", model.CursorCoordinateSummary == "X 639 · Y 639 raw-height · Z 479"
                    && model.LegendUnit == "raw-height" && selectionContext.TextTrimming == TextTrimming.CharacterEllipsis
                    && cursorScroll.Focusable && cursorScroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto,
                    $"full={model.CursorCoordinateSummary};unit={model.LegendUnit};context={selectionContext.TextTrimming}"));
            }
            await model.EnsureSourceAsync(sourcePath, "source.footer.long", "raw-height", "frame.c3d-grid-index",
                _ => Task.FromResult(C3DHeightFieldSnapshot.CreateForVerification("source.footer.long", 5, 4, Enumerable.Repeat(-1234567.75, 20).ToArray())));
            foreach (var width in new[] { 320.0, 486.0 })
            {
                view.Width = width;
                window.Width = width + 32;
                model.UpdateHover(4, 1);
                await Settle();
                var bounds = coordinateText.TransformToAncestor(cursorFooter).TransformBounds(new Rect(coordinateText.RenderSize));
                checks.Add(($"FooterLongValueSingleLine{width:0}", coordinateText.Text == "X 4 · Y -1234567.75 · Z 1"
                    && coordinateText.ActualHeight <= 14.5 && cursorScroll.ScrollableHeight <= 1 && bounds.Right <= cursorFooter.ActualWidth,
                    $"text={coordinateText.Text};height={coordinateText.ActualHeight};right={bounds.Right};footer={cursorFooter.ActualWidth};scroll={cursorScroll.ScrollableHeight}"));
            }
            model.ClearHover();
            await Settle();
            checks.Add(("FooterEmptyRecoversPlaceholders", coordinateText.Text == "X — · Y — · Z —" && coordinateText.ActualHeight <= 14.5
                && cursorScroll.ScrollableHeight <= 1, $"text={coordinateText.Text};height={coordinateText.ActualHeight};scroll={cursorScroll.ScrollableHeight}"));
        }
        finally
        {
            window.Close();
        }
    }

    internal static async Task VerifyLinkedCursorOverlayAsync(string reportPath, List<(string Name, bool Passed, string Detail)> checks)
    {
        var shared = new SharedHeightCursorSession();
        using var model = new HeightImageViewerViewModel(ThreeDLocalization.Shared, shared);
        var sourcePath = Path.ChangeExtension(reportPath, ".cursor.c3d");
        File.WriteAllBytes(sourcePath, [1]);
        await model.EnsureSourceAsync(sourcePath, "source.cursor-overlay", "raw-height", "frame.c3d-grid-index",
            _ => Task.FromResult(C3DHeightFieldSnapshot.CreateForVerification("source.cursor-overlay", 4, 3, Enumerable.Range(1, 12).Select(value => (double)value).ToArray())));
        var view = new HeightImageViewerView { DataContext = model };
        var image = (Image)view.FindName("HeightImage");
        var overlay = (Canvas)view.FindName("LinkedCursorOverlay");
        var marker = (System.Windows.Shapes.Ellipse)view.FindName("LinkedCursorMarker");
        var peer = (System.Windows.Shapes.Path)view.FindName("LinkedCursorPeerMarker");
        try
        {
            foreach (var state in new (string Name, double Width, double Height)[]
            {
                ("DefaultNaN", double.NaN, double.NaN),
                ("WidthNaN", double.NaN, 3),
                ("HeightNaN", 4, double.NaN),
                ("BothZero", 0, 0),
                ("WidthZero", 0, 3),
                ("HeightZero", 4, 0)
            })
            {
                shared.Clear();
                image.Width = state.Width;
                image.Height = state.Height;
                model.UpdateHover(2, 1);
                checks.Add(($"{state.Name}CursorHidden", overlay.Visibility == Visibility.Collapsed,
                    $"image={image.Width}x{image.Height};overlay={overlay.Visibility}"));
                model.ActualPixelsCommand.Execute(null);
                checks.Add(($"{state.Name}CursorRecovers", overlay.Visibility == Visibility.Visible
                    && image.Width == 4 && image.Height == 3 && Canvas.GetLeft(peer) + peer.Width / 2.0 == 2.5 && Canvas.GetTop(peer) + peer.Height / 2.0 == 1.5,
                    $"image={image.Width}x{image.Height};cursor={Canvas.GetLeft(peer) + peer.Width / 2.0},{Canvas.GetTop(peer) + peer.Height / 2.0}"));
            }
            checks.Add(("HeightCursorFromOtherViewUsesPeerGlyph", model.LinkedCursorOrigin == SharedHeightCursorOrigin.HeightImage
                && !image.IsMouseOver && marker.Visibility == Visibility.Collapsed && peer.Visibility == Visibility.Visible,
                "unloaded view is a peer; physical own-pointer glyph verified separately"));
            shared.Update(SharedHeightCursorOrigin.ThreeDViewer, model.Frame!.SourceContentSha256, 1, 2, 7, true);
            checks.Add(("ThreeDCursorUsesPeerGlyph", model.LinkedCursorOrigin == SharedHeightCursorOrigin.ThreeDViewer
                && marker.Visibility == Visibility.Collapsed && peer.Visibility == Visibility.Visible,
                $"origin={model.LinkedCursorOrigin};peer={peer.Visibility}"));
            model.UpdateHover(2, 1);
            checks.Add(("CursorOriginChangesAtSameCell", model.LinkedCursorOrigin == SharedHeightCursorOrigin.HeightImage
                && Canvas.GetLeft(peer) + peer.Width / 2.0 == 2.5 && Canvas.GetTop(peer) + peer.Height / 2.0 == 1.5,
                $"origin={model.LinkedCursorOrigin};cell={model.LinkedCursorColumn},{model.LinkedCursorRow}"));
            shared.Update(SharedHeightCursorOrigin.HeightImage, model.Frame.SourceContentSha256, 1, 2, double.NaN, false);
            checks.Add(("MissingCursorPreservesUnavailableHeight", !model.LinkedCursorIsValid && model.CursorCoordinateSummary.Contains("Y —")
                && overlay.Visibility == Visibility.Visible && peer.Visibility == Visibility.Visible,
                model.CursorCoordinateSummary));
            shared.Update(SharedHeightCursorOrigin.ThreeDViewer, "other-source", 1, 2, 7, true);
            checks.Add(("WrongSourceCursorHidden", model.LinkedCursorOrigin == SharedHeightCursorOrigin.None && overlay.Visibility == Visibility.Collapsed,
                $"origin={model.LinkedCursorOrigin};overlay={overlay.Visibility}"));
            shared.Update(SharedHeightCursorOrigin.ThreeDViewer, model.Frame.SourceContentSha256, 1, 2, 7, true);

            model.ClearHover();
            checks.Add(("HeightLeavePreservesThreeDCursor", model.LinkedCursorOrigin == SharedHeightCursorOrigin.ThreeDViewer && overlay.Visibility == Visibility.Visible,
                $"origin={model.LinkedCursorOrigin};overlay={overlay.Visibility}"));
            shared.Clear();
            checks.Add(("ClearedCursorHidden", model.LinkedCursorOrigin == SharedHeightCursorOrigin.None && overlay.Visibility == Visibility.Collapsed,
                $"origin={model.LinkedCursorOrigin};overlay={overlay.Visibility}"));
        }
        finally
        {
            view.DataContext = null;
        }
    }

    private static TextBlock? FindText(DependencyObject parent, string id)
    {
        if (parent is TextBlock text && AutomationProperties.GetAutomationId(text) == id) return text;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            if (FindText(VisualTreeHelper.GetChild(parent, index), id) is { } found) return found;
        return null;
    }
}

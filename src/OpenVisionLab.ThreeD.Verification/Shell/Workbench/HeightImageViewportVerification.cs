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
            await Preserve("OverlayRefreshPreservesViewport", () => model.SetCompletenessCellOverlays(
                [new C3DCompletenessCellOverlay("overlay.cell", "A1", new ToolRecipeGridRectangle(100, 120, 80, 100), ResultStatus.Pass)]), redraw: false);
            await Preserve("SelectionPreservesViewport", () => model.SetSelectedCompletenessCellId("A1"), redraw: false);
            checks.Add(("SelectedOverlayUpdated", overlay.Children.OfType<Rectangle>().Any(shape => shape.StrokeThickness == 5), "selected cell has the existing selection stroke"));
            await Preserve("PalettePreservesViewport", () => model.SelectedPalette = C3DHeightImagePalette.Grayscale, redraw: true);
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
            checks.Add(("NarrowHeightStatusHasDedicatedRow", status is { ActualWidth: > 400, FontSize: 11, TextWrapping: TextWrapping.Wrap },
                $"width={status?.ActualWidth};text={status?.Text}"));
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
        var vertical = (System.Windows.Shapes.Line)view.FindName("LinkedCursorVerticalLine");
        var horizontal = (System.Windows.Shapes.Line)view.FindName("LinkedCursorHorizontalLine");
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
                checks.Add(($"{state.Name}CursorHidden", overlay.Visibility == Visibility.Collapsed
                    && double.IsFinite(vertical.X1) && double.IsFinite(horizontal.Y1), $"image={image.Width}x{image.Height};overlay={overlay.Visibility}"));
                model.ActualPixelsCommand.Execute(null);
                checks.Add(($"{state.Name}CursorRecovers", overlay.Visibility == Visibility.Visible
                    && image.Width == 4 && image.Height == 3 && vertical.X1 == 2.5 && horizontal.Y1 == 1.5,
                    $"image={image.Width}x{image.Height};cursor={vertical.X1},{horizontal.Y1}"));
            }
            model.ClearHover();
            checks.Add(("ClearedCursorHidden", overlay.Visibility == Visibility.Collapsed, $"overlay={overlay.Visibility}"));
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

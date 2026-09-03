using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.Views.Tooling;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;
using static OpenVisionLab.ThreeD.Shell.Verification.Smoke.ShellWindowNativeInterop;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellSmokeArtifacts
{
    public static Window RefreshToolLabForCapture(Window window)
    {
        switch (window)
        {
            case FilterToolLabWindow filter:
                filter.RefreshViews();
                break;
            case HeightDifferenceEdgeToolLabWindow edge:
                edge.RefreshViews();
                break;
            case TwoPointLineToolLabWindow twoPointLine:
                twoPointLine.RefreshViews();
                break;
            case ThreePointPlaneToolLabWindow threePointPlane:
                threePointPlane.RefreshViews();
                break;
            case DatumPlaneDeviationToolLabWindow datumPlaneDeviation:
                datumPlaneDeviation.RefreshViews();
                break;
            case LineIntersectionToolLabWindow intersection:
                intersection.RefreshViews();
                break;
            case LandmarkCorrespondenceToolLabWindow correspondence:
                correspondence.RefreshViews();
                break;
            case XYZAffineSolveToolLabWindow affine:
                affine.RefreshViews();
                break;
            case XYZAffineApplyToolLabWindow apply:
                apply.RefreshViews();
                break;
            case RegridHeightMapToolLabWindow regrid:
                regrid.RefreshViews();
                break;
        }

        return window;
    }

    public static void WriteTextReport(string? path, IReadOnlyList<string> lines, bool withoutBom = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (withoutBom)
        {
            File.WriteAllLines(fullPath, lines, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        File.WriteAllLines(fullPath, lines);
    }

    public static async Task<bool> CaptureWindowWithRetryAsync(
        Window window,
        string path,
        string? qualityReportPath,
        string scope)
    {
        const int maximumAttempts = 3;
        var fullPath = Path.GetFullPath(path);
        var qualityLines = new List<string>();
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            var previousRejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            if (File.Exists(previousRejectedPath))
            {
                File.Delete(previousRejectedPath);
            }
        }

        for (var attempt = 1; attempt <= maximumAttempts; attempt++)
        {
            window.UpdateLayout();
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

            var result = WpfScreenshotCapture.Capture(window);
            var qualityLine = $"{scope}Screenshot|attempt={attempt}|{result.Quality.Summary}";
            qualityLines.Add(qualityLine);
            Console.WriteLine(qualityLine);
            if (result.Quality.IsAcceptable)
            {
                WpfScreenshotCapture.Save(result.Bitmap, fullPath);
                qualityLines.Add($"{scope}ScreenshotResult|accepted=True|attempts={attempt}|screenshot={fullPath}");
                WriteTextReport(qualityReportPath, qualityLines);
                return true;
            }

            var rejectedPath = GetRejectedScreenshotPath(fullPath, attempt);
            WpfScreenshotCapture.Save(result.Bitmap, rejectedPath);
            await Task.Delay(250);
        }

        qualityLines.Add($"{scope}ScreenshotResult|accepted=False|attempts={maximumAttempts}|screenshot={fullPath}");
        WriteTextReport(qualityReportPath, qualityLines);
        return false;
    }

    public static async Task<bool> CaptureButtonPressedForSmokeAsync(
        Window window,
        string automationId,
        string screenshotPath,
        string? qualityReportPath,
        string scope)
    {
        const uint leftButtonDown = 0x0002;
        const uint leftButtonUp = 0x0004;
        var mouseDown = false;
        var routedPointerDown = false;
        var forcedPressedState = false;
        System.Windows.Controls.Primitives.ButtonBase? pressedButton = null;
        try
        {
            window.Activate();
            SetForegroundWindow(new WindowInteropHelper(window).Handle);
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Input);
            // Force template realization before resolving a named button. The
            // validation pane is hosted through a docked ContentControl and its
            // deferred template can otherwise remain outside the visual tree
            // until the first bitmap render.
            window.UpdateLayout();
            _ = WpfScreenshotCapture.Capture(window);
            System.Windows.Controls.Primitives.ButtonBase? button = null;
            for (var attempt = 0; attempt < 40 && button is null; attempt++)
            {
                window.UpdateLayout();
                button = FindVisualDescendants<System.Windows.Controls.Primitives.ButtonBase>(window)
                    .FirstOrDefault(candidate =>
                        System.Windows.Automation.AutomationProperties.GetAutomationId(candidate)
                        == automationId);
                if (button is null)
                {
                    button = FindVisualDescendants<RecipePipelineReviewView>(window)
                        .Select(review => review.FindName(automationId) as System.Windows.Controls.Primitives.ButtonBase)
                        .FirstOrDefault(candidate => candidate is not null);
                }
                if (button is not null && !button.IsDescendantOf(window))
                {
                    button = null;
                }
                if (button is null)
                {
                    await window.Dispatcher.InvokeAsync(
                        () => { },
                        DispatcherPriority.Render);
                    await Task.Delay(50);
                }
            }
            if (button is null)
            {
                var visibleIds = string.Join(
                    ",",
                    FindVisualDescendants<System.Windows.Controls.Primitives.ButtonBase>(window)
                        .Where(candidate => candidate.Visibility == Visibility.Visible)
                        .Select(candidate => System.Windows.Automation.AutomationProperties.GetAutomationId(candidate))
                        .Where(id => !string.IsNullOrWhiteSpace(id))
                        .Distinct(StringComparer.Ordinal));
                WriteTextReport(
                    qualityReportPath,
                    [$"{scope}|failure=button-not-found|automationId={automationId}|visibleAutomationIds={visibleIds}"]);
                return false;
            }
            pressedButton = button;
            if (!button.IsEnabled)
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=button-disabled|automationId={automationId}"]);
                return false;
            }
            if (!button.Focus())
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=focus-rejected|automationId={automationId}"]);
                return false;
            }
            var focusedBeforePointer = button.IsKeyboardFocusWithin;

            var relativeCenter = button.TransformToAncestor(window).Transform(new System.Windows.Point(
                button.ActualWidth / 2.0,
                button.ActualHeight / 2.0));
            var transformToDevice = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformToDevice
                ?? System.Windows.Media.Matrix.Identity;
            var deviceCenter = transformToDevice.Transform(relativeCenter);
            var windowHandle = new WindowInteropHelper(window).Handle;
            if (!GetWindowRect(windowHandle, out var windowRectangle))
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=window-rectangle"]);
                return false;
            }
            var center = new System.Windows.Point(
                windowRectangle.Left + deviceCenter.X,
                windowRectangle.Top + deviceCenter.Y);
            if (!SetCursorPos(
                    (int)Math.Round(center.X),
                    (int)Math.Round(center.Y)))
            {
                WriteTextReport(qualityReportPath, [$"{scope}|failure=cursor-position|x={center.X:F1}|y={center.Y:F1}"]);
                return false;
            }

            await Task.Delay(150);
            var hoveredBeforePointer = button.IsMouseOver;
            SendMouseEvent(leftButtonDown, 0, 0, 0, UIntPtr.Zero);
            mouseDown = true;
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Input);
            await window.Dispatcher.InvokeAsync(
                () => { },
                DispatcherPriority.Render);
            await Task.Delay(150);
            if (!button.IsPressed)
            {
                button.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice,
                    Environment.TickCount,
                    System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonDownEvent,
                    Source = button
                });
                routedPointerDown = true;
                await window.Dispatcher.InvokeAsync(
                    () => { },
                    DispatcherPriority.Render);
            }
            if (!button.IsPressed)
            {
                var setIsPressed = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod(
                    "SetIsPressed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                setIsPressed?.Invoke(button, [true]);
                forcedPressedState = button.IsPressed;
            }
            if (!button.IsPressed)
            {
                WriteTextReport(
                    qualityReportPath,
                [
                    $"{scope}|failure=pressed-state-not-held|x={center.X:F1}|y={center.Y:F1}|window={windowRectangle.Left},{windowRectangle.Top},{windowRectangle.Right},{windowRectangle.Bottom}|relative={relativeCenter.X:F1},{relativeCenter.Y:F1}|device={deviceCenter.X:F1},{deviceCenter.Y:F1}"
                ]);
                return false;
            }

            var captured = await CaptureWindowWithRetryAsync(
                window,
                screenshotPath,
                qualityReportPath,
                scope);
            if (captured && !string.IsNullOrWhiteSpace(qualityReportPath))
            {
                File.AppendAllLines(
                    Path.GetFullPath(qualityReportPath),
                [
                    $"PointerDown|scope={scope}|state=held|osInjection={mouseDown}|routedEvent={routedPointerDown}|buttonBasePressedFallback={forcedPressedState}|focused={focusedBeforePointer}|hovered={hoveredBeforePointer}"
                ]);
            }
            return captured;
        }
        finally
        {
            if (forcedPressedState && pressedButton is not null)
            {
                var setIsPressed = typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod(
                    "SetIsPressed",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                setIsPressed?.Invoke(pressedButton, [false]);
            }
            if (routedPointerDown && pressedButton is not null)
            {
                pressedButton.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                    System.Windows.Input.Mouse.PrimaryDevice,
                    Environment.TickCount,
                    System.Windows.Input.MouseButton.Left)
                {
                    RoutedEvent = UIElement.MouseLeftButtonUpEvent,
                    Source = pressedButton
                });
            }
            if (mouseDown)
            {
                SendMouseEvent(leftButtonUp, 0, 0, 0, UIntPtr.Zero);
            }
        }
    }

    public static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static string GetRejectedScreenshotPath(string fullPath, int attempt) =>
        Path.Combine(
            Path.GetDirectoryName(fullPath)!,
            $"{Path.GetFileNameWithoutExtension(fullPath)}.rejected-attempt-{attempt}{Path.GetExtension(fullPath)}");
}

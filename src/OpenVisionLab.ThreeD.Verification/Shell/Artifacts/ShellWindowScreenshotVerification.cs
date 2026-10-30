using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;

namespace OpenVisionLab.ThreeD.Verification.Shell.Artifacts;

internal static class ShellWindowScreenshotVerification
{
    internal static bool Verify(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed)>();
        var output = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!,
            "capture-verification", Guid.NewGuid().ToString("N"));
        var thread = new Thread(() =>
        {
            Window? window = null;
            Popup? popup = null;
            ShellWindowScreenshotController? controller = null;
            try
            {
                ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
                var editor = new TextBox { Text = "Unchanged recipe value", Margin = new Thickness(40), Width = 220, Height = 30,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                var panel = new Grid { Background = Brushes.SteelBlue };
                panel.Children.Add(editor);
                popup = new Popup { PlacementTarget = editor, Placement = PlacementMode.Bottom, StaysOpen = true,
                    Child = new Border { Width = 160, Height = 90, Background = Brushes.Red,
                        Child = new TextBlock { Text = "PV 0 / RMS 0", Foreground = Brushes.White } } };
                panel.Children.Add(popup);
                window = new Window { Title = "3D PNG export verification", Content = panel,
                    WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                    Width = 640, Height = 400, Left = area.Left + 40, Top = area.Top + 40,
                    ShowInTaskbar = false, ShowActivated = false };
                controller = new ShellWindowScreenshotController(window, output);
                window.Show();
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var plain = ShellWindowScreenshotController.Capture(window);
                checks.Add(("FullWindowDimensions", plain.PixelWidth == 640 && plain.PixelHeight == 400));
                popup.IsOpen = true;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var rendered = ShellWindowScreenshotController.Capture(window);
                var pixels = new byte[rendered.PixelWidth * rendered.PixelHeight * 4];
                rendered.CopyPixels(pixels, rendered.PixelWidth * 4, 0);
                var red = Enumerable.Range(0, pixels.Length / 4).Count(index =>
                    pixels[index * 4 + 2] > 240 && pixels[index * 4] < 15 && pixels[index * 4 + 1] < 15);
                checks.Add(("OwnedPopupRenderedInFullFrame", red > 10000));
                checks.Add(("CapturePreservesPopupAndEditor", popup.IsOpen && editor.Text == "Unchanged recipe value"));
                var first = ShellWindowScreenshotController.Save(window, output);
                var firstHash = SHA256.HashData(File.ReadAllBytes(first));
                editor.Text = "Explicit next state";
                var second = ShellWindowScreenshotController.Save(window, output);
                checks.Add(("DistinctFilesWithoutOverwrite", first != second && File.Exists(first) && File.Exists(second)
                    && firstHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(first)))));
                using (var stream = File.OpenRead(second))
                {
                    var decoded = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    checks.Add(("SavedPngDecodes", decoded.Frames.Count == 1 && decoded.Frames[0].PixelWidth == 640));
                }
                checks.Add(("ActualStatesDiffer", !firstHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(second)))));
                popup.HorizontalOffset = 1000;
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var rejected = false;
                try { ShellWindowScreenshotController.Capture(window); }
                catch (InvalidOperationException) { rejected = true; }
                checks.Add(("ClippedPopupRejected", rejected));
                popup.IsOpen = false;
                MainWindow.SaveFullWindowPngCommand.Execute(null, window);
                window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                checks.Add(("RegularCommandWritesPng", Directory.GetFiles(output, "*.png").Length == 3));
                checks.Add(("CommandDoesNotEditOrCloseWindow", window.IsVisible && editor.Text == "Explicit next state"));
                controller.Dispose();
                checks.Add(("DisposedCommandDetached", !window.CommandBindings.OfType<CommandBinding>()
                    .Any(binding => binding.Command == MainWindow.SaveFullWindowPngCommand)));
            }
            catch (Exception exception)
            {
                checks.Add(($"Unexpected-{exception.GetBaseException().Message}", false));
            }
            finally
            {
                if (popup is not null) popup.IsOpen = false;
                controller?.Dispose();
                window?.Close();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        var passed = checks.Count > 0 && checks.All(check => check.Passed);
        summary = $"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(check => $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}").Append(summary));
        return passed;
    }
}

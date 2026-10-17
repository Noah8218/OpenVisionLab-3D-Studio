using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Verification;

namespace OpenVisionLab.ThreeD.Shell;

internal static class C3DTexturePresentationVerification
{
    public static bool VerifySelectionIdentity(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var lines = new List<string> { "Boundary|Real Host source transition during the injected file-selection callback; no modal automation, GL or SDK execution." };
        var passed = 0;
        void Check(string name, bool condition)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
            if (!condition) throw new InvalidOperationException(name);
            passed++;
        }
        try
        {
            using var viewer = new OpenVisionThreeDViewerControl(false);
            var source = Path.Combine(root, "texture-grid.C3D");
            var other = Path.Combine(root, "other-grid.C3D");
            var image = Path.Combine(root, "quadrants.png");
            Check("first source loads", viewer.LoadC3DSource(source));
            var execution = viewer.HostState.Inspection;
            using (var cancelled = new OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ViewerPresentationBarViewModel(viewer, selectTextureImage: () => null))
            {
                cancelled.LoadTextureCommand.Execute(null);
                Check("cancel leaves the source and image association unchanged", !viewer.HostState.Presentation.HasC3DTexture
                    && viewer.CurrentC3DSourcePath == source && cancelled.IsDisplaySettingsOpen);
            }
            var changeSource = true;
            using var bar = new OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ViewerPresentationBarViewModel(viewer, selectTextureImage: () =>
            {
                if (changeSource && !viewer.LoadC3DSource(other)) throw new InvalidOperationException("Second source did not load.");
                return image;
            });
            bar.LoadTextureCommand.Execute(null);
            Check("selection from the earlier source cannot attach after a source change", viewer.CurrentC3DSourcePath == other
                && !viewer.HostState.Presentation.HasC3DTexture && !viewer.HostState.Presentation.C3DTextureVisible && bar.TextureError.Length > 0);
            changeSource = false;
            bar.LoadTextureCommand.Execute(null);
            Check("an explicit selection for the new source recovers", viewer.HostState.Presentation.HasC3DTexture
                && viewer.HostState.Presentation.C3DTextureVisible && bar.TextureError.Length == 0);
            Check("file selection does not run or publish an inspection", viewer.HostState.Inspection.PreviewStatus == execution.PreviewStatus
                && viewer.HostState.Inspection.PublishedResultCount == execution.PublishedResultCount);
            summary = $"C3D texture selection identity: Pass ({passed} checks)";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"C3D texture selection identity: Fail after {passed} checks: {exception}";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return false;
        }
    }

    public static bool VerifyPopup(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var lines = new List<string> { "Boundary|New popup text/viewport/focus properties at native 100%; previous texture/GL checks are reused separately." };
        var passed = 0;
        var previousLanguage = OpenVisionLanguageService.CurrentLanguage;
        Window? window = null;
        OpenVisionThreeDViewerControl? viewer = null;
        void Check(string name, bool condition)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
            if (!condition) throw new InvalidOperationException(name);
            passed++;
        }
        try
        {
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            viewer = new OpenVisionThreeDViewerControl(false) { SidePanelsVisible = false };
            var bar = new ViewerPresentationBar { ViewerHost = viewer };
            var content = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            content.Children.Add(bar);
            content.Children.Add(viewer);
            window = new Window { Title = "C3D texture popup verification", Left = area.Left, Top = area.Top,
                WindowStartupLocation = WindowStartupLocation.Manual, Content = content,
                Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"), Foreground = (Brush)Application.Current.FindResource("ThreeD.TextBrush") };
            window.Show();
            if (!viewer.LoadC3DSource(Path.Combine(root, "texture-grid.C3D")) || !viewer.TryLoadC3DTexture(Path.Combine(root, "quadrants.png")))
                throw new InvalidOperationException("The previously verified texture fixture did not load.");
            foreach (var language in new[] { OpenVisionLanguage.English, OpenVisionLanguage.Korean })
            {
                OpenVisionLanguageService.SetLanguage(language, save: false);
                foreach (var width in new[] { 1920d, 1280d })
                {
                    window.Width = Math.Min(width, area.Width);
                    window.Height = Math.Min(width == 1920 ? 1040 : 760, area.Height);
                    bar.Presentation!.OpenDisplaySettingsCommand.Execute(null);
                    Settle(window);
                    var popup = (Popup)bar.FindName("ViewerDisplaySettingsPopup");
                    var scroll = (ScrollViewer)bar.FindName("ViewerDisplayOptionsScroll");
                    var stack = (StackPanel)bar.FindName("ViewerDisplayOptionsContent");
                    Check($"{language}/{width} content fits the scroll viewport", popup.IsOpen && scroll.ViewportWidth > 0
                        && Math.Abs(stack.ActualWidth - scroll.ViewportWidth) < .5 && scroll.ExtentWidth <= scroll.ViewportWidth + .5);
                    Check($"{language}/{width} required checkbox captions can wrap", Descendants<CheckBox>(popup.Child)
                        .All(control => control.ContentTemplate is not null));
                    var choose = Descendants<Button>(popup.Child).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ViewerLoadTexture");
                    Check($"{language}/{width} image actions expose a keyboard focus visual", choose.FocusVisualStyle is not null && choose.IsEnabled);
                    Capture(window, Path.Combine(root, $"popup-{language}-{width}.png"));
                    popup.IsOpen = false;
                }
            }
            summary = $"C3D texture popup verification: Pass ({passed} checks)";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"C3D texture popup verification: Fail after {passed} checks: {exception}";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return false;
        }
        finally { viewer?.Dispose(); window?.Close(); OpenVisionLanguageService.SetLanguage(previousLanguage, save: false); }
    }

    public static bool Verify(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Real WPF/GL host, routed controls and per-pane preferences. Supporting automation, not Computer Use or full UI acceptance." };
        var passed = 0;
        Window? window = null;
        OpenVisionThreeDViewerControl? viewer = null;
        void Check(string name, bool condition)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
            File.WriteAllLines(Path.ChangeExtension(reportPath, ".progress.txt"), lines);
            if (!condition) throw new InvalidOperationException(name);
            passed++;
        }
        try
        {
            var managedPassed = C3DTextureVerification.Verify(Path.Combine(root, "managed.txt"), out var managedSummary);
            lines.Add(managedSummary);
            Check("texture admission and source-state contract passes", managedPassed);
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            viewer = new OpenVisionThreeDViewerControl(false) { SidePanelsVisible = false };
            var chosen = Path.Combine(root, "quadrants.png");
            var bar = new ViewerPresentationBar { ViewerHost = viewer };
            var content = new DockPanel();
            DockPanel.SetDock(bar, Dock.Top);
            content.Children.Add(bar);
            content.Children.Add(viewer);
            window = new Window { Title = "C3D texture verification", Width = Math.Min(1100, area.Width), Height = Math.Min(760, area.Height),
                Left = area.Left, Top = area.Top, WindowStartupLocation = WindowStartupLocation.Manual, Content = content,
                Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"), Foreground = (Brush)Application.Current.FindResource("ThreeD.TextBrush") };
            window.Show();
            Check("trusted C3D source loads through the Host", viewer.LoadC3DSource(Path.Combine(root, "texture-grid.C3D")));
            viewer.UseTopView();
            Settle(window);
            var before = viewer.HostState;
            Check("actual texture attachment reaches the renderer", viewer.TryLoadC3DTexture(chosen));
            Settle(window);
            var resource = typeof(OpenVisionThreeDViewerControl).GetField("c3dRenderResources", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(viewer)!;
            int Counter(string name) => (int)resource.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resource)!;
            uint Handle() => (uint)resource.GetType().GetField("TextureId", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(resource)!;
            Check("current GL context uploaded one texture", Handle() != 0 && Counter("TextureUploadCount") == 1 && viewer.HostState.Presentation.C3DTextureVisible);
            Check("texture hides the scalar legend while keeping its numeric range", !viewer.HostState.Presentation.C3DHeightDistributionVisible
                && viewer.HostState.Presentation.C3DHeightColorMinimumRaw == before.Presentation.C3DHeightColorMinimumRaw
                && viewer.HostState.Presentation.C3DHeightColorMaximumRaw == before.Presentation.C3DHeightColorMaximumRaw);
            Capture(window, Path.Combine(root, "texture-top.png"));
            CheckBox toggle;
            bar.Presentation!.OpenDisplaySettingsCommand.Execute(null);
            Settle(window);
            var popup = (Popup)bar.FindName("ViewerDisplaySettingsPopup");
            toggle = Descendants<CheckBox>(popup.Child).Single(control => System.Windows.Automation.AutomationProperties.GetAutomationId(control) == "ViewerTextureVisible");
            Check("popup renders the selected image and checked toggle", popup.IsOpen && toggle.IsChecked == true
                && Descendants<TextBlock>(popup.Child).Any(text => text.Text == "quadrants.png") && toggle.IsEnabled);
            toggle.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
            Settle(window);
            Check("rendered toggle updates Host visibility and restores the scalar legend", !viewer.HostState.Presentation.C3DTextureVisible
                && viewer.HostState.Presentation.C3DHeightDistributionVisible && Handle() != 0);
            viewer.TrySetC3DTextureVisible(true);
            Settle(window);
            Check("Host visibility updates the rendered toggle without another upload", toggle.IsChecked == true && Counter("TextureUploadCount") == 1);
            popup.IsOpen = false;
            var profilePath = Path.Combine(root, "pane-layout.json");
            var profile = StudioLayoutProfile.Default with { PaneDisplays = new Dictionary<string, OpenVisionLab.ThreeD.Data.HeightDisplayProfile>
            {
                ["main/3d"] = new(new(), new Dictionary<string, string>(), "texture-test-recipe") { Textures = viewer.CaptureC3DTextureSettings() }
            } };
            var store = new StudioLayoutProfileStore(profilePath);
            store.Save(profile);
            var restored = store.Load();
            viewer.TryClearC3DTexture();
            Settle(window);
            Check("remove retires the current GL texture", Handle() == 0 && Counter("TextureReleaseCount") == 1 && !viewer.HostState.Presentation.HasC3DTexture);
            viewer.RestoreC3DTextureSettings(restored.Profile.PaneDisplays!["main/3d"].Textures);
            Settle(window);
            Check("layout save/reload restores the source-bound image and uploads it once", restored.Status == StudioLayoutLoadStatus.Restored
                && viewer.HostState.Presentation.C3DTextureVisible && Handle() != 0 && Counter("TextureUploadCount") == 2);
            viewer.ViewModel.SelectedRenderDensity = "Fast";
            Settle(window);
            Check("render-density reload retains texture identity without upload", viewer.HostState.Presentation.C3DTextureVisible && Counter("TextureUploadCount") == 2);
            Check("failed replacement preserves the displayed texture", !viewer.TryLoadC3DTexture(Path.Combine(root, "truncated.png"))
                && viewer.HostState.Presentation.C3DTextureVisible && viewer.HostState.Presentation.C3DTextureError.Length > 0);
            Check("valid image recovers from replacement failure", viewer.TryLoadC3DTexture(chosen));
            Settle(window);
            Check("replacement retires the prior texture exactly once", Counter("TextureUploadCount") == 3 && Counter("TextureReleaseCount") == 2);
            viewer.TrySetSelectedGeometryStyle("Surface + Edges");
            Settle(window);
            Check("texture operations retain inspection and source identity", viewer.HostState.Inspection.PreviewReferenceToken == before.Inspection.PreviewReferenceToken
                && viewer.HostState.Inspection.PublishedResultCount == before.Inspection.PublishedResultCount
                && viewer.HostState.Sources.CurrentC3DSourcePath == before.Sources.CurrentC3DSourcePath);
            viewer.Dispose();
            Check("Dispose retires the final texture without a release failure", Handle() == 0 && Counter("TextureReleaseCount") == 3 && Counter("TextureReleaseFailureCount") == 0);
            summary = $"C3D texture presentation verification: Pass ({passed} checks)";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return true;
        }
        catch (Exception exception)
        {
            summary = $"C3D texture presentation verification: Fail after {passed} checks: {exception}";
            File.WriteAllLines(reportPath, lines.Append(summary));
            return false;
        }
        finally { viewer?.Dispose(); window?.Close(); }
    }

    private static void Settle(Window window)
    {
        window.UpdateLayout();
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(180) };
        timer.Tick += (_, _) => frame.Continue = false;
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T match) yield return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static void Capture(Window window, string path)
    {
        var image = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        image.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}

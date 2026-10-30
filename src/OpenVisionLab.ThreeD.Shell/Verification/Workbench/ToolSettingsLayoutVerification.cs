using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolSettingsLayoutVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        var lines = new List<string> { "Boundary|Rendered WPF settings and scrollbar layout; synthetic local source, Graphite96DPI; no SDK execution or Computer Use." };
        var passed = 0;
        var total = 0;
        Window? host = null;
        var language = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
        void Check(string name, bool result, string detail)
        {
            total++;
            if (result) passed++;
            lines.Add($"{(result ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            OpenVisionLab.OpenVisionLanguageService.SetLanguage(OpenVisionLab.OpenVisionLanguage.Korean, save: false);
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            var recipePath = WorkbenchSettingsVerificationFixture.CreateNoiseRecipe(root);
            Check("fixture opens", workbench.TryOpenTeachingRecipe(recipePath, out var message), message);
            var original = File.ReadAllBytes(recipePath);
            var source = workbench.Source.Path;
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Style = new Style(typeof(Window)), WindowStyle = WindowStyle.None, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();host.Activate();
            void Settle() => host.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(host.UpdateLayout));
            void Click(ButtonBase button)
            {
                typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);
                Settle();
            }
            void Gutter(ScrollViewer scroll, string key)
            {
                scroll.ApplyTemplate();Settle();
                var presenter = (ScrollContentPresenter)scroll.Template.FindName("PART_ScrollContentPresenter", scroll);
                var bar = (ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar", scroll);
                var right = presenter.TranslatePoint(new Point(presenter.ActualWidth, 0), scroll).X;
                var left = bar.TranslatePoint(new Point(), scroll).X;
                Check(key + " scrollbar has its own column", bar.IsVisible && bar.ActualWidth >= 14 && right <= left + 0.5,
                    $"contentRight={right:F2};barLeft={left:F2};barWidth={bar.ActualWidth:F2};viewport={scroll.ViewportWidth:F2}");
            }
            foreach (var size in new[] { (w: 1280d, h: 760d, pane: 290d, body: 704d), (w: 1920d, h: 1040d, pane: 320d, body: 970d) })
            {
                host.MinWidth = host.Width = size.w;host.MinHeight = host.Height = size.h;
                var view = new SelectedToolWorkspaceView { DataContext = workbench, Width = size.pane, Height = size.body, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                host.Content = view;Settle();
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
                var scroll = (ScrollViewer)view.FindName("SelectedToolScrollViewer");
                var grid = (RecipeStepPropertyGridHost)view.FindName("StepPropertyGrid");
                Check($"{size.w} common settings fit", scroll.ScrollableHeight <= 1 && scroll.ScrollableWidth <= 1, $"extent={scroll.ExtentHeight:F2};viewport={scroll.ViewportHeight:F2}");
                Click((RadioButton)view.FindName("EditorDetailTab"));
                Check($"{size.w} details use the same paged editor", grid.IsPagedPresentation && !grid.PrimaryParametersOnly, $"paged={grid.IsPagedPresentation};primary={grid.PrimaryParametersOnly}");
                foreach (var topic in new[] { "Parameters", "Io", "Processing", "Help" })
                {
                    Click((RadioButton)view.FindName("Detail" + topic + "Tab"));
                    Check($"{size.w}/{topic} navigation stays outside scroll", !scroll.IsAncestorOf((DependencyObject)view.FindName("Detail" + topic + "Tab"))
                        && !scroll.IsAncestorOf((DependencyObject)view.FindName("EditorDetailTab")), $"extent={scroll.ExtentHeight:F2};viewport={scroll.ViewportHeight:F2}");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(view).Bitmap, Path.Combine(root, $"settings-{size.w}-{topic}.png"));
                }
                Click((RadioButton)view.FindName("DetailIoTab"));view.Height = 460;Settle();
                var tab = (RadioButton)view.FindName("EditorAllTab");
                var before = tab.TranslatePoint(new Point(), view);
                Gutter(scroll, $"{size.w} short settings");scroll.ScrollToEnd();Settle();
                Check($"{size.w} overflow keeps navigation fixed", tab.TranslatePoint(new Point(), view) == before && scroll.VerticalOffset > 0, $"offset={scroll.VerticalOffset:F2}");
                Check($"{size.w} navigation preserves recipe and source", workbench.Source.Path == source && !workbench.IsDirty && !workbench.HasPendingStepParameterChanges
                    && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")) && original.SequenceEqual(File.ReadAllBytes(recipePath)), "no execution or persistence write");

                // An existing high-parameter editor proves bounded paging without running re-gridding.
                var draft = new RegridHeightMapStepProperties();
                var editorHost = new RecipeStepPropertyGridHost { Width = size.pane - 24, IsCompactPresentation = true, IsPagedPresentation = true,
                    AcceptanceParametersOnly = false, SelectedObject = draft, Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                host.Content = editorHost;Settle();
                var pages = (ComboBox)editorHost.FindName("ParameterPageSelector");
                var seen = new HashSet<object>();
                Check($"{size.w} large editor offers ranges", pages.Items.Count >= 7, $"pages={pages.Items.Count};fields={editorHost.VisiblePropertyCount}");
                for (var index = 0; index < pages.Items.Count; index++)
                {
                    pages.SetCurrentValue(Selector.SelectedIndexProperty, index);Settle();
                    var visible = Descendants<System.Windows.Controls.WpfPropertyGrid.PropertyContainer>(editorHost).Where(field => field.IsVisible).ToArray();
                    foreach (var field in visible) seen.Add(field);
                    Check($"{size.w} range {index} fits and keeps all text accessible", visible.Length is >= 1 and <= 3 && editorHost.ActualHeight <= 390 && editorHost.ActualWidth <= size.pane - 24 + 1,
                        $"fields={visible.Length};height={editorHost.ActualHeight:F2};selection={pages.SelectedItem}");
                }
                Check($"{size.w} every large-editor field is reachable", seen.Count == editorHost.VisiblePropertyCount, $"seen={seen.Count};total={editorHost.VisiblePropertyCount}");
                var pitch = Descendants<TextBox>(editorHost).Single(box => AutomationProperties.GetAutomationId(box) == "PitchU");
                var fieldIndex = Descendants<System.Windows.Controls.WpfPropertyGrid.PropertyContainer>(editorHost).ToList().FindIndex(field => field.IsAncestorOf(pitch));
                pages.SetCurrentValue(Selector.SelectedIndexProperty, fieldIndex / 3);Settle();Keyboard.Focus(pitch);pitch.Text = "invalid";Settle();
                pages.SetCurrentValue(Selector.SelectedIndexProperty, 0);Settle();
                Check($"{size.w} hidden invalid draft survives page changes", editorHost.HasValidationErrors && pitch.Text == "invalid" && !editorHost.CommitPendingEdit(out _), "same editor, normal commit refusal");
                Check($"{size.w} error navigation reveals the original editor", editorHost.FocusInvalidEditor() && pitch.IsVisible && ReferenceEquals(pitch, Descendants<TextBox>(editorHost).Single(box => AutomationProperties.GetAutomationId(box) == "PitchU")), $"page={pages.SelectedIndex}");
                pitch.Text = "1.25";Settle();
                Check($"{size.w} valid retry recovers without replacing draft", editorHost.CommitPendingEdit(out _) && !editorHost.HasValidationErrors && draft.PitchU == 1.25 && ReferenceEquals(editorHost.SelectedObject, draft), $"pitch={draft.PitchU}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(editorHost).Bitmap, Path.Combine(root, $"large-parameters-{size.w}.png"));
                editorHost.CanEditValues = false;pages.SetCurrentValue(Selector.SelectedIndexProperty, pages.Items.Count - 1);Settle();
                Check($"{size.w} read-only values keep range navigation enabled", pages.IsEnabled && !pitch.IsEnabled && draft.PitchU == 1.25
                    && Descendants<System.Windows.Controls.WpfPropertyGrid.PropertyContainer>(editorHost).Any(field => field.IsVisible), $"page={pages.SelectedIndex};editable={pitch.IsEnabled}");

                var template = ToolRecipeDocumentStore.Load(recipePath);
                var manyPath = Path.Combine(root, $"flow-{size.w}.ov3d-recipe.json");
                ToolRecipeDocumentStore.Save(manyPath, template with { Steps = Enumerable.Range(0, 12).Select(index => template.Steps[0] with { Id = $"step.{index}", OutputEntityId = $"output.{index}" }).ToArray() });
                Check($"{size.w} long flow opens", workbench.TryOpenTeachingRecipe(manyPath, out message), message);
                var flow = new RecipeChainView { DataContext = workbench, IsTeachingMode = true, Width = size.pane, Height = size.body, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                host.Content = flow;Settle();
                var flowScroll = (ScrollViewer)flow.FindName("FlowGraphViewport");Gutter(flowScroll, $"{size.w} flow");
                Check($"{size.w} flow does not gain horizontal overflow", flowScroll.ScrollableWidth <= 1, $"horizontal={flowScroll.ScrollableWidth:F2}");
                flowScroll.ScrollToEnd();Settle();
                Check($"{size.w} last flow node can be reached", flowScroll.VerticalOffset >= flowScroll.ScrollableHeight - 1, $"offset={flowScroll.VerticalOffset:F2};max={flowScroll.ScrollableHeight:F2}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(flow).Bitmap, Path.Combine(root, $"flow-gutter-{size.w}.png"));
                workbench.TryOpenTeachingRecipe(recipePath, out _);Settle();host.Content = null;Settle();
                OpenVisionLab.OpenVisionLanguageService.SetLanguage(OpenVisionLab.OpenVisionLanguage.English, save: false);
                var english = new SelectedToolWorkspaceView { DataContext = workbench, Width = size.pane, Height = size.body, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                host.Content = english;Settle();Click((RadioButton)english.FindName("EditorDetailTab"));
                Check($"{size.w} English detail captions render", new[] { "DetailParametersTab", "DetailIoTab", "DetailProcessingTab", "DetailHelpTab" }
                    .All(name => english.FindName(name) is RadioButton { IsVisible: true, ActualWidth: > 55 }), "Values / I/O / Process / Help");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(english).Bitmap, Path.Combine(root, $"settings-english-{size.w}.png"));
                host.Content = null;Settle();
                var rail = new OpenVisionLab.ThreeD.Shell.Views.Shell.StudioNavigationRailView
                {
                    HorizontalAlignment = HorizontalAlignment.Left, Height = size.body,
                    DataContext = new { SelectedLanguageOption = new { Language = OpenVisionLab.OpenVisionLanguage.English } }
                };
                host.Content = rail;Settle();rail.ApplyResponsiveWidthForVerification(size.w);Settle();
                foreach (var name in new[] { "AuthoringModeButton", "ValidateModeButton", "InspectionModeButton", "ResultsModeButton" })
                {
                    var route = (RadioButton)rail.FindName(name);
                    var text = Descendants<TextBlock>(route).Single(text => text.Text is "Teach" or "Validate" or "Inspect" or "Records");
                    var measured = new FormattedText(text.Text, System.Globalization.CultureInfo.CurrentUICulture, text.FlowDirection,
                        new Typeface(text.FontFamily, text.FontStyle, text.FontWeight, text.FontStretch), text.FontSize, text.Foreground, VisualTreeHelper.GetDpi(text).PixelsPerDip);
                    Check($"{size.w}/{name} English caption fits on one line", measured.WidthIncludingTrailingWhitespace <= text.ActualWidth + .5
                        && text.ActualHeight <= measured.Height + 1, $"text={text.Text};measured={measured.WidthIncludingTrailingWhitespace:F2}x{measured.Height:F2};actual={text.ActualWidth:F2}x{text.ActualHeight:F2}");
                }
                host.Content = null;Settle();OpenVisionLab.OpenVisionLanguageService.SetLanguage(OpenVisionLab.OpenVisionLanguage.Korean, save: false);
            }
        }
        catch (Exception exception) { total++;lines.Add("FAIL | unexpected | " + exception); }
        finally { host?.Close();OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false); }
        summary = $"Tool settings layout verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject owner) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(owner); index++)
        {
            var child = VisualTreeHelper.GetChild(owner, index);
            if (child is T match) yield return match;
            foreach (var matchChild in Descendants<T>(child)) yield return matchChild;
        }
    }
}

using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Data;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Shell.Behaviors;

namespace OpenVisionLab.ThreeD.Verification.Shell.Behaviors;

internal static class WordBoundaryWrapVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed)>();
        var thread = new Thread(() =>
        {
            try
            {
                var caption = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.LightGray, TextWrapping = TextWrapping.Wrap };
                var implicitStyle = new Style(typeof(TextBlock));
                implicitStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.Black));
                implicitStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 9d));
                caption.Resources.Add(typeof(TextBlock), implicitStyle);
                WordBoundaryWrapBehavior.SetText(caption, "XYZ 어파인 계산");
                foreach (var width in new[] { 65d, 200d })
                {
                    for (var pass = 0; pass < 2; pass++)
                    {
                        caption.Measure(new Size(width, double.PositiveInfinity));
                        caption.Arrange(new Rect(0, 0, width, caption.DesiredSize.Height));
                        caption.UpdateLayout();
                    }
                    var words = caption.Inlines.OfType<InlineUIContainer>().Select(inline => (TextBlock)inline.Child).ToArray();
                    checks.Add(($"WholeWords-{width}", words.Select(word => word.Text).SequenceEqual(new[] { "XYZ", "어파인", "계산" })
                        && words.All(word => word.DesiredSize.Width <= width + 0.5 && word.ActualHeight < 30)));
                    checks.Add(($"ThemeAndHitRegion-{width}", words.All(word => word.FontSize == caption.FontSize
                        && word.FontWeight == caption.FontWeight && Equals(word.Foreground, caption.Foreground) && !word.IsHitTestVisible)));
                }
                checks.Add(("AccessibleFullName", AutomationProperties.GetName(caption) == "XYZ 어파인 계산"));
                WordBoundaryWrapBehavior.SetText(caption, "긴경로식별자ABCDEFGHIJKLMN123456789");
                caption.Measure(new Size(65, double.PositiveInfinity));
                caption.Arrange(new Rect(0, 0, 65, caption.DesiredSize.Height));
                caption.UpdateLayout();
                checks.Add(("OversizedTokenCanWrap", caption.Inlines.OfType<InlineUIContainer>().All(inline => ((TextBlock)inline.Child).DesiredSize.Width <= 65.5)));
                WordBoundaryWrapBehavior.SetText(caption, string.Empty);
                checks.Add(("ClearRemovesOldCaption", caption.Inlines.Count == 0 && AutomationProperties.GetName(caption) == string.Empty));
            }
            catch (Exception exception)
            {
                checks.Add(($"Unexpected-{exception.GetBaseException().Message}", false));
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        var passed = checks.All(check => check.Passed);
        summary = $"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(check => $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}").Append(summary));
        return passed;
    }

    public static bool VerifyLandmarkCaptions(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var thread = new Thread(() =>
        {
            var originalLanguage = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
            Application? app = null;
            void Check(string name, bool passed, string detail) => checks.Add((name, passed, detail));
            void Layout(FrameworkElement element, double width)
            {
                for (var pass = 0; pass < 3; pass++)
                {
                    element.Measure(new Size(width, double.PositiveInfinity));
                    element.Arrange(new Rect(0, 0, width, element.DesiredSize.Height));
                    element.UpdateLayout();
                }
            }
            bool CompleteCaption(TextBlock caption, string expected)
            {
                var words = caption.Inlines.OfType<InlineUIContainer>().Select(inline => (TextBlock)inline.Child).ToArray();
                return WordBoundaryWrapBehavior.GetText(caption) == expected && AutomationProperties.GetName(caption) == expected
                    && words.Select(word => word.Text).SequenceEqual(expected.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    && words.All(word =>
                    {
                        var bounds = word.TransformToAncestor(caption).TransformBounds(new Rect(word.RenderSize));
                        return bounds.Left >= -0.5 && bounds.Right <= caption.ActualWidth + 0.5 && bounds.Top >= -0.5 && bounds.Bottom <= caption.ActualHeight + 0.5
                            && !word.IsHitTestVisible && word.FontSize == caption.FontSize && Equals(word.Foreground, caption.Foreground);
                    })
                    && (OpenVisionLab.OpenVisionLanguageService.CurrentLanguage != OpenVisionLab.OpenVisionLanguage.Korean
                        || words.All(word => word.ActualHeight <= caption.FontSize * 1.8));
            }
            try
            {
                app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
                app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
                var view = new OpenVisionLab.ThreeD.Shell.Views.Workbench.SelectedToolWorkspaceView();
                var inputs = Descendants<ItemsControl>(view, logical: true).Single(control => AutomationProperties.GetAutomationId(control) == "SelectedToolDataInputs");
                var row = (FrameworkElement)inputs.ItemTemplate.LoadContent();
                var inputCaptions = Descendants<TextBlock>(row).Where(text => text.MaxWidth == 76).ToArray();
                foreach (var language in new[] { OpenVisionLab.OpenVisionLanguage.Korean, OpenVisionLab.OpenVisionLanguage.English, OpenVisionLab.OpenVisionLanguage.Korean })
                {
                    OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false);
                    var item = new OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.SelectedToolInputItem(0, "Correspondence selection", "input.identity", "input.name", "Ready", "frame", "raw-height", true);
                    row.DataContext = item;
                    Layout(row, 296);
                    var inputCaption = inputCaptions.Single(text => text.Visibility == Visibility.Visible);
                    Check($"InputCaption-{language}", CompleteCaption(inputCaption, item.RequiredContractDisplay),
                        $"text={WordBoundaryWrapBehavior.GetText(inputCaption)};bounds={inputCaption.ActualWidth:G4}x{inputCaption.ActualHeight:G4}");
                    Check($"InputIdentity-{language}", item.EntityId == "input.identity" && item.RequiredContract == "Correspondence selection"
                        && ReferenceEquals(BindingOperations.GetBindingExpression(inputCaption, WordBoundaryWrapBehavior.TextProperty)?.DataItem, item),
                        "Caption binding uses the existing typed-input item; IDs and route are unchanged.");
                    var other = item with { RequiredContract = "Published LineFeature" };
                    row.DataContext = other;
                    Layout(row, 296);
                    var otherCaption = inputCaptions.Single(text => text.Visibility == Visibility.Visible);
                    Check($"OtherInputUnchanged-{language}", otherCaption.Text == other.RequiredContractDisplay
                        && WordBoundaryWrapBehavior.GetText(otherCaption) == string.Empty && otherCaption.Inlines.OfType<InlineUIContainer>().Count() == 0,
                        $"actual={otherCaption.Text};expected={other.RequiredContractDisplay}");
                    foreach (var compact in new[] { false, true })
                    {
                        var draft = new OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.LandmarkCorrespondenceStepProperties();
                        var host = new OpenVisionLab.ThreeD.Shell.PropertyGrid.RecipeStepPropertyGridHost { IsCompactPresentation = compact, SelectedObject = draft };
                        try
                        {
                            Layout(host, 296);
                            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                            Layout(host, 296);
                            var captions = Descendants<TextBlock>(host).Where(text => text.GetType().Name == "PropertyNameTextBlock").ToArray();
                            var target = captions.FirstOrDefault(text => text.DataContext is System.Windows.Controls.WpfPropertyGrid.PropertyItem { Name: "PairCountPolicy" });
                            if (!compact)
                            {
                                var expected = language == OpenVisionLab.OpenVisionLanguage.Korean ? "대응 쌍 수 처리 방법" : "Pair count";
                                var grid = (System.Windows.Controls.WpfPropertyGrid.PropertyGrid)host.FindName("InnerGrid");
                                foreach (var nameWidth in new[] { 104d, 128d })
                                {
                                    grid.PropertyNameColumnWidth = new GridLength(nameWidth);
                                    Layout(host, 296);
                                    Check($"PairCountCaption-{language}-{nameWidth}", target is not null && CompleteCaption(target, expected),
                                        string.Join(";", captions.Select(text => $"{text.Text}:{text.DataContext?.GetType().Name}:{WordBoundaryWrapBehavior.GetText(text)}")));
                                }
                            }
                            Check($"NeighborCaptions-{language}-{compact}", captions.Where(text => !ReferenceEquals(text, target)).All(text => WordBoundaryWrapBehavior.GetText(text) == string.Empty)
                                && draft.PairCountPolicy == "ExactlyFour" && draft.SourceArtifactPolicy == "CurrentPublishedCornerAnchor" && draft.AffineIndependencePolicy == "RequireNonDegenerateTetrahedra",
                                "Other PropertyGrid captions and source policies remain unchanged.");
                        }
                        finally
                        {
                            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                        }
                    }
                }
                Check("NoDesktopWindow", app.Windows.Count == 0 && app.MainWindow is null, "No Window constructed, shown or executed; no GUI/input/monitor lookup.");
            }
            catch (Exception exception)
            {
                Check("Unexpected", false, exception.ToString());
            }
            finally
            {
                OpenVisionLab.OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
                app?.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        var passed = checks.All(check => check.Passed);
        summary = $"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(check => $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}|{check.Detail}").Append(summary));
        return passed;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root, bool logical = false) where T : DependencyObject
    {
        var children = logical ? LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()
            : Enumerable.Range(0, VisualTreeHelper.GetChildrenCount(root)).Select(index => VisualTreeHelper.GetChild(root, index));
        foreach (var child in children)
        {
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child, logical)) yield return descendant;
        }
    }
}

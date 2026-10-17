using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ValidationSamplesPresentationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var checks = new List<(string Name, bool Passed, string Detail)>();
        var thread = new Thread(() =>
        {
            try { VerifyOnSta(reportPath, checks); }
            catch (Exception exception) { checks.Add(("Execution", false, exception.ToString())); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        var passed = checks.Count > 0 && checks.All(check => check.Passed);
        summary = $"ValidationSamplesPresentation|pass={passed}|checks={checks.Count(check => check.Passed)}/{checks.Count}";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, checks.Select(check => $"{(check.Passed ? "PASS" : "FAIL")}|{check.Name}|{check.Detail}").Append(summary));
        return passed;
    }

    private static void VerifyOnSta(string reportPath, List<(string Name, bool Passed, string Detail)> checks)
    {
        var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
        var fixtureRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "validation-samples-presentation-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var paths = new[] { "sample-01.C3D", "sample-02.C3D", "입력 식별자를 읽고 선택해야 하는 매우 긴 검증 샘플 이름.C3D" }.Select(name => Path.Combine(fixtureRoot, name)).ToArray();
        foreach (var path in paths) File.WriteAllBytes(path, [1]); // Staging only; no execution or fabricated results.
        var languageBefore = OpenVisionLanguageService.CurrentLanguage;
        try
        {
            foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
            {
                OpenVisionLanguageService.SetLanguage(language, save: false);
                using var workbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, $"recent-{language}.json"));
                workbench.SetValidationSetSources(paths);
                checks.Add(($"{language} new samples default to unset expectation and adjustment use", workbench.ValidationSetUnsetCount == 3 && workbench.ValidationSetAdjustmentCount == 0
                    && workbench.ValidationSetSamples.All(sample => sample.IsUnset && sample.IsAdjustment), "unset samples excluded from agreement denominator"));
                foreach (var sample in workbench.ValidationSetSamples.ToArray()) workbench.SetValidationSampleRoleCommand.Execute(sample.GoodRoleChange);
                var view = new ValidationSamplesView { DataContext = workbench };
                var dockModel = new AvalonDock.Layout.LayoutAnchorable { ContentId = "validation-samples-presentation" };
                var dockPane = new AvalonDock.Layout.LayoutAnchorablePane();
                dockPane.Children.Add(dockModel);
                var dockPanel = new AvalonDock.Layout.LayoutPanel();
                dockPanel.Children.Add(dockPane);
                var dockManager = new AvalonDock.DockingManager { Layout = new AvalonDock.Layout.LayoutRoot { RootPanel = dockPanel } };
                view.DockPaneCommands = dockManager.GetLayoutItemFromModel(dockModel);
                var list = (ListBox)view.FindName("SamplesList");
                void Settle(double width = 272, double height = 668)
                {
                    view.Width = width;
                    view.Height = height;
                    view.Measure(new Size(width, height));
                    view.Arrange(new Rect(0, 0, width, height));
                    view.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Background);
                    view.UpdateLayout();
                }
                ButtonBase Button(string id) => Descendants<ButtonBase>(view).Single(button => AutomationProperties.GetAutomationId(button) == id);
                void Activate(ButtonBase button)
                {
                    // Supporting command/binding proof only; native pointer and keyboard evidence is separate.
                    typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(button, null);
                    Settle();
                }
                Settle();
                var beforeLog = workbench.RunLog.Count;
                var beforeDirty = workbench.IsDirty;
                Activate(Button("ValidationSamplesFilterMismatch"));
                checks.Add(($"{language} mismatch filter and empty reset", list.Items.Count == 0
                    && workbench.IsValidationSetFilterMismatch && ((StackPanel)view.FindName("SamplesEmptyState")).Visibility == Visibility.Visible
                    && workbench.ValidationSetAllCount == 3 && workbench.IsDirty == beforeDirty && workbench.RunLog.Count == beforeLog && !workbench.IsValidationSetRunning,
                    "Pending inputs yield no mismatches; canonical samples, dirty state and execution remain unchanged"));
                Activate(Button("ValidationSamplesResetFilter"));
                checks.Add(($"{language} filter reset retains selection binding", list.Items.Count == 3 && workbench.IsValidationSetFilterAll
                    && ReferenceEquals(list.SelectedItem, workbench.SelectedValidationSetSample), "Rendered command resets the filter and restores two-way selection"));
                list.SelectedIndex = 1;
                Settle();
                checks.Add(($"{language} view selection writes the existing owner", ReferenceEquals(workbench.SelectedValidationSetSample, list.Items[1]), "ListBox selection -> Workbench -> Workspace -> Review owner"));
                var row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
                var bad = Descendants<RadioButton>(row).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedBad");
                Activate(bad);
                var selected = workbench.SelectedValidationSetSample!;
                row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(selected);
                var roles = Descendants<RadioButton>(row).ToArray();
                checks.Add(($"{language} rendered role command refreshes exclusive flags", selected.IsBad && roles.Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedBad").IsChecked == true
                    && roles.Count(button => button.IsChecked == true) == 1 && workbench.ValidationSetBadCount == 1 && selected.Status == "Pending" && !workbench.IsValidationSetRunning
                    && workbench.ValidationSetSamples.All(sample => sample.Status == "Pending" && sample.Steps.Count == 0),
                    "Rendered NG command -> canonical role replacement -> new row bindings; all rows remain Pending with no execution evidence. Existing role-change diagnostic log is permitted."));
                Activate(Descendants<ToggleButton>(row).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedHeldOut"));
                selected = workbench.SelectedValidationSetSample!;
                row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(selected);
                roles = Descendants<RadioButton>(row).ToArray();
                checks.Add(($"{language} held-out use remains independent from expected NG", selected.IsHeldOut && selected.IsBad && roles.Count(button => button.IsChecked == true) == 1
                    && workbench.ValidationSetHeldOutCount == 1 && workbench.ValidationSetHeldOutCompletedCount == 0 && workbench.ValidationSetAdjustmentCount == 2,
                    "Expected NG retained; held-out use is independent; no execution occurred"));
                foreach (var (width, height) in new[] { (272d, 668d), (304d, 934d) })
                {
                    Settle(width, height);
                    var guide = (TextBlock)view.FindName("SamplesMeaningGuide");
                    var footer = guide.TransformToAncestor(view).TransformBounds(new Rect(guide.RenderSize));
                    var filters = Descendants<RadioButton>(view).Where(button => AutomationProperties.GetAutomationId(button).StartsWith("ValidationSamplesFilter", StringComparison.Ordinal)).ToArray();
                    checks.Add(($"{language} {width}px layout keeps six filters and rows reachable", filters.Length == 6 && filters.All(button => button.ActualHeight == 24)
                        && ((Border)view.FindName("SamplesHeader")).ActualHeight == 34 && list.ActualHeight >= 100 && footer.Bottom <= view.ActualHeight,
                        $"listHeight={list.ActualHeight};footer={footer}"));
                    var summarySurface = Descendants<Border>(view).Single(border => AutomationProperties.GetAutomationId(border) == "ValidationSamplesSummary");
                    var filterPanel = Descendants<WrapPanel>(view).Single(panel => AutomationProperties.GetAutomationId(panel) == "ValidationSampleRunFilters");
                    var sampleRow = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(workbench.SelectedValidationSetSample);
                    var rowSurface = Descendants<Border>(sampleRow).Single(border => border.Name == "SampleSurface");
                    var heldChoice = Descendants<ToggleButton>(sampleRow).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedHeldOut");
                    var heldSurface = Descendants<Border>(heldChoice).First();
                    var rowTitle = Descendants<TextBlock>(sampleRow).Single(text => System.Windows.Data.BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == "FileName");
                    var rowDescription = Descendants<TextBlock>(sampleRow).Single(text => System.Windows.Data.BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path.Path == "DescriptionText");
                    Rect Bounds(FrameworkElement element) => element.TransformToAncestor(view).TransformBounds(new Rect(element.RenderSize));
                    var summaryBounds = Bounds(summarySurface);
                    var filterBounds = Bounds(filterPanel);
                    var listBounds = Bounds(list);
                    var header = (Border)view.FindName("SamplesHeader");
                    var dragSurface = (AvalonDock.Controls.AnchorablePaneTitle)view.FindName("SamplesHeaderDragSurface");
                    var grip = (FrameworkElement)view.FindName("SamplesHeaderGrip");
                    var headerIcon = (global::Wpf.Ui.Controls.SymbolIcon)view.FindName("SamplesHeaderValidationIcon");
                    var headerTitle = (TextBlock)view.FindName("SamplesHeaderTitle");
                    var headerCount = (TextBlock)view.FindName("SamplesHeaderCount");
                    var addSamples = Button("AddValidationSamples");
                    var info = (global::Wpf.Ui.Controls.SymbolIcon)view.FindName("SamplesHeldOutInfo");
                    var pause = Descendants<global::Wpf.Ui.Controls.SymbolIcon>(heldChoice).Single();
                    var pin = Button("ValidationSamplesAutoHide");
                    var menu = Descendants<Menu>(header).Single();
                    bool Hits(FrameworkElement target, Point point)
                    {
                        // Mounted visual hit support only; no native InputHitTest proof without a desktop PresentationSource.
                        DependencyObject? visualHit = null;
                        VisualTreeHelper.HitTest(view,
                            visual => visual is UIElement element && !element.IsHitTestVisible ? HitTestFilterBehavior.ContinueSkipSelfAndChildren : HitTestFilterBehavior.Continue,
                            result => { visualHit = result.VisualHit; return HitTestResultBehavior.Stop; }, new PointHitTestParameters(point));
                        for (var hit = visualHit; hit is not null; hit = VisualTreeHelper.GetParent(hit))
                            if (ReferenceEquals(hit, target)) return true;
                        return false;
                    }
                    Point Center(FrameworkElement element) { var bounds = Bounds(element); return new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2); }
                    var headerActions = Descendants<FrameworkElement>(header).Where(element => element is ButtonBase or Menu && element.ActualWidth > 0).ToArray();
                    checks.Add(($"{language} {width}px sample geometry and mounted visual hit support preserve header and row access", summarySurface.CornerRadius == new CornerRadius(6)
                        && rowSurface.CornerRadius == new CornerRadius(4) && heldSurface.CornerRadius == new CornerRadius(5)
                        && Math.Abs(filterBounds.Top - summaryBounds.Bottom - 14) < 0.1 && Math.Abs(listBounds.Top - filterBounds.Bottom - 4) < 0.1
                        && Math.Abs(sampleRow.ActualHeight - 60) < 0.1 && rowTitle.LineHeight == 18.125 && rowDescription.LineHeight == 16.675
                        && rowTitle.LineStackingStrategy == LineStackingStrategy.BlockLineHeight && rowDescription.LineStackingStrategy == LineStackingStrategy.BlockLineHeight
                        && header.ActualHeight == 34 && header.Padding == new Thickness(6, 0, 4, 0) && headerTitle.FontWeight.ToOpenTypeWeight() == 650
                        && grip.ActualWidth == 10 && grip.ActualHeight == 16 && dragSurface.IsEnabled && ReferenceEquals(dragSurface.Model, dockModel)
                        && headerIcon.Symbol == global::Wpf.Ui.Controls.SymbolRegular.ClipboardCheckmark16 && headerIcon.ActualWidth == 14 && headerIcon.ActualHeight == 14
                        && Bounds(dragSurface).Left == 6 && Bounds(dragSurface).Right <= Bounds(addSamples).Left && Bounds(dragSurface).Right >= Bounds(headerCount).Right
                        && Hits(dragSurface, Center(headerTitle)) && Hits(dragSurface, Center(headerCount))
                        && Hits(dragSurface, new Point(Bounds(dragSurface).Right - 2, header.ActualHeight / 2))
                        && Hits(addSamples, Center(addSamples)) && Hits(menu, Center(menu)) && Hits(pin, Center(pin))
                        && Bounds(grip).Right <= Bounds(headerIcon).Left && Bounds(headerIcon).Right <= Bounds(headerTitle).Left
                        && Bounds(headerTitle).Right <= Bounds(headerCount).Left && Bounds(headerCount).Right <= Bounds(addSamples).Left
                        && headerTitle.ActualWidth >= headerTitle.DesiredSize.Width - 0.1 && addSamples.ActualHeight == 26
                        && ((MenuItem)view.FindName("SampleAdditionalMenu")).ActualWidth == 24 && ((MenuItem)view.FindName("SampleAdditionalMenu")).ActualHeight == 24
                        && info.Symbol == global::Wpf.Ui.Controls.SymbolRegular.Info16 && info.ActualWidth == 14 && info.ActualHeight == 14 && !info.Focusable && info.ToolTip is string infoMeaning && infoMeaning.Length > 0 && VisualTreeHelper.GetParent(info) is StackPanel infoPanel && info.ActualHeight <= infoPanel.ActualHeight
                        && Bounds(info).Top >= Bounds(infoPanel).Top && Bounds(info).Bottom <= Bounds(infoPanel).Bottom
                        && pause.Symbol == global::Wpf.Ui.Controls.SymbolRegular.Pause16 && pause.ActualWidth == 14 && pause.ActualHeight == 14
                        && headerActions.Length >= 3 && headerActions.All(action => Bounds(action).Left >= 0 && Bounds(action).Right <= width + 0.1)
                        && !workbench.IsValidationSetRunning,
                        $"summary={summaryBounds};filters={filterBounds};list={listBounds};row={Bounds(sampleRow)};title={Bounds(rowTitle)};description={Bounds(rowDescription)};held={Bounds(heldChoice)};radii={summarySurface.CornerRadius}/{rowSurface.CornerRadius}/{heldSurface.CornerRadius};font={rowTitle.FontFamily.Source};lineHeights={rowTitle.LineHeight}/{rowDescription.LineHeight};header={Bounds(header)};grip={Bounds(grip)};dragSurface={Bounds(dragSurface)};gripModel={ReferenceEquals(dragSurface.Model, dockModel)};headerTitle={Bounds(headerTitle)};headerCount={Bounds(headerCount)};add={Bounds(addSamples)};titleWeight={headerTitle.FontWeight.ToOpenTypeWeight()};info={info.RenderSize};pause={pause.RenderSize};headerActions={headerActions.Length};windows={app.Windows.Count}. Mounted visual hit support, no native InputHitTest proof; native drag, button regression and screenshot acceptance remain separate."));
                    Color BrushColor(Brush brush) => ((SolidColorBrush)brush).Color;
                    var muted = BrushColor((Brush)view.FindResource("ThreeD.SecondaryTextBrush"));
                    var primary = BrushColor((Brush)view.FindResource("ThreeD.PrimaryTextBrush"));
                    var dim = BrushColor((Brush)view.FindResource("ThreeD.DataMutedTextBrush"));
                    var labels = Descendants<TextBlock>(summarySurface).Where(text => text.LineHeight == 23.2).ToArray();
                    var summaryValues = labels.SelectMany(text => text.Inlines.OfType<System.Windows.Documents.Run>()).Where(run => run.FontSize == 16).ToArray();
                    var errorCounts = Descendants<TextBlock>(summarySurface).Where(text => text.LineHeight == 17.4).ToArray();
                    var filterCounts = filters.SelectMany(filter => Descendants<TextBlock>(filter)).SelectMany(text => text.Inlines.OfType<System.Windows.Documents.Run>())
                        .Where(run => System.Windows.Data.BindingOperations.GetBinding(run, System.Windows.Documents.Run.TextProperty)?.Path.Path.StartsWith("ValidationSet", StringComparison.Ordinal) == true).ToArray();
                    var uncheckedFilterLabels = filters.Where(filter => filter.IsChecked != true).SelectMany(filter => Descendants<TextBlock>(filter))
                        .SelectMany(text => text.Inlines.OfType<System.Windows.Documents.Run>()).Where(run => !string.IsNullOrWhiteSpace(run.Text)
                            && System.Windows.Data.BindingOperations.GetBinding(run, System.Windows.Documents.Run.TextProperty)?.Path.Path.StartsWith("ValidationSet", StringComparison.Ordinal) != true).ToArray();
                    var uncheckedRoles = Descendants<RadioButton>(sampleRow).Where(role => role.IsChecked != true).ToArray();
                    var addChrome = (Border)addSamples.Template.FindName("ContentBorder", addSamples);
                    var pinChrome = (Border)pin.Template.FindName("ContentBorder", pin);
                    var ghostStyle = (Style)view.FindResource("SamplesHeaderGhostStyle");
                    checks.Add(($"{language} {width}px local normal chrome and typography", addChrome.Background is SolidColorBrush addFill && addFill.Color.A == 0
                        && addChrome.BorderBrush is SolidColorBrush addBorder && addBorder.Color.A == 0 && pinChrome.Background is SolidColorBrush pinFill && pinFill.Color.A == 0
                        && pinChrome.BorderBrush is SolidColorBrush pinBorder && pinBorder.Color.A == 0 && labels.Length == 4 && labels.All(label => BrushColor(label.Foreground) == muted)
                        && summaryValues.Length == 2 && summaryValues.All(value => value.FontWeight.ToOpenTypeWeight() == 650 && BrushColor(value.Foreground) == primary)
                        && errorCounts.Length == 3 && errorCounts.All(text => text.FontWeight.ToOpenTypeWeight() == 600)
                        && filterCounts.Length == 6 && filterCounts.All(count => count.FontWeight.ToOpenTypeWeight() == 650 && BrushColor(count.Foreground) == primary)
                        && uncheckedFilterLabels.Length == 5 && uncheckedFilterLabels.All(label => BrushColor(label.Foreground) == muted)
                        && uncheckedRoles.Length == 2 && uncheckedRoles.All(role => role.FontWeight.ToOpenTypeWeight() == 650 && BrushColor(role.Foreground) == dim)
                        && BrushColor(((MenuItem)view.FindName("SampleAdditionalMenu")).Foreground) == dim && BrushColor(pin.Foreground) == dim
                        && !ghostStyle.Setters.OfType<Setter>().Any(setter => setter.Property == Control.TemplateProperty) && ReferenceEquals(addSamples.Template, pin.Template),
                        $"labels={labels.Length}:{string.Join(',', labels.Select(label => label.Foreground))};summaryValues={summaryValues.Length}:650;vs2={errorCounts.Length}:600;chipCounts={filterCounts.Length}:650;filterLabels={string.Join(',', uncheckedFilterLabels.Select(label => label.Foreground))};roles={string.Join(',', uncheckedRoles.Select(role => role.Foreground))}:650;addChrome={addChrome.Background}/{addChrome.BorderBrush};pinChrome={pinChrome.Background}/{pinChrome.BorderBrush};nativeTemplateReused={ReferenceEquals(addSamples.Template, pin.Template)}. Normal mounted rendering only; hover/pressed/focus source contracts and actual pointer states remain separate."));
                    var capture = new System.Windows.Media.Imaging.RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
                    capture.Render(view);
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(capture));
                    using var imageStream = File.Create(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, $"samples-header-info-{language}-{width}.png"));
                    encoder.Save(imageStream);
                }
                var longTitle = Descendants<TextBlock>(list).Single(text => text.Text == Path.GetFileName(paths[2]));
                var identityTip = (ToolTip)longTitle.ToolTip;
                identityTip.PlacementTarget = longTitle;
                var identityText = (TextBlock)identityTip.Content;
                identityText.Measure(new Size(360, double.PositiveInfinity));
                checks.Add(($"{language} long source identity keeps a bounded row and full path", longTitle.TextTrimming == TextTrimming.CharacterEllipsis && identityText.Text == paths[2]
                    && identityText.TextWrapping == TextWrapping.Wrap && identityText.DesiredSize.Width <= 360 && identityText.DesiredSize.Height > 20,
                    $"Intentional filename ellipsis; full path wraps inside the tooltip: {identityText.DesiredSize}"));
                view.IsEnabled = false;
                Settle();
                row = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(workbench.SelectedValidationSetSample);
                var held = Descendants<ToggleButton>(row).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedHeldOut");
                checks.Add(($"{language} disabled selected role uses semantic colors", !held.IsEnabled && held.Foreground is SolidColorBrush brush
                    && brush.Color == ((SolidColorBrush)view.FindResource("ThreeD.DisabledBrush")).Color,
                    $"foreground={held.Foreground};selected={held.IsChecked}"));
                var disabledAdd = (global::Wpf.Ui.Controls.Button)Button("AddValidationSamples");
                var disabledPin = (global::Wpf.Ui.Controls.Button)Button("ValidationSamplesAutoHide");
                var disabledChrome = new[] { disabledAdd, disabledPin }.Select(button => (Border)button.Template.FindName("ContentBorder", button)).ToArray();
                checks.Add(($"{language} local disabled ghost chrome remains transparent", disabledChrome.All(chrome => chrome.Background is SolidColorBrush fill && fill.Color.A == 0
                    && chrome.BorderBrush is SolidColorBrush border && border.Color.A == 0) && disabledAdd.Foreground is SolidColorBrush disabledText
                    && disabledText.Color == ((SolidColorBrush)view.FindResource("ThreeD.DisabledBrush")).Color,
                    $"chrome={string.Join(';', disabledChrome.Select(chrome => $"{chrome.Background}/{chrome.BorderBrush}"))};foreground={disabledAdd.Foreground};native template children inspected; no pointer-down proof"));
                view.IsEnabled = true;
                Settle();
                Activate(Button("ValidationSamplesFilterHeldOut"));
                checks.Add(($"{language} recovery and held-out filter", list.Items.Count == 1 && ((ValidationSetSampleRow)list.Items[0]).IsHeldOut && !workbench.IsValidationSetRunning,
                    "Re-enable restores command eligibility; no automatic run"));
                var scrollingPaths = Enumerable.Range(1, 20).Select(index => Path.Combine(fixtureRoot, $"scroll-{index:00}.C3D")).ToArray();
                foreach (var path in scrollingPaths) File.WriteAllBytes(path, [1]);
                workbench.SetValidationSetSources(scrollingPaths);
                workbench.SetValidationSetFilterCommand.Execute("All");
                Settle();
                var scrollbar = Descendants<ScrollBar>(list).Single(bar => bar.Orientation == Orientation.Vertical && bar.ActualWidth > 0);
                var scrollbarBounds = scrollbar.TransformToAncestor(list).TransformBounds(new Rect(scrollbar.RenderSize));
                var renderedRoles = Descendants<RadioButton>(list).Where(button => button.ActualHeight > 0).ToArray();
                checks.Add(($"{language} scrolling does not cover role controls", scrollbar.ActualWidth == 15 && scrollbar.Template is not null && renderedRoles.Length > 0 && renderedRoles.All(button =>
                    button.TransformToAncestor(list).TransformBounds(new Rect(button.RenderSize)).Right <= scrollbarBounds.Left + 0.1),
                    $"roles={renderedRoles.Length};maxRight={renderedRoles.Max(button => button.TransformToAncestor(list).TransformBounds(new Rect(button.RenderSize)).Right)};scrollbar={scrollbarBounds};padding={list.Padding}"));
                var listScroll = Descendants<ScrollViewer>(list).Single();
                var track = Descendants<Track>(scrollbar).Single();
                listScroll.ScrollToVerticalOffset(2);
                Settle();
                checks.Add(($"{language} native scrollbar template survives local width override", scrollbar.ActualWidth == 15 && track.Thumb is { ActualHeight: > 0 }
                    && listScroll.VerticalOffset > 0 && !workbench.IsValidationSetRunning,
                    $"bar={scrollbar.RenderSize};track={track.RenderSize};thumb={track.Thumb?.RenderSize};offset={listScroll.VerticalOffset};native focus/template kept; in-process scroll API only, no mouse wheel/native focus proof"));
                view.DockPaneCommands = null;
                Settle();
                var releasedGrip = (AvalonDock.Controls.AnchorablePaneTitle)view.FindName("SamplesHeaderDragSurface");
                checks.Add(($"{language} released dock owner disables the native grip", releasedGrip.Model is null && !releasedGrip.IsEnabled,
                    "Clearing the existing owner clears the grip model and drag eligibility; no additional event or state owner"));
                view.DataContext = null;
            }
        }
        finally { OpenVisionLanguageService.SetLanguage(languageBefore, save: false); }
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
}

using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Presentation.Commands;
using OpenVisionLab.ThreeD.Shell.Dialogs;
using OpenVisionLab.ThreeD.Shell.ViewModels.Integration;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Integration;
using OpenVisionLab.ThreeD.Shell.Views.Recipe;
using OpenVisionLab.ThreeD.Shell.Views.Shell;

namespace OpenVisionLab.ThreeD.Shell;

internal static class PathInputVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var root = Path.ChangeExtension(Path.GetFullPath(reportPath), null);
        Directory.CreateDirectory(root);
        var baseline = Path.GetFileNameWithoutExtension(reportPath).EndsWith("-before", StringComparison.Ordinal);
        var lines = new List<string>
        {
            $"Assembly: {typeof(PathInputVerification).Assembly.Location}",
            $"Baseline: {baseline}",
            "Scope: in-process WPF rendering and programmatic binding/command checks; no Window, native dialog, physical pointer, native DPI or clipboard verification."
        };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}");
        }
        void Layout(FrameworkElement element, int width, int height)
        {
            // 첫 Measure가 접힌 자식을 미리 만들지 않도록 초기 Visibility Binding을 먼저 확정한다.
            Application.Current.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
            element.Measure(new Size(width, height));
            element.Arrange(new Rect(0, 0, width, height));
            element.UpdateLayout();
            Application.Current.Dispatcher.Invoke(DispatcherPriority.ContextIdle, new Action(() => { }));
            element.UpdateLayout();
        }
        void Capture(FrameworkElement content, int width, int height, string name)
        {
            var surface = new Border { Background = (Brush)Application.Current.FindResource("ThreeD.PanelBrush"), Padding = new Thickness(16), Child = content };
            Layout(surface, width, height);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(surface);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var path = Path.Combine(root, name + ".png");
            using (var output = File.Create(path)) encoder.Save(output);
            lines.Add($"Capture | {path} | {width}x{height} | Graphite | virtual96dpi | no native monitor");
            surface.Child = null;
        }

        var language = OpenVisionLanguageService.CurrentLanguage;
        ToolWorkbenchViewModel? workbench = null;
        ThreeDIntegrationViewModel? integration = null;
        var settingsPath = Path.Combine(root, "exchange-settings.json");
        var originalSettings = File.Exists(settingsPath) ? File.ReadAllBytes(settingsPath) : null;
        try
        {
            var longFolder = Path.Combine(root, "반도체 패키지 검사와 저장 경로", "같은 파일명을 구별할 수 있는 긴 작업 폴더", "원문 보존 확인");
            Directory.CreateDirectory(longFolder);
            var sourcePath = Path.Combine(longFolder, "두께 검사 입력.C3D");
            File.Copy(Path.Combine(Directory.GetCurrentDirectory(), "3D", "Samples", "ThicknessCouponV1", "thickness-coupon-v1.C3D"), sourcePath, true);
            workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-recipes.json"));
            workbench.BeginFirstRecipeSetup();
            workbench.FirstRecipeName = "경로 입력 원문 보존";
            workbench.FirstRecipeFolderPath = longFolder;
            workbench.FirstRecipeSourcePath = sourcePath;
            integration = new ThreeDIntegrationViewModel(() => null, settingsPath);
            integration.ExchangeRoot = longFolder;
            var logCount = workbench.RunLog.Count;

            foreach (var selectedLanguage in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
            {
                OpenVisionLanguageService.SetLanguage(selectedLanguage, save: false);
                foreach (var (width, height, size) in new[] { (1920, 1040, "wide"), (1280, 760, "compact") })
                {
                    var recipeView = new RecipeManagerView { DataContext = workbench };
                    Capture(recipeView, width, height, $"recipe-{size}-{selectedLanguage}");
                    var recipeInputs = Descendants<PathInput>(recipeView).ToArray();
                    Check($"{selectedLanguage}/{size} recipe uses expected path controls", recipeInputs.Length == (baseline ? 0 : 2));
                    var statusText = Descendants<TextBlock>(recipeView).Single(text => text.GetBindingExpression(TextBlock.TextProperty)?.ParentBinding.Path?.Path == nameof(ToolWorkbenchViewModel.FirstRecipeSetupStatus));
                    Check($"{selectedLanguage}/{size} valid recipe has no redundant ready panel", workbench.IsFirstRecipeSetupValid && string.IsNullOrEmpty(workbench.FirstRecipeSetupStatus) && statusText.Parent is Border { Visibility: Visibility.Collapsed });
                    var exchangeView = new ThreeDIntegrationExchangeView { DataContext = new { IntegrationExchange = integration } };
                    Capture(exchangeView, width, height, $"exchange-{size}-{selectedLanguage}");
                    Check($"{selectedLanguage}/{size} exchange uses expected path control", Descendants<PathInput>(exchangeView).Count() == (baseline ? 0 : 1));
                    foreach (var input in recipeInputs.Concat(Descendants<PathInput>(exchangeView)))
                    {
                        var editor = (TextBox)input.FindName("PathEditor");
                        var button = (System.Windows.Controls.Button)input.FindName("BrowseButton");
                        Check($"{input.InputAutomationId} keeps original text/tooltip and right browse button", editor.Text == input.Text && Equals(editor.ToolTip, input.Text) && button.TransformToAncestor(input).Transform(new Point()).X >= editor.ActualWidth && editor.ActualWidth > 40 && button.ActualWidth > 20);
                        var expectedSymbol = input.InputAutomationId == "FirstRecipeFolder" ? Wpf.Ui.Controls.SymbolRegular.Folder24 : Wpf.Ui.Controls.SymbolRegular.FolderOpen24;
                        var icon = ((Wpf.Ui.Controls.Button)button).Icon as Wpf.Ui.Controls.SymbolIcon;
                        var glyphVisible = icon is not null && Descendants<TextBlock>(icon).Any(text => !string.IsNullOrEmpty(text.Text));
                        lines.Add($"Icon | {selectedLanguage}/{size}/{input.InputAutomationId} | declared={input.GetValue(PathInput.BrowseSymbolProperty)} | actual={icon?.Symbol} | binding={icon?.GetBindingExpression(Wpf.Ui.Controls.SymbolIcon.SymbolProperty)?.Status} | glyph={glyphVisible}");
                        Check($"{selectedLanguage}/{size}/{input.InputAutomationId} preserves the original browse icon", Equals(input.GetValue(PathInput.BrowseSymbolProperty), expectedSymbol) && icon?.Symbol == expectedSymbol && glyphVisible);
                    }
                }
            }
            if (!baseline)
            {
                foreach (var selectedLanguage in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
                {
                    OpenVisionLanguageService.SetLanguage(selectedLanguage, save: false);
                    workbench.CancelFirstRecipeSetupCommand.Execute(null);
                    var transitionView = new RecipeManagerView { DataContext = workbench };
                    Layout(transitionView, 1520, 890);
                    workbench.BeginFirstRecipeSetup();
                    Capture(transitionView, 1520, 890, $"recipe-browse-to-empty-setup-{selectedLanguage}");
                    foreach (var input in Descendants<PathInput>(transitionView))
                    {
                        var button = (Wpf.Ui.Controls.Button)input.FindName("BrowseButton");
                        var expectedSymbol = input.InputAutomationId == "FirstRecipeFolder" ? Wpf.Ui.Controls.SymbolRegular.Folder24 : Wpf.Ui.Controls.SymbolRegular.FolderOpen24;
                        var icon = button.Icon as Wpf.Ui.Controls.SymbolIcon;
                        var glyphVisible = icon is not null && Descendants<TextBlock>(icon).Any(text => !string.IsNullOrEmpty(text.Text));
                        lines.Add($"Icon | {selectedLanguage}/browse-to-empty-setup/{input.InputAutomationId} | declared={input.GetValue(PathInput.BrowseSymbolProperty)} | actual={icon?.Symbol} | binding={icon?.GetBindingExpression(Wpf.Ui.Controls.SymbolIcon.SymbolProperty)?.Status} | glyph={glyphVisible}");
                        Check($"{selectedLanguage}/browse-to-empty-setup/{input.InputAutomationId} preserves the original browse icon", Equals(input.GetValue(PathInput.BrowseSymbolProperty), expectedSymbol) && icon?.Symbol == expectedSymbol && glyphVisible);
                    }
                }
                workbench.FirstRecipeName = "경로 입력 원문 보존";
                workbench.FirstRecipeFolderPath = longFolder;
                workbench.FirstRecipeSourcePath = sourcePath;
            }
            Check("rendering/restoration does not run or replace source", workbench.RunLog.Count == logCount && string.IsNullOrEmpty(workbench.Source.Path) && workbench.FirstRecipeSourcePath == sourcePath);

            if (!baseline)
            {
                var converter = new PathSummaryConverter();
                foreach (var (raw, expected) in new[]
                {
                    (@"C:\scans\part42\scan.C3D", @"scan.C3D — …\part42"),
                    (@"D:\recipes\current\", @"current — …\recipes"),
                    (@"\\server\share\lot\scan.C3D", @"scan.C3D — …\lot"),
                    ("scan.C3D", "scan.C3D"),
                    (@"C:\", @"C:\"),
                    ("", "")
                }) Check($"path summary: {raw}", Equals(converter.Convert(raw, typeof(string), null, CultureInfo.InvariantCulture), expected));
                Check("summary cannot write into the raw binding", ReferenceEquals(converter.ConvertBack("short", typeof(string), null, CultureInfo.InvariantCulture), Binding.DoNothing));

                var source = new TextBlock { Text = sourcePath };
                var browseCount = 0;
                var canBrowse = true;
                var command = new RelayCommand(_ => browseCount++, _ => canBrowse);
                var input = new PathInput { Height = 34, BrowseText = "찾아보기", BrowseCommand = command, InputAutomationId = "PathInputFixture" };
                input.SetBinding(PathInput.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = source, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
                Layout(input, 620, 34);
                var editor = (TextBox)input.FindName("PathEditor");
                var button = (System.Windows.Controls.Button)input.FindName("BrowseButton");
                editor.SetCurrentValue(TextBox.TextProperty, sourcePath + ".edited");
                editor.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Check("editor updates original two-way source without shortening", source.Text == sourcePath + ".edited" && input.Text == source.Text);
                source.Text = sourcePath;
                Layout(input, 620, 34);
                Check("external restoration reaches editor without replacing its binding", editor.Text == sourcePath && BindingOperations.IsDataBound(editor, TextBox.TextProperty) && BindingOperations.IsDataBound(input, PathInput.TextProperty));
                source.Text = Path.Combine(longFolder, new string('x', 160), "input.C3D");
                Capture(input, 360, 90, "path-long-parent");
                Check("a long parent does not hide the primary filename", ((TextBlock)input.FindName("PathSummary")).Text.StartsWith("input.C3D — ", StringComparison.Ordinal) && input.Text == source.Text);
                source.Text = sourcePath;
                Layout(input, 620, 34);
                var peer = new ButtonAutomationPeer(button);
                ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
                Layout(input, 620, 34);
                Check("browse button invokes existing command without changing path", browseCount == 1 && source.Text == sourcePath && ReferenceEquals(button.Command, command));
                canBrowse = false;
                command.RaiseCanExecuteChanged();
                Layout(input, 620, 34);
                Check("CanExecute disables browse", !button.IsEnabled);
                canBrowse = true;
                command.RaiseCanExecuteChanged();
                input.IsReadOnly = true;
                Layout(input, 620, 34);
                Check("read-only retains selectable raw text and disables browse", editor.IsReadOnly && editor.IsEnabled && !button.IsEnabled && editor.Text == sourcePath);
                Capture(input, 660, 90, "path-read-only");
                input.IsReadOnly = false;
                input.IsEnabled = false;
                Layout(input, 620, 34);
                Check("disabled retains raw text and full tooltip", !editor.IsEnabled && !button.IsEnabled && editor.Text == sourcePath && ToolTipService.GetShowOnDisabled(editor));
                Capture(input, 660, 90, "path-disabled");

                var selectedFolder = Path.Combine(root, "selected-exchange");
                Directory.CreateDirectory(selectedFolder);
                var dialog = new SelectionDialog { SelectedPath = selectedFolder };
                integration.SetDialogHost(dialog);
                var oldPath = integration.ExchangeRoot;
                integration.BrowseExchangeRootCommand.Execute(null);
                Check("cancel preserves current exchange path", integration.ExchangeRoot == oldPath && dialog.InitialDirectory == longFolder);
                dialog.Accept = true;
                integration.BrowseExchangeRootCommand.Execute(null);
                Check("selection applies the existing dialog result", integration.ExchangeRoot == selectedFolder);
                Check("path presentation does not write settings or recipe files", (originalSettings is null ? !File.Exists(settingsPath) : originalSettings.SequenceEqual(File.ReadAllBytes(settingsPath))) && string.IsNullOrEmpty(workbench.RecipePath));
                integration.SaveSetupCommand.Execute(null);
                var restored = new ThreeDIntegrationViewModel(() => null, settingsPath);
                try
                {
                    var restoredView = new ThreeDIntegrationExchangeView { DataContext = new { IntegrationExchange = restored } };
                    Layout(restoredView, 1280, 760);
                    var restoredInput = Descendants<PathInput>(restoredView).Single();
                    Check("saved path reopens as original text without scanning or execution", restored.ExchangeRoot == selectedFolder && restoredInput.Text == selectedFolder && restored.Transactions.Count == 0 && string.IsNullOrEmpty(restored.SelectedInputArtifactPath));
                }
                finally
                {
                    restored.DisposeAsync().AsTask().GetAwaiter().GetResult();
                }

                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                workbench.FirstRecipeFolderPath = Path.Combine(root, "missing-folder-not-created");
                var errorView = new RecipeManagerView { DataContext = workbench };
                Capture(errorView, 1280, 760, "recipe-missing-folder-Korean");
                Check("missing-folder error stays visible and prevents Create", !workbench.IsFirstRecipeSetupValid && workbench.FirstRecipeSetupStatus == workbench.Localization.FirstRecipeFolderMissing && Descendants<TextBlock>(errorView).Any(text => text.Text == workbench.FirstRecipeSetupStatus && text.Parent is Border { Visibility: Visibility.Visible }));
                workbench.FirstRecipeFolderPath = longFolder;
                var wrongExtension = sourcePath + ".txt";
                File.Copy(sourcePath, wrongExtension, true);
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                workbench.FirstRecipeSourcePath = wrongExtension;
                errorView = new RecipeManagerView { DataContext = workbench };
                Capture(errorView, 1280, 760, "recipe-wrong-extension-English");
                Check("wrong-extension error stays visible and prevents Create", !workbench.IsFirstRecipeSetupValid && workbench.FirstRecipeSetupStatus == workbench.Localization.FirstRecipeSourceMustBeC3D && Descendants<TextBlock>(errorView).Any(text => text.Text == workbench.FirstRecipeSetupStatus && text.Parent is Border { Visibility: Visibility.Visible }));
                workbench.FirstRecipeSourcePath = sourcePath;
                Check("valid path recovers after validation failures without running", workbench.IsFirstRecipeSetupValid && workbench.RunLog.Count == logCount && string.IsNullOrEmpty(workbench.Source.Path));
            }
            Check("in-process verification creates no desktop Window", Application.Current.Windows.Count == 0);
        }
        catch (Exception exception)
        {
            Check("path input verification exception", false);
            lines.Add(exception.ToString());
        }
        finally
        {
            workbench?.Dispose();
            integration?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            OpenVisionLanguageService.SetLanguage(language, save: false);
        }
        summary = $"Path input: {passed}/{total}";
        lines.Add(summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return total > 0 && passed == total;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class SelectionDialog : IThreeDIntegrationDialogHost
    {
        public bool Accept { get; set; }
        public string SelectedPath { get; set; } = string.Empty;
        public string? InitialDirectory { get; private set; }

        public bool TrySelectExchangeRoot(string? initialDirectory, string title, out string path)
        {
            InitialDirectory = initialDirectory;
            path = Accept ? SelectedPath : string.Empty;
            return Accept;
        }
    }
}

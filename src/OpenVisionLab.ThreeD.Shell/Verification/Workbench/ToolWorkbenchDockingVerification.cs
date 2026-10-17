using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Docking.Controls;
using OpenVisionLab.ThreeD.Shell.Layout;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Shell;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolWorkbenchDockingVerification
{
    private static readonly string[] WorkbenchContentIds =
    [
        "tool-library",
        "data-layers",
        "tool-inspector",
        "three-d-viewer",
        "evidence-workbench",
        "output-compare",
        "displayed-outputs",
        "linked-view",
        "height-profile",
        "fit-diagnostics",
        "intersection-evidence",
        "correspondence-evidence",
    ];

    private static readonly string[] CalibrationContentIds =
    [
        "calibration-explorer",
        "calibration-workspace",
        "calibration-inspector",
        "calibration-evidence",
    ];

    private const string ThicknessRecipeRelativePath =
        "3D/Samples/ThicknessCouponV1/inspection-recipe.ov3d-recipe.json";

    private static string GetReportDirectory(string reportPath) =>
        Path.GetDirectoryName(Path.GetFullPath(reportPath))!;



    public static bool VerifyKernelEditingLifecycle(string reportPath, out string summary)
    {
        var root = GetReportDirectory(reportPath);
        var lines = new List<string> { "Boundary|Scripted WPF controls/commands, not CU; selected teaching kernel lifecycle only, Graphite100%; no SDK Run." };
        var total = 0;
        var passed = 0;
        Window? host = null;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        var language = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
        try
        {
            OpenVisionLab.OpenVisionLanguageService.SetLanguage(OpenVisionLab.OpenVisionLanguage.Korean, save: false);
            var template = ToolRecipeDocumentStore.Load(Path.Combine(root, "native-group-fixture", "noise.ov3d-recipe.json"));
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Style = new Style(typeof(Window)), WindowStyle = WindowStyle.None, ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();host.Activate();
            void Settle() => host.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(host.UpdateLayout));
            foreach (var width in new[] { 1280, 1920 })
            foreach (var toolId in new[] { "filter", "remove-outlier-pixels" })
            {
                var key = toolId == "filter" ? "KernelSize" : "WindowSize";
                var tool = workbench.Tools.Single(item => item.Id == toolId);
                var step = new ToolRecipeStep("step.lifecycle", tool.Id, tool.Name, tool.MinimumInputCount, [], "output.lifecycle",
                    tool.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Name == key ? "4" : parameter.DefaultValue)).ToArray());
                var path = Path.Combine(root, $"{width}-{toolId}.ov3d-recipe.json");
                ToolRecipeDocumentStore.Save(path, template with { Steps = [step] });
                if (!workbench.TryOpenTeachingRecipe(path, out var message)) throw new InvalidOperationException(message);
                var original = File.ReadAllBytes(path);
                var view = new SelectedToolWorkspaceView { DataContext = workbench, Width = width == 1280 ? 290 : 320,
                    Height = width == 1280 ? 704 : 970, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
                host.Width = width;host.Height = width == 1280 ? 760 : 1040;host.Content = view;Settle();
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
                var grid = (OpenVisionLab.ThreeD.Shell.PropertyGrid.RecipeStepPropertyGridHost)view.FindName("StepPropertyGrid");
                ListBox Choices() => FindVisualDescendants<ListBox>(FindVisualDescendants<TextBox>(grid).Single(editor => AutomationProperties.GetAutomationId(editor) == key)).Single();
                string Stored() => workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == key).Value;
                void SelectFive()
                {
                    var choices = Choices();
                    Keyboard.Focus((ListBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(1));
                    choices.SetCurrentValue(Selector.SelectedValueProperty, "5");Settle();
                }
                void Click(string id)
                {
                    var button = FindVisualDescendants<ButtonBase>(view).Single(item => item.IsVisible && AutomationProperties.GetAutomationId(item) == id);
                    typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);Settle();
                }
                var tag = $"{width}/{toolId}";
                SelectFive();
                Check(tag + " rendered input reaches the draft before Apply", workbench.HasPendingStepParameterChanges && Stored() == "4", Choices().SelectedValue?.ToString() ?? "none");
                Click("DiscardSelectedToolParameters");
                Check(tag + " Cancel restores unsupported stored value without writing", !workbench.HasPendingStepParameterChanges && Choices().SelectedIndex == -1
                    && Stored() == "4" && !workbench.IsDirty && original.SequenceEqual(File.ReadAllBytes(path)), workbench.StepParameterEditStatus);
                SelectFive();Click("ApplySelectedToolParameters");
                Check(tag + " Apply commits the rendered value", Stored() == "5" && !workbench.HasPendingStepParameterChanges && workbench.HasSelectedStepParameterHistory && workbench.IsDirty, workbench.StepParameterEditStatus);
                Click("UndoSelectedToolParametersFooter");
                Check(tag + " Undo restores raw value and the unselected rendered state", Stored() == "4" && Choices().SelectedIndex == -1
                    && !workbench.HasPendingStepParameterChanges && !workbench.HasSelectedStepParameterHistory, workbench.StepParameterEditStatus);
                Check(tag + " save/reopen restores the unsupported value without executing", workbench.TrySaveTeachingRecipe(path, out message)
                    && workbench.TryOpenTeachingRecipe(path, out message) && Stored() == "4" && !workbench.IsDirty, message);Settle();
                Check(tag + " reopened unsupported value is still unselected", Choices().SelectedIndex == -1, Stored());
                SelectFive();Click("ApplySelectedToolParameters");
                Check(tag + " corrected save/reopen restores the exact value", workbench.TrySaveTeachingRecipe(path, out message)
                    && workbench.TryOpenTeachingRecipe(path, out message) && Stored() == "5" && !workbench.IsDirty && !workbench.HasPendingStepParameterChanges, message);Settle();
                Check(tag + " saved value binds back to the same choice", Choices().SelectedValue?.ToString() == "5"
                    && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")), Stored());
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(view).Bitmap, Path.Combine(root, $"{width}-{toolId}-restored.png"));
                host.Content = null;Settle();
            }
        }
        catch (Exception exception) { total++;lines.Add("FAIL | unexpected | " + exception); }
        finally { host?.Close();OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false); }
        summary = $"Kernel editing lifecycle verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    public static bool VerifyKernelSettingsRestoration(string reportPath, out string summary)
    {
        var root = GetReportDirectory(reportPath);
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Saved incomplete steps, rendered teaching/Lab editors and existing Apply/commit; no Computer Use or SDK execution." };
        var passed = 0;
        var total = 0;
        Window? host = null;
        var language = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            OpenVisionLab.OpenVisionLanguageService.SetLanguage(OpenVisionLab.OpenVisionLanguage.Korean, save: false);
            var template = ToolRecipeDocumentStore.Load(Path.Combine(root, "native-group-fixture", "noise.ov3d-recipe.json"));
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window
            {
                Style = new Style(typeof(Window)),
                WindowStyle = WindowStyle.None,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Left = area.Left,
                Top = area.Top,
                Width = 1280,
                Height = 760,
            };
            host.Show();host.Activate();
            void Settle() => host.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(host.UpdateLayout));
            foreach (var width in new[] { 1280, 1920 })
            foreach (var toolId in new[] { "filter", "remove-outlier-pixels" })
            foreach (var storedValue in new[] { "4", "invalid" })
            foreach (var lab in new[] { false, true })
            {
                var key = toolId == "filter" ? "KernelSize" : "WindowSize";
                var tag = $"{width}-{toolId}-{storedValue}-{(lab ? "lab" : "teaching")}";
                var tool = workbench.Tools.Single(item => item.Id == toolId);
                var step = new ToolRecipeStep(
                    "step.kernel", tool.Id, tool.Name, tool.MinimumInputCount, [], "output.kernel",
                    tool.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Name == key ? storedValue : parameter.DefaultValue)).ToArray());
                var recipePath = Path.Combine(root, tag + ".ov3d-recipe.json");
                ToolRecipeDocumentStore.Save(recipePath, template with { Steps = [step] });
                Check(tag + " opens an incomplete saved recipe", workbench.TryOpenTeachingRecipe(recipePath, out var message), message);
                workbench.SelectPipelineStep(step.Id);
                var view = new SelectedToolWorkspaceView
                {
                    DataContext = workbench,
                    IsLabPresentation = lab,
                    Width = width == 1280 ? 290 : 320,
                    Height = width == 1280 ? 704 : 970,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                };
                host.Width = width;host.Height = width == 1280 ? 760 : 1040;host.Content = view;Settle();
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
                var grid = (OpenVisionLab.ThreeD.Shell.PropertyGrid.RecipeStepPropertyGridHost)view.FindName("StepPropertyGrid");
                TextBox Editor() => FindVisualDescendants<TextBox>(grid).Single(editor => AutomationProperties.GetAutomationId(editor) == key);
                var editor = Editor();
                Check(tag + " opening preserves stored value and execution state", workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == key).Value == storedValue
                    && !workbench.IsDirty && !workbench.HasPendingStepParameterChanges && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")), $"editor={editor.Text};pending={workbench.HasPendingStepParameterChanges}");
                var choices = FindVisualDescendants<ListBox>(editor).SingleOrDefault();
                if (lab)
                    Check(tag + " keeps the Lab numeric editor", !grid.IsCompactPresentation && choices is null && editor.IsVisible, $"text={editor.Text}");
                else
                {
                    Check(tag + " leaves all supported choices unselected", choices is not null && choices.Items.Count == 3 && choices.SelectedIndex == -1, $"selection={choices?.SelectedValue};editor={editor.Text}");
                    Check(tag + " explains the invalid saved choice beside the field", FindVisualDescendants<TextBlock>(editor).Any(text => text.IsVisible && text.Text == "3·5·7 중에서 선택하세요."), "visible inline instruction, not a tooltip");
                }
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(view).Bitmap, Path.Combine(root, tag + "-before.png"));
                Check(tag + " rejects invalid Apply without replacing stored parameters", !workbench.TryApplySelectedStepParameterDraft(out message)
                    && workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == key).Value == storedValue && !workbench.IsDirty, message);
                Settle();editor = Editor();
                if (lab) { Keyboard.Focus(editor);editor.Text = "5"; }
                else
                {
                    choices = FindVisualDescendants<ListBox>(editor).Single();
                    Keyboard.Focus((ListBoxItem)choices.ItemContainerGenerator.ContainerFromIndex(1));
                    choices.SetCurrentValue(Selector.SelectedValueProperty, "5");
                }
                Settle();
                if (lab) grid.CommitPendingEdit(out _);
                Check(tag + " valid editor commit reaches the existing draft", (toolId == "filter" ? ((FilterStepProperties)workbench.SelectedStepPropertyDraft!).KernelSize : ((RemoveOutlierPixelsStepProperties)workbench.SelectedStepPropertyDraft!).WindowSize) == 5
                    && workbench.HasPendingStepParameterChanges && !workbench.IsDirty, $"editor={Editor().Text}");
                Check(tag + " valid retry applies without executing", grid.CommitPendingEdit(out message) && workbench.TryApplySelectedStepParameterDraft(out message)
                    && workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == key).Value == "5" && !workbench.HasPendingStepParameterChanges && workbench.IsDirty
                    && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")), message);
                Settle();editor = Editor();
                Check(tag + " recovery clears the inline choice warning", !FindVisualDescendants<TextBlock>(editor).Any(text => text.IsVisible && text.Text == "3·5·7 중에서 선택하세요."), $"editor={editor.Text}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(view).Bitmap, Path.Combine(root, tag + "-recovered.png"));
                host.Content = null;Settle();
            }
        }
        catch (Exception exception) { total++;lines.Add("FAIL | unexpected | " + exception); }
        finally { host?.Close();OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false); }
        summary = $"Kernel settings restoration verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    public static bool VerifyModernToolSettings(string reportPath, out string summary)
    {
        var root = GetReportDirectory(reportPath);
        var lines = new List<string> { "Boundary|Existing selected-tool View/WPG, rendered controls and normal commands; no Computer Use, SDK arithmetic or full-theme acceptance." };
        var passed = 0;
        var total = 0;
        Window? host = null;
        var language = OpenVisionLab.OpenVisionLanguageService.CurrentLanguage;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            OpenVisionLab.OpenVisionLanguageService.SetLanguage(OpenVisionLab.OpenVisionLanguage.Korean, save: false);
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            Check("modern fixture opens", workbench.TryOpenTeachingRecipe(Path.Combine(root, "native-group-fixture", "noise.ov3d-recipe.json"), out var message), message);
            var view = new SelectedToolWorkspaceView { DataContext = workbench, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Width = 290, Height = 704 };
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Content = view, Style = new Style(typeof(Window)), WindowStyle = WindowStyle.None, ShowInTaskbar = false, WindowStartupLocation = WindowStartupLocation.Manual, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();host.Activate();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
            void Settle()
            {
                host.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(host.UpdateLayout));
                view.UpdateLayout();
            }
            void Click(ButtonBase button)
            {
                typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);
                Settle();
            }
            ButtonBase Button(string id) => FindVisualDescendants<ButtonBase>(view).Single(button => button.IsVisible && AutomationProperties.GetAutomationId(button) == id);
            var scroll = (ScrollViewer)view.FindName("SelectedToolScrollViewer");
            var propertyGrid = (OpenVisionLab.ThreeD.Shell.PropertyGrid.RecipeStepPropertyGridHost)view.FindName("StepPropertyGrid");
            TextBox Editor(string name) => FindVisualDescendants<TextBox>(propertyGrid).Single(editor => AutomationProperties.GetAutomationId(editor) == name);
            void ShowEditor(string name)
            {
                var pages = (ComboBox)propertyGrid.FindName("ParameterPageSelector");
                for (var page = 0; page < pages.Items.Count && !Editor(name).IsVisible; page++)
                {
                    pages.SetCurrentValue(Selector.SelectedIndexProperty, page);Settle();
                }
            }
            var originalSource = workbench.Source.Path;
            var originalRegions = workbench.GetCurrentAppliedTeachingSelections().ToArray();
            var runCount = workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
            foreach (var size in new[] { (width: 1280, height: 760, pane: 290d, body: 704d), (width: 1920, height: 1040, pane: 320d, body: 970d) })
            {
                host.Width = size.width;host.Height = size.height;view.Width = size.pane;view.Height = size.body;Settle();
                Check($"{size.width} simple core uses the existing draft", ReferenceEquals(propertyGrid.SelectedObject, workbench.SelectedStepPropertyDraft) && propertyGrid.PrimaryParametersOnly, "no duplicate parameter model");
                Check($"{size.width} core requires no scroll", scroll.ExtentHeight <= scroll.ViewportHeight + 1 && scroll.ExtentWidth <= scroll.ViewportWidth + 1, $"extent={scroll.ExtentWidth}x{scroll.ExtentHeight};viewport={scroll.ViewportWidth}x{scroll.ViewportHeight}");
                Check($"{size.width} only core noise parameters are visible", Editor("WindowSize").IsVisible && Editor("MaximumAbsoluteDeviation").IsVisible && !Editor("MinimumValidNeighbors").IsVisible, "advanced fields stay instantiated");
                var kernel = FindVisualDescendants<ListBox>(Editor("WindowSize")).Single();
                Check($"{size.width} kernel choices render3/5/7 and restore selection", kernel.Items.Count == 3 && string.Equals(kernel.SelectedValue?.ToString(), "3", StringComparison.Ordinal), $"selection={kernel.SelectedValue}");
                var choice = (ListBoxItem)kernel.ItemContainerGenerator.ContainerFromIndex(1);
                Keyboard.Focus(choice);kernel.SetCurrentValue(Selector.SelectedValueProperty, "5");Settle();
                Check($"{size.width} kernel choice writes draft without executing", ((RemoveOutlierPixelsStepProperties)workbench.SelectedStepPropertyDraft!).WindowSize == 5 && workbench.HasPendingStepParameterChanges && !workbench.IsDirty
                    && runCount == workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run"), "actual WPG text binding -> existing dirty command");
                Check($"{size.width} pending core keeps diagnostics/history reachable without scrolling", scroll.ExtentHeight <= scroll.ViewportHeight + 1 && Button("SelectedToolOpenDiagnostics").IsVisible && Button("SelectedToolOpenHistory").IsVisible, $"extent={scroll.ExtentHeight};viewport={scroll.ViewportHeight}");
                var threshold = Editor("MaximumAbsoluteDeviation");Keyboard.Focus(threshold);threshold.Text = "-1";Settle();
                Click(Button("SelectedToolOpenDiagnostics"));
                ShowEditor("MinimumValidNeighbors");
                Check($"{size.width} diagnostic page reveals advanced controls without replacing drafts", !propertyGrid.PrimaryParametersOnly && ReferenceEquals(propertyGrid.SelectedObject, workbench.SelectedStepPropertyDraft)
                    && ReferenceEquals(threshold, Editor("MaximumAbsoluteDeviation")) && threshold.Text == "-1" && Editor("MinimumValidNeighbors").IsVisible, "same control and same invalid text");
                Click(Button("SelectedToolBackToSettings"));
                Check($"{size.width} return retains invalid core editor and pending state", ReferenceEquals(threshold, Editor("MaximumAbsoluteDeviation")) && threshold.Text == "-1" && propertyGrid.PrimaryParametersOnly && workbench.HasPendingStepParameterChanges, "page changes never rebuild editors");
                Check($"{size.width} validation error keeps core actions on screen", scroll.ExtentHeight <= scroll.ViewportHeight + 1, $"extent={scroll.ExtentHeight};viewport={scroll.ViewportHeight}");
                Click(Button("ApplySelectedToolParameters"));
                Check($"{size.width} invalid value cannot change recipe", workbench.HasPendingStepParameterChanges && !workbench.IsDirty && workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "WindowSize").Value == "3", "normal Apply/commit failure path");
                Check($"{size.width} rejected draft exposes an explicit failure state", workbench.StepParameterDraftRejectionVersion != 0 && ((RadioButton)view.FindName("EditorDetailTab")).IsChecked == true, "normal command rejection, no status-string parsing");
                var rejectionVersion = workbench.StepParameterDraftRejectionVersion;
                Click(Button("SelectedToolBackToSettings"));Click(Button("ApplySelectedToolParameters"));
                Check($"{size.width} repeated Apply reopens the same rejected draft", workbench.StepParameterDraftRejectionVersion != rejectionVersion && ((RadioButton)view.FindName("EditorDetailTab")).IsChecked == true && threshold.Text == "-1" && !workbench.IsDirty, "no new edit or recipe mutation");
                ShowEditor("MaximumAbsoluteDeviation");
                threshold = Editor("MaximumAbsoluteDeviation");Keyboard.Focus(threshold);threshold.Text = "26";Settle();
                Check($"{size.width} editing clears the prior rejection without navigating", workbench.StepParameterDraftRejectionVersion == 0 && ((RadioButton)view.FindName("EditorDetailTab")).IsChecked == true, "new edit, same draft");
                if (((RadioButton)view.FindName("EditorDetailTab")).IsChecked != true) Click(Button("SelectedToolOpenDiagnostics"));
                ShowEditor("MinimumValidNeighbors");
                var advanced = Editor("MinimumValidNeighbors");Keyboard.Focus(advanced);advanced.Text = "0";Settle();
                Click(Button("SelectedToolBackToSettings"));
                Check($"{size.width} hidden advanced error remains pending", !advanced.IsVisible && advanced.Text == "0" && workbench.HasPendingStepParameterChanges && !workbench.IsDirty, "same WPG editor retained");
                Click(Button("ApplySelectedToolParameters"));
                Check($"{size.width} rejected hidden editor is revealed", ((RadioButton)view.FindName("EditorDetailTab")).IsChecked == true && advanced.IsVisible && workbench.HasPendingStepParameterChanges && !workbench.IsDirty, "explicit command rejection -> one-way binding -> presentation navigation");
                rejectionVersion = workbench.StepParameterDraftRejectionVersion;
                Click(Button("SelectedToolBackToSettings"));Click(Button("ApplySelectedToolParameters"));
                Check($"{size.width} repeated hidden range rejection remains visible", workbench.StepParameterDraftRejectionVersion != rejectionVersion && advanced.IsVisible && advanced.Text == "0" && !workbench.IsDirty, "normal Apply command repeated without changing the invalid value");
                Keyboard.Focus(advanced);advanced.Text = "3";Settle();
                Click(Button("ApplySelectedToolParameters"));
                Check($"{size.width} valid retry applies actual values without Run", !workbench.HasPendingStepParameterChanges && workbench.StepParameterDraftRejectionVersion == 0 && workbench.IsDirty
                    && workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "WindowSize").Value == "5"
                    && workbench.SelectedPipelineStep.Parameters.Single(parameter => parameter.Name == "MaximumAbsoluteDeviation").Value == "26", "canonical parameter list");
                Click(Button("UndoSelectedToolParametersFooter"));
                Check($"{size.width} footer Undo restores actual applied values", !workbench.HasPendingStepParameterChanges
                    && workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "WindowSize").Value == "3"
                    && workbench.SelectedPipelineStep.Parameters.Single(parameter => parameter.Name == "MaximumAbsoluteDeviation").Value == "25", "same existing Undo command");
                if (((RadioButton)view.FindName("EditorDetailTab")).IsChecked == true) Click(Button("SelectedToolBackToSettings"));
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(view).Bitmap, Path.Combine(root, $"modern-noise-{size.width}.png"));
                Check($"{size.width} edits preserve source/ROI and explicit execution", workbench.Source.Path == originalSource && workbench.GetCurrentAppliedTeachingSelections().SequenceEqual(originalRegions)
                    && runCount == workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run") && !workbench.IsSelectedStepPreviewRunning, "no new measurement call");
                Check($"{size.width} fresh reopen restores the existing draft", workbench.TryOpenTeachingRecipe(Path.Combine(root, "native-group-fixture", "noise.ov3d-recipe.json"), out message) && !workbench.IsDirty && !workbench.HasPendingStepParameterChanges, message);Settle();
            }
            Check("complex fixture opens", workbench.TryOpenTeachingRecipe(Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json"), out message), message);Settle();
            foreach (var size in new[] { (width: 1280, height: 760, pane: 290d, body: 704d), (width: 1920, height: 1040, pane: 320d, body: 970d) })
            {
                host.Width = size.width;host.Height = size.height;view.Width = size.pane;view.Height = size.body;Settle();
                foreach (var page in new[] { "EditorRegionTab", "EditorAllTab", "EditorJudgmentTab", "EditorDetailTab" })
                {
                    Click((RadioButton)view.FindName(page));
                    Check($"{size.width} {page} is reachable without executing", ((RadioButton)view.FindName(page)).IsChecked == true && !workbench.IsDirty && !workbench.HasPendingStepParameterChanges && !workbench.IsSelectedStepPreviewRunning, "presentation state only");
                    if (page != "EditorDetailTab") Check($"{size.width} {page} fits its declared pane", scroll.ExtentHeight <= scroll.ViewportHeight + 1, $"pane={view.Width}x{view.Height};extent={scroll.ExtentHeight};viewport={scroll.ViewportHeight}");
                }
            }
            Click(Button("SelectedToolBackToSettings"));
            Check("diagnostics returns to its last core page", ((RadioButton)view.FindName("EditorJudgmentTab")).IsChecked == true, "return state belongs only to this View");
        }
        catch (Exception exception) { total++;lines.Add("FAIL | unexpected | " + exception); }
        finally { host?.Close();OpenVisionLab.OpenVisionLanguageService.SetLanguage(language, save: false); }
        summary = $"Modern tool settings verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        Directory.CreateDirectory(root);File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    public static bool VerifyCommonHeader(string reportPath, out string summary) => VerifyCommonHeader(reportPath, true, out summary);

    public static bool VerifyCommonHeaderLayout(string reportPath, out string summary) => VerifyCommonHeader(reportPath, false, out summary);

    private static bool VerifyCommonHeader(string reportPath, bool verifyNativePointer, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var root = GetReportDirectory(reportPath);
        Directory.CreateDirectory(root);
        var lines = new List<string> { "HTML v3 common header focused verification", $"Generated: {DateTimeOffset.Now:O}" };
        var passed = 0;
        var total = 0;
        Window? host = null;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        void Settle() => Application.Current.Dispatcher.Invoke(() => host?.UpdateLayout(), System.Windows.Threading.DispatcherPriority.ContextIdle);
        void SettleNativeInput()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, host!.Dispatcher) { Interval = TimeSpan.FromMilliseconds(100) };
            EventHandler finishFrame = (_, _) => frame.Continue = false;
            timer.Tick += finishFrame;
            timer.Start();
            try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); timer.Tick -= finishFrame; }
            Settle();
        }
        void Capture(string name) => WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture((FrameworkElement)host!.Content).Bitmap, Path.Combine(root, name + ".png"));
        try
        {
            using var shell = new ShellMainWindowViewModel(recentRecipesPath: Path.Combine(root, "recent-recipes.json"), recentRunRecordsPath: Path.Combine(root, "recent-runs.json"), orderedRunRecordRoot: Path.Combine(root, "records"));
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            var header = new StudioHeaderView { DataContext = shell };
            var surface = new Grid { Background = (Brush)Application.Current.FindResource("ThreeD.AppBackgroundBrush") };
            surface.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            surface.RowDefinitions.Add(new RowDefinition());
            surface.Children.Add(header);
            _ = ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var workArea);
            host = new Window { Title = "3D common header verification", Content = surface, Width = 1280, Height = 760, WindowStyle = WindowStyle.None, Left = workArea.Left, Top = workArea.Top, WindowStartupLocation = WindowStartupLocation.Manual };
            System.Windows.Shell.WindowChrome.SetWindowChrome(host, new System.Windows.Shell.WindowChrome { CaptionHeight = 40, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false });
            host.Show();
            host.Activate();
            Settle();
            var title = FindVisualDescendants<StudioTitleBarView>(header).Single();
            var recipe = (Button)title.FindName("RecipeContextButton");
            var menu = FindVisualDescendants<Menu>(header).Single();
            var input = FindVisualDescendants<Wpf.Ui.Controls.Button>(header).Single(button => AutomationProperties.GetAutomationId(button) == "LoadTeachingInputButton");
            var save = FindVisualDescendants<Wpf.Ui.Controls.Button>(header).Single(button => AutomationProperties.GetAutomationId(button) == "SaveTeachingRecipeHeaderButton");
            var state = (TextBlock)title.FindName("RecipeStateStatusText");
            var nameText = (TextBlock)title.FindName("RecipeNameText");
            Check("Empty recipe uses actual unsaved state and zero tools", title.RecipeStateText == shell.Workbench.Localization.Unsaved && title.RecipeMetaText.Contains('0') && title.RecipeText == shell.Workbench.HeaderRecipeText, $"{title.RecipeText}/{title.RecipeMetaText}/{title.RecipeStateText}");
            Capture("header-empty-1280");
            foreach (var width in new[] { 1280, 1920, 1280 })
            {
                host.Width = width;
                host.Height = width == 1920 ? 1040 : 760;
                Settle();
                var expectedHeight = width == 1280 ? 36 : 40;
                Check($"Header height and brand at {width}", Math.Abs(header.ActualHeight - expectedHeight) < 0.1 && title.IsCompact == (width == 1280) && ((TextBlock)title.FindName("StudioBrandSuffix")).Visibility == (width == 1280 ? Visibility.Collapsed : Visibility.Visible), $"height={header.ActualHeight};compact={title.IsCompact}");
                Check($"All eight menus and actions fit at {width}", menu.Items.Count == 8 && menu.ActualHeight == 28 && menu.Items.OfType<MenuItem>().All(item => item.Padding.Left == (width == 1280 ? 7 : 9)) && input.ActualHeight == 26 && save.ActualHeight == 26 && recipe.ActualHeight == 30 && title.RecipeMaximumWidth == (width == 1280 ? 380 : 520), $"menu={menu.ActualWidth}x{menu.ActualHeight};recipe={recipe.ActualWidth}x{recipe.ActualHeight};actions={input.ActualHeight}/{save.ActualHeight}");
                var menuOrigin = menu.TranslatePoint(new Point(), header);
                var recipeOrigin = recipe.TranslatePoint(new Point(), header);
                Check($"Menu and recipe origins match rounded HTML geometry at {width}", Math.Abs(menuOrigin.X - (width == 1280 ? 144 : 212)) <= 1 && Math.Abs(menu.ActualWidth - (width == 1280 ? 356 : 388)) <= 1 && Math.Abs(recipeOrigin.X - (width == 1280 ? 514 : 614)) <= 1, $"menuX={menuOrigin.X};menuWidth={menu.ActualWidth};recipeX={recipeOrigin.X}");
                Check($"Window controls fill the header at {width}", new[] { "MinimizeWindowButton", "MaximizeRestoreWindowButton", "CloseWindowButton" }.All(id => title.FindName(id) is Button button && button.ActualWidth == 46 && Math.Abs(button.ActualHeight - (expectedHeight - 1)) < 0.1), $"header={expectedHeight}");
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
                Capture($"header-empty-{width}");
            }
            var fixture = CompletenessValidationVerificationFixtureFactory.Create(Path.Combine(root, "recipe-fixture"));
            Check("Open real recipe fixture", shell.Workbench.TryOpenTeachingRecipe(fixture.RecipePath, out var openMessage), openMessage);
            Settle();
            var sourceBefore = shell.Workbench.Source.Path;
            var runBefore = shell.Workbench.CurrentOrderedRunResult;
            var requests = 0;
            header.RecipeManagerRequested += (_, _) => requests++;
            var peer = new ButtonAutomationPeer(recipe);
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            Settle();
            Check("Recipe button forwards one request without mutating input or running", requests == 1 && shell.Workbench.Source.Path == sourceBefore && ReferenceEquals(runBefore, shell.Workbench.CurrentOrderedRunResult), $"requests={requests};source={sourceBefore}");
            Check("Header preserves input/save commands", ReferenceEquals(input.Command, shell.Workbench.LoadC3DSourceCommand) && ReferenceEquals(save.Command, shell.Workbench.SaveTeachingRecipeCommand) && input.ToolTip?.ToString() == "Ctrl+Shift+O", input.ToolTip?.ToString() ?? "missing");
            shell.Workbench.RecipeName = "검사기준-장착방향과표면상태를확인하는긴레시피-" + new string('X', 120);
            Settle();
            Check("Long dirty recipe trims its name while state and tooltip stay available", recipe.ActualWidth <= 380 && nameText.TextTrimming == TextTrimming.CharacterEllipsis && title.IsRecipeModified && state.Text == shell.Workbench.Localization.Modified && title.RecipeDetailText.Contains(shell.Workbench.RecipeName), $"width={recipe.ActualWidth};state={state.Text}");
            Capture("header-long-modified-1280");
            var sawSaving = false;
            var rejectedReentry = false;
            shell.Workbench.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != nameof(ToolWorkbenchViewModel.HeaderRecipeSaveStateText) || !shell.Workbench.IsRecipeSaving) return;
                Settle();
                sawSaving = state.Text == "저장 중" && !save.IsEnabled;
                rejectedReentry = !shell.Workbench.TrySaveTeachingRecipe(Path.Combine(root, "reentrant.json"), out _);
                Capture("header-saving-1280");
            };
            var blockedPath = Path.Combine(root, "blocked-save");
            Directory.CreateDirectory(blockedPath);
            Check("Actual save failure keeps the modified recipe", !shell.Workbench.TrySaveTeachingRecipe(blockedPath, out var failMessage) && shell.Workbench.IsDirty, failMessage);
            Settle();
            Check("Failure and saving state render without stale saved status", sawSaving && rejectedReentry && state.Text == "저장 실패" && title.IsRecipeSaveFailed && title.RecipeDetailText.Contains(failMessage), $"saving={sawSaving};reentry={rejectedReentry};state={state.Text}");
            Capture("header-save-failed-1280");
            var recoveredPath = Path.Combine(root, "recovered.ov3d-recipe.json");
            Check("Save recovery writes through the existing recipe store", shell.Workbench.TrySaveTeachingRecipe(recoveredPath, out var saveMessage) && File.Exists(recoveredPath), saveMessage);
            Settle();
            Check("Recovery clears failure/modified state without execution", !shell.Workbench.HasRecipeSaveFailure && !title.IsRecipeModified && state.Text == shell.Workbench.Localization.Saved && ReferenceEquals(runBefore, shell.Workbench.CurrentOrderedRunResult), state.Text);
            Capture("header-saved-recovered-1280");
            Check("Saved recipe round trip retains actual name and tool count", shell.Workbench.TryOpenTeachingRecipe(recoveredPath, out openMessage) && title.RecipeText == shell.Workbench.HeaderRecipeText, openMessage);
            Settle();
            recipe.Focus();
            Settle();
            Check("Recipe has keyboard focus", recipe.IsKeyboardFocused, Keyboard.FocusedElement?.GetType().Name ?? "none");
            Capture("header-keyboard-focus-1280");
            foreach (var button in verifyNativePointer ? new ButtonBase[] { recipe, input, save, (Button)title.FindName("MinimizeWindowButton"), (Button)title.FindName("MaximizeRestoreWindowButton"), (Button)title.FindName("CloseWindowButton") } : Array.Empty<ButtonBase>())
            {
                host.Activate();
                SettleNativeInput();
                if (!host.IsActive) throw new InvalidOperationException("Owned header test window could not be activated; pointer injection stopped.");
                var resetLocation = surface.PointToScreen(new Point(400, 150));
                ShellWindowNativeInterop.SetCursorPos((int)resetLocation.X, (int)resetLocation.Y);
                SettleNativeInput();
                var location = button.PointToScreen(new Point(button.ActualWidth / 2, button.ActualHeight / 2));
                ShellWindowNativeInterop.SetCursorPos((int)location.X, (int)location.Y);
                SettleNativeInput();
                var id = button.Name.Length > 0 ? button.Name : AutomationProperties.GetAutomationId(button);
                ShellWindowNativeInterop.GetCursorPos(out var cursor);
                var hit = host.InputHitTest(button.TranslatePoint(new Point(button.ActualWidth / 2, button.ActualHeight / 2), host));
                Check($"Native pointer hover {id}", host.IsActive && button.IsMouseOver, $"active={host.IsActive};windowState={host.WindowState};hover={button.IsMouseOver};target={location};cursor={cursor.X},{cursor.Y};hit={hit?.GetType().Name};directlyOver={Mouse.DirectlyOver?.GetType().Name};captured={Mouse.Captured?.GetType().Name};mouse={Mouse.GetPosition(button)}");
                var windowHandle = new System.Windows.Interop.WindowInteropHelper(host).Handle;
                if (!ShellWindowNativeInterop.IsForegroundWindow(windowHandle) || cursor.X != (int)location.X || cursor.Y != (int)location.Y || !button.IsMouseOver)
                    throw new InvalidOperationException("Owned control was not under the active pointer; global pointer injection stopped.");
                if (id == "CloseWindowButton")
                {
                    var chrome = (Border)button.Template.FindName("Chrome", button);
                    Check("Close hover renders the prototype warning surface", chrome.Background is SolidColorBrush brush && brush.Color == Color.FromRgb(196, 43, 28), chrome.Background.ToString());
                }
                Capture("header-hover-" + id);
                try
                {
                    ShellWindowNativeInterop.SendLeftButtonDown();
                    SettleNativeInput();
                    Check($"Held native pointer press {id}", button.IsPressed, $"pressed={button.IsPressed}");
                    Capture("header-pressed-" + id);
                    var outside = surface.PointToScreen(new Point(400, 150));
                    ShellWindowNativeInterop.SetCursorPos((int)outside.X, (int)outside.Y);
                    SettleNativeInput();
                }
                finally
                {
                    ShellWindowNativeInterop.SendLeftButtonUp();
                }
                SettleNativeInput();
                Check($"Pointer leave cancels click and restores {id}", !button.IsPressed && !button.IsMouseOver && host.IsVisible && host.WindowState == WindowState.Normal, $"pressed={button.IsPressed};hover={button.IsMouseOver};visible={host.IsVisible};windowState={host.WindowState}");
            }
            input.IsEnabled = false;
            Settle();
            Capture("header-disabled-1280");
            input.ClearValue(UIElement.IsEnabledProperty);
            var fileMenu = menu.Items.OfType<MenuItem>().First();
            fileMenu.IsSubmenuOpen = true;
            Settle();
            var popup = (Popup)fileMenu.Template.FindName("PART_Popup", fileMenu);
            Check("File menu opens below the whole header with existing commands", popup.IsOpen && popup.Placement == PlacementMode.Bottom && fileMenu.Items.OfType<MenuItem>().Any(item => ReferenceEquals(item.Command, shell.Workbench.LoadC3DSourceCommand) && item.InputGestureText == "Ctrl+Shift+O"), $"open={popup.IsOpen};placement={popup.Placement}");
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture((FrameworkElement)popup.Child).Bitmap, Path.Combine(root, "header-file-menu-popup.png"));
            fileMenu.IsSubmenuOpen = false;
            Settle();
            Check("Menu close preserves recipe/input/run", shell.Workbench.Source.Path == sourceBefore && ReferenceEquals(runBefore, shell.Workbench.CurrentOrderedRunResult), sourceBefore);
            var toolsMenu = menu.Items.OfType<MenuItem>().ElementAt(6);
            toolsMenu.IsSubmenuOpen = true;
            Settle();
            var toolsPopup = (Popup)toolsMenu.Template.FindName("PART_Popup", toolsMenu);
            var languageSelector = FindVisualDescendants<ComboBox>(toolsPopup.Child).Single();
            var originalLanguage = shell.SelectedLanguageOption;
            languageSelector.SelectedItem = shell.LanguageOptions.Single(option => option.Language == OpenVisionLanguage.English);
            Settle();
            Check("Rendered language selector updates the owner and header", shell.SelectedLanguageOption?.Language == OpenVisionLanguage.English && state.Text == "Saved", state.Text);
            toolsMenu.IsSubmenuOpen = false;
            Capture("header-English-1280");
            host.Width = 1920;
            host.Height = 1040;
            SettleNativeInput();
            Check("English Wide header preserves visible eight-menu command row", menu.Items.Count == 8 && header.ActualHeight == 40 && recipe.ActualWidth <= 520, $"menu={menu.ActualWidth};recipe={recipe.ActualWidth}");
            Capture("header-English-1920");
            languageSelector.SelectedItem = originalLanguage;
            host.Width = 1280;
            host.Height = 760;
            Settle();
            Check("Language and density restore through the rendered binding", ReferenceEquals(shell.SelectedLanguageOption, originalLanguage) && state.Text == shell.Workbench.Localization.Saved && title.IsCompact, state.Text);
            lines.Add(verifyNativePointer ? "Boundary | Native hover/held press use the existing Win32 smoke adapter; this is not Computer Use." : "Unverified | Global pointer checks were explicitly excluded from this layout/state route; use --verify-common-header on an available exclusive desktop. This pass does not satisfy pointer acceptance.");
            lines.Add("Boundary | Graphite native96DPI header-only harness. State/property/command checks are scripted, not Computer Use. Actual owner Recipe Center, input dialog cancel, drag/window controls, native125% and full-app consumer acceptance require separate evidence.");
        }
        catch (Exception exception)
        {
            Check("Focused verification completed without exception", false, exception.ToString());
        }
        finally
        {
            host?.Close();
        }
        summary = $"Common header verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }


    public static bool VerifyRecipeFlow(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var root = GetReportDirectory(reportPath);
        Directory.CreateDirectory(root);
        var lines = new List<string> { "HTML v3 RecipeChain focused verification — isolated WPF test window, no Computer Use", $"Generated: {DateTimeOffset.Now:O}" };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        Window? ownedHost = null;
        try
        {
            using var shell = new ShellMainWindowViewModel(recentRecipesPath: Path.Combine(root, "recent-recipes.json"), recentRunRecordsPath: Path.Combine(root, "recent-runs.json"), orderedRunRecordRoot: Path.Combine(root, "records"));
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            var vm = shell.Workbench;
            var view = new RecipeChainView { DataContext = vm, IsTeachingMode = true };
            _ = ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var workArea);
            ownedHost = new Window { Content = view, Width = 330, Height = 760, Left = workArea.Left, Top = workArea.Top, WindowStyle = WindowStyle.None, ShowActivated = false, ShowInTaskbar = false };
            ownedHost.Show();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownedHost, Path.Combine(root, "monitor-window.txt"));
            void Settle(double width, bool completeTransitions = false)
            {
                ownedHost.Width = Math.Max(500, width);
                view.Width = width;
                view.HorizontalAlignment = HorizontalAlignment.Left;
                for (var pass = 0; pass < 5; pass++)
                {
                    ownedHost.UpdateLayout();
                    view.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                }
                if (!completeTransitions) return;
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, view.Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
                EventHandler finishFrame = (_, _) => frame.Continue = false;
                timer.Tick += finishFrame;
                timer.Start();
                try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); timer.Tick -= finishFrame; }
                ownedHost.UpdateLayout();
            }
            void Capture(string name) => WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(view).Bitmap, Path.Combine(root, name + ".png"));
            var list = (ListBox)view.FindName("RecipeStepList");
            var viewport = (ScrollViewer)view.FindName("FlowGraphViewport");
            var run = (Button)view.FindName("TeachingRunAllButton");
            var add = (Button)view.FindName("RecipeFlowAddTool");
            Settle(330);
            Check("DataContext and existing Add/Run contracts", ReferenceEquals(add.Command, vm.OpenToolLibraryCommand) && ReferenceEquals(run.Command, vm.RunTeachingRecipeCommand), $"add={add.Command};run={run.Command}");
            Check("open supported Thickness recipe", vm.TryOpenTeachingRecipe(Path.Combine(FindRepositoryRoot(), ThicknessRecipeRelativePath), out var openMessage) && vm.PipelineSteps.Count == 8, openMessage);
            var fixtureRoot = Path.Combine(root, "fixture");
            Directory.CreateDirectory(fixtureRoot);
            var sampleRoot = Path.Combine(FindRepositoryRoot(), "3D", "Samples", "ThicknessCouponV1");
            var fixtureRecipe = Path.Combine(fixtureRoot, "inspection-recipe.ov3d-recipe.json");
            File.Copy(Path.Combine(sampleRoot, "inspection-recipe.ov3d-recipe.json"), fixtureRecipe, overwrite: true);
            File.Copy(Path.Combine(sampleRoot, "thickness-coupon-v1.C3D"), Path.Combine(fixtureRoot, "thickness-coupon-v1.C3D"), overwrite: true);
            Check("open isolated D-drive source recipe", vm.TryOpenTeachingRecipe(fixtureRecipe, out var fixtureMessage), fixtureMessage);
            for (var pass = 0; pass < 40 && vm.SourceQuality.PreviewFrame is null; pass++) Settle(330, completeTransitions: true);
            foreach (var layout in new[] { (Width: 262d, Card: 226d), (Width: 330d, Card: 294d) })
            {
                Settle(layout.Width);
                var geometryInputCard = (Border)view.FindName("RecipeInputNode");
                var geometryGroupCard = FindVisualDescendants<Border>(view).Single(border => border.Name == "FlowRepeatGroupCard" && border.IsVisible);
                var geometryHeaderToggle = FindVisualDescendants<ToggleButton>(geometryGroupCard).Single(toggle => toggle.Name == "NodeHeaderToggle");
                var geometryExpandGlyph = FindVisualDescendants<Wpf.Ui.Controls.SymbolIcon>(geometryHeaderToggle).Single(icon => icon.Name == "ExpandGlyph");
                var geometryInputOrigin = geometryInputCard.TranslatePoint(new Point(), view);
                var geometryGroupOrigin = geometryGroupCard.TranslatePoint(new Point(), view);
                Check($"HTML flow card geometry and typography at {layout.Width}", Math.Abs(geometryInputCard.ActualWidth - layout.Card) < .25
                    && Math.Abs(geometryGroupCard.ActualWidth - layout.Card) < .25 && Math.Abs(geometryInputOrigin.X - 22) < .25 && Math.Abs(geometryGroupOrigin.X - 22) < .25
                    && Math.Abs(geometryHeaderToggle.ActualWidth - 22) < .25 && Math.Abs(geometryHeaderToggle.ActualHeight - 22) < .25
                    && view.FontSize == 13 && view.FontFamily.Source == "Segoe UI Variable Text, Segoe UI, Malgun Gothic"
                    && geometryExpandGlyph.Foreground is SolidColorBrush geometryGlyphBrush && geometryGlyphBrush.Color == Color.FromRgb(139, 152, 166),
                    $"input={geometryInputCard.ActualWidth};group={geometryGroupCard.ActualWidth};left={geometryInputOrigin.X}/{geometryGroupOrigin.X};toggle={geometryHeaderToggle.ActualWidth}x{geometryHeaderToggle.ActualHeight};font={view.FontFamily.Source}/{view.FontSize};glyph={geometryExpandGlyph.Foreground}");
            }
            Settle(330);
            Check("read-only flow projection preserves saved state", !vm.IsDirty, $"dirty={vm.IsDirty}");
            Check("actual source preview ready", vm.SourceQuality.PreviewFrame is not null, vm.SourceQuality.State);
            Check("source preview belongs to its port", vm.PipelineSteps[0].FlowInputs[0].PreviewFrame is not null
                && ReferenceEquals(vm.PipelineSteps[0].FlowInputs[0].PreviewFrame, vm.SourceQuality.PreviewFrame), vm.PipelineSteps[0].InputEntityIdsText);
            Check("ROI preview retains actual selection identity", vm.PipelineSteps[0].FlowInputs.Skip(1).All(input => input.HasSelectionPreview
                && input.Selection?.Id == input.EntityId && input.SelectionPreviewWidth > 0 && input.SelectionPreviewWidth <= 64), vm.PipelineSteps[0].InputEntityIdsText);
            Check("declared output has no fabricated thumbnail", vm.PipelineSteps.All(step => !step.HasOutputPreview), "No explicit execution yet");
            var orderedStatus = FindVisualDescendants<TextBlock>(view).Single(text => BindingOperations.GetBinding(text, TextBlock.TextProperty)?.Path?.Path == nameof(ToolWorkbenchViewModel.OrderedRunStatus));
            var actionsBeforeStatusRoundTrip = vm.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
            vm.MoveSelectedStepDownCommand.Execute(null);
            Settle(262);
            Check("dirty flow status agrees with disabled full Run", vm.IsDirty && !run.IsEnabled && orderedStatus.Text == "실행 불가", $"dirty={vm.IsDirty};enabled={run.IsEnabled};status={orderedStatus.Text}");
            Capture("flow-run-status-dirty");
            vm.MoveSelectedStepUpCommand.Execute(null);
            Check("save supported recipe after order restoration", vm.TrySaveTeachingRecipe(fixtureRecipe, out var statusSaveMessage), statusSaveMessage);
            Settle(262);
            Check("saved flow status agrees with enabled full Run without execution", !vm.IsDirty && run.IsEnabled && orderedStatus.Text == "준비"
                && vm.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run") == actionsBeforeStatusRoundTrip,
                $"dirty={vm.IsDirty};enabled={run.IsEnabled};status={orderedStatus.Text}");
            Capture("flow-run-status-saved");
            var actions = vm.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
            var first = vm.PipelineSteps[0];
            var judgmentGlyph = (TextBlock)view.FindName("FlowJudgmentReadiness");
            var originalAcceptance = first.AcceptanceLimitsConfigured;
            foreach (var configured in new bool?[] { null, false, true })
            {
                first.AcceptanceLimitsConfigured = configured;
                Settle(262);
                var expectedGlyph = configured == false ? "!" : "✓";
                Check($"judgment readiness preserves configured={configured?.ToString() ?? "legacy null"}", judgmentGlyph.Text == expectedGlyph
                    && first.HasConfiguredAcceptanceLimits == (configured != false) && first.HasUnconfiguredAcceptanceLimits == (configured == false) && first.AcceptanceLimitsConfigured == configured,
                    $"glyph={judgmentGlyph.Text};limits={configured};dirty={vm.IsDirty}");
                Capture($"flow-judgment-{configured?.ToString().ToLowerInvariant() ?? "legacy"}");
            }
            first.AcceptanceLimitsConfigured = originalAcceptance;
            Check("restore acceptance fixture through explicit save", vm.TrySaveTeachingRecipe(fixtureRecipe, out var acceptanceRestoreMessage) && !vm.IsDirty, acceptanceRestoreMessage);
            Settle(262);
            var viewOptions = (ToggleButton)view.FindName("FlowViewOptionsToggle");
            var viewPopup = (Popup)view.FindName("FlowViewOptionsPopup");
            var viewToolbar = (Border)view.FindName("RecipeFlowToolbar");
            var zoomIn = (Button)view.FindName("RecipeFlowZoomInButton");
            var resetView = (Button)view.FindName("RecipeFlowResetButton");
            foreach (var width in new[] { 262d, 330d })
            {
                Settle(width);
                viewOptions.IsChecked = true;
                Settle(width, completeTransitions: true);
                var viewPopupOrigin = viewToolbar.PointToScreen(new Point());
                var viewPopupBounds = new Rect(viewPopupOrigin, viewToolbar.RenderSize);
                Check($"view popup keeps existing controls in monitor bounds at {width}", viewPopup.IsOpen && viewToolbar.IsVisible
                    && ReferenceEquals(zoomIn.Command, view.IncreaseFlowZoomCommand) && ReferenceEquals(resetView.Command, view.ResetFlowViewCommand)
                    && workArea.Contains(viewPopupBounds.Left, viewPopupBounds.Top) && workArea.Contains(viewPopupBounds.Right, viewPopupBounds.Bottom),
                    $"bounds={viewPopupBounds};work={workArea};open={viewPopup.IsOpen}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(viewToolbar).Bitmap, Path.Combine(root, $"flow-view-popup-{width:0}.png"));
                viewOptions.IsChecked = false;
                Settle(width);
            }
            viewOptions.IsChecked = true;
            Settle(262);
            typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(zoomIn, null);
            Check("view popup zoom changes presentation", Math.Abs(view.FlowZoom - 1.1d) < .001, view.FlowZoom.ToString(CultureInfo.InvariantCulture));
            typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(resetView, null);
            var escapeEvent = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(view)!, 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent };
            viewToolbar.RaiseEvent(escapeEvent);
            Settle(262);
            Check("view popup Escape closes and restores zoom without recipe execution", !viewPopup.IsOpen && viewOptions.IsChecked == false
                && Math.Abs(view.FlowZoom - 1d) < .001 && !vm.IsDirty
                && vm.RunLog.Count(entry => entry.Category is "Preview" or "Publish" or "Run") == actions,
                $"open={viewPopup.IsOpen};zoom={view.FlowZoom};dirty={vm.IsDirty};handled={escapeEvent.Handled}");
            var addAfterSteps = FindVisualDescendants<Button>(view).Single(button => AutomationProperties.GetAutomationId(button) == "RecipeFlowAddAfterSteps");
            var addOutline = FindVisualDescendants<System.Windows.Shapes.Rectangle>(addAfterSteps).Single(shape => shape.Name == "AddOutline");
            Check("transparent dashed Add preserves catalog command", addAfterSteps.Background is SolidColorBrush addBackground && addBackground.Color.A == 0
                && addOutline.StrokeDashArray.Count == 2 && ReferenceEquals(addAfterSteps.Command, vm.OpenToolLibraryCommand),
                $"dash={string.Join(',', addOutline.StrokeDashArray)};height={addAfterSteps.ActualHeight}");
            var nextIssue = (Button)view.FindName("FlowNextIssueButton");
            Check("next issue respects existing disabled navigation", ReferenceEquals(nextIssue.Command, vm.NextRecipeHealthIssueCommand)
                && nextIssue.IsEnabled == vm.CanNavigateNextRecipeHealthIssue, $"enabled={nextIssue.IsEnabled};detail={vm.NextRecipeHealthIssueDetail}");
            var originalState = first.State;
            var routes = string.Join('|', vm.PipelineSteps.Select(step => step.InputEntityIdsText + ">" + step.OutputEntityId));
            Settle(262);
            var qualityBadge = (Button)view.FindName("SourceQualityBadge");
            var sourcePortText = (TextBlock)((Button)view.FindName("SourceOutputPort")).Content;
            Check("source consumer count uses actual step routes", vm.SourceConsumerCount == 8 && sourcePortText.Text.EndsWith("8", StringComparison.Ordinal), sourcePortText.Text);
            Check("quality badge preserves existing command", ReferenceEquals(qualityBadge.Command, vm.SelectSourceQualityCommand) && qualityBadge.IsEnabled, qualityBadge.Command?.ToString() ?? "none");
            typeof(Button).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(qualityBadge, null);
            Settle(262);
            Check("quality badge opens existing workspace without execution", vm.IsSourceQualityWorkspaceVisible && vm.RunLog.Count(log => log.Category is "Preview" or "Publish" or "Run") == actions && !vm.IsDirty, vm.SourceQuality.State);
            vm.SelectPipelineStepCommand.Execute(vm.PipelineSteps[1].Id);
            vm.SelectPipelineStepCommand.Execute(first.Id);
            Settle(262);
            Check("quality workspace returns through existing selection", !vm.IsSourceQualityWorkspaceVisible && ReferenceEquals(vm.SelectedPipelineStep, first) && !vm.IsDirty, first.Id);
            var calibrationBadge = (Button)view.FindName("SourceCalibrationBadge");
            var calibrationText = (TextBlock)view.FindName("SourceCalibrationText");
            Check("unasserted calibration stays unknown rather than calibrated", calibrationText.Text == "교정 정보 미확인"
                && vm.SourceQuality.MeasurementState != nameof(HeightMeasurementEvidenceState.CalibratedPhysical), vm.SourceQuality.MeasurementState);
            Check("calibration badge uses the existing quality command and accessible label", ReferenceEquals(calibrationBadge.Command, vm.SelectSourceQualityCommand)
                && AutomationProperties.GetName(calibrationBadge) == calibrationText.Text && calibrationBadge.IsEnabled, AutomationProperties.GetName(calibrationBadge));
            var originalQualityReport = vm.SourceQuality.Report!;
            var calibrationSnapshot = C3DHeightFieldSnapshot.CreateForVerification("source.flow-calibration", 2, 2, new double[] { 1, 2, 3, 4 }, unit: "mm", frameId: "frame.flow-calibration");
            var calibrationEvidence = HeightMeasurementEvidence.CalibratedPhysical("sensor-flow", "calibration-flow", calibrationSnapshot.FrameId, "Isolated verification calibration record.", DateTimeOffset.UtcNow.AddHours(1));
            vm.SourceQuality.SetReportForVerification(C3DSourceQualityAnalyzer.Create(calibrationSnapshot, measurementEvidence: calibrationEvidence, sourceSensorId: "sensor-flow"));
            Settle(262);
            Check("asserted calibration renders evidence label and source identities", calibrationText.Text == "교정 근거 있음"
                && AutomationProperties.GetHelpText(calibrationBadge).Contains("calibration-flow", StringComparison.Ordinal)
                && AutomationProperties.GetHelpText(calibrationBadge).Contains("sensor-flow", StringComparison.Ordinal), calibrationText.Text);
            Capture("flow-calibration-evidence-262");
            vm.SourceQuality.SetReportForVerification(originalQualityReport);
            Settle(262);
            Check("calibration recovery restores unknown without recipe writes or execution", calibrationText.Text == "교정 정보 미확인"
                && !vm.IsDirty && vm.RunLog.Count(log => log.Category is "Preview" or "Publish" or "Run") == actions, calibrationText.Text);
            foreach (var calibrationWidth in new[] { 262d, 330d })
            {
                Settle(calibrationWidth);
                Check($"calibration badge fits the source column at {calibrationWidth}", calibrationBadge.ActualWidth <= ((WrapPanel)view.FindName("SourceEvidenceBadges")).ActualWidth + .1
                    && calibrationText.ActualHeight >= calibrationText.DesiredSize.Height - .1 && calibrationText.TextTrimming == TextTrimming.None, $"badge={calibrationBadge.ActualWidth};text={calibrationText.ActualHeight};desired={calibrationText.DesiredSize}");
                Capture($"flow-calibration-unknown-{calibrationWidth:0}");
            }
            Settle(262);
            var sourceFrame = (Border)view.FindName("FlowSourceThumbnailChrome");
            Check("source thumbnail frame preserves reference size and hit region", Math.Abs(sourceFrame.ActualWidth - 84) < .5 && Math.Abs(sourceFrame.ActualHeight - 55) < .5 && sourceFrame.CornerRadius.TopLeft == 3 && !sourceFrame.IsHitTestVisible, $"frame={sourceFrame.ActualWidth}x{sourceFrame.ActualHeight}");
            Check("existing repeat identity forms one display group", vm.PipelineSteps.All(step => step.IsFlowRepeatGroupMember)
                && vm.PipelineSteps.Count(step => step.IsFlowRepeatGroupLead) == 1 && first.FlowRepeatGroupCount == 8, first.FlowRepeatGroupOrderRange);
            var groupCard = FindVisualDescendants<Border>(view).Single(border => AutomationProperties.GetAutomationId(border) == "TeachingThicknessGroup" && HasVisiblePresentationPath(border));
            var groupExpander = FindVisualDescendants<Expander>(groupCard).Single();
            var groupPlate = FindVisualDescendants<Border>(view).Single(border => border.Name == "FlowGroupBackplate" && HasVisiblePresentationPath(border));
            var plateOffset = groupPlate.TranslatePoint(new Point(), view) - groupCard.TranslatePoint(new Point(), view);
            Check("folded group back layer matches reference offset", Math.Abs(plateOffset.X - 4) < .5 && Math.Abs(plateOffset.Y - 4) < .5 && !groupPlate.IsHitTestVisible, plateOffset.ToString());
            var groupPort = FindVisualDescendants<System.Windows.Shapes.Ellipse>(groupCard).Single(port => port.Name == "GroupInputPortVisual");
            Check("connected group input renders filled", first.PrimaryFlowInput?.IsConnected == true && groupPort.Fill is SolidColorBrush groupFill && groupFill.Color == ((SolidColorBrush)view.FindResource("ThreeD.AccentBrush")).Color, first.PrimaryFlowInput?.EntityId ?? "none");
            Check("folded group replaces eight individual cards", !groupExpander.IsExpanded && list.Items.Cast<object>().Count(step => ((ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(step)).Visibility == Visibility.Visible) == 1
                && view.FlowConnectionCount == 8 && !vm.IsDirty, $"connections={view.FlowConnectionCount};dirty={vm.IsDirty}");
            Capture("flow-thickness-group-closed");
            groupExpander.IsExpanded = true;
            Settle(262);
            Check("expanded group exposes every original member", list.Items.Cast<object>().All(step => ((ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(step)).Visibility == Visibility.Visible)
                && vm.PipelineSteps.All(step => FindVisualDescendants<TextBlock>((ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(step)).Any(text => HasVisiblePresentationPath(text) && text.Text == step.ToolName)), vm.ThicknessRepeatGroupMembers);
            ownedHost.Height = 560;
            foreach (var memberLayoutWidth in new[] { 262d, 330d })
            {
                Settle(memberLayoutWidth);
                var compactMemberRows = FindVisualDescendants<Border>(list).Where(border => border.Name == "FlowRepeatGroupMemberRow" && HasVisiblePresentationPath(border)).ToArray();
                Check($"HTML repeat member rows retain 24px total height at {memberLayoutWidth}", compactMemberRows.Length == 8 && compactMemberRows.All(border => Math.Abs(border.ActualHeight - 24) < .25), string.Join(",", compactMemberRows.Select(border => border.ActualHeight)));
                viewport.ScrollToTop();
                Settle(memberLayoutWidth);
                var selectionBeforeWheel = vm.SelectedPipelineStep;
                var wheelRequest = new MouseWheelEventArgs(Mouse.PrimaryDevice, Environment.TickCount, -120) { RoutedEvent = Mouse.MouseWheelEvent };
                compactMemberRows[0].RaiseEvent(wheelRequest);
                Settle(memberLayoutWidth);
                Check($"Wheel from a member reaches the flow viewport at {memberLayoutWidth}", viewport.ScrollableHeight > 0 && viewport.VerticalOffset > 0 && wheelRequest.Handled && ReferenceEquals(selectionBeforeWheel, vm.SelectedPipelineStep) && !vm.IsDirty && !vm.IsSelectedStepPreviewRunning && !vm.IsOrderedRunRunning, $"offset={viewport.VerticalOffset};scrollable={viewport.ScrollableHeight};handled={wheelRequest.Handled};dirty={vm.IsDirty}");
            }
            ownedHost.Height = 760;
            list.SelectedIndex = 1;
            Settle(262);
            Check("group member selection uses original binding", ReferenceEquals(vm.SelectedPipelineStep, vm.PipelineSteps[1]), vm.SelectedPipelineStep?.Id ?? "none");
            Capture("flow-thickness-group-open");
            var groupMember = vm.PipelineSteps[1];
            var groupPortBefore = (groupMember.InputPortState, groupMember.InputPortDetail, groupMember.InputPortHasIssue,
                groupMember.OutputPortState, groupMember.OutputPortDetail, groupMember.OutputPortHasIssue);
            var longInputIssue = "입력 source.thickness-coupon-v1과 기준 영역 및 측정 영역을 확인하세요. " + string.Concat(Enumerable.Repeat("긴 입력 식별자와 단위 raw-height의 상세 복구 사유. ", 5));
            groupMember.UpdateFlowPortPresentation(ThreeDLocalization.Shared.FlowPortUnresolved, longInputIssue, true,
                groupPortBefore.OutputPortState, groupPortBefore.OutputPortDetail, groupPortBefore.OutputPortHasIssue);
            foreach (var errorWidth in new[] { 262d, 330d })
            {
                Settle(errorWidth);
                var memberItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(groupMember);
                var memberRow = FindVisualDescendants<Border>(memberItem).Single(border => border.Name == "FlowRepeatGroupMemberRow");
                var inputIssue = FindVisualDescendants<TextBlock>(memberRow).Single(text => text.Name == "FlowInputIssueDetail");
                var status = FindVisualDescendants<TextBlock>(memberRow).Single(text => text.Text == groupMember.InputPortState);
                Check($"group input failure keeps a compact status at {errorWidth}", groupMember.FlowNodeStatusLabel == groupMember.InputPortState
                    && status.ActualHeight <= 40 && status.Text != longInputIssue, $"status={status.Text};height={status.ActualHeight}");
                Check($"group full input reason wraps across the card at {errorWidth}", HasVisiblePresentationPath(inputIssue)
                    && inputIssue.Text == longInputIssue && inputIssue.TextWrapping == TextWrapping.Wrap && inputIssue.TextTrimming == TextTrimming.None
                    && inputIssue.ActualWidth > 150 && inputIssue.ActualHeight + inputIssue.Margin.Top + inputIssue.Margin.Bottom >= inputIssue.DesiredSize.Height - .1,
                    $"detail={inputIssue.ActualWidth}x{inputIssue.ActualHeight};desired={inputIssue.DesiredSize}");
                list.ScrollIntoView(groupMember);
                Capture($"flow-group-long-input-reason-{errorWidth:0}");
            }
            var longOutputIssue = "출력 derived.pad-thickness.02를 복구한 뒤 명시적으로 다시 실행하세요. " + longInputIssue;
            groupMember.UpdateFlowPortPresentation(groupPortBefore.InputPortState, groupPortBefore.InputPortDetail, false,
                ThreeDLocalization.Shared.FlowPortUnresolved, longOutputIssue, true);
            Settle(262);
            var outputMemberItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(groupMember);
            Check("group output failure preserves full reason outside the status column", groupMember.FlowNodeStatusLabel == groupMember.OutputPortState
                && FindVisualDescendants<TextBlock>(outputMemberItem).Any(text => HasVisiblePresentationPath(text) && text.Name == "FlowOutputIssueDetail"
                    && text.Text == longOutputIssue && text.ActualWidth > 150 && text.TextTrimming == TextTrimming.None), groupMember.OutputPortState);
            groupMember.UpdateFlowPortPresentation(groupPortBefore.InputPortState, groupPortBefore.InputPortDetail, groupPortBefore.InputPortHasIssue,
                groupPortBefore.OutputPortState, groupPortBefore.OutputPortDetail, groupPortBefore.OutputPortHasIssue);
            Settle(262);
            Check("group diagnostic recovery hides details and preserves recipe and execution", FindVisualDescendants<TextBlock>(outputMemberItem)
                .Where(text => text.Name is "FlowInputIssueDetail" or "FlowOutputIssueDetail").All(text => !HasVisiblePresentationPath(text))
                && !vm.IsDirty && vm.RunLog.Count(log => log.Category is "Preview" or "Publish" or "Run") == actions, $"dirty={vm.IsDirty};actions={actions}");
            // The diagnostic fixture returns the viewport to its initial state before the initial-selection cases.
            viewport.ScrollToTop();
            groupExpander.IsExpanded = false;
            Settle(262);
            Check("group fold preserves routes, results and execution state", routes == string.Join('|', vm.PipelineSteps.Select(step => step.InputEntityIdsText + ">" + step.OutputEntityId))
                && !vm.IsDirty && vm.RunLog.Count(log => log.Category is "Preview" or "Publish" or "Run") == actions, $"dirty={vm.IsDirty};connections={view.FlowConnectionCount}");
            var memberInputs = vm.PipelineSteps[1].InputEntityIdsText;
            vm.PipelineSteps[1].InputEntityIdsText = "missing.group.source;" + string.Join(';', vm.PipelineSteps[1].InputEntityIds.Skip(1));
            Settle(262);
            Check("different repeat inputs retain separate cards", vm.PipelineSteps.All(step => !step.IsFlowRepeatGroupMember), "No implicit group input policy");
            Check("source consumer count and port recover from missing route", vm.SourceConsumerCount == 7 && sourcePortText.Text.EndsWith("7", StringComparison.Ordinal) && vm.PipelineSteps[1].PrimaryFlowInput?.IsConnected == false, sourcePortText.Text);
            vm.PipelineSteps[1].InputEntityIdsText = memberInputs;
            Settle(262);
            Check("valid repeat inputs recover the display group", vm.PipelineSteps.All(step => step.IsFlowRepeatGroupMember) && view.FlowConnectionCount == 8, first.FlowRepeatGroupLabel);
            var middleName = vm.PipelineSteps[3].ToolName;
            vm.PipelineSteps[3].ToolName += " independent";
            Settle(262);
            Check("noncontiguous repeat members remain independent", vm.PipelineSteps.All(step => !step.IsFlowRepeatGroupMember), "Original order preserved");
            vm.PipelineSteps[3].ToolName = middleName;
            vm.SelectedPipelineStep = first;
            // Independent-card coverage deliberately changes names only in this owned test fixture.
            foreach (var step in vm.PipelineSteps) step.ToolName = $"두께 {step.Order}";
            var originalName = first.ToolName;
            foreach (var width in new[] { 262d, 330d, 262d })
            {
                Settle(width);
                Check($"actual flow viewport width {width}", Math.Abs(view.ActualWidth - width) < .5 && viewport.ViewportWidth <= width && viewport.ViewportWidth >= width - 20, $"view={view.ActualWidth};viewport={viewport.ViewportWidth}");
                var firstItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
                var secondItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
                var expander = FindVisualDescendants<Expander>(firstItem).Single(expander => expander.Name == "FlowNodeDetails");
                Check($"selected expansion and caption at {width}", expander.IsExpanded && FindVisualDescendants<TextBlock>(firstItem).Any(text => text.Text == first.ToolName), $"expanded={expander.IsExpanded};card={view.FlowCardWidth};viewport={viewport.ViewportWidth}");
                Check($"card inset and gap at {width}", Math.Abs(Canvas.GetLeft(firstItem) - 22) < 1 && Canvas.GetTop(secondItem) - Canvas.GetTop(firstItem) >= firstItem.ActualHeight + 25, $"x={Canvas.GetLeft(firstItem)};gap={Canvas.GetTop(secondItem) - Canvas.GetTop(firstItem) - firstItem.ActualHeight}");
                Check($"all supported inputs rendered at {width}", FindVisualDescendants<Button>(firstItem).Count(button => button.Name == "StepInputPort") == first.FlowInputs.Count, $"inputs={first.FlowInputs.Count}");
                var expectedEdges = vm.PipelineSteps.Sum(step => step.InputEntityIds.Count(id => id == vm.Source.Id || vm.PipelineSteps.Any(producer => producer.OutputEntityId == id)));
                Check($"saved connections at {width}", view.FlowConnectionCount == expectedEdges, $"actual={view.FlowConnectionCount};expected={expectedEdges}");
                var sourceCard = (FrameworkElement)view.FindName("RecipeInputNode");
                Check($"source card visible after first selection at {width}", sourceCard.TranslatePoint(new Point(), viewport).Y >= -1, $"offset={viewport.VerticalOffset};sourceY={sourceCard.TranslatePoint(new Point(), viewport).Y}");
                var collapsed = FindVisualDescendants<Expander>(secondItem).Single(expander => expander.Name == "FlowNodeDetails");
                Check($"collapsed compound input stays compact at {width}", !collapsed.IsExpanded && Math.Abs(secondItem.ActualHeight - 68) < .5
                    && FindVisualDescendants<TextBlock>(secondItem).Any(text => text.Text == vm.PipelineSteps[1].CollapsedPrimaryInputLabel), $"height={secondItem.ActualHeight};summary={vm.PipelineSteps[1].CollapsedInputSummary}");
                var inputPorts = FindVisualDescendants<Button>(firstItem).Where(button => button.Name == "StepInputPort").ToArray();
                var primaryPort = inputPorts[0];
                var primaryDot = FindVisualDescendants<System.Windows.Shapes.Ellipse>(primaryPort).Single(port => port.Name == "PortVisual");
                Check($"connected expanded input filled at {width}", first.PrimaryFlowInput?.IsConnected == true && primaryDot.Fill is SolidColorBrush inputFill && inputFill.Color == ((SolidColorBrush)view.FindResource("ThreeD.AccentBrush")).Color, $"id={first.PrimaryFlowInput?.EntityId};model={first.PrimaryFlowInput?.IsConnected};port={((ToolWorkbenchFlowInputItem)primaryPort.DataContext).IsConnected};enabled={primaryPort.IsEnabled};fill={primaryDot.Fill};border={primaryPort.BorderBrush};alignment={primaryPort.HorizontalContentAlignment};fillOwner={DependencyPropertyHelper.GetValueSource(primaryDot, System.Windows.Shapes.Shape.FillProperty).BaseValueSource}");
                var individualCard = FindVisualDescendants<Border>(firstItem).Single(border => border.Name == "FlowIndividualCard");
                var selectionRing = FindVisualDescendants<Border>(firstItem).Single(border => border.Name == "FlowIndividualSelectionRing");
                Check($"selected outside ring preserves card layout at {width}", HasVisiblePresentationPath(selectionRing) && Math.Abs(selectionRing.ActualWidth - individualCard.ActualWidth - 2) < .5
                    && !selectionRing.IsHitTestVisible && individualCard.Effect is System.Windows.Media.Effects.DropShadowEffect, $"card={individualCard.ActualWidth};ring={selectionRing.ActualWidth}");
                Check($"thickness icon uses supported measurement glyph at {width}", FindVisualDescendants<Wpf.Ui.Controls.SymbolIcon>(firstItem).Single(icon => icon.Name == "FlowHeaderTypeIcon").Symbol == Wpf.Ui.Controls.SymbolRegular.Ruler24, "Ruler24");
                Check($"ROI inputs use short typed rows at {width}", inputPorts.Where(button => ((ToolWorkbenchFlowInputItem)button.DataContext).IsRegionInput).All(button => button.ActualHeight < 42
                    && FindVisualDescendants<ContentControl>(button).All(image => !HasVisiblePresentationPath(image))), $"heights={string.Join(',', inputPorts.Select(button => button.ActualHeight))}");
                Capture($"flow-{width:0}");
            }
            list.SelectedIndex = 1;
            Settle(262);
            Check("selection round trip", ReferenceEquals(vm.SelectedPipelineStep, vm.PipelineSteps[1]), vm.SelectedPipelineStep?.Id ?? "none");
            vm.SelectedPipelineStep = first;
            Settle(262);
            var item = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);
            var details = FindVisualDescendants<Expander>(item).Single(expander => expander.Name == "FlowNodeDetails");
            details.IsExpanded = false;
            Settle(262);
            Check("selected card collapses without changing selection", !details.IsExpanded && ReferenceEquals(list.SelectedItem, first), $"expanded={details.IsExpanded}");
            Capture("flow-selected-collapsed");
            details.IsExpanded = true;
            first.ToolName = "긴 도구 이름 — Reference and measurement ROI with multiple inputs and preserved raw-height units";
            first.UpdateResultPresentation("8.123456789 raw-height", "표시 검증용 결과", true, false);
            Settle(262);
            var names = FindVisualDescendants<TextBlock>(item).Where(text => HasVisiblePresentationPath(text) && text.Text == first.ToolName).ToArray();
            Check("long name wraps and remains available", names.Length == 1 && names[0].TextWrapping == TextWrapping.Wrap && names[0].ActualHeight > 25, $"name={names.FirstOrDefault()?.ActualWidth}x{names.FirstOrDefault()?.ActualHeight}");
            Check("long title receives a full header row", names.Length == 1 && names[0].ActualWidth >= 140, $"titleWidth={names.FirstOrDefault()?.ActualWidth};card={view.FlowCardWidth}");
            Check("result value and unit use existing projection", FindVisualDescendants<TextBlock>(item).Any(text => text.Text == "8.123456789 raw-height" && text.TextWrapping == TextWrapping.Wrap), first.ResultValueSummary);
            Capture("flow-long-name-values");
            details.IsExpanded = false;
            Settle(262);
            Check("long compound folded name and value grow without trimming", item.ActualHeight > 68
                && FindVisualDescendants<TextBlock>(item).Any(text => HasVisiblePresentationPath(text) && text.Text == first.ToolName && text.TextTrimming == TextTrimming.None)
                && FindVisualDescendants<TextBlock>(item).Any(text => HasVisiblePresentationPath(text) && text.Text == first.ResultValueSummary), $"height={item.ActualHeight};value={first.ResultValueSummary}");
            Capture("flow-long-compound-collapsed");
            details.IsExpanded = true;
            first.UpdateResultPresentation(string.Empty, string.Empty, false, false);
            foreach (var state in new[] { "Running", "Error", "Stale", "Ready" })
            {
                first.State = state;
                Settle(262);
                Check($"{state} state remains visible", FindVisualDescendants<TextBlock>(item).Any(text => HasVisiblePresentationPath(text) && text.Text == first.FlowNodeStatusLabel), first.FlowNodeStatusLabel);
                Capture("flow-state-" + state);
            }
            first.ToolName = originalName;
            first.State = originalState;
            first.UpdateResultPresentation(string.Empty, string.Empty, false, false);
            Settle(262);
            var menuButton = FindVisualDescendants<Button>(item).Single(button => button.ContextMenu is not null && HasVisiblePresentationPath(button));
            menuButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, menuButton));
            Settle(262, completeTransitions: true);
            Check("tool menu opens", menuButton.ContextMenu!.IsOpen, "presentation-only menu");
            Check("menu commands use existing selected-step owner", menuButton.ContextMenu.Items.OfType<MenuItem>().All(menuItem => menuItem.Command is not null), $"items={menuButton.ContextMenu.Items.Count}");
            var menu = menuButton.ContextMenu;
            var menuItems = menu.Items.OfType<MenuItem>().ToArray();
            var lastMenuItem = menuItems[^1];
            var lastBounds = lastMenuItem.TransformToAncestor(menu).TransformBounds(new Rect(lastMenuItem.RenderSize));
            Check("last menu row remains inside rendered popup", lastBounds.Bottom <= menu.ActualHeight + .5 && lastMenuItem.ActualHeight > 20,
                $"menu={menu.ActualWidth}x{menu.ActualHeight};last={lastBounds}");
            var popupRoot = (FrameworkElement)PresentationSource.FromVisual(menu)!.RootVisual;
            var lastPopupBounds = lastMenuItem.TransformToAncestor(popupRoot).TransformBounds(new Rect(lastMenuItem.RenderSize));
            var popupOrigin = popupRoot.PointToScreen(new Point());
            Check("full popup includes shadow margins and last row", lastPopupBounds.Left >= 0 && lastPopupBounds.Top >= 0
                && lastPopupBounds.Right <= popupRoot.ActualWidth && lastPopupBounds.Bottom <= popupRoot.ActualHeight,
                $"popup={popupRoot.ActualWidth}x{popupRoot.ActualHeight};last={lastPopupBounds};origin={popupOrigin}");
            Check("popup remains on selected test monitor", popupOrigin.X >= workArea.Left && popupOrigin.Y >= workArea.Top
                && popupOrigin.X + popupRoot.ActualWidth <= workArea.Right && popupOrigin.Y + popupRoot.ActualHeight <= workArea.Bottom,
                $"origin={popupOrigin};size={popupRoot.ActualWidth}x{popupRoot.ActualHeight}");
            var menuAnchorBounds = new Rect(menuButton.PointToScreen(new Point()), menuButton.RenderSize);
            var menuScreenBounds = new Rect(menu.PointToScreen(new Point()), menu.RenderSize);
            var anchorVicinity = menuAnchorBounds;
            anchorVicinity.Inflate(32, 32);
            Check("tool menu anchors to its button for mouse and keyboard", menu.Placement == PlacementMode.Bottom
                && ReferenceEquals(menu.PlacementTarget, menuButton) && anchorVicinity.IntersectsWith(menuScreenBounds),
                $"button={menuAnchorBounds};menu={menuScreenBounds};placement={menu.Placement}");
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(popupRoot).Bitmap, Path.Combine(root, "flow-menu.png"));
            menuButton.ContextMenu.IsOpen = false;
            var secondMenuButton = FindVisualDescendants<Button>((ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1)).Single(button => button.ContextMenu is not null && HasVisiblePresentationPath(button));
            Keyboard.ClearFocus();
            typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(secondMenuButton, null);
            Settle(262, completeTransitions: true);
            Check("unfocused menu click selects and stays open after layout", ReferenceEquals(vm.SelectedPipelineStep, vm.PipelineSteps[1])
                && secondMenuButton.ContextMenu!.IsOpen && secondMenuButton.ContextMenu.PlacementTarget is Button { IsVisible: true } visibleAnchor
                && ReferenceEquals(visibleAnchor.DataContext, vm.PipelineSteps[1]),
                $"selected={vm.SelectedPipelineStep?.Id};menuOpen={secondMenuButton.ContextMenu?.IsOpen}");
            secondMenuButton.ContextMenu!.IsOpen = false;
            vm.SelectedPipelineStep = first;
            Settle(262);
            Keyboard.Focus(secondMenuButton);
            typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(secondMenuButton, null);
            Settle(262, completeTransitions: true);
            Check("keyboard menu selects its own step", ReferenceEquals(vm.SelectedPipelineStep, vm.PipelineSteps[1]) && secondMenuButton.ContextMenu!.IsOpen, vm.SelectedPipelineStep?.Id ?? "none");
            secondMenuButton.ContextMenu!.IsOpen = false;
            vm.SelectedPipelineStep = first;
            Settle(262);
            var headerToggle = FindVisualDescendants<ToggleButton>(item).Single(toggle => toggle.Name == "NodeHeaderToggle" && HasVisiblePresentationPath(toggle));
            Keyboard.Focus(headerToggle);
            Settle(262);
            Check("keyboard focus reaches collapse control", headerToggle.IsKeyboardFocused, $"focused={Keyboard.FocusedElement}");
            Capture("flow-keyboard-focus");
            var flowLanguageBefore = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                foreach (var width in new[] { 262d, 330d })
                {
                    Settle(width);
                    var englishClosed = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(1);
                    Check($"English compound input and state visible at {width}", englishClosed.ActualHeight <= 96
                        && FindVisualDescendants<TextBlock>(englishClosed).Any(text => HasVisiblePresentationPath(text) && text.Text == vm.PipelineSteps[1].FlowNodeStatusLabel),
                        $"height={englishClosed.ActualHeight};summary={vm.PipelineSteps[1].CollapsedInputSummary}");
                    var readiness = (System.Windows.Controls.Primitives.UniformGrid)view.FindName("FlowReadinessCaptions");
                    var captions = readiness.Children.OfType<StackPanel>().Select(cell => cell.Children.OfType<TextBlock>().Single()).ToArray();
                    var captionBounds = captions.Select(caption => new Rect(caption.TranslatePoint(new Point(), readiness), caption.RenderSize)).ToArray();
                    Check($"English readiness captions fit without overlap at {width}", captions.Length == 6
                        && captionBounds.All(bounds => bounds.Left >= 0 && bounds.Right <= readiness.ActualWidth + .5)
                        && captionBounds.Zip(captionBounds.Skip(1)).All(pair => pair.First.Right + 1 <= pair.Second.Left),
                        string.Join(';', captions.Zip(captionBounds).Select(pair => $"{pair.First.Text}:{pair.Second}")));
                    Capture($"flow-English-{width:0}");
                }
            }
            finally { OpenVisionLanguageService.SetLanguage(flowLanguageBefore, save: false); Settle(262); }
            view.IncreaseFlowZoomCommand.Execute(null);
            Settle(262);
            Check("zoom in", Math.Abs(view.FlowZoom - 1.1) < .001, view.FlowZoom.ToString(CultureInfo.InvariantCulture));
            view.DecreaseFlowZoomCommand.Execute(null);
            viewport.ScrollToBottom();
            Settle(262);
            Check("scroll exposes later steps", viewport.VerticalOffset > 0 && viewport.ScrollableHeight > 0, $"offset={viewport.VerticalOffset};extent={viewport.ExtentHeight}");
            view.ResetFlowViewCommand.Execute(null);
            Settle(262);
            Check("restore resets zoom and scroll", view.FlowZoom == 1 && viewport.VerticalOffset == 0 && viewport.HorizontalOffset == 0, $"zoom={view.FlowZoom};offset={viewport.VerticalOffset}/{viewport.HorizontalOffset}");
            var sourcePort = (Button)view.FindName("SourceOutputPort");
            sourcePort.Command.Execute(sourcePort.CommandParameter);
            Settle(262);
            Check("connection starts with existing owner", vm.HasPendingFlowConnection, vm.FlowConnectionMessage);
            vm.CancelFlowConnectionCommand.Execute(null);
            Settle(262);
            Check("connection cancel recovers", !vm.HasPendingFlowConnection, vm.FlowConnectionMessage);
            Check("presentation does not execute or reconnect", vm.RunLog.Count(log => log.Category is "Preview" or "Publish" or "Run") == actions && string.Join('|', vm.PipelineSteps.Select(step => step.InputEntityIdsText + ">" + step.OutputEntityId)) == routes, $"actions={actions};now={vm.RunLog.Count(log => log.Category is "Preview" or "Publish" or "Run")}");
            // Many-node coverage uses the existing Add command and explicit save/open contract, with isolated D-drive output.
            vm.SelectedTool = vm.Tools.Single(tool => tool.Id == "filter");
            for (var index = 0; index < 24; index++) vm.AddSelectedToolCommand.Execute(null);
            Settle(262);
            var manyCount = vm.PipelineSteps.Count;
            Check("many nodes use existing Add owner", manyCount == 32, $"nodes={manyCount}");
            viewport.ScrollToBottom();
            Settle(262);
            Check("many-node scroll reaches final card", viewport.VerticalOffset > 0 && list.ItemContainerGenerator.ContainerFromIndex(manyCount - 1) is ListBoxItem, $"offset={viewport.VerticalOffset};extent={viewport.ExtentHeight}");
            Capture("flow-many-nodes-bottom");
            var savedPath = Path.Combine(root, "many-node-recipe.ov3d-recipe.json");
            Check("explicit save", vm.TrySaveTeachingRecipe(savedPath, out var saveMessage), saveMessage);
            Check("saved many-node recipe reopens", vm.TryOpenTeachingRecipe(savedPath, out var reloadMessage) && vm.PipelineSteps.Count == manyCount, reloadMessage);
            Settle(262);
            Check("reopen does not execute", !vm.IsOrderedRunRunning && !vm.IsSelectedStepPreviewRunning, $"nodes={vm.PipelineSteps.Count}");
            var filterStep = vm.PipelineSteps[^1];
            var navigationInput = filterStep.InputEntityIdsText;
            filterStep.InputEntityIdsText = "missing.flow.next.source";
            vm.SelectedPipelineStep = null;
            Settle(262);
            var expectedNextIssue = vm.RecipeHealthItems.FirstOrDefault(health => health.IsIssue);
            var navigationActions = vm.RunLog.Count(entry => entry.Category is "Preview" or "Publish" or "Run");
            var navigationDirty = vm.IsDirty;
            Check("next issue describes the actual target", expectedNextIssue is not null && nextIssue.IsEnabled
                && vm.NextRecipeHealthIssueDetail == expectedNextIssue.Detail, vm.NextRecipeHealthIssueDetail);
            typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(nextIssue, null);
            Settle(262);
            Check("next issue selects its target without mutation or execution", expectedNextIssue is not null && ReferenceEquals(vm.SelectedPipelineStep, expectedNextIssue.Step)
                && vm.IsDirty == navigationDirty && filterStep.InputEntityIdsText == "missing.flow.next.source"
                && vm.RunLog.Count(entry => entry.Category is "Preview" or "Publish" or "Run") == navigationActions, vm.SelectedPipelineStep?.Id ?? "none");
            filterStep.InputEntityIdsText = navigationInput;
            Check("recover navigation fixture through explicit save", vm.TrySaveTeachingRecipe(savedPath, out var navigationRestoreMessage) && !vm.IsDirty, navigationRestoreMessage);
            vm.SelectedPipelineStep = filterStep;
            Settle(262);
            var preprocessingAcceptance = filterStep.AcceptanceLimitsConfigured;
            foreach (var configured in new bool?[] { null, false, true })
            {
                filterStep.AcceptanceLimitsConfigured = configured;
                Settle(262);
                Check($"preprocessing has no judgment indication for configured={configured?.ToString() ?? "null"}", !filterStep.HasConfiguredAcceptanceLimits
                    && !filterStep.HasUnconfiguredAcceptanceLimits && judgmentGlyph.Text == "·" && filterStep.AcceptanceLimitsConfigured == configured, $"glyph={judgmentGlyph.Text};flag={configured}");
                if (configured == false) Capture("flow-preprocessing-flag-false");
            }
            filterStep.AcceptanceLimitsConfigured = preprocessingAcceptance;
            Check("restore preprocessing fixture through explicit save", vm.TrySaveTeachingRecipe(savedPath, out var preprocessingRestoreMessage) && !vm.IsDirty, preprocessingRestoreMessage);
            vm.BeginFlowConnectionCommand.Execute(vm.Source.Id);
            vm.ConnectFlowOutputCommand.Execute(filterStep.FlowInputs[0]);
            Settle(262);
            Check("source reconnect uses existing port command", filterStep.InputEntityIds[0] == vm.Source.Id && !vm.HasPendingFlowConnection, filterStep.InputEntityIdsText);
            var preview = vm.PreviewSelectedFilterAsync();
            for (var pass = 0; pass < 40 && !preview.IsCompleted; pass++) Settle(262, completeTransitions: true);
            Check("explicit filter Preview completes", preview.IsCompletedSuccessfully && preview.Result, vm.FilterExecutionSummary);
            Settle(262);
            Check("current output thumbnail comes from actual filter output", filterStep.OutputPreviewFrame is { Width: 1280, Height: 840 }
                && !ReferenceEquals(filterStep.OutputPreviewFrame, vm.SourceQuality.PreviewFrame), filterStep.OutputEntityId);
            viewport.ScrollToBottom();
            Settle(262);
            Capture("flow-filter-preview-output");
            var filterItem = (ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(vm.PipelineSteps.Count - 1);
            var filterDetails = FindVisualDescendants<Expander>(filterItem).Single(expander => expander.Name == "FlowNodeDetails");
            filterDetails.IsExpanded = false;
            Settle(262);
            var closedOutput = FindVisualDescendants<Image>(filterItem).Single(image => AutomationProperties.GetAutomationId(image) == "FlowCollapsedOutputThumbnail");
            Check("collapsed current output keeps its thumbnail inline", HasVisiblePresentationPath(closedOutput)
                && closedOutput.ActualWidth == 40 && closedOutput.ActualHeight == 26
                && closedOutput.Parent is Grid outputThumbnailFrame && outputThumbnailFrame.Parent is Grid collapsedOutputRow
                && collapsedOutputRow.ColumnDefinitions.Count == 2 && Grid.GetColumn(outputThumbnailFrame) == 0,
                $"thumbnail={closedOutput.ActualWidth}x{closedOutput.ActualHeight};cardHeight={filterItem.ActualHeight}");
            Check("collapsed current output retains full contract and execution state", filterItem.ActualHeight <= 110
                && FindVisualDescendants<TextBlock>(filterItem).Any(text => HasVisiblePresentationPath(text) && text.Text == filterStep.OutputContractDisplay)
                && FindVisualDescendants<TextBlock>(filterItem).Any(text => HasVisiblePresentationPath(text) && text.Text == filterStep.FlowNodeStatusLabel),
                $"height={filterItem.ActualHeight};contract={filterStep.OutputContractDisplay};state={filterStep.FlowNodeStatusLabel}");
            Capture("flow-filter-preview-output-collapsed");
            filterDetails.IsExpanded = true;
            Settle(262);
            filterStep.InputEntityIdsText = "missing.flow.test.source";
            Settle(262);
            Check("stale output thumbnail is cleared", !filterStep.HasOutputPreview && filterStep.InputPortHasIssue, filterStep.FlowNodeStatusLabel);
            Check("individual input failure keeps full detail outside its status badge", filterStep.FlowNodeStatusLabel == filterStep.InputPortState
                && FindVisualDescendants<TextBlock>(filterItem).Any(text => HasVisiblePresentationPath(text) && text.Name == "FlowInputIssueDetail"
                    && text.Text == filterStep.InputPortDetail && text.ActualWidth > 150 && text.TextTrimming == TextTrimming.None), filterStep.InputPortDetail);
            var unresolvedInput = FindVisualDescendants<Button>(filterItem).Single(button => button.Name == "StepInputPort");
            var unresolvedInputDot = FindVisualDescendants<System.Windows.Shapes.Ellipse>(unresolvedInput).Single(port => port.Name == "PortVisual");
            Check("unresolved input renders hollow", filterStep.PrimaryFlowInput?.IsConnected == false && HasVisiblePresentationPath(unresolvedInputDot)
                && unresolvedInputDot.Fill is SolidColorBrush fill && fill.Color != ((SolidColorBrush)view.FindResource("ThreeD.AccentBrush")).Color, filterStep.InputEntityIdsText);
            filterStep.InputEntityIdsText = vm.Source.Id;
            var recoveredPreview = vm.PreviewSelectedFilterAsync();
            for (var pass = 0; pass < 40 && !recoveredPreview.IsCompleted; pass++) Settle(262, completeTransitions: true);
            Settle(262);
            Check("valid explicit Preview recovers output", recoveredPreview.IsCompletedSuccessfully && recoveredPreview.Result && filterStep.HasOutputPreview, vm.FilterExecutionSummary);
            vm.CreateNewTeachingRecipe();
            Settle(262);
            Check("empty recipe disables Run and keeps Add", vm.PipelineSteps.Count == 0 && !run.IsEnabled && add.IsEnabled, $"steps={vm.PipelineSteps.Count};run={run.IsEnabled};add={add.IsEnabled}");
            Check("empty recipe has no fabricated judgment readiness", judgmentGlyph.Text == "·" && !nextIssue.IsEnabled, $"glyph={judgmentGlyph.Text};next={nextIssue.IsEnabled}");
            Check("empty recipe source consumer count is zero", vm.SourceConsumerCount == 0, vm.SourceConsumerCount.ToString(CultureInfo.InvariantCulture));
            Capture("flow-empty");
            view.DataContext = null;
            Check("View detachment keeps execution idle", !vm.IsOrderedRunRunning && !vm.IsSelectedStepPreviewRunning, "Shell owns state/lifetime; View has no business resource");
        }
        catch (Exception exception)
        {
            Check("verification exception", false, exception.ToString());
        }
        finally
        {
            ownedHost?.Close();
        }
        lines.Add("Boundary: isolated WPF test window only. Native hover/held press/keyboard/menu interaction, Computer Use and 125% are not verified here. General group authoring/aggregate judgment are outside the contract.");
        summary = $"Recipe Flow: {passed}/{total} passed.";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    public static bool VerifyActivityLayout(string reportPath, out string summary)
    {
        Directory.CreateDirectory(GetReportDirectory(reportPath));
        var lines = new List<string> { "Teaching Activity production binding and temporary layout verification" };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        var root = Path.Combine(GetReportDirectory(reportPath), "activity-layout");
        Directory.CreateDirectory(root);
        using var shell = new ShellMainWindowViewModel(recentRunRecordsPath: Path.Combine(root, "recent-runs.json"),
            recentRecipesPath: Path.Combine(root, "recent-recipes.json"), orderedRunRecordRoot: Path.Combine(root, "runs"), integrationSettingsPath: Path.Combine(root, "integration.json"));
        ToolRecipeWorkbenchView? workbench = null;
        Window? host = null;
        try
        {
            _ = ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var workArea);
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            workbench = new ToolRecipeWorkbenchView { DataContext = shell, ViewerContent = new Border() };
            host = new Window { Content = workbench, Width = 1280, Height = 760, Left = workArea.Left, Top = workArea.Top,
                WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false };
            host.Show();
            void Settle()
            {
                host.UpdateLayout();
                host.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                host.UpdateLayout();
            }
            Settle();
            var dock = workbench.DockWorkspace;
            var results = FindVisualDescendants<ResultsWorkspaceView>(workbench).Single();
            var body = (Grid)results.FindName("ResultsWorkspaceContent");
            var log = FindVisualDescendants<DataGrid>(results).Single(grid => AutomationProperties.GetAutomationId(grid) == "TeachingActivityLog");
            var rawCount = shell.Workbench.RunLog.Count;
            foreach (var width in new[] { 1920d, 1280d })
            {
                host.Width = Math.Min(width, workArea.Width);
                host.Height = Math.Min(width == 1920 ? 1040 : 760, workArea.Height);
                results.SetSection(ResultsWorkspaceSection.RunRecord);
                Settle();
                var beforeHeight = results.ActualHeight;
                var beforeWidth = results.ActualWidth;
                var beforeProfile = dock.CapturePresentationState();
                results.SetSection(ResultsWorkspaceSection.Log);
                Settle();
                Check($"Activity enters through the production section binding at {width}", dock.IsActivityLogSelected && shell.ResultsWorkspace.IsLogSelected
                    && ReferenceEquals(log.ItemsSource, shell.Workbench.ExecutionLog) && log.Items.Count == 0 && rawCount > 0, $"raw={rawCount};executions={log.Items.Count}");
                Check($"Activity body matches the public HTML default height at {width}", Math.Abs(body.ActualHeight - (width == 1920 ? 196 : 136)) < 1,
                    $"view={results.ActualWidth}x{results.ActualHeight};body={body.ActualWidth}x{body.ActualHeight}");
                Check($"Activity keeps the saved default layout at {width}", dock.CapturePresentationState() == beforeProfile, "No temporary layout written to the profile.");
                Check("Execution-empty guidance follows the execution list", FindVisualDescendants<TextBlock>(results).Any(text => text.IsVisible
                    && text.Text == shell.Localization.Resolve("ThreeD.Drawer.ExecutionLogEmpty", "이 작업에서 아직 실행하지 않았습니다.", "Nothing has been run in this task yet.")), "Ordinary startup log is nonempty.");
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
                results.SetSection(ResultsWorkspaceSection.RunRecord);
                Settle();
                Check($"Leaving Activity restores the Results geometry at {width}", !dock.IsActivityLogSelected && Math.Abs(results.ActualHeight - beforeHeight) < 1
                    && Math.Abs(results.ActualWidth - beforeWidth) < 1, $"before={beforeWidth}x{beforeHeight};after={results.ActualWidth}x{results.ActualHeight}");
                results.SetSection(ResultsWorkspaceSection.Section);
                Settle();
                var sectionHeight = results.ActualHeight;
                Check($"Section enters through the production binding and uses the HTML height at {width}", dock.IsSectionSelected && !dock.IsActivityLogSelected
                    && Math.Abs(body.ActualHeight - (width == 1920 ? 196 : 136)) < 1 && dock.CapturePresentationState() == beforeProfile,
                    $"section={results.ActualWidth}x{sectionHeight};body={body.ActualHeight};minimum={results.MinHeight}");
                results.SetSection(ResultsWorkspaceSection.Log);
                Settle();
                results.SetSection(ResultsWorkspaceSection.Section);
                Settle();
                Check($"Section HTML height survives the Activity round trip at {width}", dock.IsSectionSelected && !dock.IsActivityLogSelected
                    && Math.Abs(results.ActualHeight - sectionHeight) < 1 && results.MinHeight == 0,
                    $"before={sectionHeight};after={results.ActualHeight};minimum={results.MinHeight}");
                results.SetSection(ResultsWorkspaceSection.RunRecord);
                Settle();
                Check($"Leaving Section restores the protected Results geometry at {width}", !dock.IsSectionSelected && !dock.IsActivityLogSelected
                    && Math.Abs(results.ActualWidth - beforeWidth) < 1 && Math.Abs(results.ActualHeight - beforeHeight) < 1,
                    $"before={beforeWidth}x{beforeHeight};after={results.ActualWidth}x{results.ActualHeight}");
            }
            results.SetSection(ResultsWorkspaceSection.Log);
            Settle();
            host.WindowState = WindowState.Maximized;
            Settle();
            Check("Maximize while Activity is selected keeps the HTML height", Math.Abs(body.ActualHeight - 196) < 1, $"body={body.ActualHeight};window={host.ActualWidth}x{host.ActualHeight}");
            host.WindowState = WindowState.Normal;
            Settle();
            Check("Restore while Activity is selected keeps the Compact HTML height", Math.Abs(body.ActualHeight - 136) < 1, $"body={body.ActualHeight};window={host.ActualWidth}x{host.ActualHeight}");
            results.SetSection(ResultsWorkspaceSection.RunRecord);
            Settle();
            var custom = OpenVisionDockPresentationState.Default with
            {
                Compact = OpenVisionDockPresentationState.Default.Compact with { AuthoringSupport = 1.3, AuthoringInspector = 1.8, AuthoringViewer = .9 }
            };
            dock.ApplyPresentationState(custom);
            Settle();
            var previousWidth = results.ActualWidth;
            results.SetSection(ResultsWorkspaceSection.Log);
            Settle();
            var temporaryWidth = results.ActualWidth;
            Check("Activity uses the saved Compact ratio without inflating its Viewer weight", dock.CapturePresentationState().Compact == custom.Compact
                && temporaryWidth < previousWidth, $"before={previousWidth};activity={temporaryWidth};saved={dock.CapturePresentationState().Compact.AuthoringViewer}");
            host.Height -= 60;
            Settle();
            Check("Resize keeps the Activity height and preserves the saved ratio", Math.Abs(body.ActualHeight - 136) < 1 && dock.CapturePresentationState().Compact == custom.Compact,
                $"body={body.ActualHeight};saved={dock.CapturePresentationState().Compact.AuthoringViewer}");
            results.SetSection(ResultsWorkspaceSection.RunRecord);
            Settle();
            Check("Leaving Activity restores the previous Compact geometry", Math.Abs(results.ActualWidth - previousWidth) < 1,
                $"before={previousWidth};after={results.ActualWidth}");
            results.SetSection(ResultsWorkspaceSection.Section);
            Settle();
            Check("Section uses the saved Compact width and preserves its profile", Math.Abs(results.ActualWidth - temporaryWidth) < 1
                && Math.Abs(body.ActualHeight - 136) < 1 && dock.CapturePresentationState().Compact == custom.Compact,
                $"section={results.ActualWidth};activity={temporaryWidth};saved={dock.CapturePresentationState().Compact.AuthoringViewer}");
            host.WindowState = WindowState.Maximized;
            Settle();
            Check("Maximize while Section is selected keeps the Wide HTML height", Math.Abs(body.ActualHeight - 196) < 1,
                $"body={body.ActualHeight};window={host.ActualWidth}x{host.ActualHeight}");
            host.WindowState = WindowState.Normal;
            Settle();
            Check("Restore while Section is selected keeps the Compact HTML height and saved width", Math.Abs(body.ActualHeight - 136) < 1
                && Math.Abs(results.ActualWidth - temporaryWidth) < 1 && dock.CapturePresentationState().Compact == custom.Compact,
                $"body={body.ActualHeight};width={results.ActualWidth};saved={dock.CapturePresentationState().Compact.AuthoringViewer}");
            results.SetSection(ResultsWorkspaceSection.Log);
            Settle();
            Check("Section to Activity preserves the same temporary dimensions", !dock.IsSectionSelected && dock.IsActivityLogSelected
                && Math.Abs(body.ActualHeight - 136) < 1 && Math.Abs(results.ActualWidth - temporaryWidth) < 1
                && dock.CapturePresentationState().Compact == custom.Compact, $"width={results.ActualWidth};body={body.ActualHeight}");
            results.SetSection(ResultsWorkspaceSection.RunRecord);
            Settle();
            Check("Leaving both temporary drawers restores the protected Results width", Math.Abs(results.ActualWidth - previousWidth) < 1
                && dock.CapturePresentationState().Compact == custom.Compact,
                $"before={previousWidth};after={results.ActualWidth};saved={dock.CapturePresentationState().Compact.AuthoringViewer};expected={custom.Compact.AuthoringViewer}");
            Check("Tab and layout changes have no execution or recipe side effect", shell.Workbench.ExecutionLog.Count == 0 && !shell.Workbench.IsDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning && shell.Workbench.RunLog.Count == rawCount,
                $"raw={rawCount}->{shell.Workbench.RunLog.Count};executions={shell.Workbench.ExecutionLog.Count}");
        }
        catch (Exception exception) { Check("Unexpected exception", false, exception.ToString()); }
        finally { host?.Close(); workbench?.Dispose(); }
        summary = $"TeachingActivityLayout|{(passed == total ? "Pass" : "Fail")}|checks={passed}/{total}";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return total > 0 && passed == total;
    }

    public static bool VerifyInspectionExecutionBar(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        Directory.CreateDirectory(GetReportDirectory(reportPath));
        var lines = new List<string> { "OpenVisionLab 3D inspection execution bar verification", $"Generated: {DateTimeOffset.Now:O}" };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }
        void ActivateSampleChoice(ButtonBase button) => typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);
        Window? ownedHost = null;
        ToolRecipeWorkbenchView? ownedWorkbench = null;
        try
        {
            var inspectionBoundaryRoot = Path.Combine(GetReportDirectory(reportPath), "d20-inspection-fixture", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(inspectionBoundaryRoot);
            var validationFixture = CompletenessValidationVerificationFixtureFactory.Create(Path.Combine(inspectionBoundaryRoot, "validation-source"));
            var ownerPathRecipe = validationFixture.RecipePath;
            var copiedSampleSource = validationFixture.Samples.First(sample => sample.Role == ToolRecipeValidationSampleRole.Good).SourcePath;
            _ = ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var testWorkArea);
            var testLeft = testWorkArea.Left;
            var testTop = testWorkArea.Top;
            var monitorReportPath = Path.GetFullPath(reportPath) + ".monitors.txt";
            File.WriteAllText(monitorReportPath, string.Empty);
            var viewerOwner = new object();
            var inspectionRecipePath = Path.Combine(inspectionBoundaryRoot, "검사기준-장착방향과표면상태를확인하는긴저장레시피-012345678901234567890123456789.ov3d-recipe.json");
            File.Copy(ownerPathRecipe, inspectionRecipePath);
            using var inspectionShell = new ShellMainWindowViewModel(
                recentRunRecordsPath: Path.Combine(inspectionBoundaryRoot, "recent-runs.json"), recentRecipesPath: Path.Combine(inspectionBoundaryRoot, "recent-recipes.json"),
                orderedRunRecordRoot: Path.Combine(inspectionBoundaryRoot, "records"));
            Check("v3 inspection fixture opens the saved recipe without executing", inspectionShell.Workbench.TryOpenTeachingRecipe(inspectionRecipePath, out var inspectionOpenMessage)
                && !inspectionShell.Workbench.IsInspectionQueueRunning && !inspectionShell.Workbench.IsOrderedRunRunning, inspectionOpenMessage);
            // This entry runs in a fresh process so prior comparison-renderer fixtures cannot leak into this workflow.
            inspectionShell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            var ownerPathWorkbench = new ToolRecipeWorkbenchView { DataContext = inspectionShell, ViewerContent = viewerOwner };
            var ownerPathHost = new Window
            {
                Content = ownerPathWorkbench, Width = 1280, Height = 760, Left = testLeft, Top = testTop,
                ShowActivated = false, ShowInTaskbar = false, WindowStyle = WindowStyle.None,
            };
            ownedHost = ownerPathHost;
            ownedWorkbench = ownerPathWorkbench;
            ownerPathHost.Show();
            ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            ownerPathHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownerPathHost, monitorReportPath);
            var executionQueue = inspectionShell.Workbench.InspectionQueue;
            var fileListDock = (OpenVisionDockWorkspaceView)ownerPathWorkbench.FindName("DockWorkspace");
            T FileListControl<T>(string id) where T : FrameworkElement => FindVisualDescendants<T>(ownerPathWorkbench).Single(control => control.IsVisible && AutomationProperties.GetAutomationId(control) == id);
            string RateLabelText() => string.Concat(FileListControl<TextBlock>("InspectionQueuePassRate").Inlines.OfType<System.Windows.Documents.Run>().Select(run => run.Text));
            void SettleFileList(bool completeTransitions = false)
            {
                ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                ownerPathHost.UpdateLayout();
                ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                ownerPathHost.UpdateLayout();
                if (!completeTransitions) return;
                // Allow the actual Expander transition to finish before measuring or capturing its content.
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background, ownerPathHost.Dispatcher) { Interval = TimeSpan.FromMilliseconds(300) };
                EventHandler finishFrame = (_, _) => frame.Continue = false;
                timer.Tick += finishFrame;
                timer.Start();
                try { System.Windows.Threading.Dispatcher.PushFrame(frame); }
                finally { timer.Stop(); timer.Tick -= finishFrame; }
                ownerPathHost.UpdateLayout();
            }
            bool SummaryStyleMatches()
            {
                var card = FileListControl<Border>("InspectionQueueSummaryCard");
                var rate = FileListControl<TextBlock>("InspectionQueuePassRate");
                var value = rate.Inlines.OfType<System.Windows.Documents.Run>().Last();
                var distribution = FileListControl<Grid>("InspectionQueueSummaryProgress");
                var expectedColors = new[] { "#5E9C76", "#C9686C", "#C98450", "#F1C27A", "#59636E", "#3A444F", "#65B9AE" };
                return card.CornerRadius == new CornerRadius(6) && Math.Abs(card.ActualHeight - 53.2) <= 1
                    && ((SolidColorBrush)card.Background).Color == (Color)ColorConverter.ConvertFromString("#1D242B")
                    && ((SolidColorBrush)card.BorderBrush).Color == (Color)ColorConverter.ConvertFromString("#2A323B")
                    && rate.FontSize == 12 && value.FontSize == 16 && value.FontWeight.ToOpenTypeWeight() == 650
                    && ((SolidColorBrush)rate.Foreground).Color == (Color)ColorConverter.ConvertFromString("#ADB9C6")
                    && ((SolidColorBrush)value.Foreground).Color == (Color)ColorConverter.ConvertFromString("#E4E9EF")
                    && distribution.Clip is RectangleGeometry { RadiusX: 3, RadiusY: 3 } clip
                    && Math.Abs(clip.Rect.Width - distribution.ActualWidth) < 0.1 && clip.Rect.Height == 6
                    && ((SolidColorBrush)distribution.Background).Color == (Color)ColorConverter.ConvertFromString("#28313A")
                    && distribution.Children.Cast<Border>().Select((segment, index) =>
                        ((SolidColorBrush)segment.Background).Color == (Color)ColorConverter.ConvertFromString(expectedColors[index])).All(matches => matches);
            }
            bool DistributionMatches(InspectionQueueWorkspaceViewModel queue)
            {
                var distribution = FileListControl<Grid>("InspectionQueueSummaryProgress");
                var counts = new[] { queue.PassCount, queue.FailCount, queue.ErrorCount, queue.WarningCount, queue.CancelledCount, queue.UntestedCount, queue.RunningCount };
                var pixelWidth = 1 / VisualTreeHelper.GetDpi(distribution).DpiScaleX;
                return SummaryStyleMatches() && distribution.ActualHeight == 6 && distribution.ColumnDefinitions.Count == 7
                    && counts.Sum() == queue.AllCount && distribution.ColumnDefinitions.Select((column, index) =>
                        counts[index] == 0 ? column.Width.IsAbsolute && column.Width.Value == 0 && column.ActualWidth == 0
                            : column.Width.IsStar && column.Width.Value == counts[index]
                                && Math.Abs(column.ActualWidth - distribution.ActualWidth * counts[index] / queue.AllCount) <= pixelWidth).All(matches => matches)
                    && (queue.AllCount == 0 || Math.Abs(distribution.ColumnDefinitions.Sum(column => column.ActualWidth) - distribution.ActualWidth) < 0.1)
                    && AutomationProperties.GetHelpText(distribution) == queue.CountsText;
            }
            Expander ExpandFileInformation()
            {
                var information = FileListControl<Expander>("InspectionFileResults");
                information.IsExpanded = true;
                SettleFileList(completeTransitions: true);
                if (!SpinWait.SpinUntil(() =>
                    {
                        ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        ownerPathHost.UpdateLayout();
                        var scroll = FindVisualDescendants<ScrollViewer>(information).FirstOrDefault(control => control.Content is StackPanel);
                        return scroll is { IsVisible: true } && scroll.ActualHeight >= Math.Min(180, scroll.ExtentHeight) - 1
                            && FindVisualDescendants<TextBlock>(information).Any(label => label.IsVisible && AutomationProperties.GetAutomationId(label) == "InspectionSelectedStatusSummary");
                    }, TimeSpan.FromSeconds(3))) throw new TimeoutException($"Inspection information did not finish rendering; expanded={information.IsExpanded};visible={information.IsVisible};selected={executionQueue.SelectedSample?.SourcePath}");
                return information;
            }
            var executionBar = FindVisualDescendants<Border>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionExecutionBar");
            var executionRun = FindVisualDescendants<ButtonBase>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "StartInspectionQueueRun");
            var executionScope = FindVisualDescendants<ComboBox>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionRunScope");
            var executionStop = FindVisualDescendants<ButtonBase>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "StopInspectionQueueRun");
            var executionProgress = FindVisualDescendants<ProgressBar>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionProgress");
            var executionProgressText = FindVisualDescendants<TextBlock>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionProgressText");
            var executionBasis = FindVisualDescendants<TextBlock>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionBasisName");
            var executionReadiness = FindVisualDescendants<TextBlock>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionIdleReadiness");
            var executionSourceBefore = inspectionShell.Workbench.Source.Path;
            var executionDirtyBefore = inspectionShell.Workbench.IsDirty;
            foreach (var inspectionWidth in new[] { 1280d, 1920d })
            {
                ownerPathHost.Width = inspectionWidth;
                ownerPathHost.Height = inspectionWidth == 1280 ? 760 : 1040;
                ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                ownerPathHost.UpdateLayout();
                ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                ownerPathHost.UpdateLayout();
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownerPathHost, monitorReportPath);
                var basisBounds = executionBasis.TransformToAncestor(executionBar).TransformBounds(new Rect(executionBasis.RenderSize));
                var remainingBounds = executionScope.TransformToAncestor(executionBar).TransformBounds(new Rect(executionScope.RenderSize));
                var runBounds = executionRun.TransformToAncestor(executionBar).TransformBounds(new Rect(executionRun.RenderSize));
                Check($"v3 inspection long saved identity and empty actions fit the {inspectionWidth:0}px bar",
                    executionBar.IsVisible && executionBar.ActualHeight == 44 && ownerPathWorkbench.HasValidateStageComposition && basisBounds.Right <= remainingBounds.Left && remainingBounds.Right <= runBounds.Left && runBounds.Right <= executionBar.ActualWidth
                    && executionBasis.Text == Path.GetFileName(inspectionRecipePath) && executionBasis.TextTrimming == TextTrimming.CharacterEllipsis
                    && !executionRun.IsEnabled && executionScope.IsEnabled && executionReadiness.Text == executionQueue.RecipeReadinessText
                    && executionProgress.Width == (inspectionWidth == 1280 ? 110 : 180)
                    && new FrameworkElement[] { executionRun, executionScope }.SelectMany(FindVisualDescendants<TextBlock>).Where(label => label.IsVisible).All(label => label.ActualWidth + label.Margin.Left + label.Margin.Right >= label.DesiredSize.Width - 0.1),
                    $"visible={executionBar.IsVisible};stage={ownerPathWorkbench.OperatorStage};composed={ownerPathWorkbench.HasValidateStageComposition};bar={executionBar.ActualWidth}x{executionBar.ActualHeight};basis={basisBounds};remaining={remainingBounds};run={runBounds};progress={executionProgress.Width};ready={executionReadiness.Text}");
                var listHeader = FileListControl<Border>("InspectionQueueHeader");
                var listSearch = FileListControl<TextBox>("InspectionSearch");
                var inspectionPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(fileListDock).Single(pane => ((AvalonDock.Layout.LayoutAnchorablePane)pane.Model).SelectedContent?.ContentId == "evidence-workbench");
                var listOuterHeader = (Border)inspectionPane.Template.FindName("PaneTabHeader", inspectionPane);
                var filterChoices = FindVisualDescendants<RadioButton>(ownerPathWorkbench).Where(control => control.IsVisible && AutomationProperties.GetAutomationId(control).StartsWith("InspectionFilter", StringComparison.Ordinal)).ToArray();
                Check($"v3 inspection list has a single34px header, seven24px filters and wide search at {inspectionWidth:0}px",
                    listHeader.ActualHeight == 34 && listOuterHeader.ActualHeight == 0 && listSearch.ActualWidth >= 175
                    && listHeader.TransformToAncestor(inspectionPane).TransformBounds(new Rect(listHeader.RenderSize)).Top <= 2
                    && filterChoices.Length == 7 && filterChoices.All(control => control.ActualHeight == 24)
                    && FileListControl<Grid>("InspectionQueueSummaryProgress").Children.Cast<Border>().All(segment => segment.Visibility == Visibility.Collapsed)
                    && DistributionMatches(executionQueue) && executionQueue.PassRateText == "—%"
                    && RateLabelText().EndsWith("—%", StringComparison.Ordinal)
                    && FileListControl<CheckBox>("InspectionCheckAllVisible").IsEnabled == false,
                    $"header={listHeader.ActualWidth}x{listHeader.ActualHeight};origin={listHeader.TransformToAncestor(inspectionPane).TransformBounds(new Rect(listHeader.RenderSize)).Top};outer={listOuterHeader.ActualHeight};search={listSearch.ActualWidth};filters={filterChoices.Length};footerEnabled={FileListControl<CheckBox>("InspectionCheckAllVisible").IsEnabled}"
                    + $";distribution={DistributionMatches(executionQueue)};rate={RateLabelText()};emptyWidths={string.Join(',', FileListControl<Grid>("InspectionQueueSummaryProgress").ColumnDefinitions.Select(column => column.ActualWidth))}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d20-empty-{inspectionWidth:0}.png"));
            }

            var inspectionRecipeNameBefore = inspectionShell.Workbench.RecipeName;
            var inspectionLanguageBefore = OpenVisionLanguageService.CurrentLanguage;
            inspectionShell.Workbench.RecipeName = "검사 중 표시되는 긴 티칭 변경 이름 012345678901234567890123456789";
            ownerPathHost.Width = 1280;
            ownerPathHost.Height = 760;
            try
            {
                foreach (var inspectionLanguage in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
                {
                    OpenVisionLanguageService.SetLanguage(inspectionLanguage, save: false);
                    ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                    ownerPathHost.UpdateLayout();
                    var unsavedTeaching = FindVisualDescendants<StackPanel>(executionBar).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionUnsavedTeaching");
                    var saveTeaching = FindVisualDescendants<ButtonBase>(executionBar).Single(control => AutomationProperties.GetAutomationId(control) == "SaveInspectionTeaching");
                    var saveBounds = saveTeaching.TransformToAncestor(executionBar).TransformBounds(new Rect(saveTeaching.RenderSize));
                    var remainingBounds = executionScope.TransformToAncestor(executionBar).TransformBounds(new Rect(executionScope.RenderSize));
                    var runBounds = executionRun.TransformToAncestor(executionBar).TransformBounds(new Rect(executionRun.RenderSize));
                    Check($"v3 unsaved teaching and existing Save remain readable beside inspection actions in {inspectionLanguage}",
                        unsavedTeaching.IsVisible && saveTeaching.IsVisible && ReferenceEquals(saveTeaching.Command, inspectionShell.Workbench.SaveTeachingRecipeCommand)
                        && saveBounds.Right <= remainingBounds.Left && runBounds.Right <= executionBar.ActualWidth
                        && executionBasis.Text == Path.GetFileName(inspectionRecipePath) && !executionQueue.IsRunning
                        && FindVisualDescendants<TextBlock>(unsavedTeaching).Where(label => label.IsVisible).All(label => label.ActualWidth + label.Margin.Left + label.Margin.Right >= label.DesiredSize.Width - 0.1),
                        $"save={saveBounds};remaining={remainingBounds};run={runBounds};bar={executionBar.ActualWidth}");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d20-unsaved-{inspectionLanguage}-1280.png"));
                }
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(inspectionLanguageBefore, save: false);
                Check("v3 inspection fixture restores the saved teaching after presentation-only dirty checks", inspectionShell.Workbench.TryOpenTeachingRecipe(inspectionRecipePath, out var dirtyRestoreMessage)
                    && inspectionShell.Workbench.RecipeName == inspectionRecipeNameBefore && !inspectionShell.Workbench.IsDirty, dirtyRestoreMessage);
                ownerPathHost.UpdateLayout();
            }
            var fileListView = FileListControl<InspectionQueueWorkspaceView>("InspectionQueueView");
            var listMore = FileListControl<MenuItem>("InspectionAdditionalMenu");
            var listPin = FileListControl<ButtonBase>("InspectionQueueAutoHide");
            var listNativePane = (AvalonDock.Layout.LayoutAnchorable)fileListDock.FindName("evidenceAnchorable");
            var listDockItem = fileListDock.EvidencePaneLayoutItem;
            var listSourceBefore = inspectionShell.Workbench.Source.Path;
            Check("v3 inspection header uses existing file dialogs, native pane and Shell Results commands",
                ReferenceEquals(fileListView.DataContext, executionQueue) && ReferenceEquals(fileListView.DockPaneCommands, listDockItem)
                && ReferenceEquals(listPin.Command, listDockItem?.AutoHideCommand)
                && ReferenceEquals(FileListControl<ButtonBase>("InspectionAddFiles").Command, executionQueue.AddFilesCommand)
                && ReferenceEquals(FileListControl<ButtonBase>("InspectionAddFolder").Command, executionQueue.AddFolderCommand)
                && ReferenceEquals(fileListView.ResultsCommand, inspectionShell.SelectWorkspaceCommand),
                $"queue={ReferenceEquals(fileListView.DataContext, executionQueue)};dock={listDockItem is not null};results={fileListView.ResultsCommand is not null}");
            listMore.IsSubmenuOpen = true;
            SettleFileList();
            var listPopup = FindVisualDescendants<Popup>(listMore).First(popup => popup.IsOpen);
            var listActions = listMore.Items.OfType<MenuItem>().ToArray();
            Check("v3 inspection additional menu exposes existing actions and native auto hide without execution",
                listPopup.Child is { IsVisible: true } && listActions.Length == 15 && listActions.All(item => item.Command is not null)
                && listActions.Single(item => AutomationProperties.GetAutomationId(item) == "OpenInspectionResultsButton").Command == inspectionShell.SelectWorkspaceCommand
                && !executionQueue.IsRunning && inspectionShell.Workbench.Source.Path == listSourceBefore,
                $"popup={listPopup.IsOpen};actions={listActions.Length};running={executionQueue.IsRunning}");
            if (listPopup.Child is FrameworkElement listPopupChild)
            {
                var popupVisual = new DrawingVisual();
                using (var drawing = popupVisual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(listPopupChild), null, new Rect(listPopupChild.RenderSize));
                var popupBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(listPopupChild.ActualWidth), (int)Math.Ceiling(listPopupChild.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                popupBitmap.Render(popupVisual);
                WpfScreenshotCapture.Save(popupBitmap, Path.ChangeExtension(reportPath, ".d21-file-actions.png"));
            }
            listMore.IsSubmenuOpen = false;
            SettleFileList();
            ActivateSampleChoice(listPin);
            SettleFileList();
            var listSideTab = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorControl>(fileListDock).SingleOrDefault(tab => ReferenceEquals(tab.Model, listNativePane));
            Check("v3 inspection pin auto hides to an accessible native side tab", listNativePane.IsAutoHidden && listSideTab is { IsVisible: true } && !executionQueue.IsRunning, $"hidden={listNativePane.IsAutoHidden};tab={listSideTab?.IsVisible}");
            listDockItem!.AutoHideCommand.Execute(null);
            SettleFileList();
            Check("v3 inspection native pin restores the same Queue and header without execution", !listNativePane.IsAutoHidden && fileListView.IsVisible
                && ReferenceEquals(fileListView.DockPaneCommands, fileListDock.EvidencePaneLayoutItem) && !executionQueue.IsRunning,
                $"hidden={listNativePane.IsAutoHidden};view={fileListView.IsVisible};queue={ReferenceEquals(fileListView.DataContext, executionQueue)}");
            var longListName = "검사입력-장착방향과표면상태를확인하는동일이름-012345678901234567890123456789.C3D";
            var longListPaths = new[] { "distinct-source-A", "distinct-source-B" }.Select(directory =>
            {
                var parent = Path.Combine(inspectionBoundaryRoot, directory);
                Directory.CreateDirectory(parent);
                var path = Path.Combine(parent, longListName);
                File.Copy(copiedSampleSource, path);
                return path;
            }).ToArray();
            executionQueue.AddSources(longListPaths);
            ownerPathHost.Width = 1280;
            ownerPathHost.Height = 760;
            SettleFileList();
            var fileChoices = FindVisualDescendants<ButtonBase>(fileListView).Where(control => AutomationProperties.GetAutomationId(control) == "InspectionQueueSelectSample").ToArray();
            ActivateSampleChoice(fileChoices[1]);
            SettleFileList();
            var fileInformation = ExpandFileInformation();
            SettleFileList();
            var sourcePathText = FileListControl<TextBlock>("InspectionSelectedSourcePath");
            var rowNames = FindVisualDescendants<TextBlock>(fileListView).Where(control => AutomationProperties.GetAutomationId(control) == "InspectionQueueFileName").ToArray();
            Check("v3 compact long equal names remain distinct through selected full path and intentional row trimming",
                executionQueue.SelectedSample?.SourcePath == longListPaths[1] && sourcePathText.Text == longListPaths[1] && sourcePathText.TextWrapping == TextWrapping.Wrap
                && rowNames.Length == 2 && rowNames.All(label => label.TextTrimming == TextTrimming.CharacterEllipsis && label.ActualWidth > 50)
                && fileChoices.All(choice => choice.ActualHeight <= 52) && !executionQueue.IsRunning,
                $"selected={executionQueue.SelectedSample?.SourcePath};rows={string.Join(',', fileChoices.Select(choice => choice.ActualHeight))};nameWidths={string.Join(',', rowNames.Select(label => label.ActualWidth))}");
            var informationScroll = FindVisualDescendants<ScrollViewer>(fileInformation).First(control => control.Content is StackPanel);
            informationScroll.ScrollToHome();
            SettleFileList();
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-long-details-1280.png"));
            informationScroll.ScrollToEnd();
            SettleFileList();
            Check("v3 expanded file information permits scrolling through the complete selected path", informationScroll.VerticalOffset + informationScroll.ViewportHeight >= informationScroll.ExtentHeight - 1
                && sourcePathText.Text.EndsWith(longListName, StringComparison.Ordinal) && !executionQueue.IsRunning,
                $"offset={informationScroll.VerticalOffset};viewport={informationScroll.ViewportHeight};extent={informationScroll.ExtentHeight};source={sourcePathText.Text}");
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-long-details-end-1280.png"));
            var listLanguageBefore = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                SettleFileList(completeTransitions: true);
                var infoCaption = FindVisualDescendants<TextBlock>(fileInformation).First(label => label.Text == "Selected file details and results");
                var listFooter = FileListControl<CheckBox>("InspectionCheckAllVisible");
                Check("v3 English compact list captions and selected path remain reachable without execution", infoCaption.IsVisible && infoCaption.TextWrapping == TextWrapping.Wrap
                    && infoCaption.ActualHeight >= infoCaption.DesiredSize.Height - 1 && listFooter.IsVisible
                    && FindVisualDescendants<TextBlock>(fileListView).Where(label => AutomationProperties.GetAutomationId(label) == "InspectionQueueFileSummary").All(label => label.Text == "Untested · Waiting for explicit Run.")
                    && FileListControl<TextBlock>("InspectionSelectedStatusSummary").Text == "Untested · Waiting for explicit Run."
                    && ReferenceEquals(executionQueue.SelectedSample, executionQueue.Samples[1]) && !executionQueue.IsRunning,
                    $"caption={infoCaption.ActualWidth}x{infoCaption.ActualHeight};desired={infoCaption.DesiredSize};footer={listFooter.ActualWidth}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-long-details-English-1280.png"));
            }
            finally { OpenVisionLanguageService.SetLanguage(listLanguageBefore, save: false); SettleFileList(); }
            fileInformation.IsExpanded = false;
            SettleFileList(completeTransitions: true);
            var fileSearch = FileListControl<TextBox>("InspectionSearch");
            fileSearch.SetCurrentValue(TextBox.TextProperty, "distinct-source-A");
            SettleFileList();
            Check("v3 rendered file search matches full paths and changes selection without touching checks", executionQueue.SearchText == "distinct-source-A"
                && executionQueue.VisibleSamples.Count == 1 && executionQueue.SelectedSample?.SourcePath == longListPaths[0] && executionQueue.CheckedCount == 2 && !executionQueue.IsRunning,
                $"search={executionQueue.SearchText};visible={executionQueue.VisibleSamples.Count};checked={executionQueue.CheckedCount}");
            var checkAllVisible = FileListControl<CheckBox>("InspectionCheckAllVisible");
            ActivateSampleChoice(checkAllVisible);
            SettleFileList();
            Check("v3 rendered footer toggles only displayed files while hidden inclusion remains checked", !executionQueue.Samples[0].IsChecked && executionQueue.Samples[1].IsChecked
                && !executionQueue.AreVisibleSamplesChecked && checkAllVisible.IsChecked == false && executionQueue.CheckedCount == 1 && !executionQueue.IsRunning,
                $"visibleChecked={executionQueue.AreVisibleSamplesChecked};hidden={executionQueue.Samples[1].IsChecked};checked={executionQueue.CheckedCount}");
            FileListControl<ButtonBase>("InspectionClearSearch").Command!.Execute(null);
            SettleFileList();
            ActivateSampleChoice(checkAllVisible);
            SettleFileList();
            Check("v3 rendered footer restores all displayed checks and model changes update its state", executionQueue.CheckedCount == 2 && checkAllVisible.IsChecked == true, executionQueue.ProcessingSummaryText);
            var filterError = FileListControl<RadioButton>("InspectionFilterError");
            ActivateSampleChoice(filterError);
            SettleFileList();
            Check("v3 rendered status filter shows a distinct no-match state and preserves queue inclusion", executionQueue.StatusFilter == InspectionQueueStatusFilter.Error
                && executionQueue.VisibleSamples.Count == 0 && executionQueue.AllCount == 2 && executionQueue.CheckedCount == 2
                && FileListControl<TextBlock>("InspectionQueueEmpty").Text == executionQueue.EmptyQueueText && filterError.IsChecked == true && !executionQueue.IsRunning,
                $"filter={executionQueue.StatusFilter};empty={executionQueue.EmptyQueueText};checked={executionQueue.CheckedCount}");
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-filter-empty-1280.png"));
            ActivateSampleChoice(FileListControl<RadioButton>("InspectionFilterAll"));
            SettleFileList();
            Check("v3 rendered All filter restores both sources without saving or running", executionQueue.VisibleSamples.Count == 2 && executionQueue.IsStatusFilterAll
                && inspectionShell.Workbench.Source.Path == listSourceBefore && inspectionShell.Workbench.IsDirty == executionDirtyBefore && !executionQueue.IsRunning,
                $"visible={executionQueue.VisibleSamples.Count};source={inspectionShell.Workbench.Source.Path};dirty={inspectionShell.Workbench.IsDirty}");
            executionQueue.ClearQueueCommand.Execute(null);
            SettleFileList();
            var inspectionCopies = Enumerable.Range(0, 128).Select(index =>
            {
                var path = Path.Combine(inspectionBoundaryRoot, $"input-{index:000}.C3D");
                File.Copy(copiedSampleSource, path);
                return path;
            }).ToArray();
            executionQueue.AddSources(inspectionCopies);
            ownerPathHost.Width = 1280;
            ownerPathHost.Height = 760;
            ownerPathHost.UpdateLayout();
            var queueView = FindVisualDescendants<InspectionQueueWorkspaceView>(ownerPathWorkbench).Single(control => control.IsVisible);
            var renderedInclude = FindVisualDescendants<CheckBox>(queueView).First(control => AutomationProperties.GetAutomationId(control) == "InspectionQueueInclude");
            var includedSample = (InspectionQueueSampleItem)renderedInclude.DataContext;
            renderedInclude.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
            ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            var uncheckedFromView = !includedSample.IsChecked && executionQueue.CheckedCount == 127;
            includedSample.IsChecked = true;
            ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
            Check("v3 rendered file check round trip updates the central actual count without inspection",
                uncheckedFromView && renderedInclude.IsChecked == true && executionQueue.CheckedCount == 128 && !executionQueue.IsRunning
                && ((Wpf.Ui.Controls.Button)executionRun).Content?.ToString() == executionQueue.RunScopeLabel
                && Equals(executionScope.SelectedValue, executionQueue.SelectedRunScope),
                $"viewToModel={uncheckedFromView};checked={executionQueue.CheckedCount};remaining={executionQueue.RemainingCount}");
            var inspectionContextBefore = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(ownerPathHost.Dispatcher));
            try
            {
                void WaitInspectionIdle()
                {
                    if (!SpinWait.SpinUntil(() =>
                        {
                            ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                            return !inspectionShell.Workbench.IsInspectionQueueRunning && !inspectionShell.Workbench.IsOrderedRunRunning;
                        }, TimeSpan.FromSeconds(30))) throw new TimeoutException("Rendered inspection did not restore idle within 30 seconds.");
                    ownerPathHost.UpdateLayout();
                }
                ActivateSampleChoice(executionRun);
                ActivateSampleChoice(executionRun);
                ownerPathHost.UpdateLayout();
                Check("v3 central checked Run is guarded and displays actual progress and Stop",
                    executionQueue.IsRunning && !executionRun.IsVisible && executionStop.IsVisible && executionStop.IsEnabled
                    && !executionQueue.RunCheckedCommand.CanExecute(null) && !executionQueue.RunRemainingCommand.CanExecute(null)
                    && executionProgressText.Text == executionQueue.ProgressText && ReferenceEquals(executionRun.Command, executionQueue.RunScopeCommand),
                    $"running={executionQueue.IsRunning};stop={executionStop.IsEnabled};progress={executionProgressText.Text}");
                Check("v3 running inspection disables file inclusion, import, remove and footer but preserves selection and filtering",
                    !renderedInclude.IsEnabled && !FileListControl<ButtonBase>("InspectionAddFiles").IsEnabled && !FileListControl<ButtonBase>("InspectionRemoveSelected").IsEnabled
                    && !FileListControl<CheckBox>("InspectionCheckAllVisible").IsEnabled && FileListControl<RadioButton>("InspectionFilterAll").IsEnabled
                    && FindVisualDescendants<ButtonBase>(queueView).Where(control => AutomationProperties.GetAutomationId(control) == "InspectionQueueSelectSample").All(control => control.IsEnabled),
                    $"include={renderedInclude.IsEnabled};add={FileListControl<ButtonBase>("InspectionAddFiles").IsEnabled};footer={FileListControl<CheckBox>("InspectionCheckAllVisible").IsEnabled}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d20-running-1280.png"));
                ActivateSampleChoice(executionStop);
                WaitInspectionIdle();
                Check("v3 central Stop restores actual canceled state and enabled explicit actions",
                    executionQueue.CancelledCount > 0 && executionRun.IsVisible && executionRun.IsEnabled && !executionStop.IsVisible
                    && inspectionShell.Workbench.Source.Path == executionSourceBefore && inspectionShell.Workbench.IsDirty == executionDirtyBefore
                    && executionProgressText.Text == executionQueue.ProgressText && executionQueue.RemainingCount > 0,
                    $"cancelled={executionQueue.CancelledCount};remaining={executionQueue.RemainingCount};summary={executionQueue.SummaryText}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d20-stopped-1280.png"));
                executionQueue.ClearQueueCommand.Execute(null);
                executionQueue.AddSources(inspectionCopies.Take(2));
                ActivateSampleChoice(executionRun);
                WaitInspectionIdle();
                var firstCompletedRecord = executionQueue.Samples[0].Record;
                Check("v3 explicit central checked Run recovers to real saved results and completion progress",
                    executionQueue.PassCount == 2 && executionQueue.ErrorCount == 0 && executionQueue.RemainingCount == 0 && executionProgress.Value == 100
                    && executionQueue.Samples.All(item => item.CanOpenCurrentRunRecord && File.Exists(item.RunRecordPath))
                    && executionBasis.Text == Path.GetFileName(inspectionRecipePath) && executionRun.IsEnabled && executionScope.IsEnabled,
                    $"pass={executionQueue.PassCount};error={executionQueue.ErrorCount};progress={executionProgress.Value};basis={executionBasis.Text}");
                SettleFileList();
                var elapsedLabels = FindVisualDescendants<TextBlock>(fileListView).Where(label => AutomationProperties.GetAutomationId(label) == "InspectionQueueFileElapsed").ToArray();
                Check("D23 actual SDK records render their file elapsed and full OK rate", DistributionMatches(executionQueue) && executionQueue.PassRateText == "100.0%"
                    && elapsedLabels.Length == 2 && elapsedLabels.All(label => label.Text == ((InspectionQueueSampleItem)label.DataContext).ElapsedText
                        && ((InspectionQueueSampleItem)label.DataContext).ElapsedMilliseconds == ((InspectionQueueSampleItem)label.DataContext).Record!.ElapsedMilliseconds)
                    && RateLabelText().EndsWith("100.0%", StringComparison.Ordinal), executionQueue.CountsText);
                foreach (var inspectionWidth in new[] { 1280d, 1920d })
                {
                    ownerPathHost.Width = inspectionWidth;
                    ownerPathHost.Height = inspectionWidth == 1280 ? 760 : 1040;
                    ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                    ownerPathHost.UpdateLayout();
                    ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                    ownerPathHost.UpdateLayout();
                    ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownerPathHost, monitorReportPath);
                    Check($"v3 completed inspection keeps the visible central bar and results composition at {inspectionWidth:0}px",
                        executionBar.IsVisible && executionBar.ActualHeight == 44 && ownerPathWorkbench.HasValidateStageComposition
                        && executionRun.IsVisible && executionRun.IsEnabled && executionScope.IsEnabled && executionProgress.Value == 100
                        && ReferenceEquals(firstCompletedRecord, executionQueue.Samples[0].Record),
                        $"visible={executionBar.IsVisible};composed={ownerPathWorkbench.HasValidateStageComposition};bar={executionBar.ActualWidth}x{executionBar.ActualHeight};pass={executionQueue.PassCount};record={firstCompletedRecord?.RunId}");
                    Check($"v3 real completed output table agrees with recorded results at{inspectionWidth:0}px", FileListControl<DataGrid>("InspectionStepResults").Items.Count == executionQueue.VisibleOutputResults.Count
                        && executionQueue.SelectedResultNotice == string.Empty && !executionQueue.IsRunning, $"outputs={executionQueue.VisibleOutputResults.Count}");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d20-completed-{inspectionWidth:0}.png"));
                }
                ownerPathHost.Width = 1280;
                ownerPathHost.Height = 760;
                SettleFileList(completeTransitions: true);
                var resultsView = FileListControl<ResultsWorkspaceView>("ResultsWorkspace");
                var resultsTabs = FileListControl<TabControl>("InspectionResultsTabs");
                void SelectResultsTab(int index)
                {
                    var peer = new TabItemAutomationPeer(resultsTabs.Items[index], new TabControlAutomationPeer(resultsTabs));
                    ((ISelectionItemProvider)peer.GetPattern(PatternInterface.SelectionItem)!).Select();
                    SettleFileList(completeTransitions: true);
                }
                SelectResultsTab(0);
                var resultsHeader = FileListControl<Border>("InspectionResultsHeader");
                var resultPin = FileListControl<ButtonBase>("InspectionResultsAutoHide");
                var resultPane = (AvalonDock.Layout.LayoutAnchorable)fileListDock.FindName("resultsAnchorable");
                var resultDockItem = fileListDock.ResultsPaneLayoutItem;
                var resultLayoutControl = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorableControl>(fileListDock).Single(control => ReferenceEquals(control.Model, resultPane));
                var oldResultHeader = (Border)resultLayoutControl.Template.FindName("Header", resultLayoutControl);
                Check("v3 inspection results has one34px header, three actual tabs and the same native pin command", resultsHeader.ActualHeight == 34 && resultsTabs.Items.Count == 3
                    && oldResultHeader.Visibility == Visibility.Collapsed && ReferenceEquals(resultPin.Command, resultDockItem?.AutoHideCommand)
                    && ReferenceEquals(resultsView.DockPaneCommands, resultDockItem) && ReferenceEquals(resultsTabs.DataContext, executionQueue),
                    $"header={resultsHeader.ActualWidth}x{resultsHeader.ActualHeight};oldHeader={oldResultHeader.Visibility};tabs={resultsTabs.Items.Count}");
                var outputGrid = FileListControl<DataGrid>("InspectionStepResults");
                var realOutputCount = executionQueue.SelectedSample!.Steps.Sum(step => step.Outputs.Count);
                Check("v3 actual SDK results display recorded outputs without rerunning or replacing saved records", outputGrid.Items.Count == realOutputCount && realOutputCount > 0
                    && outputGrid.Columns.Count == 8 && !executionQueue.IsRunning && ReferenceEquals(firstCompletedRecord, executionQueue.Samples[0].Record),
                    $"outputs={outputGrid.Items.Count};source={executionQueue.SelectedSample.SourcePath};record={firstCompletedRecord?.RunId}");
                var resultMore = FileListControl<MenuItem>("InspectionResultsMenu");
                resultMore.IsSubmenuOpen = true;
                SettleFileList();
                var resultPopup = FindVisualDescendants<Popup>(resultMore).First(popup => popup.IsOpen);
                var resultActions = resultMore.Items.OfType<MenuItem>().ToArray();
                Check("v3 result menu preserves records, CSV, teaching and native docking commands", resultPopup.Child is { IsVisible: true } && resultActions.Length == 6
                    && ReferenceEquals(resultMore.Template, resultsView.FindResource("R19TopMenuItemTemplate"))
                    && resultActions.All(item => item.Command is not null) && !executionQueue.IsRunning, $"actions={resultActions.Length};popup={resultPopup.IsOpen}");
                if (resultPopup.Child is FrameworkElement resultPopupChild)
                {
                    var popupVisual = new DrawingVisual();
                    using (var drawing = popupVisual.RenderOpen()) drawing.DrawRectangle(new VisualBrush(resultPopupChild), null, new Rect(resultPopupChild.RenderSize));
                    var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(resultPopupChild.ActualWidth), (int)Math.Ceiling(resultPopupChild.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(popupVisual);
                    WpfScreenshotCapture.Save(bitmap, Path.ChangeExtension(reportPath, ".d22-result-menu.png"));
                }
                resultMore.IsSubmenuOpen = false;
                SettleFileList();
                ActivateSampleChoice(resultPin);
                SettleFileList();
                Check("v3 result pin auto hides to the existing accessible native side tab", resultPane.IsAutoHidden
                    && FindVisualDescendants<AvalonDock.Controls.LayoutAnchorControl>(fileListDock).Any(tab => ReferenceEquals(tab.Model, resultPane) && tab.IsVisible)
                    && !executionQueue.IsRunning, $"hidden={resultPane.IsAutoHidden}");
                resultDockItem!.AutoHideCommand.Execute(null);
                SettleFileList(completeTransitions: true);
                Check("v3 result native pin restores the same result tabs and recorded queue", !resultPane.IsAutoHidden && resultsView.IsVisible && resultsTabs.IsVisible
                    && ReferenceEquals(resultsView.DockPaneCommands, fileListDock.ResultsPaneLayoutItem) && !executionQueue.IsRunning, $"hidden={resultPane.IsAutoHidden};tabs={resultsTabs.IsVisible}");

                // Display-only multi-output fixture: retains a real saved run identity; does not claim SDK verdict coverage.
                var resultFixtureStep = firstCompletedRecord!.Steps![0] with
                {
                    ToolId = "thickness", ToolName = "장착방향과 표면상태를 확인하는 긴 출력 도구 이름 0123456789", Status = ResultStatus.Fail,
                    Message = "Isolated output render fixture · second output uses a distinct unit and stored verdict; the long cause must remain readable at Compact width.",
                    Metrics = [new("LowerLimit", MetricKind.Length, 7.75, "raw-height", null), new("UpperLimit", MetricKind.Length, 8.25, "raw-height", null),
                        new("Mean", MetricKind.Length, 8.250000000000002, "raw-height", ResultStatus.Fail), new("Secondary ratio", MetricKind.Number, 0.12345678901234567, "ratio", ResultStatus.Pass)]
                };
                var resultFixtureRecord = firstCompletedRecord with { Status = ResultStatus.Fail, Steps = [resultFixtureStep] };
                executionQueue.Samples[0].SetRecord(resultFixtureRecord, executionQueue.Samples[0].RunRecordPath!);
                executionQueue.SelectedSample = executionQueue.Samples[0];
                SettleFileList(completeTransitions: true);
                outputGrid = FileListControl<DataGrid>("InspectionStepResults");
                outputGrid.SelectedItem = executionQueue.VisibleOutputResults[1];
                SettleFileList();
                Check("v3 rendered output selection writes the same canonical step without losing the second output", ReferenceEquals(outputGrid.SelectedItem, executionQueue.SelectedOutputResult)
                    && executionQueue.SelectedOutputResult?.OutputName == "Secondary ratio" && ReferenceEquals(executionQueue.SelectedOutputResult.Step, executionQueue.SelectedStepResult)
                    && ReferenceEquals(executionQueue.SelectedSample, executionQueue.Samples[0]) && !executionQueue.IsRunning,
                    $"output={executionQueue.SelectedOutputResult?.OutputName};step={executionQueue.SelectedStepResult?.StepId}");
                ActivateSampleChoice(FileListControl<CheckBox>("InspectionFailedStepsOnly"));
                SettleFileList();
                Check("v3 rendered NG toggle preserves step filtering and independent output verdicts", executionQueue.ShowOnlyFailedSteps && outputGrid.Items.Count == 2
                    && executionQueue.VisibleOutputResults[1].Status == "Pass" && !executionQueue.IsRunning, $"filtered={outputGrid.Items.Count};secondary={executionQueue.VisibleOutputResults[1].Status}");
                ActivateSampleChoice(FileListControl<CheckBox>("InspectionFailedStepsOnly"));
                SettleFileList();
                outputGrid.ScrollIntoView(outputGrid.SelectedItem);
                SettleFileList();
                var outputDetails = FileListControl<Expander>("InspectionOutputDetails");
                outputDetails.IsExpanded = true;
                SettleFileList(completeTransitions: true);
                var outputDetailScroller = FindVisualDescendants<ScrollViewer>(outputGrid).First();
                var detailsPresenter = FindVisualDescendants<DataGridDetailsPresenter>(outputGrid).Single(presenter => ReferenceEquals(presenter.DataContext, executionQueue.SelectedOutputResult));
                detailsPresenter.BringIntoView(new Rect(0, 0, detailsPresenter.ActualWidth, 40));
                SettleFileList(completeTransitions: true);
                var detailsLimits = FindVisualDescendants<TextBlock>(outputDetails).Single(label => label.Text == "7.75–8.25 raw-height");
                var limitsBounds = detailsLimits.TransformToAncestor(outputGrid).TransformBounds(new Rect(detailsLimits.RenderSize));
                var outputHeaderHeight = FindVisualDescendants<DataGridColumnHeader>(outputGrid).Where(header => header.Column is not null).Max(header => header.ActualHeight);
                outputDetailScroller.ScrollToVerticalOffset(outputDetailScroller.VerticalOffset + limitsBounds.Top - outputHeaderHeight - 4);
                SettleFileList(completeTransitions: true);
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d22-output-details-top-1280.png"));
                outputDetailScroller.ScrollToBottom();
                SettleFileList(completeTransitions: true);
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d22-output-details-1280.png"));
                Check("v3 output details retain the exact stored metrics and original execution limits", FindVisualDescendants<TextBlock>(outputDetails).Any(label => label.Text.Contains("Mean: 8.2500000000000018 raw-height", StringComparison.Ordinal))
                    && FindVisualDescendants<TextBlock>(outputDetails).Any(label => label.Text == "7.75–8.25 raw-height") && !executionQueue.IsRunning,
                    $"details={outputDetails.IsExpanded};limits={executionQueue.SelectedStepResult?.Limits}");
                outputDetails.IsExpanded = false;
                foreach (var resultWidth in new[] { 1280d, 1920d })
                {
                    ownerPathHost.Width = resultWidth;
                    ownerPathHost.Height = resultWidth == 1280 ? 760 : 1040;
                    SettleFileList(completeTransitions: true);
                    var valueLabels = FindVisualDescendants<TextBlock>(outputGrid).Where(label => label.DataContext is InspectionQueueOutputResultItem && label.IsVisible).ToArray();
                    var resultScroller = FindVisualDescendants<ScrollViewer>(outputGrid).First();
                    Check($"v3 output values, units and causes wrap or scroll without disabling access at{resultWidth:0}px", outputGrid.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto
                        && valueLabels.Any(label => label.Text == "8.250000000000002 raw-height" && label.TextWrapping == TextWrapping.Wrap)
                        && valueLabels.Any(label => label.Text == "0.12345678901234566 ratio")
                        && valueLabels.Where(label => label.TextWrapping == TextWrapping.Wrap).All(label => label.ActualHeight + label.Margin.Top + label.Margin.Bottom + 1 >= label.DesiredSize.Height)
                        && resultScroller.ViewportWidth > 0 && resultsHeader.ActualHeight == 34 && !executionQueue.IsRunning,
                        $"labels={valueLabels.Length};viewport={resultScroller.ViewportWidth}x{resultScroller.ViewportHeight};extent={resultScroller.ExtentWidth}x{resultScroller.ExtentHeight};text={string.Join(";", valueLabels.Select(label => $"{label.Text}:{label.ActualHeight}+{label.Margin.Top + label.Margin.Bottom}/{label.DesiredSize.Height}"))}");
                    ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownerPathHost, monitorReportPath);
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d22-output-results-{resultWidth:0}.png"));
                }
                ownerPathHost.Width = 1280;
                ownerPathHost.Height = 760;
                var d25FixtureStep = resultFixtureStep with
                {
                    ToolId = "warpage", Message = "Rms exceeds its recorded upper limit. Isolated display fixture; preserve this stored verdict and the full cause at Compact width.", Metrics = [new("PeakToValley", MetricKind.Length, .15, "mm", ResultStatus.Pass),
                        new("Rms", MetricKind.Length, .34567890123456789, "mm", ResultStatus.Fail), new("MaximumPeakToValley", MetricKind.Length, .2, "mm", null),
                        new("MaximumRms", MetricKind.Length, .3, "mm", null)]
                };
                var d25FixtureRecord = firstCompletedRecord with { Status = ResultStatus.Fail, Steps = [d25FixtureStep] };
                executionQueue.Samples[0].SetRecord(d25FixtureRecord, executionQueue.Samples[0].RunRecordPath!);
                executionQueue.SelectedSample = executionQueue.Samples[0];
                var d25Cycle = 0;
                foreach (var d25Width in new[] { 1280d, 1920d, 1280d })
                {
                    ownerPathHost.Width = d25Width;
                    ownerPathHost.Height = d25Width == 1280 ? 760 : 1040;
                    SettleFileList(completeTransitions: true);
                    outputGrid.SelectedItem = executionQueue.VisibleOutputResults[1];
                    var d25Scroll = FindVisualDescendants<ScrollViewer>(outputGrid).First();
                    d25Scroll.ScrollToTop();
                    SettleFileList(completeTransitions: true);
                    var d25Labels = FindVisualDescendants<TextBlock>(outputGrid).Where(label => label.IsVisible).ToArray();
                    Check($"D25{d25Width:0} output rows show exact secondary range and keep two-way selection", outputGrid.Items.Count == 2
                        && executionQueue.VisibleOutputResults[1].Limits == "≤ 0.3 mm"
                        && d25Labels.Any(label => label.Text == executionQueue.VisibleOutputResults[1].Limits)
                        && d25Labels.Any(label => label.Text == executionQueue.VisibleOutputResults[1].ValueWithUnit)
                        && ReferenceEquals(outputGrid.SelectedItem, executionQueue.SelectedOutputResult) && ReferenceEquals(executionQueue.SelectedOutputResult?.Step, executionQueue.SelectedStepResult)
                        && ReferenceEquals(d25FixtureRecord, executionQueue.Samples[0].Record) && !executionQueue.IsRunning, $"range={executionQueue.VisibleOutputResults[1].Limits};value={executionQueue.VisibleOutputResults[1].ValueWithUnit}");
                    Check($"D25{d25Width:0} density, wrapped headers and complete numeric cells remain readable", outputGrid.MinRowHeight == 35 && outputGrid.FontSize == 12.5
                        && FindVisualDescendants<DataGridColumnHeader>(outputGrid).Count(header => header.IsVisible && header.Column is not null) == 8
                        && outputGrid.Columns.All(column => column.ActualWidth + .5 >= column.MinWidth)
                        && FindVisualDescendants<DataGridRow>(outputGrid).Where(row => row.DataContext is InspectionQueueOutputResultItem).All(row => AutomationProperties.GetName(row).Contains(((InspectionQueueOutputResultItem)row.DataContext).ToolName, StringComparison.Ordinal))
                        && d25Labels.Where(label => label.DataContext is InspectionQueueOutputResultItem && label.TextWrapping == TextWrapping.Wrap)
                            .All(label => label.ActualHeight + label.Margin.Top + label.Margin.Bottom + 1 >= label.DesiredSize.Height)
                        && d25Scroll.ExtentWidth <= d25Scroll.ViewportWidth + 1, $"viewport={d25Scroll.ViewportWidth};extent={d25Scroll.ExtentWidth};font={outputGrid.FontSize};widths={string.Join(',', outputGrid.Columns.Select(column => column.ActualWidth))}");
                    var d25Rows = FindVisualDescendants<DataGridRow>(outputGrid).Where(row => row.DataContext is InspectionQueueOutputResultItem).ToArray();
                    Check($"D25{d25Width:0} normal and selected rows retain semantic backgrounds and full tool identity", d25Rows.Length == 2
                        && d25Rows.All(row => row.Background is SolidColorBrush brush && brush.Color != Colors.White)
                        && d25Rows.Any(row => row.IsSelected) && executionQueue.VisibleOutputResults[0].DisplayLimits == "≤ 0.2 mm"
                        && executionQueue.VisibleOutputResults[0].Limits == executionQueue.VisibleOutputResults[0].Step.Limits
                        && executionQueue.VisibleOutputResults[1].CauseLabel == string.Empty, "row style reuses existing semantic surface;primary contract stays in details");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d25-output-{++d25Cycle}-{d25Width:0}.png"));
                    d25Scroll.ScrollToBottom();
                    SettleFileList(completeTransitions: true);
                    var d25RangeLabel = FindVisualDescendants<TextBlock>(outputGrid).First(label => label.IsVisible && label.Text == "≤ 0.3 mm");
                    var d25RangeRect = d25RangeLabel.TransformToAncestor(outputGrid).TransformBounds(new Rect(d25RangeLabel.RenderSize));
                    Check($"D25{d25Width:0} secondary range remains reachable at the bottom after resize", d25RangeRect.Top >= 0 && d25RangeRect.Bottom <= outputGrid.ActualHeight + 1
                        && !executionQueue.IsRunning, $"range={d25RangeRect};gridHeight={outputGrid.ActualHeight}");
                    var d25ValueLabel = FindVisualDescendants<TextBlock>(outputGrid).Single(label => label.IsVisible && label.Text == executionQueue.VisibleOutputResults[1].ValueWithUnit);
                    var d25NumericText = new FormattedText(executionQueue.VisibleOutputResults[1].Value, CultureInfo.InvariantCulture, d25ValueLabel.FlowDirection,
                        new Typeface(d25ValueLabel.FontFamily, d25ValueLabel.FontStyle, d25ValueLabel.FontWeight, d25ValueLabel.FontStretch), d25ValueLabel.FontSize, d25ValueLabel.Foreground, VisualTreeHelper.GetDpi(d25ValueLabel).PixelsPerDip);
                    Check($"D25{d25Width:0} the exact numeric value fits one line before the unit", d25NumericText.Width <= d25ValueLabel.ActualWidth + .5,
                        $"numericWidth={d25NumericText.Width};available={d25ValueLabel.ActualWidth};value={executionQueue.VisibleOutputResults[1].Value}");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d25-output-bottom-{d25Cycle}-{d25Width:0}.png"));
                }
                var d25OldLanguage = OpenVisionLanguageService.CurrentLanguage;
                try
                {
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    executionQueue.RefreshResultPresentation();
                    SettleFileList(completeTransitions: true);
                    Check("D25 English Compact headers and stored limits retain their identities", outputGrid.Columns[4].Header?.ToString() == "Allowable range"
                        && executionQueue.VisibleOutputResults[1].Limits == "≤ 0.3 mm" && !executionQueue.IsRunning, outputGrid.Columns[4].Header?.ToString() ?? "missing");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d25-English-output-1280.png"));
                }
                finally { OpenVisionLanguageService.SetLanguage(d25OldLanguage, save: false); executionQueue.RefreshResultPresentation(); }
                executionQueue.Samples[0].SetRecord(d25FixtureRecord with { Steps = [d25FixtureStep with { Metrics = d25FixtureStep.Metrics.Where(metric => metric.Name != "MaximumRms").Concat([new InspectionRunMetric("MaximumRms", MetricKind.Number, .3, "wrong-unit", null)]).ToArray() }] }, executionQueue.Samples[0].RunRecordPath!);
                SettleFileList(completeTransitions: true);
                Check("D25 malformed range drops the secondary limit while preserving stored NG", executionQueue.VisibleOutputResults[1].Limits == "—"
                    && executionQueue.VisibleOutputResults[1].Status == "Fail" && !executionQueue.IsRunning, executionQueue.VisibleOutputResults[1].Limits);
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d25-invalid-range-1280.png"));
                executionQueue.Samples[0].SetRecord(d25FixtureRecord, executionQueue.Samples[0].RunRecordPath!);
                SettleFileList(completeTransitions: true);
                outputGrid.SelectedItem = executionQueue.VisibleOutputResults[1];
                SettleFileList();
                Check("D25 valid recorded range recovers without Run or stale selection", executionQueue.SelectedOutputResult?.Limits == "≤ 0.3 mm"
                    && ReferenceEquals(d25FixtureRecord, executionQueue.SelectedSample?.Record) && !executionQueue.IsRunning, executionQueue.SelectedOutputResult?.Limits ?? "missing");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d25-range-recovery-1280.png"));
                executionQueue.Samples[0].SetRecord(resultFixtureRecord, executionQueue.Samples[0].RunRecordPath!);
                ownerPathHost.Width = 1280;
                ownerPathHost.Height = 760;
                SelectResultsTab(1);
                var resultSummaryGrid = FileListControl<DataGrid>("InspectionMeasurementSummaries");
                Check("v3 actual summary tab keeps recorded-unit groups and read-only scrollable values", resultSummaryGrid.Items.Count == executionQueue.MeasurementSummaries.Count
                    && resultSummaryGrid.IsReadOnly && resultSummaryGrid.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto && !executionQueue.IsRunning,
                    $"groups={resultSummaryGrid.Items.Count};selectedTab={resultsTabs.SelectedIndex}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d22-summary-1280.png"));
                var actualSavedSummaries = executionQueue.MeasurementSummaries;
                Check("D24 summary projection fixtures read Record values and identities", actualSavedSummaries.Count > 0
                    && actualSavedSummaries.All(row => row.Points.All(point => executionQueue.Samples.Any(sample => sample.SourcePath == point.SourcePath && sample.Record?.RunId == point.RunId)))
                    && FindVisualDescendants<InspectionSummaryPlot>(resultSummaryGrid).Any(plot => plot.IsVisible && plot.Summary?.Points.Count > 0), $"groups={actualSavedSummaries.Count};points={actualSavedSummaries.Sum(row => row.Points.Count)}");
                var d24Records = executionQueue.Samples.Take(2).Select(sample => (Sample: sample, Record: sample.Record!, Path: sample.RunRecordPath!)).ToArray();
                try
                {
                    var d24Step = d24Records[0].Record.Steps![0] with { ToolId = "thickness", ToolName = "Thickness", SemanticFingerprint = new string('a', 64), Status = ResultStatus.Pass, Message = "Recorded fixture", Metrics =
                    [new("Mean", MetricKind.Length, 8.125, "mm", ResultStatus.Pass), new("LowerLimit", MetricKind.Length, 7.75, "mm", null), new("UpperLimit", MetricKind.Length, 8.25, "mm", null)] };
                    var d24FixtureA = d24Records[0].Record with { Steps = [d24Step] };
                    var d24FixtureB = d24Records[1].Record with { Status = ResultStatus.Fail, Steps = [d24Step with { Metrics =
                    [new("Mean", MetricKind.Length, 8.5, "mm", ResultStatus.Fail), new("LowerLimit", MetricKind.Length, 7.75, "mm", null), new("UpperLimit", MetricKind.Length, 8.25, "mm", null)] }] };
                    d24Records[0].Sample.SetRecord(d24FixtureA, d24Records[0].Path);
                    d24Records[1].Sample.SetRecord(d24FixtureB, d24Records[1].Path);
                    foreach (var width in new[] { 1280d, 1920d })
                    {
                        ownerPathHost.Width = width;
                        ownerPathHost.Height = width == 1280 ? 760 : 1040;
                        SettleFileList(completeTransitions: true);
                        var plot = FindVisualDescendants<InspectionSummaryPlot>(resultSummaryGrid).Single(plot => plot.IsVisible);
                        var drawing = VisualTreeHelper.GetDrawing(plot);
                        var shapes = drawing.Children.OfType<GeometryDrawing>().ToArray();
                        Check($"D24 summary renders a24px saved-range band, stored OK circle and NG diamond at{width:0}px", plot.ActualWidth == 180 && plot.ActualHeight == 24 && plot.ClipToBounds
                            && plot.Summary is { RangeText: "7.75–8.25 mm", PassCount: 1, FailCount: 1, Mean: 8.3125 } && shapes.Any(shape => shape.Geometry is EllipseGeometry)
                            && shapes.Any(shape => shape.Geometry is StreamGeometry) && shapes.Any(shape => shape.Geometry is RectangleGeometry)
                            && double.IsFinite(drawing.Bounds.Top) && drawing.Bounds.Bottom <= 24.3 && !executionQueue.IsRunning,
                            $"size={plot.ActualWidth}x{plot.ActualHeight};shapes={shapes.Length};bounds={drawing.Bounds};range={plot.Summary?.RangeText}");
                        var numericLabels = FindVisualDescendants<TextBlock>(resultSummaryGrid).Where(label => label.IsVisible && label.DataContext is InspectionQueueMeasurementSummary && label.Text is "8.125" or "8.3125" or "8.5").ToArray();
                        Check($"D24 summary uses9columns and visible12.5px monospace values without clipping at{width:0}px", resultSummaryGrid.Columns.Count == 9 && resultSummaryGrid.MinRowHeight == 35
                            && numericLabels.Length == 3 && numericLabels.All(label => label.FontSize == 12.5 && label.FontFamily.Source.Contains("Cascadia Mono", StringComparison.Ordinal)
                                && label.TextWrapping == TextWrapping.Wrap && label.ActualHeight + label.Margin.Top + label.Margin.Bottom + 1 >= label.DesiredSize.Height)
                            && plot.Summary!.OutputLabel == "Mean (mm)" && !executionQueue.IsRunning, $"labels={numericLabels.Length};columns={resultSummaryGrid.Columns.Count};font={resultSummaryGrid.FontSize}");
                        ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownerPathHost, monitorReportPath);
                        WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d24-summary-{width:0}.png"));
                    }
                    ownerPathHost.Width = 1280;
                    ownerPathHost.Height = 760;
                    foreach (var boundary in new[] { (Name: "coincident-zero", Value: 0d, Lower: -.25, Upper: .25), (Name: "large-finite", Value: 1e308, Lower: -1e308, Upper: 1e308) })
                    {
                        var boundaryStep = d24Step with { Metrics = [new("Mean", MetricKind.Length, boundary.Value, "mm", ResultStatus.Pass), new("LowerLimit", MetricKind.Length, boundary.Lower, "mm", null), new("UpperLimit", MetricKind.Length, boundary.Upper, "mm", null)] };
                        d24Records[0].Sample.SetRecord(d24FixtureA with { Steps = [boundaryStep] }, d24Records[0].Path);
                        d24Records[1].Sample.SetRecord(d24FixtureB with { Steps = [boundaryStep with { Metrics = [new("Mean", MetricKind.Length, boundary.Value, "mm", ResultStatus.Fail), new("LowerLimit", MetricKind.Length, boundary.Lower, "mm", null), new("UpperLimit", MetricKind.Length, boundary.Upper, "mm", null)] }] }, d24Records[1].Path);
                        SettleFileList(completeTransitions: true);
                        var countColumns = resultSummaryGrid.Columns.Skip(2).Take(3).ToArray();
                        var countHeaders = FindVisualDescendants<DataGridColumnHeader>(resultSummaryGrid).Where(header => countColumns.Contains(header.Column)).ToArray();
                        Check($"D24 {boundary.Name} keeps OK, NG and error columns visible after Wide to Compact resize", countColumns.Select((column, index) => column.ActualWidth + .5 >= (index == 2 ? 58 : 44)).All(visible => visible)
                            && countHeaders.Length == 3 && countHeaders.All(header => header.IsVisible && header.ActualWidth >= 43.5 && header.ActualHeight > 0)
                            && FindVisualDescendants<DataGridCell>(resultSummaryGrid).Where(cell => countColumns.Contains(cell.Column)).Count(cell => cell.IsVisible && cell.ActualWidth >= 43.5) == 3,
                            $"widths={string.Join(',', resultSummaryGrid.Columns.Select(column => column.ActualWidth))};headers={countHeaders.Length}");
                        var boundaryPlot = FindVisualDescendants<InspectionSummaryPlot>(resultSummaryGrid).Single(plot => plot.IsVisible);
                        var boundaryDrawing = VisualTreeHelper.GetDrawing(boundaryPlot);
                        Check($"D24 {boundary.Name} renders finite positions with unchanged stored statuses", boundaryPlot.Summary!.Points.All(point => point.Value == boundary.Value)
                            && boundaryPlot.Summary.Points[0].Status == ResultStatus.Pass && boundaryPlot.Summary.Points[1].Status == ResultStatus.Fail
                            && double.IsFinite(boundaryDrawing.Bounds.Top) && double.IsFinite(boundaryDrawing.Bounds.Bottom)
                            && boundaryDrawing.Children.OfType<GeometryDrawing>().Any(shape => shape.Geometry is EllipseGeometry)
                            && boundaryDrawing.Children.OfType<GeometryDrawing>().Any(shape => shape.Geometry is StreamGeometry) && !executionQueue.IsRunning,
                            $"value={boundary.Value:G17};bounds={boundaryDrawing.Bounds}");
                        WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d24-{boundary.Name}-1280.png"));
                    }
                    d24Records[0].Sample.SetRecord(d24FixtureA, d24Records[0].Path);
                    d24Records[1].Sample.SetRecord(d24FixtureB with { Status = ResultStatus.Warning, Steps = [d24Step with { SemanticFingerprint = new string('b',64), Metrics = [new("Mean", MetricKind.Length, 8.2, "mm", ResultStatus.Warning)] }] }, d24Records[1].Path);
                    SettleFileList(completeTransitions: true);
                    var settingsPlots = FindVisualDescendants<InspectionSummaryPlot>(resultSummaryGrid).Where(plot => plot.IsVisible).ToArray();
                    var warningPlot = settingsPlots.Single(plot => plot.Summary!.Points[0].Status == ResultStatus.Warning);
                    Check("D24 separate settings retain visible identities and a recorded Warning marker", resultSummaryGrid.Items.Count == 2
                        && settingsPlots.All(plot => plot.Summary!.HasMultipleSettings)
                        && FindVisualDescendants<TextBlock>(resultSummaryGrid).Any(label => label.IsVisible && label.Text == new string('b',10))
                        && VisualTreeHelper.GetDrawing(warningPlot).Children.OfType<GeometryDrawing>().Any(shape => shape.Geometry is StreamGeometry
                            && ((SolidColorBrush)shape.Brush).Color == ((SolidColorBrush)warningPlot.FindResource("ThreeD.WarningBrush")).Color)
                        && warningPlot.Summary!.PassCount == 0 && !executionQueue.IsRunning, "settings a/b visible;Warning stays Warning");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-warning-settings-1280.png"));
                    d24Records[1].Sample.SetRecord(d24FixtureB, d24Records[1].Path);
                    executionQueue.SearchText = "no-visible-files";
                    executionQueue.SetStatusFilterCommand.Execute("Fail");
                    d24Records[0].Sample.IsChecked = false;
                    SettleFileList(completeTransitions: true);
                    Check("D24 rendered summary preserves whole-queue points after search, filter and check changes", executionQueue.VisibleSamples.Count == 0 && resultSummaryGrid.Items.Count == 1
                        && ((InspectionQueueMeasurementSummary)resultSummaryGrid.Items[0]).Points.Count == 2 && !executionQueue.IsRunning, "search+NG+check;two stored files remain");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-filtered-summary-1280.png"));
                    executionQueue.SearchText = string.Empty;
                    executionQueue.SetStatusFilterCommand.Execute("All");
                    d24Records[0].Sample.IsChecked = true;
                    var d24Language = OpenVisionLanguageService.CurrentLanguage;
                    try
                    {
                        OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                        executionQueue.RefreshResultPresentation();
                        SettleFileList(completeTransitions: true);
                        Check("D24 English summary headers wrap, retain units and keep records unchanged", resultSummaryGrid.Columns.Count == 9
                            && resultSummaryGrid.Columns.Last().Header?.ToString() == "Values by file · Allowable range"
                            && FindVisualDescendants<System.Windows.Controls.Primitives.DataGridColumnHeader>(resultSummaryGrid).Where(header => header.IsVisible).All(header => header.ActualHeight + 1 >= header.DesiredSize.Height)
                            && ReferenceEquals(d24FixtureA, d24Records[0].Sample.Record) && !executionQueue.IsRunning, "English range header;no new Run");
                        WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-English-summary-1280.png"));
                    }
                    finally { OpenVisionLanguageService.SetLanguage(d24Language, save: false); executionQueue.RefreshResultPresentation(); }
                    SettleFileList(completeTransitions: true);
                    resultSummaryGrid.SelectedItem = resultSummaryGrid.Items[0];
                    SettleFileList(completeTransitions: true);
                    var summaryDetails = FileListControl<Expander>("InspectionSummaryDetails");
                    summaryDetails.IsExpanded = true;
                    SettleFileList(completeTransitions: true);
                    var summaryFiles = FileListControl<ItemsControl>("InspectionSummaryFileValues");
                    Check("D24 actual row details expose full file values, saved range and settings without Run", summaryFiles.Items.Count == 2
                        && FileListControl<TextBlock>("InspectionSummarySettings").Text == new string('a', 64)
                        && FindVisualDescendants<TextBlock>(summaryFiles).Any(label => label.Text == d24Records[1].Sample.SourcePath && label.TextWrapping == TextWrapping.Wrap)
                        && FindVisualDescendants<TextBlock>(summaryFiles).Any(label => label.Text == "8.5") && !executionQueue.IsRunning, $"files={summaryFiles.Items.Count};details={summaryDetails.IsExpanded}");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-details-1280.png"));
                    var summaryScroller = FindVisualDescendants<ScrollViewer>(resultSummaryGrid).First();
                    summaryScroller.ScrollToBottom();
                    SettleFileList(completeTransitions: true);
                    var lastSummaryPath = FindVisualDescendants<TextBlock>(summaryFiles).Single(label => label.Text == d24Records[1].Sample.SourcePath);
                    var lastPathBounds = lastSummaryPath.TransformToAncestor(resultSummaryGrid).TransformBounds(new Rect(lastSummaryPath.RenderSize));
                    Check("D24 expanded details reach the final file without introducing horizontal overflow", summaryScroller.ExtentWidth <= summaryScroller.ViewportWidth + 1
                        && summaryScroller.VerticalOffset > 0 && lastPathBounds.Top >= 0 && lastPathBounds.Bottom <= resultSummaryGrid.ActualHeight
                        && lastSummaryPath.TextWrapping == TextWrapping.Wrap && !executionQueue.IsRunning,
                        $"extent={summaryScroller.ExtentWidth}x{summaryScroller.ExtentHeight};viewport={summaryScroller.ViewportWidth}x{summaryScroller.ViewportHeight};lastPath={lastPathBounds}");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-details-bottom-1280.png"));
                    summaryDetails.IsExpanded = false;
                    resultSummaryGrid.SelectedItem = null;
                    d24Records[1].Sample.SetRecord(d24FixtureB with { Steps = [d24Step with { Status = ResultStatus.Error, Metrics = [new("Mean", MetricKind.Length, double.NaN, "mm", ResultStatus.Fail)] }] }, d24Records[1].Path);
                    SettleFileList(completeTransitions: true);
                    var gapPlot = FindVisualDescendants<InspectionSummaryPlot>(resultSummaryGrid).Single(plot => plot.IsVisible);
                    Check("D24 invalid/error record renders a gap and explicit missing range without stale band", gapPlot.Summary is { RangeText: "—", ErrorCount: 1, Mean: 8.125 }
                        && gapPlot.Summary.Points.Count == 2 && gapPlot.Summary.Points[1].Value is null
                        && VisualTreeHelper.GetDrawing(gapPlot).Children.OfType<GeometryDrawing>().Count(shape => shape.Geometry is EllipseGeometry) == 1
                        && !VisualTreeHelper.GetDrawing(gapPlot).Children.OfType<GeometryDrawing>().Any(shape => shape.Geometry is RectangleGeometry)
                        && FileListControl<TextBlock>("InspectionSummaryMissingRange").Text == "—" && !executionQueue.IsRunning, "one circle + error gap;range —");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-error-gap-1280.png"));
                    d24Records[0].Sample.SetRecord(d24FixtureA with { Steps = [] }, d24Records[0].Path);
                    d24Records[1].Sample.SetRecord(d24FixtureB with { Steps = [] }, d24Records[1].Path);
                    SettleFileList(completeTransitions: true);
                    Check("D24 empty summary displays guidance without residual values or plot", resultSummaryGrid.Items.Count == 0
                        && FileListControl<TextBlock>("InspectionSummaryEmpty").IsVisible && !FindVisualDescendants<InspectionSummaryPlot>(resultSummaryGrid).Any(plot => plot.IsVisible) && !executionQueue.IsRunning, "empty recorded measurements;no stale plot");
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-empty-summary-1280.png"));
                }
                finally
                {
                    executionQueue.SearchText = string.Empty;
                    executionQueue.SetStatusFilterCommand.Execute("All");
                    foreach (var saved in d24Records) saved.Sample.SetRecord(saved.Record, saved.Path);
                    executionQueue.SelectedSample = d24Records[0].Sample;
                    SettleFileList(completeTransitions: true);
                }
                Check("D24 restoring preceding records recovers their summary without a new inspection", resultSummaryGrid.Items.Count == actualSavedSummaries.Count
                    && d24Records.All(saved => ReferenceEquals(saved.Record, saved.Sample.Record)) && !executionQueue.IsRunning, "original Record identities restored");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-fixture-record-recovery-1280.png"));
                SelectResultsTab(2);
                Check("v3 records tab shows complete input identity and uses the original record and CSV commands", FileListControl<TextBlock>("InspectionRecordInputPath").Text == executionQueue.SelectedSample!.SourcePath
                    && FileListControl<TextBlock>("InspectionRecordInputIdentity").Text == executionQueue.SelectedSample.InputSha256
                    && FileListControl<ButtonBase>("InspectionResultsExportCsv").Command == executionQueue.ExportCsvCommand
                    && FileListControl<ButtonBase>("InspectionOpenRunRecord").Command == executionQueue.OpenRunRecordCommand && !executionQueue.IsRunning,
                    $"path={executionQueue.SelectedSample.SourcePath};identity={executionQueue.SelectedSample.InputSha256};tab={resultsTabs.SelectedIndex}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d22-record-1280.png"));
                var resultLanguageBefore = OpenVisionLanguageService.CurrentLanguage;
                try
                {
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    foreach (var resultTabIndex in new[] { 0, 1, 2 })
                    {
                        SelectResultsTab(resultTabIndex);
                        Check($"v3 English compact results tab{resultTabIndex} captions stay readable without Run", resultsTabs.Items.OfType<TabItem>().All(tab => FindVisualDescendants<TextBlock>(tab).Where(label => label.IsVisible).All(label => label.ActualHeight + 1 >= label.DesiredSize.Height && label.ActualWidth + 1 >= label.DesiredSize.Width))
                            && resultsHeader.ActualHeight == 34 && !executionQueue.IsRunning, $"tab={resultTabIndex};header={resultsHeader.ActualWidth}x{resultsHeader.ActualHeight}");
                        WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d22-English-tab{resultTabIndex}-1280.png"));
                    }
                }
                finally { OpenVisionLanguageService.SetLanguage(resultLanguageBefore, save: false); }
                SelectResultsTab(0);
                executionQueue.Samples[0].SetRecord(firstCompletedRecord, executionQueue.Samples[0].RunRecordPath!);
                SettleFileList(completeTransitions: true);
                Check("v3 returning to real SDK results clears output selections and restores actual values without execution", ReferenceEquals(firstCompletedRecord, executionQueue.SelectedSample!.Record)
                    && executionQueue.SelectedOutputResult is null && executionQueue.SelectedStepResult is null && outputGrid.Items.Count == executionQueue.VisibleOutputResults.Count && !executionQueue.IsRunning,
                    $"outputs={outputGrid.Items.Count};record={executionQueue.SelectedSample.RunId}");

                SelectResultsTab(1);
                FindVisualDescendants<ScrollViewer>(resultSummaryGrid).First().ScrollToTop();
                SettleFileList(completeTransitions: true);
                var restoredSdkSummaries = executionQueue.MeasurementSummaries;
                Check("D24 real SDK summary agrees with the actual completed Records after fixture restoration", ReferenceEquals(firstCompletedRecord, executionQueue.Samples[0].Record)
                    && restoredSdkSummaries.Count > 0 && restoredSdkSummaries.All(row => row.Points.All(point =>
                    {
                        var saved = executionQueue.Samples.Single(sample => sample.SourcePath == point.SourcePath).Record!;
                        var metric = saved.Steps!.Single(step => step.Id == row.StepId).Metrics.Single(metric => metric.Name == row.MetricName && metric.Kind == row.Kind && metric.Unit == row.Unit);
                        return point.RunId == saved.RunId && point.Value == (double.IsFinite(metric.Value) ? metric.Value : null);
                    })) && !executionQueue.IsRunning, $"groups={restoredSdkSummaries.Count};points={restoredSdkSummaries.Sum(row => row.Points.Count)};run={firstCompletedRecord!.RunId}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d24-actual-sdk-summary-1280.png"));
                SelectResultsTab(0);
                var inspectionRecipeBytes = File.ReadAllBytes(inspectionRecipePath);
                File.AppendAllText(inspectionRecipePath, " ");
                executionQueue.RefreshCommands();
                ownerPathHost.UpdateLayout();
                Check("v3 changed saved basis disables central execution and retains the existing recorded identity",
                    !executionRun.IsEnabled && executionScope.IsEnabled && ReferenceEquals(firstCompletedRecord, executionQueue.Samples[0].Record)
                    && executionReadiness.Text == executionQueue.RecipeReadinessText
                    && (executionReadiness.Text.Contains("바뀌었습니다", StringComparison.Ordinal) || executionReadiness.Text.Contains("changed", StringComparison.OrdinalIgnoreCase)),
                    executionReadiness.Text);
                File.WriteAllBytes(inspectionRecipePath, inspectionRecipeBytes);
                executionQueue.RefreshCommands();
                ownerPathHost.UpdateLayout();
                Check("v3 byte-identical basis restoration enables Run without rerunning results", executionRun.IsEnabled && !executionQueue.IsRunning
                    && ReferenceEquals(firstCompletedRecord, executionQueue.Samples[0].Record), $"enabled={executionRun.IsEnabled};record={firstCompletedRecord?.RunId}");
                ownerPathHost.Width = 1280;
                ownerPathHost.Height = 760;
                SettleFileList();
                var malformedListPath = Path.Combine(inspectionBoundaryRoot, "truncated-list-input.C3D");
                File.WriteAllBytes(malformedListPath, [0x43, 0x33, 0x44]);
                executionQueue.AddSources([malformedListPath]);
                executionQueue.SelectedSample = executionQueue.Samples.Last();
                executionQueue.RunSelectedCommand.Execute(null);
                WaitInspectionIdle();
                var failedListSample = executionQueue.Samples.Last();
                fileInformation = ExpandFileInformation();
                SettleFileList();
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-error-details-before-assertion.png"));
                Check("v3 actual truncated inspection input exposes full error details while completed records remain intact", failedListSample.State == InspectionQueueItemState.Error
                    && !string.IsNullOrWhiteSpace(failedListSample.Message) && FileListControl<TextBlock>("InspectionSelectedStatusSummary").Text == failedListSample.StatusSummary
                    && ReferenceEquals(firstCompletedRecord, executionQueue.Samples[0].Record) && executionQueue.PassCount == 2 && executionRun.IsEnabled,
                    $"error={failedListSample.StatusSummary};pass={executionQueue.PassCount};retained={firstCompletedRecord?.RunId}");
                Check("v3 actual input error shows its full reason without stale measurement outputs", FileListControl<DataGrid>("InspectionStepResults").Items.Count == 0
                    && FileListControl<TextBlock>("InspectionResultsNotice").Text.Contains(failedListSample.Message, StringComparison.Ordinal)
                    && executionQueue.VisibleOutputResults.Count == 0 && !executionQueue.IsRunning,
                    $"notice={FileListControl<TextBlock>("InspectionResultsNotice").Text};outputs={executionQueue.VisibleOutputResults.Count}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-real-input-error-1280.png"));
                fileInformation.IsExpanded = false;
                SettleFileList(completeTransitions: true);
                // Fail/Warning render fixtures use isolated copies of the real record. They do not claim algorithm verdict coverage.
                var statusFixturePaths = new[] { "render-fixture-fail.C3D", "render-fixture-warning.C3D" }.Select(name =>
                {
                    var path = Path.Combine(inspectionBoundaryRoot, name);
                    File.Copy(copiedSampleSource, path);
                    return path;
                }).ToArray();
                executionQueue.AddSources(statusFixturePaths);
                executionQueue.Samples[^2].SetRecord(firstCompletedRecord! with { Status = ResultStatus.Fail, Message = "Isolated Fail rendering fixture." }, executionQueue.Samples[0].RunRecordPath!);
                executionQueue.Samples[^1].SetRecord(firstCompletedRecord! with { Status = ResultStatus.Warning, Message = "Isolated Warning rendering fixture." }, executionQueue.Samples[0].RunRecordPath!);
                SettleFileList();
                var statusLabels = FindVisualDescendants<TextBlock>(fileListView).Where(label => AutomationProperties.GetAutomationId(label) == "InspectionQueueFileStatus").ToArray();
                Check("v3 file rows keep Pass, NG, Warning and actual Error captions and semantic colors distinct", executionQueue.ProcessedCount == 5 && statusLabels.Length == 5
                    && DistributionMatches(executionQueue) && executionQueue.PassRateText == "40.0%"
                    && statusLabels.All(label => label.Text == ((InspectionQueueSampleItem)label.DataContext).StateText)
                    && statusLabels.Where(label => ((InspectionQueueSampleItem)label.DataContext).State == InspectionQueueItemState.Fail).All(label => ReferenceEquals(label.Foreground, fileListView.FindResource("ThreeD.FailBrush")))
                    && statusLabels.Where(label => ((InspectionQueueSampleItem)label.DataContext).State == InspectionQueueItemState.Warning).All(label => ReferenceEquals(label.Foreground, fileListView.FindResource("ThreeD.WarningBrush")))
                    && !executionQueue.IsRunning, $"processed={executionQueue.ProcessedCount};rows={statusLabels.Length};states={string.Join(',', statusLabels.Select(label => label.Text))}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-mixed-state-fixtures-1280.png"));
                var d23StatePaths = new[] { "d23-cancelled.C3D", "d23-untested.C3D", "d23-running.C3D" }.Select(name =>
                {
                    var path = Path.Combine(inspectionBoundaryRoot, name);
                    File.Copy(copiedSampleSource, path);
                    return path;
                }).ToArray();
                executionQueue.AddSources(d23StatePaths);
                executionQueue.Samples[^3].SetCancelled("Isolated cancelled display fixture.");
                executionQueue.Samples[^1].SetRunning();
                var longElapsedSample = executionQueue.Samples.Single(item => item.SourcePath == statusFixturePaths[0]);
                longElapsedSample.SetRecord(longElapsedSample.Record! with { ElapsedMilliseconds = 123456789000 }, longElapsedSample.RunRecordPath!);
                foreach (var d23Width in new[] { 1280d, 1920d })
                {
                    ownerPathHost.Width = d23Width;
                    ownerPathHost.Height = d23Width == 1280 ? 760 : 1040;
                    SettleFileList(completeTransitions: true);
                    var distribution = FileListControl<Grid>("InspectionQueueSummaryProgress");
                    var countLabel = FileListControl<TextBlock>("InspectionQueueCounts");
                    var rateLabel = FileListControl<TextBlock>("InspectionQueuePassRate");
                    var summaryCard = FileListControl<Border>("InspectionQueueSummaryCard");
                    var countBounds = countLabel.TransformToAncestor(summaryCard).TransformBounds(new Rect(countLabel.RenderSize));
                    var rateBounds = rateLabel.TransformToAncestor(summaryCard).TransformBounds(new Rect(rateLabel.RenderSize));
                    Check($"D23 all seven state proportions and fixed denominator render without overlap at {d23Width:0}px", DistributionMatches(executionQueue)
                        && executionQueue.AllCount == 8 && executionQueue.ProcessedCount == 5 && executionQueue.PassRateText == "40.0%"
                        && countLabel.Text == executionQueue.ProcessedCountText && RateLabelText().EndsWith("40.0%", StringComparison.Ordinal)
                        && countBounds.Right <= rateBounds.Left && rateBounds.Right <= summaryCard.ActualWidth
                        && !executionQueue.IsRunning && !inspectionShell.Workbench.IsOrderedRunRunning,
                        $"count={countLabel.Text};rate={RateLabelText()};columns={string.Join(',', distribution.ColumnDefinitions.Select(column => column.ActualWidth))};countBounds={countBounds};rateBounds={rateBounds}");
                    var timingLabel = FindVisualDescendants<TextBlock>(fileListView).Single(label => AutomationProperties.GetAutomationId(label) == "InspectionQueueFileElapsed" && ReferenceEquals(label.DataContext, longElapsedSample));
                    Check($"D23 long file timing remains fully readable at {d23Width:0}px", timingLabel.Text == "123456789.00 s"
                        && timingLabel.TextWrapping == TextWrapping.Wrap && timingLabel.ActualHeight + timingLabel.Margin.Top + timingLabel.Margin.Bottom >= timingLabel.DesiredSize.Height - 0.1
                        && FindVisualDescendants<TextBlock>(fileListView).Where(label => AutomationProperties.GetAutomationId(label) == "InspectionQueueFileElapsed")
                            .All(label => label.Text == ((InspectionQueueSampleItem)label.DataContext).ElapsedText), timingLabel.Text);
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, $".d23-states-timing-{d23Width:0}.png"));
                }
                ownerPathHost.Width = 1280;
                ownerPathHost.Height = 760;
                ActivateSampleChoice(FileListControl<RadioButton>("InspectionFilterFail"));
                SettleFileList();
                var d23Search = FileListControl<TextBox>("InspectionSearch");
                d23Search.SetCurrentValue(TextBox.TextProperty, "render-fixture-fail");
                SettleFileList();
                Check("D23 rendered search and NG filter retain whole-queue state proportions and rate", executionQueue.VisibleSamples.Count == 1
                    && executionQueue.PassRateText == "40.0%" && DistributionMatches(executionQueue)
                    && RateLabelText().EndsWith("40.0%", StringComparison.Ordinal)
                    && !executionQueue.IsRunning, executionQueue.CountsText);
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d23-filtered-states-1280.png"));
                var d23LanguageBefore = OpenVisionLanguageService.CurrentLanguage;
                try
                {
                    OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                    SettleFileList(completeTransitions: true);
                    Check("D23 English captions retain the same whole-queue values", FileListControl<TextBlock>("InspectionQueueCounts").Text == "Inspected 5 / 8"
                        && RateLabelText() == "OK rate 40.0%" && executionQueue.PassRateText == "40.0%"
                        && DistributionMatches(executionQueue), FileListControl<TextBlock>("InspectionQueueCounts").Text);
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d23-English-filtered-1280.png"));
                }
                finally { OpenVisionLanguageService.SetLanguage(d23LanguageBefore, save: false); }
                FileListControl<ButtonBase>("InspectionClearSearch").Command!.Execute(null);
                ActivateSampleChoice(FileListControl<RadioButton>("InspectionFilterAll"));
                SettleFileList();
                foreach (var d23StatePath in d23StatePaths)
                {
                    executionQueue.SelectedSample = executionQueue.Samples.Single(item => item.SourcePath == d23StatePath);
                    executionQueue.RemoveSelectedCommand.Execute(null);
                }
                foreach (var statusFixturePath in statusFixturePaths)
                {
                    executionQueue.SelectedSample = executionQueue.Samples.Single(item => item.SourcePath == statusFixturePath);
                    executionQueue.RemoveSelectedCommand.Execute(null);
                }
                File.Copy(copiedSampleSource, malformedListPath, overwrite: true);
                executionScope.SetCurrentValue(ComboBox.SelectedValueProperty, InspectionQueueRunScope.Remaining);
                ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                ActivateSampleChoice(executionRun);
                WaitInspectionIdle();
                SettleFileList();
                Check("v3 valid replacement and explicit remaining Run recover the errored row without replacing completed records", executionQueue.PassCount == 3 && executionQueue.ErrorCount == 0
                    && DistributionMatches(executionQueue) && executionQueue.PassRateText == "100.0%"
                    && failedListSample.Record?.Source.Path == malformedListPath && failedListSample.CanOpenCurrentRunRecord
                    && ReferenceEquals(firstCompletedRecord, executionQueue.Samples[0].Record) && executionQueue.RemainingCount == 0 && !executionQueue.IsRunning,
                    $"pass={executionQueue.PassCount};error={executionQueue.ErrorCount};recovered={failedListSample.RunRecordPath}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d21-real-input-recovered-1280.png"));
            }
            finally
            {
                executionQueue.StopCommand.Execute(null);
                SynchronizationContext.SetSynchronizationContext(inspectionContextBefore);
            }
            foreach (var inspectionOtherMode in new[] { ShellWorkspaceMode.Teach, ShellWorkspaceMode.Validate, ShellWorkspaceMode.Review })
            {
                inspectionShell.SelectWorkspaceCommand.Execute(inspectionOtherMode);
                ownerPathHost.UpdateLayout();
                Check($"v3 inspection central actions are absent from {inspectionOtherMode}", executionBar.Visibility == Visibility.Collapsed && !executionRun.IsVisible
                    && !executionStop.IsVisible && !executionQueue.IsRunning, $"mode={inspectionShell.SelectedWorkspaceMode};bar={executionBar.Visibility}");
            }
            var blockedInspectionRoot = Path.Combine(inspectionBoundaryRoot, "record-save-root-file");
            File.WriteAllText(blockedInspectionRoot, "owned record save failure");
            using var saveFailureShell = new ShellMainWindowViewModel(
                recentRunRecordsPath: Path.Combine(inspectionBoundaryRoot, "save-recent-runs.json"), recentRecipesPath: Path.Combine(inspectionBoundaryRoot, "save-recent-recipes.json"),
                orderedRunRecordRoot: blockedInspectionRoot);
            Check("v3 save failure fixture opens the same real recipe", saveFailureShell.Workbench.TryOpenTeachingRecipe(inspectionRecipePath, out var saveOpenMessage), saveOpenMessage);
            ownerPathWorkbench.DataContext = saveFailureShell;
            saveFailureShell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            var saveQueue = saveFailureShell.Workbench.InspectionQueue;
            saveQueue.AddSources(inspectionCopies.Take(2));
            ownerPathHost.Width = 1280;
            ownerPathHost.Height = 760;
            ownerPathHost.UpdateLayout();
            var saveQueueView = FindVisualDescendants<InspectionQueueWorkspaceView>(ownerPathWorkbench).Single(control => control.IsVisible);
            var recordSaveBanner = FindVisualDescendants<Border>(saveQueueView).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionRecordSaveFailure");
            var retryRecordSave = FindVisualDescendants<ButtonBase>(saveQueueView).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionRetryRecordSave");
            var recordContextBefore = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(ownerPathHost.Dispatcher));
            try
            {
                void WaitRecordQueueIdle()
                {
                    if (!SpinWait.SpinUntil(() =>
                        {
                            ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                            return !saveQueue.IsRunning && !saveFailureShell.Workbench.IsOrderedRunRunning;
                        }, TimeSpan.FromSeconds(30))) throw new TimeoutException("Rendered record-save queue did not return to idle within 30 seconds.");
                    ownerPathHost.UpdateLayout();
                }
                ActivateSampleChoice(executionRun);
                WaitRecordQueueIdle();
                var calculatedRecord = saveQueue.Samples[0].Record;
                var calculationBeforeRetry = saveFailureShell.Workbench.CurrentOrderedRunResult;
                Check("v3 real record-save failure retains calculation, exposes retry and blocks central Run",
                    saveQueue.HasPendingRecordSave && calculatedRecord is not null && saveQueue.Samples[0].Steps.Count == 1
                    && saveQueue.Samples[1].State == InspectionQueueItemState.Untested && recordSaveBanner.IsVisible && retryRecordSave.IsEnabled
                    && !executionRun.IsEnabled && executionScope.IsEnabled && executionReadiness.Text == saveQueue.RecipeReadinessText,
                    $"pending={saveQueue.HasPendingRecordSave};record={calculatedRecord?.RunId};ready={executionReadiness.Text}");
                Check("v3 pending record save keeps the retry visible and blocks only mutable list actions", !FileListControl<ButtonBase>("InspectionAddFiles").IsEnabled
                    && !FileListControl<ButtonBase>("InspectionRemoveSelected").IsEnabled && !FileListControl<CheckBox>("InspectionCheckAllVisible").IsEnabled
                    && FindVisualDescendants<CheckBox>(saveQueueView).Where(control => AutomationProperties.GetAutomationId(control) == "InspectionQueueInclude").All(control => !control.IsEnabled)
                    && retryRecordSave.IsVisible && retryRecordSave.TransformToAncestor(saveQueueView).TransformBounds(new Rect(retryRecordSave.RenderSize)).Bottom <= saveQueueView.ActualHeight,
                    $"retry={retryRecordSave.IsVisible};height={saveQueueView.ActualHeight};pending={saveQueue.HasPendingRecordSave}");
                SettleFileList();
                var pendingElapsedText = saveQueue.Samples[0].ElapsedText;
                Check("D23 actual record save failure retains measured time and the existing Error denominator", saveQueue.Samples[0].ElapsedMilliseconds == calculatedRecord!.ElapsedMilliseconds
                    && pendingElapsedText != "—" && saveQueue.PassRateText == "0.0%" && DistributionMatches(saveQueue)
                    && FindVisualDescendants<TextBlock>(saveQueueView).Single(label => AutomationProperties.GetAutomationId(label) == "InspectionQueueFileElapsed"
                        && ReferenceEquals(label.DataContext, saveQueue.Samples[0])).Text == pendingElapsedText, pendingElapsedText);
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d20-record-save-failed-1280.png"));
                ActivateSampleChoice(retryRecordSave);
                ownerPathHost.UpdateLayout();
                Check("v3 rendered blocked retry preserves the same pending calculation and untested remainder", saveQueue.HasPendingRecordSave
                    && ReferenceEquals(calculatedRecord, saveQueue.Samples[0].Record) && ReferenceEquals(calculationBeforeRetry, saveFailureShell.Workbench.CurrentOrderedRunResult)
                    && saveQueue.Samples[1].State == InspectionQueueItemState.Untested && !executionRun.IsEnabled, saveQueue.SummaryText);
                File.Delete(blockedInspectionRoot);
                ActivateSampleChoice(retryRecordSave);
                ownerPathHost.UpdateLayout();
                Check("v3 rendered save-only recovery restores explicit commands without executing the remainder",
                    !saveQueue.HasPendingRecordSave && !recordSaveBanner.IsVisible && saveQueue.Samples[0].Record?.RunId == calculatedRecord?.RunId
                    && ReferenceEquals(calculationBeforeRetry, saveFailureShell.Workbench.CurrentOrderedRunResult)
                    && saveQueue.Samples[0].CanOpenCurrentRunRecord && saveQueue.Samples[1].State == InspectionQueueItemState.Untested
                    && executionRun.IsEnabled && executionScope.IsEnabled && !saveQueue.IsRunning,
                    $"pending={saveQueue.HasPendingRecordSave};remaining={saveQueue.RemainingCount};record={saveQueue.Samples[0].RunRecordPath}");
                SettleFileList();
                Check("D23 save-only recovery preserves file time and updates OK share without a new Run", saveQueue.Samples[0].ElapsedText == pendingElapsedText
                    && ReferenceEquals(calculatedRecord, saveQueue.Samples[0].Record) && saveQueue.PassRateText == "100.0%" && DistributionMatches(saveQueue)
                    && saveQueue.ProcessedCount == 1 && saveQueue.UntestedCount == 1 && !saveQueue.IsRunning, saveQueue.CountsText);
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d23-save-only-timing-1280.png"));
                var savedFirstPath = saveQueue.Samples[0].RunRecordPath;
                executionScope.SetCurrentValue(ComboBox.SelectedValueProperty, InspectionQueueRunScope.Remaining);
                ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                ActivateSampleChoice(executionRun);
                WaitRecordQueueIdle();
                Check("v3 central remaining Run executes only the remainder after save recovery", saveQueue.PassCount == 2 && saveQueue.RemainingCount == 0
                    && saveQueue.Samples[0].RunRecordPath == savedFirstPath && saveQueue.Samples[0].Record?.RunId == calculatedRecord?.RunId
                    && saveQueue.Samples[1].Record?.Source.Path == inspectionCopies[1] && executionProgress.Value == 100,
                    $"pass={saveQueue.PassCount};remaining={saveQueue.RemainingCount};first={savedFirstPath}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap, Path.ChangeExtension(reportPath, ".d20-record-recovered-1280.png"));
            }
            finally
            {
                saveQueue.StopCommand.Execute(null);
                SynchronizationContext.SetSynchronizationContext(recordContextBefore);
            }
        }
        catch (Exception exception)
        {
            Check("focused inspection execution", false, exception.ToString());
        }
        finally
        {
            try
            {
                ownedHost?.Close();
                ownedWorkbench?.Dispose();
                Check("owned inspection fixture releases its Window and View", ownedHost?.IsVisible != true, "Closed only this verification container.");
            }
            catch (Exception exception)
            {
                Check("owned inspection fixture cleanup", false, exception.ToString());
            }
        }
        var result = passed == total;
        summary = $"Result: {(result ? "Pass" : "Fail")} ({passed}/{total} checks)";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return result;
    }

    public static bool VerifyValidationFloatingBounds(string reportPath, out string summary)
    {
        var lines = new List<string> { "Initial validation floating bounds; unshown managed Window objects, HWND0, no Show/Float" };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        var app = Application.Current!;
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
        app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
        using var shell = new ShellMainWindowViewModel(recentRecipesPath: Path.Combine(GetReportDirectory(reportPath), "recipes.json"), recentRunRecordsPath: Path.Combine(GetReportDirectory(reportPath), "runs.json"), orderedRunRecordRoot: Path.Combine(GetReportDirectory(reportPath), "records"), integrationSettingsPath: Path.Combine(GetReportDirectory(reportPath), "settings.json"));
        var fixtureDirectory = Path.Combine(GetReportDirectory(reportPath), "native-group-fixture");
        var workbench = shell.Workbench;
        if (!workbench.TryOpenTeachingRecipe(Path.Combine(fixtureDirectory, "grouped.ov3d-recipe.json"), out var message)) throw new InvalidOperationException(message);
        workbench.SetValidationSetSources(new[] { "coupon.C3D", "single-member-ng.C3D", "single-member-missing.C3D" }.Select(name => Path.Combine(fixtureDirectory, name)).ToArray());
        workbench.SetValidationSampleRoleCommand.Execute(new ValidationSampleRoleChange(workbench.ValidationSetSamples[0], ToolRecipeValidationSampleRole.Bad));
        var run = workbench.RunValidationSetAsync();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (!run.IsCompleted && DateTime.UtcNow < deadline) app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
        if (!run.IsCompleted) throw new TimeoutException("Float bounds fixture explicit Run did not finish.");
        run.GetAwaiter().GetResult();
        workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single(row => row.FileName == "single-member-ng.C3D");
        shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
        using var view = new ToolRecipeWorkbenchView { DataContext = shell };
        var dock = (OpenVisionDockWorkspaceView)view.FindName("DockWorkspace");
        dock.IsRecipeValidationWorkspace = true;
        dock.SetOperatorStage(OpenVisionOperatorStage.Validate);
        var evidence = (AvalonDock.Layout.LayoutAnchorable)dock.FindName("evidenceAnchorable");
        var original = (AvalonDock.Layout.LayoutAnchorablePane)evidence.Parent;
        var pane = new AvalonDock.Layout.LayoutAnchorablePane();
        var layout = evidence.Root;
        original.Children.Remove(evidence);
        pane.Children.Add(evidence);
        var model = new AvalonDock.Layout.LayoutAnchorableFloatingWindow { RootPanel = new AvalonDock.Layout.LayoutAnchorablePaneGroup(pane) };
        layout.FloatingWindows.Add(model);
        var main = new Window();
        var advanced = new OpenVisionDockWorkspaceView();
        var constructor = typeof(StudioLayoutController).GetConstructors().Single();
        StudioLayoutController? controller = constructor.GetParameters()[0].ParameterType == typeof(Window)
            ? (StudioLayoutController)constructor.Invoke(new object?[] { main, view, advanced, shell, true, null, null }) : null;
        var track = typeof(StudioLayoutController).GetMethod("TrackInitialValidationFloat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var complete = typeof(StudioLayoutController).GetMethod("CompleteInitialValidationFloat", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var child = (AvalonDock.Controls.LayoutAnchorableFloatingWindowControl)Activator.CreateInstance(typeof(AvalonDock.Controls.LayoutAnchorableFloatingWindowControl), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, new object[] { model }, null)!;
        child.Left = 1027; child.Top = 316; child.Width = 1570; child.Height = 300;
        void Track() => track?.Invoke(controller, new object[] { child });
        void Complete(Rect workArea) => complete?.Invoke(controller, new object[] { child, workArea });
        var runCount = shell.Workbench.RunLog.Count;
        var rows = shell.Workbench.ValidationSetSamples.ToArray();
        var dirty = shell.Workbench.IsDirty;
        var selected = workbench.SelectedValidationSetSample;
        var runAt = workbench.ValidationSetLastRunAt;
        var recipePath = workbench.RecipePath;
        Track();
        Complete(new Rect(0, 0, 1920, 1032));
        Check("initial Wide overflow clamps within independent workarea", child.Left == 350 && child.Top == 316 && child.Width == 1570 && child.Height == 300, $"bounds={child.Left},{child.Top},{child.Width},{child.Height};expected=350,316,1570,300");
        child.Left = 2200;
        child.Width = 1800;
        Complete(new Rect(0, 0, 1920, 1032));
        Check("later user movement and resize are not constrained", child.Left == 2200 && child.Width == 1800, $"left={child.Left};width={child.Width}");
        child.Left = -100;
        child.Top = -100;
        child.Width = 2000;
        child.Height = 1200;
        Track();
        Complete(new Rect(-1280, 0, 1280, 720));
        Check("oversized child fits negative-origin monitor", child.Left == -1280 && child.Top == 0 && child.Width == 1280 && child.Height == 720, $"bounds={child.Left},{child.Top},{child.Width},{child.Height}");
        Check("no permanent child maximum constrains later resize", double.IsPositiveInfinity(child.MaxWidth) && double.IsPositiveInfinity(child.MaxHeight), $"max={child.MaxWidth},{child.MaxHeight}");
        child.Left = 100; child.Top = 100; child.Width = 966; child.Height = 246;
        Track();
        Complete(new Rect(0, 0, 1280, 720));
        Check("Compact initial bounds already within workarea unchanged", child.Left == 100 && child.Top == 100 && child.Width == 966 && child.Height == 246, $"bounds={child.Left},{child.Top},{child.Width},{child.Height}");
        var samples = (AvalonDock.Layout.LayoutAnchorable)dock.FindName("dataLayersAnchorable");
        var samplesParent = (AvalonDock.Layout.LayoutAnchorablePane)samples.Parent;
        samplesParent.Children.Remove(samples);
        pane.Children.Add(samples);
        child.Left = 1027;
        Track();
        Complete(new Rect(0, 0, 1280, 720));
        Check("foreign Samples identity excludes mixed float", child.Left == 1027, $"left={child.Left}");
        pane.Children.Remove(samples);
        samplesParent.Children.Add(samples);
        dock.IsRecipeValidationWorkspace = false;
        child.Left = 1027;
        child.Width = 1570;
        Track();
        Complete(new Rect(0, 0, 1920, 1032));
        Check("nonvalidation workspace excluded", child.Left == 1027, $"left={child.Left}");
        dock.IsRecipeValidationWorkspace = true;
        dock.SetOperatorStage(OpenVisionOperatorStage.Teach);
        Track();
        Complete(new Rect(0, 0, 1920, 1032));
        Check("Teach stage excluded", child.Left == 1027, $"left={child.Left}");
        dock.SetOperatorStage(OpenVisionOperatorStage.Validate);
        if (evidence.Parent is AvalonDock.Layout.LayoutAnchorablePane current && !ReferenceEquals(current, pane))
        {
            current.Children.Remove(evidence);
            pane.Children.Add(evidence);
        }
        if (!layout.FloatingWindows.Contains(model)) layout.FloatingWindows.Add(model);
        Track();
        Check("Dispose test has an actual pending eligible correction", controller?.PendingInitialValidationFloatCount == 1, $"pending={controller?.PendingInitialValidationFloatCount}");
        controller?.Dispose();
        Check("Dispose releases pending subscriptions", controller?.PendingInitialValidationFloatCount == 0, $"pending={controller?.PendingInitialValidationFloatCount}");
        Complete(new Rect(0, 0, 1920, 1032));
        Check("Dispose removes pending correction", child.Left == 1027, $"left={child.Left}");
        Check("state and explicit Run contract unchanged", runCount == shell.Workbench.RunLog.Count && dirty == shell.Workbench.IsDirty && rows.SequenceEqual(shell.Workbench.ValidationSetSamples) && ReferenceEquals(selected, workbench.SelectedValidationSetSample) && runAt == workbench.ValidationSetLastRunAt && recipePath == workbench.RecipePath && rows.Length == 3, $"runs={runCount};rows={rows.Length};dirty={dirty}");
        Check("no native child handle or desktop Show", new System.Windows.Interop.WindowInteropHelper(child).Handle == IntPtr.Zero && !child.IsVisible && new System.Windows.Interop.WindowInteropHelper(main).Handle == IntPtr.Zero, $"managedWindows={app.Windows.Count};nativeHandles=0");
        pane.Children.Remove(evidence);
        original.Children.Add(evidence);
        model.Root?.CollectGarbage();
        child.Close();
        main.Close();
        Directory.CreateDirectory(GetReportDirectory(reportPath));
        File.WriteAllLines(reportPath, lines);
        summary = $"Validation initial floating bounds support: {passed}/{total} checks passed.";
        return passed == total;
    }

    public static bool VerifyValidationLayoutReset(string reportPath, out string summary)
    {
        var rootDirectory = GetReportDirectory(reportPath);
        Directory.CreateDirectory(rootDirectory);
        var lines = new List<string> { "Validation explicit layout reset, public floating model boundary; Window0, no native Float/Show" };
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ThemesDictionary { Theme = global::Wpf.Ui.Appearance.ApplicationTheme.Dark });
            app.Resources.MergedDictionaries.Add(new global::Wpf.Ui.Markup.ControlsDictionary());
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/OpenVisionLab.ThreeD.Shell;component/Themes/OpenVisionThreeDTheme.xaml") });
            Check("reset support runs STA without desktop windows", Thread.CurrentThread.GetApartmentState() == ApartmentState.STA && app.Windows.Count == 0, $"apartment={Thread.CurrentThread.GetApartmentState()};windows={app.Windows.Count}");
            foreach (var width in new[] { 956, 1600 })
            foreach (var wholePane in new[] { false, true })
            {
                var caseDirectory = Path.Combine(rootDirectory, $"reset-{width}-{wholePane}");
                Directory.CreateDirectory(caseDirectory);
                using var shell = new ShellMainWindowViewModel(recentRecipesPath: Path.Combine(caseDirectory, "recipes.json"), recentRunRecordsPath: Path.Combine(caseDirectory, "runs.json"), orderedRunRecordRoot: Path.Combine(caseDirectory, "records"), integrationSettingsPath: Path.Combine(caseDirectory, "settings.json"));
                var workbench = shell.Workbench;
                var fixtureDirectory = Path.Combine(rootDirectory, "native-group-fixture");
                if (!workbench.TryOpenTeachingRecipe(Path.Combine(fixtureDirectory, "grouped.ov3d-recipe.json"), out var message)) throw new InvalidOperationException(message);
                workbench.SetValidationSetSources(new[] { "coupon.C3D", "single-member-ng.C3D", "single-member-missing.C3D" }.Select(name => Path.Combine(fixtureDirectory, name)).ToArray());
                workbench.SetValidationSampleRoleCommand.Execute(new ValidationSampleRoleChange(workbench.ValidationSetSamples[0], ToolRecipeValidationSampleRole.Bad));
                var run = workbench.RunValidationSetAsync();
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (!run.IsCompleted && DateTime.UtcNow < deadline) app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                if (!run.IsCompleted) throw new TimeoutException("Reset fixture explicit Run did not finish.");
                run.GetAwaiter().GetResult();
                workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.Single(row => row.FileName == "single-member-ng.C3D");
                shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
                using var view = new ToolRecipeWorkbenchView { DataContext = shell, Width = width, Height = 650 };
                view.Measure(new Size(width, 650));
                view.Arrange(new Rect(0, 0, width, 650));
                view.UpdateLayout();
                app.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
                var dock = (OpenVisionDockWorkspaceView)view.FindName("DockWorkspace");
                var evidence = (AvalonDock.Layout.LayoutAnchorable)dock.FindName("evidenceAnchorable");
                var canonical = (AvalonDock.Layout.LayoutAnchorablePane)dock.FindName("evidencePane");
                var layout = (AvalonDock.Layout.LayoutRoot)evidence.Root;
                dock.ActivateEvidencePane();
                dock.IsBottomPaneExpanded = false;
                var expanded = dock.IsBottomPaneExpanded;
                var members = wholePane ? canonical.Children.ToArray() : new[] { evidence };
                var selectedSample = workbench.SelectedValidationSetSample;
                var selectedStep = workbench.SelectedValidationSetStep;
                var rows = workbench.ValidationSetSamples.ToArray();
                var runAt = workbench.ValidationSetLastRunAt;
                var logCount = workbench.RunLog.Count;
                var recipePath = workbench.RecipePath;
                var dirty = workbench.IsDirty;
                var source = workbench.MainViewerPane.SourcePath;
                var beforeCount = layout.FloatingWindows.Count;
                Check($"{width}/{wholePane} actual canonical results and validation owner prepared", dock.IsRecipeValidationWorkspace && ReferenceEquals(evidence.Parent, canonical)
                    && workbench.ValidationSetPassCount == 1 && workbench.ValidationSetFailCount == 1 && workbench.ValidationSetErrorCount == 1 && evidence.IsSelected && evidence.IsActive,
                    $"members={members.Length};pass={workbench.ValidationSetPassCount};fail={workbench.ValidationSetFailCount};error={workbench.ValidationSetErrorCount};runAt={runAt};Window0");
                var floatingPane = new AvalonDock.Layout.LayoutAnchorablePane();
                var floatingGroup = new AvalonDock.Layout.LayoutAnchorablePaneGroup();
                floatingGroup.Children.Add(floatingPane);
                var floating = new AvalonDock.Layout.LayoutAnchorableFloatingWindow { RootPanel = floatingGroup };
                foreach (var member in members) { canonical.Children.Remove(member); floatingPane.Children.Add(member); }
                layout.FloatingWindows.Add(floating);
                Check($"{width}/{wholePane} public model separation retains actual content without a Window", members.All(member => ReferenceEquals(member.Parent, floatingPane) && member.IsFloating)
                    && layout.FloatingWindows.Count == beforeCount + 1 && app.Windows.Count == 0,
                    $"members={members.Length};floating={layout.FloatingWindows.Count};windows={app.Windows.Count};no native Float/Show");
                view.Width = width + 20;
                view.Measure(new Size(width + 20, 650));
                view.Arrange(new Rect(0, 0, width + 20, 650));
                view.UpdateLayout();
                dock.ApplyPresentationState(dock.CapturePresentationState());
                Check($"{width}/{wholePane} ordinary size and presentation retain floating ownership", members.All(member => ReferenceEquals(member.Parent, floatingPane) && member.IsFloating)
                    && layout.FloatingWindows.Count == beforeCount + 1 && app.Windows.Count == 0, $"floating={layout.FloatingWindows.Count};explicit reset required");
                dock.ResetPresentationState();
                Check($"{width}/{wholePane} explicit reset restores canonical validation members and removes empty floating model", members.All(member => ReferenceEquals(member.Parent, canonical) && !member.IsFloating)
                    && layout.FloatingWindows.Count == beforeCount && evidence.IsSelected && evidence.IsActive,
                    $"parents={string.Join(',', members.Select(member => member.Parent?.GetType().Name))};floating={layout.FloatingWindows.Count};expected={beforeCount};selected={evidence.IsSelected};active={evidence.IsActive}");
                Check($"{width}/{wholePane} explicit reset preserves selected recorded results and source without Run", ReferenceEquals(selectedSample, workbench.SelectedValidationSetSample)
                    && ReferenceEquals(selectedStep, workbench.SelectedValidationSetStep) && rows.SequenceEqual(workbench.ValidationSetSamples)
                    && workbench.ValidationSetLastRunAt == runAt && workbench.RunLog.Count == logCount && workbench.RecipePath == recipePath && workbench.IsDirty == dirty
                    && workbench.MainViewerPane.SourcePath == source && dock.IsBottomPaneExpanded == expanded && !workbench.IsValidationSetRunning && app.Windows.Count == 0,
                    $"sample={workbench.SelectedValidationSetSample?.FileName};step={workbench.SelectedValidationSetStep?.StepId};runAt={workbench.ValidationSetLastRunAt};log={workbench.RunLog.Count}/{logCount};source={source};expanded={dock.IsBottomPaneExpanded}/{expanded};windows={app.Windows.Count}");
                dock.ResetPresentationState();
                Check($"{width}/{wholePane} repeated explicit reset remains docked and execution-free", members.All(member => ReferenceEquals(member.Parent, canonical) && !member.IsFloating)
                    && layout.FloatingWindows.Count == beforeCount && workbench.RunLog.Count == logCount && workbench.ValidationSetLastRunAt == runAt,
                    $"floating={layout.FloatingWindows.Count};log={workbench.RunLog.Count};runAt={workbench.ValidationSetLastRunAt}");
                foreach (var member in members) { if (member.Parent is AvalonDock.Layout.LayoutAnchorablePane parent) parent.Children.Remove(member); canonical.Children.Add(member); }
                layout.CollectGarbage();
            }
        }
        catch (Exception exception) { Check("reset support completes without exception", false, exception.ToString()); }
        summary = $"ValidationLayoutReset|pass={passed == total}|checks={passed}/{total}|report={reportPath}";
        lines.Add(summary);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    public static bool VerifyLayoutFixtureBounds(string reportPath, out string summary)
    {
        var lines = new List<string> { "Boundary|Verification-only oversized WPF layout host; reported monitor topology is unchanged. This is not native deployment or physical Wide-monitor acceptance." };
        var oldShutdown = Application.Current.ShutdownMode;
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
        var host = new Window { Style = new Style(typeof(Window)), WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false,
            Left = area.Left, Top = area.Top, Width = 1920, Height = 1040 };
        var passed = 0;
        try
        {
            host.Show();
            foreach (var size in new[] { (width: Math.Max(1920d, SystemParameters.MaximumWindowTrackWidth + 200), height: 1040d), (width: 1280d, height: 760d) })
            {
                SizeLayoutFixture(host, size.width, size.height);
                host.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                host.UpdateLayout();
                var matches = Math.Abs(host.ActualWidth - size.width) < 1 && Math.Abs(host.ActualHeight - size.height) < 1;
                if (matches) passed++;
                lines.Add($"{(matches ? "PASS" : "FAIL")} | requested={size.width}x{size.height};actual={host.ActualWidth}x{host.ActualHeight};monitor={area.Width}x{area.Height};maxTrack={SystemParameters.MaximumWindowTrackWidth}");
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
            }
        }
        catch (Exception exception) { lines.Add("FAIL | unexpected | " + exception); }
        finally { host.Close(); Application.Current.ShutdownMode = oldShutdown; }
        summary = $"Layout fixture bounds verification: {(passed == 2 && !lines.Any(line => line.StartsWith("FAIL", StringComparison.Ordinal)) ? "Pass" : "Fail")} ({passed}/2 checks)";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == 2 && !lines.Any(line => line.StartsWith("FAIL", StringComparison.Ordinal));
    }

    private static void SizeLayoutFixture(Window host, double width, double height)
    {
        host.MinWidth = width;
        host.MinHeight = height;
        host.Width = width;
        host.Height = height;
        host.UpdateLayout();
    }

    public static bool VerifyControlStates(string reportPath, out string summary)
    {
        var root = GetReportDirectory(reportPath);
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Compiled product templates in an isolated component window; actual native pointer events are observed. No inspection, SDK numerical execution or full-workflow acceptance." };
        var oldShutdown = Application.Current.ShutdownMode;
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        using var workbenchView = new ToolRecipeWorkbenchView();
        var flowView = new RecipeChainView();
        var dataView = new RecipeDataView { IsTeachingGallery = true };
        ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
        var host = new Window { Title = "Workbench control state verification", Style = new Style(typeof(Window)), Topmost = true,
            Width = 1280, Height = 420, Left = area.Left, Top = area.Top };
        host.SetResourceReference(Control.BackgroundProperty, "ThreeD.PanelBrush");
        host.SetResourceReference(Control.ForegroundProperty, "ThreeD.PrimaryTextBrush");
        var pressedObserved = false;
        var checkedObserved = false;
        var disabledObserved = false;
        var pressedPass = true;
        var checkedPass = true;
        var disabledPass = true;
        try
        {
            var sourceExpander = new Expander { Template = (ControlTemplate)flowView.Resources["FlowNodeExpanderTemplate"], Resources = flowView.Resources };
            sourceExpander.ApplyTemplate();
            var sourceNode = (ToggleButton)sourceExpander.Template.FindName("NodeHeaderToggle", sourceExpander);
            var sourceInformationExpander = (Expander)dataView.FindName("TeachingDataInformation");
            sourceInformationExpander.ApplyTemplate();
            var sourceInformation = FindVisualDescendants<ToggleButton>(sourceInformationExpander).Single(button => AutomationProperties.GetAutomationId(button) == "RecipeDataInformationToggle");
            var node = new ToggleButton { Width = 22, Height = 22, Template = sourceNode.Template, FocusVisualStyle = sourceNode.FocusVisualStyle, Resources = flowView.Resources };
            OpenVisionLab.ThreeD.Shell.Behaviors.PointerHoverBehavior.SetIsEnabled(node, true);
            var information = new ToggleButton { Width = 220, Height = 34, Template = sourceInformation.Template, FocusVisualStyle = sourceInformation.FocusVisualStyle, Resources = dataView.Resources };
            var pin = new ToggleButton { Style = (Style)workbenchView.Resources["TeachingDataHeaderButton"], Content = "Pin" };
            var panel = new StackPanel { Margin = new Thickness(24) };
            panel.SetResourceReference(TextElement.ForegroundProperty, "ThreeD.SecondaryTextBrush");
            panel.Children.Add(new TextBlock { Text = "Click each control, use Tab for focus, then disable and enable the controls. F6 changes width. Close this window to finish.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18) });
            foreach (var item in new[] { ("Node details", node), ("Input information", information), ("Data pin", pin) })
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
                row.Children.Add(new TextBlock { Text = item.Item1, Width = 180, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(item.Item2);
                panel.Children.Add(row);
            }
            var enabled = new CheckBox { Content = "Controls enabled", IsChecked = true, Margin = new Thickness(0, 0, 0, 16) };
            panel.Children.Add(enabled);
            host.Content = panel;
            host.Show();
            host.UpdateLayout();
            static string BrushText(Brush? brush) => brush is SolidColorBrush solid ? solid.Color.ToString() : brush?.ToString() ?? "null";
            void Capture(string state)
            {
                host.UpdateLayout();
                var suffix = $"{host.Width:0}-{state}";
                lines.Add($"STATE | {suffix} | nodePressed={node.IsPressed};pinChecked={pin.IsChecked};enabled={information.IsEnabled};nodeFocus={node.IsKeyboardFocused};infoFocus={information.IsKeyboardFocused};pinFocus={pin.IsKeyboardFocused}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(host).Bitmap, Path.Combine(root, suffix + ".png"));
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
            }
            var nodeSurface = (Border)node.Template.FindName("NodeHeaderSurface", node);
            var pressedDescriptor = DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border));
            EventHandler pressedChanged = (_, _) =>
            {
                if (!node.IsPressed) return;
                pressedObserved = true;
                var actual = nodeSurface.Background;
                var expected = (Brush)host.FindResource("ThreeD.PressedSurfaceBrush");
                pressedPass &= BrushText(actual) == BrushText(expected);
                lines.Add($"PRESSED | actual={BrushText(actual)};expected={BrushText(expected)}");
                Capture("node-pressed");
            };
            pressedDescriptor.AddValueChanged(nodeSurface, pressedChanged);
            pin.Checked += (_, _) => host.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
            {
                checkedObserved = true;
                var expected = (Brush)host.FindResource("ThreeD.AccentBrush");
                checkedPass &= BrushText(pin.Foreground) == BrushText(expected);
                lines.Add($"CHECKED | actual={BrushText(pin.Foreground)};expected={BrushText(expected)}");
                Capture("pin-checked");
            }));
            enabled.Click += (_, _) =>
            {
                foreach (var control in new[] { node, information, pin }) control.IsEnabled = enabled.IsChecked == true;
                host.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
                {
                    if (!information.IsEnabled)
                    {
                        disabledObserved = true;
                        var expected = (Brush)host.FindResource("ThreeD.DisabledBrush");
                        disabledPass &= BrushText(information.Foreground) == BrushText(expected);
                        lines.Add($"DISABLED | actual={BrushText(information.Foreground)};expected={BrushText(expected)}");
                    }
                    Capture(information.IsEnabled ? "enabled-recovery" : "disabled");
                }));
            };
            host.PreviewKeyDown += (_, args) =>
            {
                if (args.Key != Key.F6) return;
                host.Width = host.Width < 1500 ? 1920 : 1280;
                Capture("layout");
                args.Handled = true;
            };
            Capture("normal");
            var closed = new System.Windows.Threading.DispatcherFrame();
            host.Closed += (_, _) => closed.Continue = false;
            System.Windows.Threading.Dispatcher.PushFrame(closed);
            pressedDescriptor.RemoveValueChanged(nodeSurface, pressedChanged);
            lines.Add($"{(pressedObserved && pressedPass ? "PASS" : "FAIL")} | actual pointer-down node surface | observed={pressedObserved};match={pressedPass}");
            lines.Add($"{(checkedObserved && checkedPass ? "PASS" : "FAIL")} | selected pin foreground | observed={checkedObserved};match={checkedPass}");
            lines.Add($"{(disabledObserved && disabledPass ? "PASS" : "FAIL")} | disabled information foreground | observed={disabledObserved};match={disabledPass}");
        }
        catch (Exception exception) { lines.Add("FAIL | unexpected | " + exception); }
        finally { host.Close(); Application.Current.ShutdownMode = oldShutdown; }
        var passed = pressedObserved && pressedPass && checkedObserved && checkedPass && disabledObserved && disabledPass && !lines.Any(line => line.StartsWith("FAIL", StringComparison.Ordinal));
        summary = $"Workbench control states: {(passed ? "Pass" : "Fail")} (3 checks)";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed;
    }

    public static bool VerifyLanguageSwitch(string reportPath, out string summary) => VerifyCore(reportPath, out summary, languageOnly: true);

    public static bool Verify(string reportPath, out string summary) => VerifyCore(reportPath, out summary, languageOnly: false);

    private static bool VerifyCore(string reportPath, out string summary, bool languageOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D docking workspace verification",
            $"Generated: {DateTimeOffset.Now:O}",
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            Check(
                "verification runs on STA",
                Thread.CurrentThread.GetApartmentState() == ApartmentState.STA,
                Thread.CurrentThread.GetApartmentState().ToString());

            using var firstCallbackViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var replacementCallbackViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var disposedCallbackWorkbench = new ToolRecipeWorkbenchView
            {
                ViewerContent = firstCallbackViewer,
            };
            var profileViewerDataContextBeforeDispose =
                disposedCallbackWorkbench.ProfileViewerDataContext;
            var profileViewerHostBeforeDispose =
                disposedCallbackWorkbench.ProfileViewerHost;
            disposedCallbackWorkbench.Dispose();
            disposedCallbackWorkbench.ViewerContent = replacementCallbackViewer;
            Check(
                "disposed Workbench releases profile adapter and ignores late ViewerContent callback",
                profileViewerDataContextBeforeDispose is HeightProfileViewModel
                && ReferenceEquals(profileViewerHostBeforeDispose, firstCallbackViewer)
                && disposedCallbackWorkbench.ProfileViewerHost is null
                && disposedCallbackWorkbench.ProfileViewerDataContext is null,
                $"beforeAdapter={profileViewerDataContextBeforeDispose is HeightProfileViewModel};beforeHost={profileViewerHostBeforeDispose is not null};afterHost={disposedCallbackWorkbench.ProfileViewerHost is not null};after={disposedCallbackWorkbench.ProfileViewerDataContext is not null}");

            var dataContextOwner = new object();
            var viewerOwner = new object();
            var workbench = new ToolRecipeWorkbenchView
            {
                DataContext = dataContextOwner,
                ViewerContent = viewerOwner,
            };
            var workbenchContracts = workbench.GetDockingPaneContracts();
            var shortcutGestures = workbench.InputBindings
                .OfType<KeyBinding>()
                .Select(binding => (binding.Key, binding.Modifiers))
                .ToHashSet();
            Check(
                "Workbench exposes the bounded inspection shortcut set",
                shortcutGestures.SetEquals(
                [
                    (Key.N, ModifierKeys.Control),
                    (Key.O, ModifierKeys.Control),
                    (Key.S, ModifierKeys.Control),
                    (Key.S, ModifierKeys.Control | ModifierKeys.Shift),
                    (Key.Escape, ModifierKeys.None),
                    (Key.Enter, ModifierKeys.None),
                    (Key.O, ModifierKeys.Control | ModifierKeys.Shift),
                    (Key.F5, ModifierKeys.None),
                    (Key.Enter, ModifierKeys.Control),
                    (Key.F5, ModifierKeys.Control),
                    (Key.F5, ModifierKeys.Control | ModifierKeys.Shift),
                ]),
                string.Join(", ", shortcutGestures.OrderBy(item => item.Key).ThenBy(item => item.Modifiers)));
            Check("Workbench exposes twelve dock panes", workbenchContracts.Count == 12, Describe(workbenchContracts));
            Check(
                "Workbench separates Tool Library and Recipe Flow without hosting Recipe Center",
                HasExactIds(workbenchContracts, WorkbenchContentIds)
                && workbenchContracts[0].ContentId == "tool-library"
                && workbenchContracts[1].ContentId == "data-layers"
                && workbenchContracts[0].HasContent
                && workbenchContracts[1].HasContent,
                Describe(workbenchContracts));
            Check(
                "Workbench model preserves Recipe Flow, Selected Tool, then Viewer order",
                workbench.HasRecipeFlowInspectorViewerOrder
                && workbenchContracts[1].ContentId == "data-layers"
                && workbenchContracts[2].ContentId == "tool-inspector"
                && workbenchContracts[3].ContentId == "three-d-viewer",
                Describe(workbenchContracts));
            Check(
                "Workbench default uses Recipe Chain and the single Selected Tool workspace",
                workbench.UsesInspectionWorkspaceV3Composition,
                $"v3Composition={workbench.UsesInspectionWorkspaceV3Composition}");
            Check(
                "Recipe Flow exposes the themed health summary and requirement navigation",
                workbench.HasRecipeHealthNavigation,
                $"recipeHealthNavigation={workbench.HasRecipeHealthNavigation}");
            Check(
                "Empty Viewer keeps one primary input action without a duplicate context ribbon",
                workbench.IsViewerInputFirstActionVisible
                && !workbench.IsViewerContextRibbonVisible
                && !workbench.IsNoRecipeStepBannerVisible,
                $"inputAction={workbench.IsViewerInputFirstActionVisible}; contextRibbon={workbench.IsViewerContextRibbonVisible}; noStepBanner={workbench.IsNoRecipeStepBannerVisible}");
            Check(
                "Selected Tool workspace owns bounded Thickness repeat-grid authoring controls",
                workbench.HasThicknessRepeatGridAuthoringControls,
                $"repeatGrid={workbench.HasThicknessRepeatGridAuthoringControls}");
            Check(
                "Selected Tool owns the single explicit Run and Cancel actions",
                workbench.HasExplicitSelectedToolActions,
                $"selectedToolActions={workbench.HasExplicitSelectedToolActions}");
            Check(
                "Source Quality and Selected Tool surfaces are mutually exclusive",
                workbench.HasExclusiveSelectedToolWorkspaceSurface,
                $"exclusiveWorkspace={workbench.HasExclusiveSelectedToolWorkspaceSurface}");
            Check(
                "Displayed Outputs is adjacent to Viewer",
                workbench.HasAdjacentViewerOutputs,
                $"viewerOutputs={workbench.HasAdjacentViewerOutputs}");
            Check(
                "Workbench Viewer exposes one normal layout toolbar and two reusable slot hosts",
                workbench.HasViewerWorkspaceLayoutToolbar,
                $"viewerWorkspace={workbench.HasViewerWorkspaceLayoutToolbar}");
            Check(
                "Workbench gives the default Viewer a dominant width",
                workbench.HasDominantViewerWidth,
                $"dominantViewer={workbench.HasDominantViewerWidth}");
            Check(
                "v3 Workbench removes the duplicate workspace context row",
                workbench.FindName("WorkspaceCommandBar") is null,
                $"legacyCommandBar={workbench.FindName("WorkspaceCommandBar") is not null}");
            Check(
                "Workbench dock tabs use the top OpenVision themed strip",
                workbench.HasTopThemedDockTabs,
                $"topThemedTabs={workbench.HasTopThemedDockTabs}");
            Check(
                "Task panes expose side collapse while Viewer remains fixed",
                workbench.HasSideCollapsibleTaskPanes,
                $"sideCollapsible={workbench.HasSideCollapsibleTaskPanes}");
            var autoHideRoundTrip = workbench.VerifySupportAutoHideRoundTrip();
            Check(
                "Support pane side collapse restores without changing composition",
                autoHideRoundTrip.Collapsed
                && autoHideRoundTrip.Restored
                && workbench.HasRecipeFlowInspectorViewerOrder,
                $"collapsed={autoHideRoundTrip.Collapsed}; restored={autoHideRoundTrip.Restored}; composition={workbench.HasRecipeFlowInspectorViewerOrder}");

            var navigationViewModel = new ShellMainWindowViewModel();
            var navigationRail = new StudioNavigationRailView
            {
                DataContext = navigationViewModel,
            };
            navigationRail.ApplyResponsiveWidthForVerification(1920);
            var wideRailWidth = navigationRail.Width;
            navigationRail.ApplyResponsiveWidthForVerification(1280);
            var headerForLanguage = new StudioHeaderView { DataContext = navigationViewModel };
            ArrangeForTextVerification(headerForLanguage, 1280);
            var languageToolsMenu = (MenuItem)FindVisualDescendants<Menu>(headerForLanguage).Single().Items[6];
            var languageHost = (StackPanel)((MenuItem)languageToolsMenu.Items[0]).Header;
            var languageSelector = languageHost.Children.OfType<ComboBox>().Single();
            ArrangeForTextVerification(languageSelector, 112);
            Check(
                "Workbench v4 rail exposes accessible responsibility and utility routes",
                navigationRail.HasAccessibleResponsibilityRoutes
                && navigationRail.HasAccessibleUtilityRoutes,
                $"responsibilities={navigationRail.HasAccessibleResponsibilityRoutes}; utilities={navigationRail.HasAccessibleUtilityRoutes}");
            var expectedToolLabIds = new[]
            {
                "filter",
                "height-difference-edge",
                "two-point-line",
                "three-point-plane",
                "datum-plane-raw-height-deviation",
                "line-intersection",
                "landmark-correspondence",
                "xyz-affine-solve",
                "xyz-affine-apply",
                "re-grid-height-map",
            };
            var requestedToolLabIds = new List<string>();
            EventHandler<StudioToolLabRequestEventArgs> toolLabRequestHandler = (_, args) =>
                requestedToolLabIds.Add(args.ToolId);
            navigationRail.ToolLabRequested += toolLabRequestHandler;
            var toolLabMenuItems = navigationRail.ToolLabsMenuButton.ContextMenu?.Items
                .OfType<MenuItem>()
                .ToArray()
                ?? [];
            foreach (var toolLabMenuItem in toolLabMenuItems)
            {
                toolLabMenuItem.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, toolLabMenuItem));
            }
            navigationRail.ToolLabRequested -= toolLabRequestHandler;
            Check(
                "Studio navigation rail emits one ordered typed Tool Lab request per menu item",
                expectedToolLabIds.SequenceEqual(toolLabMenuItems.Select(item => item.Tag as string))
                && expectedToolLabIds.SequenceEqual(requestedToolLabIds),
                $"menuIds=[{string.Join(",", toolLabMenuItems.Select(item => item.Tag))}]; requests=[{string.Join(",", requestedToolLabIds)}]");
            Check(
                "Studio language selector retains the shared themed ComboBox base style",
                languageSelector.Style.BasedOn is not null
                && languageSelector.MinHeight >= 30
                && languageSelector.VerticalContentAlignment == VerticalAlignment.Center,
                $"hasThemedBase={languageSelector.Style.BasedOn is not null}; minHeight={languageSelector.MinHeight:F0}; vertical={languageSelector.VerticalContentAlignment}");
            Check(
                "v3 preserves the full-label language selector in the Tools menu",
                Math.Abs(languageSelector.Width - 112) < 0.1
                && languageHost.Children.Contains(languageSelector)
                && AutomationProperties.GetName(languageSelector).Length > 0,
                $"width={languageSelector.Width:F0}; menuHost={languageHost.Children.Contains(languageSelector)}; name={AutomationProperties.GetName(languageSelector)}");
            languageSelector.IsEnabled = false;
            Check(
                "Studio language selector resolves semantic disabled colors",
                Equals(
                    languageSelector.Background,
                    headerForLanguage.FindResource("ThreeD.DisabledSurfaceBrush"))
                && Equals(
                    languageSelector.Foreground,
                    headerForLanguage.FindResource("ThreeD.DisabledBrush")),
                $"background={languageSelector.Background}; foreground={languageSelector.Foreground}");
            languageSelector.IsEnabled = true;
            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            var englishLanguageOption = navigationViewModel.LanguageOptions.Single(
                option => option.Language == OpenVisionLanguage.English);
            if (originalLanguage == OpenVisionLanguage.English)
            {
                navigationViewModel.SetSelectedLanguageForVerification(
                    navigationViewModel.LanguageOptions.Single(
                        option => option.Language == OpenVisionLanguage.Korean));
            }
            var originalLanguageOption = navigationViewModel.LanguageOptions.Single(
                option => option.Language == originalLanguage);
            var paletteBeforeLanguageChange = navigationViewModel.Workbench.HeightImageViewer.SelectedPalette;
            var auxiliaryContentBeforeLanguageChange = navigationViewModel.Workbench.ViewerWorkspace.AuxiliaryContentId;
            var localizationNotificationCount = 0;
            string? localizationPropertyName = null;
            var workbenchNotifications = new List<string?>();
            PropertyChangedEventHandler localizationHandler = (_, args) =>
            {
                localizationNotificationCount++;
                localizationPropertyName = args.PropertyName;
            };
            PropertyChangedEventHandler workbenchHandler = (_, args) => workbenchNotifications.Add(args.PropertyName);
            ThreeDLocalization.Shared.PropertyChanged += localizationHandler;
            navigationViewModel.Workbench.PropertyChanged += workbenchHandler;
            languageSelector.SelectedItem = englishLanguageOption;
            languageSelector.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            navigationViewModel.Workbench.PropertyChanged -= workbenchHandler;
            ThreeDLocalization.Shared.PropertyChanged -= localizationHandler;
            Check(
                "Language switch emits one all-properties localization notification",
                localizationNotificationCount == 1
                && string.IsNullOrEmpty(localizationPropertyName),
                $"notifications={localizationNotificationCount}; property='{localizationPropertyName}'");
            Check(
                "Language switch remains bounded and reports completion time",
                navigationViewModel.LastLanguageChangeMilliseconds < 500
                && navigationViewModel.StatusText.Contains("ms", StringComparison.Ordinal),
                $"elapsed={navigationViewModel.LastLanguageChangeMilliseconds:F2} ms; status='{navigationViewModel.StatusText}'");
            Check(
                "Language switch preserves ComboBox selections and visible labels",
                navigationViewModel.Workbench.HeightImageViewer.SelectedPalette == paletteBeforeLanguageChange
                && navigationViewModel.Workbench.HeightImageViewer.PaletteChoices.All(
                    choice => !string.IsNullOrWhiteSpace(choice.DisplayName))
                && string.Equals(
                    navigationViewModel.Workbench.ViewerWorkspace.AuxiliaryContentId,
                    auxiliaryContentBeforeLanguageChange,
                    StringComparison.Ordinal)
                && navigationViewModel.Workbench.ViewerWorkspaceCandidates.All(
                    candidate => !string.IsNullOrWhiteSpace(candidate.DisplayName)),
                $"palette={navigationViewModel.Workbench.HeightImageViewer.SelectedPalette}; paletteLabels={navigationViewModel.Workbench.HeightImageViewer.PaletteChoices.Count}; auxiliary='{navigationViewModel.Workbench.ViewerWorkspace.AuxiliaryContentId}'; candidates={navigationViewModel.Workbench.ViewerWorkspaceCandidates.Count}");
            Check(
                "Language switch restores Viewer selections after localized candidate lists",
                workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.MainViewerCandidates)) is var mainCandidatesIndex
                && mainCandidatesIndex >= 0
                && workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.MainViewerContentId)) > mainCandidatesIndex
                && workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.ViewerWorkspaceCandidates)) is var viewerCandidatesIndex
                && viewerCandidatesIndex >= 0
                && workbenchNotifications.IndexOf(nameof(navigationViewModel.Workbench.AuxiliaryViewerContentId)) > viewerCandidatesIndex
                && new ViewerWorkspaceCandidateItem(
                    ViewerWorkspaceViewModel.HeightImageViewerContentId,
                    "높이 이미지",
                    ViewerWorkspaceCandidateKind.HeightImage,
                    "source.c3d",
                    "HeightField / native grid",
                    "Ready",
                    true)
                == new ViewerWorkspaceCandidateItem(
                    ViewerWorkspaceViewModel.HeightImageViewerContentId,
                    "Height Image",
                    ViewerWorkspaceCandidateKind.HeightImage,
                    "source.c3d",
                    "HeightField / native grid",
                    "Ready",
                    true),
                $"notifications={string.Join(",", workbenchNotifications)}");
            ArrangeForTextVerification(languageSelector, 112);
            var selectedLanguageText = FindVisualDescendants<TextBlock>(languageSelector)
                .FirstOrDefault(text => ReferenceEquals(text.DataContext, englishLanguageOption));
            var popupLanguageText = languageSelector.ItemTemplate.LoadContent() as TextBlock;
            if (popupLanguageText is not null)
            {
                popupLanguageText.DataContext = englishLanguageOption;
                popupLanguageText.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                popupLanguageText.Arrange(new Rect(popupLanguageText.DesiredSize));
                popupLanguageText.UpdateLayout();
            }
            Check(
                "Title-bar language selector keeps full language names readable",
                selectedLanguageText is { Text: "English", Clip: null, ActualHeight: >= 12 }
                && popupLanguageText is { Text: "English", Clip: null, ActualHeight: >= 12 }
                && selectedLanguageText.ActualWidth + 1 >= popupLanguageText.ActualWidth,
                $"selected='{selectedLanguageText?.Text}' {selectedLanguageText?.ActualWidth:0.#}x{selectedLanguageText?.ActualHeight:0.#}; popup='{popupLanguageText?.Text}' {popupLanguageText?.ActualWidth:0.#}x{popupLanguageText?.ActualHeight:0.#}");
            navigationRail.ApplyResponsiveWidthForVerification(1920);
            ArrangeForTextVerification(navigationRail, 52);
            var wideTeachingLabel = FindVisualDescendants<TextBlock>(navigationRail.AuthoringModeButton)
                .Single(text => ReferenceEquals(text.Style, navigationRail.FindResource("RailLabelStyle")));
            var wideTeachingLabelBounds = wideTeachingLabel.TransformToAncestor(navigationRail).TransformBounds(new Rect(wideTeachingLabel.RenderSize));
            navigationRail.ApplyResponsiveWidthForVerification(1280);
            ArrangeForTextVerification(navigationRail, 52);
            var compactTeachingLabel = FindVisualDescendants<TextBlock>(navigationRail.AuthoringModeButton)
                .Single(text => ReferenceEquals(text.Style, navigationRail.FindResource("RailLabelStyle")));
            var compactTeachingLabelBounds = compactTeachingLabel.TransformToAncestor(navigationRail).TransformBounds(new Rect(compactTeachingLabel.RenderSize));
            Check(
                "R19 navigation labels remain visible in the Wide and Compact rail",
                wideTeachingLabel.Visibility == Visibility.Visible
                && wideTeachingLabel.ActualWidth > 0
                && wideTeachingLabel.ActualHeight > 0
                && wideTeachingLabel.TextWrapping == TextWrapping.Wrap
                && wideTeachingLabelBounds.Left >= 0
                && wideTeachingLabelBounds.Right <= navigationRail.Width + 1
                && compactTeachingLabel.Visibility == Visibility.Visible
                && compactTeachingLabel.ActualWidth > 0
                && compactTeachingLabel.ActualHeight > 0
                && compactTeachingLabel.TextWrapping == TextWrapping.Wrap
                && compactTeachingLabelBounds.Left >= 0
                && compactTeachingLabelBounds.Right <= navigationRail.Width + 1,
                $"wideLabel={wideTeachingLabel.Visibility}/{wideTeachingLabel.ActualWidth:0.#}x{wideTeachingLabel.ActualHeight:0.#}/{wideTeachingLabelBounds}; compactLabel={compactTeachingLabel.Visibility}/{compactTeachingLabel.ActualWidth:0.#}x{compactTeachingLabel.ActualHeight:0.#}/{compactTeachingLabelBounds}");
            languageSelector.SelectedItem = originalLanguageOption;
            languageSelector.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            if (languageOnly)
            {
                navigationViewModel.Dispose();
                workbench.Dispose();
                summary = $"Workbench language verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
                File.WriteAllLines(reportPath, lines.Append(summary));
                return passed == total;
            }
            Check(
                "v3 rail reserves the same narrow width in Wide and Compact layouts",
                Math.Abs(wideRailWidth - 52) < 0.1
                && navigationRail.IsCompact
                && Math.Abs(navigationRail.Width - 48) < 0.1,
                $"wide={wideRailWidth:F0}; compact={navigationRail.Width:F0}; isCompact={navigationRail.IsCompact}");
            var studioHeader = new StudioHeaderView { DataContext = navigationViewModel };
            ArrangeForTextVerification(studioHeader, 1280);
            var mainMenu = FindVisualDescendants<Menu>(studioHeader).Single();
            var studioTitleBar = FindVisualDescendants<StudioTitleBarView>(studioHeader).Single();
            studioHeader.Measure(new Size(1280, double.PositiveInfinity));
            studioHeader.Arrange(new Rect(0, 0, 1280, studioHeader.DesiredSize.Height));
            studioHeader.UpdateLayout();
            Check(
                  "v3 shell menus, recipe and actions share the HTML Compact 36px title bar",
                  Math.Abs(studioTitleBar.ActualHeight - 36) < 0.1
                  && mainMenu.ActualHeight is >= 24 and <= 40
                  && Math.Abs(studioHeader.DesiredSize.Height - 36) < 0.1
                  && ReferenceEquals(studioTitleBar.MenuContent, mainMenu),
                $"title={studioTitleBar.ActualHeight:F0}; menu={mainMenu.ActualHeight:F0}; headerDesired={studioHeader.DesiredSize.Height:F0}");
            var menuTextBrush = (SolidColorBrush)studioHeader.FindResource("ThreeD.TextBrush");
            var topLevelMenuItems = mainMenu.Items.OfType<MenuItem>().ToArray();
            var topLevelMenuLabels = topLevelMenuItems
                .SelectMany(item => FindVisualDescendants<AccessText>(item))
                .ToArray();
            var topActionButtons = new[]
            {
                FindVisualDescendants<Wpf.Ui.Controls.Button>(studioHeader).Single(button => AutomationProperties.GetAutomationId(button) == "LoadTeachingInputButton"),
                FindVisualDescendants<Wpf.Ui.Controls.Button>(studioHeader).Single(button => AutomationProperties.GetAutomationId(button) == "SaveTeachingRecipeHeaderButton"),
            };
            Check(
                "R19 title actions share rounded icon buttons with accessible names",
                topActionButtons.All(button => button.Icon is not null
                    && button.CornerRadius.TopLeft == 4
                    && button.MinHeight == 26
                    && !string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))),
                $"actions={string.Join(';', topActionButtons.Select(button => $"{AutomationProperties.GetName(button)}:icon={button.Icon is not null}:radius={button.CornerRadius.TopLeft}:height={button.MinHeight}"))}");
            Check(
                "R19 top-level menu labels fit the compact command row and use semantic text color",
                topLevelMenuItems.Length == 8
                && topLevelMenuItems.All(item => item.ActualHeight is >= 24 and <= 34
                    && ReferenceEquals(
                        item.Template,
                        studioHeader.FindResource("R19TopMenuItemTemplate"))
                    && item.Foreground is SolidColorBrush brush
                    && brush.Color == menuTextBrush.Color)
                && topLevelMenuLabels.Length == 8
                && topLevelMenuLabels.All(label => label.ActualHeight >= 12
                    && label.ActualWidth > 0
                    && label.Clip is null),
                $"items={topLevelMenuItems.Length}; heights={string.Join(',', topLevelMenuItems.Select(item => item.ActualHeight.ToString("F0", CultureInfo.InvariantCulture)))}; foregrounds={string.Join(',', topLevelMenuItems.Select(item => item.Foreground))}; labels={topLevelMenuLabels.Length}; labelDetails={string.Join(',', topLevelMenuLabels.Select(label => $"{label.Text}:font={label.FontSize:F0}/desired={label.DesiredSize.Height:F0}/actual={label.ActualHeight:F0}/parent={VisualTreeHelper.GetParent(label)?.GetType().Name}:{(VisualTreeHelper.GetParent(label) as FrameworkElement)?.ActualHeight:F0}"))}; labelClips={string.Join(',', topLevelMenuLabels.Select(label => label.Clip is null ? "none" : "clip"))}");
            var recipeManagerView = new OpenVisionLab.ThreeD.Shell.Views.Recipe.RecipeManagerView
            {
                DataContext = navigationViewModel.Workbench,
            };
            ArrangeForTextVerification(recipeManagerView, 1520);
            var recipePreviewView = FindVisualDescendants<OpenVisionLab.ThreeD.Shell.Views.Workbench.HeightImageViewerView>(recipeManagerView).Single();
            Check(
                "R19 Recipe Center input preview binds the isolated read-only Viewer owner",
                ReferenceEquals(recipePreviewView.DataContext, navigationViewModel.Workbench.RecipePreviewViewer)
                && !ReferenceEquals(recipePreviewView.DataContext, navigationViewModel.Workbench.HeightImageViewer),
                $"preview={recipePreviewView.DataContext?.GetType().Name}; isolated={ReferenceEquals(recipePreviewView.DataContext, navigationViewModel.Workbench.RecipePreviewViewer)}");
            var recipeSections = (ListBox)recipeManagerView.FindName("RecipeBrowserSections");
            var recipeBrowserContent = (ContentControl)recipeManagerView.FindName("RecipeBrowserContent");
            var recipeSectionTemplates = new[]
            {
                (Section: "overview", Template: "RecipeOverviewSectionTemplate"),
                (Section: "input", Template: "RecipeInputSectionTemplate"),
                (Section: "tools", Template: "RecipeToolsSectionTemplate"),
                (Section: "regions", Template: "RecipeRegionsSectionTemplate"),
                (Section: "criteria", Template: "RecipeCriteriaSectionTemplate"),
            };
            foreach (var section in recipeSectionTemplates)
            {
                recipeSections.SelectedValue = section.Section;
                recipeSections.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
                recipeManagerView.UpdateLayout();
                Check(
                    $"R19 Recipe Center section {section.Section} selects its bound content",
                    recipeSections.SelectedValue as string == section.Section
                    && navigationViewModel.Workbench.SelectedRecipeBrowserSection == section.Section
                    && ReferenceEquals(recipeBrowserContent.ContentTemplate, recipeManagerView.FindResource(section.Template)),
                    $"selected={recipeSections.SelectedValue}; state={navigationViewModel.Workbench.SelectedRecipeBrowserSection}; template={recipeBrowserContent.ContentTemplate}");
            }
            recipeSections.SelectedValue = "overview";
            recipeSections.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
            recipeManagerView.UpdateLayout();
            var recipeBrowserColumns = (Grid)recipeManagerView.FindName("RecipeBrowserColumns");
            var wideRecipeColumns = recipeBrowserColumns.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
            ArrangeForTextVerification(recipeManagerView, 1244);
            var compactRecipeColumns = recipeBrowserColumns.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
            Check(
                "R19 Recipe Center columns match the responsive 320/182 and 285/155 authored widths",
                wideRecipeColumns.Length == 3
                && Math.Abs(wideRecipeColumns[0] - 320) < 3
                && Math.Abs(wideRecipeColumns[1] - 182) < 3
                && Math.Abs(compactRecipeColumns[0] - 285) < 3
                && Math.Abs(compactRecipeColumns[1] - 155) < 3,
                $"wide={string.Join(',', wideRecipeColumns.Select(width => width.ToString("F0", CultureInfo.InvariantCulture)))}; compact={string.Join(',', compactRecipeColumns.Select(width => width.ToString("F0", CultureInfo.InvariantCulture)))}");
            recipeManagerView.ApplyResponsiveWidthForVerification(1024);
            ArrangeForTextVerification(recipeManagerView, 1024);
            var compactColumns = recipeBrowserColumns.ColumnDefinitions.Select(column => column.ActualWidth).ToArray();
            var compactSectionsPanel = (Border)recipeManagerView.FindName("RecipeSectionsPanel");
            var compactDetailsPanel = (Border)recipeManagerView.FindName("RecipeDetailsPanel");
            Check(
                "R19 Recipe Center switches to the compact two-column layout with horizontal recipe sections",
                recipeManagerView.IsCompact
                && compactColumns.Length == 3
                && Math.Abs(compactColumns[0] - 280) < 2
                && compactColumns[2] < 1
                && Grid.GetRow(compactSectionsPanel) == 0
                && Grid.GetColumnSpan(compactSectionsPanel) == 2
                && Grid.GetRow(compactDetailsPanel) == 1
                && Grid.GetColumnSpan(compactDetailsPanel) == 2
                && ReferenceEquals(recipeSections.ItemsPanel, recipeManagerView.FindResource("RecipeBrowserCompactSectionsPanel")),
                $"compact={string.Join(',', compactColumns.Select(width => width.ToString("F0", CultureInfo.InvariantCulture)))}; nav={Grid.GetRow(compactSectionsPanel)}/{Grid.GetColumnSpan(compactSectionsPanel)}; details={Grid.GetRow(compactDetailsPanel)}/{Grid.GetColumnSpan(compactDetailsPanel)}; compact={recipeManagerView.IsCompact}");
            recipeManagerView.ApplyResponsiveWidthForVerification(1520);
            ArrangeForTextVerification(recipeManagerView, 1520);
            var recipeManagerWindow = new OpenVisionLab.ThreeD.Shell.Views.Recipe.RecipeManagerWindow
            {
                DataContext = navigationViewModel.Workbench,
            };
            var recipeManagerRoot = (OpenVisionLab.ThreeD.Shell.Views.Recipe.RecipeManagerView)recipeManagerWindow.Content;
            recipeManagerRoot.Measure(new Size(1244, 760));
            recipeManagerRoot.Arrange(new Rect(0, 0, 1244, 760));
            recipeManagerRoot.UpdateLayout();
            var recipeManagerTitle = (Border)recipeManagerRoot.FindName("RecipeManagerPageTitle");
            var recipeManagerCaptionHeight =
                System.Windows.Shell.WindowChrome.GetWindowChrome(recipeManagerWindow)?.CaptionHeight ?? 0;
            Check(
                "Recipe Center uses its page title as the 50px caption without a duplicate app titlebar",
                Math.Abs(recipeManagerTitle.ActualHeight - 50) < 1
                && Math.Abs(recipeManagerCaptionHeight - 50) < 0.1
                && !FindVisualDescendants<StudioTitleBarView>(recipeManagerRoot).Any(),
                $"pageTitle={recipeManagerTitle.ActualHeight:F0}; caption={recipeManagerCaptionHeight:F0}; studioTitlebars={FindVisualDescendants<StudioTitleBarView>(recipeManagerRoot).Count()}");
            Check("v3 exposes eight menu groups and Teaching, Validation, Inspection, Results routes",
                mainMenu.Items.Count == 8
                && navigationRail.AuthoringModeButton.Visibility == Visibility.Visible
                && navigationRail.ValidateModeButton.Visibility == Visibility.Visible
                && navigationRail.InspectionModeButton.Visibility == Visibility.Visible
                && navigationRail.ResultsModeButton.Visibility == Visibility.Visible
                && Equals(navigationRail.ValidateModeButton.CommandParameter, ShellWorkspaceMode.Validate)
                && Equals(navigationRail.InspectionModeButton.CommandParameter, ShellWorkspaceMode.Inspect)
                && navigationRail.CalibrateModeButton.Visibility == Visibility.Collapsed
                && navigationRail.AdvancedModeButton.Visibility == Visibility.Collapsed,
                $"menuGroups={mainMenu.Items.Count}; primary=Teaching,Validation,Inspection,Results");
            var fileMenu = (MenuItem)mainMenu.Items[0];
            Check("R19 file menu reuses the existing new, open, and save commands",
                ReferenceEquals(((MenuItem)fileMenu.Items[0]).Command, navigationViewModel.Workbench.NewTeachingRecipeCommand)
                && ReferenceEquals(((MenuItem)fileMenu.Items[1]).Command, navigationViewModel.Workbench.OpenTeachingRecipeCommand)
                && ReferenceEquals(((MenuItem)fileMenu.Items[4]).Command, navigationViewModel.Workbench.SaveTeachingRecipeCommand),
                "same Workbench command objects");
            var fileMenuPopup = FindVisualDescendants<System.Windows.Controls.Primitives.Popup>(fileMenu).Single();
            var fileMenuPopupBinding = System.Windows.Data.BindingOperations.GetBindingExpression(
                fileMenuPopup,
                System.Windows.Controls.Primitives.Popup.IsOpenProperty);
            Check(
                "R19 compact top-level menu retains a bound native popup below the command row",
                fileMenuPopup.Placement == System.Windows.Controls.Primitives.PlacementMode.Bottom
                && fileMenuPopupBinding?.ParentBinding.Path?.Path == "IsSubmenuOpen"
                && fileMenu.Items.OfType<MenuItem>().Count() == 7,
                $"placement={fileMenuPopup.Placement}; binding={fileMenuPopupBinding?.ParentBinding.Path?.Path}; commands={fileMenu.Items.OfType<MenuItem>().Count()}");
            var titleBar = new StudioTitleBarView
            {
                Width = 1280,
                RecipeStateText = "Valid | Saved | " + new string('x', 48),
                IsRecipeContextVisible = true,
                SourceQualityStatusText = "Input needs review | " + new string('x', 48),
                SourceQualityStatusToolTip = "Current input state detail",
                SourceQualityStatusKind = "Warning",
                IsSourceQualityStatusVisible = true,
                StatusText = "No active work item | " + new string('x', 48),
            };
            titleBar.Measure(new Size(1280, double.PositiveInfinity));
            titleBar.Arrange(new Rect(0, 0, 1280, titleBar.DesiredSize.Height));
            titleBar.UpdateLayout();
            var recipeStateText = (TextBlock)titleBar.FindName("RecipeStateStatusText");
            var sourceQualityText = (TextBlock)titleBar.FindName("CurrentSourceQualityStatusText");
            var shellStatusText = (TextBlock)titleBar.FindName("ShellStatusText");
            var sourceQualityBadge = (Border)titleBar.FindName("CurrentSourceQualityStatusBadge");
            var sourceQualityBounds = sourceQualityText.TransformToAncestor(titleBar).TransformBounds(new Rect(sourceQualityText.RenderSize));
            var sourceQualityBadgeBounds = sourceQualityBadge.TransformToAncestor(titleBar).TransformBounds(new Rect(sourceQualityBadge.RenderSize));
            var formattedSourceQualityText = new FormattedText(
                titleBar.SourceQualityStatusText,
                sourceQualityText.Language.GetEquivalentCulture(),
                sourceQualityText.FlowDirection,
                new Typeface(sourceQualityText.FontFamily, sourceQualityText.FontStyle, sourceQualityText.FontWeight, sourceQualityText.FontStretch),
                sourceQualityText.FontSize,
                sourceQualityText.Foreground,
                null,
                TextOptions.GetTextFormattingMode(sourceQualityText),
                VisualTreeHelper.GetDpi(sourceQualityText).PixelsPerDip)
            {
                MaxTextWidth = Math.Max(1, sourceQualityText.ActualWidth + 1),
                Trimming = TextTrimming.None
            };
            Check(
                "Title bar preserves compact context and displays the full wrapped input warning within its bounds",
                recipeStateText.TextWrapping == TextWrapping.NoWrap
                && recipeStateText.TextTrimming == TextTrimming.CharacterEllipsis
                && AutomationProperties.GetName(recipeStateText) == titleBar.RecipeStateText
                && Equals(recipeStateText.ToolTip, titleBar.RecipeStateText)
                && sourceQualityText.TextWrapping == TextWrapping.Wrap
                && sourceQualityText.TextTrimming == TextTrimming.None
                && sourceQualityText.Text == titleBar.SourceQualityStatusText
                && sourceQualityText.ActualHeight + 1 >= formattedSourceQualityText.Height
                && sourceQualityBounds.Top >= sourceQualityBadgeBounds.Top - 1
                && sourceQualityBounds.Bottom <= sourceQualityBadgeBounds.Bottom + 1
                && sourceQualityBounds.Left >= sourceQualityBadgeBounds.Left - 1
                && sourceQualityBounds.Right <= sourceQualityBadgeBounds.Right + 1
                && sourceQualityBounds.Bottom <= titleBar.ActualHeight + 1
                && shellStatusText.TextWrapping == TextWrapping.NoWrap
                && shellStatusText.TextTrimming == TextTrimming.CharacterEllipsis,
                $"recipe={recipeStateText.TextWrapping}/{recipeStateText.TextTrimming}; source={sourceQualityText.TextWrapping}/{sourceQualityText.TextTrimming}; status={shellStatusText.TextWrapping}/{shellStatusText.TextTrimming}; titleHeight={titleBar.ActualHeight:F1}; textBounds={sourceQualityBounds}; badgeBounds={sourceQualityBadgeBounds}; requiredTextHeight={formattedSourceQualityText.Height:F1}");
            Check(
                "Job Bar exposes the accessible current-source quality state",
                titleBar.HasAccessibleCurrentSourceQualityStatus,
                $"visible={titleBar.IsSourceQualityStatusVisible}; kind={titleBar.SourceQualityStatusKind}; text={titleBar.SourceQualityStatusText}");
            titleBar.IsSourceQualityStatusVisible = false;
            titleBar.UpdateLayout();
            Check(
                "Job Bar hides current-source quality when no input exists",
                titleBar.HasAccessibleCurrentSourceQualityStatus,
                $"visible={titleBar.IsSourceQualityStatusVisible}");
            Check("Workbench hosts the tool picker and every dockable workspace", workbench.HasAllDockContentHosts && workbenchContracts.All(contract => contract.HasContent), Describe(workbenchContracts));
            Check("Workbench panes can float", workbenchContracts.All(contract => contract.CanFloat), Describe(workbenchContracts));
            Check("Workbench required panes cannot close", workbenchContracts.All(contract => !contract.CanClose), Describe(workbenchContracts));
            Check("Fit Diagnostics may hide without closing", workbenchContracts.Single(contract => contract.ContentId == "fit-diagnostics").CanHide == true, Describe(workbenchContracts));
            Check(
                "Output Compare and Displayed Outputs may hide without closing",
                workbenchContracts.Single(contract => contract.ContentId == "output-compare").CanHide == true
                && workbenchContracts.Single(contract => contract.ContentId == "displayed-outputs").CanHide == true,
                Describe(workbenchContracts));

            Check(
                "Workbench starts with the empty bottom review pane collapsed",
                !workbench.IsBottomPaneExpanded && !workbench.IsBottomPaneAttached,
                $"expanded={workbench.IsBottomPaneExpanded}, attached={workbench.IsBottomPaneAttached}");

            workbench.IsBottomPaneExpanded = false;
            var focusCollapsed = !workbench.IsBottomPaneAttached;
            workbench.IsBottomPaneExpanded = true;
            Check(
                "Workbench capture focus detaches then restores bottom pane",
                focusCollapsed && workbench.IsBottomPaneAttached,
                $"collapsed={focusCollapsed}, restored={workbench.IsBottomPaneAttached}");
            workbench.ActivateProfilePane();
            Check(
                "Workbench Profile command selects docked height-profile pane",
                workbench.IsBottomPaneAttached && workbench.IsProfilePaneSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsProfilePaneSelected}");
            workbench.ActivateSessionLogPane();
            Check(
                "Workbench Session Log command selects docked session pane",
                workbench.IsBottomPaneAttached && workbench.IsSessionLogPaneSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsSessionLogPaneSelected}");
            workbench.ActivateFlowMap();
            Check(
                "Workbench Flow Map command selects the read-only map in the docked Pipeline pane",
                workbench.IsBottomPaneAttached && workbench.IsFlowMapSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsFlowMapSelected}");
            workbench.ActivateProblems();
            Check(
                "Workbench Problems command selects the read-only port diagnostics in the docked Pipeline pane",
                workbench.IsBottomPaneAttached && workbench.IsProblemsSelected,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsProblemsSelected}");
            workbench.ActivateRunRecord();
            Check(
                "Workbench Run Record exposes open, recent, two-run comparison, full-export, and privacy-safe support controls in the docked Pipeline pane",
                workbench.IsBottomPaneAttached
                && workbench.IsRunRecordSelected
                && workbench.HasRunRecordHistoryControls
                && workbench.HasPrivacySafeSupportBundleControls,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsRunRecordSelected}, history={workbench.HasRunRecordHistoryControls}, support={workbench.HasPrivacySafeSupportBundleControls}");
            workbench.ActivateOutputComparePane();
            Check(
                "Workbench Output Compare command selects a floatable pane with usable default height",
                workbench.IsBottomPaneAttached
                && workbench.IsOutputComparePaneSelected
                && workbench.HasUsableOutputCompareDockHeight,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsOutputComparePaneSelected}, usableHeight={workbench.HasUsableOutputCompareDockHeight}");
            workbench.ActivateViewerPane();
            Check(
                "A/B comparison returns to the primary Viewer and collapses its temporary comparison dock",
                workbench.IsViewerPaneSelected
                && !workbench.IsBottomPaneExpanded,
                $"viewer={workbench.IsViewerPaneSelected};bottomExpanded={workbench.IsBottomPaneExpanded}");
            workbench.ActivateOutputComparePane();
            workbench.ActivateDisplayedOutputsPane();
            Check(
                "Workbench Displayed Outputs command restores the standard bottom-pane height",
                workbench.IsBottomPaneAttached
                && workbench.IsDisplayedOutputsPaneSelected
                && workbench.HasStandardBottomPaneHeight,
                $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsDisplayedOutputsPaneSelected}, standardHeight={workbench.HasStandardBottomPaneHeight}");
            workbench.ActivateFitDiagnosticsPane();
            Check("Workbench Fit Diagnostics command selects docked diagnostics pane", workbench.IsBottomPaneAttached && workbench.IsFitDiagnosticsPaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsFitDiagnosticsPaneSelected}");
            workbench.ActivateIntersectionEvidencePane();
            Check("Workbench Intersection Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsIntersectionEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsIntersectionEvidencePaneSelected}");
            workbench.ActivateCorrespondenceEvidencePane();
            Check("Workbench Correspondence Evidence command selects docked evidence pane", workbench.IsBottomPaneAttached && workbench.IsCorrespondenceEvidencePaneSelected, $"attached={workbench.IsBottomPaneAttached}, selected={workbench.IsCorrespondenceEvidencePaneSelected}");

            var startPageShell = new ShellMainWindowViewModel();
            startPageShell.ShowStartPage();
            Check(
                "R19 start page hides the workbench and navigation without starting inspection",
                startPageShell.IsStartPageSelected
                && !startPageShell.IsNavigationRailVisible
                && !startPageShell.IsRecipeActionsVisible
                && !startPageShell.IsWorkbenchWorkspaceSelected
                && !startPageShell.IsAuthoringWorkspaceSelected
                && !startPageShell.IsInspectionWorkspaceSelected
                && !startPageShell.Workbench.IsOrderedRunRunning
                && !startPageShell.Workbench.IsSelectedStepPreviewRunning
                && !startPageShell.Workbench.IsValidationSetRunning,
                $"start={startPageShell.IsStartPageSelected}; rail={startPageShell.IsNavigationRailVisible}; workspace={startPageShell.SelectedWorkspaceMode}; running={startPageShell.Workbench.IsOrderedRunRunning || startPageShell.Workbench.IsSelectedStepPreviewRunning || startPageShell.Workbench.IsValidationSetRunning}");
            startPageShell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            Check(
                "Explicit workspace navigation closes the start page without executing a tool",
                !startPageShell.IsStartPageSelected
                && startPageShell.IsNavigationRailVisible
                && startPageShell.IsRecipeActionsVisible
                && startPageShell.IsWorkbenchWorkspaceSelected
                && startPageShell.SelectedWorkspaceMode == ShellWorkspaceMode.Teach
                && !startPageShell.Workbench.IsOrderedRunRunning
                && !startPageShell.Workbench.IsSelectedStepPreviewRunning,
                $"start={startPageShell.IsStartPageSelected}; rail={startPageShell.IsNavigationRailVisible}; workspace={startPageShell.SelectedWorkspaceMode}; running={startPageShell.Workbench.IsOrderedRunRunning || startPageShell.Workbench.IsSelectedStepPreviewRunning}");
            startPageShell.Dispose();

            var shell = new ShellMainWindowViewModel();
            var recipePath = Path.Combine(FindRepositoryRoot(), ThicknessRecipeRelativePath);
            Check(
                "stage verification opens the current bundled Thickness recipe",
                shell.Workbench.TryOpenTeachingRecipe(recipePath, out var openMessage)
                && shell.Workbench.PipelineSteps.Count == 8
                && shell.Workbench.SelectedPipelineStep is not null,
                openMessage);
            var individualDocument = ToolRecipeDocumentStore.Load(recipePath);
            var individualRecipePath = Path.Combine(GetReportDirectory(reportPath), "individual-thickness.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(individualRecipePath, individualDocument with
            {
                Source = individualDocument.Source with { Path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(recipePath)!, individualDocument.Source.Path)) },
                Steps = individualDocument.Steps.Select((step, index) => step with { ToolName = $"Individual datum {(char)('A' + index)}", Group = null }).ToArray()
            });
            Check("individual port fixture preserves the bundled source, ROI and routing outside automatic display groups",
                shell.Workbench.TryOpenTeachingRecipe(individualRecipePath, out var individualOpenMessage)
                && shell.Workbench.PipelineSteps.Count == 8
                && shell.Workbench.PipelineSteps.All(step => step.ToolName.StartsWith("Individual datum ", StringComparison.Ordinal)), individualOpenMessage);
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            Check(
                "Selecting the Teaching workspace does not execute inspection",
                shell.IsTeachWorkspaceSelected
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"teach={shell.IsTeachWorkspaceSelected}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var initialViewerCandidates = shell.Workbench.ViewerWorkspaceCandidates;
            var initialMainViewerCandidate = initialViewerCandidates.SingleOrDefault(candidate =>
                candidate.Id == shell.Workbench.ViewerWorkspace.MainContentId);
            var initialAuxiliaryViewerCandidate = initialViewerCandidates.SingleOrDefault(candidate =>
                candidate.Id == shell.Workbench.ViewerWorkspace.AuxiliaryContentId);
            Check(
                "Teaching starts with 3D alone and keeps XY available without running inspection",
                shell.Workbench.IsSourceReadyForRecipe
                && shell.Workbench.ViewerWorkspace.Layout == ViewerWorkspaceLayout.Single
                && initialMainViewerCandidate is { Kind: ViewerWorkspaceCandidateKind.ThreeDArtifact, IsSource: true }
                && initialAuxiliaryViewerCandidate?.Kind == ViewerWorkspaceCandidateKind.HeightImage
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"sourceReady={shell.Workbench.IsSourceReadyForRecipe};layout={shell.Workbench.ViewerWorkspace.Layout};main={initialMainViewerCandidate?.Id};aux={initialAuxiliaryViewerCandidate?.Id};run={shell.Workbench.IsOrderedRunRunning};preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var selectedStep = shell.Workbench.SelectedPipelineStep;
            shell.Workbench.SplitViewerVerticallyCommand.Execute(null);
            var recipeFlowView = new RecipeChainView
            {
                DataContext = shell.Workbench,
                IsTeachingMode = true,
            };
            ArrangeForTextVerification(recipeFlowView, 390);
            var flowAddToolButton = (System.Windows.Controls.Button)recipeFlowView.FindName("RecipeFlowAddTool");
            var flowToolbar = (Border)recipeFlowView.FindName("RecipeFlowToolbar");
            var flowViewport = (ScrollViewer)recipeFlowView.FindName("FlowGraphViewport");
            var flowHint = (Border)recipeFlowView.FindName("RecipeFlowHint");
            var flowZoomValue = (TextBlock)recipeFlowView.FindName("RecipeFlowZoomValue");
            var flowConnectionCanvas = (Canvas)recipeFlowView.FindName("FlowConnectionCanvas");
            var flowPortConnectionCanvas = (Canvas)recipeFlowView.FindName("FlowPortConnectionCanvas");
            var recipeStepList = (ListBox)recipeFlowView.FindName("RecipeStepList");
            var firstFlowStep = shell.Workbench.PipelineSteps[0];
            shell.Workbench.SelectedPipelineStep = firstFlowStep;
            recipeFlowView.UpdateLayout();
            recipeFlowView.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            var firstFlowItem = recipeStepList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
            var secondFlowItem = recipeStepList.ItemContainerGenerator.ContainerFromIndex(1) as ListBoxItem;
            var thirdFlowItem = recipeStepList.ItemContainerGenerator.ContainerFromIndex(2) as ListBoxItem;
            var sourceOutputPort = (Button)recipeFlowView.FindName("SourceOutputPort");
            var firstFlowInputPort = firstFlowItem is null
                ? null
                : FindVisualDescendants<Button>(firstFlowItem).FirstOrDefault(button => button.Name == "StepInputPort");
            var flowCancelKeyBinding = recipeFlowView.InputBindings
                .OfType<KeyBinding>()
                .SingleOrDefault(binding => binding.Key == Key.Escape);
            var firstFlowOutputPort = firstFlowItem is null
                ? null
                : FindVisualDescendants<Button>(firstFlowItem).SingleOrDefault(button => button.Name == "StepOutputPort");
            Check(
                "v5 full-width port rows retain their caption, thumbnail and command hit area",
                firstFlowInputPort is { ActualWidth: >= 120, ActualHeight: >= 28, Content: Grid }
                && firstFlowOutputPort is { ActualWidth: >= 120, ActualHeight: >= 28, Content: Grid }
                && firstFlowInputPort.HorizontalContentAlignment == HorizontalAlignment.Left
                && firstFlowOutputPort.HorizontalContentAlignment == HorizontalAlignment.Right,
                $"input={firstFlowInputPort?.ActualWidth}x{firstFlowInputPort?.ActualHeight};output={firstFlowOutputPort?.ActualWidth}x{firstFlowOutputPort?.ActualHeight};captions={firstFlowInputPort?.Content is TextBlock}/{firstFlowOutputPort?.Content is TextBlock}");
            var firstFlowText = firstFlowItem is null
                ? Array.Empty<string>()
                : FindVisualDescendants<TextBlock>(firstFlowItem).Where(HasVisiblePresentationPath).Select(text => text.Text).ToArray();
            var flowOutputIds = shell.Workbench.PipelineSteps
                .Select(step => step.OutputEntityId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var expectedFlowConnectionCount = shell.Workbench.PipelineSteps.Sum(step => step.InputEntityIds
                .Where(id => string.Equals(id, shell.Workbench.Source.Id, StringComparison.OrdinalIgnoreCase)
                    || flowOutputIds.Contains(id))
                .Count());
            var firstFlowOrigin = firstFlowItem?.TranslatePoint(new Point(), recipeStepList);
            var secondFlowOrigin = secondFlowItem?.TranslatePoint(new Point(), recipeStepList);
            var thirdFlowOrigin = thirdFlowItem?.TranslatePoint(new Point(), recipeStepList);
            var flowOrigins = Enumerable.Range(0, recipeStepList.Items.Count)
                .Select(index => (recipeStepList.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem)
                    ?.TranslatePoint(new Point(), recipeStepList))
                .ToArray();
            var flowIsSavedSourceFanOut = shell.Workbench.PipelineSteps.All(step =>
                step.InputEntityIds.Contains(shell.Workbench.Source.Id, StringComparer.OrdinalIgnoreCase)
                && !step.InputEntityIds.Any(flowOutputIds.Contains));
            var flowRowsMatchProposal = flowIsSavedSourceFanOut
                && flowOrigins.Length == 8
                && Enumerable.Range(1, flowOrigins.Length - 1).All(index =>
                {
                    var first = flowOrigins[index - 1];
                    var second = flowOrigins[index];
                    return first.HasValue
                        && second.HasValue
                        && Math.Abs(first.Value.X - second.Value.X) < 2
                        && second.Value.Y > first.Value.Y;
                });
            recipeStepList.SelectedIndex = 1;
            recipeFlowView.UpdateLayout();
            var flowSelectionRoundTrip = ReferenceEquals(
                shell.Workbench.SelectedPipelineStep,
                shell.Workbench.PipelineSteps[1]);
            recipeStepList.SelectedItem = selectedStep;
            recipeFlowView.UpdateLayout();
            recipeFlowView.IsTeachingMode = false;
            recipeFlowView.UpdateLayout();
            var flowAddToolHiddenDuringInspection = flowAddToolButton.Visibility == Visibility.Collapsed;
            var flowPresentationHiddenDuringInspection = flowToolbar.Visibility == Visibility.Collapsed
                && flowHint.Visibility == Visibility.Collapsed;
            recipeFlowView.IsTeachingMode = true;
            recipeFlowView.UpdateLayout();
            var flowRouteBeforePortInteraction = firstFlowStep.InputEntityIdsText;
            var flowDirtyBeforePortInteraction = shell.Workbench.IsDirty;
            var flowPortActionCountBeforeInteraction = shell.Workbench.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            var flowPortCommandsBound = firstFlowInputPort is not null
                && ReferenceEquals(sourceOutputPort.Command, shell.Workbench.BeginFlowConnectionCommand)
                && Equals(sourceOutputPort.CommandParameter, shell.Workbench.Source.Id)
                && ReferenceEquals(firstFlowInputPort.Command, shell.Workbench.ConnectFlowOutputCommand)
                && firstFlowInputPort.CommandParameter is ToolWorkbenchFlowInputItem { Index: 0 } flowInput && ReferenceEquals(flowInput.Step, firstFlowStep)
                && flowCancelKeyBinding is not null
                && ReferenceEquals(flowCancelKeyBinding.Command, shell.Workbench.CancelFlowConnectionCommand)
                && flowCancelKeyBinding.Key == Key.Escape;
            var inputDisabledUntilOutputSelected = firstFlowInputPort?.IsEnabled == false;
            sourceOutputPort.Command.Execute(sourceOutputPort.CommandParameter);
            recipeFlowView.UpdateLayout();
            var outputSelectionEnablesInput = shell.Workbench.HasPendingFlowConnection
                && firstFlowInputPort?.IsEnabled == true;
            flowCancelKeyBinding?.Command.Execute(null);
            recipeFlowView.UpdateLayout();
            Check(
                "R19 graph output/input ports bind to route commands and Escape cancels selection without running inspection",
                flowPortCommandsBound
                && inputDisabledUntilOutputSelected
                && outputSelectionEnablesInput
                && !shell.Workbench.HasPendingFlowConnection
                && firstFlowInputPort?.IsEnabled == false
                && firstFlowStep.InputEntityIdsText == flowRouteBeforePortInteraction
                && shell.Workbench.IsDirty == flowDirtyBeforePortInteraction
                && shell.Workbench.RunLog.Count(item =>
                    item.Category is "Preview" or "Publish" or "Run") == flowPortActionCountBeforeInteraction,
                $"bound={flowPortCommandsBound};inputDisabled={inputDisabledUntilOutputSelected};armed={outputSelectionEnablesInput};pending={shell.Workbench.HasPendingFlowConnection};route={firstFlowStep.InputEntityIdsText};dirty={shell.Workbench.IsDirty};actions={shell.Workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run")}");
            var flowPaths = flowConnectionCanvas.Children.OfType<System.Windows.Shapes.Path>().ToArray();
            var flowArrows = flowPortConnectionCanvas.Children.OfType<System.Windows.Shapes.Polygon>().ToArray();
            var flowPortPaths = flowPortConnectionCanvas.Children.OfType<System.Windows.Shapes.Path>().ToArray();
            var firstInputGlyph = firstFlowInputPort is null
                ? null
                : FindVisualDescendants<System.Windows.Shapes.Ellipse>(firstFlowInputPort).SingleOrDefault(glyph => glyph.Name == "PortVisual");
            var firstInputAnchor = firstInputGlyph?.TranslatePoint(
                new Point(firstInputGlyph.ActualWidth / 2, firstInputGlyph.ActualHeight / 2), flowConnectionCanvas);
            Check(
                "Expanded graph port hit regions keep saved wire endpoints on the input glyph",
                firstInputAnchor.HasValue
                && Panel.GetZIndex(flowConnectionCanvas) < Panel.GetZIndex(recipeStepList)
                && Panel.GetZIndex(flowPortConnectionCanvas) > Panel.GetZIndex(recipeStepList)
                && !flowConnectionCanvas.IsHitTestVisible && !flowPortConnectionCanvas.IsHitTestVisible
                && flowPortPaths.All(path => path.Data is GeometryGroup { Children.Count: 2 } terminals
                    && terminals.Children.All(geometry => geometry is LineGeometry line
                        && Math.Abs(line.StartPoint.Y - line.EndPoint.Y) < 0.01))
                && flowArrows.Any(arrow => (arrow.Points[0] - firstInputAnchor.Value).Length < 0.01),
                $"inputAnchor={firstInputAnchor};arrows={flowArrows.Length};z={Panel.GetZIndex(flowConnectionCanvas)}/{Panel.GetZIndex(recipeStepList)}/{Panel.GetZIndex(flowPortConnectionCanvas)};hit={flowConnectionCanvas.IsHitTestVisible}/{flowPortConnectionCanvas.IsHitTestVisible};figures={string.Join(',', flowPortPaths.Select(path => path.Data is GeometryGroup group ? group.Children.Count : 0))};sample={flowPortPaths.FirstOrDefault()?.Data}");
            var flowUsesSidePortRouting = flowPaths.All(path => path.Data.Bounds.Width > 0 && path.Data.Bounds.Height > 0)
                && flowArrows.All(arrow => arrow.Points.Count == 3
                    && arrow.Points[1].X < arrow.Points[0].X
                    && arrow.Points[2].X < arrow.Points[0].X);
            Check(
                "v3 Recipe Flow renders authored height-data routes in one column with ROI inputs retained",
                flowAddToolButton.Visibility == Visibility.Visible
                && ReferenceEquals(flowAddToolButton.Command, shell.Workbench.OpenToolLibraryCommand)
                && recipeStepList.ItemsSource == shell.Workbench.PipelineSteps
                && FindVisualDescendants<Canvas>(recipeStepList).Any()
                && !double.IsNaN(recipeStepList.Height)
                && recipeStepList.ActualHeight > 1
                && recipeStepList.Items.Count == shell.Workbench.PipelineSteps.Count
                && firstFlowText.Contains(firstFlowStep.ToolName)
                && firstFlowText.Contains(ThreeDLocalization.Shared.Resolve(
                    "ThreeD.Flow.Input", "입력", "Input"))
                && !firstFlowText.Contains(firstFlowStep.InputSummary)
                && !firstFlowText.Contains(firstFlowStep.InputPortState)
                && firstFlowText.Contains(ThreeDLocalization.Shared.Resolve(
                    "ThreeD.Flow.Output", "출력", "Output"))
                && !firstFlowText.Contains(firstFlowStep.OutputEntityId)
                && !firstFlowText.Contains(firstFlowStep.OutputPortState)
                && firstFlowText.Count(text => text == firstFlowStep.FlowNodeStatusLabel) == 1
                && expectedFlowConnectionCount > 0
                && recipeFlowView.FlowConnectionCount == expectedFlowConnectionCount
                && flowConnectionCanvas.Children.Count == expectedFlowConnectionCount
                && flowPortConnectionCanvas.Children.Count == expectedFlowConnectionCount * 2
                && flowUsesSidePortRouting
                && flowRowsMatchProposal
                && flowSelectionRoundTrip
                && ReferenceEquals(recipeStepList.SelectedItem, selectedStep)
                && flowAddToolHiddenDuringInspection
                && flowPresentationHiddenDuringInspection
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"addVisible={flowAddToolButton.Visibility}; command={ReferenceEquals(flowAddToolButton.Command, shell.Workbench.OpenToolLibraryCommand)}; items={recipeStepList.Items.Count}; connections={recipeFlowView.FlowConnectionCount}/{expectedFlowConnectionCount}; sideRouting={flowUsesSidePortRouting}; savedFanOut={flowIsSavedSourceFanOut}; rows={flowRowsMatchProposal}; nodes={string.Join(';', flowOrigins)}; firstPorts={string.Join('|', firstFlowText)}; selectionRoundTrip={flowSelectionRoundTrip}; inspectHidden={flowAddToolHiddenDuringInspection}/{flowPresentationHiddenDuringInspection}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var notRunLabel = ThreeDLocalization.Shared.ResultStatusLabel(ResultStatus.NotRun);
            Check(
                "R19 Recipe Flow omits repeated port states and labels saved, unrun tools as Not run",
                shell.Workbench.PipelineSteps.All(step =>
                    step.OutputPortState == ThreeDLocalization.Shared.FlowPortDeclared
                    && step.FlowNodeStatusLabel == notRunLabel
                    && !step.FlowNodeStatusHasIssue
                    && !step.FlowNodeStatusIsFailure)
                && !firstFlowText.Contains(firstFlowStep.InputPortState)
                && !firstFlowText.Contains(firstFlowStep.OutputPortState)
                && firstFlowText.Count(text => text == notRunLabel) == 1
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"notRun='{notRunLabel}'; nodeLabels={string.Join('|', shell.Workbench.PipelineSteps.Select(step => step.FlowNodeStatusLabel))}; firstPorts={string.Join('|', firstFlowText)}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var flowPresentationSelectedStep = shell.Workbench.SelectedPipelineStep;
            var flowPresentationDirty = shell.Workbench.IsDirty;
            for (var index = 0; index < 8; index++)
            {
                recipeFlowView.IncreaseFlowZoomCommand.Execute(null);
            }
            var flowZoomStopsAtMaximum = Math.Abs(recipeFlowView.FlowZoom - 1.5) < 0.001
                && !recipeFlowView.IncreaseFlowZoomCommand.CanExecute(null)
                && recipeFlowView.DecreaseFlowZoomCommand.CanExecute(null);
            recipeFlowView.ResetFlowViewCommand.Execute(null);
            recipeFlowView.BringSelectedToolIntoViewCommand.Execute(null);
            recipeFlowView.UpdateLayout();
            Check(
                "R19 Recipe Flow matches the reference graph controls without mutating or running the recipe",
                flowToolbar.Visibility == Visibility.Visible
                && flowHint.Visibility == Visibility.Collapsed
                && flowViewport.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto
                && flowViewport.VerticalScrollBarVisibility == ScrollBarVisibility.Auto
                && !string.IsNullOrWhiteSpace(flowZoomValue.Text)
                && flowZoomStopsAtMaximum
                && Math.Abs(recipeFlowView.FlowZoom - 1d) < 0.001
                && recipeFlowView.IncreaseFlowZoomCommand.CanExecute(null)
                && recipeFlowView.DecreaseFlowZoomCommand.CanExecute(null)
                && ReferenceEquals(flowPresentationSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.IsDirty == flowPresentationDirty
                && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"toolbar={flowToolbar.Visibility}; hint={flowHint.Visibility}; zoom={recipeFlowView.FlowZoom}/{flowZoomValue.Text}; selected={shell.Workbench.SelectedPipelineStep?.Id}; dirty={flowPresentationDirty}->{shell.Workbench.IsDirty}; run={shell.Workbench.IsOrderedRunRunning}; preview={shell.Workbench.IsSelectedStepPreviewRunning}");
            var selectedStepState = selectedStep?.State;
            var recipeStepCount = shell.Workbench.PipelineSteps.Count;
            var recipeDirty = shell.Workbench.IsDirty;
            var validationFixture =
                CompletenessValidationVerificationFixtureFactory.Create(
                    Path.Combine(
                        GetReportDirectory(reportPath),
                        "validation-set-fixture"));
            var browseSelection = ToolWorkbenchRecentRecipeItem.From(validationFixture.RecipePath);
            navigationViewModel.Workbench.SelectedRecentRecipe = browseSelection;
            recipeManagerView.UpdateLayout();
            var selectedRecipeHeading = (StackPanel)recipeManagerView.FindName("SelectedRecipeHeading");
            Check(
                "R19 Recipe Center shows selected recipe details and saved content",
                navigationViewModel.Workbench.HasSelectedRecentRecipe
                && navigationViewModel.Workbench.IsRecipePreviewVisible
                && selectedRecipeHeading.Visibility == Visibility.Visible
                && recipeBrowserContent.Visibility == Visibility.Visible,
                $"selected={navigationViewModel.Workbench.HasSelectedRecentRecipe}; content={navigationViewModel.Workbench.IsRecipePreviewVisible}; heading={selectedRecipeHeading.Visibility}; details={recipeBrowserContent.Visibility}");
            navigationViewModel.Workbench.SelectedRecentRecipe = null;
            recipeManagerView.UpdateLayout();
            Check(
                "R19 Recipe Center hides selected details when selection is cleared",
                !navigationViewModel.Workbench.HasSelectedRecentRecipe
                && !navigationViewModel.Workbench.IsRecipePreviewVisible
                && selectedRecipeHeading.Visibility == Visibility.Collapsed
                && recipeBrowserContent.Visibility == Visibility.Collapsed,
                $"selected={navigationViewModel.Workbench.HasSelectedRecentRecipe}; content={navigationViewModel.Workbench.IsRecipePreviewVisible}; heading={selectedRecipeHeading.Visibility}; details={recipeBrowserContent.Visibility}");
            var validationSamplePaths = validationFixture.Samples
                .Select(sample => sample.SourcePath)
                .ToArray();
            Check(
                "stage verification fixture owns five readable Validation Set samples",
                validationSamplePaths.All(File.Exists),
                string.Join(", ", validationSamplePaths.Select(Path.GetFileName)));
            shell.Workbench.SetValidationSetSources(validationSamplePaths);
            var stageWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = shell,
                ViewerContent = viewerOwner,
            };
            _ = ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var testWorkArea);
            var testLeft = testWorkArea.Left;
            var testTop = testWorkArea.Top;
            var monitorReportPath = Path.GetFullPath(reportPath) + ".monitors.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(monitorReportPath)!);
            File.WriteAllText(monitorReportPath, string.Empty);
            var stageHost = new Window
            {
                Content = stageWorkbench,
                Width = 1600,
                Height = 900,
                Left = testLeft,
                Top = testTop,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            stageHost.Show();
            SizeLayoutFixture(stageHost, 1600, 900);
            stageHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(stageHost, monitorReportPath);
            var viewerWorkspaceView = FindVisualDescendants<ViewerWorkspaceView>(stageWorkbench).Single();
            var auxiliaryViewerSelector = FindVisualDescendants<ComboBox>(viewerWorkspaceView).Single(comboBox =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(comboBox) == "AuxiliaryViewerContentPin");
            var languageWorkspace = shell.SelectedWorkspaceMode;
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            shell.Workbench.OpenHeightImageCommand.Execute(null);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            var auxiliaryViewerContentId = shell.Workbench.AuxiliaryViewerContentId;
            var auxiliaryKindTabs = FindVisualDescendants<ContentControl>(viewerWorkspaceView).Single(control => AutomationProperties.GetAutomationId(control) == "AuxiliaryPaneKindTabs");
            var auxiliaryHeightTab = FindVisualDescendants<ToggleButton>(auxiliaryKindTabs).Single(button => AutomationProperties.GetAutomationId(button) == "PaneKindHeight");
            var auxiliarySourceName = shell.Workbench.AuxiliaryViewerPane.SourceName;
            var stageLanguage = OpenVisionLanguageService.CurrentLanguage;
            var auxiliaryViewerSelectorEnglish = false;
            var auxiliaryEnglishTexts = string.Empty;
            var auxiliaryViewerSelectorKorean = false;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
                stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                stageHost.UpdateLayout();
                auxiliaryEnglishTexts = string.Join(" / ", FindVisualDescendants<TextBlock>(auxiliaryViewerSelector).Select(text => text.Text));
                auxiliaryViewerSelectorEnglish = auxiliaryViewerSelector.IsVisible && Equals(auxiliaryViewerSelector.SelectedValue, auxiliaryViewerContentId)
                    && FindVisualDescendants<TextBlock>(auxiliaryViewerSelector).Any(text => text.Text == auxiliarySourceName)
                    && Equals(auxiliaryHeightTab.Content, "Height") && auxiliaryHeightTab.IsChecked == true;

                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                stageHost.UpdateLayout();
                auxiliaryViewerSelectorKorean = Equals(auxiliaryViewerSelector.SelectedValue, auxiliaryViewerContentId)
                    && FindVisualDescendants<TextBlock>(auxiliaryViewerSelector).Any(text => text.Text == auxiliarySourceName)
                    && Equals(auxiliaryHeightTab.Content, "높이") && auxiliaryHeightTab.IsChecked == true;
            }
            finally
            {
                OpenVisionLanguageService.SetLanguage(stageLanguage, save: false);
                shell.SelectWorkspaceCommand.Execute(languageWorkspace);
                stageHost.UpdateLayout();
            }
            Check(
                "Auxiliary Viewer selection keeps its stable output and current language in both directions",
                auxiliaryViewerSelectorEnglish
                && auxiliaryViewerSelectorKorean
                && Equals(auxiliaryViewerSelector.SelectedValue, auxiliaryViewerContentId),
                $"english={auxiliaryViewerSelectorEnglish};texts={auxiliaryEnglishTexts};visible={auxiliaryViewerSelector.IsVisible}; korean={auxiliaryViewerSelectorKorean}; selected={auxiliaryViewerSelector.SelectedValue}; expected={auxiliaryViewerContentId}");
            Check("localized node contract refresh preserves recipe and execution identity",
                shell.Workbench.IsDirty == recipeDirty && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount && shell.Workbench.SelectedPipelineStep?.State == selectedStepState
                && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning,
                $"dirty={recipeDirty}->{shell.Workbench.IsDirty};step={shell.Workbench.SelectedPipelineStep?.Id};state={shell.Workbench.SelectedPipelineStep?.State};steps={shell.Workbench.PipelineSteps.Count}");
            var dockWorkspace = (OpenVisionDockWorkspaceView)stageWorkbench.FindName("DockWorkspace");
            var wideWorkbenchPane = (AvalonDock.Layout.LayoutPanel)dockWorkspace.FindName("workbenchPane");
            var supportPane = dockWorkspace.FindName("dataLayersPane");
            var inspectorPane = dockWorkspace.FindName("toolInspectorPane");
            var viewerPane = dockWorkspace.FindName("primaryPane");
            var viewerAnchorable = (AvalonDock.Layout.LayoutAnchorable)dockWorkspace.FindName("viewerAnchorable");
            var displayedOutputsAnchorable = (AvalonDock.Layout.LayoutAnchorable)dockWorkspace.FindName("displayedOutputsAnchorable");
            var wideResultsPaneModel = dockWorkspace.FindName("resultsPane");
            var previousCompareSlotA = shell.Workbench.CompareSlotAArtifactId;
            var previousCompareSlotB = shell.Workbench.CompareSlotBArtifactId;
            var compareCandidateIds = shell.Workbench.RenderableC3DCatalog
                .Where(candidate => candidate.IsDisplayable)
                .Select(candidate => candidate.Id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(2)
                .ToArray();
            var canRunCompareComposition = compareCandidateIds.Length == 2;
            if (canRunCompareComposition)
            {
                shell.Workbench.CompareSlotAArtifactId = compareCandidateIds[0];
                shell.Workbench.CompareSlotBArtifactId = compareCandidateIds[1];
                canRunCompareComposition = shell.Workbench.ShowOutputCompareViewerCommand.CanExecute(null);
            }
            Check(
                "Teaching compare fixture uses two distinct current renderable artifacts",
                canRunCompareComposition,
                $"candidates={string.Join('|', compareCandidateIds)}; canShow={shell.Workbench.CanShowOutputCompareViewer}");
            SizeLayoutFixture(stageHost, 1920, 1040);
            dockWorkspace.SetOperatorStage(OpenVisionOperatorStage.Teach);
            stageHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(stageHost, monitorReportPath);
            var viewerLayoutToolbar = (Border)viewerWorkspaceView.FindName("ViewerLayoutToolbar");
            var viewerWorkspaceOptions = (Menu)viewerWorkspaceView.FindName("ViewerWorkspaceOptionsMenu");
            var viewerInspectionDataSwitch = (Border)viewerWorkspaceView.FindName("ViewerInspectionDataSwitch");
            var threeDViewerMode = (ToggleButton)viewerWorkspaceView.FindName("ThreeDViewerMode");
            var heightOnlyViewerMode = (ToggleButton)viewerWorkspaceView.FindName("HeightOnlyViewerMode");
            var threeDHeightViewerMode = (ToggleButton)viewerWorkspaceView.FindName("ThreeDHeightViewerMode");
            Check(
                "single-view controls and Displayed Outputs are available before entering A/B comparison",
                viewerLayoutToolbar.Visibility == Visibility.Visible
                && displayedOutputsAnchorable.IsVisible
                && viewerAnchorable.IsSelected,
                $"toolbar={viewerLayoutToolbar.Visibility};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewerSelected={viewerAnchorable.IsSelected}");
            var wideResultsControlBeforeCompare = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, wideResultsPaneModel));
            var wideResultsGridBeforeCompare = wideResultsControlBeforeCompare is null
                ? null
                : VisualTreeHelper.GetParent(wideResultsControlBeforeCompare) as Grid;
            var wideResultsRowIndexBeforeCompare = wideResultsControlBeforeCompare is null
                ? -1
                : Grid.GetRow(wideResultsControlBeforeCompare);
            var wideResultsRowBeforeCompare = wideResultsGridBeforeCompare is not null
                && wideResultsRowIndexBeforeCompare > 0
                && wideResultsRowIndexBeforeCompare < wideResultsGridBeforeCompare.RowDefinitions.Count
                    ? wideResultsGridBeforeCompare.RowDefinitions[wideResultsRowIndexBeforeCompare]
                    : null;
            var wideResultsSplitterRowBeforeCompare = wideResultsGridBeforeCompare is not null
                && wideResultsRowIndexBeforeCompare > 0
                    ? wideResultsGridBeforeCompare.RowDefinitions[wideResultsRowIndexBeforeCompare - 1]
                    : null;
            var wideResultsRowMinHeightBeforeCompare = wideResultsRowBeforeCompare?.MinHeight ?? double.NaN;
            var wideResultsSplitterHeightBeforeCompare = wideResultsSplitterRowBeforeCompare?.Height ?? default;
            if (canRunCompareComposition)
            {
                shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
                stageHost.UpdateLayout();
            }
            WpfScreenshotCapture.Save(
                WpfScreenshotCapture.Capture(stageWorkbench).Bitmap,
                Path.ChangeExtension(reportPath, ".ab-compare-wide.png"));
            var wideCompareViewerPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .Single(pane => ReferenceEquals(pane.Model, viewerPane));
            var wideCompareResultsPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, wideResultsPaneModel));
            var wideCompareViewerBounds = wideCompareViewerPane.TransformToAncestor(dockWorkspace)
                .TransformBounds(new Rect(wideCompareViewerPane.RenderSize));
            var wideCompareViewerTree = DescribeVisualAncestors(wideCompareViewerPane, dockWorkspace);
            var wideCompareResultsBounds = wideCompareResultsPane?.TransformToAncestor(dockWorkspace)
                .TransformBounds(new Rect(wideCompareResultsPane.RenderSize)) ?? Rect.Empty;
            var wideComparePaneBounds = string.Join(';', FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace)
                .Select(pane => $"{(ReferenceEquals(pane.Model, wideResultsPaneModel) ? "results" : ReferenceEquals(pane.Model, viewerPane) ? "viewer" : ReferenceEquals(pane.Model, supportPane) ? "support" : ReferenceEquals(pane.Model, inspectorPane) ? "inspector" : "other")}:{string.Join('|', ((AvalonDock.Layout.LayoutAnchorablePane)pane.Model).Children.Select(item => item.ContentId))}:{pane.TransformToAncestor(dockWorkspace).TransformBounds(new Rect(pane.RenderSize))}"));
            var wideRootChildren = string.Join(',', ((AvalonDock.Layout.LayoutPanel)dockWorkspace.FindName("workspaceRootPanel"))
                .Children.Select(child => child.GetType().Name));
            var wideCompareColumn = ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).Parent as AvalonDock.Layout.LayoutPanel;
            Check(
                "A/B comparison keeps display modes and the menu path for closing comparison reachable",
                viewerLayoutToolbar.Visibility == Visibility.Visible
                && viewerWorkspaceOptions.Visibility == Visibility.Visible
                && threeDViewerMode.Visibility == Visibility.Visible
                && ((MenuItem)viewerWorkspaceView.FindName("ViewerInspectionOutputCompare")).Command?.CanExecute(null) == true
                && OpenVisionLab.ThreeD.Presentation.Behaviors.ViewerKeyboardBehavior.GetBindings(viewerWorkspaceView).Any(binding => binding.Key == Key.D2
                    && ReferenceEquals(binding.Command, shell.Workbench.ShowHeightImageOnlyCommand))
                && threeDHeightViewerMode.Visibility == Visibility.Visible
                && viewerInspectionDataSwitch.Visibility == Visibility.Visible
                && !displayedOutputsAnchorable.IsVisible
                && ((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Children.Count == 1
                && viewerAnchorable.IsSelected
                && shell.Workbench.IsOutputCompareViewerActive,
                $"toolbar={viewerLayoutToolbar.Visibility};options={viewerWorkspaceOptions.Visibility};dataSwitch={viewerInspectionDataSwitch.Visibility};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewerSelected={viewerAnchorable.IsSelected};compare={shell.Workbench.IsOutputCompareViewerActive}");
            Check(
                "A/B comparison expands the teaching Viewer and hides Results in Wide layout",
                canRunCompareComposition
                && shell.Workbench.IsOutputCompareViewerActive
                && dockWorkspace.HasOutputCompareAuthoringComposition
                && !dockWorkspace.HasAuthoringResultsPaneComposition
                && wideCompareColumn is not null
                && ReferenceEquals(wideCompareColumn, ((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Parent)
                && ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockHeight.IsStar
                && ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockHeight.Value <= 0.001
                && ((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockMinHeight == 0
                && wideResultsRowBeforeCompare?.MinHeight == 0
                && wideResultsSplitterRowBeforeCompare?.Height == new GridLength(0)
                && (wideCompareResultsPane is null || !wideCompareResultsPane.IsVisible)
                && wideCompareViewerBounds.Bottom >= dockWorkspace.ActualHeight * 0.99
                && shell.Workbench.IsOutputCompareViewerActive
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                 && !shell.Workbench.IsSelectedStepPreviewRunning
                 && !shell.Workbench.IsOrderedRunRunning,
                $"compare={dockWorkspace.HasOutputCompareAuthoringComposition}; resultsParent={((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).Parent}; resultsHeight={((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockHeight}; resultsMinHeight={((AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel).DockMinHeight}; resultsVisible={wideCompareResultsPane?.IsVisible}; viewerBounds={wideCompareViewerBounds}; dockHeight={dockWorkspace.ActualHeight:F0}; viewerTree={wideCompareViewerTree}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "the selected A/B action closes compare and restores its layout controls and Results without running inspection",
                !shell.Workbench.IsOutputCompareViewerActive
                && viewerLayoutToolbar.Visibility == Visibility.Visible
                && viewerWorkspaceOptions.Visibility == Visibility.Visible
                && displayedOutputsAnchorable.IsVisible
                && viewerAnchorable.IsSelected
                && dockWorkspace.HasAuthoringResultsPaneComposition
                && wideResultsRowBeforeCompare?.MinHeight == wideResultsRowMinHeightBeforeCompare
                && wideResultsSplitterRowBeforeCompare?.Height == wideResultsSplitterHeightBeforeCompare
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"compare={shell.Workbench.IsOutputCompareViewerActive};toolbar={viewerLayoutToolbar.Visibility};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewerSelected={viewerAnchorable.IsSelected};results={dockWorkspace.HasAuthoringResultsPaneComposition};preview={shell.Workbench.IsSelectedStepPreviewRunning};run={shell.Workbench.IsOrderedRunRunning}");
            displayedOutputsAnchorable.IsSelected = true;
            displayedOutputsAnchorable.IsActive = true;
            stageHost.UpdateLayout();
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "A/B comparison selects the Viewer and hides a previously selected Displayed Outputs tab",
                shell.Workbench.IsOutputCompareViewerActive
                && !displayedOutputsAnchorable.IsVisible
                && viewerAnchorable.IsSelected
                && viewerAnchorable.IsActive,
                $"compare={shell.Workbench.IsOutputCompareViewerActive};displayedOutputs={displayedOutputsAnchorable.IsVisible};viewer={viewerAnchorable.IsSelected}/{viewerAnchorable.IsActive}");
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "closing A/B restores the prior Displayed Outputs selection",
                !shell.Workbench.IsOutputCompareViewerActive
                && displayedOutputsAnchorable.IsVisible
                && displayedOutputsAnchorable.IsSelected
                && displayedOutputsAnchorable.IsActive
                && dockWorkspace.HasAuthoringResultsPaneComposition
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"compare={shell.Workbench.IsOutputCompareViewerActive};displayedOutputs={displayedOutputsAnchorable.IsVisible};selected={displayedOutputsAnchorable.IsSelected}/{displayedOutputsAnchorable.IsActive};results={dockWorkspace.HasAuthoringResultsPaneComposition};preview={shell.Workbench.IsSelectedStepPreviewRunning};run={shell.Workbench.IsOrderedRunRunning}");
            viewerAnchorable.IsSelected = true;
            viewerAnchorable.IsActive = true;
            shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
            stageHost.UpdateLayout();
            shell.Workbench.SelectedPipelineStep = null;
            var wideCompareClosed = !shell.Workbench.IsOutputCompareViewerActive;
            shell.Workbench.SelectedPipelineStep = selectedStep;
            stageHost.UpdateLayout();
            Check(
                "Closing A/B comparison restores the Results pane and saved Wide teaching layout",
                dockWorkspace.HasAuthoringResultsPaneComposition
                && !dockWorkspace.HasOutputCompareAuthoringComposition
                && wideCompareClosed
                && wideResultsRowBeforeCompare?.MinHeight == wideResultsRowMinHeightBeforeCompare
                && wideResultsSplitterRowBeforeCompare?.Height == wideResultsSplitterHeightBeforeCompare
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"results={dockWorkspace.HasAuthoringResultsPaneComposition}; compare={dockWorkspace.HasOutputCompareAuthoringComposition}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            var widePaneControls = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace).ToArray();
            var widePanes = widePaneControls
                .Where(pane => ReferenceEquals(pane.Model, supportPane)
                    || ReferenceEquals(pane.Model, inspectorPane)
                    || ReferenceEquals(pane.Model, viewerPane))
                .ToArray();
            var wideResultsPaneControl = widePaneControls.Single(pane => ReferenceEquals(pane.Model, wideResultsPaneModel));
            var wideSupportWidth = widePanes.Single(pane => ReferenceEquals(pane.Model, supportPane)).ActualWidth;
            var wideInspectorWidth = widePanes.Single(pane => ReferenceEquals(pane.Model, inspectorPane)).ActualWidth;
            var wideViewerWidth = widePanes.Single(pane => ReferenceEquals(pane.Model, viewerPane)).ActualWidth;
            var wideViewerBounds = widePanes.Single(pane => ReferenceEquals(pane.Model, viewerPane))
                .TransformToAncestor(dockWorkspace).TransformBounds(new Rect(widePanes.Single(pane => ReferenceEquals(pane.Model, viewerPane)).RenderSize));
            var wideHostWidth = stageHost.ActualWidth;
            var wideDockWidth = dockWorkspace.ActualWidth;
            var widePaneSummary = string.Join(',', widePanes.Select(pane => $"{pane.ActualWidth:F0}/{pane.IsVisible}"));
            var wideWeights = $"{((AvalonDock.Layout.LayoutAnchorablePane)supportPane).DockWidth.Value:F2}+{((AvalonDock.Layout.LayoutAnchorablePane)inspectorPane).DockWidth.Value:F2}+{((AvalonDock.Layout.LayoutPanel)((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Parent).DockWidth.Value:F2}";
            dockWorkspace.ApplyPresentationState(OpenVisionDockPresentationState.Default with
            {
                Wide = OpenVisionDockPresentationState.Default.Wide with
                {
                    AuthoringSupport = 1.30,
                    AuthoringInspector = 1.87,
                    AuthoringViewer = 6.84,
                },
            });
            var appliedWideSupportWeight = ((AvalonDock.Layout.LayoutAnchorablePane)supportPane).DockWidth.Value;
            var appliedWideInspectorWeight = ((AvalonDock.Layout.LayoutAnchorablePane)inspectorPane).DockWidth.Value;
            var appliedWideViewerWeight = ((AvalonDock.Layout.LayoutPanel)((AvalonDock.Layout.LayoutAnchorablePane)viewerPane).Parent).DockWidth.Value;
            stageHost.UpdateLayout();
            var recoveredWidePaneControls = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(dockWorkspace).ToArray();
            var recoveredWideSupportWidth = recoveredWidePaneControls.Single(pane => ReferenceEquals(pane.Model, supportPane)).ActualWidth;
            var recoveredWideInspectorWidth = recoveredWidePaneControls.Single(pane => ReferenceEquals(pane.Model, inspectorPane)).ActualWidth;
            var recoveredWideViewerWidth = recoveredWidePaneControls.Single(pane => ReferenceEquals(pane.Model, viewerPane)).ActualWidth;
            Check(
                "v3 Wide authoring keeps Recipe Flow and Tool settings readable with narrow saved ratios",
                recoveredWideSupportWidth >= 329
                && recoveredWideInspectorWidth >= 319
                && recoveredWideViewerWidth >= 700
                && Math.Abs(appliedWideSupportWeight - 1.30) < 0.01
                && Math.Abs(appliedWideInspectorWeight - 1.87) < 0.01
                && Math.Abs(appliedWideViewerWeight - 6.84) < 0.01
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"requested={appliedWideSupportWeight:F2}+{appliedWideInspectorWeight:F2}+{appliedWideViewerWeight:F2}; support={recoveredWideSupportWidth:F0}; inspector={recoveredWideInspectorWidth:F0}; viewer={recoveredWideViewerWidth:F0}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; recipe={shell.Workbench.IsOrderedRunRunning}");
            Check(
                "Teaching keeps one pane heading without duplicate dock title or empty single-tab strip",
                dockWorkspace.IsAuthoringLayout
                && FindVisualDescendants<AvalonDock.Controls.AnchorablePaneTitle>(dockWorkspace).Where(title => title.IsVisible)
                    .Select(title => AutomationProperties.GetAutomationId(title)).SequenceEqual(["TeachingResultsGrip"])
                && recoveredWidePaneControls.Where(pane => pane.Items.Count == 1).All(pane =>
                    pane.Template.FindName("PaneTabHeader", pane) is Border { ActualHeight: 0 }),
                $"authoring={dockWorkspace.IsAuthoringLayout}; titles={string.Join(',', FindVisualDescendants<AvalonDock.Controls.AnchorablePaneTitle>(dockWorkspace).Select(title => title.IsVisible))}; strips={string.Join(',', recoveredWidePaneControls.Where(pane => pane.Items.Count == 1).Select(pane => (pane.Template.FindName("PaneTabHeader", pane) as Border)?.ActualHeight))}");
            dockWorkspace.ResetPresentationState();
            stageHost.UpdateLayout();
            var compactWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = shell,
                ViewerContent = viewerOwner,
            };
            var compactDockWorkspace = (OpenVisionDockWorkspaceView)compactWorkbench.FindName("DockWorkspace");
            var compactWorkbenchPane = (AvalonDock.Layout.LayoutPanel)compactDockWorkspace.FindName("workbenchPane");
            var compactResultsPaneModel = compactDockWorkspace.FindName("resultsPane");
            var compactInspectorPane = compactDockWorkspace.FindName("toolInspectorPane");
            var compactInspectorAnchorable = (AvalonDock.Layout.LayoutAnchorable)compactDockWorkspace.FindName("toolInspectorAnchorable");
            var compactViewerPane = compactDockWorkspace.FindName("primaryPane");
            compactDockWorkspace.ApplyPresentationState(OpenVisionDockPresentationState.Default with
            {
                Compact = OpenVisionDockPresentationState.Default.Compact with
                {
                    AuthoringSupport = 0.35,
                    AuthoringInspector = 1.20,
                    AuthoringViewer = 3.80,
                },
            });
            compactDockWorkspace.SetOperatorStage(OpenVisionOperatorStage.Teach);
            var compactHost = new Window
            {
                Content = compactWorkbench,
                Width = 1280,
                Height = 780,
                Left = testLeft,
                Top = testTop,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            compactHost.Show();
            SizeLayoutFixture(compactHost, 1280, 780);
            compactWorkbench.ActivateSelectedToolPane();
            compactHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(compactHost, monitorReportPath);
            var compactResultsControlBeforeCompare = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, compactResultsPaneModel));
            var compactResultsGridBeforeCompare = compactResultsControlBeforeCompare is null
                ? null
                : VisualTreeHelper.GetParent(compactResultsControlBeforeCompare) as Grid;
            var compactResultsRowIndexBeforeCompare = compactResultsControlBeforeCompare is null
                ? -1
                : Grid.GetRow(compactResultsControlBeforeCompare);
            var compactResultsRowBeforeCompare = compactResultsGridBeforeCompare is not null
                && compactResultsRowIndexBeforeCompare > 0
                && compactResultsRowIndexBeforeCompare < compactResultsGridBeforeCompare.RowDefinitions.Count
                    ? compactResultsGridBeforeCompare.RowDefinitions[compactResultsRowIndexBeforeCompare]
                    : null;
            var compactResultsSplitterRowBeforeCompare = compactResultsGridBeforeCompare is not null
                && compactResultsRowIndexBeforeCompare > 0
                    ? compactResultsGridBeforeCompare.RowDefinitions[compactResultsRowIndexBeforeCompare - 1]
                    : null;
            var compactResultsRowMinHeightBeforeCompare = compactResultsRowBeforeCompare?.MinHeight ?? double.NaN;
            var compactResultsSplitterHeightBeforeCompare = compactResultsSplitterRowBeforeCompare?.Height ?? default;
            if (canRunCompareComposition)
            {
                shell.Workbench.ShowOutputCompareViewerCommand.Execute(null);
                compactHost.UpdateLayout();
            }
            WpfScreenshotCapture.Save(
                WpfScreenshotCapture.Capture(compactWorkbench).Bitmap,
                Path.ChangeExtension(reportPath, ".ab-compare-compact.png"));
            var compactCompareViewerPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .Single(pane => ReferenceEquals(pane.Model, compactViewerPane));
            var compactCompareResultsPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .SingleOrDefault(pane => ReferenceEquals(pane.Model, compactResultsPaneModel));
            var compactCompareViewerBounds = compactCompareViewerPane.TransformToAncestor(compactDockWorkspace)
                .TransformBounds(new Rect(compactCompareViewerPane.RenderSize));
            var compactCompareViewerTree = DescribeVisualAncestors(compactCompareViewerPane, compactDockWorkspace);
            var compactCompareResultsBounds = compactCompareResultsPane?.TransformToAncestor(compactDockWorkspace)
                .TransformBounds(new Rect(compactCompareResultsPane.RenderSize)) ?? Rect.Empty;
            var compactComparePaneBounds = string.Join(';', FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .Select(pane => $"{(ReferenceEquals(pane.Model, compactResultsPaneModel) ? "results" : ReferenceEquals(pane.Model, compactViewerPane) ? "viewer" : "other")}:{string.Join('|', ((AvalonDock.Layout.LayoutAnchorablePane)pane.Model).Children.Select(item => item.ContentId))}:{pane.TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(pane.RenderSize))}"));
            var compactRootChildren = string.Join(',', ((AvalonDock.Layout.LayoutPanel)compactDockWorkspace.FindName("workspaceRootPanel"))
                .Children.Select(child => child.GetType().Name));
            var compactCompareColumn = ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).Parent as AvalonDock.Layout.LayoutPanel;
            Check(
                "A/B comparison expands the teaching Viewer and hides Results in Compact layout",
                canRunCompareComposition
                && shell.Workbench.IsOutputCompareViewerActive
                && compactDockWorkspace.HasOutputCompareAuthoringComposition
                && !compactDockWorkspace.HasAuthoringResultsPaneComposition
                && compactCompareColumn is not null
                && ReferenceEquals(compactCompareColumn, ((AvalonDock.Layout.LayoutAnchorablePane)compactViewerPane).Parent)
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockHeight.IsStar
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockHeight.Value <= 0.001
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockMinHeight == 0
                && compactResultsRowBeforeCompare?.MinHeight == 0
                && compactResultsSplitterRowBeforeCompare?.Height == new GridLength(0)
                && (compactCompareResultsPane is null || !compactCompareResultsPane.IsVisible)
                && compactCompareViewerBounds.Bottom >= compactDockWorkspace.ActualHeight * 0.99
                && shell.Workbench.IsOutputCompareViewerActive
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"compare={compactDockWorkspace.HasOutputCompareAuthoringComposition}; resultsParent={((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).Parent}; resultsHeight={((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockHeight}; resultsMinHeight={((AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel).DockMinHeight}; resultsVisible={compactCompareResultsPane?.IsVisible}; viewerBounds={compactCompareViewerBounds}; dockHeight={compactDockWorkspace.ActualHeight:F0}; viewerTree={compactCompareViewerTree}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            shell.Workbench.SelectedPipelineStep = null;
            var compactCompareClosed = !shell.Workbench.IsOutputCompareViewerActive;
            shell.Workbench.SelectedPipelineStep = selectedStep;
            compactHost.UpdateLayout();
            Check(
                "Closing A/B comparison restores the Results pane and saved Compact teaching layout",
                compactDockWorkspace.HasAuthoringResultsPaneComposition
                && !compactDockWorkspace.HasOutputCompareAuthoringComposition
                && compactCompareClosed
                && compactResultsRowBeforeCompare?.MinHeight == compactResultsRowMinHeightBeforeCompare
                && compactResultsSplitterRowBeforeCompare?.Height == compactResultsSplitterHeightBeforeCompare
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"results={compactDockWorkspace.HasAuthoringResultsPaneComposition}; compare={compactDockWorkspace.HasOutputCompareAuthoringComposition}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; run={shell.Workbench.IsOrderedRunRunning}");
            shell.Workbench.CompareSlotAArtifactId = previousCompareSlotA;
            shell.Workbench.CompareSlotBArtifactId = previousCompareSlotB;
            var compactViewerBeforeSettle = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace)
                .Single(pane => ReferenceEquals(pane.Model, compactViewerPane)).RenderSize;
            compactHost.Dispatcher.Invoke(new Action(compactHost.UpdateLayout), System.Windows.Threading.DispatcherPriority.ContextIdle);
            var compactSupportPane = compactDockWorkspace.FindName("dataLayersPane");
            var compactPaneControls = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(compactDockWorkspace).ToArray();
            var compactPanes = compactPaneControls
                .Where(pane => ReferenceEquals(pane.Model, compactSupportPane)
                    || ReferenceEquals(pane.Model, compactInspectorPane)
                    || ReferenceEquals(pane.Model, compactViewerPane))
                .ToArray();
            var compactResultsPaneControl = compactPaneControls.Single(pane => ReferenceEquals(pane.Model, compactResultsPaneModel));
            var compactSupportWidth = compactPanes.Single(pane => ReferenceEquals(pane.Model, compactSupportPane)).ActualWidth;
            var compactInspectorWidth = compactPanes.Single(pane => ReferenceEquals(pane.Model, compactInspectorPane)).ActualWidth;
            var compactViewerWidth = compactPanes.Single(pane => ReferenceEquals(pane.Model, compactViewerPane)).ActualWidth;
            var compactHostWidth = compactHost.ActualWidth;
            var compactDockWidth = compactDockWorkspace.ActualWidth;
            var compactPaneSummary = string.Join(',', compactPanes.Select(pane =>
            {
                var bounds = pane.TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(pane.RenderSize));
                var model = (AvalonDock.Layout.LayoutAnchorablePane)pane.Model;
                return $"{bounds.Left:F0}-{bounds.Right:F0}/{pane.IsVisible}/{model.Parent?.GetType().Name}/{model.Children.Count}";
            }));
            var wideResultsBounds = wideResultsPaneControl.TransformToAncestor(dockWorkspace).TransformBounds(new Rect(wideResultsPaneControl.RenderSize));
            var compactResultsBounds = compactResultsPaneControl.TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(compactResultsPaneControl.RenderSize));
            var compactViewerBounds = compactPanes.Single(pane => ReferenceEquals(pane.Model, compactViewerPane))
                .TransformToAncestor(compactDockWorkspace).TransformBounds(new Rect(compactPanes.Single(pane => ReferenceEquals(pane.Model, compactViewerPane)).RenderSize));
            var compactWeights = $"{((AvalonDock.Layout.LayoutAnchorablePane)compactSupportPane).DockWidth.Value:F2}+{((AvalonDock.Layout.LayoutPanel)((AvalonDock.Layout.LayoutAnchorablePane)compactViewerPane).Parent).DockWidth.Value:F2}";
            var widePaneWidthTotal = wideSupportWidth + wideInspectorWidth + wideViewerWidth;
            var compactPaneWidthTotal = compactSupportWidth + compactInspectorWidth + compactViewerWidth;
            var wideScale = widePaneWidthTotal / (301d + 277 + 833);
            var wideResultsUnderViewer = wideResultsBounds.Top >= wideViewerBounds.Bottom - 1
                && Math.Abs(wideResultsBounds.Left - wideViewerBounds.Left) < 3
                && Math.Abs(wideResultsBounds.Right - wideViewerBounds.Right) < 3;
            var compactResultsUnderViewer = compactResultsBounds.Top >= compactViewerBounds.Bottom - 1
                && Math.Abs(compactResultsBounds.Left - compactViewerBounds.Left) < 3
                && Math.Abs(compactResultsBounds.Right - compactViewerBounds.Right) < 3;
            var wideResultsPane = (AvalonDock.Layout.LayoutAnchorablePane)wideResultsPaneModel;
            var compactResultsPane = (AvalonDock.Layout.LayoutAnchorablePane)compactResultsPaneModel;
            var wideViewerResultsColumn = (AvalonDock.Layout.LayoutPanel)wideResultsPane.Parent;
            var compactViewerResultsColumn = (AvalonDock.Layout.LayoutPanel)compactResultsPane.Parent;
            var wideLayoutMatchesR19 = wideWorkbenchPane.Children.Count == 3
                && ReferenceEquals(wideWorkbenchPane.Children[0], supportPane)
                && ReferenceEquals(wideWorkbenchPane.Children[1], inspectorPane)
                && ReferenceEquals(wideWorkbenchPane.Children[2], wideViewerResultsColumn)
                && wideViewerResultsColumn.Orientation == Orientation.Vertical
                && ReferenceEquals(wideViewerResultsColumn.Children[0], viewerPane)
                && ReferenceEquals(wideViewerResultsColumn.Children[1], wideResultsPaneModel);
            var compactLayoutMatchesProposal = compactWorkbenchPane.Children.Count == 3
                && ReferenceEquals(compactWorkbenchPane.Children[0], compactSupportPane)
                && ReferenceEquals(compactWorkbenchPane.Children[1], compactInspectorPane)
                && ReferenceEquals(compactWorkbenchPane.Children[2], compactViewerResultsColumn)
                && compactViewerResultsColumn.Orientation == Orientation.Vertical
                && ReferenceEquals(compactViewerResultsColumn.Children[0], compactViewerPane)
                && ReferenceEquals(compactViewerResultsColumn.Children[1], compactResultsPaneModel)
                && !((AvalonDock.Layout.LayoutAnchorablePane)compactSupportPane).Children.Contains(compactInspectorAnchorable)
                && ((AvalonDock.Layout.LayoutAnchorablePane)compactInspectorPane).Children.Contains(compactInspectorAnchorable)
                && compactInspectorAnchorable.IsSelected;
            Check(
                "v3 Compact keeps flow, selected tool, and Viewer visible together after a narrow saved layout",
                !dockWorkspace.IsCompactLayout
                && wideLayoutMatchesR19
                && widePanes.Length == 3
                && widePanes.All(pane => pane.IsVisible)
                && Math.Abs(wideSupportWidth - 330) < 2
                && Math.Abs(wideInspectorWidth - 320) < 2
                && Math.Abs(wideViewerWidth - (widePaneWidthTotal - 650)) < 2
                && compactDockWorkspace.IsCompactLayout
                && compactLayoutMatchesProposal
                && compactPanes.Length == 3
                && compactPanes.All(pane => pane.IsVisible)
                && compactSupportWidth >= 260 - 1
                && compactInspectorWidth >= 290 - 1
                && compactViewerWidth > compactInspectorWidth
                && compactSupportWidth + compactInspectorWidth + compactViewerWidth <= compactDockWidth,
                $"wideHost={wideHostWidth:F0}; wideDock={wideDockWidth:F0}; wideScale={wideScale:F3}; wideWeights={wideWeights}; wide={wideSupportWidth:F0}+{wideInspectorWidth:F0}+{wideViewerWidth:F0}[{widePaneSummary}]; compactHost={compactHostWidth:F0}; compactDock={compactDockWidth:F0}; compactWeights={compactWeights}; compact={compactSupportWidth:F0}+{compactInspectorWidth:F0}+{compactViewerWidth:F0}[{compactPaneSummary}]");
            Check(
                "v3 Results is confined to the Viewer column at the source Wide and Compact heights",
                wideResultsPaneControl.IsVisible
                && compactResultsPaneControl.IsVisible
                && Math.Abs(wideResultsBounds.Height - 230) < 2
                && Math.Abs(compactResultsBounds.Height - 170) < 2
                && wideResultsUnderViewer
                && compactResultsUnderViewer,
                $"wideResults={wideResultsBounds}; wideViewer={wideViewerBounds}; compactResults={compactResultsBounds}; compactViewer={compactViewerBounds}; beforeSettle={compactViewerBeforeSettle}");
            var compactSavedState = compactDockWorkspace.CapturePresentationState();
            var compactLayoutPath = Path.ChangeExtension(reportPath, ".compact-layout.json");
            var compactLayoutStore = new StudioLayoutProfileStore(compactLayoutPath);
            compactLayoutStore.Save(StudioLayoutProfile.Default with { Workbench = compactSavedState });
            var compactReloadedState = compactLayoutStore.Load();
            compactDockWorkspace.ResetPresentationState();
            compactDockWorkspace.ApplyPresentationState(compactReloadedState.Profile.Workbench);
            compactDockWorkspace.SetOperatorStage(OpenVisionOperatorStage.Validate);
            compactDockWorkspace.SetOperatorStage(OpenVisionOperatorStage.Teach);
            compactHost.UpdateLayout();
            var compactReopenedState = compactDockWorkspace.CapturePresentationState();
            Check(
                "v3 Compact restores all three saved pane proportions without changing recipe or executing",
                compactReloadedState.Status == StudioLayoutLoadStatus.Restored
                && compactReopenedState.Compact.AuthoringSupport == compactSavedState.Compact.AuthoringSupport
                && compactReopenedState.Compact.AuthoringInspector == compactSavedState.Compact.AuthoringInspector
                && compactReopenedState.Compact.AuthoringViewer == compactSavedState.Compact.AuthoringViewer
                && compactReopenedState.Wide == compactSavedState.Wide
                && compactDockWorkspace.HasTeachStageComposition
                && compactWorkbench.HasStableStageHostedDataContexts
                && shell.Workbench.IsDirty == recipeDirty
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"saved={compactSavedState.Compact};reopened={compactReopenedState.Compact};dirty={recipeDirty}->{shell.Workbench.IsDirty};steps={shell.Workbench.PipelineSteps.Count};preview={shell.Workbench.IsSelectedStepPreviewRunning};run={shell.Workbench.IsOrderedRunRunning}");
            compactHost.Close();
            stageHost.UpdateLayout();
            var teachingHeader = new StudioHeaderView { DataContext = shell };
            ArrangeForTextVerification(teachingHeader, 1600);
            var loadTeachingInputButton = FindVisualDescendants<Button>(teachingHeader).Single(button =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(button) == "LoadTeachingInputButton");
            var teachingMenu = FindVisualDescendants<Menu>(teachingHeader).Single();
            var inspectionNavigation = ((MenuItem)teachingMenu.Items[3]).Items.OfType<MenuItem>().Single(item =>
                AutomationProperties.GetAutomationId(item) == "OpenInspectionWorkspaceButton");
            var recipeInfoEditor = ((MenuItem)teachingMenu.Items[0]).Items.OfType<MenuItem>().Single(item =>
                AutomationProperties.GetAutomationId(item) == "OpenRecipeInfoEditor");
            var saveTeachingRecipeButton = FindVisualDescendants<Button>(teachingHeader).Single(button => AutomationProperties.GetAutomationId(button) == "SaveTeachingRecipeHeaderButton");
            Check(
                "v3 removes the extra workspace label strip from the full stage",
                stageWorkbench.FindName("WorkspaceCommandBar") is null,
                $"legacyCommandBar={stageWorkbench.FindName("WorkspaceCommandBar") is not null}");
            Check(
                "R19 Teaching header exposes input and save while menus retain secondary commands",
                loadTeachingInputButton.Visibility == Visibility.Visible
                && saveTeachingRecipeButton.Visibility == Visibility.Visible
                && FindVisualDescendants<Wpf.Ui.Controls.Button>(teachingHeader).Count(button => button.Command is not null) == 2
                && ReferenceEquals(loadTeachingInputButton.Command, shell.Workbench.LoadC3DSourceCommand)
                && ReferenceEquals(saveTeachingRecipeButton.Command, shell.Workbench.SaveTeachingRecipeCommand)
                && ReferenceEquals(recipeInfoEditor.Command, shell.Workbench.OpenRecipeInfoEditorCommand)
                && ReferenceEquals(inspectionNavigation.Command, shell.SelectWorkspaceCommand)
                && Equals(inspectionNavigation.CommandParameter, ShellWorkspaceMode.Inspect),
                $"load={loadTeachingInputButton.Visibility}; save={saveTeachingRecipeButton.Visibility}; infoBound={ReferenceEquals(recipeInfoEditor.Command, shell.Workbench.OpenRecipeInfoEditorCommand)}; inspectParameter={inspectionNavigation.CommandParameter}");
            var selectedToolView = FindVisualDescendants<SelectedToolWorkspaceView>(stageWorkbench).Single();
            var selectedToolHeaderTitle = (TextBlock)selectedToolView.FindName("SelectedToolHeaderTitle");
            var selectedToolStepSelector = (ComboBox)selectedToolView.FindName("SelectedToolStepSelector");
            var selectorOriginal = selectedToolStepSelector.SelectedItem;
            var selectorTarget = shell.Workbench.PipelineSteps.First(step => !ReferenceEquals(step, selectorOriginal));
            selectedToolStepSelector.SelectedItem = selectorTarget;
            stageHost.UpdateLayout();
            var selectorRoundTrip = ReferenceEquals(shell.Workbench.SelectedPipelineStep, selectorTarget);
            selectedToolStepSelector.SelectedItem = selectorOriginal;
            stageHost.UpdateLayout();
            Check(
                "R19 Tool settings header selects an authored step without execution",
                !string.IsNullOrWhiteSpace(selectedToolHeaderTitle.Text)
                && selectedToolStepSelector.ItemsSource == shell.Workbench.PipelineSteps
                && selectedToolStepSelector.Items.Count == shell.Workbench.PipelineSteps.Count
                && selectorRoundTrip
                && ReferenceEquals(shell.Workbench.SelectedPipelineStep, selectorOriginal)
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"title={selectedToolHeaderTitle.Text}; items={selectedToolStepSelector.Items.Count}; roundTrip={selectorRoundTrip}; preview={shell.Workbench.IsSelectedStepPreviewRunning}; recipe={shell.Workbench.IsOrderedRunRunning}");
            var editorAllTab = (RadioButton)selectedToolView.FindName("EditorAllTab");
            var editorRegionTab = (RadioButton)selectedToolView.FindName("EditorRegionTab");
            var editorJudgmentTab = (RadioButton)selectedToolView.FindName("EditorJudgmentTab");
            var editorDetailTab = (RadioButton)selectedToolView.FindName("EditorDetailTab");
            var sectionMenuLabel = (TextBlock)selectedToolView.FindName("SelectedToolSectionMenuLabel");
            var parametersExpander = (Expander)selectedToolView.FindName("ParametersExpander");
            var regionsExpander = (Expander)selectedToolView.FindName("RegionsExpander");
            var settingsDetails = (Expander)selectedToolView.FindName("SettingsDetails");
            var editorPages = (FrameworkElement)VisualTreeHelper.GetParent(editorAllTab);
            var activeRoiSummary = (Border)selectedToolView.FindName("ActiveRoiLifecycleSummary");
            var repeatGridPanel = (Border)selectedToolView.FindName("ThicknessRepeatGridPanel");
            var numericRoiEditor = (Border)selectedToolView.FindName("GridRectangleNumericEditor");
            var referenceRoiSummary = (TextBlock)selectedToolView.FindName("ReferenceRoiSummaryText");
            var measurementRoiSummary = (TextBlock)selectedToolView.FindName("MeasurementRoiSummaryText");
            var detailContextGroup = (StackPanel)selectedToolView.FindName("SelectedToolDetailContextGroup");
            var detailOutputGroup = (StackPanel)selectedToolView.FindName("SelectedToolDetailOutputGroup");
            Check(
                "v5 Tool settings opens the Region page with source input and authored ROI summaries",
                editorRegionTab.IsChecked == true && editorPages.IsVisible
                && !sectionMenuLabel.IsVisible && !parametersExpander.IsVisible
                && regionsExpander.IsVisible && regionsExpander.IsExpanded
                && !activeRoiSummary.IsVisible && !repeatGridPanel.IsVisible && !numericRoiEditor.IsVisible
                && referenceRoiSummary.Text == shell.Workbench.SelectedToolWorkspace.Regions[0].Detail
                && measurementRoiSummary.Text == shell.Workbench.SelectedToolWorkspace.Regions[1].Detail
                && !detailContextGroup.IsVisible && !detailOutputGroup.IsVisible,
                $"region={editorRegionTab.IsChecked};tabs={editorPages.IsVisible};conditions={parametersExpander.IsVisible};regions={regionsExpander.IsVisible}");
            ActivateSampleChoice(editorAllTab);
            stageHost.UpdateLayout();
            Check(
                "v5 Conditions page shows required parameters without collapsible headings or diagnostics",
                editorAllTab.IsChecked == true && parametersExpander.IsVisible && parametersExpander.IsExpanded
                && !regionsExpander.IsVisible && !detailContextGroup.IsVisible && !detailOutputGroup.IsVisible
                && FindVisualDescendants<ToggleButton>(parametersExpander).All(button => button.Name != "SectionHeader"),
                $"conditions={parametersExpander.IsVisible};regions={regionsExpander.IsVisible};details={detailOutputGroup.IsVisible}");
            ActivateSampleChoice(editorRegionTab);
            stageHost.UpdateLayout();
            Check(
                "v5 page navigation retains ROI, recipe state and the explicit execution contract",
                regionsExpander.IsVisible && !parametersExpander.IsVisible
                && shell.Workbench.IsDirty == recipeDirty
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning,
                $"region={editorRegionTab.IsChecked};dirty={shell.Workbench.IsDirty};step={shell.Workbench.SelectedPipelineStep?.Id}");
            shell.Workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            shell.Workbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Verification candidate");
            stageHost.UpdateLayout();
            var numericEditorVisibleDuringCapture = numericRoiEditor.IsVisible;
            shell.Workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
            stageHost.UpdateLayout();
            Check(
                "ROI numeric controls appear for an editable candidate and disappear after cancel",
                numericEditorVisibleDuringCapture && !numericRoiEditor.IsVisible
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning
                && !shell.Workbench.IsOrderedRunRunning,
                $"editing={numericEditorVisibleDuringCapture}; afterCancel={numericRoiEditor.IsVisible}; dirty={shell.Workbench.IsDirty}");

            var parameterDraftBeforeDetails = ((OpenVisionLab.ThreeD.Shell.PropertyGrid.RecipeStepPropertyGridHost)selectedToolView.FindName("StepPropertyGrid")).SelectedObject;
            ActivateSampleChoice(editorDetailTab);
            ActivateSampleChoice((RadioButton)selectedToolView.FindName("DetailIoTab"));
            selectedToolView.UpdateLayout();
            settingsDetails.BringIntoView();
            stageHost.UpdateLayout();
            Check(
                "Details I/O exposes context and output while retaining the same parameter draft",
                detailContextGroup.IsVisible && detailOutputGroup.IsVisible
                && editorDetailTab.IsChecked == true && settingsDetails.IsExpanded
                && !parametersExpander.IsVisible && !regionsExpander.IsVisible
                && ReferenceEquals(parameterDraftBeforeDetails, ((OpenVisionLab.ThreeD.Shell.PropertyGrid.RecipeStepPropertyGridHost)selectedToolView.FindName("StepPropertyGrid")).SelectedObject)
                && stageWorkbench.HasContextualSelectedStepSetup && stageWorkbench.HasVisibleInspectionContext
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning,
                $"context={detailContextGroup.IsVisible};output={detailOutputGroup.IsVisible};detail={editorDetailTab.IsChecked}/{settingsDetails.IsExpanded};conditions={parametersExpander.IsVisible};region={regionsExpander.IsVisible};contextContract={stageWorkbench.HasVisibleInspectionContext};setup={stageWorkbench.ContextualSelectedStepSetupState}");
            var identityDetails = (Expander)selectedToolView.FindName("InspectionContextDetails");
            var stepIdentity = (TextBlock)selectedToolView.FindName("InspectionContextStepId");
            var selectedToolScrollViewer = (ScrollViewer)selectedToolView.FindName("SelectedToolScrollViewer");
            Check(
                "Selected Tool starts with internal identifiers collapsed",
                !identityDetails.IsExpanded && !stepIdentity.IsVisible,
                stageWorkbench.InspectionContextState);
            identityDetails.IsExpanded = true;
            stageHost.UpdateLayout();
            Check(
                "Expanded details retain the selected step identity without editing or execution",
                stepIdentity.IsVisible
                && stepIdentity.Text == selectedStep?.Id
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.IsDirty == recipeDirty
                && shell.Workbench.SelectedPipelineStep?.State == selectedStepState,
                stageWorkbench.InspectionContextState);
            identityDetails.IsExpanded = false;
            ActivateSampleChoice(editorAllTab);
            stageHost.UpdateLayout();
            var actionBar = (Border)selectedToolView.FindName("SelectedToolActionBar");
            var explicitRunHint = (TextBlock)selectedToolView.FindName("SelectedToolExplicitRunHint");
            var actionButtons = new[] { "PreviewActionButton", "CancelPreviewActionButton" }
                .Select(name => (FrameworkElement)selectedToolView.FindName(name)).ToArray();
            Check("Selected Tool exposes one Run command instead of separate Preview/Publish actions",
                ReferenceEquals(((System.Windows.Controls.Primitives.ButtonBase)actionButtons[0]).Command, shell.Workbench.RunSelectedStepCommand)
                && selectedToolView.FindName("SelectedToolSetupPrimaryActionPreview") is null
                && selectedToolView.FindName("SaveRecipeActionButton") is null
                && selectedToolView.FindName("PublishActionButton") is null,
                "Selected tool has one Run and Cancel surface; recipe Save stays in the shell header.");
            var cancelButton = actionButtons[^1];
            // Exercise the busiest layout without starting an inspection or replacing its binding.
            cancelButton.SetCurrentValue(UIElement.VisibilityProperty, Visibility.Visible);
            var needsRunNotice = !shell.Workbench.IsSourceReadyForRecipe || shell.Workbench.SelectedPipelineStep?.OutputEnabled == false;
            foreach (var width in new[] { 1280d, 1920d })
            {
                SizeLayoutFixture(stageHost, width, stageHost.Height);
                stageWorkbench.ActivateSelectedToolPane();
                stageHost.UpdateLayout();
                var buttonBounds = actionButtons.Select(button => button.TransformToAncestor(actionBar).TransformBounds(new Rect(button.RenderSize))).ToArray();
                Check(
                    $"Selected Tool actions including cancellation stay inside the {width:0}px layout",
                    actionButtons.All(button => button.IsVisible && button.ActualWidth > 0)
                    && actionButtons[0].ActualHeight == 34 && actionButtons[1].ActualHeight == 34
                    && FindVisualDescendants<TextBlock>(actionButtons[0]).Any(text => text.Text == "F5")
                    && (needsRunNotice
                        ? explicitRunHint.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(explicitRunHint.Text)
                        : explicitRunHint.Visibility == Visibility.Collapsed)
                    && buttonBounds.All(bounds => bounds.Left >= 0 && bounds.Right <= actionBar.ActualWidth + 1 && bounds.Top >= 0 && bounds.Bottom <= actionBar.ActualHeight + 1),
                    $"bar={actionBar.ActualWidth:0.0}x{actionBar.ActualHeight:0.0}; notice={needsRunNotice}/{explicitRunHint.Visibility}/{explicitRunHint.Text}; visible={string.Join(',', actionButtons.Select(button => button.IsVisible))}; buttons={string.Join(';', buttonBounds)}");
            }
            System.Windows.Data.BindingOperations.GetBindingExpression(cancelButton, UIElement.VisibilityProperty)?.UpdateTarget();
            selectedToolScrollViewer.ScrollToEnd();
            stageHost.UpdateLayout();
            var pinnedActionBounds = actionBar.TransformToAncestor(selectedToolView).TransformBounds(new Rect(actionBar.RenderSize));
            Check(
                "Selected Tool Run remains visible while long settings are scrolled",
                actionBar.IsVisible
                && pinnedActionBounds.Bottom <= selectedToolView.ActualHeight + 1
                && pinnedActionBounds.Bottom >= selectedToolView.ActualHeight - actionBar.ActualHeight - 20,
                $"action={pinnedActionBounds}; viewHeight={selectedToolView.ActualHeight:0.0}; barHeight={actionBar.ActualHeight:0.0}");
            selectedToolScrollViewer.ScrollToTop();
            SizeLayoutFixture(stageHost, 1600, stageHost.Height);
            stageHost.UpdateLayout();
            Check(
                "Collapsing details and resizing preserve selected inspection state",
                !identityDetails.IsExpanded && !stepIdentity.IsVisible
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && shell.Workbench.SelectedPipelineStep?.State == selectedStepState,
                stageWorkbench.InspectionContextState);
            Check(
                "Initial Workbench mode composes the R19 authoring layout with bottom Results",
                stageWorkbench.OperatorStage == OpenVisionOperatorStage.Teach
                && stageWorkbench.HasTeachStageComposition
                && stageWorkbench.HasAuthoringStageComposition
                && stageWorkbench.HasAdjacentViewerOutputs
                && stageWorkbench.HasStableStageHostedDataContexts,
                $"stage={stageWorkbench.OperatorStage}; authoring={stageWorkbench.HasAuthoringStageComposition}; results={stageWorkbench.DockWorkspace.HasAuthoringResultsPaneComposition}; outputs={stageWorkbench.HasAdjacentViewerOutputs}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; evidence={stageWorkbench.IsBottomPaneAttached}");

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            stageHost.UpdateLayout();
            Check(
                "Teach composes the compact step rail, Selected Tool, Viewer, and bottom Results",
                shell.IsTeachWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Teach
                && stageWorkbench.HasTeachStageComposition
                && stageWorkbench.AuthoringSupportTabCount == 1
                && stageWorkbench.IsToolLibraryDetachedFromAuthoring
                && stageWorkbench.HasStableStageHostedDataContexts,
                $"stage={stageWorkbench.OperatorStage}; teach={stageWorkbench.HasTeachStageComposition}; tabs={stageWorkbench.AuthoringSupportTabCount}; libraryDetached={stageWorkbench.IsToolLibraryDetachedFromAuthoring}; results={stageWorkbench.DockWorkspace.HasAuthoringResultsPaneComposition}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; evidence={stageWorkbench.IsBottomPaneAttached}");
            Check(
                "Authoring entry normalization preserves recipe and selected-step identity",
                ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && string.Equals(selectedStepState, shell.Workbench.SelectedPipelineStep?.State, StringComparison.Ordinal),
                $"selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}");
            var recipeDataView = (RecipeDataView)stageWorkbench.FindName("RecipeDataSurface");
            var recipeDataTabs = (TabControl)recipeDataView.FindName("RecipeDataTabs");
            var recipeDataRegions = (ItemsControl)recipeDataView.FindName("RecipeDataRegions");
            var recipeDataToggle = (ToggleButton)stageWorkbench.FindName("TeachingDataToggle");
            var teachingRunAllButton = FindVisualDescendants<ButtonBase>(stageWorkbench).Single(button =>
                AutomationProperties.GetAutomationId(button) == "TeachingRunAllTools");
            recipeDataTabs.SelectedIndex = 1;
            foreach (var dataWidth in new[] { 1280d, 1920d })
            {
                SizeLayoutFixture(stageHost, dataWidth, stageHost.Height);
                stageHost.UpdateLayout();
                Check(
                    $"v3 Teaching data panel uses real recipe regions and the default layout at {dataWidth:0}px",
                    ReferenceEquals(recipeDataView.DataContext, shell.Workbench)
                    && ReferenceEquals(recipeDataRegions.ItemsSource, shell.Workbench.Selections)
                    && recipeDataRegions.Items.Count == shell.Workbench.Selections.Count
                    && ReferenceEquals(teachingRunAllButton.Command, shell.Workbench.RunTeachingRecipeCommand)
                    && teachingRunAllButton.IsVisible
                    && stageWorkbench.IsTeachingDataExpanded == (dataWidth >= 1500)
                    && recipeDataView.IsVisible == (dataWidth >= 1500)
                    && (!recipeDataView.IsVisible || recipeDataView.ActualWidth is >= 225 and <= 235)
                    && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                    && shell.Workbench.IsDirty == recipeDirty
                    && !shell.Workbench.IsOrderedRunRunning && !shell.Workbench.IsSelectedStepPreviewRunning,
                    $"expanded={stageWorkbench.IsTeachingDataExpanded}; visible={recipeDataView.IsVisible}; width={recipeDataView.ActualWidth:F1}; regions={recipeDataRegions.Items.Count}");
                recipeDataToggle.Focus();
                ActivateSampleChoice(recipeDataToggle);
                stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                stageHost.UpdateLayout();
                var dataPopup = (Popup)stageWorkbench.FindName("TeachingDataPopup");
                var popupDataView = (RecipeDataView)stageWorkbench.FindName("RecipeDataPopupSurface");
                Check(
                    $"v3 Teaching data toggle binds in both directions at {dataWidth:0}px without executing",
                    stageWorkbench.IsTeachingDataExpanded == (dataWidth < 1500)
                    && (dataWidth < 1500
                        ? dataPopup.IsOpen && popupDataView.IsVisible && !recipeDataView.IsVisible
                            && ReferenceEquals(popupDataView.DataContext, shell.Workbench)
                        : !dataPopup.IsOpen && !recipeDataView.IsVisible)
                    && recipeDataToggle.IsChecked == stageWorkbench.IsTeachingDataExpanded
                    && shell.Workbench.IsDirty == recipeDirty
                    && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                    && !shell.Workbench.IsOrderedRunRunning && !shell.Workbench.IsSelectedStepPreviewRunning,
                    $"checked={recipeDataToggle.IsChecked}; expanded={stageWorkbench.IsTeachingDataExpanded}; visible={recipeDataView.IsVisible}");
                recipeDataToggle.SetCurrentValue(ToggleButton.IsCheckedProperty, dataWidth >= 1500);
            }
            recipeDataTabs.SelectedIndex = 0;

            foreach (var catalogWidth in new[] { 1280d, 1920d })
            {
                SizeLayoutFixture(stageHost, catalogWidth, catalogWidth == 1280 ? 760 : 1040);
                stageWorkbench.ActivateToolLibraryPane();
                stageHost.UpdateLayout();
                Check($"R19 Tool Library opens as a modal overlay at {catalogWidth:0}px",
                    stageWorkbench.IsToolLibraryPaneVisible
                    && stageWorkbench.IsToolLibraryDetachedFromAuthoring,
                    $"visible={stageWorkbench.IsToolLibraryPaneVisible}; detached={stageWorkbench.IsToolLibraryDetachedFromAuthoring}; tabs={stageWorkbench.AuthoringSupportTabCount}");
                if (catalogWidth == 1280)
                {
                    var escape = new KeyEventArgs(
                        Keyboard.PrimaryDevice,
                        PresentationSource.FromVisual(stageWorkbench),
                        Environment.TickCount,
                        Key.Escape)
                    {
                        RoutedEvent = Keyboard.PreviewKeyDownEvent,
                    };
                    stageWorkbench.ToolLibraryOverlay.RaiseEvent(escape);
                    stageHost.UpdateLayout();
                    Check("R19 Tool Library Escape closes the overlay without changing recipe state",
                        escape.Handled
                        && !stageWorkbench.IsToolLibraryPaneVisible
                        && shell.Workbench.PipelineSteps.Count == recipeStepCount
                        && shell.Workbench.IsDirty == recipeDirty,
                        $"handled={escape.Handled}; visible={stageWorkbench.IsToolLibraryPaneVisible}; steps={shell.Workbench.PipelineSteps.Count}/{recipeStepCount}; dirty={shell.Workbench.IsDirty}/{recipeDirty}");
                    stageWorkbench.ActivateToolLibraryPane();
                    stageHost.UpdateLayout();
                }
                var library = FindVisualDescendants<ToolLibraryView>(stageWorkbench).First(view => view.IsVisible);
                var suggestions = FindVisualDescendants<ScrollViewer>(library).Single(view =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(view) == "CompatibleToolScroller");
                var catalog = FindVisualDescendants<ListBox>(library).Single();
                var details = FindVisualDescendants<Expander>(library).First(view =>
                    System.Windows.Automation.AutomationProperties.GetAutomationId(view) == "CompatibleToolDetails");
                details.IsExpanded = true;
                stageHost.UpdateLayout();
                var catalogBounds = catalog.TransformToAncestor(library).TransformBounds(new Rect(catalog.RenderSize));
                Check($"Tool details leave the complete catalog reachable at {catalogWidth:0}px", suggestions.ActualHeight <= 250
                    && catalog.ActualHeight >= 100 && catalogBounds.Bottom <= library.ActualHeight + 1
                    && shell.Workbench.IsDirty == recipeDirty && shell.Workbench.PipelineSteps.Count == recipeStepCount,
                    $"suggestions={suggestions.ActualHeight};catalog={catalogBounds};library={library.ActualHeight}");
                details.IsExpanded = false;
                stageWorkbench.CloseToolLibraryPane();
            }
            SizeLayoutFixture(stageHost, 1600, 900);
            stageWorkbench.ActivateSelectedToolPane();
            stageHost.UpdateLayout();

            Check(
                "ROI capture can start for the selected Thickness step",
                shell.Workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null),
                shell.Workbench.TeachingSelectionCaptureInstruction);
            shell.Workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            Check(
                "active ROI Review blocks stage navigation",
                shell.Workbench.IsSelectionCandidateActive
                && !shell.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Inspect)
                && !shell.SelectWorkspaceCommand.CanExecute(ShellWorkspaceMode.Validate),
                shell.InspectionStageNavigationStatus);
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            Check(
                "blocked stage execution leaves Teach active",
                shell.IsTeachWorkspaceSelected,
                $"stage={shell.SelectedWorkspaceMode}; status={shell.StatusText}");
            shell.Workbench.CancelTeachingSelectionCaptureCommand.Execute(null);

            var validationSourceBefore = shell.Workbench.Source.Path;
            var validationDirtyBefore = shell.Workbench.IsDirty;
            var validationSelectionBefore = shell.Workbench.SelectedValidationSetSample?.SourcePath;
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
            stageHost.UpdateLayout();
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            Check(
                "v3 Validation navigation completes after cancelling ROI capture",
                shell.IsValidateWorkspaceSelected && stageWorkbench.HasRecipeValidationStageComposition,
                $"mode={shell.SelectedWorkspaceMode}; layout={stageWorkbench.HasRecipeValidationStageComposition}; pending={shell.Workbench.HasPendingStepParameterChanges}; capture={shell.Workbench.IsSelectionCandidateActive}; status={shell.InspectionStageNavigationStatus}");
            var validationSamplesView = FindVisualDescendants<ValidationSamplesView>(stageWorkbench).Single();
            var validationSamplesList = FindVisualDescendants<ListBox>(validationSamplesView).Single();
            var validationResultView = FindVisualDescendants<RecipePipelineReviewView>(stageWorkbench).Single();
            var validationRunAction = FindVisualDescendants<ButtonBase>(stageWorkbench).Single(button =>
                AutomationProperties.GetAutomationId(button) == "RunValidationWorkspaceSet");
            var validationExecutionBar = FindVisualDescendants<Border>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationExecutionBar");
            var validationCancelAction = FindVisualDescendants<ButtonBase>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "CancelValidationWorkspaceSet");
            var validationRecipeName = FindVisualDescendants<TextBlock>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationRecipeName");
            Check("v3 central validation action keeps the existing command owners and removes the sample footer duplicate",
                validationExecutionBar.IsVisible && validationRunAction.IsVisible && validationRunAction.IsEnabled
                && validationRunAction.Focusable && validationRunAction.IsTabStop && !validationCancelAction.IsVisible
                && ReferenceEquals(validationRunAction.Command, shell.Workbench.RunValidationSetCommand)
                && ReferenceEquals(validationCancelAction.Command, shell.Workbench.CancelValidationSetCommand)
                && validationRecipeName.Text == shell.Workbench.HeaderRecipeText
                && !FindVisualDescendants<ButtonBase>(validationSamplesView).Any(button => AutomationProperties.GetAutomationId(button) is "RunValidationWorkspaceSet" or "CancelValidationWorkspaceSet")
                && !shell.Workbench.IsValidationSetRunning,
                $"bar={validationExecutionBar.IsVisible};run={validationRunAction.IsEnabled};recipe={validationRecipeName.Text}");
            var validationMatrix = (DataGrid)validationResultView.FindName("ValidationSamplesGrid");
            var validationTabs = new[] { "ValidationResultsNavigation", "ValidationFailuresNavigation", "ValidationThresholdNavigation" }
                .Select(name => (RadioButton)validationResultView.FindName(name)).ToArray();
            var validationDock = stageWorkbench.DockWorkspace;
            var samplesAnchorable = (AvalonDock.Layout.LayoutAnchorable)validationDock.FindName("dataLayersAnchorable");
            var sampleMore = (MenuItem)validationSamplesView.FindName("SampleAdditionalMenu");
            var sampleAutoHide = (MenuItem)validationSamplesView.FindName("SampleAutoHideAction");
            var sampleFloat = (MenuItem)validationSamplesView.FindName("SampleFloatAction");
            var sampleFilterMismatch = FindVisualDescendants<RadioButton>(validationSamplesView).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSamplesFilterMismatch");
            var sampleResetFilter = FindVisualDescendants<Button>(validationSamplesView).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSamplesResetFilter");
            void ActivateSampleChoice(ButtonBase button)
            {
                // Exercise the rendered control's command path; this scripted invocation is not pointer or keyboard evidence.
                typeof(ButtonBase).GetMethod("OnClick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(button, null);
            }
            ActivateSampleChoice(sampleFilterMismatch);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            Check("v3 sample mismatch filter shows an accessible empty state without changing expected roles or executing",
                shell.Workbench.IsValidationSetFilterMismatch && validationSamplesList.Items.Count == 0
                && ((StackPanel)validationSamplesView.FindName("SamplesEmptyState")).IsVisible
                && sampleResetFilter.IsVisible && shell.Workbench.ValidationSetAllCount == 5 && !shell.Workbench.IsValidationSetRunning,
                $"mismatch={shell.Workbench.IsValidationSetFilterMismatch};visible={validationSamplesList.Items.Count};all={shell.Workbench.ValidationSetAllCount}");
            ActivateSampleChoice(sampleResetFilter);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            Check("v3 empty filter reset restores the list and selected sample binding without execution",
                shell.Workbench.IsValidationSetFilterAll && validationSamplesList.Items.Count == 5
                && ReferenceEquals(validationSamplesList.SelectedItem, shell.Workbench.SelectedValidationSetSample)
                && !((StackPanel)validationSamplesView.FindName("SamplesEmptyState")).IsVisible && !shell.Workbench.IsValidationSetRunning,
                $"all={shell.Workbench.IsValidationSetFilterAll};visible={validationSamplesList.Items.Count};selected={shell.Workbench.SelectedValidationSetSample?.FileName}");
            var rowSelectionBefore = shell.Workbench.SelectedValidationSetSample?.SourcePath;
            var roleTarget = (ValidationSetSampleRow)validationSamplesList.Items[1];
            var roleTargetContainer = (ListBoxItem)validationSamplesList.ItemContainerGenerator.ContainerFromItem(roleTarget);
            var roleTargetBad = FindVisualDescendants<RadioButton>(roleTargetContainer).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedBad");
            ActivateSampleChoice(roleTargetBad);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            var changedRoleTarget = shell.Workbench.ValidationSetSamples.Single(sample => sample.SourcePath == roleTarget.SourcePath);
            var changedRoleContainer = (ListBoxItem)validationSamplesList.ItemContainerGenerator.ContainerFromItem(changedRoleTarget);
            Check("v3 rendered role action updates its own non-selected row and its check mark without executing",
                changedRoleTarget.Role == ToolRecipeValidationSampleRole.Bad && changedRoleTarget.Status == "Pending"
                && shell.Workbench.SelectedValidationSetSample?.SourcePath == rowSelectionBefore
                && FindVisualDescendants<RadioButton>(changedRoleContainer).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedBad").IsChecked == true
                && !shell.Workbench.IsValidationSetRunning && shell.Workbench.Source.Path == validationSourceBefore,
                $"role={changedRoleTarget.Role};selected={shell.Workbench.SelectedValidationSetSample?.SourcePath};prior={rowSelectionBefore}");
            var roleTargetGood = FindVisualDescendants<RadioButton>(changedRoleContainer).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedGood");
            ActivateSampleChoice(roleTargetGood);
            validationSamplesList.SetCurrentValue(Selector.SelectedIndexProperty, 1);
            stageHost.UpdateLayout();
            Check("v3 sample selection writes through the rendered list and restores from its ViewModel",
                shell.Workbench.SelectedValidationSetSample?.SourcePath == roleTarget.SourcePath && shell.Workbench.SelectedValidationSetSample.IsGood
                && ReferenceEquals(validationSamplesList.SelectedItem, shell.Workbench.SelectedValidationSetSample) && !shell.Workbench.IsValidationSetRunning,
                $"selected={shell.Workbench.SelectedValidationSetSample?.FileName};role={shell.Workbench.SelectedValidationSetSample?.Role}");
            shell.Workbench.SelectedValidationSetSample = (ValidationSetSampleRow)validationSamplesList.Items[0];
            stageHost.UpdateLayout();
            Check("v3 restored model selection renders back into the sample list",
                validationSamplesList.SelectedIndex == 0 && ReferenceEquals(validationSamplesList.SelectedItem, shell.Workbench.SelectedValidationSetSample),
                $"index={validationSamplesList.SelectedIndex}");
            Check("v3 sample header reuses the current native pane commands",
                ReferenceEquals(validationSamplesView.DockPaneCommands, validationDock.DataLayersPaneLayoutItem)
                && ReferenceEquals(sampleAutoHide.Command, validationDock.DataLayersPaneLayoutItem?.AutoHideCommand)
                && ReferenceEquals(sampleFloat.Command, validationDock.DataLayersPaneLayoutItem?.FloatCommand)
                && sampleAutoHide.IsEnabled && sampleFloat.IsEnabled,
                $"item={validationSamplesView.DockPaneCommands is not null};hide={sampleAutoHide.IsEnabled};float={sampleFloat.IsEnabled}");
            var validationEvidence = (AvalonDock.Layout.LayoutAnchorable)validationDock.FindName("evidenceAnchorable");
            var validationMenu = (Menu)validationResultView.FindName("ValidationAdvancedNavigation");
            var validationMore = (MenuItem)validationMenu.Items[0];
            var validationAutoHide = (MenuItem)validationResultView.FindName("ValidationAutoHidePaneAction");
            var validationFloat = (MenuItem)validationResultView.FindName("ValidationFloatPaneAction");
            Check("v3 Validation header binds the existing native docking commands and their enabled state",
                validationDock.EvidencePaneLayoutItem is { } validationItem && ReferenceEquals(validationResultView.DockPaneCommands, validationItem)
                && ReferenceEquals(validationAutoHide.Command, validationItem.AutoHideCommand) && ReferenceEquals(validationFloat.Command, validationItem.FloatCommand)
                && validationAutoHide.Command.CanExecute(null) && validationAutoHide.IsEnabled && validationFloat.IsEnabled,
                $"item={validationDock.EvidencePaneLayoutItem is not null};reviewItem={validationResultView.DockPaneCommands is not null};sameHide={ReferenceEquals(validationAutoHide.Command, validationDock.EvidencePaneLayoutItem?.AutoHideCommand)};model={ReferenceEquals(validationDock.EvidencePaneLayoutItem?.LayoutElement, validationEvidence)};allow={validationEvidence.CanAutoHide};canHide={validationDock.EvidencePaneLayoutItem?.AutoHideCommand.CanExecute(null)};hide={validationAutoHide.IsEnabled};float={validationFloat.IsEnabled}");
            Check("v3 initial Validation result selection and its v5 34px navigation render together",
                validationTabs[0].IsChecked == true && validationTabs.Skip(1).All(tab => tab.IsChecked == false)
                && ((Border)validationResultView.FindName("ValidationWorkspaceNavigation")).ActualHeight == 34
                && ((Menu)validationResultView.FindName("ValidationAdvancedNavigation")).ActualHeight == 34,
                $"checked={string.Join(',', validationTabs.Select(tab => tab.IsChecked))};header={((Border)validationResultView.FindName("ValidationWorkspaceNavigation")).ActualHeight}");
            var validationSurface = (Grid)((Border)validationResultView.FindName("ReviewSurface")).Child;
            var siblingReview = new RecipePipelineReviewView { DataContext = shell.Workbench, IsValidationSamplesExternal = true };
            validationSurface.Children.Add(siblingReview);
            siblingReview.SetPresentationMode(RecipeReviewPresentationMode.Validation);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            siblingReview.Visibility = Visibility.Collapsed;
            var siblingResults = (RadioButton)siblingReview.FindName("ValidationResultsNavigation");
            Check("v3 hidden Review instances preserve each View's selected result tab",
                validationTabs[0].IsChecked == true && siblingResults.IsChecked == true
                && string.IsNullOrEmpty(validationTabs[0].GroupName) && string.IsNullOrEmpty(siblingResults.GroupName)
                && validationResultView.ValidationSection == ValidationWorkspaceSection.Results && !shell.Workbench.IsValidationSetRunning,
                $"first={validationTabs[0].IsChecked};sibling={siblingResults.IsChecked};section={validationResultView.ValidationSection}");
            validationTabs[0].GroupName = siblingResults.GroupName = "ReconstructedLegacyValidationGroup";
            siblingResults.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
            siblingResults.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            Check("reconstructed legacy shared radio group loses the visible mark while its section remains selected",
                validationTabs[0].IsChecked == false && siblingResults.IsChecked == true && validationResultView.ValidationSection == ValidationWorkspaceSection.Results,
                $"first={validationTabs[0].IsChecked};sibling={siblingResults.IsChecked};section={validationResultView.ValidationSection}");
            validationTabs[0].GroupName = siblingResults.GroupName = string.Empty;
            validationTabs[0].GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            siblingResults.GetBindingExpression(ToggleButton.IsCheckedProperty)?.UpdateTarget();
            validationSurface.Children.Remove(siblingReview);
            siblingReview.DataContext = null;
            Check("v3 Validation drawer exposes three result tabs and the actual sample matrix",
                validationTabs.All(tab => tab.IsVisible) && validationMatrix.IsVisible
                && ReferenceEquals(validationMatrix.ItemsSource, shell.Workbench.ValidationSetSamples)
                && ((RadioButton)validationResultView.FindName("ValidationSamplesNavigation")).Visibility == Visibility.Collapsed
                && ((RadioButton)validationResultView.FindName("ValidationHeldOutNavigation")).Visibility == Visibility.Collapsed
                && ((Menu)validationResultView.FindName("ValidationAdvancedNavigation")).IsVisible
                && validationMatrix.Columns.Count == 5 + shell.Workbench.ValidationTableColumns.Count
                && !shell.Workbench.IsValidationSetRunning && shell.Workbench.IsDirty == validationDirtyBefore,
                $"tabs={string.Join(",", validationTabs.Select(tab => tab.Content))};matrix={validationMatrix.Columns.Count};metrics={shell.Workbench.ValidationMatrixColumns.Count}");
            foreach (var validationWidth in new[] { 1280d, 1920d })
            {
                SizeLayoutFixture(stageHost, validationWidth, validationWidth == 1280 ? 760 : 1040);
                stageHost.UpdateLayout();
                lines.Add($"FixtureSettleBefore|mode={shell.SelectedWorkspaceMode};samplesLoaded={validationSamplesView.IsLoaded};samplesVisible={validationSamplesView.IsVisible};sameContent={ReferenceEquals(validationDock.ValidationSamplesContent, validationSamplesView)};items={validationSamplesList.Items.Count};rows={FindVisualDescendants<ListBoxItem>(validationSamplesList).Count(item => item.IsVisible)}");
                stageHost.Dispatcher.Invoke(new Action(stageHost.UpdateLayout), System.Windows.Threading.DispatcherPriority.ContextIdle);
                lines.Add($"FixtureSettleAfter|mode={shell.SelectedWorkspaceMode};samplesLoaded={validationSamplesView.IsLoaded};samplesVisible={validationSamplesView.IsVisible};sameContent={ReferenceEquals(validationDock.ValidationSamplesContent, validationSamplesView)};items={validationSamplesList.Items.Count};rows={FindVisualDescendants<ListBoxItem>(validationSamplesList).Count(item => item.IsVisible)}");
                var samplePane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(validationDock).Single(pane => ReferenceEquals(pane.Model, samplesAnchorable.Parent));
                samplePane.ApplyTemplate();
                var sampleOuterHeader = (Border)samplePane.Template.FindName("PaneTabHeader", samplePane);
                var sampleControl = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorableControl>(validationDock).Single(control => ReferenceEquals(control.Model, samplesAnchorable));
                sampleControl.ApplyTemplate();
                var sampleTitle = (Border)sampleControl.Template.FindName("Header", sampleControl);
                var sampleRows = FindVisualDescendants<ListBoxItem>(validationSamplesList).Where(item => item.IsVisible).ToArray();
                var sampleRoleButtons = sampleRows.SelectMany(item => FindVisualDescendants<RadioButton>(item)).ToArray();
                var sampleGuide = (TextBlock)validationSamplesView.FindName("SamplesMeaningGuide");
                var sampleFooterBounds = sampleGuide.TransformToAncestor(validationSamplesView).TransformBounds(new Rect(sampleGuide.RenderSize));
                Check($"v3 samples have one v5 34px header, 24px filters and accessible segmented row roles at {validationWidth:0}px",
                    sampleOuterHeader is { ActualHeight: 0 } && sampleTitle is { Visibility: Visibility.Collapsed } && ((Border)validationSamplesView.FindName("SamplesHeader")).ActualHeight == 34
                    && sampleFilterMismatch.ActualHeight == 24 && sampleRoleButtons.Length == 15 && sampleRoleButtons.All(button => button.ActualHeight == (AutomationProperties.GetAutomationId(button) == "ValidationSampleExpectedHeldOut" ? 22 : 20))
                    && validationSamplesList.ActualHeight >= 100 && sampleFooterBounds.Bottom <= validationSamplesView.ActualHeight
                    && sampleMore.ActualWidth == 24 && sampleMore.ActualHeight == 24,
                    $"outer={sampleOuterHeader?.ActualHeight};title={sampleTitle?.Visibility};header={((Border)validationSamplesView.FindName("SamplesHeader")).ActualHeight};roles={sampleRoleButtons.Length};list={validationSamplesList.ActualHeight};footer={sampleFooterBounds}");
                var executionBarBounds = validationExecutionBar.TransformToAncestor((Grid)validationDock.ViewerContent!).TransformBounds(new Rect(validationExecutionBar.RenderSize));
                var centralActionBounds = validationRunAction.TransformToAncestor(validationExecutionBar).TransformBounds(new Rect(validationRunAction.RenderSize));
                Check($"v3 Validation has a 44px central execution bar and reachable v5 30px action at {validationWidth:0}px",
                    validationExecutionBar.ActualHeight == 44 && Math.Abs(executionBarBounds.Top) < 0.1 && validationRunAction.ActualHeight == 30
                    && centralActionBounds.Right <= validationExecutionBar.ActualWidth && centralActionBounds.Bottom <= 44
                    && (FindVisualDescendants<Border>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationTeachingIdentity")).ActualHeight <= 44
                    && (FindVisualDescendants<TextBlock>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationTeachingSaveState")).IsVisible
                    && (FindVisualDescendants<TextBlock>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationIdleSummary")).IsVisible,
                    $"bar={executionBarBounds};action={centralActionBounds};identity={validationRecipeName.Text}");
                var validationPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(validationDock)
                    .Single(pane => ReferenceEquals(pane.Model, validationEvidence.Parent));
                var validationOuterHeader = (Border)validationPane.Template.FindName("PaneTabHeader", validationPane);
                Check($"v3 Validation renders one v5 34px result header at {validationWidth:0}px",
                    validationOuterHeader.ActualHeight == 0 && validationTabs.All(tab => tab.IsVisible)
                    && ((Border)validationResultView.FindName("ValidationWorkspaceNavigation")).ActualHeight == 34
                    && validationMore.ActualWidth == 32 && validationMore.ActualHeight == 28
                    && validationMore.TransformToAncestor(validationResultView).Transform(new Point()).X + validationMore.ActualWidth <= validationResultView.ActualWidth + 0.1,
                    $"outer={validationOuterHeader.ActualHeight};inner={((Border)validationResultView.FindName("ValidationWorkspaceNavigation")).ActualHeight};menu={validationMore.ActualWidth},{validationMore.ActualHeight};pane={validationPane.ActualWidth:F0},{validationPane.ActualHeight:F0}");
                if (validationWidth == 1280)
                {
                    validationOuterHeader.Height = validationOuterHeader.MinHeight = 34;
                    validationOuterHeader.Padding = new Thickness(6, 4, 6, 0);
                    validationOuterHeader.BorderThickness = new Thickness(0, 0, 0, 1);
                    stageHost.UpdateLayout();
                    WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(stageWorkbench).Bitmap,
                        Path.ChangeExtension(reportPath, ".d17-reconstructed-duplicate-header.png"));
                    validationOuterHeader.ClearValue(FrameworkElement.HeightProperty);
                    validationOuterHeader.ClearValue(FrameworkElement.MinHeightProperty);
                    validationOuterHeader.ClearValue(Border.PaddingProperty);
                    validationOuterHeader.ClearValue(Border.BorderThicknessProperty);
                    stageHost.UpdateLayout();
                }
                var validationCapture = WpfScreenshotCapture.Capture(stageWorkbench);
                Check($"v3 Validation contextual capture contains rendered UI at {validationWidth:0}px",
                    validationCapture.Quality.IsAcceptable, validationCapture.Quality.Summary);
                WpfScreenshotCapture.Save(validationCapture.Bitmap,
                    Path.ChangeExtension(reportPath, $".d17-validation-{validationWidth:0}.png"));
                WpfScreenshotCapture.Save(validationCapture.Bitmap,
                    Path.ChangeExtension(reportPath, $".d18-validation-{validationWidth:0}.png"));
                WpfScreenshotCapture.Save(validationCapture.Bitmap,
                    Path.ChangeExtension(reportPath, $".d19-validation-{validationWidth:0}.png"));
                Check(
                    $"v3 Validation hosts real samples left, Viewer above and result below at {validationWidth:0}px",
                    shell.IsValidateWorkspaceSelected && !shell.IsInspectWorkspaceSelected
                    && shell.IsWorkbenchWorkspaceSelected
                    && shell.Workbench.IsValidationSampleView && shell.Workbench.IsReadOnlySampleView
                    && stageWorkbench.HasRecipeValidationStageComposition
                    && ReferenceEquals(validationSamplesView.DataContext, shell.Workbench)
                    && validationSamplesList.Items.Count == shell.Workbench.ValidationSetSamples.Count
                    && validationSamplesView.ActualWidth is >= 260 and <= 330
                    && validationResultView.IsValidationSamplesExternal
                    && validationResultView.PresentationMode == RecipeReviewPresentationMode.Validation
                    && validationResultView.ValidationSection == ValidationWorkspaceSection.Results
                    && validationRunAction.IsVisible
                    && ReferenceEquals(validationRunAction.Command, shell.Workbench.RunValidationSetCommand)
                    && !shell.Workbench.IsValidationSetRunning && !shell.Workbench.IsOrderedRunRunning,
                    $"mode={shell.SelectedWorkspaceMode}; layout={stageWorkbench.HasRecipeValidationStageComposition}; sampleWidth={validationSamplesView.ActualWidth:F1}; samples={validationSamplesList.Items.Count}; external={validationResultView.IsValidationSamplesExternal}; section={validationResultView.ValidationSection}; running={shell.Workbench.IsValidationSetRunning}");
            }
            sampleMore.IsSubmenuOpen = true;
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            var samplePopup = FindVisualDescendants<Popup>(sampleMore).FirstOrDefault(popup => popup.IsOpen);
            Check("v3 samples additional menu preserves actual staging, clear, comparison and native pane commands",
                samplePopup?.Child is { IsVisible: true } && sampleMore.Items.OfType<MenuItem>().Count(item => item.Command is not null) == 5
                && sampleAutoHide.IsVisible && sampleFloat.IsVisible && !shell.Workbench.IsValidationSetRunning,
                $"popup={samplePopup?.IsOpen};actions={sampleMore.Items.OfType<MenuItem>().Count(item => item.Command is not null)}");
            if (samplePopup?.Child is FrameworkElement samplePopupChild)
            {
                var samplePopupDrawing = new DrawingVisual();
                using (var drawing = samplePopupDrawing.RenderOpen()) drawing.DrawRectangle(new VisualBrush(samplePopupChild), null, new Rect(samplePopupChild.RenderSize));
                var samplePopupBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(samplePopupChild.ActualWidth), (int)Math.Ceiling(samplePopupChild.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                samplePopupBitmap.Render(samplePopupDrawing);
                WpfScreenshotCapture.Save(samplePopupBitmap, Path.ChangeExtension(reportPath, ".d18-sample-menu.png"));
            }
            ((IInvokeProvider)new MenuItemAutomationPeer(sampleAutoHide).GetPattern(PatternInterface.Invoke)).Invoke();
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            var sampleSideTab = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorControl>(validationDock).SingleOrDefault(tab => ReferenceEquals(tab.Model, samplesAnchorable));
            Check("v3 sample header auto hide keeps an accessible native side tab", samplesAnchorable.IsAutoHidden && sampleSideTab is { IsVisible: true }, $"hidden={samplesAnchorable.IsAutoHidden};tab={sampleSideTab?.IsVisible}");
            validationDock.DataLayersPaneLayoutItem!.AutoHideCommand.Execute(null);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            Check("v3 native pin restores the sample list, selection and role command binding without executing",
                !samplesAnchorable.IsAutoHidden && validationSamplesList.IsVisible && validationSamplesList.Items.Count == 5
                && ReferenceEquals(validationSamplesList.SelectedItem, shell.Workbench.SelectedValidationSetSample)
                && ReferenceEquals(validationSamplesView.DockPaneCommands, validationDock.DataLayersPaneLayoutItem) && !shell.Workbench.IsValidationSetRunning,
                $"hidden={samplesAnchorable.IsAutoHidden};list={validationSamplesList.IsVisible};selected={shell.Workbench.SelectedValidationSetSample?.FileName}");
            validationMore.IsSubmenuOpen = true;
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            var validationPopup = FindVisualDescendants<Popup>(validationMore).FirstOrDefault(popup => popup.IsOpen);
            Check("v3 Validation additional menu preserves HeldOut and both native pane actions",
                validationPopup is { IsOpen: true, Child: FrameworkElement { IsVisible: true } }
                && validationAutoHide.IsVisible && validationFloat.IsVisible
                && validationMore.Items[0] is MenuItem heldOutItem && ReferenceEquals(heldOutItem.Command, shell.Workbench.ValidationWorkspace.SelectSectionCommand)
                && Equals(heldOutItem.CommandParameter, "HeldOut"),
                $"popup={validationPopup?.IsOpen};hide={validationAutoHide.IsVisible};float={validationFloat.IsVisible};section={validationResultView.ValidationSection}");
            if (validationPopup?.Child is FrameworkElement popupContent)
            {
                var popupDrawing = new DrawingVisual();
                using (var drawing = popupDrawing.RenderOpen())
                {
                    drawing.DrawRectangle(new VisualBrush(popupContent), null, new Rect(0, 0, popupContent.ActualWidth, popupContent.ActualHeight));
                }
                var popupBitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(popupContent.ActualWidth),
                    (int)Math.Ceiling(popupContent.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                popupBitmap.Render(popupDrawing);
                WpfScreenshotCapture.Save(popupBitmap,
                    Path.ChangeExtension(reportPath, ".d17-additional-menu.png"));
            }
            var validationHidePeer = new MenuItemAutomationPeer(validationAutoHide);
            ((IInvokeProvider)validationHidePeer.GetPattern(PatternInterface.Invoke)).Invoke();
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            var validationSideTab = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorControl>(validationDock)
                .SingleOrDefault(tab => ReferenceEquals(tab.Model, validationEvidence));
            Check("v3 Validation actual auto-hide menu invokes the native side-tab transition without execution",
                validationEvidence.IsAutoHidden && validationSideTab is { IsVisible: true }
                && validationResultView.ValidationSection == ValidationWorkspaceSection.Results && shell.Workbench.IsDirty == validationDirtyBefore
                && !shell.Workbench.IsValidationSetRunning && !shell.Workbench.IsOrderedRunRunning,
                $"autoHidden={validationEvidence.IsAutoHidden};sideTab={validationSideTab?.IsVisible};section={validationResultView.ValidationSection};dirty={shell.Workbench.IsDirty}");
            validationDock.EvidencePaneLayoutItem?.AutoHideCommand.Execute(null);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            Check("v3 Validation uses the existing native pin command to restore its result panel and selection",
                !validationEvidence.IsAutoHidden && validationResultView.IsVisible && validationTabs[0].IsChecked == true
                && validationMatrix.IsVisible && stageWorkbench.HasRecipeValidationStageComposition
                && ReferenceEquals(validationAutoHide.Command, validationDock.EvidencePaneLayoutItem?.AutoHideCommand)
                && shell.Workbench.Source.Path == validationSourceBefore && shell.Workbench.IsDirty == validationDirtyBefore
                && !shell.Workbench.IsValidationSetRunning && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"autoHidden={validationEvidence.IsAutoHidden};result={validationResultView.IsVisible};selected={validationTabs[0].IsChecked};layout={stageWorkbench.HasRecipeValidationStageComposition}");
            validationMore.IsSubmenuOpen = false;
            var validationRow = shell.Workbench.ValidationSetSamples[1];
            validationTabs[1].SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            Check("v3 mismatch tab uses the actual Checked command and card projection",
                validationResultView.ValidationSection == ValidationWorkspaceSection.Failures && validationTabs[1].IsChecked == true
                && validationTabs[1].ActualHeight == 34 && ((Grid)validationResultView.FindName("ValidationMismatchCardsHost")).IsVisible
                && ReferenceEquals(((ListBox)validationResultView.FindName("ValidationMismatchCards")).SelectedItem, shell.Workbench.SelectedValidationSetSample)
                && !((Border)validationResultView.FindName("ValidationFailureOperatorSummary")).IsVisible && !shell.Workbench.IsValidationSetRunning,
                $"section={validationResultView.ValidationSection};checked={validationTabs[1].IsChecked};height={validationTabs[1].ActualHeight}");
            validationTabs[2].SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            Check("v3 distribution tab hosts the real output list and two-way sample chart",
                validationResultView.ValidationSection == ValidationWorkspaceSection.Thresholds && validationTabs[2].IsChecked == true
                && ((Grid)validationResultView.FindName("ValidationDistributionWorkspace")).IsVisible
                && ((ListBox)validationResultView.FindName("ValidationDistributionOutputs")).ActualWidth == 206
                && ReferenceEquals(((ValidationDistributionPlot)validationResultView.FindName("ValidationDistributionChart")).SelectedSample, shell.Workbench.SelectedValidationSetSample)
                && !shell.Workbench.IsValidationSetRunning && shell.Workbench.IsDirty == validationDirtyBefore,
                $"section={validationResultView.ValidationSection};checked={validationTabs[2].IsChecked}");
            validationTabs[0].SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            Check("v3 results tab restores the matrix and its selected underline", validationResultView.ValidationSection == ValidationWorkspaceSection.Results
                && validationTabs[0].IsChecked == true && validationTabs.Skip(1).All(tab => tab.IsChecked == false) && validationMatrix.IsVisible,
                $"section={validationResultView.ValidationSection};checked={string.Join(",", validationTabs.Select(tab => tab.IsChecked))}");
            validationMatrix.SelectedItem = validationRow;
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            Check("v3 Validation matrix selects the same sample in the left list without executing",
                ReferenceEquals(shell.Workbench.SelectedValidationSetSample, validationRow) && ReferenceEquals(validationSamplesList.SelectedItem, validationRow)
                && !shell.Workbench.IsValidationSetRunning && !shell.Workbench.IsOrderedRunRunning && shell.Workbench.IsDirty == validationDirtyBefore,
                $"matrix={(validationMatrix.SelectedItem is ValidationSetSampleRow matrixRow ? matrixRow.FileName : "none")};left={shell.Workbench.SelectedValidationSetSample?.FileName}");
            validationSamplesList.SelectedItem = validationRow;
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            var validationSelectionFromView = shell.Workbench.SelectedValidationSetSample?.SourcePath == validationRow.SourcePath;
            shell.Workbench.SelectedValidationSetSample = shell.Workbench.ValidationSetSamples[0];
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            Check(
                "v3 sample selection binds in both directions without recipe or execution changes",
                validationSelectionFromView
                && ReferenceEquals(validationSamplesList.SelectedItem, shell.Workbench.SelectedValidationSetSample)
                && shell.Workbench.Source.Path == validationSourceBefore
                && shell.Workbench.IsDirty == validationDirtyBefore
                && !shell.Workbench.IsValidationSetRunning && !shell.Workbench.IsOrderedRunRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"viewToModel={validationSelectionFromView}; selected={shell.Workbench.SelectedValidationSetSample?.FileName}; source={shell.Workbench.Source.Path}; dirty={shell.Workbench.IsDirty}");
            shell.Workbench.SelectedValidationSetSample = shell.Workbench.ValidationSetSamples.FirstOrDefault(sample => sample.SourcePath == validationSelectionBefore);
            stageWorkbench.ActivateSessionLogPane();
            stageHost.UpdateLayout();
            validationDock.ActivateEvidencePane();
            stageHost.UpdateLayout();
            var validationSupportPane = FindVisualDescendants<AvalonDock.Controls.LayoutAnchorablePaneControl>(validationDock)
                .Single(pane => ReferenceEquals(pane.Model, validationEvidence.Parent));
            Check("v3 Validation restores the outer dock tabs when an advanced support pane is explicitly requested",
                validationDock.VisibleValidationPaneTabCount == 2 && validationDock.IsEvidencePaneSelected
                && ((Border)validationSupportPane.Template.FindName("PaneTabHeader", validationSupportPane)).ActualHeight >= 34
                && validationResultView.IsVisible && shell.Workbench.IsDirty == validationDirtyBefore && !shell.Workbench.IsValidationSetRunning,
                $"tabs={validationDock.VisibleValidationPaneTabCount};outer={((Border)validationSupportPane.Template.FindName("PaneTabHeader", validationSupportPane)).ActualHeight};evidence={validationDock.IsEvidencePaneSelected}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Teach);
            stageHost.UpdateLayout();
            Check(
                "v3 Validation returns to the canonical Teaching host and command context",
                stageWorkbench.HasAuthoringStageComposition
                && !shell.Workbench.IsReadOnlySampleView
                && ReferenceEquals(shell.Workbench.RunCurrentWorkspaceCommand, shell.Workbench.RunSelectedStepCommand)
                && shell.Workbench.Source.Path == validationSourceBefore
                && shell.Workbench.IsDirty == validationDirtyBefore,
                $"mode={shell.SelectedWorkspaceMode}; authoring={stageWorkbench.HasAuthoringStageComposition}; readonly={shell.Workbench.IsReadOnlySampleView}; dirty={shell.Workbench.IsDirty}");

            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            stageHost.UpdateLayout();
            var stageReview = FindVisualDescendants<RecipePipelineReviewView>(stageWorkbench)
                .SingleOrDefault();
            var inspectionQueueView = FindVisualDescendants<InspectionQueueWorkspaceView>(stageWorkbench)
                .SingleOrDefault(view => view.IsVisible);
            var inspectionQueue = inspectionQueueView?.DataContext as InspectionQueueWorkspaceViewModel;
            var inspectionRunButton = FindVisualDescendants<ButtonBase>(stageWorkbench)
                .SingleOrDefault(button => string.Equals(
                    AutomationProperties.GetAutomationId(button),
                    "StartInspectionQueueRun",
                    StringComparison.Ordinal));
            var inspectionAdditionalMenu = FindVisualDescendants<MenuItem>(inspectionQueueView!)
                .SingleOrDefault(item => AutomationProperties.GetAutomationId(item) == "InspectionAdditionalMenu");
            var inspectionResultsAction = inspectionAdditionalMenu?.Items.OfType<MenuItem>()
                .SingleOrDefault(item => AutomationProperties.GetAutomationId(item) == "OpenInspectionResultsButton");
            Check(
                "Inspection opens the checked-input queue without running until explicit start",
                shell.IsInspectWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Validate
                && stageWorkbench.HasValidateStageComposition
                && stageWorkbench.HasEvidenceLinkedViewerComposition
                && stageReview?.IsInspectionQueueSelected == true
                && inspectionQueue is { } queue
                && queue.Samples.Count == 0
                && !queue.IsRunning
                && !queue.RunCheckedCommand.CanExecute(null)
                && inspectionRunButton is { } startButton
                && ReferenceEquals(startButton.Command, queue.RunScopeCommand)
                && startButton is Wpf.Ui.Controls.Button startAction
                && startAction.Icon is not null
                && startAction.CornerRadius.TopLeft == 4
                && startAction.ActualHeight == 32
                && inspectionAdditionalMenu is { IsVisible: true }
                && inspectionResultsAction is { } resultsAction
                && ReferenceEquals(resultsAction.Command, shell.SelectWorkspaceCommand)
                && Equals(resultsAction.CommandParameter, ShellWorkspaceMode.Review)
                && ReferenceEquals(inspectionQueueView!.ResultsCommand, shell.SelectWorkspaceCommand)
                && stageWorkbench.HasStableStageHostedDataContexts
                && !stageWorkbench.IsBottomPaneAttached,
                $"stage={stageWorkbench.OperatorStage}; validate={stageWorkbench.HasValidateStageComposition}; linkedViewer={stageWorkbench.HasEvidenceLinkedViewerComposition}; queueTab={stageReview?.IsInspectionQueueSelected}; queueItems={inspectionQueue?.Samples.Count}; running={inspectionQueue?.IsRunning}; canRunChecked={inspectionQueue?.RunCheckedCommand.CanExecute(null)}; primaryBound={inspectionRunButton is not null && inspectionQueue is not null && ReferenceEquals(inspectionRunButton.Command, inspectionQueue.RunScopeCommand)}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; bottom={stageWorkbench.IsBottomPaneAttached}");

            var inspectionBar = FindVisualDescendants<Border>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionExecutionBar");
            var inspectionScope = FindVisualDescendants<ComboBox>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionRunScope");
            var inspectionStop = FindVisualDescendants<ButtonBase>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "StopInspectionQueueRun");
            var inspectionProgress = FindVisualDescendants<ProgressBar>(stageWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionProgress");
            Check("v3 inspection central bar owns the existing checked, remaining and stop commands without left duplicates",
                inspectionBar.IsVisible && inspectionBar.ActualHeight == 44 && inspectionRunButton!.ActualHeight == 32 && inspectionScope.ActualHeight == 32
                && Equals(inspectionScope.SelectedValue, inspectionQueue!.SelectedRunScope) && ReferenceEquals(inspectionStop.Command, inspectionQueue.StopCommand)
                && !inspectionRunButton.IsEnabled && inspectionScope.IsEnabled && !inspectionStop.IsVisible
                && !FindVisualDescendants<ButtonBase>(inspectionQueueView!).Any(control => AutomationProperties.GetAutomationId(control) is "StartInspectionQueueRun" or "StopInspectionQueueRun")
                && !FindVisualDescendants<ProgressBar>(inspectionQueueView!).Any()
                && FindVisualDescendants<Grid>(inspectionQueueView!).Single(control => AutomationProperties.GetAutomationId(control) == "InspectionQueueSummaryProgress") is { } queueDistribution
                && queueDistribution.ColumnDefinitions.Count == 7 && queueDistribution.ColumnDefinitions.All(column => column.Width.IsAbsolute && column.Width.Value == 0 && column.ActualWidth == 0)
                && inspectionQueue.PassRateText == "—%",
                $"bar={inspectionBar.ActualHeight};checked={inspectionRunButton!.ActualHeight};remaining={inspectionScope.ActualHeight};leftProgress={FindVisualDescendants<ProgressBar>(inspectionQueueView!).Count()}");
            SizeLayoutFixture(stageHost, 1280, stageHost.Height);
            stageHost.UpdateLayout();
            Check(
                "Compact Validate clamps restored ratios to a readable action-pane width",
                stageWorkbench.IsCompactDockLayout
                && stageWorkbench.HasReadableValidationPaneRatio
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty,
                $"compact={stageWorkbench.IsCompactDockLayout}; readableRatio={stageWorkbench.HasReadableValidationPaneRatio}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}");
            SizeLayoutFixture(stageHost, 1600, stageHost.Height);
            stageHost.UpdateLayout();
            foreach (var section in Enum.GetValues<ValidationWorkspaceSection>())
            {
                stageWorkbench.SetValidationWorkspaceSection(section);
            }
            Check(
                "Validate exposes five local drill-down sections without leaving the stage",
                stageWorkbench.ActiveValidationWorkspaceSection == ValidationWorkspaceSection.HeldOut
                && shell.IsInspectWorkspaceSelected,
                $"section={stageWorkbench.ActiveValidationWorkspaceSection}; stage={shell.SelectedWorkspaceMode}");
            stageWorkbench.ActivateSessionLogPane();
            stageHost.UpdateLayout();
            Check(
                "Validate reveals one explicitly requested advanced support pane without execution",
                shell.IsInspectWorkspaceSelected
                && stageWorkbench.IsSessionLogPaneSelected
                && stageWorkbench.VisibleValidationPaneTabCount == 2
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && !shell.Workbench.IsValidationSetRunning
                && !shell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={stageWorkbench.OperatorStage}; sessionLog={stageWorkbench.IsSessionLogPaneSelected}; visibleTabs={stageWorkbench.VisibleValidationPaneTabCount}; selected={shell.Workbench.SelectedPipelineStep?.Id}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; validationRunning={shell.Workbench.IsValidationSetRunning}; previewRunning={shell.Workbench.IsSelectedStepPreviewRunning}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Review);
            stageHost.UpdateLayout();
            Check(
                "v3 Results gives the read-only record workspace the full stage with saved history on the left",
                shell.IsResultsWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Results
                && stageWorkbench.HasResultsStageComposition
                && !stageWorkbench.HasEvidenceLinkedViewerComposition
                && stageWorkbench.IsDedicatedResultsWorkspace
                && stageWorkbench.HasStableStageHostedDataContexts
                && stageWorkbench.HasLocalizedResultsNavigation
                && !stageWorkbench.IsBottomPaneAttached,
                $"stage={stageWorkbench.OperatorStage}; results={stageWorkbench.HasResultsStageComposition}; linkedViewer={stageWorkbench.HasEvidenceLinkedViewerComposition}; dedicated={stageWorkbench.IsDedicatedResultsWorkspace}; contexts={stageWorkbench.HasStableStageHostedDataContexts}; localized={stageWorkbench.HasLocalizedResultsNavigation}; bottom={stageWorkbench.IsBottomPaneAttached}");
            var resultsSelectedStep = shell.Workbench.SelectedPipelineStep;
            var resultsDirtyBeforeHistory = shell.Workbench.IsDirty;
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            stageHost.UpdateLayout();
            var resultsWorkspaceView = FindVisualDescendants<ResultsWorkspaceView>(stageWorkbench).Single(view => view.IsVisible);
            var resultsHistoryView = (ResultsHistoryView)resultsWorkspaceView.FindName("HistoryDashboard");
            var resultsHistory = (ListBox)resultsHistoryView.FindName("StoredHistoryList");
            var recentSelectionBefore = shell.SelectedRecentRunRecord;
            var recentFixture = new RunRecordRecentItem("owned-record.json", "owned-record.json", "2026-10-05 12:00", "Pass", 8, false);
            shell.RecentRunRecords.Add(recentFixture);
            foreach (var resultsWidth in new[] { 1280d, 1920d })
            {
                SizeLayoutFixture(stageHost, resultsWidth, stageHost.Height);
                stageHost.UpdateLayout();
                stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                resultsHistory.SelectedItem = recentFixture;
                var recentFromView = ReferenceEquals(shell.SelectedRecentRunRecord, recentFixture);
                shell.SelectedRecentRunRecord = null;
                stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                var openHistory = FindVisualDescendants<ButtonBase>(resultsWorkspaceView).Single(button => AutomationProperties.GetAutomationId(button) == "ResultsOpenStoredHistory");
                Check(
                    $"v3 Results binds actual history in both directions with an explicit open action at {resultsWidth:0}px",
                    ReferenceEquals(resultsHistory.ItemsSource, shell.RecentRunRecords)
                    && recentFromView && resultsHistory.SelectedItem is null
                    && resultsHistory.IsVisible && resultsHistory.ActualWidth is >= 240 and <= 320
                    && ReferenceEquals(openHistory.Command, shell.OpenRecentRunRecordCommand)
                    && !shell.Workbench.IsSelectedStepPreviewRunning && !shell.Workbench.IsOrderedRunRunning
                    && shell.Workbench.IsDirty == resultsDirtyBeforeHistory,
                    $"historyWidth={resultsHistory.ActualWidth:F1}; viewToModel={recentFromView}; modelToView={resultsHistory.SelectedItem is null}; running={shell.Workbench.IsOrderedRunRunning}");
            }
            shell.RecentRunRecords.Remove(recentFixture);
            shell.SelectedRecentRunRecord = recentSelectionBefore;
            var resultsStepCount = shell.Workbench.PipelineSteps.Count;
            var resultsDirty = shell.Workbench.IsDirty;
            var resultsViewerOutput = shell.Workbench.CurrentViewerOutputSummary;
            var resultsRunSummary = shell.RunSnapshotSummary;
            foreach (var section in Enum.GetValues<ResultsWorkspaceSection>())
            {
                stageWorkbench.SetResultsWorkspaceSection(section);
            }
            Check(
                "Results and teaching drawer section commands preserve recipe, selection, and recorded evidence",
                stageWorkbench.ActiveResultsWorkspaceSection == ResultsWorkspaceSection.Log
                && shell.IsResultsWorkspaceSelected
                && ReferenceEquals(resultsSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == resultsStepCount
                && shell.Workbench.IsDirty == resultsDirty
                && string.Equals(resultsViewerOutput, shell.Workbench.CurrentViewerOutputSummary, StringComparison.Ordinal)
                && string.Equals(resultsRunSummary, shell.RunSnapshotSummary, StringComparison.Ordinal),
                $"section={stageWorkbench.ActiveResultsWorkspaceSection}; stage={shell.SelectedWorkspaceMode}; steps={shell.Workbench.PipelineSteps.Count}; dirty={shell.Workbench.IsDirty}; output={shell.Workbench.CurrentViewerOutputSummary}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Expert);
            Check(
                "Advanced diagnostics remain an explicit full-layout route",
                shell.IsExpertWorkspaceSelected
                && stageWorkbench.OperatorStage == OpenVisionOperatorStage.Legacy
                && stageWorkbench.HasAllLegacySupportPaneTabs
                && stageWorkbench.GetDockingPaneContracts().Count == 12,
                $"stage={stageWorkbench.OperatorStage}; mode={shell.SelectedWorkspaceMode}; supportTabs={stageWorkbench.HasAllLegacySupportPaneTabs}; panes={stageWorkbench.GetDockingPaneContracts().Count}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Review);
            Check(
                "Results and Advanced navigation preserve recipe, output, and run evidence",
                ReferenceEquals(resultsSelectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == resultsStepCount
                && shell.Workbench.IsDirty == resultsDirty
                && string.Equals(resultsViewerOutput, shell.Workbench.CurrentViewerOutputSummary, StringComparison.Ordinal)
                && string.Equals(resultsRunSummary, shell.RunSnapshotSummary, StringComparison.Ordinal),
                $"stage={stageWorkbench.OperatorStage}; selected={shell.Workbench.SelectedPipelineStep?.Id}; dirty={shell.Workbench.IsDirty}; output={shell.Workbench.CurrentViewerOutputSummary}");
            shell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Workbench);
            Check(
                "all stage navigation is presentation-only",
                stageWorkbench.HasTeachStageComposition
                && stageWorkbench.HasAuthoringStageComposition
                && ReferenceEquals(selectedStep, shell.Workbench.SelectedPipelineStep)
                && shell.Workbench.PipelineSteps.Count == recipeStepCount
                && shell.Workbench.IsDirty == recipeDirty
                && string.Equals(selectedStepState, shell.Workbench.SelectedPipelineStep?.State, StringComparison.Ordinal),
                $"stage={stageWorkbench.OperatorStage}; selected={shell.Workbench.SelectedPipelineStep?.Id}; state={shell.Workbench.SelectedPipelineStep?.State}; dirty={shell.Workbench.IsDirty}");
            // Exercise the real deferred floating Window after other layout checks so a failed round trip cannot invalidate their visual references.
            var transition = stageWorkbench.VerifyFirstPaneFloatDockRoundTrip();
            Check(
                "Workbench pane Float then Dock transition",
                transition.Floated && transition.Redocked
                && transition.FloatingWindowCountAfterFloat == transition.FloatingWindowCountBefore + 1
                && transition.FloatingWindowCountAfterDock == transition.FloatingWindowCountBefore,
                transition.ToString());
            Check(
                "Workbench state owners survive dock transition",
                ReferenceEquals(stageWorkbench.DataContext, shell)
                && ReferenceEquals(stageWorkbench.ViewerContent, viewerOwner)
                && stageWorkbench.HasStableStageHostedDataContexts,
                "Loaded Workbench DataContext and ViewerContent references retained");
            stageHost.Close();
            stageHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            Check("v3 unloaded dock host clears the presentation command projection and disables its actions",
                validationDock.EvidencePaneLayoutItem is null && validationResultView.DockPaneCommands is null
                && !validationAutoHide.IsEnabled && !validationFloat.IsEnabled,
                $"item={validationDock.EvidencePaneLayoutItem is not null};reviewItem={validationResultView.DockPaneCommands is not null};hide={validationAutoHide.IsEnabled};float={validationFloat.IsEnabled}");
            Check("v3 unloaded sample host clears its native command projection and disables actions",
                validationDock.DataLayersPaneLayoutItem is null && validationSamplesView.DockPaneCommands is null && !sampleAutoHide.IsEnabled && !sampleFloat.IsEnabled,
                $"item={validationDock.DataLayersPaneLayoutItem is not null};viewItem={validationSamplesView.DockPaneCommands is not null};hide={sampleAutoHide.IsEnabled};float={sampleFloat.IsEnabled}");

            var ownerPathShell = new ShellMainWindowViewModel();
            var ownerPathRecipe = validationFixture.RecipePath;
            var ownerPathRunRecord = WriteOwnerPathRunRecord(
                validationFixture);
            var ownerPathOpenMessage = string.Empty;
            var ownerPathRunMessage = string.Empty;
            Check(
                "IA-4b owner path opens the controlled recipe, five labeled samples, and Fail Run Record",
                ownerPathShell.Workbench.TryOpenTeachingRecipe(
                    ownerPathRecipe,
                    out ownerPathOpenMessage)
                && ownerPathShell.Workbench.ValidationSetSamples.Count == 5
                && ownerPathShell.LoadRunRecord(
                    ownerPathRunRecord,
                    out ownerPathRunMessage)
                && ownerPathShell.InspectionSteps.Count == 1,
                $"{ownerPathOpenMessage}; run={ownerPathRunMessage}; samples={ownerPathShell.Workbench.ValidationSetSamples.Count}; runSteps={ownerPathShell.InspectionSteps.Count}");
            Task.Run(() =>
                    ownerPathShell.Workbench.RunValidationSetAsync())
                .GetAwaiter()
                .GetResult();
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Inspect);
            var ownerPathWorkbench = new ToolRecipeWorkbenchView
            {
                DataContext = ownerPathShell,
                ViewerContent = viewerOwner,
            };
            var ownerPathHost = new Window
            {
                Content = ownerPathWorkbench,
                Width = 1600,
                Height = 900,
                Left = testLeft,
                Top = testTop,
                ShowActivated = false,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };
            ownerPathHost.Show();
            SizeLayoutFixture(ownerPathHost, 1600, 900);
            ownerPathHost.UpdateLayout();
            ShellWindowNativeInterop.AppendWindowMonitorEvidence(ownerPathHost, monitorReportPath);
            var sampleBoundaryRoot = Path.Combine(GetReportDirectory(reportPath), "d18-row-fixture");
            Directory.CreateDirectory(sampleBoundaryRoot);
            var copiedSampleSource = validationFixture.Samples.First(sample => sample.Role == ToolRecipeValidationSampleRole.Good).SourcePath;
            var longSamplePath = Path.Combine(sampleBoundaryRoot, "sample-long-name-장착방향과표면상태를확인하는검증샘플-012345678901234567890123456789.C3D");
            var truncatedSamplePath = Path.Combine(sampleBoundaryRoot, "truncated.C3D");
            File.Copy(copiedSampleSource, longSamplePath, overwrite: true);
            File.WriteAllBytes(truncatedSamplePath, [0x43, 0x33, 0x44]);
            void RunBoundarySetOnDispatcher()
            {
                var dispatcher = ownerPathHost.Dispatcher;
                var previousContext = SynchronizationContext.Current;
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                try
                {
                    var runTask = ownerPathShell.Workbench.RunValidationSetAsync();
                    // Complete UI-affine continuations without claiming an active-renderer timing check.
                    if (!SpinWait.SpinUntil(() =>
                        {
                            dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                            return runTask.IsCompleted;
                        }, TimeSpan.FromSeconds(30)))
                    {
                        throw new TimeoutException("Validation sample boundary execution did not complete within 30 seconds.");
                    }
                    runTask.GetAwaiter().GetResult();
                }
                finally
                {
                    SynchronizationContext.SetSynchronizationContext(previousContext);
                }
            }
            ownerPathShell.Workbench.SetValidationSetSources(validationSamplePaths.Append(longSamplePath).Append(truncatedSamplePath));
            RunBoundarySetOnDispatcher();
            ownerPathShell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Validate);
            ownerPathHost.UpdateLayout();
            ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
            var boundarySamplesView = FindVisualDescendants<ValidationSamplesView>(ownerPathWorkbench).Single();
            var boundarySamplesList = (ListBox)boundarySamplesView.FindName("SamplesList");
            var actualLongSample = ownerPathShell.Workbench.ValidationSetSamples.Single(sample => sample.SourcePath == longSamplePath);
            var actualTruncatedSample = ownerPathShell.Workbench.ValidationSetSamples.Single(sample => sample.SourcePath == truncatedSamplePath);
            Check("v3 sample row boundaries use an identical valid C3D and an actual three-byte truncated input",
                actualLongSample.Status == ownerPathShell.Workbench.ValidationSetSamples.Single(sample => sample.SourcePath == copiedSampleSource).Status
                && actualTruncatedSample.Status == "Error" && actualTruncatedSample.Steps.All(step => step.Status != "Pass")
                && File.ReadAllBytes(copiedSampleSource).SequenceEqual(File.ReadAllBytes(longSamplePath)) && !ownerPathShell.Workbench.IsValidationSetRunning,
                $"long={actualLongSample.Status};truncated={actualTruncatedSample.Status};error={actualTruncatedSample.Message}");
            foreach (var boundaryWidth in new[] { 1280d, 1920d })
            {
                SizeLayoutFixture(ownerPathHost, boundaryWidth, boundaryWidth == 1280 ? 760 : 1040);
                ownerPathShell.Workbench.SelectedValidationSetSample = actualLongSample;
                ownerPathHost.UpdateLayout();
                var longSampleContainer = (ListBoxItem)boundarySamplesList.ItemContainerGenerator.ContainerFromItem(actualLongSample);
                var longNameText = FindVisualDescendants<TextBlock>(longSampleContainer).Single(text => text.Text == actualLongSample.FileName);
                var sourcePathToolTip = (ToolTip)longNameText.ToolTip;
                sourcePathToolTip.PlacementTarget = longNameText;
                sourcePathToolTip.IsOpen = true;
                ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                Check($"v5 selected long filename trims within its sample row and exposes the full source path at {boundaryWidth:0}px",
                    longNameText.IsVisible && longNameText.TextWrapping == TextWrapping.NoWrap && longNameText.TextTrimming == TextTrimming.CharacterEllipsis
                    && longNameText.ToolTip is ToolTip { Content: TextBlock pathText } && pathText.Text == actualLongSample.SourcePath
                    && pathText.TextWrapping == TextWrapping.Wrap
                    && longNameText.TransformToAncestor(longSampleContainer).TransformBounds(new Rect(longNameText.RenderSize)).Right <= longSampleContainer.ActualWidth + 0.1
                    && ReferenceEquals(boundarySamplesList.SelectedItem, actualLongSample),
                    $"name={longNameText.Text};size={longNameText.ActualWidth},{longNameText.ActualHeight};desired={longNameText.DesiredSize}");
                sourcePathToolTip.IsOpen = false;
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap,
                    Path.ChangeExtension(reportPath, $".d18-long-name-{boundaryWidth:0}.png"));
                ownerPathShell.Workbench.SelectedValidationSetSample = actualTruncatedSample;
                ownerPathHost.UpdateLayout();
                var errorSampleContainer = (ListBoxItem)boundarySamplesList.ItemContainerGenerator.ContainerFromItem(actualTruncatedSample);
                var selectedErrorPanel = FindVisualDescendants<Border>(boundarySamplesView).Single(border => AutomationProperties.GetAutomationId(border) == "ValidationSelectedSampleError");
                var errorMessageText = FindVisualDescendants<TextBlock>(selectedErrorPanel).Single(text => text.Text == actualTruncatedSample.Message);
                Check($"v3 actual truncated-input error remains readable and distinct from NG at {boundaryWidth:0}px",
                    errorMessageText.IsVisible && errorMessageText.TextWrapping == TextWrapping.Wrap && errorMessageText.TextTrimming == TextTrimming.None
                    && errorMessageText.ActualHeight >= errorMessageText.DesiredSize.Height - 0.1
                    && selectedErrorPanel.IsVisible
                    && errorMessageText.TransformToAncestor(selectedErrorPanel).TransformBounds(new Rect(errorMessageText.RenderSize)).Bottom <= selectedErrorPanel.ActualHeight + 0.1
                    && FindVisualDescendants<FrameworkElement>(errorSampleContainer).Any(control => control.IsVisible && AutomationProperties.GetName(control) == actualTruncatedSample.StatusText),
                    $"status={actualTruncatedSample.StatusText};message={errorMessageText.Text};size={errorMessageText.ActualWidth},{errorMessageText.ActualHeight}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap,
                    Path.ChangeExtension(reportPath, $".d18-error-{boundaryWidth:0}.png"));
            }
            var boundaryErrorFilter = FindVisualDescendants<RadioButton>(boundarySamplesView).Single(button => AutomationProperties.GetAutomationId(button) == "ValidationSamplesFilterError");
            ActivateSampleChoice(boundaryErrorFilter);
            ownerPathHost.UpdateLayout();
            Check("v3 rendered Error filter selects the actual input error without rerunning or removing staged samples",
                ownerPathShell.Workbench.IsValidationSetFilterError && boundarySamplesList.Items.Count == 1 && ownerPathShell.Workbench.ValidationSetAllCount == 7
                && ReferenceEquals(boundarySamplesList.Items[0], actualTruncatedSample) && !ownerPathShell.Workbench.IsValidationSetRunning,
                $"visible={boundarySamplesList.Items.Count};all={ownerPathShell.Workbench.ValidationSetAllCount};selected={ownerPathShell.Workbench.SelectedValidationSetSample?.FileName}");
            ownerPathShell.Workbench.SetValidationSetSources(validationSamplePaths);
            ownerPathShell.Workbench.SetValidationSetFilterCommand.Execute("All");
            RunBoundarySetOnDispatcher();
            ownerPathHost.UpdateLayout();
            Check("v3 replacing the malformed-input set with its valid original set restores real results and enabled row commands",
                ownerPathShell.Workbench.ValidationSetAllCount == 5 && ownerPathShell.Workbench.ValidationSetErrorCount == 0
                && ownerPathShell.Workbench.ValidationSetFailCount > 0 && ownerPathShell.Workbench.SelectedValidationSetSample?.Status == "Fail"
                && ownerPathShell.Workbench.SetValidationSampleRoleCommand.CanExecute(ownerPathShell.Workbench.SelectedValidationSetSample!.GoodRoleChange)
                && !ownerPathShell.Workbench.IsValidationSetRunning,
                $"samples={ownerPathShell.Workbench.ValidationSetAllCount};errors={ownerPathShell.Workbench.ValidationSetErrorCount};fail={ownerPathShell.Workbench.ValidationSetFailCount}");
            var boundaryBar = FindVisualDescendants<Border>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationExecutionBar");
            var boundaryRun = FindVisualDescendants<ButtonBase>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "RunValidationWorkspaceSet");
            var boundaryCancel = FindVisualDescendants<ButtonBase>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "CancelValidationWorkspaceSet");
            var boundaryProgress = FindVisualDescendants<ProgressBar>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationProgress");
            var boundaryProgressText = FindVisualDescendants<TextBlock>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationProgressText");
            var boundarySummary = FindVisualDescendants<TextBlock>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationIdleSummary");
            var boundaryRecipeName = FindVisualDescendants<TextBlock>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationRecipeName");
            var boundarySaveState = FindVisualDescendants<TextBlock>(ownerPathWorkbench).Single(control => AutomationProperties.GetAutomationId(control) == "ValidationTeachingSaveState");
            var recipeNameBeforeBarCheck = ownerPathShell.Workbench.RecipeName;
            ownerPathShell.Workbench.RecipeName = "현재 티칭 조건의 긴 이름 · 장착 방향과 표면 상태 확인 · 012345678901234567890123456789";
            ownerPathHost.UpdateLayout();
            Check("v3 current teaching capsule follows the actual recipe name and unsaved edit state without execution",
                boundaryRecipeName.Text == ownerPathShell.Workbench.HeaderRecipeText && boundaryRecipeName.TextTrimming == TextTrimming.CharacterEllipsis
                && boundarySaveState.Text == ownerPathShell.Workbench.Localization.Modified && boundaryBar.IsVisible
                && !ownerPathShell.Workbench.IsValidationSetRunning,
                $"recipe={boundaryRecipeName.Text};save={boundarySaveState.Text}");
            foreach (var executionWidth in new[] { 1280d, 1920d })
            {
                SizeLayoutFixture(ownerPathHost, executionWidth, executionWidth == 1280 ? 760 : 1040);
                ownerPathHost.UpdateLayout();
                var nameBounds = boundaryRecipeName.TransformToAncestor(boundaryBar).TransformBounds(new Rect(boundaryRecipeName.RenderSize));
                var actionBounds = boundaryRun.TransformToAncestor(boundaryBar).TransformBounds(new Rect(boundaryRun.RenderSize));
                Check($"v3 long teaching identity and complete action captions remain within the execution bar at {executionWidth:0}px",
                    boundaryBar.ActualHeight == 44 && nameBounds.Right <= actionBounds.Left && actionBounds.Right <= boundaryBar.ActualWidth
                    && FindVisualDescendants<TextBlock>(boundaryRun).Where(label => label.IsVisible).All(label => label.ActualWidth + label.Margin.Left + label.Margin.Right >= label.DesiredSize.Width - 0.1)
                    && boundarySummary.Text == ownerPathShell.Workbench.ValidationSetRunBasisText
                    && boundaryProgress.Width == (executionWidth == 1280 ? 110 : 180),
                    $"name={nameBounds};action={actionBounds};progressWidth={boundaryProgress.Width};summary={boundarySummary.Text}");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap,
                    Path.ChangeExtension(reportPath, $".d19-long-teaching-{executionWidth:0}.png"));
            }
            ownerPathShell.Workbench.RecipeName = recipeNameBeforeBarCheck;
            var commandBoundaryRoot = Path.Combine(GetReportDirectory(reportPath), "d19-command-fixture");
            Directory.CreateDirectory(commandBoundaryRoot);
            var commandSamplePaths = Enumerable.Range(0, 512).Select(index =>
            {
                var samplePath = Path.Combine(commandBoundaryRoot, $"cancel-sample-{index:000}.C3D");
                File.Copy(copiedSampleSource, samplePath, overwrite: true);
                return samplePath;
            }).ToArray();
            ownerPathShell.Workbench.SetValidationSetSources(commandSamplePaths);
            SizeLayoutFixture(ownerPathHost, 1280, 760);
            ownerPathHost.UpdateLayout();
            var commandSourceBefore = ownerPathShell.Workbench.Source.Path;
            var commandDirtyBefore = ownerPathShell.Workbench.IsDirty;
            var previousCommandContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(ownerPathHost.Dispatcher));
            try
            {
                ActivateSampleChoice(boundaryRun);
                ActivateSampleChoice(boundaryRun);
                Check("v3 rendered Run uses one guarded execution and exposes the existing Cancel command",
                    ownerPathShell.Workbench.IsValidationSetRunning && !ownerPathShell.Workbench.RunValidationSetCommand.CanExecute(null)
                    && !boundaryRun.IsVisible && boundaryCancel.IsVisible && boundaryCancel.IsEnabled
                    && boundaryProgressText.Text == ownerPathShell.Workbench.ValidationSetProgressText,
                    $"running={ownerPathShell.Workbench.IsValidationSetRunning};run={boundaryRun.IsVisible};cancel={boundaryCancel.IsEnabled};progress={boundaryProgressText.Text}");
                ActivateSampleChoice(boundaryCancel);
                ownerPathHost.UpdateLayout();
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap,
                    Path.ChangeExtension(reportPath, ".d19-cancel-pending-1280.png"));
                if (!SpinWait.SpinUntil(() =>
                    {
                        ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                        return !ownerPathShell.Workbench.IsValidationSetRunning && ownerPathShell.Workbench.RunValidationSetCommand.CanExecute(null);
                    }, TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Rendered validation cancellation did not restore its command within 30 seconds.");
                ownerPathHost.UpdateLayout();
                Check("v3 rendered Cancel restores Run, the actual canceled summary, and unchanged source and authored recipe",
                    boundaryRun.IsVisible && boundaryRun.IsEnabled && !boundaryCancel.IsVisible
                    && boundarySummary.Text == ownerPathShell.Workbench.ValidationSetRunBasisText
                    && (boundarySummary.Text.Contains("canceled", StringComparison.OrdinalIgnoreCase) || boundarySummary.Text.Contains("취소", StringComparison.Ordinal))
                    && ownerPathShell.Workbench.Source.Path == commandSourceBefore && ownerPathShell.Workbench.IsDirty == commandDirtyBefore,
                    $"summary={boundarySummary.Text};run={boundaryRun.IsEnabled};source={ownerPathShell.Workbench.Source.Path}");
                ownerPathShell.Workbench.SetValidationSetSources(validationSamplePaths);
                ActivateSampleChoice(boundaryRun);
                if (!SpinWait.SpinUntil(() =>
                    {
                        ownerPathHost.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.DataBind);
                        return !ownerPathShell.Workbench.IsValidationSetRunning && ownerPathShell.Workbench.RunValidationSetCommand.CanExecute(null);
                    }, TimeSpan.FromSeconds(30)))
                    throw new TimeoutException("Rendered validation recovery did not complete within 30 seconds.");
                ownerPathHost.UpdateLayout();
                Check("v3 explicit central Run recovers from cancellation with real results and progress completion",
                    ownerPathShell.Workbench.ValidationSetAllCount == 5 && ownerPathShell.Workbench.ValidationSetErrorCount == 0
                    && ownerPathShell.Workbench.ValidationSetPassCount + ownerPathShell.Workbench.ValidationSetFailCount == 5
                    && boundaryProgress.Value == 100 && boundaryProgressText.Text == ownerPathShell.Workbench.ValidationSetProgressText
                    && boundarySummary.Text == ownerPathShell.Workbench.ValidationSetRunBasisText && boundaryRun.IsEnabled && !boundaryCancel.IsVisible,
                    $"pass={ownerPathShell.Workbench.ValidationSetPassCount};fail={ownerPathShell.Workbench.ValidationSetFailCount};progress={boundaryProgress.Value};summary={boundarySummary.Text}");
            }
            finally
            {
                ownerPathShell.Workbench.CancelValidationSetCommand.Execute(null);
                SynchronizationContext.SetSynchronizationContext(previousCommandContext);
            }
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap,
                Path.ChangeExtension(reportPath, ".d19-recovered-1280.png"));
            ownerPathShell.Workbench.ClearValidationSetCommand.Execute(null);
            ownerPathHost.UpdateLayout();
            Check("v3 empty sample set disables the central Run while preserving the visible next action and explanation",
                ownerPathShell.Workbench.ValidationSetAllCount == 0 && boundaryRun.IsVisible && !boundaryRun.IsEnabled
                && !boundaryCancel.IsVisible && boundarySummary.Text == ownerPathShell.Workbench.ValidationSetRunBasisText
                && (boundarySummary.Text.Contains("검증을 실행하세요", StringComparison.Ordinal) || boundarySummary.Text.Contains("run validation", StringComparison.OrdinalIgnoreCase))
                && !ownerPathShell.Workbench.IsValidationSetRunning,
                $"count={ownerPathShell.Workbench.ValidationSetAllCount};enabled={boundaryRun.IsEnabled};summary={boundarySummary.Text}");
            WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(ownerPathWorkbench).Bitmap,
                Path.ChangeExtension(reportPath, ".d19-empty-1280.png"));
            Check("v3 owned fixture restores its saved recipe and labeled samples after the empty-state check",
                ownerPathShell.Workbench.TryOpenTeachingRecipe(ownerPathRecipe, out var restoredRecipeMessage)
                && ownerPathShell.Workbench.ValidationSetAllCount == 5 && boundaryRun.IsEnabled,
                $"samples={ownerPathShell.Workbench.ValidationSetAllCount};recipe={ownerPathShell.Workbench.RecipeName};message={restoredRecipeMessage}");
            RunBoundarySetOnDispatcher();
            foreach (var nonValidationMode in new[] { ShellWorkspaceMode.Teach, ShellWorkspaceMode.Inspect, ShellWorkspaceMode.Review })
            {
                ownerPathShell.SelectWorkspaceCommand.Execute(nonValidationMode);
                ownerPathHost.UpdateLayout();
                Check($"v3 central validation actions are absent from the {nonValidationMode} stage",
                    boundaryBar.Visibility == Visibility.Collapsed && !boundaryRun.IsVisible && !boundaryCancel.IsVisible
                    && !ownerPathShell.Workbench.IsValidationSetRunning,
                    $"mode={ownerPathShell.SelectedWorkspaceMode};bar={boundaryBar.Visibility};run={boundaryRun.IsVisible}");
            }
            ownerPathShell.SelectWorkspaceCommand.Execute(ShellWorkspaceMode.Inspect);
            ownerPathHost.UpdateLayout();
            ownerPathWorkbench.SetValidationWorkspaceSection(
                ValidationWorkspaceSection.Samples);
            ownerPathHost.UpdateLayout();
            Check(
                "Validate exposes one stable keyboard-focusable sample-set action",
                ownerPathWorkbench.HasAccessibleValidationSampleSetAction
                && ownerPathWorkbench.HasValidationSamplesFirstUseClarity
                && !ownerPathWorkbench.IsValidationIssueNavigationVisible
                && ownerPathShell.Workbench.RunValidationSetCommand
                    .CanExecute(null),
                $"accessible={ownerPathWorkbench.HasAccessibleValidationSampleSetAction}; firstUse={ownerPathWorkbench.HasValidationSamplesFirstUseClarity}; issueNav={ownerPathWorkbench.IsValidationIssueNavigationVisible}; canRun={ownerPathShell.Workbench.RunValidationSetCommand.CanExecute(null)}");
            ownerPathWorkbench.SetValidationWorkspaceSection(
                ValidationWorkspaceSection.Failures);
            ownerPathHost.UpdateLayout();
            var ownerFailureStepId =
                ownerPathShell.Workbench.SelectedValidationSetStep?.StepId;
            Check(
                "Failure Analysis leads with selected sample rule and reason before technical evidence",
                ownerPathWorkbench.HasValidationFailureOperatorSummary
                && ownerPathWorkbench.HasValidationResultsReviewControls
                && ownerPathWorkbench.IsValidationIssueNavigationVisible,
                $"summary={ownerPathWorkbench.HasValidationFailureOperatorSummary}; reviewControls={ownerPathWorkbench.HasValidationResultsReviewControls}; issueNav={ownerPathWorkbench.IsValidationIssueNavigationVisible}; sample={ownerPathShell.Workbench.SelectedValidationSetSample?.FileName}; rule={ownerPathShell.Workbench.SelectedValidationSetStep?.ToolName}; reason={ownerPathShell.Workbench.SelectedValidationSetStep?.Evidence}");
            Check(
                "IA-4b explicit sample-set execution exposes a real failure-to-Teach route",
                ownerPathShell.Workbench.ValidationSetFailCount > 0
                && ownerPathShell.Workbench.SelectedValidationSetSample
                    ?.Status == "Fail"
                && ownerPathShell
                    .OpenSelectedValidationIssueInTeachCommand
                    .CanExecute(null),
                $"pass={ownerPathShell.Workbench.ValidationSetPassCount}; fail={ownerPathShell.Workbench.ValidationSetFailCount}; error={ownerPathShell.Workbench.ValidationSetErrorCount}; selected={ownerPathShell.Workbench.SelectedValidationSetSample?.Status}; step={ownerFailureStepId}");
            var ownerRecipePath = ownerPathShell.Workbench.RecipePath;
            var ownerSourcePath = ownerPathShell.Workbench.Source.Path;
            var ownerStepCount = ownerPathShell.Workbench.PipelineSteps.Count;
            var ownerDirty = ownerPathShell.Workbench.IsDirty;
            var ownerValidationSummary =
                ownerPathShell.Workbench.ValidationSetSummary;
            var ownerRunSummary = ownerPathShell.RunSnapshotSummary;
            var ownerInspectionSummary =
                ownerPathShell.InspectionStepSummary;
            ownerPathShell.OpenSelectedValidationIssueInTeachCommand
                .Execute(null);
            ownerPathHost.UpdateLayout();
            var ownerSelectedStepId =
                ownerPathShell.Workbench.SelectedPipelineStep?.Id;
            Check(
                "IA-4b failure opens its owning Teach step without mutation or hidden execution",
                ownerPathShell.IsTeachWorkspaceSelected
                && string.Equals(
                    ownerSelectedStepId,
                    ownerFailureStepId,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; selected={ownerSelectedStepId}; failureStep={ownerFailureStepId}; dirty={ownerDirty}->{ownerPathShell.Workbench.IsDirty}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            var correctionContext =
                ownerPathShell.Workbench.ActiveValidationFailureCorrectionContext;
            Check(
                "Teach correction carries the failed sample, owning rule, reason, and cell summary",
                correctionContext is not null
                && correctionContext.SampleName.Length > 0
                && string.Equals(
                    correctionContext.StepId,
                    ownerFailureStepId,
                    StringComparison.OrdinalIgnoreCase)
                && correctionContext.ToolName.Length > 0
                && correctionContext.Reason.Length > 0
                && correctionContext.CellSummary.Contains(
                    "failed cells",
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathWorkbench.IsFailureCorrectionContextVisible,
                $"sample={correctionContext?.SampleName}; step={correctionContext?.StepId}; rule={correctionContext?.ToolName}; reason={correctionContext?.Reason}; cells={correctionContext?.CellSummary}; card={ownerPathWorkbench.IsFailureCorrectionContextVisible}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Inspect);
            SizeLayoutFixture(ownerPathHost, 1100, 760);
            ownerPathHost.UpdateLayout();
            ownerPathShell.OpenSelectedValidationIssueInTeachCommand
                .Execute(null);
            ownerPathHost.UpdateLayout();
            Check(
                "Compact failure-to-Teach opens Selected Tool automatically without hidden execution",
                ownerPathShell.IsTeachWorkspaceSelected
                && ownerPathWorkbench.IsCompactDockLayout
                && ownerPathWorkbench.IsToolInspectorPaneSelected
                && ownerPathWorkbench.IsFailureCorrectionContextVisible
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; compact={ownerPathWorkbench.IsCompactDockLayout}; selectedTool={ownerPathWorkbench.IsToolInspectorPaneSelected}; card={ownerPathWorkbench.IsFailureCorrectionContextVisible}; dirty={ownerDirty}->{ownerPathShell.Workbench.IsDirty}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Review);
            ownerPathWorkbench.SetResultsWorkspaceSection(
                ResultsWorkspaceSection.RunRecord);
            ownerPathHost.UpdateLayout();
            Check(
                "Results leads with operator decision steps and a keyboard-focusable correction route",
                ownerPathWorkbench.HasResultsOperatorSummary
                && ownerPathShell
                    .OpenSelectedValidationIssueInTeachCommand
                    .CanExecute(null),
                $"summary={ownerPathWorkbench.HasResultsOperatorSummary}; decision={ownerPathShell.RunSnapshotSummary}; steps={ownerPathShell.InspectionStepSummary}; canFix={ownerPathShell.OpenSelectedValidationIssueInTeachCommand.CanExecute(null)}");
            // Check state on the actual Results owner; rendered disclosure is verified through the running Shell.
            var resultsDock = (OpenVisionDockWorkspaceView)ownerPathWorkbench.FindName("DockWorkspace");
            var resultsView = (ResultsWorkspaceView)resultsDock.ResultsContent!;
            var measurementDetails = (Expander)resultsView.FindName("ResultsValueValidityPanel");
            var measurementText = (TextBlock)resultsView.FindName("ResultsValueValidityValue");
            var resultBeforeDisclosure = ownerPathShell.ResultsOperatorDecisionDisplaySummary;
            var initiallyCollapsed = !measurementDetails.IsExpanded;
            measurementDetails.IsExpanded = true;
            var detailsExpanded = measurementDetails.IsExpanded;
            measurementDetails.IsExpanded = false;
            Check(
                "Results disclosure preserves the bound decision, selection, dirty state and explicit execution boundary",
                initiallyCollapsed && detailsExpanded && !measurementDetails.IsExpanded
                && measurementText.FontSize >= 12
                && ownerPathShell.ResultsOperatorDecisionDisplaySummary == resultBeforeDisclosure
                && ownerPathShell.Workbench.SelectedPipelineStep?.Id == ownerSelectedStepId
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && ownerPathShell.IsResultsWorkspaceSelected
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"collapsed={initiallyCollapsed}; expanded={detailsExpanded}; font={measurementText.FontSize}; dirty={ownerPathShell.Workbench.IsDirty}; stage={ownerPathShell.SelectedWorkspaceMode}");
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Expert);
            ownerPathHost.UpdateLayout();
            ownerPathShell.SelectWorkspaceCommand.Execute(
                ShellWorkspaceMode.Review);
            ownerPathWorkbench.SetResultsWorkspaceSection(
                ResultsWorkspaceSection.RunRecord);
            ownerPathHost.UpdateLayout();
            Check(
                "IA-4b Results to Advanced to Results preserves recipe, failure, validation, output, and Run Record evidence",
                ownerPathShell.IsResultsWorkspaceSelected
                && ownerPathWorkbench.HasResultsStageComposition
                && ownerPathWorkbench.HasStableStageHostedDataContexts
                && string.Equals(
                    ownerPathShell.Workbench.RecipePath,
                    ownerRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    ownerPathShell.Workbench.Source.Path,
                    ownerSourcePath,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.PipelineSteps.Count
                    == ownerStepCount
                && string.Equals(
                    ownerPathShell.Workbench.SelectedPipelineStep?.Id,
                    ownerSelectedStepId,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == ownerDirty
                && string.Equals(
                    ownerPathShell.Workbench.ValidationSetSummary,
                    ownerValidationSummary,
                    StringComparison.Ordinal)
                && ownerPathShell.Workbench.ValidationSetFailCount > 0
                && string.Equals(
                    ownerPathShell.RunSnapshotSummary,
                    ownerRunSummary,
                    StringComparison.Ordinal)
                && string.Equals(
                    ownerPathShell.InspectionStepSummary,
                    ownerInspectionSummary,
                    StringComparison.Ordinal)
                && ownerPathShell.InspectionSteps.Count == 1,
                $"stage={ownerPathShell.SelectedWorkspaceMode}; recipe={ownerPathShell.Workbench.RecipePath}; selected={ownerPathShell.Workbench.SelectedPipelineStep?.Id}; dirty={ownerPathShell.Workbench.IsDirty}; validation={ownerPathShell.Workbench.ValidationSetSummary}; run={ownerPathShell.RunSnapshotSummary}; runSteps={ownerPathShell.InspectionSteps.Count}");

            var beforeLayoutRecipePath = ownerPathShell.Workbench.RecipePath;
            var beforeLayoutDirty = ownerPathShell.Workbench.IsDirty;
            var beforeLayoutStepCount = ownerPathShell.Workbench.PipelineSteps.Count;
            var beforeLayoutValidationSummary =
                ownerPathShell.Workbench.ValidationSetSummary;
            var customDockState = OpenVisionDockPresentationState.Default with
            {
                Wide = OpenVisionDockPresentationState.Default.Wide with
                {
                    ValidateEvidence = 1.25,
                    ValidateViewer = 3.05,
                    ResultsEvidence = 1.30,
                    ResultsViewer = 3.00,
                },
                PrimaryContentId = "displayed-outputs",
                SupportContentId = "tool-inspector",
            };
            ownerPathWorkbench.ApplyDockPresentationState(customDockState);
            ownerPathWorkbench.ResetDockPresentationState();
            var resetDockState = ownerPathWorkbench.CaptureDockPresentationState();
            Check(
                "R19 default layout keeps Recipe Flow selected beside the dedicated Tool settings panel",
                resetDockState.SupportContentId == "data-layers",
                $"support={resetDockState.SupportContentId}");
            Check(
                "layout apply and reset are presentation-only",
                string.Equals(
                    ownerPathShell.Workbench.RecipePath,
                    beforeLayoutRecipePath,
                    StringComparison.OrdinalIgnoreCase)
                && ownerPathShell.Workbench.IsDirty == beforeLayoutDirty
                && ownerPathShell.Workbench.PipelineSteps.Count
                    == beforeLayoutStepCount
                && string.Equals(
                    ownerPathShell.Workbench.ValidationSetSummary,
                    beforeLayoutValidationSummary,
                    StringComparison.Ordinal)
                && !ownerPathShell.Workbench.IsValidationSetRunning
                && !ownerPathShell.Workbench.IsSelectedStepPreviewRunning,
                $"recipe={beforeLayoutRecipePath}; dirty={beforeLayoutDirty}->{ownerPathShell.Workbench.IsDirty}; steps={beforeLayoutStepCount}->{ownerPathShell.Workbench.PipelineSteps.Count}; validationRunning={ownerPathShell.Workbench.IsValidationSetRunning}; previewRunning={ownerPathShell.Workbench.IsSelectedStepPreviewRunning}");

            ownerPathHost.Close();

            var layoutVerificationDirectory = Path.Combine(
                GetReportDirectory(reportPath),
                $"layout-profile-{Guid.NewGuid():N}");
            Directory.CreateDirectory(layoutVerificationDirectory);
            var layoutPath = Path.Combine(
                layoutVerificationDirectory,
                "studio-layout.json");
            var layoutStore = new StudioLayoutProfileStore(layoutPath);
            var savedProfile = StudioLayoutProfile.Default with
            {
                Workbench = customDockState,
                Window = new StudioWindowPlacement(
                    SystemParameters.VirtualScreenLeft + 20,
                    SystemParameters.VirtualScreenTop + 20,
                    1440,
                    900,
                    IsMaximized: false),
            };
            layoutStore.Save(savedProfile);
            var restoredLayout = layoutStore.Load();
            Check(
                "safe layout profile save and reload round-trip preserves allowlisted presentation state",
                restoredLayout.Status == StudioLayoutLoadStatus.Restored
                && restoredLayout.Profile.Workbench.Wide.ValidateEvidence
                    == 1.25
                && restoredLayout.Profile.Workbench.Wide.ValidateViewer
                    == 3.05
                && restoredLayout.Profile.Workbench.PrimaryContentId
                    == "displayed-outputs"
                && restoredLayout.Profile.Workbench.SupportContentId
                    == "tool-inspector",
                $"status={restoredLayout.Status}; evidence={restoredLayout.Profile.Workbench.Wide.ValidateEvidence}; viewer={restoredLayout.Profile.Workbench.Wide.ValidateViewer}; primary={restoredLayout.Profile.Workbench.PrimaryContentId}; support={restoredLayout.Profile.Workbench.SupportContentId}");
            Check(
                "layout save is atomic and leaves no temporary sidecar",
                !Directory.EnumerateFiles(
                        layoutVerificationDirectory,
                        "*.tmp",
                        SearchOption.TopDirectoryOnly)
                    .Any(),
                layoutVerificationDirectory);

            File.WriteAllText(layoutPath, "{ not-json");
            var corruptLayout = layoutStore.Load();
            Check(
                "corrupt layout fails safely to defaults and disables automatic overwrite",
                corruptLayout.Status == StudioLayoutLoadStatus.Corrupt
                && !corruptLayout.CanAutoSave
                && corruptLayout.Profile == StudioLayoutProfile.Default,
                $"status={corruptLayout.Status}; canAutoSave={corruptLayout.CanAutoSave}; message={corruptLayout.Message}");

            File.WriteAllText(
                layoutPath,
                JsonSerializer.Serialize(
                    savedProfile with { SchemaVersion = 999 },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            var incompatibleLayout = layoutStore.Load();
            Check(
                "incompatible layout schema fails safely to defaults",
                incompatibleLayout.Status
                    == StudioLayoutLoadStatus.Incompatible
                && !incompatibleLayout.CanAutoSave,
                $"status={incompatibleLayout.Status}; canAutoSave={incompatibleLayout.CanAutoSave}; message={incompatibleLayout.Message}");

            var unsafeDockState = customDockState with
            {
                Wide = customDockState.Wide with
                {
                    ValidateEvidence = 99,
                },
                PrimaryContentId = "unknown-content",
                SupportContentId = "unknown-support",
            };
            File.WriteAllText(
                layoutPath,
                JsonSerializer.Serialize(
                    savedProfile with
                    {
                        Workbench = unsafeDockState,
                        Window = new StudioWindowPlacement(
                            999999,
                            999999,
                            1200,
                            800,
                            IsMaximized: false),
                    },
                    new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }));
            var sanitizedLayout = layoutStore.Load();
            Check(
                "unknown IDs invalid ratios and off-screen bounds are sanitized",
                sanitizedLayout.Status
                    == StudioLayoutLoadStatus.RestoredWithFallback
                && sanitizedLayout.Profile.Window is null
                && sanitizedLayout.Profile.Workbench.Wide.ValidateEvidence
                    == OpenVisionDockPresentationState.Default.Wide
                        .ValidateEvidence
                && sanitizedLayout.Profile.Workbench.PrimaryContentId
                    == OpenVisionDockPresentationState.Default.PrimaryContentId
                && sanitizedLayout.Profile.Workbench.SupportContentId
                    == OpenVisionDockPresentationState.Default.SupportContentId,
                $"status={sanitizedLayout.Status}; window={sanitizedLayout.Profile.Window}; evidence={sanitizedLayout.Profile.Workbench.Wide.ValidateEvidence}; primary={sanitizedLayout.Profile.Workbench.PrimaryContentId}; support={sanitizedLayout.Profile.Workbench.SupportContentId}");
            layoutStore.Reset();
            Check(
                "explicit layout reset removes only the selected profile",
                !File.Exists(layoutPath)
                && Directory.Exists(layoutVerificationDirectory),
                $"profileExists={File.Exists(layoutPath)}; directoryExists={Directory.Exists(layoutVerificationDirectory)}");

            var advancedMarker = new object();
            var advanced = new OpenVisionDockWorkspaceView
            {
                ToolLibraryContent = advancedMarker,
                DataLayersContent = advancedMarker,
                ViewerContent = advancedMarker,
                ToolInspectorContent = advancedMarker,
                EvidenceContent = advancedMarker,
                OutputCompareContent = advancedMarker,
                DisplayedOutputsContent = advancedMarker,
                LinkedViewContent = advancedMarker,
                ProfileContent = advancedMarker,
                FitDiagnosticsContent = advancedMarker,
                IntersectionEvidenceContent = advancedMarker,
                CorrespondenceEvidenceContent = advancedMarker,
            };
            var advancedContracts = advanced.GetDockingPaneContracts();
            Check("Advanced exposes twelve dock panes", advancedContracts.Count == 12 && HasExactIds(advancedContracts, WorkbenchContentIds), Describe(advancedContracts));
            Check("Advanced panes can float and remain required", advancedContracts.All(contract => contract.CanFloat && !contract.CanClose) && advancedContracts.Single(contract => contract.ContentId == "fit-diagnostics").CanHide == true, Describe(advancedContracts));
            var reactivatedAdvancedViewer = new object();
            Check(
                "Advanced reactivation owns the requested Viewer in its live presenter",
                advanced.ReactivateViewerContent(reactivatedAdvancedViewer),
                $"viewer={ReferenceEquals(advanced.ViewerContent, reactivatedAdvancedViewer)}");

            var repositoryRoot = FindRepositoryRoot();
            var xamlFiles = Directory.GetFiles(
                Path.Combine(repositoryRoot, "src"),
                "*.xaml",
                SearchOption.AllDirectories);
            var xamlDocuments = xamlFiles
                .Select(path => (Path: path, Document: XDocument.Load(path)))
                .ToArray();
            var comboBoxTags = xamlFiles
                .SelectMany(path => Regex.Matches(
                    File.ReadAllText(path),
                    "<ComboBox(?=\\s|>)[^>]*>",
                    RegexOptions.Singleline)
                    .Cast<Match>()
                    .Select(match => (Path: path, Tag: match.Value)))
                .ToArray();
            var undersizedComboBoxes = comboBoxTags
                .SelectMany(combo => Regex.Matches(
                    combo.Tag,
                    "(?:Height|MinHeight)=\"([0-9.]+)\"")
                    .Cast<Match>()
                    .Where(match =>
                    {
                        // HTML v3 explicitly uses compact selectors inside 32/34-pixel headers.
                        // Restrict that contract to the actual changed owner and control IDs.
                        var owner = Path.GetFileName(combo.Path);
                        var fixedSourcePin = owner == "ViewerWorkspaceView.xaml" && (combo.Tag.Contains("AutomationProperties.AutomationId=\"MainViewerContentPin\"", StringComparison.Ordinal)
                            || combo.Tag.Contains("AutomationProperties.AutomationId=\"AuxiliaryViewerContentPin\"", StringComparison.Ordinal));
                        if (fixedSourcePin && match.Value == "MinHeight=\"0\"" && Regex.IsMatch(combo.Tag, "(?<!Min)Height=\"24\"")) return false;
                        var minimum = owner == "HeightImageViewerView.xaml" && combo.Tag.Contains("AutomationProperties.AutomationId=\"HeightImageLegacyPalette\"", StringComparison.Ordinal) ? 26
                            : owner == "SelectedToolWorkspaceView.xaml" && combo.Tag.Contains("AutomationProperties.AutomationId=\"SelectedToolStepSelector\"", StringComparison.Ordinal) ? 26
                            : owner == "SelectedToolWorkspaceView.xaml" && combo.Tag.Contains("AutomationProperties.AutomationId=\"SelectedToolInputRoute\"", StringComparison.Ordinal) ? 28
                            : owner == "ViewerWorkspaceView.xaml" && (combo.Tag.Contains("AutomationProperties.AutomationId=\"MainViewerContentPin\"", StringComparison.Ordinal)
                                || combo.Tag.Contains("AutomationProperties.AutomationId=\"AuxiliaryViewerContentPin\"", StringComparison.Ordinal)) ? 24 : 30;
                        return double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) < minimum;
                    })
                    .Select(match => $"{Path.GetRelativePath(repositoryRoot, combo.Path)}:{match.Value}"))
                .ToArray();
            var animatedPopups = xamlDocuments
                .Where(owner => owner.Document.Descendants().Any(element => element.Name.LocalName == "Popup"
                    && element.Attribute("PopupAnimation") is { Value: not "None" }))
                .Select(owner => Path.GetRelativePath(repositoryRoot, owner.Path))
                .ToArray();
            var comboBoxStyles = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Style")
                    .Where(element => IsStyleTarget(element, "ComboBox"))
                    .Select(element => (owner.Path, Style: element)))
                .ToArray();
            var comboBoxItemStyles = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Style")
                    .Where(element => IsStyleTarget(element, "ComboBoxItem"))
                    .Select(element => (owner.Path, Style: element)))
                .ToArray();
            var undersizedComboBoxStyleSetters = comboBoxStyles
                .SelectMany(owner => owner.Style
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Setter")
                    .Where(element => element.Attribute("Property")?.Value is "Height" or "MinHeight")
                    .Where(element => ReferenceEquals(element.Ancestors().FirstOrDefault(ancestor => ancestor.Name.LocalName == "Style"), owner.Style))
                    .Where(element => !(Path.GetFileName(owner.Path) == "ViewerWorkspaceView.xaml"
                        && owner.Style.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "ViewerSourcePinStyle")
                        && (element.Attribute("Property")?.Value, element.Attribute("Value")?.Value) is ("Height", "24") or ("MinHeight", "0")))
                    .Where(element => double.TryParse(
                        element.Attribute("Value")?.Value,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out var value) && value < 30)
                    .Select(element => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{element.Attribute("Property")?.Value}={element.Attribute("Value")?.Value}"))
                .ToArray();
            var unsafeComboBoxTextStyles = comboBoxStyles
                .Where(owner =>
                    !HasIdealTextFormatting(owner.Style)
                    && owner.Style.Attribute("BasedOn") is null
                    || HasPixelSnapping(owner.Style))
                .Select(owner => Path.GetRelativePath(repositoryRoot, owner.Path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var unsafeComboBoxItemTextStyles = comboBoxItemStyles
                .Where(owner =>
                    !HasIdealTextFormatting(owner.Style) && owner.Style.Attribute("BasedOn") is null
                    || HasPixelSnapping(owner.Style))
                .Select(owner => Path.GetRelativePath(repositoryRoot, owner.Path))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Check(
                "ComboBoxes preserve the shared minimum and the explicit v3 header/input dimensions",
                comboBoxTags.Length > 0
                && undersizedComboBoxes.Length == 0
                && undersizedComboBoxStyleSetters.Length == 0,
                $"count={comboBoxTags.Length}; tagUndersized={string.Join(',', undersizedComboBoxes)}; styleUndersized={string.Join(',', undersizedComboBoxStyleSetters)}");
            Check(
                "All ComboBox style owners preserve fractional-DPI glyph bounds",
                comboBoxStyles.Length >= 6
                && comboBoxItemStyles.Length >= 7
                && unsafeComboBoxTextStyles.Length == 0
                && unsafeComboBoxItemTextStyles.Length == 0,
                $"comboStyles={comboBoxStyles.Length}; itemStyles={comboBoxItemStyles.Length}; unsafeCombo={string.Join(',', unsafeComboBoxTextStyles)}; unsafeItems={string.Join(',', unsafeComboBoxItemTextStyles)}");
            Check(
                "Repository XAML disables popup animation",
                animatedPopups.Length == 0,
                $"animated={string.Join(',', animatedPopups)}");
            var buttonBaseTypeNames = new HashSet<string>(StringComparer.Ordinal)
            {
                "Button",
                "ToggleButton",
                "RadioButton",
                "CheckBox",
            };
            var buttonBaseControls = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => buttonBaseTypeNames.Contains(element.Name.LocalName))
                    .Select(element => (owner.Path, Control: element)))
                .ToArray();
            var buttonBaseStyles = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "Style")
                    .Where(element => buttonBaseTypeNames.Contains(GetTargetTypeLocalName(element)))
                    .Select(element => (owner.Path, Style: element)))
                .ToArray();
            var unsafeButtonStyles = buttonBaseStyles
                .Where(owner => owner.Style.Attribute("BasedOn") is null
                    && !owner.Style.Descendants()
                        .Any(element => element.Name.LocalName == "Setter"
                            && element.Attribute("Property")?.Value == "Template"))
                .Select(owner => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{GetTargetTypeLocalName(owner.Style)}")
                .ToArray();
            var appButtonTemplates = xamlDocuments
                .SelectMany(owner => owner.Document
                    .Descendants()
                    .Where(element => element.Name.LocalName == "ControlTemplate")
                    .Where(element => buttonBaseTypeNames.Contains(GetTargetTypeLocalName(element)))
                    .Where(element => !element.Ancestors().Any(ancestor =>
                        (ancestor.Name.LocalName is "Style" or "ControlTemplate")
                        && (GetTargetTypeLocalName(ancestor) is "ComboBox" or "ComboBoxItem")))
                    .Select(element => (owner.Path, Template: element)))
                .ToArray();
            var incompleteButtonTemplates = appButtonTemplates
                .Select(owner =>
                {
                    var style = owner.Template.Ancestors().FirstOrDefault(element => element.Name.LocalName == "Style");
                    var control = owner.Template.Ancestors().FirstOrDefault(element => buttonBaseTypeNames.Contains(element.Name.LocalName));
                    var styleChain = new List<XElement>();
                    while (style is not null && !styleChain.Contains(style))
                    {
                        styleChain.Add(style);
                        var basedOn = style.Attribute("BasedOn")?.Value;
                        var key = basedOn is not null && basedOn.StartsWith("{StaticResource ", StringComparison.Ordinal) && basedOn.EndsWith('}')
                            ? basedOn[16..^1] : null;
                        style = key is null ? null : owner.Template.Document!.Descendants().FirstOrDefault(element => element.Name.LocalName == "Style"
                            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == key));
                    }
                    var states = owner.Template.Descendants().Concat(styleChain.SelectMany(styleOwner => styleOwner.Elements()
                            .Where(element => element.Name.LocalName == "Style.Triggers").SelectMany(element => element.Descendants())))
                        .Where(element => element.Name.LocalName is "Trigger" or "Condition")
                        .Select(element => element.Attribute("Property")?.Value)
                        .Where(property => property is not null)
                        .ToHashSet(StringComparer.Ordinal);
                    var focusVisual = control?.Attribute("FocusVisualStyle")?.Value ?? styleChain.SelectMany(styleOwner => styleOwner.Elements())
                        .FirstOrDefault(element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "FocusVisualStyle")?.Attribute("Value")?.Value;
                    var focusKey = focusVisual is not null && focusVisual.StartsWith("{StaticResource ", StringComparison.Ordinal) && focusVisual.EndsWith('}')
                        ? focusVisual[16..^1] : null;
                    var hasFocusVisual = focusKey is not null && owner.Template.Document!.Descendants().Any(element => element.Name.LocalName == "Style"
                        && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == focusKey)
                        && element.Descendants().Any(child => child.Name.LocalName == "ControlTemplate"));
                    var missing = new List<string>();
                    if (!states.Contains("IsMouseOver") && !states.Contains("behaviors:PointerHoverBehavior.IsHovered")) missing.Add("hover");
                    if (!states.Contains("IsPressed")) missing.Add("pressed");
                    if (!states.Contains("IsKeyboardFocused")
                        && !states.Contains("IsKeyboardFocusWithin") && !hasFocusVisual) missing.Add("focus");
                    if (!states.Contains("IsEnabled")) missing.Add("disabled");
                    if ((GetTargetTypeLocalName(owner.Template) is "ToggleButton" or "RadioButton")
                        && !states.Contains("IsChecked")) missing.Add("checked");
                    return (owner.Path, Type: GetTargetTypeLocalName(owner.Template), Missing: missing);
                })
                .Where(owner => owner.Missing.Count > 0)
                .Select(owner => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{owner.Type}:{string.Join('+', owner.Missing)}")
                .ToArray();
            var hardCodedButtonGlyphs = buttonBaseControls
                .SelectMany(owner => owner.Control
                    .Descendants()
                    .Where(element => element.Name.LocalName is "Path" or "Polygon")
                    .Where(element => ReferenceEquals(
                        element.Ancestors().FirstOrDefault(ancestor => buttonBaseTypeNames.Contains(ancestor.Name.LocalName)),
                        owner.Control))
                    .SelectMany(element => element.Attributes()
                        .Where(attribute => attribute.Name.LocalName is "Stroke" or "Fill")
                        .Where(attribute => attribute.Value.StartsWith('#')
                            || attribute.Value is "White" or "Black")
                        .Select(attribute => $"{Path.GetRelativePath(repositoryRoot, owner.Path)}:{owner.Control.Name.LocalName}:{element.Name.LocalName}.{attribute.Name.LocalName}={attribute.Value}")))
                .ToArray();
            var messageDialogSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.Wpf.MessageDialogs",
                "WpfMessageDialogControl.xaml.cs"));
            var shellSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "MainWindow.xaml.cs"));
            var smokeScenarioSource = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "Automation",
                "ShellSmokeScenarioRunner.cs"));
            var hasDialogPrimaryTarget = shellSource.Contains("new ShellSmokeScenarioRunner(", StringComparison.Ordinal)
                && smokeScenarioSource.Contains("CaptureMessageDialogForSmokeAsync(", StringComparison.Ordinal)
                && smokeScenarioSource.Contains("MessageDialogPrimaryButton", StringComparison.Ordinal);
            Check(
                "Button audit covers all declarations, style owners, custom templates, and dynamic dialog buttons",
                buttonBaseControls.Length > 0
                && buttonBaseStyles.Length > 0
                && appButtonTemplates.Length > 0
                && messageDialogSource.Contains("var button = new Button", StringComparison.Ordinal)
                && messageDialogSource.Contains("MessageDialogPrimaryButton", StringComparison.Ordinal),
                $"controls={buttonBaseControls.Length}; styles={buttonBaseStyles.Length}; templates={appButtonTemplates.Length}; dynamicDialog={messageDialogSource.Contains("var button = new Button", StringComparison.Ordinal)}; primaryId={messageDialogSource.Contains("MessageDialogPrimaryButton", StringComparison.Ordinal)}");
            Check(
                "Every explicit ButtonBase style derives from a themed base or owns its template",
                unsafeButtonStyles.Length == 0,
                $"unsafe={string.Join(',', unsafeButtonStyles)}");
            Check(
                "Every app-facing button resolves hover, pressed, focus, disabled, and checked states through its template/style with semantic glyphs",
                incompleteButtonTemplates.Length == 0
                && hardCodedButtonGlyphs.Length == 0
                && hasDialogPrimaryTarget,
                $"incomplete={string.Join(',', incompleteButtonTemplates)}; glyphLeaks={string.Join(',', hardCodedButtonGlyphs)}; dialogPrimaryTarget={hasDialogPrimaryTarget}");
            var advancedXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "MainWindow.xaml"));
            var legacyAdvancedColors = new[]
            {
                "#ffffff",
                "#d1d5db",
                "#111827",
                "#6b7280",
                "#374151",
                "#b45309",
                "#b91c1c",
                "#f9fafb",
            };
            var advancedThemeResources = new[]
            {
                "ThreeD.PanelBrush",
                "ThreeD.PanelAlternateBrush",
                "ThreeD.ViewportBrush",
                "ThreeD.DividerBrush",
                "ThreeD.TextBrush",
                "ThreeD.PrimaryTextBrush",
                "ThreeD.SecondaryTextBrush",
                "ThreeD.MutedTextBrush",
                "ThreeD.WarningBrush",
                "ThreeD.FailBrush",
            };
            Check(
                "Advanced content surfaces use semantic theme resources without the legacy light palette",
                legacyAdvancedColors.All(color => !advancedXaml.Contains(color, StringComparison.OrdinalIgnoreCase))
                && advancedThemeResources.All(resource => advancedXaml.Contains(resource, StringComparison.Ordinal)),
                $"legacy={string.Join(',', legacyAdvancedColors.Where(color => advancedXaml.Contains(color, StringComparison.OrdinalIgnoreCase)))}; semantic={advancedThemeResources.Count(resource => advancedXaml.Contains(resource, StringComparison.Ordinal))}/{advancedThemeResources.Length}");

            var themeXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "Themes",
                "OpenVisionThreeDTheme.xaml"));
            Check(
                "v3 Shell declares the source compact/wide footer heights with operation and actual input status",
                advancedXaml.Contains("x:Name=\"StudioBottomStatus\"", StringComparison.Ordinal)
                && advancedXaml.Contains("<Setter Property=\"Height\" Value=\"24\" />", StringComparison.Ordinal)
                && advancedXaml.Contains("<Setter Property=\"Height\" Value=\"22\" />", StringComparison.Ordinal)
                && advancedXaml.Contains("Text=\"{Binding Workbench.SourceContractSummary}\"", StringComparison.Ordinal)
                && advancedXaml.Contains("Text=\"{Binding StatusText, Converter={StaticResource ViewerRuntimeTextConverter}}\"", StringComparison.Ordinal),
                "Source footer 24/22; actual operation, declared input contract, and recipe state bindings present");
            var dockXaml = File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "OpenVisionLab.ThreeD.Docking.Controls",
                "Views",
                "OpenVisionDockWorkspaceView.xaml"));
            var controlStateMarkers = new[]
            {
                "IsMouseOver",
                "IsKeyboardFocusWithin",
                "Validation.HasError",
                "IsReadOnly",
                "IsEnabled",
                "ThreeD.AccentHoverBrush",
                "ThreeD.FocusBrush",
                "ThreeD.FailBrush",
                "ThreeD.DisabledSurfaceBrush",
            };
            var dockStateMarkers = new[]
            {
                "IsMouseOver",
                "IsSelected",
                "IsKeyboardFocused",
                "IsEnabled",
                "ThreeD.PanelAlternateBrush",
                "ThreeD.SelectedSurfaceBrush",
                "ThreeD.FocusBrush",
                "ThreeD.DisabledBrush",
            };
            Check(
                "Advanced generated controls retain semantic hover focus selected disabled read-only and validation states",
                controlStateMarkers.All(marker => themeXaml.Contains(marker, StringComparison.Ordinal))
                && dockStateMarkers.All(marker => dockXaml.Contains(marker, StringComparison.Ordinal)),
                $"controls={controlStateMarkers.Count(marker => themeXaml.Contains(marker, StringComparison.Ordinal))}/{controlStateMarkers.Length}; dockTabs={dockStateMarkers.Count(marker => dockXaml.Contains(marker, StringComparison.Ordinal))}/{dockStateMarkers.Length}");

            var calibrationMarker = new object();
            var calibration = new OpenVisionCalibrationDockWorkspaceView
            {
                ExplorerContent = calibrationMarker,
                WorkspaceContent = calibrationMarker,
                InspectorContent = calibrationMarker,
                EvidenceContent = calibrationMarker,
            };
            var calibrationContracts = calibration.GetDockingPaneContracts();
            Check("Calibration exposes four dock panes", calibrationContracts.Count == 4 && HasExactIds(calibrationContracts, CalibrationContentIds), Describe(calibrationContracts));
            Check("Calibration panes can float and cannot close", calibrationContracts.All(contract => contract.CanFloat && !contract.CanClose), Describe(calibrationContracts));
            Check("Calibration anchorables cannot hide", calibrationContracts.Where(contract => contract.CanHide.HasValue).All(contract => contract.CanHide == false), Describe(calibrationContracts));
        }
        catch (Exception exception)
        {
            Check("unexpected exception", false, exception.ToString());
        }

        Directory.CreateDirectory(GetReportDirectory(reportPath));

        var succeeded = passed == total
            && total > 0
            && !lines.Any(line => line.StartsWith("FAIL | unexpected exception", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        File.WriteAllLines(reportPath, lines);
        summary = $"Docking workspace verification: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return succeeded;
    }

    private static bool HasExactIds(
        IReadOnlyList<DockingPaneContract> contracts,
        IReadOnlyList<string> expectedIds) =>
        contracts.Select(contract => contract.ContentId).Order(StringComparer.Ordinal)
            .SequenceEqual(expectedIds.Order(StringComparer.Ordinal), StringComparer.Ordinal)
        && contracts.Select(contract => contract.ContentId).Distinct(StringComparer.Ordinal).Count() == contracts.Count;

    private static string WriteOwnerPathRunRecord(
        CompletenessValidationVerificationFixture fixture)
    {
        var path = Path.Combine(fixture.RootPath, "owner-path-run-record.json");
        var document = fixture.Document;
        var step = document.Steps.Single();
        var metrics = new[]
        {
            new InspectionRunMetric(
                "Passed cells",
                MetricKind.Count,
                2,
                "cells",
                ResultStatus.Fail),
            new InspectionRunMetric(
                "Failed cells",
                MetricKind.Count,
                2,
                "cells",
                ResultStatus.Fail),
        };
        var stepResult = new InspectionRunStepResult(
            0,
            step.Id,
            step.ToolId,
            step.ToolName,
            step.InputEntityIds,
            step.OutputEntityId,
            ResultStatus.Fail,
            "Completeness Grid Fail: 2 passed, 2 failed, 4 total cells.",
            0,
            metrics,
            []);
        var record = new InspectionRunRecord(
            "1.5",
            "run-workbench-owner-path-fixture",
            new DateTimeOffset(2026, 7, 29, 0, 0, 0, TimeSpan.Zero),
            new InspectionRunRecipe(
                "tool-recipe",
                document.SchemaVersion,
                fixture.RecipePath,
                Convert.ToHexString(SHA256.HashData(
                    File.ReadAllBytes(fixture.RecipePath)))),
            new InspectionRunSource(
                document.Source.Id,
                document.Source.Path,
                document.Source.ContentSha256!,
                document.Source.ByteLength
                    ?? new FileInfo(document.Source.Path).Length,
                document.Source.Unit),
            "Ordered Tool Recipe Replay",
            ResultStatus.Fail,
            "Executed 1 authored typed step in recipe order.",
            0,
            metrics,
            [],
            "NotCompared",
            new InspectionRunArtifacts(
                Path.Combine(fixture.RootPath, "owner-path-runner.txt"),
                null,
                null,
                path,
                null,
                null))
        {
            Steps = [stepResult],
        };
        File.WriteAllText(
            path,
            JsonSerializer.Serialize(
                record,
                new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Converters = { new JsonStringEnumConverter() },
                }));
        return path;
    }

    private static string FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            for (var directory = new DirectoryInfo(start);
                 directory is not null;
                 directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, ThicknessRecipeRelativePath)))
                {
                    return directory.FullName;
                }
            }
        }

        throw new DirectoryNotFoundException(
            $"Could not locate repository file {ThicknessRecipeRelativePath}.");
    }

    private static void ArrangeForTextVerification(
        FrameworkElement element,
        double width)
    {
        element.Measure(new Size(width, 760));
        element.Arrange(new Rect(0, 0, width, 760));
        element.UpdateLayout();
    }

    private static bool HasVisiblePresentationPath(DependencyObject element)
    {
        for (var current = element; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is UIElement { Visibility: not Visibility.Visible }) return false;
        }
        return true;
    }

    private static IEnumerable<T> FindVisualDescendants<T>(DependencyObject root)
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

    private static string DescribeVisualAncestors(DependencyObject descendant, DependencyObject root)
    {
        var descriptions = new List<string>();
        for (var current = descendant; current is not null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement element)
            {
                var description = $"{element.GetType().Name}#{element.Name}[{element.ActualWidth:F0}x{element.ActualHeight:F0};min={element.MinWidth:F0}x{element.MinHeight:F0};visibility={element.Visibility};row={Grid.GetRow(element)}]";
                if (element is Grid grid)
                {
                    description += $" rows={string.Join(',', grid.RowDefinitions.Select(row => $"{row.Height}:{row.ActualHeight:F0}/{row.MinHeight:F0}"))}";
                }

                descriptions.Add(description);
            }

            if (ReferenceEquals(current, root))
            {
                break;
            }
        }

        return string.Join(" <- ", descriptions);
    }

    private static bool IsStyleTarget(XElement style, string targetType) =>
        string.Equals(GetTargetTypeLocalName(style), targetType, StringComparison.Ordinal);

    private static string GetTargetTypeLocalName(XElement element)
    {
        var value = element.Attribute("TargetType")?.Value?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        const string xTypePrefix = "{x:Type ";
        if (value.StartsWith(xTypePrefix, StringComparison.Ordinal) && value.EndsWith('}'))
        {
            value = value[xTypePrefix.Length..^1];
        }

        var prefixSeparator = value.LastIndexOf(':');
        return prefixSeparator >= 0 ? value[(prefixSeparator + 1)..] : value;
    }

    private static bool HasIdealTextFormatting(XElement style) =>
        style.Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Any(element =>
                element.Attribute("Property")?.Value == "TextOptions.TextFormattingMode"
                && element.Attribute("Value")?.Value == "Ideal");

    private static bool HasPixelSnapping(XElement style) =>
        style.DescendantsAndSelf().Any(element =>
            element.Attribute("SnapsToDevicePixels")?.Value == "True"
            || element.Attribute("UseLayoutRounding")?.Value == "True")
        || style.Descendants()
            .Where(element => element.Name.LocalName == "Setter")
            .Any(element =>
                (element.Attribute("Property")?.Value is "SnapsToDevicePixels" or "UseLayoutRounding")
                && element.Attribute("Value")?.Value == "True");

    private static string Describe(IEnumerable<DockingPaneContract> contracts) =>
        string.Join(
            "; ",
            contracts.Select(contract =>
                $"{contract.ContentId}:'{contract.Title}'[float={contract.CanFloat},close={contract.CanClose},hide={contract.CanHide?.ToString() ?? "n/a"},content={contract.HasContent}]"));
}

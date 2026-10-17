using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class SelectedToolInputChoiceVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(root);
        var lines = new List<string> { "Boundary|Existing recipe contracts and actual WPF input picker; programmatic selection, separate from native pointer evidence. No inspection/SDK numerical execution." };
        var passed = 0;
        var total = 0;
        var shutdownMode = Application.Current.ShutdownMode;
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Window? host = null;
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }
        try
        {
            var sourcePath = Path.Combine(root, "input.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("source.input-choice", 3, 3, [1, 2, 3, 4, 5, 6, 7, 8, 9], "raw-height", "frame.grid").SaveC3D(sourcePath);
            var sourceBytes = File.ReadAllBytes(sourcePath);
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            workbench.SetC3DSource(sourcePath, markDirty: false);
            var recipePath = Path.Combine(root, "input-choice.ov3d-recipe.json");
            if (!workbench.TrySaveTeachingRecipe(recipePath, out var message)) throw new InvalidOperationException(message);
            var document = ToolRecipeDocumentStore.Load(recipePath);
            ToolRecipeStep Step(string id, string toolId, string output)
            {
                var tool = ToolWorkbenchToolCatalog.Create().Single(item => item.Id == toolId);
                return new ToolRecipeStep(id, toolId, tool.Name, tool.MinimumInputCount, [document.Source.Id], output,
                    tool.Parameters.Select(item => new ToolRecipeParameter(item.Name, item.DefaultValue)).ToArray());
            }
            var filter = Step("step.filter", "filter", "output.filter");
            var noise = Step("step.noise", "remove-outlier-pixels", "output.noise");
            var warpage = Step("step.warpage", "warpage", "output.warpage");
            document = document with { Steps = [filter, noise, warpage] };
            ToolRecipeDocumentStore.Save(recipePath, document);
            var invalidNoise = document with { Steps = [filter, noise with { InputEntityIds = [filter.OutputEntityId] }, warpage] };
            Check("the existing contract requires the original source for outlier removal",
                ToolRecipeValidator.Validate(invalidNoise).Errors.Any(error => error.Contains("Remove Outlier Pixels v1 requires exactly the recipe C3D source", StringComparison.Ordinal)),
                "Filter output rejection is an existing contract; this check does not broaden the algorithm");
            if (!workbench.TryOpenTeachingRecipe(recipePath, out message)) throw new InvalidOperationException(message);
            ShellWindowNativeInterop.TryGetLeftmostWorkArea(out var area);
            host = new Window { Style = new Style(typeof(Window)), WindowStyle = WindowStyle.None, ShowInTaskbar = false,
                ShowActivated = false, Left = area.Left, Top = area.Top, Width = 1280, Height = 760 };
            host.Show();
            void Settle() => host.Dispatcher.Invoke(DispatcherPriority.Background, new Action(host.UpdateLayout));
            foreach (var width in new[] { 1280, 1920 })
            {
                host.Width = width;
                host.Height = width == 1280 ? 760 : 1040;
                var view = new SelectedToolWorkspaceView { DataContext = workbench, Width = width == 1280 ? 290 : 320,
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Stretch };
                var content = new Grid();
                content.Children.Add(view);
                host.Content = content;
                ComboBox Picker() => ShellSmokeArtifacts.FindVisualDescendants<ComboBox>(view)
                    .Single(item => AutomationProperties.GetAutomationId(item) == "SelectedToolInputRoute");
                workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(item => item.Id == noise.Id);
                Settle();
                var noiseInput = workbench.SelectedToolWorkspace.DataInputs.Single();
                Check($"{width} outlier picker excludes the unsupported Filter output",
                    noiseInput.Choices.All(item => item.EntityId != filter.OutputEntityId) && noiseInput.Choices.Any(item => item.EntityId == document.Source.Id),
                    string.Join(";", noiseInput.Choices.Select(item => item.EntityId)));
                var rejectedChoice = new SelectedToolInputChoice(workbench.SelectedPipelineStep.FlowInputs[0], filter.OutputEntityId, "Unsupported output", true);
                workbench.ChooseWorkspaceInputCommand.Execute(rejectedChoice);
                Check($"{width} rejected input cannot alter the recipe or leave a pending connection",
                    workbench.SelectedPipelineStep.InputEntityIds.Single() == document.Source.Id && !workbench.HasPendingFlowConnection && !workbench.IsDirty,
                    workbench.FlowConnectionMessage);
                workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(item => item.Id == warpage.Id);
                Settle();
                workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(item => item.Id == noise.Id);
                Settle();
                var conditionsTab = (RadioButton)view.FindName("EditorAllTab");
                var regionTab = (RadioButton)view.FindName("EditorRegionTab");
                Check($"{width} returning from a measurement to preprocessing keeps Conditions visible",
                    conditionsTab.IsChecked == true && ((Expander)view.FindName("ParametersExpander")).IsVisible,
                    $"conditions={conditionsTab.IsChecked};region={regionTab.IsChecked}/{regionTab.Visibility};parameters={((Expander)view.FindName("ParametersExpander")).IsVisible}");
                workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(item => item.Id == warpage.Id);
                Settle();
                var picker = Picker();
                Check($"{width} warpage offers the compatible Filter output",
                    workbench.SelectedToolWorkspace.DataInputs.Single().Choices.Any(item => item.EntityId == filter.OutputEntityId && item.IsAvailable),
                    "The primary HeightField contract accepts an earlier FilteredHeightField");
                picker.IsDropDownOpen = true;
                picker.SelectedValue = filter.OutputEntityId;
                picker.IsDropDownOpen = false;
                Settle();
                Check($"{width} rendered picker commits the compatible input without running",
                    workbench.SelectedPipelineStep.InputEntityIds[0] == filter.OutputEntityId && Picker().SelectedValue as string == filter.OutputEntityId
                    && workbench.IsDirty && !workbench.HasPendingFlowConnection, workbench.SelectedPipelineStep.InputEntityIdsText);
                Check($"{width} compatible input survives save and reopen",
                    workbench.TrySaveTeachingRecipe(recipePath, out message) && workbench.TryOpenTeachingRecipe(recipePath, out message)
                    && ToolRecipeDocumentStore.Load(recipePath).Steps.Single(item => item.Id == warpage.Id).InputEntityIds[0] == filter.OutputEntityId, message);
                workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(item => item.Id == warpage.Id);
                Settle();
                Check($"{width} restoration binds the saved value without dirtying the recipe",
                    Picker().SelectedValue as string == filter.OutputEntityId && !workbench.IsDirty, Picker().SelectedValue?.ToString() ?? "none");
                picker = Picker();
                picker.IsDropDownOpen = true;
                picker.SelectedValue = document.Source.Id;
                picker.IsDropDownOpen = false;
                Settle();
                Check($"{width} source selection recovers the original input through the same picker",
                    workbench.SelectedPipelineStep.InputEntityIds[0] == document.Source.Id && Picker().SelectedValue as string == document.Source.Id, workbench.SelectedPipelineStep.InputEntityIdsText);
                Check($"{width} fixture recovery saves without any execution or source-file writes",
                    workbench.TrySaveTeachingRecipe(recipePath, out message) && sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath))
                    && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")), message);
                foreach (var mode in new[] { "Inspection", "Validation" })
                {
                    if (mode == "Inspection") workbench.SetInspectionFileView(true);
                    else workbench.SetValidationSampleView(true);
                    var readOnlyChoice = new SelectedToolInputChoice(workbench.SelectedPipelineStep.FlowInputs[0], filter.OutputEntityId, "Filter", true);
                    var inputsBefore = workbench.SelectedPipelineStep.InputEntityIds.ToArray();
                    var canChoose = workbench.ChooseWorkspaceInputCommand.CanExecute(readOnlyChoice);
                    workbench.ChooseWorkspaceInputCommand.Execute(readOnlyChoice);
                    Check($"{width} {mode} sample context rejects direct input mutation",
                        !canChoose && inputsBefore.SequenceEqual(workbench.SelectedPipelineStep.InputEntityIds) && !workbench.IsDirty && !workbench.HasPendingFlowConnection,
                        $"readonly={workbench.IsReadOnlySampleView};canChoose={canChoose};inputs={workbench.SelectedPipelineStep.InputEntityIdsText};dirty={workbench.IsDirty}");
                    if (mode == "Inspection") workbench.SetInspectionFileView(false);
                    else workbench.SetValidationSampleView(false);
                    if (!workbench.TryOpenTeachingRecipe(recipePath, out message)) throw new InvalidOperationException(message);
                    workbench.SelectedPipelineStep = workbench.PipelineSteps.Single(item => item.Id == warpage.Id);
                    Settle();
                }
                var sibling = new SelectedToolWorkspaceView { DataContext = workbench, IsLabPresentation = true };
                content.Children.Add(sibling);
                Settle();
                sibling.Visibility = Visibility.Collapsed;
                Settle();
                conditionsTab.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                var siblingConditionsTab = (RadioButton)sibling.FindName("EditorAllTab");
                siblingConditionsTab.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                siblingConditionsTab.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                Settle();
                Check($"{width} settings instances preserve independent page selections",
                    conditionsTab.IsChecked == true && siblingConditionsTab.IsChecked == true && !workbench.IsDirty,
                    $"visible={conditionsTab.IsChecked};hidden={siblingConditionsTab.IsChecked};groups={conditionsTab.GroupName}/{siblingConditionsTab.GroupName}");
                conditionsTab.GroupName = siblingConditionsTab.GroupName = "ReconstructedLegacySettingsGroup";
                conditionsTab.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                siblingConditionsTab.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
                siblingConditionsTab.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                Settle();
                Check($"{width} reconstructed fixed group clears the other settings instance",
                    conditionsTab.IsChecked == false && siblingConditionsTab.IsChecked == true,
                    $"visible={conditionsTab.IsChecked};hidden={siblingConditionsTab.IsChecked};reconstructed legacy group only");
                conditionsTab.GroupName = siblingConditionsTab.GroupName = string.Empty;
                conditionsTab.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
                content.Children.Remove(sibling);
                sibling.DataContext = null;
                ShellWindowNativeInterop.AppendWindowMonitorEvidence(host, reportPath + ".monitors.txt");
                WpfScreenshotCapture.Save(WpfScreenshotCapture.Capture(view).Bitmap, Path.Combine(root, $"{width}-input-choice.png"));
                host.Content = null;
            }
        }
        catch (Exception exception) { total++;lines.Add("FAIL | unexpected | " + exception); }
        finally
        {
            host?.Close();
            Application.Current.ShutdownMode = shutdownMode;
        }
        summary = $"Selected tool input choice verification: {(passed == total ? "Pass" : "Fail")} ({passed}/{total} checks)";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }
}

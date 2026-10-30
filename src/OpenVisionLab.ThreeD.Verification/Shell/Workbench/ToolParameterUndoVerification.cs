using System.IO;
using System.ComponentModel;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using System.Security.Cryptography;
using OpenVisionLab.ThreeD.Shell.PropertyGrid;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolParameterUndoVerification
{

    public static bool VerifyNoiseToolCopy(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition) { total++; if (condition) passed++; lines.Add($"{(condition ? "PASS" : "FAIL")} | {name}"); }
        var language = OpenVisionLanguageService.CurrentLanguage;
        try
        {
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            using var workbench = new ToolWorkbenchViewModel(Path.ChangeExtension(reportPath, ".recent.json"));
            var tool = workbench.Tools.Single(item => item.Id == "remove-outlier-pixels");
            Check("approved Korean tool name", tool.DisplayName == "노이즈 제거");
            Check("approved processing explanation", tool.DisplayDescription.Contains("주변과 높이 차이가 큰 데이터를 계산에서 제외"));
            Check("stable tool and localization identifiers", tool.Name == "Remove Outlier Pixels" && tool.LocalizedNameKey == "ThreeD.Tool.RemoveOutlierPixels");
            Check("new search term and previous search aliases retained", tool.SearchAliases!.Contains("노이즈") && tool.SearchAliases!.Contains("이상치"));
            Check("kernel and threshold defaults retained", tool.Parameters.Single(item => item.Name == "WindowSize").DefaultValue == "3" && tool.Parameters.Single(item => item.Name == "MaximumAbsoluteDeviation").DefaultValue == "25");
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            Check("English tool contract retained", tool.DisplayName == "Remove Outlier Pixels" && tool.DisplayDescription.Contains("local outliers"));
            Check("localization does not execute", workbench.PipelineSteps.Count == 0 && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")));
        }
        catch (Exception exception) { total++; lines.Add("FAIL | unexpected | " + exception); }
        finally { OpenVisionLanguageService.SetLanguage(language, save: false); }
        summary = $"NoiseToolCopy|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }

    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var passed = 0;
        var total = 0;
        void Check(string name, bool condition) { total++; if (condition) passed++; lines.Add($"{(condition ? "PASS" : "FAIL")}|{name}"); }
        try
        {
            var previousLanguage = OpenVisionLanguageService.CurrentLanguage;
            try
            {
                OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
                foreach (var type in new[] { typeof(ThicknessStepProperties), typeof(CompletenessGridStepProperties), typeof(ConnectedRegionGateStepProperties), typeof(WarpageStepProperties),
                    typeof(PlaneFlatnessStepProperties), typeof(PointPairDimensionsStepProperties), typeof(GapFlushStepProperties), typeof(VolumeStepProperties), typeof(CrossSectionDimensionsStepProperties),
                    typeof(DatumPlaneDeviationStepProperties), typeof(PhysicalEdgeGapFlushStepProperties) })
                {
                    var helpDraft = Activator.CreateInstance(type)!;
                    var properties = TypeDescriptor.GetProperties(LocalizedPropertyGridObject.Create(helpDraft)).Cast<PropertyDescriptor>().Where(property => !property.IsReadOnly).ToArray();
                    Check(type.Name + " every editable parameter has localized name, explanation and catalogue default", properties.Length > 0
                        && properties.All(property => property.DisplayName.Any(character => character is >= '가' and <= '힣')
                            && property.Description.Any(character => character is >= '가' and <= '힣') && property.Description.Contains("기본값:")));
                }
                foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English })
                {
                    OpenVisionLanguageService.SetLanguage(language, save: false);
                    foreach (var type in new[] { typeof(FilterStepProperties), typeof(RemoveOutlierPixelsStepProperties), typeof(ThicknessStepProperties) })
                    {
                        var properties = TypeDescriptor.GetProperties(LocalizedPropertyGridObject.Create(Activator.CreateInstance(type)!)).Cast<PropertyDescriptor>().ToArray();
                        Check($"{language} {type.Name} editor tooltip attributes use the same descriptions as visible help", properties.All(property =>
                            ((DescriptionAttribute?)property.Attributes[typeof(DescriptionAttribute)])?.Description == property.Description));
                    }
                }
            }
            finally { OpenVisionLanguageService.SetLanguage(previousLanguage, save: false); }
            var root = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "undo-recent.json"));
            var recipe = Path.Combine(root, "native-group-fixture", "grouped.ov3d-recipe.json");
            Check("own fixture opens without execution", workbench.TryOpenTeachingRecipe(recipe, out _) && !workbench.IsValidationSetRunning);
            var step = workbench.PipelineSteps.First(item => item.ToolId == "thickness");
            workbench.SelectPipelineStep(step.Id);
            var original = step.Parameters.Select(parameter => (parameter.Name, parameter.Value)).ToArray();
            var source = (workbench.Source.Path, workbench.SourceSession.SourceBinding?.ContentSha256);
            var acceptance = step.AcceptanceLimitsConfigured;
            var draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
            var maximum = draft.MaximumThickness;
            Check("first explicit measurement has no invented previous value", workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult()
                && !workbench.HasSelectedToolResultComparison);
            Check("second explicit measurement exposes its prior executed value", workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult()
                && workbench.HasSelectedToolResultComparison && workbench.SelectedToolResultComparisonText.Contains("→"));
            draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumThickness = maximum + 1;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            Check("pending editor changes cannot be undone as applied values", !workbench.UndoSelectedStepParametersCommand.CanExecute(null));
            Check("normal Apply records the previous parameter values", workbench.TryApplySelectedStepParameterDraft(out _) && workbench.HasSelectedStepParameterHistory
                && workbench.SelectedStepLastAppliedChange.Contains("→") && workbench.UndoSelectedStepParametersCommand.CanExecute(null));
            workbench.SetInspectionFileView(true);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            Check("inspection read-only context rejects Undo without changing the applied settings", !workbench.UndoSelectedStepParametersCommand.CanExecute(null)
                && ((ThicknessStepProperties)workbench.SelectedStepPropertyDraft!).MaximumThickness == maximum + 1 && workbench.HasSelectedStepParameterHistory);
            workbench.SetInspectionFileView(false);
            workbench.SetValidationSampleView(true);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            Check("validation read-only context rejects Undo without dropping history", !workbench.UndoSelectedStepParametersCommand.CanExecute(null)
                && ((ThicknessStepProperties)workbench.SelectedStepPropertyDraft!).MaximumThickness == maximum + 1 && workbench.HasSelectedStepParameterHistory);
            workbench.SetValidationSampleView(false);
            draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumThickness = maximum + 2;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            workbench.TryApplySelectedStepParameterDraft(out _);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            Check("first Undo restores the immediately preceding Apply", ((ThicknessStepProperties)workbench.SelectedStepPropertyDraft!).MaximumThickness == maximum + 1
                && workbench.HasSelectedStepParameterHistory);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            Check("second Undo restores original conditions and ends the session stack", step.Parameters.Select(parameter => (parameter.Name, parameter.Value)).SequenceEqual(original)
                && step.AcceptanceLimitsConfigured == acceptance && !workbench.HasSelectedStepParameterHistory && !workbench.UndoSelectedStepParametersCommand.CanExecute(null));
            Check("Undo retains the previous measured values as stale evidence, never as a restored current result", !workbench.HasCurrentMeasurementPreview
                && workbench.TeachingResults.Where(row => row.Step.Id == step.Id).All(row => !row.IsCurrent));
            Check("Undo retains input and remains an explicit rerun workflow", source == (workbench.Source.Path, workbench.SourceSession.SourceBinding?.ContentSha256)
                && workbench.IsDirty && !workbench.IsSelectedStepPreviewRunning && !workbench.IsValidationSetRunning
                && (workbench.StepParameterEditStatus.Contains("Run again") || workbench.StepParameterEditStatus.Contains("다시 실행 필요")));
            draft = (ThicknessStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumThickness = maximum + 3;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            workbench.TryApplySelectedStepParameterDraft(out _);
            workbench.TryOpenTeachingRecipe(recipe, out _);
            workbench.SelectPipelineStep(step.Id);
            Check("recipe reopen cannot replay another edit session", !workbench.HasSelectedStepParameterHistory && !workbench.IsValidationSetRunning);
            Check("recipe reopen clears previous result comparison", !workbench.HasSelectedToolResultComparison);
            step = workbench.SelectedPipelineStep!;
            var reference = workbench.PlaneFlatnessReferenceSelection!;
            var originalInputs = step.InputEntityIds.ToArray();
            var originalRouting = step.DualRoiRouting;
            var otherStep = workbench.PipelineSteps.First(item => item.Id != step.Id);
            var otherInputs = otherStep.InputEntityIds.ToArray();
            var actionCount = workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            var replacement = reference with { GridRectangle = reference.GridRectangle! with { Column = reference.GridRectangle.Column + 1 } };
            workbench.UpdateTeachingGridRectangleDraft(replacement.GridRectangle);
            workbench.UpdateTeachingSelectionCaptureState(true, 2, 2, true, "Isolated ROI replacement");
            Check("ROI Apply records the same identity and actual coordinate change", workbench.TryApplyCapturedTeachingSelection(replacement, out _)
                && workbench.Selections.Single(selection => selection.Id == reference.Id).GridRectangle == replacement.GridRectangle
                && workbench.UndoSelectedStepParametersCommand.CanExecute(null) && workbench.SelectedStepLastAppliedChange.Contains("→"));
            workbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            Check("an unfinished ROI draft blocks Undo of applied changes", workbench.IsTeachingSelectionCaptureActive && !workbench.UndoSelectedStepParametersCommand.CanExecute(null));
            workbench.CancelTeachingSelectionCaptureCommand.Execute(null);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            Check("ROI Undo restores coordinates and ordered role routing without touching another step", workbench.Selections.Single(selection => selection.Id == reference.Id) == reference
                && step.InputEntityIds.SequenceEqual(originalInputs) && step.DualRoiRouting == originalRouting
                && otherStep.InputEntityIds.SequenceEqual(otherInputs) && !workbench.HasSelectedStepParameterHistory);
            Check("ROI Apply and Undo never execute and require an explicit rerun", actionCount == workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run")
                && workbench.IsDirty && !workbench.IsSelectedStepPreviewRunning && !workbench.IsValidationSetRunning);

            using (var routeWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "route-recent.json")))
            {
                routeWorkbench.SetC3DSource(workbench.Source.Path, markDirty: false);
                var filter = routeWorkbench.Tools.First(tool => tool.Id == "filter");
                routeWorkbench.AddSelectedToolCommand.Execute(filter);
                var firstFilter = routeWorkbench.SelectedPipelineStep!;
                routeWorkbench.AddSelectedToolCommand.Execute(filter);
                var secondFilter = routeWorkbench.SelectedPipelineStep!;
                // Auto-add requires a published detection input. The graph editor also supports an opened, valid declared route before explicit execution.
                var graphPath = Path.Combine(root, "graph-route.ov3d-recipe.json");
                if (!routeWorkbench.TrySaveTeachingRecipe(graphPath, out var graphSaveMessage)) throw new InvalidOperationException(graphSaveMessage);
                var graph = ToolRecipeDocumentStore.Load(graphPath);
                var connectedTool = routeWorkbench.Tools.First(tool => tool.Id == "connected-region");
                var connectedStep = new ToolRecipeStep("step.connected.01", connectedTool.Id, connectedTool.Name, connectedTool.MinimumInputCount,
                    [firstFilter.OutputEntityId], "output.connected.01", connectedTool.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.DefaultValue)).ToArray());
                ToolRecipeDocumentStore.Save(graphPath, graph with { Steps = graph.Steps.Append(connectedStep).ToArray() });
                if (!routeWorkbench.TryOpenTeachingRecipe(graphPath, out var graphOpenMessage)) throw new InvalidOperationException(graphOpenMessage);
                firstFilter = routeWorkbench.PipelineSteps.First(item => item.Id == firstFilter.Id);
                secondFilter = routeWorkbench.PipelineSteps.First(item => item.Id == secondFilter.Id);
                routeWorkbench.SelectPipelineStep(connectedStep.Id);
                var connected = routeWorkbench.SelectedPipelineStep!;
                var routeBefore = connected.InputEntityIds.ToArray();
                routeWorkbench.BeginFlowConnectionCommand.Execute(secondFilter.OutputEntityId);
                routeWorkbench.ConnectFlowOutputCommand.Execute(connected);
                lines.Add($"GraphConnection|steps={routeWorkbench.PipelineSteps.Count}|first={firstFilter.Id}|second={secondFilter.Id}|target={connected.Id}|route={connected.InputEntityIdsText}|message={routeWorkbench.FlowConnectionMessage}|history={routeWorkbench.HasSelectedStepParameterHistory}");
                Check("compatible graph connection records one applied change", connected.ToolId == "connected-region"
                    && connected.InputEntityIds.Contains(secondFilter.OutputEntityId) && routeWorkbench.UndoSelectedStepParametersCommand.CanExecute(null));
                routeWorkbench.UndoSelectedStepParametersCommand.Execute(null);
                Check("graph Undo restores the original input without executing downstream tools", connected.InputEntityIds.SequenceEqual(routeBefore)
                    && !routeWorkbench.HasSelectedStepParameterHistory && routeWorkbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")));
                routeWorkbench.BeginFlowConnectionCommand.Execute(connected.OutputEntityId);
                routeWorkbench.ConnectFlowOutputCommand.Execute(firstFilter);
                Check("rejected forward connection does not create Undo history", !routeWorkbench.HasSelectedStepParameterHistory
                    && firstFilter.InputEntityIds.Contains(routeWorkbench.Source.Id));
            }

            var savePath = Path.Combine(root, "definition-integration", "recipe.ov3d-recipe.json");
            workbench.ClearValidationSetCommand.Execute(null);
            Check("integration recipe saves through the normal workflow", workbench.TrySaveTeachingRecipe(savePath, out _));
            var savedRecipeBytes = File.ReadAllBytes(savePath);
            var savedRecipeHash = Convert.ToHexString(SHA256.HashData(savedRecipeBytes));
            workbench.SetValidationSetSources([workbench.Source.Path]);
            Check("sample-only changes are visible without condition dirtiness", workbench.IsValidationSetDefinitionDirty && !workbench.IsDirty
                && workbench.HeaderRecipeSaveStateText.Contains("샘플"));
            Check("normal sample-only Save preserves exact recipe bytes and saved identity", workbench.TrySaveTeachingRecipe(savePath, out _)
                && savedRecipeBytes.SequenceEqual(File.ReadAllBytes(savePath)) && !workbench.IsValidationSetDefinitionDirty && !workbench.IsDirty);
            var savedDefinition = ToolRecipeValidationSetDefinitionStore.LoadForRecipe(savePath)!;
            Check("definition captures actual saved recipe SHA and condition identity", savedDefinition.RecipeContentSha256 == savedRecipeHash
                && savedDefinition.CalculationContextSha256 is { Length: 64 });
            var definitionBytes = File.ReadAllBytes(ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(savePath));
            var baselineSample = workbench.ValidationSetSamples.Single();
            workbench.SelectedValidationSetSample = baselineSample;
            workbench.SetValidationSampleRoleCommand.Execute("Good");
            workbench.SetValidationSampleRoleCommand.Execute("Unset");
            lines.Add($"DefinitionRestore|saved={savedDefinition.Samples.Single()}|actual={workbench.ValidationSetSamples.Single()}|dirty={workbench.IsValidationSetDefinitionDirty}");
            Check("restoring saved expectation clears sample dirty state without another save", !workbench.IsValidationSetDefinitionDirty
                && savedRecipeBytes.SequenceEqual(File.ReadAllBytes(savePath)) && definitionBytes.SequenceEqual(File.ReadAllBytes(ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(savePath))));
            workbench.SetValidationSampleRoleCommand.Execute("Good");
            var sidecarPath = ToolRecipeValidationSetDefinitionStore.GetPathForRecipe(savePath);
            using (var locked = new FileStream(sidecarPath, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var savedWhileLocked = workbench.TrySaveTeachingRecipe(savePath, out var lockedSaveMessage);
                lines.Add($"LockedSave|saved={savedWhileLocked}|definitionDirty={workbench.IsValidationSetDefinitionDirty}|recipeDirty={workbench.IsDirty}|message={lockedSaveMessage}");
                Check("normal Save failure preserves definition edits and recipe bytes", !savedWhileLocked
                    && workbench.IsValidationSetDefinitionDirty && !workbench.IsDirty && savedRecipeBytes.SequenceEqual(File.ReadAllBytes(savePath)));
            }
            Check("normal Save retry recovers with the same recipe identity", workbench.TrySaveTeachingRecipe(savePath, out _)
                && !workbench.IsValidationSetDefinitionDirty && savedRecipeBytes.SequenceEqual(File.ReadAllBytes(savePath)));
            Check("reopen restores saved declarations without execution", workbench.TryOpenTeachingRecipe(savePath, out _)
                && workbench.ValidationSetSamples.Single().EffectiveExpectation == ToolRecipeValidationExpectation.Ok && !workbench.IsValidationSetRunning);
        }
        catch (Exception exception) { Check("execution: " + exception, false); }
        summary = $"ToolParameterUndo|pass={passed == total}|checks={passed}/{total}";
        File.WriteAllLines(reportPath, lines.Append(summary));
        return passed == total;
    }
}

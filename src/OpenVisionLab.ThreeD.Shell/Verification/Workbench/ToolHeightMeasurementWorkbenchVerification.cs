using System.IO;
using OpenVisionLab;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolHeightMeasurementWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "Generic height measurement Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", nameof(ToolHeightMeasurementWorkbenchVerification), Guid.NewGuid().ToString("N"));
        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++;
        }

        try
        {
            Directory.CreateDirectory(root);
            var sourcePath = Path.Combine(root, "measurement.C3D");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.measurement", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 17, 18, 19, 20, 18, 19, 20, 21]).SaveC3D(sourcePath);
            var recipePath = Path.Combine(root, "measurement.ov3d-recipe.json");
            var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            workbench.RecipeName = "Generic measurement recipe";
            workbench.SetC3DSource(sourcePath);
            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var thicknessReferenceSelection = new ToolRecipeSelection(
                "selection.reference-thickness", "Reference surface ROI", ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new ToolRecipeGridRectangle(0, 0, 2, 4), null, null);
            var selection = new ToolRecipeSelection(
                "selection.measurement", "Measurement ROI", ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new ToolRecipeGridRectangle(2, 0, 2, 4), null, null);
            workbench.Selections.Add(thicknessReferenceSelection);
            workbench.Selections.Add(selection);

            var thickness = Add(workbench, "Thickness", thicknessReferenceSelection.Id);
            Check("Thickness is a typed generic adapter", workbench.IsSelectedStepPropertyGridSupported && workbench.SelectedStepAdapterStatus == "Typed adapter ready", workbench.SelectedStepAdapterStatus);
            Check("Current one-ROI Thickness treats input 2 as Reference and enables Measurement capture",
                workbench.IsSelectedStepDualRoiMeasurement
                && workbench.PlaneFlatnessReferenceSelection?.Id == thicknessReferenceSelection.Id
                && workbench.PlaneFlatnessMeasurementSelection is null
                && workbench.HasDualRoiFirstSelection
                && !workbench.HasDualRoiSecondSelection
                && !workbench.HasCompleteDualRoiTeaching
                && workbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null)
                && workbench.CanSaveTeachingRecipe
                && !workbench.PreviewSelectedStepCommand.CanExecute(null)
                && workbench.MeasurementExecutionSummary.Contains("requires 3 input", StringComparison.OrdinalIgnoreCase),
                workbench.MeasurementExecutionSummary);
            var referenceOnlyPath = Path.Combine(root, "reference-only-thickness.ov3d-recipe.json");
            var referenceOnlySaved = workbench.TrySaveTeachingRecipe(referenceOnlyPath, out var referenceOnlySaveMessage);
            var referenceOnlyWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "reference-only-recent.json"));
            var referenceOnlyOpened = referenceOnlySaved
                && referenceOnlyWorkbench.TryOpenTeachingRecipe(referenceOnlyPath, out _);
            WaitForSourceQualityIdle(referenceOnlyWorkbench.SourceQuality);
            Check(
                "saved current-schema Reference-only Thickness reopens with Measurement ROI drawing enabled",
                referenceOnlyOpened
                && referenceOnlyWorkbench.PlaneFlatnessReferenceSelection is not null
                && referenceOnlyWorkbench.PlaneFlatnessMeasurementSelection is null
                && referenceOnlyWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null),
                referenceOnlyOpened ? referenceOnlyWorkbench.PlaneFlatnessMeasurementSummary : referenceOnlySaveMessage);
            workbench.SelectedCompatibleSelection = selection;
            workbench.ReusePlaneFlatnessMeasurementRoiCommand.Execute(null);
            Check("Teaching Measurement ROI completes the ordered dual-surface route",
                thickness.InputEntityIds.Count == 3
                && thickness.InputEntityIds[1] == thicknessReferenceSelection.Id
                && thickness.InputEntityIds[2] == selection.Id
                && workbench.HasCompleteDualRoiTeaching
                && workbench.CanSaveTeachingRecipe
                && workbench.PreviewSelectedStepCommand.CanExecute(null),
                string.Join(" -> ", thickness.InputEntityIds));

            var legacyRecipePath = Path.Combine(root, "legacy-one-roi-thickness.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(
                legacyRecipePath,
                new ToolRecipeDocument(
                    ToolRecipeDocument.GenericMeasurementSchemaVersion,
                    "Legacy one-ROI Thickness",
                    new ToolRecipeSource(
                        workbench.Source.Id, workbench.Source.Name, workbench.Source.Format,
                        workbench.Source.Unit, workbench.Source.FrameId, sourcePath,
                        new FileInfo(sourcePath).Length, binding.ContentSha256, binding.GridWidth, binding.GridHeight),
                    [],
                    [new ToolRecipeStep(
                        "step.legacy-thickness", "thickness", "Thickness", 3,
                        [workbench.Source.Id, selection.Id], "result.legacy-thickness",
                        [
                            new ToolRecipeParameter("MinimumThickness", "0"),
                            new ToolRecipeParameter("MaximumThickness", "100000"),
                            new ToolRecipeParameter("MinimumValidSampleCount", "1")
                        ])],
                    [thicknessReferenceSelection, selection]));
            var legacyWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-legacy.json"));
            var legacyOpened = legacyWorkbench.TryOpenTeachingRecipe(legacyRecipePath, out var legacyOpenMessage);
            legacyWorkbench.SelectPipelineStep("step.legacy-thickness");
            Check("Schema 1.2 one-ROI Thickness preserves its ROI as Measurement",
                legacyOpened
                && legacyWorkbench.PlaneFlatnessReferenceSelection is null
                && legacyWorkbench.PlaneFlatnessMeasurementSelection?.Id == selection.Id
                && legacyWorkbench.MeasurementExecutionSummary.Contains("legacy one-ROI Thickness", StringComparison.OrdinalIgnoreCase),
                $"opened={legacyOpened};message={legacyOpenMessage};schema={legacyWorkbench.RecipeSchemaVersion};" +
                $"reference={legacyWorkbench.PlaneFlatnessReferenceSelection?.Id ?? "(none)"};" +
                $"measurement={legacyWorkbench.PlaneFlatnessMeasurementSelection?.Id ?? "(none)"};" +
                $"summary={legacyWorkbench.MeasurementExecutionSummary}");
            legacyWorkbench.SelectedCompatibleSelection = legacyWorkbench.Selections.Single(item => item.Id == thicknessReferenceSelection.Id);
            legacyWorkbench.ReusePlaneFlatnessReferenceRoiCommand.Execute(null);
            Check("Teaching Reference upgrades legacy Thickness without losing Measurement",
                legacyWorkbench.RecipeSchemaVersion == ToolRecipeDocument.CurrentSchemaVersion
                && legacyWorkbench.SelectedPipelineStep?.InputEntityIds.SequenceEqual(
                    [workbench.Source.Id, thicknessReferenceSelection.Id, selection.Id],
                    StringComparer.OrdinalIgnoreCase) == true,
                string.Join(" -> ", legacyWorkbench.SelectedPipelineStep?.InputEntityIds ?? []));
            Check("Viewer selection synchronizes the active Thickness ROI role",
                workbench.SelectPipelineStepForSelection(thicknessReferenceSelection.Id)
                && workbench.IsPlaneFlatnessReferenceRoleActive
                && workbench.SelectPipelineStepForSelection(selection.Id)
                && workbench.IsPlaneFlatnessMeasurementRoleActive,
                $"referenceActive={workbench.IsPlaneFlatnessReferenceRoleActive}; measurementActive={workbench.IsPlaneFlatnessMeasurementRoleActive}");
            var originalThicknessLanguage = OpenVisionLanguageService.CurrentLanguage;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishThicknessGuide = (
                workbench.SelectedStepSelectionRequirementTitle,
                workbench.ThicknessRoiTeachingDetail,
                workbench.SelectionCaptureActionText);
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanThicknessGuide = (
                workbench.SelectedStepSelectionRequirementTitle,
                workbench.ThicknessRoiTeachingDetail,
                workbench.SelectionCaptureActionText);
            OpenVisionLanguageService.SetLanguage(originalThicknessLanguage, save: false);
            Check(
                "Thickness ROI teaching is explicit and independently localized",
                englishThicknessGuide.Item1 == "Measurement ROI \u00B7 2 grid corners"
                && englishThicknessGuide.Item2.Contains("H-axis separation", StringComparison.Ordinal)
                && englishThicknessGuide.Item3 == "Replace ROI"
                && koreanThicknessGuide.Item1 == "\uCE21\uC815 ROI \u00B7 3D \uADF8\uB9AC\uB4DC \uBAA8\uC11C\uB9AC 2\uAC1C"
                && koreanThicknessGuide.Item2.Contains("H\uCD95 \uAC70\uB9AC", StringComparison.Ordinal)
                && koreanThicknessGuide.Item3 == "ROI \uAD50\uCCB4",
                $"en={englishThicknessGuide}; ko={koreanThicknessGuide}");
            var thicknessPreview = workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
            Check("Thickness Preview produces dual-surface H-axis evidence",
                thicknessPreview
                && workbench.MeasurementEvidenceSummary.Contains("H-axis thickness", StringComparison.Ordinal)
                && workbench.CurrentMeasurementOutput?.Result.Metrics.Single(metric => metric.Name == "Mean").Value is double mean
                && Math.Abs(mean - 5d) <= 1e-9,
                workbench.MeasurementEvidenceSummary);
            var selectedThicknessOutput = workbench.SelectedToolWorkspace.Outputs.Single();
            Check(
                "Selected Tool exposes Thickness value, unit, decision, and evidence-only action boundary",
                selectedThicknessOutput.ValueLabel == "Mean"
                && selectedThicknessOutput.Value == "5"
                && selectedThicknessOutput.Unit == workbench.CurrentMeasurementOutput?.Unit
                && selectedThicknessOutput.ResultStatus == "Pass"
                && selectedThicknessOutput.Detail.Contains("H-axis thickness mean 5", StringComparison.Ordinal)
                && !selectedThicknessOutput.CanShowInViewer
                && !selectedThicknessOutput.CanPinToCompare
                && !selectedThicknessOutput.CanCompare
                && selectedThicknessOutput.Availability == workbench.Localization.EvidenceOnlyOutput
                && !workbench.ShowWorkspaceOutputCommand.CanExecute(selectedThicknessOutput)
                && !workbench.PinWorkspaceOutputCommand.CanExecute(selectedThicknessOutput)
                && !workbench.CompareWorkspaceOutputCommand.CanExecute(selectedThicknessOutput),
                $"value={selectedThicknessOutput.Value} {selectedThicknessOutput.Unit};status={selectedThicknessOutput.ResultStatus};availability={selectedThicknessOutput.Availability}");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Thickness publishes exact Preview", thickness.State == "Published" && workbench.IsMeasurementPreviewPublished, thickness.State);

            ToolWorkbenchTeachingCaptureRequestEventArgs? thicknessReplaceRequest = null;
            ToolWorkbenchGridRectangleDraftChangedEventArgs? thicknessDraft = null;
            var thicknessDraftEventCount = 0;
            workbench.BeginTeachingSelectionCaptureRequested += (_, args) => thicknessReplaceRequest = args;
            workbench.TeachingGridRectangleDraftChanged += (_, args) =>
            {
                thicknessDraft = args;
                thicknessDraftEventCount++;
            };
            workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            Check(
                "Thickness replacement request preserves the authored ROI identity and geometry",
                thicknessReplaceRequest?.ExistingSelection == selection
                && thicknessReplaceRequest.SelectionId == selection.Id
                && workbench.IsTeachingGridRectangleEditorVisible
                && workbench.TeachingGridRectangleRowCount == 2
                && workbench.TeachingGridRectangleColumnCount == 4,
                thicknessReplaceRequest?.ExistingSelection?.GridRectangle?.ToString() ?? "no existing ROI");
            workbench.UpdateTeachingSelectionCaptureState(
                active: true,
                capturedPointCount: 2,
                requiredPointCount: 2,
                canApply: true,
                message: "Seeded replacement candidate.");
            Check(
                "ready GridRectangle capture switches from drawing to explicit review mode",
                workbench.TeachingSelectionCaptureProgress == workbench.Localization.RoiCaptureReadyProgress
                && workbench.TeachingSelectionCaptureInstruction == workbench.Localization.RoiCaptureReadyInstruction
                && workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null),
                $"{workbench.TeachingSelectionCaptureProgress} | {workbench.TeachingSelectionCaptureInstruction}");
            workbench.TeachingGridRectangleRowCount = 2;
            workbench.TeachingGridRectangleRow = 1;
            Check(
                "numeric Surface ROI edits raise only a valid transient candidate",
                thicknessDraft?.Rectangle == new ToolRecipeGridRectangle(1, 0, 2, 4)
                && workbench.Selections.Single(item => item.Id == selection.Id).GridRectangle == selection.GridRectangle
                && workbench.IsTeachingGridRectangleDraftValid
                && workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null),
                thicknessDraft?.Rectangle.ToString() ?? "no draft");
            var validDraftEventCount = thicknessDraftEventCount;
            workbench.TeachingGridRectangleRow = 3;
            Check(
                "out-of-grid numeric ROI is blocked before Apply",
                !workbench.IsTeachingGridRectangleDraftValid
                && !workbench.ApplyTeachingSelectionCaptureCommand.CanExecute(null)
                && thicknessDraftEventCount == validDraftEventCount,
                workbench.TeachingGridRectangleValidationSummary);
            workbench.TeachingGridRectangleRow = 2;
            var editedThicknessSelection = thicknessReplaceRequest!.ExistingSelection! with
            {
                GridRectangle = thicknessDraft!.Rectangle
            };
            Check(
                "explicit Apply replaces the same selection once without invoking inspection",
                workbench.TryApplyCapturedTeachingSelection(editedThicknessSelection, out var editMessage)
                && workbench.Selections.Count == 2
                && workbench.Selections.Single(item => item.Id == selection.Id).GridRectangle == new ToolRecipeGridRectangle(2, 0, 2, 4)
                && !workbench.IsTeachingSelectionCaptureActive,
                editMessage);

            var warpage = Add(workbench, "Warpage", selection.Id);
            Check("Warpage is another tool step, not a workspace mode", workbench.IsSelectedStepWarpage && workbench.IsSelectedStepPropertyGridSupported, workbench.SelectedPipelineStepTitle);
            var warpagePreview = workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
            Check("Warpage Preview produces P2V evidence", warpagePreview && workbench.MeasurementEvidenceSummary.Contains("P2V", StringComparison.Ordinal), workbench.MeasurementEvidenceSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Warpage publishes exact Preview", warpage.State == "Published" && workbench.IsMeasurementPreviewPublished, warpage.State);

            var planeWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-plane.json"));
            planeWorkbench.SetC3DSource(sourcePath);
            PrepareTransformedHeightFieldRoute(planeWorkbench);
            var referenceSelection = new ToolRecipeSelection(
                "selection.reference", "Reference ROI", ToolRecipeSelectionKinds.GridRectangle,
                planeWorkbench.Source.Id, planeWorkbench.Source.FrameId, binding,
                new ToolRecipeGridRectangle(0, 0, 2, 2), null, null);
            var measurementSelection = new ToolRecipeSelection(
                "selection.measurement-plane", "Measurement ROI", ToolRecipeSelectionKinds.GridRectangle,
                planeWorkbench.Source.Id, planeWorkbench.Source.FrameId, binding,
                new ToolRecipeGridRectangle(2, 2, 2, 2), null, null);
            var replacementReferenceSelection = new ToolRecipeSelection(
                "selection.reference-replacement", "Replacement reference ROI", ToolRecipeSelectionKinds.GridRectangle,
                planeWorkbench.Source.Id, planeWorkbench.Source.FrameId, binding,
                new ToolRecipeGridRectangle(0, 1, 2, 2), null, null);
            planeWorkbench.Selections.Add(referenceSelection);
            planeWorkbench.Selections.Add(measurementSelection);
            planeWorkbench.Selections.Add(replacementReferenceSelection);
            planeWorkbench.SelectedTool = planeWorkbench.Tools.Single(tool => tool.Id == "plane-flatness");
            planeWorkbench.AddSelectedToolCommand.Execute(null);
            Check("Plane Flatness is a generic three-input Measure tool",
                planeWorkbench.SelectedPipelineStep is { ToolId: "plane-flatness", MinimumInputCount: 3 }
                && planeWorkbench.IsSelectedStepPlaneFlatness
                && planeWorkbench.IsSelectedStepPropertyGridSupported
                && planeWorkbench.SelectedStepPropertyDraft is PlaneFlatnessStepProperties,
                planeWorkbench.SelectedStepAdapterStatus);
            var planeStep = planeWorkbench.SelectedPipelineStep!;
            // This fixture exercises legacy source-bound ROI ordering. The shared
            // route suite separately proves that new Plane Flatness insertion
            // requires a TransformedHeightField and opens legacy repair directly.
            planeStep.InputEntityIdsText = planeWorkbench.Source.Id;
            var originalOutputId = planeStep.OutputEntityId;
            Check("Plane Flatness starts at Reference ROI and blocks Measurement ROI",
                planeWorkbench.IsPlaneFlatnessReferenceRoleActive
                && planeWorkbench.CapturePlaneFlatnessReferenceRoiCommand.CanExecute(null)
                && !planeWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null)
                && !planeWorkbench.HasCurrentMeasurementPreview,
                $"reference={planeWorkbench.PlaneFlatnessReferenceState}; measurement={planeWorkbench.PlaneFlatnessMeasurementState}");

            planeWorkbench.SelectedCompatibleSelection = referenceSelection;
            planeWorkbench.ReusePlaneFlatnessReferenceRoiCommand.Execute(null);
            Check("Reference ROI reuse routes input 2 and advances to Measurement ROI",
                planeStep.InputEntityIds.Count == 2
                && string.Equals(planeStep.InputEntityIds[1], referenceSelection.Id, StringComparison.Ordinal)
                && planeWorkbench.IsPlaneFlatnessMeasurementRoleActive
                && planeWorkbench.CanTeachPlaneFlatnessMeasurementRoi,
                string.Join(" -> ", planeStep.InputEntityIds));

            planeWorkbench.SelectedCompatibleSelection = measurementSelection;
            planeWorkbench.ReusePlaneFlatnessMeasurementRoiCommand.Execute(null);
            Check("Measurement ROI reuse completes the ordered three-input route",
                planeStep.InputEntityIds.Count == 3
                && string.Equals(planeStep.InputEntityIds[1], referenceSelection.Id, StringComparison.Ordinal)
                && string.Equals(planeStep.InputEntityIds[2], measurementSelection.Id, StringComparison.Ordinal)
                && planeWorkbench.PlaneFlatnessReferenceSelection is not null
                && planeWorkbench.PlaneFlatnessMeasurementSelection is not null,
                string.Join(" -> ", planeStep.InputEntityIds));

            planeWorkbench.SelectedCompatibleSelection = replacementReferenceSelection;
            planeWorkbench.ReusePlaneFlatnessReferenceRoiCommand.Execute(null);
            Check("Reference ROI replacement preserves role order and Measurement ROI",
                planeStep.InputEntityIds.Count == 3
                && string.Equals(planeStep.InputEntityIds[1], replacementReferenceSelection.Id, StringComparison.Ordinal)
                && string.Equals(planeStep.InputEntityIds[2], measurementSelection.Id, StringComparison.Ordinal),
                string.Join(" -> ", planeStep.InputEntityIds));
            Check("ROI teaching never runs Preview or mutates the declared output",
                !planeWorkbench.HasCurrentMeasurementPreview
                && string.Equals(planeStep.OutputEntityId, originalOutputId, StringComparison.Ordinal),
                $"preview={planeWorkbench.HasCurrentMeasurementPreview}; output={planeStep.OutputEntityId}");

            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishTeachingTitle = planeWorkbench.Localization.PlaneFlatnessRoiTeaching;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanTeachingTitle = planeWorkbench.Localization.PlaneFlatnessRoiTeaching;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check("Plane Flatness teaching labels are distinct in English and Korean",
                englishTeachingTitle == "Plane Flatness ROI teaching order"
                && koreanTeachingTitle == "평면도 ROI 티칭 순서",
                $"en={englishTeachingTitle}; ko={koreanTeachingTitle}");

            var volumeWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-volume.json"));
            volumeWorkbench.SetC3DSource(sourcePath);
            PrepareTransformedHeightFieldRoute(volumeWorkbench);
            volumeWorkbench.SelectedTool = volumeWorkbench.Tools.Single(tool => tool.Id == "volume");
            volumeWorkbench.AddSelectedToolCommand.Execute(null);
            Check("Volume is a generic dual-ROI Measure tool with typed WPG parameters",
                volumeWorkbench.SelectedPipelineStep is { ToolId: "volume", MinimumInputCount: 3 }
                && volumeWorkbench.IsSelectedStepVolume
                && volumeWorkbench.IsSelectedStepDualRoiMeasurement
                && volumeWorkbench.IsSelectedStepPropertyGridSupported
                && volumeWorkbench.SelectedStepPropertyDraft is VolumeStepProperties,
                volumeWorkbench.SelectedStepAdapterStatus);
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishVolumeTitle = volumeWorkbench.DualRoiTeachingTitle;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanVolumeTitle = volumeWorkbench.DualRoiTeachingTitle;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check("Volume ROI teaching title is localized independently in English and Korean",
                englishVolumeTitle == "Volume ROI teaching order"
                && koreanVolumeTitle == "\uCCB4\uC801 ROI \uD2F0\uCE6D \uC21C\uC11C",
                $"en={englishVolumeTitle}; ko={koreanVolumeTitle}");

            var crossSectionWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-cross-section.json"));
            crossSectionWorkbench.SetC3DSource(sourcePath);
            PrepareTransformedHeightFieldRoute(crossSectionWorkbench);
            crossSectionWorkbench.SelectedTool = crossSectionWorkbench.Tools.Single(tool => tool.Id == "cross-section-dimensions");
            crossSectionWorkbench.AddSelectedToolCommand.Execute(null);
            Check("Cross-section Dimensions is a generic single-row Measure tool with typed WPG parameters",
                crossSectionWorkbench.SelectedPipelineStep is { ToolId: "cross-section-dimensions", MinimumInputCount: 2 }
                && crossSectionWorkbench.IsSelectedStepCrossSectionDimensions
                && crossSectionWorkbench.IsSelectedStepMeasurement
                && crossSectionWorkbench.IsSelectedStepPropertyGridSupported
                && crossSectionWorkbench.SelectedStepPropertyDraft is CrossSectionDimensionsStepProperties
                && crossSectionWorkbench.SelectedStepSelectionRequirement is { Kind: ToolRecipeSelectionKinds.GridRectangle, RequiredPointCount: 2 },
                crossSectionWorkbench.SelectedStepAdapterStatus);
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.English, save: false);
            var englishCrossSectionTitle = crossSectionWorkbench.SelectedStepSelectionRequirement?.Name;
            OpenVisionLanguageService.SetLanguage(OpenVisionLanguage.Korean, save: false);
            var koreanCrossSectionTitle = crossSectionWorkbench.SelectedStepSelectionRequirement?.Name;
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check("Cross-section row selection is localized independently in English and Korean",
                englishCrossSectionTitle == "Cross-section row segment"
                && koreanCrossSectionTitle == "\uB2E8\uBA74 \uD589 \uAD6C\uAC04",
                $"en={englishCrossSectionTitle}; ko={koreanCrossSectionTitle}");

            var completenessWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(root, "recent-completeness.json"));
            completenessWorkbench.SetC3DSource(sourcePath);
            var completenessReference = thicknessReferenceSelection with
            {
                Id = "selection.completeness-reference",
                Name = "Completeness reference"
            };
            var completenessGrid = selection with
            {
                Id = "selection.completeness-grid",
                Name = "Completeness inspection grid",
                GridRectangle = new ToolRecipeGridRectangle(0, 0, 4, 4)
            };
            completenessWorkbench.Selections.Add(completenessReference);
            completenessWorkbench.Selections.Add(completenessGrid);
            completenessWorkbench.SelectedTool = completenessWorkbench.Tools.Single(
                tool => tool.Id == "completeness-grid");
            var completenessAddReady = WaitForCanExecute(
                completenessWorkbench,
                null);
            if (completenessAddReady)
            {
                completenessWorkbench.AddSelectedToolCommand.Execute(null);
            }

            var completenessStep = completenessWorkbench.SelectedPipelineStep;
            Check(
                "Completeness Grid is a typed dual-ROI Measure tool",
                completenessStep is
                {
                    ToolId: "completeness-grid",
                    MinimumInputCount: 3
                }
                && completenessWorkbench.IsSelectedStepCompletenessGrid
                && completenessWorkbench.IsSelectedStepDualRoiMeasurement
                && completenessWorkbench.IsSelectedStepPropertyGridSupported
                && completenessWorkbench.SelectedStepPropertyDraft
                    is CompletenessGridStepProperties,
                $"{completenessWorkbench.SelectedStepAdapterStatus};" +
                $"sourceReady={completenessWorkbench.IsSourceReadyForRecipe};" +
                $"source={completenessWorkbench.Source.Path};" +
                $"selectedTool={completenessWorkbench.SelectedTool?.Id};" +
                $"canAddBefore={completenessAddReady};" +
                $"canAdd={completenessWorkbench.AddSelectedToolCommand.CanExecute(null)};" +
                $"route={completenessWorkbench.SelectedToolProposedRouteDetail};" +
                $"pipelineCount={completenessWorkbench.PipelineSteps.Count};" +
                $"pipeline={string.Join(",", completenessWorkbench.PipelineSteps.Select(item => $"{item.Id}:{item.ToolId}"))};" +
                $"artifacts={string.Join(",", completenessWorkbench.ArtifactRegistry.Select(item => $"{item.Id}:{item.Contract}:{item.State}"))}");
            if (completenessStep is null)
            {
                throw new InvalidOperationException(
                    "Completeness Grid was not added after its command became ready.");
            }
            completenessWorkbench.SelectedCompatibleSelection =
                completenessReference;
            completenessWorkbench.ReusePlaneFlatnessReferenceRoiCommand.Execute(
                null);
            completenessWorkbench.SelectedCompatibleSelection = completenessGrid;
            completenessWorkbench
                .ReusePlaneFlatnessMeasurementRoiCommand.Execute(null);
            Check(
                "Completeness roles preserve Reference then Inspection Grid order",
                completenessStep.InputEntityIds.SequenceEqual(
                    [
                        completenessWorkbench.Source.Id,
                        completenessReference.Id,
                        completenessGrid.Id
                    ])
                && completenessWorkbench.DualRoiSecondLabel
                    == completenessWorkbench.Localization.InspectionGridRoi,
                string.Join(" -> ", completenessStep.InputEntityIds));
            var completenessPreview = completenessWorkbench
                .PreviewSelectedMeasurementAsync()
                .GetAwaiter()
                .GetResult();
            Check(
                "Completeness Preview exposes typed cell, aggregate, and Height Image overlay evidence",
                completenessPreview
                && completenessWorkbench.CurrentMeasurementOutput
                    ?.CompletenessGrid is
                    {
                        Cells.Count: 4,
                        Profile.Rows: 2,
                        Profile.Columns: 2
                    } completenessOutput
                && completenessOutput.Cells.All(cell =>
                    cell.FiniteCoverageRatio == 1d)
                && completenessOutput.Cells.All(cell =>
                    cell.Decision == ResultStatus.Pass)
                && completenessOutput is
                {
                    PassedCellCount: 4,
                    FailedCellCount: 0,
                    AggregateStatus: ResultStatus.Pass,
                    CellOverlays.Count: 4
                }
                && completenessWorkbench.CurrentMeasurementOutput.Result.Status
                    == ResultStatus.Pass
                && completenessWorkbench.MeasurementEvidenceSummary.Contains(
                    "pass 4 | fail 0 | aggregate Pass",
                    StringComparison.Ordinal)
                && completenessWorkbench.HeightImageViewer
                    .CompletenessCellOverlays.Count == 4,
                completenessWorkbench.MeasurementEvidenceSummary);
            Check(
                "All-pass Completeness review selects a visible cell but disables failed-cell navigation",
                completenessWorkbench.HasCompletenessCellResults
                && completenessWorkbench.CompletenessCellResults.Count == 4
                && completenessWorkbench.SelectedCompletenessCellId
                    == completenessWorkbench.CompletenessCellResults[0].CellId
                && completenessWorkbench.HeightImageViewer
                    .SelectedCompletenessCellId
                    == completenessWorkbench.SelectedCompletenessCellId
                && !completenessWorkbench.CanNavigateCompletenessFailures
                && !completenessWorkbench.PreviousCompletenessFailureCommand
                    .CanExecute(null)
                && !completenessWorkbench.NextCompletenessFailureCommand
                    .CanExecute(null),
                completenessWorkbench.CompletenessFailureNavigationSummary);
            completenessWorkbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Completeness publishes the exact Preview identity",
                completenessStep.State == "Published"
                && completenessWorkbench.IsMeasurementPreviewPublished
                && completenessWorkbench.CurrentMeasurementOutput
                    ?.ContentSha256
                    == completenessWorkbench.CurrentMeasurementOutput
                        ?.CompletenessGrid?.ContentSha256,
                completenessWorkbench.CurrentMeasurementOutput
                    ?.ContentSha256
                    ?? "no output");

            completenessStep.Parameters.Single(parameter =>
                parameter.Name == "MinimumReferenceRelativeMeanRawHeight").Value = "-2";
            completenessStep.Parameters.Single(parameter =>
                parameter.Name == "MaximumReferenceRelativeMeanRawHeight").Value = "2";
            var mixedPreview = completenessWorkbench
                .PreviewSelectedMeasurementAsync()
                .GetAwaiter()
                .GetResult();
            var mixedOutputSha =
                completenessWorkbench.CurrentMeasurementOutput?.ContentSha256;
            var mixedRecipeDirty = completenessWorkbench.IsDirty;
            var mixedStepId = completenessStep.Id;
            var mixedOutputId = completenessStep.OutputEntityId;
            var firstFailedCell = completenessWorkbench
                .CompletenessCellResults
                .FirstOrDefault(item => item.Status == ResultStatus.Fail);
            completenessWorkbench.NextCompletenessFailureCommand.Execute(null);
            var secondFailedCellId =
                completenessWorkbench.SelectedCompletenessCellId;
            completenessWorkbench.NextCompletenessFailureCommand.Execute(null);
            var wrappedFailedCellId =
                completenessWorkbench.SelectedCompletenessCellId;
            completenessWorkbench.PreviousCompletenessFailureCommand.Execute(
                null);
            Check(
                "Failed-cell Previous/Next navigation is row-major, wraps, and synchronizes Height Image selection",
                mixedPreview
                && completenessWorkbench.CurrentMeasurementOutput
                    ?.CompletenessGrid is
                    {
                        PassedCellCount: 2,
                        FailedCellCount: 2,
                        AggregateStatus: ResultStatus.Fail
                    }
                && firstFailedCell is not null
                && firstFailedCell.CellId == "r002.c001"
                && secondFailedCellId == "r002.c002"
                && wrappedFailedCellId == "r002.c001"
                && completenessWorkbench.SelectedCompletenessCellId == "r002.c002"
                && completenessWorkbench.HeightImageViewer
                    .SelectedCompletenessCellId == "r002.c002",
                $"{completenessWorkbench.CompletenessFailureNavigationSummary}; selected={completenessWorkbench.SelectedCompletenessCellId}");
            Check(
                "Failed-cell review is presentation-only",
                completenessWorkbench.IsDirty == mixedRecipeDirty
                && completenessStep.Id == mixedStepId
                && completenessStep.OutputEntityId == mixedOutputId
                && completenessWorkbench.CurrentMeasurementOutput
                    ?.ContentSha256 == mixedOutputSha,
                $"dirty={completenessWorkbench.IsDirty}; step={completenessStep.Id}; output={completenessStep.OutputEntityId}; sha={mixedOutputSha}");

            var thicknessTool = completenessWorkbench.Tools.Single(
                tool => tool.Id == "thickness");
            var tabSteps = Enumerable.Range(1, 8)
                .Select(number => new ToolWorkbenchPipelineStepItem(
                    $"step.tab-{number}",
                    thicknessTool,
                    completenessWorkbench.Source.Id,
                    $"output.tab-{number}",
                    toolName: $"Tab {number} Thickness"))
                .ToArray();
            var tabBefore = tabSteps
                .Select(step => (step.Id, step.ToolName, step.OutputEntityId))
                .ToArray();
            var tabMap =
                ToolWorkbenchViewModel.CreateTabThicknessIdentityMap(tabSteps);
            var tabAfter = tabSteps
                .Select(step => (step.Id, step.ToolName, step.OutputEntityId))
                .ToArray();
            Check(
                "Tab 1..8 Thickness names map row-major to cell presentation with stable step and output identities",
                tabMap.Count == 8
                && Enumerable.Range(1, 8).All(number =>
                    tabMap[number].DisplayName == $"Tab {number} Thickness"
                    && tabMap[number].StepId == $"step.tab-{number}"
                    && tabMap[number].OutputEntityId == $"output.tab-{number}")
                && tabBefore.SequenceEqual(tabAfter),
                string.Join(
                    "; ",
                    tabMap.OrderBy(pair => pair.Key)
                        .Select(pair =>
                            $"{pair.Key}:{pair.Value.StepId}->{pair.Value.OutputEntityId}")));

            var captureRecipePath = Path.Combine(root, "captured-plane.ov3d-recipe.json");
            var captureWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-captured-plane.json"));
            captureWorkbench.SetC3DSource(sourcePath);
            PrepareTransformedHeightFieldRoute(captureWorkbench);
            captureWorkbench.SelectedTool = captureWorkbench.Tools.Single(tool => tool.Id == "plane-flatness");
            captureWorkbench.AddSelectedToolCommand.Execute(null);
            var captureStep = captureWorkbench.SelectedPipelineStep!;
            // Keep this legacy-route fixture focused on ROI command plumbing; typed-route
            // prevention and repair are verified separately by the recipe-teaching suite.
            captureStep.InputEntityIdsText = captureWorkbench.Source.Id;
            ToolWorkbenchTeachingCaptureRequestEventArgs? request = null;
            captureWorkbench.BeginTeachingSelectionCaptureRequested += (_, args) => request = args;

            captureWorkbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            Check("Reference ROI command raises a role-specific Viewer capture request",
                request is { Kind: ToolRecipeSelectionKinds.GridRectangle, RequiredPointCount: 2 }
                && request.SelectionId.EndsWith(".reference-roi", StringComparison.Ordinal)
                && request.SelectionName.Contains(captureWorkbench.Localization.ReferenceRoi, StringComparison.Ordinal),
                request is null ? "no request" : $"{request.SelectionId}; {request.SelectionName}");
            var capturedReference = CapturedRectangle(request!, new ToolRecipeGridRectangle(0, 0, 2, 2));
            Check("Shell applies the Viewer-shaped Reference ROI candidate",
                capturedReference.GridRectangle == new ToolRecipeGridRectangle(0, 0, 2, 2)
                && captureWorkbench.TryApplyCapturedTeachingSelection(capturedReference, out _)
                && captureWorkbench.PlaneFlatnessReferenceSelection?.Id == request!.SelectionId
                && captureWorkbench.IsPlaneFlatnessMeasurementRoleActive
                && captureWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null),
                capturedReference.Id);

            request = null;
            captureWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
            Check("Measurement ROI command raises a distinct Viewer capture request after Reference",
                request is { Kind: ToolRecipeSelectionKinds.GridRectangle, RequiredPointCount: 2 }
                && request.SelectionId.EndsWith(".measurement-roi", StringComparison.Ordinal)
                && !string.Equals(request.SelectionId, capturedReference.Id, StringComparison.Ordinal),
                request?.SelectionId ?? "no request");
            var capturedMeasurement = CapturedRectangle(request!, new ToolRecipeGridRectangle(2, 2, 2, 2));
            Check("Shell applies the Viewer-shaped Measurement ROI candidate in input 3",
                capturedMeasurement.GridRectangle == new ToolRecipeGridRectangle(2, 2, 2, 2)
                && captureWorkbench.TryApplyCapturedTeachingSelection(capturedMeasurement, out _)
                && captureStep.InputEntityIds.Count == 3
                && captureStep.InputEntityIds[1] == capturedReference.Id
                && captureStep.InputEntityIds[2] == capturedMeasurement.Id,
                capturedMeasurement.Id);

            var initialMeasurementId = capturedMeasurement.Id;
            request = null;
            captureWorkbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            var replacementReference = CapturedRectangle(request!, new ToolRecipeGridRectangle(0, 1, 2, 2));
            var replacementApplied = captureWorkbench.TryApplyCapturedTeachingSelection(replacementReference, out var replacementMessage);
            Check("Reference ROI recapture replaces the same identity and preserves Measurement ROI",
                replacementApplied
                && replacementReference.Id == capturedReference.Id
                && replacementReference.GridRectangle == new ToolRecipeGridRectangle(0, 1, 2, 2)
                && captureWorkbench.Selections.Count == 2
                && captureStep.InputEntityIds[2] == initialMeasurementId,
                replacementMessage);
            var rawCaptureSaved = captureWorkbench.TrySaveTeachingRecipe(captureRecipePath, out var rawCaptureSaveMessage);
            Check("Workbench keeps raw-C3D Plane Flatness as a repairable non-executable draft",
                !captureWorkbench.HasCurrentMeasurementPreview
                && rawCaptureSaved
                && captureWorkbench.ValidationMessages.Any(item => item.Message.Contains(
                    "TransformedHeightField is required",
                    StringComparison.Ordinal))
                && captureWorkbench.FlowPortDiagnostics.Any(item =>
                    ReferenceEquals(item.Step, captureStep)
                    && item.Status == captureWorkbench.Localization.FlowPortIncompatible),
                rawCaptureSaveMessage);

            Check("Measurement ROI exposes a direct Delete command",
                captureWorkbench.RemovePlaneFlatnessMeasurementRoiCommand.CanExecute(null),
                captureWorkbench.PlaneFlatnessMeasurementSummary);
            captureWorkbench.RemovePlaneFlatnessMeasurementRoiCommand.Execute(null);
            Check("Deleting Measurement ROI retains Reference and re-enables Measurement capture",
                captureStep.InputEntityIds.Count == 2
                && captureStep.InputEntityIds[1] == replacementReference.Id
                && captureWorkbench.PlaneFlatnessReferenceSelection?.Id == replacementReference.Id
                && captureWorkbench.PlaneFlatnessMeasurementSelection is null
                && captureWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null)
                && captureWorkbench.CanSaveTeachingRecipe,
                string.Join(" -> ", captureStep.InputEntityIds));

            request = null;
            captureWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.Execute(null);
            var recapturedMeasurement = CapturedRectangle(request!, new ToolRecipeGridRectangle(2, 2, 2, 2));
            Check("Measurement ROI can be created again after Delete",
                captureWorkbench.TryApplyCapturedTeachingSelection(recapturedMeasurement, out var recaptureMessage)
                && captureStep.InputEntityIds.Count == 3
                && captureStep.InputEntityIds[2] == recapturedMeasurement.Id,
                recaptureMessage);

            Check("Reference ROI exposes a direct Delete command",
                captureWorkbench.RemovePlaneFlatnessReferenceRoiCommand.CanExecute(null),
                captureWorkbench.PlaneFlatnessReferenceSummary);
            captureWorkbench.RemovePlaneFlatnessReferenceRoiCommand.Execute(null);
            var incompleteRecipePath = Path.Combine(root, "incomplete-plane-draft.ov3d-recipe.json");
            var incompleteSaved = captureWorkbench.TrySaveTeachingRecipe(incompleteRecipePath, out var incompleteSaveMessage);
            var incompleteReopened = incompleteSaved
                ? ToolRecipeDocumentStore.Load(incompleteRecipePath)
                : null;
            Check("Deleting Reference preserves the Measurement role and saves an incomplete draft",
                captureStep.InputEntityIds.Count == 2
                && captureStep.InputEntityIds[1] == recapturedMeasurement.Id
                && captureWorkbench.PlaneFlatnessReferenceSelection is null
                && captureWorkbench.PlaneFlatnessMeasurementSelection?.Id == recapturedMeasurement.Id
                && captureWorkbench.Selections.Any(item => item.Id == recapturedMeasurement.Id)
                && !captureWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null)
                && incompleteSaved
                && incompleteReopened?.SchemaVersion == ToolRecipeDocument.DualRoiRoutingSchemaVersion
                && incompleteReopened.Steps.Single(step => step.ToolId == "plane-flatness").InputEntityIds.Count == 2
                && incompleteReopened.Steps.Single(step => step.ToolId == "plane-flatness").DualRoiRouting
                    == new ToolRecipeDualRoiRouting(null, recapturedMeasurement.Id),
                $"save={incompleteSaved};message={incompleteSaveMessage};" +
                $"route={string.Join(" -> ", captureStep.InputEntityIds)};" +
                $"reference={captureWorkbench.PlaneFlatnessReferenceSelection?.Id ?? "(none)"};" +
                $"measurement={captureWorkbench.PlaneFlatnessMeasurementSelection?.Id ?? "(none)"};" +
                $"captureMeasurement={captureWorkbench.CapturePlaneFlatnessMeasurementRoiCommand.CanExecute(null)};" +
                $"reopenedSchema={incompleteReopened?.SchemaVersion ?? "(none)"};" +
                $"reopenedRoute={string.Join(" -> ", incompleteReopened?.Steps.SingleOrDefault(step => step.ToolId == "plane-flatness")?.InputEntityIds ?? [])};" +
                $"reopenedDual={incompleteReopened?.Steps.SingleOrDefault(step => step.ToolId == "plane-flatness")?.DualRoiRouting}");

            request = null;
            captureWorkbench.CapturePlaneFlatnessReferenceRoiCommand.Execute(null);
            var restoredReference = CapturedRectangle(request!, new ToolRecipeGridRectangle(0, 1, 2, 2));
            Check("Redrawing deleted Reference restores the ordered dual-ROI route",
                captureWorkbench.TryApplyCapturedTeachingSelection(restoredReference, out var restoredReferenceMessage)
                && captureStep.InputEntityIds.Count == 3
                && captureStep.InputEntityIds[1] == restoredReference.Id
                && captureStep.InputEntityIds[2] == recapturedMeasurement.Id
                && captureStep.DualRoiRouting
                    == new ToolRecipeDualRoiRouting(restoredReference.Id, recapturedMeasurement.Id),
                restoredReferenceMessage);

            const string transformedId = "derived.transformed-height-field.verification";
            var transformedBinding = new ToolRecipeSelectionSourceBinding(
                "TransformedHeightField", new string('D', 64), 4, 4,
                transformedId, binding.ContentSha256, "unitless", "frame.transformed.verification");
            var persistedReference = replacementReference with
            {
                FrameId = transformedBinding.FrameId!,
                SourceBinding = transformedBinding
            };
            var persistedMeasurement = capturedMeasurement with
            {
                FrameId = transformedBinding.FrameId!,
                SourceBinding = transformedBinding
            };
            var capturedDocument = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Captured Plane Flatness roles",
                new ToolRecipeSource(
                    captureWorkbench.Source.Id, captureWorkbench.Source.Name, captureWorkbench.Source.Format,
                    captureWorkbench.Source.Unit, captureWorkbench.Source.FrameId, sourcePath,
                    new FileInfo(sourcePath).Length, binding.ContentSha256, binding.GridWidth, binding.GridHeight),
                [new ToolRecipeReference(transformedId, "Published transformed verification artifact", "TransformedHeightField")],
                [new ToolRecipeStep(
                    captureStep.Id, captureStep.ToolId, captureStep.ToolName, 3,
                    [transformedId, persistedReference.Id, persistedMeasurement.Id], captureStep.OutputEntityId,
                    [
                        new ToolRecipeParameter("MaximumFlatness", "100000"),
                        new ToolRecipeParameter("MinimumReferenceSampleCount", "3"),
                        new ToolRecipeParameter("MinimumMeasurementSampleCount", "3")
                    ])],
                [persistedReference, persistedMeasurement]);
            ToolRecipeDocumentStore.Save(captureRecipePath, capturedDocument);
            var reopenedCaptureDocument = ToolRecipeDocumentStore.Load(captureRecipePath);
            var reopenedPlaneStep = reopenedCaptureDocument.Steps.Single();
            Check("Artifact-owned captured role identities and rectangles survive document reopen",
                reopenedPlaneStep.InputEntityIds.Count == 3
                && reopenedPlaneStep.InputEntityIds[1] == capturedReference.Id
                && reopenedPlaneStep.InputEntityIds[2] == initialMeasurementId
                && reopenedCaptureDocument.Selections!.Single(selection => selection.Id == capturedReference.Id).GridRectangle == new ToolRecipeGridRectangle(0, 1, 2, 2)
                && reopenedCaptureDocument.Selections!.Single(selection => selection.Id == initialMeasurementId).GridRectangle == new ToolRecipeGridRectangle(2, 2, 2, 2),
                captureRecipePath);

            Check("generic recipe saves", workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage), saveMessage);
            var reopened = new ToolWorkbenchViewModel(Path.Combine(root, "recent-reopen.json"));
            Check("generic recipe reopens both measurement steps", reopened.TryOpenTeachingRecipe(recipePath, out var openMessage)
                && reopened.PipelineSteps.Count(step => step.ToolId is "thickness" or "warpage") == 2
                && reopened.Selections.Count == 2, openMessage);
            reopened.SelectPipelineStep(thickness.Id);
            var reopenActionLogCount = reopened.RunLog.Count(item =>
                item.Category is "Preview" or "Publish" or "Run");
            Check(
                "reopen restores complete dual-ROI setup without implicit execution",
                reopened.HasCompleteDualRoiTeaching
                && reopened.SelectedToolWorkspace.Inputs.FirstOrDefault()?.EntityId == reopened.Source.Id
                && reopenActionLogCount == 0,
                $"complete={reopened.HasCompleteDualRoiTeaching}; actionLogs={reopenActionLogCount}");
            reopened.CreateNewTeachingRecipe("Reset recipe");
            Check(
                "new-recipe reset returns contextual setup to safe empty defaults",
                reopened.PipelineSteps.Count == 0
                && reopened.Selections.Count == 0
                && reopened.SelectedPipelineStep is null
                && reopened.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run") == reopenActionLogCount,
                $"steps={reopened.PipelineSteps.Count}; selections={reopened.Selections.Count}; actionLogs={reopenActionLogCount}");

            OVLog.Flush();
            var logDirectory = OVLog.GetLogDirectory();
            var allLog = string.IsNullOrWhiteSpace(logDirectory)
                ? null
                : Directory.EnumerateFiles(logDirectory, "*ALL.log", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            var persistedLog = allLog is null ? string.Empty : ReadSharedText(allLog);
            Check("Workbench ROI and save diagnostics persist to the application log",
                persistedLog.Contains($"step={captureStep.Id}", StringComparison.Ordinal)
                && persistedLog.Contains("Selection deleted", StringComparison.Ordinal)
                && persistedLog.Contains("Recipe save requested", StringComparison.Ordinal)
                && persistedLog.Contains("role=measurement", StringComparison.Ordinal),
                allLog ?? "no ALL log");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch { }
        }

        var success = total > 0 && passed == total && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        lines.Add($"Result: {(success ? "Pass" : "Fail")} ({passed}/{total} checks)");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = $"Generic height measurement Workbench verification: {(success ? "Pass" : "Fail")} ({passed}/{total} checks)";
        return success;
    }

    private static ToolWorkbenchPipelineStepItem Add(ToolWorkbenchViewModel workbench, string name, string selectionId)
    {
        workbench.SelectedTool = workbench.Tools.Single(tool => tool.Name == name);
        if (!WaitForCanExecute(workbench, null))
        {
            throw new InvalidOperationException(
                $"{name} was not ready to add: {workbench.SelectedToolProposedRouteDetail}");
        }

        workbench.AddSelectedToolCommand.Execute(null);
        var step = workbench.SelectedPipelineStep ?? throw new InvalidOperationException($"{name} was not added.");
        step.InputEntityIdsText = $"{workbench.Source.Id}; {selectionId}";
        return step;
    }

    private static void PrepareTransformedHeightFieldRoute(ToolWorkbenchViewModel workbench)
    {
        foreach (var toolId in new[] { "xyz-affine-apply", "re-grid-height-map" })
        {
            var tool = workbench.Tools.Single(candidate => candidate.Id == toolId);
            if (!WaitForCanExecute(workbench, tool))
            {
                throw new InvalidOperationException(
                    $"{toolId} was not ready to add: {workbench.SelectedToolProposedRouteDetail}");
            }

            workbench.AddSelectedToolCommand.Execute(tool);
        }
    }

    private static bool WaitForCanExecute(
        ToolWorkbenchViewModel workbench,
        object? parameter)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTimeOffset.UtcNow < deadline
               && (workbench.SourceQuality.IsLoading
                   || !workbench.AddSelectedToolCommand.CanExecute(parameter)))
        {
            Thread.Sleep(10);
        }

        return !workbench.SourceQuality.IsLoading
               && workbench.AddSelectedToolCommand.CanExecute(parameter);
    }

    private static void WaitForSourceQualityIdle(
        SourceQualityWorkspaceViewModel workspace)
    {
        var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (workspace.IsLoading && DateTimeOffset.UtcNow < deadline)
        {
            Thread.Sleep(10);
        }
    }

    private static ToolRecipeSelection CapturedRectangle(
        ToolWorkbenchTeachingCaptureRequestEventArgs request,
        ToolRecipeGridRectangle rectangle) =>
        new(
            request.SelectionId,
            request.SelectionName,
            request.Kind,
            request.RootSourceId,
            request.FrameId,
            request.SourceBinding,
            rectangle,
            null,
            null);

    private static string ReadSharedText(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

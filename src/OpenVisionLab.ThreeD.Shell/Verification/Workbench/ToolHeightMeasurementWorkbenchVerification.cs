using System.IO;
using OpenVisionLab;
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
                [10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15, 13, 14, 15, 16]).SaveC3D(sourcePath);
            var recipePath = Path.Combine(root, "measurement.ov3d-recipe.json");
            var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            workbench.RecipeName = "Generic measurement recipe";
            workbench.SetC3DSource(sourcePath);
            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var selection = new ToolRecipeSelection(
                "selection.measurement", "Measurement ROI", ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id, workbench.Source.FrameId, binding,
                new ToolRecipeGridRectangle(0, 0, 4, 4), null, null);
            workbench.Selections.Add(selection);

            var thickness = Add(workbench, "Thickness", selection.Id);
            Check("Thickness is a typed generic adapter", workbench.IsSelectedStepPropertyGridSupported && workbench.SelectedStepAdapterStatus == "Typed adapter ready", workbench.SelectedStepAdapterStatus);
            Check("Thickness route validates", workbench.CanSaveTeachingRecipe && workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.ValidationSummary);
            var thicknessPreview = workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
            Check("Thickness Preview produces evidence", thicknessPreview && workbench.MeasurementEvidenceSummary.Contains("mean", StringComparison.OrdinalIgnoreCase), workbench.MeasurementEvidenceSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Thickness publishes exact Preview", thickness.State == "Published" && workbench.IsMeasurementPreviewPublished, thickness.State);

            var warpage = Add(workbench, "Warpage", selection.Id);
            Check("Warpage is another tool step, not a workspace mode", workbench.IsSelectedStepWarpage && workbench.IsSelectedStepPropertyGridSupported, workbench.SelectedPipelineStepTitle);
            var warpagePreview = workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
            Check("Warpage Preview produces P2V evidence", warpagePreview && workbench.MeasurementEvidenceSummary.Contains("P2V", StringComparison.Ordinal), workbench.MeasurementEvidenceSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Warpage publishes exact Preview", warpage.State == "Published" && workbench.IsMeasurementPreviewPublished, warpage.State);

            var planeWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-plane.json"));
            planeWorkbench.SetC3DSource(sourcePath);
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

            var captureRecipePath = Path.Combine(root, "captured-plane.ov3d-recipe.json");
            var captureWorkbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent-captured-plane.json"));
            captureWorkbench.SetC3DSource(sourcePath);
            captureWorkbench.SelectedTool = captureWorkbench.Tools.Single(tool => tool.Id == "plane-flatness");
            captureWorkbench.AddSelectedToolCommand.Execute(null);
            var captureStep = captureWorkbench.SelectedPipelineStep!;
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
                && captureWorkbench.IsPlaneFlatnessMeasurementRoleActive,
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
            Check("Workbench blocks raw-C3D Plane Flatness save and still never runs Preview",
                !captureWorkbench.HasCurrentMeasurementPreview
                && !rawCaptureSaved
                && rawCaptureSaveMessage.Contains("Published TransformedHeightField", StringComparison.Ordinal),
                rawCaptureSaveMessage);

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
                && reopened.Selections.Count == 1, openMessage);
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
        workbench.AddSelectedToolCommand.Execute(null);
        var step = workbench.SelectedPipelineStep ?? throw new InvalidOperationException($"{name} was not added.");
        step.InputEntityIdsText = $"{workbench.Source.Id}; {selectionId}";
        return step;
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
}

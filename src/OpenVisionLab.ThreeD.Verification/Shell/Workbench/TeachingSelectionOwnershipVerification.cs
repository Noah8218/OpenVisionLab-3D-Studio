using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class TeachingSelectionOwnershipVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var total = 0;
        var passed = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        var binding = new ToolRecipeSelectionSourceBinding(
            "C3D",
            new string('A', 64),
            12,
            10);
        var sourceId = "source.fixture";
        var frameId = "frame.fixture";
        var store = new ToolWorkbenchTeachingSelectionStoreOwner(
            () => sourceId,
            () => frameId,
            () => binding,
            _ => ToolWorkbenchPublishedSelectionBindingState.Unavailable,
            () => null,
            () => false,
            _ => { },
            _ => { });
        var selection = CreateRectangleSelection(
            "selection.roi.01",
            sourceId,
            frameId,
            binding,
            new ToolRecipeGridRectangle(1, 2, 3, 4));
        store.Upsert(selection);
        store.Upsert(selection with { Name = "Updated ROI" });
        var requirement = new ToolWorkbenchTeachingSelectionRequirement(
            "ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            2,
            true,
            "Fixture requirement.");
        store.RefreshCompatibleSelections(requirement);
        Check(
            "selection store owns upsert and compatible projection",
            store.Selections is [var stored]
            && stored.Name == "Updated ROI"
            && store.AvailableCompatibleSelections is [var compatible]
            && ReferenceEquals(stored, compatible)
            && store.IsCurrent(stored),
            $"stored={store.Selections.Count};compatible={store.AvailableCompatibleSelections.Count}");

        var appliedChanged = 0;
        store.AppliedSelectionsChanged += (_, _) => appliedChanged++;
        store.NotifyAppliedSelectionsChanged();
        Check(
            "applied-selection event policy is store-owned",
            appliedChanged == 1,
            $"events={appliedChanged}");

        var invalid = selection with { Id = "selection.invalid", RootSourceId = "source.other" };
        store.Upsert(invalid);
        var bindingErrors = store.ValidateSourceBindings();
        Check(
            "source-currentness fails closed at the store boundary",
            bindingErrors.Count == 1
            && bindingErrors[0].Contains("does not match root source", StringComparison.Ordinal),
            string.Join(" | ", bindingErrors));
        store.Remove(invalid);

        var tool = new ToolWorkbenchToolItem(
            "Verification",
            "ROI / Crop",
            "roi-crop",
            2,
            "HeightField + GridRectangle",
            "HeightField",
            "Teaching owner fixture.",
            []);
        var step = new ToolWorkbenchPipelineStepItem(
            "step.roi-crop.01",
            tool,
            sourceId,
            "derived.roi-crop.01");
        var policyRequirement = ToolWorkbenchTeachingSelectionPolicy.CreateRequirement(
            step,
            requirement,
            "Cross-section",
            "Cross-section detail",
            measurementRole: false);
        var generatedId = ToolWorkbenchTeachingSelectionPolicy.CreateSelectionId(
            step,
            policyRequirement!,
            false,
            false,
            false,
            false,
            false,
            store.Selections);
        Check(
            "pure policy owns requirement, matching, and deterministic identity",
            policyRequirement is
            {
                Kind: ToolRecipeSelectionKinds.GridRectangle,
                RequiredPointCount: 2,
                UsesViewerCapture: true
            }
            && ToolWorkbenchTeachingSelectionPolicy.MatchesRequirement(
                selection,
                policyRequirement)
            && generatedId == "selection.roi-crop-01.roi",
            $"kind={policyRequirement?.Kind};points={policyRequirement?.RequiredPointCount};id={generatedId}");

        ToolRecipeSelection? persisted = null;
        var beginEvents = 0;
        var applyEvents = 0;
        var cancelEvents = 0;
        var captureAppliedEvents = 0;
        var captureLogs = new List<string>();
        var session = new ToolWorkbenchTeachingCaptureSession();
        var capture = new ToolWorkbenchTeachingSelectionCaptureOwner(
            session,
            _ => new ToolWorkbenchTeachingCaptureContext(
                step,
                policyRequirement!,
                persisted,
                generatedId,
                "ROI / Crop selection",
                sourceId,
                frameId,
                binding,
                "selection"),
            () => policyRequirement,
            () => persisted,
            () => persisted?.SourceBinding ?? binding,
            () => false,
            value => persisted = value,
            () => { },
            () => captureAppliedEvents++,
            (category, message) => captureLogs.Add($"{category}:{message}"),
            () => false,
            () => false,
            () => { },
            () => false,
            () => { });
        capture.BeginRequested += (_, _) => beginEvents++;
        capture.ApplyRequested += (_, _) => applyEvents++;
        capture.CancelRequested += (_, _) => cancelEvents++;
        capture.BeginCommand.Execute(null);
        capture.UpdateState(true, 2, 2, true, "Candidate ready.");
        capture.UpdateGridRectangleDraft(new ToolRecipeGridRectangle(2, 3, 4, 5));
        capture.ApplyCommand.Execute(null);
        var candidate = CreateRectangleSelection(
            generatedId,
            sourceId,
            frameId,
            binding,
            session.GridRectangleDraft);
        var applied = capture.TryApplyCapturedSelection(candidate, out var applyMessage);
        Check(
            "capture owner preserves explicit Apply and commits only through its seam",
            beginEvents == 1
            && applyEvents == 1
            && applied
            && persisted?.GridRectangle == candidate.GridRectangle
            && captureAppliedEvents == 1
            && !capture.IsActive,
            $"begin={beginEvents};applyRequest={applyEvents};applied={applied};events={captureAppliedEvents};message={applyMessage}");

        capture.BeginCommand.Execute(null);
        capture.UpdateState(true, 1, 2, false, "First point.");
        capture.CancelCommand.Execute(null);
        Check(
            "capture Cancel clears only transient state",
            cancelEvents == 1
            && !capture.IsActive
            && persisted?.GridRectangle == candidate.GridRectangle,
            $"cancel={cancelEvents};active={capture.IsActive};persisted={persisted?.GridRectangle}");

        var lineTool = tool with
        {
            Name = "Line Intersection",
            Id = "line-intersection",
            OutputContract = "CornerAnchor"
        };
        var lineStep = new ToolWorkbenchPipelineStepItem(
            "step.line-intersection.01",
            lineTool,
            sourceId,
            "derived.corner-anchor.01");
        var correspondenceTool = tool with
        {
            Name = "Landmark Correspondence",
            Id = "landmark-correspondence",
            InputContract = "CornerAnchor",
            OutputContract = "LandmarkCorrespondenceSet"
        };
        var correspondenceStep = new ToolWorkbenchPipelineStepItem(
            "step.landmark-correspondence.01",
            correspondenceTool,
            string.Empty,
            "derived.landmark-correspondence.01");
        var correspondenceRequirement = new ToolWorkbenchTeachingSelectionRequirement(
            "Landmark correspondences",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
            0,
            false,
            "Fixture correspondence requirement.");
        ToolRecipeSelection? correspondenceSelection = null;
        var correspondenceAppliedEvents = 0;
        var editor = new ToolWorkbenchLandmarkCorrespondenceEditorOwner(
            () => new ToolWorkbenchLandmarkCorrespondenceEditorContext(
                correspondenceStep,
                true,
                correspondenceSelection,
                binding,
                sourceId,
                frameId,
                correspondenceRequirement,
                [lineStep, correspondenceStep]),
            (_, _) => "selection.landmark-correspondence-01.correspondences",
            value => correspondenceSelection = value,
            _ => correspondenceSelection = null,
            () => correspondenceAppliedEvents++,
            (_, _) => { });
        editor.Refresh();
        editor.ReferenceUnit = "mm";
        editor.ReferenceProvenance = "fixture";
        editor.ReferenceRevision = "rev-1";
        editor.MinimumNormalizedTetrahedronVolume = 0.1;
        editor.ReferenceX = 1;
        editor.ReferenceY = 2;
        editor.ReferenceZ = 3;
        editor.AddOrUpdateRowCommand.Execute(null);
        Check(
            "correspondence editor owns row-to-selection commits",
            correspondenceSelection is
            {
                Kind: ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
                Rows.Count: 1,
                CorrespondenceDescriptor.ReferenceUnit: "mm"
            }
            && correspondenceAppliedEvents == 1,
            $"rows={correspondenceSelection?.Rows?.Count};events={correspondenceAppliedEvents}");

        var noExecutionLogs = captureLogs.All(line =>
            !line.StartsWith("Preview:", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("Publish:", StringComparison.OrdinalIgnoreCase)
            && !line.StartsWith("Run:", StringComparison.OrdinalIgnoreCase));
        Check(
            "teaching owners expose no implicit Preview Publish or Run seam",
            noExecutionLogs,
            $"logs={captureLogs.Count}");

        var passedAll = total > 0 && passed == total;
        lines.Add(
            $"TeachingSelectionOwnership|{(passedAll ? "PASS" : "FAIL")}|checks={passed}/{total}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = lines[^1];
        return passedAll;
    }

    private static ToolRecipeSelection CreateRectangleSelection(
        string id,
        string sourceId,
        string frameId,
        ToolRecipeSelectionSourceBinding binding,
        ToolRecipeGridRectangle rectangle) =>
        new(
            id,
            "ROI fixture",
            ToolRecipeSelectionKinds.GridRectangle,
            sourceId,
            frameId,
            binding,
            rectangle,
            null,
            null);
}

using System.Security.Cryptography;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell;

/// <summary>
/// D-backed Workbench contract verification for G-13 Presence Check.
/// This exercises the existing authoring, PropertyGrid, Preview/Publish,
/// output projection, and save/reopen seams without starting a desktop EXE.
/// </summary>
internal static class PresenceCheckWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Presence Check Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var reportFullPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(reportFullPath)
            ?? throw new InvalidOperationException("The verification report directory is unavailable.");
        var fixtureRoot = Path.Combine(reportDirectory, "PresenceCheckWorkbenchFixture");

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "presence-workbench.c3d");
            var goodRecipePath = Path.Combine(fixtureRoot, "presence-workbench-good.ov3d-recipe.json");
            var missingRecipePath = Path.Combine(fixtureRoot, "presence-workbench-missing.ov3d-recipe.json");
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.presence-check-workbench",
                4,
                3,
                [
                    10, 10, 10, 10,
                    double.NaN, double.NaN, 10, 10,
                    11, 11, 11, 11
                ]);
            source.SaveC3D(sourcePath);
            var sourceSha256 = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(sourcePath)));

            var workbench = new ToolWorkbenchViewModel(
                Path.Combine(fixtureRoot, "presence-workbench-recent.json"));
            workbench.RecipeName = "Presence Check Workbench recipe";
            workbench.SetC3DSource(sourcePath, markDirty: false);
            var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
            var goodSelection = new ToolRecipeSelection(
                "selection.presence.workbench.good",
                "Presence feature",
                ToolRecipeSelectionKinds.GridRectangle,
                workbench.Source.Id,
                workbench.Source.FrameId,
                binding,
                new ToolRecipeGridRectangle(0, 0, 1, 2),
                null,
                null);
            workbench.Selections.Add(goodSelection);

            var presenceTool = workbench.Tools.Single(tool => tool.Id == "presence-check");
            workbench.SelectedTool = presenceTool;
            Check(
                "catalog proposes a source-bound Presence Check route",
                workbench.AddSelectedToolCommand.CanExecute(null)
                && workbench.IsSelectedToolProposedRouteCompatible,
                workbench.SelectedToolProposedRouteDetail);
            workbench.AddSelectedToolCommand.Execute(null);
            var step = workbench.SelectedPipelineStep
                ?? throw new InvalidOperationException("Presence Check was not added to the Workbench.");
            step.InputEntityIdsText = $"{workbench.Source.Id}; {goodSelection.Id}";

            Check(
                "Workbench exposes the explicit GridRectangle feature contract",
                step.ToolId == "presence-check"
                && step.MinimumInputCount == 2
                && step.InputEntityIds.SequenceEqual([workbench.Source.Id, goodSelection.Id])
                && workbench.IsSelectedStepPresenceCheck
                && workbench.IsSelectedStepMeasurement
                && workbench.SelectedStepSelectionRequirement is
                {
                    Kind: ToolRecipeSelectionKinds.GridRectangle,
                    RequiredPointCount: 2,
                    UsesViewerCapture: true
                }
                && workbench.SelectedStepTeachingSelection?.Id == goodSelection.Id,
                $"inputs={string.Join(";", step.InputEntityIds)}; requirement={workbench.SelectedStepSelectionRequirementTitle}");
            Check(
                "typed Presence Check PropertyGrid adapter is ready",
                workbench.IsSelectedStepPropertyGridSupported
                && workbench.SelectedStepPropertyDraft is PresenceCheckStepProperties
                && workbench.SelectedStepAdapterStatus == "Typed adapter ready"
                && workbench.SelectedToolWorkspace.ParameterDraft is PresenceCheckStepProperties,
                workbench.SelectedStepAdapterStatus);

            var draft = workbench.SelectedStepPropertyDraft as PresenceCheckStepProperties
                ?? throw new InvalidOperationException("Presence Check PropertyGrid draft is unavailable.");
            draft.MinimumFiniteCoverageRatio = 0.95d;
            draft.MinimumMeanRawHeight = 9d;
            draft.MaximumMeanRawHeight = 11d;
            workbench.MarkSelectedStepParameterDraftDirtyCommand.Execute(null);
            var actionLogsBeforeApply = CountActionLogs(workbench);
            var applied = workbench.TryApplySelectedStepParameterDraft(out var applyMessage);
            Check(
                "parameter Apply commits policy without implicit execution",
                applied
                && !workbench.HasPendingStepParameterChanges
                && actionLogsBeforeApply == CountActionLogs(workbench)
                && step.Parameters.Select(parameter => parameter.Name).SequenceEqual(
                    C3DPresenceCheckPolicy.ParameterNames),
                applyMessage);
            workbench.ValidateTeachingRecipeCommand.Execute(null);
            Check(
                "good source and feature recipe is storage-valid",
                workbench.CanSaveTeachingRecipe
                && workbench.IsSourceReadyForRecipe
                && workbench.ValidationSummary.Contains("valid", StringComparison.OrdinalIgnoreCase),
                workbench.ValidationSummary);

            var sourceShaBeforeSave = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(sourcePath)));
            Check(
                "good recipe saves with source-bound identity",
                workbench.TrySaveTeachingRecipe(goodRecipePath, out var saveMessage)
                && File.Exists(goodRecipePath)
                && string.Equals(sourceShaBeforeSave, sourceSha256, StringComparison.Ordinal),
                saveMessage);
            var goodRecipeShaBeforePreview = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(goodRecipePath)));

            var previewSucceeded = workbench.PreviewSelectedMeasurementAsync()
                .GetAwaiter()
                .GetResult();
            var goodOutput = workbench.CurrentMeasurementOutput;
            var goodPresence = goodOutput?.PresenceCheck;
            Check(
                "good fixture Preview returns Present with exact feature metrics",
                previewSucceeded
                && goodOutput?.Result.Status == ResultStatus.Pass
                && goodPresence is
                {
                    Feature:
                    {
                        FeatureId: var featureId,
                        TotalCellCount: 2,
                        FiniteCellCount: 2,
                        MissingCellCount: 0,
                        FiniteCoverageRatio: 1,
                        MeanRawHeight: 10,
                        Decision: ResultStatus.Pass,
                        IsPresent: true
                    }
                }
                && featureId == goodSelection.Id
                && goodPresence.Feature.Region == goodSelection.GridRectangle,
                workbench.MeasurementEvidenceSummary);
            Check(
                "Preview projects evidence-only output with no display/compare action",
                workbench.SelectedToolWorkspace.Outputs.SingleOrDefault() is
                {
                    Contract: "PresenceCheckResult",
                    State: "Preview",
                    Value: "10",
                    Unit: "raw-height",
                    ResultStatus: "Pass",
                    CanShowInViewer: false,
                    CanPinToCompare: false,
                    CanCompare: false,
                    Availability: var availability
                }
                && availability == workbench.Localization.EvidenceOnlyOutput
                && workbench.DisplayedOutputs.SingleOrDefault(item => item.Id == step.OutputEntityId) is
                {
                    IsEvidenceOnly: true,
                    CanShowInViewer: false,
                    CanPinToCompare: false
                }
                && workbench.ArtifactRegistry.SingleOrDefault(item => item.Id == step.OutputEntityId) is
                {
                    Contract: "PresenceCheckResult",
                    State: "Preview",
                    HasContentHash: true
                },
                $"workspace={workbench.SelectedToolWorkspace.Outputs.SingleOrDefault()?.Detail}; displayed={workbench.DisplayedOutputsSummary}");
            var goodContentSha256 = goodPresence?.ContentSha256 ?? string.Empty;
            var actionLogsAfterPreview = CountActionLogs(workbench);
            workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Publish reuses the exact Preview output",
                step.State == "Published"
                && workbench.IsMeasurementPreviewPublished
                && workbench.CurrentMeasurementOutput?.PresenceCheck?.ContentSha256 == goodContentSha256
                && CountActionLogs(workbench) == actionLogsAfterPreview + 1,
                $"state={step.State}; hash={workbench.CurrentMeasurementOutput?.ContentSha256}; expected={goodContentSha256}");
            Check(
                "Preview and Publish do not mutate source or saved recipe bytes",
                Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath))) == sourceShaBeforeSave
                && Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(goodRecipePath))) == goodRecipeShaBeforePreview,
                $"source={sourceShaBeforeSave}; recipe={goodRecipeShaBeforePreview}");

            var goodDocument = ToolRecipeDocumentStore.Load(goodRecipePath);
            var goodStep = goodDocument.Steps.Single();
            var missingSelection = goodSelection with
            {
                Id = "selection.presence.workbench.missing",
                Name = "Missing presence feature",
                GridRectangle = new ToolRecipeGridRectangle(1, 0, 1, 2)
            };
            var missingDocument = goodDocument with
            {
                Name = "Presence Check Workbench missing fixture",
                Steps =
                [
                    goodStep with
                    {
                        OutputEntityId = "derived.presence-check.missing.01",
                        InputEntityIds = [goodDocument.Source.Id, missingSelection.Id]
                    }
                ],
                Selections = [missingSelection]
            };
            ToolRecipeDocumentStore.Save(missingRecipePath, missingDocument);

            var reopened = new ToolWorkbenchViewModel(
                Path.Combine(fixtureRoot, "presence-workbench-reopen-recent.json"));
            var reopenedGood = reopened.TryOpenTeachingRecipe(goodRecipePath, out var reopenMessage);
            Check(
                "saved good recipe reopens with typed setup and no implicit execution",
                reopenedGood
                && reopened.PipelineSteps.SingleOrDefault()?.ToolId == "presence-check"
                && reopened.SelectedStepPropertyDraft is PresenceCheckStepProperties
                && reopened.SelectedStepTeachingSelection?.Id == goodSelection.Id
                && reopened.CurrentMeasurementOutput is null
                && CountActionLogs(reopened) == 0
                && reopened.PreviewSelectedStepCommand.CanExecute(null),
                reopenMessage);

            var missingWorkbench = new ToolWorkbenchViewModel(
                Path.Combine(fixtureRoot, "presence-workbench-missing-recent.json"));
            var openedMissing = missingWorkbench.TryOpenTeachingRecipe(
                missingRecipePath,
                out var missingOpenMessage);
            var missingPreview = openedMissing
                && missingWorkbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
            var missingOutput = missingWorkbench.CurrentMeasurementOutput;
            Check(
                "missing fixture Preview fails closed with no finite mean",
                missingPreview
                && missingOutput?.Result.Status == ResultStatus.Fail
                && missingOutput.PresenceCheck is
                {
                    Feature:
                    {
                        TotalCellCount: 2,
                        FiniteCellCount: 0,
                        MissingCellCount: 2,
                        FiniteCoverageRatio: 0,
                        MeanRawHeight: null,
                        Decision: ResultStatus.Fail,
                        IsPresent: false
                    }
                }
                && missingWorkbench.MeasurementEvidenceSummary.Contains(
                    "mean raw height missing",
                    StringComparison.OrdinalIgnoreCase),
                openedMissing ? missingWorkbench.MeasurementEvidenceSummary : missingOpenMessage);
            Check(
                "missing fixture remains evidence-only and unpublished",
                missingWorkbench.SelectedToolWorkspace.Outputs.SingleOrDefault() is
                {
                    Contract: "PresenceCheckResult",
                    State: "Preview",
                    ResultStatus: "Fail",
                    CanShowInViewer: false,
                    CanPinToCompare: false,
                    CanCompare: false
                }
                && !missingWorkbench.IsMeasurementPreviewPublished,
                $"state={missingWorkbench.SelectedPipelineStep?.State}; output={missingOutput?.ContentSha256}");

            reopened.CreateNewTeachingRecipe("Presence Check reset");
            Check(
                "new-recipe lifecycle clears Presence Check context without execution",
                reopened.PipelineSteps.Count == 0
                && reopened.Selections.Count == 0
                && reopened.SelectedPipelineStep is null
                && CountActionLogs(reopened) == 0,
                $"steps={reopened.PipelineSteps.Count}; selections={reopened.Selections.Count}");

            lines.Add($"Fixture|root={fixtureRoot}|source={sourcePath}|goodRecipe={goodRecipePath}|missingRecipe={missingRecipePath}|sourceSha256={sourceSha256}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception.GetType().Name}: {exception.Message}");
        }

        var success = total > 0
            && passed == total
            && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        summary = $"PresenceCheckWorkbench|pass={success}|checks={passed}/{total}|report={reportFullPath}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(reportFullPath)!);
        File.WriteAllLines(reportFullPath, lines);
        return success;
    }

    private static int CountActionLogs(ToolWorkbenchViewModel workbench) =>
        workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
}

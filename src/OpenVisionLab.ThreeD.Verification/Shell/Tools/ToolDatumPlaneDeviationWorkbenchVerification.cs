using System.IO;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class ToolDatumPlaneDeviationWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D Datum Plane Raw-Height Deviation Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "DatumPlaneDeviationWorkbench", Guid.NewGuid().ToString("N"));
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 3, 3, [10d, 11d, 12d, 12d, 13d, 14d, 14d, 15d, 16.4d]);
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var planeSelection = CreatePointSelection(source, "selection.plane", (0, 0), (0, 1), (1, 0));
            var roi = CreateRectangleSelection(source, "selection.deviation-roi", 0, 0, 3, 3);
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "Datum Plane Deviation Workbench",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [
                    new ToolRecipeStep("step.plane", "three-point-plane", "3-Point Plane", 1, [source.EntityId, planeSelection.Id], "derived.plane", [new("OutputRole", "FixtureDatum"), new("ConstructionPolicy", "OrderedPointsDefineOrientedPlane")]),
                    new ToolRecipeStep("step.datum-deviation", "datum-plane-raw-height-deviation", "Datum Plane Raw-Height Deviation", 2, [source.EntityId, "derived.plane", roi.Id], "derived.datum-deviation", [new("MaximumPeakToValleyRawHeight", "0.5"), new("OutputRole", "FixtureDeviation"), new("ResidualPolicy", "RawHeightMinusDatumPlanePredictedRawHeight"), new("MinimumValidSampleCount", "3"), new("MinimumAbsoluteNormalY", "0.1")])
                ],
                [planeSelection, roi]);
            var recipePath = Path.Combine(root, "datum-plane-deviation.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var open), open);
            workbench.SelectPipelineStep("step.datum-deviation");
            Check("Datum Plane Deviation uses typed WPG adapter", workbench.SelectedStepPropertyDraft is DatumPlaneDeviationStepProperties, workbench.SelectedStepAdapterStatus);
            Check("Datum Plane Deviation waits for Published plane", !workbench.PreviewSelectedStepCommand.CanExecute(null) && workbench.DatumPlaneDeviationUpstreamSummary.Contains("missing/stale", StringComparison.Ordinal), workbench.DatumPlaneDeviationExecutionSummary);

            workbench.SelectPipelineStep("step.plane");
            Check("3-Point Plane previews explicitly", workbench.PreviewSelectedThreePointPlaneAsync().GetAwaiter().GetResult(), workbench.ThreePointPlaneExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            C3DThreePointPlaneFeature? publishedPlane = null;
            Check("3-Point Plane publishes exact preview", workbench.IsThreePointPlanePreviewPublished && workbench.TryGetPublishedThreePointPlaneOutput("derived.plane", out publishedPlane) && publishedPlane is not null, workbench.ThreePointPlaneExecutionSummary);

            C3DDatumPlaneDeviationFeature? preview = null;
            C3DDatumPlaneDeviationFeature? published = null;
            workbench.DatumPlaneDeviationDisplayRequested += (_, args) =>
            {
                if (args.Output.OutputEntityId != "derived.datum-deviation") return;
                if (args.IsPublished) published = args.Output;
                else preview = args.Output;
            };
            workbench.SelectPipelineStep("step.datum-deviation");
            Check("Datum Plane Deviation becomes preview-ready from Published plane and ROI", workbench.PreviewSelectedStepCommand.CanExecute(null) && workbench.DatumPlaneDeviationUpstreamSummary.Contains("Published", StringComparison.Ordinal), workbench.DatumPlaneDeviationExecutionSummary);
            Check("explicit Datum Plane Deviation Preview", workbench.PreviewSelectedDatumPlaneDeviationAsync().GetAwaiter().GetResult()
                && preview is not null
                && preview.Status == ResultStatus.Pass
                && NearlyEqual(preview.PeakToValleyRawHeight, 0.4d, 0.00001)
                && preview.ValidSampleCount == 9
                && preview.OverlaySamples.Count == 9,
                workbench.DatumPlaneDeviationExecutionSummary);
            var unconfigured = document with { Steps = document.Steps.Select(step => step.Id == "step.datum-deviation" ? step with { AcceptanceLimitsConfigured = false } : step).ToArray() };
            var measured = ToolRecipeDatumPlaneDeviationExecution.Execute(unconfigured, "step.datum-deviation", publishedPlane!, root);
            Check("unconfigured datum retains values and neutral geometry", measured.Output is { Status: ResultStatus.NotRun }
                && measured.Result.Status == ResultStatus.NotRun && measured.Output.PeakToValleyRawHeight > 0,
                measured.Result.Message);
            var expected = ToolRecipeDatumPlaneDeviationExecution.Execute(document, "step.datum-deviation", publishedPlane!, root);
            Check("Workbench and headless share datum result identity", preview?.ContentSha256 == expected.Output?.ContentSha256, $"workbench={preview?.ContentSha256};headless={expected.Output?.ContentSha256}");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Publish reuses exact datum Preview", workbench.IsDatumPlaneDeviationPreviewPublished && ReferenceEquals(preview, published)
                && workbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out var stored) && ReferenceEquals(stored, published), workbench.DatumPlaneDeviationExecutionSummary);
            Check("published datum result becomes a Tool Lab artifact", workbench.IsSelectedToolLabAvailable
                && workbench.ArtifactRegistry.Count(item => item.Contract == "DatumPlaneDeviationResult" && item.State == "Published") == 1, workbench.ArtifactRegistrySummary);

            workbench.SelectPipelineStep("step.datum-deviation");
            OpenVisionLanguageService.Load();
            var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
            var resultActions = workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
            var resultMessage = workbench.CurrentDatumPlaneDeviationResult?.Message;
            try
            {
                foreach (var language in new[] { OpenVisionLanguage.Korean, OpenVisionLanguage.English, OpenVisionLanguage.Korean })
                {
                    OpenVisionLanguageService.SetLanguage(language, save: false);
                    var selectedResult = workbench.SelectedToolWorkspace.CurrentOutput;
                    Check($"datum P2V, limit and judgement render in {language}", selectedResult is { Unit: "raw-height", ValueLabel: "P2V", LimitSummary: "≤ 0.5 raw-height" }
                        && selectedResult.Value == preview!.PeakToValleyRawHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                        && selectedResult.ResultStatus == (language == OpenVisionLanguage.Korean ? "통과" : "Pass")
                        && selectedResult.Reason == (language == OpenVisionLanguage.Korean ? "기준면 높이 편차가 규격 안에 있습니다." : "Datum-plane height deviation is within limits."),
                        $"value={selectedResult?.Value};status={selectedResult?.ResultStatus};reason={selectedResult?.Reason}");
                }
                Check("datum language switches preserve calculations and raw message", resultActions == workbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run")
                    && workbench.CurrentDatumPlaneDeviationResult?.Message == resultMessage, "Presentation only; immutable calculation retained.");
            }
            finally { OpenVisionLanguageService.SetLanguage(originalLanguage, save: false); }

            workbench.SelectPipelineStep("step.plane");
            workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "OutputRole").Value = "FixtureDatumRevised";
            Check("upstream Plane change stales and unpublishes datum result", workbench.IsDatumPlaneDeviationPreviewStale
                && !workbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _), workbench.DatumPlaneDeviationExecutionSummary);

            var replacementSourcePath = Path.Combine(root, "replacement.c3d");
            C3DHeightFieldSnapshot.CreateForVerification("source.replacement", 2, 2, [1d, 2d, 3d, 4d]).SaveC3D(replacementSourcePath);
            workbench.SetC3DSource(replacementSourcePath);
            Check("source replacement clears datum result", workbench.CurrentDatumPlaneDeviationOutput is null
                && !workbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _), workbench.DatumPlaneDeviationExecutionSummary);


            var secondRoi = roi with { Id = "selection.deviation-roi.b", Name = "Other measurement region" };
            var branches = document with
            {
                Steps = [document.Steps[0], document.Steps[1],
                    document.Steps[0] with { Id = "step.plane.b", OutputEntityId = "derived.plane.b" },
                    document.Steps[1] with { Id = "step.datum.b", OutputEntityId = "derived.datum.b", InputEntityIds = [source.EntityId, "derived.plane.b", secondRoi.Id] }],
                Selections = [planeSelection, roi, secondRoi]
            };
            var branchesPath = Path.Combine(root, "branches.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(branchesPath, branches);
            using (var branchWorkbench = new ToolWorkbenchViewModel())
            {
                Check("open independent datum branches", branchWorkbench.TryOpenTeachingRecipe(branchesPath, out var branchOpen), branchOpen);
                branchWorkbench.SelectPipelineStep("step.plane");
                var planeAReady = branchWorkbench.PreviewSelectedThreePointPlaneAsync().GetAwaiter().GetResult();
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                branchWorkbench.SelectPipelineStep("step.datum-deviation");
                var datumAReady = branchWorkbench.PreviewSelectedDatumPlaneDeviationAsync().GetAwaiter().GetResult();
                var datumA = branchWorkbench.CurrentDatumPlaneDeviationOutput;
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                branchWorkbench.SelectPipelineStep("step.datum.b");
                Check("unexecuted sibling datum cannot reuse another result", planeAReady && datumAReady
                    && branchWorkbench.CurrentDatumPlaneDeviationOutput is null && !branchWorkbench.HasCurrentDatumPlaneDeviationPreview
                    && !branchWorkbench.PublishSelectedStepCommand.CanExecute(null), branchWorkbench.DatumPlaneDeviationExecutionSummary);
                branchWorkbench.SelectPipelineStep("step.plane.b");
                var planeBReady = branchWorkbench.PreviewSelectedThreePointPlaneAsync().GetAwaiter().GetResult();
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                branchWorkbench.SelectPipelineStep("step.datum.b");
                var datumBReady = branchWorkbench.PreviewSelectedDatumPlaneDeviationAsync().GetAwaiter().GetResult();
                var datumB = branchWorkbench.CurrentDatumPlaneDeviationOutput;
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                Check("both datum branches remain published", planeBReady && datumBReady && datumB is not null
                    && branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out var retainedA) && ReferenceEquals(retainedA, datumA)
                    && branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum.b", out var retainedB) && ReferenceEquals(retainedB, datumB), branchWorkbench.ArtifactRegistrySummary);
                var displays = 0;
                branchWorkbench.DatumPlaneDeviationDisplayRequested += (_, _) => displays++;
                branchWorkbench.SelectPipelineStep("step.datum-deviation");
                Check("datum selection restores exact output without calculation", ReferenceEquals(branchWorkbench.CurrentDatumPlaneDeviationOutput, datumA)
                    && branchWorkbench.IsDatumPlaneDeviationPreviewPublished && displays == 0, branchWorkbench.DatumPlaneDeviationExecutionSummary);
                branchWorkbench.SelectedPipelineStep!.ToolName = "Renamed deviation";
                Check("datum descriptive rename keeps both results", branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _)
                    && branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum.b", out _) && displays == 0, branchWorkbench.DatumPlaneDeviationExecutionSummary);
                branchWorkbench.PipelineSteps.Single(step => step.Id == "step.datum.b").Parameters.Single(parameter => parameter.Name == "MaximumPeakToValleyRawHeight").Value = "0.2";
                Check("inactive datum edit preserves unrelated branch", branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out var unchangedA)
                    && ReferenceEquals(unchangedA, datumA) && !branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum.b", out _)
                    && ReferenceEquals(branchWorkbench.CurrentDatumPlaneDeviationOutput, datumA) && displays == 0, branchWorkbench.DatumPlaneDeviationExecutionSummary);
                branchWorkbench.SelectPipelineStep("step.datum.b");
                var rerunB = branchWorkbench.PreviewSelectedDatumPlaneDeviationAsync().GetAwaiter().GetResult();
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                Check("NG datum keeps exact P2V and the executed limit", rerunB && branchWorkbench.CurrentDatumPlaneDeviationOutput is { Status: ResultStatus.Fail }
                    && branchWorkbench.SelectedToolWorkspace.CurrentOutput is { LimitSummary: "≤ 0.2 raw-height", Unit: "raw-height" }
                    && branchWorkbench.TeachingResults.Any(row => row.Step.Id == "step.datum-deviation" && row.Limits == "≤ 0.5 raw-height"), branchWorkbench.DatumPlaneDeviationEvidenceSummary);
                var parameterDraft = (DatumPlaneDeviationStepProperties)branchWorkbench.SelectedStepPropertyDraft!;
                parameterDraft.MaximumPeakToValleyRawHeight = 0.25;
                branchWorkbench.MarkSelectedStepParameterDraftDirty();
                Check("pending datum draft blocks Run without execution", branchWorkbench.HasPendingStepParameterChanges
                    && !branchWorkbench.RunSelectedStepCommand.CanExecute(null), branchWorkbench.StepParameterEditStatus);
                var lastSignaledCanRun = branchWorkbench.RunSelectedStepCommand.CanExecute(null);
                EventHandler runStateChanged = (_, _) => lastSignaledCanRun = branchWorkbench.RunSelectedStepCommand.CanExecute(null);
                branchWorkbench.RunSelectedStepCommand.CanExecuteChanged += runStateChanged;
                var applyActions = branchWorkbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run");
                try
                {
                    var draftApplied = branchWorkbench.TryApplySelectedStepParameterDraft(out var applyMessage);
                    Check("Apply notifies Run readiness without save or execution", draftApplied && !branchWorkbench.HasPendingStepParameterChanges
                        && branchWorkbench.RunSelectedStepCommand.CanExecute(null) && lastSignaledCanRun
                        && applyActions == branchWorkbench.RunLog.Count(item => item.Category is "Preview" or "Publish" or "Run"),
                        $"signaled={lastSignaledCanRun};canRun={branchWorkbench.RunSelectedStepCommand.CanExecute(null)};{applyMessage}");
                    Check("draft Apply retains both planes and the unrelated datum", branchWorkbench.TryGetPublishedThreePointPlaneOutput("derived.plane", out _)
                        && branchWorkbench.TryGetPublishedThreePointPlaneOutput("derived.plane.b", out _)
                        && branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out var afterApplyA) && ReferenceEquals(afterApplyA, datumA)
                        && !branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum.b", out _), branchWorkbench.DatumPlaneDeviationExecutionSummary);
                }
                finally { branchWorkbench.RunSelectedStepCommand.CanExecuteChanged -= runStateChanged; }
                branchWorkbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "MaximumPeakToValleyRawHeight").Value = "0.2";
                branchWorkbench.SelectPipelineStep("step.plane.b");
                branchWorkbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "OutputRole").Value = "RevisedPlaneB";
                Check("plane edit invalidates its datum and preserves other branch", rerunB
                    && branchWorkbench.TryGetPublishedThreePointPlaneOutput("derived.plane", out _)
                    && !branchWorkbench.TryGetPublishedThreePointPlaneOutput("derived.plane.b", out _)
                    && branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out var stillA) && ReferenceEquals(stillA, datumA)
                    && !branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum.b", out _), branchWorkbench.DatumPlaneDeviationExecutionSummary);
                branchWorkbench.SelectPipelineStep("step.datum.b");
                Check("stale upstream plane blocks only its datum", !branchWorkbench.PreviewSelectedStepCommand.CanExecute(null)
                    && branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _), branchWorkbench.DatumPlaneDeviationUpstreamSummary);
                branchWorkbench.SelectPipelineStep("step.plane.b");
                var restoredPlaneB = branchWorkbench.PreviewSelectedThreePointPlaneAsync().GetAwaiter().GetResult();
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                branchWorkbench.SelectPipelineStep("step.datum.b");
                var restoredDatumB = branchWorkbench.PreviewSelectedDatumPlaneDeviationAsync().GetAwaiter().GetResult();
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                branchWorkbench.SelectedPipelineStep!.AcceptanceLimitsConfigured = false;
                var measuredOnly = branchWorkbench.PreviewSelectedDatumPlaneDeviationAsync().GetAwaiter().GetResult();
                branchWorkbench.PublishSelectedStepCommand.Execute(null);
                Check("unconfigured datum presents values without judgement", measuredOnly
                    && branchWorkbench.CurrentDatumPlaneDeviationOutput is { Status: ResultStatus.NotRun }
                    && branchWorkbench.SelectedToolWorkspace.CurrentOutput is { Unit: "raw-height" } measuredEvidence
                    && measuredEvidence.ResultStatus == (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean ? "미판정" : "Unjudged")
                    && measuredEvidence.LimitSummary == (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean ? "규격 미설정" : "Limits not configured"), branchWorkbench.DatumPlaneDeviationEvidenceSummary);
                var restoredB = branchWorkbench.CurrentDatumPlaneDeviationOutput;
                branchWorkbench.SelectPipelineStep("step.datum-deviation");
                var removed = branchWorkbench.ConfirmSelectedStepRemoval("step.datum-deviation");
                var restored = branchWorkbench.TryRestoreLastRemovedStep(out var restoredMessage);
                Check("datum remove and restore retains other branch without restored result", restoredPlaneB && restoredDatumB && removed && restored
                    && !branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _)
                    && !branchWorkbench.HasCurrentDatumPlaneDeviationPreview
                    && branchWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum.b", out var afterRestoreB) && ReferenceEquals(afterRestoreB, restoredB), restoredMessage);
            }

            var lateDocument = document;
            var lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.datum-deviation");
            var changeOnStart = 1;
            var lateDisplays = 0;
            using (var lateOwner = new ToolWorkbenchDatumPlaneDeviationExecutionOwner(
                () => true, () => lateSelected, () => true, () => false, _ => publishedPlane, _ => roi, _ => true,
                id => workbench.PipelineSteps.FirstOrDefault(step => step.OutputEntityId == id), () => lateDocument, () => recipePath,
                (_, message) =>
                {
                    if (!message.Contains("Preview started", StringComparison.Ordinal)) return;
                    if (changeOnStart == 1)
                        lateDocument = document with { Steps = document.Steps.Select(step => step.Id == "step.datum-deviation"
                            ? step with { Parameters = step.Parameters.Select(parameter => parameter.Name == "MaximumPeakToValleyRawHeight" ? parameter with { Value = "0.2" } : parameter).ToArray() }
                            : step).ToArray() };
                    else if (changeOnStart == 2)
                        lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.plane");
                }, _ => lateDisplays++, () => { }, () => { }))
            {
                Check("late datum condition change rejects completion", !lateOwner.PreviewAsync().GetAwaiter().GetResult()
                    && lateOwner.CurrentOutput is null && !lateOwner.IsPreviewRunning && lateDisplays == 0, lateOwner.ExecutionSummary);
                lateDocument = document;
                lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.datum-deviation");
                changeOnStart = 2;
                Check("late datum selection change rejects completion", !lateOwner.PreviewAsync().GetAwaiter().GetResult()
                    && lateOwner.CurrentOutput is null && !lateOwner.IsPreviewRunning && lateDisplays == 0, lateOwner.ExecutionSummary);
                lateSelected = workbench.PipelineSteps.Single(step => step.Id == "step.datum-deviation");
                changeOnStart = 0;
                var recovered = lateOwner.PreviewAsync().GetAwaiter().GetResult();
                var recoveredOutput = lateOwner.CurrentOutput;
                lateOwner.Publish();
                Check("datum rejection recovers by explicit rerun", recovered && recoveredOutput?.ContentSha256 == expected.Output?.ContentSha256
                    && lateOwner.TryGetPublishedOutput("derived.datum-deviation", out var recoveredDatum)
                    && ReferenceEquals(recoveredOutput, recoveredDatum) && lateDisplays == 2, lateOwner.ExecutionSummary);
                lateDocument = document with { Selections = [planeSelection, roi with { GridRectangle = new ToolRecipeGridRectangle(0, 0, 2, 3) }] };
                Check("published datum lookup rejects changed ROI context", !lateOwner.TryGetPublishedOutput("derived.datum-deviation", out _), lateOwner.ExecutionSummary);
            }

            var disposalWorkbench = new ToolWorkbenchViewModel();
            Check("open recipe for Datum Plane Deviation disposal", disposalWorkbench.TryOpenTeachingRecipe(recipePath, out var disposalOpen), disposalOpen);
            disposalWorkbench.SelectPipelineStep("step.plane");
            var disposalPlanePreviewCompleted = disposalWorkbench.PreviewSelectedThreePointPlaneAsync().GetAwaiter().GetResult();
            disposalWorkbench.PublishSelectedStepCommand.Execute(null);
            disposalWorkbench.SelectPipelineStep("step.datum-deviation");
            var disposalDatumPreviewCompleted = disposalWorkbench.PreviewSelectedDatumPlaneDeviationAsync().GetAwaiter().GetResult();
            disposalWorkbench.PublishSelectedStepCommand.Execute(null);
            var hadDatumOutputBeforeDispose = disposalPlanePreviewCompleted
                && disposalDatumPreviewCompleted
                && disposalWorkbench.CurrentDatumPlaneDeviationOutput is not null
                && disposalWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _);
            disposalWorkbench.Dispose();
            disposalWorkbench.Dispose();
            Check(
                "Datum Plane Deviation disposal clears Preview and published state idempotently",
                hadDatumOutputBeforeDispose
                && !disposalWorkbench.IsDatumPlaneDeviationPreviewRunning
                && !disposalWorkbench.HasCurrentDatumPlaneDeviationPreview
                && !disposalWorkbench.IsDatumPlaneDeviationPreviewPublished
                && disposalWorkbench.CurrentDatumPlaneDeviationOutput is null
                && !disposalWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _)
                && !disposalWorkbench.PreviewSelectedStepCommand.CanExecute(null)
                && !disposalWorkbench.PublishSelectedStepCommand.CanExecute(null)
                && !disposalWorkbench.CancelSelectedPreviewCommand.CanExecute(null),
                $"before={hadDatumOutputBeforeDispose};running={disposalWorkbench.IsDatumPlaneDeviationPreviewRunning};current={disposalWorkbench.HasCurrentDatumPlaneDeviationPreview};published={disposalWorkbench.IsDatumPlaneDeviationPreviewPublished};output={disposalWorkbench.CurrentDatumPlaneDeviationOutput is not null};registry={disposalWorkbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _)};preview={disposalWorkbench.PreviewSelectedStepCommand.CanExecute(null)};publish={disposalWorkbench.PublishSelectedStepCommand.CanExecute(null)};cancel={disposalWorkbench.CancelSelectedPreviewCommand.CanExecute(null)}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        summary = $"3D Datum Plane Raw-Height Deviation Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static ToolRecipeSelection CreatePointSelection(C3DHeightFieldSnapshot source, string id, params (int Row, int Column)[] points) => new(
        id, id, ToolRecipeSelectionKinds.PointSet, source.EntityId, source.FrameId,
        new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), null,
        points.Select(point => CreatePoint(source, point.Row, point.Column)).ToArray(), null);

    private static ToolRecipeSelection CreateRectangleSelection(C3DHeightFieldSnapshot source, string id, int row, int column, int rows, int columns) => new(
        id, id, ToolRecipeSelectionKinds.GridRectangle, source.EntityId, source.FrameId,
        new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), new ToolRecipeGridRectangle(row, column, rows, columns), null, null);

    private static ToolRecipeSelectionPoint CreatePoint(C3DHeightFieldSnapshot source, int row, int column)
    {
        var height = source.Values.Span[(row * source.Width) + column];
        return new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", row, column), new ToolRecipeXyz(column, height, row), height);
    }

    private static bool NearlyEqual(double actual, double expected, double tolerance) => double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;
}

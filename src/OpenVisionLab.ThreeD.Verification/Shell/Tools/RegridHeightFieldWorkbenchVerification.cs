using System.IO;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class RegridHeightFieldWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Re-grid Height Field Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "RegridHeightFieldWorkbench",
            Guid.NewGuid().ToString("N"));

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
            var fixture = PlaneFlatnessLiveA3PointerSmokeFixture.Prepare(root);
            var workbench = new ToolWorkbenchViewModel();
            Check(
                "open deterministic A3 recipe",
                workbench.TryOpenTeachingRecipe(fixture.RecipePath, out var openMessage),
                openMessage);

            workbench.SelectPipelineStep(PlaneFlatnessLiveA3PointerSmokeFixture.RegridStepId);
            Check(
                "Re-grid uses typed WPG adapter",
                workbench.SelectedStepPropertyDraft is RegridHeightMapStepProperties,
                workbench.SelectedStepAdapterStatus);
            Check(
                "Re-grid waits for Published A2 without implicit execution",
                !workbench.PreviewSelectedStepCommand.CanExecute(null)
                    && !workbench.HasCurrentRegridHeightFieldPreview,
                workbench.RegridHeightFieldExecutionSummary);

            var publishedA2 = PlaneFlatnessLiveA3PointerSmokeFixture.CreatePublishedA2(
                workbench.RecipePath!);
            Check(
                "register deterministic Published A2 prerequisite",
                workbench.TryRegisterSyntheticPublishedAffineApplyOutputForSmoke(
                    publishedA2,
                    out var registrationMessage),
                registrationMessage);
            Check(
                "Re-grid becomes ready only after Published A2",
                workbench.PreviewSelectedStepCommand.CanExecute(null),
                workbench.RegridHeightFieldExecutionSummary);

            var previewPassed = workbench.PreviewSelectedRegridHeightFieldAsync()
                .GetAwaiter()
                .GetResult();
            var preview = workbench.CurrentRegridHeightFieldOutput;
            Check(
                "explicit Preview creates coverage evidence",
                previewPassed
                    && preview is { MeetsMinimumCoverage: true }
                    && preview.PopulatedCellCount > 0,
                workbench.RegridHeightFieldEvidenceSummary);

            var document = ToolRecipeDocumentStore.Load(workbench.RecipePath!);
            var direct = ToolRecipeRegridHeightFieldExecution.Execute(
                document,
                PlaneFlatnessLiveA3PointerSmokeFixture.RegridStepId,
                publishedA2);
            Check(
                "Workbench and Tools output parity",
                preview?.ContentSha256 == direct.Output?.ContentSha256,
                $"workbench={preview?.ContentSha256};tools={direct.Output?.ContentSha256}");
            Check(
                "Preview artifact remains unpublished",
                workbench.ArtifactRegistry.Any(item =>
                    item.Id == PlaneFlatnessLiveA3PointerSmokeFixture.HeightFieldEntityId
                    && item.Contract == "TransformedHeightField"
                    && item.State == "Preview"),
                workbench.ArtifactRegistrySummary);

            workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Publish reuses and registers exact Preview",
                workbench.IsRegridHeightFieldPreviewPublished
                    && workbench.TryGetPublishedRegridHeightFieldOutput(
                        PlaneFlatnessLiveA3PointerSmokeFixture.HeightFieldEntityId,
                        out var published)
                    && ReferenceEquals(preview, published),
                workbench.RegridHeightFieldExecutionSummary);
            Check(
                "published A3 becomes downstream evidence",
                workbench.ArtifactRegistry.Any(item =>
                    item.Id == PlaneFlatnessLiveA3PointerSmokeFixture.HeightFieldEntityId
                    && item.State == "Published"),
                workbench.ArtifactRegistrySummary);

            var draft = (RegridHeightMapStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MinimumCoverageRatio = 0.7d;
            workbench.MarkSelectedStepParameterDraftDirty();
            Check(
                "draft edit alone does not stale or execute",
                workbench.HasPendingStepParameterChanges
                    && !workbench.IsRegridHeightFieldPreviewStale
                    && workbench.IsRegridHeightFieldPreviewPublished,
                workbench.StepParameterEditStatus);
            Check(
                "explicit parameter Apply stales and unpublishes A3",
                workbench.TryApplySelectedStepParameterDraft(out var applyMessage)
                    && workbench.IsRegridHeightFieldPreviewStale
                    && !workbench.TryGetPublishedRegridHeightFieldOutput(
                        PlaneFlatnessLiveA3PointerSmokeFixture.HeightFieldEntityId,
                        out _),
                applyMessage);

            var replacementPath = Path.Combine(root, "replacement.c3d");
            C3DHeightFieldSnapshot.CreateForVerification(
                "source.replacement",
                2,
                2,
                [1d, 2d, 3d, 4d]).SaveC3D(replacementPath);
            workbench.SetC3DSource(replacementPath);
            Check(
                "source replacement preserves stale evidence and clears Publish",
                workbench.CurrentRegridHeightFieldOutput is not null
                    && workbench.IsRegridHeightFieldPreviewStale
                    && !workbench.IsRegridHeightFieldPreviewPublished
                    && !workbench.TryGetPublishedRegridHeightFieldOutput(
                        PlaneFlatnessLiveA3PointerSmokeFixture.HeightFieldEntityId,
                        out _),
                workbench.RegridHeightFieldExecutionSummary);

            var hadRegridPreviewBeforeDispose =
                workbench.CurrentRegridHeightFieldOutput is not null;
            workbench.Dispose();
            workbench.Dispose();
            Check(
                "Re-grid disposal clears Preview state idempotently",
                hadRegridPreviewBeforeDispose
                && !workbench.IsRegridHeightFieldPreviewRunning
                && !workbench.HasCurrentRegridHeightFieldPreview
                && !workbench.IsRegridHeightFieldPreviewPublished
                && workbench.CurrentRegridHeightFieldOutput is null
                && !workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.PublishSelectedStepCommand.CanExecute(null)
                && !workbench.CancelSelectedPreviewCommand.CanExecute(null),
                $"before={hadRegridPreviewBeforeDispose};running={workbench.IsRegridHeightFieldPreviewRunning};current={workbench.HasCurrentRegridHeightFieldPreview};published={workbench.IsRegridHeightFieldPreviewPublished};preview={workbench.PreviewSelectedStepCommand.CanExecute(null)};publish={workbench.PublishSelectedStepCommand.CanExecute(null)};cancel={workbench.CancelSelectedPreviewCommand.CanExecute(null)}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }

        summary =
            $"Re-grid Height Field Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }
}

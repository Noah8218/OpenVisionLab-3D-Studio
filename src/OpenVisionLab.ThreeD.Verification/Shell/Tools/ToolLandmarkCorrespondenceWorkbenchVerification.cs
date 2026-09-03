using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class ToolLandmarkCorrespondenceWorkbenchVerification
{
    private const string SelectionId = "selection.landmarks";
    private const string StepId = "step.landmarks";
    private const string OutputId = "derived.correspondence";

    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Landmark Correspondence Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "LandmarkCorrespondenceWorkbench",
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
            Directory.CreateDirectory(root);
            var sourcePath = Path.Combine(root, "source.c3d");
            var recipePath = Path.Combine(root, "landmark-correspondence.ov3d-recipe.json");
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.landmarks.synthetic",
                4,
                4,
                [0d, 1d, 2d, 3d, 1d, 2d, 3d, 4d, 2d, 3d, 4d, 5d, 3d, 4d, 5d, 6d]);
            source.SaveC3D(sourcePath);
            var anchors = new[]
            {
                CreateAnchor("anchor.a", source, 0d, 0d, 0d),
                CreateAnchor("anchor.b", source, 1d, 0d, 0d),
                CreateAnchor("anchor.c", source, 0d, 1d, 0d),
                CreateAnchor("anchor.d", source, 0d, 0d, 1d)
            };
            var document = CreateDocument(source, sourcePath, anchors);
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "open deterministic Landmark Correspondence recipe",
                workbench.TryOpenTeachingRecipe(recipePath, out var openMessage),
                openMessage);
            workbench.SelectPipelineStep(StepId);
            Check(
                "Preview remains blocked without four Published CornerAnchors",
                !workbench.PreviewSelectedStepCommand.CanExecute(null)
                    && workbench.CurrentLandmarkCorrespondenceOutput is null,
                workbench.LandmarkCorrespondenceUpstreamSummary);

            foreach (var anchor in anchors)
            {
                Check(
                    $"register routed Published CornerAnchor {anchor.OutputEntityId}",
                    workbench.TryRegisterSyntheticPublishedLineIntersectionOutputForSmoke(
                        anchor,
                        out var registrationMessage),
                    registrationMessage);
            }

            Check(
                "Preview becomes ready only after all four Published CornerAnchors",
                workbench.PreviewSelectedStepCommand.CanExecute(null),
                workbench.LandmarkCorrespondenceUpstreamSummary);

            C3DLandmarkCorrespondenceSet? displayedPreview = null;
            C3DLandmarkCorrespondenceSet? displayedPublished = null;
            workbench.LandmarkCorrespondenceDisplayRequested += (_, args) =>
            {
                if (args.IsPublished)
                {
                    displayedPublished = args.Output;
                }
                else
                {
                    displayedPreview = args.Output;
                }
            };
            var previewPassed = workbench.PreviewSelectedLandmarkCorrespondenceAsync()
                .GetAwaiter()
                .GetResult();
            var preview = workbench.CurrentLandmarkCorrespondenceOutput;
            Check(
                "explicit Preview creates correspondence evidence only",
                previewPassed
                    && preview is { Pairs.Count: 4, SourceRank: 4, ReferenceRank: 4 }
                    && ReferenceEquals(preview, displayedPreview)
                    && !workbench.IsLandmarkCorrespondencePreviewPublished,
                workbench.LandmarkCorrespondenceEvidenceSummary);
            var direct = ToolRecipeLandmarkCorrespondenceExecution.Execute(
                document,
                StepId,
                anchors);
            Check(
                "Workbench and Tools output parity",
                preview?.ContentSha256 == direct.Output?.ContentSha256,
                $"workbench={preview?.ContentSha256};tools={direct.Output?.ContentSha256}");
            Check(
                "Preview artifact remains unpublished",
                workbench.ArtifactRegistry.Any(item =>
                    item.Id == OutputId
                    && item.Contract == "CorrespondenceSet"
                    && item.State == "Preview"),
                workbench.ArtifactRegistrySummary);

            workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Publish registers the exact Preview without rerun",
                workbench.IsLandmarkCorrespondencePreviewPublished
                    && ReferenceEquals(preview, displayedPublished)
                    && workbench.TryGetPublishedLandmarkCorrespondenceOutput(
                        OutputId,
                        out var published)
                    && ReferenceEquals(preview, published),
                workbench.LandmarkCorrespondenceExecutionSummary);

            var firstRow = workbench.SelectedCorrespondenceRows[0];
            workbench.SelectedCorrespondenceRow = firstRow;
            var retainedPreview = workbench.CurrentLandmarkCorrespondenceOutput;
            workbench.CorrespondenceReferenceX += 0.25d;
            Check(
                "row draft edit alone does not execute or stale Preview",
                ReferenceEquals(retainedPreview, workbench.CurrentLandmarkCorrespondenceOutput)
                    && workbench.IsLandmarkCorrespondencePreviewPublished,
                "Draft value remains explicit until Add/Update.");
            var canCommitRow = workbench.AddOrUpdateCorrespondenceRowCommand.CanExecute(null);
            workbench.AddOrUpdateCorrespondenceRowCommand.Execute(null);
            Check(
                "explicit row update stales Landmark publication without rerun",
                canCommitRow
                    && workbench.IsLandmarkCorrespondencePreviewStale
                    && !workbench.IsLandmarkCorrespondencePreviewPublished
                    && !workbench.TryGetPublishedLandmarkCorrespondenceOutput(OutputId, out _)
                    && ReferenceEquals(retainedPreview, workbench.CurrentLandmarkCorrespondenceOutput),
                $"canCommit={canCommitRow} | {workbench.LandmarkCorrespondenceExecutionSummary}");

            workbench.Dispose();
            Check(
                "Landmark Correspondence disposal clears output and published registry",
                workbench.CurrentLandmarkCorrespondenceOutput is null
                    && !workbench.TryGetPublishedLandmarkCorrespondenceOutput(OutputId, out _)
                    && !workbench.IsLandmarkCorrespondencePreviewRunning,
                "disposed Landmark Correspondence owner is empty and fail-closed");
            var previewAfterDispose = workbench.PreviewSelectedLandmarkCorrespondenceAsync()
                .GetAwaiter()
                .GetResult();
            workbench.Dispose();
            Check(
                "repeated Workbench disposal rejects new Landmark execution",
                !previewAfterDispose
                    && !workbench.PreviewSelectedStepCommand.CanExecute(null),
                "second Dispose is safe; Preview and command CanExecute remain false");
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

        summary = $"Landmark Correspondence Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        IReadOnlyList<C3DLineIntersectionFeature> anchors)
    {
        var rows = anchors.Select((anchor, index) =>
            new ToolRecipeLandmarkCorrespondence(
                anchor.OutputEntityId,
                $"reference.{index + 1}",
                new ToolRecipeXyz(
                    anchor.CornerAnchorX + 10d,
                    anchor.CornerAnchorY + 20d,
                    anchor.CornerAnchorZ + 30d),
                "frame.landmarks.reference")).ToArray();
        var selection = new ToolRecipeSelection(
            SelectionId,
            "Synthetic landmark correspondence",
            ToolRecipeSelectionKinds.LandmarkCorrespondenceSet,
            source.EntityId,
            source.FrameId,
            new ToolRecipeSelectionSourceBinding(
                "C3D",
                source.ContentSha256,
                source.Width,
                source.Height),
            null,
            null,
            rows,
            new ToolRecipeLandmarkCorrespondenceDescriptor(
                "frame.landmarks.reference",
                "synthetic-unit",
                "OpenVisionLab deterministic Landmark Correspondence verification",
                "R1",
                "ExactlyFour",
                "CurrentPublishedCornerAnchor",
                "RequireNonDegenerateTetrahedra",
                1e-12));
        var fixtureSteps = anchors.Select((anchor, index) =>
            new ToolRecipeStep(
                $"step.fixture.anchor.{index + 1}",
                "fixture-line-intersection",
                $"Synthetic CornerAnchor {index + 1}",
                1,
                [source.EntityId],
                anchor.OutputEntityId,
                [])).ToList();
        fixtureSteps.Add(new ToolRecipeStep(
            StepId,
            "landmark-correspondence",
            "Landmark Correspondence",
            1,
            [SelectionId],
            OutputId,
            [
                new ToolRecipeParameter("PairCountPolicy", "ExactlyFour"),
                new ToolRecipeParameter("SourceArtifactPolicy", "CurrentPublishedCornerAnchor"),
                new ToolRecipeParameter("AffineIndependencePolicy", "RequireNonDegenerateTetrahedra")
            ]));
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Deterministic Landmark Correspondence verification",
            new ToolRecipeSource(
                source.EntityId,
                "Synthetic landmark source",
                "C3D",
                source.Unit,
                source.FrameId,
                sourcePath,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            fixtureSteps,
            [selection]);
    }

    private static C3DLineIntersectionFeature CreateAnchor(
        string outputEntityId,
        C3DHeightFieldSnapshot source,
        double x,
        double y,
        double z)
    {
        var first = CreateLine($"{outputEntityId}.line-x", source, x, y, z, 1d, 0d, 0d);
        var second = CreateLine($"{outputEntityId}.line-y", source, x, y, z, 0d, 1d, 0d);
        return C3DLineIntersectionFeature.Create(
            outputEntityId,
            first,
            second,
            0.001d,
            45d,
            1d,
            $"CornerAnchor-{outputEntityId}",
            x, y, z,
            x, y, z,
            x, y, z,
            0d, 0d,
            90d, 0d,
            -1d, 1d, 0d,
            -1d, 1d, 0d,
            "synthetic software verification only");
    }

    private static C3DTwoPointLineFeature CreateLine(
        string outputEntityId,
        C3DHeightFieldSnapshot source,
        double x,
        double y,
        double z,
        double directionX,
        double directionY,
        double directionZ) =>
        C3DTwoPointLineFeature.Create(
            outputEntityId,
            source.EntityId,
            source.RootSourceSha256,
            source.Unit,
            source.FrameId,
            $"selection.{outputEntityId}",
            source.ContentSha256,
            0, 0, 0, 1,
            x, y, z,
            directionX, directionY, directionZ,
            x + directionX,
            y + directionY,
            z + directionZ,
            1d,
            "SyntheticLine",
            "synthetic software verification only");
}

using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
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

    public static bool VerifyReferenceRevisionSave(string reportPath, out string summary)
    {
        var lines = new List<string>();
        var total = 0;
        var passed = 0;
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "landmark-save-fixtures");

        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(root);
            var sourcePath = Path.Combine(root, "source.c3d");
            var recipePath = Path.Combine(root, "revision.ov3d-recipe.json");
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.revision.synthetic", 2, 2, [1d, 2d, 3d, 4d]);
            source.SaveC3D(sourcePath);
            var sourceBytes = File.ReadAllBytes(sourcePath);
            var anchors = new[]
            {
                CreateAnchor("anchor.a", source, 0d, 0d, 0d),
                CreateAnchor("anchor.b", source, 1d, 0d, 0d),
                CreateAnchor("anchor.c", source, 0d, 1d, 0d),
                CreateAnchor("anchor.d", source, 0d, 0d, 1d)
            };
            ToolRecipeDocumentStore.Save(recipePath, CreateDocument(source, sourcePath, anchors) with { SchemaVersion = ToolRecipeDocument.DualRoiRoutingSchemaVersion });
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, "recent.json"));
            Check("open revision fixture", workbench.TryOpenTeachingRecipe(recipePath, out var message), message);
            workbench.SelectPipelineStep(StepId);
            Check("save fixture baseline", workbench.TrySaveTeachingRecipe(recipePath, out message), message);
            var before = ToolRecipeDocumentStore.Load(recipePath);
            var originalRevision = before.Selections!.Single(item => item.Id == SelectionId).CorrespondenceDescriptor!.ReferenceRevision;

            bool SameRecipeExceptRevision(ToolRecipeDocument after)
            {
                var restored = after with
                {
                    Source = after.Source with { Path = Path.GetFullPath(after.Source.Path!, root) },
                    Selections = after.Selections!.Select(item => item.Id == SelectionId
                        ? item with { CorrespondenceDescriptor = item.CorrespondenceDescriptor! with { ReferenceRevision = originalRevision } }
                        : item).ToArray()
                };
                var expected = before with { Source = before.Source with { Path = Path.GetFullPath(before.Source.Path!, root) } };
                return JsonSerializer.Serialize(expected) == JsonSerializer.Serialize(restored);
            }

            var baselineBytes = File.ReadAllBytes(recipePath);
            workbench.CorrespondenceReferenceRevision = "R2-save-review";
            Check("revision binding marks the recipe unsaved without writing disk",
                workbench.IsDirty && workbench.CorrespondenceReferenceRevision == "R2-save-review"
                && File.ReadAllBytes(recipePath).SequenceEqual(baselineBytes), workbench.HeaderRecipeSaveStateText);
            workbench.SelectPipelineStep(workbench.PipelineSteps[0].Id);
            workbench.SelectPipelineStep(StepId);
            Check("editor refresh retains the unsaved revision", workbench.CorrespondenceReferenceRevision == "R2-save-review", workbench.CorrespondenceReferenceRevision);
            Check("save the edited revision", workbench.TrySaveTeachingRecipe(recipePath, out message), message);
            var saved = ToolRecipeDocumentStore.Load(recipePath);
            Check("saved descriptor contains the edited revision",
                saved.Selections!.Single(item => item.Id == SelectionId).CorrespondenceDescriptor!.ReferenceRevision == "R2-save-review",
                saved.Selections!.Single(item => item.Id == SelectionId).CorrespondenceDescriptor!.ReferenceRevision);
            Check("saving revision preserves every other recipe value and source bytes",
                SameRecipeExceptRevision(saved) && File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes), $"Only referenceRevision may differ; legacy schema remains {before.SchemaVersion} -> {saved.SchemaVersion}.");
            Check("reopen edited revision", workbench.TryOpenTeachingRecipe(recipePath, out message), message);
            workbench.SelectPipelineStep(StepId);
            Check("reopened revision is clean and exact", !workbench.IsDirty && workbench.CorrespondenceReferenceRevision == "R2-save-review", workbench.CorrespondenceReferenceRevision);

            var savedBytes = File.ReadAllBytes(recipePath);
            var saveRequests = 0;
            workbench.SaveTeachingRecipeRequested += (_, _) => saveRequests++;
            workbench.CorrespondenceReferenceRevision = "R3-cancelled-save";
            workbench.SaveTeachingRecipeCommand.Execute(null);
            Check("cancelled save request leaves the edit unsaved and disk unchanged",
                saveRequests == 1 && workbench.IsDirty && workbench.CorrespondenceReferenceRevision == "R3-cancelled-save"
                && File.ReadAllBytes(recipePath).SequenceEqual(savedBytes), "The save-request callback deliberately performs no write, matching a cancelled save dialog.");
            var canUndo = workbench.UndoSelectedStepParametersCommand.CanExecute(null);
            workbench.UndoSelectedStepParametersCommand.Execute(null);
            Check("existing undo restores revision without changing saved bytes",
                canUndo && workbench.CorrespondenceReferenceRevision == "R2-save-review"
                && File.ReadAllBytes(recipePath).SequenceEqual(savedBytes), $"canUndo={canUndo};revision={workbench.CorrespondenceReferenceRevision}");
            workbench.CorrespondenceReferenceRevision = "R4-discarded";
            Check("explicit discard through reopen succeeds", workbench.TryOpenTeachingRecipe(recipePath, out message), message);
            workbench.SelectPipelineStep(StepId);
            Check("discard/reopen restores saved revision and clears dirty state",
                !workbench.IsDirty && workbench.CorrespondenceReferenceRevision == "R2-save-review"
                && File.ReadAllBytes(recipePath).SequenceEqual(savedBytes), workbench.CorrespondenceReferenceRevision);

            workbench.CorrespondenceReferenceRevision = string.Empty;
            Check("empty revision cannot silently save the previous value",
                !workbench.CanSaveTeachingRecipe && !workbench.TrySaveTeachingRecipe(recipePath, out message)
                && File.ReadAllBytes(recipePath).SequenceEqual(savedBytes), workbench.RecipeStateSummary);
            workbench.CorrespondenceReferenceRevision = "R2-save-review";
            Check("valid revision recovers saving", workbench.TrySaveTeachingRecipe(recipePath, out message), message);
            Check("revision edit/save/cancel/undo never executes inspection",
                workbench.CurrentLandmarkCorrespondenceOutput is null
                && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run")),
                "No Preview, Publish or Run command was requested.");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        summary = $"Landmark reference-revision save verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(0, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    public static bool VerifyReferenceRevisionFieldPresence(string legacyRecipePath, string reportPath, out string summary)
    {
        var lines = new List<string>();
        var total = 0;
        var passed = 0;
        var root = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "output-field-presence-fixtures");
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(root);
            legacyRecipePath = Path.GetFullPath(legacyRecipePath);
            var originalBytes = File.ReadAllBytes(legacyRecipePath);
            var original = ToolRecipeDocumentStore.Load(legacyRecipePath);
            var sourcePath = Path.GetFullPath(original.Source.Path, Path.GetDirectoryName(legacyRecipePath)!);
            var sourceBytes = File.ReadAllBytes(sourcePath);
            var copiedSourcePath = Path.Combine(root, Path.GetFileName(sourcePath));
            File.WriteAllBytes(copiedSourcePath, sourceBytes);
            foreach (var variant in new[] { "all-omitted", "mixed" })
            {
                var json = JsonNode.Parse(File.ReadAllText(legacyRecipePath))!;
                json["source"]!["path"] = Path.GetFileName(copiedSourcePath);
                var steps = json["steps"]!.AsArray();
                foreach (var step in steps) step!.AsObject().Remove("outputEnabled");
                if (variant == "mixed")
                {
                    steps[1]!["outputEnabled"] = true;
                    steps[2]!["outputEnabled"] = false;
                }
                var recipePath = Path.Combine(root, variant + ".ov3d-recipe.json");
                File.WriteAllText(recipePath, json.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                var expected = json.DeepClone();
                var selectedStep = original.Steps.Single(step => step.ToolId == "landmark-correspondence");
                var selectionIndex = original.Selections!.ToList().FindIndex(selection => selection.Id == selectedStep.InputEntityIds.Single());
                Check(variant + " legacy input", original.SchemaVersion == "1.5" && steps.Count == 21 && selectionIndex >= 0,
                    $"schema={original.SchemaVersion};steps={steps.Count};selection={selectionIndex}");
                using var workbench = new ToolWorkbenchViewModel(Path.Combine(root, variant + "-recent.json"));
                Check(variant + " open", workbench.TryOpenTeachingRecipe(recipePath, out var message), message);
                workbench.SelectPipelineStep(selectedStep.Id);
                workbench.CorrespondenceReferenceRevision = "R2-output-presence";
                Check(variant + " revision save", workbench.TrySaveTeachingRecipe(recipePath, out message), message);
                var saved = JsonNode.Parse(File.ReadAllText(recipePath))!;
                expected["selections"]![selectionIndex]!["correspondenceDescriptor"]!["referenceRevision"] = "R2-output-presence";
                Check(variant + " only revision changes in raw JSON", JsonNode.DeepEquals(expected, saved), "All other properties, omissions, IDs, coordinates and schema must match.");
                for (var index = 0; index < steps.Count; index++)
                {
                    var expectedStep = expected["steps"]![index]!.AsObject();
                    var savedStep = saved["steps"]![index]!.AsObject();
                    Check($"{variant} step{index} output field", expectedStep.ContainsKey("outputEnabled") == savedStep.ContainsKey("outputEnabled")
                        && JsonNode.DeepEquals(expectedStep["outputEnabled"], savedStep["outputEnabled"]),
                        $"id={original.Steps[index].Id};before={(expectedStep.ContainsKey("outputEnabled") ? expectedStep["outputEnabled"]!.ToJsonString() : "absent")};after={(savedStep.ContainsKey("outputEnabled") ? savedStep["outputEnabled"]!.ToJsonString() : "absent")}");
                }
                Check(variant + " reopen", workbench.TryOpenTeachingRecipe(recipePath, out message), message);
                workbench.SelectPipelineStep(selectedStep.Id);
                Check(variant + " reopened revision and output meaning", !workbench.IsDirty && workbench.CorrespondenceReferenceRevision == "R2-output-presence"
                    && workbench.PipelineSteps.Select(step => step.OutputEnabled).SequenceEqual(Enumerable.Range(0, steps.Count).Select(index => variant != "mixed" || index != 2)),
                    "Absent and explicit true remain enabled; explicit false remains disabled.");
                workbench.CorrespondenceReferenceRevision = "R3-output-presence";
                Check(variant + " second revision save", workbench.TrySaveTeachingRecipe(recipePath, out message), message);
                expected["selections"]![selectionIndex]!["correspondenceDescriptor"]!["referenceRevision"] = "R3-output-presence";
                Check(variant + " second raw preservation", JsonNode.DeepEquals(expected, JsonNode.Parse(File.ReadAllText(recipePath))), "Repeated revision save preserves the same field presence.");
                Check(variant + " no execution and input unchanged", workbench.CurrentLandmarkCorrespondenceOutput is null
                    && workbench.RunLog.All(item => item.Category is not ("Preview" or "Publish" or "Run"))
                    && File.ReadAllBytes(copiedSourcePath).SequenceEqual(sourceBytes), "No inspection requested; source bytes unchanged.");

                var snapshotPath = recipePath + ".scope-snapshot";
                File.Copy(recipePath, snapshotPath, overwrite: true);
                var document = ToolRecipeDocumentStore.Load(recipePath, out var savedSha256);
                var unrelatedEdit = document with
                {
                    Name = document.Name + " other edit",
                    Selections = document.Selections!.Select(selection => selection.Id == selectedStep.InputEntityIds.Single()
                        ? selection with { CorrespondenceDescriptor = selection.CorrespondenceDescriptor! with { ReferenceRevision = "R4-unrelated-edit" } }
                        : selection).ToArray()
                };
                Check(variant + " unrelated edit declines revision-only writer",
                    !ToolRecipeDocumentStore.TrySaveLandmarkReferenceRevisions(recipePath, unrelatedEdit, snapshotPath, savedSha256, out _)
                    && File.ReadAllBytes(recipePath).SequenceEqual(File.ReadAllBytes(snapshotPath)), "General edits remain with the existing save path; no write performed by this narrow path.");
            }
            Check("original fixture and source unchanged", File.ReadAllBytes(legacyRecipePath).SequenceEqual(originalBytes)
                && File.ReadAllBytes(sourcePath).SequenceEqual(sourceBytes), "Only isolated copies were edited.");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }

        summary = $"Landmark revision output-field-presence verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(0, summary);
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

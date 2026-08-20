using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolXyzAffineWorkbenchVerification
{
    private const string CorrespondenceId = "fixture.affine.correspondence";
    private const string SolveStepId = "step.affine.solve";
    private const string TransformId = "derived.affine.transform";
    private const string ApplyStepId = "step.affine.apply";
    private const string CloudId = "derived.affine.cloud";

    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D XYZ Affine Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "XyzAffineWorkbench",
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
            var recipePath = Path.Combine(root, "xyz-affine.ov3d-recipe.json");
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.affine.synthetic",
                4,
                4,
                [1d, 1.5d, 2d, 2.5d, 2d, 3d, 4d, 5d, 3d, 4.5d, 6d, 7.5d, 4d, 6d, 8d, 10d]);
            source.SaveC3D(sourcePath);
            var correspondence = CreateCorrespondence(source);
            var document = CreateDocument(source, sourcePath);
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "open deterministic XYZ Affine recipe",
                workbench.TryOpenTeachingRecipe(recipePath, out var openMessage),
                openMessage);

            workbench.SelectPipelineStep(SolveStepId);
            Check(
                "Solve waits for Published CorrespondenceSet without implicit execution",
                !workbench.PreviewSelectedStepCommand.CanExecute(null)
                    && workbench.CurrentAffineSolveOutput is null
                    && workbench.CurrentAffineApplyOutput is null,
                workbench.AffineSolveExecutionSummary);
            Check(
                "register deterministic Published CorrespondenceSet prerequisite",
                workbench.TryRegisterSyntheticPublishedLandmarkCorrespondenceOutputForSmoke(
                    correspondence,
                    out var registrationMessage),
                registrationMessage);
            Check(
                "Solve becomes ready only after Published CorrespondenceSet",
                workbench.PreviewSelectedStepCommand.CanExecute(null),
                workbench.AffineSolveExecutionSummary);

            var solvePreviewPassed = workbench.PreviewSelectedXYZAffineSolveAsync()
                .GetAwaiter()
                .GetResult();
            var solvePreview = workbench.CurrentAffineSolveOutput;
            Check(
                "explicit Solve Preview creates matrix evidence only",
                solvePreviewPassed
                    && solvePreview is { Residuals.Count: 4 }
                    && !workbench.IsAffineSolvePreviewPublished,
                workbench.AffineSolveEvidenceSummary);
            var directSolve = ToolRecipeXYZAffineSolveExecution.Execute(
                document,
                SolveStepId,
                correspondence);
            Check(
                "Solve Workbench and Tools output parity",
                solvePreview?.ContentSha256 == directSolve.Output?.ContentSha256,
                $"workbench={solvePreview?.ContentSha256};tools={directSolve.Output?.ContentSha256}");
            Check(
                "Solve Preview remains unpublished in the artifact registry",
                !workbench.ArtifactRegistry.Any(item =>
                    item.Id == TransformId
                    && item.Contract == "AffineTransform3D"
                    && item.State == "Published"),
                workbench.ArtifactRegistrySummary);

            workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Solve Publish registers the exact Preview without rerun",
                workbench.IsAffineSolvePreviewPublished
                    && workbench.TryGetPublishedAffineSolveOutput(TransformId, out var publishedTransform)
                    && ReferenceEquals(solvePreview, publishedTransform),
                workbench.AffineSolveExecutionSummary);

            workbench.SelectPipelineStep(ApplyStepId);
            Check(
                "Apply becomes ready only after explicit Solve Publish",
                workbench.PreviewSelectedStepCommand.CanExecute(null)
                    && workbench.CurrentAffineApplyOutput is null,
                workbench.AffineApplyExecutionSummary);
            var applyPreviewPassed = workbench.PreviewSelectedXYZAffineApplyAsync()
                .GetAwaiter()
                .GetResult();
            var applyPreview = workbench.CurrentAffineApplyOutput;
            Check(
                "explicit Apply Preview transforms finite source-grid points once",
                applyPreviewPassed
                    && applyPreview is { FinitePointCount: 16, MissingPointCount: 0 }
                    && applyPreview.RootSourceSha256 == source.RootSourceSha256
                    && !workbench.IsAffineApplyPreviewPublished,
                workbench.AffineApplyEvidenceSummary);
            var directApply = ToolRecipeXYZAffineApplyExecution.Execute(
                document,
                ApplyStepId,
                solvePreview!,
                root);
            Check(
                "Apply Workbench and Tools output parity",
                applyPreview?.ContentSha256 == directApply.Output?.ContentSha256,
                $"workbench={applyPreview?.ContentSha256};tools={directApply.Output?.ContentSha256}");
            Check(
                "Apply Preview artifact remains unpublished",
                workbench.ArtifactRegistry.Any(item =>
                    item.Id == CloudId
                    && item.Contract == "TransformedPointCloud"
                    && item.State == "Preview"),
                workbench.ArtifactRegistrySummary);

            workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Apply Publish registers the exact Preview without rerun",
                workbench.IsAffineApplyPreviewPublished
                    && workbench.TryGetPublishedAffineApplyOutput(CloudId, out var publishedCloud)
                    && ReferenceEquals(applyPreview, publishedCloud),
                workbench.AffineApplyExecutionSummary);

            workbench.SelectPipelineStep(SolveStepId);
            var draft = (XYZAffineSolveStepProperties)workbench.SelectedStepPropertyDraft!;
            draft.MaximumConditionEstimate = 2_000_000_000d;
            workbench.MarkSelectedStepParameterDraftDirty();
            Check(
                "Solve draft edit alone neither executes nor invalidates A1/A2",
                workbench.HasPendingStepParameterChanges
                    && workbench.IsAffineSolvePreviewPublished
                    && workbench.IsAffineApplyPreviewPublished
                    && ReferenceEquals(applyPreview, workbench.CurrentAffineApplyOutput),
                workbench.StepParameterEditStatus);
            Check(
                "explicit Solve parameter Apply stales A1 and clears A2 without rerun",
                workbench.TryApplySelectedStepParameterDraft(out var applyMessage)
                    && workbench.IsAffineSolvePreviewStale
                    && !workbench.IsAffineSolvePreviewPublished
                    && workbench.CurrentAffineApplyOutput is null
                    && !workbench.TryGetPublishedAffineApplyOutput(CloudId, out _),
                applyMessage);
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

        summary = $"XYZ Affine Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Deterministic XYZ Affine verification",
            new ToolRecipeSource(
                source.EntityId,
                "Synthetic affine source",
                "C3D",
                source.Unit,
                source.FrameId,
                sourcePath,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [
                new ToolRecipeStep(
                    "step.affine.correspondence-fixture",
                    "fixture-landmark-correspondence",
                    "Synthetic Published CorrespondenceSet prerequisite",
                    1,
                    [source.EntityId],
                    CorrespondenceId,
                    []),
                new ToolRecipeStep(
                    SolveStepId,
                    "xyz-affine-solve",
                    "XYZ Affine Solve",
                    1,
                    [CorrespondenceId],
                    TransformId,
                    [
                        new ToolRecipeParameter("SolvePolicy", "ExactFourPartialPivot"),
                        new ToolRecipeParameter("MaximumConditionEstimate", "1000000000"),
                        new ToolRecipeParameter("ArithmeticResidualWarning", "0.000000001")
                    ]),
                new ToolRecipeStep(
                    ApplyStepId,
                    "xyz-affine-apply",
                    "Apply XYZ Affine",
                    2,
                    [source.EntityId, TransformId],
                    CloudId,
                    [])
            ],
            []);

    private static C3DLandmarkCorrespondenceSet CreateCorrespondence(
        C3DHeightFieldSnapshot source)
    {
        var pairs = new[]
        {
            Pair("a", source, 0, 1, 0),
            Pair("b", source, 3, 2, 0),
            Pair("c", source, 0, 3, 3),
            Pair("d", source, 3, 8, 3)
        };
        return C3DLandmarkCorrespondenceSet.Create(
            CorrespondenceId,
            pairs,
            source.EntityId,
            source.RootSourceSha256,
            source.Unit,
            source.FrameId,
            "frame.affine.reference",
            "synthetic-unit",
            "OpenVisionLab deterministic XYZ Affine verification",
            "R1",
            1e-12,
            4,
            4,
            0.1,
            0.1,
            "synthetic software verification only");
    }

    private static C3DLandmarkCorrespondencePair Pair(
        string id,
        C3DHeightFieldSnapshot source,
        double x,
        double y,
        double z) =>
        new(
            $"source.anchor.{id}",
            "CornerAnchor",
            source.RootSourceSha256,
            x,
            y,
            z,
            $"reference.anchor.{id}",
            x + 10d,
            y + 20d,
            z + 30d);
}

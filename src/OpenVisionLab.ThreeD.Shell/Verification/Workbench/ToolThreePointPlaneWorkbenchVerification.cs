using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ToolThreePointPlaneWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D 3-Point Plane Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "ThreePointPlaneWorkbench", Guid.NewGuid().ToString("N"));
        void Check(string name, bool condition, string detail)
        {
            total++;
            if (condition) passed++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
        }

        try
        {
            Directory.CreateDirectory(root);
            var source = C3DHeightFieldSnapshot.CreateForVerification("source.synthetic", 3, 3, [1d, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d]);
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var selection = CreateSelection(source, "selection.plane-a", (0, 0), (0, 2), (2, 0));
            var document = new ToolRecipeDocument(
                ToolRecipeDocument.CurrentSchemaVersion,
                "3-Point Plane Workbench",
                new ToolRecipeSource(source.EntityId, "Synthetic", "C3D", source.Unit, source.FrameId, sourcePath, source.ByteLength, source.ContentSha256, source.Width, source.Height),
                [],
                [ThreePointPlaneStep("step.plane.a", selection.Id, "derived.plane.a", "FixtureDatum")],
                [selection]);
            var recipePath = Path.Combine(root, "three-point-plane.ov3d-teach.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var open), open);
            workbench.SelectPipelineStep("step.plane.a");
            Check("3-Point Plane uses typed WPG adapter", workbench.SelectedStepPropertyDraft is ThreePointPlaneStepProperties, workbench.SelectedStepAdapterStatus);
            Check("3-Point Plane exposes ordered PointSet(3)", workbench.ThreePointPlaneSelectionSummary.Contains("(0, 0) -> (0, 2) -> (2, 0)", StringComparison.Ordinal), workbench.ThreePointPlaneSelectionSummary);
            Check("3-Point Plane has no published downstream consumer in v1", !workbench.ArtifactRegistry.Any(item => item.Contract is "PlaneMeasurement" or "WarpageInput"), workbench.ArtifactRegistrySummary);

            C3DThreePointPlaneFeature? preview = null;
            C3DThreePointPlaneFeature? published = null;
            workbench.ThreePointPlaneDisplayRequested += (_, args) =>
            {
                if (args.Output.OutputEntityId != "derived.plane.a") return;
                if (args.IsPublished) published = args.Output;
                else preview = args.Output;
            };
            Check("3-Point Plane becomes preview-ready from raw source plus PointSet", workbench.PreviewSelectedStepCommand.CanExecute(null), workbench.ThreePointPlaneExecutionSummary);
            Check("explicit 3-Point Plane Preview", workbench.PreviewSelectedThreePointPlaneAsync().GetAwaiter().GetResult()
                && preview is not null
                && NearlyEqual(Math.Sqrt((preview.NormalX * preview.NormalX) + (preview.NormalY * preview.NormalY) + (preview.NormalZ * preview.NormalZ)), 1)
                && NearlyEqual(DotPlane(preview, preview.AnchorX, preview.AnchorY, preview.AnchorZ), 0)
                && NearlyEqual(DotPlane(preview, preview.SecondX, preview.SecondY, preview.SecondZ), 0)
                && NearlyEqual(DotPlane(preview, preview.ThirdX, preview.ThirdY, preview.ThirdZ), 0), workbench.ThreePointPlaneExecutionSummary);
            var expected = ToolRecipeThreePointPlaneExecution.Execute(document, "step.plane.a", root);
            Check("Workbench and headless share plane identity", preview?.ContentSha256 == expected.Output?.ContentSha256, $"workbench={preview?.ContentSha256};headless={expected.Output?.ContentSha256}");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Publish reuses exact 3-Point Plane Preview", workbench.IsThreePointPlanePreviewPublished && ReferenceEquals(preview, published) && workbench.TryGetPublishedThreePointPlaneOutput("derived.plane.a", out var stored) && ReferenceEquals(stored, published), workbench.ThreePointPlaneExecutionSummary);
            Check("published plane appears as a PlaneFeature artifact", workbench.ArtifactRegistry.Count(item => item.Contract == "PlaneFeature" && item.State == "Published") == 1, workbench.ArtifactRegistrySummary);

            workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "OutputRole").Value = "FixtureDatumRevised";
            Check("parameter change stales and unpublishes plane", workbench.IsThreePointPlanePreviewStale && !workbench.TryGetPublishedThreePointPlaneOutput("derived.plane.a", out _), workbench.ThreePointPlaneExecutionSummary);

            var replacementSourcePath = Path.Combine(root, "replacement.c3d");
            C3DHeightFieldSnapshot.CreateForVerification("source.replacement", 2, 2, [1d, 2d, 3d, 4d]).SaveC3D(replacementSourcePath);
            workbench.SetC3DSource(replacementSourcePath);
            Check("source replacement clears plane evidence", !workbench.TryGetPublishedThreePointPlaneOutput("derived.plane.a", out _) && workbench.CurrentThreePointPlaneOutput is null, workbench.ThreePointPlaneExecutionSummary);
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

        summary = $"3D 3-Point Plane Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static ToolRecipeStep ThreePointPlaneStep(string id, string selectionId, string outputId, string outputRole) =>
        new(id, "three-point-plane", "3-Point Plane", 1, ["source.synthetic", selectionId], outputId, [new("OutputRole", outputRole), new("ConstructionPolicy", "OrderedPointsDefineOrientedPlane")]);

    private static ToolRecipeSelection CreateSelection(C3DHeightFieldSnapshot source, string id, (int Row, int Column) first, (int Row, int Column) second, (int Row, int Column) third) =>
        new(id, id, ToolRecipeSelectionKinds.PointSet, source.EntityId, source.FrameId, new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height), null,
            [CreatePoint(source, first.Row, first.Column), CreatePoint(source, second.Row, second.Column), CreatePoint(source, third.Row, third.Column)], null);

    private static ToolRecipeSelectionPoint CreatePoint(C3DHeightFieldSnapshot source, int row, int column)
    {
        var height = source.Values.Span[row * source.Width + column];
        return new ToolRecipeSelectionPoint(new ToolRecipeGridCellLocator("grid-cell", row, column), new ToolRecipeXyz(column, height, row), height);
    }

    private static double DotPlane(C3DThreePointPlaneFeature plane, double x, double y, double z) => (plane.NormalX * x) + (plane.NormalY * y) + (plane.NormalZ * z) + plane.PlaneOffset;
    private static bool NearlyEqual(double actual, double expected) => double.IsFinite(actual) && Math.Abs(actual - expected) <= 0.000001;
}

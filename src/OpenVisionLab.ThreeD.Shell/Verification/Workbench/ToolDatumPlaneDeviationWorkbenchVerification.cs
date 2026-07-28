using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

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
            var expected = ToolRecipeDatumPlaneDeviationExecution.Execute(document, "step.datum-deviation", publishedPlane!, root);
            Check("Workbench and headless share datum result identity", preview?.ContentSha256 == expected.Output?.ContentSha256, $"workbench={preview?.ContentSha256};headless={expected.Output?.ContentSha256}");
            workbench.PublishSelectedStepCommand.Execute(null);
            Check("Publish reuses exact datum Preview", workbench.IsDatumPlaneDeviationPreviewPublished && ReferenceEquals(preview, published)
                && workbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out var stored) && ReferenceEquals(stored, published), workbench.DatumPlaneDeviationExecutionSummary);
            Check("published datum result becomes a Tool Lab artifact", workbench.IsSelectedToolLabAvailable
                && workbench.ArtifactRegistry.Count(item => item.Contract == "DatumPlaneDeviationResult" && item.State == "Published") == 1, workbench.ArtifactRegistrySummary);

            workbench.SelectPipelineStep("step.plane");
            workbench.SelectedPipelineStep!.Parameters.Single(parameter => parameter.Name == "OutputRole").Value = "FixtureDatumRevised";
            Check("upstream Plane change stales and unpublishes datum result", workbench.IsDatumPlaneDeviationPreviewStale
                && !workbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _), workbench.DatumPlaneDeviationExecutionSummary);

            var replacementSourcePath = Path.Combine(root, "replacement.c3d");
            C3DHeightFieldSnapshot.CreateForVerification("source.replacement", 2, 2, [1d, 2d, 3d, 4d]).SaveC3D(replacementSourcePath);
            workbench.SetC3DSource(replacementSourcePath);
            Check("source replacement clears datum result", workbench.CurrentDatumPlaneDeviationOutput is null
                && !workbench.TryGetPublishedDatumPlaneDeviationOutput("derived.datum-deviation", out _), workbench.DatumPlaneDeviationExecutionSummary);
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

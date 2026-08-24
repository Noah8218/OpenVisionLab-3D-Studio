using System.Security.Cryptography;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class RoiCropWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string> { "OpenVisionLab 3D ROI / Crop Workbench verification" };
        var passed = 0;
        var total = 0;
        var root = Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "RoiCropWorkbench", Guid.NewGuid().ToString("N"));

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
            CreateSource().SaveC3D(sourcePath);
            var sourceBytesBefore = File.ReadAllBytes(sourcePath);
            var source = C3DHeightFieldSnapshot.LoadIdentified(
                sourcePath, "source.roi-crop", "raw-height", "frame.c3d-grid-index");
            var cropOnlyDocument = CreateDocument(source, Path.GetFileName(sourcePath));
            var expectedCrop = ToolRecipeRoiCropExecution.Execute(
                cropOnlyDocument,
                "step.roi-crop.01",
                root).Output
                ?? throw new InvalidDataException("The deterministic ROI / Crop fixture did not produce its typed output.");
            var document = CreateDocument(source, Path.GetFileName(sourcePath), expectedCrop);
            var recipePath = Path.Combine(root, "roi-crop.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "catalog exposes typed crop preparation",
                workbench.Tools.Any(tool => tool.Id == "roi-crop"
                    && tool.Category == "Prepare"
                    && tool.OutputContract == "HeightField"),
                "Prepare -> ROI / Crop -> HeightField");
            Check("open typed recipe", workbench.TryOpenTeachingRecipe(recipePath, out var openMessage), openMessage);
            workbench.SelectPipelineStep("step.roi-crop.01");
            Check(
                "Preview is explicit",
                workbench.PreviewSelectedStepCommand.CanExecute(null)
                    && !workbench.HasCurrentRoiCropPreview,
                $"state={workbench.SelectedPipelineStep?.State}");

            string? displayPath = null;
            string? displayHash = null;
            string? displayLabel = null;
            workbench.FilterDisplayRequested += (_, args) =>
            {
                displayPath = args.C3DPath;
                displayHash = args.ContentSha256;
                displayLabel = args.DisplayLabel;
            };
            var preview = workbench.PreviewSelectedRoiCropAsync().GetAwaiter().GetResult();
            var output = workbench.CurrentRoiCropPreviewOutput;
            Check(
                "Preview creates a smaller derived HeightField",
                preview && output is
                {
                    Width: 3,
                    Height: 3,
                    GridOriginColumn: 2,
                    GridOriginRow: 1,
                    ValidCount: 8,
                    MissingCount: 1
                },
                workbench.RoiCropOutputSummary);
            var expected = new[] { 9d, 10d, 11d, 15d, double.NaN, 17d, 21d, 22d, 23d };
            Check(
                "row-major finite and missing cells are exact",
                output is not null && output.Values.Span.SequenceEqual(expected),
                output is null ? "no output" : string.Join(",", output.Values.ToArray().Select(value => double.IsNaN(value) ? "NaN" : value.ToString("G17"))));
            Check(
                "source identity and source-grid origin are preserved",
                output?.RootSourceSha256 == source.ContentSha256
                    && output.FrameId == source.FrameId
                    && output.Unit == source.Unit,
                $"root={output?.RootSourceSha256};frame={output?.FrameId};origin={output?.GridOriginColumn},{output?.GridOriginRow}");
            Check(
                "Viewer request uses exact Preview bytes",
                displayPath == workbench.CurrentRoiCropPreviewPath
                    && displayHash == output?.ContentSha256
                    && displayLabel == "ROI / Crop Preview"
                    && File.Exists(displayPath),
                $"label={displayLabel};path={displayPath};hash={displayHash}");

            var direct = ToolRecipeRoiCropExecution.Execute(document, "step.roi-crop.01", root);
            Check(
                "Workbench and Tools output parity",
                output?.ContentSha256 == direct.Output?.ContentSha256,
                $"workbench={output?.ContentSha256};tools={direct.Output?.ContentSha256}");
            Check(
                "metrics and source ROI overlay are immutable evidence",
                direct.Result.Metrics.Count == 6
                    && direct.Result.Overlays.Count == 1
                    && direct.Result.Overlays[0].SourceEntityId == source.EntityId
                    && direct.SourceRegion == new ToolRecipeGridRectangle(1, 2, 3, 3),
                $"metrics={direct.Result.Metrics.Count};overlays={direct.Result.Overlays.Count};region={direct.SourceRegion}");
            var artifact = workbench.ArtifactRegistry.FirstOrDefault(item => item.Id == "derived.roi-crop.01");
            Check(
                "artifact and compare registry expose the crop",
                artifact is { Contract: "HeightField", NodeKind: "HeightField" }
                    && workbench.CompareCandidates.Any(candidate => candidate.Id == artifact.Id
                        && candidate.C3DPath == workbench.CurrentRoiCropPreviewPath),
                artifact?.Detail ?? "missing artifact");

            var previewHash = output!.ContentSha256;
            workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Publish reuses Preview without rerun",
                workbench.IsRoiCropPreviewPublished
                    && workbench.CurrentRoiCropPreviewOutput?.ContentSha256 == previewHash,
                workbench.RoiCropExecutionSummary);
            Check(
                "Published crop is available by exact output identity",
                workbench.TryGetPublishedRoiCropOutput("derived.roi-crop.01", out var publishedCrop)
                    && publishedCrop?.ContentSha256 == previewHash,
                $"output={publishedCrop?.EntityId};sha={publishedCrop?.ContentSha256}");

            workbench.SelectPipelineStep("step.warpage.01");
            ToolWorkbenchTeachingCaptureRequestEventArgs? captureRequest = null;
            workbench.BeginTeachingSelectionCaptureRequested += (_, args) => captureRequest = args;
            Check(
                "compatible later tool can teach and Preview from Published crop",
                workbench.BeginTeachingSelectionCaptureCommand.CanExecute(null)
                    && workbench.PreviewSelectedStepCommand.CanExecute(null),
                $"step={workbench.SelectedPipelineStep?.ToolId};state={workbench.SelectedPipelineStep?.State}");
            workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            Check(
                "later ROI is owned by exact cropped HeightField",
                captureRequest?.SourceBinding.Format == "HeightField"
                    && captureRequest.SourceBinding.OwnerEntityId == "derived.roi-crop.01"
                    && captureRequest.SourceBinding.ContentSha256 == previewHash
                    && captureRequest.SourceBinding.GridWidth == 3
                    && captureRequest.SourceBinding.GridHeight == 3,
                $"format={captureRequest?.SourceBinding.Format};owner={captureRequest?.SourceBinding.OwnerEntityId};grid={captureRequest?.SourceBinding.GridWidth}x{captureRequest?.SourceBinding.GridHeight}");
            var measurementPreview = workbench.PreviewSelectedMeasurementAsync().GetAwaiter().GetResult();
            Check(
                "later Warpage executes against cropped bytes",
                measurementPreview
                    && workbench.CurrentMeasurementOutput?.InputEntityId == "derived.roi-crop.01"
                    && workbench.CurrentMeasurementOutput?.Result.Status == ResultStatus.Pass,
                workbench.MeasurementExecutionSummary);
            Check(
                "source bytes and active source remain unchanged",
                sourceBytesBefore.SequenceEqual(File.ReadAllBytes(sourcePath))
                    && workbench.Source.Id == source.EntityId
                    && Path.GetFullPath(workbench.Source.Path) == Path.GetFullPath(sourcePath),
                $"source={workbench.Source.Id};sha={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(sourcePath)))}");

            var savePath = Path.Combine(root, "saved-roi-crop.ov3d-recipe.json");
            var saved = workbench.TrySaveTeachingRecipe(savePath, out var saveMessage);
            var reopened = saved ? ToolRecipeDocumentStore.Load(savePath) : null;
            var reopenedOutput = reopened is null
                ? null
                : ToolRecipeRoiCropExecution.Execute(reopened, "step.roi-crop.01", root).Output;
            Check(
                "save and reopen preserve region and output identity",
                reopened?.Selections?.Single(selection => selection.Id == "selection.roi-crop.01").GridRectangle == new ToolRecipeGridRectangle(1, 2, 3, 3)
                    && reopenedOutput?.ContentSha256 == previewHash,
                saveMessage);

            var ordered = ToolRecipeOrderedGraphExecution.Execute(document, sourcePath);
            Check(
                "ordered Shell and Runner execution path reproduces the crop",
                ordered.Status == ResultStatus.Pass
                    && ordered.Steps.Count == 2
                    && ordered.Steps[0].ToolId == "roi-crop"
                    && ordered.Steps[0].OutputContentSha256 == previewHash
                    && ordered.Steps[1].ToolId == "warpage"
                    && ordered.Steps[1].Result.Status == ResultStatus.Pass,
                ordered.Message);

            workbench.SelectPipelineStep("step.roi-crop.01");
            var changed = workbench.Selections.Single(selection => selection.Id == "selection.roi-crop.01") with
            {
                GridRectangle = new ToolRecipeGridRectangle(0, 0, 2, 2)
            };
            workbench.BeginTeachingSelectionCaptureCommand.Execute(null);
            var applied = workbench.TryApplyCapturedTeachingSelection(changed, out var applyMessage);
            Check(
                "ROI edit invalidates Published Preview without execution",
                applied
                    && workbench.IsRoiCropPreviewStale
                    && !workbench.IsRoiCropPreviewPublished
                    && workbench.IsMeasurementPreviewStale
                    && !workbench.HasCurrentMeasurementPreview
                    && workbench.CurrentRoiCropPreviewOutput?.ContentSha256 == previewHash,
                $"{applyMessage} | {workbench.RoiCropExecutionSummary}");
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

        summary = $"ROI / Crop Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static C3DHeightFieldSnapshot CreateSource()
    {
        var values = Enumerable.Range(1, 30).Select(value => (double)value).ToArray();
        values[2 * 6 + 3] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification("source.roi-crop", 6, 5, values);
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        C3DHeightFieldSnapshot? croppedOutput = null)
    {
        var selection = new ToolRecipeSelection(
            "selection.roi-crop.01",
            "Crop region",
            ToolRecipeSelectionKinds.GridRectangle,
            source.EntityId,
            source.FrameId,
            new ToolRecipeSelectionSourceBinding("C3D", source.ContentSha256, source.Width, source.Height),
            new ToolRecipeGridRectangle(1, 2, 3, 3),
            null,
            null);
        var selections = new List<ToolRecipeSelection> { selection };
        var steps = new List<ToolRecipeStep>
        {
            new(
                "step.roi-crop.01",
                "roi-crop",
                "ROI / Crop",
                1,
                [source.EntityId, selection.Id],
                "derived.roi-crop.01",
                [new("ROI", "Select in Viewer"), new("Output frame", "Keep source frame")])
        };
        if (croppedOutput is not null)
        {
            var measurementSelection = new ToolRecipeSelection(
                "selection.warpage.01",
                "Cropped inspection region",
                ToolRecipeSelectionKinds.GridRectangle,
                source.EntityId,
                croppedOutput.FrameId,
                ToolRecipeSelectionSourceBindingVerifier.FromHeightField(croppedOutput),
                new ToolRecipeGridRectangle(0, 0, croppedOutput.Height, croppedOutput.Width),
                null,
                null);
            selections.Add(measurementSelection);
            steps.Add(new ToolRecipeStep(
                "step.warpage.01",
                "warpage",
                "Warpage",
                1,
                [croppedOutput.EntityId, measurementSelection.Id],
                "result.warpage.01",
                [
                    new("MaximumPeakToValley", "100"),
                    new("MaximumRms", "100"),
                    new("MinimumValidSampleCount", "3")
                ]));
        }

        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "ROI Crop Workbench",
            new ToolRecipeSource(
                source.EntityId,
                "Crop Source",
                "C3D",
                source.Unit,
                source.FrameId,
                sourcePath,
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            steps,
            selections);
    }
}

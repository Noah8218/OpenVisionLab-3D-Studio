using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class RemoveOutlierPixelsWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Remove Outlier Pixels Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var rootDirectory = Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "RemoveOutlierWorkbench",
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
            Directory.CreateDirectory(rootDirectory);
            var source = CreateSource();
            var sourcePath = Path.Combine(rootDirectory, "source.c3d");
            source.SaveC3D(sourcePath);
            var document = CreateDocument(source, sourcePath);
            var recipePath = Path.Combine(
                rootDirectory,
                "remove-outliers.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(recipePath, document);

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "catalog exposes preparation tool",
                workbench.Tools.Any(
                    tool => tool.Id == "remove-outlier-pixels"
                            && tool.Category == "Prepare"
                            && tool.OutputContract == "FilteredHeightField"),
                "typed Prepare catalog entry");
            Check(
                "open typed recipe",
                workbench.TryOpenTeachingRecipe(recipePath, out var openMessage),
                openMessage);
            workbench.SelectPipelineStep("step.remove-outliers.01");
            Check(
                "typed PropertyGrid draft",
                workbench.SelectedStepPropertyDraft
                    is RemoveOutlierPixelsStepProperties
                    {
                        WindowSize: 3,
                        MaximumAbsoluteDeviation: 20,
                        MinimumValidNeighbors: 3
                    },
                workbench.SelectedStepAdapterStatus);
            Check(
                "Preview enabled but no implicit execution",
                workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.HasCurrentRemoveOutlierPreview,
                $"state={workbench.SelectedPipelineStep?.State}");

            string? displayPath = null;
            string? displayHash = null;
            workbench.FilterDisplayRequested += (_, args) =>
            {
                displayPath = args.C3DPath;
                displayHash = args.ContentSha256;
            };
            var preview = workbench.PreviewSelectedRemoveOutlierPixelsAsync()
                .GetAwaiter()
                .GetResult();
            Check(
                "explicit Preview creates output and mask",
                preview
                && workbench.CurrentRemoveOutlierPreviewOutput is not null
                && workbench.CurrentRemoveOutlierMask?.OutlierCellCount == 2,
                workbench.RemoveOutlierOutputSummary);
            Check(
                "Viewer request uses exact Preview bytes",
                displayPath == workbench.CurrentRemoveOutlierPreviewPath
                && displayHash
                    == workbench.CurrentRemoveOutlierPreviewOutput?.ContentSha256
                && File.Exists(displayPath),
                $"path={displayPath};hash={displayHash}");

            var direct = ToolRecipeRemoveOutlierPixelsExecution.Execute(
                document,
                "step.remove-outliers.01",
                rootDirectory);
            Check(
                "Workbench and Tools output parity",
                workbench.CurrentRemoveOutlierPreviewOutput?.ContentSha256
                    == direct.Output?.ContentSha256,
                $"workbench={workbench.CurrentRemoveOutlierPreviewOutput?.ContentSha256};tools={direct.Output?.ContentSha256}");
            Check(
                "Workbench and Tools mask parity",
                workbench.CurrentRemoveOutlierMask?.Sha256
                    == direct.OutlierMask?.Sha256,
                $"workbench={workbench.CurrentRemoveOutlierMask?.Sha256};tools={direct.OutlierMask?.Sha256}");
            Check(
                "source remains immutable",
                source.ContentSha256
                    == workbench.CurrentRemoveOutlierPreviewOutput?.RootSourceSha256
                && source.ValidCount == 63
                && source.MissingCount == 1,
                $"source={source.ContentSha256};root={workbench.CurrentRemoveOutlierPreviewOutput?.RootSourceSha256}");
            var artifact = workbench.ArtifactRegistry.FirstOrDefault(
                item => item.Id == "derived.outlier-removed.01");
            Check(
                "artifact exposes before-after mask evidence",
                artifact?.Detail.Contains(
                    "removed 2",
                    StringComparison.Ordinal) == true
                && artifact.Detail.Contains(
                    workbench.CurrentRemoveOutlierMask!.Sha256,
                    StringComparison.Ordinal),
                artifact?.Detail ?? "missing artifact");
            Check(
                "output is Viewer/compare renderable",
                workbench.CompareCandidates.Any(
                    candidate =>
                        candidate.Id == "derived.outlier-removed.01"
                        && candidate.C3DPath
                            == workbench.CurrentRemoveOutlierPreviewPath),
                $"candidate={workbench.CurrentRemoveOutlierPreviewPath}");

            workbench.PublishSelectedStepCommand.Execute(null);
            Check(
                "Publish reuses Preview without rerun",
                workbench.IsRemoveOutlierPreviewPublished
                && workbench.CurrentRemoveOutlierPreviewOutput?.ContentSha256
                    == direct.Output?.ContentSha256,
                workbench.RemoveOutlierExecutionSummary);

            var draft = (RemoveOutlierPixelsStepProperties)
                workbench.SelectedStepPropertyDraft!;
            draft.MaximumAbsoluteDeviation = 30d;
            workbench.MarkSelectedStepParameterDraftDirty();
            Check(
                "draft edit alone does not stale or run",
                workbench.HasPendingStepParameterChanges
                && !workbench.IsRemoveOutlierPreviewStale
                && workbench.IsRemoveOutlierPreviewPublished,
                workbench.StepParameterEditStatus);
            Check(
                "explicit parameter Apply marks Preview stale",
                workbench.TryApplySelectedStepParameterDraft(out var applyMessage)
                && workbench.IsRemoveOutlierPreviewStale
                && !workbench.IsRemoveOutlierPreviewPublished,
                applyMessage);
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
            total++;
        }
        finally
        {
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, true);
            }
        }

        summary =
            $"Remove Outlier Pixels Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(
            Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        return passed == total;
    }

    private static C3DHeightFieldSnapshot CreateSource()
    {
        const int width = 8;
        const int height = 8;
        var values = Enumerable.Repeat(100d, width * height).ToArray();
        values[2 * width + 2] = 150d;
        values[5 * width + 6] = 40d;
        values[4 * width + 4] = double.NaN;
        return C3DHeightFieldSnapshot.CreateForVerification(
            "source.outlier-workbench",
            width,
            height,
            values);
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Remove Outlier Workbench",
            new ToolRecipeSource(
                source.EntityId,
                "Outlier Workbench Source",
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
                    "step.remove-outliers.01",
                    "remove-outlier-pixels",
                    "Remove Outlier Pixels",
                    1,
                    [source.EntityId],
                    "derived.outlier-removed.01",
                    [
                        new("Rule", "LocalMedianAbsoluteDeviation"),
                        new("WindowSize", "3"),
                        new("MaximumAbsoluteDeviation", "20"),
                        new("MinimumValidNeighbors", "3"),
                        new("MissingValuePolicy", "PreserveMask"),
                        new("BoundaryPolicy", "AvailableNeighbors"),
                        new("OutlierPolicy", "SetMissing")
                    ])
            ],
            []);
}

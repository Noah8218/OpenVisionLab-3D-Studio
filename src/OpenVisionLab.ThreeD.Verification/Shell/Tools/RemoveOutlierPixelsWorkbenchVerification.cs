using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

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
        var configuredTestRoot = Environment.GetEnvironmentVariable(
            "OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var testDataRoot = string.IsNullOrWhiteSpace(configuredTestRoot)
            ? Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "RemoveOutlierWorkbench")
            : Path.GetFullPath(configuredTestRoot);
        var rootDirectory = Path.Combine(
            testDataRoot,
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
            workbench.SourceQuality.EnsureSourceAsync(
                sourcePath,
                workbench.Source.Id,
                workbench.Source.Unit,
                workbench.Source.FrameId,
                cancellationToken => workbench.SourceSession.GetOrLoadDecodedSourceAsync(
                    workbench.Source.Path,
                    workbench.Source.Id,
                    workbench.Source.Unit,
                    workbench.Source.FrameId,
                    cancellationToken)).GetAwaiter().GetResult();
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
                && artifact.PreparationQualityDelta is
                {
                    BeforeValidSampleCount: 63,
                    BeforeMissingSampleCount: 1,
                    DetectedOutlierCount: 2,
                    SourceIdentityRetained: true
                }
                && artifact.PreparationQualityDelta.AfterValidSampleCount
                    == workbench.CurrentRemoveOutlierPreviewOutput?.ValidCount
                && artifact.PreparationQualityDelta.AfterMissingSampleCount
                    == workbench.CurrentRemoveOutlierPreviewOutput?.MissingCount
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

            var connectedSourceBytesBefore = File.ReadAllBytes(sourcePath);
            var connectedDocument = CreateConnectedDocument(source, sourcePath);
            var connectedRecipePath = Path.Combine(
                rootDirectory,
                "connected-region.ov3d-recipe.json");
            ToolRecipeDocumentStore.Save(connectedRecipePath, connectedDocument);
            var connectedWorkbench = new ToolWorkbenchViewModel();
            Check(
                "connected-region recipe opens with typed catalog route",
                connectedWorkbench.TryOpenTeachingRecipe(connectedRecipePath, out var connectedOpenMessage)
                && connectedWorkbench.Tools.Any(
                    tool => tool.Id == "connected-region"
                        && tool.Category == "Feature & Datum"
                        && tool.OutputContract == "ConnectedRegionArtifact"),
                connectedOpenMessage);
            connectedWorkbench.SelectPipelineStep("step.connected-region.01");
            Check(
                "Connected Region waits for Published upstream",
                connectedWorkbench.SelectedStepPropertyDraft is ConnectedRegionStepProperties
                && !connectedWorkbench.PreviewSelectedStepCommand.CanExecute(null)
                && !connectedWorkbench.HasCurrentConnectedRegionPreview,
                connectedWorkbench.ConnectedRegionExecutionSummary);

            connectedWorkbench.SelectPipelineStep("step.remove-outliers.01");
            Check(
                "Remove Outlier Preview remains explicit in two-step recipe",
                connectedWorkbench.PreviewSelectedStepCommand.CanExecute(null),
                connectedWorkbench.RemoveOutlierExecutionSummary);
            Check(
                "Remove Outlier Preview succeeds before downstream route",
                connectedWorkbench.PreviewSelectedRemoveOutlierPixelsAsync()
                    .GetAwaiter()
                    .GetResult(),
                connectedWorkbench.RemoveOutlierExecutionSummary);
            connectedWorkbench.SelectPipelineStep("step.connected-region.01");
            Check(
                "Connected Region still blocks on Preview-only upstream",
                !connectedWorkbench.PreviewSelectedStepCommand.CanExecute(null)
                && !connectedWorkbench.HasCurrentConnectedRegionPreview,
                connectedWorkbench.ConnectedRegionExecutionSummary);

            connectedWorkbench.SelectPipelineStep("step.remove-outliers.01");
            connectedWorkbench.PublishSelectedStepCommand.Execute(null);
            var publishedMaskHash = connectedWorkbench.CurrentRemoveOutlierMask?.Sha256;
            var publishedOutputHash = connectedWorkbench.CurrentRemoveOutlierPreviewOutput?.ContentSha256;
            connectedWorkbench.SelectPipelineStep("step.connected-region.01");
            Check(
                "Connected Region enables after upstream Publish",
                connectedWorkbench.PreviewSelectedStepCommand.CanExecute(null),
                connectedWorkbench.ConnectedRegionExecutionSummary);
            Check(
                "Connected Region Preview creates typed artifact without implicit Publish",
                connectedWorkbench.PreviewSelectedConnectedRegionAsync()
                    .GetAwaiter()
                    .GetResult()
                && connectedWorkbench.CurrentConnectedRegionArtifact is { Regions.Count: > 0 }
                && !connectedWorkbench.IsConnectedRegionPreviewPublished,
                connectedWorkbench.ConnectedRegionExecutionSummary);
            var connectedPreviewHash = connectedWorkbench.CurrentConnectedRegionArtifact?.ContentSha256;
            var connectedPreviewRegistry = connectedWorkbench.ArtifactRegistry.FirstOrDefault(
                item => item.Id == "derived.connected-regions.01");
            Check(
                "Connected Region Preview is registered with mask/source evidence",
                connectedPreviewRegistry?.Contract == "ConnectedRegionArtifact"
                && connectedPreviewRegistry.State == "Preview"
                && publishedMaskHash is not null
                && connectedPreviewRegistry.Detail.Contains(publishedMaskHash, StringComparison.Ordinal)
                && publishedOutputHash is not null
                && connectedPreviewRegistry.Detail.Contains(publishedOutputHash, StringComparison.Ordinal)
                && connectedWorkbench.CurrentConnectedRegionArtifact?.RootSourceSha256 == source.ContentSha256,
                $"contract={connectedPreviewRegistry?.Contract};state={connectedPreviewRegistry?.State};expectedMask={publishedMaskHash};expectedOutput={publishedOutputHash};expectedRoot={source.ContentSha256};detail={connectedPreviewRegistry?.Detail ?? "missing connected-region artifact"}");

            connectedWorkbench.PublishSelectedStepCommand.Execute(null);
            var connectedSidecarPath = connectedWorkbench.CurrentConnectedRegionArtifactPath;
            var savedConnectedArtifact = !string.IsNullOrWhiteSpace(connectedSidecarPath)
                && File.Exists(connectedSidecarPath)
                ? C3DConnectedRegionArtifactStore.Load(connectedSidecarPath)
                : null;
            Check(
                "Connected Region Publish persists one sidecar without rerun",
                connectedWorkbench.IsConnectedRegionPreviewPublished
                && savedConnectedArtifact?.ContentSha256 == connectedPreviewHash
                && savedConnectedArtifact?.MaskContentSha256 == publishedMaskHash,
                connectedWorkbench.ConnectedRegionExecutionSummary);
            Check(
                "Connected Region recipe save preserves sidecar",
                connectedWorkbench.TrySaveTeachingRecipe(connectedRecipePath, out var connectedSaveMessage)
                && connectedWorkbench.CurrentConnectedRegionArtifactPath == connectedSidecarPath
                && File.Exists(connectedSidecarPath),
                connectedSaveMessage);

            var reopenedConnectedWorkbench = new ToolWorkbenchViewModel();
            var reopenedConnected = reopenedConnectedWorkbench.TryOpenTeachingRecipe(
                connectedRecipePath,
                out var reopenedConnectedMessage);
            reopenedConnectedWorkbench.SelectPipelineStep("step.connected-region.01");
            Check(
                "Connected Region sidecar restores Published artifact without execution",
                reopenedConnected
                && reopenedConnectedWorkbench.IsConnectedRegionPreviewPublished
                && reopenedConnectedWorkbench.CurrentConnectedRegionArtifact?.ContentSha256 == connectedPreviewHash
                && reopenedConnectedWorkbench.CurrentConnectedRegionArtifactPath == connectedSidecarPath
                && !reopenedConnectedWorkbench.IsConnectedRegionPreviewRunning
                && reopenedConnectedWorkbench.CurrentRemoveOutlierPreviewOutput is null,
                $"open={reopenedConnectedMessage};summary={reopenedConnectedWorkbench.ConnectedRegionExecutionSummary};path={reopenedConnectedWorkbench.CurrentConnectedRegionArtifactPath}");
            var connectedSourceBytesAfter = File.ReadAllBytes(sourcePath);
            Check(
                "Connected Region route keeps source file immutable",
                connectedSourceBytesBefore.SequenceEqual(connectedSourceBytesAfter)
                && reopenedConnectedWorkbench.CurrentConnectedRegionArtifact?.RootSourceSha256 == source.ContentSha256,
                $"bytesUnchanged={connectedSourceBytesBefore.SequenceEqual(connectedSourceBytesAfter)};root={reopenedConnectedWorkbench.CurrentConnectedRegionArtifact?.RootSourceSha256}");

            var hadRemoveOutlierPreviewBeforeDispose =
                workbench.CurrentRemoveOutlierPreviewOutput is not null
                && workbench.CurrentRemoveOutlierMask is not null;
            workbench.Dispose();
            workbench.Dispose();
            Check(
                "Remove Outlier disposal clears Preview state idempotently",
                hadRemoveOutlierPreviewBeforeDispose
                && !workbench.IsRemoveOutlierPreviewRunning
                && !workbench.HasCurrentRemoveOutlierPreview
                && !workbench.IsRemoveOutlierPreviewPublished
                && workbench.CurrentRemoveOutlierPreviewOutput is null
                && workbench.CurrentRemoveOutlierMask is null
                && !workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.PublishSelectedStepCommand.CanExecute(null)
                && !workbench.CancelSelectedPreviewCommand.CanExecute(null),
                $"before={hadRemoveOutlierPreviewBeforeDispose};running={workbench.IsRemoveOutlierPreviewRunning};current={workbench.HasCurrentRemoveOutlierPreview};published={workbench.IsRemoveOutlierPreviewPublished};preview={workbench.PreviewSelectedStepCommand.CanExecute(null)};publish={workbench.PublishSelectedStepCommand.CanExecute(null)};cancel={workbench.CancelSelectedPreviewCommand.CanExecute(null)}");

            var hadConnectedRegionArtifactBeforeDispose =
                connectedWorkbench.CurrentConnectedRegionArtifact is not null;
            connectedWorkbench.Dispose();
            connectedWorkbench.Dispose();
            Check(
                "Connected Region disposal clears artifact state idempotently",
                hadConnectedRegionArtifactBeforeDispose
                && !connectedWorkbench.IsConnectedRegionPreviewRunning
                && !connectedWorkbench.HasCurrentConnectedRegionPreview
                && !connectedWorkbench.IsConnectedRegionPreviewPublished
                && connectedWorkbench.CurrentConnectedRegionArtifact is null
                && !connectedWorkbench.PreviewSelectedStepCommand.CanExecute(null)
                && !connectedWorkbench.PublishSelectedStepCommand.CanExecute(null)
                && !connectedWorkbench.CancelSelectedPreviewCommand.CanExecute(null),
                $"before={hadConnectedRegionArtifactBeforeDispose};running={connectedWorkbench.IsConnectedRegionPreviewRunning};current={connectedWorkbench.HasCurrentConnectedRegionPreview};published={connectedWorkbench.IsConnectedRegionPreviewPublished};preview={connectedWorkbench.PreviewSelectedStepCommand.CanExecute(null)};publish={connectedWorkbench.PublishSelectedStepCommand.CanExecute(null)};cancel={connectedWorkbench.CancelSelectedPreviewCommand.CanExecute(null)}");

            reopenedConnectedWorkbench.Dispose();
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

    private static ToolRecipeDocument CreateConnectedDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath)
    {
        var recipe = CreateDocument(source, sourcePath);
        return recipe with
        {
            Name = "Connected Region Workbench",
            Steps = recipe.Steps
                .Concat(
                [
                    new ToolRecipeStep(
                        "step.connected-region.01",
                        "connected-region",
                        "Connected Region",
                        1,
                        ["derived.outlier-removed.01"],
                        "derived.connected-regions.01",
                        [
                            new("Connectivity", "Four"),
                            new("OriginX", "0"),
                            new("OriginY", "0"),
                            new("ColumnPitch", "1"),
                            new("RowPitch", "1"),
                            new("AreaUnit", "grid-unit^2")
                        ])
                ])
                .ToArray()
        };
    }
}

using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

internal static class DomainMaskWorkbenchVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Domain / Mask Workbench verification"
        };
        var passed = 0;
        var total = 0;
        var configuredTestRoot = Environment.GetEnvironmentVariable(
            "OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var testDataRoot = string.IsNullOrWhiteSpace(configuredTestRoot)
            ? Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "DomainMaskWorkbench")
            : Path.GetFullPath(configuredTestRoot);
        var rootDirectory = Path.Combine(testDataRoot, Guid.NewGuid().ToString("N"));
        var previousArtifactRoot = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");

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
            Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", rootDirectory);

            var source = CreateSource();
            var sourcePath = Path.Combine(rootDirectory, "source.c3d");
            source.SaveC3D(sourcePath);
            var recipePath = Path.Combine(rootDirectory, "domain-mask.ov3d-recipe.json");
            var document = CreateDocument(source, sourcePath);
            ToolRecipeDocumentStore.Save(recipePath, document);
            var sourceBytesBefore = File.ReadAllBytes(sourcePath);

            var workbench = new ToolWorkbenchViewModel();
            Check(
                "catalog exposes D-07 Prepare route",
                workbench.Tools.Any(tool =>
                    tool.Id == "domain-mask"
                    && tool.Category == "Prepare"
                    && tool.MinimumInputCount == 2
                    && tool.OutputContract == "HeightField"),
                "Prepare / Domain / Mask / HeightField");
            Check(
                "open typed route",
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
                "upstream Remove Outlier is explicit",
                workbench.PreviewSelectedRemoveOutlierPixelsAsync().GetAwaiter().GetResult()
                && !workbench.IsRemoveOutlierPreviewPublished,
                workbench.RemoveOutlierExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);

            workbench.SelectPipelineStep("step.connected-region.01");
            Check(
                "Connected Region is explicit and ready after upstream Publish",
                workbench.PreviewSelectedConnectedRegionAsync().GetAwaiter().GetResult()
                && !workbench.IsConnectedRegionPreviewPublished,
                workbench.ConnectedRegionExecutionSummary);
            workbench.PublishSelectedStepCommand.Execute(null);
            var connectedArtifact = workbench.CurrentConnectedRegionArtifact;
            Check(
                "Published ConnectedRegionArtifact is the D-07 domain",
                workbench.IsConnectedRegionPreviewPublished
                && connectedArtifact is { Regions.Count: > 0 },
                workbench.ConnectedRegionExecutionSummary);

            workbench.SelectPipelineStep("step.domain-mask.01");
            Check(
                "D-07 typed property contract is read-only and parameter-free",
                workbench.SelectedStepPropertyDraft is DomainMaskStepProperties
                && workbench.IsSelectedStepPropertyGridSupported
                && workbench.SelectedPipelineStep?.Parameters.Count == 0,
                workbench.SelectedStepAdapterStatus);
            Check(
                "D-07 Preview is enabled without implicit execution",
                workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.HasCurrentDomainMaskPreview,
                workbench.DomainMaskExecutionSummary);

            var previewed = workbench.PreviewSelectedDomainMaskAsync().GetAwaiter().GetResult();
            var domainOutput = workbench.CurrentDomainMaskPreviewOutput;
            var expectedInput = workbench.CurrentRemoveOutlierPreviewOutput;
            var direct = expectedInput is not null && connectedArtifact is not null
                ? ToolRecipeDomainMaskExecution.Execute(
                    document,
                    "step.domain-mask.01",
                    expectedInput,
                    connectedArtifact)
                : null;
            Check(
                "explicit Preview creates separate same-grid output",
                previewed
                && domainOutput is not null
                && !workbench.IsDomainMaskPreviewPublished
                && domainOutput.EntityId == "derived.domain-mask.01"
                && domainOutput.Width == source.Width
                && domainOutput.Height == source.Height,
                workbench.DomainMaskOutputSummary);
            Check(
                "Workbench and Tools output identity parity",
                domainOutput?.ContentSha256 == direct?.Output?.ContentSha256,
                $"workbench={domainOutput?.ContentSha256};tools={direct?.Output?.ContentSha256}");
            Check(
                "domain mask preserves root and source bytes",
                domainOutput?.RootSourceSha256 == source.ContentSha256
                && sourceBytesBefore.SequenceEqual(File.ReadAllBytes(sourcePath)),
                $"root={domainOutput?.RootSourceSha256};bytesUnchanged={sourceBytesBefore.SequenceEqual(File.ReadAllBytes(sourcePath))}");
            var domainArtifact = workbench.ArtifactRegistry.FirstOrDefault(item =>
                item.Id == "derived.domain-mask.01");
            Check(
                "artifact registry exposes HeightField evidence",
                domainArtifact?.Contract == "HeightField"
                && domainArtifact.State == "Preview"
                && domainArtifact.Detail.Contains("domain-reduced", StringComparison.Ordinal),
                domainArtifact?.Detail ?? "missing artifact");
            Check(
                "output is Viewer/compare renderable",
                domainOutput is not null
                && workbench.CompareCandidates.Any(candidate =>
                    candidate.Id == domainOutput.EntityId
                    && candidate.C3DPath == workbench.CurrentDomainMaskPreviewPath),
                workbench.CurrentDomainMaskPreviewPath ?? "no preview path");

            workbench.PublishSelectedStepCommand.Execute(null);
            var publishedHash = workbench.CurrentDomainMaskPreviewOutput?.ContentSha256;
            var sidecarPath = Path.Combine(
                rootDirectory,
                "domain-mask.ov3d-recipe.domain-mask.derived_domain-mask_01.json");
            var c3dPath = Path.Combine(
                rootDirectory,
                "domain-mask.ov3d-recipe.domain-mask.derived_domain-mask_01.c3d");
            Check(
                "Publish reuses Preview and persists output sidecar",
                workbench.IsDomainMaskPreviewPublished
                && publishedHash == domainOutput?.ContentSha256
                && File.Exists(sidecarPath)
                && File.Exists(c3dPath),
                workbench.DomainMaskExecutionSummary);
            Check(
                "recipe save keeps D-07 sidecar",
                workbench.TrySaveTeachingRecipe(recipePath, out var saveMessage)
                && File.Exists(sidecarPath)
                && File.Exists(c3dPath),
                saveMessage);

            var reopened = new ToolWorkbenchViewModel();
            var reopenedOk = reopened.TryOpenTeachingRecipe(recipePath, out var reopenedMessage);
            reopened.SelectPipelineStep("step.domain-mask.01");
            Check(
                "save/reopen restores Published D-07 output without execution",
                reopenedOk
                && reopened.IsDomainMaskPreviewPublished
                && reopened.CurrentDomainMaskPreviewOutput?.ContentSha256 == publishedHash
                && !reopened.IsDomainMaskPreviewRunning
                && reopened.CurrentDomainMaskPreviewPath == c3dPath,
                $"open={reopenedMessage};summary={reopened.DomainMaskExecutionSummary};path={reopened.CurrentDomainMaskPreviewPath}");
            Check(
                "ordered Run remains explicit and reaches D-07",
                reopened.RunTeachingRecipeAsync().GetAwaiter().GetResult()
                && reopened.CurrentOrderedRunResult?.Status is not (ResultStatus.Error or ResultStatus.NotRun)
                && reopened.CurrentOrderedRunResult?.Steps.Any(step =>
                    step.ToolId == "domain-mask"
                    && step.Result.Status == ResultStatus.Pass) == true,
                reopened.OrderedRunCapabilitySummary);
            Check(
                "reopened route keeps source file immutable",
                sourceBytesBefore.SequenceEqual(File.ReadAllBytes(sourcePath)),
                $"bytesUnchanged={sourceBytesBefore.SequenceEqual(File.ReadAllBytes(sourcePath))}");

            var hadDomainMaskOutputBeforeDispose =
                workbench.CurrentDomainMaskPreviewOutput is not null;
            workbench.Dispose();
            workbench.Dispose();
            Check(
                "Domain / Mask disposal clears output state idempotently",
                hadDomainMaskOutputBeforeDispose
                && !workbench.IsDomainMaskPreviewRunning
                && !workbench.HasCurrentDomainMaskPreview
                && !workbench.IsDomainMaskPreviewPublished
                && workbench.CurrentDomainMaskPreviewOutput is null
                && !workbench.PreviewSelectedStepCommand.CanExecute(null)
                && !workbench.PublishSelectedStepCommand.CanExecute(null)
                && !workbench.CancelSelectedPreviewCommand.CanExecute(null),
                $"before={hadDomainMaskOutputBeforeDispose};running={workbench.IsDomainMaskPreviewRunning};current={workbench.HasCurrentDomainMaskPreview};published={workbench.IsDomainMaskPreviewPublished};preview={workbench.PreviewSelectedStepCommand.CanExecute(null)};publish={workbench.PublishSelectedStepCommand.CanExecute(null)};cancel={workbench.CancelSelectedPreviewCommand.CanExecute(null)}");

            reopened.Dispose();
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected exception | {exception}");
            total++;
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT", previousArtifactRoot);
            if (Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, true);
            }
        }

        summary =
            $"Domain / Mask Workbench verification: {(passed == total ? "PASS" : "FAIL")} ({passed}/{total})";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
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
            "source.domain-mask-workbench",
            width,
            height,
            values);
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Domain Mask Workbench",
            new ToolRecipeSource(
                source.EntityId,
                "Domain Mask Workbench Source",
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
                    ]),
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
                    ]),
                new ToolRecipeStep(
                    "step.domain-mask.01",
                    "domain-mask",
                    "Domain / Mask",
                    2,
                    ["derived.outlier-removed.01", "derived.connected-regions.01"],
                    "derived.domain-mask.01",
                    [])
            ],
            []);
}

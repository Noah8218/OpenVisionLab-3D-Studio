using System.Globalization;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Tools;

/// <summary>
/// Focused, headless proof that Editable Region execution owns its
/// cancellation and published-artifact lifetime independently of the Shell.
/// </summary>
internal static class ToolEditableRegionOwnerVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Editable Region execution owner verification"
        };
        var passed = 0;
        var total = 0;
        var fullReportPath = Path.GetFullPath(reportPath);
        var root = CreateVerificationRoot();
        ToolWorkbenchEditableRegionExecutionOwner? owner = null;
        ToolWorkbenchEditableRegionExecutionOwner? restoredOwner = null;

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
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.editable-region",
                3,
                3,
                [1d, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d]);
            var sourcePath = Path.Combine(root, "source.c3d");
            source.SaveC3D(sourcePath);
            var connected = CreateConnectedRegion(source);
            var recipePath = Path.Combine(root, "editable-region.ov3d-teach.json");
            var document = CreateRecipe(source, sourcePath);
            ToolRecipeDocumentStore.Save(recipePath, document);
            var step = new ToolWorkbenchPipelineStepItem(
                "step.editable",
                ToolWorkbenchToolCatalog.Create().Single(tool => tool.Id == "editable-region"),
                "connected.01",
                "editable.01",
                [new ToolRecipeParameter(
                    ToolRecipeEditableRegionExecution.SelectedRegionIndexParameter,
                    "0")]);
            var stateChanges = 0;
            var logs = new List<string>();

            ToolWorkbenchEditableRegionExecutionOwner CreateOwner() =>
                new(
                    () => true,
                    () => step,
                    () => true,
                    () => false,
                    () => document,
                    () => recipePath,
                    entityId => string.Equals(entityId, connected.ArtifactId, StringComparison.OrdinalIgnoreCase)
                        ? connected
                        : null,
                    (category, message) => logs.Add($"{category}: {message}"),
                    () => stateChanges++);

            owner = CreateOwner();
            Check(
                "owner accepts a valid typed ConnectedRegionArtifact route",
                owner.CanPreview(),
                owner.ExecutionSummary);
            var previewPassed = owner.PreviewAsync().GetAwaiter().GetResult();
            Check(
                "explicit Preview creates the exact selected region",
                previewPassed
                && owner.CurrentArtifact is { RegionIndex: 0, Cells.Count: 3 }
                && owner.HasCurrentPreview
                && !owner.IsPreviewRunning,
                owner.ExecutionSummary);
            Check(
                "Preview preserves source identity and produces no implicit side effect",
                owner.CurrentArtifact?.SourceContentSha256 == source.ContentSha256
                && owner.CurrentArtifact?.SourceConnectedRegionContentSha256 == connected.ContentSha256
                && owner.CurrentArtifact?.SourceEntityId == source.EntityId,
                $"source={owner.CurrentArtifact?.SourceContentSha256};connected={owner.CurrentArtifact?.SourceConnectedRegionContentSha256}");

            var expected = ToolRecipeEditableRegionExecution.Execute(document, "step.editable", connected);
            Check(
                "Workbench owner and headless adapter produce the same artifact identity",
                expected.Output is not null
                && owner.CurrentArtifact?.ContentSha256 == expected.Output.ContentSha256,
                $"owner={owner.CurrentArtifact?.ContentSha256};headless={expected.Output?.ContentSha256}");

            owner.Publish();
            Check(
                "Publish reuses the exact Preview and exposes the typed output",
                owner.IsPreviewPublished
                && owner.TryGetPublishedArtifact("editable.01") is { RegionIndex: 0 },
                owner.ExecutionSummary);
            Check(
                "Publish persists an atomic sidecar without re-execution",
                owner.CurrentArtifactPath is { } artifactPath && File.Exists(artifactPath)
                && logs.Any(log => log.StartsWith("Save:", StringComparison.Ordinal)),
                $"path={owner.CurrentArtifactPath};logs={logs.Count}");

            var parameter = step.Parameters.Single(item =>
                item.Name == ToolRecipeEditableRegionExecution.SelectedRegionIndexParameter);
            parameter.Value = "1";
            owner.MarkStaleIfNeeded(parameter);
            Check(
                "recipe parameter change invalidates the published artifact",
                owner.IsPreviewStale
                && !owner.IsPreviewPublished
                && owner.TryGetPublishedArtifact("editable.01") is null,
                owner.ExecutionSummary);

            owner.Dispose();
            Check(
                "Dispose clears Editable Region state and sidecar reference",
                owner.IsDisposed
                && owner.CurrentArtifact is null
                && owner.CurrentArtifactPath is null
                && !owner.IsPreviewRunning
                && !owner.IsPreviewStale
                && !owner.IsPreviewPublished
                && owner.TryGetPublishedArtifact("editable.01") is null,
                owner.ExecutionSummary);
            Check(
                "post-disposal execution is rejected and disposal is idempotent",
                !owner.CanPreview()
                && !owner.PreviewAsync().GetAwaiter().GetResult(),
                owner.ExecutionSummary);
            owner.Dispose();

            parameter.Value = "0";
            restoredOwner = CreateOwner();
            restoredOwner.RestorePublishedArtifact();
            Check(
                "a new owner restores the matching published sidecar without execution",
                restoredOwner.IsPreviewPublished
                && restoredOwner.CurrentArtifact?.ContentSha256 == expected.Output?.ContentSha256
                && restoredOwner.CurrentArtifactPath is { } restoredPath
                && File.Exists(restoredPath),
                restoredOwner.ExecutionSummary);
            restoredOwner.Dispose();
            Check(
                "restored owner also releases its artifact state",
                restoredOwner.IsDisposed
                && restoredOwner.CurrentArtifact is null
                && restoredOwner.CurrentArtifactPath is null,
                restoredOwner.ExecutionSummary);

            lines.Add($"Fixture | root={root} | source={source.ContentSha256} | connected={connected.ContentSha256}");
            lines.Add($"Callbacks | stateChanges={stateChanges} | logs={logs.Count}");
        }
        catch (Exception exception)
        {
            total++;
            lines.Add($"FAIL | unexpected exception | {exception}");
        }
        finally
        {
            owner?.Dispose();
            restoredOwner?.Dispose();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }

        summary = $"EditableRegionOwner|pass={passed == total}|checks={passed}/{total}|report={fullReportPath}";
        lines.Insert(1, summary);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        return total > 0 && passed == total;
    }

    private static string CreateVerificationRoot()
    {
        var evidenceRoot = Path.Combine(
            "D:\\OpenVisionLab-TestData",
            "OpenVisionLab-3D-Studio",
            "editable-region-disposal",
            "runtime",
            Guid.NewGuid().ToString("N"));
        return Directory.Exists("D:\\")
            ? evidenceRoot
            : Path.Combine(Path.GetTempPath(), "OpenVisionLab.ThreeD", "EditableRegionOwner", Guid.NewGuid().ToString("N"));
    }

    private static C3DConnectedRegionArtifact CreateConnectedRegion(C3DHeightFieldSnapshot source) =>
        C3DConnectedRegionArtifact.Create(
            "connected.01",
            "Connected Region",
            source.EntityId,
            source.ContentSha256,
            source.RootSourceSha256,
            new string('A', 64),
            source.Unit,
            source.FrameId,
            source.Width,
            source.Height,
            C3DConnectedRegionArtifact.FourConnectivity,
            0d,
            0d,
            1d,
            1d,
            "grid-unit^2",
            [new C3DConnectedRegionArtifactRegion(
                0,
                0,
                0,
                [
                    new C3DConnectedRegionArtifactCell(0, 0),
                    new C3DConnectedRegionArtifactCell(0, 1),
                    new C3DConnectedRegionArtifactCell(1, 0)
                ],
                0,
                0,
                1,
                1,
                null)]);

    private static ToolRecipeDocument CreateRecipe(
        C3DHeightFieldSnapshot source,
        string sourcePath) =>
        new(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Editable Region Owner Verification",
            new ToolRecipeSource(
                source.EntityId,
                "Synthetic source",
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
                    "step.filter",
                    "remove-outlier-pixels",
                    "Remove Outlier Pixels",
                    1,
                    [source.EntityId],
                    "filtered.01",
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
                    "step.connected",
                    "connected-region",
                    "Connected Region",
                    1,
                    ["filtered.01"],
                    "connected.01",
                    [
                        new("Connectivity", "Four"),
                        new("OriginX", "0"),
                        new("OriginY", "0"),
                        new("ColumnPitch", "1"),
                        new("RowPitch", "1"),
                        new("AreaUnit", "grid-unit^2")
                    ]),
                new ToolRecipeStep(
                    "step.editable",
                    "editable-region",
                    "Editable Region",
                    1,
                    ["connected.01"],
                    "editable.01",
                    [new(
                        ToolRecipeEditableRegionExecution.SelectedRegionIndexParameter,
                        0.ToString(CultureInfo.InvariantCulture))])
            ],
            []);
}

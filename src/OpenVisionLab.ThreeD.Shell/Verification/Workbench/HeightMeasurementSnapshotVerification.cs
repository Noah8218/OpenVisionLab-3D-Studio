using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class HeightMeasurementSnapshotVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureRoot = Path.Combine(reportDirectory, $"height-measurement-snapshot-{Guid.NewGuid():N}");
        var lines = new List<string>
        {
            "OpenVisionLab 3D Height Measurement snapshot verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Boundary|The UI thread captures the document, step route, immutable input artifacts, and recipe directory before Task.Run; a result is discarded when the current recipe fingerprint no longer matches that start snapshot.",
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            var sourceContractPath = FindHeightMeasurementOwnerPath();
            Check(
                "source-contract-path-is-available",
                sourceContractPath is not null,
                sourceContractPath ?? "not found from the built Shell base directory");

            if (sourceContractPath is not null)
            {
                var ownerSource = File.ReadAllText(sourceContractPath);
                var taskStart = ownerSource.IndexOf(
                    "var evaluation = await Task.Run(",
                    StringComparison.Ordinal);
                var workerStart = ownerSource.IndexOf(
                    "() => ToolRecipeHeightMeasurementExecution.Execute(",
                    taskStart,
                    StringComparison.Ordinal);
                var workerEnd = ownerSource.IndexOf(
                    "cancellationToken),",
                    workerStart,
                    StringComparison.Ordinal);
                var snapshotNames = new[]
                {
                    "documentSnapshot",
                    "stepIdSnapshot",
                    "inputEntityIdsSnapshot",
                    "sourceEntityIdSnapshot",
                    "croppedHeightFieldSnapshot",
                    "transformedHeightFieldSnapshot",
                    "editableRegionSnapshot",
                    "recipeDirectorySnapshot",
                    "executionFingerprint",
                };
                var declarationsBeforeWorker = taskStart >= 0
                    && snapshotNames.All(name =>
                        ownerSource.IndexOf($"var {name}", StringComparison.Ordinal) >= 0
                        && ownerSource.IndexOf($"var {name}", StringComparison.Ordinal) < taskStart);
                Check(
                    "ui-snapshot-declarations-precede-task-run",
                    declarationsBeforeWorker,
                    $"taskStart={taskStart};names={string.Join(',', snapshotNames)}");

                var workerBody = workerStart >= 0 && workerEnd > workerStart
                    ? ownerSource[workerStart..workerEnd]
                    : string.Empty;
                var forbiddenWorkerReads = new[]
                {
                    "createDocument()",
                    "getSelectedPipelineStep()",
                    "getSourceEntityId()",
                    "getPublishedCroppedHeightField(",
                    "getPublishedHeightField(",
                    "getPublishedEditableRegion(",
                    "getRecipePath()",
                    "GetCurrentCroppedHeightField()",
                    "GetCurrentTransformedHeightField()",
                    "GetCurrentEditableRegion()",
                    "GetRecipeDirectory()",
                };
                var workerUsesOnlySnapshots = workerBody.Length > 0
                    && forbiddenWorkerReads.All(read => !workerBody.Contains(read, StringComparison.Ordinal));
                Check(
                    "worker-uses-snapshot-values-only",
                    workerUsesOnlySnapshots,
                    $"workerStart={workerStart};workerEnd={workerEnd};forbiddenReads={string.Join(',', forbiddenWorkerReads.Where(workerBody.Contains))}");
            }

            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "source.c3d");
            var recipePath = Path.Combine(fixtureRoot, "height-measurement.ov3d-recipe.json");
            var fixtureSource = C3DHeightFieldSnapshot.CreateForVerification(
                "source.height-measurement-snapshot",
                4,
                4,
                Enumerable.Range(1, 16).Select(value => (double)value).ToArray(),
                "fixture-unit",
                "frame.raw");
            fixtureSource.SaveC3D(sourcePath);
            var initialInputIds = new[] { fixtureSource.EntityId, "selection.reference", "selection.measurement" };
            var initialParameters = new[]
            {
                new ToolRecipeParameter("MinimumThickness", "0"),
                new ToolRecipeParameter("MaximumThickness", "100000"),
                new ToolRecipeParameter("MinimumValidSampleCount", "1"),
            };
            var initialDocument = CreateDocument(fixtureSource, sourcePath, initialInputIds, initialParameters);
            var tool = ToolWorkbenchToolCatalog.Create().Single(item => item.Id == "thickness");
            var step = new ToolWorkbenchPipelineStepItem(
                "step.thickness",
                tool,
                string.Join("; ", initialInputIds),
                "measurement.output",
                initialDocument.Steps[0].Parameters);
            var recipePathReads = 0;
            var mutationObserved = false;
            var logs = new List<string>();
            ToolWorkbenchHeightMeasurementExecutionOwner? owner = null;
            owner = new ToolWorkbenchHeightMeasurementExecutionOwner(
                () => true,
                () => step,
                () => false,
                () =>
                {
                    recipePathReads++;
                    if (recipePathReads == 2)
                    {
                        step.Parameters.Single(parameter => parameter.Name == "MinimumThickness").Value = "1";
                        mutationObserved = true;
                    }

                    return recipePath;
                },
                () => fixtureSource.EntityId,
                _ => null,
                _ => null,
                _ => null,
                outputEntityId => string.Equals(step.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
                    ? step
                    : null,
                () => CreateDocument(
                    fixtureSource,
                    sourcePath,
                    step.InputEntityIds,
                    step.Parameters.Select(parameter => new ToolRecipeParameter(parameter.Name, parameter.Value)).ToArray()),
                (category, message) => logs.Add($"{category}:{message}"),
                _ => { },
                () => { });

            try
            {
                var previewResult = Task.Run(() => owner.PreviewAsync()).GetAwaiter().GetResult();
                var staleLog = logs.Any(log => log.Contains("start snapshot is no longer current", StringComparison.Ordinal));
                Check(
                    "changed-parameter-invalidates-in-flight-result",
                    mutationObserved
                    && !previewResult
                    && !owner.IsPreviewRunning
                    && !owner.HasCurrentPreview
                    && owner.CurrentOutput is null
                    && !owner.IsPreviewPublished
                    && step.State == "Preview stale"
                    && owner.ExecutionSummary.Contains("start snapshot", StringComparison.Ordinal)
                    && staleLog,
                    $"mutated={mutationObserved};previewResult={previewResult};running={owner.IsPreviewRunning};hasPreview={owner.HasCurrentPreview};stepState={step.State};summary={owner.ExecutionSummary};staleLog={staleLog}");
            }
            finally
            {
                owner.Dispose();
            }
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected | {exception}");
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, recursive: true);
            }
        }

        var succeeded = passed == total && total > 0 && !lines.Any(line => line.StartsWith("FAIL | unexpected", StringComparison.Ordinal));
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"HeightMeasurementSnapshot|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static string? FindHeightMeasurementOwnerPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "OpenVisionLab.ThreeD.Shell",
                "ViewModels",
                "Workbench",
                "ToolWorkbenchHeightMeasurementExecutionOwner.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath,
        IReadOnlyList<string> inputEntityIds,
        IReadOnlyList<ToolRecipeParameter> parameters)
    {
        var binding = ToolRecipeSelectionSourceBindingVerifier.ReadIdentity(sourcePath);
        var reference = new ToolRecipeSelection(
            "selection.reference",
            "Reference ROI",
            ToolRecipeSelectionKinds.GridRectangle,
            source.EntityId,
            source.FrameId,
            binding,
            new ToolRecipeGridRectangle(0, 0, 2, 2),
            null,
            null);
        var measurement = reference with
        {
            Id = "selection.measurement",
            Name = "Measurement ROI",
            GridRectangle = new ToolRecipeGridRectangle(2, 2, 2, 2)
        };
        return new ToolRecipeDocument(
            ToolRecipeDocument.CurrentSchemaVersion,
            "Height measurement snapshot fixture",
            new ToolRecipeSource(
                source.EntityId,
                "Snapshot fixture",
                "C3D",
                source.Unit,
                source.FrameId,
                Path.GetFileName(sourcePath),
                source.ByteLength,
                source.ContentSha256,
                source.Width,
                source.Height),
            [],
            [new ToolRecipeStep(
                "step.thickness",
                "thickness",
                "Thickness",
                3,
                inputEntityIds.ToArray(),
                "measurement.output",
                parameters.ToArray(),
                new ToolRecipeDualRoiRouting(reference.Id, measurement.Id))],
            [reference, measurement]);
    }
}

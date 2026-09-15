using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class HeightMeasurementCancellationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var reportDirectory = Path.GetDirectoryName(fullReportPath) ?? Environment.CurrentDirectory;
        var fixtureRoot = Path.Combine(reportDirectory, $"height-measurement-cancel-{Guid.NewGuid():N}");
        var lines = new List<string>();
        var passed = false;

        try
        {
            Directory.CreateDirectory(fixtureRoot);
            var sourcePath = Path.Combine(fixtureRoot, "source.c3d");
            var recipePath = Path.Combine(fixtureRoot, "height-measurement.ov3d-recipe.json");
            var source = C3DHeightFieldSnapshot.CreateForVerification(
                "source.height-measurement-cancellation",
                4,
                4,
                Enumerable.Range(1, 16).Select(value => (double)value).ToArray(),
                "fixture-unit",
                "frame.raw");
            source.SaveC3D(sourcePath);
            var document = CreateDocument(source, sourcePath);
            var tool = ToolWorkbenchToolCatalog.Create().Single(item => item.Id == "thickness");
            var step = new ToolWorkbenchPipelineStepItem(
                "step.thickness",
                tool,
                $"{source.EntityId}; selection.reference; selection.measurement",
                "measurement.output",
                document.Steps[0].Parameters);
            var logs = new List<string>();
            var presented = new List<ToolRecipeHeightMeasurementOutput?>();
            ToolWorkbenchHeightMeasurementExecutionOwner? owner = null;
            owner = new ToolWorkbenchHeightMeasurementExecutionOwner(
                () => true,
                () => step,
                () => false,
                () => recipePath,
                () => source.EntityId,
                _ => null,
                _ => null,
                _ => null,
                outputEntityId => string.Equals(step.OutputEntityId, outputEntityId, StringComparison.OrdinalIgnoreCase)
                    ? step
                    : null,
                () => document,
                (category, message) => logs.Add($"{category}:{message}"),
                output =>
                {
                    presented.Add(output);
                    if (output is not null)
                    {
                        owner!.Cancel();
                    }
                },
                () => { });

            try
            {
                var previewResult = Task.Run(() => owner.PreviewAsync()).GetAwaiter().GetResult();
                owner.Publish();
                var outputPresented = presented.Any(output => output is not null);
                var presentationCleared = presented.Any(output => output is null);
                var canceledSummary = owner.ExecutionSummary.Contains("canceled", StringComparison.OrdinalIgnoreCase);
                var canceledLog = logs.Any(log => log.Contains("Preview canceled", StringComparison.Ordinal));
                passed = !previewResult
                    && !owner.IsPreviewRunning
                    && !owner.HasCurrentPreview
                    && owner.CurrentOutput is null
                    && !owner.IsPreviewPublished
                    && step.State != "Preview ready"
                    && step.State != "Published"
                    && outputPresented
                    && presentationCleared
                    && canceledSummary
                    && canceledLog;
                lines.Add($"late-cancel|pass={passed}|previewResult={previewResult}|running={owner.IsPreviewRunning}|hasPreview={owner.HasCurrentPreview}|published={owner.IsPreviewPublished}|stepState={step.State}|presented={presented.Count}|outputPresented={outputPresented}|presentationCleared={presentationCleared}|summary={owner.ExecutionSummary}|canceledLog={canceledLog}");
            }
            finally
            {
                owner.Dispose();
            }
        }
        catch (Exception exception)
        {
            lines.Add($"unexpected|type={exception.GetType().Name}|message={exception.Message}");
            passed = false;
        }
        finally
        {
            if (Directory.Exists(fixtureRoot))
            {
                Directory.Delete(fixtureRoot, true);
            }
        }

        Directory.CreateDirectory(reportDirectory);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"HeightMeasurementCancellation|pass={passed}|report={fullReportPath}";
        return passed;
    }

    private static ToolRecipeDocument CreateDocument(
        C3DHeightFieldSnapshot source,
        string sourcePath)
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
            "Height measurement cancellation fixture",
            new ToolRecipeSource(
                source.EntityId,
                "Cancellation fixture",
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
                [source.EntityId, reference.Id, measurement.Id],
                "measurement.output",
                [
                    new("MinimumThickness", "0"),
                    new("MaximumThickness", "100000"),
                    new("MinimumValidSampleCount", "1")
                ],
                new ToolRecipeDualRoiRouting(reference.Id, measurement.Id))],
            [reference, measurement]);
    }
}

using System.IO;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

internal static class ShellViewerOnlyImportSmoke
{
    public static async Task<bool> RunAsync(
        OpenVisionThreeDViewerControl viewer,
        ToolWorkbenchViewModel workbench,
        ShellWorkbenchLifecycleController lifecycle,
        string path,
        string? reportPath)
    {
        var sourceBefore = workbench.Source.Path;
        var stepsBefore = workbench.PipelineSteps.Count;
        var dirtyBefore = workbench.IsDirty;
        var loaded = await lifecycle.LoadViewerOnlySourceAsync(path, showFailureDialog: false);
        var expectedPath = Path.GetFullPath(path);
        var initialPass = loaded
            && string.Equals(viewer.CurrentViewerOnlySourcePath, expectedPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                viewer.CurrentViewerOnlySourceFormat,
                Path.GetExtension(path).TrimStart('.'),
                StringComparison.OrdinalIgnoreCase)
            && workbench.HasViewerOnlyImport
            && workbench.Source.Path == sourceBefore
            && workbench.PipelineSteps.Count == stepsBefore
            && workbench.IsDirty == dirtyBefore;

        var corruptPath = Path.Combine(
            Path.GetTempPath(),
            $"corrupt-{Guid.NewGuid():N}{Path.GetExtension(path)}");
        var failurePreserved = false;
        try
        {
            await File.WriteAllBytesAsync(corruptPath, [0x00, 0x01, 0x02, 0x03]);
            var corruptLoaded = await lifecycle.LoadViewerOnlySourceAsync(
                corruptPath,
                showFailureDialog: false);
            failurePreserved = !corruptLoaded
                && string.Equals(viewer.CurrentViewerOnlySourcePath, expectedPath, StringComparison.OrdinalIgnoreCase)
                && workbench.ViewerOnlyImportSummary.Contains(Path.GetFileName(path), StringComparison.Ordinal)
                && workbench.Source.Path == sourceBefore
                && workbench.PipelineSteps.Count == stepsBefore
                && workbench.IsDirty == dirtyBefore;
        }
        finally
        {
            File.Delete(corruptPath);
        }

        var cancellationPreserved = false;
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            try
            {
                _ = await viewer.LoadViewerOnlySourceAsync(path, cancellation.Token);
            }
            catch (OperationCanceledException)
            {
                cancellationPreserved = string.Equals(
                    viewer.CurrentViewerOnlySourcePath,
                    expectedPath,
                    StringComparison.OrdinalIgnoreCase);
            }
        }

        var passed = initialPass && failurePreserved && cancellationPreserved;

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(
                reportPath,
                [
                    $"{(passed ? "PASS" : "FAIL")} | viewer-only-import",
                    $"requested={expectedPath}",
                    $"loaded={loaded}",
                    $"viewerPath={viewer.CurrentViewerOnlySourcePath}",
                    $"format={viewer.CurrentViewerOnlySourceFormat}",
                    $"summary={workbench.ViewerOnlyImportSummary}",
                    $"recipeSourceBefore={sourceBefore}",
                    $"recipeSourceAfter={workbench.Source.Path}",
                    $"stepsBefore={stepsBefore};stepsAfter={workbench.PipelineSteps.Count}",
                    $"dirtyBefore={dirtyBefore};dirtyAfter={workbench.IsDirty}",
                    $"failurePreserved={failurePreserved}",
                    $"cancellationPreserved={cancellationPreserved}",
                    "preview=false;publish=false;run=false"
                ]);
        }

        return passed;
    }
}

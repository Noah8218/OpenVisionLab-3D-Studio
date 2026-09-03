using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Workbench;

internal static class ImportSurfaceViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D coherent Import surface ViewModel verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Boundary: dedicated OpenVisionLab.ThreeD.Verification executable; no Shell verification source is compiled into the product assembly"
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

        var workbench = new ToolWorkbenchViewModel();
        var c3dRequests = 0;
        var importRequests = 0;
        workbench.LoadC3DSourceRequested += (_, _) => c3dRequests++;
        workbench.Import3DDataRequested += (_, _) => importRequests++;
        workbench.LoadC3DSourceCommand.Execute(null);
        workbench.Import3DDataCommand.Execute(null);
        Check(
            "recipe-c3d-and-general-import-commands-remain-distinct",
            c3dRequests == 1 && importRequests == 1,
            $"c3d={c3dRequests};import={importRequests}");

        var sourceBefore = workbench.Source.Path;
        var stepsBefore = workbench.PipelineSteps.Count;
        var dirtyBefore = workbench.IsDirty;
        workbench.Begin3DDataImport("viewer-mesh.glb", "GLB");
        Check(
            "active-import-disables-open-and-enables-cancel",
            !workbench.Import3DDataCommand.CanExecute(null)
            && !workbench.LoadC3DSourceCommand.CanExecute(null)
            && workbench.CancelC3DSourceLoadCommand.CanExecute(null),
            $"importing={workbench.IsC3DSourceLoading}");

        workbench.ReportC3DSourceLoadProgress(42.0);
        Check(
            "import-progress-identifies-file-and-percent",
            workbench.C3DSourceLoadStatus.Contains("viewer-mesh.glb", StringComparison.Ordinal)
            && workbench.C3DSourceLoadStatus.Contains("42", StringComparison.Ordinal),
            workbench.C3DSourceLoadStatus);

        workbench.CompleteViewerOnlyImport("viewer-mesh.glb", "GLB", 17);
        Check(
            "viewer-only-result-is-visible-and-truthful",
            workbench.HasViewerOnlyImport
            && workbench.ViewerOnlyImportSummary.Contains("GLB", StringComparison.Ordinal)
            && workbench.ViewerOnlyImportSummary.Contains("viewer-mesh.glb", StringComparison.Ordinal),
            workbench.ViewerOnlyImportSummary);
        Check(
            "viewer-only-import-does-not-mutate-recipe",
            workbench.Source.Path == sourceBefore
            && workbench.PipelineSteps.Count == stepsBefore
            && workbench.IsDirty == dirtyBefore,
            $"source='{workbench.Source.Path}';steps={workbench.PipelineSteps.Count};dirty={workbench.IsDirty}");

        workbench.Begin3DDataImport("cancelled.laz", "LAZ");
        workbench.CancelC3DSourceLoad(5);
        Check(
            "cancel-retains-last-successful-viewer-import",
            workbench.HasViewerOnlyImport
            && workbench.ViewerOnlyImportSummary.Contains("viewer-mesh.glb", StringComparison.Ordinal),
            workbench.ViewerOnlyImportSummary);

        workbench.Begin3DDataImport("broken.stl", "STL");
        workbench.FailC3DSourceLoad("broken.stl", 4);
        Check(
            "failure-retains-last-successful-viewer-import",
            workbench.HasViewerOnlyImport
            && workbench.ViewerOnlyImportSummary.Contains("viewer-mesh.glb", StringComparison.Ordinal),
            workbench.ViewerOnlyImportSummary);

        workbench.BeginC3DSourceLoad("recipe-source.c3d");
        workbench.CompleteC3DSourceLoad("recipe-source.c3d", 8);
        Check(
            "successful-c3d-import-clears-viewer-only-marker",
            !workbench.HasViewerOnlyImport
            && string.IsNullOrEmpty(workbench.ViewerOnlyImportSummary)
            && workbench.Import3DDataCommand.CanExecute(null),
            $"viewerOnly={workbench.HasViewerOnlyImport};importEnabled={workbench.Import3DDataCommand.CanExecute(null)}");
        Check(
            "successful-c3d-import-completes-progress",
            !workbench.IsC3DSourceLoading
            && Math.Abs(workbench.C3DSourceLoadProgressPercent - 100.0) < 0.001,
            $"loading={workbench.IsC3DSourceLoading};progress={workbench.C3DSourceLoadProgressPercent:F1}");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        summary = $"Import surface ViewModel verification: {passed}/{total} passed. Report: {Path.GetFullPath(reportPath)}";
        return passed == total;
    }
}

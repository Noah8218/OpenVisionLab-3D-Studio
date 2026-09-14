using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchOrientedBoxEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench OrientedBox3D event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: editor event routing, typed callback flow, and disposal"
        };
        var passed = 0;
        var total = 0;
        Exception? failure = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var editor = new OrientedBox3DEditorViewModel();
        var source = new ToolRecipeSource(
            "source-1",
            "Source 1",
            "C3D",
            "raw-height",
            "frame.c3d-grid-index",
            "source.c3d",
            GridWidth: 3,
            GridHeight: 3);
        var binding = new ToolRecipeSelectionSourceBinding(
            "C3D",
            "hash-1",
            3,
            3,
            Unit: "raw-height",
            FrameId: "frame.c3d-grid-index");
        var draftCount = 0;
        var applyCount = 0;
        var deleteCount = 0;
        ToolRecipeSelection? appliedSelection = null;
        ToolRecipeSelection? deletedSelection = null;
        var coordinator = new ToolWorkbenchOrientedBoxEventCoordinator(
            editor,
            args => draftCount++,
            args =>
            {
                applyCount++;
                appliedSelection = args.Selection;
            },
            args =>
            {
                deleteCount++;
                deletedSelection = editor.Selections.FirstOrDefault(selection =>
                    string.Equals(selection.Id, args.SelectionId, StringComparison.OrdinalIgnoreCase));
            });

        try
        {
            editor.Synchronize(source, binding, []);
            editor.NewCommand.Execute(null);
            Check(
                "DraftChanged reaches the Workbench callback",
                draftCount > 0,
                $"draftCount={draftCount}");

            editor.ApplyCommand.Execute(null);
            Check(
                "Apply reaches the typed Workbench callback",
                applyCount == 1
                && appliedSelection?.Kind == ToolRecipeSelectionKinds.OrientedBox3D,
                $"applyCount={applyCount};selection={appliedSelection?.Id ?? "(none)"}");

            editor.Synchronize(source, binding, appliedSelection is null ? [] : [appliedSelection]);
            editor.SelectedSelection = appliedSelection;
            editor.DeleteCommand.Execute(null);
            Check(
                "Delete reaches the typed Workbench callback",
                deleteCount == 1
                && deletedSelection?.Kind == ToolRecipeSelectionKinds.OrientedBox3D,
                $"deleteCount={deleteCount};selection={deletedSelection?.Id ?? "(none)"}");

            coordinator.Dispose();
            coordinator.Dispose();
            var draftCountAfterDispose = draftCount;
            var applyCountAfterDispose = applyCount;
            var deleteCountAfterDispose = deleteCount;
            editor.CenterX += 1;
            editor.ApplyCommand.Execute(null);
            editor.DeleteCommand.Execute(null);
            Check(
                "Dispose is idempotent and blocks every later editor callback",
                draftCount == draftCountAfterDispose
                && applyCount == applyCountAfterDispose
                && deleteCount == deleteCountAfterDispose,
                $"draft={draftCount}; apply={applyCount}; delete={deleteCount}");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            coordinator.Dispose();
        }

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ToolWorkbenchOrientedBoxEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}

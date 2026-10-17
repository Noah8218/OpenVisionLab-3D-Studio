using System.Collections.ObjectModel;
using System.IO;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell;

internal static class ShellMainWindowEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell MainWindow event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: inspection-step, Workbench, ordered-run, language routing and disposal"
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

        var originalLanguage = OpenVisionLanguageService.CurrentLanguage;
        try
        {
            var fixtureRoot = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "event-comparison-fixture");
            Directory.CreateDirectory(fixtureRoot);
            var taughtPath = Path.Combine(fixtureRoot, "taught.C3D");
            var selectedPath = Path.Combine(fixtureRoot, "selected.C3D");
            C3DHeightFieldSnapshot.CreateForVerification("source.event-comparison", 4, 4,
                [10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15, 13, 14, 15, 16]).SaveC3D(taughtPath);
            C3DHeightFieldSnapshot.CreateForVerification("source.event-comparison", 4, 4,
                [9, 10, 11, 12, 10, 11, 12, 13, 11, 12, 13, 14, 12, 13, 14, 15]).SaveC3D(selectedPath);
            using var workbench = new ToolWorkbenchViewModel(Path.Combine(fixtureRoot, "recent-recipes.json"));
            workbench.SetC3DSource(taughtPath, markDirty: false);
            workbench.SetValidationSetSources([selectedPath]);
            var inspectionSteps = new ObservableCollection<InspectionStepItem>();
            var inspectionStepCallbacks = 0;
            var workbenchCallbacks = 0;
            var languageCallbacks = 0;
            var comparisonCallbacks = 0;
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.FirstOrDefault();

            using var coordinator = new ShellMainWindowEventCoordinator(
                inspectionSteps,
                workbench,
                () => inspectionStepCallbacks++,
                _ => workbenchCallbacks++,
                _ => { },
                () => { },
                () => languageCallbacks++,
                () => comparisonCallbacks++);

            inspectionSteps.Add(new InspectionStepItem("1", "Source", "Ready", "test"));
            workbench.SelectedTool = null;
            workbench.SelectedTool = workbench.Tools[0];
            var alternateLanguage = originalLanguage == OpenVisionLanguage.English
                ? OpenVisionLanguage.Korean
                : OpenVisionLanguage.English;
            OpenVisionLanguageService.SetLanguage(alternateLanguage, save: false);

            Check(
                "Inspection-step collection changes reach the Shell callback",
                inspectionStepCallbacks == 1,
                $"callbacks={inspectionStepCallbacks}");
            Check(
                "Workbench property changes reach the Shell callback",
                workbenchCallbacks > 0,
                $"callbacks={workbenchCallbacks}");
            Check(
                "Language changes reach the Shell callback",
                languageCallbacks == 1,
                $"callbacks={languageCallbacks}");

            Check("Public comparison command has a real loaded source and selected input", workbench.OpenValidationSetComparisonCommand.CanExecute(null), $"taught={taughtPath};selected={selectedPath};self-contained canonical input paths");
            workbench.OpenValidationSetComparisonCommand.Execute(null);
            Check("Public validation comparison event reaches the existing coordinator exactly once", comparisonCallbacks == 1, $"callbacks={comparisonCallbacks};canonical command -> public event");
            coordinator.Dispose();
            coordinator.Dispose();
            workbench.OpenValidationSetComparisonCommand.Execute(null);
            Check("Disposed coordinator detaches comparison event", comparisonCallbacks == 1, $"callbacks={comparisonCallbacks};public command remains available");
            var inspectionStepsAfterDispose = inspectionStepCallbacks;
            var workbenchAfterDispose = workbenchCallbacks;
            var languageAfterDispose = languageCallbacks;
            inspectionSteps.Add(new InspectionStepItem("2", "Source", "Ready", "test"));
            workbench.SelectedTool = null;
            workbench.SelectedTool = workbench.Tools[0];
            OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            Check(
                "Dispose is idempotent and suppresses all later callbacks",
                inspectionStepCallbacks == inspectionStepsAfterDispose
                && workbenchCallbacks == workbenchAfterDispose
                && languageCallbacks == languageAfterDispose,
                $"inspection={inspectionStepCallbacks}; workbench={workbenchCallbacks}; language={languageCallbacks}");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (OpenVisionLanguageService.CurrentLanguage != originalLanguage)
            {
                OpenVisionLanguageService.SetLanguage(originalLanguage, save: false);
            }
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
        summary = $"ShellMainWindowEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}

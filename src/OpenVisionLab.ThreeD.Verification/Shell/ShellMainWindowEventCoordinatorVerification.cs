using System.Collections.ObjectModel;
using System.IO;
using OpenVisionLab;
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
            using var workbench = new ToolWorkbenchViewModel();
            var inspectionSteps = new ObservableCollection<InspectionStepItem>();
            var inspectionStepCallbacks = 0;
            var workbenchCallbacks = 0;
            var languageCallbacks = 0;

            using var coordinator = new ShellMainWindowEventCoordinator(
                inspectionSteps,
                workbench,
                () => inspectionStepCallbacks++,
                _ => workbenchCallbacks++,
                _ => { },
                () => { },
                () => languageCallbacks++);

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

            coordinator.Dispose();
            coordinator.Dispose();
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

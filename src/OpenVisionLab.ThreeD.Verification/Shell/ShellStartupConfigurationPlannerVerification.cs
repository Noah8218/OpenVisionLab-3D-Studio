using System.Globalization;
using System.IO;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.Coordination;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell;

internal static class ShellStartupConfigurationPlannerVerification
{
    private const string Option = "--verify-shell-startup-configuration";

    public static bool TryRun(string[] arguments, out bool passed, out string summary)
    {
        var optionIndex = Array.FindIndex(
            arguments,
            argument => argument.Equals(Option, StringComparison.OrdinalIgnoreCase));
        if (optionIndex < 0)
        {
            passed = false;
            summary = string.Empty;
            return false;
        }

        if (optionIndex + 1 >= arguments.Length)
        {
            passed = false;
            summary = $"{Option} requires a report path.";
            return true;
        }

        var reportPath = Path.GetFullPath(arguments[optionIndex + 1]);
        var checks = new List<(string Name, bool Passed)>
        {
            ("CombinedIntent", VerifyCombinedIntent()),
            ("Aliases", VerifyAliases()),
            ("InvalidValuesKeepDefaults", VerifyInvalidValuesKeepDefaults()),
            ("PlainStartUsesEmptyInput", VerifyPlainStartUsesEmptyInput()),
            ("RepeatedParseIsDeterministic", VerifyRepeatedParseIsDeterministic()),
            ("StartupApplicationOrder", VerifyStartupApplicationOrder()),
            ("ViewerProjectionOrder", VerifyViewerProjectionOrder()),
            ("ViewerHostPolicy", VerifyViewerHostPolicy())
        };
        passed = checks.All(check => check.Passed);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Shell startup configuration planner verification"
        };
        lines.AddRange(checks.Select(check => $"{check.Name}={(check.Passed ? "PASS" : "FAIL")}"));
        lines.Add($"Result={(passed ? "PASS" : "FAIL")}|{checks.Count(check => check.Passed)}/{checks.Count}");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        File.WriteAllLines(reportPath, lines);
        summary = lines[^1];
        return true;
    }

    private static bool VerifyCombinedIntent()
    {
        var plan = ShellStartupConfigurationPlanner.Parse(
        [
            "shell.exe",
            "--ui-language", "ko",
            "--shell-evidence-tab", "timeline",
            "--shell-workspace", "Review",
            "--shell-results-section", "Reports",
            "--shell-task", "Warpage",
            "--smoke-stage", "teach",
            "--smoke-view", "top",
            "--smoke-fit-roi",
            "--smoke-height-color-min", "-2.5",
            "--smoke-height-color-max", "4.25",
            "--workbench-bottom-pane", "output-compare",
            "--workbench-compare-slot-a", "a",
            "--workbench-compare-slot-b", "b",
            "--workbench-compare-slot-c", "c",
            "--smoke-c3d-load-progress", "0.5",
            "--shell-smoke-screenshot", "capture.png"
        ]);

        return plan.RequestedLanguage == OpenVisionLanguage.Korean
            && plan.EvidenceTabIndex == 3
            && plan.Workspace == ShellWorkspaceMode.Review
            && plan.ResultsSection == ResultsWorkspaceSection.Reports
            && plan.InspectionTask == ShellInspectionTask.Warpage
            && plan.StageWorkspace == ShellWorkspaceMode.Teach
            && plan.ViewerView == ShellStartupViewerView.Top
            && plan.FitRoi
            && plan.HeightColorMinimumRaw == -2.5
            && plan.HeightColorMaximumRaw == 4.25
            && plan.BottomPane == ShellStartupBottomPane.OutputCompare
            && plan.CompareSlotAArtifactId == "a"
            && plan.CompareSlotBArtifactId == "b"
            && plan.CompareSlotCArtifactId == "c"
            && plan.C3DSourceLoadProgress == 0.5
            && plan.IsAutomatedShellRun
            && !plan.ShouldStartWithEmptyRecipeInput;
    }

    private static bool VerifyAliases()
    {
        var plan = ShellStartupConfigurationPlanner.Parse(
        [
            "shell.exe",
            "--ui-language", "english",
            "--shell-evidence-tab", "run-record",
            "--smoke-stage", "results",
            "--smoke-view", "perspective",
            "--workbench-bottom-pane", "height-profile"
        ]);

        return plan.RequestedLanguage == OpenVisionLanguage.English
            && plan.EvidenceTabIndex == 2
            && plan.StageWorkspace == ShellWorkspaceMode.Review
            && plan.ViewerView == ShellStartupViewerView.Perspective
            && plan.BottomPane == ShellStartupBottomPane.Profile;
    }

    private static bool VerifyInvalidValuesKeepDefaults()
    {
        var plan = ShellStartupConfigurationPlanner.Parse(
        [
            "shell.exe",
            "--ui-language", "fr",
            "--shell-evidence-tab", "unknown",
            "--shell-workspace", "invalid",
            "--shell-results-section", "invalid",
            "--shell-task", "invalid",
            "--smoke-stage", "invalid",
            "--smoke-view", "invalid",
            "--smoke-height-color-min", "not-a-number",
            "--smoke-height-color-max", "not-a-number",
            "--workbench-bottom-pane", "invalid",
            "--smoke-c3d-load-progress", "not-a-number"
        ]);

        return plan.RequestedLanguage is null
            && plan.EvidenceTabIndex == 0
            && plan.Workspace is null
            && plan.ResultsSection is null
            && plan.InspectionTask is null
            && plan.StageWorkspace is null
            && plan.ViewerView == ShellStartupViewerView.None
            && !plan.FitRoi
            && plan.HeightColorMinimumRaw is null
            && plan.HeightColorMaximumRaw is null
            && plan.BottomPane == ShellStartupBottomPane.None
            && plan.C3DSourceLoadProgress is null
            && plan.IsAutomatedShellRun
            && !plan.ShouldStartWithEmptyRecipeInput;
    }

    private static bool VerifyPlainStartUsesEmptyInput()
    {
        var plan = ShellStartupConfigurationPlanner.Parse(["shell.exe"]);
        return !plan.IsAutomatedShellRun && plan.ShouldStartWithEmptyRecipeInput;
    }

    private static bool VerifyRepeatedParseIsDeterministic()
    {
        var args = new[]
        {
            "shell.exe",
            "--SMOKE-FIT-ROI",
            "--smoke-c3d-load-progress", 0.75.ToString(CultureInfo.InvariantCulture),
            "--workbench-bottom-pane", "flow-problems"
        };
        var first = ShellStartupConfigurationPlanner.Parse(args);
        var second = ShellStartupConfigurationPlanner.Parse(args);
        return first == second
            && first.FitRoi
            && first.BottomPane == ShellStartupBottomPane.Problems
            && first.C3DSourceLoadProgress == 0.75;
    }

    private static bool VerifyViewerProjectionOrder()
    {
        var events = new List<string>();
        var coordinator = new ShellStartupViewerProjectionCoordinator(
            new ShellStartupViewerProjectionCallbacks
            {
                SelectWorkbenchWorkspace = () => events.Add("workspace:setup"),
                SelectTeachWorkspace = () => events.Add("workspace:teach"),
                SelectInspectWorkspace = () => events.Add("workspace:inspect"),
                SelectReviewWorkspace = () => events.Add("workspace:review"),
                UseTopView = () => events.Add("view:top"),
                UsePerspectiveView = () => events.Add("view:perspective"),
                FitRoi = () => events.Add("fit-roi"),
                SetHeightColorMinimumRaw = value => events.Add($"height-min:{value}"),
                SetHeightColorMaximumRaw = value => events.Add($"height-max:{value}")
            });

        coordinator.Apply(
            new ShellStartupConfigurationPlan
            {
                StageWorkspace = ShellWorkspaceMode.Review,
                ViewerView = ShellStartupViewerView.Top,
                FitRoi = true,
                HeightColorMinimumRaw = -2.5,
                HeightColorMaximumRaw = 4.25
            });
        coordinator.Apply(new ShellStartupConfigurationPlan());

        return events.SequenceEqual(
        [
            "workspace:review",
            "view:top",
            "fit-roi",
            "height-min:-2.5",
            "height-max:4.25"
        ]);
    }

    private static bool VerifyStartupApplicationOrder()
    {
        var events = new List<string>();
        var coordinator = new ShellStartupCoordinator(
            new ShellStartupCoordinatorCallbacks
            {
                SelectWorkspace = workspace => events.Add($"workspace:{workspace}"),
                IsResultsWorkspaceSelected = () => true,
                SelectResultsSection = section => events.Add($"results:{section}"),
                SelectInspectionTask = task => events.Add($"task:{task}"),
                ActivateBottomPane = pane => events.Add($"pane:{pane}"),
                SetOutputCompareSlots = (slotA, slotB, slotC) =>
                    events.Add($"compare:{slotA},{slotB},{slotC}"),
                ApplyC3DSourceLoadProgress = progress => events.Add($"progress:{progress}"),
                ConfigureValidationSet = _ =>
                {
                    events.Add("validation");
                    return ShellValidationSetSmokeState.Empty;
                },
                TryLoadRunRecord = path =>
                {
                    events.Add($"run:{path}");
                    return new ShellStartupRunRecordLoadResult(false, "load failed");
                },
                ReportRunRecordRestoreFailure = message => events.Add($"warning:{message}"),
                ApplyCalibration = (path, calculate) =>
                    events.Add($"calibration:{path}:{calculate}"),
                ConfigureToolTeaching = request =>
                {
                    events.Add($"teaching:{request.RecipePath}:{request.RequestedStepId}");
                    return new ShellToolTeachingStartupResult("teaching failed");
                },
                SetViewerSmokeFailure = failure => events.Add($"smoke-failure:{failure}"),
                ApplyViewerProjection = _ => events.Add("viewer")
            });
        var configuration = new ShellStartupConfigurationPlan
        {
            Workspace = ShellWorkspaceMode.Review,
            ResultsSection = ResultsWorkspaceSection.Reports,
            InspectionTask = ShellInspectionTask.Warpage,
            BottomPane = ShellStartupBottomPane.OutputCompare,
            CompareSlotAArtifactId = "a",
            CompareSlotBArtifactId = "b",
            CompareSlotCArtifactId = "c",
            C3DSourceLoadProgress = 0.5
        };
        var commandLine = new ShellCommandLineArguments(
        [
            "shell.exe",
            "--calibration-study", "calibration.json",
            "--smoke-calibration-calculate",
            "--tool-teaching-recipe", "recipe.json",
            "--tool-teaching-step", "step-1",
            "--run-record", "run-record.json"
        ]);

        coordinator.ApplyWorkspaceAndResults(configuration);
        coordinator.ApplyInspectionTask(configuration);
        coordinator.ApplyCalibration(commandLine);
        coordinator.ApplyToolTeaching(commandLine);
        coordinator.RestoreRunRecord(commandLine);
        coordinator.ApplyOutputCompare(configuration);
        _ = coordinator.ConfigureValidationSet(commandLine);
        coordinator.ApplyWorkbenchBottomPane(configuration);
        coordinator.ApplyC3DSourceLoadProgress(configuration);
        coordinator.ApplyViewerProjection(configuration);

        var language = OpenVisionLanguage.Korean;
        ShellStartupCoordinator.ApplyLanguage(
            new ShellStartupConfigurationPlan { RequestedLanguage = language },
            value => events.Add($"language:{value}"));

        return events.SequenceEqual(
        [
            "workspace:Review",
            "results:Reports",
            "task:Warpage",
            "calibration:calibration.json:True",
            "teaching:recipe.json:step-1",
            "smoke-failure:teaching failed",
            "run:run-record.json",
            "warning:load failed",
            "compare:a,b,c",
            "validation",
            "pane:OutputCompare",
            "progress:0.5",
            "viewer",
            "language:Korean"
        ]);
    }

    private static bool VerifyViewerHostPolicy()
    {
        var workbench = ShellViewerHostPolicy.Resolve(
            ShellWorkspaceMode.Workbench,
            teachingCaptureActive: true);
        var teach = ShellViewerHostPolicy.Resolve(
            ShellWorkspaceMode.Teach,
            teachingCaptureActive: true);
        var inspect = ShellViewerHostPolicy.Resolve(
            ShellWorkspaceMode.Inspect,
            teachingCaptureActive: false);
        var review = ShellViewerHostPolicy.Resolve(
            ShellWorkspaceMode.Review,
            teachingCaptureActive: false);
        var expert = ShellViewerHostPolicy.Resolve(
            ShellWorkspaceMode.Expert,
            teachingCaptureActive: true);
        var calibrate = ShellViewerHostPolicy.Resolve(
            ShellWorkspaceMode.Calibrate,
            teachingCaptureActive: true);
        var exchange = ShellViewerHostPolicy.Resolve(
            ShellWorkspaceMode.Exchange,
            teachingCaptureActive: false);

        return workbench.Target == ShellViewerHostTarget.Workbench
            && !workbench.CancelTeachingCapture
            && teach.Target == ShellViewerHostTarget.Workbench
            && !teach.CancelTeachingCapture
            && inspect.Target == ShellViewerHostTarget.Workbench
            && !inspect.CancelTeachingCapture
            && review.Target == ShellViewerHostTarget.Workbench
            && !review.CancelTeachingCapture
            && expert.Target == ShellViewerHostTarget.Expert
            && expert.CancelTeachingCapture
            && calibrate.Target == ShellViewerHostTarget.None
            && calibrate.CancelTeachingCapture
            && exchange.Target == ShellViewerHostTarget.None
            && !exchange.CancelTeachingCapture;
    }
}

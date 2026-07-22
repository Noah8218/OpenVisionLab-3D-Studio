using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Shell;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        const string verificationOption = "--verify-calibration-viewmodel";
        const string loggingVerificationOption = "--verify-logging";
        const string toolRecipeTeachingVerificationOption = "--verify-tool-recipe-teaching";
        const string toolRecipeSelectionsVerificationOption = "--verify-tool-recipe-selections";
        const string workbenchDockingVerificationOption = "--verify-workbench-docking";
        const string teachingCaptureViewModelVerificationOption = "--verify-teaching-capture-viewmodel";
        const string c3dHeightProfileVerificationOption = "--verify-c3d-height-profile";
        const string profileViewModelVerificationOption = "--verify-profile-viewmodel";
        const string c3dHeightDistributionVerificationOption = "--verify-c3d-height-distribution";
        const string heightDifferenceEdgeWorkbenchVerificationOption = "--verify-tool-edge-workbench";
        const string twoPointLineWorkbenchVerificationOption = "--verify-tool-two-point-line-workbench";
        const string threePointPlaneWorkbenchVerificationOption = "--verify-tool-three-point-plane-workbench";
        const string datumPlaneDeviationWorkbenchVerificationOption = "--verify-tool-datum-plane-deviation-workbench";
        const string lineFitWorkbenchVerificationOption = "--verify-tool-line-fit-workbench";
        const string lineIntersectionWorkbenchVerificationOption = "--verify-tool-line-intersection-workbench";
        const string recipeManagerWpgVerificationOption = "--verify-recipe-manager-wpg";
        const string artifactNavigatorVerificationOption = "--verify-artifact-navigator";
        const string heightMeasurementWorkbenchVerificationOption = "--verify-tool-height-measurement-workbench";
        var heightMeasurementWorkbenchVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(heightMeasurementWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (heightMeasurementWorkbenchVerificationIndex >= 0)
        {
            if (heightMeasurementWorkbenchVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{heightMeasurementWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }
            var result = Task.Run(() =>
            {
                var passed = ToolHeightMeasurementWorkbenchVerification.Verify(
                    e.Args[heightMeasurementWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Detail: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Detail);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        var artifactNavigatorVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(artifactNavigatorVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (artifactNavigatorVerificationIndex >= 0)
        {
            if (artifactNavigatorVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{artifactNavigatorVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolArtifactNavigatorVerification.Verify(
                    e.Args[artifactNavigatorVerificationIndex + 1],
                    out var summary);
                return (Passed: passed, Summary: summary);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }

        var recipeManagerWpgVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(recipeManagerWpgVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (recipeManagerWpgVerificationIndex >= 0)
        {
            if (recipeManagerWpgVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{recipeManagerWpgVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = RecipeManagerWpgVerification.Verify(
                e.Args[recipeManagerWpgVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var verificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(verificationOption, StringComparison.OrdinalIgnoreCase));
        var heightDifferenceEdgeWorkbenchVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(heightDifferenceEdgeWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var twoPointLineWorkbenchVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(twoPointLineWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var threePointPlaneWorkbenchVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(threePointPlaneWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var datumPlaneDeviationWorkbenchVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(datumPlaneDeviationWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var lineFitWorkbenchVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(lineFitWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var lineIntersectionWorkbenchVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(lineIntersectionWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (twoPointLineWorkbenchVerificationIndex >= 0)
        {
            if (twoPointLineWorkbenchVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{twoPointLineWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolTwoPointLineWorkbenchVerification.Verify(e.Args[twoPointLineWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (threePointPlaneWorkbenchVerificationIndex >= 0)
        {
            if (threePointPlaneWorkbenchVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{threePointPlaneWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolThreePointPlaneWorkbenchVerification.Verify(e.Args[threePointPlaneWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (datumPlaneDeviationWorkbenchVerificationIndex >= 0)
        {
            if (datumPlaneDeviationWorkbenchVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{datumPlaneDeviationWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolDatumPlaneDeviationWorkbenchVerification.Verify(e.Args[datumPlaneDeviationWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (lineIntersectionWorkbenchVerificationIndex >= 0)
        {
            if (lineIntersectionWorkbenchVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{lineIntersectionWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolLineIntersectionWorkbenchVerification.Verify(e.Args[lineIntersectionWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (lineFitWorkbenchVerificationIndex >= 0)
        {
            if (lineFitWorkbenchVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{lineFitWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolLineFitWorkbenchVerification.Verify(e.Args[lineFitWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (heightDifferenceEdgeWorkbenchVerificationIndex >= 0)
        {
            if (heightDifferenceEdgeWorkbenchVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{heightDifferenceEdgeWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolHeightDifferenceEdgeWorkbenchVerification.Verify(
                    e.Args[heightDifferenceEdgeWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            var passed = result.Passed;
            var summary = result.Summary;
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }
        if (verificationIndex >= 0)
        {
            if (verificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{verificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = CalibrationCenterViewModelVerification.Verify(
                e.Args[verificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var loggingVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(loggingVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (loggingVerificationIndex >= 0)
        {
            if (loggingVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{loggingVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = LoggingIntegrationVerification.Verify(
                e.Args[loggingVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var toolRecipeTeachingVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(toolRecipeTeachingVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (toolRecipeTeachingVerificationIndex >= 0)
        {
            if (toolRecipeTeachingVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{toolRecipeTeachingVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolRecipeTeachingVerification.Verify(
                e.Args[toolRecipeTeachingVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var toolRecipeSelectionsVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(toolRecipeSelectionsVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (toolRecipeSelectionsVerificationIndex >= 0)
        {
            if (toolRecipeSelectionsVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{toolRecipeSelectionsVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolRecipeSelectionContractVerification.Verify(
                e.Args[toolRecipeSelectionsVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var workbenchDockingVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(workbenchDockingVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (workbenchDockingVerificationIndex >= 0)
        {
            if (workbenchDockingVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{workbenchDockingVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolWorkbenchDockingVerification.Verify(
                e.Args[workbenchDockingVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var teachingCaptureViewModelVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(teachingCaptureViewModelVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (teachingCaptureViewModelVerificationIndex >= 0)
        {
            if (teachingCaptureViewModelVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{teachingCaptureViewModelVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = TeachingCaptureViewModelVerification.Verify(
                e.Args[teachingCaptureViewModelVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var c3dHeightProfileVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(c3dHeightProfileVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (c3dHeightProfileVerificationIndex >= 0)
        {
            if (c3dHeightProfileVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{c3dHeightProfileVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = C3DHeightProfileVerification.Verify(
                e.Args[c3dHeightProfileVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var profileViewModelVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(profileViewModelVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (profileViewModelVerificationIndex >= 0)
        {
            if (profileViewModelVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{profileViewModelVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ProfileViewModelVerification.Verify(
                e.Args[profileViewModelVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var c3dHeightDistributionVerificationIndex = Array.FindIndex(
            e.Args,
            argument => argument.Equals(c3dHeightDistributionVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (c3dHeightDistributionVerificationIndex >= 0)
        {
            if (c3dHeightDistributionVerificationIndex + 1 >= e.Args.Length)
            {
                Console.WriteLine($"{c3dHeightDistributionVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = C3DHeightDistributionVerification.Verify(
                e.Args[c3dHeightDistributionVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        LiveCharts.Configure(settings => settings.UseDefaults());
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            OVLog.Flush();
            OVLog.Shutdown();
        }
        finally
        {
            base.OnExit(e);
        }
    }
}

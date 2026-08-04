using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.Verification.Smoke;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Shell.Verification;

internal static class ShellVerificationCommandRouter
{
    public static bool IsVerificationRequest(string[] args) =>
        args.Any(argument => argument.StartsWith("--verify-", StringComparison.OrdinalIgnoreCase));

    public static void Run(string[] args)
    {
        var e = new VerificationArguments(args);
        const string multipleSurfaceMatchWorkbenchOption =
            "--verify-multiple-surface-match-workbench";
        var multipleSurfaceMatchWorkbenchIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                multipleSurfaceMatchWorkbenchOption,
                StringComparison.OrdinalIgnoreCase));
        if (multipleSurfaceMatchWorkbenchIndex >= 0)
        {
            if (multipleSurfaceMatchWorkbenchIndex + 4 >= args.Length)
            {
                Console.WriteLine(
                    $"{multipleSurfaceMatchWorkbenchOption} requires model, scene, collection, and report paths.");
                Shutdown(2);
                return;
            }

            var passed = MultipleSurfaceMatchWorkbenchVerification.Verify(
                args[multipleSurfaceMatchWorkbenchIndex + 1],
                args[multipleSurfaceMatchWorkbenchIndex + 2],
                args[multipleSurfaceMatchWorkbenchIndex + 3],
                args[multipleSurfaceMatchWorkbenchIndex + 4],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string surfaceEdgeDiagnosticReviewParityOption =
            "--verify-surface-edge-diagnostic-review-workbench-parity";
        var surfaceEdgeDiagnosticReviewParityIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                surfaceEdgeDiagnosticReviewParityOption,
                StringComparison.OrdinalIgnoreCase));
        if (surfaceEdgeDiagnosticReviewParityIndex >= 0)
        {
            if (surfaceEdgeDiagnosticReviewParityIndex + 2 >= args.Length)
            {
                Console.WriteLine(
                    $"{surfaceEdgeDiagnosticReviewParityOption} requires artifact-directory and report paths.");
                Shutdown(2);
                return;
            }

            var passed =
                SurfaceEdgeDiagnosticReviewWorkbenchParityVerification.Verify(
                    args[surfaceEdgeDiagnosticReviewParityIndex + 1],
                    args[surfaceEdgeDiagnosticReviewParityIndex + 2],
                    out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string surfaceEdgeWorkbenchParityOption =
            "--verify-surface-edge-workbench-parity";
        var surfaceEdgeWorkbenchParityIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                surfaceEdgeWorkbenchParityOption,
                StringComparison.OrdinalIgnoreCase));
        if (surfaceEdgeWorkbenchParityIndex >= 0)
        {
            if (surfaceEdgeWorkbenchParityIndex + 7 >= args.Length)
            {
                Console.WriteLine(
                    $"{surfaceEdgeWorkbenchParityOption} requires model, scene, execution, model edge, scene edge, Runner score, and report paths.");
                Shutdown(2);
                return;
            }

            var passed = SurfaceEdgeWorkbenchParityVerification.Verify(
                args[surfaceEdgeWorkbenchParityIndex + 1],
                args[surfaceEdgeWorkbenchParityIndex + 2],
                args[surfaceEdgeWorkbenchParityIndex + 3],
                args[surfaceEdgeWorkbenchParityIndex + 4],
                args[surfaceEdgeWorkbenchParityIndex + 5],
                args[surfaceEdgeWorkbenchParityIndex + 6],
                args[surfaceEdgeWorkbenchParityIndex + 7],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string surfaceMatchWorkbenchParityOption =
            "--verify-surface-match-workbench-parity";
        var surfaceMatchWorkbenchParityIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                surfaceMatchWorkbenchParityOption,
                StringComparison.OrdinalIgnoreCase));
        if (surfaceMatchWorkbenchParityIndex >= 0)
        {
            if (surfaceMatchWorkbenchParityIndex + 4 >= args.Length)
            {
                Console.WriteLine(
                    $"{surfaceMatchWorkbenchParityOption} requires model, scene, Runner execution, and report paths.");
                Shutdown(2);
                return;
            }

            var passed =
                SurfaceMatchWorkbenchParityVerification.Verify(
                    args[surfaceMatchWorkbenchParityIndex + 1],
                    args[surfaceMatchWorkbenchParityIndex + 2],
                    args[surfaceMatchWorkbenchParityIndex + 3],
                    args[surfaceMatchWorkbenchParityIndex + 4],
                    out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string sourceQualityWorkspaceVerificationOption =
            "--verify-source-quality-workspace";
        const string sourceAcquisitionProvenanceVerificationOption =
            "--verify-source-acquisition-provenance";
        var sourceAcquisitionProvenanceVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                sourceAcquisitionProvenanceVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (sourceAcquisitionProvenanceVerificationIndex >= 0)
        {
            if (sourceAcquisitionProvenanceVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{sourceAcquisitionProvenanceVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = SourceAcquisitionProvenanceVerification.Verify(
                args[sourceAcquisitionProvenanceVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }
        const string sourceChannelNormalQualityVerificationOption =
            "--verify-source-channel-normal-quality";
        var sourceChannelNormalQualityVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                sourceChannelNormalQualityVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (sourceChannelNormalQualityVerificationIndex >= 0)
        {
            if (sourceChannelNormalQualityVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{sourceChannelNormalQualityVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = SourceChannelAndNormalQualityVerification.Verify(
                args[sourceChannelNormalQualityVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var sourceQualityWorkspaceVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                sourceQualityWorkspaceVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (sourceQualityWorkspaceVerificationIndex >= 0)
        {
            if (sourceQualityWorkspaceVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{sourceQualityWorkspaceVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = SourceQualityWorkspaceVerification.Verify(
                args[sourceQualityWorkspaceVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string thicknessRepeatGridVerificationOption =
            "--verify-thickness-repeat-grid";
        var thicknessRepeatGridVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                thicknessRepeatGridVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (thicknessRepeatGridVerificationIndex >= 0)
        {
            if (thicknessRepeatGridVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{thicknessRepeatGridVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ThicknessRepeatGridAuthoringVerification.Verify(
                args[thicknessRepeatGridVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string inspectionWorkspaceSelectionVerificationOption =
            "--verify-inspection-workspace-selection";
        var inspectionWorkspaceSelectionVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                inspectionWorkspaceSelectionVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (inspectionWorkspaceSelectionVerificationIndex >= 0)
        {
            if (inspectionWorkspaceSelectionVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{inspectionWorkspaceSelectionVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = InspectionWorkspaceSelectionVerification.Verify(
                args[inspectionWorkspaceSelectionVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        const string removeOutlierPixelsWorkbenchVerificationOption =
            "--verify-remove-outlier-pixels-workbench";
        var removeOutlierPixelsWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                removeOutlierPixelsWorkbenchVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (removeOutlierPixelsWorkbenchVerificationIndex >= 0)
        {
            if (removeOutlierPixelsWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{removeOutlierPixelsWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = RemoveOutlierPixelsWorkbenchVerification.Verify(
                    args[removeOutlierPixelsWorkbenchVerificationIndex + 1],
                    out var detail);
                return (Passed: passed, Detail: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Detail);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }

        const string levelSurfaceWorkbenchVerificationOption =
            "--verify-level-surface-workbench";
        var levelSurfaceWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                levelSurfaceWorkbenchVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (levelSurfaceWorkbenchVerificationIndex >= 0)
        {
            if (levelSurfaceWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{levelSurfaceWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = LevelSurfaceWorkbenchVerification.Verify(
                    args[levelSurfaceWorkbenchVerificationIndex + 1],
                    out var detail);
                return (Passed: passed, Detail: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Detail);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }

        if (ShellSmokeCommandLineOptionsVerification.TryRun(args, out var smokeOptionsPassed, out var smokeOptionsSummary))
        {
            Console.WriteLine(smokeOptionsSummary);
            Shutdown(smokeOptionsPassed ? 0 : 1);
            return;
        }

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
        const string validationSetVerificationOption = "--verify-validation-set";
        const string runRecordHistoryVerificationOption = "--verify-run-record-history";
        var runRecordHistoryVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(runRecordHistoryVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (runRecordHistoryVerificationIndex >= 0)
        {
            if (runRecordHistoryVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{runRecordHistoryVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = RunRecordHistoryVerification.Verify(
                args[runRecordHistoryVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }
        var validationSetVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(validationSetVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (validationSetVerificationIndex >= 0)
        {
            if (validationSetVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{validationSetVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolRecipeValidationSetVerification.Verify(
                    args[validationSetVerificationIndex + 1],
                    out var detail);
                return (Passed: passed, Detail: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Detail);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        var heightMeasurementWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(heightMeasurementWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (heightMeasurementWorkbenchVerificationIndex >= 0)
        {
            if (heightMeasurementWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{heightMeasurementWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }
            var result = Task.Run(() =>
            {
                var passed = ToolHeightMeasurementWorkbenchVerification.Verify(
                    args[heightMeasurementWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Detail: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Detail);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        var artifactNavigatorVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(artifactNavigatorVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (artifactNavigatorVerificationIndex >= 0)
        {
            if (artifactNavigatorVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{artifactNavigatorVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolArtifactNavigatorVerification.Verify(
                    args[artifactNavigatorVerificationIndex + 1],
                    out var summary);
                return (Passed: passed, Summary: summary);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }

        var recipeManagerWpgVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(recipeManagerWpgVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (recipeManagerWpgVerificationIndex >= 0)
        {
            if (recipeManagerWpgVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{recipeManagerWpgVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = RecipeManagerWpgVerification.Verify(
                args[recipeManagerWpgVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var verificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(verificationOption, StringComparison.OrdinalIgnoreCase));
        var heightDifferenceEdgeWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(heightDifferenceEdgeWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var twoPointLineWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(twoPointLineWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var threePointPlaneWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(threePointPlaneWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var datumPlaneDeviationWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(datumPlaneDeviationWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var lineFitWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(lineFitWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        var lineIntersectionWorkbenchVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(lineIntersectionWorkbenchVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (twoPointLineWorkbenchVerificationIndex >= 0)
        {
            if (twoPointLineWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{twoPointLineWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolTwoPointLineWorkbenchVerification.Verify(args[twoPointLineWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (threePointPlaneWorkbenchVerificationIndex >= 0)
        {
            if (threePointPlaneWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{threePointPlaneWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolThreePointPlaneWorkbenchVerification.Verify(args[threePointPlaneWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (datumPlaneDeviationWorkbenchVerificationIndex >= 0)
        {
            if (datumPlaneDeviationWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{datumPlaneDeviationWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolDatumPlaneDeviationWorkbenchVerification.Verify(args[datumPlaneDeviationWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (lineIntersectionWorkbenchVerificationIndex >= 0)
        {
            if (lineIntersectionWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{lineIntersectionWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolLineIntersectionWorkbenchVerification.Verify(args[lineIntersectionWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (lineFitWorkbenchVerificationIndex >= 0)
        {
            if (lineFitWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{lineFitWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolLineFitWorkbenchVerification.Verify(args[lineFitWorkbenchVerificationIndex + 1], out var detail);
                return (Passed: passed, Summary: detail);
            }).GetAwaiter().GetResult();
            Console.WriteLine(result.Summary);
            Shutdown(result.Passed ? 0 : 1);
            return;
        }
        if (heightDifferenceEdgeWorkbenchVerificationIndex >= 0)
        {
            if (heightDifferenceEdgeWorkbenchVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{heightDifferenceEdgeWorkbenchVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var result = Task.Run(() =>
            {
                var passed = ToolHeightDifferenceEdgeWorkbenchVerification.Verify(
                    args[heightDifferenceEdgeWorkbenchVerificationIndex + 1], out var detail);
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
            if (verificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{verificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = CalibrationCenterViewModelVerification.Verify(
                args[verificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var loggingVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(loggingVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (loggingVerificationIndex >= 0)
        {
            if (loggingVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{loggingVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = LoggingIntegrationVerification.Verify(
                args[loggingVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var toolRecipeTeachingVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(toolRecipeTeachingVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (toolRecipeTeachingVerificationIndex >= 0)
        {
            if (toolRecipeTeachingVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{toolRecipeTeachingVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolRecipeTeachingVerification.Verify(
                args[toolRecipeTeachingVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var toolRecipeSelectionsVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(toolRecipeSelectionsVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (toolRecipeSelectionsVerificationIndex >= 0)
        {
            if (toolRecipeSelectionsVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{toolRecipeSelectionsVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolRecipeSelectionContractVerification.Verify(
                args[toolRecipeSelectionsVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var workbenchDockingVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(workbenchDockingVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (workbenchDockingVerificationIndex >= 0)
        {
            if (workbenchDockingVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{workbenchDockingVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ToolWorkbenchDockingVerification.Verify(
                args[workbenchDockingVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var teachingCaptureViewModelVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(teachingCaptureViewModelVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (teachingCaptureViewModelVerificationIndex >= 0)
        {
            if (teachingCaptureViewModelVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{teachingCaptureViewModelVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = TeachingCaptureViewModelVerification.Verify(
                args[teachingCaptureViewModelVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var c3dHeightProfileVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(c3dHeightProfileVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (c3dHeightProfileVerificationIndex >= 0)
        {
            if (c3dHeightProfileVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{c3dHeightProfileVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = C3DHeightProfileVerification.Verify(
                args[c3dHeightProfileVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var profileViewModelVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(profileViewModelVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (profileViewModelVerificationIndex >= 0)
        {
            if (profileViewModelVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{profileViewModelVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = ProfileViewModelVerification.Verify(
                args[profileViewModelVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        var c3dHeightDistributionVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(c3dHeightDistributionVerificationOption, StringComparison.OrdinalIgnoreCase));
        if (c3dHeightDistributionVerificationIndex >= 0)
        {
            if (c3dHeightDistributionVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{c3dHeightDistributionVerificationOption} requires a report path.");
                Shutdown(2);
                return;
            }

            var passed = C3DHeightDistributionVerification.Verify(
                args[c3dHeightDistributionVerificationIndex + 1],
                out var summary);
            Console.WriteLine(summary);
            Shutdown(passed ? 0 : 1);
            return;
        }

        Console.WriteLine($"Unsupported Shell verification option: {string.Join(' ', args)}");
        Shutdown(2);
    }

    private static void Shutdown(int exitCode) =>
        System.Windows.Application.Current.Shutdown(exitCode);

    private sealed record VerificationArguments(string[] Args);
}

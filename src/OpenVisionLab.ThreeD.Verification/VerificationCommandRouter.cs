using System.IO;
using OpenVisionLab.ThreeD.Verification.Integration;
using OpenVisionLab.ThreeD.Verification.Data;
using OpenVisionLab.ThreeD.Verification.Logging;
using OpenVisionLab.ThreeD.Verification.Workbench;
using OpenVisionLab.ThreeD.Verification.Viewer;
using OpenVisionLab.ThreeD.Verification.Shell;
using OpenVisionLab.ThreeD.Verification.Shell.Smoke;
using OpenVisionLab.ThreeD.Verification.Shell.Artifacts;
using OpenVisionLab.ThreeD.Verification.Shell.Support;
using OpenVisionLab.ThreeD.Verification.Shell.Tools;
using OpenVisionLab.ThreeD.Verification.Shell.Workbench;

namespace OpenVisionLab.ThreeD.Verification;

internal static class VerificationCommandRouter
{
    public static int Run(string[] args)
    {
        const string importSurfaceOption = "--verify-import-surface";
        const string loggingVerificationOption = "--verify-logging";
        const string sourceChannelNormalQualityOption =
            "--verify-source-channel-normal-quality";
        const string viewerRecipeLoadPlanOption = "--verify-viewer-recipe-load-plan";
        const string viewerRecipeSavePlanOption = "--verify-viewer-recipe-save-plan";
        const string profileViewModelOption = "--verify-profile-viewmodel";
        const string inspectionSessionOption = "--verify-inspection-session";
        const string teachingCaptureViewModelOption = "--verify-teaching-capture-viewmodel";
        const string nominalActualViewModelOption =
            "--verify-nominal-actual-viewmodel";
        const string displayViewModelOption = "--verify-display-viewmodel";
        const string teachingSelectionSourcePolicyOption =
            "--verify-teaching-selection-source-policy";
        const string viewerSourceLoadOperationCoordinatorOption =
            "--verify-viewer-source-load-operation-coordinator";
        const string viewerControlLifetimeOption =
            "--verify-viewer-control-lifetime";
        const string shellSourceLoadOperationCoordinatorOption =
            "--verify-source-load-operation-coordinator";
        const string integrationViewModelOption =
            "--verify-integration-view-model";
        const string shellSurfaceMatchSmokeOption =
            "--verify-shell-surface-match-smoke";
        const string shellValidationSetSmokeOption =
            "--verify-shell-validation-set-smoke";
        const string shellSmokeCommandLineOptionsOption =
            "--verify-shell-smoke-command-line";
        const string inspectionWorkspaceSelectionOption =
            "--verify-inspection-workspace-selection";
        const string levelSurfaceWorkbenchOption =
            "--verify-level-surface-workbench";
        const string orderedRunOption =
            "--verify-current-recipe-ordered-run";
        const string toolRecipeTeachingOption =
            "--verify-tool-recipe-teaching";
        const string validationSetOption = "--verify-validation-set";
        const string displayedOutputsOwnerOption =
            "--verify-displayed-outputs-owner";
        const string completenessReviewOwnerOption =
            "--verify-completeness-review-owner";
        const string validationSetDefinitionOwnerOption =
            "--verify-validation-set-definition-owner";
        const string runLogRetentionOption = "--verify-run-log-retention";
        const string selectedStepExecutionRoutingOption =
            "--verify-selected-step-execution-routing";
        const string teachingSelectionOwnershipOption =
            "--verify-teaching-selection-ownership";
        const string renderableC3DCatalogOption =
            "--verify-renderable-c3d-catalog";
        const string multipleSurfaceMatchWorkbenchOption =
            "--verify-multiple-surface-match-workbench";
        const string surfaceMatchPublishedEvidenceOwnerOption =
            "--verify-surface-match-published-owner";
        const string surfaceEdgeDiagnosticReviewParityOption =
            "--verify-surface-edge-diagnostic-review-workbench-parity";
        const string surfaceEdgeWorkbenchParityOption =
            "--verify-surface-edge-workbench-parity";
        const string surfaceMatchWorkbenchParityOption =
            "--verify-surface-match-workbench-parity";
        const string sourceAcquisitionProvenanceOption =
            "--verify-source-acquisition-provenance";
        const string thicknessRepeatGridOption =
            "--verify-thickness-repeat-grid";
        const string artifactNavigatorOption = "--verify-artifact-navigator";
        const string calibrationCenterViewModelOption =
            "--verify-calibration-viewmodel";
        const string privacySafeSupportBundleOption =
            "--verify-privacy-safe-support-bundle";
        const string runRecordHistoryOption =
            "--verify-run-record-history";
        const string heightDifferenceEdgeWorkbenchOption =
            "--verify-tool-edge-workbench";
        const string twoPointLineWorkbenchOption =
            "--verify-tool-two-point-line-workbench";
        const string threePointPlaneWorkbenchOption =
            "--verify-tool-three-point-plane-workbench";
        const string datumPlaneDeviationWorkbenchOption =
            "--verify-tool-datum-plane-deviation-workbench";
        const string lineFitWorkbenchOption =
            "--verify-tool-line-fit-workbench";
        const string lineIntersectionWorkbenchOption =
            "--verify-tool-line-intersection-workbench";
        const string editableRegionOwnerOption =
            "--verify-tool-editable-region-owner";
        const string landmarkCorrespondenceWorkbenchOption =
            "--verify-tool-landmark-correspondence-workbench";
        const string xyzAffineWorkbenchOption =
            "--verify-tool-xyz-affine-workbench";
        const string heightMeasurementWorkbenchOption =
            "--verify-tool-height-measurement-workbench";
        const string domainMaskWorkbenchOption =
            "--verify-domain-mask-workbench";
        const string removeOutlierPixelsWorkbenchOption =
            "--verify-remove-outlier-pixels-workbench";
        const string roiCropWorkbenchOption =
            "--verify-roi-crop-workbench";
        const string regridHeightFieldWorkbenchOption =
            "--verify-regrid-height-field-workbench";
        var loggingVerificationIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                loggingVerificationOption,
                StringComparison.OrdinalIgnoreCase));
        if (loggingVerificationIndex >= 0)
        {
            if (loggingVerificationIndex + 1 >= args.Length)
            {
                Console.WriteLine($"{loggingVerificationOption} requires a report path.");
                return 2;
            }

            var loggingPassed = LoggingIntegrationVerification.Verify(
                args[loggingVerificationIndex + 1],
                out var loggingSummary);
            Console.WriteLine(loggingSummary);
            return loggingPassed ? 0 : 1;
        }

        var sourceChannelNormalQualityIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                sourceChannelNormalQualityOption,
                StringComparison.OrdinalIgnoreCase));
        if (sourceChannelNormalQualityIndex >= 0)
        {
            if (sourceChannelNormalQualityIndex + 1 >= args.Length)
            {
                Console.WriteLine(
                    $"{sourceChannelNormalQualityOption} requires a report path.");
                return 2;
            }

            var sourceQualityPassed = SourceChannelAndNormalQualityVerification.Verify(
                args[sourceChannelNormalQualityIndex + 1],
                out var sourceQualitySummary);
            Console.WriteLine(sourceQualitySummary);
            return sourceQualityPassed ? 0 : 1;
        }

        var viewerRecipeLoadPlanIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                viewerRecipeLoadPlanOption,
                StringComparison.OrdinalIgnoreCase));
        if (viewerRecipeLoadPlanIndex >= 0)
        {
            if (viewerRecipeLoadPlanIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[viewerRecipeLoadPlanIndex + 1]))
            {
                Console.WriteLine(
                    $"{viewerRecipeLoadPlanOption} requires a report path.");
                return 2;
            }

            var viewerPassed = ViewerRecipeLoadPlanVerification.Verify(
                args[viewerRecipeLoadPlanIndex + 1],
                out var viewerSummary);
            Console.WriteLine(viewerSummary);
            return viewerPassed ? 0 : 1;
        }

        var viewerRecipeSavePlanIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                viewerRecipeSavePlanOption,
                StringComparison.OrdinalIgnoreCase));
        if (viewerRecipeSavePlanIndex >= 0)
        {
            if (viewerRecipeSavePlanIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[viewerRecipeSavePlanIndex + 1]))
            {
                Console.WriteLine(
                    $"{viewerRecipeSavePlanOption} requires a report path.");
                return 2;
            }

            var viewerSavePassed = ViewerRecipeSavePlanVerification.Verify(
                args[viewerRecipeSavePlanIndex + 1],
                out var viewerSaveSummary);
            Console.WriteLine(viewerSaveSummary);
            return viewerSavePassed ? 0 : 1;
        }

        var profileViewModelIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                profileViewModelOption,
                StringComparison.OrdinalIgnoreCase));
        if (profileViewModelIndex >= 0)
        {
            if (profileViewModelIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[profileViewModelIndex + 1]))
            {
                Console.WriteLine(
                    $"{profileViewModelOption} requires a report path.");
                return 2;
            }

            var profilePassed = ProfileViewModelVerification.Verify(
                args[profileViewModelIndex + 1],
                out var profileSummary);
            Console.WriteLine(profileSummary);
            return profilePassed ? 0 : 1;
        }

        var inspectionSessionIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                inspectionSessionOption,
                StringComparison.OrdinalIgnoreCase));
        if (inspectionSessionIndex >= 0)
        {
            if (inspectionSessionIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[inspectionSessionIndex + 1]))
            {
                Console.WriteLine(
                    $"{inspectionSessionOption} requires a report path.");
                return 2;
            }

            var inspectionSessionPassed = ViewerInspectionSessionVerification.Verify(
                args[inspectionSessionIndex + 1],
                out var inspectionSessionSummary);
            Console.WriteLine(inspectionSessionSummary);
            return inspectionSessionPassed ? 0 : 1;
        }

        var teachingCaptureViewModelIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                teachingCaptureViewModelOption,
                StringComparison.OrdinalIgnoreCase));
        if (teachingCaptureViewModelIndex >= 0)
        {
            if (teachingCaptureViewModelIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[teachingCaptureViewModelIndex + 1]))
            {
                Console.WriteLine(
                    $"{teachingCaptureViewModelOption} requires a report path.");
                return 2;
            }

            var teachingCapturePassed = TeachingCaptureViewModelVerification.Verify(
                args[teachingCaptureViewModelIndex + 1],
                out var teachingCaptureSummary);
            Console.WriteLine(teachingCaptureSummary);
            return teachingCapturePassed ? 0 : 1;
        }

        var nominalActualViewModelIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                nominalActualViewModelOption,
                StringComparison.OrdinalIgnoreCase));
        if (nominalActualViewModelIndex >= 0)
        {
            if (nominalActualViewModelIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[nominalActualViewModelIndex + 1]))
            {
                Console.WriteLine(
                    $"{nominalActualViewModelOption} requires a report path.");
                return 2;
            }

            var nominalActualPassed = NominalActualComparisonViewModelVerification.Verify(
                args[nominalActualViewModelIndex + 1],
                out var nominalActualSummary);
            Console.WriteLine(nominalActualSummary);
            return nominalActualPassed ? 0 : 1;
        }

        var displayViewModelIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                displayViewModelOption,
                StringComparison.OrdinalIgnoreCase));
        if (displayViewModelIndex >= 0)
        {
            if (displayViewModelIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[displayViewModelIndex + 1]))
            {
                Console.WriteLine(
                    $"{displayViewModelOption} requires a report path.");
                return 2;
            }

            var displayPassed = ViewerDisplaySettingsViewModelVerification.Verify(
                args[displayViewModelIndex + 1],
                out var displaySummary);
            Console.WriteLine(displaySummary);
            return displayPassed ? 0 : 1;
        }

        var teachingSelectionSourcePolicyIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                teachingSelectionSourcePolicyOption,
                StringComparison.OrdinalIgnoreCase));
        if (teachingSelectionSourcePolicyIndex >= 0)
        {
            if (teachingSelectionSourcePolicyIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[teachingSelectionSourcePolicyIndex + 1]))
            {
                Console.WriteLine(
                    $"{teachingSelectionSourcePolicyOption} requires a report path.");
                return 2;
            }

            var teachingPolicyPassed = TeachingSelectionSourcePolicyVerification.Verify(
                out var teachingPolicySummary);
            var teachingPolicyReportPath = Path.GetFullPath(
                args[teachingSelectionSourcePolicyIndex + 1]);
            Directory.CreateDirectory(Path.GetDirectoryName(teachingPolicyReportPath)!);
            File.WriteAllText(
                teachingPolicyReportPath,
                teachingPolicySummary + Environment.NewLine);
            Console.WriteLine(teachingPolicySummary);
            return teachingPolicyPassed ? 0 : 1;
        }

        var viewerSourceLoadOperationCoordinatorIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                viewerSourceLoadOperationCoordinatorOption,
                StringComparison.OrdinalIgnoreCase));
        if (viewerSourceLoadOperationCoordinatorIndex >= 0)
        {
            if (viewerSourceLoadOperationCoordinatorIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[viewerSourceLoadOperationCoordinatorIndex + 1]))
            {
                Console.WriteLine(
                    $"{viewerSourceLoadOperationCoordinatorOption} requires a report path.");
                return 2;
            }

            var viewerOperationPassed = ViewerSourceLoadOperationCoordinatorVerification.Verify(
                args[viewerSourceLoadOperationCoordinatorIndex + 1],
                out var viewerOperationSummary);
            Console.WriteLine(viewerOperationSummary);
            return viewerOperationPassed ? 0 : 1;
        }

        var viewerControlLifetimeIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                viewerControlLifetimeOption,
                StringComparison.OrdinalIgnoreCase));
        if (viewerControlLifetimeIndex >= 0)
        {
            if (viewerControlLifetimeIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[viewerControlLifetimeIndex + 1]))
            {
                Console.WriteLine(
                    $"{viewerControlLifetimeOption} requires a report path.");
                return 2;
            }

            var viewerControlPassed = ViewerControlLifetimeVerification.Verify(
                args[viewerControlLifetimeIndex + 1],
                out var viewerControlSummary);
            Console.WriteLine(viewerControlSummary);
            return viewerControlPassed ? 0 : 1;
        }

        var shellSourceLoadOperationCoordinatorIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                shellSourceLoadOperationCoordinatorOption,
                StringComparison.OrdinalIgnoreCase));
        if (shellSourceLoadOperationCoordinatorIndex >= 0)
        {
            if (shellSourceLoadOperationCoordinatorIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[shellSourceLoadOperationCoordinatorIndex + 1]))
            {
                Console.WriteLine(
                    $"{shellSourceLoadOperationCoordinatorOption} requires a report path.");
                return 2;
            }

            var shellSourceLoadPassed = ShellSourceLoadOperationCoordinatorVerification.Verify(
                args[shellSourceLoadOperationCoordinatorIndex + 1],
                out var shellSourceLoadSummary);
            Console.WriteLine(shellSourceLoadSummary);
            return shellSourceLoadPassed ? 0 : 1;
        }

        if (ShellStartupConfigurationPlannerVerification.TryRun(
                args,
                out var shellStartupPassed,
                out var shellStartupSummary))
        {
            Console.WriteLine(shellStartupSummary);
            return shellStartupPassed ? 0 : 1;
        }

        if (ShellMainWindowViewModelLifecycleVerification.TryRun(
                args,
                out var lifecyclePassed,
                out var lifecycleSummary))
        {
            Console.WriteLine(lifecycleSummary);
            return lifecyclePassed ? 0 : 1;
        }

        var integrationViewModelIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                integrationViewModelOption,
                StringComparison.OrdinalIgnoreCase));
        if (integrationViewModelIndex >= 0)
        {
            if (integrationViewModelIndex + 1 >= args.Length
                || string.IsNullOrWhiteSpace(args[integrationViewModelIndex + 1]))
            {
                Console.WriteLine(
                    $"{integrationViewModelOption} requires a report path.");
                return 2;
            }

            var integrationPassed = ThreeDIntegrationViewModelVerification.Verify(
                args[integrationViewModelIndex + 1],
                out var integrationSummary);
            Console.WriteLine(integrationSummary);
            return integrationPassed ? 0 : 1;
        }

        var shellSurfaceMatchSmokeIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                shellSurfaceMatchSmokeOption,
                StringComparison.OrdinalIgnoreCase));
        if (shellSurfaceMatchSmokeIndex >= 0)
        {
            if (shellSurfaceMatchSmokeIndex + 2 >= args.Length
                || string.IsNullOrWhiteSpace(args[shellSurfaceMatchSmokeIndex + 1])
                || string.IsNullOrWhiteSpace(args[shellSurfaceMatchSmokeIndex + 2]))
            {
                Console.WriteLine(
                    $"{shellSurfaceMatchSmokeOption} requires artifact-directory and report paths.");
                return 2;
            }

            var shellSurfaceMatchPassed = ShellSurfaceMatchSmokeVerification.Verify(
                args[shellSurfaceMatchSmokeIndex + 1],
                args[shellSurfaceMatchSmokeIndex + 2],
                out var shellSurfaceMatchSummary);
            Console.WriteLine(shellSurfaceMatchSummary);
            return shellSurfaceMatchPassed ? 0 : 1;
        }

        var shellValidationSetSmokeIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                shellValidationSetSmokeOption,
                StringComparison.OrdinalIgnoreCase));
        if (shellValidationSetSmokeIndex >= 0)
        {
            if (shellValidationSetSmokeIndex + 2 >= args.Length
                || string.IsNullOrWhiteSpace(args[shellValidationSetSmokeIndex + 1])
                || string.IsNullOrWhiteSpace(args[shellValidationSetSmokeIndex + 2]))
            {
                Console.WriteLine(
                    $"{shellValidationSetSmokeOption} requires artifact-directory and report paths.");
                return 2;
            }

            var shellValidationSetPassed = ShellValidationSetSmokeVerification.Verify(
                args[shellValidationSetSmokeIndex + 1],
                args[shellValidationSetSmokeIndex + 2],
                out var shellValidationSetSummary);
            Console.WriteLine(shellValidationSetSummary);
            return shellValidationSetPassed ? 0 : 1;
        }

        var shellSmokeCommandLineOptionsIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                shellSmokeCommandLineOptionsOption,
                StringComparison.OrdinalIgnoreCase));
        if (shellSmokeCommandLineOptionsIndex >= 0)
        {
            var handled = ShellSmokeCommandLineOptionsVerification.TryRun(
                args,
                out var shellSmokeOptionsPassed,
                out var shellSmokeOptionsSummary);
            Console.WriteLine(shellSmokeOptionsSummary);
            return handled && shellSmokeOptionsPassed ? 0 : 1;
        }

        if (ShellSmokeLifetimeVerification.TryRun(
                args,
                out var shellSmokeLifetimePassed,
                out var shellSmokeLifetimeSummary))
        {
            Console.WriteLine(shellSmokeLifetimeSummary);
            return shellSmokeLifetimePassed ? 0 : 1;
        }

        var inspectionWorkspaceSelectionExitCode = TryRunReportOnly(
            args,
            inspectionWorkspaceSelectionOption,
            InspectionWorkspaceSelectionVerification.Verify);
        if (inspectionWorkspaceSelectionExitCode.HasValue)
        {
            return inspectionWorkspaceSelectionExitCode.Value;
        }

        var levelSurfaceWorkbenchExitCode = TryRunReportOnly(
            args,
            levelSurfaceWorkbenchOption,
            LevelSurfaceWorkbenchVerification.Verify);
        if (levelSurfaceWorkbenchExitCode.HasValue)
        {
            return levelSurfaceWorkbenchExitCode.Value;
        }

        var orderedRunExitCode = TryRunReportOnly(
            args,
            orderedRunOption,
            ToolRecipeOrderedRunVerification.Verify);
        if (orderedRunExitCode.HasValue)
        {
            return orderedRunExitCode.Value;
        }

        var toolRecipeTeachingExitCode = TryRunReportOnly(
            args,
            toolRecipeTeachingOption,
            ToolRecipeTeachingVerification.Verify);
        if (toolRecipeTeachingExitCode.HasValue)
        {
            return toolRecipeTeachingExitCode.Value;
        }

        var displayedOutputsExitCode = TryRunReportOnly(
            args,
            displayedOutputsOwnerOption,
            DisplayedOutputsOwnerVerification.Verify);
        if (displayedOutputsExitCode.HasValue)
        {
            return displayedOutputsExitCode.Value;
        }

        var completenessReviewExitCode = TryRunReportOnly(
            args,
            completenessReviewOwnerOption,
            CompletenessReviewOwnerVerification.Verify);
        if (completenessReviewExitCode.HasValue)
        {
            return completenessReviewExitCode.Value;
        }

        var validationSetDefinitionExitCode = TryRunReportOnly(
            args,
            validationSetDefinitionOwnerOption,
            ValidationSetDefinitionOwnerVerification.Verify);
        if (validationSetDefinitionExitCode.HasValue)
        {
            return validationSetDefinitionExitCode.Value;
        }

        var validationSetExitCode = TryRunReportOnly(
            args,
            validationSetOption,
            ToolRecipeValidationSetVerification.Verify);
        if (validationSetExitCode.HasValue)
        {
            return validationSetExitCode.Value;
        }

        var runLogRetentionExitCode = TryRunReportOnly(
            args,
            runLogRetentionOption,
            RunLogRetentionVerification.Verify);
        if (runLogRetentionExitCode.HasValue)
        {
            return runLogRetentionExitCode.Value;
        }

        var selectedStepExecutionRoutingExitCode = TryRunReportOnly(
            args,
            selectedStepExecutionRoutingOption,
            SelectedStepExecutionRoutingVerification.Verify);
        if (selectedStepExecutionRoutingExitCode.HasValue)
        {
            return selectedStepExecutionRoutingExitCode.Value;
        }

        var teachingSelectionOwnershipExitCode = TryRunReportOnly(
            args,
            teachingSelectionOwnershipOption,
            TeachingSelectionOwnershipVerification.Verify);
        if (teachingSelectionOwnershipExitCode.HasValue)
        {
            return teachingSelectionOwnershipExitCode.Value;
        }

        var sourceAcquisitionProvenanceExitCode = TryRunReportOnly(
            args,
            sourceAcquisitionProvenanceOption,
            SourceAcquisitionProvenanceVerification.Verify);
        if (sourceAcquisitionProvenanceExitCode.HasValue)
        {
            return sourceAcquisitionProvenanceExitCode.Value;
        }

        var thicknessRepeatGridExitCode = TryRunReportOnly(
            args,
            thicknessRepeatGridOption,
            ThicknessRepeatGridAuthoringVerification.Verify);
        if (thicknessRepeatGridExitCode.HasValue)
        {
            return thicknessRepeatGridExitCode.Value;
        }

        var artifactNavigatorExitCode = TryRunReportOnly(
            args,
            artifactNavigatorOption,
            ToolArtifactNavigatorVerification.Verify);
        if (artifactNavigatorExitCode.HasValue)
        {
            return artifactNavigatorExitCode.Value;
        }

        var calibrationCenterViewModelExitCode = TryRunReportOnly(
            args,
            calibrationCenterViewModelOption,
            CalibrationCenterViewModelVerification.Verify);
        if (calibrationCenterViewModelExitCode.HasValue)
        {
            return calibrationCenterViewModelExitCode.Value;
        }

        var privacySafeSupportBundleExitCode = TryRunReportOnly(
            args,
            privacySafeSupportBundleOption,
            PrivacySafeSupportBundleVerification.Verify);
        if (privacySafeSupportBundleExitCode.HasValue)
        {
            return privacySafeSupportBundleExitCode.Value;
        }

        var runRecordHistoryExitCode = TryRunReportOnly(
            args,
            runRecordHistoryOption,
            RunRecordHistoryVerification.Verify);
        if (runRecordHistoryExitCode.HasValue)
        {
            return runRecordHistoryExitCode.Value;
        }

        var heightMeasurementWorkbenchExitCode = TryRunReportOnly(
            args,
            heightMeasurementWorkbenchOption,
            ToolHeightMeasurementWorkbenchVerification.Verify);
        if (heightMeasurementWorkbenchExitCode.HasValue)
        {
            return heightMeasurementWorkbenchExitCode.Value;
        }

        var xyzAffineWorkbenchExitCode = TryRunReportOnly(
            args,
            xyzAffineWorkbenchOption,
            ToolXyzAffineWorkbenchVerification.Verify);
        if (xyzAffineWorkbenchExitCode.HasValue)
        {
            return xyzAffineWorkbenchExitCode.Value;
        }

        var landmarkCorrespondenceWorkbenchExitCode = TryRunReportOnly(
            args,
            landmarkCorrespondenceWorkbenchOption,
            ToolLandmarkCorrespondenceWorkbenchVerification.Verify);
        if (landmarkCorrespondenceWorkbenchExitCode.HasValue)
        {
            return landmarkCorrespondenceWorkbenchExitCode.Value;
        }

        var editableRegionOwnerExitCode = TryRunReportOnly(
            args,
            editableRegionOwnerOption,
            ToolEditableRegionOwnerVerification.Verify);
        if (editableRegionOwnerExitCode.HasValue)
        {
            return editableRegionOwnerExitCode.Value;
        }

        var domainMaskWorkbenchExitCode = TryRunReportOnly(
            args,
            domainMaskWorkbenchOption,
            DomainMaskWorkbenchVerification.Verify);
        if (domainMaskWorkbenchExitCode.HasValue)
        {
            return domainMaskWorkbenchExitCode.Value;
        }

        var removeOutlierPixelsWorkbenchExitCode = TryRunReportOnly(
            args,
            removeOutlierPixelsWorkbenchOption,
            RemoveOutlierPixelsWorkbenchVerification.Verify);
        if (removeOutlierPixelsWorkbenchExitCode.HasValue)
        {
            return removeOutlierPixelsWorkbenchExitCode.Value;
        }

        var roiCropWorkbenchExitCode = TryRunReportOnly(
            args,
            roiCropWorkbenchOption,
            RoiCropWorkbenchVerification.Verify);
        if (roiCropWorkbenchExitCode.HasValue)
        {
            return roiCropWorkbenchExitCode.Value;
        }

        var regridHeightFieldWorkbenchExitCode = TryRunReportOnly(
            args,
            regridHeightFieldWorkbenchOption,
            RegridHeightFieldWorkbenchVerification.Verify);
        if (regridHeightFieldWorkbenchExitCode.HasValue)
        {
            return regridHeightFieldWorkbenchExitCode.Value;
        }

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
                return 2;
            }

            var surfaceMatchPassed = SurfaceMatchWorkbenchParityVerification.Verify(
                args[surfaceMatchWorkbenchParityIndex + 1],
                args[surfaceMatchWorkbenchParityIndex + 2],
                args[surfaceMatchWorkbenchParityIndex + 3],
                args[surfaceMatchWorkbenchParityIndex + 4],
                out var surfaceMatchSummary);
            Console.WriteLine(surfaceMatchSummary);
            return surfaceMatchPassed ? 0 : 1;
        }

        var heightDifferenceEdgeWorkbenchExitCode = TryRunReportOnly(
            args,
            heightDifferenceEdgeWorkbenchOption,
            ToolHeightDifferenceEdgeWorkbenchVerification.Verify);
        if (heightDifferenceEdgeWorkbenchExitCode.HasValue)
        {
            return heightDifferenceEdgeWorkbenchExitCode.Value;
        }

        var twoPointLineWorkbenchExitCode = TryRunReportOnly(
            args,
            twoPointLineWorkbenchOption,
            ToolTwoPointLineWorkbenchVerification.Verify);
        if (twoPointLineWorkbenchExitCode.HasValue)
        {
            return twoPointLineWorkbenchExitCode.Value;
        }

        var threePointPlaneWorkbenchExitCode = TryRunReportOnly(
            args,
            threePointPlaneWorkbenchOption,
            ToolThreePointPlaneWorkbenchVerification.Verify);
        if (threePointPlaneWorkbenchExitCode.HasValue)
        {
            return threePointPlaneWorkbenchExitCode.Value;
        }

        var datumPlaneDeviationWorkbenchExitCode = TryRunReportOnly(
            args,
            datumPlaneDeviationWorkbenchOption,
            ToolDatumPlaneDeviationWorkbenchVerification.Verify);
        if (datumPlaneDeviationWorkbenchExitCode.HasValue)
        {
            return datumPlaneDeviationWorkbenchExitCode.Value;
        }

        var lineFitWorkbenchExitCode = TryRunReportOnly(
            args,
            lineFitWorkbenchOption,
            ToolLineFitWorkbenchVerification.Verify);
        if (lineFitWorkbenchExitCode.HasValue)
        {
            return lineFitWorkbenchExitCode.Value;
        }

        var lineIntersectionWorkbenchExitCode = TryRunReportOnly(
            args,
            lineIntersectionWorkbenchOption,
            ToolLineIntersectionWorkbenchVerification.Verify);
        if (lineIntersectionWorkbenchExitCode.HasValue)
        {
            return lineIntersectionWorkbenchExitCode.Value;
        }

        var renderableC3DCatalogExitCode = TryRunReportOnly(
            args,
            renderableC3DCatalogOption,
            RenderableC3DCatalogVerification.Verify);
        if (renderableC3DCatalogExitCode.HasValue)
        {
            return renderableC3DCatalogExitCode.Value;
        }

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
                return 2;
            }

            var multipleSurfaceMatchPassed = MultipleSurfaceMatchWorkbenchVerification.Verify(
                args[multipleSurfaceMatchWorkbenchIndex + 1],
                args[multipleSurfaceMatchWorkbenchIndex + 2],
                args[multipleSurfaceMatchWorkbenchIndex + 3],
                args[multipleSurfaceMatchWorkbenchIndex + 4],
                out var multipleSurfaceMatchSummary);
            Console.WriteLine(multipleSurfaceMatchSummary);
            return multipleSurfaceMatchPassed ? 0 : 1;
        }

        var surfaceMatchPublishedEvidenceOwnerIndex = Array.FindIndex(
            args,
            argument => argument.Equals(
                surfaceMatchPublishedEvidenceOwnerOption,
                StringComparison.OrdinalIgnoreCase));
        if (surfaceMatchPublishedEvidenceOwnerIndex >= 0)
        {
            if (surfaceMatchPublishedEvidenceOwnerIndex + 2 >= args.Length)
            {
                Console.WriteLine(
                    $"{surfaceMatchPublishedEvidenceOwnerOption} requires artifact-directory and report paths.");
                return 2;
            }

            var publishedOwnerPassed = SurfaceMatchPublishedEvidenceOwnerVerification.Verify(
                args[surfaceMatchPublishedEvidenceOwnerIndex + 1],
                args[surfaceMatchPublishedEvidenceOwnerIndex + 2],
                out var publishedOwnerSummary);
            Console.WriteLine(publishedOwnerSummary);
            return publishedOwnerPassed ? 0 : 1;
        }

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
                return 2;
            }

            var diagnosticReviewPassed =
                SurfaceEdgeDiagnosticReviewWorkbenchParityVerification.Verify(
                    args[surfaceEdgeDiagnosticReviewParityIndex + 1],
                    args[surfaceEdgeDiagnosticReviewParityIndex + 2],
                    out var diagnosticReviewSummary);
            Console.WriteLine(diagnosticReviewSummary);
            return diagnosticReviewPassed ? 0 : 1;
        }

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
                return 2;
            }

            var edgeParityPassed = SurfaceEdgeWorkbenchParityVerification.Verify(
                args[surfaceEdgeWorkbenchParityIndex + 1],
                args[surfaceEdgeWorkbenchParityIndex + 2],
                args[surfaceEdgeWorkbenchParityIndex + 3],
                args[surfaceEdgeWorkbenchParityIndex + 4],
                args[surfaceEdgeWorkbenchParityIndex + 5],
                args[surfaceEdgeWorkbenchParityIndex + 6],
                args[surfaceEdgeWorkbenchParityIndex + 7],
                out var edgeParitySummary);
            Console.WriteLine(edgeParitySummary);
            return edgeParityPassed ? 0 : 1;
        }

        var index = Array.FindIndex(
            args,
            argument => argument.Equals(
                importSurfaceOption,
                StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            Console.WriteLine(
                $"The verification runner requires {importSurfaceOption} and a report path.");
            return 2;
        }

        if (index + 1 >= args.Length
            || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            Console.WriteLine(
                $"{importSurfaceOption} requires a report path.");
            return 2;
        }

        var passed = ImportSurfaceViewModelVerification.Verify(
            args[index + 1],
            out var summary);
        Console.WriteLine(summary);
        return passed ? 0 : 1;
    }

    private delegate bool ReportVerifier(string reportPath, out string summary);

    private static int? TryRunReportOnly(
        string[] args,
        string option,
        ReportVerifier verifier)
    {
        var index = Array.FindIndex(
            args,
            argument => argument.Equals(option, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return null;
        }

        if (index + 1 >= args.Length
            || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            Console.WriteLine($"{option} requires a report path.");
            return 2;
        }

        var passed = verifier(args[index + 1], out var summary);
        Console.WriteLine(summary);
        return passed ? 0 : 1;
    }
}

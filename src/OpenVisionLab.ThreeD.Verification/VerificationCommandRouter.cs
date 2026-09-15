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
using OpenVisionLab.ThreeD.Verification.Shell.Behaviors;
using OpenVisionLab.ThreeD.Verification.Shell.Tools;
using OpenVisionLab.ThreeD.Verification.Shell.Workbench;

namespace OpenVisionLab.ThreeD.Verification;

internal static class VerificationCommandRouter
{
    private delegate bool ReportVerifier(string reportPath, out string summary);
    private delegate bool PathsVerifier(string[] args, int optionIndex, out string summary);
    private delegate bool SelfParsingVerifier(string[] args, out bool passed, out string summary);
    private sealed record Command(string Option, Func<string[], int, int> Execute);

    // Order is the CLI precedence contract, independent of the order of input flags.
    // Extend this registration list; do not split completed suite owners again.
    private static readonly Command[] Commands =
    [
        Report("--verify-c3d-roi-editing-session", C3DRoiEditingSessionVerification.Verify),
        Report("--verify-viewer-smoke-scenario", ViewerSmokeScenarioVerification.Verify),
        Report("--verify-validation-set-workspace-viewmodel", ValidationSetWorkspaceViewModelVerification.Verify),
        Report("--verify-viewer-workspace-viewmodel", ViewerWorkspaceViewModelVerification.Verify),
        Report("--verify-logging", LoggingIntegrationVerification.Verify, allowBlank: true),
        Report("--verify-source-channel-normal-quality", SourceChannelAndNormalQualityVerification.Verify, allowBlank: true),
        Report("--verify-viewer-recipe-load-plan", ViewerRecipeLoadPlanVerification.Verify),
        Report("--verify-viewer-recipe-save-plan", ViewerRecipeSavePlanVerification.Verify),
        Report("--verify-profile-viewmodel", ProfileViewModelVerification.Verify),
        Report("--verify-inspection-session", ViewerInspectionSessionVerification.Verify),
        Report("--verify-teaching-capture-viewmodel", TeachingCaptureViewModelVerification.Verify),
        Report("--verify-nominal-actual-viewmodel", NominalActualComparisonViewModelVerification.Verify),
        Report("--verify-display-viewmodel", ViewerDisplaySettingsViewModelVerification.Verify),
        Report("--verify-teaching-selection-source-policy", VerifyTeachingSelectionSourcePolicy),
        Report("--verify-height-deviation-roi-geometry", HeightDeviationRoiGeometryVerification.Verify),
        Report("--verify-viewer-source-load-operation-coordinator", ViewerSourceLoadOperationCoordinatorVerification.Verify),
        Report("--verify-viewer-laz-cache-store-admission", ViewerLazPointCloudStoreAdmissionVerification.Verify),
        Report("--verify-viewer-stale-progress-operation", ViewerStaleProgressOperationVerification.Verify),
        Report("--verify-viewer-dispatcher-shutdown-dispose", ViewerDispatcherShutdownDisposeVerification.Verify),
        Report("--verify-c3d-memory-measurement", C3DMemoryMeasurementVerification.Verify),
        Report("--verify-c3d-input-stability", C3DInputStabilityVerification.Verify),
        Report("--verify-viewer-source-unload-cancellation", ViewerSourceUnloadCancellationCoordinatorVerification.Verify),
        Report("--verify-viewer-language-refresh", ViewerLanguageRefreshCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-height-image-roi-coordinator", ToolWorkbenchHeightImageRoiCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-oriented-box-event-coordinator", ToolWorkbenchOrientedBoxEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-first-recipe-event-coordinator", ToolWorkbenchFirstRecipeEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-output-compare-event-coordinator", ToolWorkbenchOutputCompareEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-artifact-navigator-event-coordinator", ToolWorkbenchArtifactNavigatorEventCoordinatorVerification.Verify),
        Report("--verify-shell-main-window-event-coordinator", ShellMainWindowEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-validation-set-event-coordinator", ToolWorkbenchValidationSetEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-flow-diagnostics-event-coordinator", ToolWorkbenchFlowDiagnosticsEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-compatible-tool-catalog-event-coordinator", ToolWorkbenchCompatibleToolCatalogEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-completeness-review-event-coordinator", ToolWorkbenchCompletenessReviewEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-displayed-outputs-event-coordinator", ToolWorkbenchDisplayedOutputsEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-preparation-preset-event-coordinator", ToolWorkbenchPreparationPresetEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-surface-match-collection-event-coordinator", ToolWorkbenchSurfaceMatchCollectionEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-session-event-coordinators", ToolWorkbenchSessionEventCoordinatorsVerification.Verify),
        Report("--verify-tool-workbench-reference-catalog-event-coordinator", ToolWorkbenchReferenceCatalogEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-recipe-part-event-coordinator", ToolWorkbenchRecipePartEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-recipe-part-collection-tracking", ToolWorkbenchRecipePartCollectionTrackingVerification.Verify),
        Report("--verify-tool-workbench-editor-session-event-coordinator", ToolWorkbenchEditorSessionEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-teaching-selection-event-coordinator", ToolWorkbenchTeachingSelectionEventCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-lifetime-coordinator", ToolWorkbenchLifetimeCoordinatorVerification.Verify),
        Report("--verify-tool-workbench-cancellation-source-lifetime", ToolWorkbenchCancellationSourceLifetimeVerification.Verify),
        Report("--verify-viewer-control-lifetime", ViewerControlLifetimeVerification.Verify),
        Report("--verify-viewer-host-operation-facade", ViewerHostOperationFacadeVerification.Verify),
        Report("--verify-viewer-workbench-overlay-renderer", ViewerWorkbenchOverlayRendererVerification.Verify),
        Report("--verify-viewer-interaction-lod-state", ViewerInteractionLodStateVerification.Verify),
        Report("--verify-viewer-property-change-policy", ViewerPropertyChangePolicyVerification.Verify),
        Report("--verify-viewer-event-subscription", ViewerEventSubscriptionVerification.Verify),
        Report("--verify-viewer-localization-scope", ViewerLocalizationScopeVerification.Verify),
        Report("--verify-viewer-linked-height-hover-sampling", ViewerLinkedHeightHoverSamplingVerification.Verify),
        Report("--verify-viewer-ray-geometry", ViewerRayGeometryVerification.Verify),
        Report("--verify-viewer-input-geometry", ViewerInputGeometryVerification.Verify),
        Report("--verify-viewer-c3d-display-geometry", ViewerC3DGridDisplayGeometryVerification.Verify),
        Report("--verify-viewer-interaction-telemetry", ViewerInteractionTelemetryVerification.Verify),
        Report("--verify-c3d-source-apply-render-state", C3DSourceApplyRenderStateVerification.Verify),
        Report("--verify-c3d-gpu-telemetry", C3DGpuTelemetryVerification.Verify),
        Report("--verify-imported-mesh-texture-state", ImportedMeshTextureStateVerification.Verify),
        Report("--verify-opengl-resource-retirement-telemetry", OpenGLResourceRetirementTelemetryVerification.Verify),
        Report("--verify-opengl-resource-retirement-coordinator", OpenGLResourceRetirementCoordinatorVerification.Verify),
        Report("--verify-laz-point-cloud-load-telemetry", LazPointCloudLoadTelemetryVerification.Verify),
        Report("--verify-viewer-laz-point-cloud-load-preparation", ViewerLazPointCloudLoadPreparationVerification.Verify),
        Report("--verify-viewer-only-mesh-load-preparation", ViewerOnlyMeshLoadPreparationVerification.Verify),
        Report("--verify-viewer-only-source-load-coordinator", ViewerOnlySourceLoadCoordinatorVerification.Verify),
        Report("--verify-source-load-operation-coordinator", ShellSourceLoadOperationCoordinatorVerification.Verify),
        Report("--verify-shell-workbench-request-owner", ShellWorkbenchRequestOwnerVerification.Verify),
        Report("--verify-height-image-viewer-load-owner", HeightImageViewerLoadVerification.Verify),
        Report("--verify-shell-viewer-host-coordinator", ShellViewerHostCoordinatorVerification.Verify),
        SelfParsing("--verify-shell-startup-configuration", ShellStartupConfigurationPlannerVerification.TryRun),
        SelfParsing("--verify-shell-viewmodel-lifecycle", ShellMainWindowViewModelLifecycleVerification.TryRun),
        Report("--verify-integration-view-model", ThreeDIntegrationViewModelVerification.Verify),
        Report("--verify-integration-settings-store", ThreeDIntegrationSettingsStoreVerification.Verify),
        Report("--verify-integration-shared-key-session", ThreeDIntegrationSharedKeySessionVerification.Verify),
        Report("--verify-passwordbox-command-behavior", PasswordChangedCommandBehaviorVerification.Verify),
        Report("--verify-listbox-double-click-command-behavior", ListBoxMouseDoubleClickCommandBehaviorVerification.Verify),
        Report("--verify-property-grid-command-behavior", CommitPropertyGridCommandBehaviorVerification.Verify),
        Report("--verify-height-image-viewer-keyboard-behavior", HeightImageViewerKeyboardBehaviorVerification.Verify),
        Report("--verify-preview-mouse-down-command-behavior", PreviewMouseDownCommandBehaviorVerification.Verify),
        Report("--verify-results-workspace-section-binding", ResultsWorkspaceSectionBindingVerification.Verify),
        Report("--verify-tool-lab-activation-selection", ToolLabActivationSelectionVerification.Verify),
        Report("--verify-viewer-workspace-popout-dismissal", ViewerWorkspacePopoutDismissalVerification.Verify),
        Report("--verify-tool-recipe-workbench-stage-navigation", ToolRecipeWorkbenchStageNavigationVerification.Verify),
        Paths("--verify-shell-surface-match-smoke", 2, false, "artifact-directory and report paths.",
            (string[] args, int index, out string summary) => ShellSurfaceMatchSmokeVerification.Verify(args[index + 1], args[index + 2], out summary)),
        Paths("--verify-shell-validation-set-smoke", 2, false, "artifact-directory and report paths.",
            (string[] args, int index, out string summary) => ShellValidationSetSmokeVerification.Verify(args[index + 1], args[index + 2], out summary)),
        SelfParsing("--verify-shell-smoke-command-line", ShellSmokeCommandLineOptionsVerification.TryRun),
        SelfParsing("--verify-shell-smoke-lifetime", ShellSmokeLifetimeVerification.TryRun),
        Report("--verify-inspection-workspace-selection", InspectionWorkspaceSelectionVerification.Verify),
        Report("--verify-level-surface-workbench", LevelSurfaceWorkbenchVerification.Verify),
        Report("--verify-current-recipe-ordered-run", ToolRecipeOrderedRunVerification.Verify),
        Report("--verify-tool-recipe-teaching", ToolRecipeTeachingVerification.Verify),
        Report("--verify-displayed-outputs-owner", DisplayedOutputsOwnerVerification.Verify),
        Report("--verify-completeness-review-owner", CompletenessReviewOwnerVerification.Verify),
        Report("--verify-validation-set-definition-owner", ValidationSetDefinitionOwnerVerification.Verify),
        Report("--verify-validation-set", ToolRecipeValidationSetVerification.Verify),
        Report("--verify-run-log-retention", RunLogRetentionVerification.Verify),
        Report("--verify-selected-step-execution-routing", SelectedStepExecutionRoutingVerification.Verify),
        Report("--verify-teaching-selection-ownership", TeachingSelectionOwnershipVerification.Verify),
        Report("--verify-source-acquisition-provenance", SourceAcquisitionProvenanceVerification.Verify),
        Report("--verify-thickness-repeat-grid", ThicknessRepeatGridAuthoringVerification.Verify),
        Report("--verify-artifact-navigator", ToolArtifactNavigatorVerification.Verify),
        Report("--verify-calibration-viewmodel", CalibrationCenterViewModelVerification.Verify),
        Report("--verify-privacy-safe-support-bundle", PrivacySafeSupportBundleVerification.Verify),
        Report("--verify-run-record-history", RunRecordHistoryVerification.Verify),
        Report("--verify-tool-height-measurement-workbench", ToolHeightMeasurementWorkbenchVerification.Verify),
        Report("--verify-tool-xyz-affine-workbench", ToolXyzAffineWorkbenchVerification.Verify),
        Report("--verify-tool-landmark-correspondence-workbench", ToolLandmarkCorrespondenceWorkbenchVerification.Verify),
        Report("--verify-tool-editable-region-owner", ToolEditableRegionOwnerVerification.Verify),
        Report("--verify-domain-mask-workbench", DomainMaskWorkbenchVerification.Verify),
        Report("--verify-remove-outlier-pixels-workbench", RemoveOutlierPixelsWorkbenchVerification.Verify),
        Report("--verify-roi-crop-workbench", RoiCropWorkbenchVerification.Verify),
        Report("--verify-regrid-height-field-workbench", RegridHeightFieldWorkbenchVerification.Verify),
        Paths("--verify-surface-match-workbench-parity", 4, true, "model, scene, Runner execution, and report paths.",
            (string[] args, int index, out string summary) => SurfaceMatchWorkbenchParityVerification.Verify(
                args[index + 1],
                args[index + 2],
                args[index + 3],
                args[index + 4],
                out summary)),
        Report("--verify-tool-edge-workbench", ToolHeightDifferenceEdgeWorkbenchVerification.Verify),
        Report("--verify-tool-two-point-line-workbench", ToolTwoPointLineWorkbenchVerification.Verify),
        Report("--verify-tool-three-point-plane-workbench", ToolThreePointPlaneWorkbenchVerification.Verify),
        Report("--verify-tool-datum-plane-deviation-workbench", ToolDatumPlaneDeviationWorkbenchVerification.Verify),
        Report("--verify-tool-line-fit-workbench", ToolLineFitWorkbenchVerification.Verify),
        Report("--verify-tool-line-intersection-workbench", ToolLineIntersectionWorkbenchVerification.Verify),
        Report("--verify-renderable-c3d-catalog", RenderableC3DCatalogVerification.Verify),
        Paths("--verify-multiple-surface-match-workbench", 4, true, "model, scene, collection, and report paths.",
            (string[] args, int index, out string summary) => MultipleSurfaceMatchWorkbenchVerification.Verify(
                args[index + 1],
                args[index + 2],
                args[index + 3],
                args[index + 4],
                out summary)),
        Paths("--verify-surface-match-published-owner", 2, true, "artifact-directory and report paths.",
            (string[] args, int index, out string summary) => SurfaceMatchPublishedEvidenceOwnerVerification.Verify(args[index + 1], args[index + 2], out summary)),
        Paths("--verify-surface-edge-diagnostic-review-workbench-parity", 2, true, "artifact-directory and report paths.",
            (string[] args, int index, out string summary) => SurfaceEdgeDiagnosticReviewWorkbenchParityVerification.Verify(args[index + 1], args[index + 2], out summary)),
        Paths("--verify-surface-edge-workbench-parity", 7, true, "model, scene, execution, model edge, scene edge, Runner score, and report paths.",
            (string[] args, int index, out string summary) => SurfaceEdgeWorkbenchParityVerification.Verify(
                args[index + 1],
                args[index + 2],
                args[index + 3],
                args[index + 4],
                args[index + 5],
                args[index + 6],
                args[index + 7],
                out summary)),
        Report("--verify-import-surface", ImportSurfaceViewModelVerification.Verify),
    ];

    public static int Run(string[] args)
    {
        foreach (var command in Commands)
        {
            var index = Array.FindIndex(args, argument => argument.Equals(command.Option, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                return command.Execute(args, index);
            }
        }

        Console.WriteLine("The verification runner requires --verify-import-surface and a report path.");
        return 2;
    }

    private static Command Report(string option, ReportVerifier verifier, bool allowBlank = false) =>
        Paths(option, 1, allowBlank, "a report path.",
            (string[] args, int index, out string summary) => verifier(args[index + 1], out summary));

    private static Command Paths(string option, int count, bool allowBlank, string requiredPaths, PathsVerifier verifier) =>
        new(option, (args, index) =>
        {
            if (index + count >= args.Length
                || (!allowBlank && Enumerable.Range(1, count).Any(offset => string.IsNullOrWhiteSpace(args[index + offset]))))
            {
                Console.WriteLine($"{option} requires {requiredPaths}");
                return 2;
            }

            var passed = verifier(args, index, out var summary);
            Console.WriteLine(summary);
            return passed ? 0 : 1;
        });

    // These legacy suites own their argument validation, including failure exit code 1.
    private static Command SelfParsing(string option, SelfParsingVerifier verifier) =>
        new(option, (args, _) =>
        {
            var handled = verifier(args, out var passed, out var summary);
            Console.WriteLine(summary);
            return handled && passed ? 0 : 1;
        });

    private static bool VerifyTeachingSelectionSourcePolicy(string reportPath, out string summary)
    {
        var passed = TeachingSelectionSourcePolicyVerification.Verify(out summary);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllText(fullReportPath, summary + Environment.NewLine);
        return passed;
    }
}

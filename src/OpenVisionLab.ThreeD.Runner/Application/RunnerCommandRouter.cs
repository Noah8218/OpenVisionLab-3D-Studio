using System.Globalization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
using static RunnerApplication;

internal static class RunnerCommandRouter
{
    public static int Run(string[] args)
    {
        if (args.Contains("--help", StringComparer.OrdinalIgnoreCase))
        {
            WriteUsage(Console.Out);
            return 0;
        }

        var lazProbePath = ReadOption(args, "--laz-probe");
        var lazLoadPlanPath = ReadOption(args, "--laz-load-plan");
        var stlStreamProbePath = ReadOption(args, "--stl-stream-probe");
        var meshDeviationParityPath = ReadOption(args, "--mesh-deviation-parity");
        var meshDeviationNominalPath = ReadOption(args, "--nominal-stl");
        var meshDeviationUnsignedPath = ReadOption(args, "--cloudcompare-unsigned");
        var meshDeviationSignedPath = ReadOption(args, "--cloudcompare-signed");
        var stanfordTransformPath = ReadOption(args, "--stanford-transform-parity");
        var stanfordTransformReferencePath = ReadOption(args, "--transform-reference");
        var stlStreamProbeUnit = ReadOption(args, "--unit");
        var sourceQualityC3DPath = ReadOption(args, "--source-quality-c3d");
        var sourceQualityEntityId = ReadOption(args, "--entity-id");
        var sourceQualityFrameId = ReadOption(args, "--frame");
        var heightImageC3DPath = ReadOption(args, "--height-image-c3d");
        var heightImageAlignmentSpecificationPath = ReadOption(args, "--height-image-alignment-spec");
        var rigidPointPairAlignmentSpecificationPath = ReadOption(args, "--rigid-point-pair-alignment-spec");
        var constrainedBestFitRigidAlignmentSpecificationPath = ReadOption(args, "--constrained-best-fit-rigid-alignment-spec");
        var heightThresholdBackgroundRemovalSpecificationPath = ReadOption(args, "--height-threshold-background-removal-spec");
        var heightBackgroundSubtractionSpecificationPath = ReadOption(args, "--height-background-subtraction-spec");
        var pointCloudBackgroundFilterSpecificationPath = ReadOption(args, "--point-cloud-background-filter-spec");
        var pointCloudVoxelDownsampleSpecificationPath = ReadOption(args, "--point-cloud-voxel-downsample-spec");
        var heightMapNormalPreparationSpecificationPath = ReadOption(args, "--height-map-normal-preparation-spec");
        var regionGrowingComponentSpecificationPath = ReadOption(args, "--region-growing-component-spec");
        var c3DMapProbePath = ReadOption(args, "--c3d-map-probe");
        var c3DMapPlyPath = ReadOption(args, "--ply");
        var recipePath = ReadOption(args, "--recipe");
        var toolRecipePath = ReadOption(args, "--tool-recipe");
        var labeledValidationRecipePath =
            ReadOption(args, "--labeled-validation-recipe");
        var thresholdCorrectionRecipePath =
            ReadOption(args, "--threshold-correction-recipe");
        var thresholdCandidateId =
            ReadOption(args, "--threshold-candidate-id");
        var thresholdManualValues =
            ReadOption(args, "--threshold-manual-values");
        var surfaceMatchModelPath =
            ReadOption(args, "--surface-match-model");
        var surfaceMatchScenePath =
            ReadOption(args, "--surface-match-scene");
        var surfaceMatchExecutionPath =
            ReadOption(args, "--surface-match-execution");
        var surfaceMatchScorePath =
            ReadOption(args, "--surface-match-score");
        var surfaceMatchAssessmentPath =
            ReadOption(args, "--surface-match-assessment");
        var surfaceMatchRuntimePath =
            ReadOption(args, "--surface-match-runtime");
        var toolRecipeSourcePath = ReadOption(args, "--source");
        var toolTeachingFilterPath = ReadOption(args, "--tool-teaching-filter");
        var toolTeachingRemoveOutliersPath =
            ReadOption(args, "--tool-teaching-remove-outliers");
        var toolTeachingLevelSurfacePath =
            ReadOption(args, "--tool-teaching-level-surface");
        var toolTeachingEdgePath = ReadOption(args, "--tool-teaching-edge");
        var toolTeachingLineFitPath = ReadOption(args, "--tool-teaching-line-fit");
        var toolTeachingTwoPointLinePath = ReadOption(args, "--tool-teaching-two-point-line");
        var toolTeachingThreePointPlanePath = ReadOption(args, "--tool-teaching-three-point-plane");
        var toolTeachingDatumPlaneDeviationPath = ReadOption(args, "--tool-teaching-datum-plane-deviation");
        var toolTeachingLineIntersectionPath = ReadOption(args, "--tool-teaching-line-intersection");
        var toolTeachingLandmarkCorrespondencePath = ReadOption(args, "--tool-teaching-landmark-correspondence");
        var toolTeachingStepId = ReadOption(args, "--tool-teaching-step");
        var outputC3DPath = ReadOption(args, "--output-c3d");
        var alignedPointRepeatabilityStudyPath = ReadOption(args, "--aligned-point-repeatability-study");
        var syntheticAffinePackagePath = ReadOption(args, "--synthetic-affine-package");
        var runRecordHistoryRootPath = ReadOption(args, "--run-record-history");
        var runRecordHistoryStatus = ReadOption(args, "--history-status");
        var runRecordHistoryTool = ReadOption(args, "--history-tool");
        var runRecordHistoryFromUtc = ReadOption(args, "--history-from-utc");
        var runRecordHistoryToUtc = ReadOption(args, "--history-to-utc");
        var runRecordHistoryCsvPath = ReadOption(args, "--history-csv");
        var runRecordHistoryJsonPath = ReadOption(args, "--history-json");
        var reportPath = ReadOption(args, "--report");
        var expectedStatus = ReadOption(args, "--expect-status");
        var compareContractPath = ReadOption(args, "--compare-contract");
        var runArtifacts = new RunArtifactOptions(
            ReadOption(args, "--run-record"),
            ReadOption(args, "--html-report"),
            ReadOption(args, "--csv-report"),
            ReadOption(args, "--viewer-screenshot"));

        var c3DMapPointOnly = args.Contains("--point-only", StringComparer.OrdinalIgnoreCase);

        if (runRecordHistoryRootPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --run-record-history <directory> --report <path>");
                return 2;
            }

            if (!TryCreateRunRecordHistoryOptions(
                    runRecordHistoryStatus,
                    runRecordHistoryTool,
                    runRecordHistoryFromUtc,
                    runRecordHistoryToUtc,
                    out var historyOptions,
                    out var historyFilterError))
            {
                Console.Error.WriteLine($"Invalid Run Record history filter: {historyFilterError}");
                return 2;
            }

            return RunRecordHistoryExecution.Run(
                runRecordHistoryRootPath,
                reportPath,
                historyOptions,
                runRecordHistoryCsvPath,
                runRecordHistoryJsonPath);
        }

        if (sourceQualityC3DPath is not null)
        {
            if (sourceQualityEntityId is null
                || string.IsNullOrWhiteSpace(stlStreamProbeUnit)
                || sourceQualityFrameId is null
                || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --source-quality-c3d <path> --entity-id <id> --unit <unit> --frame <frame> --report <json>");
                return 2;
            }

            return SourceQualityReportExecution.Run(
                sourceQualityC3DPath,
                sourceQualityEntityId,
                stlStreamProbeUnit,
                sourceQualityFrameId,
                reportPath);
        }

        if (heightImageC3DPath is not null)
        {
            if (sourceQualityEntityId is null
                || string.IsNullOrWhiteSpace(stlStreamProbeUnit)
                || sourceQualityFrameId is null
                || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --height-image-c3d <path> --entity-id <id> --unit <unit> --frame <frame> --report <path>");
                return 2;
            }

            return C3DHeightImageVerification.RunProbe(
                heightImageC3DPath,
                sourceQualityEntityId,
                stlStreamProbeUnit,
                sourceQualityFrameId,
                reportPath);
        }

        if (heightImageAlignmentSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --height-image-alignment-spec <json> --report <path>");
                return 2;
            }

            return C3DHeightImageAlignmentRunnerExecution.Run(
                heightImageAlignmentSpecificationPath,
                reportPath);
        }

        if (rigidPointPairAlignmentSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --rigid-point-pair-alignment-spec <json> --report <path>");
                return 2;
            }

            return C3DRigidPointPairAlignmentRunnerExecution.Run(
                rigidPointPairAlignmentSpecificationPath,
                reportPath);
        }

        if (constrainedBestFitRigidAlignmentSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --constrained-best-fit-rigid-alignment-spec <json> --report <path>");
                return 2;
            }

            return C3DConstrainedBestFitRigidAlignmentRunnerExecution.Run(
                constrainedBestFitRigidAlignmentSpecificationPath,
                reportPath);
        }

        if (heightThresholdBackgroundRemovalSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --height-threshold-background-removal-spec <json> --report <path>");
                return 2;
            }

            return C3DHeightThresholdBackgroundRemovalRunnerExecution.Run(
                heightThresholdBackgroundRemovalSpecificationPath,
                reportPath);
        }

        if (heightBackgroundSubtractionSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --height-background-subtraction-spec <json> --report <path>");
                return 2;
            }

            return C3DHeightBackgroundSubtractionRunnerExecution.Run(
                heightBackgroundSubtractionSpecificationPath,
                reportPath);
        }

        if (pointCloudBackgroundFilterSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --point-cloud-background-filter-spec <json> --report <path>");
                return 2;
            }

            return C3DPointCloudBackgroundFilterRunnerExecution.Run(
                pointCloudBackgroundFilterSpecificationPath,
                reportPath);
        }

        if (pointCloudVoxelDownsampleSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --point-cloud-voxel-downsample-spec <json> --report <path>");
                return 2;
            }

            return C3DPointCloudVoxelDownsampleRunnerExecution.Run(
                pointCloudVoxelDownsampleSpecificationPath,
                reportPath);
        }

        if (heightMapNormalPreparationSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --height-map-normal-preparation-spec <json> --report <path>");
                return 2;
            }

            return C3DHeightMapNormalPreparationRunnerExecution.Run(
                heightMapNormalPreparationSpecificationPath,
                reportPath);
        }

        if (regionGrowingComponentSpecificationPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --region-growing-component-spec <json> --report <path>");
                return 2;
            }

            return C3DRegionGrowingComponentRunnerExecution.Run(
                regionGrowingComponentSpecificationPath,
                reportPath);
        }

        if (labeledValidationRecipePath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --labeled-validation-recipe <recipe> --report <json>");
                return 2;
            }

            return ToolRecipeLabeledValidationRunnerExecution.Run(
                labeledValidationRecipePath,
                reportPath);
        }

        if (thresholdCorrectionRecipePath is not null)
        {
            if (string.IsNullOrWhiteSpace(thresholdCandidateId)
                || reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --threshold-correction-recipe <recipe> --threshold-candidate-id <id> [--threshold-manual-values <Name=Value;...>] --report <json>");
                return 2;
            }

            return ToolRecipeThresholdCorrectionRunnerExecution.Run(
                thresholdCorrectionRecipePath,
                thresholdCandidateId,
                reportPath,
                thresholdManualValues);
        }

        if (surfaceMatchExecutionPath is not null
            || surfaceMatchModelPath is not null
            || surfaceMatchScenePath is not null
            || surfaceMatchScorePath is not null
            || surfaceMatchAssessmentPath is not null
            || surfaceMatchRuntimePath is not null)
        {
            if (toolRecipePath is null
                || surfaceMatchModelPath is null
                || surfaceMatchScenePath is null
                || surfaceMatchExecutionPath is null
                || reportPath is null
                || !runArtifacts.Requested
                || (surfaceMatchScorePath is null)
                    != (surfaceMatchAssessmentPath is null)
                || (surfaceMatchRuntimePath is not null
                    && surfaceMatchAssessmentPath is null))
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --tool-recipe <recipe> --surface-match-model <json> --surface-match-scene <json> --surface-match-execution <json> [--surface-match-score <json> --surface-match-assessment <json>] [--surface-match-runtime <json>] --report <txt> [--run-record <json> --html-report <html> --csv-report <csv>]");
                return 2;
            }

            return SurfaceMatchRunRecordExportExecution.Run(
                toolRecipePath,
                surfaceMatchModelPath,
                surfaceMatchScenePath,
                surfaceMatchExecutionPath,
                surfaceMatchScorePath,
                surfaceMatchAssessmentPath,
                surfaceMatchRuntimePath,
                reportPath,
                runArtifacts);
        }

        if (toolRecipePath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-recipe <path> [--source <c3d>] --report <path> [--expect-status Pass|Fail|Warning|Error] [--run-record <json> --html-report <html> --csv-report <csv>]");
                return 2;
            }
            return RunToolRecipe(
                toolRecipePath,
                toolRecipeSourcePath,
                reportPath,
                expectedStatus,
                runArtifacts);
        }

        if (toolTeachingFilterPath is not null)
        {
            if (toolTeachingStepId is null || outputC3DPath is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-filter <recipe> --tool-teaching-step <id> --output-c3d <path> --report <path>");
                return 2;
            }

            return ToolRecipeFilterRunnerExecution.Run(toolTeachingFilterPath, toolTeachingStepId, outputC3DPath, reportPath);
        }

        if (toolTeachingRemoveOutliersPath is not null)
        {
            if (toolTeachingStepId is null
                || outputC3DPath is null
                || reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-remove-outliers <recipe> --tool-teaching-step <id> --output-c3d <path> --report <path>");
                return 2;
            }

            return ToolRecipeRemoveOutlierPixelsRunnerExecution.Run(
                toolTeachingRemoveOutliersPath,
                toolTeachingStepId,
                outputC3DPath,
                reportPath);
        }

        if (toolTeachingLevelSurfacePath is not null)
        {
            if (toolTeachingStepId is null
                || outputC3DPath is null
                || reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-level-surface <recipe> --tool-teaching-step <id> --output-c3d <path> --report <path>");
                return 2;
            }

            return ToolRecipeLevelSurfaceRunnerExecution.Run(
                toolTeachingLevelSurfacePath,
                toolTeachingStepId,
                outputC3DPath,
                reportPath);
        }

        if (toolTeachingEdgePath is not null)
        {
            if (toolTeachingStepId is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-edge <recipe> --tool-teaching-step <id> --report <path>");
                return 2;
            }

            return ToolRecipeHeightDifferenceEdgeRunnerExecution.Run(toolTeachingEdgePath, toolTeachingStepId, reportPath);
        }

        if (toolTeachingLineFitPath is not null)
        {
            if (toolTeachingStepId is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-line-fit <recipe> --tool-teaching-step <id> --report <path>");
                return 2;
            }

            return ToolRecipeLineFitRunnerExecution.Run(toolTeachingLineFitPath, toolTeachingStepId, reportPath);
        }

        if (toolTeachingTwoPointLinePath is not null)
        {
            if (toolTeachingStepId is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-two-point-line <recipe> --tool-teaching-step <id> --report <path>");
                return 2;
            }

            return ToolRecipeTwoPointLineRunnerExecution.Run(toolTeachingTwoPointLinePath, toolTeachingStepId, reportPath);
        }

        if (toolTeachingThreePointPlanePath is not null)
        {
            if (toolTeachingStepId is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-three-point-plane <recipe> --tool-teaching-step <id> --report <path>");
                return 2;
            }

            return ToolRecipeThreePointPlaneRunnerExecution.Run(toolTeachingThreePointPlanePath, toolTeachingStepId, reportPath);
        }

        if (toolTeachingDatumPlaneDeviationPath is not null)
        {
            if (toolTeachingStepId is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-datum-plane-deviation <recipe> --tool-teaching-step <id> --report <path>");
                return 2;
            }

            return ToolRecipeDatumPlaneDeviationRunnerExecution.Run(toolTeachingDatumPlaneDeviationPath, toolTeachingStepId, reportPath);
        }

        if (toolTeachingLineIntersectionPath is not null)
        {
            if (toolTeachingStepId is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-line-intersection <recipe> --tool-teaching-step <id> --report <path>");
                return 2;
            }

            return ToolRecipeLineIntersectionRunnerExecution.Run(toolTeachingLineIntersectionPath, toolTeachingStepId, reportPath);
        }

        if (toolTeachingLandmarkCorrespondencePath is not null)
        {
            if (toolTeachingStepId is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-landmark-correspondence <recipe> --tool-teaching-step <id> --report <path>");
                return 2;
            }

            return ToolRecipeLandmarkCorrespondenceRunnerExecution.Run(toolTeachingLandmarkCorrespondencePath, toolTeachingStepId, reportPath);
        }

        var preparationExitCode = TryRunVerification(args, reportPath,
        [
            new("--verify-c3d-filter", C3DMedianFilterGoldenVerification.Run),
            new("--verify-c3d-remove-outliers", C3DRemoveOutlierPixelsGoldenVerification.Run),
            new("--verify-c3d-level-surface", C3DLevelSurfaceGoldenVerification.Run),
            new("--verify-c3d-roi-crop", C3DRoiCropGoldenVerification.Run),
            new("--verify-c3d-crop-axis-grid-roundtrip", C3DCropAxisGridRoundTripVerification.Run),
            new("--verify-c3d-domain-mask", C3DDomainMaskGoldenVerification.Run),
            new("--verify-oriented-box-3d", RunSelectionContract, UsageOption: "--verify-grid-polygon"),
            new("--verify-grid-circle", RunSelectionContract, UsageOption: "--verify-grid-polygon"),
            new("--verify-grid-polygon", RunSelectionContract, UsageOption: "--verify-grid-polygon"),
            new("--verify-c3d-completeness-grid", C3DCompletenessGridGoldenVerification.Run),
            new("--verify-labeled-validation-runner", ToolRecipeLabeledValidationRunnerVerification.Run),
            new("--verify-threshold-correction-runner", ToolRecipeThresholdCorrectionRunnerVerification.Run),
            new("--verify-c3d-region-completeness-output-state", C3DRegionCompletenessOutputStateVerification.Run),
            new("--verify-artifact-owned-roi-runner", report => ArtifactOwnedRoiRunnerVerification.Run(report, runArtifacts)),
            new("--verify-synthetic-affine-inspection-plate",
                report => SyntheticAffineInspectionPlateVerification.Run(syntheticAffinePackagePath!, report, runArtifacts),
                "--synthetic-affine-package <directory>",
                syntheticAffinePackagePath is not null),
            new("--verify-c3d-edge", C3DHeightDifferenceEdgeGoldenVerification.Run),
            new("--verify-c3d-line-fit", C3DLineFitGoldenVerification.Run),
            new("--verify-c3d-two-point-line", C3DTwoPointLineGoldenVerification.Run),
            new("--verify-c3d-three-point-plane", C3DThreePointPlaneGoldenVerification.Run),
            new("--verify-c3d-datum-plane-deviation", C3DDatumPlaneDeviationGoldenVerification.Run),
            new("--verify-c3d-line-intersection", C3DLineIntersectionGoldenVerification.Run),
            new("--verify-c3d-landmark-correspondence", C3DLandmarkCorrespondenceGoldenVerification.Run),
            new("--verify-c3d-affine-solve", C3DAffineSolveGoldenVerification.Run),
            new("--verify-c3d-affine-apply", C3DAffineApplyGoldenVerification.Run),
            new("--verify-c3d-coordinate-transform", C3DCoordinateTransformGoldenVerification.Run),
            new("--verify-c3d-regrid-height-field", C3DRegridHeightFieldGoldenVerification.Run),
            new("--verify-source-quality-report", SourceQualityReportVerification.Run),
            new("--verify-laz-load-plan", LazPointCloudLoadPlanVerification.Run),
            new("--verify-c3d-height-image", C3DHeightImageVerification.Run),
            new("--verify-c3d-height-image-alignment", C3DHeightImageAlignmentGoldenVerification.Run),
            new("--verify-c3d-rigid-point-pair-alignment", C3DRigidPointPairAlignmentGoldenVerification.Run),
            new("--verify-c3d-constrained-best-fit-rigid-alignment", C3DConstrainedBestFitRigidAlignmentGoldenVerification.Run),
            new("--verify-c3d-height-threshold-background-removal", C3DHeightThresholdBackgroundRemovalGoldenVerification.Run),
            new("--verify-c3d-height-background-subtraction", C3DHeightBackgroundSubtractionGoldenVerification.Run),
            new("--verify-c3d-point-cloud-background-filter", C3DPointCloudBackgroundFilterGoldenVerification.Run),
            new("--verify-c3d-point-cloud-voxel-downsample", C3DPointCloudVoxelDownsampleGoldenVerification.Run),
            new("--verify-c3d-height-map-normal-preparation", C3DHeightMapNormalPreparationGoldenVerification.Run),
            new("--verify-c3d-region-growing-component", C3DRegionGrowingComponentGoldenVerification.Run),
            new("--verify-c3d-region-transform-propagation", C3DRegionTransformPropagationGoldenVerification.Run),
            new("--verify-c3d-invalid-cell-map", C3DInvalidCellMapVerification.Run),
        ]);
        if (preparationExitCode.HasValue)
        {
            return preparationExitCode.Value;
        }

        if (stanfordTransformPath is not null)
        {
            if (stanfordTransformReferencePath is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --stanford-transform-parity <conf> --transform-reference <json> --report <path>");
                return 2;
            }

            return StanfordTransformParityVerification.Run(stanfordTransformPath, stanfordTransformReferencePath, reportPath);
        }

        if (alignedPointRepeatabilityStudyPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --aligned-point-repeatability-study <json> --report <path>");
                return 2;
            }

            return AlignedPointRepeatabilityStudyExecution.Run(alignedPointRepeatabilityStudyPath, reportPath);
        }

        var inspectionExitCode = TryRunVerification(args, reportPath,
        [
            new("--verify-run-record-reports", RunRecordReportVerification.Run),
            new("--verify-run-record-history-query", RunRecordHistoryQueryVerification.Run),
            new("--verify-nominal-actual-comparison", NominalActualComparisonVerification.Run),
            new("--verify-surface-model-foundation", SurfaceModelFoundationVerification.Run),
            new("--verify-surface-model-surface-selection", SurfaceModelSurfaceSelectionVerification.Run),
            new("--verify-model-key-points", ModelKeyPointArtifactVerification.Run),
            new("--verify-surface-matching-foundation", SurfaceMatchingFoundationVerification.Run),
            new("--verify-surface-match-run-record-export", SurfaceMatchRunRecordExportVerification.Run),
            new("--verify-surface-match-acceptance", SurfaceMatchAcceptanceGoldenVerification.Run),
            new("--verify-surface-match-performance-budget", SurfaceMatchPerformanceBudgetVerification.Run),
            new("--verify-multiple-surface-match", MultipleSurfaceMatchVerification.Run),
            new("--verify-surface-match-pose-equivalence", SurfaceMatchPoseEquivalenceVerification.Run),
            new("--verify-surface-edge-matching", SurfaceEdgeMatchingVerification.Run),
            new("--verify-surface-edge-diagnostic-review", SurfaceEdgeDiagnosticReviewVerification.Run),
            new("--verify-surface-edge-acquisition-direction", SurfaceEdgeAcquisitionDirectionVerification.Run),
            new("--verify-registration-acceptance", RegistrationAcceptanceGoldenVerification.Run),
            new("--verify-thickness-repeatability", ThicknessRepeatabilityGoldenVerification.Run),
            new("--verify-thickness-repeatability-study", ThicknessRepeatabilityStudyLoaderVerification.Run),
            new("--verify-aligned-point-repeatability", AlignedPointRepeatabilityGoldenVerification.Run),
            new("--verify-aligned-point-repeatability-study", AlignedPointRepeatabilityStudyLoaderVerification.Run),
            new("--verify-vision-sdk-3d", VisionSdkThreeDPackageVerification.Run),
            new("--verify-mesh-deviation", MeshDeviationGoldenVerification.Run),
            new("--verify-c3d-map-fidelity", C3DMapFidelityVerification.RunGolden),
            new("--verify-point-pair-dimensions", PointPairDimensionsGoldenVerification.Run),
            new("--verify-c3d-thickness", C3DThicknessGoldenVerification.Run),
            new("--verify-c3d-thickness-h-axis", C3DThicknessAxisGoldenVerification.Run),
            new("--verify-c3d-warpage", C3DWarpageGoldenVerification.Run),
            new("--verify-gap-flush", GapFlushGoldenVerification.Run),
            new("--verify-volume", VolumeGoldenVerification.Run),
            new("--verify-cross-section", CrossSectionDimensionsGoldenVerification.Run),
            new("--verify-plane-flatness", PlaneFlatnessGoldenVerification.Run),
        ]);
        if (inspectionExitCode.HasValue)
        {
            return inspectionExitCode.Value;
        }

        if (lazLoadPlanPath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --laz-load-plan <path> --report <path> [--max-sampled-points <count>]");
                return 2;
            }

            int maxSampledPoints;
            try
            {
                maxSampledPoints = ReadIntOption(args, "--max-sampled-points") ?? 50000;
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            return RunLazLoadPlan(lazLoadPlanPath, reportPath, maxSampledPoints);
        }

        if (lazProbePath is not null)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --laz-probe <path> --report <path> [--max-sampled-points <count>]");
                return 2;
            }

            int maxSampledPoints;
            try
            {
                maxSampledPoints = ReadIntOption(args, "--max-sampled-points") ?? 50000;
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            return RunLazProbe(lazProbePath, reportPath, maxSampledPoints);
        }

        if (stlStreamProbePath is not null)
        {
            if (reportPath is null || string.IsNullOrWhiteSpace(stlStreamProbeUnit))
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --stl-stream-probe <path> --unit <unit> --report <path>");
                return 2;
            }

            return RunStlStreamProbe(stlStreamProbePath, stlStreamProbeUnit, reportPath);
        }

        if (meshDeviationParityPath is not null)
        {
            if (reportPath is null
                || string.IsNullOrWhiteSpace(stlStreamProbeUnit)
                || meshDeviationNominalPath is null
                || meshDeviationUnsignedPath is null
                || meshDeviationSignedPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --mesh-deviation-parity <measured.ply> --nominal-stl <nominal.stl> --cloudcompare-unsigned <unsigned.ply> --cloudcompare-signed <signed.ply> --unit <unit> --report <path> [--max-points <count>]");
                return 2;
            }

            int? maxPoints;
            try
            {
                maxPoints = ReadIntOption(args, "--max-points");
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            return RunMeshDeviationParity(
                meshDeviationNominalPath,
                meshDeviationParityPath,
                meshDeviationUnsignedPath,
                meshDeviationSignedPath,
                stlStreamProbeUnit,
                reportPath,
                maxPoints);
        }

        if (c3DMapProbePath is not null)
        {
            if (reportPath is null || c3DMapPlyPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --c3d-map-probe <path> --ply <path> --report <path> [--max-sampled-points <count>] [--point-only]");
                return 2;
            }

            int maxSampledPoints;
            try
            {
                maxSampledPoints = ReadIntOption(args, "--max-sampled-points") ?? 140000;
            }
            catch (InvalidDataException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }

            return C3DMapFidelityVerification.RunProbe(c3DMapProbePath, c3DMapPlyPath, reportPath, maxSampledPoints, includeFaces: !c3DMapPointOnly);
        }

        if (recipePath is null || reportPath is null)
        {
            WriteUsage(Console.Error);
            return 2;
        }

        return RunRecipe(recipePath, reportPath, expectedStatus, compareContractPath, runArtifacts);

    }

    private sealed record VerificationCommand(
        string Option,
        Func<string, int> Execute,
        string? AdditionalUsage = null,
        bool HasAdditionalArguments = true,
        string? UsageOption = null);

    // Keep the two registration groups at their existing product-command precedence.
    // Verifiers retain their own integer exit codes; only argument admission is shared here.
    private static int? TryRunVerification(string[] args, string? reportPath, VerificationCommand[] commands)
    {
        foreach (var command in commands)
        {
            if (!args.Contains(command.Option, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (reportPath is null || !command.HasAdditionalArguments)
            {
                var additionalUsage = command.AdditionalUsage is null ? string.Empty : $"{command.AdditionalUsage} ";
                Console.Error.WriteLine($"Usage: OpenVisionLab.ThreeD.Runner {command.UsageOption ?? command.Option} {additionalUsage}--report <path>");
                return 2;
            }

            return command.Execute(reportPath);
        }

        return null;
    }

    private static int RunSelectionContract(string reportPath)
    {
        var succeeded = ToolRecipeSelectionContractVerification.Verify(reportPath, out var summary);
        Console.WriteLine(summary);
        return succeeded ? 0 : 5;
    }

    private static bool TryCreateRunRecordHistoryOptions(
        string? statusText,
        string? toolName,
        string? fromUtcText,
        string? toUtcText,
        out RunRecordHistoryQueryOptions options,
        out string error)
    {
        ResultStatus? status = null;
        if (statusText is not null
            && (!Enum.TryParse<ResultStatus>(statusText, ignoreCase: true, out var parsedStatus)
                || !Enum.IsDefined(parsedStatus)))
        {
            options = new RunRecordHistoryQueryOptions();
            error = $"unknown status '{statusText}'";
            return false;
        }

        if (statusText is not null)
        {
            status = Enum.Parse<ResultStatus>(statusText, ignoreCase: true);
        }

        if (!TryParseHistoryDate(fromUtcText, "--history-from-utc", out var fromUtc, out error)
            || !TryParseHistoryDate(toUtcText, "--history-to-utc", out var toUtc, out error))
        {
            options = new RunRecordHistoryQueryOptions();
            return false;
        }

        options = new RunRecordHistoryQueryOptions(status, toolName, fromUtc, toUtc);
        try
        {
            options.Validate();
            error = string.Empty;
            return true;
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static bool TryParseHistoryDate(
        string? value,
        string optionName,
        out DateTimeOffset? parsed,
        out string error)
    {
        if (value is null)
        {
            parsed = null;
            error = string.Empty;
            return true;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind,
                out var timestamp))
        {
            parsed = timestamp;
            error = string.Empty;
            return true;
        }

        parsed = null;
        error = $"{optionName} requires an ISO-8601 timestamp.";
        return false;
    }

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --recipe <path> --report <path> [--expect-status Pass|Fail|Warning|Error] [--compare-contract <path>] [--run-record <json> --html-report <html> --csv-report <csv> --viewer-screenshot <png>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --laz-load-plan <path> --report <path> [--max-sampled-points <count>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --laz-probe <path> --report <path> [--max-sampled-points <count>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-laz-load-plan --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --stl-stream-probe <path> --unit <unit> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --mesh-deviation-parity <measured.ply> --nominal-stl <nominal.stl> --cloudcompare-unsigned <unsigned.ply> --cloudcompare-signed <signed.ply> --unit <unit> --report <path> [--max-points <count>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --stanford-transform-parity <conf> --transform-reference <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --c3d-map-probe <path> --ply <path> --report <path> [--max-sampled-points <count>] [--point-only]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --source-quality-c3d <path> --entity-id <id> --unit <unit> --frame <frame> --report <json>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --run-record-history <directory> --report <path> [--history-csv <path>] [--history-json <path>] [--history-status Pass|Fail|Warning|Error|NotRun] [--history-tool <exact-name>] [--history-from-utc <ISO-8601>] [--history-to-utc <ISO-8601>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --height-image-alignment-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --rigid-point-pair-alignment-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --constrained-best-fit-rigid-alignment-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --height-threshold-background-removal-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --height-background-subtraction-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --point-cloud-background-filter-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --point-cloud-voxel-downsample-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --height-map-normal-preparation-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --region-growing-component-spec <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --aligned-point-repeatability-study <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-plane-flatness --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-thickness --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-warpage --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-oriented-box-3d --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-grid-circle --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-grid-polygon --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-artifact-owned-roi-runner --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-edge --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-remove-outliers --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-level-surface --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-roi-crop --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-crop-axis-grid-roundtrip --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-domain-mask --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-line-fit --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-point-pair-dimensions --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-gap-flush --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-volume --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-cross-section --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-map-fidelity --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-source-quality-report --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-invalid-cell-map --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-height-image-alignment --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-rigid-point-pair-alignment --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-constrained-best-fit-rigid-alignment --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-height-threshold-background-removal --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-height-background-subtraction --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-point-cloud-background-filter --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-point-cloud-voxel-downsample --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-height-map-normal-preparation --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-region-growing-component --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-mesh-deviation --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-nominal-actual-comparison --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-model-foundation --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-model-surface-selection --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-model-key-points --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-matching-foundation --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-run-record-reports --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-run-record-history-query --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-match-run-record-export --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-match-acceptance --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-match-performance-budget --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-multiple-surface-match --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-registration-acceptance --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-vision-sdk-3d --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --tool-recipe <path> [--source <c3d>] --report <path> [--run-record <json> --html-report <html> --csv-report <csv>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --tool-recipe <recipe> --surface-match-model <json> --surface-match-scene <json> --surface-match-execution <json> [--surface-match-score <json> --surface-match-assessment <json>] [--surface-match-runtime <json>] --report <txt> [--run-record <json> --html-report <html> --csv-report <csv>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --labeled-validation-recipe <recipe> --report <json>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --threshold-correction-recipe <recipe> --threshold-candidate-id <id> [--threshold-manual-values <Name=Value;...>] --report <json>");
    }
}

using System.Globalization;
using OpenVisionLab.ThreeD.Core;
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
        var reportPath = ReadOption(args, "--report");
        var expectedStatus = ReadOption(args, "--expect-status");
        var compareContractPath = ReadOption(args, "--compare-contract");
        var runArtifacts = new RunArtifactOptions(
            ReadOption(args, "--run-record"),
            ReadOption(args, "--html-report"),
            ReadOption(args, "--csv-report"),
            ReadOption(args, "--viewer-screenshot"));
        var verifyPlaneFlatness = args.Contains("--verify-plane-flatness", StringComparer.OrdinalIgnoreCase);
        var verifyC3DThickness = args.Contains("--verify-c3d-thickness", StringComparer.OrdinalIgnoreCase);
        var verifyC3DFilter = args.Contains("--verify-c3d-filter", StringComparer.OrdinalIgnoreCase);
        var verifyC3DRemoveOutliers = args.Contains(
            "--verify-c3d-remove-outliers",
            StringComparer.OrdinalIgnoreCase);
        var verifyC3DLevelSurface = args.Contains(
            "--verify-c3d-level-surface",
            StringComparer.OrdinalIgnoreCase);
        var verifyC3DEdge = args.Contains("--verify-c3d-edge", StringComparer.OrdinalIgnoreCase);
        var verifyC3DLineFit = args.Contains("--verify-c3d-line-fit", StringComparer.OrdinalIgnoreCase);
        var verifyC3DTwoPointLine = args.Contains("--verify-c3d-two-point-line", StringComparer.OrdinalIgnoreCase);
        var verifyC3DThreePointPlane = args.Contains("--verify-c3d-three-point-plane", StringComparer.OrdinalIgnoreCase);
        var verifyC3DDatumPlaneDeviation = args.Contains("--verify-c3d-datum-plane-deviation", StringComparer.OrdinalIgnoreCase);
        var verifyC3DLineIntersection = args.Contains("--verify-c3d-line-intersection", StringComparer.OrdinalIgnoreCase);
        var verifyC3DLandmarkCorrespondence = args.Contains("--verify-c3d-landmark-correspondence", StringComparer.OrdinalIgnoreCase);
        var verifyC3DAffineSolve = args.Contains("--verify-c3d-affine-solve", StringComparer.OrdinalIgnoreCase);
        var verifyC3DAffineApply = args.Contains("--verify-c3d-affine-apply", StringComparer.OrdinalIgnoreCase);
        var verifyC3DRegridHeightField = args.Contains("--verify-c3d-regrid-height-field", StringComparer.OrdinalIgnoreCase);
        var verifyArtifactOwnedRoiRunner = args.Contains("--verify-artifact-owned-roi-runner", StringComparer.OrdinalIgnoreCase);
        var verifySyntheticAffineInspectionPlate = args.Contains("--verify-synthetic-affine-inspection-plate", StringComparer.OrdinalIgnoreCase);
        var verifyC3DWarpage = args.Contains("--verify-c3d-warpage", StringComparer.OrdinalIgnoreCase);
        var verifyPointPairDimensions = args.Contains("--verify-point-pair-dimensions", StringComparer.OrdinalIgnoreCase);
        var verifyGapFlush = args.Contains("--verify-gap-flush", StringComparer.OrdinalIgnoreCase);
        var verifyVolume = args.Contains("--verify-volume", StringComparer.OrdinalIgnoreCase);
        var verifyCrossSection = args.Contains("--verify-cross-section", StringComparer.OrdinalIgnoreCase);
        var verifyC3DMapFidelity = args.Contains("--verify-c3d-map-fidelity", StringComparer.OrdinalIgnoreCase);
        var verifyMeshDeviation = args.Contains("--verify-mesh-deviation", StringComparer.OrdinalIgnoreCase);
        var verifyNominalActualComparison = args.Contains("--verify-nominal-actual-comparison", StringComparer.OrdinalIgnoreCase);
        var verifyRegistrationAcceptance = args.Contains("--verify-registration-acceptance", StringComparer.OrdinalIgnoreCase);
        var verifyThicknessRepeatability = args.Contains("--verify-thickness-repeatability", StringComparer.OrdinalIgnoreCase);
        var verifyThicknessRepeatabilityStudy = args.Contains("--verify-thickness-repeatability-study", StringComparer.OrdinalIgnoreCase);
        var verifyAlignedPointRepeatability = args.Contains("--verify-aligned-point-repeatability", StringComparer.OrdinalIgnoreCase);
        var verifyAlignedPointRepeatabilityStudy = args.Contains("--verify-aligned-point-repeatability-study", StringComparer.OrdinalIgnoreCase);
        var verifyVisionSdkThreeD = args.Contains("--verify-vision-sdk-3d", StringComparer.OrdinalIgnoreCase);
        var verifySourceQualityReport = args.Contains("--verify-source-quality-report", StringComparer.OrdinalIgnoreCase);
        var verifyC3DHeightImage = args.Contains("--verify-c3d-height-image", StringComparer.OrdinalIgnoreCase);
        var verifyC3DInvalidCellMap = args.Contains("--verify-c3d-invalid-cell-map", StringComparer.OrdinalIgnoreCase);
        var verifySurfaceModelFoundation = args.Contains(
            "--verify-surface-model-foundation",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceModelSurfaceSelection = args.Contains(
            "--verify-surface-model-surface-selection",
            StringComparer.OrdinalIgnoreCase);
        var verifyModelKeyPoints = args.Contains(
            "--verify-model-key-points",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceMatchingFoundation = args.Contains(
            "--verify-surface-matching-foundation",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceMatchAcceptance = args.Contains(
            "--verify-surface-match-acceptance",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceMatchPerformanceBudget = args.Contains(
            "--verify-surface-match-performance-budget",
            StringComparer.OrdinalIgnoreCase);
        var verifyMultipleSurfaceMatch = args.Contains(
            "--verify-multiple-surface-match",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceMatchPoseEquivalence = args.Contains(
            "--verify-surface-match-pose-equivalence",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceEdgeMatching = args.Contains(
            "--verify-surface-edge-matching",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceEdgeDiagnosticReview = args.Contains(
            "--verify-surface-edge-diagnostic-review",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceEdgeAcquisitionDirection = args.Contains(
            "--verify-surface-edge-acquisition-direction",
            StringComparer.OrdinalIgnoreCase);
        var verifySurfaceMatchRunRecordExport = args.Contains(
            "--verify-surface-match-run-record-export",
            StringComparer.OrdinalIgnoreCase);
        var verifyC3DCompletenessGrid = args.Contains(
            "--verify-c3d-completeness-grid",
            StringComparer.OrdinalIgnoreCase);
        var c3DMapPointOnly = args.Contains("--point-only", StringComparer.OrdinalIgnoreCase);

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

        if (verifyC3DFilter)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-filter --report <path>");
                return 2;
            }

            return C3DMedianFilterGoldenVerification.Run(reportPath);
        }

        if (verifyC3DRemoveOutliers)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-remove-outliers --report <path>");
                return 2;
            }

            return C3DRemoveOutlierPixelsGoldenVerification.Run(reportPath);
        }

        if (verifyC3DLevelSurface)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-level-surface --report <path>");
                return 2;
            }

            return C3DLevelSurfaceGoldenVerification.Run(reportPath);
        }

        if (verifyC3DCompletenessGrid)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-completeness-grid --report <path>");
                return 2;
            }

            return C3DCompletenessGridGoldenVerification.Run(reportPath);
        }

        if (verifyArtifactOwnedRoiRunner)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-artifact-owned-roi-runner --report <path>");
                return 2;
            }
            return ArtifactOwnedRoiRunnerVerification.Run(reportPath, runArtifacts);
        }

        if (verifySyntheticAffineInspectionPlate)
        {
            if (syntheticAffinePackagePath is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-synthetic-affine-inspection-plate --synthetic-affine-package <directory> --report <path>");
                return 2;
            }

            return SyntheticAffineInspectionPlateVerification.Run(syntheticAffinePackagePath, reportPath, runArtifacts);
        }

        if (verifyC3DEdge)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-edge --report <path>");
                return 2;
            }

            return C3DHeightDifferenceEdgeGoldenVerification.Run(reportPath);
        }

        if (verifyC3DLineFit)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-line-fit --report <path>");
                return 2;
            }

            return C3DLineFitGoldenVerification.Run(reportPath);
        }

        if (verifyC3DTwoPointLine)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-two-point-line --report <path>");
                return 2;
            }

            return C3DTwoPointLineGoldenVerification.Run(reportPath);
        }

        if (verifyC3DThreePointPlane)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-three-point-plane --report <path>");
                return 2;
            }

            return C3DThreePointPlaneGoldenVerification.Run(reportPath);
        }

        if (verifyC3DDatumPlaneDeviation)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-datum-plane-deviation --report <path>");
                return 2;
            }

            return C3DDatumPlaneDeviationGoldenVerification.Run(reportPath);
        }

        if (verifyC3DLineIntersection)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-line-intersection --report <path>");
                return 2;
            }

            return C3DLineIntersectionGoldenVerification.Run(reportPath);
        }

        if (verifyC3DLandmarkCorrespondence)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-landmark-correspondence --report <path>");
                return 2;
            }

            return C3DLandmarkCorrespondenceGoldenVerification.Run(reportPath);
        }

        if (verifyC3DAffineSolve)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-affine-solve --report <path>");
                return 2;
            }

            return C3DAffineSolveGoldenVerification.Run(reportPath);
        }

        if (verifyC3DAffineApply)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-affine-apply --report <path>");
                return 2;
            }

            return C3DAffineApplyGoldenVerification.Run(reportPath);
        }

        if (verifyC3DRegridHeightField)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-regrid-height-field --report <path>");
                return 2;
            }

            return C3DRegridHeightFieldGoldenVerification.Run(reportPath);
        }

        if (verifySourceQualityReport)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-source-quality-report --report <path>");
                return 2;
            }

            return SourceQualityReportVerification.Run(reportPath);
        }

        if (verifyC3DHeightImage)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-height-image --report <path>");
                return 2;
            }

            return C3DHeightImageVerification.Run(reportPath);
        }

        if (verifyC3DInvalidCellMap)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-invalid-cell-map --report <path>");
                return 2;
            }

            return C3DInvalidCellMapVerification.Run(reportPath);
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

        if (verifyNominalActualComparison)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-nominal-actual-comparison --report <path>");
                return 2;
            }

            return NominalActualComparisonVerification.Run(reportPath);
        }

        if (verifySurfaceModelFoundation)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-model-foundation --report <path>");
                return 2;
            }

            return SurfaceModelFoundationVerification.Run(reportPath);
        }

        if (verifySurfaceModelSurfaceSelection)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-model-surface-selection --report <path>");
                return 2;
            }

            return SurfaceModelSurfaceSelectionVerification.Run(reportPath);
        }

        if (verifyModelKeyPoints)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-model-key-points --report <path>");
                return 2;
            }

            return ModelKeyPointArtifactVerification.Run(reportPath);
        }

        if (verifySurfaceMatchingFoundation)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-matching-foundation --report <path>");
                return 2;
            }

            return SurfaceMatchingFoundationVerification.Run(reportPath);
        }

        if (verifySurfaceMatchRunRecordExport)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-match-run-record-export --report <path>");
                return 2;
            }

            return SurfaceMatchRunRecordExportVerification.Run(reportPath);
        }

        if (verifySurfaceMatchAcceptance)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-match-acceptance --report <path>");
                return 2;
            }

            return SurfaceMatchAcceptanceGoldenVerification.Run(
                reportPath);
        }

        if (verifySurfaceMatchPerformanceBudget)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-match-performance-budget --report <path>");
                return 2;
            }

            return SurfaceMatchPerformanceBudgetVerification.Run(
                reportPath);
        }

        if (verifyMultipleSurfaceMatch)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-multiple-surface-match --report <path>");
                return 2;
            }

            return MultipleSurfaceMatchVerification.Run(reportPath);
        }

        if (verifySurfaceMatchPoseEquivalence)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-match-pose-equivalence --report <path>");
                return 2;
            }

            return SurfaceMatchPoseEquivalenceVerification.Run(reportPath);
        }

        if (verifySurfaceEdgeMatching)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-edge-matching --report <path>");
                return 2;
            }

            return SurfaceEdgeMatchingVerification.Run(reportPath);
        }

        if (verifySurfaceEdgeDiagnosticReview)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-edge-diagnostic-review --report <path>");
                return 2;
            }

            return SurfaceEdgeDiagnosticReviewVerification.Run(reportPath);
        }

        if (verifySurfaceEdgeAcquisitionDirection)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine(
                    "Usage: OpenVisionLab.ThreeD.Runner --verify-surface-edge-acquisition-direction --report <path>");
                return 2;
            }

            return SurfaceEdgeAcquisitionDirectionVerification.Run(reportPath);
        }

        if (verifyRegistrationAcceptance)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-registration-acceptance --report <path>");
                return 2;
            }

            return RegistrationAcceptanceGoldenVerification.Run(reportPath);
        }

        if (verifyThicknessRepeatability)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-thickness-repeatability --report <path>");
                return 2;
            }

            return ThicknessRepeatabilityGoldenVerification.Run(reportPath);
        }

        if (verifyThicknessRepeatabilityStudy)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-thickness-repeatability-study --report <path>");
                return 2;
            }

            return ThicknessRepeatabilityStudyLoaderVerification.Run(reportPath);
        }

        if (verifyAlignedPointRepeatability)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-aligned-point-repeatability --report <path>");
                return 2;
            }

            return AlignedPointRepeatabilityGoldenVerification.Run(reportPath);
        }

        if (verifyAlignedPointRepeatabilityStudy)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-aligned-point-repeatability-study --report <path>");
                return 2;
            }

            return AlignedPointRepeatabilityStudyLoaderVerification.Run(reportPath);
        }

        if (verifyVisionSdkThreeD)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-vision-sdk-3d --report <path>");
                return 2;
            }

            return VisionSdkThreeDPackageVerification.Run(reportPath);
        }

        if (verifyMeshDeviation)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-mesh-deviation --report <path>");
                return 2;
            }

            return MeshDeviationGoldenVerification.Run(reportPath);
        }

        if (verifyC3DMapFidelity)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-map-fidelity --report <path>");
                return 2;
            }

            return C3DMapFidelityVerification.RunGolden(reportPath);
        }

        if (verifyPointPairDimensions)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-point-pair-dimensions --report <path>");
                return 2;
            }

            return PointPairDimensionsGoldenVerification.Run(reportPath);
        }

        if (verifyC3DThickness)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-thickness --report <path>");
                return 2;
            }

            return C3DThicknessGoldenVerification.Run(reportPath);
        }

        if (verifyC3DWarpage)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-c3d-warpage --report <path>");
                return 2;
            }

            return C3DWarpageGoldenVerification.Run(reportPath);
        }

        if (verifyGapFlush)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-gap-flush --report <path>");
                return 2;
            }

            return GapFlushGoldenVerification.Run(reportPath);
        }

        if (verifyVolume)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-volume --report <path>");
                return 2;
            }

            return VolumeGoldenVerification.Run(reportPath);
        }

        if (verifyCrossSection)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-cross-section --report <path>");
                return 2;
            }

            return CrossSectionDimensionsGoldenVerification.Run(reportPath);
        }

        if (verifyPlaneFlatness)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-plane-flatness --report <path>");
                return 2;
            }

            return PlaneFlatnessGoldenVerification.Run(reportPath);
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

    private static void WriteUsage(TextWriter writer)
    {
        writer.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --recipe <path> --report <path> [--expect-status Pass|Fail|Warning|Error] [--compare-contract <path>] [--run-record <json> --html-report <html> --csv-report <csv> --viewer-screenshot <png>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --laz-probe <path> --report <path> [--max-sampled-points <count>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --stl-stream-probe <path> --unit <unit> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --mesh-deviation-parity <measured.ply> --nominal-stl <nominal.stl> --cloudcompare-unsigned <unsigned.ply> --cloudcompare-signed <signed.ply> --unit <unit> --report <path> [--max-points <count>]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --stanford-transform-parity <conf> --transform-reference <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --c3d-map-probe <path> --ply <path> --report <path> [--max-sampled-points <count>] [--point-only]");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --source-quality-c3d <path> --entity-id <id> --unit <unit> --frame <frame> --report <json>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --aligned-point-repeatability-study <json> --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-plane-flatness --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-thickness --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-warpage --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-artifact-owned-roi-runner --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-edge --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-remove-outliers --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-level-surface --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-line-fit --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-point-pair-dimensions --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-gap-flush --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-volume --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-cross-section --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-map-fidelity --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-source-quality-report --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-invalid-cell-map --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-mesh-deviation --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-nominal-actual-comparison --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-model-foundation --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-model-surface-selection --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-model-key-points --report <path>");
        writer.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-surface-matching-foundation --report <path>");
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

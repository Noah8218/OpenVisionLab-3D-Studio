using System.Globalization;
using System.Numerics;
using System.Text.Json;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class RunnerApplication
{
    public static int Run(string[] args)
    {
        var lazProbePath = ReadOption(args, "--laz-probe");
        var stlStreamProbePath = ReadOption(args, "--stl-stream-probe");
        var meshDeviationParityPath = ReadOption(args, "--mesh-deviation-parity");
        var meshDeviationNominalPath = ReadOption(args, "--nominal-stl");
        var meshDeviationUnsignedPath = ReadOption(args, "--cloudcompare-unsigned");
        var meshDeviationSignedPath = ReadOption(args, "--cloudcompare-signed");
        var stanfordTransformPath = ReadOption(args, "--stanford-transform-parity");
        var stanfordTransformReferencePath = ReadOption(args, "--transform-reference");
        var stlStreamProbeUnit = ReadOption(args, "--unit");
        var c3DMapProbePath = ReadOption(args, "--c3d-map-probe");
        var c3DMapPlyPath = ReadOption(args, "--ply");
        var recipePath = ReadOption(args, "--recipe");
        var toolTeachingFilterPath = ReadOption(args, "--tool-teaching-filter");
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
        var verifyLibraryNoahThreeD = args.Contains("--verify-library-noah-3d", StringComparer.OrdinalIgnoreCase);
        var c3DMapPointOnly = args.Contains("--point-only", StringComparer.OrdinalIgnoreCase);

        if (toolTeachingFilterPath is not null)
        {
            if (toolTeachingStepId is null || outputC3DPath is null || reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --tool-teaching-filter <recipe> --tool-teaching-step <id> --output-c3d <path> --report <path>");
                return 2;
            }

            return ToolRecipeFilterRunnerExecution.Run(toolTeachingFilterPath, toolTeachingStepId, outputC3DPath, reportPath);
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

        if (verifyLibraryNoahThreeD)
        {
            if (reportPath is null)
            {
                Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --verify-library-noah-3d --report <path>");
                return 2;
            }

            return LibraryNoahThreeDPackageVerification.Run(reportPath);
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
            Console.Error.WriteLine("Usage: OpenVisionLab.ThreeD.Runner --recipe <path> --report <path> [--expect-status Pass|Fail|Warning|Error] [--compare-contract <path>] [--run-record <json> --html-report <html> --csv-report <csv> --viewer-screenshot <png>]");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --laz-probe <path> --report <path> [--max-sampled-points <count>]");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --stl-stream-probe <path> --unit <unit> --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --mesh-deviation-parity <measured.ply> --nominal-stl <nominal.stl> --cloudcompare-unsigned <unsigned.ply> --cloudcompare-signed <signed.ply> --unit <unit> --report <path> [--max-points <count>]");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --stanford-transform-parity <conf> --transform-reference <json> --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --c3d-map-probe <path> --ply <path> --report <path> [--max-sampled-points <count>] [--point-only]");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --aligned-point-repeatability-study <json> --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-plane-flatness --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-thickness --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-warpage --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-edge --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-line-fit --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-point-pair-dimensions --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-gap-flush --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-volume --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-cross-section --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-c3d-map-fidelity --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-mesh-deviation --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-nominal-actual-comparison --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-registration-acceptance --report <path>");
            Console.Error.WriteLine("   or: OpenVisionLab.ThreeD.Runner --verify-library-noah-3d --report <path>");
            return 2;
        }

        try
        {
            var fullRecipePath = Path.GetFullPath(recipePath);
            var recipeType = ReadRecipeType(fullRecipePath);
            if (recipeType.Equals(NominalActualComparisonRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return RunNominalActualComparisonRecipe(
                    fullRecipePath,
                    reportPath,
                    expectedStatus,
                    compareContractPath,
                    runArtifacts);
            }

            if (recipeType.Equals(LazTwoPointMeasurementRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return RunLazTwoPointRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath, runArtifacts);
            }

            if (recipeType.Equals(C3DThicknessRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return RunC3DThicknessRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath, runArtifacts);
            }

            if (recipeType.Equals(C3DWarpageRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return RunC3DWarpageRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath, runArtifacts);
            }

            if (recipeType.Equals(C3DPointPairDimensionsRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return RunC3DPointPairDimensionsRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath, runArtifacts);
            }

            if (recipeType.Equals(C3DGapFlushRecipe.SupportedRecipeType, StringComparison.OrdinalIgnoreCase))
            {
                return RunC3DGapFlushRecipe(fullRecipePath, reportPath, expectedStatus, compareContractPath, runArtifacts);
            }

            var recipe = HeightDeviationRecipe.Load(fullRecipePath);
            var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
            var maxSampledPoints = new[]
            {
            recipe.RoiStep?.MaxSampledPoints ?? 0,
            recipe.PlaneFlatness is { Enabled: true } planeFlatnessStep ? planeFlatnessStep.MaxSampledPoints : 0,
            recipe.Volume is { Enabled: true } volumeStep ? volumeStep.MaxSampledPoints : 0
        }.Max();
            var grid = C3DHeightGrid.Load(sourcePath, maxSampledPoints);
            var heightDeviationResult = HeightDeviationRule.Evaluate(new HeightDeviationRuleInput(
                recipe.Source.EntityId,
                recipe.Source.Name,
                grid.Min,
                grid.Max,
                grid.Mean,
                grid.ValidSampleCount,
                recipe.Rule.PeakTolerance,
                recipe.Source.Unit));
            var roiStepResult = recipe.RoiStep is null
                ? null
                : EvaluateRoiStep(recipe.RoiStep, recipe.Transform ?? ModelTransform.Identity, grid);
            var planeFlatnessResult = recipe.PlaneFlatness is { Enabled: true } planeFlatness
                ? EvaluatePlaneFlatness(planeFlatness, recipe.Transform ?? ModelTransform.Identity, grid)
                : null;
            var volumeResult = recipe.Volume is { Enabled: true } volume
                ? EvaluateVolume(volume, recipe.Transform ?? ModelTransform.Identity, grid)
                : null;
            var crossSectionResult = recipe.CrossSection is { Enabled: true } crossSection
                ? EvaluateCrossSection(crossSection, recipe.Transform ?? ModelTransform.Identity, grid)
                : null;
            var result = crossSectionResult?.Result ?? volumeResult?.Result ?? planeFlatnessResult?.Result ?? heightDeviationResult;
            var runStep = crossSectionResult is not null
                ? new InspectionRunStep(recipe.CrossSection!.Id, recipe.CrossSection.SourceEntityId, [recipe.CrossSection.ReferenceId], [])
                : volumeResult is not null
                    ? new InspectionRunStep(recipe.Volume!.Id, recipe.Volume.SourceEntityId, [recipe.Volume.ReferenceId], [recipe.Volume.MeasurementId])
                    : planeFlatnessResult is not null
                        ? new InspectionRunStep(recipe.PlaneFlatness!.Id, recipe.PlaneFlatness.SourceEntityId, [recipe.PlaneFlatness.ReferenceId], [])
                        : null;

            WriteReport(reportPath, fullRecipePath, sourcePath, recipe, grid, result, roiStepResult, planeFlatnessResult, volumeResult, crossSectionResult);
            if (compareContractPath is not null)
            {
                if (crossSectionResult is not null)
                    CompareUiContract(compareContractPath, result, "Section width", "Raw-height range");
                else if (volumeResult is not null)
                    CompareUiContract(compareContractPath, result, "Above-plane volume", "Below-plane volume", "Signed net volume");
                else
                    CompareUiContract(compareContractPath, result);
            }

            RunRecordWriter.Write(
                runArtifacts,
                fullRecipePath,
                recipe.RecipeType,
                recipe.Version,
                sourcePath,
                recipe.Source.EntityId,
                recipe.Source.Unit,
                runStep,
                result,
                reportPath,
                compareContractPath);

            if (expectedStatus is not null
                && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || result.Status != status))
            {
                Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {result.Status}.");
                return 3;
            }

            Console.WriteLine($"{result.ToolName}: {result.Status}");
            return result.Status == ResultStatus.Error ? 4 : 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static int RunNominalActualComparisonRecipe(
        string fullRecipePath,
        string reportPath,
        string? expectedStatus,
        string? compareContractPath,
        RunArtifactOptions runArtifacts)
    {
        var recipe = NominalActualComparisonRecipe.Load(fullRecipePath);
        var input = recipe.ToInput(fullRecipePath);
        var result = new NominalActualComparisonExecutor()
            .ExecuteAsync(input, maximumDisplaySamples: 0)
            .GetAwaiter()
            .GetResult();
        var toolResult = NominalActualComparisonContract.CreateToolResult(result);
        var comparisonState = "NotCompared";
        try
        {
            if (compareContractPath is not null)
            {
                CompareNominalActualUiContract(compareContractPath, result);
                comparisonState = "Matched";
            }
        }
        catch (InvalidDataException exception)
        {
            WriteNominalActualReport(
                reportPath,
                fullRecipePath,
                recipe,
                result,
                $"Mismatch: {exception.Message}");
            throw;
        }

        WriteNominalActualReport(
            reportPath,
            fullRecipePath,
            recipe,
            result,
            comparisonState);
        var runStep = new InspectionRunStep(
            input.StepId,
            input.ActualSource.Id,
            [input.NominalSource.Id],
            [input.QuerySource.Id]);
        RunRecordWriter.Write(
            runArtifacts,
            fullRecipePath,
            recipe.RecipeType,
            recipe.Version,
            input.ActualSource.Path,
            input.ActualSource.Id,
            input.Unit,
            runStep,
            toolResult,
            reportPath,
            compareContractPath);

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status)
                || result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {result.Status}.");
            return 3;
        }

        Console.WriteLine($"{toolResult.ToolName}: {toolResult.Status} | Viewer/Runner {comparisonState}");
        return toolResult.Status == ResultStatus.Error ? 4 : 0;
    }

    static int RunC3DThicknessRecipe(
        string fullRecipePath,
        string reportPath,
        string? expectedStatus,
        string? compareContractPath,
        RunArtifactOptions runArtifacts)
    {
        var recipe = C3DThicknessRecipe.Load(fullRecipePath);
        var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0);
        var evaluation = C3DThicknessRule.Evaluate(new C3DThicknessInput(
            recipe.Step.SourceEntityId,
            grid.Height,
            grid.Width,
            grid.ReadHeightMapValues(),
            recipe.Step.Roi,
            recipe.Step.Acceptance,
            recipe.Step.Unit,
            recipe.Step.FrameId,
            recipe.Step.MinimumValidSamples));

        WriteC3DThicknessReport(reportPath, fullRecipePath, sourcePath, recipe, grid, evaluation);
        if (compareContractPath is not null)
        {
            CompareUiContract(compareContractPath, evaluation.Result, "Mean", "Minimum", "Maximum", "Range");
        }

        var runStep = new InspectionRunStep(
            recipe.Step.Id,
            recipe.Step.SourceEntityId,
            [recipe.Step.RoiReferenceId],
            []);
        RunRecordWriter.Write(runArtifacts, fullRecipePath, recipe.RecipeType, recipe.Version, sourcePath,
            recipe.Source.EntityId, recipe.Source.Unit, runStep, evaluation.Result, reportPath, compareContractPath);

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || evaluation.Result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {evaluation.Result.Status}.");
            return 3;
        }

        Console.WriteLine($"{evaluation.Result.ToolName}: {evaluation.Result.Status}");
        return evaluation.Result.Status == ResultStatus.Error ? 4 : 0;
    }

    static int RunC3DWarpageRecipe(
        string fullRecipePath,
        string reportPath,
        string? expectedStatus,
        string? compareContractPath,
        RunArtifactOptions runArtifacts)
    {
        var recipe = C3DWarpageRecipe.Load(fullRecipePath);
        var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0);
        var evaluation = C3DWarpageRule.Evaluate(new C3DWarpageInput(
            recipe.Step.SourceEntityId,
            grid.Height,
            grid.Width,
            grid.ReadHeightMapValues(),
            recipe.Step.Roi,
            recipe.Step.Acceptance,
            recipe.Step.Unit,
            recipe.Step.FrameId,
            recipe.Step.MinimumValidSamples));

        WriteC3DWarpageReport(reportPath, fullRecipePath, sourcePath, recipe, grid, evaluation);
        if (compareContractPath is not null)
        {
            CompareUiContract(compareContractPath, evaluation.Result, "PeakToValley", "Rms", "MinimumResidual", "MaximumResidual");
        }

        var runStep = new InspectionRunStep(
            recipe.Step.Id,
            recipe.Step.SourceEntityId,
            [recipe.Step.ReferenceId],
            []);
        RunRecordWriter.Write(runArtifacts, fullRecipePath, recipe.RecipeType, recipe.Version, sourcePath,
            recipe.Source.EntityId, recipe.Source.Unit, runStep, evaluation.Result, reportPath, compareContractPath);

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || evaluation.Result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {evaluation.Result.Status}.");
            return 3;
        }

        Console.WriteLine($"{evaluation.Result.ToolName}: {evaluation.Result.Status}");
        return evaluation.Result.Status == ResultStatus.Error ? 4 : 0;
    }

    static int RunC3DPointPairDimensionsRecipe(
        string fullRecipePath,
        string reportPath,
        string? expectedStatus,
        string? compareContractPath,
        RunArtifactOptions runArtifacts)
    {
        var recipe = C3DPointPairDimensionsRecipe.Load(fullRecipePath);
        var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
        var grid = C3DHeightGrid.Load(sourcePath, maxRenderedPoints: 0);
        var first = grid.ReadPoint(recipe.Step.First.Row, recipe.Step.First.Column);
        var second = grid.ReadPoint(recipe.Step.Second.Row, recipe.Step.Second.Column);
        var transform = recipe.Transform ?? ModelTransform.Identity;
        var evaluation = PointPairDimensionsRule.Evaluate(new PointPairDimensionsInput(
            recipe.Step.SourceEntityId,
            ApplyModelTransform(first.Position, transform),
            ApplyModelTransform(second.Position, transform),
            first.RawValue,
            second.RawValue,
            recipe.Step.Acceptance,
            recipe.Step.Unit,
            recipe.Source.Unit));

        WritePointPairDimensionsReport(reportPath, fullRecipePath, sourcePath, recipe, grid, first, second, evaluation);
        if (compareContractPath is not null)
        {
            CompareUiContract(
                compareContractPath,
                evaluation.Result,
                "3D distance",
                "XZ planar width",
                "Elevation angle");
        }

        var runStep = new InspectionRunStep(
            recipe.Step.Id,
            recipe.Step.SourceEntityId,
            [recipe.Step.First.Id, recipe.Step.Second.Id],
            []);
        RunRecordWriter.Write(runArtifacts, fullRecipePath, recipe.RecipeType, recipe.Version, sourcePath,
            recipe.Source.EntityId, recipe.Source.Unit, runStep, evaluation.Result, reportPath, compareContractPath);

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || evaluation.Result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {evaluation.Result.Status}.");
            return 3;
        }

        Console.WriteLine($"{evaluation.Result.ToolName}: {evaluation.Result.Status}");
        return evaluation.Result.Status == ResultStatus.Error ? 4 : 0;
    }

    static int RunC3DGapFlushRecipe(
        string fullRecipePath,
        string reportPath,
        string? expectedStatus,
        string? compareContractPath,
        RunArtifactOptions runArtifacts)
    {
        var recipe = C3DGapFlushRecipe.Load(fullRecipePath);
        var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
        var grid = C3DHeightGrid.Load(sourcePath, recipe.Step.MaxSampledPoints);
        var transform = recipe.Transform ?? ModelTransform.Identity;
        TryCalculateRoiStats(grid.Points, recipe.Step.LeftRegion, transform, out var left);
        TryCalculateRoiStats(grid.Points, recipe.Step.RightRegion, transform, out var right);
        var evaluation = GapFlushRule.Evaluate(new GapFlushInput(
            recipe.Step.SourceEntityId,
            recipe.Step.LeftRegion,
            recipe.Step.RightRegion,
            new GapFlushRegionStats(left.Count, left.RawMean, left.ModelYMean),
            new GapFlushRegionStats(right.Count, right.RawMean, right.ModelYMean),
            recipe.Step.Acceptance,
            recipe.Step.GapUnit,
            recipe.Step.FlushUnit));

        WriteGapFlushReport(reportPath, fullRecipePath, sourcePath, recipe, grid, evaluation);
        if (compareContractPath is not null)
        {
            CompareUiContract(compareContractPath, evaluation.Result, "Signed gap", "Signed flush");
        }

        var runStep = new InspectionRunStep(
            recipe.Step.Id,
            recipe.Step.SourceEntityId,
            [recipe.Step.LeftReferenceId, recipe.Step.RightReferenceId],
            []);
        RunRecordWriter.Write(runArtifacts, fullRecipePath, recipe.RecipeType, recipe.Version, sourcePath,
            recipe.Source.EntityId, recipe.Source.Unit, runStep, evaluation.Result, reportPath, compareContractPath);

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || evaluation.Result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {evaluation.Result.Status}.");
            return 3;
        }

        Console.WriteLine($"{evaluation.Result.ToolName}: {evaluation.Result.Status}");
        return evaluation.Result.Status == ResultStatus.Error ? 4 : 0;
    }

    static int RunLazTwoPointRecipe(
        string fullRecipePath,
        string reportPath,
        string? expectedStatus,
        string? compareContractPath,
        RunArtifactOptions runArtifacts)
    {
        var recipe = LazTwoPointMeasurementRecipe.Load(fullRecipePath);
        var sourcePath = ResolveRecipePath(recipe.Source.Path, Path.GetDirectoryName(fullRecipePath)!);
        var pointCloud = LazPointCloud.Load(sourcePath, recipe.Measurement.MaxSampledPoints);
        if (pointCloud.SampledPoints.Length < 2)
        {
            throw new InvalidDataException("LAZ/LAS two-point recipe requires at least two sampled points.");
        }

        var first = pointCloud.SampledPoints.MinBy(point => MapLazPosition(point.Position).X);
        var second = pointCloud.SampledPoints.MaxBy(point => MapLazPosition(point.Position).X);
        var result = CreateLazTwoPointResult(first, second, recipe.Measurement.HeightUnit, recipe.Source.EntityId, recipe.Acceptance);

        WriteLazTwoPointReport(reportPath, fullRecipePath, sourcePath, recipe, pointCloud, first, second, result);
        if (compareContractPath is not null)
        {
            CompareUiContract(compareContractPath, result);
        }

        RunRecordWriter.Write(runArtifacts, fullRecipePath, recipe.RecipeType, recipe.Version, sourcePath,
            recipe.Source.EntityId, recipe.Source.Unit, null, result, reportPath, compareContractPath);

        if (expectedStatus is not null
            && (!Enum.TryParse<ResultStatus>(expectedStatus, true, out var status) || result.Status != status))
        {
            Console.Error.WriteLine($"Expected status {expectedStatus}, actual status {result.Status}.");
            return 3;
        }

        Console.WriteLine($"{result.ToolName}: {result.Status}");
        return result.Status == ResultStatus.Error ? 4 : 0;
    }

    static int RunLazProbe(string lazPath, string reportPath, int maxSampledPoints)
    {
        try
        {
            var pointCloud = LazPointCloud.Load(lazPath, maxSampledPoints);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, [
                "LazPointCloudProbe",
            pointCloud.FormatContractLine(),
            $"Metadata|version={pointCloud.Metadata.Version}|rawPointFormat={pointCloud.Metadata.RawPointDataFormat}|recordLength={pointCloud.Metadata.PointDataRecordLength}|laszipVlr={pointCloud.Metadata.HasLaszipVlr}|pointOffset={pointCloud.Metadata.PointDataOffset}",
            $"Sample|first={(pointCloud.SampledPoints.Length == 0 ? "(none)" : FormatLazPoint(pointCloud.SampledPoints[0]))}"
            ]);
            Console.WriteLine(pointCloud.FormatContractLine());
            return pointCloud.BoundsMatch && pointCloud.HasRgb && pointCloud.SampledPoints.Length > 0 ? 0 : 5;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static int RunStlStreamProbe(string stlPath, string unit, string reportPath)
    {
        try
        {
            ValidateReportUnit(unit);

            var summary = BinaryStlInspectionReader.Scan(stlPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
            File.WriteAllLines(reportPath, [
                "BinaryStlInspectionProbe|Pass",
            $"Source|path={summary.SourcePath}|bytes={summary.SourceByteLength}|sha256={summary.SourceSha256}",
            $"Geometry|declaredTriangles={summary.DeclaredTriangleCount}|processedTriangles={summary.ProcessedTriangleCount}|expandedVertices={summary.ExpandedVertexCount}",
            $"Bounds|minimum={FormatStlVector(summary.BoundsMinimum)}|maximum={FormatStlVector(summary.BoundsMaximum)}|unit={unit}",
            "Frame|transform=identity|coordinates=source-stl|alignment=none",
            "Sampling|mode=none|renderDensity=independent|sourceTriangleOrder=preserved"
            ]);
            Console.WriteLine(
                $"Binary STL inspection probe: Pass ({summary.ProcessedTriangleCount:N0} triangles, {summary.SourceByteLength:N0} bytes)");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static int RunMeshDeviationParity(
        string nominalStlPath,
        string measuredPlyPath,
        string cloudCompareUnsignedPlyPath,
        string cloudCompareSignedPlyPath,
        string unit,
        string reportPath,
        int? maxPoints)
    {
        try
        {
            ValidateReportUnit(unit);
            return MeshDeviationParityVerification.Run(
                nominalStlPath,
                measuredPlyPath,
                cloudCompareUnsignedPlyPath,
                cloudCompareSignedPlyPath,
                unit,
                reportPath,
                maxPoints);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    static void ValidateReportUnit(string unit)
    {
        if (unit.Length > 64 || unit.IndexOfAny(['|', '\r', '\n']) >= 0)
        {
            throw new InvalidDataException("--unit must be a single report-safe value of at most 64 characters.");
        }
    }

    static string? ReadOption(IReadOnlyList<string> args, string name)
    {
        for (var i = 0; i < args.Count - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    static int? ReadIntOption(IReadOnlyList<string> args, string name)
    {
        var value = ReadOption(args, name);
        if (value is null)
        {
            return null;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : throw new InvalidDataException($"{name} must be an integer.");
    }

    static string ReadRecipeType(string path)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.TryGetProperty("recipeType", out var recipeType)
            ? recipeType.GetString() ?? throw new InvalidDataException($"Recipe type is empty: {path}")
            : throw new InvalidDataException($"Recipe type is missing: {path}");
    }

    static string ResolveRecipePath(string path, string recipeDirectory)
    {
        return Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(recipeDirectory, path));
    }

    static void WriteReport(
        string reportPath,
        string recipePath,
        string sourcePath,
        HeightDeviationRecipe recipe,
        C3DHeightGrid grid,
        ToolResult result,
        RoiStepReport? roiStepResult,
        PlaneFlatnessEvaluation? planeFlatnessResult,
        VolumeEvaluation? volumeResult,
        CrossSectionEvaluation? crossSectionResult)
    {
        var transform = recipe.Transform ?? ModelTransform.Identity;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={grid.Min.ToString("F3", CultureInfo.InvariantCulture)}|max={grid.Max.ToString("F3", CultureInfo.InvariantCulture)}|mean={grid.Mean.ToString("F3", CultureInfo.InvariantCulture)}",
        InspectionContractText.FormatToolResult(result, includePrefix: true),
        "RecipeTransform",
        $"Transform|configured={recipe.Transform is not null}|tx={FormatNumber(transform.TranslateX)}|ty={FormatNumber(transform.TranslateY)}|tz={FormatNumber(transform.TranslateZ)}|rx={FormatNumber(transform.RotateXDegrees)}|ry={FormatNumber(transform.RotateYDegrees)}|rz={FormatNumber(transform.RotateZDegrees)}|scale={FormatNumber(transform.Scale)}",
        "RecipeRoiStep",
        recipe.RoiStep is null
            ? "RoiStep|configured=False"
            : $"RoiStep|configured=True|mode={recipe.RoiStep.Mode}|maxSampledPoints={recipe.RoiStep.MaxSampledPoints}|left={FormatRegion(recipe.RoiStep.Left)}|right={FormatRegion(recipe.RoiStep.Right)}",
    };

        if (recipe.PlaneFlatness is { } planeFlatness)
        {
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                planeFlatness.Id,
                PlaneFlatnessRule.ToolName,
                planeFlatness.SourceEntityId,
                planeFlatness.ReferenceId,
                planeFlatness.Enabled)));
            lines.Add($"PlaneFlatnessStep|configured=True|id={planeFlatness.Id}|source={planeFlatness.SourceEntityId}|reference={planeFlatness.ReferenceId}|enabled={planeFlatness.Enabled}|roi={FormatRegion(planeFlatness.ReferenceRegion)}|tolerance={FormatNumber(planeFlatness.Tolerance)}|unit={planeFlatness.Unit}|maxSampledPoints={planeFlatness.MaxSampledPoints}");
        }
        else
        {
            lines.Add("PlaneFlatnessStep|configured=False");
        }

        if (recipe.Volume is { } volume)
        {
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                volume.Id,
                VolumeRule.ToolName,
                volume.SourceEntityId,
                $"{volume.ReferenceId},{volume.MeasurementId}",
                volume.Enabled)));
            lines.Add($"VolumeStep|configured=True|id={volume.Id}|source={volume.SourceEntityId}|reference={volume.ReferenceId}|measurement={volume.MeasurementId}|referenceRegion={FormatRegion(volume.ReferenceRegion)}|measurementRegion={FormatRegion(volume.MeasurementRegion)}|expectedNet={FormatNumber(volume.ExpectedNetVolume)}|tolerance={FormatNumber(volume.Tolerance)}|unit={volume.Unit}|maxSampledPoints={volume.MaxSampledPoints}|enabled={volume.Enabled}");
        }
        else
        {
            lines.Add("VolumeStep|configured=False");
        }

        if (recipe.CrossSection is { } crossSection)
        {
            lines.Add(InspectionContractText.FormatInspectionStep(new InspectionStep(
                crossSection.Id,
                CrossSectionDimensionsRule.ToolName,
                crossSection.SourceEntityId,
                crossSection.ReferenceId,
                crossSection.Enabled)));
            lines.Add($"CrossSectionStep|configured=True|id={crossSection.Id}|source={crossSection.SourceEntityId}|reference={crossSection.ReferenceId}|row={crossSection.Row}|startColumn={crossSection.StartColumn}|endColumn={crossSection.EndColumn}|expectedWidth={FormatNumber(crossSection.ExpectedWidth)}|widthTolerance={FormatNumber(crossSection.WidthTolerance)}|expectedHeightRange={FormatNumber(crossSection.ExpectedHeightRange)}|heightTolerance={FormatNumber(crossSection.HeightTolerance)}|widthUnit={crossSection.WidthUnit}|heightUnit={crossSection.HeightUnit}|enabled={crossSection.Enabled}");
        }
        else
        {
            lines.Add("CrossSectionStep|configured=False");
        }

        lines.Add(InspectionContractText.MetricsMarker);

        lines.AddRange(result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.OverlaysMarker);
        lines.AddRange(result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
        if (roiStepResult is not null)
        {
            lines.Add("RoiStepResult");
            lines.Add(
                $"RoiStepResult|leftCount={roiStepResult.LeftCount}|rightCount={roiStepResult.RightCount}|leftMeanRaw={FormatNumber(roiStepResult.LeftRawMean)}|rightMeanRaw={FormatNumber(roiStepResult.RightRawMean)}|heightDeltaRaw={FormatNumber(roiStepResult.RawHeightDelta)}|modelDeltaY={FormatNumber(roiStepResult.ModelHeightDelta)}");
        }

        if (planeFlatnessResult is not null)
        {
            lines.Add($"PlaneFlatness|status={planeFlatnessResult.Result.Status}|referenceSamples={planeFlatnessResult.ReferenceSampleCount}|measurementSamples={planeFlatnessResult.MeasurementSampleCount}|minimum={FormatNumber(planeFlatnessResult.MinimumSignedDistance)}|maximum={FormatNumber(planeFlatnessResult.MaximumSignedDistance)}|flatness={FormatNumber(planeFlatnessResult.Flatness)}|rms={FormatNumber(planeFlatnessResult.RootMeanSquareDistance)}|summary={InspectionContractText.Clean(planeFlatnessResult.Result.Message)}");
        }

        if (volumeResult is not null)
        {
            lines.Add($"Volume|status={volumeResult.Result.Status}|above={FormatNumber(volumeResult.AboveVolume)}|below={FormatNumber(volumeResult.BelowVolume)}|net={FormatNumber(volumeResult.NetVolume)}|referenceSamples={volumeResult.ReferenceSampleCount}|measurementSamples={volumeResult.MeasurementSampleCount}|summary={InspectionContractText.Clean(volumeResult.Result.Message)}");
        }

        if (crossSectionResult is not null)
        {
            lines.Add($"CrossSection|status={crossSectionResult.Result.Status}|width={FormatNumber(crossSectionResult.Width)}|heightRange={FormatNumber(crossSectionResult.HeightRange)}|rawMinimum={FormatNumber(crossSectionResult.RawMinimum)}|rawMaximum={FormatNumber(crossSectionResult.RawMaximum)}|validSamples={crossSectionResult.ValidSampleCount}|summary={InspectionContractText.Clean(crossSectionResult.Result.Message)}");
        }

        File.WriteAllLines(reportPath, lines);
    }

    static void WriteLazTwoPointReport(
        string reportPath,
        string recipePath,
        string sourcePath,
        LazTwoPointMeasurementRecipe recipe,
        LazPointCloud pointCloud,
        LazPointCloudPoint first,
        LazPointCloudPoint second,
        ToolResult result)
    {
        var firstPosition = MapLazPosition(first.Position);
        var secondPosition = MapLazPosition(second.Position);
        var delta = secondPosition - firstPosition;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        pointCloud.FormatContractLine(),
        InspectionContractText.FormatToolResult(result, includePrefix: true),
        $"MeasurementSelection|selection={recipe.Measurement.Selection}|maxSampledPoints={recipe.Measurement.MaxSampledPoints}|heightUnit={recipe.Measurement.HeightUnit}",
        $"Acceptance|expectedDistance={FormatNumber(recipe.Acceptance.ExpectedDistance)}|distanceTolerance={FormatNumber(recipe.Acceptance.DistanceTolerance)}|expectedHeightDelta={FormatNumber(recipe.Acceptance.ExpectedHeightDelta)}|heightDeltaTolerance={FormatNumber(recipe.Acceptance.HeightDeltaTolerance)}",
        $"TwoPointResult|distance={FormatNumber(delta.Length())}|dx={FormatNumber(delta.X)}|dy={FormatNumber(delta.Y)}|dz={FormatNumber(delta.Z)}|heightDeltaRaw={FormatNumber(secondPosition.Y - firstPosition.Y)}|first={FormatLazPoint(first)}|second={FormatLazPoint(second)}",
        InspectionContractText.MetricsMarker
    };

        lines.AddRange(result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.OverlaysMarker);
        lines.AddRange(result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));

        File.WriteAllLines(reportPath, lines);
    }

    static void WriteC3DThicknessReport(
        string reportPath,
        string recipePath,
        string sourcePath,
        C3DThicknessRecipe recipe,
        C3DHeightGrid grid,
        C3DThicknessEvaluation evaluation)
    {
        var step = recipe.Step;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={FormatNumber(grid.Min)}|max={FormatNumber(grid.Max)}|mean={FormatNumber(grid.Mean)}",
        InspectionContractText.FormatInspectionStep(new InspectionStep(
            step.Id,
            C3DThicknessRule.ToolName,
            step.SourceEntityId,
            step.RoiReferenceId,
            step.Enabled)),
        $"C3DThicknessStep|configured=True|id={step.Id}|source={step.SourceEntityId}|reference={step.RoiReferenceId}|row={step.Roi.Row}|column={step.Roi.Column}|rowCount={step.Roi.RowCount}|columnCount={step.Roi.ColumnCount}|minimum={FormatNumber(step.Acceptance.MinimumThickness)}|maximum={FormatNumber(step.Acceptance.MaximumThickness)}|minimumValidSamples={step.MinimumValidSamples}|unit={step.Unit}|frame={step.FrameId}|enabled={step.Enabled}",
        InspectionContractText.FormatToolResult(evaluation.Result, includePrefix: true),
        $"C3DThickness|status={evaluation.Result.Status}|hasMeasurement={evaluation.HasMeasurement}|packageStatus={evaluation.PackageResultStatus}|packageError={evaluation.PackageErrorCode}|mean={FormatNumber(evaluation.Mean)}|minimum={FormatNumber(evaluation.Minimum)}|maximum={FormatNumber(evaluation.Maximum)}|range={FormatNumber(evaluation.Range)}|validSamples={evaluation.ValidSampleCount}|below={evaluation.BelowLowerLimitCount}|above={evaluation.AboveUpperLimitCount}",
        InspectionContractText.MetricsMarker
    };
        lines.AddRange(evaluation.Result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.OverlaysMarker);
        lines.AddRange(evaluation.Result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
        File.WriteAllLines(reportPath, lines);
    }

    static void WriteC3DWarpageReport(
        string reportPath,
        string recipePath,
        string sourcePath,
        C3DWarpageRecipe recipe,
        C3DHeightGrid grid,
        C3DWarpageEvaluation evaluation)
    {
        var step = recipe.Step;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var lines = new List<string>
        {
            $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
            $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
            $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={FormatNumber(grid.Min)}|max={FormatNumber(grid.Max)}|mean={FormatNumber(grid.Mean)}",
            InspectionContractText.FormatInspectionStep(new InspectionStep(
                step.Id,
                C3DWarpageRule.ToolName,
                step.SourceEntityId,
                step.ReferenceId,
                step.Enabled)),
            $"C3DWarpageStep|configured=True|id={step.Id}|source={step.SourceEntityId}|reference={step.ReferenceId}|referenceMode={step.ReferenceMode}|row={step.Roi.Row}|column={step.Roi.Column}|rowCount={step.Roi.RowCount}|columnCount={step.Roi.ColumnCount}|maximumPeakToValley={FormatNumber(step.Acceptance.MaximumPeakToValley)}|maximumRms={FormatNumber(step.Acceptance.MaximumRms ?? double.NaN)}|minimumValidSamples={step.MinimumValidSamples}|unit={step.Unit}|frame={step.FrameId}|enabled={step.Enabled}",
            InspectionContractText.FormatToolResult(evaluation.Result, includePrefix: true),
            $"C3DWarpage|status={evaluation.Result.Status}|hasMeasurement={evaluation.HasMeasurement}|packageStatus={evaluation.PackageResultStatus}|packageError={evaluation.PackageErrorCode}|peakToValley={FormatNumber(evaluation.PeakToValley)}|rms={FormatNumber(evaluation.Rms)}|minimumResidual={FormatNumber(evaluation.MinimumResidual)}|maximumResidual={FormatNumber(evaluation.MaximumResidual)}|planeSlopeX={FormatNumber(evaluation.PlaneSlopeX)}|planeSlopeY={FormatNumber(evaluation.PlaneSlopeY)}|planeIntercept={FormatNumber(evaluation.PlaneIntercept)}|validSamples={evaluation.ValidSampleCount}",
            InspectionContractText.MetricsMarker
        };
        lines.AddRange(evaluation.Result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.OverlaysMarker);
        lines.AddRange(evaluation.Result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
        File.WriteAllLines(reportPath, lines);
    }

    static void WritePointPairDimensionsReport(
        string reportPath,
        string recipePath,
        string sourcePath,
        C3DPointPairDimensionsRecipe recipe,
        C3DHeightGrid grid,
        HeightGridPoint first,
        HeightGridPoint second,
        PointPairDimensionsEvaluation evaluation)
    {
        var transform = recipe.Transform ?? ModelTransform.Identity;
        var step = recipe.Step;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={FormatNumber(grid.Min)}|max={FormatNumber(grid.Max)}|mean={FormatNumber(grid.Mean)}",
        "RecipeTransform",
        $"Transform|configured={recipe.Transform is not null}|tx={FormatNumber(transform.TranslateX)}|ty={FormatNumber(transform.TranslateY)}|tz={FormatNumber(transform.TranslateZ)}|rx={FormatNumber(transform.RotateXDegrees)}|ry={FormatNumber(transform.RotateYDegrees)}|rz={FormatNumber(transform.RotateZDegrees)}|scale={FormatNumber(transform.Scale)}",
        InspectionContractText.FormatInspectionStep(new InspectionStep(
            step.Id,
            PointPairDimensionsRule.ToolName,
            step.SourceEntityId,
            $"{step.First.Id},{step.Second.Id}",
            step.Enabled)),
        $"PointPairDimensionsStep|configured=True|id={step.Id}|source={step.SourceEntityId}|first={step.First.Id}@({step.First.Row},{step.First.Column})|second={step.Second.Id}@({step.Second.Row},{step.Second.Column})|enabled={step.Enabled}|expectedDistance={FormatNumber(step.Acceptance.ExpectedDistance)}|distanceTolerance={FormatNumber(step.Acceptance.DistanceTolerance)}|expectedWidth={FormatNumber(step.Acceptance.ExpectedWidth)}|widthTolerance={FormatNumber(step.Acceptance.WidthTolerance)}|expectedAngle={FormatNumber(step.Acceptance.ExpectedElevationAngleDegrees)}|angleTolerance={FormatNumber(step.Acceptance.ElevationAngleToleranceDegrees)}|unit={step.Unit}",
        InspectionContractText.FormatToolResult(evaluation.Result, includePrefix: true),
        $"PointPairDimensions|status={evaluation.Result.Status}|distance={FormatNumber(evaluation.Distance)}|width={FormatNumber(evaluation.PlanarWidth)}|angleDegrees={FormatNumber(evaluation.ElevationAngleDegrees)}|rawHeightDelta={FormatNumber(evaluation.RawHeightDelta)}|firstRaw={FormatNumber(first.RawValue)}|secondRaw={FormatNumber(second.RawValue)}",
        InspectionContractText.MetricsMarker
    };

        lines.AddRange(evaluation.Result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.OverlaysMarker);
        lines.AddRange(evaluation.Result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
        File.WriteAllLines(reportPath, lines);
    }

    static void WriteGapFlushReport(
        string reportPath,
        string recipePath,
        string sourcePath,
        C3DGapFlushRecipe recipe,
        C3DHeightGrid grid,
        GapFlushEvaluation evaluation)
    {
        var step = recipe.Step;
        var transform = recipe.Transform ?? ModelTransform.Identity;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        var lines = new List<string>
    {
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"Source|{recipe.Source.EntityId}|name={recipe.Source.Name}|path={sourcePath}|unit={recipe.Source.Unit}",
        $"HeightGrid|width={grid.Width}|height={grid.Height}|valid={grid.ValidSampleCount}|zero={grid.ZeroSampleCount}|min={FormatNumber(grid.Min)}|max={FormatNumber(grid.Max)}|mean={FormatNumber(grid.Mean)}",
        "RecipeTransform",
        $"Transform|configured={recipe.Transform is not null}|tx={FormatNumber(transform.TranslateX)}|ty={FormatNumber(transform.TranslateY)}|tz={FormatNumber(transform.TranslateZ)}|rx={FormatNumber(transform.RotateXDegrees)}|ry={FormatNumber(transform.RotateYDegrees)}|rz={FormatNumber(transform.RotateZDegrees)}|scale={FormatNumber(transform.Scale)}",
        InspectionContractText.FormatInspectionStep(new InspectionStep(
            step.Id,
            GapFlushRule.ToolName,
            step.SourceEntityId,
            $"{step.LeftReferenceId},{step.RightReferenceId}",
            step.Enabled)),
        $"GapFlushStep|configured=True|id={step.Id}|source={step.SourceEntityId}|leftReference={step.LeftReferenceId}|rightReference={step.RightReferenceId}|left={FormatRegion(step.LeftRegion)}|right={FormatRegion(step.RightRegion)}|expectedGap={FormatNumber(step.Acceptance.ExpectedGap)}|gapTolerance={FormatNumber(step.Acceptance.GapTolerance)}|expectedFlush={FormatNumber(step.Acceptance.ExpectedFlush)}|flushTolerance={FormatNumber(step.Acceptance.FlushTolerance)}|gapUnit={step.GapUnit}|flushUnit={step.FlushUnit}|maxSampledPoints={step.MaxSampledPoints}|enabled={step.Enabled}",
        InspectionContractText.FormatToolResult(evaluation.Result, includePrefix: true),
        $"GapFlush|status={evaluation.Result.Status}|gap={FormatNumber(evaluation.SignedGap)}|flush={FormatNumber(evaluation.SignedFlush)}|modelFlush={FormatNumber(evaluation.ModelFlush)}|leftCount={evaluation.LeftPointCount}|rightCount={evaluation.RightPointCount}",
        InspectionContractText.MetricsMarker
    };
        lines.AddRange(evaluation.Result.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.OverlaysMarker);
        lines.AddRange(evaluation.Result.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));
        File.WriteAllLines(reportPath, lines);
    }

    static ToolResult CreateLazTwoPointResult(
        LazPointCloudPoint first,
        LazPointCloudPoint second,
        string heightUnit,
        string sourceEntityId,
        LazTwoPointMeasurementRecipeAcceptance acceptance)
    {
        var firstPosition = MapLazPosition(first.Position);
        var secondPosition = MapLazPosition(second.Position);
        var delta = secondPosition - firstPosition;
        var distance = delta.Length();
        var heightDelta = secondPosition.Y - firstPosition.Y;
        var distanceStatus = IsWithinTolerance(distance, acceptance.ExpectedDistance, acceptance.DistanceTolerance)
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var heightStatus = IsWithinTolerance(heightDelta, acceptance.ExpectedHeightDelta, acceptance.HeightDeltaTolerance)
            ? ResultStatus.Pass
            : ResultStatus.Fail;
        var status = distanceStatus == ResultStatus.Pass && heightStatus == ResultStatus.Pass
            ? ResultStatus.Pass
            : ResultStatus.Fail;

        return new ToolResult(
            "LAZ/LAS Two Point Measurement",
            status,
            status == ResultStatus.Pass
                ? "Runner replay within configured tolerance; source point cloud is unchanged."
                : "Runner replay exceeds configured tolerance; source point cloud is unchanged.",
            TimeSpan.Zero,
            [
                new Metric("Distance", MetricKind.Length, distance, "model", distanceStatus),
            new Metric("Delta X", MetricKind.Length, delta.X, "model", ResultStatus.Pass),
            new Metric("Delta Y", MetricKind.Length, delta.Y, "model", ResultStatus.Pass),
            new Metric("Delta Z", MetricKind.Length, delta.Z, "model", ResultStatus.Pass),
            new Metric("Source Z height delta", MetricKind.Length, heightDelta, heightUnit, heightStatus)
            ],
            [
                new Overlay("overlay.laz-two-point-line", OverlayKind.Polyline, "LAZ/LAS two-point distance line", distanceStatus, sourceEntityId),
            new Overlay("overlay.laz-two-point-height-marker", OverlayKind.Marker, "LAZ/LAS source-Z height delta marker", heightStatus, sourceEntityId)
            ]);
    }

    static PlaneFlatnessEvaluation EvaluatePlaneFlatness(
        HeightDeviationRecipePlaneFlatness step,
        ModelTransform transform,
        C3DHeightGrid grid)
    {
        var measurementSamples = grid.Points
            .Select(point => new HeightFieldPlaneSample(ApplyModelTransform(point.Position, transform), point.RawValue))
            .ToArray();
        var referenceSamples = measurementSamples
            .Where(sample => Contains(step.ReferenceRegion, sample.Position))
            .ToArray();
        return PlaneFlatnessRule.Evaluate(new PlaneFlatnessRuleInput(
            step.SourceEntityId,
            referenceSamples,
            measurementSamples,
            step.Tolerance,
            step.Unit));
    }

    static VolumeEvaluation EvaluateVolume(
        HeightDeviationRecipeVolume step,
        ModelTransform transform,
        C3DHeightGrid grid)
    {
        var samples = grid.Points
            .Select(point => new HeightFieldPlaneSample(ApplyModelTransform(point.Position, transform), point.RawValue))
            .ToArray();
        var referenceSamples = samples.Where(sample => Contains(step.ReferenceRegion, sample.Position)).ToArray();
        var measurementSamples = samples.Where(sample => Contains(step.MeasurementRegion, sample.Position)).ToArray();
        var spacing = grid.HorizontalScale * grid.PointStride * transform.Scale;
        return VolumeRule.Evaluate(new VolumeRuleInput(
            step.SourceEntityId,
            referenceSamples,
            measurementSamples,
            spacing * spacing,
            step.ExpectedNetVolume,
            step.Tolerance,
            step.Unit));
    }

    static CrossSectionEvaluation EvaluateCrossSection(
        HeightDeviationRecipeCrossSection step,
        ModelTransform transform,
        C3DHeightGrid grid)
    {
        var samples = grid.ReadRowRange(step.Row, step.StartColumn, step.EndColumn)
            .Select(point => new CrossSectionSample(point.Column, ApplyModelTransform(point.Position, transform), point.RawValue))
            .ToArray();
        return CrossSectionDimensionsRule.Evaluate(new CrossSectionDimensionsInput(
            step.SourceEntityId,
            step.Row,
            step.StartColumn,
            step.EndColumn,
            samples,
            step.ExpectedWidth,
            step.WidthTolerance,
            step.ExpectedHeightRange,
            step.HeightTolerance,
            step.WidthUnit,
            step.HeightUnit));
    }

    static bool Contains(HeightDeviationRecipeRoiRegion region, Vector3 point) =>
        point.X >= region.CenterX - region.HalfWidth
        && point.X <= region.CenterX + region.HalfWidth
        && point.Z >= region.CenterZ - region.HalfDepth
        && point.Z <= region.CenterZ + region.HalfDepth;

    static RoiStepReport EvaluateRoiStep(HeightDeviationRecipeRoiStep roiStep, ModelTransform transform, C3DHeightGrid grid)
    {
        if (!TryCalculateRoiStats(grid.Points, roiStep.Left, transform, out var left))
        {
            throw new InvalidDataException("ROI step found no C3D points in the left recipe region.");
        }

        if (!TryCalculateRoiStats(grid.Points, roiStep.Right, transform, out var right))
        {
            throw new InvalidDataException("ROI step found no C3D points in the right recipe region.");
        }

        return new RoiStepReport(
            left.Count,
            right.Count,
            left.RawMean,
            right.RawMean,
            right.RawMean - left.RawMean,
            right.ModelYMean - left.ModelYMean);
    }

    static bool TryCalculateRoiStats(
        IReadOnlyList<HeightGridPoint> points,
        HeightDeviationRecipeRoiRegion region,
        ModelTransform transform,
        out (int Count, double RawMean, double ModelYMean) stats)
    {
        var minX = region.CenterX - region.HalfWidth;
        var maxX = region.CenterX + region.HalfWidth;
        var minZ = region.CenterZ - region.HalfDepth;
        var maxZ = region.CenterZ + region.HalfDepth;
        var count = 0;
        var rawSum = 0.0;
        var ySum = 0.0;

        foreach (var point in points)
        {
            var position = ApplyModelTransform(point.Position, transform);
            if (position.X < minX || position.X > maxX || position.Z < minZ || position.Z > maxZ)
            {
                continue;
            }

            count++;
            rawSum += point.RawValue;
            ySum += position.Y;
        }

        if (count == 0)
        {
            stats = default;
            return false;
        }

        var inverse = 1.0 / count;
        stats = (count, rawSum * inverse, ySum * inverse);
        return true;
    }

    static Vector3 ApplyModelTransform(Vector3 sourcePosition, ModelTransform transform)
    {
        var position = sourcePosition * (float)transform.Scale;
        position = Vector3.Transform(position, Matrix4x4.CreateRotationX(ToRadians(transform.RotateXDegrees)));
        position = Vector3.Transform(position, Matrix4x4.CreateRotationY(ToRadians(transform.RotateYDegrees)));
        position = Vector3.Transform(position, Matrix4x4.CreateRotationZ(ToRadians(transform.RotateZDegrees)));
        return position + new Vector3((float)transform.TranslateX, (float)transform.TranslateY, (float)transform.TranslateZ);
    }

    static float ToRadians(double degrees) => (float)(degrees * Math.PI / 180.0);

    static string FormatRegion(HeightDeviationRecipeRoiRegion region) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"cx={region.CenterX:F3},cz={region.CenterZ:F3},halfWidth={region.HalfWidth:F3},halfDepth={region.HalfDepth:F3}");

    static string FormatNumber(double value) =>
        double.IsFinite(value) ? value.ToString("F3", CultureInfo.InvariantCulture) : "(pending)";

    static bool IsWithinTolerance(double actual, double expected, double tolerance) =>
        Math.Abs(actual - expected) <= tolerance;

    static Vector3 MapLazPosition(Vector3 source) =>
        new(source.X, source.Z, source.Y);

    static string FormatLazPoint(LazPointCloudPoint point) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"x={point.Position.X:F3},y={point.Position.Y:F3},z={point.Position.Z:F3},rgb={point.Red},{point.Green},{point.Blue}");

    static string FormatStlVector(Vector3 point) =>
        string.Create(CultureInfo.InvariantCulture, $"({point.X:G9},{point.Y:G9},{point.Z:G9})");

    static void WriteNominalActualReport(
        string reportPath,
        string recipePath,
        NominalActualComparisonRecipe recipe,
        NominalActualComparisonResult result,
        string viewerRunnerState)
    {
        var input = result.Input;
        var toolResult = NominalActualComparisonContract.CreateToolResult(result);
        var lines = new List<string>
    {
        "OpenVisionLab 3D nominal/actual Runner report",
        $"Recipe|{recipe.RecipeType}|version={recipe.Version}|path={Path.GetFullPath(recipePath)}",
        $"NominalActualRecipeStep|id={input.StepId}|direction={NominalActualComparisonInput.Direction}|sampling={NominalActualComparisonRecipe.FullQuerySampling}|unit={input.Unit}|frame={input.FrameId}|alignment={input.AlignmentId}|lowerTolerance={FormatPreciseNumber(input.LowerTolerance)}|upperTolerance={FormatPreciseNumber(input.UpperTolerance)}",
        $"NominalActualActualSource|id={input.ActualSource.Id}|path={input.ActualSource.Path}|bytes={input.ActualSource.ByteLength}|sha256={input.ActualSource.Sha256}",
        $"NominalActualNominalSource|id={input.NominalSource.Id}|path={input.NominalSource.Path}|bytes={input.NominalSource.ByteLength}|sha256={input.NominalSource.Sha256}",
        $"NominalActualQuerySource|id={input.QuerySource.Id}|path={input.QuerySource.Path}|bytes={input.QuerySource.ByteLength}|sha256={input.QuerySource.Sha256}",
        $"NominalActualInput|sourceFingerprint={input.SourceFingerprint}|executionFingerprint={input.ExecutionFingerprint}",
        $"NominalActualResult|status={result.Status}|points={result.ComparedPointCount}|below={result.BelowLowerToleranceCount}|within={result.WithinToleranceCount}|above={result.AboveUpperToleranceCount}|out={result.OutOfToleranceCount}|directSign={result.DirectSignResolvedCount}|robustRecovered={result.RobustSignRecoveredCount}|fullQuery=True|message={InspectionContractText.Clean(result.Message)}",
        FormatNominalActualStatistics("NominalActualSignedStatistics", result.Signed, input.Unit),
        FormatNominalActualStatistics("NominalActualUnsignedStatistics", result.Unsigned, input.Unit),
        InspectionContractText.FormatInspectionStep(NominalActualComparisonContract.CreateInspectionStep(input)),
        InspectionContractText.FormatToolResult(toolResult, includePrefix: true),
        $"ViewerRunnerComparison|{viewerRunnerState}|contract={(viewerRunnerState == "NotCompared" ? InspectionContractText.Missing : "provided")}",
        InspectionContractText.MetricsMarker
    };
        lines.AddRange(toolResult.Metrics.Select(metric => InspectionContractText.FormatMetric(metric)));
        lines.Add(InspectionContractText.OverlaysMarker);
        lines.AddRange(toolResult.Overlays.Select(overlay => InspectionContractText.FormatOverlay(overlay)));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
    }

    static void CompareNominalActualUiContract(
        string path,
        NominalActualComparisonResult result)
    {
        var lines = File.ReadAllLines(path);
        var input = result.Input;
        var inputFields = ReadContractFields(lines, "NominalActualInput");
        RequireContractValue(inputFields, "configured", "True", "input configured");
        RequireContractValue(inputFields, "step", input.StepId, "step ID");
        RequireContractValue(inputFields, "direction", NominalActualComparisonInput.Direction, "direction");
        RequireContractValue(inputFields, "unit", input.Unit, "unit");
        RequireContractValue(inputFields, "frame", input.FrameId, "frame");
        RequireContractValue(inputFields, "alignment", input.AlignmentId, "alignment");
        RequireContractValue(inputFields, "actualId", input.ActualSource.Id, "actual ID");
        RequireContractValue(inputFields, "actualSha256", input.ActualSource.Sha256, "actual SHA-256");
        RequireContractValue(inputFields, "nominalId", input.NominalSource.Id, "nominal ID");
        RequireContractValue(inputFields, "nominalSha256", input.NominalSource.Sha256, "nominal SHA-256");
        RequireContractValue(inputFields, "queryId", input.QuerySource.Id, "query ID");
        RequireContractValue(inputFields, "querySha256", input.QuerySource.Sha256, "query SHA-256");
        RequireContractValue(inputFields, "sourceFingerprint", input.SourceFingerprint, "source fingerprint");

        var viewModelFields = ReadContractFields(lines, "NominalActualViewModel");
        RequireContractValue(viewModelFields, "state", "Published", "ViewModel state");
        RequireContractValue(
            viewModelFields,
            "publishedFingerprint",
            input.ExecutionFingerprint,
            "published Preview fingerprint");

        var resultFields = ReadContractFields(lines, "NominalActualResult");
        RequireContractValue(resultFields, "available", "True", "result available");
        RequireContractValue(resultFields, "status", result.Status.ToString(), "status");
        RequireContractValue(resultFields, "executionFingerprint", input.ExecutionFingerprint, "execution fingerprint");
        RequireContractLong(resultFields, "points", result.ComparedPointCount);
        RequireContractLong(resultFields, "below", result.BelowLowerToleranceCount);
        RequireContractLong(resultFields, "within", result.WithinToleranceCount);
        RequireContractLong(resultFields, "above", result.AboveUpperToleranceCount);
        RequireContractLong(resultFields, "directSign", result.DirectSignResolvedCount);
        RequireContractLong(resultFields, "robustRecovered", result.RobustSignRecoveredCount);
        RequireContractValue(resultFields, "fullQuery", "True", "full-query sampling");

        CompareNominalActualStatistics(
            ReadContractFields(lines, "NominalActualSignedStatistics"),
            result.Signed,
            input.Unit,
            "signed");
        CompareNominalActualStatistics(
            ReadContractFields(lines, "NominalActualUnsignedStatistics"),
            result.Unsigned,
            input.Unit,
            "unsigned");

        var resultEntityFields = ReadContractFields(
            lines,
            NominalActualComparisonContract.ResultEntityId);
        RequireContractValue(resultEntityFields, "source", input.ActualSource.Id, "published result source");
        RequireContractValue(resultEntityFields, "status", result.Status.ToString(), "published result status");

        var resultLayerFields = ReadContractFields(
            lines,
            NominalActualComparisonContract.ResultLayerId);
        RequireContractValue(resultLayerFields, "visible", "True", "published result layer visibility");
        RequireContractValue(
            resultLayerFields,
            "entities",
            NominalActualComparisonContract.ResultEntityId,
            "published result layer entity");
    }

    static Dictionary<string, string> ReadContractFields(
        IReadOnlyList<string> lines,
        string prefix)
    {
        var line = lines.FirstOrDefault(item =>
            item.StartsWith(prefix + "|", StringComparison.Ordinal));
        if (line is null)
        {
            throw new InvalidDataException($"UI contract has no {prefix} line.");
        }

        return line.Split('|')
            .Skip(1)
            .Select(part => part.Split('=', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(parts => parts[0], parts => parts[1], StringComparer.Ordinal);
    }

    static void RequireContractValue(
        IReadOnlyDictionary<string, string> fields,
        string name,
        string expected,
        string label)
    {
        if (!fields.TryGetValue(name, out var actual)
            || !actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Nominal/actual {label} mismatch. UI={actual ?? "(missing)"}, runner={expected}.");
        }
    }

    static void RequireContractLong(
        IReadOnlyDictionary<string, string> fields,
        string name,
        long expected)
    {
        if (!fields.TryGetValue(name, out var text)
            || !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var actual)
            || actual != expected)
        {
            throw new InvalidDataException(
                $"Nominal/actual {name} mismatch. UI={text ?? "(missing)"}, runner={expected}.");
        }
    }

    static void CompareNominalActualStatistics(
        IReadOnlyDictionary<string, string> fields,
        NominalActualDeviationStatistics expected,
        string unit,
        string label)
    {
        RequireContractLong(fields, "count", expected.Count);
        RequireContractDouble(fields, "min", expected.Minimum, label);
        RequireContractDouble(fields, "max", expected.Maximum, label);
        RequireContractDouble(fields, "mean", expected.Mean, label);
        RequireContractDouble(fields, "stdPopulation", expected.StandardDeviationPopulation, label);
        RequireContractDouble(fields, "rms", expected.RootMeanSquare, label);
        RequireContractValue(fields, "unit", unit, $"{label} unit");
    }

    static void RequireContractDouble(
        IReadOnlyDictionary<string, string> fields,
        string name,
        double expected,
        string label)
    {
        if (!fields.TryGetValue(name, out var text)
            || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var actual)
            || Math.Abs(actual - expected) > 1e-12)
        {
            throw new InvalidDataException(
                $"Nominal/actual {label} {name} mismatch. UI={text ?? "(missing)"}, runner={FormatPreciseNumber(expected)}.");
        }
    }

    static string FormatNominalActualStatistics(
        string label,
        NominalActualDeviationStatistics statistics,
        string unit) =>
        $"{label}|count={statistics.Count}|min={FormatPreciseNumber(statistics.Minimum)}|max={FormatPreciseNumber(statistics.Maximum)}|mean={FormatPreciseNumber(statistics.Mean)}|stdPopulation={FormatPreciseNumber(statistics.StandardDeviationPopulation)}|rms={FormatPreciseNumber(statistics.RootMeanSquare)}|unit={unit}";

    static string FormatPreciseNumber(double value) =>
        value.ToString("G17", CultureInfo.InvariantCulture);

    static void CompareUiContract(string path, ToolResult result, params string[] metricNames)
    {
        var lines = File.ReadAllLines(path);
        var toolResultLine = lines.FirstOrDefault(line => line.StartsWith($"{result.ToolName}|", StringComparison.Ordinal));
        if (toolResultLine is null)
        {
            throw new InvalidDataException($"UI contract has no {result.ToolName} result: {path}");
        }

        var parts = toolResultLine.Split('|');
        if (parts.Length < 2 || !parts[1].Equals(result.Status.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"UI status mismatch. UI={parts.ElementAtOrDefault(1) ?? "(missing)"}, runner={result.Status}.");
        }

        var metrics = metricNames.Length == 0
            ? [result.Metrics.First()]
            : metricNames.Select(name => result.Metrics.Single(metric => metric.Name == name)).ToArray();
        foreach (var runnerMetric in metrics)
        {
            var uiMetricLine = lines.FirstOrDefault(line => line.StartsWith($"{runnerMetric.Name}|", StringComparison.Ordinal));
            if (uiMetricLine is null)
            {
                throw new InvalidDataException($"UI contract has no {runnerMetric.Name} metric: {path}");
            }

            var uiMetric = ParseMetricValue(uiMetricLine);
            if (Math.Abs(uiMetric - runnerMetric.Value) > 0.001)
            {
                throw new InvalidDataException($"{runnerMetric.Name} mismatch. UI={uiMetric:F3}, runner={runnerMetric.Value:F3}.");
            }

            if (runnerMetric.Status is { } metricStatus)
            {
                var uiStatus = ParseMetricStatus(uiMetricLine);
                if (!uiStatus.Equals(metricStatus.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"{runnerMetric.Name} status mismatch. UI={uiStatus}, runner={metricStatus}.");
                }
            }
        }
    }

    static double ParseMetricValue(string line)
    {
        foreach (var part in line.Split('|'))
        {
            if (part.StartsWith("value=", StringComparison.Ordinal))
            {
                return double.Parse(part["value=".Length..], CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidDataException($"Metric line has no value field: {line}");
    }

    static string ParseMetricStatus(string line)
    {
        foreach (var part in line.Split('|'))
        {
            if (part.StartsWith("status=", StringComparison.Ordinal))
            {
                return part["status=".Length..];
            }
        }

        throw new InvalidDataException($"Metric line has no status field: {line}");
    }

    public sealed record RoiStepReport(
        int LeftCount,
        int RightCount,
        double LeftRawMean,
        double RightRawMean,
        double RawHeightDelta,
        double ModelHeightDelta);
}

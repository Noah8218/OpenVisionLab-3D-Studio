using System.Globalization;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class VisionSdkThreeDPackageVerification
{
    private const string Unit = "mm";
    private const string FrameId = "frame.synthetic-vision-sdk";
    private const string SourceId = "source.synthetic-vision-sdk";

    public static int Run(string reportPath)
    {
        var thicknessSource = CreateThicknessSource();
        var cases = new (string Name, Func<(bool Passed, string Evidence)> Verify)[]
        {
            ("package-identity", VerifyPackageIdentity),
            ("thickness-pass-metrics", () => VerifyThicknessPass(thicknessSource)),
            ("thickness-fail-retains-measurement", () => VerifyThicknessFailure(thicknessSource)),
            ("invalid-roi-is-controlled", () => VerifyInvalidRoi(thicknessSource)),
            ("missing-unit-is-bridge-error", () => VerifyMissingUnit(thicknessSource)),
            ("strict-height-map-contract-metadata-and-units", VerifyStrictContractMetadataAndUnits),
            ("strict-height-map-contract-mismatch", VerifyStrictContractMismatch),
            ("strict-height-map-coverage-gate", VerifyStrictCoverageGate),
            ("datum-plane-rejects-mixed-units", VerifyDatumPlaneRejectsMixedUnits),
            ("warpage-analytic-plane-pass", VerifyWarpagePlane),
            ("warpage-fail-and-insufficient-data", VerifyWarpageFailureAndInsufficientData),
            ("height-grid-summary-tool", VerifyHeightGridSummaryTool),
            ("height-distribution-statistics-tool", VerifyHeightDistributionStatisticsTool),
            ("height-map-region-statistics-tool", VerifyHeightMapRegionStatisticsTool),
            ("completeness-grid-inspection-tool", VerifyCompletenessGridInspectionTool),
            ("reference-grid-point-reconstruction-tool", VerifyReferenceGridPointReconstructionTool),
            ("dual-surface-thickness-inspection-tool", VerifyDualSurfaceThicknessInspectionTool),
            ("height-deviation-inspection-tool", VerifyHeightDeviationInspectionTool),
            ("declared-mesh-normal-quality-tool", VerifyDeclaredMeshNormalQualityTool),
            ("landmark-correspondence-validation-tool", VerifyLandmarkCorrespondenceValidationTool),
            ("repeatability-statistics-tool", VerifyRepeatabilityStatisticsTool),
            ("labeled-evidence-statistics-tool", VerifyLabeledEvidenceStatisticsTool),
            ("threshold-candidate-analysis-tool", VerifyThresholdCandidateAnalysisTool),
            ("deterministic-model-surface-selection-tool", VerifyDeterministicModelSurfaceSelectionTool),
            ("rigid-pose-symmetry-equivalence-tool", VerifyRigidPoseSymmetryEquivalenceTool),
            ("acquisition-direction-orientation-tool", VerifyAcquisitionDirectionOrientationTool)
        };

        var results = cases
            .Select(item =>
            {
                var verification = Check(item.Name, item.Verify);
                return (item.Name, verification.Passed, verification.Evidence);
            })
            .ToArray();

        var passed = results.Count(item => item.Passed);
        var status = passed == results.Length ? "Pass" : "Fail";
        var lines = new List<string>
        {
            $"VisionSdkThreeDPackageVerification|{status}|cases={results.Length}|passed={passed}|failed={results.Length - passed}",
            $"Package|id={VisionSdkHeightMapInspection.PackageId}|version={VisionSdkHeightMapInspection.PackageVersion}|assembly={VisionSdkHeightMapInspection.PackageAssemblyName}|sourceCommit={VisionSdkHeightMapInspection.PackageSourceCommit}|target=netstandard2.0"
        };
        lines.AddRange(results.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{item.Evidence}"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"OpenVisionLab Vision SDK 3D package verification: {status} ({passed}/{results.Length})");
        return passed == results.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyPackageIdentity()
    {
        var passed = VisionSdkHeightMapInspection.PackageAssemblyName == "OpenVisionLab.Vision3D"
            && VisionSdkHeightMapInspection.PackageId == "OpenVisionLab.Vision3D"
            && VisionSdkHeightMapInspection.PackageVersion == "3.0.0"
            && VisionSdkHeightMapInspection.PackageSourceCommit == "f34fdf912ff38fe20f36dbb063837e14b4f922b3";
        return (passed, $"assembly={VisionSdkHeightMapInspection.PackageAssemblyName},version={VisionSdkHeightMapInspection.PackageVersion},commit={VisionSdkHeightMapInspection.PackageSourceCommit}");
    }

    private static (bool Passed, string Evidence)
        VerifyDeterministicModelSurfaceSelectionTool()
    {
        var result = new DeterministicModelSurfaceSelectionTool().Execute(
            [
                new ThreeDPoint(0.0, 0.0, 0.0),
                new ThreeDPoint(1.0, 0.0, 0.0),
                new ThreeDPoint(0.0, 1.0, 0.0)
            ],
            [
                new SurfaceModelTriangleInput(0, 1, 2),
                new SurfaceModelTriangleInput(2, 0, 1)
            ],
            new DeterministicModelSurfaceSelectionOptions
            {
                RemoveExactDuplicateTriangles = true
            });
        var removal = result.RemovedSurfaces.SingleOrDefault();
        var passed = result.Success
            && result.RetainedSourceTriangleIndices.SequenceEqual([0])
            && removal is not null
            && removal.SourceTriangleIndex == 1
            && removal.Reason == ModelSurfaceRemovalReason.ExactDuplicate
            && removal.DuplicateOfSourceTriangleIndex == 0;
        return (
            passed,
            $"success={result.Success},retained={string.Join(',', result.RetainedSourceTriangleIndices)},removed={removal?.SourceTriangleIndex}:{removal?.Reason}:{removal?.DuplicateOfSourceTriangleIndex}");
    }

    private static (bool Passed, string Evidence)
        VerifyRigidPoseSymmetryEquivalenceTool()
    {
        var reference = new RigidSurfacePose(
            1.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 1.0,
            0.0, 0.0, 0.0);
        var candidate = new RigidSurfacePose(
            0.0, -1.0, 0.0,
            1.0, 0.0, 0.0,
            0.0, 0.0, 1.0,
            0.0, 0.0, 0.0);
        var result = new RigidPoseSymmetryEquivalenceTool().Execute(
            reference,
            candidate,
            new RigidPoseSymmetryEquivalenceOptions
            {
                Symmetry = new RigidPoseSymmetry(
                    RigidPoseSymmetryKind.DiscreteRotation,
                    RigidPoseSymmetryAxis.Z,
                    4),
                MaximumTranslationDifference = 1e-9,
                MaximumRotationDifferenceDegrees = 1e-6,
                RigidTransformTolerance = 1e-9
            });
        var passed = result.Success
            && result.Equivalent
            && result.SymmetryOperationIndex == 1
            && Approximately(result.SymmetryOperationAngleDegrees, 90.0)
            && result.TranslationDifference <= 1e-12
            && result.RotationDifferenceDegrees <= 1e-6;
        return (
            passed,
            $"success={result.Success},equivalent={result.Equivalent},operation={result.SymmetryOperationIndex},angle={result.SymmetryOperationAngleDegrees:R},translation={result.TranslationDifference:R},rotation={result.RotationDifferenceDegrees:R}");
    }

    private static (bool Passed, string Evidence)
        VerifyAcquisitionDirectionOrientationTool()
    {
        var result = new AcquisitionDirectionOrientationTool().Execute(
            new ThreeDPoint(0.0, 0.0, -2.0),
            [
                new AcquisitionDirectionNormalInput(
                    0,
                    new ThreeDPoint(0.0, 0.0, 1.0)),
                new AcquisitionDirectionNormalInput(
                    1,
                    new ThreeDPoint(0.0, 0.0, -1.0)),
                new AcquisitionDirectionNormalInput(
                    2,
                    new ThreeDPoint(1.0, 0.0, 0.0))
            ],
            new AcquisitionDirectionOrientationOptions
            {
                GrazingAbsoluteCosineMaximum = 0.05
            });
        var passed = result.Success
            && result.Items.Select(item => item.Orientation).SequenceEqual(
                [
                    AcquisitionDirectionOrientation.SensorFacing,
                    AcquisitionDirectionOrientation.AwayFromSensor,
                    AcquisitionDirectionOrientation.Grazing
                ])
            && Approximately(
                result.NormalizedSensorToSceneDirection.Z,
                -1.0);
        return (
            passed,
            $"success={result.Success},directionZ={result.NormalizedSensorToSceneDirection?.Z:R},orientations={string.Join(',', result.Items.Select(item => item.Orientation))}");
    }

    private static (bool Passed, string Evidence) VerifyHeightGridSummaryTool()
    {
        var result = new HeightGridSummaryTool().Execute(
            new float[] { 0f, 1f, 2f, float.NaN },
            new HeightGridSummaryOptions
            {
                ZeroIsMissing = true,
                DistributionBinCount = 2
            });
        var passed = result.Success
            && result.ValidSampleCount == 2
            && result.ZeroSampleCount == 1
            && result.NonFiniteSampleCount == 1
            && Approximately(result.Minimum, 1.0)
            && Approximately(result.Maximum, 2.0)
            && Approximately(result.Mean, 1.5)
            && result.Bins.SequenceEqual([1, 1]);
        return (passed, $"success={result.Success},valid={result.ValidSampleCount},zero={result.ZeroSampleCount},nonFinite={result.NonFiniteSampleCount},range={result.Minimum:R}..{result.Maximum:R},mean={result.Mean:R},bins={string.Join(',', result.Bins)}");
    }

    private static (bool Passed, string Evidence) VerifyHeightDistributionStatisticsTool()
    {
        var result = new HeightDistributionStatisticsTool().Execute(
            new double[] { double.NaN, 1.0, 1.0, 3.0 },
            new HeightDistributionStatisticsOptions
            {
                BinCount = 2,
                ZeroIsMissing = false,
                ExpectedValidSampleCount = 3
            });
        var passed = result.Success
            && result.ValidSampleCount == 3
            && result.MissingSampleCount == 1
            && Approximately(result.Mean, 5.0 / 3.0)
            && result.Bins.SequenceEqual([2, 1])
            && result.PeakBinIndex == 0;
        return (passed, $"success={result.Success},valid={result.ValidSampleCount},missing={result.MissingSampleCount},mean={result.Mean:R},peak={result.PeakBinIndex},bins={string.Join(',', result.Bins)}");
    }

    private static (bool Passed, string Evidence) VerifyHeightMapRegionStatisticsTool()
    {
        var result = new HeightMapRegionStatisticsTool().Execute(
            2,
            2,
            new double[] { 1.0, double.NaN, 3.0, 5.0 },
            new HeightGridRegion(0, 0, 2, 2));
        var passed = result.Success
            && result.TotalCellCount == 4
            && result.FiniteCellCount == 3
            && Approximately(result.Sum, 9.0)
            && Approximately(result.Mean, 3.0)
            && Approximately(result.FiniteCoverageRatio, 0.75);
        return (passed, $"success={result.Success},total={result.TotalCellCount},finite={result.FiniteCellCount},sum={result.Sum:R},mean={result.Mean:R},coverage={result.FiniteCoverageRatio:R}");
    }

    private static (bool Passed, string Evidence) VerifyCompletenessGridInspectionTool()
    {
        var result = new CompletenessGridInspectionTool().Execute(
            2,
            2,
            new double[] { 10.0, 10.0, 11.0, 9.0 },
            new HeightGridRegion(0, 0, 1, 2),
            new HeightGridRegion(1, 0, 1, 2),
            new CompletenessGridProfile
            {
                Rows = 1,
                Columns = 2,
                XPitchColumns = 1,
                ZPitchRows = 1,
                CellWidthColumns = 1,
                CellHeightRows = 1
            },
            new CompletenessPresencePolicy
            {
                MinimumFiniteCoverageRatio = 1.0,
                MinimumReferenceRelativeMeanHeight = -2.0,
                MaximumReferenceRelativeMeanHeight = 2.0
            });
        var passed = result.Success
            && result.ReferenceFiniteCellCount == 2
            && Approximately(result.ReferenceMeanHeight, 10.0)
            && result.Cells.Count == 2
            && result.PassedCellCount == 2
            && result.FailedCellCount == 0
            && result.AggregateDecision == CompletenessCellDecision.Pass
            && result.Cells.All(cell => cell.Decision == CompletenessCellDecision.Pass);
        return (passed, $"success={result.Success},referenceCount={result.ReferenceFiniteCellCount},referenceMean={result.ReferenceMeanHeight:R},cells={result.Cells.Count},passed={result.PassedCellCount},failed={result.FailedCellCount},decision={result.AggregateDecision}");
    }

    private static (bool Passed, string Evidence) VerifyReferenceGridPointReconstructionTool()
    {
        var result = new ReferenceGridPointReconstructionTool().Execute(
            1,
            1,
            new double[] { 2.0 },
            new HeightGridRegion(0, 0, 1, 1),
            new ReferenceGridDefinition
            {
                Origin = new ReferenceGridVector(0.0, 0.0, 0.0),
                UAxis = new ReferenceGridVector(1.0, 0.0, 0.0),
                VAxis = new ReferenceGridVector(0.0, 0.0, 1.0),
                HAxis = new ReferenceGridVector(0.0, 1.0, 0.0),
                PitchU = 1.0,
                PitchV = 1.0
            },
            new ReferenceGridPointReconstructionOptions
            {
                CoordinateMode = ReferenceGridCoordinateMode.DeclaredFrame,
                MinimumSupportedCoordinate = float.MinValue,
                MaximumSupportedCoordinate = float.MaxValue
            });
        var sample = result.Samples.SingleOrDefault();
        var passed = result.Success
            && sample is not null
            && Approximately(sample.U, 0.5)
            && Approximately(sample.V, 0.5)
            && Approximately(sample.X, 0.5)
            && Approximately(sample.Y, 2.0)
            && Approximately(sample.Z, 0.5);
        return (passed, sample is null
            ? $"success={result.Success},samples={result.Samples.Count},message={result.Message}"
            : $"success={result.Success},samples={result.Samples.Count},uv={sample.U:R},{sample.V:R},xyz={sample.X:R},{sample.Y:R},{sample.Z:R}");
    }

    private static (bool Passed, string Evidence) VerifyDualSurfaceThicknessInspectionTool()
    {
        var reference = new[]
        {
            new HeightFieldPlaneFitSample(new ThreeDPoint(0.0, 10.0, 0.0), 10.0),
            new HeightFieldPlaneFitSample(new ThreeDPoint(1.0, 10.0, 0.0), 10.0),
            new HeightFieldPlaneFitSample(new ThreeDPoint(0.0, 10.0, 1.0), 10.0),
            new HeightFieldPlaneFitSample(new ThreeDPoint(1.0, 10.0, 1.0), 10.0)
        };
        var measurement = reference
            .Select(sample => new HeightFieldPlaneFitSample(sample.Position, 15.0))
            .ToArray();
        var result = new DualSurfaceThicknessInspectionTool().Execute(
            reference,
            measurement,
            4.0,
            6.0,
            4);
        var passed = result.Success
            && result.Decision == DualSurfaceThicknessDecision.Pass
            && Approximately(result.Mean, 5.0)
            && Approximately(result.Minimum, 5.0)
            && Approximately(result.Maximum, 5.0)
            && Approximately(result.Range, 0.0)
            && Approximately(result.RootMeanSquareSpread, 0.0)
            && result.ReferenceSampleCount == 4
            && result.MeasurementSampleCount == 4;
        return (passed, $"success={result.Success},decision={result.Decision},mean={result.Mean:R},range={result.Range:R},reference={result.ReferenceSampleCount},measurement={result.MeasurementSampleCount}");
    }

    private static (bool Passed, string Evidence) VerifyHeightDeviationInspectionTool()
    {
        var result = new HeightDeviationInspectionTool().Execute(8.0, 13.0, 10.0, 12, 2.5);
        var passed = result.Success
            && result.Decision == HeightDeviationDecision.Fail
            && Approximately(result.LowDeviation, 2.0)
            && Approximately(result.HighDeviation, 3.0)
            && Approximately(result.PeakDeviation, 3.0);
        return (passed, $"success={result.Success},decision={result.Decision},low={result.LowDeviation:R},high={result.HighDeviation:R},peak={result.PeakDeviation:R}");
    }

    private static (bool Passed, string Evidence) VerifyDeclaredMeshNormalQualityTool()
    {
        var points = new[]
        {
            new ThreeDPoint(0.0, 0.0, 0.0),
            new ThreeDPoint(1.0, 0.0, 0.0),
            new ThreeDPoint(1.0, 1.0, 0.0),
            new ThreeDPoint(0.0, 1.0, 0.0)
        };
        var normals = Enumerable.Repeat(
            new ThreeDPoint(0.0, 0.0, 1.0),
            points.Length).ToArray();
        var tool = new DeclaredMeshNormalQualityTool();
        var valid = tool.Execute(
            points,
            [0, 1, 2, 0, 2, 3],
            normals,
            null,
            1e-3,
            0.5);
        var reversed = tool.Execute(
            points,
            [0, 1, 2, 0, 2, 3],
            Enumerable.Repeat(
                new ThreeDPoint(0.0, 0.0, -1.0),
                points.Length).ToArray(),
            null,
            1e-3,
            0.5);
        var passed = valid.State == DeclaredMeshNormalQualityState.Valid
            && valid.ComparableCornerCount == 6
            && valid.ConsistentCornerCount == 6
            && reversed.State == DeclaredMeshNormalQualityState.Invalid
            && reversed.ReversedCornerCount == 6;
        return (passed, $"valid={valid.State},aligned={valid.ConsistentCornerCount}/{valid.ComparableCornerCount},reversed={reversed.State}:{reversed.ReversedCornerCount}");
    }

    private static (bool Passed, string Evidence) VerifyLandmarkCorrespondenceValidationTool()
    {
        var independent = new[]
        {
            new ThreeDPoint(0.0, 0.0, 0.0),
            new ThreeDPoint(1.0, 0.0, 0.0),
            new ThreeDPoint(0.0, 1.0, 0.0),
            new ThreeDPoint(0.0, 0.0, 1.0)
        };
        var coplanar = new[]
        {
            new ThreeDPoint(0.0, 0.0, 0.0),
            new ThreeDPoint(1.0, 0.0, 0.0),
            new ThreeDPoint(0.0, 1.0, 0.0),
            new ThreeDPoint(1.0, 1.0, 0.0)
        };
        var tool = new LandmarkCorrespondenceValidationTool();
        var valid = tool.Execute(independent, independent, 0.1);
        var rejected = tool.Execute(coplanar, independent, 0.1);
        var passed = valid.Success
            && valid.SourceRank == 4
            && valid.ReferenceRank == 4
            && !rejected.Success
            && rejected.SourceRank == 3;
        return (passed, $"valid={valid.Success},rank={valid.SourceRank}/{valid.ReferenceRank},volume={valid.SourceNormalizedTetrahedronVolume:R};coplanar={rejected.Success},rank={rejected.SourceRank}");
    }

    private static (bool Passed, string Evidence) VerifyRepeatabilityStatisticsTool()
    {
        var result = new RepeatabilityStatisticsTool().Execute([10.0, 12.0, 14.0, 16.0]);
        var passed = result.Success
            && result.Count == 4
            && Approximately(result.Mean, 13.0)
            && Approximately(result.Minimum, 10.0)
            && Approximately(result.Maximum, 16.0)
            && Approximately(result.SampleStandardDeviation, 2.581988897471611)
            && Approximately(result.SixSigmaSpread, 15.491933384829668)
            && Approximately(result.Range, 6.0);
        return (passed, $"success={result.Success},count={result.Count},mean={result.Mean:R},standardDeviation={result.SampleStandardDeviation:R},sixSigma={result.SixSigmaSpread:R},range={result.Range:R}");
    }

    private static (bool Passed, string Evidence) VerifyLabeledEvidenceStatisticsTool()
    {
        var result = new LabeledEvidenceStatisticsTool().Execute(
            [
                new LabeledEvidenceStatisticsObservation("good-1", LabeledEvidenceRole.Good, 2.0),
                new LabeledEvidenceStatisticsObservation("good-1", LabeledEvidenceRole.Good, 4.0),
                new LabeledEvidenceStatisticsObservation("bad-1", LabeledEvidenceRole.Bad, -10.0),
                new LabeledEvidenceStatisticsObservation("bad-2", LabeledEvidenceRole.Bad, 20.0)
            ]);
        var good = result.RoleStatistics.Single(item =>
            item.Role == LabeledEvidenceRole.Good);
        var bad = result.RoleStatistics.Single(item =>
            item.Role == LabeledEvidenceRole.Bad);
        var heldOut = result.RoleStatistics.Single(item =>
            item.Role == LabeledEvidenceRole.HeldOut);
        var passed = result.Success
            && result.RoleStatistics.Count == 3
            && good.SampleCount == 1
            && good.ValueCount == 2
            && Approximately(good.Mean!.Value, 3.0)
            && Approximately(good.PopulationStandardDeviation!.Value, 1.0)
            && bad.SampleCount == 2
            && Approximately(bad.Minimum!.Value, -10.0)
            && Approximately(bad.Maximum!.Value, 20.0)
            && Approximately(bad.PopulationStandardDeviation!.Value, 15.0)
            && heldOut.ValueCount == 0
            && heldOut.Mean is null;
        return (passed, $"success={result.Success},roles={result.RoleStatistics.Count},good={good.SampleCount}/{good.ValueCount}:{good.Mean:R}:{good.PopulationStandardDeviation:R},bad={bad.SampleCount}/{bad.ValueCount}:{bad.Minimum:R}..{bad.Maximum:R}:{bad.PopulationStandardDeviation:R},heldOut={heldOut.ValueCount}");
    }

    private static (bool Passed, string Evidence) VerifyThresholdCandidateAnalysisTool()
    {
        var result = new ThresholdCandidateAnalysisTool().Execute(
            [
                new ThresholdCandidateObservation(0, ThresholdObservationClass.Accepted, 2.0),
                new ThresholdCandidateObservation(1, ThresholdObservationClass.Accepted, 4.0),
                new ThresholdCandidateObservation(2, ThresholdObservationClass.Rejected, -10.0),
                new ThresholdCandidateObservation(3, ThresholdObservationClass.Rejected, 20.0)
            ]);
        var minimum = result.Candidates.Single(item =>
            item.LimitKind == ThresholdCandidateLimitKind.Minimum);
        var maximum = result.Candidates.Single(item =>
            item.LimitKind == ThresholdCandidateLimitKind.Maximum);
        var range = result.Candidates.Single(item =>
            item.LimitKind == ThresholdCandidateLimitKind.Range);
        var passed = result.Success
            && result.Candidates.Count == 3
            && Approximately(minimum.Minimum!.Value, 2.0)
            && minimum.ErrorCount == 1
            && Approximately(maximum.Maximum!.Value, 4.0)
            && maximum.ErrorCount == 1
            && Approximately(range.Minimum!.Value, 2.0)
            && Approximately(range.Maximum!.Value, 4.0)
            && range.ErrorCount == 0
            && range.Decisions.Select(item => item.ObservationIndex)
                .SequenceEqual([0, 1, 2, 3]);
        return (passed, $"success={result.Success},candidates={result.Candidates.Count},minimum={minimum.Minimum:R}:{minimum.ErrorCount},maximum={maximum.Maximum:R}:{maximum.ErrorCount},range={range.Minimum:R}..{range.Maximum:R}:{range.ErrorCount}");
    }

    private static (bool Passed, string Evidence) VerifyThicknessPass(VisionSdkHeightMapInput source)
    {
        var evaluation = VisionSdkHeightMapInspection.EvaluateThickness(
            new VisionSdkThicknessInspectionInput(source, null, 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "Passed"
            && evaluation.PlanarUnit == Unit
            && evaluation.HeightUnit == Unit
            && evaluation.CoordinateConvention == "GridXGridYScalarHeight"
            && Approximately(Metric(evaluation, "ValidSampleCount"), 4.0)
            && Approximately(Metric(evaluation, "Mean"), 1.0875)
            && Approximately(Metric(evaluation, "Range"), 0.2)
            && MetricUnit(evaluation, "Mean") == Unit;
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyThicknessFailure(VisionSdkHeightMapInput source)
    {
        var evaluation = VisionSdkHeightMapInspection.EvaluateThickness(
            new VisionSdkThicknessInspectionInput(source, null, 1.02, 1.12));
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "Failed"
            && Approximately(Metric(evaluation, "BelowLowerLimitCount"), 1.0)
            && Approximately(Metric(evaluation, "AboveUpperLimitCount"), 1.0);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInvalidRoi(VisionSdkHeightMapInput source)
    {
        var evaluation = VisionSdkHeightMapInspection.EvaluateThickness(
            new VisionSdkThicknessInspectionInput(source, new VisionSdkGridRoi(1, 1, 2, 2), 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InvalidRoi"
            && evaluation.PackageErrorCode == "InvalidRoi";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissingUnit(VisionSdkHeightMapInput source)
    {
        var evaluation = VisionSdkHeightMapInspection.EvaluateThickness(
            new VisionSdkThicknessInspectionInput(source with { Unit = string.Empty }, null, 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "BridgeError"
            && evaluation.Result.Message.Contains("unit", StringComparison.OrdinalIgnoreCase);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyStrictContractMetadataAndUnits()
    {
        var source = CreateThicknessSource() with
        {
            PlanarUnit = "grid-index",
            HeightUnit = "raw-height",
            ExpectedContract = new VisionSdkHeightMapContract(
                "grid-index",
                "raw-height",
                FrameId)
        };
        var evaluation = VisionSdkHeightMapInspection.EvaluateThickness(
            new VisionSdkThicknessInspectionInput(source, null, 0.9, 1.2, MinimumValidCoverageRatio: 0.75));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && evaluation.PlanarUnit == "grid-index"
            && evaluation.HeightUnit == "raw-height"
            && evaluation.CoordinateConvention == "GridXGridYScalarHeight"
            && Approximately(Metric(evaluation, "TotalSampleCount"), 4.0)
            && Approximately(Metric(evaluation, "ValidCoverageRatio"), 1.0)
            && MetricUnit(evaluation, "Mean") == "raw-height"
            && MetricUnit(evaluation, "ValidCoverageRatio") == "ratio";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyStrictContractMismatch()
    {
        var source = CreateThicknessSource() with
        {
            PlanarUnit = "grid-index",
            HeightUnit = "raw-height",
            ExpectedContract = new VisionSdkHeightMapContract(
                "mm",
                "raw-height",
                FrameId)
        };
        var evaluation = VisionSdkHeightMapInspection.EvaluateThickness(
            new VisionSdkThicknessInspectionInput(source, null, 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InvalidInput"
            && evaluation.PackageErrorCode == "InputContractMismatch"
            && evaluation.Result.Message.Contains("expected", StringComparison.OrdinalIgnoreCase)
            && evaluation.Result.Message.Contains("actual", StringComparison.OrdinalIgnoreCase);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyStrictCoverageGate()
    {
        var evaluation = VisionSdkHeightMapInspection.EvaluateThickness(
            new VisionSdkThicknessInspectionInput(
                CreateSource(2, 2, [1.0, double.NaN, 1.1, double.NaN]),
                null,
                0.9,
                1.2,
                MinimumValidSamples: 1,
                MinimumValidCoverageRatio: 0.75));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InsufficientData"
            && evaluation.PackageErrorCode == "InsufficientValidCoverage"
            && Approximately(Metric(evaluation, "TotalSampleCount"), 4.0)
            && Approximately(Metric(evaluation, "MissingSampleCount"), 2.0)
            && Approximately(Metric(evaluation, "ValidCoverageRatio"), 0.5)
            && Approximately(Metric(evaluation, "MinimumValidCoverageRatio"), 0.75);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyDatumPlaneRejectsMixedUnits()
    {
        var source = CreatePlanarSource() with
        {
            PlanarUnit = "grid-index",
            HeightUnit = "raw-height"
        };
        var evaluation = VisionSdkHeightMapInspection.EvaluateDatumPlaneRawHeightDeviation(
            new VisionSdkDatumPlaneRawHeightDeviationInspectionInput(
                source,
                null,
                0.0,
                1.0,
                0.0,
                0.0,
                1.0));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InvalidInput"
            && evaluation.PackageErrorCode == "InputContractMismatch"
            && evaluation.Result.Message.Contains("identical planar and height units", StringComparison.OrdinalIgnoreCase);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyWarpagePlane()
    {
        var evaluation = VisionSdkHeightMapInspection.EvaluateWarpage(
            new VisionSdkWarpageInspectionInput(CreatePlanarSource(), null, 0.000001, 0.000001));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && Approximately(Metric(evaluation, "PeakToValley"), 0.0)
            && Approximately(Metric(evaluation, "Rms"), 0.0)
            && Approximately(Metric(evaluation, "PlaneSlopeX"), 2.0)
            && Approximately(Metric(evaluation, "PlaneSlopeY"), 3.0);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyWarpageFailureAndInsufficientData()
    {
        var residualValues = CreatePlanarValues();
        residualValues[^1] += 0.1;
        var failure = VisionSdkHeightMapInspection.EvaluateWarpage(
            new VisionSdkWarpageInspectionInput(CreateSource(3, 3, residualValues), null, 0.001));
        var insufficient = VisionSdkHeightMapInspection.EvaluateWarpage(
            new VisionSdkWarpageInspectionInput(
                CreateSource(2, 2, [double.NaN, double.NaN, double.NaN, 1.0]),
                null,
                0.001,
                MinimumValidSamples: 3));
        var passed = failure.Result.Status == ResultStatus.Fail
            && failure.HasMeasurement
            && Metric(failure, "PeakToValley") > 0.001
            && insufficient.Result.Status == ResultStatus.Error
            && !insufficient.HasMeasurement
            && insufficient.PackageResultStatus == "InsufficientData";
        return (passed, $"failure=({Evidence(failure)}),insufficient=({Evidence(insufficient)})");
    }

    private static VisionSdkHeightMapInput CreateThicknessSource() =>
        CreateSource(2, 2, [1.0, 1.1, 1.05, 1.2]);

    private static VisionSdkHeightMapInput CreatePlanarSource() =>
        CreateSource(3, 3, CreatePlanarValues());

    private static VisionSdkHeightMapInput CreateSource(int rows, int columns, IReadOnlyList<double> values) =>
        new(SourceId, rows, columns, 0.0, 0.0, 1.0, 1.0, values, Unit, FrameId);

    private static double[] CreatePlanarValues()
    {
        var values = new double[9];
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                values[row * 3 + column] = 2.0 * column + 3.0 * row + 5.0;
            }
        }

        return values;
    }

    private static double Metric(VisionSdkInspectionEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.Single(metric => metric.Name == name).Value;

    private static string MetricUnit(VisionSdkInspectionEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.Single(metric => metric.Name == name).Unit;

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Evidence(VisionSdkInspectionEvaluation evaluation) =>
        $"status={evaluation.Result.Status},hasMeasurement={evaluation.HasMeasurement},packageStatus={evaluation.PackageResultStatus},error={evaluation.PackageErrorCode},planarUnit={evaluation.PlanarUnit},heightUnit={evaluation.HeightUnit},coordinateConvention={evaluation.CoordinateConvention},metrics={string.Join(',', evaluation.Result.Metrics.Select(metric => $"{metric.Name}={metric.Value.ToString("R", CultureInfo.InvariantCulture)}[{metric.Unit}]"))}";

    private static (bool Passed, string Evidence) Check(string name, Func<(bool Passed, string Evidence)> verify)
    {
        try
        {
            return verify();
        }
        catch (Exception exception)
        {
            return (false, $"unexpected {exception.GetType().Name}: {exception.Message}");
        }
    }
}

using System.Globalization;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.Vision3D.Geometry;
using SdkInspectionResult = OpenVisionLab.Vision3D.Inspection.ThreeDInspectionResult;
using SdkInspectionStatus = OpenVisionLab.Vision3D.Inspection.ThreeDInspectionResultStatus;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
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
            ("saved-background-subtraction-tool", VerifySavedBackgroundSubtractionTool),
            ("point-cloud-background-filter-tool", VerifyPointCloudBackgroundFilterTool),
            ("connected-region-outlier-mask-adapter", VerifyConnectedRegionOutlierMaskAdapter),
            ("connected-region-artifact-contract", VerifyConnectedRegionArtifactContract),
            ("connected-region-metrics-and-selected-overlay", VerifyConnectedRegionMetricsAndSelectedOverlay),
            ("connected-region-dimensions", VerifyConnectedRegionDimensions),
            ("connected-region-presence", VerifyConnectedRegionPresence),
            ("connected-region-all-regions-acceptance", VerifyConnectedRegionAllRegionsAcceptance),
            ("connected-region-fill-height", VerifyConnectedRegionFillHeight),
            ("thickness-pass-metrics", () => VerifyThicknessPass(thicknessSource)),
            ("thickness-fail-retains-measurement", () => VerifyThicknessFailure(thicknessSource)),
            ("invalid-roi-is-controlled", () => VerifyInvalidRoi(thicknessSource)),
            ("missing-unit-is-bridge-error", () => VerifyMissingUnit(thicknessSource)),
            ("strict-height-map-contract-metadata-and-units", VerifyStrictContractMetadataAndUnits),
            ("strict-height-map-contract-mismatch", VerifyStrictContractMismatch),
            ("strict-height-map-coverage-gate", VerifyStrictCoverageGate),
            ("datum-plane-rejects-mixed-units", VerifyDatumPlaneRejectsMixedUnits),
            ("warpage-analytic-plane-pass", VerifyWarpagePlane),
            ("warpage-slope-missing-sdk-units", VerifyWarpageSlopeFallbackUnits),
            ("warpage-fail-and-insufficient-data", VerifyWarpageFailureAndInsufficientData),
            ("height-grid-summary-tool", VerifyHeightGridSummaryTool),
            ("height-distribution-statistics-tool", VerifyHeightDistributionStatisticsTool),
            ("height-map-region-statistics-tool", VerifyHeightMapRegionStatisticsTool),
            ("completeness-grid-inspection-tool", VerifyCompletenessGridInspectionTool),
            ("completeness-grid-mask-aware", VerifyMaskAwareCompletenessGridInspectionTool),
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
            ("acquisition-direction-orientation-tool", VerifyAcquisitionDirectionOrientationTool),
            ("constrained-best-fit-rigid-alignment-tool", VerifyConstrainedBestFitRigidAlignmentTool)
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
            && VisionSdkHeightMapInspection.PackageVersion == "3.0.1-dev.20260828.point-cloud-background-filter.1"
            && VisionSdkHeightMapInspection.PackageSourceCommit == "35f1eef6626db710ac18452cd1e729530f2c0f2f";
        return (passed, $"assembly={VisionSdkHeightMapInspection.PackageAssemblyName},version={VisionSdkHeightMapInspection.PackageVersion},commit={VisionSdkHeightMapInspection.PackageSourceCommit}");
    }

    private static (bool Passed, string Evidence) VerifySavedBackgroundSubtractionTool()
    {
        var current = new HeightMap3D(
            2,
            3,
            10.0,
            20.0,
            0.5,
            0.25,
            [5.0, 4.0, double.NaN, -2.0, 8.0, 3.0],
            "mm",
            "raw-height",
            "fixture-top",
            "current.package");
        var background = new HeightMap3D(
            2,
            3,
            10.0,
            20.0,
            0.5,
            0.25,
            [2.0, 1.0, 4.0, double.NaN, 5.0, 4.0],
            "mm",
            "raw-height",
            "fixture-top",
            "background.package");
        var result = new HeightMapBackgroundSubtractionTool().Execute(
            current,
            background,
            new HeightMapBackgroundSubtractionOptions());
        var values = result.Output?.CopyValues();
        var passed = result.Success
            && result.Output is not null
            && result.CurrentValidSampleCount == 5
            && result.BackgroundValidSampleCount == 5
            && result.PairedValidSampleCount == 4
            && result.MissingEitherSampleCount == 2
            && result.PositiveDeltaSampleCount == 3
            && result.NegativeDeltaSampleCount == 1
            && result.ZeroDeltaSampleCount == 0
            && values is not null
            && values.Length == 6
            && values[0] == 3.0
            && values[1] == 3.0
            && double.IsNaN(values[2])
            && double.IsNaN(values[3])
            && values[4] == 3.0
            && values[5] == -1.0
            && result.Output.SourceId == current.SourceId
            && result.Output.FrameId == current.FrameId;
        return (passed, $"success={result.Success};paired={result.PairedValidSampleCount};missing={result.MissingEitherSampleCount};positive={result.PositiveDeltaSampleCount};negative={result.NegativeDeltaSampleCount};zero={result.ZeroDeltaSampleCount};message={result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyPointCloudBackgroundFilterTool()
    {
        var current = new[]
        {
            new ThreeDPoint(0.0, 0.0, 0.0),
            new ThreeDPoint(0.4, 0.0, 0.0),
            new ThreeDPoint(2.0, 0.0, 0.0),
            new ThreeDPoint(5.0, 0.0, 0.0)
        };
        var background = new[]
        {
            new ThreeDPoint(0.0, 0.0, 0.0),
            new ThreeDPoint(3.0, 0.0, 0.0)
        };
        var result = new PointCloudBackgroundFilterTool().Execute(
            current,
            background,
            new PointCloudBackgroundFilterOptions
            {
                Mode = PointCloudBackgroundFilterMode.RemoveAtOrBelowDistance,
                MaximumBackgroundDistance = 0.5
            });
        var passed = result.Success
            && result.InputPointCount == 4
            && result.BackgroundPointCount == 2
            && result.RetainedPointCount == 2
            && result.RemovedPointCount == 2
            && result.RetainedPoints.Select(point => point.SourceIndex).SequenceEqual([2, 3])
            && result.RetainedPoints.Select(point => point.NearestBackgroundDistance).SequenceEqual([1.0, 2.0])
            && result.RetainedPoints[0].Point.X == 2.0
            && result.RetainedPoints[1].Point.X == 5.0
            && Math.Abs(result.MeanNearestBackgroundDistance - 0.85) <= 1e-12;
        return (passed, $"success={result.Success};retained={string.Join(',', result.RetainedPoints.Select(point => point.SourceIndex))};removed={result.RemovedPointCount};mean={result.MeanNearestBackgroundDistance:R};message={result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyConstrainedBestFitRigidAlignmentTool()
    {
        var correspondences = new[]
        {
            new ConstrainedBestFitRigidCorrespondence(new ThreeDPoint(0d, 0d, 0d), new ThreeDPoint(4d, -2d, 1d)),
            new ConstrainedBestFitRigidCorrespondence(new ThreeDPoint(1d, 0d, 0d), new ThreeDPoint(4d, -1d, 1d)),
            new ConstrainedBestFitRigidCorrespondence(new ThreeDPoint(0d, 1d, 0d), new ThreeDPoint(3d, -2d, 1d)),
            new ConstrainedBestFitRigidCorrespondence(new ThreeDPoint(0d, 0d, 1d), new ThreeDPoint(4d, -2d, 2d))
        };
        var result = new ConstrainedBestFitRigidAlignmentTool().Execute(
            correspondences,
            new ConstrainedBestFitRigidAlignmentOptions
            {
                MaximumCorrespondenceCount = 4,
                MinimumNormalizedLineSpread = 1e-12,
                ArithmeticResidualWarning = 1e-9
            });
        var passed = result.Success
            && result.Pose is not null
            && result.PairCount == 4
            && result.UsedAllCorrespondences
            && result.Residuals.Count == 4
            && result.MaximumResidual <= 1e-12;
        return (passed, $"success={result.Success};pairs={result.PairCount};residuals={result.Residuals.Count};maxResidual={result.MaximumResidual:R};message={result.Message}");
    }

    private static (bool Passed, string Evidence) VerifyConnectedRegionOutlierMaskAdapter()
    {
        var mask = C3DOutlierCellMap.Create(5, 5, [0, 1, 6, 9, 13, 18, 19]);
        var four = C3DConnectedRegionAnalyzer.AnalyzeOutlierMask(mask);
        var eight = C3DConnectedRegionAnalyzer.AnalyzeOutlierMask(
            mask,
            C3DConnectedRegionConnectivity.Eight);
        var invalidConnectivity = C3DConnectedRegionAnalyzer.AnalyzeOutlierMask(
            mask,
            (C3DConnectedRegionConnectivity)99);
        var passed = four.Success
            && four.ForegroundCellCount == 7
            && four.RegionCount == 3
            && four.Regions.Select(region => region.CellCount).SequenceEqual([3, 1, 3])
            && four.Regions[0].Cells.SequenceEqual(
                [
                    new C3DConnectedRegionCell(0, 0),
                    new C3DConnectedRegionCell(0, 1),
                    new C3DConnectedRegionCell(1, 1)
                ])
            && eight.Success
            && eight.RegionCount == 2
            && eight.Regions.Select(region => region.CellCount).SequenceEqual([3, 4])
            && eight.Regions[1].Cells.SequenceEqual(
                [
                    new C3DConnectedRegionCell(1, 4),
                    new C3DConnectedRegionCell(2, 3),
                    new C3DConnectedRegionCell(3, 3),
                    new C3DConnectedRegionCell(3, 4)
                ])
            && !invalidConnectivity.Success
            && invalidConnectivity.RegionCount == 0;
        return (
            passed,
            $"four={four.Success}:{four.RegionCount}:{string.Join(',', four.Regions.Select(region => region.CellCount))},eight={eight.Success}:{eight.RegionCount}:{string.Join(',', eight.Regions.Select(region => region.CellCount))},invalid={invalidConnectivity.Success}");
    }

    private static (bool Passed, string Evidence) VerifyConnectedRegionArtifactContract()
    {
        var source = C3DHeightFieldSnapshot.CreateForVerification(
            "source.connected-region-artifact",
            5,
            5,
            Enumerable.Repeat(1.0, 25).ToArray(),
            Unit,
            FrameId);
        var mask = C3DOutlierCellMap.Create(
            5,
            5,
            [0, 1, 6, 9, 13, 18, 19]);
        var analysis = C3DConnectedRegionAnalyzer.AnalyzeOutlierMask(
            mask,
            C3DConnectedRegionConnectivity.Four);
        var metrics = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskMetrics(
            mask,
            C3DConnectedRegionConnectivity.Four,
            new C3DConnectedRegionMetricsOptions
            {
                OriginX = 0.0,
                OriginY = 0.0,
                ColumnPitch = 1.0,
                RowPitch = 1.0,
                AreaUnit = "grid-unit^2"
            });

        var artifact = C3DConnectedRegionArtifactFactory.Create(
            "derived.connected-region.artifact.01",
            "Detected connected regions",
            source,
            mask,
            analysis,
            C3DConnectedRegionConnectivity.Four,
            metrics,
            originX: 0.0,
            originY: 0.0,
            columnPitch: 1.0,
            rowPitch: 1.0,
            areaUnit: "grid-unit^2");
        var repeatedArtifact = C3DConnectedRegionArtifactFactory.Create(
            "derived.connected-region.artifact.01",
            "Detected connected regions",
            source,
            mask,
            analysis,
            C3DConnectedRegionConnectivity.Four,
            metrics,
            originX: 0.0,
            originY: 0.0,
            columnPitch: 1.0,
            rowPitch: 1.0,
            areaUnit: "grid-unit^2");
        var validity = C3DConnectedRegionArtifactValidator.Inspect(artifact);
        var tamperedValidity = C3DConnectedRegionArtifactValidator.Inspect(
            artifact with { ContentSha256 = new string('0', 64) });

        var verificationRoot = ResolveConnectedRegionArtifactVerificationRoot();
        Directory.CreateDirectory(verificationRoot);
        var artifactPath = Path.Combine(
            verificationRoot,
            "connected-region-artifact.json");
        C3DConnectedRegionArtifactStore.Save(artifactPath, artifact);
        var loaded = C3DConnectedRegionArtifactStore.Load(artifactPath);

        File.WriteAllText(artifactPath, "{");
        var malformedRejected = false;
        try
        {
            _ = C3DConnectedRegionArtifactStore.Load(artifactPath);
        }
        catch (InvalidDataException)
        {
            malformedRejected = true;
        }
        C3DConnectedRegionArtifactStore.Save(artifactPath, artifact);

        var mismatchedMaskRejected = false;
        try
        {
            _ = C3DConnectedRegionArtifactFactory.Create(
                "derived.connected-region.artifact.invalid",
                "Invalid connected regions",
                source,
                C3DOutlierCellMap.Create(4, 5, [0]),
                analysis,
                C3DConnectedRegionConnectivity.Four);
        }
        catch (InvalidDataException)
        {
            mismatchedMaskRejected = true;
        }

        var failedAnalysisRejected = false;
        try
        {
            _ = C3DConnectedRegionArtifactFactory.Create(
                "derived.connected-region.artifact.invalid-analysis",
                "Invalid connected regions",
                source,
                mask,
                new C3DConnectedRegionAnalysis(
                    false,
                    "synthetic failure",
                    [],
                    0,
                    0),
                C3DConnectedRegionConnectivity.Four);
        }
        catch (InvalidDataException)
        {
            failedAnalysisRejected = true;
        }

        var passed = analysis.Success
            && metrics.Success
            && validity.IsValid
            && validity.RegionCount == 3
            && validity.CellCount == 7
            && validity.SourceIdentityShapeValid
            && validity.MaskIdentityShapeValid
            && validity.ContentIdentityValid
            && artifact.ContentSha256 == repeatedArtifact.ContentSha256
            && loaded.ContentSha256 == artifact.ContentSha256
            && C3DConnectedRegionArtifactValidator.Inspect(loaded).IsValid
            && !tamperedValidity.IsValid
            && malformedRejected
            && mismatchedMaskRejected
            && failedAnalysisRejected;
        return (
            passed,
            $"artifact={artifact.ArtifactId};schema={artifact.SchemaVersion};regions={artifact.Regions.Count};cells={validity.CellCount};hash={artifact.ContentSha256};roundTrip={loaded.ContentSha256 == artifact.ContentSha256};tamperedRejected={!tamperedValidity.IsValid};malformedRejected={malformedRejected};mismatchRejected={mismatchedMaskRejected};failedAnalysisRejected={failedAnalysisRejected};path={artifactPath}");
    }

    private static string ResolveConnectedRegionArtifactVerificationRoot()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "OpenVisionLab.ThreeD",
            "E11ConnectedRegionArtifact",
            Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
    }

    private static (bool Passed, string Evidence) VerifyConnectedRegionMetricsAndSelectedOverlay()
    {
        var mask = C3DOutlierCellMap.Create(5, 5, [0, 1, 6, 9, 13, 18, 19]);
        var result = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskMetrics(
            mask,
            metricsOptions: new C3DConnectedRegionMetricsOptions
            {
                OriginX = 0.0,
                OriginY = 0.0,
                ColumnPitch = 1.0,
                RowPitch = 1.0,
                AreaUnit = "grid-unit^2",
                SelectedRegionIndex = 1
            });
        var first = result.Regions[0];
        var single = result.Regions[1];
        var selectedOverlay = result.Overlays.Single(overlay => overlay.RegionIndex == 1);
        var passed = result.Success
            && result.AreaUnit == "grid-unit^2"
            && result.RegionCount == 3
            && result.Regions.Select(region => region.CellCount).SequenceEqual([3, 1, 3])
            && Approximately(result.TotalArea, 7.0)
            && Approximately(first.CenterX, 2.0 / 3.0)
            && Approximately(first.CenterY, 1.0 / 3.0)
            && first.HasOrientation
            && Approximately(first.OrientationDegrees, 45.0)
            && !single.HasOrientation
            && double.IsNaN(single.OrientationDegrees)
            && result.Overlays.Count == 3
            && result.Overlays.Count(overlay => overlay.IsSelected) == 1
            && selectedOverlay.IsSelected
            && Approximately(selectedOverlay.Bounding.MinimumX, 3.5)
            && Approximately(selectedOverlay.Bounding.MaximumX, 4.5)
            && Approximately(selectedOverlay.Bounding.MinimumY, 0.5)
            && Approximately(selectedOverlay.Bounding.MaximumY, 1.5)
            && selectedOverlay.Bounding.CoordinateConvention == "GridXGridYCellCenterFootprint";
        return (
            passed,
            $"success={result.Success},regions={result.RegionCount},areas={string.Join(',', result.Regions.Select(region => region.Area.ToString("R", CultureInfo.InvariantCulture)))},firstCenter={first.CenterX:R},{first.CenterY:R},firstOrientation={first.HasOrientation}:{first.OrientationDegrees:R},selected={selectedOverlay.IsSelected}:{selectedOverlay.Bounding.MinimumX:R}..{selectedOverlay.Bounding.MaximumX:R},{selectedOverlay.Bounding.MinimumY:R}..{selectedOverlay.Bounding.MaximumY:R}");
    }

    private static (bool Passed, string Evidence) VerifyConnectedRegionDimensions()
    {
        var mask = C3DOutlierCellMap.Create(5, 5, [0, 1, 6, 9, 13, 18, 19]);
        var options = new C3DConnectedRegionDimensionsOptions
        {
            OriginX = 10.0,
            OriginY = 20.0,
            ColumnPitch = 2.0,
            RowPitch = 3.0,
            DimensionUnit = "mm",
            AreaUnit = "mm^2"
        };
        var result = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskDimensions(
            mask,
            dimensionsOptions: options);
        var invalidUnit = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskDimensions(
            mask,
            dimensionsOptions: options with { DimensionUnit = " " });
        var invalidAreaUnit = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskDimensions(
            mask,
            dimensionsOptions: options with { AreaUnit = " " });
        var invalidPitch = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskDimensions(
            mask,
            dimensionsOptions: options with { ColumnPitch = 0.0 });
        var invalidConnectivity = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskDimensions(
            mask,
            (C3DConnectedRegionConnectivity)99,
            options);
        var invalidForeground = C3DConnectedRegionAnalyzer.AnalyzeDimensions(
            5,
            5,
            null,
            dimensionsOptions: options);
        var first = result.Regions[0];
        var single = result.Regions[1];
        var third = result.Regions[2];
        var passed = result.Success
            && result.DimensionUnit == "mm"
            && result.AreaUnit == "mm^2"
            && result.RegionCount == 3
            && Approximately(result.TotalArea, 42.0)
            && first.CellCount == 3
            && Approximately(first.Width, 4.0)
            && Approximately(first.Height, 6.0)
            && Approximately(first.Area, 18.0)
            && single.CellCount == 1
            && Approximately(single.Width, 2.0)
            && Approximately(single.Height, 3.0)
            && Approximately(single.Area, 6.0)
            && third.CellCount == 3
            && Approximately(third.Width, 4.0)
            && Approximately(third.Height, 6.0)
            && Approximately(third.Area, 18.0)
            && !invalidUnit.Success
            && invalidUnit.RegionCount == 0
            && !invalidAreaUnit.Success
            && invalidAreaUnit.RegionCount == 0
            && !invalidPitch.Success
            && invalidPitch.RegionCount == 0
            && !invalidConnectivity.Success
            && invalidConnectivity.RegionCount == 0
            && !invalidForeground.Success
            && invalidForeground.RegionCount == 0;
        return (
            passed,
            $"success={result.Success},units={result.DimensionUnit}/{result.AreaUnit},regions={result.RegionCount},dimensions={string.Join(';', result.Regions.Select(region => $"{region.Width:R}x{region.Height:R}:{region.Area:R}"))},totalArea={result.TotalArea:R},invalidUnit={invalidUnit.Success},invalidAreaUnit={invalidAreaUnit.Success},invalidPitch={invalidPitch.Success},invalidConnectivity={invalidConnectivity.Success},invalidForeground={invalidForeground.Success}");
    }

    private static (bool Passed, string Evidence) VerifyConnectedRegionPresence()
    {
        var mask = C3DOutlierCellMap.Create(3, 4, [0, 1, 7]);
        var values = new[]
        {
            5.0, 5.0, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, 1.0,
            double.NaN, double.NaN, double.NaN, double.NaN
        };
        var result = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskPresence(
            mask,
            values,
            presenceOptions: new C3DConnectedRegionPresenceOptions
            {
                MinimumFiniteCoverageRatio = 1.0,
                MinimumMeanHeight = 4.0,
                MaximumMeanHeight = 6.0,
                HeightUnit = "raw-height"
            });
        var invalidUnit = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskPresence(
            mask,
            values,
            presenceOptions: new C3DConnectedRegionPresenceOptions
            {
                HeightUnit = " "
            });
        var present = result.Regions[0];
        var missing = result.Regions[1];
        var passed = result.Success
            && result.HeightUnit == "raw-height"
            && result.RegionCount == 2
            && result.PresentRegionCount == 1
            && result.MissingRegionCount == 1
            && result.AggregateDecision == C3DConnectedRegionPresenceDecision.Present
            && present.FiniteCellCount == 2
            && Approximately(present.FiniteCoverageRatio, 1.0)
            && present.MeanHeight == 5.0
            && present.Decision == C3DConnectedRegionPresenceDecision.Present
            && missing.FiniteCellCount == 1
            && missing.MeanHeight == 1.0
            && missing.HeightDisposition
                == C3DConnectedRegionPresenceHeightDisposition.BelowMinimum
            && missing.Decision == C3DConnectedRegionPresenceDecision.Missing
            && !invalidUnit.Success
            && invalidUnit.RegionCount == 0;
        return (
            passed,
            $"success={result.Success},unit={result.HeightUnit},regions={result.RegionCount},present={result.PresentRegionCount},missing={result.MissingRegionCount},decisions={string.Join(',', result.Regions.Select(region => region.Decision))},invalidUnit={invalidUnit.Success}");
    }

    private static (bool Passed, string Evidence) VerifyConnectedRegionAllRegionsAcceptance()
    {
        var mask = C3DOutlierCellMap.Create(3, 4, [0, 1, 7]);
        var allValues = new[]
        {
            5.0, 5.0, double.NaN,
            double.NaN, double.NaN, double.NaN,
            double.NaN, 5.0, double.NaN,
            double.NaN, double.NaN, double.NaN
        };
        var mixedValues = (double[])allValues.Clone();
        mixedValues[7] = 1.0;
        var options = new C3DConnectedRegionPresenceOptions
        {
            MinimumFiniteCoverageRatio = 1.0,
            MinimumMeanHeight = 4.0,
            MaximumMeanHeight = 6.0,
            HeightUnit = "raw-height"
        };
        var all = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskAllRegionsAcceptance(
            mask,
            allValues,
            presenceOptions: options);
        var mixed = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskAllRegionsAcceptance(
            mask,
            mixedValues,
            presenceOptions: options);
        var empty = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskAllRegionsAcceptance(
            C3DOutlierCellMap.Create(3, 4, Array.Empty<int>()),
            Enumerable.Repeat(double.NaN, 12).ToArray(),
            presenceOptions: options);
        var invalidUnit = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskAllRegionsAcceptance(
            mask,
            allValues,
            presenceOptions: options with { HeightUnit = " " });
        var invalidValues = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskAllRegionsAcceptance(
            mask,
            null,
            presenceOptions: options);
        var directPresence = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskPresence(
            mask,
            allValues,
            presenceOptions: options);
        var reused = C3DConnectedRegionAnalyzer.EvaluateAllRegionsAcceptance(directPresence);
        var passed = all.Success
            && all.HeightUnit == "raw-height"
            && all.RegionCount == 2
            && all.AcceptedRegionCount == 2
            && all.RejectedRegionCount == 0
            && all.AggregateDecision
                == C3DConnectedRegionAllRegionsAcceptanceDecision.Accepted
            && all.Regions.All(region =>
                region.Decision == C3DConnectedRegionPresenceDecision.Present)
            && mixed.Success
            && mixed.AcceptedRegionCount == 1
            && mixed.RejectedRegionCount == 1
            && mixed.AggregateDecision
                == C3DConnectedRegionAllRegionsAcceptanceDecision.Rejected
            && mixed.Regions[1].Decision == C3DConnectedRegionPresenceDecision.Missing
            && mixed.Regions[1].MeanHeight == 1.0
            && empty.Success
            && empty.RegionCount == 0
            && empty.AggregateDecision
                == C3DConnectedRegionAllRegionsAcceptanceDecision.Rejected
            && !invalidUnit.Success
            && invalidUnit.AggregateDecision
                == C3DConnectedRegionAllRegionsAcceptanceDecision.NotEvaluated
            && !invalidValues.Success
            && invalidValues.AggregateDecision
                == C3DConnectedRegionAllRegionsAcceptanceDecision.NotEvaluated
            && reused.AggregateDecision
                == C3DConnectedRegionAllRegionsAcceptanceDecision.Accepted
            && reused.Regions.SequenceEqual(all.Regions);
        return (
            passed,
            $"all={all.Success}:{all.AggregateDecision}:{all.AcceptedRegionCount}/{all.RejectedRegionCount},mixed={mixed.Success}:{mixed.AggregateDecision}:{mixed.AcceptedRegionCount}/{mixed.RejectedRegionCount},empty={empty.Success}:{empty.AggregateDecision}:{empty.RegionCount},invalidUnit={invalidUnit.Success},invalidValues={invalidValues.Success},reused={reused.AggregateDecision}:{reused.Regions.Count}");
    }

    private static (bool Passed, string Evidence) VerifyConnectedRegionFillHeight()
    {
        var mask = C3DOutlierCellMap.Create(5, 5, [0, 1, 3, 4, 5]);
        var values = new[]
        {
            12.0, 12.5, double.NaN, 10.5, 11.0,
            11.75, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN,
            double.NaN, double.NaN, double.NaN, double.NaN, double.NaN
        };
        var options = new C3DConnectedRegionFillHeightOptions
        {
            ReferenceSurface = new C3DConnectedRegionFillHeightReferenceSurface(
                0.5,
                -0.25,
                10.0),
            MinimumFiniteCoverageRatio = 1.0,
            MinimumMeanFillHeight = 1.5,
            MaximumMeanFillHeight = 2.5,
            HeightUnit = "raw-height"
        };
        var result = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskFillHeight(
            mask,
            values,
            fillHeightOptions: options);
        var invalidUnit = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskFillHeight(
            mask,
            values,
            fillHeightOptions: options with { HeightUnit = " " });
        var invalidSurface = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskFillHeight(
            mask,
            values,
            fillHeightOptions: options with
            {
                ReferenceSurface = new C3DConnectedRegionFillHeightReferenceSurface(
                    double.NaN,
                    0.0,
                    0.0)
            });
        var invalidConnectivity = C3DConnectedRegionAnalyzer.AnalyzeOutlierMaskFillHeight(
            mask,
            values,
            (C3DConnectedRegionConnectivity)99,
            options);
        var accepted = result.Regions[0];
        var rejected = result.Regions[1];
        var passed = result.Success
            && result.HeightUnit == "raw-height"
            && result.ReferenceSurface is not null
            && result.ReferenceSurface.SlopeX == 0.5
            && result.ReferenceSurface.SlopeZ == -0.25
            && result.ReferenceSurface.Intercept == 10.0
            && result.RegionCount == 2
            && result.AcceptedRegionCount == 1
            && result.RejectedRegionCount == 1
            && accepted.FiniteCellCount == 3
            && Approximately(accepted.MeanFillHeight!.Value, 2.0)
            && accepted.Decision == C3DConnectedRegionFillHeightDecision.Accepted
            && rejected.FiniteCellCount == 2
            && Approximately(rejected.MeanFillHeight!.Value, -1.0)
            && rejected.FillHeightDisposition
                == C3DConnectedRegionFillHeightDisposition.BelowMinimum
            && rejected.Decision == C3DConnectedRegionFillHeightDecision.Rejected
            && !invalidUnit.Success
            && !invalidSurface.Success
            && !invalidConnectivity.Success;
        return (
            passed,
            $"success={result.Success},unit={result.HeightUnit},regions={result.RegionCount},accepted={result.AcceptedRegionCount},rejected={result.RejectedRegionCount},means={string.Join(',', result.Regions.Select(region => region.MeanFillHeight?.ToString("R", CultureInfo.InvariantCulture) ?? "missing"))},invalidUnit={invalidUnit.Success},invalidSurface={invalidSurface.Success},invalidConnectivity={invalidConnectivity.Success}");
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

    private static (bool Passed, string Evidence) VerifyMaskAwareCompletenessGridInspectionTool()
    {
        var result = new CompletenessGridInspectionTool().ExecuteMaskAware(
            3,
            3,
            new double[]
            {
                10.0, 100.0, 10.0,
                12.0, 100.0, 12.0,
                double.NaN, 12.0, 100.0
            },
            new HeightGridRegion(0, 0, 1, 1),
            new HeightGridRegion(1, 0, 2, 3),
            new HeightGridMask(
                3,
                3,
                new[]
                {
                    false, false, false,
                    true, false, true,
                    true, true, false
                }),
            new CompletenessGridProfile
            {
                Rows = 1,
                Columns = 1,
                XPitchColumns = 3,
                ZPitchRows = 2,
                CellWidthColumns = 3,
                CellHeightRows = 2
            },
            new CompletenessPresencePolicy
            {
                MinimumFiniteCoverageRatio = 0.75,
                MinimumReferenceRelativeMeanHeight = 0.0,
                MaximumReferenceRelativeMeanHeight = 2.0
            });
        var cell = result.Cells.SingleOrDefault();
        var passed = result.Success
            && cell is not null
            && cell.TotalCellCount == 4
            && cell.FiniteCellCount == 3
            && cell.MissingCellCount == 1
            && Approximately(cell.FiniteCoverageRatio, 0.75)
            && Approximately(cell.MeanHeight ?? double.NaN, 12.0)
            && cell.Decision == CompletenessCellDecision.Pass;
        return (passed, $"success={result.Success},total={cell?.TotalCellCount},finite={cell?.FiniteCellCount},missing={cell?.MissingCellCount},coverage={cell?.FiniteCoverageRatio:R},mean={cell?.MeanHeight:R},decision={cell?.Decision}");
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
            && Approximately(Metric(evaluation, "PlaneSlopeY"), 3.0)
            && MetricUnit(evaluation, "PlaneSlopeX") == $"{Unit}/{Unit}"
            && MetricUnit(evaluation, "PlaneSlopeY") == $"{Unit}/{Unit}";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyWarpageSlopeFallbackUnits()
    {
        var source = CreatePlanarSource() with
        {
            PlanarUnit = "grid-index",
            HeightUnit = "raw-height"
        };
        var inspection = new SdkInspectionResult
        {
            ResultStatus = SdkInspectionStatus.Passed,
            HasMeasurement = true,
            SourceId = source.SourceEntityId,
            FrameId = source.FrameId,
            PlanarUnit = string.Empty,
            HeightUnit = string.Empty
        };
        inspection.Metrics["PlaneSlopeX"] = 2.0;
        inspection.Metrics["PlaneSlopeY"] = 3.0;
        inspection.Metrics["PlaneIntercept"] = 5.0;
        inspection.Metrics["PlaneNormalX"] = 0.25;
        inspection.Metrics["TotalSampleCount"] = 9.0;
        inspection.Metrics["ValidCoverageRatio"] = 1.0;
        inspection.Metrics["PeakToValley"] = 0.0;
        inspection.Metrics["SyntheticLength"] = 4.0;

        var translate = typeof(VisionSdkHeightMapInspection).GetMethod(
            "Translate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (translate?.Invoke(null, new object?[] { "Synthetic missing metric units", inspection, source, null })
            is not VisionSdkInspectionEvaluation evaluation)
        {
            return (false, "Translate reflection hook was unavailable.");
        }

        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && MetricUnit(evaluation, "PlaneSlopeX") == "raw-height/grid-index"
            && MetricUnit(evaluation, "PlaneSlopeY") == "raw-height/grid-index"
            && MetricUnit(evaluation, "PlaneIntercept") == "ratio"
            && MetricUnit(evaluation, "PlaneNormalX") == "ratio"
            && MetricUnit(evaluation, "TotalSampleCount") == "count"
            && MetricUnit(evaluation, "ValidCoverageRatio") == "ratio"
            && MetricUnit(evaluation, "PeakToValley") == "raw-height"
            && MetricUnit(evaluation, "SyntheticLength") == "raw-height";
        return (passed, $"metricUnits={inspection.MetricUnits.Count},{Evidence(evaluation)}");
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

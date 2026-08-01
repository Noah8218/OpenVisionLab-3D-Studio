using System.Globalization;
using Lib.ThreeD.FeatureExtraction;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

internal static class LibraryNoahThreeDPackageVerification
{
    private const string Unit = "mm";
    private const string FrameId = "frame.synthetic-library-noah";
    private const string SourceId = "source.synthetic-library-noah";

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
            ("landmark-correspondence-validation-tool", VerifyLandmarkCorrespondenceValidationTool)
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
            $"LibraryNoahThreeDPackageVerification|{status}|cases={results.Length}|passed={passed}|failed={results.Length - passed}",
            $"Package|id={LibraryNoahHeightMapInspection.PackageId}|version={LibraryNoahHeightMapInspection.PackageVersion}|assembly={LibraryNoahHeightMapInspection.PackageAssemblyName}|sourceCommit={LibraryNoahHeightMapInspection.PackageSourceCommit}|target=netstandard2.0"
        };
        lines.AddRange(results.Select(item => $"Case|{item.Name}|{(item.Passed ? "Pass" : "Fail")}|{item.Evidence}"));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(reportPath))!);
        File.WriteAllLines(reportPath, lines);
        Console.WriteLine($"Library-Noah 3D package verification: {status} ({passed}/{results.Length})");
        return passed == results.Length ? 0 : 5;
    }

    private static (bool Passed, string Evidence) VerifyPackageIdentity()
    {
        var passed = LibraryNoahHeightMapInspection.PackageAssemblyName == "Lib.ThreeD"
            && LibraryNoahHeightMapInspection.PackageId == "Lib.ThreeD"
            && LibraryNoahHeightMapInspection.PackageVersion == "2.8.6"
            && LibraryNoahHeightMapInspection.PackageSourceCommit == "3ef2f52546a9187df465bf8973e26426c30f7634";
        return (passed, $"assembly={LibraryNoahHeightMapInspection.PackageAssemblyName},version={LibraryNoahHeightMapInspection.PackageVersion},commit={LibraryNoahHeightMapInspection.PackageSourceCommit}");
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

    private static (bool Passed, string Evidence) VerifyThicknessPass(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source, null, 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Pass
            && evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "Passed"
            && Approximately(Metric(evaluation, "ValidSampleCount"), 4.0)
            && Approximately(Metric(evaluation, "Mean"), 1.0875)
            && Approximately(Metric(evaluation, "Range"), 0.2);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyThicknessFailure(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source, null, 1.02, 1.12));
        var passed = evaluation.Result.Status == ResultStatus.Fail
            && evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "Failed"
            && Approximately(Metric(evaluation, "BelowLowerLimitCount"), 1.0)
            && Approximately(Metric(evaluation, "AboveUpperLimitCount"), 1.0);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyInvalidRoi(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source, new LibraryNoahGridRoi(1, 1, 2, 2), 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "InvalidRoi"
            && evaluation.PackageErrorCode == "InvalidRoi";
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyMissingUnit(LibraryNoahHeightMapInput source)
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateThickness(
            new LibraryNoahThicknessInspectionInput(source with { Unit = string.Empty }, null, 0.9, 1.2));
        var passed = evaluation.Result.Status == ResultStatus.Error
            && !evaluation.HasMeasurement
            && evaluation.PackageResultStatus == "BridgeError"
            && evaluation.Result.Message.Contains("unit", StringComparison.OrdinalIgnoreCase);
        return (passed, Evidence(evaluation));
    }

    private static (bool Passed, string Evidence) VerifyWarpagePlane()
    {
        var evaluation = LibraryNoahHeightMapInspection.EvaluateWarpage(
            new LibraryNoahWarpageInspectionInput(CreatePlanarSource(), null, 0.000001, 0.000001));
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
        var failure = LibraryNoahHeightMapInspection.EvaluateWarpage(
            new LibraryNoahWarpageInspectionInput(CreateSource(3, 3, residualValues), null, 0.001));
        var insufficient = LibraryNoahHeightMapInspection.EvaluateWarpage(
            new LibraryNoahWarpageInspectionInput(
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

    private static LibraryNoahHeightMapInput CreateThicknessSource() =>
        CreateSource(2, 2, [1.0, 1.1, 1.05, 1.2]);

    private static LibraryNoahHeightMapInput CreatePlanarSource() =>
        CreateSource(3, 3, CreatePlanarValues());

    private static LibraryNoahHeightMapInput CreateSource(int rows, int columns, IReadOnlyList<double> values) =>
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

    private static double Metric(LibraryNoahInspectionEvaluation evaluation, string name) =>
        evaluation.Result.Metrics.Single(metric => metric.Name == name).Value;

    private static bool Approximately(double actual, double expected, double tolerance = 1e-9) =>
        double.IsFinite(actual) && Math.Abs(actual - expected) <= tolerance;

    private static string Evidence(LibraryNoahInspectionEvaluation evaluation) =>
        $"status={evaluation.Result.Status},hasMeasurement={evaluation.HasMeasurement},packageStatus={evaluation.PackageResultStatus},error={evaluation.PackageErrorCode},metrics={string.Join(',', evaluation.Result.Metrics.Select(metric => $"{metric.Name}={metric.Value.ToString("R", CultureInfo.InvariantCulture)}"))}";

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

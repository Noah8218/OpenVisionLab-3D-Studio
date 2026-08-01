using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceMatchPerformanceBudgetVerification
{
    private const int WarmupCount = 10;
    private const int MeasurementCount = 25;
    private const int FixtureSampleCount = 256;

    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory);

        var configuration = typeof(
                SurfaceMatchPerformanceBudgetVerification)
            .Assembly
            .GetCustomAttribute<AssemblyConfigurationAttribute>()
            ?.Configuration
            ?? "Unknown";
        if (!string.Equals(
                configuration,
                "Release",
                StringComparison.OrdinalIgnoreCase))
        {
            File.WriteAllLines(
                fullReportPath,
                [
                    "SurfaceMatchPerformanceBudgetVerification|FAIL|cases=1|passed=0|failed=1",
                    "release-configuration-required|FAIL|configuration="
                        + configuration,
                    "Boundary|Release-only fixed synthetic fixture regression gate; no production throughput, physical metrology, or human-usability claim."
                ],
                new UTF8Encoding(false));
            Console.Error.WriteLine(
                "Surface match performance budget verification requires a Release build.");
            return 1;
        }

        var fixture = CreateFixture();
        var policy = SurfaceMatchAcceptancePolicy.Create(
            0.99,
            0.001);
        var profiles = new[]
        {
            new BudgetProfile(
                "bounded-11-candidates",
                SearchParameters(8.0, 28.0, 2.0, 11),
                11,
                40.0,
                80.0,
                150.0),
            new BudgetProfile(
                "broad-61-candidates",
                SearchParameters(-60.0, 60.0, 2.0, 61),
                61,
                180.0,
                350.0,
                700.0)
        };

        var measurements = profiles
            .Select(profile => Measure(
                profile,
                fixture.Model,
                fixture.Scene,
                fixture.ExpectedPose,
                policy))
            .ToArray();
        var cases = new List<CaseResult>
        {
            Check(
                "release-configuration",
                string.Equals(
                    configuration,
                    "Release",
                    StringComparison.OrdinalIgnoreCase),
                $"configuration={configuration}"),
            Check(
                "fixed-fixture-identity",
                fixture.Model.Samples.Length == FixtureSampleCount
                && fixture.Scene.Samples.Length == FixtureSampleCount
                && SurfaceModelArtifactValidator
                    .Inspect(fixture.Model).IsValid
                && PreparedSceneArtifactValidator
                    .Inspect(fixture.Scene).IsValid,
                $"model={fixture.Model.ContentSha256};scene={fixture.Scene.ContentSha256};samples={FixtureSampleCount}"),
            Check(
                "fixed-profile-candidate-order",
                measurements[0].Profile.ExpectedCandidateCount
                    < measurements[1].Profile.ExpectedCandidateCount,
                $"bounded={measurements[0].Profile.ExpectedCandidateCount};broad={measurements[1].Profile.ExpectedCandidateCount}"),
            Check(
                "broad-profile-exercises-larger-workload",
                measurements[1].OuterMilliseconds.Median
                    >= measurements[0].OuterMilliseconds.Median * 2.0,
                $"boundedMedianMs={FormatMilliseconds(measurements[0].OuterMilliseconds.Median)};broadMedianMs={FormatMilliseconds(measurements[1].OuterMilliseconds.Median)};minimumRatio=2.0")
        };

        foreach (var measurement in measurements)
        {
            var name = measurement.Profile.Name;
            cases.Add(Check(
                $"{name}-candidate-count",
                measurement.CandidateCounts.All(count =>
                    count
                    == measurement.Profile.ExpectedCandidateCount),
                $"expected={measurement.Profile.ExpectedCandidateCount};observed={string.Join(',', measurement.CandidateCounts.Distinct())}"));
            cases.Add(Check(
                $"{name}-decision-and-pose",
                measurement.AllDecisionsPass
                && measurement.AllPosesMatch,
                $"decision=Pass;pose=rotation18,translation(15,-7,3);coverage={measurement.CoverageRatio:G17};rmse={measurement.InlierRmse:G17}"));
            cases.Add(Check(
                $"{name}-deterministic-identities",
                measurement.ExecutionHashes.Distinct(
                    StringComparer.Ordinal).Count() == 1
                && measurement.AssessmentHashes.Distinct(
                    StringComparer.Ordinal).Count() == 1,
                $"execution={measurement.ExecutionHashes[0]};assessment={measurement.AssessmentHashes[0]};repetitions={MeasurementCount}"));
            cases.Add(Check(
                $"{name}-runtime-contract",
                measurement.AllRuntimeReportsValid
                && measurement.InternalStageMilliseconds.Keys
                    .SequenceEqual(ExpectedStages),
                $"clock={SurfaceMatchRuntimeReport.CurrentClock};stages={string.Join(',', measurement.InternalStageMilliseconds.Keys)}"));
            cases.Add(BudgetCheck(
                name,
                "median",
                measurement.OuterMilliseconds.Median,
                measurement.Profile.MedianBudgetMilliseconds));
            cases.Add(BudgetCheck(
                name,
                "p95",
                measurement.OuterMilliseconds.P95,
                measurement.Profile.P95BudgetMilliseconds));
            cases.Add(BudgetCheck(
                name,
                "max",
                measurement.OuterMilliseconds.Maximum,
                measurement.Profile.MaximumBudgetMilliseconds));
        }

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceMatchPerformanceBudgetVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "Boundary|Release-only fixed synthetic 256-sample regression budget on the current host; measurements are observational and excluded from execution, assessment, and acceptance identities; no production throughput, cross-hardware equivalence, physical metrology, or human-usability claim.",
            $"Environment|configuration={configuration}|framework={RuntimeInformation.FrameworkDescription}|os={RuntimeInformation.OSDescription}|architecture={RuntimeInformation.ProcessArchitecture}|processors={Environment.ProcessorCount}|serverGc={GCSettings.IsServerGC}|stopwatchHighResolution={Stopwatch.IsHighResolution}|stopwatchFrequency={Stopwatch.Frequency}",
            $"Fixture|samples={FixtureSampleCount}|model={fixture.Model.ContentSha256}|scene={fixture.Scene.ContentSha256}|policy={policy.ContentSha256}|expectedRotationDegrees=18|expectedTranslation=(15,-7,3)|warmups={WarmupCount}|measurements={MeasurementCount}"
        };
        foreach (var measurement in measurements)
        {
            lines.Add(ProfileEvidence(measurement));
            lines.Add(
                $"Runs|profile={measurement.Profile.Name}|outerMilliseconds={string.Join(',', measurement.OuterRunsMilliseconds.Select(FormatMilliseconds))}");
            foreach (var stage in ExpectedStages)
            {
                lines.Add(
                    $"Stage|profile={measurement.Profile.Name}|stage={stage}|{StatisticsEvidence(measurement.InternalStageMilliseconds[stage])}");
            }
        }

        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        Console.WriteLine(
            "Surface match performance budget verification: "
            + $"{(passed == cases.Count ? "PASS" : "FAIL")} "
            + $"({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static ProfileMeasurement Measure(
        BudgetProfile profile,
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        RigidPose3D expectedPose,
        SurfaceMatchAcceptancePolicy policy)
    {
        var validity =
            RigidSurfacePoseSearchParameterValidator.Inspect(
                profile.Parameters);
        if (!validity.IsValid
            || validity.CandidateCount
                != profile.ExpectedCandidateCount)
        {
            throw new InvalidDataException(
                $"Performance profile {profile.Name} is invalid: "
                + string.Join(" ", validity.Errors));
        }

        for (var index = 0; index < WarmupCount; index++)
        {
            _ = SurfaceMatchEvaluationExecutor.Execute(
                model,
                scene,
                profile.Parameters,
                policy);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var outerRuns = new List<double>(MeasurementCount);
        var stageRuns = ExpectedStages.ToDictionary(
            stage => stage,
            _ => new List<double>(MeasurementCount),
            StringComparer.Ordinal);
        var executionHashes = new List<string>(MeasurementCount);
        var assessmentHashes = new List<string>(MeasurementCount);
        var candidateCounts = new List<int>(MeasurementCount);
        var allRuntimeReportsValid = true;
        var allDecisionsPass = true;
        var allPosesMatch = true;
        var coverageRatio = double.NaN;
        var inlierRmse = double.NaN;

        for (var index = 0;
             index < MeasurementCount;
             index++)
        {
            var started = Stopwatch.GetTimestamp();
            var result = SurfaceMatchEvaluationExecutor.Execute(
                model,
                scene,
                profile.Parameters,
                policy);
            outerRuns.Add(
                Stopwatch.GetElapsedTime(started)
                    .TotalMilliseconds);
            executionHashes.Add(result.Execution.ContentSha256);
            assessmentHashes.Add(result.Assessment.ContentSha256);
            candidateCounts.Add(
                result.Execution.PoseResult
                    .EvaluatedCandidateCount);
            allRuntimeReportsValid &=
                SurfaceMatchAssessmentArtifactValidator
                    .InspectRuntime(
                        result.Runtime,
                        out _);
            allDecisionsPass &=
                result.Assessment.Decision
                    == SurfaceMatchDecision.Pass;
            allPosesMatch &= PoseMatches(
                result.Execution.PoseResult.Pose,
                expectedPose);
            coverageRatio =
                result.Execution.PoseResult.Coverage
                    .CoverageRatio;
            inlierRmse =
                result.Execution.PoseResult.Coverage
                    .InlierRmse
                ?? double.NaN;
            foreach (var stage in result.Runtime.Stages)
            {
                if (stageRuns.TryGetValue(
                        stage.StageId,
                        out var values))
                {
                    values.Add(
                        TimeSpan.FromTicks(stage.ElapsedTicks)
                            .TotalMilliseconds);
                }
                else
                {
                    allRuntimeReportsValid = false;
                }
            }
        }

        allRuntimeReportsValid &= stageRuns.Values.All(values =>
            values.Count == MeasurementCount);
        return new ProfileMeasurement(
            profile,
            outerRuns.ToArray(),
            Statistics.Create(outerRuns),
            stageRuns.ToDictionary(
                pair => pair.Key,
                pair => Statistics.Create(pair.Value),
                StringComparer.Ordinal),
            executionHashes.ToArray(),
            assessmentHashes.ToArray(),
            candidateCounts.ToArray(),
            allRuntimeReportsValid,
            allDecisionsPass,
            allPosesMatch,
            coverageRatio,
            inlierRmse);
    }

    private static Fixture CreateFixture()
    {
        var positions = new List<Vector3>(FixtureSampleCount * 3);
        var indices = new List<int>(FixtureSampleCount * 3);
        var normals = new List<Vector3>(FixtureSampleCount * 3);
        for (var row = 0; row < 16; row++)
        {
            for (var column = 0; column < 16; column++)
            {
                var center = new Vector3(
                    (float)(column * 1.7
                        + row % 3 * 0.13),
                    (float)(row * 1.45
                        + column % 5 * 0.07),
                    (float)((row * 17 + column * 11) % 13
                        * 0.03));
                var offset = positions.Count;
                positions.Add(center + new Vector3(-0.12f, -0.08f, 0.0f));
                positions.Add(center + new Vector3(0.14f, -0.06f, 0.0f));
                positions.Add(center + new Vector3(-0.02f, 0.16f, 0.0f));
                indices.Add(offset);
                indices.Add(offset + 1);
                indices.Add(offset + 2);
                normals.Add(Vector3.UnitZ);
                normals.Add(Vector3.UnitZ);
                normals.Add(Vector3.UnitZ);
            }
        }

        var mesh = ImportedMesh.CreateTriangleMesh(
            "fixture://surface-match-performance/nominal",
            "Surface Match Performance Fixture",
            "SYNTHETIC",
            positions.ToArray(),
            indices.ToArray(),
            normals.ToArray());
        var sourceSha256 = HashMesh(
            positions,
            indices,
            normals);
        var model = SurfaceModelPreparation.Prepare(
            mesh,
            new SurfaceModelPreparationRequest(
                "surface-model.performance.256",
                "Surface Match Performance Model",
                "source.mesh.performance.256",
                sourceSha256,
                "mm",
                "model-frame",
                new SurfaceModelPreparationParameters(
                    SurfaceModelPreparationParameters
                        .DeterministicTriangleCentroidSampling,
                    FixtureSampleCount,
                    1e-9,
                    1e-6,
                    0.9)));
        var radians = 18.0 * Math.PI / 180.0;
        var expectedPose = new RigidPose3D(
            "mm",
            "model-frame",
            "scene-frame",
            Math.Cos(radians),
            -Math.Sin(radians),
            0.0,
            Math.Sin(radians),
            Math.Cos(radians),
            0.0,
            0.0,
            0.0,
            1.0,
            15.0,
            -7.0,
            3.0);
        var scenePoints = model.Samples
            .Select(sample =>
                expectedPose.TransformPoint(sample.Position))
            .ToArray();
        var scene = PreparedScenePreparation.Prepare(
            new PreparedScenePreparationRequest(
                "prepared-scene.performance.256",
                "Surface Match Performance Scene",
                PreparedSceneArtifact.CurrentCoordinateConvention,
                CreateQuality(scenePoints),
                scenePoints,
                new PreparedScenePreparationParameters(
                    PreparedScenePreparationParameters
                        .DeterministicEvenPointSampling,
                    FixtureSampleCount)));
        return new Fixture(model, scene, expectedPose);
    }

    private static SourceQualityReport CreateQuality(
        IReadOnlyList<SurfaceModelPoint3> points)
    {
        var contentSha256 = HashPoints(points);
        var maskBytes = new byte[(points.Count + 7) / 8];
        return new SourceQualityReport(
            SourceQualityReport.CurrentSchemaVersion,
            new SourceQualitySourceIdentity(
                "scene.measured.performance.256",
                "SYNTHETIC",
                "fixture://surface-match-performance/scene",
                checked(points.Count * 24L),
                contentSha256,
                contentSha256),
            new SourceQualityGrid(
                points.Count,
                1,
                points.Count),
            new SourceQualityCoverage(
                points.Count,
                points.Count,
                0,
                1.0,
                0.0,
                "explicit-finite-point-list",
                new SourceQualityInvalidCellMaskIdentity(
                    "synthetic-packed-mask-1.0",
                    "packed-lsb-row-major",
                    maskBytes.Length,
                    Convert.ToHexString(
                        SHA256.HashData(maskBytes)))),
            new SourceQualityHeightStatistics(
                "cartesian-z",
                points.Min(point => point.Z),
                points.Max(point => point.Z),
                points.Average(point => point.Z),
                null),
            new SourceQualityCoordinateContext(
                "mm",
                "scene-frame",
                PreparedSceneArtifact
                    .CurrentCoordinateConvention),
            "controlled-synthetic-performance-fixture",
            true,
            Enum.GetValues<SourceQualityChannel>()
                .Select(channel =>
                    new SourceQualityChannelAvailability(
                        channel,
                        channel == SourceQualityChannel.Height
                            ? SourceQualityChannelState.Available
                            : SourceQualityChannelState.Unavailable,
                        channel == SourceQualityChannel.Height
                            ? "Controlled Cartesian Z values are present."
                            : "Controlled fixture does not declare this source channel."))
                .ToArray());
    }

    private static RigidSurfacePoseSearchParameters SearchParameters(
        double minimumRotationZ,
        double maximumRotationZ,
        double rotationStepZ,
        int maximumCandidateCount) =>
        new(
            0.0, 0.0, 1.0,
            0.0, 0.0, 1.0,
            minimumRotationZ,
            maximumRotationZ,
            rotationStepZ,
            -50.0, 50.0,
            -50.0, 50.0,
            2.0, 4.0,
            0.01,
            250,
            maximumCandidateCount);

    private static string HashMesh(
        IReadOnlyList<Vector3> positions,
        IReadOnlyList<int> indices,
        IReadOnlyList<Vector3> normals)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write(positions.Count);
            foreach (var value in positions)
            {
                writer.Write(value.X);
                writer.Write(value.Y);
                writer.Write(value.Z);
            }

            writer.Write(indices.Count);
            foreach (var value in indices)
            {
                writer.Write(value);
            }

            writer.Write(normals.Count);
            foreach (var value in normals)
            {
                writer.Write(value.X);
                writer.Write(value.Y);
                writer.Write(value.Z);
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(stream.ToArray()));
    }

    private static string HashPoints(
        IReadOnlyList<SurfaceModelPoint3> points)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(
                   stream,
                   Encoding.UTF8,
                   leaveOpen: true))
        {
            writer.Write(points.Count);
            foreach (var point in points)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
                writer.Write(point.Z);
            }
        }

        return Convert.ToHexString(
            SHA256.HashData(stream.ToArray()));
    }

    private static bool PoseMatches(
        RigidPose3D? actual,
        RigidPose3D expected) =>
        actual is not null
        && Nearly(actual.RotationAngleDegrees, 18.0, 1e-8)
        && Nearly(actual.TranslationX, expected.TranslationX, 1e-8)
        && Nearly(actual.TranslationY, expected.TranslationY, 1e-8)
        && Nearly(actual.TranslationZ, expected.TranslationZ, 1e-8);

    private static bool Nearly(
        double actual,
        double expected,
        double tolerance) =>
        double.IsFinite(actual)
        && Math.Abs(actual - expected) <= tolerance;

    private static CaseResult BudgetCheck(
        string profile,
        string statistic,
        double observedMilliseconds,
        double budgetMilliseconds) =>
        Check(
            $"{profile}-{statistic}-budget",
            observedMilliseconds <= budgetMilliseconds,
            $"observedMilliseconds={FormatMilliseconds(observedMilliseconds)};budgetMilliseconds={FormatMilliseconds(budgetMilliseconds)}");

    private static string ProfileEvidence(
        ProfileMeasurement measurement) =>
        $"Profile|name={measurement.Profile.Name}|candidates={measurement.Profile.ExpectedCandidateCount}|samples={FixtureSampleCount}|outer={StatisticsEvidence(measurement.OuterMilliseconds)}|budgets=median:{FormatMilliseconds(measurement.Profile.MedianBudgetMilliseconds)},p95:{FormatMilliseconds(measurement.Profile.P95BudgetMilliseconds)},max:{FormatMilliseconds(measurement.Profile.MaximumBudgetMilliseconds)}";

    private static string StatisticsEvidence(Statistics statistics) =>
        $"minMs={FormatMilliseconds(statistics.Minimum)}|medianMs={FormatMilliseconds(statistics.Median)}|p95Ms={FormatMilliseconds(statistics.P95)}|maxMs={FormatMilliseconds(statistics.Maximum)}";

    private static string FormatMilliseconds(double milliseconds) =>
        milliseconds.ToString("F3", CultureInfo.InvariantCulture);

    private static CaseResult Check(
        string name,
        bool passed,
        string evidence) =>
        new(name, passed, evidence);

    private static readonly string[] ExpectedStages =
    [
        SurfaceMatchRuntimeReport.PoseSearchStage,
        SurfaceMatchRuntimeReport.ExecutionArtifactStage,
        SurfaceMatchRuntimeReport.AcceptanceEvaluationStage
    ];

    private sealed record Fixture(
        SurfaceModelArtifact Model,
        PreparedSceneArtifact Scene,
        RigidPose3D ExpectedPose);

    private sealed record BudgetProfile(
        string Name,
        RigidSurfacePoseSearchParameters Parameters,
        int ExpectedCandidateCount,
        double MedianBudgetMilliseconds,
        double P95BudgetMilliseconds,
        double MaximumBudgetMilliseconds);

    private sealed record ProfileMeasurement(
        BudgetProfile Profile,
        double[] OuterRunsMilliseconds,
        Statistics OuterMilliseconds,
        IReadOnlyDictionary<string, Statistics>
            InternalStageMilliseconds,
        string[] ExecutionHashes,
        string[] AssessmentHashes,
        int[] CandidateCounts,
        bool AllRuntimeReportsValid,
        bool AllDecisionsPass,
        bool AllPosesMatch,
        double CoverageRatio,
        double InlierRmse);

    private sealed record Statistics(
        double Minimum,
        double Median,
        double P95,
        double Maximum)
    {
        public static Statistics Create(
            IEnumerable<double> values)
        {
            var ordered = values.Order().ToArray();
            if (ordered.Length == 0)
            {
                throw new InvalidDataException(
                    "Performance statistics require at least one value.");
            }

            var median = ordered.Length % 2 == 0
                ? (ordered[ordered.Length / 2 - 1]
                    + ordered[ordered.Length / 2]) / 2.0
                : ordered[ordered.Length / 2];
            var p95Index = Math.Clamp(
                checked((int)Math.Ceiling(ordered.Length * 0.95)) - 1,
                0,
                ordered.Length - 1);
            return new Statistics(
                ordered[0],
                median,
                ordered[p95Index],
                ordered[^1]);
        }
    }

    private sealed record CaseResult(
        string Name,
        bool Passed,
        string Evidence);
}

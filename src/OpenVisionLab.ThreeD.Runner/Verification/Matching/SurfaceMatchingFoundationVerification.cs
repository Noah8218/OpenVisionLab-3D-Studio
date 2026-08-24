using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceMatchingFoundationVerification
{
    private const string ModelSourceSha256 =
        "D9E4D1D4082A58FDF1F0431F72A136FA76648C896EC483E6C5A73092D5B06D9D";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var artifactDirectory =
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(artifactDirectory);

        var model = SurfaceModelPreparation.Prepare(
            CreateNominalMesh(),
            new SurfaceModelPreparationRequest(
                "surface-model.nominal.asymmetric-five",
                "Known Asymmetric Five-Sample Model",
                "source.mesh.nominal.asymmetric-five",
                ModelSourceSha256,
                "mm",
                "model-frame",
                new SurfaceModelPreparationParameters(
                    SurfaceModelPreparationParameters
                        .DeterministicTriangleCentroidSampling,
                    5,
                    1e-9,
                    1e-6,
                    0.9)));
        var modelPath = Path.Combine(
            artifactDirectory,
            "known-pose.surface-model.json");
        SurfaceModelArtifactStore.Save(
            modelPath,
            model);
        var expectedPose = KnownPose();
        var fullScenePoints = model.Samples
            .Select(sample =>
                expectedPose.TransformPoint(sample.Position))
            .ToArray();
        var fullQuality = CreateQuality(
            "scene.measured.full",
            "fixture://known-pose/full",
            "scene-frame",
            fullScenePoints);
        var sceneParameters =
            new PreparedScenePreparationParameters(
                PreparedScenePreparationParameters
                    .DeterministicEvenPointSampling,
                fullScenePoints.Length);
        var sceneRequest = new PreparedScenePreparationRequest(
            "prepared-scene.known-pose.full",
            "Known Pose Full Scene",
            PreparedSceneArtifact.CurrentCoordinateConvention,
            fullQuality,
            fullScenePoints,
            sceneParameters);
        var fullScene =
            PreparedScenePreparation.Prepare(sceneRequest);
        var repeatedScene =
            PreparedScenePreparation.Prepare(sceneRequest);
        var sceneValidity =
            PreparedSceneArtifactValidator.Inspect(fullScene);

        var fullScenePath = Path.Combine(
            artifactDirectory,
            "known-pose-full.prepared-scene.json");
        PreparedSceneArtifactStore.Save(
            fullScenePath,
            fullScene);
        var loadedScene =
            PreparedSceneArtifactStore.Load(fullScenePath);

        var searchParameters =
            new RigidSurfacePoseSearchParameters(
                0.0,
                0.0,
                1.0,
                0.0,
                0.0,
                1.0,
                -45.0,
                45.0,
                15.0,
                8.0,
                12.0,
                -6.0,
                -2.0,
                1.0,
                3.0,
                1e-6,
                3,
                100);
        var firstExecution = SurfaceMatchExecutor.Execute(
            model,
            fullScene,
            searchParameters);
        var repeatedExecution = SurfaceMatchExecutor.Execute(
            model,
            fullScene,
            searchParameters);
        var firstSearch = firstExecution.PoseResult;
        var repeatedSearch = repeatedExecution.PoseResult;

        var occludedPoints =
            fullScenePoints.Take(4).ToArray();
        var occludedScene =
            PreparedScenePreparation.Prepare(
                new PreparedScenePreparationRequest(
                    "prepared-scene.known-pose.occluded",
                    "Known Pose One-Sample Occluded Scene",
                    PreparedSceneArtifact
                        .CurrentCoordinateConvention,
                    CreateQuality(
                        "scene.measured.occluded",
                        "fixture://known-pose/occluded",
                        "scene-frame",
                        occludedPoints),
                    occludedPoints,
                    sceneParameters with
                    {
                        MaximumSampleCount =
                            occludedPoints.Length
                    }));
        var occludedCoverage =
            SurfaceCoverageScorer.Evaluate(
                model,
                occludedScene,
                expectedPose,
                searchParameters
                    .MaximumCorrespondenceDistance);
        var occludedScenePath = Path.Combine(
            artifactDirectory,
            "known-pose-occluded.prepared-scene.json");
        PreparedSceneArtifactStore.Save(
            occludedScenePath,
            occludedScene);

        var invalidPointRejected = ThrowsInvalidData(
            () => PreparedScenePreparation.Prepare(
                sceneRequest with
                {
                    FinitePoints =
                    [
                        .. fullScenePoints.Take(4),
                        new SurfaceModelPoint3(
                            double.NaN,
                            0.0,
                            0.0)
                    ]
                }),
            out var invalidPointEvidence);
        var countMismatchRejected = ThrowsInvalidData(
            () => PreparedScenePreparation.Prepare(
                sceneRequest with
                {
                    FinitePoints =
                        fullScenePoints.Take(4).ToArray()
                }),
            out var countMismatchEvidence);
        var tamperedQualityRejected = ThrowsInvalidData(
            () => PreparedScenePreparation.Prepare(
                sceneRequest with
                {
                    SourceQuality = fullQuality with
                    {
                        Source = fullQuality.Source with
                        {
                            ContentSha256 =
                                fullQuality.Source
                                    .ContentSha256
                                    .ToLowerInvariant()
                        }
                    }
                }),
            out var tamperedQualityEvidence);
        var coordinateMismatchRejected = ThrowsInvalidData(
            () => PreparedScenePreparation.Prepare(
                sceneRequest with
                {
                    SourceQuality = fullQuality with
                    {
                        Coordinates =
                            fullQuality.Coordinates with
                            {
                                CoordinateConvention =
                                    "incompatible-coordinate-frame"
                            }
                    }
                }),
            out var coordinateMismatchEvidence);
        var undefinedChannelRejected = ThrowsInvalidData(
            () => PreparedScenePreparation.Prepare(
                sceneRequest with
                {
                    SourceQuality = fullQuality with
                    {
                        Channels =
                        [
                            .. fullQuality.Channels.Take(6),
                            fullQuality.Channels[6] with
                            {
                                Channel =
                                    (SourceQualityChannel)999
                            }
                        ]
                    }
                }),
            out var undefinedChannelEvidence);

        var tamperedScene = fullScene with
        {
            Points =
            [
                fullScene.Points[0] with
                {
                    X = fullScene.Points[0].X + 0.25
                },
                .. fullScene.Points.Skip(1)
            ]
        };
        var tamperedSceneValidity =
            PreparedSceneArtifactValidator.Inspect(tamperedScene);
        var tamperedSaveRejected = ThrowsInvalidData(
            () => PreparedSceneArtifactStore.Save(
                fullScenePath,
                tamperedScene),
            out var tamperedSaveEvidence);
        var preservedScene =
            PreparedSceneArtifactStore.Load(fullScenePath);

        var corruptPath = Path.Combine(
            artifactDirectory,
            "corrupt.prepared-scene.json");
        File.WriteAllText(
            corruptPath,
            "{\"schemaVersion\":\"1.0\"",
            new UTF8Encoding(false));
        var corruptRejected = ThrowsInvalidData(
            () => PreparedSceneArtifactStore.Load(corruptPath),
            out var corruptEvidence);

        var noMatchExecution = SurfaceMatchExecutor.Execute(
            model,
            fullScene,
            searchParameters with
            {
                MinimumTranslationX = -1.0,
                MaximumTranslationX = 1.0,
                MinimumTranslationY = -1.0,
                MaximumTranslationY = 1.0,
                MinimumTranslationZ = -1.0,
                MaximumTranslationZ = 1.0
            });
        var noMatch = noMatchExecution.PoseResult;
        var candidateLimitRejected = ThrowsInvalidData(
            () => RigidSurfacePoseSearch.Execute(
                model,
                fullScene,
                searchParameters with
                {
                    MinimumRotationZDegrees = -180.0,
                    MaximumRotationZDegrees = 180.0,
                    RotationStepZDegrees = 1.0,
                    MaximumCandidateCount = 100
                }),
            out var candidateLimitEvidence);
        var excessiveAxisResolutionRejected = ThrowsInvalidData(
            () => RigidSurfacePoseSearch.Execute(
                model,
                fullScene,
                searchParameters with
                {
                    MinimumRotationZDegrees = -180.0,
                    MaximumRotationZDegrees = 180.0,
                    RotationStepZDegrees = 1e-9,
                    MaximumCandidateCount =
                        RigidSurfacePoseSearch
                            .AbsoluteMaximumCandidateCount
                }),
            out var excessiveAxisResolutionEvidence);
        var overflowingDistributionScene =
            fullScene with
            {
                SourceQuality = fullQuality with
                {
                    Height = fullQuality.Height with
                    {
                        Distribution =
                            new SourceQualityDistribution(
                                2,
                                0,
                                [long.MaxValue, 1])
                    }
                }
            };
        var overflowingDistributionValidity =
            PreparedSceneArtifactValidator.Inspect(
                overflowingDistributionScene);
        var invalidDistanceRejected = ThrowsInvalidData(
            () => SurfaceCoverageScorer.Evaluate(
                model,
                fullScene,
                expectedPose,
                0.0),
            out var invalidDistanceEvidence);

        var resultPath = Path.Combine(
            artifactDirectory,
            "known-pose-result.json");
        File.WriteAllText(
            resultPath,
            JsonSerializer.Serialize(
                firstSearch,
                JsonOptions),
            new UTF8Encoding(false));
        var executionPath = Path.Combine(
            artifactDirectory,
            "known-pose.surface-match-execution.json");
        SurfaceMatchExecutionArtifactStore.Save(
            executionPath,
            firstExecution);
        var loadedExecution =
            SurfaceMatchExecutionArtifactStore.Load(executionPath);
        var executionValidity =
            SurfaceMatchExecutionArtifactValidator.Inspect(
                firstExecution);
        var tamperedExecution =
            firstExecution with
            {
                Overlay = firstExecution.Overlay! with
                {
                    TransformedPoints =
                    [
                        firstExecution.Overlay!.TransformedPoints[0]
                            with
                            {
                                X = firstExecution.Overlay
                                    .TransformedPoints[0].X + 0.5
                            },
                        .. firstExecution.Overlay
                            .TransformedPoints.Skip(1)
                    ]
                }
            };
        var tamperedExecutionRejected = ThrowsInvalidData(
            () => SurfaceMatchExecutionArtifactStore.Save(
                executionPath,
                tamperedExecution),
            out var tamperedExecutionEvidence);
        var preservedExecution =
            SurfaceMatchExecutionArtifactStore.Load(executionPath);

        var recoveredPose = firstSearch.Pose;
        var cases = new List<(
            string Name,
            bool Passed,
            string Evidence)>
        {
            Check(
                "prepared-scene-valid",
                sceneValidity.IsValid
                && sceneValidity.PointCount == 5
                && sceneValidity.ValidSampleCount == 5,
                sceneValidity.Evidence),
            Check(
                "prepared-scene-source-quality-identity",
                sceneValidity.SourceQualityIdentityValid
                && fullScene.SourceQualitySha256
                    == SourceQualityReportContentIdentity
                        .CalculateSha256(fullQuality),
                $"qualitySha={fullScene.SourceQualitySha256}"),
            Check(
                "prepared-scene-stable-content-identity",
                fullScene.ContentSha256
                    == repeatedScene.ContentSha256,
                $"first={fullScene.ContentSha256};repeated={repeatedScene.ContentSha256}"),
            Check(
                "prepared-scene-save-load-roundtrip",
                loadedScene.ContentSha256
                    == fullScene.ContentSha256
                && loadedScene.Points
                    .SequenceEqual(fullScene.Points)
                && loadedScene.Samples
                    .SequenceEqual(fullScene.Samples),
                $"path={fullScenePath};sha256={loadedScene.ContentSha256}"),
            Check(
                "non-finite-scene-point-rejected",
                invalidPointRejected
                && invalidPointEvidence.Contains(
                    "finite",
                    StringComparison.OrdinalIgnoreCase),
                invalidPointEvidence),
            Check(
                "source-quality-count-mismatch-rejected",
                countMismatchRejected
                && countMismatchEvidence.Contains(
                    "point count",
                    StringComparison.OrdinalIgnoreCase),
                countMismatchEvidence),
            Check(
                "noncanonical-source-quality-rejected",
                tamperedQualityRejected
                && tamperedQualityEvidence.Contains(
                    "uppercase SHA-256",
                    StringComparison.Ordinal),
                tamperedQualityEvidence),
            Check(
                "coordinate-convention-mismatch-rejected",
                coordinateMismatchRejected
                && coordinateMismatchEvidence.Contains(
                    "coordinate convention",
                    StringComparison.OrdinalIgnoreCase),
                coordinateMismatchEvidence),
            Check(
                "undefined-source-channel-rejected",
                undefinedChannelRejected
                && undefinedChannelEvidence.Contains(
                    "each channel exactly once",
                    StringComparison.OrdinalIgnoreCase),
                undefinedChannelEvidence),
            Check(
                "tampered-scene-content-rejected",
                !tamperedSceneValidity.IsValid
                && !tamperedSceneValidity
                    .ContentIdentityValid,
                tamperedSceneValidity.Evidence),
            Check(
                "rejected-save-preserves-prior-scene",
                tamperedSaveRejected
                && preservedScene.ContentSha256
                    == fullScene.ContentSha256,
                $"{tamperedSaveEvidence}|preserved={preservedScene.ContentSha256}"),
            Check(
                "malformed-scene-json-rejected",
                corruptRejected
                && corruptEvidence.Contains(
                    "malformed",
                    StringComparison.OrdinalIgnoreCase),
                corruptEvidence),
            Check(
                "bounded-search-recovers-pose",
                firstSearch.State
                    == RigidSurfacePoseSearchState.Matched
                && recoveredPose is not null
                && firstSearch.EvaluatedCandidateCount == 7,
                $"state={firstSearch.State};candidates={firstSearch.EvaluatedCandidateCount}"),
            Check(
                "recovered-rotation-is-known-yaw",
                recoveredPose is not null
                && Nearly(recoveredPose.M11, Math.Sqrt(3.0) / 2.0)
                && Nearly(recoveredPose.M12, -0.5)
                && Nearly(recoveredPose.M21, 0.5)
                && Nearly(recoveredPose.M22, Math.Sqrt(3.0) / 2.0)
                && Nearly(recoveredPose.RotationAngleDegrees, 30.0),
                PoseEvidence(recoveredPose)),
            Check(
                "recovered-translation-is-known",
                recoveredPose is not null
                && Nearly(recoveredPose.TranslationX, 10.0)
                && Nearly(recoveredPose.TranslationY, -4.0)
                && Nearly(recoveredPose.TranslationZ, 2.0),
                PoseEvidence(recoveredPose)),
            Check(
                "recovered-transform-is-rigid",
                recoveredPose?.IsRigid(1e-12) == true,
                PoseEvidence(recoveredPose)),
            Check(
                "full-scene-coverage-is-one",
                firstSearch.Coverage
                    .MatchedModelSampleCount == 5
                && firstSearch.Coverage
                    .UnmatchedModelSampleCount == 0
                && Nearly(
                    firstSearch.Coverage.CoverageRatio,
                    1.0)
                && firstSearch.Coverage.InlierRmse
                    is <= 1e-12,
                firstSearch.Coverage.Evidence),
            Check(
                "coverage-semantics-are-explicit",
                firstSearch.Coverage.Semantics
                    == SurfaceCoverageEvaluation
                        .CurrentSemantics
                && firstSearch.Coverage.Matches
                    .Select(match =>
                        match.SceneSampleOrder)
                    .Distinct()
                    .Count()
                    == firstSearch.Coverage.Matches.Length,
                firstSearch.Coverage.Evidence),
            Check(
                "occluded-scene-coverage-is-four-fifths",
                occludedCoverage.MatchedModelSampleCount
                    == 4
                && occludedCoverage
                    .UnmatchedModelSampleCount == 1
                && Nearly(
                    occludedCoverage.CoverageRatio,
                    0.8)
                && occludedCoverage.InlierRmse
                    is <= 1e-12,
                occludedCoverage.Evidence),
            Check(
                "pose-search-is-repeatable",
                firstSearch.ContentSha256
                    == repeatedSearch.ContentSha256,
                $"first={firstSearch.ContentSha256};repeated={repeatedSearch.ContentSha256}"),
            Check(
                "surface-match-execution-is-valid",
                executionValidity.IsValid
                && executionValidity.PoseIdentityValid
                && executionValidity.OverlayIdentityValid
                && executionValidity.ExecutionIdentityValid,
                executionValidity.Evidence),
            Check(
                "transformed-overlay-preserves-model-topology",
                firstExecution.Overlay is { } overlay
                && overlay.TransformedPoints.Length
                    == model.Points.Length
                && overlay.Triangles
                    .SequenceEqual(model.Triangles)
                && overlay.TransformedPoints
                    .SequenceEqual(
                        model.Points.Select(
                            firstSearch.Pose!.TransformPoint)),
                $"points={firstExecution.Overlay?.TransformedPoints.Length};triangles={firstExecution.Overlay?.Triangles.Length}"),
            Check(
                "surface-match-overlay-and-execution-are-repeatable",
                firstExecution.ContentSha256
                    == repeatedExecution.ContentSha256
                && firstExecution.Overlay?.ContentSha256
                    == repeatedExecution.Overlay?.ContentSha256,
                $"execution={firstExecution.ContentSha256};overlay={firstExecution.Overlay?.ContentSha256}"),
            Check(
                "surface-match-execution-save-load-roundtrip",
                loadedExecution.ContentSha256
                    == firstExecution.ContentSha256
                && loadedExecution.Overlay?.ContentSha256
                    == firstExecution.Overlay?.ContentSha256,
                $"path={executionPath};sha256={loadedExecution.ContentSha256}"),
            Check(
                "tampered-overlay-save-fails-closed",
                tamperedExecutionRejected
                && preservedExecution.ContentSha256
                    == firstExecution.ContentSha256,
                $"{tamperedExecutionEvidence};preserved={preservedExecution.ContentSha256}"),
            Check(
                "pose-result-content-identity",
                firstSearch.ContentSha256
                    == RigidSurfacePoseSearchResult
                        .CalculateContentSha256(
                            firstSearch),
                $"sha256={firstSearch.ContentSha256}"),
            Check(
                "out-of-bounds-translation-fails-closed",
                noMatch.State
                    == RigidSurfacePoseSearchState.NoMatch
                && noMatch.Pose is null
                && noMatch.RejectionReason.Contains(
                    "bounds",
                    StringComparison.OrdinalIgnoreCase),
                $"state={noMatch.State};reason={noMatch.RejectionReason}"),
            Check(
                "no-match-has-no-overlay",
                noMatchExecution.Overlay is null
                && SurfaceMatchExecutionArtifactValidator
                    .Inspect(noMatchExecution)
                    .IsValid,
                $"state={noMatchExecution.PoseResult.State};overlay={(noMatchExecution.Overlay is null ? "none" : "unexpected")}"),
            Check(
                "candidate-budget-fails-closed",
                candidateLimitRejected
                && candidateLimitEvidence.Contains(
                    "exceeds",
                    StringComparison.OrdinalIgnoreCase),
                candidateLimitEvidence),
            Check(
                "excessive-axis-resolution-fails-closed",
                excessiveAxisResolutionRejected
                && excessiveAxisResolutionEvidence.Contains(
                    "supported limit",
                    StringComparison.OrdinalIgnoreCase),
                excessiveAxisResolutionEvidence),
            Check(
                "overflowing-distribution-fails-closed",
                !overflowingDistributionValidity.IsValid
                && overflowingDistributionValidity.Errors.Any(
                    error => error.Contains(
                        "distribution",
                        StringComparison.OrdinalIgnoreCase)),
                overflowingDistributionValidity.Evidence),
            Check(
                "invalid-coverage-distance-rejected",
                invalidDistanceRejected
                && invalidDistanceEvidence.Contains(
                    "positive",
                    StringComparison.OrdinalIgnoreCase),
                invalidDistanceEvidence),
            Check(
                "prepared-scene-does-not-mutate-source",
                fullScenePoints
                    .SequenceEqual(
                        model.Samples.Select(sample =>
                            expectedPose.TransformPoint(
                                sample.Position))),
                $"sourcePoints={fullScenePoints.Length};scenePoints={fullScene.Points.Length}"),
            Check(
                "matching-is-decision-free",
                !firstSearch.Coverage.Evidence.Contains(
                    "Pass",
                    StringComparison.OrdinalIgnoreCase)
                && !firstSearch.Coverage.Evidence.Contains(
                    "Fail",
                    StringComparison.OrdinalIgnoreCase),
                firstSearch.Coverage.Evidence)
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceMatchingFoundationVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            $"Contract|sceneSchema={PreparedSceneArtifact.CurrentSchemaVersion}|sceneCoordinate={PreparedSceneArtifact.CurrentCoordinateConvention}|sceneSampling={PreparedScenePreparationParameters.DeterministicEvenPointSampling}|poseSchema={RigidSurfacePoseSearchResult.CurrentSchemaVersion}|solver={RigidSurfacePoseSearchResult.CurrentSolverVersion}|coverage={SurfaceCoverageEvaluation.CurrentSemantics}|executionSchema={SurfaceMatchExecutionArtifact.CurrentSchemaVersion}|overlay={SurfaceMatchOverlayArtifact.CurrentSemantics}|decision=false|ui=false|sourceMutation=false",
            $"SurfaceModel|path={modelPath}|id={model.ArtifactId}|sha256={model.ContentSha256}|points={model.Points.Length}|triangles={model.Triangles.Length}|samples={model.Samples.Length}",
            $"PreparedScene|path={fullScenePath}|id={fullScene.ArtifactId}|sha256={fullScene.ContentSha256}|sourceQualitySha256={fullScene.SourceQualitySha256}|points={fullScene.Points.Length}|samples={fullScene.Samples.Length}",
            $"PoseResult|path={resultPath}|sha256={firstSearch.ContentSha256}|state={firstSearch.State}|candidates={firstSearch.EvaluatedCandidateCount}|coverage={firstSearch.Coverage.CoverageRatio:G17}|rmse={firstSearch.Coverage.InlierRmse:G17}",
            $"Execution|path={executionPath}|sha256={firstExecution.ContentSha256}|overlaySha256={firstExecution.Overlay?.ContentSha256}|acceptancePolicy=none",
            $"OccludedScene|path={occludedScenePath}|sha256={occludedScene.ContentSha256}|coverage={occludedCoverage.CoverageRatio:G17}|matched={occludedCoverage.MatchedModelSampleCount}/{occludedCoverage.ModelSampleCount}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        Console.WriteLine(
            $"Surface matching foundation verification: "
            + $"{(passed == cases.Count ? "PASS" : "FAIL")} "
            + $"({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static ImportedMesh CreateNominalMesh()
    {
        var centers = new[]
        {
            new Vector3(0.0f, 0.0f, 0.0f),
            new Vector3(2.0f, 0.0f, 0.0f),
            new Vector3(0.0f, 3.0f, 0.0f),
            new Vector3(4.0f, 1.0f, 0.0f),
            new Vector3(1.0f, 5.0f, 0.0f)
        };
        var positions = new List<Vector3>();
        var indices = new List<int>();
        var normals = new List<Vector3>();
        foreach (var center in centers)
        {
            var offset = positions.Count;
            positions.Add(
                center + new Vector3(-0.1f, -0.1f, 0.0f));
            positions.Add(
                center + new Vector3(0.1f, -0.1f, 0.0f));
            positions.Add(
                center + new Vector3(0.0f, 0.2f, 0.0f));
            indices.Add(offset);
            indices.Add(offset + 1);
            indices.Add(offset + 2);
            normals.Add(Vector3.UnitZ);
            normals.Add(Vector3.UnitZ);
            normals.Add(Vector3.UnitZ);
        }

        return ImportedMesh.CreateTriangleMesh(
            "known-asymmetric-five.stl",
            "Known Asymmetric Five",
            "STL",
            positions.ToArray(),
            indices.ToArray(),
            normals.ToArray());
    }

    private static RigidPose3D KnownPose()
    {
        var cosine = Math.Sqrt(3.0) / 2.0;
        return new RigidPose3D(
            "mm",
            "model-frame",
            "scene-frame",
            cosine,
            -0.5,
            0.0,
            0.5,
            cosine,
            0.0,
            0.0,
            0.0,
            1.0,
            10.0,
            -4.0,
            2.0);
    }

    internal static SourceQualityReport CreateQuality(
        string entityId,
        string path,
        string frameId,
        IReadOnlyList<SurfaceModelPoint3> points)
    {
        var contentSha256 = HashPoints(points);
        var maskBytes = new byte[(points.Count + 7) / 8];
        var minimum = points.Min(point => point.Z);
        var maximum = points.Max(point => point.Z);
        var mean = points.Average(point => point.Z);
        return new SourceQualityReport(
            SourceQualityReport.LegacySchemaVersion,
            new SourceQualitySourceIdentity(
                entityId,
                "SYNTHETIC",
                path,
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
                minimum,
                maximum,
                mean,
                null),
            new SourceQualityCoordinateContext(
                "mm",
                frameId,
                PreparedSceneArtifact
                    .CurrentCoordinateConvention),
            "controlled-synthetic-known-pose",
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

    private static bool Nearly(
        double actual,
        double expected,
        double tolerance = 1e-9) =>
        double.IsFinite(actual)
        && Math.Abs(actual - expected) <= tolerance;

    private static string PoseEvidence(RigidPose3D? pose) =>
        pose is null
            ? "pose=unavailable"
            : $"translation=({pose.TranslationX:G17},{pose.TranslationY:G17},{pose.TranslationZ:G17});rotationDegrees={pose.RotationAngleDegrees:G17};rigid={pose.IsRigid(1e-12)}";

    private static bool ThrowsInvalidData(
        Action action,
        out string evidence)
    {
        try
        {
            action();
            evidence = "No exception.";
            return false;
        }
        catch (Exception exception)
            when (exception is InvalidDataException
                  or ArgumentOutOfRangeException)
        {
            evidence = exception.Message;
            return true;
        }
    }

    private static (
        string Name,
        bool Passed,
        string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}

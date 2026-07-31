using System.Security.Cryptography;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceEdgeMatchingVerification
{
    private const string SourceSha256 =
        "0E15C30B584094EFAE1E28A7766D35E178952BAC19A84BF62B9B57A86B784ED7";

    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var model = CreateModel();
        var heightScene = CreateScene(
            "prepared-scene.edge-height",
            "Height Edge Scene",
            (x, y) => x is >= 0 and <= 6 && y is >= 0 and <= 6
                ? 1.0
                : 0.0);
        var flatScene = CreateScene(
            "prepared-scene.flat-background",
            "Flat Background Scene",
            (_, _) => 1.0);
        var modelParameters = new ModelSurfaceEdgeExtractionParameters(
            ModelSurfaceEdgeExtractionParameters
                .TopologyBoundaryAndCreaseMethod,
            0.1,
            30.0,
            true);
        var sceneParameters = new SceneSurfaceEdgeExtractionParameters(
            SceneSurfaceEdgeExtractionParameters
                .OrganizedHeightStepMethod,
            0.5,
            true,
            true);
        var modelEdges = ModelSurfaceEdgeExtractor.Extract(
            model,
            modelParameters);
        var repeatedModelEdges = ModelSurfaceEdgeExtractor.Extract(
            model,
            modelParameters);
        var heightSceneEdges = SceneSurfaceEdgeExtractor.Extract(
            heightScene,
            sceneParameters);
        var flatSceneEdges = SceneSurfaceEdgeExtractor.Extract(
            flatScene,
            sceneParameters);
        var execution = CreateIdentityExecution(model, heightScene);
        var heightScore = SurfaceAndEdgeMatchScorer.Evaluate(
            execution,
            modelEdges,
            heightSceneEdges,
            0.01);
        var repeatedHeightScore = SurfaceAndEdgeMatchScorer.Evaluate(
            execution,
            modelEdges,
            heightSceneEdges,
            0.01);

        var flatExecution = CreateIdentityExecution(model, flatScene);
        var flatScore = SurfaceAndEdgeMatchScorer.Evaluate(
            flatExecution,
            modelEdges,
            flatSceneEdges,
            0.01);

        var modelPath = Path.Combine(directory, "edge-score.surface-model.json");
        var scenePath = Path.Combine(directory, "edge-score-height.prepared-scene.json");
        var flatScenePath = Path.Combine(directory, "edge-score-flat.prepared-scene.json");
        var executionPath = Path.Combine(directory, "edge-score.surface-match-execution.json");
        var modelEdgePath = Path.Combine(directory, "edge-score.model-edges.json");
        var sceneEdgePath = Path.Combine(directory, "edge-score.height-scene-edges.json");
        var flatSceneEdgePath = Path.Combine(directory, "edge-score.flat-scene-edges.json");
        var scorePath = Path.Combine(directory, "edge-score.height.score.json");
        var flatScorePath = Path.Combine(directory, "edge-score.flat.score.json");
        SurfaceModelArtifactStore.Save(modelPath, model);
        PreparedSceneArtifactStore.Save(scenePath, heightScene);
        PreparedSceneArtifactStore.Save(flatScenePath, flatScene);
        SurfaceMatchExecutionArtifactStore.Save(executionPath, execution);
        SurfaceEdgeArtifactStore.SaveModel(modelEdgePath, modelEdges);
        SurfaceEdgeArtifactStore.SaveScene(sceneEdgePath, heightSceneEdges);
        SurfaceEdgeArtifactStore.SaveScene(flatSceneEdgePath, flatSceneEdges);
        SurfaceEdgeArtifactStore.SaveScore(scorePath, heightScore);
        SurfaceEdgeArtifactStore.SaveScore(flatScorePath, flatScore);

        var savedScoreBytes = File.ReadAllBytes(scorePath);
        var invalidSaveRejected = ThrowsInvalidData(
            () => SurfaceEdgeArtifactStore.SaveScore(
                scorePath,
                heightScore with
                {
                    ContentSha256 = new string('B', 64)
                }),
            out var invalidSaveEvidence);
        var priorScorePreserved = savedScoreBytes.SequenceEqual(
            File.ReadAllBytes(scorePath));
        var incompleteSceneRejected = ThrowsInvalidData(
            () => SceneSurfaceEdgeExtractor.Extract(
                heightScene with
                {
                    Points = heightScene.Points.Take(80).ToArray()
                },
                sceneParameters),
            out var incompleteSceneEvidence);
        var nonManifoldRejected = ThrowsInvalidData(
            () => ModelSurfaceEdgeExtractor.Extract(
                CreateNonManifoldModel(),
                modelParameters),
            out var nonManifoldEvidence);
        var mismatchedSceneRejected = ThrowsInvalidData(
            () => SurfaceAndEdgeMatchScorer.Evaluate(
                execution,
                modelEdges,
                flatSceneEdges,
                0.01),
            out var mismatchedSceneEvidence);
        var tamperedSurfaceLink = heightScore with
        {
            SurfaceScore = heightScore.SurfaceScore with
            {
                PoseResultContentSha256 = new string('C', 64)
            }
        };
        tamperedSurfaceLink = tamperedSurfaceLink with
        {
            ContentSha256 = SurfaceAndEdgeMatchScoreArtifact
                .CalculateContentSha256(tamperedSurfaceLink)
        };
        var tamperedSurfaceLinkRejected =
            SurfaceEdgeArtifactValidator
                .Inspect(tamperedSurfaceLink, execution)
                .IsValid == false;

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "model-edge-artifact-valid",
                SurfaceEdgeArtifactValidator.Inspect(modelEdges).IsValid,
                SurfaceEdgeArtifactValidator.Inspect(modelEdges).Evidence),
            Check(
                "model-boundary-edges-exclude-flat-diagonal",
                modelEdges.Edges.Length == 4
                && modelEdges.Edges.All(edge => edge.Kind == ModelSurfaceEdgeKind.Boundary),
                $"edges={modelEdges.Edges.Length};kinds={string.Join(',', modelEdges.Edges.Select(edge => edge.Kind))}"),
            Check(
                "model-edge-repeatability",
                modelEdges.ContentSha256 == repeatedModelEdges.ContentSha256,
                $"first={modelEdges.ContentSha256};repeat={repeatedModelEdges.ContentSha256}"),
            Check(
                "organized-height-scene-edge-artifact-valid",
                SurfaceEdgeArtifactValidator.Inspect(heightSceneEdges).IsValid,
                SurfaceEdgeArtifactValidator.Inspect(heightSceneEdges).Evidence),
            Check(
                "height-scene-perimeter-edges",
                heightSceneEdges.Edges.Length == 28,
                $"edges={heightSceneEdges.Edges.Length}"),
            Check(
                "flat-scene-has-no-height-edges",
                flatSceneEdges.Edges.Length == 0
                && SurfaceEdgeArtifactValidator.Inspect(flatSceneEdges).IsValid,
                $"edges={flatSceneEdges.Edges.Length}"),
            Check(
                "surface-score-does-not-distinguish-background",
                Nearly(execution.PoseResult.Coverage.CoverageRatio, 1.0)
                && Nearly(flatExecution.PoseResult.Coverage.CoverageRatio, 1.0),
                $"height={execution.PoseResult.Coverage.CoverageRatio:G17};flat={flatExecution.PoseResult.Coverage.CoverageRatio:G17}"),
            Check(
                "height-edge-score-full",
                heightScore.EdgeScore.MatchedModelEdgeCount == 4
                && Nearly(heightScore.EdgeScore.CoverageRatio, 1.0)
                && Nearly(heightScore.EdgeScore.InlierRmse ?? double.NaN, 0.0),
                heightScore.EdgeScore.Evidence),
            Check(
                "flat-background-edge-score-zero",
                flatScore.EdgeScore.MatchedModelEdgeCount == 0
                && Nearly(flatScore.EdgeScore.CoverageRatio, 0.0)
                && flatScore.EdgeScore.InlierRmse is null,
                flatScore.EdgeScore.Evidence),
            Check(
                "surface-and-edge-channels-remain-separate",
                Nearly(heightScore.SurfaceScore.CoverageRatio, 1.0)
                && Nearly(flatScore.SurfaceScore.CoverageRatio, 1.0)
                && Nearly(heightScore.EdgeScore.CoverageRatio, 1.0)
                && Nearly(flatScore.EdgeScore.CoverageRatio, 0.0),
                $"height={heightScore.SurfaceScore.CoverageRatio:G17}/{heightScore.EdgeScore.CoverageRatio:G17};flat={flatScore.SurfaceScore.CoverageRatio:G17}/{flatScore.EdgeScore.CoverageRatio:G17}"),
            Check(
                "score-has-no-acceptance-policy",
                heightScore.Semantics.Contains("no-acceptance", StringComparison.Ordinal)
                && heightScore.EdgeScore.Semantics.Contains("one-way", StringComparison.Ordinal),
                $"artifact={heightScore.Semantics};edge={heightScore.EdgeScore.Semantics}"),
            Check(
                "score-repeatability",
                heightScore.ContentSha256 == repeatedHeightScore.ContentSha256,
                $"first={heightScore.ContentSha256};repeat={repeatedHeightScore.ContentSha256}"),
            Check(
                "model-edge-store-roundtrip",
                SurfaceEdgeArtifactStore.LoadModel(modelEdgePath).ContentSha256
                    == modelEdges.ContentSha256,
                modelEdgePath),
            Check(
                "scene-edge-store-roundtrip",
                SurfaceEdgeArtifactStore.LoadScene(sceneEdgePath).ContentSha256
                    == heightSceneEdges.ContentSha256,
                sceneEdgePath),
            Check(
                "score-store-roundtrip",
                SurfaceEdgeArtifactStore.LoadScore(scorePath).ContentSha256
                    == heightScore.ContentSha256,
                scorePath),
            Check(
                "invalid-score-save-rejected-and-prior-preserved",
                invalidSaveRejected && priorScorePreserved,
                $"rejected={invalidSaveRejected};preserved={priorScorePreserved};detail={invalidSaveEvidence}"),
            Check(
                "incomplete-organized-scene-rejected",
                incompleteSceneRejected,
                incompleteSceneEvidence),
            Check(
                "non-manifold-model-rejected",
                nonManifoldRejected,
                nonManifoldEvidence),
            Check(
                "mismatched-scene-identity-rejected",
                mismatchedSceneRejected,
                mismatchedSceneEvidence),
            Check(
                "tampered-surface-execution-link-rejected",
                tamperedSurfaceLinkRejected,
                SurfaceEdgeArtifactValidator
                    .Inspect(tamperedSurfaceLink, execution)
                    .Evidence),
            Check(
                "score-does-not-mutate-execution",
                SurfaceMatchExecutionArtifactStore.Load(executionPath).ContentSha256
                    == execution.ContentSha256,
                execution.ContentSha256)
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceEdgeMatchingVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            $"Model|path={modelPath}|sha256={model.ContentSha256}",
            $"Scene|path={scenePath}|sha256={heightScene.ContentSha256}",
            $"Execution|path={executionPath}|sha256={execution.ContentSha256}|surfaceCoverage={execution.PoseResult.Coverage.CoverageRatio:G17}",
            $"ModelEdges|path={modelEdgePath}|sha256={modelEdges.ContentSha256}|count={modelEdges.Edges.Length}",
            $"SceneEdges|path={sceneEdgePath}|sha256={heightSceneEdges.ContentSha256}|count={heightSceneEdges.Edges.Length}",
            $"Score|path={scorePath}|sha256={heightScore.ContentSha256}|surface={heightScore.SurfaceScore.CoverageRatio:G17}|edge={heightScore.EdgeScore.CoverageRatio:G17}",
            $"FlatScore|path={flatScorePath}|sha256={flatScore.ContentSha256}|surface={flatScore.SurfaceScore.CoverageRatio:G17}|edge={flatScore.EdgeScore.CoverageRatio:G17}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        Console.WriteLine(
            $"Surface edge matching verification: {(passed == cases.Count ? "PASS" : "FAIL")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static SurfaceModelArtifact CreateModel() =>
        CreateModel(
            "surface-model.edge-square",
            [
                new SurfaceModelPoint3(0.0, 0.0, 1.0),
                new SurfaceModelPoint3(6.0, 0.0, 1.0),
                new SurfaceModelPoint3(6.0, 6.0, 1.0),
                new SurfaceModelPoint3(0.0, 6.0, 1.0)
            ],
            [
                new SurfaceModelTriangle(0, 1, 2),
                new SurfaceModelTriangle(0, 2, 3)
            ]);

    private static SurfaceModelArtifact CreateNonManifoldModel() =>
        CreateModel(
            "surface-model.non-manifold",
            [
                new SurfaceModelPoint3(0.0, 0.0, 1.0),
                new SurfaceModelPoint3(6.0, 0.0, 1.0),
                new SurfaceModelPoint3(3.0, 3.0, 1.0),
                new SurfaceModelPoint3(3.0, -3.0, 1.0),
                new SurfaceModelPoint3(3.0, 6.0, 1.0)
            ],
            [
                new SurfaceModelTriangle(0, 1, 2),
                new SurfaceModelTriangle(1, 0, 3),
                new SurfaceModelTriangle(0, 1, 4)
            ]);

    private static SurfaceModelArtifact CreateModel(
        string artifactId,
        SurfaceModelPoint3[] points,
        SurfaceModelTriangle[] triangles)
    {
        var preparation = new SurfaceModelPreparationParameters(
            SurfaceModelPreparationParameters
                .DeterministicTriangleCentroidSampling,
            triangles.Length,
            1e-9,
            1e-6,
            0.9);
        var normals = points
            .Select(_ => new SurfaceModelPoint3(0.0, 0.0, 1.0))
            .ToArray();
        var samples = triangles
            .Select((triangle, order) =>
            {
                var first = points[triangle.FirstPointIndex];
                var second = points[triangle.SecondPointIndex];
                var third = points[triangle.ThirdPointIndex];
                return new SurfaceModelSample(
                    order,
                    order,
                    new SurfaceModelPoint3(
                        (first.X + second.X + third.X) / 3.0,
                        (first.Y + second.Y + third.Y) / 3.0,
                        (first.Z + second.Z + third.Z) / 3.0),
                    new SurfaceModelPoint3(0.0, 0.0, 1.0));
            })
            .ToArray();
        return SurfaceModelArtifact.Create(
            artifactId,
            artifactId,
            $"source.{artifactId}",
            SourceSha256,
            "SYNTHETIC",
            "mm",
            "edge-model-frame",
            preparation,
            points,
            triangles,
            normals,
            samples);
    }

    private static PreparedSceneArtifact CreateScene(
        string artifactId,
        string name,
        Func<int, int, double> height)
    {
        const int width = 9;
        const int gridHeight = 9;
        var points = new List<SurfaceModelPoint3>(width * gridHeight);
        for (var row = 0; row < gridHeight; row++)
        {
            var y = row - 1;
            for (var column = 0; column < width; column++)
            {
                var x = column - 1;
                points.Add(new SurfaceModelPoint3(x, y, height(x, y)));
            }
        }

        return PreparedScenePreparation.Prepare(
            new PreparedScenePreparationRequest(
                artifactId,
                name,
                PreparedSceneArtifact.CurrentCoordinateConvention,
                CreateQuality(artifactId, points, width, gridHeight),
                points,
                new PreparedScenePreparationParameters(
                    PreparedScenePreparationParameters
                        .DeterministicEvenPointSampling,
                    points.Count)));
    }

    private static SourceQualityReport CreateQuality(
        string entityId,
        IReadOnlyList<SurfaceModelPoint3> points,
        int width,
        int height)
    {
        var contentSha256 = HashPoints(points);
        var maskBytes = new byte[(points.Count + 7) / 8];
        return new SourceQualityReport(
            SourceQualityReport.CurrentSchemaVersion,
            new SourceQualitySourceIdentity(
                entityId,
                "SYNTHETIC",
                $"fixture://surface-edge/{entityId}",
                checked(points.Count * 24L),
                contentSha256,
                contentSha256),
            new SourceQualityGrid(width, height, points.Count),
            new SourceQualityCoverage(
                points.Count,
                points.Count,
                0,
                1.0,
                0.0,
                "complete-organized-xyz-grid",
                new SourceQualityInvalidCellMaskIdentity(
                    "synthetic-packed-mask-1.0",
                    "packed-lsb-row-major",
                    maskBytes.Length,
                    Convert.ToHexString(SHA256.HashData(maskBytes)))),
            new SourceQualityHeightStatistics(
                "cartesian-z",
                points.Min(point => point.Z),
                points.Max(point => point.Z),
                points.Average(point => point.Z),
                null),
            new SourceQualityCoordinateContext(
                "mm",
                "edge-scene-frame",
                PreparedSceneArtifact.CurrentCoordinateConvention),
            "controlled-organized-surface-edge-fixture",
            true,
            Enum.GetValues<SourceQualityChannel>()
                .Select(channel => new SourceQualityChannelAvailability(
                    channel,
                    channel == SourceQualityChannel.Height
                        ? SourceQualityChannelState.Available
                        : SourceQualityChannelState.Unavailable,
                    channel == SourceQualityChannel.Height
                        ? "Controlled Cartesian Z grid is present."
                        : "Controlled fixture does not declare this source channel."))
                .ToArray());
    }

    private static SurfaceMatchExecutionArtifact CreateIdentityExecution(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene)
    {
        var pose = new RigidPose3D(
            "mm",
            "edge-model-frame",
            "edge-scene-frame",
            1.0, 0.0, 0.0,
            0.0, 1.0, 0.0,
            0.0, 0.0, 1.0,
            0.0, 0.0, 0.0);
        var parameters = new RigidSurfacePoseSearchParameters(
            0.0, 0.0, 1.0,
            0.0, 0.0, 1.0,
            0.0, 0.0, 1.0,
            0.0, 0.0,
            0.0, 0.0,
            0.0, 0.0,
            0.01,
            model.Samples.Length,
            1);
        var coverage = SurfaceCoverageScorer.Evaluate(
            model,
            scene,
            pose,
            parameters.MaximumCorrespondenceDistance);
        var poseResult = RigidSurfacePoseSearchResult.Create(
            model.ContentSha256,
            scene.ContentSha256,
            parameters,
            RigidSurfacePoseSearchState.Matched,
            1,
            pose,
            coverage,
            string.Empty);
        return SurfaceMatchExecutionArtifact.Create(model, scene, poseResult);
    }

    private static string HashPoints(IReadOnlyList<SurfaceModelPoint3> points)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            writer.Write(points.Count);
            foreach (var point in points)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
                writer.Write(point.Z);
            }
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static bool ThrowsInvalidData(Action action, out string evidence)
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

    private static bool Nearly(
        double actual,
        double expected,
        double tolerance = 1e-9) =>
        double.IsFinite(actual)
        && Math.Abs(actual - expected) <= tolerance;

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}

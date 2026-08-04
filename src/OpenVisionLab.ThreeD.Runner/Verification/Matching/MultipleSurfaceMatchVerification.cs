using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class MultipleSurfaceMatchVerification
{
    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)
                        ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);
        var foundationReport = Path.Combine(
            directory,
            "multiple-match-foundation.txt");
        if (SurfaceMatchingFoundationVerification.Run(foundationReport) != 0)
        {
            File.WriteAllText(
                fullReportPath,
                "MultipleSurfaceMatchVerification|FAIL|foundation verification failed",
                new UTF8Encoding(false));
            return 1;
        }

        var model = SurfaceModelArtifactStore.Load(Path.Combine(
            directory,
            "known-pose.surface-model.json"));
        var firstPose = Pose(10.0, -4.0, 2.0);
        var secondPose = Pose(-12.0, 7.0, 1.0);
        var scenePoints = model.Samples
            .Select(sample => firstPose.TransformPoint(sample.Position))
            .Concat(model.Samples.Select(sample =>
                secondPose.TransformPoint(sample.Position)))
            .ToArray();
        var scene = PreparedScenePreparation.Prepare(
            new PreparedScenePreparationRequest(
                "prepared-scene.known-two-object",
                "Known Two-Object Scene",
                PreparedSceneArtifact.CurrentCoordinateConvention,
                SurfaceMatchingFoundationVerification.CreateQuality(
                    "scene.measured.known-two-object",
                    "fixture://known-two-object",
                    "scene-frame",
                    scenePoints),
                scenePoints,
                new PreparedScenePreparationParameters(
                    PreparedScenePreparationParameters
                        .DeterministicEvenPointSampling,
                    scenePoints.Length)));
        var search = new RigidSurfacePoseSearchParameters(
            0.0,
            0.0,
            1.0,
            0.0,
            0.0,
            1.0,
            30.0,
            30.0,
            1.0,
            -15.0,
            15.0,
            -8.0,
            9.0,
            0.0,
            3.0,
            1e-6,
            3,
            10);
        var policy = SurfaceMatchAcceptancePolicy.Create(0.9, 0.01);
        var collection = MultipleSurfaceMatchEvaluationExecutor.Execute(
            model,
            scene,
            search,
            policy,
            maximumMatchCount: 2,
            maximumExpandedCandidateCount: 100);
        var repeated = MultipleSurfaceMatchEvaluationExecutor.Execute(
            model,
            scene,
            search,
            policy,
            maximumMatchCount: 2,
            maximumExpandedCandidateCount: 100);
        var single = MultipleSurfaceMatchEvaluationExecutor.Execute(
            model,
            scene,
            search,
            policy,
            maximumMatchCount: 1,
            maximumExpandedCandidateCount: 50);
        var modelPath = Path.Combine(
            directory,
            "known-two-object.surface-model.json");
        var scenePath = Path.Combine(
            directory,
            "known-two-object.prepared-scene.json");
        var collectionPath = Path.Combine(
            directory,
            "known-two-object.surface-match-collection.json");
        SurfaceModelArtifactStore.Save(modelPath, model);
        PreparedSceneArtifactStore.Save(scenePath, scene);
        SurfaceMatchCollectionArtifactStore.Save(
            collectionPath,
            collection);
        var loaded = SurfaceMatchCollectionArtifactStore.Load(
            collectionPath);
        var tampered = collection with
        {
            Items = collection.Items
                .Select((item, index) => index == 0
                    ? item with { MatchId = "match.surface.tampered" }
                    : item)
                .ToArray()
        };
        var tamperedRejected = ThrowsInvalidData(() =>
            SurfaceMatchCollectionArtifactStore.Save(
                Path.Combine(directory, "tampered.surface-match-collection.json"),
                tampered));
        var tamperedPolicy = collection with
        {
            AcceptancePolicy = collection.AcceptancePolicy with
            {
                MinimumCoverageRatio = 0.5
            }
        };
        var tamperedPolicyRejected = ThrowsInvalidData(() =>
            SurfaceMatchCollectionArtifactStore.Save(
                Path.Combine(directory, "tampered-policy.surface-match-collection.json"),
                tamperedPolicy));
        var budgetRejected = ThrowsInvalidData(() =>
            MultipleSurfaceMatchEvaluationExecutor.Execute(
                model,
                scene,
                search,
                policy,
                maximumMatchCount: 2,
                maximumExpandedCandidateCount: 99));

        var cases = new List<Case>();
        void Check(string name, bool passed, string evidence) =>
            cases.Add(new Case(name, passed, evidence));
        Check(
            "two-object-collection-count",
            collection.Items.Length == 2,
            $"count={collection.Items.Length}");
        Check(
            "first-pose-stable",
            Near(collection.Items[0].Execution.PoseResult.Pose!.TranslationX, 10.0)
            && Near(collection.Items[0].Execution.PoseResult.Pose!.TranslationY, -4.0),
            PoseEvidence(collection.Items[0]));
        Check(
            "second-pose-stable",
            Near(collection.Items[1].Execution.PoseResult.Pose!.TranslationX, -12.0)
            && Near(collection.Items[1].Execution.PoseResult.Pose!.TranslationY, 7.0),
            PoseEvidence(collection.Items[1]));
        Check(
            "full-coverage-per-result",
            collection.Items.All(item =>
                item.Execution.PoseResult.Coverage.CoverageRatio == 1.0
                && item.Execution.PoseResult.Coverage.MatchedModelSampleCount == 5),
            string.Join(";", collection.Items.Select(item =>
                item.Execution.PoseResult.Coverage.Evidence)));
        Check(
            "disjoint-scene-evidence",
            collection.Items
                .SelectMany(item => item.Execution.PoseResult.Coverage.Matches)
                .Select(match => match.SceneSampleOrder)
                .Distinct()
                .Count() == 10,
            $"claims={collection.Items.Sum(item => item.Execution.PoseResult.Coverage.Matches.Length)}");
        Check(
            "stable-match-identities",
            collection.Items.Select(item => item.MatchId)
                .SequenceEqual(repeated.Items.Select(item => item.MatchId)),
            string.Join(";", collection.Items.Select(item => item.MatchId)));
        Check(
            "stable-collection-identity",
            collection.ContentSha256 == repeated.ContentSha256
            && collection.CollectionId == repeated.CollectionId,
            $"first={collection.ContentSha256};repeat={repeated.ContentSha256}");
        Check(
            "single-result-bound-preserves-first-result",
            single.Items.Length == 1
            && single.Items[0].MatchId == collection.Items[0].MatchId,
            $"single={single.Items.FirstOrDefault()?.MatchId}");
        Check(
            "assessment-linked-per-result",
            collection.Items.All(item =>
                item.Assessment.ExecutionContentSha256
                    == item.Execution.ContentSha256
                && item.Assessment.Decision == SurfaceMatchDecision.Pass),
            string.Join(";", collection.Items.Select(item =>
                $"{item.Order}:{item.Assessment.Decision}")));
        Check(
            "collection-valid",
            SurfaceMatchCollectionArtifactValidator.Inspect(collection).IsValid,
            SurfaceMatchCollectionArtifactValidator.Inspect(collection).Evidence);
        Check(
            "save-load-round-trip",
            loaded.ContentSha256 == collection.ContentSha256
            && loaded.Items.Select(item => item.MatchId)
                .SequenceEqual(collection.Items.Select(item => item.MatchId)),
            $"path={collectionPath};sha256={loaded.ContentSha256}");
        Check(
            "tampered-match-id-rejected",
            tamperedRejected,
            $"rejected={tamperedRejected}");
        Check(
            "tampered-acceptance-policy-rejected",
            tamperedPolicyRejected,
            $"rejected={tamperedPolicyRejected}");
        Check(
            "expanded-candidate-budget-rejected",
            budgetRejected,
            $"rejected={budgetRejected}");

        var passedCount = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"MultipleSurfaceMatchVerification|{(passedCount == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passedCount}|failed={cases.Count - passedCount}",
            "Boundary|Deterministic known two-object fixture; stable typed collection and disjoint scene-sample evidence; no symmetry, physical metrology, acquisition-direction, cross-hardware, production-throughput, or human-usability claim.",
            $"Collection|path={collectionPath}|id={collection.CollectionId}|sha256={collection.ContentSha256}|items={collection.Items.Length}|evaluated={collection.EvaluatedCandidateCount}",
            $"NoahPackage|version={LibraryNoahHeightMapInspection.PackageVersion}|sourceCommit={LibraryNoahHeightMapInspection.PackageSourceCommit}"
        };
        lines.AddRange(cases.Select(item =>
            $"{(item.Passed ? "PASS" : "FAIL")} | {item.Name} | {item.Evidence}"));
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        Console.WriteLine(
            $"Multiple Surface Match verification: "
            + $"{(passedCount == cases.Count ? "PASS" : "FAIL")} "
            + $"({passedCount}/{cases.Count})");
        return passedCount == cases.Count ? 0 : 1;
    }

    private static RigidPose3D Pose(
        double translationX,
        double translationY,
        double translationZ)
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
            translationX,
            translationY,
            translationZ);
    }

    private static string PoseEvidence(SurfaceMatchCollectionItem item)
    {
        var pose = item.Execution.PoseResult.Pose!;
        return $"id={item.MatchId};translation={pose.TranslationX:G17},{pose.TranslationY:G17},{pose.TranslationZ:G17}";
    }

    private static bool Near(double actual, double expected) =>
        Math.Abs(actual - expected) <= 1e-12;

    private static bool ThrowsInvalidData(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidDataException)
        {
            return true;
        }
    }

    private sealed record Case(
        string Name,
        bool Passed,
        string Evidence);
}

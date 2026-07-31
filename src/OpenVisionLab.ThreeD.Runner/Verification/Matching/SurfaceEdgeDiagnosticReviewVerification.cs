using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceEdgeDiagnosticReviewVerification
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
            "surface-edge-foundation-regression.txt");
        if (SurfaceEdgeMatchingVerification.Run(foundationReport) != 0)
        {
            File.WriteAllText(
                fullReportPath,
                "SurfaceEdgeDiagnosticReviewVerification|FAIL|surface-edge foundation failed",
                new UTF8Encoding(false));
            return 1;
        }

        var model = SurfaceModelArtifactStore.Load(
            Path.Combine(directory, "edge-score.surface-model.json"));
        var acceptedScene = PreparedSceneArtifactStore.Load(
            Path.Combine(directory, "edge-score-height.prepared-scene.json"));
        var rejectedScene = PreparedSceneArtifactStore.Load(
            Path.Combine(directory, "edge-score-flat.prepared-scene.json"));
        var acceptedExecution = SurfaceMatchExecutionArtifactStore.Load(
            Path.Combine(directory, "edge-score.surface-match-execution.json"));
        var rejectedExecution = SurfaceMatchExecutionArtifactStore.Load(
            Path.Combine(directory, "edge-score-flat.surface-match-execution.json"));
        var modelEdges = SurfaceEdgeArtifactStore.LoadModel(
            Path.Combine(directory, "edge-score.model-edges.json"));
        var acceptedSceneEdges = SurfaceEdgeArtifactStore.LoadScene(
            Path.Combine(directory, "edge-score.height-scene-edges.json"));
        var rejectedSceneEdges = SurfaceEdgeArtifactStore.LoadScene(
            Path.Combine(directory, "edge-score.flat-scene-edges.json"));
        var acceptedScore = SurfaceEdgeArtifactStore.LoadScore(
            Path.Combine(directory, "edge-score.height.score.json"));
        var rejectedScore = SurfaceEdgeArtifactStore.LoadScore(
            Path.Combine(directory, "edge-score.flat.score.json"));

        var acceptedOverlay = SurfaceEdgeDiagnosticOverlayBuilder.Build(
            model,
            acceptedScene,
            acceptedExecution,
            modelEdges,
            acceptedSceneEdges,
            acceptedScore);
        var rejectedOverlay = SurfaceEdgeDiagnosticOverlayBuilder.Build(
            model,
            rejectedScene,
            rejectedExecution,
            modelEdges,
            rejectedSceneEdges,
            rejectedScore);
        var policy = SurfaceAndEdgeMatchAcceptancePolicy.Create(
            SurfaceMatchAcceptancePolicy.Create(0.9, 0.25),
            SurfaceEdgeAcceptancePolicy.Create(0.9, 0.05));
        var acceptedAssessment =
            SurfaceAndEdgeMatchAssessmentEvaluator.Evaluate(
                acceptedScore,
                policy);
        var rejectedAssessment =
            SurfaceAndEdgeMatchAssessmentEvaluator.Evaluate(
                rejectedScore,
                policy);
        var review = SurfaceMatchFalsePositiveReviewBuilder.Build(
            model,
            new SurfaceMatchReviewEvidenceSet(
                "Raised perimeter reference",
                acceptedScene,
                acceptedExecution,
                acceptedScore,
                acceptedAssessment),
            new SurfaceMatchReviewEvidenceSet(
                "Flat surface-only candidate",
                rejectedScene,
                rejectedExecution,
                rejectedScore,
                rejectedAssessment));

        var acceptedOverlayPath = Path.Combine(
            directory,
            "edge-review.accepted.overlay.json");
        var rejectedOverlayPath = Path.Combine(
            directory,
            "edge-review.rejected.overlay.json");
        var acceptedAssessmentPath = Path.Combine(
            directory,
            "edge-review.accepted.assessment.json");
        var rejectedAssessmentPath = Path.Combine(
            directory,
            "edge-review.rejected.assessment.json");
        var reviewPath = Path.Combine(
            directory,
            "edge-review.false-positive-review.json");
        SurfaceEdgeDiagnosticReviewArtifactStore.SaveOverlay(
            acceptedOverlayPath,
            acceptedOverlay);
        SurfaceEdgeDiagnosticReviewArtifactStore.SaveOverlay(
            rejectedOverlayPath,
            rejectedOverlay);
        SurfaceEdgeDiagnosticReviewArtifactStore.SaveAssessment(
            acceptedAssessmentPath,
            acceptedAssessment);
        SurfaceEdgeDiagnosticReviewArtifactStore.SaveAssessment(
            rejectedAssessmentPath,
            rejectedAssessment);
        SurfaceEdgeDiagnosticReviewArtifactStore.SaveReview(
            reviewPath,
            review);

        var savedReview = File.ReadAllBytes(reviewPath);
        var tamperedReviewRejected = ThrowsInvalidData(
            () => SurfaceEdgeDiagnosticReviewArtifactStore.SaveReview(
                reviewPath,
                review with { ModelSampleCount = review.ModelSampleCount + 1 }),
            out var tamperedReviewEvidence);
        var reviewPreserved = savedReview.SequenceEqual(
            File.ReadAllBytes(reviewPath));
        var invalidEdgePolicyRejected = ThrowsInvalidData(
            () => SurfaceEdgeAcceptancePolicy.Create(1.01, 0.05),
            out var invalidPolicyEvidence);
        var tamperedAssessment = rejectedAssessment with
        {
            Decision = SurfaceMatchDecision.Pass
        };
        tamperedAssessment = tamperedAssessment with
        {
            ContentSha256 = SurfaceAndEdgeMatchAssessmentArtifact
                .CalculateContentSha256(tamperedAssessment)
        };

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "accepted-overlay-valid",
                SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(acceptedOverlay).IsValid,
                SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(acceptedOverlay).Evidence),
            Check(
                "overlay-links-exact-evidence-chain",
                acceptedOverlay.SurfaceMatchExecutionContentSha256
                    == acceptedExecution.ContentSha256
                && acceptedOverlay.ScoreContentSha256
                    == acceptedScore.ContentSha256
                && acceptedOverlay.ModelEdgeContentSha256
                    == modelEdges.ContentSha256
                && acceptedOverlay.SceneEdgeContentSha256
                    == acceptedSceneEdges.ContentSha256,
                acceptedOverlay.ContentSha256),
            Check(
                "known-outward-normal-fixture",
                acceptedOverlay.ModelSegments.Length == 4
                && acceptedOverlay.ModelSegments.All(segment =>
                    Nearly(segment.DeclaredNormal.X, 0.0)
                    && Nearly(segment.DeclaredNormal.Y, 0.0)
                    && Nearly(segment.DeclaredNormal.Z, 1.0)),
                string.Join(';', acceptedOverlay.ModelSegments.Select(segment =>
                    $"{segment.ModelEdgeOrder}:{segment.DeclaredNormal.X:G3},{segment.DeclaredNormal.Y:G3},{segment.DeclaredNormal.Z:G3}"))),
            Check(
                "edge-directions-unit-and-tangent",
                acceptedOverlay.ModelSegments.All(segment =>
                    Nearly(Length(segment.EdgeDirection), 1.0)
                    && Nearly(Dot(
                        segment.EdgeDirection,
                        segment.DeclaredNormal),
                        0.0)),
                "All four canonical edge directions are unit length and perpendicular to +Z declared normals."),
            Check(
                "accepted-overlay-retains-scene-steps-and-matches",
                acceptedOverlay.SceneSegments.Length == 28
                && acceptedOverlay.ModelSegments.Count(segment => segment.IsMatched) == 4
                && acceptedOverlay.SceneSegments.Count(segment => segment.IsMatched) == 4,
                $"model={acceptedOverlay.ModelSegments.Length};scene={acceptedOverlay.SceneSegments.Length};matched={acceptedOverlay.ModelSegments.Count(segment => segment.IsMatched)}"),
            Check(
                "rejected-overlay-retains-original-flat-scene",
                rejectedOverlay.SceneContentSha256 == rejectedScene.ContentSha256
                && rejectedOverlay.SceneSegments.Length == 0
                && rejectedOverlay.ModelSegments.All(segment => !segment.IsMatched),
                SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(rejectedOverlay).Evidence),
            Check(
                "independent-policy-valid",
                SurfaceAndEdgeAssessmentArtifactValidator
                    .InspectPolicy(policy, out var policyEvidence),
                policyEvidence),
            Check(
                "accepted-reference-passes-both-components",
                acceptedAssessment.Decision == SurfaceMatchDecision.Pass
                && acceptedAssessment.Surface.Decision == SurfaceMatchDecision.Pass
                && acceptedAssessment.Edge.Decision == SurfaceMatchDecision.Pass,
                SurfaceAndEdgeAssessmentArtifactValidator
                    .Inspect(acceptedAssessment, acceptedScore).Evidence),
            Check(
                "surface-only-candidate-fails-edge-component",
                rejectedAssessment.Decision == SurfaceMatchDecision.Fail
                && rejectedAssessment.Surface.Decision == SurfaceMatchDecision.Pass
                && rejectedAssessment.Edge.Decision == SurfaceMatchDecision.Fail
                && rejectedAssessment.Reason
                    == SurfaceAndEdgeDecisionReason.EdgeCoverageBelowMinimum,
                SurfaceAndEdgeAssessmentArtifactValidator
                    .Inspect(rejectedAssessment, rejectedScore).Evidence),
            Check(
                "equal-surface-score-remains-visible",
                Nearly(acceptedAssessment.Surface.RawCoverageRatio, 1.0)
                && Nearly(rejectedAssessment.Surface.RawCoverageRatio, 1.0)
                && Nearly(acceptedAssessment.Edge.RawCoverageRatio, 1.0)
                && Nearly(rejectedAssessment.Edge.RawCoverageRatio, 0.0),
                $"accepted={acceptedAssessment.Surface.RawCoverageRatio:G17}/{acceptedAssessment.Edge.RawCoverageRatio:G17};rejected={rejectedAssessment.Surface.RawCoverageRatio:G17}/{rejectedAssessment.Edge.RawCoverageRatio:G17}"),
            Check(
                "assessment-has-no-weighted-score",
                policy.Semantics.Contains("no-weighted-score", StringComparison.Ordinal)
                && acceptedAssessment.Semantics.Contains("no-weighted-score", StringComparison.Ordinal),
                $"policy={policy.Semantics};assessment={acceptedAssessment.Semantics}"),
            Check(
                "retained-false-positive-review-valid",
                SurfaceMatchFalsePositiveReviewArtifactValidator
                    .Inspect(review).IsValid,
                SurfaceMatchFalsePositiveReviewArtifactValidator
                    .Inspect(review).Evidence),
            Check(
                "review-retains-original-scenes-samples-poses-and-scores",
                review.ModelContentSha256 == model.ContentSha256
                && review.ModelSampleCount == model.Samples.Length
                && review.Accepted.SceneContentSha256 == acceptedScene.ContentSha256
                && review.Accepted.SceneSampleCount == acceptedScene.Samples.Length
                && review.Accepted.PoseResultContentSha256 == acceptedExecution.PoseResult.ContentSha256
                && review.Accepted.ScoreContentSha256 == acceptedScore.ContentSha256
                && review.Rejected.SceneContentSha256 == rejectedScene.ContentSha256
                && review.Rejected.SceneSampleCount == rejectedScene.Samples.Length
                && review.Rejected.PoseResultContentSha256 == rejectedExecution.PoseResult.ContentSha256
                && review.Rejected.ScoreContentSha256 == rejectedScore.ContentSha256,
                review.Evidence),
            Check(
                "overlay-round-trip",
                SurfaceEdgeDiagnosticReviewArtifactStore
                    .LoadOverlay(acceptedOverlayPath).ContentSha256
                    == acceptedOverlay.ContentSha256
                && SurfaceEdgeDiagnosticReviewArtifactStore
                    .LoadOverlay(rejectedOverlayPath).ContentSha256
                    == rejectedOverlay.ContentSha256,
                $"accepted={acceptedOverlay.ContentSha256};rejected={rejectedOverlay.ContentSha256}"),
            Check(
                "assessment-round-trip",
                SurfaceEdgeDiagnosticReviewArtifactStore
                    .LoadAssessment(acceptedAssessmentPath).ContentSha256
                    == acceptedAssessment.ContentSha256
                && SurfaceEdgeDiagnosticReviewArtifactStore
                    .LoadAssessment(rejectedAssessmentPath).ContentSha256
                    == rejectedAssessment.ContentSha256,
                $"accepted={acceptedAssessment.ContentSha256};rejected={rejectedAssessment.ContentSha256}"),
            Check(
                "review-round-trip",
                SurfaceEdgeDiagnosticReviewArtifactStore
                    .LoadReview(reviewPath).ContentSha256
                    == review.ContentSha256,
                review.ContentSha256),
            Check(
                "tampered-independent-decision-rejected",
                !SurfaceAndEdgeAssessmentArtifactValidator
                    .Inspect(tamperedAssessment, rejectedScore).IsValid,
                SurfaceAndEdgeAssessmentArtifactValidator
                    .Inspect(tamperedAssessment, rejectedScore).Evidence),
            Check(
                "tampered-review-save-rejected-and-preserved",
                tamperedReviewRejected && reviewPreserved,
                $"rejected={tamperedReviewRejected};preserved={reviewPreserved};detail={tamperedReviewEvidence}"),
            Check(
                "invalid-edge-limit-rejected",
                invalidEdgePolicyRejected,
                invalidPolicyEvidence),
            Check(
                "diagnostic-does-not-infer-acquisition-direction",
                acceptedOverlay.Semantics.Contains("diagnostic", StringComparison.Ordinal)
                && SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(acceptedOverlay).Evidence.Contains(
                        "acquisitionDirection=unavailable",
                        StringComparison.Ordinal),
                SurfaceEdgeDiagnosticOverlayArtifactValidator
                    .Inspect(acceptedOverlay).Evidence)
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceEdgeDiagnosticReviewVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "Boundary|Declared model normals and canonical edge directions only; acquisition viewpoint remains unavailable; surface and edge limits are independent; no weighted score or metrology claim.",
            $"Overlay|accepted={acceptedOverlay.ContentSha256}|rejected={rejectedOverlay.ContentSha256}",
            $"Policy|{policy.ContentSha256}|surface={policy.Surface.MinimumCoverageRatio:G17}/{policy.Surface.MaximumInlierRmse:G17}|edge={policy.Edge.MinimumCoverageRatio:G17}/{policy.Edge.MaximumInlierRmse:G17}",
            $"Assessment|accepted={acceptedAssessment.ContentSha256}/{acceptedAssessment.Decision}|rejected={rejectedAssessment.ContentSha256}/{rejectedAssessment.Decision}/{rejectedAssessment.Reason}",
            $"Review|path={reviewPath}|sha256={review.ContentSha256}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        Console.WriteLine(
            $"Surface edge diagnostic/review verification: {(passed == cases.Count ? "PASS" : "FAIL")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static double Length(SurfaceModelPoint3 point) =>
        Math.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);

    private static double Dot(
        SurfaceModelPoint3 first,
        SurfaceModelPoint3 second) =>
        first.X * second.X + first.Y * second.Y + first.Z * second.Z;

    private static bool Nearly(
        double actual,
        double expected,
        double tolerance = 1e-9) =>
        double.IsFinite(actual)
        && Math.Abs(actual - expected) <= tolerance;

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
        catch (InvalidDataException exception)
        {
            evidence = exception.Message;
            return true;
        }
    }

    private static (string Name, bool Passed, string Evidence) Check(
        string name,
        bool passed,
        string evidence) =>
        (name, passed, evidence);
}

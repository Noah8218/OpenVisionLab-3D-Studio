using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

internal static class SurfaceMatchAcceptanceGoldenVerification
{
    private const string ExpectedPolicySha256 =
        "2113FB3D6E13D582993BA9CE7EF7AA531F438605650895FE53135B159BA3569E";
    private const string ExpectedPassAssessmentSha256 =
        "EBB504571A2E3FEDDEEFD4645A14B29C6940574B2F1FDFC97F32F784F197698A";
    private const string ExpectedFalsePositiveAssessmentSha256 =
        "9B9E711B9CB72DF0F4A2DC9E520B5A2EE8715BBB4B5B586FF8BB886B78557C95";
    private const string ExpectedOutOfDomainAssessmentSha256 =
        "D9C8EF83D73E8683CB3BA122BAD011B9D39A03607B0F005A00224CE878F58210";

    public static int Run(string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var fullReportPath = Path.GetFullPath(reportPath);
        var directory = Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(directory);

        var foundationReport = Path.Combine(
            directory,
            "surface-match-foundation-regression.txt");
        if (SurfaceMatchingFoundationVerification.Run(
                foundationReport) != 0)
        {
            File.WriteAllText(
                fullReportPath,
                "SurfaceMatchAcceptanceGoldenVerification|FAIL|foundation regression failed",
                new UTF8Encoding(false));
            return 1;
        }

        var model = SurfaceModelArtifactStore.Load(
            Path.Combine(
                directory,
                "known-pose.surface-model.json"));
        var fullScene = PreparedSceneArtifactStore.Load(
            Path.Combine(
                directory,
                "known-pose-full.prepared-scene.json"));
        var occludedScene = PreparedSceneArtifactStore.Load(
            Path.Combine(
                directory,
                "known-pose-occluded.prepared-scene.json"));
        var bounded = BoundedParameters();
        var broad = bounded with
        {
            MinimumRotationZDegrees = -180.0,
            MaximumRotationZDegrees = 180.0,
            MaximumCandidateCount = 1000
        };
        var outOfDomain = bounded with
        {
            MinimumTranslationX = 0.0,
            MaximumTranslationX = 1.0,
            MinimumTranslationY = 0.0,
            MaximumTranslationY = 1.0,
            MinimumTranslationZ = 0.0,
            MaximumTranslationZ = 1.0
        };
        var policy = SurfaceMatchAcceptancePolicy.Create(
            0.9,
            0.25);
        var positive = SurfaceMatchEvaluationExecutor.Execute(
            model,
            fullScene,
            bounded,
            policy);
        var repeated = SurfaceMatchEvaluationExecutor.Execute(
            model,
            fullScene,
            bounded,
            policy);
        var falsePositive = SurfaceMatchEvaluationExecutor.Execute(
            model,
            occludedScene,
            bounded,
            policy);
        var rejected = SurfaceMatchEvaluationExecutor.Execute(
            model,
            fullScene,
            outOfDomain,
            policy);
        var broadResult = SurfaceMatchEvaluationExecutor.Execute(
            model,
            fullScene,
            broad,
            policy);

        var positiveAssessmentPath = Path.Combine(
            directory,
            "known-pose.surface-match-assessment.json");
        var positiveExecutionPath = Path.Combine(
            directory,
            "accepted-known-pose.surface-match-execution.json");
        var positiveRuntimePath = Path.Combine(
            directory,
            "known-pose.surface-match-runtime.json");
        SurfaceMatchExecutionArtifactStore.Save(
            positiveExecutionPath,
            positive.Execution);
        SurfaceMatchAssessmentArtifactStore.Save(
            positiveAssessmentPath,
            positive.Assessment);
        SurfaceMatchAssessmentArtifactStore.SaveRuntime(
            positiveRuntimePath,
            positive.Runtime);
        var loadedAssessment =
            SurfaceMatchAssessmentArtifactStore.Load(
                positiveAssessmentPath);
        var loadedRuntime =
            SurfaceMatchAssessmentArtifactStore.LoadRuntime(
                positiveRuntimePath);

        var preservedAssessment = loadedAssessment;
        var tamperedSaveRejected = ThrowsInvalidData(
            () => SurfaceMatchAssessmentArtifactStore.Save(
                positiveAssessmentPath,
                positive.Assessment with
                {
                    Decision = SurfaceMatchDecision.Fail
                }),
            out var tamperedEvidence);
        var afterTamperAttempt =
            SurfaceMatchAssessmentArtifactStore.Load(
                positiveAssessmentPath);
        SurfaceMatchAssessmentArtifactStore.Save(
            Path.Combine(
                directory,
                "occluded.surface-match-assessment.json"),
            falsePositive.Assessment);
        SurfaceMatchAssessmentArtifactStore.Save(
            Path.Combine(
                directory,
                "out-of-domain.surface-match-assessment.json"),
            rejected.Assessment);

        var invalidRange = bounded with
        {
            MinimumRotationZDegrees = 45.0,
            MaximumRotationZDegrees = -45.0
        };
        var invalidRangeValidity =
            RigidSurfacePoseSearchParameterValidator.Inspect(
                invalidRange);
        var boundedValidity =
            RigidSurfacePoseSearchParameterValidator.Inspect(
                bounded);
        var broadValidity =
            RigidSurfacePoseSearchParameterValidator.Inspect(
                broad);
        var runtimeValid =
            SurfaceMatchAssessmentArtifactValidator.InspectRuntime(
                positive.Runtime,
                out var runtimeEvidence);

        var cases = new List<(string Name, bool Passed, string Evidence)>
        {
            Check(
                "known-pose-pass",
                positive.Assessment.Decision == SurfaceMatchDecision.Pass
                && positive.Assessment.Reason
                    == SurfaceMatchDecisionReason.MeetsAuthoredLimits
                && positive.Execution.PoseResult.Pose is { } pose
                && Nearly(pose.RotationAngleDegrees, 30.0)
                && Nearly(pose.TranslationX, 10.0)
                && Nearly(pose.TranslationY, -4.0)
                && Nearly(pose.TranslationZ, 2.0),
                AssessmentEvidence(positive)),
            Check(
                "acceptance-does-not-change-raw-execution",
                positive.Execution.Semantics
                    == SurfaceMatchExecutionArtifact.CurrentSemantics
                && positive.Assessment.ExecutionContentSha256
                    == positive.Execution.ContentSha256,
                $"execution={positive.Execution.ContentSha256};assessment={positive.Assessment.ContentSha256};semantics={positive.Execution.Semantics}"),
            Check(
                "deterministic-assessment-repeat",
                positive.Execution.ContentSha256
                    == repeated.Execution.ContentSha256
                && positive.Assessment.ContentSha256
                    == repeated.Assessment.ContentSha256
                && positive.Runtime.ObservedAtUtc
                    != repeated.Runtime.ObservedAtUtc,
                $"first={positive.Assessment.ContentSha256};repeat={repeated.Assessment.ContentSha256};runtimeExcluded=true"),
            Check(
                "controlled-false-positive-fails-policy",
                falsePositive.Execution.PoseResult.State
                    == RigidSurfacePoseSearchState.Matched
                && falsePositive.Assessment.Decision
                    == SurfaceMatchDecision.Fail
                && falsePositive.Assessment.Reason
                    is SurfaceMatchDecisionReason.CoverageBelowMinimum
                    or SurfaceMatchDecisionReason.InlierRmseAboveMaximum,
                AssessmentEvidence(falsePositive)),
            Check(
                "out-of-domain-rejected-with-reason",
                rejected.Execution.PoseResult.State
                    == RigidSurfacePoseSearchState.NoMatch
                && rejected.Assessment.Decision
                    == SurfaceMatchDecision.Rejected
                && rejected.Assessment.Reason
                    == SurfaceMatchDecisionReason.PoseSearchNoMatch
                && rejected.Assessment.RawSearchRejectionReason.Contains(
                    "bounds",
                    StringComparison.OrdinalIgnoreCase),
                AssessmentEvidence(rejected)),
            Check(
                "bounded-domain-has-fewer-candidates",
                boundedValidity.IsValid
                && broadValidity.IsValid
                && boundedValidity.CandidateCount
                    < broadValidity.CandidateCount
                && positive.Execution.PoseResult.EvaluatedCandidateCount
                    < broadResult.Execution.PoseResult
                        .EvaluatedCandidateCount,
                $"bounded={boundedValidity.CandidateCount}/{positive.Runtime.TotalMilliseconds:F3}ms;broad={broadValidity.CandidateCount}/{broadResult.Runtime.TotalMilliseconds:F3}ms"),
            Check(
                "invalid-authored-range-fails-closed",
                !invalidRangeValidity.IsValid
                && invalidRangeValidity.Errors.Any(error =>
                    error.Contains(
                        "ordered bounds",
                        StringComparison.OrdinalIgnoreCase)),
                string.Join(" ", invalidRangeValidity.Errors)),
            Check(
                "runtime-stages-valid-and-observational",
                runtimeValid
                && positive.Runtime.ExecutionContentSha256
                    == positive.Execution.ContentSha256
                && positive.Runtime.AssessmentContentSha256
                    == positive.Assessment.ContentSha256,
                runtimeEvidence),
            Check(
                "assessment-and-runtime-round-trip",
                loadedAssessment == positive.Assessment
                && loadedRuntime.SchemaVersion
                    == positive.Runtime.SchemaVersion
                && loadedRuntime.Clock == positive.Runtime.Clock
                && loadedRuntime.ExecutionContentSha256
                    == positive.Runtime.ExecutionContentSha256
                && loadedRuntime.AssessmentContentSha256
                    == positive.Runtime.AssessmentContentSha256
                && loadedRuntime.TotalElapsedTicks
                    == positive.Runtime.TotalElapsedTicks
                && loadedRuntime.ObservedAtUtc
                    == positive.Runtime.ObservedAtUtc
                && loadedRuntime.Stages.SequenceEqual(
                    positive.Runtime.Stages),
                $"assessment={loadedAssessment.ContentSha256};runtimeTicks={loadedRuntime.TotalElapsedTicks}"),
            Check(
                "tampered-assessment-rejected-and-preserved",
                tamperedSaveRejected
                && afterTamperAttempt == preservedAssessment,
                $"rejected={tamperedSaveRejected};reason={tamperedEvidence};preserved={afterTamperAttempt.ContentSha256}"),
            Check(
                "policy-sha-golden",
                policy.ContentSha256 == ExpectedPolicySha256,
                $"expected={ExpectedPolicySha256};actual={policy.ContentSha256}"),
            Check(
                "pass-assessment-sha-golden",
                positive.Assessment.ContentSha256
                    == ExpectedPassAssessmentSha256,
                $"expected={ExpectedPassAssessmentSha256};actual={positive.Assessment.ContentSha256}"),
            Check(
                "false-positive-assessment-sha-golden",
                falsePositive.Assessment.ContentSha256
                    == ExpectedFalsePositiveAssessmentSha256,
                $"expected={ExpectedFalsePositiveAssessmentSha256};actual={falsePositive.Assessment.ContentSha256}"),
            Check(
                "out-of-domain-assessment-sha-golden",
                rejected.Assessment.ContentSha256
                    == ExpectedOutOfDomainAssessmentSha256,
                $"expected={ExpectedOutOfDomainAssessmentSha256};actual={rejected.Assessment.ContentSha256}")
        };

        var passed = cases.Count(item => item.Passed);
        var lines = new List<string>
        {
            $"SurfaceMatchAcceptanceGoldenVerification|{(passed == cases.Count ? "PASS" : "FAIL")}|cases={cases.Count}|passed={passed}|failed={cases.Count - passed}",
            "Boundary|Deterministic local golden fixture; separate acceptance over raw evidence; observed timings are not performance budgets; no metrology or human-usability claim.",
            $"Policy|sha256={policy.ContentSha256}|minimumCoverage={policy.MinimumCoverageRatio:G17}|maximumRmse={policy.MaximumInlierRmse:G17}",
            $"Positive|{AssessmentEvidence(positive)}",
            $"FalsePositive|{AssessmentEvidence(falsePositive)}",
            $"OutOfDomain|{AssessmentEvidence(rejected)}",
            $"SearchComparison|boundedCandidates={boundedValidity.CandidateCount}|broadCandidates={broadValidity.CandidateCount}|boundedObservedMs={positive.Runtime.TotalMilliseconds:F3}|broadObservedMs={broadResult.Runtime.TotalMilliseconds:F3}"
        };
        lines.AddRange(cases.Select(item =>
            $"{item.Name}|{(item.Passed ? "PASS" : "FAIL")}|{item.Evidence}"));
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        Console.WriteLine(
            $"Surface match acceptance golden verification: {(passed == cases.Count ? "PASS" : "FAIL")} ({passed}/{cases.Count})");
        return passed == cases.Count ? 0 : 1;
    }

    private static RigidSurfacePoseSearchParameters BoundedParameters() =>
        new(
            0.0, 0.0, 1.0,
            0.0, 0.0, 1.0,
            -45.0, 45.0, 15.0,
            8.0, 12.0,
            -6.0, -2.0,
            1.0, 3.0,
            2.0,
            3,
            100);

    private static string AssessmentEvidence(
        SurfaceMatchEvaluationResult result) =>
        $"decision={result.Assessment.Decision};reason={result.Assessment.Reason};rawState={result.Execution.PoseResult.State};coverage={result.Execution.PoseResult.Coverage.CoverageRatio:G17};rmse={result.Execution.PoseResult.Coverage.InlierRmse:G17};candidates={result.Execution.PoseResult.EvaluatedCandidateCount};execution={result.Execution.ContentSha256};assessment={result.Assessment.ContentSha256};observedMs={result.Runtime.TotalMilliseconds:F3}";

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

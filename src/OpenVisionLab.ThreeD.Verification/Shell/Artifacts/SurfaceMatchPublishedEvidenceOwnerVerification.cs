using System.IO;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Verification.Shell.Artifacts;

internal static class SurfaceMatchPublishedEvidenceOwnerVerification
{
    public static bool Verify(
        string artifactDirectory,
        string reportPath,
        out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Surface Match Published evidence owner verification",
            "Boundary|Direct Workbench composition over immutable evidence; fresh malformed-linkage matrix; no Preview, Publish, Run, Validation, recipe, source, or selection mutation."
        };
        var total = 0;
        var passed = 0;

        void Check(string name, bool condition, string evidence)
        {
            total++;
            if (condition)
            {
                passed++;
            }

            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {evidence}");
        }

        try
        {
            var directory = Path.GetFullPath(artifactDirectory);
            var model = SurfaceModelArtifactStore.Load(
                Path.Combine(directory, "edge-score.surface-model.json"));
            var scene = PreparedSceneArtifactStore.Load(
                Path.Combine(directory, "edge-score-height.prepared-scene.json"));
            var execution = SurfaceMatchExecutionArtifactStore.Load(
                Path.Combine(directory, "edge-score.surface-match-execution.json"));
            var score = SurfaceEdgeArtifactStore.LoadScore(
                Path.Combine(directory, "edge-score.height.score.json"));
            var overlay = SurfaceEdgeDiagnosticReviewArtifactStore.LoadOverlay(
                Path.Combine(directory, "edge-review.accepted.overlay.json"));
            var edgeAssessment = SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                Path.Combine(directory, "edge-review.accepted.assessment.json"));
            var review = SurfaceEdgeDiagnosticReviewArtifactStore.LoadReview(
                Path.Combine(directory, "edge-review.false-positive-review.json"));
            var policy = SurfaceMatchAcceptancePolicy.Create(0.0, double.MaxValue);
            var expectedDecision = SurfaceMatchAssessmentArtifactValidator.ExpectedDecision(
                execution.PoseResult.State,
                execution.PoseResult.Coverage.CoverageRatio,
                execution.PoseResult.Coverage.InlierRmse,
                policy);
            var assessment = SurfaceMatchAssessmentArtifact.Create(
                execution,
                policy,
                expectedDecision.Decision,
                expectedDecision.Reason);
            var runtime = CreateRuntime(execution, assessment);
            var acquisitionDirection = new ToolRecipeAcquisitionDirection(
                ToolRecipeAcquisitionDirectionState.Available,
                ToolRecipeAcquisitionDirectionConvention.SensorToScene,
                overlay.TargetFrameId,
                new ToolRecipeXyz(0.0, 0.0, -1.0));
            var acquisitionOrientation = SurfaceEdgeAcquisitionDirectionBuilder.Build(
                overlay,
                model.ContentSha256,
                acquisitionDirection);

            var workbench = new ToolWorkbenchViewModel();
            var initialDirty = workbench.IsDirty;
            var initialSource = workbench.Source;
            var initialSelection = workbench.WorkspaceSelection.Current;
            var initialSteps = workbench.PipelineSteps.ToArray();
            var initialStepStates = initialSteps
                .Select(step => step.State)
                .ToArray();
            var initialRunLogCount = workbench.RunLog.Count;
            var displayRequestCount = 0;
            var displayClearedCount = 0;
            ToolWorkbenchSurfaceMatchDisplayRequestEventArgs? request = null;
            workbench.SurfaceMatchDisplayRequested += (_, args) =>
            {
                request = args;
                displayRequestCount++;
            };
            workbench.SurfaceMatchDisplayCleared += (_, _) => displayClearedCount++;

            workbench.ShowSurfaceMatchEvidence(
                model,
                scene,
                execution,
                assessment,
                runtime,
                score,
                overlay,
                edgeAssessment,
                review,
                acquisitionOrientation);

            Check(
                "valid evidence is accepted by the direct Workbench owner path",
                ReferenceEquals(workbench.SurfaceMatchEvidence, execution)
                && ReferenceEquals(workbench.SurfaceMatchAssessment, assessment)
                && ReferenceEquals(workbench.SurfaceMatchRuntime, runtime)
                && ReferenceEquals(workbench.SurfaceEdgeScore, score)
                && ReferenceEquals(workbench.SurfaceEdgeDiagnosticOverlay, overlay)
                && ReferenceEquals(workbench.SurfaceEdgeAssessment, edgeAssessment)
                && ReferenceEquals(workbench.SurfaceMatchFalsePositiveReview, review)
                && ReferenceEquals(
                    workbench.SurfaceEdgeAcquisitionDirection,
                    acquisitionOrientation)
                && workbench.HasSurfaceMatchEvidence
                && displayRequestCount == 1
                && request is not null
                && ReferenceEquals(request.Model, model)
                && ReferenceEquals(request.Scene, scene)
                && ReferenceEquals(request.Execution, execution)
                && ReferenceEquals(request.Assessment, assessment)
                && ReferenceEquals(request.Runtime, runtime)
                && ReferenceEquals(request.EdgeScore, score)
                && ReferenceEquals(request.EdgeDiagnosticOverlay, overlay)
                && ReferenceEquals(request.EdgeAssessment, edgeAssessment)
                && ReferenceEquals(request.FalsePositiveReview, review)
                && ReferenceEquals(
                    request.AcquisitionDirectionOrientation,
                    acquisitionOrientation),
                $"displayRequests={displayRequestCount};execution={execution.ContentSha256};assessment={assessment.ContentSha256}");
            Check(
                "valid evidence preserves metadata and quality projections",
                workbench.SurfaceMatchAssessment?.Policy.ContentSha256
                    == assessment.Policy.ContentSha256
                && workbench.SurfaceMatchEvidence?.PoseResult.Coverage.CoverageRatio
                    == execution.PoseResult.Coverage.CoverageRatio
                && workbench.SurfaceEdgeScore?.SurfaceScore.CoverageRatio
                    == score.SurfaceScore.CoverageRatio
                && workbench.SurfaceEdgeScore?.EdgeScore.CoverageRatio
                    == score.EdgeScore.CoverageRatio
                && workbench.SurfaceMatchRuntime?.AssessmentContentSha256
                    == assessment.ContentSha256
                && workbench.SurfaceMatchFalsePositiveReview?.Evidence
                    == review.Evidence
                && workbench.SurfaceEdgeAcquisitionDirection?.Items.Length
                    == acquisitionOrientation.Items.Length,
                $"coverage={workbench.SurfaceMatchEvidence?.PoseResult.Coverage.CoverageRatio};surface={workbench.SurfaceEdgeScore?.SurfaceScore.CoverageRatio};edge={workbench.SurfaceEdgeScore?.EdgeScore.CoverageRatio}");

            var baseline = Capture(workbench);
            Check(
                "valid display path does not mutate recipe, source, selection, or execution state",
                baseline.IsDirty == initialDirty
                && ReferenceEquals(baseline.Source, initialSource)
                && ReferenceEquals(baseline.Selection, initialSelection)
                && baseline.StepReferences.SequenceEqual(initialSteps)
                && baseline.StepStates.SequenceEqual(initialStepStates)
                && baseline.RunLogCount == initialRunLogCount
                && !baseline.IsPreviewRunning
                && !baseline.IsValidationSetRunning
                && !baseline.IsOrderedRunRunning,
                $"dirty={initialDirty}->{baseline.IsDirty};steps={baseline.StepReferences.Length};selection={baseline.Selection.SelectedStepId ?? "(none)"};logs={initialRunLogCount}->{baseline.RunLogCount}");

            var malformedModel = model with
            {
                ContentSha256 = OtherSha(model.ContentSha256)
            };
            var malformedScene = scene with
            {
                ContentSha256 = OtherSha(scene.ContentSha256)
            };
            var malformedExecution = execution with
            {
                ModelContentSha256 = OtherSha(model.ContentSha256)
            };
            malformedExecution = malformedExecution with
            {
                ContentSha256 = SurfaceMatchExecutionArtifact
                    .CalculateContentSha256(malformedExecution)
            };
            var malformedAssessment = assessment with
            {
                ExecutionContentSha256 = OtherSha(execution.ContentSha256)
            };
            malformedAssessment = malformedAssessment with
            {
                ContentSha256 = SurfaceMatchAssessmentArtifact
                    .CalculateContentSha256(malformedAssessment)
            };
            var malformedRuntime = runtime with
            {
                AssessmentContentSha256 = OtherSha(assessment.ContentSha256)
            };
            var malformedScore = score with
            {
                SurfaceMatchExecutionContentSha256 = OtherSha(execution.ContentSha256)
            };
            malformedScore = malformedScore with
            {
                ContentSha256 = SurfaceAndEdgeMatchScoreArtifact
                    .CalculateContentSha256(malformedScore)
            };
            var malformedOverlay = overlay with
            {
                ScoreContentSha256 = OtherSha(score.ContentSha256)
            };
            malformedOverlay = malformedOverlay with
            {
                ContentSha256 = SurfaceEdgeDiagnosticOverlayArtifact
                    .CalculateContentSha256(malformedOverlay)
            };
            var malformedEdgeAssessment = edgeAssessment with
            {
                ScoreContentSha256 = OtherSha(score.ContentSha256)
            };
            malformedEdgeAssessment = malformedEdgeAssessment with
            {
                ContentSha256 = SurfaceAndEdgeMatchAssessmentArtifact
                    .CalculateContentSha256(malformedEdgeAssessment)
            };
            var malformedAcquisitionOrientation = acquisitionOrientation with
            {
                EdgeDiagnosticOverlayContentSha256 = OtherSha(overlay.ContentSha256)
            };
            malformedAcquisitionOrientation = malformedAcquisitionOrientation with
            {
                ContentSha256 = SurfaceEdgeAcquisitionDirectionArtifact
                    .CalculateContentSha256(malformedAcquisitionOrientation)
            };
            var malformedReview = review with
            {
                ModelContentSha256 = OtherSha(model.ContentSha256)
            };
            malformedReview = malformedReview with
            {
                ContentSha256 = SurfaceMatchFalsePositiveReviewArtifact
                    .CalculateContentSha256(malformedReview)
            };

            // Fresh malformed-linkage matrix: model/scene, assessment/runtime,
            // edge score/overlay/assessment/acquisition, and false-positive review.
            var malformedCases = new[]
            {
                new LinkageCase(
                    "model-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        malformedModel,
                        scene,
                        execution)),
                new LinkageCase(
                    "scene-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        malformedScene,
                        execution)),
                new LinkageCase(
                    "execution-model-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        malformedExecution)),
                new LinkageCase(
                    "assessment-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        execution,
                        malformedAssessment)),
                new LinkageCase(
                    "runtime-assessment-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        execution,
                        assessment,
                        malformedRuntime)),
                new LinkageCase(
                    "edge-score-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        execution,
                        edgeScore: malformedScore)),
                new LinkageCase(
                    "edge-overlay-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        execution,
                        edgeScore: score,
                        edgeDiagnosticOverlay: malformedOverlay)),
                new LinkageCase(
                    "edge-assessment-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        execution,
                        edgeScore: score,
                        edgeAssessment: malformedEdgeAssessment)),
                new LinkageCase(
                    "acquisition-direction-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        execution,
                        edgeScore: score,
                        edgeDiagnosticOverlay: overlay,
                        acquisitionDirectionOrientation: malformedAcquisitionOrientation)),
                new LinkageCase(
                    "false-positive-review-linkage",
                    current => current.ShowSurfaceMatchEvidence(
                        model,
                        scene,
                        execution,
                        edgeScore: score,
                        edgeDiagnosticOverlay: overlay,
                        edgeAssessment: edgeAssessment,
                        falsePositiveReview: malformedReview))
            };

            foreach (var malformedCase in malformedCases)
            {
                // Every malformed linkage rejects fail-closed, preserves existing
                // Published, raises no display request, and performs no
                // recipe/source/selection/execution mutation.
                var rejected = false;
                string? detail = null;
                try
                {
                    malformedCase.Invoke(workbench);
                }
                catch (InvalidDataException exception)
                {
                    rejected = true;
                    detail = exception.Message;
                }
                catch (Exception exception)
                {
                    detail = $"unexpected {exception.GetType().Name}: {exception.Message}";
                }

                var unchanged = SameProjection(workbench, baseline, out var stateDetail);
                Check(
                    $"{malformedCase.Name} rejects fail-closed and preserves existing Published",
                    rejected
                    && unchanged
                    && displayRequestCount == 1
                    && displayClearedCount == 0,
                    $"rejected={rejected};exception={detail ?? "none"};{stateDetail};displayRequests={displayRequestCount};displayCleared={displayClearedCount}");
            }
        }
        catch (Exception exception)
        {
            total++;
            lines.Add(
                $"FAIL | unexpected-exception | {exception.GetType().Name}: {exception.Message}");
        }

        var allPassed = total > 0 && total == passed;
        lines.Insert(
            0,
            $"SurfaceMatchPublishedEvidenceOwnerVerification|{(allPassed ? "PASS" : "FAIL")}|cases={total}|passed={passed}|failed={total - passed}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        summary =
            $"Surface Match Published evidence owner: {(allPassed ? "PASS" : "FAIL")} ({passed}/{total})";
        return allPassed;
    }

    private static SurfaceMatchRuntimeReport CreateRuntime(
        SurfaceMatchExecutionArtifact execution,
        SurfaceMatchAssessmentArtifact assessment) =>
        new(
            SurfaceMatchRuntimeReport.CurrentSchemaVersion,
            SurfaceMatchRuntimeReport.CurrentClock,
            execution.ContentSha256,
            assessment.ContentSha256,
            [
                new SurfaceMatchRuntimeStage(
                    SurfaceMatchRuntimeReport.PoseSearchStage,
                    1),
                new SurfaceMatchRuntimeStage(
                    SurfaceMatchRuntimeReport.ExecutionArtifactStage,
                    2),
                new SurfaceMatchRuntimeStage(
                    SurfaceMatchRuntimeReport.AcceptanceEvaluationStage,
                    3)
            ],
            6,
            new DateTimeOffset(2026, 8, 31, 0, 0, 0, TimeSpan.Zero));

    private static PublishedProjection Capture(ToolWorkbenchViewModel workbench) =>
        new(
            workbench.SurfaceMatchEvidence,
            workbench.SurfaceMatchAssessment,
            workbench.SurfaceMatchRuntime,
            workbench.SurfaceEdgeScore,
            workbench.SurfaceEdgeDiagnosticOverlay,
            workbench.SurfaceEdgeAcquisitionDirection,
            workbench.SurfaceEdgeAssessment,
            workbench.SurfaceMatchFalsePositiveReview,
            workbench.SurfaceMatchExperimentCandidate,
            workbench.IsSurfaceEdgeAcquisitionDirectionStale,
            workbench.IsDirty,
            workbench.Source,
            workbench.WorkspaceSelection.Current,
            workbench.PipelineSteps.ToArray(),
            workbench.PipelineSteps.Select(step => step.State).ToArray(),
            workbench.RunLog.Count,
            workbench.IsSelectedStepPreviewRunning,
            workbench.IsValidationSetRunning,
            workbench.IsOrderedRunRunning);

    private static bool SameProjection(
        ToolWorkbenchViewModel workbench,
        PublishedProjection expected,
        out string detail)
    {
        var sameEvidence =
            ReferenceEquals(workbench.SurfaceMatchEvidence, expected.Execution)
            && ReferenceEquals(workbench.SurfaceMatchAssessment, expected.Assessment)
            && ReferenceEquals(workbench.SurfaceMatchRuntime, expected.Runtime)
            && ReferenceEquals(workbench.SurfaceEdgeScore, expected.EdgeScore)
            && ReferenceEquals(
                workbench.SurfaceEdgeDiagnosticOverlay,
                expected.EdgeDiagnosticOverlay)
            && ReferenceEquals(
                workbench.SurfaceEdgeAcquisitionDirection,
                expected.AcquisitionDirectionOrientation)
            && ReferenceEquals(workbench.SurfaceEdgeAssessment, expected.EdgeAssessment)
            && ReferenceEquals(
                workbench.SurfaceMatchFalsePositiveReview,
                expected.FalsePositiveReview)
            && ReferenceEquals(
                workbench.SurfaceMatchExperimentCandidate,
                expected.Candidate)
            && workbench.IsSurfaceEdgeAcquisitionDirectionStale
                == expected.IsAcquisitionDirectionStale;
        var sameAuthoring =
            workbench.IsDirty == expected.IsDirty
            && ReferenceEquals(workbench.Source, expected.Source)
            && ReferenceEquals(
                workbench.WorkspaceSelection.Current,
                expected.Selection)
            && workbench.PipelineSteps.SequenceEqual(expected.StepReferences)
            && workbench.PipelineSteps.Select(step => step.State)
                .SequenceEqual(expected.StepStates)
            && workbench.RunLog.Count == expected.RunLogCount
            && workbench.IsSelectedStepPreviewRunning
                == expected.IsPreviewRunning
            && workbench.IsValidationSetRunning
                == expected.IsValidationSetRunning
            && workbench.IsOrderedRunRunning
                == expected.IsOrderedRunRunning;
        detail =
            $"published={sameEvidence};authoring={sameAuthoring};execution={workbench.SurfaceMatchEvidence?.ContentSha256 ?? "none"};candidate={workbench.SurfaceMatchExperimentCandidate?.ContentSha256 ?? "none"}";
        return sameEvidence && sameAuthoring;
    }

    private static string OtherSha(string value) =>
        value.StartsWith("0", StringComparison.Ordinal)
            ? new string('1', 64)
            : new string('0', 64);

    private sealed record LinkageCase(
        string Name,
        Action<ToolWorkbenchViewModel> Invoke);

    private sealed record PublishedProjection(
        SurfaceMatchExecutionArtifact? Execution,
        SurfaceMatchAssessmentArtifact? Assessment,
        SurfaceMatchRuntimeReport? Runtime,
        SurfaceAndEdgeMatchScoreArtifact? EdgeScore,
        SurfaceEdgeDiagnosticOverlayArtifact? EdgeDiagnosticOverlay,
        SurfaceEdgeAcquisitionDirectionArtifact? AcquisitionDirectionOrientation,
        SurfaceAndEdgeMatchAssessmentArtifact? EdgeAssessment,
        SurfaceMatchFalsePositiveReviewArtifact? FalsePositiveReview,
        SurfaceMatchExecutionArtifact? Candidate,
        bool IsAcquisitionDirectionStale,
        bool IsDirty,
        ToolWorkbenchSourceItem Source,
        InspectionWorkspaceSelectionSnapshot Selection,
        ToolWorkbenchPipelineStepItem[] StepReferences,
        string?[] StepStates,
        int RunLogCount,
        bool IsPreviewRunning,
        bool IsValidationSetRunning,
        bool IsOrderedRunRunning);
}

using System.IO;
using System.Globalization;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class SurfaceEdgeDiagnosticReviewWorkbenchParityVerification
{
    public static bool Verify(
        string artifactDirectory,
        string reportPath,
        out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Surface Edge diagnostic/review Workbench parity verification",
            "Boundary|Presentation routes identified evidence only; explicit acquisition direction classifies declared normals without changing score or acceptance; no Preview, Publish, Run, Validation, inferred viewpoint, or metrology claim."
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
            var runnerAcceptedOverlay =
                SurfaceEdgeDiagnosticReviewArtifactStore.LoadOverlay(
                    Path.Combine(directory, "edge-review.accepted.overlay.json"));
            var runnerRejectedOverlay =
                SurfaceEdgeDiagnosticReviewArtifactStore.LoadOverlay(
                    Path.Combine(directory, "edge-review.rejected.overlay.json"));
            var runnerAcceptedAssessment =
                SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                    Path.Combine(directory, "edge-review.accepted.assessment.json"));
            var runnerRejectedAssessment =
                SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                    Path.Combine(directory, "edge-review.rejected.assessment.json"));
            var runnerReview =
                SurfaceEdgeDiagnosticReviewArtifactStore.LoadReview(
                    Path.Combine(directory, "edge-review.false-positive-review.json"));

            var workbenchAcceptedOverlay =
                SurfaceEdgeDiagnosticOverlayBuilder.Build(
                    model,
                    acceptedScene,
                    acceptedExecution,
                    modelEdges,
                    acceptedSceneEdges,
                    acceptedScore);
            var workbenchRejectedOverlay =
                SurfaceEdgeDiagnosticOverlayBuilder.Build(
                    model,
                    rejectedScene,
                    rejectedExecution,
                    modelEdges,
                    rejectedSceneEdges,
                    rejectedScore);
            var workbenchAcceptedAssessment =
                SurfaceAndEdgeMatchAssessmentEvaluator.Evaluate(
                    acceptedScore,
                    runnerAcceptedAssessment.Policy);
            var workbenchRejectedAssessment =
                SurfaceAndEdgeMatchAssessmentEvaluator.Evaluate(
                    rejectedScore,
                    runnerRejectedAssessment.Policy);
            var workbenchReview = SurfaceMatchFalsePositiveReviewBuilder.Build(
                model,
                new SurfaceMatchReviewEvidenceSet(
                    runnerReview.Accepted.Label,
                    acceptedScene,
                    acceptedExecution,
                    acceptedScore,
                    workbenchAcceptedAssessment),
                new SurfaceMatchReviewEvidenceSet(
                    runnerReview.Rejected.Label,
                    rejectedScene,
                    rejectedExecution,
                    rejectedScore,
                    workbenchRejectedAssessment));
            var acquisitionDirection = new ToolRecipeAcquisitionDirection(
                ToolRecipeAcquisitionDirectionState.Available,
                ToolRecipeAcquisitionDirectionConvention.SensorToScene,
                workbenchRejectedOverlay.TargetFrameId,
                new ToolRecipeXyz(0.0, 0.0, -1.0));
            var acquisitionOrientation = SurfaceEdgeAcquisitionDirectionBuilder.Build(
                workbenchRejectedOverlay,
                new string('A', 64),
                acquisitionDirection);

            Check(
                "runner-workbench-accepted-overlay-parity",
                runnerAcceptedOverlay.ContentSha256
                    == workbenchAcceptedOverlay.ContentSha256,
                $"runner={runnerAcceptedOverlay.ContentSha256};workbench={workbenchAcceptedOverlay.ContentSha256}");
            Check(
                "runner-workbench-rejected-overlay-parity",
                runnerRejectedOverlay.ContentSha256
                    == workbenchRejectedOverlay.ContentSha256,
                $"runner={runnerRejectedOverlay.ContentSha256};workbench={workbenchRejectedOverlay.ContentSha256}");
            Check(
                "runner-workbench-accepted-assessment-parity",
                runnerAcceptedAssessment.ContentSha256
                    == workbenchAcceptedAssessment.ContentSha256,
                $"runner={runnerAcceptedAssessment.ContentSha256};workbench={workbenchAcceptedAssessment.ContentSha256}");
            Check(
                "runner-workbench-rejected-assessment-parity",
                runnerRejectedAssessment.ContentSha256
                    == workbenchRejectedAssessment.ContentSha256,
                $"runner={runnerRejectedAssessment.ContentSha256};workbench={workbenchRejectedAssessment.ContentSha256}");
            Check(
                "runner-workbench-review-parity",
                runnerReview.ContentSha256 == workbenchReview.ContentSha256,
                $"runner={runnerReview.ContentSha256};workbench={workbenchReview.ContentSha256}");

            var workbench = new ToolWorkbenchViewModel();
            var initialDirty = workbench.IsDirty;
            var initialStepCount = workbench.PipelineSteps.Count;
            ToolWorkbenchSurfaceMatchDisplayRequestEventArgs? request = null;
            var requestCount = 0;
            var clearCount = 0;
            workbench.SurfaceMatchDisplayRequested += (_, args) =>
            {
                request = args;
                requestCount++;
            };
            workbench.SurfaceMatchDisplayCleared += (_, _) => clearCount++;
            workbench.ShowSurfaceMatchEvidence(
                model,
                rejectedScene,
                rejectedExecution,
                edgeScore: rejectedScore,
                edgeDiagnosticOverlay: workbenchRejectedOverlay,
                edgeAssessment: workbenchRejectedAssessment,
                falsePositiveReview: workbenchReview,
                acquisitionDirectionOrientation: acquisitionOrientation);
            Check(
                "workbench-owns-complete-rejected-review-evidence",
                ReferenceEquals(workbench.SurfaceEdgeScore, rejectedScore)
                && ReferenceEquals(workbench.SurfaceEdgeDiagnosticOverlay, workbenchRejectedOverlay)
                && ReferenceEquals(workbench.SurfaceEdgeAcquisitionDirection, acquisitionOrientation)
                && ReferenceEquals(workbench.SurfaceEdgeAssessment, workbenchRejectedAssessment)
                && ReferenceEquals(workbench.SurfaceMatchFalsePositiveReview, workbenchReview),
                $"score={workbench.SurfaceEdgeScore?.ContentSha256};overlay={workbench.SurfaceEdgeDiagnosticOverlay?.ContentSha256};assessment={workbench.SurfaceEdgeAssessment?.ContentSha256};review={workbench.SurfaceMatchFalsePositiveReview?.ContentSha256}");
            Check(
                "workbench-routes-complete-review-once",
                requestCount == 1
                && request is not null
                && ReferenceEquals(request.EdgeScore, rejectedScore)
                && ReferenceEquals(request.EdgeDiagnosticOverlay, workbenchRejectedOverlay)
                && ReferenceEquals(request.AcquisitionDirectionOrientation, acquisitionOrientation)
                && ReferenceEquals(request.EdgeAssessment, workbenchRejectedAssessment)
                && ReferenceEquals(request.FalsePositiveReview, workbenchReview),
                $"requests={requestCount}");
            Check(
                "review-display-is-presentation-only",
                workbench.IsDirty == initialDirty
                && workbench.PipelineSteps.Count == initialStepCount
                && workbench.SelectedPipelineStep is null,
                $"dirty={workbench.IsDirty};steps={workbench.PipelineSteps.Count}");
            Check(
                "rejected-review-keeps-independent-reason",
                workbench.SurfaceEdgeAssessment?.Decision
                    == SurfaceMatchDecision.Fail
                && workbench.SurfaceEdgeAssessment?.Surface.Decision
                    == SurfaceMatchDecision.Pass
                && workbench.SurfaceEdgeAssessment?.Edge.Decision
                    == SurfaceMatchDecision.Fail
                && workbench.SurfaceEdgeAssessment?.Reason
                    == SurfaceAndEdgeDecisionReason.EdgeCoverageBelowMinimum,
                $"decision={workbench.SurfaceEdgeAssessment?.Decision};reason={workbench.SurfaceEdgeAssessment?.Reason}");
            Check(
                "explicit-direction-orientation-keeps-raw-evidence-identities",
                acquisitionOrientation.EdgeDiagnosticOverlayContentSha256
                    == workbenchRejectedOverlay.ContentSha256
                && workbench.SurfaceEdgeScore?.ContentSha256
                    == rejectedScore.ContentSha256
                && workbench.SurfaceEdgeAssessment?.ContentSha256
                    == workbenchRejectedAssessment.ContentSha256,
                $"orientation={acquisitionOrientation.ContentSha256};overlay={workbenchRejectedOverlay.ContentSha256};score={rejectedScore.ContentSha256};assessment={workbenchRejectedAssessment.ContentSha256}");

            var draft = new SurfaceMatchStepProperties();
            var defaultsValid = draft.TryCreateIndependentContracts(
                out var defaultSearch,
                out var defaultPolicy,
                out var defaultMessage);
            Check(
                "property-grid-defaults-own-independent-limits",
                defaultsValid
                && defaultSearch is not null
                && defaultPolicy is not null
                && defaultPolicy.Surface.MinimumCoverageRatio == 0.9
                && defaultPolicy.Surface.MaximumInlierRmse == 0.25
                && defaultPolicy.Edge.MinimumCoverageRatio == 0.9
                && defaultPolicy.Edge.MaximumInlierRmse == 0.25,
                defaultMessage);
            draft.MinimumCoverageRatio = 0.95;
            draft.MaximumInlierRmse = 0.2;
            draft.MinimumEdgeCoverageRatio = 0.8;
            draft.MaximumEdgeInlierRmse = 0.04;
            var authoredValid = draft.TryCreateIndependentContracts(
                out _,
                out var authoredPolicy,
                out var authoredMessage);
            Check(
                "property-grid-authors-components-independently",
                authoredValid
                && authoredPolicy is not null
                && authoredPolicy.Surface.MinimumCoverageRatio == 0.95
                && authoredPolicy.Surface.MaximumInlierRmse == 0.2
                && authoredPolicy.Edge.MinimumCoverageRatio == 0.8
                && authoredPolicy.Edge.MaximumInlierRmse == 0.04,
                authoredMessage);
            var recipeParameters = draft.ToRecipeParameters();
            Check(
                "recipe-retains-all-four-limits",
                Nearly(Parse(recipeParameters["MinimumCoverageRatio"]), 0.95)
                && Nearly(Parse(recipeParameters["MaximumInlierRmse"]), 0.2)
                && Nearly(Parse(recipeParameters["MinimumEdgeCoverageRatio"]), 0.8)
                && Nearly(Parse(recipeParameters["MaximumEdgeInlierRmse"]), 0.04),
                string.Join(';', recipeParameters
                    .Where(pair => pair.Key.Contains("Coverage", StringComparison.Ordinal)
                        || pair.Key.Contains("InlierRmse", StringComparison.Ordinal))
                    .Select(pair => $"{pair.Key}={pair.Value}")));

            var logsBeforeDirectionApply = workbench.RunLog.Count;
            workbench.SourceQuality.LoadAcquisitionProvenance(
                workbench.SourceAcquisitionProvenance,
                workbench.Source.FrameId);
            workbench.SourceQuality.SelectedAcquisitionStateOption =
                workbench.SourceQuality.AcquisitionStateOptions.Single(option =>
                    option.State == ToolRecipeAcquisitionProvenanceState.Available);
            workbench.SourceQuality.SelectedAcquisitionDirectionStateOption =
                workbench.SourceQuality.AcquisitionDirectionStateOptions.Single(option =>
                    option.State == ToolRecipeAcquisitionDirectionState.Available);
            workbench.SourceQuality.AcquisitionEvidenceDraft =
                "Operator-confirmed acquisition direction for stale-evidence verification.";
            workbench.SourceQuality.AcquisitionLimitationNotesDraft =
                "Direction only; camera pose and calibration remain unavailable.";
            workbench.SourceQuality.AcquisitionDirectionXDraft = "0";
            workbench.SourceQuality.AcquisitionDirectionYDraft = "0";
            workbench.SourceQuality.AcquisitionDirectionZDraft = "1";
            workbench.SourceQuality.ApplyAcquisitionProvenanceCommand.Execute(null);
            Check(
                "direction-change-invalidates-only-orientation-evidence",
                workbench.SurfaceEdgeAcquisitionDirection is null
                && workbench.IsSurfaceEdgeAcquisitionDirectionStale
                && workbench.SurfaceEdgeDiagnosticOverlay?.ContentSha256
                    == workbenchRejectedOverlay.ContentSha256
                && workbench.SurfaceEdgeScore?.ContentSha256
                    == rejectedScore.ContentSha256
                && workbench.SurfaceEdgeAssessment?.ContentSha256
                    == workbenchRejectedAssessment.ContentSha256
                && request?.AcquisitionDirectionOrientation is null,
                $"stale={workbench.IsSurfaceEdgeAcquisitionDirectionStale};orientation={workbench.SurfaceEdgeAcquisitionDirection?.ContentSha256 ?? "none"};overlay={workbench.SurfaceEdgeDiagnosticOverlay?.ContentSha256};requests={requestCount}");
            Check(
                "direction-apply-does-not-execute-surface-match",
                workbench.RunLog.Count == logsBeforeDirectionApply
                && !workbench.IsSelectedStepPreviewRunning
                && !workbench.IsValidationSetRunning,
                $"logs={logsBeforeDirectionApply}->{workbench.RunLog.Count};preview={workbench.IsSelectedStepPreviewRunning};validation={workbench.IsValidationSetRunning}");

            var dirtyBeforeClear = workbench.IsDirty;
            workbench.ClearSurfaceMatchEvidence();
            Check(
                "clear-removes-review-without-editing",
                !workbench.HasSurfaceMatchEvidence
                && workbench.SurfaceEdgeScore is null
                && workbench.SurfaceEdgeDiagnosticOverlay is null
                && workbench.SurfaceEdgeAssessment is null
                && workbench.SurfaceMatchFalsePositiveReview is null
                && clearCount == 1
                && workbench.IsDirty == dirtyBeforeClear
                && workbench.PipelineSteps.Count == initialStepCount,
                $"clear={clearCount};dirty={workbench.IsDirty};steps={workbench.PipelineSteps.Count}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | unexpected-exception | {exception.GetType().Name}: {exception.Message}");
        }

        var allPassed = total > 0 && total == passed;
        lines.Insert(
            0,
            $"SurfaceEdgeDiagnosticReviewWorkbenchParityVerification|{(allPassed ? "PASS" : "FAIL")}|cases={total}|passed={passed}|failed={total - passed}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory);
        File.WriteAllLines(fullReportPath, lines, new UTF8Encoding(false));
        summary =
            $"Surface edge diagnostic/review Workbench parity: {(allPassed ? "PASS" : "FAIL")} ({passed}/{total})";
        return allPassed;
    }

    private static double Parse(string value) =>
        double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static bool Nearly(double first, double second) =>
        double.IsFinite(first)
        && Math.Abs(first - second) <= 1e-12;
}

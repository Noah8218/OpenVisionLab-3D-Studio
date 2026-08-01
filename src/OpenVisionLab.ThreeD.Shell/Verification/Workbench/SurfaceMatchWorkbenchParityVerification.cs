using System.IO;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell;

internal static class SurfaceMatchWorkbenchParityVerification
{
    public static bool Verify(
        string modelPath,
        string scenePath,
        string runnerExecutionPath,
        string reportPath,
        out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(scenePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(runnerExecutionPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Surface Match Workbench/Runner parity verification",
            "Boundary|Deterministic local fixture; separate authored acceptance over raw evidence; no automatic Preview, Publish, Run, Validation, performance-budget, or physical metrology claim."
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

            lines.Add(
                $"{(condition ? "PASS" : "FAIL")} | {name} | {evidence}");
        }

        try
        {
            var model = SurfaceModelArtifactStore.Load(modelPath);
            var scene = PreparedSceneArtifactStore.Load(scenePath);
            var runnerExecution =
                SurfaceMatchExecutionArtifactStore.Load(
                    runnerExecutionPath);
            var artifactDirectory =
                Path.GetDirectoryName(
                    Path.GetFullPath(runnerExecutionPath))
                ?? Environment.CurrentDirectory;
            var assessmentPath = Path.Combine(
                artifactDirectory,
                "known-pose.surface-match-assessment.json");
            var runtimePath = Path.Combine(
                artifactDirectory,
                "known-pose.surface-match-runtime.json");
            var runnerAssessment = File.Exists(assessmentPath)
                ? SurfaceMatchAssessmentArtifactStore.Load(
                    assessmentPath)
                : null;
            var runnerRuntime = File.Exists(runtimePath)
                ? SurfaceMatchAssessmentArtifactStore.LoadRuntime(
                    runtimePath)
                : null;
            var workbenchEvaluation = runnerAssessment is null
                ? null
                : SurfaceMatchEvaluationExecutor.Execute(
                    model,
                    scene,
                    runnerExecution.PoseResult.Parameters,
                    runnerAssessment.Policy);
            var workbenchExecution =
                workbenchEvaluation?.Execution
                ?? SurfaceMatchExecutor.Execute(
                    model,
                    scene,
                    runnerExecution.PoseResult.Parameters);
            var workbench = new ToolWorkbenchViewModel();
            var initialDirty = workbench.IsDirty;
            var initialStepCount = workbench.PipelineSteps.Count;
            ToolWorkbenchSurfaceMatchDisplayRequestEventArgs? request = null;
            var requestCount = 0;
            var clearRequestCount = 0;
            workbench.SurfaceMatchDisplayRequested += (_, args) =>
            {
                request = args;
                requestCount++;
            };
            workbench.SurfaceMatchDisplayCleared += (_, _) =>
                clearRequestCount++;

            workbench.ShowSurfaceMatchEvidence(
                model,
                scene,
                workbenchExecution,
                workbenchEvaluation?.Assessment,
                workbenchEvaluation?.Runtime);

            Check(
                "runner-and-workbench-pose-hash-match",
                runnerExecution.PoseResult.ContentSha256
                    == workbenchExecution.PoseResult.ContentSha256,
                $"runner={runnerExecution.PoseResult.ContentSha256};workbench={workbenchExecution.PoseResult.ContentSha256}");
            Check(
                "runner-and-workbench-coverage-match",
                runnerExecution.PoseResult.Coverage
                    .CoverageRatio
                    == workbenchExecution.PoseResult.Coverage
                        .CoverageRatio
                && runnerExecution.PoseResult.Coverage
                    .MatchedModelSampleCount
                    == workbenchExecution.PoseResult.Coverage
                        .MatchedModelSampleCount
                && runnerExecution.PoseResult.Coverage.InlierRmse
                    == workbenchExecution.PoseResult.Coverage
                        .InlierRmse,
                $"runner={runnerExecution.PoseResult.Coverage.Evidence};workbench={workbenchExecution.PoseResult.Coverage.Evidence}");
            Check(
                "runner-and-workbench-overlay-hash-match",
                runnerExecution.Overlay?.ContentSha256
                    == workbenchExecution.Overlay?.ContentSha256,
                $"runner={runnerExecution.Overlay?.ContentSha256};workbench={workbenchExecution.Overlay?.ContentSha256}");
            Check(
                "runner-and-workbench-execution-hash-match",
                runnerExecution.ContentSha256
                    == workbenchExecution.ContentSha256,
                $"runner={runnerExecution.ContentSha256};workbench={workbenchExecution.ContentSha256}");
            Check(
                "workbench-owns-identified-evidence",
                ReferenceEquals(
                    workbench.SurfaceMatchEvidence,
                    workbenchExecution)
                && workbench.HasSurfaceMatchEvidence,
                $"hasEvidence={workbench.HasSurfaceMatchEvidence};sha256={workbench.SurfaceMatchEvidence?.ContentSha256}");
            Check(
                "workbench-raises-one-display-request",
                requestCount == 1
                && request is not null
                && ReferenceEquals(request.Model, model)
                && ReferenceEquals(request.Scene, scene)
                && ReferenceEquals(
                    request.Execution,
                    workbenchExecution)
                && ReferenceEquals(
                    request.Assessment,
                    workbenchEvaluation?.Assessment)
                && ReferenceEquals(
                    request.Runtime,
                    workbenchEvaluation?.Runtime),
                $"requestCount={requestCount}");
            Check(
                "display-routing-does-not-edit-recipe",
                workbench.IsDirty == initialDirty
                && workbench.PipelineSteps.Count == initialStepCount
                && workbench.SelectedPipelineStep is null,
                $"dirty={workbench.IsDirty};steps={workbench.PipelineSteps.Count};selected={workbench.SelectedPipelineStep?.Id ?? "(none)"}");
            Check(
                "display-evidence-is-decision-free",
                workbenchExecution.Semantics
                    == SurfaceMatchExecutionArtifact.CurrentSemantics
                && !workbenchExecution.Semantics.Contains(
                    "Pass",
                    StringComparison.OrdinalIgnoreCase)
                && !workbenchExecution.Semantics.Contains(
                    "Fail",
                    StringComparison.OrdinalIgnoreCase),
                workbenchExecution.Semantics);

            workbench.ClearSurfaceMatchEvidence();
            Check(
                "clear-is-presentation-only",
                !workbench.HasSurfaceMatchEvidence
                && workbench.SurfaceMatchEvidence is null
                && clearRequestCount == 1
                && workbench.IsDirty == initialDirty
                && workbench.PipelineSteps.Count == initialStepCount,
                $"hasEvidence={workbench.HasSurfaceMatchEvidence};clearRequestCount={clearRequestCount};dirty={workbench.IsDirty};steps={workbench.PipelineSteps.Count}");
            Check(
                "shared-execution-validates-after-parity",
                SurfaceMatchExecutionArtifactValidator
                    .Inspect(workbenchExecution)
                    .IsValid,
                SurfaceMatchExecutionArtifactValidator
                    .Inspect(workbenchExecution)
                    .Evidence);

            if (runnerAssessment is not null
                && runnerRuntime is not null
                && workbenchEvaluation is not null)
            {
                Check(
                    "runner-and-workbench-assessment-hash-match",
                    runnerAssessment.ContentSha256
                        == workbenchEvaluation.Assessment
                            .ContentSha256,
                    $"runner={runnerAssessment.ContentSha256};workbench={workbenchEvaluation.Assessment.ContentSha256}");
                Check(
                    "runtime-is-linked-but-not-parity-hashed",
                    runnerRuntime.ExecutionContentSha256
                        == runnerExecution.ContentSha256
                    && runnerRuntime.AssessmentContentSha256
                        == runnerAssessment.ContentSha256
                    && workbenchEvaluation.Runtime
                        .ExecutionContentSha256
                        == workbenchExecution.ContentSha256
                    && workbenchEvaluation.Runtime
                        .AssessmentContentSha256
                        == workbenchEvaluation.Assessment
                            .ContentSha256,
                    $"runnerTicks={runnerRuntime.TotalElapsedTicks};workbenchTicks={workbenchEvaluation.Runtime.TotalElapsedTicks};deterministicRuntimeParity=false");

                var experiment = new ToolWorkbenchViewModel();
                var experimentTool = experiment.Tools.Single(tool =>
                    tool.Id == "surface-match");
                experiment.SetC3DSource(
                    FindRepoFile(
                        "3D",
                        "Samples",
                        "ThicknessCouponV1",
                        "thickness-coupon-v1.C3D"),
                    markDirty: false);
                experiment.AddSelectedToolCommand.Execute(
                    experimentTool);
                var experimentDraft =
                    experiment.SelectedStepPropertyDraft
                        as SurfaceMatchStepProperties;
                ApplySurfaceMatchContracts(
                    experimentDraft!,
                    runnerExecution.PoseResult.Parameters,
                    runnerAssessment.Policy);
                experiment.MarkSelectedStepParameterDraftDirty();
                var experimentApplied =
                    experiment.TryApplySelectedStepParameterDraft(
                        out var experimentApplyMessage);
                var experimentDisplayRequests = 0;
                ToolWorkbenchSurfaceMatchDisplayRequestEventArgs?
                    experimentRequest = null;
                experiment.SurfaceMatchDisplayRequested += (_, args) =>
                {
                    experimentDisplayRequests++;
                    experimentRequest = args;
                };
                experiment.ShowSurfaceMatchEvidence(
                    model,
                    scene,
                    runnerExecution,
                    runnerAssessment,
                    runnerRuntime);
                var publishedBeforePreview =
                    experiment.SurfaceMatchEvidence;
                var dirtyBeforePreview = experiment.IsDirty;
                var parametersBeforePreview = string.Join(
                    "|",
                    experiment.SelectedPipelineStep!.Parameters.Select(
                        parameter =>
                            $"{parameter.Name}={parameter.Value}"));
                var displayCountBeforePreview =
                    experimentDisplayRequests;
                var previewed = Task.Run(
                        experiment
                            .PreviewSelectedSurfaceMatchExperimentAsync)
                    .GetAwaiter()
                    .GetResult();
                var candidateExecution =
                    experiment.SurfaceMatchExperimentCandidate;
                var candidateRequest = experimentRequest;
                Check(
                    "experiment-preview-creates-one-temporary-candidate",
                    experimentApplied
                    && previewed
                    && candidateExecution is not null
                    && experiment.HasSurfaceMatchExperimentCandidate
                    && !experiment.IsSurfaceMatchExperimentCandidateStale
                    && experiment.IsSurfaceMatchExperimentCandidateDisplayed
                    && experimentDisplayRequests
                        == displayCountBeforePreview + 1
                    && ReferenceEquals(
                        candidateRequest?.Execution,
                        candidateExecution),
                    $"applied={experimentApplied};previewed={previewed};candidate={candidateExecution?.ContentSha256 ?? "(none)"};displayRequests={experimentDisplayRequests};apply={experimentApplyMessage}");
                Check(
                    "experiment-preview-preserves-published-and-recipe",
                    ReferenceEquals(
                        experiment.SurfaceMatchEvidence,
                        publishedBeforePreview)
                    && ReferenceEquals(
                        experiment.SurfaceMatchAssessment,
                        runnerAssessment)
                    && ReferenceEquals(
                        experiment.SurfaceMatchRuntime,
                        runnerRuntime)
                    && experiment.IsDirty == dirtyBeforePreview
                    && parametersBeforePreview == string.Join(
                        "|",
                        experiment.SelectedPipelineStep.Parameters.Select(
                            parameter =>
                                $"{parameter.Name}={parameter.Value}")),
                    $"published={experiment.SurfaceMatchEvidence?.ContentSha256};dirty={experiment.IsDirty};parametersUnchanged={parametersBeforePreview == string.Join("|", experiment.SelectedPipelineStep.Parameters.Select(parameter => $"{parameter.Name}={parameter.Value}"))}");

                var displayCountBeforeSwitch =
                    experimentDisplayRequests;
                experiment.ShowPublishedSurfaceMatchExperimentCommand
                    .Execute(null);
                var publishedRequest = experimentRequest;
                experiment.ShowCandidateSurfaceMatchExperimentCommand
                    .Execute(null);
                Check(
                    "experiment-view-switch-is-presentation-only",
                    experimentDisplayRequests
                        == displayCountBeforeSwitch + 2
                    && ReferenceEquals(
                        publishedRequest?.Execution,
                        publishedBeforePreview)
                    && ReferenceEquals(
                        experimentRequest?.Execution,
                        candidateExecution)
                    && experiment.IsDirty == dirtyBeforePreview
                    && experiment.HasSurfaceMatchExperimentCandidate,
                    $"displayRequests={experimentDisplayRequests};dirty={experiment.IsDirty};candidate={experiment.HasSurfaceMatchExperimentCandidate}");

                var candidateAssessment = candidateRequest?.Assessment;
                var candidateRuntime = candidateRequest?.Runtime;
                var displayCountBeforePublish =
                    experimentDisplayRequests;
                var canPublish = experiment.PublishSelectedStepCommand
                    .CanExecute(null);
                experiment.PublishSelectedStepCommand.Execute(null);
                Check(
                    "experiment-publish-promotes-exact-preview-without-rerun",
                    canPublish
                    && candidateExecution is not null
                    && ReferenceEquals(
                        experiment.SurfaceMatchEvidence,
                        candidateExecution)
                    && ReferenceEquals(
                        experiment.SurfaceMatchAssessment,
                        candidateAssessment)
                    && ReferenceEquals(
                        experiment.SurfaceMatchRuntime,
                        candidateRuntime)
                    && !experiment.HasSurfaceMatchExperimentCandidate
                    && experimentDisplayRequests
                        == displayCountBeforePublish + 1
                    && ReferenceEquals(
                        experimentRequest?.Execution,
                        candidateExecution),
                    $"canPublish={canPublish};published={experiment.SurfaceMatchEvidence?.ContentSha256};candidateCleared={!experiment.HasSurfaceMatchExperimentCandidate};displayRequests={experimentDisplayRequests}");

                var publishedAfterPublish =
                    experiment.SurfaceMatchEvidence;
                var secondPreviewed = Task.Run(
                        experiment
                            .PreviewSelectedSurfaceMatchExperimentAsync)
                    .GetAwaiter()
                    .GetResult();
                var staleDraft =
                    experiment.SelectedStepPropertyDraft
                        as SurfaceMatchStepProperties;
                staleDraft!.MinimumCoverageRatio =
                    staleDraft.MinimumCoverageRatio >= 0.99
                        ? 0.98
                        : staleDraft.MinimumCoverageRatio + 0.01;
                experiment.MarkSelectedStepParameterDraftDirty();
                var staleApplied =
                    experiment.TryApplySelectedStepParameterDraft(
                        out var staleApplyMessage);
                Check(
                    "experiment-parameter-change-stales-candidate-and-restores-published",
                    secondPreviewed
                    && staleApplied
                    && experiment.HasSurfaceMatchExperimentCandidate
                    && experiment.IsSurfaceMatchExperimentCandidateStale
                    && !experiment.IsSurfaceMatchExperimentCandidateDisplayed
                    && !experiment.PublishSelectedStepCommand.CanExecute(null)
                    && ReferenceEquals(
                        experiment.SurfaceMatchEvidence,
                        publishedAfterPublish)
                    && ReferenceEquals(
                        experimentRequest?.Execution,
                        publishedAfterPublish),
                    $"secondPreviewed={secondPreviewed};applied={staleApplied};stale={experiment.IsSurfaceMatchExperimentCandidateStale};publishEnabled={experiment.PublishSelectedStepCommand.CanExecute(null)};message={staleApplyMessage}");

                experiment.DiscardSurfaceMatchExperimentCommand.Execute(
                    null);
                Check(
                    "experiment-discard-keeps-published-baseline",
                    !experiment.HasSurfaceMatchExperimentCandidate
                    && !experiment.IsSurfaceMatchExperimentCandidateStale
                    && !experiment.IsSurfaceMatchExperimentCandidateDisplayed
                    && ReferenceEquals(
                        experiment.SurfaceMatchEvidence,
                        publishedAfterPublish)
                    && ReferenceEquals(
                        experimentRequest?.Execution,
                        publishedAfterPublish),
                    $"candidate={experiment.HasSurfaceMatchExperimentCandidate};published={experiment.SurfaceMatchEvidence?.ContentSha256}");

                var transientRecipePath = Path.Combine(
                    Path.GetDirectoryName(
                        Path.GetFullPath(reportPath))
                    ?? Environment.CurrentDirectory,
                    "surface-match-experiment-transient.ov3d-recipe.json");
                var experimentSaved =
                    experiment.TrySaveTeachingRecipe(
                        transientRecipePath,
                        out var experimentSaveMessage);
                var experimentReopened = new ToolWorkbenchViewModel();
                var reopenedExperimentDisplayRequests = 0;
                experimentReopened.SurfaceMatchDisplayRequested +=
                    (_, _) => reopenedExperimentDisplayRequests++;
                var experimentOpened = experimentSaved
                    && experimentReopened.TryOpenTeachingRecipe(
                        transientRecipePath,
                        out _);
                Check(
                    "experiment-evidence-is-not-persisted-or-auto-executed",
                    experimentOpened
                    && !experimentReopened.HasSurfaceMatchEvidence
                    && !experimentReopened
                        .HasSurfaceMatchExperimentCandidate
                    && reopenedExperimentDisplayRequests == 0,
                    $"saved={experimentSaved};opened={experimentOpened};hasEvidence={experimentReopened.HasSurfaceMatchEvidence};hasCandidate={experimentReopened.HasSurfaceMatchExperimentCandidate};displayRequests={reopenedExperimentDisplayRequests};save={experimentSaveMessage}");
            }

            var authoring = new ToolWorkbenchViewModel();
            var surfaceMatchTool = authoring.Tools.Single(tool =>
                tool.Id == "surface-match");
            var authoringDisplayRequests = 0;
            authoring.SurfaceMatchDisplayRequested += (_, _) =>
                authoringDisplayRequests++;
            authoring.SetC3DSource(
                FindRepoFile(
                    "3D",
                    "Samples",
                    "ThicknessCouponV1",
                    "thickness-coupon-v1.C3D"),
                markDirty: false);
            authoring.AddSelectedToolCommand.Execute(
                surfaceMatchTool);
            var draft =
                authoring.SelectedStepPropertyDraft
                    as SurfaceMatchStepProperties;
            RigidSurfacePoseSearchParameters? defaultSearch = null;
            SurfaceMatchAcceptancePolicy? defaultPolicy = null;
            var defaultContractsValid = draft is not null
                && draft.TryCreateContracts(
                    out defaultSearch,
                    out defaultPolicy,
                    out _)
                && defaultSearch is not null
                && defaultPolicy is not null;
            Check(
                "surface-match-property-grid-separates-policy-and-search",
                defaultContractsValid
                && defaultSearch!.MaximumCorrespondenceDistance == 1.0
                && defaultPolicy!.MinimumCoverageRatio == 0.9
                && defaultPolicy.MaximumInlierRmse == 0.25,
                $"draft={draft is not null};searchDistance={defaultSearch?.MaximumCorrespondenceDistance};minimumCoverage={defaultPolicy?.MinimumCoverageRatio};maximumRmse={defaultPolicy?.MaximumInlierRmse}");

            draft!.MinimumCoverageRatio = 0.92;
            draft.MaximumInlierRmse = 0.2;
            draft.MinimumRotationZDegrees = -30.0;
            draft.MaximumRotationZDegrees = 30.0;
            draft.RotationStepZDegrees = 10.0;
            draft.MinimumTranslationX = 8.0;
            draft.MaximumTranslationX = 12.0;
            draft.MaximumCandidateCount = 500;
            authoring.MarkSelectedStepParameterDraftDirty();
            var applied =
                authoring.TryApplySelectedStepParameterDraft(
                    out var applyMessage);
            Check(
                "property-apply-is-authoring-only",
                applied
                && authoringDisplayRequests == 0
                && !authoring.HasSurfaceMatchEvidence
                && applyMessage.Contains(
                    "Preview and Publish were not run",
                    StringComparison.Ordinal),
                $"applied={applied};displayRequests={authoringDisplayRequests};hasEvidence={authoring.HasSurfaceMatchEvidence};message={applyMessage}");

            var recipePath = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(reportPath))
                    ?? Environment.CurrentDirectory,
                "surface-match-authored-bounds.ov3d-recipe.json");
            var saved = authoring.TrySaveTeachingRecipe(
                recipePath,
                out var saveMessage);
            var reopened = new ToolWorkbenchViewModel();
            var reopenedDisplayRequests = 0;
            reopened.SurfaceMatchDisplayRequested += (_, _) =>
                reopenedDisplayRequests++;
            var opened = saved
                && reopened.TryOpenTeachingRecipe(
                    recipePath,
                    out var openMessage);
            var reopenedDraft =
                reopened.SelectedStepPropertyDraft
                    as SurfaceMatchStepProperties;
            Check(
                "authored-bounds-save-reload-round-trip",
                opened
                && reopenedDraft is not null
                && reopenedDraft.MinimumCoverageRatio == 0.92
                && reopenedDraft.MaximumInlierRmse == 0.2
                && reopenedDraft.MinimumRotationZDegrees == -30.0
                && reopenedDraft.MaximumRotationZDegrees == 30.0
                && reopenedDraft.RotationStepZDegrees == 10.0
                && reopenedDraft.MinimumTranslationX == 8.0
                && reopenedDraft.MaximumTranslationX == 12.0
                && reopenedDraft.MaximumCandidateCount == 500,
                $"saved={saved};opened={opened};save={saveMessage};open={(opened ? "ok" : "failed")}");
            Check(
                "reload-does-not-execute-match",
                opened
                && reopenedDisplayRequests == 0
                && !reopened.HasSurfaceMatchEvidence
                && reopened.SelectedPipelineStep?.State
                    == "Taught / pending",
                $"opened={opened};displayRequests={reopenedDisplayRequests};hasEvidence={reopened.HasSurfaceMatchEvidence};state={reopened.SelectedPipelineStep?.State}");
        }
        catch (Exception exception)
        {
            lines.Add(
                $"FAIL | unexpected-exception | {exception.GetType().Name}: {exception.Message}");
        }

        var allPassed = passed == total && total > 0;
        lines.Insert(
            0,
            $"SurfaceMatchWorkbenchParityVerification|{(allPassed ? "PASS" : "FAIL")}|cases={total}|passed={passed}|failed={total - passed}");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullReportPath)
            ?? Environment.CurrentDirectory);
        File.WriteAllLines(
            fullReportPath,
            lines,
            new UTF8Encoding(false));
        summary =
            $"Surface match Workbench/Runner parity: "
            + $"{(allPassed ? "PASS" : "FAIL")} "
            + $"({passed}/{total})";
        return allPassed;
    }

    private static string FindRepoFile(params string[] segments)
    {
        foreach (var start in new[]
                 {
                     Environment.CurrentDirectory,
                     AppContext.BaseDirectory
                 })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = segments.Aggregate(
                    directory.FullName,
                    Path.Combine);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException(
            $"Repository fixture was not found: {Path.Combine(segments)}");
    }

    private static void ApplySurfaceMatchContracts(
        SurfaceMatchStepProperties target,
        RigidSurfacePoseSearchParameters search,
        SurfaceMatchAcceptancePolicy policy)
    {
        target.MinimumCoverageRatio = policy.MinimumCoverageRatio;
        target.MaximumInlierRmse = policy.MaximumInlierRmse;
        target.MinimumRotationXDegrees = search.MinimumRotationXDegrees;
        target.MaximumRotationXDegrees = search.MaximumRotationXDegrees;
        target.RotationStepXDegrees = search.RotationStepXDegrees;
        target.MinimumRotationYDegrees = search.MinimumRotationYDegrees;
        target.MaximumRotationYDegrees = search.MaximumRotationYDegrees;
        target.RotationStepYDegrees = search.RotationStepYDegrees;
        target.MinimumRotationZDegrees = search.MinimumRotationZDegrees;
        target.MaximumRotationZDegrees = search.MaximumRotationZDegrees;
        target.RotationStepZDegrees = search.RotationStepZDegrees;
        target.MinimumTranslationX = search.MinimumTranslationX;
        target.MaximumTranslationX = search.MaximumTranslationX;
        target.MinimumTranslationY = search.MinimumTranslationY;
        target.MaximumTranslationY = search.MaximumTranslationY;
        target.MinimumTranslationZ = search.MinimumTranslationZ;
        target.MaximumTranslationZ = search.MaximumTranslationZ;
        target.MaximumCorrespondenceDistance =
            search.MaximumCorrespondenceDistance;
        target.MinimumMatchedSampleCount =
            search.MinimumMatchedSampleCount;
        target.MaximumCandidateCount = search.MaximumCandidateCount;
    }
}

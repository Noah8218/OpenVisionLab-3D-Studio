using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Coordination;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns the command-line Surface Match Smoke evidence policy. The Shell Window
/// remains responsible for failure/shutdown wiring, while this owner keeps
/// artifact loading and Workbench evidence projection independent of WPF.
/// </summary>
internal static class ShellSurfaceMatchSmoke
{
    public static bool TryConfigureEvidenceFromCommandLine(
        string[] arguments,
        ToolWorkbenchViewModel workbench,
        out string failure)
        => TryConfigureEvidenceFromCommandLine(
            new ShellCommandLineArguments(arguments),
            workbench,
            out failure);

    internal static bool TryConfigureEvidenceFromCommandLine(
        ShellCommandLineArguments commandLine,
        ToolWorkbenchViewModel workbench,
        out string failure)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        ArgumentNullException.ThrowIfNull(workbench);

        failure = string.Empty;
        var modelPath = commandLine.GetValue("--smoke-surface-match-model");
        var scenePath = commandLine.GetValue("--smoke-surface-match-scene");
        var executionPath = commandLine.GetValue("--smoke-surface-match-execution");
        var assessmentPath = commandLine.GetValue("--smoke-surface-match-assessment");
        var runtimePath = commandLine.GetValue("--smoke-surface-match-runtime");
        var collectionPath = commandLine.GetValue("--smoke-surface-match-collection");
        var edgeScorePath = commandLine.GetValue("--smoke-surface-edge-score");
        var edgeOverlayPath = commandLine.GetValue("--smoke-surface-edge-overlay");
        var edgeAssessmentPath = commandLine.GetValue("--smoke-surface-edge-assessment");
        var falsePositiveReviewPath = commandLine.GetValue("--smoke-surface-match-review");
        if (modelPath is null
            && scenePath is null
            && executionPath is null
            && assessmentPath is null
            && runtimePath is null
            && collectionPath is null
            && edgeScorePath is null
            && edgeOverlayPath is null
            && edgeAssessmentPath is null
            && falsePositiveReviewPath is null)
        {
            return true;
        }

        if (collectionPath is not null)
        {
            if (string.IsNullOrWhiteSpace(modelPath)
                || string.IsNullOrWhiteSpace(scenePath)
                || string.IsNullOrWhiteSpace(collectionPath)
                || executionPath is not null
                || assessmentPath is not null
                || runtimePath is not null
                || edgeScorePath is not null
                || edgeOverlayPath is not null
                || edgeAssessmentPath is not null
                || falsePositiveReviewPath is not null)
            {
                failure =
                    "Multiple Surface Match smoke requires model, scene, and collection paths without single-result evidence paths.";
                return false;
            }

            try
            {
                var model = SurfaceModelArtifactStore.Load(modelPath);
                var scene = PreparedSceneArtifactStore.Load(scenePath);
                var collection = SurfaceMatchCollectionArtifactStore.Load(
                    collectionPath);
                workbench.ShowSurfaceMatchCollectionEvidence(
                    model,
                    scene,
                    collection);
                var selectionText = commandLine.GetValue("--smoke-surface-match-select-index");
                if (selectionText is not null
                    && (!int.TryParse(selectionText, out var selectionIndex)
                        || selectionIndex < 0
                        || selectionIndex >= collection.Items.Length
                        || !workbench.SelectSurfaceMatchCollectionItem(
                            collection.Items[selectionIndex].MatchId)))
                {
                    failure =
                        "Multiple Surface Match smoke selection index is invalid or could not be selected.";
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                failure =
                    $"Multiple Surface Match smoke evidence failed: {exception.Message}";
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(modelPath)
            || string.IsNullOrWhiteSpace(scenePath)
            || string.IsNullOrWhiteSpace(executionPath)
            || edgeScorePath is not null
                && string.IsNullOrWhiteSpace(edgeScorePath)
            || edgeOverlayPath is not null
                && string.IsNullOrWhiteSpace(edgeOverlayPath)
            || edgeAssessmentPath is not null
                && string.IsNullOrWhiteSpace(edgeAssessmentPath)
            || falsePositiveReviewPath is not null
                && string.IsNullOrWhiteSpace(falsePositiveReviewPath)
            || (edgeOverlayPath is not null
                    || edgeAssessmentPath is not null
                    || falsePositiveReviewPath is not null)
                && string.IsNullOrWhiteSpace(edgeScorePath)
            || runtimePath is not null
                && string.IsNullOrWhiteSpace(assessmentPath))
        {
            failure =
                "Surface match smoke requires model, scene, and execution paths; runtime also requires an assessment path.";
            return false;
        }

        try
        {
            var model = SurfaceModelArtifactStore.Load(modelPath);
            var scene = PreparedSceneArtifactStore.Load(scenePath);
            var execution = SurfaceMatchExecutionArtifactStore.Load(
                executionPath);
            var assessment = string.IsNullOrWhiteSpace(assessmentPath)
                ? null
                : SurfaceMatchAssessmentArtifactStore.Load(assessmentPath);
            var runtime = string.IsNullOrWhiteSpace(runtimePath)
                ? null
                : SurfaceMatchAssessmentArtifactStore.LoadRuntime(runtimePath);
            var edgeScore = string.IsNullOrWhiteSpace(edgeScorePath)
                ? null
                : SurfaceEdgeArtifactStore.LoadScore(edgeScorePath);
            var edgeOverlay = string.IsNullOrWhiteSpace(edgeOverlayPath)
                ? null
                : SurfaceEdgeDiagnosticReviewArtifactStore.LoadOverlay(
                    edgeOverlayPath);
            var edgeAssessment = string.IsNullOrWhiteSpace(edgeAssessmentPath)
                ? null
                : SurfaceEdgeDiagnosticReviewArtifactStore.LoadAssessment(
                    edgeAssessmentPath);
            var falsePositiveReview = string.IsNullOrWhiteSpace(falsePositiveReviewPath)
                ? null
                : SurfaceEdgeDiagnosticReviewArtifactStore.LoadReview(
                    falsePositiveReviewPath);
            workbench.ShowSurfaceMatchEvidence(
                model,
                scene,
                execution,
                assessment,
                runtime,
                edgeScore,
                edgeOverlay,
                edgeAssessment,
                falsePositiveReview);
            return true;
        }
        catch (Exception exception)
        {
            failure =
                $"Surface match smoke evidence failed: {exception.Message}";
            return false;
        }
    }

}

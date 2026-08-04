using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Data;

/// <summary>
/// Projects immutable Surface Match artifacts into reporting evidence after
/// validating their exact links. This type has no dependency on the Tools
/// project and cannot recompute matching or acceptance.
/// </summary>
public static class SurfaceMatchRunRecordProjection
{
    public static InspectionRunSurfaceMatchEvidence Create(
        SurfaceModelArtifact model,
        PreparedSceneArtifact scene,
        SurfaceMatchExecutionArtifact execution,
        SurfaceAndEdgeMatchScoreArtifact? score,
        SurfaceAndEdgeMatchAssessmentArtifact? assessment)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(execution);

        var modelValidity = SurfaceModelArtifactValidator.Inspect(model);
        var sceneValidity = PreparedSceneArtifactValidator.Inspect(scene);
        var executionValidity =
            SurfaceMatchExecutionArtifactValidator.Inspect(execution);
        if (!modelValidity.IsValid
            || !sceneValidity.IsValid
            || !executionValidity.IsValid)
        {
            throw new InvalidDataException(
                "Surface Match Run Record requires valid model, scene, and execution artifacts.");
        }

        if (!string.Equals(
                execution.ModelContentSha256,
                model.ContentSha256,
                StringComparison.Ordinal)
            || !string.Equals(
                execution.SceneContentSha256,
                scene.ContentSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Surface Match Run Record model or scene identity does not match the execution.");
        }

        var matched = execution.PoseResult.State
            == RigidSurfacePoseSearchState.Matched;
        if (matched && (score is null || assessment is null))
        {
            throw new InvalidDataException(
                "A matched Surface Match Run Record requires separate score and assessment evidence.");
        }

        if (!matched && (score is not null || assessment is not null))
        {
            throw new InvalidDataException(
                "A NoMatch Surface Match Run Record must not attach score or assessment evidence.");
        }

        if (score is not null)
        {
            var scoreValidity =
                SurfaceEdgeArtifactValidator.Inspect(score, execution);
            if (!scoreValidity.IsValid)
            {
                throw new InvalidDataException(
                    "Surface Match Run Record score is not linked to the exact execution.");
            }
        }

        if (assessment is not null)
        {
            var assessmentValidity =
                SurfaceAndEdgeAssessmentArtifactValidator.Inspect(
                    assessment,
                    score);
            if (!assessmentValidity.IsValid
                || !string.Equals(
                    assessment.SurfaceMatchExecutionContentSha256,
                    execution.ContentSha256,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Surface Match Run Record assessment is not linked to the exact execution and score.");
            }
        }

        return new InspectionRunSurfaceMatchEvidence(
            InspectionRunSurfaceMatchEvidence.CurrentSchemaVersion,
            InspectionRunSurfaceMatchEvidence.CurrentSemantics,
            model.ArtifactId,
            model.ContentSha256,
            scene.ArtifactId,
            scene.ContentSha256,
            execution,
            score,
            assessment);
    }
}

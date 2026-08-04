using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the transient comparison state between one immutable published
/// surface-match result and one explicitly previewed candidate. Numerical
/// matching remains behind <see cref="SurfaceMatchEvaluationExecutor"/>;
/// this session never implements or alters matching mathematics.
/// </summary>
internal sealed class SurfaceMatchExperimentSession
{
    public SurfaceMatchExperimentEvidence? Published { get; private set; }

    public SurfaceMatchExperimentEvidence? Candidate { get; private set; }

    public bool IsCandidateStale { get; private set; }

    public void LoadPublished(SurfaceMatchExperimentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        Published = evidence;
        Candidate = null;
        IsCandidateStale = false;
    }

    public void SetCandidate(SurfaceMatchEvaluationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var published = Published
            ?? throw new InvalidOperationException(
                "A published surface-match baseline is required before Preview.");
        Candidate = new SurfaceMatchExperimentEvidence(
            published.Model,
            published.Scene,
            result.Execution,
            result.Assessment,
            result.Runtime,
            null,
            null,
            null,
            null);
        IsCandidateStale = false;
    }

    public void MarkCandidateStale()
    {
        if (Candidate is not null)
        {
            IsCandidateStale = true;
        }
    }

    public SurfaceMatchExperimentEvidence PublishCandidate()
    {
        if (Candidate is null || IsCandidateStale)
        {
            throw new InvalidOperationException(
                "Only a current surface-match Preview candidate can be published.");
        }

        Published = Candidate;
        Candidate = null;
        IsCandidateStale = false;
        return Published;
    }

    public void DiscardCandidate()
    {
        Candidate = null;
        IsCandidateStale = false;
    }

    public bool ClearAcquisitionDirectionEvidence()
    {
        var hadEvidence = Published?.AcquisitionDirectionOrientation is not null
            || Candidate?.AcquisitionDirectionOrientation is not null;
        if (Published is not null)
        {
            Published = Published with { AcquisitionDirectionOrientation = null };
        }
        if (Candidate is not null)
        {
            Candidate = Candidate with { AcquisitionDirectionOrientation = null };
        }
        return hadEvidence;
    }

    public void Clear()
    {
        Published = null;
        Candidate = null;
        IsCandidateStale = false;
    }
}

internal sealed record SurfaceMatchExperimentEvidence(
    SurfaceModelArtifact Model,
    PreparedSceneArtifact Scene,
    SurfaceMatchExecutionArtifact Execution,
    SurfaceMatchAssessmentArtifact? Assessment,
    SurfaceMatchRuntimeReport? Runtime,
    SurfaceAndEdgeMatchScoreArtifact? EdgeScore,
    SurfaceEdgeDiagnosticOverlayArtifact? EdgeDiagnosticOverlay,
    SurfaceAndEdgeMatchAssessmentArtifact? EdgeAssessment,
    SurfaceMatchFalsePositiveReviewArtifact? FalsePositiveReview,
    SurfaceEdgeAcquisitionDirectionArtifact? AcquisitionDirectionOrientation = null);

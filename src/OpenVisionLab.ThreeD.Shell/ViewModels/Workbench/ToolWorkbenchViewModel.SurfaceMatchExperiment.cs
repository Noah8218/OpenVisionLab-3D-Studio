using System.Globalization;
using System.IO;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Thin Workbench binding and evidence adapter over
/// <see cref="SurfaceMatchExperimentSession"/>. OpenVisionLab Vision SDK remains
/// the required owner for any future new or changed numerical matching algorithm.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private readonly SurfaceMatchExperimentSession surfaceMatchExperiment;

    public bool IsSelectedStepSurfaceMatch =>
        string.Equals(
            SelectedPipelineStep?.ToolId,
            "surface-match",
            StringComparison.Ordinal);

    public bool IsSurfaceMatchExperimentVisible =>
        IsSelectedStepSurfaceMatch
        && surfaceMatchExperiment.Published is not null;

    public bool IsSurfaceMatchExperimentRunning =>
        surfaceMatchExperiment.IsRunning;

    public bool HasSurfaceMatchExperimentCandidate =>
        surfaceMatchExperiment.Candidate is not null;

    public bool IsSurfaceMatchExperimentCandidateStale =>
        surfaceMatchExperiment.IsCandidateStale;

    public bool IsSurfaceMatchExperimentCandidateDisplayed =>
        surfaceMatchExperiment.IsCandidateDisplayed;

    public SurfaceMatchExecutionArtifact? SurfaceMatchExperimentCandidate =>
        surfaceMatchExperiment.Candidate?.Execution;

    public string SurfaceMatchExperimentStatus =>
        LocalizeSurfaceMatchExperiment(
            surfaceMatchExperiment.StatusKorean,
            surfaceMatchExperiment.StatusEnglish);

    public string SurfaceMatchPublishedSummary =>
        FormatExperimentEvidence(
            surfaceMatchExperiment.Published,
            LocalizeSurfaceMatchExperiment(
                "게시 기준선 없음",
                "No published baseline"));

    public string SurfaceMatchCandidateSummary =>
        surfaceMatchExperiment.Candidate is null
            ? LocalizeSurfaceMatchExperiment("미리보기 안 함", "Not previewed")
            : FormatExperimentEvidence(
                surfaceMatchExperiment.Candidate,
                LocalizeSurfaceMatchExperiment("미리보기 안 함", "Not previewed"))
              + (surfaceMatchExperiment.IsCandidateStale
                  ? LocalizeSurfaceMatchExperiment(" · 오래됨", " · STALE")
                  : string.Empty);

    public string SurfaceMatchExperimentDeltaSummary =>
        FormatExperimentDelta(
            surfaceMatchExperiment.Published,
            surfaceMatchExperiment.Candidate);

    public string SurfaceMatchExperimentViewSummary =>
        surfaceMatchExperiment.IsCandidateDisplayed
            ? LocalizeSurfaceMatchExperiment(
                "뷰어: 후보 Preview(임시)",
                "Viewer: Candidate Preview (temporary)")
            : LocalizeSurfaceMatchExperiment(
                "뷰어: 게시 기준선",
                "Viewer: Published baseline");

    public ICommand ShowPublishedSurfaceMatchExperimentCommand =>
        surfaceMatchExperiment.ShowPublishedCommand;

    public ICommand ShowCandidateSurfaceMatchExperimentCommand =>
        surfaceMatchExperiment.ShowCandidateCommand;

    public ICommand DiscardSurfaceMatchExperimentCommand =>
        surfaceMatchExperiment.DiscardCommand;

    public Task<bool> PreviewSelectedSurfaceMatchExperimentAsync() =>
        surfaceMatchExperiment.PreviewAsync();

    private bool CanPreviewSelectedSurfaceMatchExperiment() =>
        surfaceMatchExperiment.CanPreview();

    private void PublishSelectedSurfaceMatchExperiment() =>
        surfaceMatchExperiment.Publish();

    private bool CanPublishSelectedSurfaceMatchExperiment() =>
        surfaceMatchExperiment.CanPublish();

    private void CancelSurfaceMatchExperimentPreview() =>
        surfaceMatchExperiment.CancelPreview();

    private void ShowPublishedSurfaceMatchExperiment() =>
        surfaceMatchExperiment.ShowPublished();

    private void ShowCandidateSurfaceMatchExperiment() =>
        surfaceMatchExperiment.ShowCandidate();

    private void DiscardSurfaceMatchExperiment() =>
        surfaceMatchExperiment.Discard();

    private void MarkSurfaceMatchExperimentCandidateStaleIfNeeded(
        ToolWorkbenchPipelineStepItem step) =>
        surfaceMatchExperiment.MarkCandidateStaleIfNeeded(step);

    private void LoadPublishedSurfaceMatchExperiment(
        SurfaceMatchExperimentEvidence evidence) =>
        surfaceMatchExperiment.LoadPublished(evidence);

    private void ClearSurfaceMatchExperiment() =>
        surfaceMatchExperiment.Clear();

    private void InvalidateSurfaceEdgeAcquisitionDirectionEvidence()
    {
        var sessionHadEvidence = surfaceMatchExperiment.ClearAcquisitionDirectionEvidence();
        var hadEvidence = surfaceEdgeAcquisitionDirection is not null
            || sessionHadEvidence;
        if (!hadEvidence)
        {
            return;
        }

        surfaceEdgeAcquisitionDirection = null;
        isSurfaceEdgeAcquisitionDirectionStale = true;
        if (surfaceMatchExperiment.Published is { } published)
        {
            ApplyPublishedSurfaceMatchEvidence(published);
            RaiseSurfaceMatchExperimentDisplay(published);
        }
        else
        {
            RaisePublishedSurfaceMatchProperties();
        }
    }

    private void ApplyPublishedSurfaceMatchEvidence(
        SurfaceMatchExperimentEvidence evidence)
    {
        surfaceMatchEvidence = evidence.Execution;
        surfaceMatchAssessment = evidence.Assessment;
        surfaceMatchRuntime = evidence.Runtime;
        surfaceEdgeScore = evidence.EdgeScore;
        surfaceEdgeDiagnosticOverlay = evidence.EdgeDiagnosticOverlay;
        surfaceEdgeAcquisitionDirection = evidence.AcquisitionDirectionOrientation;
        surfaceEdgeAssessment = evidence.EdgeAssessment;
        surfaceMatchFalsePositiveReview = evidence.FalsePositiveReview;
        RaisePublishedSurfaceMatchProperties();
    }

    private void RaiseSurfaceMatchExperimentDisplay(
        SurfaceMatchExperimentEvidence evidence) =>
        SurfaceMatchDisplayRequested?.Invoke(
            this,
            new ToolWorkbenchSurfaceMatchDisplayRequestEventArgs(
                evidence.Model,
                evidence.Scene,
                evidence.Execution,
                evidence.Assessment,
                evidence.Runtime,
                evidence.EdgeScore,
                evidence.EdgeDiagnosticOverlay,
                evidence.EdgeAssessment,
                evidence.FalsePositiveReview,
                evidence.AcquisitionDirectionOrientation));

    private void RaisePublishedSurfaceMatchProperties()
    {
        OnPropertyChanged(nameof(SurfaceMatchEvidence));
        OnPropertyChanged(nameof(HasSurfaceMatchEvidence));
        OnPropertyChanged(nameof(SurfaceMatchAssessment));
        OnPropertyChanged(nameof(SurfaceMatchRuntime));
        OnPropertyChanged(nameof(SurfaceEdgeScore));
        OnPropertyChanged(nameof(SurfaceEdgeDiagnosticOverlay));
        OnPropertyChanged(nameof(SurfaceEdgeAcquisitionDirection));
        OnPropertyChanged(nameof(IsSurfaceEdgeAcquisitionDirectionStale));
        OnPropertyChanged(nameof(SurfaceEdgeAssessment));
        OnPropertyChanged(nameof(SurfaceMatchFalsePositiveReview));
        RefreshSurfaceMatchExperimentState();
    }

    private void RefreshSurfaceMatchExperimentState()
    {
        OnPropertyChanged(nameof(IsSelectedStepSurfaceMatch));
        OnPropertyChanged(nameof(IsSurfaceMatchExperimentVisible));
        OnPropertyChanged(nameof(IsSurfaceMatchExperimentRunning));
        OnPropertyChanged(nameof(HasSurfaceMatchExperimentCandidate));
        OnPropertyChanged(nameof(IsSurfaceMatchExperimentCandidateStale));
        OnPropertyChanged(nameof(IsSurfaceMatchExperimentCandidateDisplayed));
        OnPropertyChanged(nameof(SurfaceMatchExperimentCandidate));
        OnPropertyChanged(nameof(SurfaceMatchExperimentStatus));
        OnPropertyChanged(nameof(SurfaceMatchPublishedSummary));
        OnPropertyChanged(nameof(SurfaceMatchCandidateSummary));
        OnPropertyChanged(nameof(SurfaceMatchExperimentDeltaSummary));
        OnPropertyChanged(nameof(SurfaceMatchExperimentViewSummary));
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
        RefreshSurfaceMatchCollectionState();
    }

    private static string FormatExperimentEvidence(
        SurfaceMatchExperimentEvidence? evidence,
        string fallback)
    {
        if (evidence is null)
        {
            return fallback;
        }

        var coverage = evidence.Execution.PoseResult.Coverage;
        var decision = evidence.Assessment?.Decision.ToString().ToUpperInvariant()
                       ?? "RAW";
        var rmse = coverage.InlierRmse is { } value
            ? value.ToString("G5", CultureInfo.InvariantCulture)
            : "n/a";
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{decision} · {coverage.CoverageRatio:P1} · RMSE {rmse} · {ShortHash(evidence.Execution.ContentSha256)}");
    }

    private static string FormatExperimentDelta(
        SurfaceMatchExperimentEvidence? published,
        SurfaceMatchExperimentEvidence? candidate)
    {
        if (published is null || candidate is null)
        {
            return LocalizeSurfaceMatchExperiment(
                "Preview는 임시 후보 하나를 만들며, 게시는 후보를 다시 실행하지 않습니다.",
                "Preview creates one temporary candidate; Publish never reruns it.");
        }

        var publishedCoverage =
            published.Execution.PoseResult.Coverage;
        var candidateCoverage =
            candidate.Execution.PoseResult.Coverage;
        var coverageDelta =
            (candidateCoverage.CoverageRatio
             - publishedCoverage.CoverageRatio) * 100.0;
        var rmseDelta = candidateCoverage.InlierRmse.HasValue
                        && publishedCoverage.InlierRmse.HasValue
            ? (candidateCoverage.InlierRmse.Value
               - publishedCoverage.InlierRmse.Value)
                .ToString("+0.#####;-0.#####;0", CultureInfo.InvariantCulture)
            : "n/a";
        return LocalizeSurfaceMatchExperiment(
            string.Create(
                CultureInfo.InvariantCulture,
                $"후보 - 게시 결과: 커버리지 {coverageDelta:+0.0;-0.0;0.0} pp · RMSE {rmseDelta}"),
            string.Create(
                CultureInfo.InvariantCulture,
                $"Candidate - Published: coverage {coverageDelta:+0.0;-0.0;0.0} pp · RMSE {rmseDelta}"));
    }

    private static string LocalizeSurfaceMatchExperiment(
        string korean,
        string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English
            ? english
            : korean;

    private static string ShortHash(string hash) =>
        hash.Length <= 12 ? hash : hash[..12];
}

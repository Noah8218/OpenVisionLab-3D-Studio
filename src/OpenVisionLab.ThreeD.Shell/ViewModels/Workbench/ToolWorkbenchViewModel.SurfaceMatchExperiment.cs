using System.Globalization;
using System.IO;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Thin Workbench command adapter over <see cref="SurfaceMatchExperimentSession"/>.
/// The session owns comparison state; Library-Noah remains the required owner
/// for any future new or changed numerical matching algorithm.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private readonly SurfaceMatchExperimentSession surfaceMatchExperiment = new();
    private CancellationTokenSource? surfaceMatchExperimentCancellation;
    private bool isSurfaceMatchExperimentRunning;
    private bool isSurfaceMatchExperimentCandidateDisplayed;
    private string surfaceMatchExperimentStatusKorean =
        "파라미터를 비교하려면 게시된 정합 결과를 먼저 불러오세요.";
    private string surfaceMatchExperimentStatusEnglish =
        "Load a published match result before comparing parameters.";
    private string? surfaceMatchExperimentStepStateBeforePreview;
    private RelayCommand showPublishedSurfaceMatchExperimentCommand = null!;
    private RelayCommand showCandidateSurfaceMatchExperimentCommand = null!;
    private RelayCommand discardSurfaceMatchExperimentCommand = null!;

    public bool IsSelectedStepSurfaceMatch =>
        string.Equals(
            SelectedPipelineStep?.ToolId,
            "surface-match",
            StringComparison.Ordinal);

    public bool IsSurfaceMatchExperimentVisible =>
        IsSelectedStepSurfaceMatch
        && surfaceMatchExperiment.Published is not null;

    public bool IsSurfaceMatchExperimentRunning =>
        isSurfaceMatchExperimentRunning;

    public bool HasSurfaceMatchExperimentCandidate =>
        surfaceMatchExperiment.Candidate is not null;

    public bool IsSurfaceMatchExperimentCandidateStale =>
        surfaceMatchExperiment.IsCandidateStale;

    public bool IsSurfaceMatchExperimentCandidateDisplayed =>
        isSurfaceMatchExperimentCandidateDisplayed;

    public SurfaceMatchExecutionArtifact? SurfaceMatchExperimentCandidate =>
        surfaceMatchExperiment.Candidate?.Execution;

    public string SurfaceMatchExperimentStatus =>
        LocalizeSurfaceMatchExperiment(
            surfaceMatchExperimentStatusKorean,
            surfaceMatchExperimentStatusEnglish);

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
        isSurfaceMatchExperimentCandidateDisplayed
            ? LocalizeSurfaceMatchExperiment(
                "뷰어: 후보 Preview(임시)",
                "Viewer: Candidate Preview (temporary)")
            : LocalizeSurfaceMatchExperiment(
                "뷰어: 게시 기준선",
                "Viewer: Published baseline");

    public ICommand ShowPublishedSurfaceMatchExperimentCommand =>
        showPublishedSurfaceMatchExperimentCommand;

    public ICommand ShowCandidateSurfaceMatchExperimentCommand =>
        showCandidateSurfaceMatchExperimentCommand;

    public ICommand DiscardSurfaceMatchExperimentCommand =>
        discardSurfaceMatchExperimentCommand;

    private void InitializeSurfaceMatchExperiment()
    {
        showPublishedSurfaceMatchExperimentCommand = new RelayCommand(
            _ => ShowPublishedSurfaceMatchExperiment(),
            _ => surfaceMatchExperiment.Published is not null
                 && isSurfaceMatchExperimentCandidateDisplayed);
        showCandidateSurfaceMatchExperimentCommand = new RelayCommand(
            _ => ShowCandidateSurfaceMatchExperiment(),
            _ => surfaceMatchExperiment.Candidate is not null
                 && !surfaceMatchExperiment.IsCandidateStale
                 && !isSurfaceMatchExperimentCandidateDisplayed);
        discardSurfaceMatchExperimentCommand = new RelayCommand(
            _ => DiscardSurfaceMatchExperiment(),
            _ => surfaceMatchExperiment.Candidate is not null
                 && !isSurfaceMatchExperimentRunning);
    }

    public async Task<bool> PreviewSelectedSurfaceMatchExperimentAsync()
    {
        if (!CanPreviewSelectedSurfaceMatchExperiment()
            || SelectedPipelineStep is not { } step
            || surfaceMatchExperiment.Published is not { } published)
        {
            return false;
        }

        var properties = SurfaceMatchStepProperties.From(step);
        if (!properties.TryCreateContracts(
                out var search,
                out var policy,
                out var message)
            || search is null
            || policy is null)
        {
            SetSurfaceMatchExperimentStatus(
                "정합 파라미터가 유효하지 않아 후보 Preview를 시작할 수 없습니다.",
                message);
            return false;
        }

        surfaceMatchExperimentCancellation?.Dispose();
        surfaceMatchExperimentCancellation = new CancellationTokenSource();
        var cancellation = surfaceMatchExperimentCancellation;
        surfaceMatchExperimentStepStateBeforePreview = step.State;
        surfaceMatchExperiment.DiscardCandidate();
        isSurfaceMatchExperimentCandidateDisplayed = false;
        SetSurfaceMatchExperimentRunning(true);
        step.State = "Preview running";
        SetSurfaceMatchExperimentStatus(
            $"작성된 후보 {search.MaximumCandidateCount}개 제한 안에서 후보 Preview를 실행 중입니다. 게시 증거는 변경되지 않습니다.",
            $"Candidate Preview is running inside {search.MaximumCandidateCount} authored-candidate guard. Published evidence is unchanged.");
        AppendLog(
            "Preview",
            $"Surface Match parameter experiment started: {step.Id}; publishedSha256={published.Execution.ContentSha256}.");

        try
        {
            // K-10 only orchestrates the existing shared execution boundary.
            // Any change to matching mathematics must first move to Library-Noah.
            var result = await Task.Run(
                () => SurfaceMatchEvaluationExecutor.Execute(
                    published.Model,
                    published.Scene,
                    search,
                    policy),
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(
                    surfaceMatchExperiment.Published,
                    published))
            {
                throw new OperationCanceledException(
                    "Published surface-match evidence changed while Preview was running.");
            }

            surfaceMatchExperiment.SetCandidate(result);
            isSurfaceMatchExperimentCandidateDisplayed = true;
            step.State = "Preview ready";
            SetSurfaceMatchExperimentStatus(
                "후보는 임시 상태입니다. 게시 결과와 비교한 뒤 명시적으로 게시하거나 버리세요.",
                "Candidate is temporary. Compare it with Published, then Publish explicitly or discard it.");
            RaiseSurfaceMatchExperimentDisplay(
                surfaceMatchExperiment.Candidate!);
            AppendLog(
                "Preview",
                $"Surface Match candidate ready without replacing Published: candidateSha256={result.Execution.ContentSha256};publishedSha256={published.Execution.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            surfaceMatchExperiment.DiscardCandidate();
            isSurfaceMatchExperimentCandidateDisplayed = false;
            RestoreSurfaceMatchExperimentStepState(step);
            SetSurfaceMatchExperimentStatus(
                "후보 Preview가 취소되었습니다. 게시 증거는 그대로 유지됩니다.",
                "Candidate Preview cancelled. Published evidence remains active.");
            ShowPublishedSurfaceMatchExperiment();
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException
            or InvalidOperationException
            or ArgumentException
            or OverflowException)
        {
            surfaceMatchExperiment.DiscardCandidate();
            isSurfaceMatchExperimentCandidateDisplayed = false;
            RestoreSurfaceMatchExperimentStepState(step);
            SetSurfaceMatchExperimentStatus(
                "후보 Preview가 안전하게 실패했습니다. 게시 증거는 변경되지 않았습니다.",
                $"Candidate Preview failed closed: {exception.Message}");
            ShowPublishedSurfaceMatchExperiment();
            AppendLog(
                "Preview",
                $"Surface Match candidate rejected without changing Published: {exception.Message}");
            return false;
        }
        finally
        {
            if (ReferenceEquals(
                    surfaceMatchExperimentCancellation,
                    cancellation))
            {
                surfaceMatchExperimentCancellation.Dispose();
                surfaceMatchExperimentCancellation = null;
            }

            SetSurfaceMatchExperimentRunning(false);
        }
    }

    private bool CanPreviewSelectedSurfaceMatchExperiment()
    {
        if (!IsSelectedStepSurfaceMatch
            || HasPendingStepParameterChanges
            || isSurfaceMatchExperimentRunning
            || SelectedPipelineStep is not { } step
            || surfaceMatchExperiment.Published is null)
        {
            return false;
        }

        return SurfaceMatchStepProperties.From(step)
            .TryCreateContracts(out _, out _, out _);
    }

    private void PublishSelectedSurfaceMatchExperiment()
    {
        if (!CanPublishSelectedSurfaceMatchExperiment()
            || SelectedPipelineStep is not { } step)
        {
            return;
        }

        var published = surfaceMatchExperiment.PublishCandidate();
        ApplyPublishedSurfaceMatchEvidence(published);
        isSurfaceMatchExperimentCandidateDisplayed = false;
        surfaceMatchExperimentStepStateBeforePreview = null;
        step.State = "Published";
        SetSurfaceMatchExperimentStatus(
            "후보를 Preview 그대로 게시했습니다. 다른 비교를 시작하려면 파라미터를 편집하고 Preview를 실행하세요.",
            "Candidate published exactly as previewed. Edit parameters and Preview to start another comparison.");
        RaiseSurfaceMatchExperimentDisplay(published);
        AppendLog(
            "Publish",
            $"Surface Match candidate published without re-running: executionSha256={published.Execution.ContentSha256}.");
    }

    private bool CanPublishSelectedSurfaceMatchExperiment() =>
        IsSelectedStepSurfaceMatch
        && !isSurfaceMatchExperimentRunning
        && surfaceMatchExperiment.Candidate is not null
        && !surfaceMatchExperiment.IsCandidateStale;

    private void CancelSurfaceMatchExperimentPreview()
    {
        surfaceMatchExperimentCancellation?.Cancel();
        isSurfaceMatchExperimentCandidateDisplayed = false;
        ShowPublishedSurfaceMatchExperiment();
    }

    private void ShowPublishedSurfaceMatchExperiment()
    {
        if (surfaceMatchExperiment.Published is not { } published)
        {
            return;
        }

        isSurfaceMatchExperimentCandidateDisplayed = false;
        RaiseSurfaceMatchExperimentDisplay(published);
        RefreshSurfaceMatchExperimentState();
    }

    private void ShowCandidateSurfaceMatchExperiment()
    {
        if (surfaceMatchExperiment.Candidate is not { } candidate
            || surfaceMatchExperiment.IsCandidateStale)
        {
            return;
        }

        isSurfaceMatchExperimentCandidateDisplayed = true;
        RaiseSurfaceMatchExperimentDisplay(candidate);
        RefreshSurfaceMatchExperimentState();
    }

    private void DiscardSurfaceMatchExperiment()
    {
        if (surfaceMatchExperiment.Candidate is null)
        {
            return;
        }

        surfaceMatchExperiment.DiscardCandidate();
        isSurfaceMatchExperimentCandidateDisplayed = false;
        if (SelectedPipelineStep is { } step)
        {
            RestoreSurfaceMatchExperimentStepState(step);
        }

        SetSurfaceMatchExperimentStatus(
            "후보를 버렸습니다. 게시 증거가 활성 상태로 유지됩니다.",
            "Candidate discarded. Published evidence remains active.");
        ShowPublishedSurfaceMatchExperiment();
    }

    private void MarkSurfaceMatchExperimentCandidateStaleIfNeeded(
        ToolWorkbenchPipelineStepItem step)
    {
        if (!string.Equals(
                step.ToolId,
                "surface-match",
                StringComparison.Ordinal)
            || surfaceMatchExperiment.Candidate is null)
        {
            return;
        }

        surfaceMatchExperiment.MarkCandidateStale();
        isSurfaceMatchExperimentCandidateDisplayed = false;
        RestoreSurfaceMatchExperimentStepState(step);
        SetSurfaceMatchExperimentStatus(
            "Preview 이후 파라미터가 변경되어 후보가 오래된 상태입니다. 게시하기 전에 다시 Preview를 실행하세요.",
            "Parameters changed after Preview. The candidate is stale; Preview again before Publish.");
        ShowPublishedSurfaceMatchExperiment();
    }

    private void LoadPublishedSurfaceMatchExperiment(
        SurfaceMatchExperimentEvidence evidence)
    {
        surfaceMatchExperimentCancellation?.Cancel();
        surfaceMatchExperiment.LoadPublished(evidence);
        isSurfaceMatchExperimentCandidateDisplayed = false;
        surfaceMatchExperimentStepStateBeforePreview = null;
        ApplyPublishedSurfaceMatchEvidence(evidence);
        SetSurfaceMatchExperimentStatus(
            "게시 기준선이 준비되었습니다. 파라미터 변경을 적용한 뒤 명시적으로 Preview하여 비교하세요.",
            "Published baseline ready. Apply parameter changes, then Preview explicitly to compare.");
    }

    private void ClearSurfaceMatchExperiment()
    {
        surfaceMatchExperimentCancellation?.Cancel();
        surfaceMatchExperiment.Clear();
        isSurfaceMatchExperimentCandidateDisplayed = false;
        surfaceMatchExperimentStepStateBeforePreview = null;
        SetSurfaceMatchExperimentStatus(
            "파라미터를 비교하려면 게시된 정합 결과를 먼저 불러오세요.",
            "Load a published match result before comparing parameters.");
    }

    private void ApplyPublishedSurfaceMatchEvidence(
        SurfaceMatchExperimentEvidence evidence)
    {
        surfaceMatchEvidence = evidence.Execution;
        surfaceMatchAssessment = evidence.Assessment;
        surfaceMatchRuntime = evidence.Runtime;
        surfaceEdgeScore = evidence.EdgeScore;
        surfaceEdgeDiagnosticOverlay = evidence.EdgeDiagnosticOverlay;
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
                evidence.FalsePositiveReview));

    private void RaisePublishedSurfaceMatchProperties()
    {
        OnPropertyChanged(nameof(SurfaceMatchEvidence));
        OnPropertyChanged(nameof(HasSurfaceMatchEvidence));
        OnPropertyChanged(nameof(SurfaceMatchAssessment));
        OnPropertyChanged(nameof(SurfaceMatchRuntime));
        OnPropertyChanged(nameof(SurfaceEdgeScore));
        OnPropertyChanged(nameof(SurfaceEdgeDiagnosticOverlay));
        OnPropertyChanged(nameof(SurfaceEdgeAssessment));
        OnPropertyChanged(nameof(SurfaceMatchFalsePositiveReview));
        RefreshSurfaceMatchExperimentState();
    }

    private void RestoreSurfaceMatchExperimentStepState(
        ToolWorkbenchPipelineStepItem step)
    {
        if (!string.IsNullOrWhiteSpace(
                surfaceMatchExperimentStepStateBeforePreview))
        {
            step.State = surfaceMatchExperimentStepStateBeforePreview;
        }
    }

    private void SetSurfaceMatchExperimentRunning(bool value)
    {
        isSurfaceMatchExperimentRunning = value;
        OnPropertyChanged(nameof(IsSurfaceMatchExperimentRunning));
        OnPropertyChanged(nameof(IsSelectedStepPreviewRunning));
        RefreshSurfaceMatchExperimentState();
    }

    private void SetSurfaceMatchExperimentStatus(
        string korean,
        string english)
    {
        surfaceMatchExperimentStatusKorean = korean;
        surfaceMatchExperimentStatusEnglish = english;
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
        showPublishedSurfaceMatchExperimentCommand?.RaiseCanExecuteChanged();
        showCandidateSurfaceMatchExperimentCommand?.RaiseCanExecuteChanged();
        discardSurfaceMatchExperimentCommand?.RaiseCanExecuteChanged();
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

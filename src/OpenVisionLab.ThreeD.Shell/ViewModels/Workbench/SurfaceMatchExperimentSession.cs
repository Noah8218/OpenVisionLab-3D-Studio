using System.IO;
using System.Threading;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the explicit Preview/Publish lifecycle between one immutable published
/// surface-match result and one temporary candidate. Numerical matching remains
/// behind <see cref="SurfaceMatchEvaluationExecutor"/>.
/// </summary>
internal sealed class SurfaceMatchExperimentSession : IDisposable
{
    private readonly Func<bool> isSelectedSurfaceMatch;
    private readonly Func<bool> hasPendingStepParameterChanges;
    private readonly Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep;
    private readonly Action notifyPublishedEvidenceChanged;
    private readonly Action<SurfaceMatchExperimentEvidence> requestDisplay;
    private readonly Action<string, string> appendLog;
    private readonly Action onStateChanged;

    private CancellationTokenSource? previewCancellation;
    private bool isRunning;
    private bool isCandidateDisplayed;
    private string statusKorean =
        "파라미터를 비교하려면 게시된 정합 결과를 먼저 불러오세요.";
    private string statusEnglish =
        "Load a published match result before comparing parameters.";
    private string? stepStateBeforePreview;
    private int disposalState;

    public SurfaceMatchExperimentSession(
        Func<bool> isSelectedSurfaceMatch,
        Func<bool> hasPendingStepParameterChanges,
        Func<ToolWorkbenchPipelineStepItem?> getSelectedPipelineStep,
        Action notifyPublishedEvidenceChanged,
        Action<SurfaceMatchExperimentEvidence> requestDisplay,
        Action<string, string> appendLog,
        Action onStateChanged)
    {
        this.isSelectedSurfaceMatch = isSelectedSurfaceMatch;
        this.hasPendingStepParameterChanges = hasPendingStepParameterChanges;
        this.getSelectedPipelineStep = getSelectedPipelineStep;
        this.notifyPublishedEvidenceChanged = notifyPublishedEvidenceChanged;
        this.requestDisplay = requestDisplay;
        this.appendLog = appendLog;
        this.onStateChanged = onStateChanged;

        ShowPublishedCommand = new RelayCommand(
            _ => ShowPublished(),
            _ => Published is not null && isCandidateDisplayed);
        ShowCandidateCommand = new RelayCommand(
            _ => ShowCandidate(),
            _ => Candidate is not null && !IsCandidateStale && !isCandidateDisplayed);
        DiscardCommand = new RelayCommand(
            _ => Discard(),
            _ => Candidate is not null && !isRunning);
    }

    public SurfaceMatchExperimentEvidence? Published { get; private set; }
    public SurfaceMatchExperimentEvidence? Candidate { get; private set; }
    public bool IsCandidateStale { get; private set; }
    public bool IsRunning => isRunning;
    public bool IsCandidateDisplayed => isCandidateDisplayed;
    public bool IsDisposed => Volatile.Read(ref disposalState) != 0;
    public string StatusKorean => statusKorean;
    public string StatusEnglish => statusEnglish;
    public RelayCommand ShowPublishedCommand { get; }
    public RelayCommand ShowCandidateCommand { get; }
    public RelayCommand DiscardCommand { get; }

    public async Task<bool> PreviewAsync()
    {
        if (IsDisposed
            || !CanPreview()
            || getSelectedPipelineStep() is not { } step
            || Published is not { } published)
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
            SetStatus(
                "정합 파라미터가 유효하지 않아 후보 Preview를 시작할 수 없습니다.",
                message);
            return false;
        }

        var cancellation = new CancellationTokenSource();
        var cancellationToken = cancellation.Token;
        var previousCancellation = Interlocked.Exchange(
            ref previewCancellation,
            cancellation);
        CancelAndDispose(previousCancellation);
        if (IsDisposed)
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    cancellation),
                cancellation))
            {
                cancellation.Dispose();
            }

            return false;
        }

        stepStateBeforePreview = step.State;
        DiscardCandidate();
        isCandidateDisplayed = false;
        SetRunning(true);
        step.State = "Preview running";
        SetStatus(
            $"작성된 후보 {search.MaximumCandidateCount}개 제한 안에서 후보 Preview를 실행 중입니다. 게시 증거는 변경되지 않습니다.",
            $"Candidate Preview is running inside {search.MaximumCandidateCount} authored-candidate guard. Published evidence is unchanged.");
        appendLog(
            "Preview",
            $"Surface Match parameter experiment started: {step.Id}; publishedSha256={published.Execution.ContentSha256}.");

        try
        {
            // K-10 only orchestrates the existing shared execution boundary.
            // Any change to matching mathematics must first move to OpenVisionLab Vision SDK.
            var result = await Task.Run(
                () => SurfaceMatchEvaluationExecutor.Execute(
                    published.Model,
                    published.Scene,
                    search,
                    policy),
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (IsDisposed)
            {
                return false;
            }

            if (!ReferenceEquals(Published, published))
            {
                throw new OperationCanceledException(
                    "Published surface-match evidence changed while Preview was running.");
            }

            SetCandidate(result);
            isCandidateDisplayed = true;
            step.State = "Preview ready";
            SetStatus(
                "후보는 임시 상태입니다. 게시 결과와 비교한 뒤 명시적으로 게시하거나 버리세요.",
                "Candidate is temporary. Compare it with Published, then Publish explicitly or discard it.");
            requestDisplay(Candidate!);
            appendLog(
                "Preview",
                $"Surface Match candidate ready without replacing Published: candidateSha256={result.Execution.ContentSha256};publishedSha256={published.Execution.ContentSha256}.");
            return true;
        }
        catch (OperationCanceledException)
        {
            if (IsDisposed)
            {
                return false;
            }

            DiscardCandidate();
            isCandidateDisplayed = false;
            RestoreStepState(step);
            SetStatus(
                "후보 Preview가 취소되었습니다. 게시 증거는 그대로 유지됩니다.",
                "Candidate Preview cancelled. Published evidence remains active.");
            ShowPublished();
            return false;
        }
        catch (Exception exception) when (exception is InvalidDataException
            or InvalidOperationException
            or ArgumentException
            or OverflowException)
        {
            if (IsDisposed)
            {
                return false;
            }

            DiscardCandidate();
            isCandidateDisplayed = false;
            RestoreStepState(step);
            SetStatus(
                "후보 Preview가 안전하게 실패했습니다. 게시 증거는 변경되지 않았습니다.",
                $"Candidate Preview failed closed: {exception.Message}");
            ShowPublished();
            appendLog(
                "Preview",
                $"Surface Match candidate rejected without changing Published: {exception.Message}");
            return false;
        }
        finally
        {
            if (ReferenceEquals(
                Interlocked.CompareExchange(
                    ref previewCancellation,
                    null,
                    cancellation),
                cancellation))
            {
                cancellation.Dispose();
            }

            if (!IsDisposed)
            {
                SetRunning(false);
            }
        }
    }

    public bool CanPreview()
    {
        if (IsDisposed
            || !isSelectedSurfaceMatch()
            || hasPendingStepParameterChanges()
            || isRunning
            || getSelectedPipelineStep() is not { } step
            || Published is null)
        {
            return false;
        }

        return SurfaceMatchStepProperties.From(step)
            .TryCreateContracts(out _, out _, out _);
    }

    public void Publish()
    {
        if (IsDisposed || !CanPublish() || getSelectedPipelineStep() is not { } step)
        {
            return;
        }

        var published = PublishCandidate();
        notifyPublishedEvidenceChanged();
        isCandidateDisplayed = false;
        stepStateBeforePreview = null;
        step.State = "Published";
        SetStatus(
            "후보를 Preview 그대로 게시했습니다. 다른 비교를 시작하려면 파라미터를 편집하고 Preview를 실행하세요.",
            "Candidate published exactly as previewed. Edit parameters and Preview to start another comparison.");
        requestDisplay(published);
        appendLog(
            "Publish",
            $"Surface Match candidate published without re-running: executionSha256={published.Execution.ContentSha256}.");
    }

    public bool CanPublish() =>
        !IsDisposed
        && isSelectedSurfaceMatch()
        && !isRunning
        && Candidate is not null
        && !IsCandidateStale;

    public void CancelPreview()
    {
        if (IsDisposed)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        isCandidateDisplayed = false;
        ShowPublished();
    }

    public void ShowPublished()
    {
        if (IsDisposed || Published is not { } published)
        {
            return;
        }

        isCandidateDisplayed = false;
        requestDisplay(published);
        NotifyStateChanged();
    }

    public void ShowCandidate()
    {
        if (IsDisposed || Candidate is not { } candidate || IsCandidateStale)
        {
            return;
        }

        isCandidateDisplayed = true;
        requestDisplay(candidate);
        NotifyStateChanged();
    }

    public void Discard()
    {
        if (IsDisposed || Candidate is null)
        {
            return;
        }

        DiscardCandidate();
        isCandidateDisplayed = false;
        if (getSelectedPipelineStep() is { } step)
        {
            RestoreStepState(step);
        }

        SetStatus(
            "후보를 버렸습니다. 게시 증거가 활성 상태로 유지됩니다.",
            "Candidate discarded. Published evidence remains active.");
        ShowPublished();
    }

    public void MarkCandidateStaleIfNeeded(ToolWorkbenchPipelineStepItem step)
    {
        if (IsDisposed
            || !string.Equals(step.ToolId, "surface-match", StringComparison.Ordinal)
            || Candidate is null)
        {
            return;
        }

        MarkCandidateStale();
        isCandidateDisplayed = false;
        RestoreStepState(step);
        SetStatus(
            "Preview 이후 파라미터가 변경되어 후보가 오래된 상태입니다. 게시하기 전에 다시 Preview를 실행하세요.",
            "Parameters changed after Preview. The candidate is stale; Preview again before Publish.");
        ShowPublished();
    }

    public void LoadPublished(SurfaceMatchExperimentEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (IsDisposed)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        Published = evidence;
        Candidate = null;
        IsCandidateStale = false;
        isCandidateDisplayed = false;
        stepStateBeforePreview = null;
        notifyPublishedEvidenceChanged();
        SetStatus(
            "게시 기준선이 준비되었습니다. 파라미터 변경을 적용한 뒤 명시적으로 Preview하여 비교하세요.",
            "Published baseline ready. Apply parameter changes, then Preview explicitly to compare.");
    }

    public bool ClearAcquisitionDirectionEvidence()
    {
        if (IsDisposed)
        {
            return false;
        }

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
        if (IsDisposed)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        Published = null;
        Candidate = null;
        IsCandidateStale = false;
        isCandidateDisplayed = false;
        stepStateBeforePreview = null;
        notifyPublishedEvidenceChanged();
        SetStatus(
            "파라미터를 비교하려면 게시된 정합 결과를 먼저 불러오세요.",
            "Load a published match result before comparing parameters.");
    }

    private void SetCandidate(SurfaceMatchEvaluationResult result)
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

    private void MarkCandidateStale()
    {
        if (Candidate is not null)
        {
            IsCandidateStale = true;
        }
    }

    private SurfaceMatchExperimentEvidence PublishCandidate()
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

    private void DiscardCandidate()
    {
        Candidate = null;
        IsCandidateStale = false;
    }

    private void RestoreStepState(ToolWorkbenchPipelineStepItem step)
    {
        if (!string.IsNullOrWhiteSpace(stepStateBeforePreview))
        {
            step.State = stepStateBeforePreview;
        }
    }

    private void SetRunning(bool value)
    {
        if (IsDisposed)
        {
            return;
        }

        isRunning = value;
        NotifyStateChanged();
    }

    private void SetStatus(string korean, string english)
    {
        if (IsDisposed)
        {
            return;
        }

        statusKorean = korean;
        statusEnglish = english;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        if (IsDisposed)
        {
            return;
        }

        ShowPublishedCommand.RaiseCanExecuteChanged();
        ShowCandidateCommand.RaiseCanExecuteChanged();
        DiscardCommand.RaiseCanExecuteChanged();
        onStateChanged();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var cancellation = Interlocked.Exchange(ref previewCancellation, null);
        CancelAndDispose(cancellation);
        Published = null;
        Candidate = null;
        IsCandidateStale = false;
        isCandidateDisplayed = false;
        isRunning = false;
        stepStateBeforePreview = null;
    }
}

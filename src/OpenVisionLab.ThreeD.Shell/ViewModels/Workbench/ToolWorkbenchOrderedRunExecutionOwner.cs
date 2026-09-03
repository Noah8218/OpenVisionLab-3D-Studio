using System.IO;
using System.Threading;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the explicit full-recipe Ordered Run lifecycle. The Workbench
/// ViewModel supplies recipe capability, source, presentation, and state
/// callbacks; the typed ordered-graph executor remains the existing Tools
/// boundary.
/// </summary>
internal sealed class ToolWorkbenchOrderedRunExecutionOwner : IDisposable
{
    private readonly RelayCommand runCommand;
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<ToolRecipeDocument, (bool CanRun, string Message)> getCapability;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string> getSourcePath;
    private readonly Func<SourceQualityReport?> getSourceQualityReport;
    private readonly Func<IEnumerable<ToolWorkbenchPipelineStepItem>> getPipelineSteps;
    private readonly Func<ToolRecipeOrderedGraphExecutionResult, string> createSummary;
    private readonly Func<string, string, string> localize;
    private readonly Action<string, string> appendLog;
    private readonly Action stateChanged;
    private readonly Action<ToolWorkbenchOrderedRunCompletedEventArgs> completed;
    private readonly Action invalidated;
    private readonly RelayCommand cancelCommand;

    private bool isRunning;
    private int runGate;
    private int disposalState;
    private CancellationTokenSource? cancellation;
    private ToolRecipeOrderedGraphExecutionResult? result;
    private string? recordPath;
    private string summary;

    public ToolWorkbenchOrderedRunExecutionOwner(
        Func<ToolRecipeDocument> createDocument,
        Func<ToolRecipeDocument, (bool CanRun, string Message)> getCapability,
        Func<string?> getRecipePath,
        Func<string> getSourcePath,
        Func<SourceQualityReport?> getSourceQualityReport,
        Func<IEnumerable<ToolWorkbenchPipelineStepItem>> getPipelineSteps,
        Func<ToolRecipeOrderedGraphExecutionResult, string> createSummary,
        Func<string, string, string> localize,
        Action<string, string> appendLog,
        Action stateChanged,
        Action<ToolWorkbenchOrderedRunCompletedEventArgs> completed,
        Action invalidated)
    {
        this.createDocument = createDocument ?? throw new ArgumentNullException(nameof(createDocument));
        this.getCapability = getCapability ?? throw new ArgumentNullException(nameof(getCapability));
        this.getRecipePath = getRecipePath ?? throw new ArgumentNullException(nameof(getRecipePath));
        this.getSourcePath = getSourcePath ?? throw new ArgumentNullException(nameof(getSourcePath));
        this.getSourceQualityReport = getSourceQualityReport
            ?? throw new ArgumentNullException(nameof(getSourceQualityReport));
        this.getPipelineSteps = getPipelineSteps
            ?? throw new ArgumentNullException(nameof(getPipelineSteps));
        this.createSummary = createSummary ?? throw new ArgumentNullException(nameof(createSummary));
        this.localize = localize ?? throw new ArgumentNullException(nameof(localize));
        this.appendLog = appendLog ?? throw new ArgumentNullException(nameof(appendLog));
        this.stateChanged = stateChanged ?? throw new ArgumentNullException(nameof(stateChanged));
        this.completed = completed ?? throw new ArgumentNullException(nameof(completed));
        this.invalidated = invalidated ?? throw new ArgumentNullException(nameof(invalidated));

        summary = localize(
            "저장된 현재 레시피를 명시적으로 실행하면 Run Record가 생성됩니다.",
            "Run the saved current recipe explicitly to create a Run Record.");
        runCommand = new RelayCommand(_ => _ = RunAsync(), _ => CanRun());
        cancelCommand = new RelayCommand(_ => Cancel(), _ => IsRunning);
    }

    public RelayCommand RunCommand => runCommand;

    public RelayCommand CancelCommand => cancelCommand;

    public bool IsRunning => isRunning;

    public ToolRecipeOrderedGraphExecutionResult? Result => result;

    public string? RecordPath => recordPath;

    public string Summary => summary;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(ref cancellation, null);
        CancelAndDispose(currentCancellation);
        result = null;
        recordPath = null;
        isRunning = false;
    }

    public async Task<bool> RunAsync()
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return false;
        }

        // The command normally runs on the WPF dispatcher, but public and
        // verification callers can race from different threads. Gate before
        // capability evaluation so only one logical Run owns the callbacks,
        // cancellation token, step-state projection, and completion event.
        if (Interlocked.CompareExchange(ref runGate, 1, 0) != 0)
        {
            return false;
        }

        try
        {
            if (Volatile.Read(ref disposalState) != 0)
            {
                return false;
            }

            var document = createDocument();
            var capability = getCapability(document);
            var recipePath = getRecipePath();
            if (Volatile.Read(ref disposalState) != 0)
            {
                return false;
            }

            if (!capability.CanRun || recipePath is not { } fullRecipePath)
            {
                summary = capability.Message;
                NotifyStateChanged();
                appendLog("Run", capability.Message);
                return false;
            }

            result = null;
            recordPath = null;
            var currentCancellation = new CancellationTokenSource();
            var currentToken = currentCancellation.Token;
            var previousCancellation = Interlocked.Exchange(
                ref cancellation,
                currentCancellation);
            CancelAndDispose(previousCancellation);
            if (Volatile.Read(ref disposalState) != 0)
            {
                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref cancellation, null, currentCancellation),
                    currentCancellation))
                {
                    currentCancellation.Dispose();
                }

                return false;
            }

            SetRunning(true);
            summary = localize(
                $"저장된 현재 레시피의 {document.Steps.Count}개 단계를 순서대로 실행하고 있습니다.",
                $"Running {document.Steps.Count} saved current-recipe step(s) in order.");
            foreach (var step in getPipelineSteps())
            {
                step.State = "Run running";
            }

            NotifyStateChanged();
            appendLog(
                "Run",
                $"Ordered recipe Run started: {Path.GetFileName(fullRecipePath)} | steps={document.Steps.Count}.");

            try
            {
                var sourcePath = getSourcePath();
                var sourceQuality = getSourceQualityReport();
                var execution = await Task.Run(
                    () => ToolRecipeOrderedGraphExecution.Execute(
                        document,
                        sourcePath,
                        sourceQuality,
                        currentToken),
                    currentToken);
                currentToken.ThrowIfCancellationRequested();
                if (Volatile.Read(ref disposalState) != 0)
                {
                    return false;
                }

                result = execution;
                foreach (var step in getPipelineSteps())
                {
                    step.State = "Not run";
                }

                foreach (var stepResult in execution.Steps)
                {
                    var step = getPipelineSteps().FirstOrDefault(candidate =>
                        string.Equals(candidate.Id, stepResult.StepId, StringComparison.Ordinal));
                    if (step is not null)
                    {
                        step.State = $"Run {stepResult.Result.Status}";
                    }
                }

                summary = createSummary(execution);
                NotifyStateChanged();
                appendLog(
                    "Run",
                    $"Ordered recipe Run completed: status={execution.Status} | steps={execution.Steps.Count} | elapsedMs={execution.Duration.TotalMilliseconds:F3}.");
                completed(
                    new ToolWorkbenchOrderedRunCompletedEventArgs(
                        Path.GetFullPath(fullRecipePath),
                        document,
                        Path.GetFullPath(sourcePath),
                        execution));
                return execution.Status is not (ResultStatus.Error or ResultStatus.NotRun);
            }
            catch (OperationCanceledException) when (currentToken.IsCancellationRequested)
            {
                if (Volatile.Read(ref disposalState) != 0)
                {
                    return false;
                }

                foreach (var step in getPipelineSteps())
                {
                    step.State = "Run canceled";
                }

                result = null;
                recordPath = null;
                summary = localize(
                    "현재 레시피 실행이 취소되었습니다. Run Record는 생성되지 않았습니다.",
                    "Current recipe Run was canceled. No Run Record was created.");
                NotifyStateChanged();
                appendLog("Run", summary);
                return false;
            }
            finally
            {
                if (ReferenceEquals(cancellation, currentCancellation))
                {
                    cancellation = null;
                    currentCancellation.Dispose();
                }

                SetRunning(false);
            }
        }
        finally
        {
            Volatile.Write(ref runGate, 0);
            if (Volatile.Read(ref disposalState) == 0)
            {
                runCommand.RaiseCanExecuteChanged();
                cancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanRun()
    {
        if (Volatile.Read(ref disposalState) != 0
            || Volatile.Read(ref runGate) != 0
            || isRunning)
        {
            return false;
        }

        var capability = getCapability(createDocument());
        return capability.CanRun;
    }

    public void Cancel()
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        try
        {
            Volatile.Read(ref cancellation)?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }
    }

    public void AttachRecord(string path)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        recordPath = Path.GetFullPath(path);
        NotifyStateChanged();
    }

    public void ReportRecordFailure(string message)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        summary = localize(
            $"실행은 완료되었지만 Run Record 저장에 실패했습니다: {message}",
            $"Run completed, but its Run Record could not be saved: {message}");
        appendLog("Error", summary);
        NotifyStateChanged();
    }

    public void Invalidate(string invalidationSummary)
    {
        if (Volatile.Read(ref disposalState) != 0
            || result is null && recordPath is null)
        {
            return;
        }

        result = null;
        recordPath = null;
        summary = invalidationSummary;
        NotifyStateChanged();
        invalidated();
    }

    private void SetRunning(bool value)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        isRunning = value;
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        stateChanged();
        runCommand.RaiseCanExecuteChanged();
        cancelCommand.RaiseCanExecuteChanged();
    }

    private static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal already released the token source.
        }

        cancellation.Dispose();
    }
}

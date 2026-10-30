using System.IO;
using System.Threading;
using System.Text.Json;
using static OpenVisionLab.ThreeD.Shell.ViewModels.Workbench.ToolWorkbenchCancellationSourceLifetime;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Reporting.RunRecords;
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
    private readonly Func<ToolRecipeDocument, string, (bool CanRun, string Message)>? getInspectionCapability;
    private readonly Func<string?> getRecipePath;
    private readonly Func<string?>? getSavedRecipeSha256;
    private readonly Func<string> getSourcePath;
    private readonly Func<SourceQualityReport?> getSourceQualityReport;
    private readonly Func<IEnumerable<ToolWorkbenchPipelineStepItem>> getPipelineSteps;
    private readonly Func<ToolRecipeOrderedGraphExecutionResult, string> createSummary;
    private readonly Func<string, string, string> localize;
    private readonly Action<string, string> appendLog;
    private readonly Action<string, ResultStatus?, bool>? recordExecution;
    private readonly Action stateChanged;
    private readonly Action<ToolWorkbenchOrderedRunCompletedEventArgs> completed;
    private readonly Action invalidated;
    private readonly Action? clearTeachingOutputs;
    private readonly Func<ToolRecipeDocument, ToolRecipeOrderedGraphExecutionResult, CancellationToken, Task>? applyTeachingOutputs;
    private readonly RelayCommand cancelCommand;
    private readonly RelayCommand retryRecordSaveCommand;

    private readonly Dictionary<string, (string Step, string Context, ToolRecipeOrderedGraphStepResult? Result, string? Path)> publishedOutputs = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, InspectionQueueHeightMapSnapshot> heightMaps = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, (string Authored, ToolRecipeSelection Rebound)> runSelections = new(StringComparer.OrdinalIgnoreCase);

    private bool isRunning;
    private int runGate;
    private int disposalState;
    private CancellationTokenSource? cancellation;
    private Task<bool>? commandRunTask;
    private Task? commandRunObservationTask;
    private ToolRecipeOrderedGraphExecutionResult? result;
    private string? recordPath;
    private InspectionRunRecord? record;
    private ToolWorkbenchOrderedRunCompletedEventArgs? completedRun;
    private volatile bool hasPendingRecordSave;
    private bool wasLastRunCancelled;
    private string summary;
    private (ToolRecipeOrderedGraphExecutionResult? Result, string? RecordPath, InspectionRunRecord? Record, string Summary)? teachingRunBeforeInspection;

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
        Action invalidated,
        Func<ToolRecipeDocument, string, (bool CanRun, string Message)>? getInspectionCapability = null,
        Func<string?>? getSavedRecipeSha256 = null,
        Action<string, ResultStatus?, bool>? recordExecution = null,
        Action? clearTeachingOutputs = null,
        Func<ToolRecipeDocument, ToolRecipeOrderedGraphExecutionResult, CancellationToken, Task>? applyTeachingOutputs = null)
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
        this.getInspectionCapability = getInspectionCapability;
        this.getSavedRecipeSha256 = getSavedRecipeSha256;
        this.recordExecution = recordExecution;
        this.clearTeachingOutputs = clearTeachingOutputs;
        this.applyTeachingOutputs = applyTeachingOutputs;

        summary = localize(
            "저장된 현재 레시피를 직접 실행하면 검사 기록이 생성됩니다.",
            "Run the saved current recipe explicitly to create a Run Record.");
        runCommand = new RelayCommand(_ => StartCommandRun(), _ => CanRun());
        cancelCommand = new RelayCommand(
            _ => Cancel(),
            _ => Volatile.Read(ref disposalState) == 0 && Volatile.Read(ref runGate) != 0);
        retryRecordSaveCommand = new RelayCommand(_ => RetryRecordSave(), _ => HasPendingRecordSave && !IsRunning && Volatile.Read(ref disposalState) == 0 && Volatile.Read(ref runGate) == 0);
    }

    public RelayCommand RunCommand => runCommand;

    public RelayCommand CancelCommand => cancelCommand;

    public bool IsRunning => isRunning;
    public bool HasPendingRecordSave => hasPendingRecordSave;
    public RelayCommand RetryRecordSaveCommand => retryRecordSaveCommand;

    public ToolRecipeOrderedGraphExecutionResult? Result => result;
    internal IReadOnlyDictionary<string, InspectionQueueHeightMapSnapshot> HeightMaps => new Dictionary<string, InspectionQueueHeightMapSnapshot>(heightMaps, StringComparer.OrdinalIgnoreCase);

    public string? RecordPath => recordPath;

    public InspectionRunRecord? Record => record;

    public bool WasLastRunCancelled => wasLastRunCancelled;

    public string Summary => summary;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        var currentCancellation = Interlocked.Exchange(ref cancellation, null);
        CancelAndDispose(currentCancellation);
        Volatile.Write(ref commandRunObservationTask, null);
        result = null;
        recordPath = null;
        record = null;
        completedRun = null;
        teachingRunBeforeInspection = null;
        publishedOutputs.Clear();
        heightMaps.Clear();
        runSelections.Clear();
        hasPendingRecordSave = false;
        isRunning = false;
    }

    public Task<bool> RunAsync() => RunCoreAsync(null, null, null);

    public Task<bool> RunInspectionInputAsync(
        ToolRecipeDocument document,
        string recipePath,
        string sourcePath)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        if (getInspectionCapability is null)
        {
            throw new InvalidOperationException("Inspection input execution is not configured.");
        }

        return RunCoreAsync(document, recipePath, sourcePath);
    }

    private async Task<bool> RunCoreAsync(
        ToolRecipeDocument? requestedDocument,
        string? requestedRecipePath,
        string? requestedSourcePath)
    {
        if (Volatile.Read(ref disposalState) != 0 || HasPendingRecordSave)
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
        CancellationTokenSource? currentCancellation = null;
        try
        {
            if (Volatile.Read(ref disposalState) != 0 || HasPendingRecordSave)
            {
                return false;
            }

            wasLastRunCancelled = false;
            currentCancellation = new CancellationTokenSource();
            var currentToken = currentCancellation.Token;
            var previousCancellation = Interlocked.Exchange(
                ref cancellation,
                currentCancellation);
            CancelAndDispose(previousCancellation);
            cancelCommand.RaiseCanExecuteChanged();
            if (Volatile.Read(ref disposalState) != 0)
            {
                return false;
            }

            var document = requestedDocument ?? createDocument();
            var recipePath = requestedRecipePath ?? getRecipePath();
            var sourcePath = requestedSourcePath ?? getSourcePath();
            currentToken.ThrowIfCancellationRequested();
            var capability = requestedSourcePath is null
                ? getCapability(document)
                : getInspectionCapability!(document, sourcePath);
            currentToken.ThrowIfCancellationRequested();
            if (Volatile.Read(ref disposalState) != 0)
            {
                return false;
            }

            if (!capability.CanRun || string.IsNullOrWhiteSpace(recipePath) || !File.Exists(recipePath))
            {
                summary = capability.Message;
                NotifyStateChanged();
                appendLog("Run", capability.Message);
                return false;
            }

            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                summary = localize("검사할 3D 입력을 먼저 여세요.", "Open a 3D input before running an inspection.");
                NotifyStateChanged();
                return false;
            }

            var fullRecipePath = Path.GetFullPath(recipePath);
            var fullSourcePath = Path.GetFullPath(sourcePath);
            var identity = OrderedRunRecordIdentity.Create(fullRecipePath, fullSourcePath);
            if (getSavedRecipeSha256 is not null && !string.Equals(getSavedRecipeSha256(), identity.RecipeSha256, StringComparison.OrdinalIgnoreCase))
            {
                summary = localize("실행 준비 중 저장된 레시피가 바뀌었습니다. 현재 결과를 유지합니다. 레시피를 다시 확인하세요.", "The saved recipe changed while preparing Run. The current results were retained. Review the recipe again.");
                NotifyStateChanged();
                appendLog("Run", summary);
                return false;
            }
            if (requestedSourcePath is not null)
                teachingRunBeforeInspection ??= (result, recordPath, record, summary);
            else
                teachingRunBeforeInspection = null;
            completedRun = null;
            result = null;
            recordPath = null;
            record = null;
            heightMaps.Clear();
            SetRunning(true);
            if (requestedSourcePath is null)
            {
                publishedOutputs.Clear();
                runSelections.Clear();
                foreach (var step in document.Steps)
                    publishedOutputs[step.OutputEntityId] = (step.Id, document.CreateStepExecutionContext(step.Id), null, null);
                clearTeachingOutputs?.Invoke();
            }
            summary = localize(
                $"저장된 현재 레시피의 {document.Steps.Count}개 단계를 순서대로 실행하고 있습니다.",
                $"Running {document.Steps.Count} saved current-recipe step(s) in order.");
            NotifyStateChanged();

            try
            {
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

                var sourceQuality = requestedSourcePath is null
                    ? getSourceQualityReport()
                    : null;
                var execution = await Task.Run(
                    () => ToolRecipeOrderedGraphExecution.Execute(
                        document,
                        fullSourcePath,
                        sourceQuality,
                        currentToken),
                    currentToken);
                currentToken.ThrowIfCancellationRequested();
                if (Volatile.Read(ref disposalState) != 0)
                {
                    return false;
                }

                foreach (var stepResult in execution.Steps.Where(item => item.RuntimeOutput is not null))
                {
                    currentToken.ThrowIfCancellationRequested();
                    string? path = null;
                    if (stepResult.RuntimeOutput is C3DHeightFieldSnapshot map)
                    {
                        path = await MaterializeOutputAsync(map, currentToken);
                        currentToken.ThrowIfCancellationRequested();
                        heightMaps[map.EntityId] = new(map.EntityId, stepResult.ToolId, path, map.ContentSha256,
                            map.RootSourceSha256, map.Unit, map.FrameId, map.Width, map.Height, map.GridOriginColumn, map.GridOriginRow);
                    }
                    if (requestedSourcePath is null)
                    {
                        var context = document.CreateStepExecutionContext(stepResult.StepId);
                        if (createDocument().CreateStepExecutionContext(stepResult.StepId) != context)
                            throw new InvalidDataException("The input or conditions changed during ordered Run.");
                        publishedOutputs[stepResult.OutputEntityId] = (stepResult.StepId, context, stepResult, path);
                    }
                }
                if (requestedSourcePath is null && applyTeachingOutputs is not null)
                {
                    await applyTeachingOutputs(document, execution, currentToken);
                    currentToken.ThrowIfCancellationRequested();
                    if (Volatile.Read(ref disposalState) != 0) return false;
                }

                if (requestedSourcePath is null)
                {
                    foreach (var selection in document.Selections ?? [])
                    {
                        var rebound = execution.ReboundDocument.Selections?.FirstOrDefault(item => item.Id == selection.Id);
                        if (rebound?.SourceBinding.OwnerEntityId is not null)
                            runSelections[selection.Id] = (JsonSerializer.Serialize(selection with { Name = string.Empty }), rebound);
                    }
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
                    $"Ordered recipe Run completed: status={execution.Status} | steps={execution.Steps.Count} | elapsedMs={execution.Duration.TotalMilliseconds:0.##}.");
                var passCount = execution.Steps.Count(step => step.Result.Status == ResultStatus.Pass && ToolRecipeStep.RequiresAcceptanceLimits(step.ToolId));
                var completedCount = execution.Steps.Count(step => step.Result.Status == ResultStatus.Pass && !ToolRecipeStep.RequiresAcceptanceLimits(step.ToolId));
                var failCount = execution.Steps.Count(step => step.Result.Status == ResultStatus.Fail);
                var errorCount = execution.Steps.Count(step => step.Result.Status == ResultStatus.Error);
                var warningCount = execution.Steps.Count(step => step.Result.Status == ResultStatus.Warning);
                var notRunCount = execution.Steps.Count(step => step.Result.Status == ResultStatus.NotRun);
                var executionMessage = localize($"모든 도구 실행 · OK {passCount} · NG {failCount} · 오류 {errorCount}", $"Run all tools · OK {passCount} · NG {failCount} · errors {errorCount}");
                if (completedCount > 0) executionMessage += localize($" · 전처리 완료 {completedCount}", $" · preprocessing completed {completedCount}");
                if (warningCount > 0) executionMessage += localize($" · 경고 {warningCount}", $" · warnings {warningCount}");
                if (notRunCount > 0) executionMessage += localize($" · 미실행 {notRunCount}", $" · not run {notRunCount}");
                recordExecution?.Invoke(executionMessage, execution.Status, false);
                completedRun = new ToolWorkbenchOrderedRunCompletedEventArgs(fullRecipePath, document, fullSourcePath, execution, identity);
                completed(completedRun);
                return execution.Status is not (ResultStatus.Error or ResultStatus.NotRun);
            }
            catch (OperationCanceledException) when (currentToken.IsCancellationRequested)
            {
                if (requestedSourcePath is null)
                {
                    foreach (var id in publishedOutputs.Keys.ToArray())
                    {
                        var entry = publishedOutputs[id];
                        publishedOutputs[id] = (entry.Step, entry.Context, null, null);
                    }
                    clearTeachingOutputs?.Invoke();
                }
                return CompleteCancelledRun();
            }
        }
        catch (OperationCanceledException) when (currentCancellation?.IsCancellationRequested == true)
        {
            return CompleteCancelledRun();
        }
        catch (Exception exception)
        {
            if (isRunning) heightMaps.Clear();
            if (requestedSourcePath is null && isRunning)
            {
                foreach (var id in publishedOutputs.Keys.ToArray())
                {
                    var entry = publishedOutputs[id];
                    publishedOutputs[id] = (entry.Step, entry.Context, null, null);
                }
                clearTeachingOutputs?.Invoke();
            }
            if (Volatile.Read(ref disposalState) == 0 && isRunning)
                recordExecution?.Invoke(localize($"현재 레시피 실행 중 오류가 발생했습니다: {exception.Message}", $"The current recipe Run failed: {exception.Message}"), ResultStatus.Error, false);
            throw;
        }
        finally
        {
            if (currentCancellation is not null
                && ReferenceEquals(
                    Interlocked.CompareExchange(ref cancellation, null, currentCancellation),
                    currentCancellation))
            {
                currentCancellation.Dispose();
            }

            SetRunning(false);
            Volatile.Write(ref runGate, 0);
            if (Volatile.Read(ref disposalState) == 0)
            {
                runCommand.RaiseCanExecuteChanged();
                cancelCommand.RaiseCanExecuteChanged();
                retryRecordSaveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    internal bool TryGetPublishedOutput<T>(string id, out T? output) where T : class
    {
        output = null;
        if (!publishedOutputs.TryGetValue(id, out var entry)) return false;
        if (Volatile.Read(ref disposalState) == 0 && !IsRunning && entry.Result?.RuntimeOutput is T value
            && createDocument().Steps.Any(step => step.Id == entry.Step)
            && createDocument().CreateStepExecutionContext(entry.Step) == entry.Context) output = value;
        return true;
    }

    internal ToolRecipeSelection? GetCurrentRunSelection(ToolRecipeSelection selection)
    {
        if (IsRunning || !runSelections.TryGetValue(selection.Id, out var cached)
            || cached.Rebound.SourceBinding.OwnerEntityId is not { } ownerId
            || GetPublishedStep(ownerId) is null
            || cached.Authored != JsonSerializer.Serialize(selection with { Name = string.Empty })) return null;
        return cached.Rebound with { Name = selection.Name };
    }

    internal ToolRecipeDocument WithCurrentRunSelections(ToolRecipeDocument document) => document with
    {
        Selections = document.Selections?.Select(selection => GetCurrentRunSelection(selection) ?? selection).ToArray()
    };

    internal bool OwnsOutput(string id) => publishedOutputs.ContainsKey(id);

    internal ToolRecipeOrderedGraphStepResult? GetPublishedStep(string id) =>
        TryGetPublishedOutput<object>(id, out var output) && output is not null ? publishedOutputs[id].Result : null;

    internal string? GetMaterializedPath(string id) => publishedOutputs.TryGetValue(id, out var entry) ? entry.Path : null;

    internal string? GetPublishedPath(string id) => GetPublishedStep(id) is not null ? GetMaterializedPath(id) : null;

    internal IReadOnlyList<ToolRecipeOrderedGraphStepResult> GetPublishedSteps() => publishedOutputs.Keys
        .Select(GetPublishedStep).OfType<ToolRecipeOrderedGraphStepResult>().ToArray();

    internal IReadOnlyList<string> BeginSelectedExecution(ToolWorkbenchPipelineStepItem step)
    {
        var outputs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { step.OutputEntityId };
        var document = createDocument();
        foreach (var candidate in getPipelineSteps())
            if (document.DependsOnEntity(candidate.Id, step.OutputEntityId)) outputs.Add(candidate.OutputEntityId);
        foreach (var id in outputs)
        {
            if (!publishedOutputs.TryGetValue(id, out var entry)) continue;
            if (id == step.OutputEntityId) publishedOutputs.Remove(id);
            else publishedOutputs[id] = (entry.Step, entry.Context, null, null);
        }
        Invalidate(string.Empty);
        return outputs.ToArray();
    }

    private static Task<string> MaterializeOutputAsync(C3DHeightFieldSnapshot map, CancellationToken token) => Task.Run(() =>
    {
        var root = Environment.GetEnvironmentVariable("OPENVISIONLAB_3D_TEST_ARTIFACT_ROOT");
        var folder = Path.Combine(string.IsNullOrWhiteSpace(root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenVisionLab", "3DStudio")
            : Path.GetFullPath(root), "Preview");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"ordered-{map.ContentSha256}.c3d");
        var temporary = path + ".tmp." + Guid.NewGuid().ToString("N");
        try { map.SaveC3D(temporary); token.ThrowIfCancellationRequested(); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        return path;
    }, token);

    private bool CompleteCancelledRun()
    {
        heightMaps.Clear();
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
        record = null;
        wasLastRunCancelled = true;
        summary = localize(
            "현재 레시피 실행이 취소되었습니다. 검사 기록은 생성되지 않았습니다.",
            "Current recipe Run was canceled. No Run Record was created.");
        NotifyStateChanged();
        appendLog("Run", summary);
        recordExecution?.Invoke(summary, null, true);
        return false;
    }

    private void StartCommandRun()
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        if (Volatile.Read(ref commandRunObservationTask) is { IsCompleted: true })
        {
            Volatile.Write(ref commandRunObservationTask, null);
        }

        var task = RunAsync();
        Volatile.Write(ref commandRunTask, task);
        Volatile.Write(ref commandRunObservationTask, ObserveCommandRunAsync(task));
    }

    private async Task ObserveCommandRunAsync(Task<bool> task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            ReportCommandRunFailure(exception);
        }
        finally
        {
            _ = Interlocked.CompareExchange(ref commandRunTask, null, task);
        }
    }

    private void ReportCommandRunFailure(Exception exception)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        summary = localize(
            $"현재 레시피 실행 중 오류가 발생했습니다: {exception.Message}",
            $"The current recipe Run failed: {exception.Message}");
        appendLog("Error", summary);
        NotifyStateChanged();
    }

    public bool CanRun()
    {
        if (Volatile.Read(ref disposalState) != 0
            || Volatile.Read(ref runGate) != 0
            || isRunning || HasPendingRecordSave)
        {
            return false;
        }

        var capability = getCapability(createDocument());
        return capability.CanRun;
    }

    public void Cancel()
    {
        if (Volatile.Read(ref disposalState) != 0 || Volatile.Read(ref runGate) == 0)
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

    public void AttachRecord(string path, InspectionRunRecord attachedRecord)
    {
        ArgumentNullException.ThrowIfNull(attachedRecord);
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        recordPath = Path.GetFullPath(path);
        record = attachedRecord;
        hasPendingRecordSave = false;
        completedRun = null;
        NotifyStateChanged();
    }

    public void ReportRecordFailure(string message, InspectionRunRecord? preparedRecord = null)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        hasPendingRecordSave = completedRun is not null;
        record = preparedRecord;
        summary = localize(
            $"실행은 완료되었지만 검사 기록 저장에 실패했습니다: {message}",
            $"Run completed, but its Run Record could not be saved: {message}");
        appendLog("Error", summary);
        NotifyStateChanged();
    }

    public bool RetryRecordSave()
    {
        if (!HasPendingRecordSave || completedRun is not { } pending || Volatile.Read(ref disposalState) != 0 || Interlocked.CompareExchange(ref runGate, 1, 0) != 0) return false;
        try
        {
            completed(pending);
            return !HasPendingRecordSave && record is not null && recordPath is not null;
        }
        finally
        {
            Volatile.Write(ref runGate, 0);
            NotifyStateChanged();
        }
    }

    public void Invalidate(string invalidationSummary)
    {
        if (Volatile.Read(ref disposalState) != 0 || HasPendingRecordSave)
        {
            return;
        }

        if (IsRunning)
        {
            Cancel();
            return;
        }

        var hadTeachingRun = teachingRunBeforeInspection is not null;
        teachingRunBeforeInspection = null;
        if (result is null && recordPath is null && !hadTeachingRun)
        {
            return;
        }

        result = null;
        recordPath = null;
        record = null;
        completedRun = null;
        summary = invalidationSummary;
        NotifyStateChanged();
        invalidated();
    }

    public bool RestoreTeachingRun()
    {
        if (IsRunning || HasPendingRecordSave || Volatile.Read(ref disposalState) != 0
            || teachingRunBeforeInspection is not { } teachingRun) return false;

        teachingRunBeforeInspection = null;
        result = teachingRun.Result;
        recordPath = teachingRun.RecordPath;
        record = teachingRun.Record;
        summary = teachingRun.Summary;
        completedRun = null;
        wasLastRunCancelled = false;
        foreach (var step in getPipelineSteps())
        {
            var executed = result?.Steps.FirstOrDefault(item => item.StepId == step.Id);
            step.State = executed is null ? "Not run" : $"Run {executed.Result.Status}";
        }
        NotifyStateChanged();
        return true;
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
        retryRecordSaveCommand.RaiseCanExecuteChanged();
    }

}

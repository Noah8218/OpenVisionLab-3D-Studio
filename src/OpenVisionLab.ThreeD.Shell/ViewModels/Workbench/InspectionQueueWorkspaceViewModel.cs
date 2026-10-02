using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public enum InspectionQueueItemState
{
    Untested,
    Running,
    Pass,
    Fail,
    Warning,
    Error,
    Cancelled
}

public enum InspectionQueueStatusFilter
{
    All,
    Untested,
    Pass,
    Fail,
    Warning,
    Error,
    Cancelled
}

public enum InspectionQueueRunScope
{
    Selected,
    All,
    Checked,
    Remaining
}

public sealed record InspectionQueueRunOutcome(
    bool WasCancelled,
    InspectionRunRecord? Record,
    string? RecordPath,
    string Message,
    bool RecordSaveFailed = false);

public sealed record InspectionQueueExportRow(
    int Order,
    string SourcePath,
    string Status,
    string Message,
    string RunId,
    DateTimeOffset? RecordedAtUtc,
    string RecipePath,
    string RecipeVersion,
    string RecipeSha256,
    string SourceSha256,
    InspectionRunRecord? Record = null,
    bool RecordSaved = false);

public sealed class InspectionQueueWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly Func<ToolRecipeDocument> createDocument;
    private readonly Func<bool> canUseTeachingInput;
    private readonly Func<string?> getRecipePath;
    private readonly Func<ToolRecipeDocument, (bool CanRun, string Message)> getRecipeReadiness;
    private readonly Func<ToolRecipeDocument, string, (bool IsCompatible, string Message)> validateInput;
    private readonly Func<ToolRecipeDocument, string, string, Task<InspectionQueueRunOutcome>> runInput;
    private readonly Action cancelCurrentRun;
    private readonly Func<InspectionQueueRunOutcome>? retryRecordSave;
    private readonly ObservableCollection<InspectionQueueSampleItem> samples = [];
    private readonly ObservableCollection<InspectionQueueSampleItem> visibleSamples = [];
    private readonly ReadOnlyObservableCollection<InspectionQueueSampleItem> readOnlySamples;
    private readonly ReadOnlyObservableCollection<InspectionQueueSampleItem> readOnlyVisibleSamples;
    private readonly RelayCommand addFilesCommand;
    private readonly RelayCommand addFolderCommand;
    private readonly RelayCommand removeSelectedCommand;
    private readonly RelayCommand selectSampleCommand;
    private readonly RelayCommand useSelectedInputForTeachingCommand;
    private readonly RelayCommand clearCommand;
    private readonly RelayCommand checkVisibleCommand;
    private readonly RelayCommand uncheckVisibleCommand;
    private readonly RelayCommand clearSearchCommand;
    private readonly RelayCommand runSelectedCommand;
    private readonly RelayCommand runAllCommand;
    private readonly RelayCommand runCheckedCommand;
    private readonly RelayCommand runRemainingCommand;
    private readonly RelayCommand stopCommand;
    private readonly RelayCommand openRunRecordCommand;
    private readonly RelayCommand openPreviousRunRecordCommand;
    private readonly RelayCommand exportCsvCommand;
    private readonly RelayCommand returnToTeachingCommand;
    private readonly RelayCommand retryRecordSaveCommand;
    private readonly RelayCommand newInspectionContextCommand;
    private InspectionQueueSampleItem? pendingRecordSample;
    private ToolRecipeDocument? pendingRecordDocument;
    private (string Path, string Sha256)? recipeContext;
    private InspectionQueueSampleItem? selectedSample;
    private InspectionQueueStepResultItem? selectedStepResult;
    private InspectionQueueStatusFilter statusFilter;
    private bool showOnlyFailedSteps;
    private string searchText = string.Empty;
    private string summaryText = string.Empty;
    private (string Korean, string English)? localizedSummary;
    private string progressText = string.Empty;
    private int totalRunTargetCount;
    private int completedRunTargetCount;
    private int isRunning;
    private int cancelRequested;
    private int disposalState;
    private int recordSaveGate;

    public InspectionQueueWorkspaceViewModel(
        Func<ToolRecipeDocument> createDocument,
        Func<string?> getRecipePath,
        Func<ToolRecipeDocument, (bool CanRun, string Message)> getRecipeReadiness,
        Func<ToolRecipeDocument, string, (bool IsCompatible, string Message)> validateInput,
        Func<ToolRecipeDocument, string, string, Task<InspectionQueueRunOutcome>> runInput,
        Action cancelCurrentRun,
        Func<string, string, string> localize,
        Func<InspectionQueueRunOutcome>? retryRecordSave = null,
        Func<bool>? canUseTeachingInput = null)
    {
        this.createDocument = createDocument ?? throw new ArgumentNullException(nameof(createDocument));
        this.canUseTeachingInput = canUseTeachingInput ?? (() => true);
        this.getRecipePath = getRecipePath ?? throw new ArgumentNullException(nameof(getRecipePath));
        this.getRecipeReadiness = getRecipeReadiness ?? throw new ArgumentNullException(nameof(getRecipeReadiness));
        this.validateInput = validateInput ?? throw new ArgumentNullException(nameof(validateInput));
        this.runInput = runInput ?? throw new ArgumentNullException(nameof(runInput));
        this.cancelCurrentRun = cancelCurrentRun ?? throw new ArgumentNullException(nameof(cancelCurrentRun));
        this.retryRecordSave = retryRecordSave;
        Localize = localize ?? throw new ArgumentNullException(nameof(localize));
        readOnlySamples = new ReadOnlyObservableCollection<InspectionQueueSampleItem>(samples);
        readOnlyVisibleSamples = new ReadOnlyObservableCollection<InspectionQueueSampleItem>(visibleSamples);
        addFilesCommand = new RelayCommand(_ => SelectFilesRequested?.Invoke(this, EventArgs.Empty), _ => CanEditQueue);
        addFolderCommand = new RelayCommand(_ => SelectFolderRequested?.Invoke(this, EventArgs.Empty), _ => CanEditQueue);
        removeSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => CanEditQueue && SelectedSample is not null);
        clearCommand = new RelayCommand(_ => ClearQueue(), _ => CanEditQueue && samples.Count > 0);
        selectSampleCommand = new RelayCommand(
            parameter => SelectedSample = parameter as InspectionQueueSampleItem,
            parameter => !IsDisposed && parameter is InspectionQueueSampleItem item && samples.Contains(item));
        checkVisibleCommand = new RelayCommand(_ => SetVisibleChecked(true), _ => CanEditQueue && visibleSamples.Count > 0);
        uncheckVisibleCommand = new RelayCommand(_ => SetVisibleChecked(false), _ => CanEditQueue && visibleSamples.Count > 0);
        clearSearchCommand = new RelayCommand(_ => SearchText = string.Empty, _ => !string.IsNullOrEmpty(SearchText));
        runSelectedCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.Selected), _ => CanRun(InspectionQueueRunScope.Selected));
        runAllCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.All), _ => CanRun(InspectionQueueRunScope.All));
        runCheckedCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.Checked), _ => CanRun(InspectionQueueRunScope.Checked));
        runRemainingCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.Remaining), _ => CanRun(InspectionQueueRunScope.Remaining));
        stopCommand = new RelayCommand(_ => Stop(), _ => IsRunning);
        openRunRecordCommand = new RelayCommand(
            _ => OpenRunRecordRequested?.Invoke(this, new InspectionQueueRunRecordRequestEventArgs(SelectedSample!.RunRecordPath!)),
            _ => !IsRunning && SelectedSample?.CanOpenCurrentRunRecord == true);
        openPreviousRunRecordCommand = new RelayCommand(
            _ => OpenRunRecordRequested?.Invoke(this, new InspectionQueueRunRecordRequestEventArgs(SelectedSample!.PreviousRunRecordPath!)),
            _ => !IsRunning && SelectedSample?.CanOpenPreviousRunRecord == true);
        exportCsvCommand = new RelayCommand(_ => ExportCsvRequested?.Invoke(this, EventArgs.Empty), _ => !IsRunning && samples.Count > 0);
        returnToTeachingCommand = new RelayCommand(
            _ => ReturnToTeachingRequested?.Invoke(this, EventArgs.Empty),
            _ => !IsRunning);
        useSelectedInputForTeachingCommand = new RelayCommand(_ =>
        {
            if (CanUseSelectedInputForTeaching) UseSelectedInputForTeachingRequested?.Invoke(this, new InspectionQueueSelectionChangedEventArgs(SelectedSample));
        }, _ => CanUseSelectedInputForTeaching);
        retryRecordSaveCommand = new RelayCommand(_ => RetryRecordSave(), _ => !IsDisposed && !IsRunning && HasPendingRecordSave && Volatile.Read(ref recordSaveGate) == 0 && this.retryRecordSave is not null);
        newInspectionContextCommand = new RelayCommand(_ => StartNewInspectionContext(), _ => CanEditQueue && recipeContext is not null);
        statusFilter = InspectionQueueStatusFilter.All;
        RefreshProjection();
        RefreshCommands();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? SelectFilesRequested;
    public event EventHandler? SelectFolderRequested;
    public event EventHandler? ExportCsvRequested;
    public event EventHandler<InspectionQueueRunRecordRequestEventArgs>? OpenRunRecordRequested;
    public event EventHandler? ReturnToTeachingRequested;
    public event EventHandler<InspectionQueueSelectionChangedEventArgs>? UseSelectedInputForTeachingRequested;
    public event EventHandler<InspectionQueueSelectionChangedEventArgs>? SelectedSampleChanged;

    public ReadOnlyObservableCollection<InspectionQueueSampleItem> Samples => readOnlySamples;
    public ReadOnlyObservableCollection<InspectionQueueSampleItem> VisibleSamples => readOnlyVisibleSamples;
    public ICommand AddFilesCommand => addFilesCommand;
    public ICommand AddFolderCommand => addFolderCommand;
    public ICommand RemoveSelectedCommand => removeSelectedCommand;
    public ICommand ClearQueueCommand => clearCommand;
    public ICommand SelectSampleCommand => selectSampleCommand;
    public ICommand CheckVisibleCommand => checkVisibleCommand;
    public ICommand UncheckVisibleCommand => uncheckVisibleCommand;
    public ICommand ClearSearchCommand => clearSearchCommand;
    public ICommand RunSelectedCommand => runSelectedCommand;
    public ICommand RunAllCommand => runAllCommand;
    public ICommand RunCheckedCommand => runCheckedCommand;
    public ICommand RunRemainingCommand => runRemainingCommand;
    public ICommand StopCommand => stopCommand;
    public ICommand OpenRunRecordCommand => openRunRecordCommand;
    public ICommand OpenPreviousRunRecordCommand => openPreviousRunRecordCommand;
    public ICommand ExportCsvCommand => exportCsvCommand;
    public ICommand ReturnToTeachingCommand => returnToTeachingCommand;
    public ICommand UseSelectedInputForTeachingCommand => useSelectedInputForTeachingCommand;
    public bool CanUseSelectedInputForTeaching => CanEditQueue && canUseTeachingInput() && SelectedSample is { } sample
        && string.Equals(Path.GetExtension(sample.SourcePath), ".c3d", StringComparison.OrdinalIgnoreCase) && File.Exists(sample.SourcePath);
    public void RefreshTeachingInputCommand() => useSelectedInputForTeachingCommand.RaiseCanExecuteChanged();
    public ICommand RetryRecordSaveCommand => retryRecordSaveCommand;
    public ICommand NewInspectionContextCommand => newInspectionContextCommand;
    public bool HasPendingRecordSave => pendingRecordSample is not null;
    public Func<string, string, string> Localize { get; }
    public bool IsRunning => Volatile.Read(ref isRunning) != 0;
    public bool IsIdle => !IsRunning;
    public bool HasSamples => samples.Count > 0;
    public bool HasVisibleSamples => visibleSamples.Count > 0;
    public bool HasSelectedSample => SelectedSample is not null;
    public bool ShowOnlyFailedSteps
    {
        get => showOnlyFailedSteps;
        set
        {
            if (showOnlyFailedSteps == value) return;
            showOnlyFailedSteps = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VisibleStepResults));
        }
    }
    public IReadOnlyList<InspectionQueueStepResultItem> VisibleStepResults =>
        (SelectedSample?.Steps ?? []).Where(item => !ShowOnlyFailedSteps || item.Status == nameof(ResultStatus.Fail)).ToArray();
    public bool IsStatusFilterAll => statusFilter == InspectionQueueStatusFilter.All;
    public bool IsStatusFilterUntested => statusFilter == InspectionQueueStatusFilter.Untested;
    public bool IsStatusFilterPass => statusFilter == InspectionQueueStatusFilter.Pass;
    public bool IsStatusFilterFail => statusFilter == InspectionQueueStatusFilter.Fail;
    public bool IsStatusFilterWarning => statusFilter == InspectionQueueStatusFilter.Warning;
    public bool IsStatusFilterError => statusFilter == InspectionQueueStatusFilter.Error;
    public bool IsStatusFilterCancelled => statusFilter == InspectionQueueStatusFilter.Cancelled;
    public int AllCount => samples.Count;
    public int CheckedCount => samples.Count(item => item.IsChecked);
    public string RunCheckedLabel => Localize($"체크한 {CheckedCount}개 검사", $"Inspect {CheckedCount} checked");
    public int PassCount => Count(InspectionQueueItemState.Pass);
    public int FailCount => Count(InspectionQueueItemState.Fail);
    public int WarningCount => Count(InspectionQueueItemState.Warning);
    public int ErrorCount => Count(InspectionQueueItemState.Error);
    public int CancelledCount => Count(InspectionQueueItemState.Cancelled);
    public int UntestedCount => Count(InspectionQueueItemState.Untested);
    public string CountsText => Localize(
        $"전체 {AllCount} · 체크 {CheckedCount} · 통과 {PassCount} · NG {FailCount} · 주의 {WarningCount} · 오류 {ErrorCount} · 취소 {CancelledCount} · 미검사 {UntestedCount}",
        $"All {AllCount} · checked {CheckedCount} · pass {PassCount} · fail {FailCount} · warning {WarningCount} · error {ErrorCount} · cancelled {CancelledCount} · untested {UntestedCount}");
    public string SavedRecipeText => getRecipePath() is { Length: > 0 } path && File.Exists(path)
        ? Path.GetFileName(path)
        : Localize("저장된 레시피 없음", "No saved recipe");
    public int ProgressPercent => totalRunTargetCount == 0
        ? 0
        : (int)Math.Round(100d * completedRunTargetCount / totalRunTargetCount);
    public string ProgressText => progressText;
    public string SummaryText
    {
        get => localizedSummary is { } text ? Localize(text.Korean, text.English) : summaryText;
        private set
        {
            if (localizedSummary is null && summaryText == value) return;
            localizedSummary = null;
            summaryText = value;
            OnPropertyChanged();
        }
    }
    public string RecipeReadinessText
    {
        get
        {
            try
            {
                if (HasPendingRecordSave) return Localize("기록 저장이 필요합니다. 저장 재시도 후 남은 파일을 직접 실행하세요.", "Save the pending Run Record, then explicitly inspect remaining files.");
                if (!IsRecipeContextCurrent()) return Localize("저장된 레시피가 바뀌었습니다. '새 검사 목록'으로 준비한 뒤 실행하세요.", "The saved recipe has changed. Prepare a new inspection queue before running.");
                return getRecipeReadiness(createDocument()).Message;
            }
            catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
            {
                return exception.Message;
            }
        }
    }

    public InspectionQueueSampleItem? SelectedSample
    {
        get => selectedSample;
        set
        {
            if (ReferenceEquals(selectedSample, value)) return;
            if (selectedSample is not null) selectedSample.IsSelected = false;
            selectedSample = value;
            SelectedStepResult = null;
            if (selectedSample is not null) selectedSample.IsSelected = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedSample));
            OnPropertyChanged(nameof(VisibleStepResults));
            SelectedSampleChanged?.Invoke(this, new InspectionQueueSelectionChangedEventArgs(value));
            RefreshCommands();
        }
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (searchText == value) return;
            searchText = value ?? string.Empty;
            OnPropertyChanged();
            RefreshProjection();
            clearSearchCommand.RaiseCanExecuteChanged();
        }
    }

    public InspectionQueueStepResultItem? SelectedStepResult
    {
        get => selectedStepResult;
        set
        {
            if (ReferenceEquals(selectedStepResult, value)) return;
            if (value is not null && SelectedSample?.Steps.Contains(value) != true) return;
            selectedStepResult = value;
            OnPropertyChanged();
        }
    }

    public InspectionQueueStatusFilter StatusFilter
    {
        get => statusFilter;
        set
        {
            if (statusFilter == value) return;
            statusFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsStatusFilterAll));
            OnPropertyChanged(nameof(IsStatusFilterUntested));
            OnPropertyChanged(nameof(IsStatusFilterPass));
            OnPropertyChanged(nameof(IsStatusFilterFail));
            OnPropertyChanged(nameof(IsStatusFilterWarning));
            OnPropertyChanged(nameof(IsStatusFilterError));
            OnPropertyChanged(nameof(IsStatusFilterCancelled));
            RefreshProjection();
        }
    }

    public void AddSources(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        if (!CanEditQueue) return;

        var added = 0;
        var duplicates = 0;
        var rejected = 0;
        foreach (var sourcePath in sourcePaths)
        {
            if (string.IsNullOrWhiteSpace(sourcePath)
                || !string.Equals(Path.GetExtension(sourcePath), ".c3d", StringComparison.OrdinalIgnoreCase))
            {
                rejected++;
                continue;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(sourcePath);
            }
            catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException)
            {
                rejected++;
                continue;
            }

            if (samples.Any(item => string.Equals(item.SourcePath, fullPath, StringComparison.OrdinalIgnoreCase)))
            {
                duplicates++;
                continue;
            }

            var item = new InspectionQueueSampleItem(samples.Count + 1, fullPath, Localize);
            item.PropertyChanged += OnSamplePropertyChanged;
            if (!File.Exists(fullPath))
            {
                item.SetError(Localize("파일을 찾을 수 없습니다.", "File was not found."));
            }
            samples.Add(item);
            added++;
        }

        ReindexSamples();
        RefreshCounts();
        RefreshProjection();
        SetLocalizedSummary(
            $"파일 {added}개 추가 · 중복 {duplicates}개 건너뜀 · 거부 {rejected}개",
            $"Added {added} file(s) · skipped {duplicates} duplicate(s) · rejected {rejected}");
        RefreshCommands();
    }

    public void SetStatusFilter(string? value)
    {
        if (Enum.TryParse(value, ignoreCase: true, out InspectionQueueStatusFilter parsed)
            && Enum.IsDefined(parsed))
        {
            StatusFilter = parsed;
        }
    }

    public void ReportImportError(string message)
    {
        if (IsDisposed || string.IsNullOrWhiteSpace(message)) return;
        SummaryText = message;
    }

    public IReadOnlyList<InspectionQueueExportRow> CreateExportRows() => samples.Select(item =>
        new InspectionQueueExportRow(
            item.Order,
            item.SourcePath,
            item.State.ToString(),
            item.Message,
            item.Record?.RunId ?? string.Empty,
            item.Record?.RecordedAtUtc,
            item.Record?.Recipe.Path ?? string.Empty,
            item.Record?.Recipe.Version ?? string.Empty,
            item.Record?.Recipe.Sha256 ?? string.Empty,
            item.Record?.Source.Sha256 ?? string.Empty,
            item.Record,
            item.CanOpenCurrentRunRecord)).ToArray();

    public void RefreshResultPresentation()
    {
        RefreshCounts();
        UpdateProgress();
        OnPropertyChanged(nameof(RecipeReadinessText));
        OnPropertyChanged(nameof(SavedRecipeText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(VisibleStepResults));
        OnPropertyChanged(nameof(HasPendingRecordSave));
        foreach (var item in samples) item.RefreshResultPresentation();
    }

    public void RefreshCommands()
    {
        RefreshTeachingInputCommand();
        OnPropertyChanged(nameof(HasPendingRecordSave));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(RecipeReadinessText));
        OnPropertyChanged(nameof(SavedRecipeText));
        OnPropertyChanged(nameof(RunCheckedLabel));
        OnPropertyChanged(nameof(VisibleStepResults));
        addFilesCommand.RaiseCanExecuteChanged();
        addFolderCommand.RaiseCanExecuteChanged();
        removeSelectedCommand.RaiseCanExecuteChanged();
        selectSampleCommand.RaiseCanExecuteChanged();
        clearCommand.RaiseCanExecuteChanged();
        checkVisibleCommand.RaiseCanExecuteChanged();
        uncheckVisibleCommand.RaiseCanExecuteChanged();
        clearSearchCommand.RaiseCanExecuteChanged();
        runSelectedCommand.RaiseCanExecuteChanged();
        runAllCommand.RaiseCanExecuteChanged();
        runCheckedCommand.RaiseCanExecuteChanged();
        runRemainingCommand.RaiseCanExecuteChanged();
        stopCommand.RaiseCanExecuteChanged();
        openRunRecordCommand.RaiseCanExecuteChanged();
        openPreviousRunRecordCommand.RaiseCanExecuteChanged();
        exportCsvCommand.RaiseCanExecuteChanged();
        returnToTeachingCommand.RaiseCanExecuteChanged();
        retryRecordSaveCommand.RaiseCanExecuteChanged();
        newInspectionContextCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0) return;
        Interlocked.Exchange(ref cancelRequested, 1);
        if (IsRunning) cancelCurrentRun();
        foreach (var item in samples) item.PropertyChanged -= OnSamplePropertyChanged;
        samples.Clear();
        visibleSamples.Clear();
        selectedSample = null;
        pendingRecordSample = null;
        pendingRecordDocument = null;
        recipeContext = null;
    }

    private bool CanEditQueue => !IsDisposed && !IsRunning && !HasPendingRecordSave && Volatile.Read(ref recordSaveGate) == 0;
    private bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    private void StartRun(InspectionQueueRunScope scope)
    {
        if (IsDisposed) return;
        if (HasPendingRecordSave || Volatile.Read(ref recordSaveGate) != 0 || !IsRecipeContextCurrent())
        {
            SummaryText = RecipeReadinessText;
            RefreshCommands();
            return;
        }
        if (IsDisposed || Interlocked.CompareExchange(ref isRunning, 1, 0) != 0) return;

        ToolRecipeDocument document;
        string recipePath;
        try
        {
            document = createDocument();
            recipePath = getRecipePath() is { } path && File.Exists(path)
                ? Path.GetFullPath(path)
                : string.Empty;
            var readiness = getRecipeReadiness(document);
            if (!readiness.CanRun || string.IsNullOrWhiteSpace(recipePath))
            {
                SummaryText = string.IsNullOrWhiteSpace(recipePath)
                    ? Localize("저장된 레시피를 먼저 여세요.", "Open a saved recipe first.")
                    : readiness.Message;
                Interlocked.Exchange(ref isRunning, 0);
                RefreshCommands();
                return;
            }
            recipeContext ??= ReadRecipeContext(recipePath);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            SummaryText = exception.Message;
            Interlocked.Exchange(ref isRunning, 0);
            RefreshCommands();
            return;
        }

        var targets = GetTargets(scope);
        if (targets.Count == 0)
        {
            SetLocalizedSummary("실행할 파일을 선택하세요.", "Select files to run.");
            Interlocked.Exchange(ref isRunning, 0);
            RefreshCommands();
            return;
        }

        Interlocked.Exchange(ref cancelRequested, 0);
        totalRunTargetCount = targets.Count;
        completedRunTargetCount = 0;
        progressText = Localize($"0 / {totalRunTargetCount} 완료", $"0 / {totalRunTargetCount} completed");
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressText));
        foreach (var item in targets) item.BeginRun();
        SetLocalizedSummary(
            $"저장된 레시피 {Path.GetFileName(recipePath)}을(를) 입력 {targets.Count}개에 순서대로 실행합니다.",
            $"Running saved recipe {Path.GetFileName(recipePath)} sequentially on {targets.Count} input(s).");
        RefreshCounts();
        RefreshCommands();
        _ = ObserveRunAsync(RunTargetsAsync(document, recipePath, targets));
    }

    private async Task RunTargetsAsync(
        ToolRecipeDocument document,
        string recipePath,
        IReadOnlyList<InspectionQueueSampleItem> targets)
    {
        var stopped = false;
        try
        {
            foreach (var item in targets)
            {
                if (IsDisposed || Volatile.Read(ref cancelRequested) != 0)
                {
                    stopped = true;
                    break;
                }

                if (!IsRecipeContextCurrent())
                {
                    SetLocalizedSummary("저장된 레시피가 바뀌어 검사를 중단했습니다. 이전 결과를 보존했습니다. 새 검사 목록을 준비하세요.", "Inspection stopped because the saved recipe changed. Previous results were retained. Prepare a new inspection queue.");
                    return;
                }

                var input = validateInput(document, item.SourcePath);
                if (!input.IsCompatible)
                {
                    item.SetError(input.Message);
                    completedRunTargetCount++;
                    UpdateProgress();
                    continue;
                }

                item.SetRunning();
                SelectedSample = item;
                try
                {
                    var outcome = await runInput(document, recipePath, item.SourcePath);
                    if (IsDisposed) return;
                    if (outcome.RecordSaveFailed)
                    {
                        pendingRecordSample = item;
                        pendingRecordDocument = document;
                        item.SetPendingRecord(outcome.Record, outcome.Message, document);
                        SummaryText = Localize($"검사 중단 · 계산 결과를 보존했습니다. 기록 저장 재시도가 필요합니다. {outcome.Message}", $"Inspection stopped · calculation retained. Retry saving its Run Record. {outcome.Message}");
                        SelectedSampleChanged?.Invoke(this, new InspectionQueueSelectionChangedEventArgs(item));
                        return;
                    }
                    if (outcome.WasCancelled || (Volatile.Read(ref cancelRequested) != 0 && outcome.Record is null))
                    {
                        item.SetCancelled(outcome.Message);
                        stopped = true;
                        break;
                    }

                    if (outcome.Record is null || string.IsNullOrWhiteSpace(outcome.RecordPath))
                    {
                        item.SetError(string.IsNullOrWhiteSpace(outcome.Message)
                            ? Localize("실행 기록을 저장하지 못했습니다.", "The Run Record could not be saved.")
                            : outcome.Message);
                    }
                    else
                    {
                        item.SetRecord(outcome.Record, outcome.RecordPath, document);
                    }

                    if (ReferenceEquals(SelectedSample, item))
                    {
                        SelectedSampleChanged?.Invoke(this, new InspectionQueueSelectionChangedEventArgs(item));
                    }
                }
                catch (OperationCanceledException)
                {
                    item.SetCancelled(Localize("사용자가 현재 입력을 중지했습니다.", "The operator stopped the current input."));
                    stopped = true;
                    break;
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException
                    or InvalidDataException
                    or ArgumentException
                    or InvalidOperationException
                    or NotSupportedException
                    or OverflowException)
                {
                    item.SetError(exception.Message);
                }

                completedRunTargetCount++;
                UpdateProgress();
                RefreshCounts();
            }

            if (stopped)
            {
                SetLocalizedSummary(
                    "중지됨 · 완료 결과는 유지하고 현재 파일은 취소, 나머지는 미검사로 남겼습니다.",
                    "Stopped · completed results were kept, the current file was cancelled, and remaining files stay untested.");
            }
            else
            {
                SetLocalizedSummary(
                    $"검사 완료 · 통과 {PassCount} · NG {FailCount} · 오류 {ErrorCount}",
                    $"Inspection complete · Pass {PassCount} · NG {FailCount} · Error {ErrorCount}");
            }
        }
        finally
        {
            if (!IsDisposed)
            {
                Interlocked.Exchange(ref isRunning, 0);
                Interlocked.Exchange(ref cancelRequested, 0);
                if (stopped)
                {
                    foreach (var item in targets.Where(item => item.State == InspectionQueueItemState.Running))
                    {
                        item.SetCancelled(Localize("사용자가 현재 입력을 중지했습니다.", "The operator stopped the current input."));
                    }
                }
                RefreshCounts();
                RefreshCommands();
            }
        }
    }

    private async Task ObserveRunAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception)
        {
            if (!IsDisposed) SummaryText = exception.Message;
        }
    }

    private void Stop()
    {
        if (!IsRunning) return;
        Interlocked.Exchange(ref cancelRequested, 1);
        SetLocalizedSummary("현재 입력을 중지하는 중입니다…", "Stopping the current input…");
        cancelCurrentRun();
        RefreshCommands();
    }

    private IReadOnlyList<InspectionQueueSampleItem> GetTargets(InspectionQueueRunScope scope) => scope switch
    {
        InspectionQueueRunScope.Selected => SelectedSample is { } selected ? [selected] : [],
        InspectionQueueRunScope.All => samples.ToArray(),
        InspectionQueueRunScope.Checked => samples.Where(item => item.IsChecked).ToArray(),
        _ => samples.Where(item => item.State is InspectionQueueItemState.Untested
            or InspectionQueueItemState.Cancelled
            or InspectionQueueItemState.Error).ToArray()
    };

    private bool CanRunSavedRecipe()
    {
        try
        {
            var recipePath = getRecipePath();
            if (string.IsNullOrWhiteSpace(recipePath) || !File.Exists(recipePath)) return false;
            return getRecipeReadiness(createDocument()).CanRun;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or ArgumentException
            or IOException
            or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool CanRun(InspectionQueueRunScope scope) =>
        CanEditQueue && IsRecipeContextCurrent() && CanRunSavedRecipe() && GetTargets(scope).Count > 0;

    private static (string Path, string Sha256) ReadRecipeContext(string path)
    {
        var fullPath = Path.GetFullPath(path);
        using var stream = File.OpenRead(fullPath);
        return (fullPath, Convert.ToHexString(SHA256.HashData(stream)));
    }

    private bool IsRecipeContextCurrent()
    {
        if (recipeContext is not { } context) return true;
        try
        {
            if (getRecipePath() is not { Length: > 0 } path) return false;
            var current = ReadRecipeContext(path);
            return string.Equals(current.Path, context.Path, StringComparison.OrdinalIgnoreCase) && current.Sha256 == context.Sha256;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private void RetryRecordSave()
    {
        if (IsDisposed || IsRunning || pendingRecordSample is not { } item || retryRecordSave is null || Interlocked.CompareExchange(ref recordSaveGate, 1, 0) != 0) return;
        try
        {
            var outcome = retryRecordSave();
            if (outcome.RecordSaveFailed || outcome.Record is null || outcome.RecordPath is null)
            {
                item.SetPendingRecord(outcome.Record, outcome.Message, pendingRecordDocument);
                SummaryText = outcome.Message;
                return;
            }
            item.SetRecord(outcome.Record, outcome.RecordPath, pendingRecordDocument);
            pendingRecordSample = null;
            pendingRecordDocument = null;
            completedRunTargetCount++;
            UpdateProgress();
            SetLocalizedSummary("기록을 저장했습니다. 남은 파일은 직접 실행하세요.", "Run Record saved. Explicitly inspect remaining files to continue.");
            SelectedSampleChanged?.Invoke(this, new InspectionQueueSelectionChangedEventArgs(SelectedSample));
            RefreshCounts();
        }
        finally
        {
            Volatile.Write(ref recordSaveGate, 0);
            if (!IsDisposed) RefreshCommands();
        }
    }

    private void StartNewInspectionContext()
    {
        if (!CanEditQueue || recipeContext is null) return;
        foreach (var item in samples) item.BeginRun();
        recipeContext = null;
        totalRunTargetCount = 0;
        completedRunTargetCount = 0;
        UpdateProgress();
        SetLocalizedSummary("새 검사 목록을 준비했습니다. 이전 기록은 보존되며 검사는 실행을 기다립니다.", "A new inspection queue is ready. Previous records are retained; inspection waits for explicit Run.");
        SelectedSampleChanged?.Invoke(this, new InspectionQueueSelectionChangedEventArgs(SelectedSample));
        RefreshCounts();
        RefreshCommands();
    }

    private void RemoveSelected()
    {
        if (SelectedSample is not { } selected || !CanEditQueue) return;
        var index = samples.IndexOf(selected);
        if (index < 0) return;
        selected.PropertyChanged -= OnSamplePropertyChanged;
        samples.RemoveAt(index);
        ReindexSamples();
        SelectedSample = samples.ElementAtOrDefault(Math.Min(index, samples.Count - 1));
        RefreshCounts();
        RefreshProjection();
        RefreshCommands();
    }

    private void ClearQueue()
    {
        if (!CanEditQueue) return;
        foreach (var item in samples) item.PropertyChanged -= OnSamplePropertyChanged;
        samples.Clear();
        recipeContext = null;
        SelectedSample = null;
        RefreshCounts();
        RefreshProjection();
        SetLocalizedSummary("검사 목록을 비웠습니다. 원본 파일은 삭제하지 않았습니다.", "The inspection queue was cleared. Source files were not deleted.");
        RefreshCommands();
    }

    private void SetVisibleChecked(bool isChecked)
    {
        if (!CanEditQueue) return;
        foreach (var item in visibleSamples) item.IsChecked = isChecked;
        RefreshCounts();
    }

    private void OnSamplePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (ReferenceEquals(sender, SelectedSample) && args.PropertyName == nameof(InspectionQueueSampleItem.Steps))
        {
            SelectedStepResult = null;
            OnPropertyChanged(nameof(VisibleStepResults));
        }
        if (args.PropertyName == nameof(InspectionQueueSampleItem.IsChecked))
        {
            RefreshCounts();
            RefreshCommands();
        }
        else if (args.PropertyName == nameof(InspectionQueueSampleItem.State))
        {
            RefreshCounts();
            RefreshProjection();
            RefreshCommands();
        }
    }

    private void RefreshCounts()
    {
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(RunCheckedLabel));
        OnPropertyChanged(nameof(PassCount));
        OnPropertyChanged(nameof(FailCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(CancelledCount));
        OnPropertyChanged(nameof(UntestedCount));
        OnPropertyChanged(nameof(CountsText));
        OnPropertyChanged(nameof(HasSamples));
    }

    private int Count(InspectionQueueItemState state) => samples.Count(item => item.State == state);

    private void RefreshProjection()
    {
        var selectedPath = SelectedSample?.SourcePath;
        visibleSamples.Clear();
        foreach (var item in samples.Where(MatchesFilter)) visibleSamples.Add(item);
        if (selectedPath is not null
            && !visibleSamples.Any(item => string.Equals(item.SourcePath, selectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedSample = visibleSamples.FirstOrDefault();
        }
        OnPropertyChanged(nameof(VisibleSamples));
        OnPropertyChanged(nameof(HasVisibleSamples));
        checkVisibleCommand.RaiseCanExecuteChanged();
        uncheckVisibleCommand.RaiseCanExecuteChanged();
    }

    private bool MatchesFilter(InspectionQueueSampleItem item)
    {
        var matchesStatus = statusFilter == InspectionQueueStatusFilter.All
            || (statusFilter, item.State) switch
            {
                (InspectionQueueStatusFilter.Untested, InspectionQueueItemState.Untested) => true,
                (InspectionQueueStatusFilter.Pass, InspectionQueueItemState.Pass) => true,
                (InspectionQueueStatusFilter.Fail, InspectionQueueItemState.Fail) => true,
                (InspectionQueueStatusFilter.Warning, InspectionQueueItemState.Warning) => true,
                (InspectionQueueStatusFilter.Error, InspectionQueueItemState.Error) => true,
                (InspectionQueueStatusFilter.Cancelled, InspectionQueueItemState.Cancelled) => true,
                _ => false
            };
        return matchesStatus
            && (string.IsNullOrWhiteSpace(searchText)
                || item.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || item.SourcePath.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private void ReindexSamples()
    {
        for (var index = 0; index < samples.Count; index++) samples[index].Order = index + 1;
        OnPropertyChanged(nameof(AllCount));
    }

    private void UpdateProgress()
    {
        progressText = Localize(
            $"{completedRunTargetCount} / {totalRunTargetCount} 완료",
            $"{completedRunTargetCount} / {totalRunTargetCount} completed");
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressText));
    }

    private void SetLocalizedSummary(string korean, string english)
    {
        localizedSummary = (korean, english);
        OnPropertyChanged(nameof(SummaryText));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class InspectionQueueSampleItem : INotifyPropertyChanged
{
    private bool isChecked = true;
    private bool isSelected;
    private int order;
    private InspectionQueueItemState state = InspectionQueueItemState.Untested;
    private InspectionRunRecord? record;
    private string? runRecordPath;
    private string? previousRunRecordPath;
    private string message;
    private readonly Func<string, string, string> localize;
    private IReadOnlyList<InspectionQueueStepResultItem> steps = [];

    public InspectionQueueSampleItem(int order, string sourcePath, Func<string, string, string> localize)
    {
        this.order = order;
        SourcePath = sourcePath;
        this.localize = localize;
        message = localize("실행을 기다립니다.", "Waiting for explicit Run.");
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public int Order
    {
        get => order;
        set
        {
            if (order == value) return;
            order = value;
            OnPropertyChanged();
        }
    }
    public string SourcePath { get; }
    public string FileName => Path.GetFileName(SourcePath);
    public bool IsChecked
    {
        get => isChecked;
        set
        {
            if (isChecked == value) return;
            isChecked = value;
            OnPropertyChanged();
        }
    }
    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value) return;
            isSelected = value;
            OnPropertyChanged();
        }
    }
    public InspectionQueueItemState State
    {
        get => state;
        private set
        {
            if (state == value) return;
            state = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StateText));
            OnPropertyChanged(nameof(StatusSummary));
        }
    }
    public string Message
    {
        get => message;
        private set
        {
            if (message == value) return;
            message = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusSummary));
        }
    }
    public string? RunRecordPath
    {
        get => runRecordPath;
        private set
        {
            if (runRecordPath == value) return;
            runRecordPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpenCurrentRunRecord));
        }
    }
    public string? PreviousRunRecordPath
    {
        get => previousRunRecordPath;
        private set
        {
            if (previousRunRecordPath == value) return;
            previousRunRecordPath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpenPreviousRunRecord));
        }
    }
    public InspectionRunRecord? Record
    {
        get => record;
        private set
        {
            if (ReferenceEquals(record, value)) return;
            record = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RunId));
            OnPropertyChanged(nameof(RecordedAtText));
            OnPropertyChanged(nameof(RecipeRevision));
            OnPropertyChanged(nameof(InputSha256));
        }
    }
    public IReadOnlyList<InspectionQueueStepResultItem> Steps
    {
        get => steps;
        private set
        {
            steps = value;
            OnPropertyChanged();
        }
    }
    public string RunId => Record?.RunId ?? string.Empty;
    public string StateText => state switch
    {
        InspectionQueueItemState.Untested => localize("미검사", "Untested"),
        InspectionQueueItemState.Running => localize("검사 중", "Running"),
        InspectionQueueItemState.Pass => localize("통과", "Pass"),
        InspectionQueueItemState.Fail => localize("NG", "Fail"),
        InspectionQueueItemState.Warning => localize("주의", "Warning"),
        InspectionQueueItemState.Error => localize("오류", "Error"),
        InspectionQueueItemState.Cancelled => localize("취소", "Cancelled"),
        _ => state.ToString()
    };
    public string StatusSummary => string.IsNullOrWhiteSpace(Message) ? StateText
        : Record is not null && Steps.Count > 0 && Message == $"Executed {Steps.Count} authored typed step(s) in recipe order."
            ? $"{StateText} · {localize($"{Steps.Count}단계 검사 완료", Steps.Count == 1 ? "1 step inspected" : $"{Steps.Count} steps inspected")}"
            : $"{StateText} · {Message}";
    public string RecordedAtText => Record?.RecordedAtUtc.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty;
    public string RecipeRevision => Record?.Recipe.Version ?? string.Empty;
    public string InputSha256 => Record?.Source.Sha256 ?? string.Empty;
    public string RecipeSha256 => Record?.Recipe.Sha256 ?? string.Empty;
    public bool CanOpenCurrentRunRecord => !string.IsNullOrWhiteSpace(RunRecordPath) && File.Exists(RunRecordPath);
    public bool CanOpenPreviousRunRecord => !string.IsNullOrWhiteSpace(PreviousRunRecordPath) && File.Exists(PreviousRunRecordPath);

    internal void RefreshResultPresentation()
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(StatusSummary));
    }

    public void BeginRun()
    {
        if (RunRecordPath is not null) PreviousRunRecordPath = RunRecordPath;
        RunRecordPath = null;
        Record = null;
        Steps = [];
        Message = string.Empty;
        State = InspectionQueueItemState.Untested;
    }

    public void SetRunning()
    {
        State = InspectionQueueItemState.Running;
        Message = string.Empty;
    }

    public void SetRecord(InspectionRunRecord value, string path, ToolRecipeDocument? document = null)
    {
        Record = value;
        RunRecordPath = Path.GetFullPath(path);
        Steps = (value.Steps ?? []).Select(step => InspectionQueueStepResultItem.FromRecord(
            step, document?.Steps.FirstOrDefault(authored => authored.Id == step.Id))).ToArray();
        Message = value.Message;
        State = value.Status switch
        {
            ResultStatus.Pass => InspectionQueueItemState.Pass,
            ResultStatus.Fail => InspectionQueueItemState.Fail,
            ResultStatus.Warning => InspectionQueueItemState.Warning,
            _ => InspectionQueueItemState.Error
        };
    }

    public void SetError(string reason)
    {
        Record = null;
        RunRecordPath = null;
        Steps = [];
        Message = reason;
        State = InspectionQueueItemState.Error;
    }

    public void SetPendingRecord(InspectionRunRecord? value, string reason, ToolRecipeDocument? document)
    {
        Record = value;
        RunRecordPath = null;
        Steps = (value?.Steps ?? []).Select(step => InspectionQueueStepResultItem.FromRecord(step, document?.Steps.FirstOrDefault(authored => authored.Id == step.Id))).ToArray();
        Message = reason;
        State = InspectionQueueItemState.Error;
    }

    public void SetCancelled(string reason)
    {
        Record = null;
        RunRecordPath = null;
        Steps = [];
        Message = reason;
        State = InspectionQueueItemState.Cancelled;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed record InspectionQueueStepResultItem(
    int Order,
    string StepId,
    string ToolName,
    string OutputEntityId,
    string Status,
    string Message,
    string Metrics,
    string Overlays,
    string OutputSha256,
    double DurationMilliseconds)
{
    private ToolResult? PresentationResult { get; init; }
    private string ToolId { get; init; } = string.Empty;
    private IReadOnlyList<ToolRecipeParameter> ExecutionParameters { get; init; } = [];

    public int DisplayOrder => Order + 1;
    public string Value { get; init; } = "—";
    public string Unit { get; init; } = string.Empty;
    public string StatusLabel => PresentationResult is null ? Status : ToolWorkbenchViewModel.MeasurementResultStatusLabel(PresentationResult);
    public string DisplayMessage => PresentationResult is null ? Message : ToolWorkbenchViewModel.MeasurementResultMessage(PresentationResult);
    public string Limits => PresentationResult is null ? "—" : ToolWorkbenchViewModel.MeasurementLimitSummary(ToolId, PresentationResult, ExecutionParameters);

    public static InspectionQueueStepResultItem FromRecord(InspectionRunStepResult step, ToolRecipeStep? authoredStep = null)
    {
        var metrics = step.Metrics.Select(metric => new Metric(metric.Name, metric.Kind, metric.Value, metric.Unit, metric.Status)).ToArray();
        var result = new ToolResult(step.ToolName, step.Status, step.Message, TimeSpan.Zero, metrics, []);
        var primary = ToolWorkbenchViewModel.SelectPrimaryMeasurementMetric(step.ToolId, metrics);
        return new(
            step.RecipeIndex,
            step.Id,
            authoredStep?.ToolName ?? step.ToolName,
            step.OutputEntityId,
            step.Status.ToString(),
            step.Message,
            string.Join(" · ", step.Metrics.Select(metric =>
                $"{metric.Name}: {metric.Value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)} {metric.Unit}".TrimEnd())),
            string.Join(" · ", step.Overlays.Select(overlay => overlay.Label)),
            step.OutputContentSha256 ?? string.Empty,
            step.ElapsedMilliseconds)
        {
            PresentationResult = result,
            ToolId = step.ToolId,
            ExecutionParameters = authoredStep?.Parameters.ToArray() ?? [],
            Value = primary is not null && double.IsFinite(primary.Value)
                ? $"{primary.Name}{Environment.NewLine}{primary.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}" : "—",
            Unit = primary?.Unit ?? string.Empty
        };
    }
}

public sealed class InspectionQueueRunRecordRequestEventArgs(string path) : EventArgs
{
    public string Path { get; } = path;
}

public sealed class InspectionQueueSelectionChangedEventArgs(InspectionQueueSampleItem? sample) : EventArgs
{
    public InspectionQueueSampleItem? Sample { get; } = sample;
}

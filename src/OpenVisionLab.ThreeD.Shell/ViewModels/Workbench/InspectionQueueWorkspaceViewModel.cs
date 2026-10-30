using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

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
    Remaining,
    FailedAndError
}

public sealed record InspectionQueueHeightMapSnapshot(string EntityId, string ToolId, string Path, string ContentSha256,
    string RootSourceSha256, string Unit, string FrameId, int Width, int Height, int OriginColumn, int OriginRow);

public sealed record InspectionQueueRunOutcome(
    bool WasCancelled,
    InspectionRunRecord? Record,
    string? RecordPath,
    string Message,
    bool RecordSaveFailed = false)
{
    public IReadOnlyDictionary<string, HeightMeasurementColorSnapshot> MeasurementColors { get; init; } = new Dictionary<string, HeightMeasurementColorSnapshot>();
    public IReadOnlyDictionary<string, InspectionQueueHeightMapSnapshot> HeightMaps { get; init; } = new Dictionary<string, InspectionQueueHeightMapSnapshot>();
}

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
    private readonly RelayCommand setStatusFilterCommand;
    private readonly RelayCommand setVisibleCheckedCommand;
    private readonly RelayCommand runSelectedCommand;
    private readonly RelayCommand runAllCommand;
    private readonly RelayCommand runCheckedCommand;
    private readonly RelayCommand runRemainingCommand;
    private readonly RelayCommand runScopeCommand;
    private InspectionQueueRunScope selectedRunScope = InspectionQueueRunScope.Checked;
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
    private InspectionQueueOutputResultItem? selectedOutputResult;
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
        setStatusFilterCommand = new RelayCommand(parameter => SetStatusFilter(parameter as string), _ => !IsDisposed);
        setVisibleCheckedCommand = new RelayCommand(parameter => SetVisibleChecked(parameter is true), _ => CanEditQueue && visibleSamples.Count > 0);
        runSelectedCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.Selected), _ => CanRun(InspectionQueueRunScope.Selected));
        runAllCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.All), _ => CanRun(InspectionQueueRunScope.All));
        runCheckedCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.Checked), _ => CanRun(InspectionQueueRunScope.Checked));
        runRemainingCommand = new RelayCommand(_ => StartRun(InspectionQueueRunScope.Remaining), _ => CanRun(InspectionQueueRunScope.Remaining));
        runScopeCommand = new RelayCommand(_ => StartRun(SelectedRunScope), _ => CanRun(SelectedRunScope));
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
    public ICommand SetStatusFilterCommand => setStatusFilterCommand;
    public ICommand SetVisibleCheckedCommand => setVisibleCheckedCommand;
    public ICommand RunSelectedCommand => runSelectedCommand;
    public ICommand RunAllCommand => runAllCommand;
    public ICommand RunCheckedCommand => runCheckedCommand;
    public ICommand RunRemainingCommand => runRemainingCommand;
    public ICommand RunScopeCommand => runScopeCommand;
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
    public bool AreVisibleSamplesChecked => visibleSamples.Count > 0 && visibleSamples.All(item => item.IsChecked);
    public string EmptyQueueText => HasSamples
        ? Localize("검색·상태 필터에 맞는 파일이 없습니다.", "No files match the search and status filter.")
        : Localize("검사할 파일이나 폴더를 추가하세요.", "Add files or a folder to inspect.");
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
            OnPropertyChanged(nameof(VisibleOutputResults));
            OnPropertyChanged(nameof(SelectedResultNotice));
        }
    }
    public IReadOnlyList<InspectionQueueStepResultItem> VisibleStepResults =>
        (SelectedSample?.Steps ?? []).Where(item => !ShowOnlyFailedSteps || item.Status == nameof(ResultStatus.Fail)).ToArray();
    public IReadOnlyList<InspectionQueueOutputResultItem> VisibleOutputResults => VisibleStepResults.SelectMany(step => step.Outputs).ToArray();
    public string SelectedResultNotice => SelectedSample is not { } sample
        ? string.Empty
        : sample.Steps.Count == 0
            ? sample.State == InspectionQueueItemState.Untested
                ? Localize("미검사", "Untested")
                : $"{sample.StateText} · {sample.Message}"
            : ShowOnlyFailedSteps && VisibleStepResults.Count == 0
                ? Localize("선택 파일에 NG 단계가 없습니다.", "The selected file has no failed steps.") : string.Empty;
    public IReadOnlyList<InspectionQueueMeasurementSummary> MeasurementSummaries
    {
        get
        {
            var groups = samples.Where(sample => sample.Record is not null)
                .SelectMany(sample => (sample.Record!.Steps ?? []).SelectMany(step => step.Metrics
                    .Where(metric => metric.Status.HasValue)
                    .Select(metric => (step, metric, point: InspectionQueueMeasurementPoint.FromRecord(sample, step, metric),
                        settings: step.SemanticFingerprint ?? sample.Record.Recipe.Sha256))))
                .GroupBy(item => (item.step.Id, item.metric.Name, item.metric.Kind, item.metric.Unit, item.settings)).ToArray();
            return groups.Select(group =>
            {
                var values = group.Where(item => item.step.Status != ResultStatus.Error && double.IsFinite(item.metric.Value))
                    .Select(item => item.metric.Value).ToArray();
                return new InspectionQueueMeasurementSummary(group.First().step.ToolName, group.Key.Name, group.Key.Unit,
                    group.Count(item => item.step.Status != ResultStatus.Error && item.metric.Status == ResultStatus.Pass),
                    group.Count(item => item.step.Status != ResultStatus.Error && item.metric.Status == ResultStatus.Fail),
                    group.Count(item => item.step.Status == ResultStatus.Error), values.Length == 0 ? null : values.Min(),
                    values.Length == 0 ? null : values.Sum(value => value / values.Length), values.Length == 0 ? null : values.Max())
                {
                    StepId = group.Key.Id, Kind = group.Key.Kind, SettingsIdentity = group.Key.settings,
                    HasMultipleSettings = groups.Count(other => other.Key.Id == group.Key.Id && other.Key.Name == group.Key.Name
                        && other.Key.Kind == group.Key.Kind && other.Key.Unit == group.Key.Unit) > 1,
                    Points = Array.AsReadOnly(group.Select(item => item.point).ToArray())
                };
            }).ToArray();
        }
    }
    public bool IsStatusFilterAll => statusFilter == InspectionQueueStatusFilter.All;
    public bool IsStatusFilterUntested => statusFilter == InspectionQueueStatusFilter.Untested;
    public bool IsStatusFilterPass => statusFilter == InspectionQueueStatusFilter.Pass;
    public bool IsStatusFilterFail => statusFilter == InspectionQueueStatusFilter.Fail;
    public bool IsStatusFilterWarning => statusFilter == InspectionQueueStatusFilter.Warning;
    public bool IsStatusFilterError => statusFilter == InspectionQueueStatusFilter.Error;
    public bool IsStatusFilterCancelled => statusFilter == InspectionQueueStatusFilter.Cancelled;
    public int AllCount => samples.Count;
    public int CheckedCount => samples.Count(item => item.IsChecked);
    public InspectionQueueRunScope SelectedRunScope
    {
        get => selectedRunScope;
        set
        {
            if (!Enum.IsDefined(value) || IsRunning || selectedRunScope == value) return;
            selectedRunScope = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RunScopeTargetCount));
            OnPropertyChanged(nameof(RunScopeLabel));
            runScopeCommand.RaiseCanExecuteChanged();
        }
    }
    public int RunScopeTargetCount => GetTargets(SelectedRunScope).Count;
    public string RunScopeLabel => Localize($"{SelectedRunScope switch { InspectionQueueRunScope.Selected => "선택", InspectionQueueRunScope.All => "전체", InspectionQueueRunScope.Checked => "체크", InspectionQueueRunScope.Remaining => "남은 파일", _ => "NG·오류만" }} {RunScopeTargetCount}개 검사",
        $"Inspect {RunScopeTargetCount} {SelectedRunScope switch { InspectionQueueRunScope.Selected => "selected", InspectionQueueRunScope.All => "all", InspectionQueueRunScope.Checked => "checked", InspectionQueueRunScope.Remaining => "remaining", _ => "NG/error" }}");
    public int RemainingCount => GetTargets(InspectionQueueRunScope.Remaining).Count;
    public string RunRemainingLabel => Localize($"남은 파일 {RemainingCount}", $"Remaining files {RemainingCount}");
    public string InspectionBasisText => recipeContext is { } context ? Path.GetFileName(context.Path) : SavedRecipeText;
    public string RunCheckedLabel => Localize($"체크한 {CheckedCount}개 검사", $"Inspect {CheckedCount} checked");
    public int PassCount => Count(InspectionQueueItemState.Pass);
    public int FailCount => Count(InspectionQueueItemState.Fail);
    public int WarningCount => Count(InspectionQueueItemState.Warning);
    public int ErrorCount => Count(InspectionQueueItemState.Error);
    public int CancelledCount => Count(InspectionQueueItemState.Cancelled);
    public int UntestedCount => Count(InspectionQueueItemState.Untested);
    public int RunningCount => Count(InspectionQueueItemState.Running);
    public int ProcessedCount => PassCount + FailCount + WarningCount + ErrorCount;
    public string ProcessedCountText => Localize($"검사 완료 {ProcessedCount} / {AllCount}", $"Inspected {ProcessedCount} / {AllCount}");
    public string PassRateText => ProcessedCount == 0 ? "—%" : (100d * PassCount / ProcessedCount).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    public string ProcessingSummaryText => Localize($"처리 {ProcessedCount}/{AllCount} · 체크 {CheckedCount}", $"Processed {ProcessedCount}/{AllCount} · checked {CheckedCount}");
    public string CountsText => Localize(
        $"전체 {AllCount} · 체크 {CheckedCount} · 통과 {PassCount} · NG {FailCount} · 주의 {WarningCount} · 오류 {ErrorCount} · 취소 {CancelledCount} · 미검사 {UntestedCount} · 검사 중 {RunningCount}",
        $"All {AllCount} · checked {CheckedCount} · pass {PassCount} · fail {FailCount} · warning {WarningCount} · error {ErrorCount} · cancelled {CancelledCount} · untested {UntestedCount} · running {RunningCount}");
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
            SelectedOutputResult = null;
            if (selectedSample is not null) selectedSample.IsSelected = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedSample));
            OnPropertyChanged(nameof(VisibleStepResults));
            OnPropertyChanged(nameof(VisibleOutputResults));
            OnPropertyChanged(nameof(SelectedResultNotice));
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
            if (!ReferenceEquals(selectedOutputResult?.Step, value))
            {
                selectedOutputResult = value?.Outputs.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedOutputResult));
            }
            OnPropertyChanged();
        }
    }

    public InspectionQueueOutputResultItem? SelectedOutputResult
    {
        get => selectedOutputResult;
        set
        {
            if (ReferenceEquals(selectedOutputResult, value)) return;
            if (value is not null && SelectedSample?.Steps.Any(step => step.Outputs.Contains(value)) != true) return;
            selectedOutputResult = value;
            if (value is not null) SelectedStepResult = value.Step;
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
        OnPropertyChanged(nameof(InspectionBasisText));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(VisibleStepResults));
        OnPropertyChanged(nameof(VisibleOutputResults));
        OnPropertyChanged(nameof(SelectedResultNotice));
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
        OnPropertyChanged(nameof(InspectionBasisText));
        OnPropertyChanged(nameof(RunCheckedLabel));
        OnPropertyChanged(nameof(RunScopeTargetCount));
        OnPropertyChanged(nameof(RunScopeLabel));
        OnPropertyChanged(nameof(VisibleStepResults));
        OnPropertyChanged(nameof(VisibleOutputResults));
        OnPropertyChanged(nameof(SelectedResultNotice));
        addFilesCommand.RaiseCanExecuteChanged();
        addFolderCommand.RaiseCanExecuteChanged();
        removeSelectedCommand.RaiseCanExecuteChanged();
        selectSampleCommand.RaiseCanExecuteChanged();
        clearCommand.RaiseCanExecuteChanged();
        checkVisibleCommand.RaiseCanExecuteChanged();
        uncheckVisibleCommand.RaiseCanExecuteChanged();
        clearSearchCommand.RaiseCanExecuteChanged();
        setStatusFilterCommand.RaiseCanExecuteChanged();
        setVisibleCheckedCommand.RaiseCanExecuteChanged();
        runSelectedCommand.RaiseCanExecuteChanged();
        runAllCommand.RaiseCanExecuteChanged();
        runCheckedCommand.RaiseCanExecuteChanged();
        runRemainingCommand.RaiseCanExecuteChanged();
        runScopeCommand.RaiseCanExecuteChanged();
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
            $"파일 {targets.Count}개 검사 중",
            $"Inspecting {targets.Count} files");
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

                try
                {
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
                    var outcome = await runInput(document, recipePath, item.SourcePath);
                    if (IsDisposed) return;
                    if (outcome.RecordSaveFailed)
                    {
                        pendingRecordSample = item;
                        pendingRecordDocument = document;
                        item.SetPendingRecord(outcome.Record, outcome.Message, document, outcome.MeasurementColors, outcome.HeightMaps);
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
                        item.SetRecord(outcome.Record, outcome.RecordPath, document, outcome.MeasurementColors, outcome.HeightMaps);
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
        InspectionQueueRunScope.FailedAndError => samples.Where(item => item.State is InspectionQueueItemState.Fail or InspectionQueueItemState.Error).ToArray(),
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
                item.SetPendingRecord(outcome.Record, outcome.Message, pendingRecordDocument, outcome.MeasurementColors, outcome.HeightMaps);
                SummaryText = outcome.Message;
                return;
            }
            item.SetRecord(outcome.Record, outcome.RecordPath, pendingRecordDocument, outcome.MeasurementColors, outcome.HeightMaps);
            pendingRecordSample = null;
            pendingRecordDocument = null;
            completedRunTargetCount++;
            UpdateProgress();
            SetLocalizedSummary("기록을 저장했습니다. 남은 파일은 직접 실행하세요.", "Run Record saved. Explicitly inspect remaining files to continue.");
            SelectedSampleChanged?.Invoke(this, new InspectionQueueSelectionChangedEventArgs(SelectedSample));
            RefreshCounts();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException
            or ArgumentException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            item.SetPendingRecord(item.Record, exception.Message, pendingRecordDocument, item.MeasurementColors, item.HeightMaps);
            SummaryText = exception.Message;
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
        SetLocalizedSummary("새 검사 목록을 만들었습니다.", "New inspection list created.");
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
        if (args.PropertyName == nameof(InspectionQueueSampleItem.Steps)) OnPropertyChanged(nameof(MeasurementSummaries));
        if (ReferenceEquals(sender, SelectedSample) && args.PropertyName == nameof(InspectionQueueSampleItem.Steps))
        {
            SelectedStepResult = null;
            OnPropertyChanged(nameof(VisibleStepResults));
            OnPropertyChanged(nameof(VisibleOutputResults));
            OnPropertyChanged(nameof(SelectedResultNotice));
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
        OnPropertyChanged(nameof(RunScopeTargetCount));
        OnPropertyChanged(nameof(RunScopeLabel));
        OnPropertyChanged(nameof(RemainingCount));
        OnPropertyChanged(nameof(RunRemainingLabel));
        OnPropertyChanged(nameof(PassCount));
        OnPropertyChanged(nameof(FailCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(CancelledCount));
        OnPropertyChanged(nameof(UntestedCount));
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(ProcessedCountText));
        OnPropertyChanged(nameof(PassRateText));
        OnPropertyChanged(nameof(ProcessedCount));
        OnPropertyChanged(nameof(ProcessingSummaryText));
        OnPropertyChanged(nameof(AreVisibleSamplesChecked));
        OnPropertyChanged(nameof(EmptyQueueText));
        OnPropertyChanged(nameof(CountsText));
        OnPropertyChanged(nameof(HasSamples));
        OnPropertyChanged(nameof(MeasurementSummaries));
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
        OnPropertyChanged(nameof(AreVisibleSamplesChecked));
        OnPropertyChanged(nameof(EmptyQueueText));
        setVisibleCheckedCommand.RaiseCanExecuteChanged();
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
            if (value is null)
            {
                MeasurementColors = new Dictionary<string, HeightMeasurementColorSnapshot>();
                HeightMaps = new Dictionary<string, InspectionQueueHeightMapSnapshot>();
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(RunId));
            OnPropertyChanged(nameof(RecordedAtText));
            OnPropertyChanged(nameof(RecipeRevision));
            OnPropertyChanged(nameof(InputSha256));
            OnPropertyChanged(nameof(ElapsedMilliseconds));
            OnPropertyChanged(nameof(ElapsedText));
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
    public double? ElapsedMilliseconds => Record is { ElapsedMilliseconds: var elapsed } && double.IsFinite(elapsed) && elapsed >= 0 ? elapsed : null;
    public string ElapsedText => ElapsedMilliseconds is { } elapsed ? (elapsed / 1000).ToString("0.00", CultureInfo.InvariantCulture) + " s" : "—";
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
        : Record is null && Message is "실행을 기다립니다." or "Waiting for explicit Run."
            ? $"{StateText} · {localize("실행을 기다립니다.", "Waiting for explicit Run.")}"
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

    public IReadOnlyDictionary<string, HeightMeasurementColorSnapshot> MeasurementColors { get; private set; } = new Dictionary<string, HeightMeasurementColorSnapshot>();
    public IReadOnlyDictionary<string, InspectionQueueHeightMapSnapshot> HeightMaps { get; private set; } = new Dictionary<string, InspectionQueueHeightMapSnapshot>();

    public void SetRecord(InspectionRunRecord value, string path, ToolRecipeDocument? document = null,
        IReadOnlyDictionary<string, HeightMeasurementColorSnapshot>? measurementColors = null,
        IReadOnlyDictionary<string, InspectionQueueHeightMapSnapshot>? heightMaps = null)
    {
        Record = value;
        MeasurementColors = measurementColors ?? new Dictionary<string, HeightMeasurementColorSnapshot>();
        HeightMaps = heightMaps ?? new Dictionary<string, InspectionQueueHeightMapSnapshot>();
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

    public void SetPendingRecord(InspectionRunRecord? value, string reason, ToolRecipeDocument? document,
        IReadOnlyDictionary<string, HeightMeasurementColorSnapshot>? measurementColors = null,
        IReadOnlyDictionary<string, InspectionQueueHeightMapSnapshot>? heightMaps = null)
    {
        Record = value;
        MeasurementColors = measurementColors ?? new Dictionary<string, HeightMeasurementColorSnapshot>();
        HeightMaps = heightMaps ?? new Dictionary<string, InspectionQueueHeightMapSnapshot>();
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

public sealed record InspectionQueueMeasurementSummary(string ToolName, string MetricName, string Unit,
    int PassCount, int FailCount, int ErrorCount, double? Minimum, double? Mean, double? Maximum)
{
    public string StepId { get; init; } = string.Empty;
    public MetricKind Kind { get; init; }
    public string SettingsIdentity { get; init; } = string.Empty;
    public bool HasMultipleSettings { get; init; }
    public IReadOnlyList<InspectionQueueMeasurementPoint> Points { get; init; } = [];
    public string OutputLabel => string.IsNullOrEmpty(Unit) ? MetricName : $"{MetricName} ({Unit})";
    public string SettingsLabel => SettingsIdentity.Length > 10 ? SettingsIdentity[..10] : SettingsIdentity;
    private bool HasSharedRange => Points.Count > 0 && Points.Select(point => (point.LowerLimit, point.UpperLimit)).Distinct().Count() == 1;
    public double? LowerLimit => HasSharedRange ? Points[0].LowerLimit : null;
    public double? UpperLimit => HasSharedRange ? Points[0].UpperLimit : null;
    public string RangeText => InspectionQueueMeasurementPoint.FormatRange(LowerLimit, UpperLimit, Unit);
}

public sealed record InspectionQueueMeasurementPoint(int Order, string FileName, string SourcePath, string RunId,
    double? Value, ResultStatus Status, string Unit, double? LowerLimit, double? UpperLimit)
{
    public string ValueText => Value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "—";
    public string StatusLabel => ThreeDLocalization.Shared.ResultStatusLabel(Status);
    public string RangeText => FormatRange(LowerLimit, UpperLimit, Unit);

    internal static InspectionQueueMeasurementPoint FromRecord(InspectionQueueSampleItem sample, InspectionRunStepResult step, InspectionRunMetric metric)
    {
        var (lower, upper) = ReadRecordedRange(step, metric.Name, metric.Kind, metric.Unit);
        return new(sample.Order, sample.FileName, sample.SourcePath, sample.Record!.RunId,
            step.Status != ResultStatus.Error && double.IsFinite(metric.Value) ? metric.Value : null,
            step.Status == ResultStatus.Error ? ResultStatus.Error : metric.Status!.Value, metric.Unit, lower, upper);
    }

    internal static (double? Lower, double? Upper) ReadRecordedRange(InspectionRunStepResult step, string name, MetricKind kind, string unit)
    {
        var primary = ToolWorkbenchViewModel.SelectPrimaryMeasurementMetric(step.ToolId,
            step.Metrics.Select(item => new Metric(item.Name, item.Kind, item.Value, item.Unit, item.Status)).ToArray());
        double? Bound(string name)
        {
            var candidates = step.Metrics.Where(item => item.Name == name && item.Unit == unit && item.Kind == kind).ToArray();
            return candidates.Length > 0 && candidates.All(item => double.IsFinite(item.Value) && item.Value == candidates[0].Value) ? candidates[0].Value : null;
        }
        double? lower = null, upper = null;
        if (step.Message != ToolRecipeStep.UnconfiguredAcceptanceMessage)
        {
            if (primary?.Name == name && primary.Kind == kind && primary.Unit == unit
                && Bound("LowerLimit") is { } recordedLower && Bound("UpperLimit") is { } recordedUpper && recordedLower <= recordedUpper)
            {
                lower = recordedLower;
                upper = recordedUpper;
            }
            else
            {
                var upperName = (step.ToolId, name) switch
                {
                    ("warpage", "PeakToValley") => "MaximumPeakToValley",
                    ("warpage", "Rms") => "MaximumRms",
                    ("plane-flatness", "Flatness") => "MaximumFlatness",
                    ("datum-plane-raw-height-deviation", "PeakToValleyRawHeight") => "MaximumPeakToValleyRawHeight",
                    _ => null
                };
                if (upperName is not null) upper = Bound(upperName);
            }
        }
        return (lower, upper);
    }

    internal static string FormatRange(double? lower, double? upper, string unit, string format = "0.##") => lower is { } minimum && upper is { } maximum
        ? $"{minimum.ToString(format, CultureInfo.InvariantCulture)}–{maximum.ToString(format, CultureInfo.InvariantCulture)} {unit}".TrimEnd()
        : lower is { } oneLower ? $"≥ {oneLower.ToString(format, CultureInfo.InvariantCulture)} {unit}".TrimEnd()
        : upper is { } oneUpper ? $"≤ {oneUpper.ToString(format, CultureInfo.InvariantCulture)} {unit}".TrimEnd() : "—";
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
    internal bool IsUnjudged => Message == ToolRecipeStep.UnconfiguredAcceptanceMessage;
    public IReadOnlyList<InspectionQueueOutputResultItem> Outputs { get; private set; } = [];

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
        var item = new InspectionQueueStepResultItem(
            step.RecipeIndex,
            step.Id,
            authoredStep?.ToolName ?? step.ToolName,
            step.OutputEntityId,
            step.Status.ToString(),
            step.Message,
            string.Join(" · ", step.Metrics.Select(metric =>
                $"{metric.Name}: {metric.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)} {metric.Unit}".TrimEnd())),
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
        var judged = metrics.Where(metric => metric.Status.HasValue).ToArray();
        var outputs = judged.Length > 0 ? judged : primary is not null ? [primary] : Array.Empty<Metric>();
        item.Outputs = outputs.Length == 0 ? [new(item, null, true)]
            : outputs.Select((metric, index) =>
            {
                var (lower, upper) = InspectionQueueMeasurementPoint.ReadRecordedRange(step, metric.Name, metric.Kind, metric.Unit);
                return new InspectionQueueOutputResultItem(item, metric, ReferenceEquals(metric, primary))
                {
                    IsFirstOutput = index == 0,
                    RecordedLimits = InspectionQueueMeasurementPoint.FormatRange(lower, upper, metric.Unit, "R")
                };
            }).ToArray();
        return item;
    }
}

public sealed record InspectionQueueOutputResultItem(InspectionQueueStepResultItem Step, Metric? Measurement, bool IsPrimary)
{
    internal string RecordedLimits { get; init; } = "—";
    public bool IsFirstOutput { get; init; } = true;
    public string OrderLabel => IsFirstOutput ? DisplayOrder.ToString("00", CultureInfo.InvariantCulture) : string.Empty;
    public string ToolLabel => IsFirstOutput ? ToolName : string.Empty;
    public string CauseLabel => IsFirstOutput ? DisplayMessage : string.Empty;
    public int DisplayOrder => Step.DisplayOrder;
    public string ToolName => Step.ToolName;
    public string OutputName => Measurement?.Name ?? "—";
    public string Unit => Measurement?.Unit ?? string.Empty;
    public string Value => Step.Status != nameof(ResultStatus.Error) && Measurement is { } metric && double.IsFinite(metric.Value)
        ? metric.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "—";
    public string ValueWithUnit => string.IsNullOrEmpty(Unit) ? Value : $"{Value} {Unit}";
    public string Limits => IsPrimary || Step.IsUnjudged ? Step.Limits : RecordedLimits;
    public string DisplayLimits => RecordedLimits == "—" ? Limits : RecordedLimits;
    public string Status => Step.Status == nameof(ResultStatus.Error) || Step.IsUnjudged
        ? Step.Status : Measurement?.Status?.ToString() ?? Step.Status;
    public string StatusLabel => Status == Step.Status ? Step.StatusLabel
        : ThreeDLocalization.Shared.ResultStatusLabel(Measurement!.Status!.Value);
    public string DisplayMessage => Step.DisplayMessage;
}


public sealed class InspectionQueueRunRecordRequestEventArgs(string path) : EventArgs
{
    public string Path { get; } = path;
}

public sealed class InspectionQueueSelectionChangedEventArgs(InspectionQueueSampleItem? sample) : EventArgs
{
    public InspectionQueueSampleItem? Sample { get; } = sample;
}

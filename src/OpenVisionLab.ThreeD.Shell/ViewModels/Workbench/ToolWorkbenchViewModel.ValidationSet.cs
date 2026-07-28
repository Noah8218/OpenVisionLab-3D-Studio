using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private readonly ObservableCollection<ValidationSetSampleRow> validationSetSamples = [];
    private readonly ObservableCollection<ValidationSetSampleRow> filteredValidationSetSamples = [];
    private readonly ObservableCollection<ValidationSetStepRow> selectedValidationSetSteps = [];
    private RelayCommand selectValidationSetSourcesCommand = null!;
    private RelayCommand addCurrentSourceToValidationSetCommand = null!;
    private RelayCommand runValidationSetCommand = null!;
    private RelayCommand clearValidationSetCommand = null!;
    private RelayCommand cancelValidationSetCommand = null!;
    private RelayCommand setValidationSetFilterCommand = null!;
    private RelayCommand previousValidationSetIssueCommand = null!;
    private RelayCommand nextValidationSetIssueCommand = null!;
    private RelayCommand openValidationSetComparisonCommand = null!;
    private ValidationSetSampleRow? selectedValidationSetSample;
    private ValidationSetStepRow? selectedValidationSetStep;
    private ValidationSetStatusFilter validationSetFilter = ValidationSetStatusFilter.All;
    private string validationSetSummary = string.Empty;
    private string validationSetCapability = string.Empty;
    private string validationSetProgressText = string.Empty;
    private double validationSetProgress;
    private bool isValidationSetRunning;
    private CancellationTokenSource? validationSetCancellation;

    public event EventHandler? SelectValidationSetSourcesRequested;
    public event EventHandler? ValidationSetComparisonRequested;

    public ReadOnlyObservableCollection<ValidationSetSampleRow> ValidationSetSamples { get; private set; } = null!;

    public ReadOnlyObservableCollection<ValidationSetStepRow> SelectedValidationSetSteps { get; private set; } = null!;

    public ICommand SelectValidationSetSourcesCommand => selectValidationSetSourcesCommand;

    public ICommand AddCurrentSourceToValidationSetCommand => addCurrentSourceToValidationSetCommand;

    public ICommand RunValidationSetCommand => runValidationSetCommand;

    public ICommand ClearValidationSetCommand => clearValidationSetCommand;

    public ICommand CancelValidationSetCommand => cancelValidationSetCommand;

    public ICommand SetValidationSetFilterCommand => setValidationSetFilterCommand;

    public ICommand PreviousValidationSetIssueCommand => previousValidationSetIssueCommand;

    public ICommand NextValidationSetIssueCommand => nextValidationSetIssueCommand;

    public ICommand OpenValidationSetComparisonCommand => openValidationSetComparisonCommand;

    public ValidationSetSampleRow? SelectedValidationSetSample
    {
        get => selectedValidationSetSample;
        set
        {
            if (ReferenceEquals(selectedValidationSetSample, value))
            {
                return;
            }

            selectedValidationSetSample = value;
            selectedValidationSetSteps.Clear();
            foreach (var step in value?.Steps ?? [])
            {
                selectedValidationSetSteps.Add(step);
            }
            SelectedValidationSetStep =
                value?.Steps.FirstOrDefault(step => step.Status is "Fail" or "Error")
                ?? value?.Steps.FirstOrDefault();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedValidationSetSample));
            previousValidationSetIssueCommand.RaiseCanExecuteChanged();
            nextValidationSetIssueCommand.RaiseCanExecuteChanged();
            openValidationSetComparisonCommand.RaiseCanExecuteChanged();
        }
    }

    public ValidationSetStepRow? SelectedValidationSetStep
    {
        get => selectedValidationSetStep;
        set
        {
            if (ReferenceEquals(selectedValidationSetStep, value)) return;
            selectedValidationSetStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedValidationSetStep));
        }
    }

    public bool HasSelectedValidationSetStep => SelectedValidationSetStep is not null;

    public ValidationSetStatusFilter ValidationSetFilter
    {
        get => validationSetFilter;
        private set
        {
            if (validationSetFilter == value) return;
            validationSetFilter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValidationSetFilterAll));
            OnPropertyChanged(nameof(IsValidationSetFilterPass));
            OnPropertyChanged(nameof(IsValidationSetFilterFail));
            OnPropertyChanged(nameof(IsValidationSetFilterError));
            RefreshFilteredValidationSetSamples();
        }
    }

    public bool IsValidationSetFilterAll => ValidationSetFilter == ValidationSetStatusFilter.All;
    public bool IsValidationSetFilterPass => ValidationSetFilter == ValidationSetStatusFilter.Pass;
    public bool IsValidationSetFilterFail => ValidationSetFilter == ValidationSetStatusFilter.Fail;
    public bool IsValidationSetFilterError => ValidationSetFilter == ValidationSetStatusFilter.Error;

    public int ValidationSetAllCount => validationSetSamples.Count;
    public int ValidationSetPassCount => validationSetSamples.Count(row => row.Status == "Pass");
    public int ValidationSetFailCount => validationSetSamples.Count(row => row.Status == "Fail");
    public int ValidationSetErrorCount => validationSetSamples.Count(row => row.Status == "Error");

    public string ValidationSetSummary
    {
        get => validationSetSummary;
        private set
        {
            if (validationSetSummary == value) return;
            validationSetSummary = value;
            OnPropertyChanged();
        }
    }

    public string ValidationSetCapability
    {
        get => validationSetCapability;
        private set
        {
            if (validationSetCapability == value) return;
            validationSetCapability = value;
            OnPropertyChanged();
        }
    }

    public string ValidationSetProgressText
    {
        get => validationSetProgressText;
        private set
        {
            if (validationSetProgressText == value) return;
            validationSetProgressText = value;
            OnPropertyChanged();
        }
    }

    public double ValidationSetProgress
    {
        get => validationSetProgress;
        private set
        {
            if (Math.Abs(validationSetProgress - value) < 0.001) return;
            validationSetProgress = value;
            OnPropertyChanged();
        }
    }

    public bool IsValidationSetRunning
    {
        get => isValidationSetRunning;
        private set
        {
            if (isValidationSetRunning == value) return;
            isValidationSetRunning = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsValidationSetIdle));
            addCurrentSourceToValidationSetCommand.RaiseCanExecuteChanged();
            runValidationSetCommand.RaiseCanExecuteChanged();
            clearValidationSetCommand.RaiseCanExecuteChanged();
            selectValidationSetSourcesCommand.RaiseCanExecuteChanged();
            cancelValidationSetCommand.RaiseCanExecuteChanged();
            previousValidationSetIssueCommand.RaiseCanExecuteChanged();
            nextValidationSetIssueCommand.RaiseCanExecuteChanged();
            openValidationSetComparisonCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsValidationSetIdle => !IsValidationSetRunning;

    public bool HasValidationSetSamples => validationSetSamples.Count > 0;

    public bool HasSelectedValidationSetSample => SelectedValidationSetSample is not null;

    private void InitializeValidationSet()
    {
        ValidationSetSamples = new ReadOnlyObservableCollection<ValidationSetSampleRow>(filteredValidationSetSamples);
        SelectedValidationSetSteps = new ReadOnlyObservableCollection<ValidationSetStepRow>(selectedValidationSetSteps);
        selectValidationSetSourcesCommand = new RelayCommand(
            _ => SelectValidationSetSourcesRequested?.Invoke(this, EventArgs.Empty),
            _ => !IsValidationSetRunning);
        addCurrentSourceToValidationSetCommand = new RelayCommand(
            _ => AddCurrentSourceToValidationSet(),
            _ => !IsValidationSetRunning
                 && IsSourceReadyForRecipe
                 && !string.IsNullOrWhiteSpace(Source.Path)
                 && File.Exists(Source.Path));
        runValidationSetCommand = new RelayCommand(
            _ => _ = RunValidationSetAsync(),
            _ => !IsValidationSetRunning && validationSetSamples.Count > 0);
        clearValidationSetCommand = new RelayCommand(
            _ => ClearValidationSet(),
            _ => !IsValidationSetRunning && validationSetSamples.Count > 0);
        cancelValidationSetCommand = new RelayCommand(
            _ => validationSetCancellation?.Cancel(),
            _ => IsValidationSetRunning);
        setValidationSetFilterCommand = new RelayCommand(
            parameter => SetValidationSetFilter(parameter?.ToString()));
        previousValidationSetIssueCommand = new RelayCommand(
            _ => MoveValidationSetIssue(-1),
            _ => !IsValidationSetRunning && VisibleValidationSetIssues().Count > 0);
        nextValidationSetIssueCommand = new RelayCommand(
            _ => MoveValidationSetIssue(1),
            _ => !IsValidationSetRunning && VisibleValidationSetIssues().Count > 0);
        openValidationSetComparisonCommand = new RelayCommand(
            _ => OpenSelectedValidationSetComparison(),
            _ => !IsValidationSetRunning
                 && SelectedValidationSetSample is { SourcePath: var path }
                 && File.Exists(path)
                 && IsSourceReadyForRecipe);
        Localization.PropertyChanged += (_, _) => RefreshValidationSetLocalization();
        RefreshValidationSetCapability();
        RefreshValidationSetSummary();
    }

    private void AddCurrentSourceToValidationSet()
    {
        if (!IsSourceReadyForRecipe
            || string.IsNullOrWhiteSpace(Source.Path)
            || !File.Exists(Source.Path))
        {
            return;
        }

        SetValidationSetSources(
            validationSetSamples
                .Select(sample => sample.SourcePath)
                .Append(Source.Path));
        AppendLog(
            "Validation Set",
            $"Current recipe input staged without execution: {Path.GetFullPath(Source.Path)}.");
    }

    public void SetValidationSetSources(IEnumerable<string> sourcePaths)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        var paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        validationSetSamples.Clear();
        foreach (var (path, index) in paths.Select((path, index) => (path, index)))
        {
            validationSetSamples.Add(new ValidationSetSampleRow(
                index + 1,
                path,
                "Pending",
                Localize("대기", "Pending"),
                Localize("명시적 전체 실행 대기", "Waiting for explicit Run All"),
                string.Empty,
                []));
        }

        ValidationSetFilter = ValidationSetStatusFilter.All;
        RefreshFilteredValidationSetSamples();
        NotifyValidationSetCountsChanged();
        RebuildOutputCompareCandidates();
        runValidationSetCommand.RaiseCanExecuteChanged();
        clearValidationSetCommand.RaiseCanExecuteChanged();
        RefreshValidationSetSummary();
    }

    internal async Task RunValidationSetAsync()
    {
        if (IsValidationSetRunning || validationSetSamples.Count == 0)
        {
            return;
        }

        validationSetCancellation?.Dispose();
        validationSetCancellation = new CancellationTokenSource();
        IsValidationSetRunning = true;
        ValidationSetProgress = 0;
        ValidationSetProgressText = Localize("반복 검증 준비 중", "Preparing repeat validation");
        ValidationSetSummary = Localize(
            $"{validationSetSamples.Count}개 샘플을 순서대로 실행하고 있습니다.",
            $"Running {validationSetSamples.Count} sample(s) sequentially.");
        try
        {
            var document = CreateDocument();
            var paths = validationSetSamples.Select(row => row.SourcePath).ToArray();
            var progress = new Progress<ToolRecipeValidationProgress>(ReportValidationSetProgress);
            var result = await Task.Run(() => ToolRecipeValidationSetExecution.Execute(
                document,
                paths,
                validationSetCancellation.Token,
                progress));
            validationSetSamples.Clear();
            foreach (var sample in result.Samples)
            {
                var steps = sample.Steps.Select(step => new ValidationSetStepRow(
                    step.Order,
                    step.StepId,
                    step.ToolName,
                    step.Status.ToString(),
                    LocalizeStatus(step.Status),
                    step.Evidence,
                    step.Metrics.Select(metric => new ValidationSetMetricRow(
                        metric.Name,
                        metric.Value.ToString("G6", System.Globalization.CultureInfo.InvariantCulture),
                        metric.Unit,
                        metric.Status?.ToString() ?? string.Empty,
                        metric.Status is { } metricStatus ? LocalizeStatus(metricStatus) : string.Empty)).ToArray(),
                    step.Overlays.Select(overlay => new ValidationSetOverlayRow(
                        overlay.Kind.ToString(),
                        overlay.Label,
                        overlay.Status?.ToString() ?? string.Empty,
                        overlay.Status is { } overlayStatus ? LocalizeStatus(overlayStatus) : string.Empty)).ToArray())).ToArray();
                validationSetSamples.Add(new ValidationSetSampleRow(
                    sample.Order,
                    sample.SourcePath,
                    sample.Status.ToString(),
                    LocalizeStatus(sample.Status),
                    LocalizeResultMessage(sample),
                    sample.Duration.TotalMilliseconds.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " ms",
                    steps));
            }

            ValidationSetFilter = ValidationSetStatusFilter.All;
            RefreshFilteredValidationSetSamples();
            NotifyValidationSetCountsChanged();
            RebuildOutputCompareCandidates();
            ValidationSetSummary = result.Samples.Count == 0
                ? Localize(result.Message, result.Message)
                : LocalizeResultSummary(result);
            SelectedValidationSetSample =
                validationSetSamples.FirstOrDefault(row => row.Status is "Fail" or "Error")
                ?? validationSetSamples.FirstOrDefault();
            ValidationSetProgress = 100;
            ValidationSetProgressText = Localize(
                $"완료 {result.Samples.Count}개",
                $"{result.Samples.Count} completed");
            AppendLog("Validation Set", result.Message);
        }
        catch (OperationCanceledException)
        {
            ValidationSetSummary = Localize(
                "반복 검증이 취소되었습니다. 작성 중인 레시피와 3D 뷰 입력은 변경되지 않았습니다.",
                "Repeat validation was canceled. The authored recipe and 3D Viewer input were not changed.");
            ValidationSetProgressText = Localize("사용자 취소", "Canceled");
            AppendLog("Validation Set", "Canceled by operator.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or OverflowException)
        {
            ValidationSetSummary = Localize(
                $"반복 검증을 시작할 수 없습니다: {exception.Message}",
                $"Validation Set could not start: {exception.Message}");
            AppendLog("Validation Set", exception.Message);
        }
        finally
        {
            IsValidationSetRunning = false;
            validationSetCancellation?.Dispose();
            validationSetCancellation = null;
            OnPropertyChanged(nameof(HasValidationSetSamples));
        }
    }

    private void ClearValidationSet()
    {
        validationSetSamples.Clear();
        filteredValidationSetSamples.Clear();
        SelectedValidationSetSample = null;
        SelectedValidationSetStep = null;
        ValidationSetFilter = ValidationSetStatusFilter.All;
        ValidationSetProgress = 0;
        ValidationSetProgressText = string.Empty;
        ClearValidationSetComparePins();
        NotifyValidationSetCountsChanged();
        RebuildOutputCompareCandidates();
        runValidationSetCommand.RaiseCanExecuteChanged();
        clearValidationSetCommand.RaiseCanExecuteChanged();
        RefreshValidationSetSummary();
    }

    private void SetValidationSetFilter(string? value)
    {
        if (Enum.TryParse<ValidationSetStatusFilter>(value, ignoreCase: true, out var filter))
        {
            ValidationSetFilter = filter;
        }
    }

    private void RefreshFilteredValidationSetSamples()
    {
        var selectedPath = SelectedValidationSetSample?.SourcePath;
        filteredValidationSetSamples.Clear();
        foreach (var sample in validationSetSamples.Where(MatchesValidationSetFilter))
        {
            filteredValidationSetSamples.Add(sample);
        }

        SelectedValidationSetSample = selectedPath is null
            ? filteredValidationSetSamples.FirstOrDefault()
            : filteredValidationSetSamples.FirstOrDefault(sample =>
                string.Equals(sample.SourcePath, selectedPath, StringComparison.OrdinalIgnoreCase))
              ?? filteredValidationSetSamples.FirstOrDefault();
        OnPropertyChanged(nameof(HasValidationSetSamples));
        previousValidationSetIssueCommand.RaiseCanExecuteChanged();
        nextValidationSetIssueCommand.RaiseCanExecuteChanged();
    }

    private bool MatchesValidationSetFilter(ValidationSetSampleRow sample) =>
        ValidationSetFilter switch
        {
            ValidationSetStatusFilter.Pass => sample.Status == "Pass",
            ValidationSetStatusFilter.Fail => sample.Status == "Fail",
            ValidationSetStatusFilter.Error => sample.Status == "Error",
            _ => true
        };

    private IReadOnlyList<ValidationSetSampleRow> VisibleValidationSetIssues() =>
        filteredValidationSetSamples
            .Where(sample => sample.Status is "Fail" or "Error")
            .ToArray();

    private void MoveValidationSetIssue(int offset)
    {
        var issues = VisibleValidationSetIssues();
        if (issues.Count == 0) return;

        var currentIndex = -1;
        for (var index = 0; index < issues.Count; index++)
        {
            if (ReferenceEquals(issues[index], SelectedValidationSetSample))
            {
                currentIndex = index;
                break;
            }
        }
        var nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + offset + issues.Count) % issues.Count;
        SelectedValidationSetSample = issues[nextIndex];
    }

    private void OpenSelectedValidationSetComparison()
    {
        if (SelectedValidationSetSample is not { } sample || !File.Exists(sample.SourcePath))
        {
            return;
        }

        RebuildOutputCompareCandidates();
        CompareSlotAArtifactId = Source.Id;
        CompareSlotBArtifactId = GetValidationSetCompareArtifactId(sample);
        CompareSlotCArtifactId = string.Empty;
        ValidationSetComparisonRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ReportValidationSetProgress(ToolRecipeValidationProgress progress)
    {
        ValidationSetProgress = progress.TotalCount == 0
            ? 0
            : progress.CompletedCount * 100d / progress.TotalCount;
        ValidationSetProgressText = progress.CompletedStatus is null
            ? Localize(
                $"{progress.CompletedCount + 1}/{progress.TotalCount} 실행 중 · {Path.GetFileName(progress.CurrentSourcePath)}",
                $"Running {progress.CompletedCount + 1}/{progress.TotalCount} · {Path.GetFileName(progress.CurrentSourcePath)}")
            : Localize(
                $"{progress.CompletedCount}/{progress.TotalCount} 완료 · {LocalizeStatus(progress.CompletedStatus.Value)}",
                $"{progress.CompletedCount}/{progress.TotalCount} completed · {LocalizeStatus(progress.CompletedStatus.Value)}");
    }

    private void NotifyValidationSetCountsChanged()
    {
        OnPropertyChanged(nameof(HasValidationSetSamples));
        OnPropertyChanged(nameof(ValidationSetAllCount));
        OnPropertyChanged(nameof(ValidationSetPassCount));
        OnPropertyChanged(nameof(ValidationSetFailCount));
        OnPropertyChanged(nameof(ValidationSetErrorCount));
        previousValidationSetIssueCommand.RaiseCanExecuteChanged();
        nextValidationSetIssueCommand.RaiseCanExecuteChanged();
        openValidationSetComparisonCommand.RaiseCanExecuteChanged();
    }

    private void ClearValidationSetComparePins()
    {
        if (IsValidationSetCompareArtifactId(CompareSlotAArtifactId)) CompareSlotAArtifactId = string.Empty;
        if (IsValidationSetCompareArtifactId(CompareSlotBArtifactId)) CompareSlotBArtifactId = string.Empty;
        if (IsValidationSetCompareArtifactId(CompareSlotCArtifactId)) CompareSlotCArtifactId = string.Empty;
    }

    private void RefreshValidationSetCapability()
    {
        addCurrentSourceToValidationSetCommand?.RaiseCanExecuteChanged();
        if (ToolRecipeValidationSetExecution.CanExecute(CreateDocument(), out var message))
        {
            ValidationSetCapability = Localize(
                "현재 레시피의 지원되는 전체 툴 체인을 동일 그리드 C3D 샘플에 안전하게 재바인딩하여 순서대로 실행할 수 있습니다.",
                "The complete supported tool chain can be safely rebound and replayed in order against same-grid C3D samples.");
            return;
        }

        ValidationSetCapability = Localize(
            $"현재 레시피 실행 범위: {message}",
            $"Current recipe coverage: {message}");
    }

    private void RefreshValidationSetSummary()
    {
        ValidationSetSummary = validationSetSamples.Count == 0
            ? Localize(
                "검증할 C3D 샘플을 추가한 다음 전체 실행을 누르세요. 샘플 선택만으로 레시피나 뷰어 입력은 바뀌지 않습니다.",
                "Add C3D samples, then choose Run All. Selecting samples never changes the recipe or Viewer input.")
            : Localize(
                $"{validationSetSamples.Count}개 샘플 준비됨 · 실행 전",
                $"{validationSetSamples.Count} sample(s) ready · not run");
    }

    private void RefreshValidationSetLocalization()
    {
        RefreshValidationSetCapability();
        for (var index = 0; index < validationSetSamples.Count; index++)
        {
            var sample = validationSetSamples[index];
            var status = Enum.TryParse<ResultStatus>(sample.Status, out var parsed)
                ? parsed
                : (ResultStatus?)null;
            var steps = sample.Steps.Select(step =>
            {
                var stepStatus = Enum.TryParse<ResultStatus>(step.Status, out var parsedStep)
                    ? parsedStep
                    : ResultStatus.Error;
                return step with
                {
                    StatusText = LocalizeStatus(stepStatus),
                    Metrics = step.Metrics.Select(metric => metric with
                    {
                        StatusText = Enum.TryParse<ResultStatus>(metric.Status, out var metricStatus)
                            ? LocalizeStatus(metricStatus)
                            : string.Empty
                    }).ToArray(),
                    Overlays = step.Overlays.Select(overlay => overlay with
                    {
                        StatusText = Enum.TryParse<ResultStatus>(overlay.Status, out var overlayStatus)
                            ? LocalizeStatus(overlayStatus)
                            : string.Empty
                    }).ToArray()
                };
            }).ToArray();
            validationSetSamples[index] = sample with
            {
                StatusText = status is null ? Localize("대기", "Pending") : LocalizeStatus(status.Value),
                Steps = steps
            };
        }

        RefreshFilteredValidationSetSamples();
        NotifyValidationSetCountsChanged();
        RebuildOutputCompareCandidates();
        if (validationSetSamples.All(sample => sample.Status == "Pending"))
        {
            RefreshValidationSetSummary();
        }
        else if (validationSetSamples.Count > 0)
        {
            var pass = validationSetSamples.Count(sample => sample.Status == "Pass");
            var fail = validationSetSamples.Count(sample => sample.Status == "Fail");
            var error = validationSetSamples.Count(sample => sample.Status == "Error");
            ValidationSetSummary = Localize(
                $"완료 {validationSetSamples.Count}개 · 통과 {pass} · 실패 {fail} · 오류 {error}",
                $"Completed {validationSetSamples.Count} · Pass {pass} · Fail {fail} · Error {error}");
        }
    }

    private static string Localize(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;

    private static string LocalizeStatus(ResultStatus status) => status switch
    {
        ResultStatus.Pass => Localize("통과", "Pass"),
        ResultStatus.Fail => Localize("실패", "Fail"),
        ResultStatus.Warning => Localize("경고", "Warning"),
        _ => Localize("오류", "Error")
    };

    private static string LocalizeResultMessage(ToolRecipeValidationSampleResult sample) =>
        sample.Status switch
        {
            ResultStatus.Pass => Localize("모든 지원 검사 단계가 통과했습니다.", "All supported inspection steps passed."),
            ResultStatus.Fail => Localize("하나 이상의 검사 단계가 허용 범위를 벗어났습니다.", "One or more inspection steps are out of tolerance."),
            ResultStatus.Warning => Localize("검사가 경고와 함께 완료되었습니다.", "Inspection completed with warnings."),
            _ => Localize($"실행 오류: {LocalizeValidationError(sample.Message)}", $"Execution error: {sample.Message}")
        };

    private static string LocalizeValidationError(string message)
    {
        if (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English)
        {
            return message;
        }

        const string gridPrefix = "Grid mismatch. Recipe expects ";
        if (message.StartsWith(gridPrefix, StringComparison.Ordinal))
        {
            return "그리드 불일치. 레시피 "
                   + message[gridPrefix.Length..]
                       .Replace("; sample is ", "; 샘플 ", StringComparison.Ordinal)
                       .TrimEnd('.');
        }
        if (message.StartsWith("Validation sample does not exist.", StringComparison.Ordinal))
        {
            return "검증 샘플 파일이 없습니다.";
        }
        if (message.Equals("Recipe source grid identity is incomplete.", StringComparison.Ordinal))
        {
            return "레시피 소스의 그리드 식별 정보가 완전하지 않습니다.";
        }

        return message;
    }

    private static string LocalizeResultSummary(ToolRecipeValidationSetResult result)
    {
        var pass = result.Samples.Count(sample => sample.Status == ResultStatus.Pass);
        var fail = result.Samples.Count(sample => sample.Status == ResultStatus.Fail);
        var error = result.Samples.Count(sample => sample.Status == ResultStatus.Error);
        return Localize(
            $"완료 {result.Samples.Count}개 · 통과 {pass} · 실패 {fail} · 오류 {error} · {result.Duration.TotalMilliseconds:N0} ms",
            $"Completed {result.Samples.Count} · Pass {pass} · Fail {fail} · Error {error} · {result.Duration.TotalMilliseconds:N0} ms");
    }
}

public sealed record ValidationSetSampleRow(
    int Order,
    string SourcePath,
    string Status,
    string StatusText,
    string Message,
    string Duration,
    IReadOnlyList<ValidationSetStepRow> Steps)
{
    public string FileName => Path.GetFileName(SourcePath);
}

public sealed record ValidationSetStepRow(
    int Order,
    string StepId,
    string ToolName,
    string Status,
    string StatusText,
    string Evidence,
    IReadOnlyList<ValidationSetMetricRow> Metrics,
    IReadOnlyList<ValidationSetOverlayRow> Overlays);

public sealed record ValidationSetMetricRow(
    string Name,
    string Value,
    string Unit,
    string Status,
    string StatusText);

public sealed record ValidationSetOverlayRow(
    string Kind,
    string Label,
    string Status,
    string StatusText);

public enum ValidationSetStatusFilter
{
    All,
    Pass,
    Fail,
    Error
}

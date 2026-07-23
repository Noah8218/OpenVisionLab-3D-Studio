using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using LiveChartsCore.Kernel;
using OpenVisionLab;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell;

public enum CalibrationSection
{
    Overview = 0,
    HeightCalibration = 1,
    SensorAlignment = 2,
    Repeatability = 3,
    History = 4
}

public sealed class CalibrationCenterViewModel : INotifyPropertyChanged
{
    private static readonly ReadOnlyCollection<string> SectionNames = Array.AsReadOnly(
        new[] { "Overview", "Height Calibration", "Sensor Alignment", "Repeatability", "History" });

    private readonly RelayCommand loadStudyCommand;
    private readonly RelayCommand calculateCommand;
    private readonly RelayCommand validateCommand;
    private readonly RelayCommand activateCommand;
    private readonly RelayCommand selectRepeatabilityChartPointCommand;
    private readonly List<CalibrationRepeatabilityRunItem> repeatabilityChartRuns = [];
    private CalibrationSection selectedSection = CalibrationSection.Repeatability;
    private CalibrationRepeatabilityRunItem? selectedRepeatabilityRun;
    private LoadedThicknessRepeatabilityStudy? loadedStudy;
    private ThicknessRepeatabilityInputValidation? inputValidation;
    private ThicknessRepeatabilityEvaluation? evaluation;
    private string repeatabilityOperationStatus = "No repeatability study loaded";

    public CalibrationCenterViewModel()
    {
        repeatabilityOperationStatus = L("반복성 스터디를 불러오지 않았습니다.", "No repeatability study loaded");
        loadStudyCommand = new RelayCommand(_ => LoadStudyRequested?.Invoke(this, EventArgs.Empty));
        calculateCommand = new RelayCommand(_ => CalculateRepeatability(), _ => CanCalculate);
        validateCommand = new RelayCommand(_ => { }, _ => false);
        activateCommand = new RelayCommand(_ => { }, _ => false);
        selectRepeatabilityChartPointCommand = new RelayCommand(
            SelectRepeatabilityChartPoint,
            CanSelectRepeatabilityChartPoint);
        RepeatabilityChartSeries.Add(new CalibrationRepeatabilityChartSeriesModel(RepeatabilityChartValues));
        OpenVisionLanguageService.LanguageChanged += (_, _) => RefreshLanguage();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LoadStudyRequested;

    public IReadOnlyList<string> Sections => SectionNames;
    public IReadOnlyList<string> RepeatabilityMetricOptions => [SelectedRepeatabilityMetric];
    public ObservableCollection<CalibrationRepeatabilityRunItem> RepeatabilityRuns { get; } = [];
    public ObservableCollection<double> RepeatabilityChartValues { get; } = [];
    public ObservableCollection<CalibrationRepeatabilityChartSeriesModel> RepeatabilityChartSeries { get; } = [];
    public ObservableCollection<string> RepeatabilityChartRunLabels { get; } = [];

    public ICommand LoadStudyCommand => loadStudyCommand;
    public ICommand CalculateCommand => calculateCommand;
    public ICommand ValidateCommand => validateCommand;
    public ICommand ActivateCommand => activateCommand;
    public ICommand SelectRepeatabilityChartPointCommand => selectRepeatabilityChartPointCommand;

    public CalibrationSection SelectedSection
    {
        get => selectedSection;
        set
        {
            if (!Enum.IsDefined(value)
                || !IsSectionAvailable(value)
                || !SetField(ref selectedSection, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedSectionIndex));
        }
    }

    public static bool IsSectionAvailable(CalibrationSection section) =>
        section is CalibrationSection.Overview or CalibrationSection.Repeatability;

    public int SelectedSectionIndex
    {
        get => (int)SelectedSection;
        set
        {
            if (Enum.IsDefined(typeof(CalibrationSection), value))
            {
                SelectedSection = (CalibrationSection)value;
            }
        }
    }

    public CalibrationRepeatabilityRunItem? SelectedRepeatabilityRun
    {
        get => selectedRepeatabilityRun;
        set
        {
            if (SetField(ref selectedRepeatabilityRun, value))
            {
                RebuildRepeatabilityChart();
                OnPropertyChanged(nameof(SelectedRepeatabilityRunSummary));
            }
        }
    }

    public bool CanCalculate => inputValidation?.IsReady == true;
    public bool CanValidate => false;
    public bool CanActivate => false;
    public bool HasLoadedRepeatabilityStudy => loadedStudy is not null;
    public bool HasCalculatedRepeatability => evaluation is not null;
    public bool HasMultipleRepeatabilityMetrics => false;
    public string SelectedRepeatabilityMetric => L("두께", "Thickness");
    public string? LoadedRepeatabilityStudyPath => loadedStudy?.Path;

    public string WorkspaceStatus => L("오프라인 교정 작업공간", "Offline calibration workspace");
    public string UnitFrameStatus => loadedStudy is null
        ? L("선언 단위: 미설정 | 프레임: 미설정", "Declared unit: not set | Frame: not set")
        : L($"선언 단위: {loadedStudy.Input.Unit} | 프레임: {loadedStudy.Input.FrameId}", $"Declared unit: {loadedStudy.Input.Unit} | Frame: {loadedStudy.Input.FrameId}");
    public string CalibrationStatus => L("교정: 활성 프로파일 없음", "Calibration: no active profile");
    public string ActiveProfileStatus => L("활성 교정 프로파일 없음", "No active calibration profile");
    public string SelectedProfileStatus => L("선택된 프로파일 없음", "No profile selected");
    public string ProfileVersionStatus => L("버전: -", "Version: -");
    public string ProfileValidityStatus => L("유효 기한: -", "Valid until: -");
    public string HeightTargetStatus => L("높이 타깃: 불러오지 않음", "Height target: not loaded");
    public string AlignmentTargetStatus => L("정렬 타깃: 불러오지 않음", "Alignment target: not loaded");
    public string RepeatabilityInputStatus => loadedStudy is null
        ? repeatabilityOperationStatus
        : L($"반복 실행: {loadedStudy.Input.Runs!.Count} | {Path.GetFileName(loadedStudy.Path)}", $"Repeatability runs: {loadedStudy.Input.Runs!.Count} | {Path.GetFileName(loadedStudy.Path)}");
    public string RepeatabilitySummary => evaluation is not null
        ? L(
            string.Create(
                CultureInfo.InvariantCulture,
                $"N={evaluation.RunCount} | 평균 {FormatNumber(evaluation.Mean)} | s {FormatNumber(evaluation.SampleStandardDeviation)} | 6s {FormatNumber(evaluation.SixSigmaSpread)} | 범위 {FormatNumber(evaluation.Range)} {loadedStudy!.Input.Unit} | {LocalizeResultStatus(evaluation.Result.Status)}"),
            string.Create(
            CultureInfo.InvariantCulture,
            $"N={evaluation.RunCount} | Mean {FormatNumber(evaluation.Mean)} | s {FormatNumber(evaluation.SampleStandardDeviation)} | 6s {FormatNumber(evaluation.SixSigmaSpread)} | Range {FormatNumber(evaluation.Range)} {loadedStudy!.Input.Unit} | {evaluation.Result.Status}")
          )
        : loadedStudy is not null
            ? L($"{loadedStudy.Input.Runs!.Count}회 | {loadedStudy.Input.ReferenceRoiId} | {LocalizedOperationState}", $"{loadedStudy.Input.Runs!.Count} runs | {loadedStudy.Input.ReferenceRoiId} | {LocalizedOperationState}")
            : repeatabilityOperationStatus;
    public string RepeatabilityMapState => evaluation is null
        ? L("계산하지 않음", "Not calculated")
        : L($"종합 결과: {LocalizeResultStatus(evaluation.Result.Status)}", $"Aggregate result: {evaluation.Result.Status}");
    public string RepeatabilityMapHint => evaluation is not null
        ? L("대표값 스터디에는 포인트별 변동 데이터가 없습니다.", "Per-point variation is unavailable for this representative-value study")
        : loadedStudy is not null
            ? L("검증된 스터디 입력이 준비되었습니다.", "Verified study inputs are ready")
            : L("반복 측정값을 불러와 포인트별 변동을 비교하세요.", "Load repeated measurements to compare per-point variation");
    public string RepeatabilityChartState => evaluation is not null
        ? L($"{evaluation.RunCount}회 포함 | {LocalizeResultStatus(evaluation.Result.Status)}", $"{evaluation.RunCount} included runs | {evaluation.Result.Status}")
        : loadedStudy is not null
            ? inputValidation?.IsReady == true
                ? L($"{loadedStudy.Input.Runs!.Count}회 검증됨", $"{loadedStudy.Input.Runs!.Count} verified runs")
                : L($"{loadedStudy.Input.Runs!.Count}회 | 입력 오류", $"{loadedStudy.Input.Runs!.Count} runs | input invalid")
            : L("반복 실행 없음", "No repeatability runs");
    public string HeightCalibrationState => L("계산하지 않음", "Not calculated");
    public string AlignmentState => L("계산하지 않음", "Not calculated");
    public string RepeatabilityOverviewState => evaluation is not null
        ? $"{LocalizeResultStatus(evaluation.Result.Status)} | N={evaluation.RunCount}"
        : inputValidation?.IsReady == true
            ? L($"준비됨 | N={inputValidation.RunCount}", $"Ready | N={inputValidation.RunCount}")
            : L("유효한 스터디 없음", "No valid study");
    public string ProfileActivationState => L("차단됨", "Blocked");
    public string HeightTargetEmptyState => L("높이 타깃 없음", "No height target loaded");
    public string AlignmentTargetEmptyState => L("정렬 타깃 없음", "No alignment target loaded");
    public string EmptyRepeatabilityRunsText => L("반복 실행 없음", "No repeatability runs");
    public string EmptyEvidenceText => L("계산 증거 없음", "No calculation evidence");
    public string EmptyProfilesText => L("교정 프로파일 없음", "No calibration profiles");
    public string ProfileNameValue => loadedStudy?.Input.StudyId ?? L("미설정", "Not set");
    public string SensorIdentityValue => L("미설정", "Not set");
    public string CalibrationTargetValue => loadedStudy is null
        ? L("미설정", "Not set")
        : $"{loadedStudy.Input.MeasurementDefinitionId} | {loadedStudy.Input.ReferenceRoiId}";
    public string UnitFrameValue => loadedStudy is null
        ? L("미설정", "Not set")
        : $"{loadedStudy.Input.Unit} | {loadedStudy.Input.FrameId}";
    public string MinimumRunCountValue => loadedStudy?.Input.Acceptance is { } acceptance
        ? acceptance.MinimumRunCount.ToString(CultureInfo.InvariantCulture)
        : L("미설정", "Not configured");
    public string StandardDeviationLimitValue => loadedStudy?.Input.Acceptance is { } acceptance
        ? Format(acceptance.MaximumSampleStandardDeviation, loadedStudy.Input.Unit)
        : L("미설정", "Not configured");
    public string RangeLimitValue => loadedStudy?.Input.Acceptance is { } acceptance
        ? Format(acceptance.MaximumRange, loadedStudy.Input.Unit)
        : L("미설정", "Not configured");
    public string RepeatabilityCalculationMessage => evaluation?.Result.Message ?? repeatabilityOperationStatus;
    public string ProfileLifecycleSummary => L("초안  >  계산됨  >  검증됨  >  활성", "Draft  >  Calculated  >  Validated  >  Active");
    public string SelectedRepeatabilityRunSummary => SelectedRepeatabilityRun is null
        ? L("선택된 반복 실행 없음", "No repeatability run selected")
        : L($"실행 {SelectedRepeatabilityRun.RunNumber} | {SelectedRepeatabilityRun.RunId} | {SelectedRepeatabilityRun.ValueText} | {SelectedRepeatabilityRun.Status}", $"Run {SelectedRepeatabilityRun.RunNumber} | {SelectedRepeatabilityRun.RunId} | {SelectedRepeatabilityRun.ValueText} | {SelectedRepeatabilityRun.Status}");

    public bool LoadStudy(string path)
    {
        ResetRepeatabilityStudy();
        try
        {
            loadedStudy = ThicknessRepeatabilityStudyLoader.Load(path);
            inputValidation = ThicknessRepeatabilityRule.Validate(loadedStudy.Input);
            repeatabilityOperationStatus = inputValidation.Message;
            PopulateRunRows(inputValidation.IsReady ? L("준비됨", "Ready") : L("오류", "Invalid"));
            NotifyRepeatabilityState();
            return inputValidation.IsReady;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or JsonException
            or ArgumentException)
        {
            repeatabilityOperationStatus = $"Study load failed: {exception.Message}";
            NotifyRepeatabilityState();
            return false;
        }
    }

    private void CalculateRepeatability()
    {
        if (loadedStudy is null)
        {
            return;
        }

        inputValidation = ThicknessRepeatabilityRule.Validate(loadedStudy.Input);
        if (!inputValidation.IsReady)
        {
            evaluation = null;
            repeatabilityOperationStatus = inputValidation.Message;
            PopulateRunRows(L("오류", "Invalid"));
            NotifyRepeatabilityState();
            return;
        }

        evaluation = ThicknessRepeatabilityRule.Evaluate(inputValidation.Input);
        repeatabilityOperationStatus = evaluation.Result.Message;
        PopulateRunRows(L("포함됨", "Included"));
        NotifyRepeatabilityState();
    }

    private void ResetRepeatabilityStudy()
    {
        loadedStudy = null;
        inputValidation = null;
        evaluation = null;
        repeatabilityOperationStatus = L("반복성 스터디를 불러오지 않았습니다.", "No repeatability study loaded");
        SelectedRepeatabilityRun = null;
        RepeatabilityRuns.Clear();
        RepeatabilityChartValues.Clear();
        repeatabilityChartRuns.Clear();
        RepeatabilityChartRunLabels.Clear();
    }

    private void PopulateRunRows(string status)
    {
        SelectedRepeatabilityRun = null;
        RepeatabilityRuns.Clear();
        if (loadedStudy is null)
        {
            return;
        }

        var sources = loadedStudy.Sources.ToDictionary(source => source.RunId, StringComparer.Ordinal);
        var runs = loadedStudy.Input.Runs!;
        for (var index = 0; index < runs.Count; index++)
        {
            var run = runs[index];
            sources.TryGetValue(run.RunId, out var source);
            RepeatabilityRuns.Add(new CalibrationRepeatabilityRunItem(
                index + 1,
                run.RunId,
                run.CapturedAt,
                source?.Name ?? run.SourceEntityId,
                run.Thickness,
                run.Unit,
                status));
        }

        RebuildRepeatabilityChart();
    }

    private void RebuildRepeatabilityChart()
    {
        RepeatabilityChartValues.Clear();
        repeatabilityChartRuns.Clear();
        RepeatabilityChartRunLabels.Clear();

        foreach (var run in RepeatabilityRuns)
        {
            if (!double.IsFinite(run.Value))
            {
                continue;
            }

            RepeatabilityChartValues.Add(run.Value);
            repeatabilityChartRuns.Add(run);
            RepeatabilityChartRunLabels.Add(run.RunNumber.ToString(CultureInfo.InvariantCulture));
        }
    }

    private string LocalizedOperationState => evaluation is not null
        ? L($"계산 완료 | {LocalizeResultStatus(evaluation.Result.Status)}", $"Calculated | {evaluation.Result.Status}")
        : loadedStudy is not null
            ? inputValidation?.IsReady == true
                ? L("검증된 입력이 계산 준비되었습니다.", "Verified inputs are ready to calculate.")
                : L("스터디 입력이 유효하지 않습니다.", "Study inputs are invalid.")
            : L("반복성 스터디를 불러오지 않았습니다.", "No repeatability study loaded");

    private void RefreshLanguage()
    {
        if (loadedStudy is null)
        {
            repeatabilityOperationStatus = L("반복성 스터디를 불러오지 않았습니다.", "No repeatability study loaded");
        }
        else
        {
            PopulateRunRows(evaluation is not null
                ? L("포함됨", "Included")
                : inputValidation?.IsReady == true
                    ? L("준비됨", "Ready")
                    : L("오류", "Invalid"));
        }

        OnPropertyChanged(string.Empty);
    }

    private void SelectRepeatabilityChartPoint(object? parameter)
    {
        if (parameter is CalibrationRepeatabilityRunItem run)
        {
            SelectRepeatabilityRun(run);
            return;
        }

        if (parameter is ChartPoint chartPoint)
        {
            SelectRepeatabilityRun(chartPoint);
            return;
        }

        if (parameter is not IEnumerable<ChartPoint> points)
        {
            return;
        }

        foreach (var candidatePoint in points)
        {
            if (SelectRepeatabilityRun(candidatePoint))
            {
                return;
            }
        }
    }

    private bool CanSelectRepeatabilityChartPoint(object? parameter)
    {
        if (parameter is CalibrationRepeatabilityRunItem run)
        {
            return RepeatabilityRuns.Contains(run);
        }

        if (parameter is ChartPoint chartPoint)
        {
            return TryGetRepeatabilityRun(chartPoint, out _);
        }

        if (parameter is IEnumerable<ChartPoint> points)
        {
        foreach (var candidatePoint in points)
        {
            if (TryGetRepeatabilityRun(candidatePoint, out _))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool SelectRepeatabilityRun(CalibrationRepeatabilityRunItem run)
    {
        if (!RepeatabilityRuns.Contains(run))
        {
            return false;
        }

        SelectedRepeatabilityRun = run;
        return true;
    }

    private bool SelectRepeatabilityRun(ChartPoint point)
    {
        return TryGetRepeatabilityRun(point, out var run)
            && SelectRepeatabilityRun(run);
    }

    private bool TryGetRepeatabilityRun(ChartPoint point, out CalibrationRepeatabilityRunItem run) =>
        TryGetRepeatabilityRun(point.Index, out run);

    private bool TryGetRepeatabilityRun(int runIndex, out CalibrationRepeatabilityRunItem run)
    {
        run = null!;
        if (runIndex < 0 || runIndex >= repeatabilityChartRuns.Count)
        {
            return false;
        }

        run = repeatabilityChartRuns[runIndex];
        return true;
    }

    private void NotifyRepeatabilityState()
    {
        OnPropertyChanged(nameof(CanCalculate));
        OnPropertyChanged(nameof(HasLoadedRepeatabilityStudy));
        OnPropertyChanged(nameof(HasCalculatedRepeatability));
        OnPropertyChanged(nameof(LoadedRepeatabilityStudyPath));
        OnPropertyChanged(nameof(UnitFrameStatus));
        OnPropertyChanged(nameof(RepeatabilityInputStatus));
        OnPropertyChanged(nameof(RepeatabilitySummary));
        OnPropertyChanged(nameof(RepeatabilityMapState));
        OnPropertyChanged(nameof(RepeatabilityMapHint));
        OnPropertyChanged(nameof(RepeatabilityChartState));
        OnPropertyChanged(nameof(RepeatabilityOverviewState));
        OnPropertyChanged(nameof(ProfileNameValue));
        OnPropertyChanged(nameof(CalibrationTargetValue));
        OnPropertyChanged(nameof(UnitFrameValue));
        OnPropertyChanged(nameof(MinimumRunCountValue));
        OnPropertyChanged(nameof(StandardDeviationLimitValue));
        OnPropertyChanged(nameof(RangeLimitValue));
        OnPropertyChanged(nameof(RepeatabilityCalculationMessage));
        calculateCommand.RaiseCanExecuteChanged();
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string Format(double value, string unit) => string.IsNullOrWhiteSpace(unit)
        ? value.ToString("0.######", CultureInfo.InvariantCulture)
        : $"{value.ToString("0.######", CultureInfo.InvariantCulture)} {unit}";

    private static string FormatNumber(double value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string LocalizeResultStatus(object? status) =>
        status?.ToString() switch
        {
            "Pass" => L("통과", "Pass"),
            "Fail" => L("실패", "Fail"),
            "Error" => L("오류", "Error"),
            var value => value ?? L("미정", "Unknown")
        };

    private static string L(string korean, string english) =>
        OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.English ? english : korean;
}

public sealed record CalibrationRepeatabilityRunItem(
    int RunNumber,
    string RunId,
    DateTimeOffset Timestamp,
    string Source,
    double Value,
    string Unit,
    string Status)
{
    public string TimestampText => Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    public string ValueText => string.IsNullOrWhiteSpace(Unit)
        ? Value.ToString("0.######", CultureInfo.InvariantCulture)
        : $"{Value.ToString("0.######", CultureInfo.InvariantCulture)} {Unit}";
}

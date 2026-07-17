using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
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
    private static readonly ReadOnlyCollection<string> MetricNames = Array.AsReadOnly(
        new[] { "Thickness" });

    private readonly RelayCommand loadStudyCommand;
    private readonly RelayCommand calculateCommand;
    private readonly RelayCommand validateCommand;
    private readonly RelayCommand activateCommand;
    private CalibrationSection selectedSection = CalibrationSection.Repeatability;
    private CalibrationRepeatabilityRunItem? selectedRepeatabilityRun;
    private LoadedThicknessRepeatabilityStudy? loadedStudy;
    private ThicknessRepeatabilityInputValidation? inputValidation;
    private ThicknessRepeatabilityEvaluation? evaluation;
    private string repeatabilityOperationStatus = "No repeatability study loaded";

    public CalibrationCenterViewModel()
    {
        loadStudyCommand = new RelayCommand(_ => LoadStudyRequested?.Invoke(this, EventArgs.Empty));
        calculateCommand = new RelayCommand(_ => CalculateRepeatability(), _ => CanCalculate);
        validateCommand = new RelayCommand(_ => { }, _ => false);
        activateCommand = new RelayCommand(_ => { }, _ => false);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? LoadStudyRequested;

    public IReadOnlyList<string> Sections => SectionNames;
    public IReadOnlyList<string> RepeatabilityMetricOptions => MetricNames;
    public ObservableCollection<CalibrationRepeatabilityRunItem> RepeatabilityRuns { get; } = [];

    public ICommand LoadStudyCommand => loadStudyCommand;
    public ICommand CalculateCommand => calculateCommand;
    public ICommand ValidateCommand => validateCommand;
    public ICommand ActivateCommand => activateCommand;

    public CalibrationSection SelectedSection
    {
        get => selectedSection;
        set
        {
            if (!Enum.IsDefined(value) || !SetField(ref selectedSection, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedSectionIndex));
        }
    }

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
                OnPropertyChanged(nameof(SelectedRepeatabilityRunSummary));
            }
        }
    }

    public bool CanCalculate => inputValidation?.IsReady == true;
    public bool CanValidate => false;
    public bool CanActivate => false;
    public bool HasLoadedRepeatabilityStudy => loadedStudy is not null;
    public bool HasCalculatedRepeatability => evaluation is not null;
    public bool HasMultipleRepeatabilityMetrics => MetricNames.Count > 1;
    public string SelectedRepeatabilityMetric => MetricNames[0];
    public string? LoadedRepeatabilityStudyPath => loadedStudy?.Path;

    public string WorkspaceStatus => "Offline calibration workspace";
    public string UnitFrameStatus => loadedStudy is null
        ? "Declared unit: not set | Frame: not set"
        : $"Declared unit: {loadedStudy.Input.Unit} | Frame: {loadedStudy.Input.FrameId}";
    public string CalibrationStatus => "Calibration: no active profile";
    public string ActiveProfileStatus => "No active calibration profile";
    public string SelectedProfileStatus => "No profile selected";
    public string ProfileVersionStatus => "Version: -";
    public string ProfileValidityStatus => "Valid until: -";
    public string HeightTargetStatus => "Height target: not loaded";
    public string AlignmentTargetStatus => "Alignment target: not loaded";
    public string RepeatabilityInputStatus => loadedStudy is null
        ? repeatabilityOperationStatus
        : $"Repeatability runs: {loadedStudy.Input.Runs!.Count} | {Path.GetFileName(loadedStudy.Path)}";
    public string RepeatabilitySummary => evaluation is not null
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"N={evaluation.RunCount} | Mean {FormatNumber(evaluation.Mean)} | s {FormatNumber(evaluation.SampleStandardDeviation)} | 6s {FormatNumber(evaluation.SixSigmaSpread)} | Range {FormatNumber(evaluation.Range)} {loadedStudy!.Input.Unit} | {evaluation.Result.Status}")
        : loadedStudy is not null
            ? $"{loadedStudy.Input.Runs!.Count} runs | {loadedStudy.Input.ReferenceRoiId} | {repeatabilityOperationStatus}"
            : repeatabilityOperationStatus;
    public string RepeatabilityMapState => evaluation is null
        ? "Not calculated"
        : $"Aggregate result: {evaluation.Result.Status}";
    public string RepeatabilityMapHint => evaluation is not null
        ? "Per-point variation is unavailable for this representative-value study"
        : loadedStudy is not null
            ? "Verified study inputs are ready"
            : "Load repeated measurements to compare per-point variation";
    public string RepeatabilityChartState => evaluation is not null
        ? $"{evaluation.RunCount} included runs | {evaluation.Result.Status}"
        : loadedStudy is not null
            ? $"{loadedStudy.Input.Runs!.Count} verified runs"
            : "No repeatability runs";
    public string HeightCalibrationState => "Not calculated";
    public string AlignmentState => "Not calculated";
    public string RepeatabilityOverviewState => evaluation is not null
        ? $"{evaluation.Result.Status} | N={evaluation.RunCount}"
        : inputValidation?.IsReady == true
            ? $"Ready | N={inputValidation.RunCount}"
            : "No valid study";
    public string ProfileActivationState => "Blocked";
    public string HeightTargetEmptyState => "No height target loaded";
    public string AlignmentTargetEmptyState => "No alignment target loaded";
    public string EmptyRepeatabilityRunsText => "No repeatability runs";
    public string EmptyEvidenceText => "No calculation evidence";
    public string EmptyProfilesText => "No calibration profiles";
    public string ProfileNameValue => loadedStudy?.Input.StudyId ?? "Not set";
    public string SensorIdentityValue => "Not set";
    public string CalibrationTargetValue => loadedStudy is null
        ? "Not set"
        : $"{loadedStudy.Input.MeasurementDefinitionId} | {loadedStudy.Input.ReferenceRoiId}";
    public string UnitFrameValue => loadedStudy is null
        ? "Not set"
        : $"{loadedStudy.Input.Unit} | {loadedStudy.Input.FrameId}";
    public string MinimumRunCountValue => loadedStudy?.Input.Acceptance is { } acceptance
        ? acceptance.MinimumRunCount.ToString(CultureInfo.InvariantCulture)
        : "Not configured";
    public string StandardDeviationLimitValue => loadedStudy?.Input.Acceptance is { } acceptance
        ? Format(acceptance.MaximumSampleStandardDeviation, loadedStudy.Input.Unit)
        : "Not configured";
    public string RangeLimitValue => loadedStudy?.Input.Acceptance is { } acceptance
        ? Format(acceptance.MaximumRange, loadedStudy.Input.Unit)
        : "Not configured";
    public string RepeatabilityCalculationMessage => evaluation?.Result.Message ?? repeatabilityOperationStatus;
    public string ProfileLifecycleSummary => "Draft  >  Calculated  >  Validated  >  Active";
    public string SelectedRepeatabilityRunSummary => SelectedRepeatabilityRun is null
        ? "No repeatability run selected"
        : $"Run {SelectedRepeatabilityRun.RunNumber} | {SelectedRepeatabilityRun.RunId} | {SelectedRepeatabilityRun.ValueText} | {SelectedRepeatabilityRun.Status}";

    public bool LoadStudy(string path)
    {
        ResetRepeatabilityStudy();
        try
        {
            loadedStudy = ThicknessRepeatabilityStudyLoader.Load(path);
            inputValidation = ThicknessRepeatabilityRule.Validate(loadedStudy.Input);
            repeatabilityOperationStatus = inputValidation.Message;
            PopulateRunRows(inputValidation.IsReady ? "Ready" : "Invalid");
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
            PopulateRunRows("Invalid");
            NotifyRepeatabilityState();
            return;
        }

        evaluation = ThicknessRepeatabilityRule.Evaluate(inputValidation.Input);
        repeatabilityOperationStatus = evaluation.Result.Message;
        PopulateRunRows("Included");
        NotifyRepeatabilityState();
    }

    private void ResetRepeatabilityStudy()
    {
        loadedStudy = null;
        inputValidation = null;
        evaluation = null;
        repeatabilityOperationStatus = "No repeatability study loaded";
        SelectedRepeatabilityRun = null;
        RepeatabilityRuns.Clear();
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

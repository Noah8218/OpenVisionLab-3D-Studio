using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.Vision3D.FeatureExtraction;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public enum ResultsWorkspaceSection
{
    RunRecord,
    OutputCompare,
    Reports,
    Section,
    Log
}

public sealed class ResultsWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private ResultsWorkspaceSection activeSection = ResultsWorkspaceSection.RunRecord;
    private bool showOnlySelectedTool;
    private ResultsHistorySection historySection;
    private ResultsMeasurementOption? selectedMeasurement;
    private IReadOnlyList<RunRecordRecentItem> history = [];
    private ResultsRunSnapshot? openedRecord;
    private readonly HashSet<string> hiddenTeachingResultLabels = new(StringComparer.Ordinal);
    private readonly RelayCommand toggleTeachingResultLabelCommand;
    private IReadOnlyList<TeachingMeasurementResultItem> teachingResultRows = [];
    private string teachingResultContext = string.Empty;
    private bool disposed;

    public ResultsWorkspaceViewModel()
    {
        SelectSectionCommand = new RelayCommand(parameter => SelectSection(parameter));
        SelectHistorySectionCommand = new RelayCommand(SelectHistorySection);
        toggleTeachingResultLabelCommand = new RelayCommand(ToggleTeachingResultLabel, CanToggleTeachingResultLabel);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SelectSectionCommand { get; }
    public ICommand SelectHistorySectionCommand { get; }

    public ResultsWorkspaceSection ActiveSection
    {
        get => activeSection;
        private set
        {
            if (activeSection == value)
            {
                return;
            }

            activeSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsRunRecordSelected));
            OnPropertyChanged(nameof(IsOutputCompareSelected));
            OnPropertyChanged(nameof(IsReportsSelected));
            OnPropertyChanged(nameof(IsSectionSelected));
            OnPropertyChanged(nameof(IsLogSelected));
        }
    }

    public bool IsRunRecordSelected => ActiveSection == ResultsWorkspaceSection.RunRecord;
    public bool IsOutputCompareSelected => ActiveSection == ResultsWorkspaceSection.OutputCompare;
    public bool IsReportsSelected => ActiveSection == ResultsWorkspaceSection.Reports;
    public bool IsSectionSelected => ActiveSection == ResultsWorkspaceSection.Section;
    public bool IsLogSelected => ActiveSection == ResultsWorkspaceSection.Log;

    public bool ShowOnlySelectedTool
    {
        get => showOnlySelectedTool;
        set
        {
            if (showOnlySelectedTool == value) return;
            showOnlySelectedTool = value;
            OnPropertyChanged();
        }
    }

    public IReadOnlyList<ViewerTeachingResultLabel> TeachingResultLabels => teachingResultRows
        .Select(row => row.ResultLabel).Where(item => item.CanShow)
        .Select(item => item.Snapshot! with { IsVisible = item.IsVisible }).ToArray();
    public ICommand ToggleTeachingResultLabelCommand => toggleTeachingResultLabelCommand;

    public HeightImageViewerViewModel InputPreview { get; } =
        new(ThreeDLocalization.Shared, new SharedHeightCursorSession());
    public IReadOnlyList<ResultsMeasurementOption> Measurements { get; private set; } = [];
    public IReadOnlyList<ResultsTrendPoint> TrendPoints { get; private set; } = [];
    public IReadOnlyList<ResultsMeasurementStatistics> MeasurementStatistics { get; private set; } = [];
    public ResultsRunSnapshot? OpenedRecord => openedRecord;
    public bool HasOpenedRecord => openedRecord is not null;
    public bool HasTrendPoints => TrendPoints.Count > 0;
    public bool HasFailures => openedRecord?.Failures.Count > 0;
    public ResultsHistorySection HistorySection => historySection;
    public bool IsTrendSelected => historySection == ResultsHistorySection.Trend;
    public bool IsFailuresSelected => historySection == ResultsHistorySection.Failures;
    public bool IsReportSelected => historySection == ResultsHistorySection.Report;
    public string HistorySummary => (OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean)
        ? $"저장 기록 {history.Count}개 · 유한 측정값 {TrendPoints.Count}개"
        : $"{history.Count} saved records · {TrendPoints.Count} finite values";

    public ResultsMeasurementOption? SelectedMeasurement
    {
        get => selectedMeasurement;
        set
        {
            if (selectedMeasurement?.Key == value?.Key) return;
            selectedMeasurement = value;
            OnPropertyChanged();
            RefreshTrend();
        }
    }


    public void SelectSection(ResultsWorkspaceSection section) => ActiveSection = section;

    private void SelectSection(object? parameter)
    {
        if (parameter is ResultsWorkspaceSection section)
        {
            SelectSection(section);
            return;
        }

        if (Enum.TryParse<ResultsWorkspaceSection>(parameter?.ToString(), true, out section))
        {
            SelectSection(section);
        }
    }



    public void RefreshHistory(IReadOnlyList<RunRecordRecentItem> records)
    {
        history = records;
        Measurements = history.Where(item => item.Snapshot is not null)
            .SelectMany(item => item.Snapshot!.Metrics)
            .Where(metric => metric.IsFinite)
            .DistinctBy(metric => metric.Key)
            .Select(metric => new ResultsMeasurementOption(metric.Key, $"{metric.ToolName} · {metric.Name}", metric.Unit))
            .ToArray();
        selectedMeasurement = Measurements.FirstOrDefault(item => item.Key == selectedMeasurement?.Key)
            ?? Measurements.FirstOrDefault();
        OnPropertyChanged(nameof(Measurements));
        OnPropertyChanged(nameof(SelectedMeasurement));
        RefreshTrend();
    }

    public void SetOpenedRecord(InspectionRunRecord? record)
    {
        openedRecord = record is null ? null : ResultsRunSnapshot.FromRecord(record);
        OnPropertyChanged(nameof(OpenedRecord));
        OnPropertyChanged(nameof(HasOpenedRecord));
        OnPropertyChanged(nameof(HasFailures));
        InputPreview.ClearSource();
        if (openedRecord is not { } snapshot || disposed) return;
        InputPreview.StartObservedLoad(async () =>
        {
            await InputPreview.EnsureSourceAsync(snapshot.SourcePath, snapshot.SourceEntityId, snapshot.Unit, snapshot.FrameId);
            if (!ReferenceEquals(openedRecord, snapshot)) return;
            if (InputPreview.Frame is { } frame
                && !string.Equals(frame.SourceContentSha256, snapshot.SourceSha256, StringComparison.OrdinalIgnoreCase))
            {
                InputPreview.ReportSourceFailure((OpenVisionLanguageService.CurrentLanguage == OpenVisionLanguage.Korean)
                    ? "입력 파일이 실행 당시 기록과 다릅니다."
                    : "The input file differs from the saved run.");
            }
        }, exception => InputPreview.ReportSourceFailure(exception.Message));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        teachingResultRows = [];
        hiddenTeachingResultLabels.Clear();
        toggleTeachingResultLabelCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(TeachingResultLabels));
        InputPreview.Dispose();
    }

    private void RefreshTrend()
    {
        TrendPoints = selectedMeasurement is null ? [] : history
            .Where(item => item.Snapshot is not null)
            .OrderBy(item => item.Snapshot!.RecordedAt)
            .SelectMany(item => item.Snapshot!.Metrics
                .Where(metric => metric.Key == selectedMeasurement.Key && metric.IsFinite)
                .Select(metric => new ResultsTrendPoint(item, item.Snapshot!, metric)))
            .ToArray();
        MeasurementStatistics = TrendPoints.GroupBy(point => (point.Metric.SettingsIdentity, point.Snapshot.FrameId, point.Metric.Unit))
            .Select(group =>
            {
                var points = group.DistinctBy(point => (point.Snapshot.RunId, point.Metric.Key)).ToArray();
                var values = points.Select(point => point.Metric.Value).ToArray();
                var summary = new HeightDistributionStatisticsTool().Execute(values, new HeightDistributionStatisticsOptions { BinCount = 1, ZeroIsMissing = false });
                var repeatability = values.Length >= 2 ? new RepeatabilityStatisticsTool().Execute(values, RepeatabilityNegativeVariancePolicy.ClampAnyNegative) : null;
                return new ResultsMeasurementStatistics(points[0].Snapshot.RecipeIdentity, group.Key.SettingsIdentity, group.Key.FrameId, group.Key.Unit,
                    values.Length, summary.Success ? summary.Mean : null, summary.Success ? summary.Minimum : null, summary.Success ? summary.Maximum : null,
                    repeatability?.Success == true ? repeatability.SampleStandardDeviation : null,
                    points.Count(point => point.Metric.Status == ResultStatus.Fail), points.Count(point => point.Metric.ExecutionStatus == ResultStatus.Error));
            }).ToArray();
        OnPropertyChanged(nameof(MeasurementStatistics));
        OnPropertyChanged(nameof(TrendPoints));
        OnPropertyChanged(nameof(HasTrendPoints));
        OnPropertyChanged(nameof(HistorySummary));
    }

    private void SelectHistorySection(object? value)
    {
        if (!Enum.TryParse<ResultsHistorySection>(value?.ToString(), true, out var section) || !Enum.IsDefined(section)) return;
        historySection = section;
        OnPropertyChanged(nameof(HistorySection));
        OnPropertyChanged(nameof(IsTrendSelected));
        OnPropertyChanged(nameof(IsFailuresSelected));
        OnPropertyChanged(nameof(IsReportSelected));
    }

    public void RefreshTeachingResultLabels(IReadOnlyList<TeachingMeasurementResultItem> rows,
        IReadOnlyList<ToolRecipeSelection> selections, string recipeContext)
    {
        if (disposed) return;
        if (teachingResultContext != recipeContext) hiddenTeachingResultLabels.Clear();
        teachingResultContext = recipeContext;
        teachingResultRows = rows.ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in teachingResultRows)
        {
            var id = row.Step.DualRoiRouting?.SecondRegionSelectionId;
            var anchor = id is not null ? selections.FirstOrDefault(selection => selection.Id == id)
                : row.Step.FlowInputs.Select(input => input.Selection).FirstOrDefault(selection => selection?.GridRectangle is not null);
            var metric = ToolWorkbenchViewModel.SelectPrimaryMeasurementMetric(row.Step.ToolId, row.Metrics);
            var isError = row.ExecutionStatus is ResultStatus.Error || row.State is "Error" or "오류";
            ViewerTeachingResultLabel? snapshot = null;
            if (row.Step.OutputContract == "MeasurementResult" && anchor is { GridRectangle: not null }
                && (metric is not null && double.IsFinite(metric.Value) || isError))
            {
                snapshot = new(row.Step.Id, row.Step.ToolName, metric?.Name ?? row.OutputName,
                    isError ? null : metric?.Value, isError ? ThreeDLocalization.Shared.Resolve("ThreeD.Results.Error", "오류", "Error") : row.DisplayValue,
                    row.Unit, row.ExecutionStatus, row.IsCurrent, anchor);
                if (!snapshot.IsValid) snapshot = null;
            }
            var key = snapshot is null ? string.Empty : TeachingResultLabelKey(snapshot);
            if (snapshot is not null) keys.Add(key);
            row.ResultLabel.Update(snapshot, hiddenTeachingResultLabels.Contains(key));
        }
        hiddenTeachingResultLabels.IntersectWith(keys);
        toggleTeachingResultLabelCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(TeachingResultLabels));
    }

    private void ToggleTeachingResultLabel(object? parameter)
    {
        if (!CanToggleTeachingResultLabel(parameter) || parameter is not TeachingResultLabelItem { Snapshot: { } snapshot } item) return;
        var key = TeachingResultLabelKey(snapshot);
        if (!hiddenTeachingResultLabels.Add(key)) hiddenTeachingResultLabels.Remove(key);
        item.Update(snapshot, hiddenTeachingResultLabels.Contains(key));
        OnPropertyChanged(nameof(TeachingResultLabels));
    }

    private bool CanToggleTeachingResultLabel(object? parameter) => !disposed && parameter is TeachingResultLabelItem { CanShow: true } item
        && teachingResultRows.Any(row => ReferenceEquals(row.ResultLabel, item));
    private static string TeachingResultLabelKey(ViewerTeachingResultLabel label) => $"{label.Anchor.SourceBinding.ContentSha256}|{label.StepId}";

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum ValidationWorkspaceSection
{
    Samples,
    Results,
    Failures,
    Thresholds,
    HeldOut
}

public sealed class RecipePipelineReviewValidationViewModel : INotifyPropertyChanged
{
    private readonly ToolWorkbenchViewModel workbench;
    private ValidationWorkspaceSection section = ValidationWorkspaceSection.Samples;

    public RecipePipelineReviewValidationViewModel(ToolWorkbenchViewModel workbench)
    {
        this.workbench = workbench ?? throw new ArgumentNullException(nameof(workbench));
        SelectSectionCommand = new RelayCommand(parameter => SelectSection(parameter));
        OpenMismatchDistributionCommand = new RelayCommand(parameter =>
        {
            if (!workbench.SelectValidationMismatchDistributionCommand.CanExecute(parameter)) return;
            workbench.SelectValidationMismatchDistributionCommand.Execute(parameter);
            SelectSection(ValidationWorkspaceSection.Thresholds);
        }, parameter => workbench.SelectValidationMismatchDistributionCommand.CanExecute(parameter));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SelectSectionCommand { get; }
    public ICommand OpenMismatchDistributionCommand { get; }
    internal void RefreshMismatchCommandStates() => ((RelayCommand)OpenMismatchDistributionCommand).RaiseCanExecuteChanged();

    public ValidationWorkspaceSection Section
    {
        get => section;
        private set
        {
            if (section == value)
            {
                return;
            }

            section = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSamplesSelected));
            OnPropertyChanged(nameof(IsResultsSelected));
            OnPropertyChanged(nameof(IsFailuresSelected));
            OnPropertyChanged(nameof(IsThresholdsSelected));
            OnPropertyChanged(nameof(IsHeldOutSelected));
        }
    }

    public bool IsSamplesSelected => Section == ValidationWorkspaceSection.Samples;
    public bool IsResultsSelected => Section == ValidationWorkspaceSection.Results;
    public bool IsFailuresSelected => Section == ValidationWorkspaceSection.Failures;
    public bool IsThresholdsSelected => Section == ValidationWorkspaceSection.Thresholds;
    public bool IsHeldOutSelected => Section == ValidationWorkspaceSection.HeldOut;

    public void SelectSection(ValidationWorkspaceSection value)
    {
        Section = value;

        if (value == ValidationWorkspaceSection.Samples
            && workbench.ValidationSetFilter != ValidationSetStatusFilter.All)
        {
            workbench.SetValidationSetFilterCommand.Execute("All");
        }

        if (value == ValidationWorkspaceSection.Failures
            && workbench.SelectedValidationSetSample?.IsMismatch != true
            && workbench.SelectedValidationSetSample?.Status is not ("Fail" or "Error"))
        {
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.FirstOrDefault(
                sample => sample.IsMismatch || sample.Status is "Fail" or "Error");
        }
    }

    private void SelectSection(object? parameter)
    {
        if (parameter is ValidationWorkspaceSection value)
        {
            SelectSection(value);
            return;
        }

        if (Enum.TryParse<ValidationWorkspaceSection>(parameter?.ToString(), true, out value))
        {
            SelectSection(value);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

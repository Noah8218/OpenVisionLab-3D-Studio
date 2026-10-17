using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the review-only Validation Set projection, selection, filtering, and
/// issue navigation. Canonical samples, execution, persistence, role changes,
/// threshold policy, and Output Compare mutation remain outside this owner.
/// </summary>
internal sealed class ToolWorkbenchValidationSetReviewOwner : INotifyPropertyChanged
{
    private readonly ObservableCollection<ValidationSetSampleRow> samples;
    private readonly Func<bool> isRunning;
    private readonly Func<bool> isSourceReady;
    private readonly Action openComparison;
    private readonly ObservableCollection<ValidationSetSampleRow> filteredSamples = [];
    private readonly ObservableCollection<ValidationSetStepRow> selectedSteps = [];
    private readonly RelayCommand setFilterCommand;
    private readonly RelayCommand previousIssueCommand;
    private readonly RelayCommand nextIssueCommand;
    private readonly RelayCommand openComparisonCommand;
    private readonly RelayCommand selectMismatchCommand;
    private readonly RelayCommand selectMismatchDistributionCommand;
    private ValidationSetSampleRow? selectedSample;
    private ValidationSetStepRow? selectedStep;
    private IReadOnlyList<ValidationDistributionOutput> distributionOutputs = [];
    private ValidationDistributionOutput? selectedDistributionOutput;
    private ValidationSetStatusFilter filter = ValidationSetStatusFilter.All;

    public ToolWorkbenchValidationSetReviewOwner(
        ObservableCollection<ValidationSetSampleRow> samples,
        Func<bool> isRunning,
        Func<bool> isSourceReady,
        Action openComparison)
    {
        this.samples = samples ?? throw new ArgumentNullException(nameof(samples));
        this.isRunning = isRunning ?? throw new ArgumentNullException(nameof(isRunning));
        this.isSourceReady = isSourceReady ?? throw new ArgumentNullException(nameof(isSourceReady));
        this.openComparison = openComparison ?? throw new ArgumentNullException(nameof(openComparison));

        ValidationSetSamples = new ReadOnlyObservableCollection<ValidationSetSampleRow>(
            filteredSamples);
        SelectedValidationSetSteps = new ReadOnlyObservableCollection<ValidationSetStepRow>(
            selectedSteps);
        setFilterCommand = new RelayCommand(parameter =>
            SetFilter(parameter?.ToString()));
        previousIssueCommand = new RelayCommand(
            _ => MoveIssue(-1),
            _ => CanNavigateIssues);
        nextIssueCommand = new RelayCommand(
            _ => MoveIssue(1),
            _ => CanNavigateIssues);
        selectMismatchCommand = new RelayCommand(parameter =>
        {
            if (parameter is ValidationSetSampleRow sample && samples.Contains(sample))
            {
                SelectedValidationSetSample = sample;
                SelectedValidationSetStep = sample.MismatchStep ?? sample.ReviewOutputStep;
            }
        }, parameter => !isRunning() && parameter is ValidationSetSampleRow sample && samples.Contains(sample) && File.Exists(sample.SourcePath));
        selectMismatchDistributionCommand = new RelayCommand(parameter =>
        {
            if (parameter is not ValidationSetSampleRow sample || !selectMismatchCommand.CanExecute(sample)) return;
            selectMismatchCommand.Execute(sample);
            var step = sample.ReviewOutputStep;
            var metric = sample.ReviewOutputMetric;
            SelectedValidationDistributionOutput = distributionOutputs.FirstOrDefault(output => output.Column.StepId == step?.StepId
                && output.Column.MetricName == metric?.Name && output.Column.Unit == metric?.Unit);
        }, parameter => selectMismatchCommand.CanExecute(parameter) && parameter is ValidationSetSampleRow sample && sample.HasReviewOutput);
        openComparisonCommand = new RelayCommand(
            parameter =>
            {
                if (parameter is ValidationSetSampleRow sample && samples.Contains(sample)) SelectedValidationSetSample = sample;
                openComparison();
            },
            parameter => !isRunning() && (parameter is not ValidationSetSampleRow row || samples.Contains(row))
                 && (parameter as ValidationSetSampleRow ?? SelectedValidationSetSample) is { SourcePath: var path }
                 && File.Exists(path)
                 && isSourceReady());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ValidationSetSampleRow> ValidationSetSamples { get; }

    public ReadOnlyObservableCollection<ValidationSetStepRow> SelectedValidationSetSteps { get; }

    public IReadOnlyList<ValidationMatrixColumn> ValidationMatrixColumns => samples.SelectMany(sample => sample.Steps.SelectMany(step =>
        step.Metrics.Where(metric => !string.IsNullOrWhiteSpace(metric.Status)).Select(metric => new ValidationMatrixColumn(step.StepId, step.ToolName, metric.Name, metric.Unit, step.Order))))
        .DistinctBy(column => (column.StepId, column.MetricName, column.Unit)).ToArray();

    public IReadOnlyList<ValidationTableColumn> ValidationTableColumns
    {
        get
        {
            var snapshot = samples.Select(sample => sample.RecipeSteps).FirstOrDefault(steps => steps is not null) ?? [];
            var columns = new List<ValidationTableColumn>();
            var grouped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var flat = ValidationMatrixColumns;
            foreach (var step in snapshot.Where(step => step.OutputEnabled))
            {
                if (step.Group is { } group)
                {
                    if (!grouped.Add(group.Id)) continue;
                    var members = snapshot.Where(member => string.Equals(member.Group?.Id, group.Id, StringComparison.OrdinalIgnoreCase) && member.OutputEnabled).ToArray();
                    if (members.Length > 0) columns.Add(new ValidationTableColumn($"{group.Name} ×{members.Length}", null, members));
                }
                else foreach (var metric in flat.Where(column => column.StepId == step.StepId))
                    columns.Add(new ValidationTableColumn(metric.ToolName + " · " + metric.MetricName, metric));
            }
            if (snapshot.Count == 0) columns.AddRange(flat.Select(metric => new ValidationTableColumn(metric.ToolName + " · " + metric.MetricName, metric)));
            return columns;
        }
    }

    public IReadOnlyList<ValidationSetSampleRow> ValidationMismatchSamples => samples.Where(sample => sample.IsMismatch).ToArray();
    public bool HasValidationMismatches => samples.Any(sample => sample.IsMismatch);
    public bool HasValidationResults => samples.Any(sample => sample.Status != "Pending");
    public IReadOnlyList<ValidationDistributionOutput> ValidationDistributionOutputs => distributionOutputs;
    public string SelectedValidationDistributionReadout
    {
        get
        {
            var localization = ThreeDLocalization.Shared;
            if (selectedSample is null) return localization.Resolve("ThreeD.Validation.SelectSampleReadout", "샘플을 선택하세요.", "Select a sample.");
            var column = selectedDistributionOutput?.Column;
            var step = column is null ? null : selectedSample.Steps.FirstOrDefault(item => item.StepId == column.StepId);
            var metric = step?.Metrics.FirstOrDefault(item => item.Name == column!.MetricName && item.Unit == column.Unit);
            var lower = step?.Metrics.FirstOrDefault(item => item.Name == "LowerLimit" && item.Unit == column!.Unit);
            var upper = step?.Metrics.FirstOrDefault(item => item.Name == "UpperLimit" && item.Unit == column!.Unit);
            var state = step?.Status == "Error" || column is null && selectedSample.Status == "Error" ? "Error" : metric?.Status;
            var status = state switch
            {
                "Pass" => "✓ OK", "Fail" => "× NG",
                "Error" => localization.Resolve("ThreeD.Validation.ReadoutError", "오류", "Error"),
                _ => localization.Resolve("ThreeD.Validation.ReadoutNoResult", "결과 없음", "No result")
            };
            var value = metric is null ? localization.Resolve("ThreeD.Validation.ReadoutNoValue", "값 없음", "No value") : $"{metric.Value} {metric.Unit}";
            var range = lower is null && upper is null
                ? localization.Resolve("ThreeD.Validation.ReadoutNoLimits", "기록된 허용 범위 없음", "No recorded limits")
                : $"{lower?.Value ?? "—"} – {upper?.Value ?? "—"} {column!.Unit}";
            var output = selectedDistributionOutput?.Label ?? localization.Resolve("ThreeD.Validation.ReadoutNoOutput", "기록된 판정 출력 없음", "No recorded judged output");
            return $"{selectedSample.FileName} · {output}\n"
                + $"{localization.Resolve("ThreeD.Validation.ReadoutValue", "실행 값", "Executed value")}: {value} · {status}\n"
                + $"{localization.Resolve("ThreeD.Validation.ReadoutLimits", "실행 허용 범위", "Executed limits")}: {range}";
        }
    }

    public ValidationDistributionOutput? SelectedValidationDistributionOutput
    {
        get => selectedDistributionOutput;
        set
        {
            if (ReferenceEquals(selectedDistributionOutput, value)) return;
            selectedDistributionOutput = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedValidationDistributionReadout));
        }
    }

    public ICommand SetValidationSetFilterCommand => setFilterCommand;

    public ICommand PreviousValidationSetIssueCommand => previousIssueCommand;

    public ICommand NextValidationSetIssueCommand => nextIssueCommand;

    public ICommand OpenValidationSetComparisonCommand => openComparisonCommand;
    public ICommand SelectValidationMismatchSampleCommand => selectMismatchCommand;
    public ICommand SelectValidationMismatchDistributionCommand => selectMismatchDistributionCommand;

    public ValidationSetSampleRow? SelectedValidationSetSample
    {
        get => selectedSample;
        set
        {
            if (ReferenceEquals(selectedSample, value))
            {
                return;
            }

            selectedSample = value;
            selectedSteps.Clear();
            foreach (var step in value?.Steps ?? [])
            {
                selectedSteps.Add(step);
            }

            SelectedValidationSetStep =
                value?.Steps.FirstOrDefault(step => step.Status is "Fail" or "Error")
                ?? value?.Steps.FirstOrDefault();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedValidationSetSample));
            OnPropertyChanged(nameof(IsSelectedValidationRoleGood));
            OnPropertyChanged(nameof(IsSelectedValidationRoleBad));
            OnPropertyChanged(nameof(IsSelectedValidationRoleHeldOut));
            OnPropertyChanged(nameof(SelectedValidationDistributionReadout));
            RefreshCommandStates();
        }
    }

    public ValidationSetStepRow? SelectedValidationSetStep
    {
        get => selectedStep;
        set
        {
            if (ReferenceEquals(selectedStep, value))
            {
                return;
            }

            selectedStep = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelectedValidationSetStep));
        }
    }

    public ValidationSetStatusFilter ValidationSetFilter => filter;

    public bool IsValidationSetFilterAll => filter == ValidationSetStatusFilter.All;

    public bool IsValidationSetFilterPass => filter == ValidationSetStatusFilter.Pass;

    public bool IsValidationSetFilterFail => filter == ValidationSetStatusFilter.Fail;

    public bool IsValidationSetFilterError => filter == ValidationSetStatusFilter.Error;

    public bool IsValidationSetFilterMismatch => filter == ValidationSetStatusFilter.Mismatch;
    public bool IsValidationSetFilterFalseReject => filter == ValidationSetStatusFilter.FalseReject;
    public bool IsValidationSetFilterFalseAccept => filter == ValidationSetStatusFilter.FalseAccept;
    public bool IsValidationSetFilterHeldOut => filter == ValidationSetStatusFilter.HeldOut;

    public int ValidationSetAdjustmentCount => samples.Count(sample => sample.IsAdjustment && sample.HasExpectation);
    public int ValidationSetMatchCount => samples.Count(sample => sample.IsAdjustment && sample.Agreement == "Match");
    public int ValidationSetMismatchCount => samples.Count(sample => sample.IsMismatch);
    public int ValidationSetFalseRejectCount => samples.Count(sample => sample.IsAdjustment && sample.Agreement == "FalseReject");
    public int ValidationSetFalseAcceptCount => samples.Count(sample => sample.IsAdjustment && sample.Agreement == "FalseAccept");
    public int ValidationSetAdjustmentErrorCount => samples.Count(sample => sample.IsAdjustment && sample.HasExpectation && sample.Status == "Error");
    public int ValidationSetAdjustmentPendingCount => samples.Count(sample => sample.IsAdjustment && sample.HasExpectation && sample.Status is "Pending" or "Warning");
    public int ValidationSetHeldOutCompletedCount => samples.Count(sample => sample.IsHeldOut && sample.Status != "Pending");
    public int ValidationSetHeldOutPendingCount => samples.Count(sample => sample.IsHeldOut && sample.Status == "Pending");

    public bool HasSelectedValidationSetSample => SelectedValidationSetSample is not null;

    public bool HasSelectedValidationSetStep => SelectedValidationSetStep is not null;

    public bool IsSelectedValidationRoleGood =>
        SelectedValidationSetSample?.IsGood == true;

    public bool IsSelectedValidationRoleBad =>
        SelectedValidationSetSample?.IsBad == true;

    public bool IsSelectedValidationRoleHeldOut =>
        SelectedValidationSetSample?.IsHeldOut == true;

    public void SetFilter(ValidationSetStatusFilter value)
    {
        if (filter == value)
        {
            return;
        }

        filter = value;
        OnPropertyChanged(nameof(ValidationSetFilter));
        OnPropertyChanged(nameof(IsValidationSetFilterAll));
        OnPropertyChanged(nameof(IsValidationSetFilterPass));
        OnPropertyChanged(nameof(IsValidationSetFilterFail));
        OnPropertyChanged(nameof(IsValidationSetFilterError));
        OnPropertyChanged(nameof(IsValidationSetFilterMismatch));
        OnPropertyChanged(nameof(IsValidationSetFilterFalseReject));
        OnPropertyChanged(nameof(IsValidationSetFilterFalseAccept));
        OnPropertyChanged(nameof(IsValidationSetFilterHeldOut));
        RefreshSamples();
    }

    public void RefreshSamples()
    {
        var selectedPath = SelectedValidationSetSample?.SourcePath;
        filteredSamples.Clear();
        foreach (var sample in samples.Where(MatchesFilter))
        {
            filteredSamples.Add(sample);
        }

        SelectedValidationSetSample = selectedPath is null
            ? filteredSamples.FirstOrDefault()
            : filteredSamples.FirstOrDefault(sample =>
                string.Equals(sample.SourcePath, selectedPath, StringComparison.OrdinalIgnoreCase))
              ?? filteredSamples.FirstOrDefault();
        RefreshCommandStates();
        OnPropertyChanged(nameof(ValidationMatrixColumns));
        OnPropertyChanged(nameof(ValidationTableColumns));
        RefreshDistributions();
        OnPropertyChanged(nameof(SelectedValidationDistributionReadout));
        OnPropertyChanged(nameof(ValidationMismatchSamples));
        OnPropertyChanged(nameof(HasValidationMismatches));
        OnPropertyChanged(nameof(HasValidationResults));
        OnPropertyChanged(nameof(ValidationSetAdjustmentCount));
        OnPropertyChanged(nameof(ValidationSetMatchCount));
        OnPropertyChanged(nameof(ValidationSetMismatchCount));
        OnPropertyChanged(nameof(ValidationSetFalseRejectCount));
        OnPropertyChanged(nameof(ValidationSetFalseAcceptCount));
        OnPropertyChanged(nameof(ValidationSetAdjustmentErrorCount));
        OnPropertyChanged(nameof(ValidationSetAdjustmentPendingCount));
        OnPropertyChanged(nameof(ValidationSetHeldOutCompletedCount));
        OnPropertyChanged(nameof(ValidationSetHeldOutPendingCount));
    }

    private void RefreshDistributions()
    {
        var previousColumn = selectedDistributionOutput?.Column;
        distributionOutputs = ValidationMatrixColumns.Select(column =>
        {
            var observed = samples.Where(sample => sample.HasExpectation).Select(sample => (Sample: sample, Step: sample.Steps.FirstOrDefault(step => step.StepId == column.StepId))).ToArray();
            var points = observed.Select(item => (item.Sample, Metric: item.Step?.Metrics.FirstOrDefault(metric => metric.Name == column.MetricName && metric.Unit == column.Unit)))
                .Where(item => item.Metric?.NumericValue is { } value && double.IsFinite(value))
                .Select(item => new ValidationDistributionPoint(item.Sample, item.Metric!.NumericValue!.Value, item.Metric.Value, item.Metric.Status)).ToArray();
            // Use execution evidence only. Differing or missing ranges never inherit live recipe settings.
            double? Bound(string name)
            {
                var values = observed.Select(item => item.Step?.Metrics.FirstOrDefault(metric => metric.Name == name && metric.Unit == column.Unit)?.NumericValue).Distinct().ToArray();
                return values.Length == 1 && values[0] is { } value && double.IsFinite(value) ? value : null;
            }
            return new ValidationDistributionOutput(column, points, Bound("LowerLimit"), Bound("UpperLimit"));
        }).ToArray();
        OnPropertyChanged(nameof(ValidationDistributionOutputs));
        SelectedValidationDistributionOutput = distributionOutputs.FirstOrDefault(output => output.Column == previousColumn) ?? distributionOutputs.FirstOrDefault();
    }

    public void RefreshCommandStates()
    {
        previousIssueCommand.RaiseCanExecuteChanged();
        nextIssueCommand.RaiseCanExecuteChanged();
        openComparisonCommand.RaiseCanExecuteChanged();
        selectMismatchCommand.RaiseCanExecuteChanged();
        selectMismatchDistributionCommand.RaiseCanExecuteChanged();
    }

    private bool CanNavigateIssues => !isRunning() && VisibleIssues().Count > 0;

    private void SetFilter(string? value)
    {
        if (Enum.TryParse<ValidationSetStatusFilter>(value, ignoreCase: true, out var parsed))
        {
            SetFilter(parsed);
        }
    }

    private bool MatchesFilter(ValidationSetSampleRow sample) =>
        filter switch
        {
            ValidationSetStatusFilter.Pass => sample.Status == "Pass",
            ValidationSetStatusFilter.Fail => sample.Status == "Fail",
            ValidationSetStatusFilter.Error => sample.Status == "Error",
            ValidationSetStatusFilter.Mismatch => sample.IsMismatch,
            ValidationSetStatusFilter.FalseReject => sample.Agreement == "FalseReject",
            ValidationSetStatusFilter.FalseAccept => sample.Agreement == "FalseAccept",
            ValidationSetStatusFilter.HeldOut => sample.IsHeldOut,
            _ => true
        };

    private IReadOnlyList<ValidationSetSampleRow> VisibleIssues() =>
        filteredSamples
            .Where(sample => sample.IsMismatch || sample.Status is "Fail" or "Error")
            .ToArray();

    private void MoveIssue(int offset)
    {
        var issues = VisibleIssues();
        if (issues.Count == 0)
        {
            return;
        }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

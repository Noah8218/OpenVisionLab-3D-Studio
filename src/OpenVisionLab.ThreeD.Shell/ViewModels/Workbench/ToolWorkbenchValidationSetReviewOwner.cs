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
    private ValidationSetSampleRow? selectedSample;
    private ValidationSetStepRow? selectedStep;
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
        openComparisonCommand = new RelayCommand(
            _ => openComparison(),
            _ => !isRunning()
                 && SelectedValidationSetSample is { SourcePath: var path }
                 && File.Exists(path)
                 && isSourceReady());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReadOnlyObservableCollection<ValidationSetSampleRow> ValidationSetSamples { get; }

    public ReadOnlyObservableCollection<ValidationSetStepRow> SelectedValidationSetSteps { get; }

    public ICommand SetValidationSetFilterCommand => setFilterCommand;

    public ICommand PreviousValidationSetIssueCommand => previousIssueCommand;

    public ICommand NextValidationSetIssueCommand => nextIssueCommand;

    public ICommand OpenValidationSetComparisonCommand => openComparisonCommand;

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

    public bool HasSelectedValidationSetSample => SelectedValidationSetSample is not null;

    public bool HasSelectedValidationSetStep => SelectedValidationSetStep is not null;

    public bool IsSelectedValidationRoleGood =>
        SelectedValidationSetSample?.Role == ToolRecipeValidationSampleRole.Good;

    public bool IsSelectedValidationRoleBad =>
        SelectedValidationSetSample?.Role == ToolRecipeValidationSampleRole.Bad;

    public bool IsSelectedValidationRoleHeldOut =>
        SelectedValidationSetSample?.Role == ToolRecipeValidationSampleRole.HeldOut;

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
    }

    public void RefreshCommandStates()
    {
        previousIssueCommand.RaiseCanExecuteChanged();
        nextIssueCommand.RaiseCanExecuteChanged();
        openComparisonCommand.RaiseCanExecuteChanged();
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
            _ => true
        };

    private IReadOnlyList<ValidationSetSampleRow> VisibleIssues() =>
        filteredSamples
            .Where(sample => sample.Status is "Fail" or "Error")
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

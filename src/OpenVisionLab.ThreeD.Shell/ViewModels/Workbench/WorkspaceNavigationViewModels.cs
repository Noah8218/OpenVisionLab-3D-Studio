using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public enum ResultsWorkspaceSection
{
    RunRecord,
    OutputCompare,
    Reports
}

public sealed class ResultsWorkspaceViewModel : INotifyPropertyChanged
{
    private ResultsWorkspaceSection activeSection = ResultsWorkspaceSection.RunRecord;

    public ResultsWorkspaceViewModel()
    {
        SelectSectionCommand = new RelayCommand(parameter => SelectSection(parameter));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SelectSectionCommand { get; }

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
        }
    }

    public bool IsRunRecordSelected => ActiveSection == ResultsWorkspaceSection.RunRecord;
    public bool IsOutputCompareSelected => ActiveSection == ResultsWorkspaceSection.OutputCompare;
    public bool IsReportsSelected => ActiveSection == ResultsWorkspaceSection.Reports;

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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand SelectSectionCommand { get; }

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
            && workbench.SelectedValidationSetSample?.Status is not ("Fail" or "Error"))
        {
            workbench.SelectedValidationSetSample = workbench.ValidationSetSamples.FirstOrDefault(
                sample => sample.Status is "Fail" or "Error");
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

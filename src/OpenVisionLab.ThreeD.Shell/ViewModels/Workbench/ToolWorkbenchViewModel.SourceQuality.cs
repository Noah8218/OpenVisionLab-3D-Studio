using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private RelayCommand selectSourceQualityCommand = null!;

    public SourceQualityWorkspaceViewModel SourceQuality { get; private set; } = null!;

    public ICommand SelectSourceQualityCommand => selectSourceQualityCommand;

    public bool IsCurrentSourceQualityStatusVisible =>
        !string.IsNullOrWhiteSpace(Source.Path);

    public string CurrentSourceQualityStatusKind =>
        !IsCurrentSourceQualityStatusVisible
            ? "Unavailable"
            : SourceQuality.IsLoading
                ? "Loading"
                : SourceQuality.HasError
                    ? "Error"
                    : SourceQuality.Report is { Coverage.MissingSampleCount: > 0 }
                        ? "Warning"
                        : SourceQuality.HasReport
                            ? "Pass"
                            : "Unavailable";

    public string CurrentSourceQualitySummary => SourceQuality.Report is { } report
        ? string.Format(
            CultureInfo.InvariantCulture,
            Localization.CurrentSourceQualitySummaryFormat,
            report.Coverage.ValidRatio,
            report.Coverage.MissingRatio)
        : $"{Localization.SourceQuality}: {SourceQuality.State}";

    public string CurrentSourceQualityDetail => SourceQuality.Report is not null
        ? string.Concat(
            string.Format(
                CultureInfo.InvariantCulture,
                Localization.CurrentSourceQualityDetailFormat,
                SourceQuality.GridValue,
                SourceQuality.ValidValue,
                SourceQuality.MissingValue),
            Environment.NewLine,
            Localization.SourceQualityViewOnly)
        : SourceQuality.HasError
            ? $"{SourceQuality.State}: {SourceQuality.Error}"
            : SourceQuality.State;

    public bool IsSourceQualityWorkspaceVisible =>
        !HasSelectedPipelineStep
        && !string.IsNullOrWhiteSpace(Source.Path)
        && SourceQuality.IsAvailableOrLoading;

    public string SelectedWorkspaceTitle => HasSelectedPipelineStep
        ? SelectedToolWorkspace.Title
        : IsSourceQualityWorkspaceVisible
            ? Localization.SourceQuality
            : SelectedToolWorkspace.Title;

    public string SelectedWorkspaceState => HasSelectedPipelineStep
        ? SelectedToolWorkspace.State
        : IsSourceQualityWorkspaceVisible
            ? SourceQuality.State
            : SelectedToolWorkspace.State;

    private void InitializeSourceQualityWorkspace()
    {
        SourceQuality = new SourceQualityWorkspaceViewModel(Localization);
        SourceQuality.PropertyChanged += OnSourceQualityPropertyChanged;
        selectSourceQualityCommand = new RelayCommand(
            _ => SelectSourceQualityWorkspace(),
            _ => !HasPendingStepParameterChanges
                 && !string.IsNullOrWhiteSpace(Source.Path)
                 && SourceQuality.IsAvailableOrLoading);
    }

    private void SelectSourceQualityWorkspace()
    {
        SelectedPipelineStep = null;
        if (SelectedPipelineStep is not null)
        {
            return;
        }

        WorkspaceSelection.ClearRecipeSelection();
        NotifySourceQualityWorkspaceState();
    }

    private void BeginSourceQualityLoad()
    {
        if (string.IsNullOrWhiteSpace(Source.Path))
        {
            SourceQuality.Clear();
            return;
        }

        _ = SourceQuality.EnsureSourceAsync(
            Source.Path,
            Source.Id,
            Source.Unit,
            Source.FrameId);
    }

    private void OnSourceQualityPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SourceQualityWorkspaceViewModel.IsLoading)
            or nameof(SourceQualityWorkspaceViewModel.HasReport)
            or nameof(SourceQualityWorkspaceViewModel.HasError)
            or nameof(SourceQualityWorkspaceViewModel.IsAvailableOrLoading)
            or nameof(SourceQualityWorkspaceViewModel.State))
        {
            NotifySourceQualityWorkspaceState();
        }
    }

    private void NotifySourceQualityWorkspaceState()
    {
        OnPropertyChanged(nameof(IsSourceQualityWorkspaceVisible));
        OnPropertyChanged(nameof(SelectedWorkspaceTitle));
        OnPropertyChanged(nameof(SelectedWorkspaceState));
        OnPropertyChanged(nameof(IsCurrentSourceQualityStatusVisible));
        OnPropertyChanged(nameof(CurrentSourceQualityStatusKind));
        OnPropertyChanged(nameof(CurrentSourceQualitySummary));
        OnPropertyChanged(nameof(CurrentSourceQualityDetail));
        selectSourceQualityCommand.RaiseCanExecuteChanged();
    }
}

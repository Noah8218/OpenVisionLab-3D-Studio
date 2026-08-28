using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

public sealed partial class ToolWorkbenchViewModel
{
    private readonly System.Windows.Threading.Dispatcher? sourceQualityUiDispatcher;
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
                    : SourceQuality.HasGridDiagnosticError
                        ? "Error"
                    : SourceQuality.Report is { Coverage.MissingSampleCount: > 0 }
                        ? "Warning"
                        : SourceQuality.HasReport
                            ? "Pass"
                            : "Unavailable";

    public string CurrentSourceQualitySummary => SourceQuality.Report is { } report
        ? string.Concat(
            SourceQuality.GridDiagnosticsStatus,
            " · ",
            string.Format(
                CultureInfo.InvariantCulture,
                Localization.CurrentSourceQualitySummaryFormat,
                report.Coverage.ValidRatio,
                report.Coverage.MissingRatio)
                .Replace(" %", "\u00A0%", StringComparison.Ordinal))
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
            SourceQuality.GridDiagnosticsSummary,
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
        SourceQuality = new SourceQualityWorkspaceViewModel(
            Localization,
            ApplySourceAcquisitionProvenance);
        SourceQuality.PropertyChanged += OnSourceQualityPropertyChanged;
        selectSourceQualityCommand = new RelayCommand(
            _ => SelectSourceQualityWorkspace(),
            _ => !HasPendingStepParameterChanges
                 && !string.IsNullOrWhiteSpace(Source.Path)
                 && SourceQuality.IsAvailableOrLoading);
    }

    private void ApplySourceAcquisitionProvenance(
        ToolRecipeAcquisitionProvenance provenance)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        var directionChanged = SourceSession.SourceAcquisitionProvenance?.AcquisitionDirection
            != provenance.AcquisitionDirection;
        var changed = SourceSession.SetSourceAcquisitionProvenance(provenance);
        if (changed)
        {
            MutateRecipe(() => { });
        }
        SourceQuality.LoadAcquisitionProvenance(SourceSession.SourceAcquisitionProvenance, Source.FrameId);
        if (directionChanged)
        {
            InvalidateSurfaceEdgeAcquisitionDirectionEvidence();
        }
        OnPropertyChanged(nameof(SourceAcquisitionProvenance));
    }

    private ToolRecipeAcquisitionProvenance CreateUnavailableSourceAcquisitionProvenance() => new(
        ToolRecipeAcquisitionProvenanceState.Unavailable,
        Localization.SourceAcquisitionDefaultEvidence,
        Localization.SourceAcquisitionDefaultLimitations,
        ToolRecipeAcquisitionDirection.CreateUnavailable(Source.FrameId));

    private void SelectSourceQualityWorkspace()
    {
        SelectedPipelineStep = null;
        if (SelectedPipelineStep is not null)
        {
            return;
        }

        WorkspaceSelection.ClearRecipeSelection();
        NotifySourceQualityWorkspaceState();
        SourceQualityWorkspaceRequested?.Invoke(this, EventArgs.Empty);
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
            Source.FrameId,
            GetOrLoadDecodedC3DSourceAsync);
    }

    private Task<C3DHeightFieldSnapshot> GetOrLoadDecodedC3DSourceAsync(
        CancellationToken cancellationToken) =>
        SourceSession.GetOrLoadDecodedSourceAsync(
            Source.Path,
            Source.Id,
            Source.Unit,
            Source.FrameId,
            cancellationToken);

    internal SourceQualityDelta? CreateSourceQualityDelta(
        C3DHeightFieldSnapshot derivedOutput,
        long? detectedOutlierCount,
        string outlierEvidence)
    {
        ArgumentNullException.ThrowIfNull(derivedOutput);
        var report = SourceQuality.Report;
        var sourceBinding = SourceSession.SourceBinding;
        if (report is null
            || sourceBinding is null
            || !string.Equals(report.Source.EntityId, Source.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(report.Source.ContentSha256, sourceBinding.ContentSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(report.Source.RootSourceSha256, derivedOutput.RootSourceSha256, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(report.Coordinates.Unit, derivedOutput.Unit, StringComparison.Ordinal)
            || !string.Equals(report.Coordinates.FrameId, derivedOutput.FrameId, StringComparison.Ordinal))
        {
            return null;
        }

        return new SourceQualityDelta(
            report.Source.EntityId,
            report.Source.ContentSha256,
            derivedOutput.EntityId,
            derivedOutput.ContentSha256,
            report.Source.RootSourceSha256,
            derivedOutput.RootSourceSha256,
            report.Coverage.ValidSampleCount,
            derivedOutput.ValidCount,
            report.Coverage.MissingSampleCount,
            derivedOutput.MissingCount,
            detectedOutlierCount,
            outlierEvidence);
    }

    private void OnSourceQualityPropertyChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SourceQualityWorkspaceViewModel.IsLoading)
            or nameof(SourceQualityWorkspaceViewModel.Report)
            or nameof(SourceQualityWorkspaceViewModel.HasReport)
            or nameof(SourceQualityWorkspaceViewModel.HasError)
            or nameof(SourceQualityWorkspaceViewModel.IsAvailableOrLoading)
            or nameof(SourceQualityWorkspaceViewModel.State))
        {
            if (sourceQualityUiDispatcher is { } dispatcher
                && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(
                    new Action(() => OnSourceQualityPropertyChanged(sender, args)));
                return;
            }

            if (args.PropertyName is nameof(SourceQualityWorkspaceViewModel.Report)
                or nameof(SourceQualityWorkspaceViewModel.HasError))
            {
                RebuildArtifactRegistryAndNavigator();
            }
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

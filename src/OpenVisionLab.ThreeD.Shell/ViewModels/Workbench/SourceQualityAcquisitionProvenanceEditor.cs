using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns the editable acquisition provenance draft and its explicit apply/reset
/// commands. Source Quality report loading remains with the workspace owner.
/// </summary>
internal sealed class SourceQualityAcquisitionProvenanceEditor : INotifyPropertyChanged, IDisposable
{
    private readonly ThreeDLocalization localization;
    private readonly Action<ToolRecipeAcquisitionProvenance>? applyAcquisitionProvenance;
    private readonly RelayCommand applyAcquisitionProvenanceCommand;
    private readonly RelayCommand resetAcquisitionProvenanceCommand;
    private ToolRecipeAcquisitionProvenance appliedAcquisitionProvenance =
        ToolRecipeAcquisitionProvenance.CreateUnavailable();
    private IReadOnlyList<SourceAcquisitionProvenanceStateOption> acquisitionStateOptions = [];
    private SourceAcquisitionProvenanceStateOption? selectedAcquisitionStateOption;
    private string acquisitionEvidenceDraft = string.Empty;
    private string acquisitionLimitationNotesDraft = string.Empty;
    private bool acquisitionReflectiveFlagDraft;
    private bool acquisitionTransparentFlagDraft;
    private bool acquisitionTexturelessFlagDraft;
    private bool acquisitionClippedFlagDraft;
    private bool acquisitionLowCoverageFlagDraft;
    private bool isAcquisitionProvenancePersisted;
    private string sourceFrameId = string.Empty;
    private ToolRecipeAcquisitionDirection appliedAcquisitionDirection =
        ToolRecipeAcquisitionDirection.CreateUnavailable(string.Empty);
    private IReadOnlyList<SourceAcquisitionDirectionStateOption> acquisitionDirectionStateOptions = [];
    private SourceAcquisitionDirectionStateOption? selectedAcquisitionDirectionStateOption;
    private string acquisitionDirectionXDraft = string.Empty;
    private string acquisitionDirectionYDraft = string.Empty;
    private string acquisitionDirectionZDraft = string.Empty;
    private bool isAcquisitionDirectionPersisted;
    private int disposalState;

    public SourceQualityAcquisitionProvenanceEditor(
        ThreeDLocalization localization,
        Action<ToolRecipeAcquisitionProvenance>? applyAcquisitionProvenance)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.applyAcquisitionProvenance = applyAcquisitionProvenance;
        applyAcquisitionProvenanceCommand = new RelayCommand(
            _ => ApplyAcquisitionProvenance(),
            _ => CanApplyAcquisitionProvenance);
        resetAcquisitionProvenanceCommand = new RelayCommand(
            _ => LoadAcquisitionProvenance(
                IsAcquisitionProvenancePersisted ? appliedAcquisitionProvenance : null,
                sourceFrameId),
            _ => HasPendingAcquisitionProvenanceChanges);
        LoadAcquisitionProvenance(null);
        localization.PropertyChanged += OnLocalizationChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        localization.PropertyChanged -= OnLocalizationChanged;
        GC.SuppressFinalize(this);
    }

    public ToolRecipeAcquisitionProvenance AppliedAcquisitionProvenance =>
        appliedAcquisitionProvenance;
    public IReadOnlyList<SourceAcquisitionProvenanceStateOption> AcquisitionStateOptions =>
        acquisitionStateOptions;
    public IReadOnlyList<SourceAcquisitionDirectionStateOption> AcquisitionDirectionStateOptions =>
        acquisitionDirectionStateOptions;

    public SourceAcquisitionProvenanceStateOption? SelectedAcquisitionStateOption
    {
        get => selectedAcquisitionStateOption;
        set
        {
            if (SetField(ref selectedAcquisitionStateOption, value))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public string AcquisitionEvidenceDraft
    {
        get => acquisitionEvidenceDraft;
        set
        {
            if (SetField(ref acquisitionEvidenceDraft, value ?? string.Empty))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public string AcquisitionLimitationNotesDraft
    {
        get => acquisitionLimitationNotesDraft;
        set
        {
            if (SetField(ref acquisitionLimitationNotesDraft, value ?? string.Empty))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public bool IsAcquisitionReflectiveFlagDraft
    {
        get => acquisitionReflectiveFlagDraft;
        set
        {
            if (SetField(ref acquisitionReflectiveFlagDraft, value))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public bool IsAcquisitionTransparentFlagDraft
    {
        get => acquisitionTransparentFlagDraft;
        set
        {
            if (SetField(ref acquisitionTransparentFlagDraft, value))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public bool IsAcquisitionTexturelessFlagDraft
    {
        get => acquisitionTexturelessFlagDraft;
        set
        {
            if (SetField(ref acquisitionTexturelessFlagDraft, value))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public bool IsAcquisitionClippedFlagDraft
    {
        get => acquisitionClippedFlagDraft;
        set
        {
            if (SetField(ref acquisitionClippedFlagDraft, value))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public bool IsAcquisitionLowCoverageFlagDraft
    {
        get => acquisitionLowCoverageFlagDraft;
        set
        {
            if (SetField(ref acquisitionLowCoverageFlagDraft, value))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public SourceAcquisitionDirectionStateOption? SelectedAcquisitionDirectionStateOption
    {
        get => selectedAcquisitionDirectionStateOption;
        set
        {
            if (SetField(ref selectedAcquisitionDirectionStateOption, value))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public string AcquisitionDirectionXDraft
    {
        get => acquisitionDirectionXDraft;
        set
        {
            if (SetField(ref acquisitionDirectionXDraft, value ?? string.Empty))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public string AcquisitionDirectionYDraft
    {
        get => acquisitionDirectionYDraft;
        set
        {
            if (SetField(ref acquisitionDirectionYDraft, value ?? string.Empty))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public string AcquisitionDirectionZDraft
    {
        get => acquisitionDirectionZDraft;
        set
        {
            if (SetField(ref acquisitionDirectionZDraft, value ?? string.Empty))
            {
                NotifyAcquisitionDraftProperties();
            }
        }
    }

    public bool IsAcquisitionProvenancePersisted
    {
        get => isAcquisitionProvenancePersisted;
        private set
        {
            if (SetField(ref isAcquisitionProvenancePersisted, value))
            {
                OnPropertyChanged(nameof(AcquisitionPersistenceSummary));
            }
        }
    }

    public bool IsAcquisitionStateAvailable =>
        SelectedAcquisitionStateOption?.State == ToolRecipeAcquisitionProvenanceState.Available;

    public bool IsAcquisitionDirectionAvailable =>
        SelectedAcquisitionDirectionStateOption?.State
            == ToolRecipeAcquisitionDirectionState.Available;

    public string AcquisitionDirectionFrame => string.IsNullOrWhiteSpace(sourceFrameId)
        ? "\u2014"
        : sourceFrameId;

    public string AcquisitionDirectionConvention => "Sensor \u2192 scene";

    public bool IsAcquisitionDirectionPersisted
    {
        get => isAcquisitionDirectionPersisted;
        private set
        {
            if (SetField(ref isAcquisitionDirectionPersisted, value))
            {
                OnPropertyChanged(nameof(AcquisitionDirectionPersistenceSummary));
            }
        }
    }

    public bool HasPendingAcquisitionDirectionChanges =>
        !TryCreateDraftAcquisitionDirection(out var direction)
        || direction != appliedAcquisitionDirection;

    public bool HasPendingAcquisitionProvenanceChanges =>
        SelectedAcquisitionStateOption?.State != appliedAcquisitionProvenance.State
        || !string.Equals(
            AcquisitionEvidenceDraft.Trim(),
            appliedAcquisitionProvenance.Evidence,
            StringComparison.Ordinal)
        || !string.Equals(
            AcquisitionLimitationNotesDraft.Trim(),
            appliedAcquisitionProvenance.LimitationNotes,
            StringComparison.Ordinal)
        || !LimitationKindsEqual(
            CreateDraftLimitationFlags(),
            appliedAcquisitionProvenance.LimitationFlags)
        || HasPendingAcquisitionDirectionChanges;

    public bool CanApplyAcquisitionProvenance =>
        applyAcquisitionProvenance is not null
        && HasPendingAcquisitionProvenanceChanges
        && SelectedAcquisitionStateOption is not null
        && !string.IsNullOrWhiteSpace(AcquisitionEvidenceDraft)
        && !string.IsNullOrWhiteSpace(AcquisitionLimitationNotesDraft)
        && TryCreateDraftAcquisitionDirection(out _);

    public bool HasAcquisitionValidationError =>
        string.IsNullOrWhiteSpace(AcquisitionEvidenceDraft)
        || string.IsNullOrWhiteSpace(AcquisitionLimitationNotesDraft)
        || !TryCreateDraftAcquisitionDirection(out _);

    public string AcquisitionDraftMessage =>
        string.IsNullOrWhiteSpace(AcquisitionEvidenceDraft)
            ? localization.SourceAcquisitionEvidenceRequired
            : string.IsNullOrWhiteSpace(AcquisitionLimitationNotesDraft)
                ? localization.SourceAcquisitionLimitationsRequired
                : !TryCreateDraftAcquisitionDirection(out _)
                    ? localization.SourceAcquisitionDirectionInvalid
                : HasPendingAcquisitionProvenanceChanges
                    ? localization.SourceAcquisitionReady
                    : localization.SourceAcquisitionNoChanges;

    public string AcquisitionPersistenceSummary => IsAcquisitionProvenancePersisted
        ? localization.SourceAcquisitionPersisted
        : localization.SourceAcquisitionFallback;

    public string AcquisitionDirectionPersistenceSummary => IsAcquisitionDirectionPersisted
        ? localization.SourceAcquisitionDirectionPersisted
        : localization.SourceAcquisitionDirectionFallback;

    public ICommand ApplyAcquisitionProvenanceCommand => applyAcquisitionProvenanceCommand;
    public ICommand ResetAcquisitionProvenanceCommand => resetAcquisitionProvenanceCommand;
    public void LoadAcquisitionProvenance(
        ToolRecipeAcquisitionProvenance? acquisitionProvenance,
        string? frameId = null)
    {
        if (frameId is not null)
        {
            sourceFrameId = frameId;
            OnPropertyChanged(nameof(AcquisitionDirectionFrame));
        }
        IsAcquisitionProvenancePersisted = acquisitionProvenance is not null;
        appliedAcquisitionProvenance = acquisitionProvenance
            ?? CreateLocalizedUnavailableProvenance();
        IsAcquisitionDirectionPersisted = acquisitionProvenance?.AcquisitionDirection is not null;
        appliedAcquisitionDirection = acquisitionProvenance?.AcquisitionDirection
            ?? ToolRecipeAcquisitionDirection.CreateUnavailable(sourceFrameId);
        RebuildAcquisitionStateOptions(appliedAcquisitionProvenance.State);
        RebuildAcquisitionDirectionStateOptions(appliedAcquisitionDirection.State);
        acquisitionEvidenceDraft = appliedAcquisitionProvenance.Evidence;
        acquisitionLimitationNotesDraft = appliedAcquisitionProvenance.LimitationNotes;
        acquisitionReflectiveFlagDraft = HasLimitationFlag(
            appliedAcquisitionProvenance,
            ToolRecipeAcquisitionLimitationKind.Reflective);
        acquisitionTransparentFlagDraft = HasLimitationFlag(
            appliedAcquisitionProvenance,
            ToolRecipeAcquisitionLimitationKind.Transparent);
        acquisitionTexturelessFlagDraft = HasLimitationFlag(
            appliedAcquisitionProvenance,
            ToolRecipeAcquisitionLimitationKind.Textureless);
        acquisitionClippedFlagDraft = HasLimitationFlag(
            appliedAcquisitionProvenance,
            ToolRecipeAcquisitionLimitationKind.Clipped);
        acquisitionLowCoverageFlagDraft = HasLimitationFlag(
            appliedAcquisitionProvenance,
            ToolRecipeAcquisitionLimitationKind.LowCoverage);
        acquisitionDirectionXDraft = FormatDirectionComponent(appliedAcquisitionDirection.Vector?.X);
        acquisitionDirectionYDraft = FormatDirectionComponent(appliedAcquisitionDirection.Vector?.Y);
        acquisitionDirectionZDraft = FormatDirectionComponent(appliedAcquisitionDirection.Vector?.Z);
        OnPropertyChanged(nameof(AppliedAcquisitionProvenance));
        OnPropertyChanged(nameof(AcquisitionEvidenceDraft));
        OnPropertyChanged(nameof(AcquisitionLimitationNotesDraft));
        OnPropertyChanged(nameof(IsAcquisitionReflectiveFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionTransparentFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionTexturelessFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionClippedFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionLowCoverageFlagDraft));
        OnPropertyChanged(nameof(AcquisitionDirectionXDraft));
        OnPropertyChanged(nameof(AcquisitionDirectionYDraft));
        OnPropertyChanged(nameof(AcquisitionDirectionZDraft));
        NotifyAcquisitionDraftProperties();
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(AcquisitionPersistenceSummary));
        OnPropertyChanged(nameof(AcquisitionDirectionPersistenceSummary));
        RebuildAcquisitionStateOptions(
            SelectedAcquisitionStateOption?.State ?? appliedAcquisitionProvenance.State);
        RebuildAcquisitionDirectionStateOptions(
            SelectedAcquisitionDirectionStateOption?.State ?? appliedAcquisitionDirection.State);
        NotifyAcquisitionDraftProperties();
    }

    private void ApplyAcquisitionProvenance()
    {
        if (!CanApplyAcquisitionProvenance
            || SelectedAcquisitionStateOption is null)
        {
            return;
        }

        if (!TryCreateDraftAcquisitionDirection(out var direction))
        {
            return;
        }

        var provenance = new ToolRecipeAcquisitionProvenance(
            SelectedAcquisitionStateOption.State,
            AcquisitionEvidenceDraft.Trim(),
            AcquisitionLimitationNotesDraft.Trim(),
            direction,
            CreateDraftLimitationFlags());
        applyAcquisitionProvenance!(provenance);
        LoadAcquisitionProvenance(provenance, sourceFrameId);
    }

    private IReadOnlyList<ToolRecipeAcquisitionLimitationFlag> CreateDraftLimitationFlags()
    {
        var flags = new List<ToolRecipeAcquisitionLimitationFlag>();
        if (IsAcquisitionReflectiveFlagDraft)
        {
            flags.Add(new(
                ToolRecipeAcquisitionLimitationKind.Reflective,
                ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored));
        }
        if (IsAcquisitionTransparentFlagDraft)
        {
            flags.Add(new(
                ToolRecipeAcquisitionLimitationKind.Transparent,
                ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored));
        }
        if (IsAcquisitionTexturelessFlagDraft)
        {
            flags.Add(new(
                ToolRecipeAcquisitionLimitationKind.Textureless,
                ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored));
        }
        if (IsAcquisitionClippedFlagDraft)
        {
            flags.Add(new(
                ToolRecipeAcquisitionLimitationKind.Clipped,
                ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored));
        }
        if (IsAcquisitionLowCoverageFlagDraft)
        {
            flags.Add(new(
                ToolRecipeAcquisitionLimitationKind.LowCoverage,
                ToolRecipeAcquisitionLimitationOrigin.OperatorAuthored));
        }

        return flags;
    }

    private static bool HasLimitationFlag(
        ToolRecipeAcquisitionProvenance provenance,
        ToolRecipeAcquisitionLimitationKind kind) =>
        provenance.LimitationFlags?.Any(flag => flag?.Kind == kind) == true;

    private static bool LimitationKindsEqual(
        IReadOnlyList<ToolRecipeAcquisitionLimitationFlag> draft,
        IReadOnlyList<ToolRecipeAcquisitionLimitationFlag>? applied)
    {
        var draftKinds = draft.Select(flag => flag.Kind).ToHashSet();
        var appliedKinds = (applied ?? [])
            .Where(flag => flag is not null)
            .Select(flag => flag.Kind)
            .ToHashSet();
        return draftKinds.SetEquals(appliedKinds);
    }

    private void RebuildAcquisitionDirectionStateOptions(
        ToolRecipeAcquisitionDirectionState selectedState)
    {
        acquisitionDirectionStateOptions =
        [
            new(
                ToolRecipeAcquisitionDirectionState.Available,
                localization.SourceAcquisitionDirectionAvailable),
            new(
                ToolRecipeAcquisitionDirectionState.Unavailable,
                localization.SourceAcquisitionDirectionUnavailable)
        ];
        selectedAcquisitionDirectionStateOption = acquisitionDirectionStateOptions.First(option =>
            option.State == selectedState);
        OnPropertyChanged(nameof(AcquisitionDirectionStateOptions));
        OnPropertyChanged(nameof(SelectedAcquisitionDirectionStateOption));
        OnPropertyChanged(nameof(IsAcquisitionDirectionAvailable));
    }

    private bool TryCreateDraftAcquisitionDirection(
        out ToolRecipeAcquisitionDirection direction)
    {
        direction = ToolRecipeAcquisitionDirection.CreateUnavailable(sourceFrameId);
        if (SelectedAcquisitionDirectionStateOption is null
            || string.IsNullOrWhiteSpace(sourceFrameId))
        {
            return false;
        }
        if (SelectedAcquisitionDirectionStateOption.State
            == ToolRecipeAcquisitionDirectionState.Unavailable)
        {
            return true;
        }
        if (!IsAcquisitionStateAvailable
            || !TryParseFinite(AcquisitionDirectionXDraft, out var x)
            || !TryParseFinite(AcquisitionDirectionYDraft, out var y)
            || !TryParseFinite(AcquisitionDirectionZDraft, out var z))
        {
            return false;
        }

        var length = Math.Sqrt(x * x + y * y + z * z);
        if (!double.IsFinite(length) || length <= 1e-12)
        {
            return false;
        }

        direction = new ToolRecipeAcquisitionDirection(
            ToolRecipeAcquisitionDirectionState.Available,
            ToolRecipeAcquisitionDirectionConvention.SensorToScene,
            sourceFrameId,
            new ToolRecipeXyz(x / length, y / length, z / length));
        return true;
    }

    private static bool TryParseFinite(string value, out double parsed) =>
        double.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out parsed)
        && double.IsFinite(parsed);

    private static string FormatDirectionComponent(double? value) =>
        value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;

    private void RebuildAcquisitionStateOptions(
        ToolRecipeAcquisitionProvenanceState selectedState)
    {
        acquisitionStateOptions =
        [
            new(
                ToolRecipeAcquisitionProvenanceState.Available,
                localization.SourceAcquisitionAvailable),
            new(
                ToolRecipeAcquisitionProvenanceState.Unavailable,
                localization.SourceAcquisitionUnavailable)
        ];
        selectedAcquisitionStateOption = acquisitionStateOptions.First(option =>
            option.State == selectedState);
        OnPropertyChanged(nameof(AcquisitionStateOptions));
        OnPropertyChanged(nameof(SelectedAcquisitionStateOption));
        OnPropertyChanged(nameof(IsAcquisitionStateAvailable));
    }

    private ToolRecipeAcquisitionProvenance CreateLocalizedUnavailableProvenance() => new(
        ToolRecipeAcquisitionProvenanceState.Unavailable,
        localization.SourceAcquisitionDefaultEvidence,
        localization.SourceAcquisitionDefaultLimitations);

    private void NotifyAcquisitionDraftProperties()
    {
        OnPropertyChanged(nameof(IsAcquisitionReflectiveFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionTransparentFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionTexturelessFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionClippedFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionLowCoverageFlagDraft));
        OnPropertyChanged(nameof(IsAcquisitionStateAvailable));
        OnPropertyChanged(nameof(IsAcquisitionDirectionAvailable));
        OnPropertyChanged(nameof(HasPendingAcquisitionDirectionChanges));
        OnPropertyChanged(nameof(HasPendingAcquisitionProvenanceChanges));
        OnPropertyChanged(nameof(CanApplyAcquisitionProvenance));
        OnPropertyChanged(nameof(HasAcquisitionValidationError));
        OnPropertyChanged(nameof(AcquisitionDraftMessage));
        applyAcquisitionProvenanceCommand.RaiseCanExecuteChanged();
        resetAcquisitionProvenanceCommand.RaiseCanExecuteChanged();
    }
    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
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
}

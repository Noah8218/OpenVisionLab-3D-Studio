using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Presentation of one identified source-quality report plus an explicit-draft
/// acquisition provenance editor. Quality loading remains read-only; only the
/// injected Apply callback may change a recipe. This type never invokes Preview,
/// Publish, Run, Validation Set, or Save.
/// </summary>
public sealed class SourceQualityWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly ThreeDLocalization localization;
    private readonly Action<ToolRecipeAcquisitionProvenance>? applyAcquisitionProvenance;
    private readonly RelayCommand applyAcquisitionProvenanceCommand;
    private readonly RelayCommand resetAcquisitionProvenanceCommand;
    private CancellationTokenSource? loadCancellation;
    private SourceQualityReport? report;
    private string loadedSourceKey = string.Empty;
    private string error = string.Empty;
    private bool isLoading;
    private int loadGeneration;
    private ToolRecipeAcquisitionProvenance appliedAcquisitionProvenance =
        ToolRecipeAcquisitionProvenance.CreateUnavailable();
    private IReadOnlyList<SourceAcquisitionProvenanceStateOption> acquisitionStateOptions = [];
    private SourceAcquisitionProvenanceStateOption? selectedAcquisitionStateOption;
    private string acquisitionEvidenceDraft = string.Empty;
    private string acquisitionLimitationNotesDraft = string.Empty;
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

    public SourceQualityWorkspaceViewModel(
        ThreeDLocalization localization,
        Action<ToolRecipeAcquisitionProvenance>? applyAcquisitionProvenance = null)
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

    public ThreeDLocalization Localization => localization;
    public SourceQualityReport? Report => report;
    public bool HasReport => Report is not null;
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

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetField(ref isLoading, value))
            {
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsAvailableOrLoading));
            }
        }
    }

    public string Error
    {
        get => error;
        private set
        {
            if (SetField(ref error, value))
            {
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsAvailableOrLoading));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public bool IsAvailableOrLoading => IsLoading || HasReport || HasError;

    public string State => IsLoading
        ? localization.SourceQualityLoading
        : HasError
            ? localization.SourceQualityError
            : HasReport
                ? localization.SourceQualityReady
                : localization.SourceQualityUnavailable;

    public string SourceName => Report is null
        ? localization.SourceQualityUnavailable
        : Path.GetFileName(Report.Source.Path);

    public string GridValue => Report is null
        ? "\u2014"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Report.Grid.Width:N0} \u00d7 {Report.Grid.Height:N0}");

    public string CellCountValue => Report is null
        ? "\u2014"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Report.Grid.CellCount:N0}");

    public string ValidValue => Report is null
        ? "\u2014"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Report.Coverage.ValidSampleCount:N0} ({Report.Coverage.ValidRatio:P1})");

    public string MissingValue => Report is null
        ? "\u2014"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Report.Coverage.MissingSampleCount:N0} ({Report.Coverage.MissingRatio:P1})");

    public double ValidPercent => (Report?.Coverage.ValidRatio ?? 0.0) * 100.0;

    public string HeightRangeValue => Report?.Height is
        {
            Minimum: { } minimum,
            Maximum: { } maximum
        }
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"{minimum:0.###} \u2026 {maximum:0.###}")
        : "\u2014";

    public string HeightMeanValue => Report?.Height.Mean is { } mean
        ? string.Create(CultureInfo.InvariantCulture, $"{mean:0.###}")
        : "\u2014";

    public string DistributionSummary => Report?.Height.Distribution is { } distribution
        ? string.Create(
            CultureInfo.InvariantCulture,
            $"{distribution.BinCount:N0} bins | peak {distribution.PeakBinIndex + 1:N0}")
        : localization.SourceQualityUnavailable;

    public string CoordinateSummary => Report is null
        ? "\u2014"
        : $"{Report.Coordinates.FrameId} | {Report.Coordinates.Unit}";

    public string CoordinateConvention => Report?.Coordinates.CoordinateConvention ?? "\u2014";

    public string MaskSummary => Report is null
        ? "\u2014"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Report.Coverage.InvalidCellMask.ByteLength:N0} bytes | {ShortHash(Report.Coverage.InvalidCellMask.Sha256)}");

    public string MaskSha256 => Report?.Coverage.InvalidCellMask.Sha256 ?? "\u2014";

    public string SourceIdentitySummary => Report is null
        ? "\u2014"
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Report.Source.ByteLength:N0} bytes | {ShortHash(Report.Source.ContentSha256)}");

    public string SourceSha256 => Report?.Source.ContentSha256 ?? "\u2014";
    public string Provenance => Report?.Provenance ?? "\u2014";

    public bool HasGridDiagnostics => GridDiagnostics.Count > 0;
    public bool HasGridDiagnosticError =>
        Report?.GridDiagnostics?.State == SourceQualityGridDiagnosticState.Error;
    public string GridDiagnosticsStatus => Report?.GridDiagnostics is { } diagnostics
        ? LocalizedDiagnosticState(diagnostics.State)
        : localization.SourceQualityUnavailable;
    public string GridDiagnosticsState => Report?.GridDiagnostics is { } diagnostics
        ? $"{LocalizedDiagnosticState(diagnostics.State)} · {localization.SourceQualityGridDiagnostics}"
        : localization.SourceQualityUnavailable;
    public string GridDiagnosticsSummary => Report?.GridDiagnostics is { } diagnostics
        ? string.Format(
            CultureInfo.InvariantCulture,
            localization.SourceQualityGridDiagnosticsSummaryFormat,
            LocalizedDiagnosticState(diagnostics.State),
            diagnostics.DeclaredCellCount,
            diagnostics.ObservedSampleCount,
            diagnostics.UniqueLocatorCount)
        : localization.SourceQualityUnavailable;

    public ResettableObservableCollection<SourceQualityChannelItem> Channels { get; } = [];
    public ResettableObservableCollection<SourceQualityDistributionBinItem> DistributionBins { get; } = [];
    public ResettableObservableCollection<SourceQualityGridDiagnosticItem> GridDiagnostics { get; } = [];

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
        acquisitionDirectionXDraft = FormatDirectionComponent(appliedAcquisitionDirection.Vector?.X);
        acquisitionDirectionYDraft = FormatDirectionComponent(appliedAcquisitionDirection.Vector?.Y);
        acquisitionDirectionZDraft = FormatDirectionComponent(appliedAcquisitionDirection.Vector?.Z);
        OnPropertyChanged(nameof(AppliedAcquisitionProvenance));
        OnPropertyChanged(nameof(AcquisitionEvidenceDraft));
        OnPropertyChanged(nameof(AcquisitionLimitationNotesDraft));
        OnPropertyChanged(nameof(AcquisitionDirectionXDraft));
        OnPropertyChanged(nameof(AcquisitionDirectionYDraft));
        OnPropertyChanged(nameof(AcquisitionDirectionZDraft));
        NotifyAcquisitionDraftProperties();
    }

    public Task EnsureSourceAsync(
        string path,
        string entityId,
        string unit,
        string frameId)
        => EnsureSourceAsync(
            path,
            entityId,
            unit,
            frameId,
            cancellationToken => Task.Run(
                () => C3DHeightFieldSnapshot.LoadIdentified(
                    Path.GetFullPath(path),
                    entityId,
                    unit,
                    frameId),
                cancellationToken));

    internal async Task EnsureSourceAsync(
        string path,
        string entityId,
        string unit,
        string frameId,
        Func<CancellationToken, Task<C3DHeightFieldSnapshot>> loadSourceAsync)
    {
        ArgumentNullException.ThrowIfNull(loadSourceAsync);
        if (string.IsNullOrWhiteSpace(path))
        {
            Clear();
            return;
        }

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            SetUnavailable($"Source file does not exist: {fullPath}");
            return;
        }

        var sourceKey = string.Join(
            "|",
            fullPath,
            new FileInfo(fullPath).Length.ToString(CultureInfo.InvariantCulture),
            File.GetLastWriteTimeUtc(fullPath).Ticks.ToString(CultureInfo.InvariantCulture),
            entityId,
            unit,
            frameId);
        if (string.Equals(loadedSourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
            && Report is not null)
        {
            return;
        }

        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = new CancellationTokenSource();
        var cancellationToken = loadCancellation.Token;
        var generation = ++loadGeneration;
        IsLoading = true;
        Error = string.Empty;
        SetReport(null);

        try
        {
            var snapshot = await loadSourceAsync(cancellationToken);
            var nextReport = await Task.Run(
                () => C3DSourceQualityAnalyzer.Create(snapshot),
                cancellationToken);

            if (generation != loadGeneration || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            loadedSourceKey = sourceKey;
            SetReport(nextReport);
        }
        catch (OperationCanceledException)
        {
            // A newer source owns the visible report.
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or ArgumentException
                or OverflowException)
        {
            if (generation != loadGeneration)
            {
                return;
            }

            loadedSourceKey = string.Empty;
            SetReport(null);
            Error = exception.Message;
        }
        finally
        {
            if (generation == loadGeneration)
            {
                IsLoading = false;
            }
        }
    }

    public void Clear()
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = null;
        loadGeneration++;
        loadedSourceKey = string.Empty;
        Error = string.Empty;
        IsLoading = false;
        SetReport(null);
    }

    private void SetUnavailable(string message)
    {
        Clear();
        Error = message;
    }

    private void SetReport(SourceQualityReport? value)
    {
        if (ReferenceEquals(report, value))
        {
            return;
        }

        report = value;
        Channels.ReplaceAll(CreateChannelItems(value));
        DistributionBins.ReplaceAll(CreateDistributionBins(value));
        GridDiagnostics.ReplaceAll(CreateGridDiagnosticItems(value));
        NotifyReportProperties();
    }

    internal void SetReportForVerification(SourceQualityReport value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.TryValidateGridDiagnostics(out var validationMessage))
        {
            throw new InvalidDataException(validationMessage);
        }

        SetReport(value);
    }

    private IEnumerable<SourceQualityChannelItem> CreateChannelItems(
        SourceQualityReport? value) =>
        value?.Channels.Select(channel => new SourceQualityChannelItem(
            channel.Channel == SourceQualityChannel.SignalToNoiseRatio
                ? "SNR"
                : channel.Channel.ToString(),
            channel.State == SourceQualityChannelState.Available,
            channel.State == SourceQualityChannelState.Available
                ? localization.Available
                : localization.Unavailable,
            channel.Evidence))
        ?? [];

    private static IEnumerable<SourceQualityDistributionBinItem> CreateDistributionBins(
        SourceQualityReport? value)
    {
        if (value?.Height.Distribution is not { } distribution
            || distribution.Bins.Count == 0)
        {
            return [];
        }

        var peak = Math.Max(1L, distribution.Bins.Max());
        return distribution.Bins.Select((count, index) =>
            new SourceQualityDistributionBinItem(
                index,
                count,
                4.0 + count / (double)peak * 42.0,
                index == distribution.PeakBinIndex));
    }

    private IEnumerable<SourceQualityGridDiagnosticItem> CreateGridDiagnosticItems(
        SourceQualityReport? value) =>
        value?.GridDiagnostics?.Checks.Select(check =>
            new SourceQualityGridDiagnosticItem(
                check.Code.ToString(),
                LocalizedDiagnosticTitle(check.Code),
                LocalizedDiagnosticState(check.State),
                LocalizedDiagnosticDetail(check),
                check.State == SourceQualityGridDiagnosticState.Error
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        localization.Resolve(
                            "ThreeD.SourceQuality.GridDiagnostics.EvidenceFormat",
                            "진단 근거: {0}",
                            "Diagnostic evidence: {0}"),
                        check.Message)
                    : string.Empty,
                check.State == SourceQualityGridDiagnosticState.Pass,
                check.State == SourceQualityGridDiagnosticState.Error))
        ?? [];

    private string LocalizedDiagnosticTitle(SourceQualityGridDiagnosticCode code) =>
        code switch
        {
            SourceQualityGridDiagnosticCode.Topology => localization.Resolve(
                "ThreeD.SourceQuality.GridDiagnostics.Topology",
                "예상 격자 구조",
                "Expected grid topology"),
            SourceQualityGridDiagnosticCode.LocatorMonotonicity => localization.Resolve(
                "ThreeD.SourceQuality.GridDiagnostics.LocatorMonotonicity",
                "단조 위치 순서",
                "Monotonic locator order"),
            SourceQualityGridDiagnosticCode.DuplicateLocator => localization.Resolve(
                "ThreeD.SourceQuality.GridDiagnostics.DuplicateLocator",
                "고유 위치 식별자",
                "Unique locators"),
            SourceQualityGridDiagnosticCode.CoordinateFiniteness => localization.Resolve(
                "ThreeD.SourceQuality.GridDiagnostics.CoordinateFiniteness",
                "유한 유효 셀 좌표",
                "Finite valid-cell coordinates"),
            _ => code.ToString()
        };

    private string LocalizedDiagnosticState(SourceQualityGridDiagnosticState state) =>
        state == SourceQualityGridDiagnosticState.Pass
            ? localization.ValidationSetFilterPass
            : localization.ValidationSetFilterError;

    private string LocalizedDiagnosticDetail(SourceQualityGridDiagnosticCheck check)
    {
        if (check.State == SourceQualityGridDiagnosticState.Pass)
        {
            return check.Code switch
            {
                SourceQualityGridDiagnosticCode.Topology => localization.Resolve(
                    "ThreeD.SourceQuality.GridDiagnostics.Topology.Pass",
                    "선언 크기와 관측 위치 구조가 일치합니다.",
                    "Declared dimensions and observed locator coverage match."),
                SourceQualityGridDiagnosticCode.LocatorMonotonicity => localization.Resolve(
                    "ThreeD.SourceQuality.GridDiagnostics.LocatorMonotonicity.Pass",
                    "위치 식별자가 행 우선 순서로 단조 증가합니다.",
                    "Locators are monotonic in row-major order."),
                SourceQualityGridDiagnosticCode.DuplicateLocator => localization.Resolve(
                    "ThreeD.SourceQuality.GridDiagnostics.DuplicateLocator.Pass",
                    "중복된 위치 식별자가 없습니다.",
                    "No duplicate locators were found."),
                SourceQualityGridDiagnosticCode.CoordinateFiniteness => localization.Resolve(
                    "ThreeD.SourceQuality.GridDiagnostics.CoordinateFiniteness.Pass",
                    "모든 유효 셀 좌표가 유한합니다.",
                    "All valid-cell coordinates are finite."),
                _ => check.Message
            };
        }

        var location = string.Format(
            CultureInfo.InvariantCulture,
            localization.Resolve(
                "ThreeD.SourceQuality.GridDiagnostics.FirstLocationFormat",
                "샘플 {0} · 행 {1} · 열 {2} · 성분 {3}",
                "sample {0} · row {1} · column {2} · component {3}"),
            FormatDiagnosticIndex(check.FirstSampleOrdinal),
            FormatDiagnosticIndex(check.FirstRow),
            FormatDiagnosticIndex(check.FirstColumn),
            string.IsNullOrWhiteSpace(check.FirstComponent) ? "\u2014" : check.FirstComponent);
        return string.Format(
            CultureInfo.InvariantCulture,
            localization.Resolve(
                "ThreeD.SourceQuality.GridDiagnostics.ErrorDetailFormat",
                "영향 {0:N0} · 첫 위치: {1}",
                "{0:N0} affected · first location: {1}"),
            check.AffectedCount,
            location);
    }

    private static string FormatDiagnosticIndex(long? value) =>
        value?.ToString("N0", CultureInfo.InvariantCulture) ?? "\u2014";

    private static string FormatDiagnosticIndex(int? value) =>
        value?.ToString("N0", CultureInfo.InvariantCulture) ?? "\u2014";

    private void NotifyReportProperties()
    {
        OnPropertyChanged(nameof(Report));
        OnPropertyChanged(nameof(HasReport));
        OnPropertyChanged(nameof(IsAvailableOrLoading));
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(SourceName));
        OnPropertyChanged(nameof(GridValue));
        OnPropertyChanged(nameof(CellCountValue));
        OnPropertyChanged(nameof(ValidValue));
        OnPropertyChanged(nameof(MissingValue));
        OnPropertyChanged(nameof(ValidPercent));
        OnPropertyChanged(nameof(HeightRangeValue));
        OnPropertyChanged(nameof(HeightMeanValue));
        OnPropertyChanged(nameof(DistributionSummary));
        OnPropertyChanged(nameof(CoordinateSummary));
        OnPropertyChanged(nameof(CoordinateConvention));
        OnPropertyChanged(nameof(MaskSummary));
        OnPropertyChanged(nameof(MaskSha256));
        OnPropertyChanged(nameof(SourceIdentitySummary));
        OnPropertyChanged(nameof(SourceSha256));
        OnPropertyChanged(nameof(Provenance));
        OnPropertyChanged(nameof(HasGridDiagnostics));
        OnPropertyChanged(nameof(HasGridDiagnosticError));
        OnPropertyChanged(nameof(GridDiagnosticsStatus));
        OnPropertyChanged(nameof(GridDiagnosticsState));
        OnPropertyChanged(nameof(GridDiagnosticsSummary));
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(DistributionSummary));
        OnPropertyChanged(nameof(AcquisitionPersistenceSummary));
        OnPropertyChanged(nameof(AcquisitionDirectionPersistenceSummary));
        RebuildAcquisitionStateOptions(
            SelectedAcquisitionStateOption?.State ?? appliedAcquisitionProvenance.State);
        RebuildAcquisitionDirectionStateOptions(
            SelectedAcquisitionDirectionStateOption?.State ?? appliedAcquisitionDirection.State);
        NotifyAcquisitionDraftProperties();
        Channels.ReplaceAll(CreateChannelItems(Report));
        GridDiagnostics.ReplaceAll(CreateGridDiagnosticItems(Report));
        OnPropertyChanged(nameof(GridDiagnosticsStatus));
        OnPropertyChanged(nameof(GridDiagnosticsState));
        OnPropertyChanged(nameof(GridDiagnosticsSummary));
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
            direction);
        applyAcquisitionProvenance!(provenance);
        LoadAcquisitionProvenance(provenance, sourceFrameId);
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

    private static string ShortHash(string value) =>
        value.Length <= 12 ? value : $"{value[..12]}\u2026";

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

public sealed record SourceQualityChannelItem(
    string Name,
    bool IsAvailable,
    string State,
    string Evidence);

public sealed record SourceQualityDistributionBinItem(
    int Index,
    long Count,
    double DisplayHeight,
    bool IsPeak);

public sealed record SourceQualityGridDiagnosticItem(
    string Code,
    string Title,
    string State,
    string Detail,
    string Evidence,
    bool IsPass,
    bool IsError)
{
    public bool HasEvidence => !string.IsNullOrWhiteSpace(Evidence);
    public string AutomationId => $"SourceQualityGridDiagnostic.{Code}";
}

public sealed record SourceAcquisitionProvenanceStateOption(
    ToolRecipeAcquisitionProvenanceState State,
    string Label);

public sealed record SourceAcquisitionDirectionStateOption(
    ToolRecipeAcquisitionDirectionState State,
    string Label);

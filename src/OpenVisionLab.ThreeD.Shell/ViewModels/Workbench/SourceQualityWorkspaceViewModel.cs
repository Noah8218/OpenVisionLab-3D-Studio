using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
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
public sealed class SourceQualityWorkspaceViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly object loadGate = new();
    private readonly ThreeDLocalization localization;
    private readonly SourceQualityAcquisitionProvenanceEditor acquisitionEditor;
    private AsyncLoadCancellation? loadCancellation;
    private Task? loadTask;
    private Task? loadObservationTask;
    private SourceQualityReport? report;
    private string loadedSourceKey = string.Empty;
    private string error = string.Empty;
    private bool isLoading;
    private int loadGeneration;
    private int disposalState;

    public SourceQualityWorkspaceViewModel(
        ThreeDLocalization localization,
        Action<ToolRecipeAcquisitionProvenance>? applyAcquisitionProvenance = null)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        acquisitionEditor = new(localization, applyAcquisitionProvenance);
        acquisitionEditor.PropertyChanged += OnAcquisitionEditorPropertyChanged;
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
        acquisitionEditor.PropertyChanged -= OnAcquisitionEditorPropertyChanged;
        acquisitionEditor.Dispose();
        lock (loadGate)
        {
            Volatile.Write(ref loadTask, null);
            Volatile.Write(ref loadObservationTask, null);
        }
        Clear();
    }

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    internal bool IsObservedLoadRunning =>
        !IsDisposed && Volatile.Read(ref loadTask) is { IsCompleted: false };

    internal bool StartObservedLoad(Func<Task> load, Action<Exception> reportFailure)
    {
        ArgumentNullException.ThrowIfNull(load);
        ArgumentNullException.ThrowIfNull(reportFailure);
        Task? task = null;
        Exception? synchronousFailure = null;
        lock (loadGate)
        {
            if (IsDisposed)
            {
                return false;
            }

            try
            {
                task = load();
            }
            catch (Exception exception)
            {
                synchronousFailure = exception;
            }

            if (task is not null)
            {
                Volatile.Write(ref loadTask, task);
            }
        }

        if (synchronousFailure is not null)
        {
            if (!IsDisposed)
            {
                reportFailure(synchronousFailure);
            }

            return false;
        }

        if (task is null)
        {
            return false;
        }

        var observer = ObserveLoadAsync(task, reportFailure);
        lock (loadGate)
        {
            if (ReferenceEquals(Volatile.Read(ref loadTask), task))
            {
                Volatile.Write(ref loadObservationTask, observer);
            }
        }

        return true;
    }

    public ThreeDLocalization Localization => localization;
    public SourceQualityReport? Report => report;
    public bool HasReport => Report is not null;
    public ToolRecipeAcquisitionProvenance AppliedAcquisitionProvenance =>
        acquisitionEditor.AppliedAcquisitionProvenance;
    public IReadOnlyList<SourceAcquisitionProvenanceStateOption> AcquisitionStateOptions =>
        acquisitionEditor.AcquisitionStateOptions;
    public IReadOnlyList<SourceAcquisitionDirectionStateOption> AcquisitionDirectionStateOptions =>
        acquisitionEditor.AcquisitionDirectionStateOptions;
    public SourceAcquisitionProvenanceStateOption? SelectedAcquisitionStateOption
    {
        get => acquisitionEditor.SelectedAcquisitionStateOption;
        set => acquisitionEditor.SelectedAcquisitionStateOption = value;
    }
    public string AcquisitionEvidenceDraft
    {
        get => acquisitionEditor.AcquisitionEvidenceDraft;
        set => acquisitionEditor.AcquisitionEvidenceDraft = value;
    }
    public string AcquisitionLimitationNotesDraft
    {
        get => acquisitionEditor.AcquisitionLimitationNotesDraft;
        set => acquisitionEditor.AcquisitionLimitationNotesDraft = value;
    }
    public bool IsAcquisitionReflectiveFlagDraft
    {
        get => acquisitionEditor.IsAcquisitionReflectiveFlagDraft;
        set => acquisitionEditor.IsAcquisitionReflectiveFlagDraft = value;
    }
    public bool IsAcquisitionTransparentFlagDraft
    {
        get => acquisitionEditor.IsAcquisitionTransparentFlagDraft;
        set => acquisitionEditor.IsAcquisitionTransparentFlagDraft = value;
    }
    public bool IsAcquisitionTexturelessFlagDraft
    {
        get => acquisitionEditor.IsAcquisitionTexturelessFlagDraft;
        set => acquisitionEditor.IsAcquisitionTexturelessFlagDraft = value;
    }
    public bool IsAcquisitionClippedFlagDraft
    {
        get => acquisitionEditor.IsAcquisitionClippedFlagDraft;
        set => acquisitionEditor.IsAcquisitionClippedFlagDraft = value;
    }
    public bool IsAcquisitionLowCoverageFlagDraft
    {
        get => acquisitionEditor.IsAcquisitionLowCoverageFlagDraft;
        set => acquisitionEditor.IsAcquisitionLowCoverageFlagDraft = value;
    }
    public SourceAcquisitionDirectionStateOption? SelectedAcquisitionDirectionStateOption
    {
        get => acquisitionEditor.SelectedAcquisitionDirectionStateOption;
        set => acquisitionEditor.SelectedAcquisitionDirectionStateOption = value;
    }
    public string AcquisitionDirectionXDraft
    {
        get => acquisitionEditor.AcquisitionDirectionXDraft;
        set => acquisitionEditor.AcquisitionDirectionXDraft = value;
    }
    public string AcquisitionDirectionYDraft
    {
        get => acquisitionEditor.AcquisitionDirectionYDraft;
        set => acquisitionEditor.AcquisitionDirectionYDraft = value;
    }
    public string AcquisitionDirectionZDraft
    {
        get => acquisitionEditor.AcquisitionDirectionZDraft;
        set => acquisitionEditor.AcquisitionDirectionZDraft = value;
    }
    public bool IsAcquisitionProvenancePersisted => acquisitionEditor.IsAcquisitionProvenancePersisted;
    public bool IsAcquisitionStateAvailable => acquisitionEditor.IsAcquisitionStateAvailable;
    public bool IsAcquisitionDirectionAvailable => acquisitionEditor.IsAcquisitionDirectionAvailable;
    public string AcquisitionDirectionFrame => acquisitionEditor.AcquisitionDirectionFrame;
    public string AcquisitionDirectionConvention => acquisitionEditor.AcquisitionDirectionConvention;
    public bool IsAcquisitionDirectionPersisted => acquisitionEditor.IsAcquisitionDirectionPersisted;
    public bool HasPendingAcquisitionDirectionChanges => acquisitionEditor.HasPendingAcquisitionDirectionChanges;
    public bool HasPendingAcquisitionProvenanceChanges => acquisitionEditor.HasPendingAcquisitionProvenanceChanges;
    public bool CanApplyAcquisitionProvenance => acquisitionEditor.CanApplyAcquisitionProvenance;
    public bool HasAcquisitionValidationError => acquisitionEditor.HasAcquisitionValidationError;
    public string AcquisitionDraftMessage => acquisitionEditor.AcquisitionDraftMessage;
    public string AcquisitionPersistenceSummary => acquisitionEditor.AcquisitionPersistenceSummary;
    public string AcquisitionDirectionPersistenceSummary => acquisitionEditor.AcquisitionDirectionPersistenceSummary;
    public ICommand ApplyAcquisitionProvenanceCommand => acquisitionEditor.ApplyAcquisitionProvenanceCommand;
    public ICommand ResetAcquisitionProvenanceCommand => acquisitionEditor.ResetAcquisitionProvenanceCommand;
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
        string? frameId = null) =>
        acquisitionEditor.LoadAcquisitionProvenance(acquisitionProvenance, frameId);

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
            && Report is not null
            && !IsLoading)
        {
            return;
        }

        var cancellation = new AsyncLoadCancellation();
        var previousCancellation = Interlocked.Exchange(ref loadCancellation, cancellation);
        previousCancellation?.Cancel();
        var cancellationToken = cancellation.Token;
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

            cancellation.Dispose();
        }
    }

    private async Task ObserveLoadAsync(
        Task task,
        Action<Exception> reportFailure)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // Source Quality owns expected latest-source cancellation.
        }
        catch (Exception exception)
        {
            if (!IsDisposed)
            {
                reportFailure(exception);
            }
        }
        finally
        {
            lock (loadGate)
            {
                if (ReferenceEquals(Volatile.Read(ref loadTask), task))
                {
                    Volatile.Write(ref loadTask, null);
                    Volatile.Write(ref loadObservationTask, null);
                }
            }
        }
    }

    public void Clear()
    {
        var cancellation = Interlocked.Exchange(ref loadCancellation, null);
        cancellation?.Cancel();
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
        Channels.ReplaceAll(CreateChannelItems(Report));
        GridDiagnostics.ReplaceAll(CreateGridDiagnosticItems(Report));
        OnPropertyChanged(nameof(GridDiagnosticsStatus));
        OnPropertyChanged(nameof(GridDiagnosticsState));
        OnPropertyChanged(nameof(GridDiagnosticsSummary));
    }

    private void OnAcquisitionEditorPropertyChanged(object? sender, PropertyChangedEventArgs args) =>
        OnPropertyChanged(args.PropertyName);

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

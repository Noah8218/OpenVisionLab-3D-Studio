using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Read-only presentation of one identified source-quality report. Loading,
/// selection, and inspection remain separate: this type never mutates a recipe
/// and never invokes Preview, Publish, Run, Validation Set, or Save.
/// </summary>
public sealed class SourceQualityWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly ThreeDLocalization localization;
    private CancellationTokenSource? loadCancellation;
    private SourceQualityReport? report;
    private string loadedSourceKey = string.Empty;
    private string error = string.Empty;
    private bool isLoading;
    private int loadGeneration;

    public SourceQualityWorkspaceViewModel(ThreeDLocalization localization)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        localization.PropertyChanged += OnLocalizationChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ThreeDLocalization Localization => localization;
    public SourceQualityReport? Report => report;
    public bool HasReport => Report is not null;

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

    public ResettableObservableCollection<SourceQualityChannelItem> Channels { get; } = [];
    public ResettableObservableCollection<SourceQualityDistributionBinItem> DistributionBins { get; } = [];

    public async Task EnsureSourceAsync(
        string path,
        string entityId,
        string unit,
        string frameId)
    {
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
            var nextReport = await Task.Run(
                () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var snapshot = C3DHeightFieldSnapshot.LoadIdentified(
                        fullPath,
                        entityId,
                        unit,
                        frameId);
                    cancellationToken.ThrowIfCancellationRequested();
                    return C3DSourceQualityAnalyzer.Create(snapshot);
                },
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
        NotifyReportProperties();
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
    }

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(DistributionSummary));
        Channels.ReplaceAll(CreateChannelItems(Report));
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

using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using OpenVisionLab.ThreeD.Presentation.Models;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Owns Height Image loading, coordinate inspection, display-only controls,
/// and the composed ROI interaction surface. Recipe mutation remains outside
/// this type and inspection is never invoked by its presentation commands.
/// </summary>
public sealed class HeightImageViewerViewModel : INotifyPropertyChanged, IDisposable
{
    private const string SignedDeltaSourceKeyPrefix = "signed-delta|";

    private readonly object loadGate = new();
    private readonly ThreeDLocalization localization;
    private readonly SharedHeightCursorSession sharedCursor;
    private readonly bool ownsRoiWorkspace;
    private readonly RelayCommand fitCommand;
    private readonly RelayCommand fitRoiCommand;
    private readonly RelayCommand actualPixelsCommand;
    private readonly RelayCommand zoomInCommand;
    private readonly RelayCommand zoomOutCommand;
    private readonly RelayCommand autoRangeCommand;
    private readonly RelayCommand applyManualRangeCommand;
    private RelayCommand? heightLegendEditCommand;
    private (double Minimum, double Maximum, bool Auto, string Source)? heightLegendEditStart;
    private AsyncLoadCancellation? loadCancellation;
    private C3DHeightImageFrame? frame;
    private C3DHeightImageDisplayFrame? displayFrame;
    private string loadedSourceKey = string.Empty;
    private string status = string.Empty;
    private string hoverSummary = string.Empty;
    private string error = string.Empty;
    private string rangeMinimumText = string.Empty;
    private string rangeMaximumText = string.Empty;
    private string rangeError = string.Empty;
    private bool isLoading;
    private bool isAutoRange = true;
    private bool showInvalidCells = true;
    private double zoomPercent = 100.0;
    private double activeRangeMinimum;
    private double activeRangeMaximum;
    private C3DHeightImagePalette selectedPalette = C3DHeightImagePalette.Height;
    private IReadOnlyList<HeightImagePaletteChoice> paletteChoices = [];
    private IReadOnlyList<C3DCompletenessCellOverlay> completenessCellOverlays = [];
    private string? selectedCompletenessCellId;
    private int loadGeneration;
    private int displayRangeRevision;
    private int disposalState;
    private string loadingSourceKey = string.Empty;
    private Task? sourceLoadTask;
    private Task? loadTask;
    private Task? loadObservationTask;
    private HeightDisplayOptions heightDisplayOptions = new();
    private HeightDisplayOptions? heightLegendOptionsAtStart;
    private string heightDisplayWorkspace = "teach";
    private string heightDisplayMapId = "source";
    private string heightDisplayMapName = string.Empty;
    private readonly Dictionary<string, (string Source, string Mode, double Minimum, double Maximum)> heightDisplayRanges = new();

    public HeightImageViewerViewModel(
        ThreeDLocalization localization,
        SharedHeightCursorSession sharedCursor,
        HeightImageRoiWorkspaceViewModel? roiWorkspace = null)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.sharedCursor = sharedCursor ?? throw new ArgumentNullException(nameof(sharedCursor));
        ownsRoiWorkspace = roiWorkspace is null;
        RoiWorkspace = roiWorkspace ?? new HeightImageRoiWorkspaceViewModel(localization);
        fitCommand = new RelayCommand(_ => DisplayRequest?.Invoke(this, HeightImageDisplayRequest.Fit), _ => HasImage);
        fitRoiCommand = new RelayCommand(_ => DisplayRequest?.Invoke(this, HeightImageDisplayRequest.FitRoi), _ => HasImage && FitRoiRectangle is not null);
        actualPixelsCommand = new RelayCommand(
            _ => DisplayRequest?.Invoke(this, HeightImageDisplayRequest.ActualPixels),
            _ => HasImage);
        zoomInCommand = new RelayCommand(_ => DisplayRequest?.Invoke(this, HeightImageDisplayRequest.ZoomIn), _ => HasImage);
        zoomOutCommand = new RelayCommand(_ => DisplayRequest?.Invoke(this, HeightImageDisplayRequest.ZoomOut), _ => HasImage);
        autoRangeCommand = new RelayCommand(_ => UseAutoRange(), _ => HasImage);
        applyManualRangeCommand = new RelayCommand(
            _ => ApplyManualRange(),
            _ => CanApplyManualRange());
        status = localization.HeightImageUnavailable;
        hoverSummary = localization.HeightImageCoordinateHint;
        paletteChoices = CreatePaletteChoices();
        localization.PropertyChanged += OnLocalizationChanged;
        sharedCursor.PropertyChanged += OnSharedCursorChanged;
        RoiWorkspace.PropertyChanged += OnRoiWorkspaceChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<HeightImageDisplayRequest>? DisplayRequest;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        localization.PropertyChanged -= OnLocalizationChanged;
        sharedCursor.PropertyChanged -= OnSharedCursorChanged;
        RoiWorkspace.PropertyChanged -= OnRoiWorkspaceChanged;
        lock (loadGate)
        {
            Volatile.Write(ref loadTask, null);
            Volatile.Write(ref loadObservationTask, null);
        }
        Clear(localization.HeightImageUnavailable);
        if (ownsRoiWorkspace) RoiWorkspace.Dispose();
    }

    internal bool IsDisposed => Volatile.Read(ref disposalState) != 0;

    internal bool IsObservedLoadRunning =>
        !IsDisposed && Volatile.Read(ref loadTask) is { IsCompleted: false };

    internal Task ObservedLoadTask => Volatile.Read(ref loadObservationTask) ?? Task.CompletedTask;

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

    public C3DHeightImageFrame? Frame
    {
        get => frame;
        private set
        {
            if (ReferenceEquals(frame, value))
            {
                return;
            }

            if (ownsRoiWorkspace) sharedCursor.Clear();
            RememberHeightDisplayRange();
            frame = value;
            if (value is null || heightDisplayOptions.Measurement?.MatchesSource(value.SourceContentSha256, value.Width, value.Height) != true)
                heightDisplayOptions = heightDisplayOptions with { Measurement = null,
                    ColorStyle = heightDisplayOptions.ColorStyle == "tolerance" ? "continuous" : heightDisplayOptions.ColorStyle };
            ResetDisplayRangeForFrame();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(HeightLegendHistogram));
            OnPropertyChanged(nameof(ImageSummary));
            RefreshSharedCursorPresentation();
            RaiseCommandState();
        }
    }

    public IReadOnlyList<int> HeightLegendHistogram => Frame?.DisplayHistogramBins ?? [];
    public bool HasImage => Frame is not null;
    public HeightDisplayOptions HeightDisplayOptions => heightDisplayOptions with { Palette = SelectedPalette == C3DHeightImagePalette.Monochrome ? "Solid" : SelectedPalette.ToString() };
    public string HeightDisplayWorkspace => heightDisplayWorkspace;
    public string HeightDisplayMapId => heightDisplayMapId;
    public bool CanUseMeasurementColor => Frame is { } current && heightDisplayOptions.Measurement?.MatchesSource(
        current.SourceContentSha256, current.Width, current.Height) == true;
    public string MeasurementColorSummary => CanUseMeasurementColor && heightDisplayOptions.Measurement is { } measurement
        ? $"{measurement.ToolName} · {measurement.LowerLimit:0.##} – {measurement.UpperLimit:0.##} {measurement.Unit}" : string.Empty;

    internal void SetHeightMeasurementColor(HeightMeasurementColorSnapshot? snapshot)
    {
        if (snapshot is not null && (Frame is not { } current || !snapshot.MatchesSource(current.SourceContentSha256, current.Width, current.Height))) snapshot = null;
        if (ReferenceEquals(heightDisplayOptions.Measurement, snapshot)) return;
        heightLegendEditStart = null;
        heightLegendOptionsAtStart = null;
        heightDisplayOptions = heightDisplayOptions with { Measurement = snapshot, IsRangeDragActive = false,
            ColorStyle = snapshot is null && heightDisplayOptions.ColorStyle == "tolerance" ? "continuous" : heightDisplayOptions.ColorStyle };
        NotifyRangeState();
        RenderActiveDisplay();
        DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
    }

    public bool HasHeightDisplayRegion => Frame is { } current && C3DHeightImageFrame.TryGetRegionDisplayRange(current.DisplayValues.Span,
        current.Width, current.Height, FitRoiRectangle, out _, out _);
    public bool IsHeightLegendCollapsed
    {
        get => heightDisplayOptions.IsCollapsed;
        set { if (value != heightDisplayOptions.IsCollapsed) TryApplyHeightDisplayOptions(HeightDisplayOptions with { IsCollapsed = value }); }
    }
    public string HeightLegendInformation
    {
        get
        {
            if (Frame is not { } current) return string.Empty;
            if (heightDisplayOptions.ColorStyle == "tolerance" && CanUseMeasurementColor && heightDisplayOptions.Measurement is { } measurement)
            {
                var measuredOutside = C3DHeightImageFrame.GetOutsideDisplayPercentages(C3DHeightImageFrame.SortDisplaySamples(measurement.Values.Span), measurement.LowerLimit, measurement.UpperLimit);
                return localization.Resolve("ThreeD.Legend.MeasuredInformation",
                    $"{MeasurementColorSummary}\n측정 영역 · 실행 당시 허용 범위\n미만 {measuredOutside.Below:0.0}% · 초과 {measuredOutside.Above:0.0}%",
                    $"{MeasurementColorSummary}\nMeasured region · limits at execution\nBelow {measuredOutside.Below:0.0}% · above {measuredOutside.Above:0.0}%");
            }
            var outside = C3DHeightImageFrame.GetOutsideDisplayPercentages(current.SortedDisplaySamples.Span, activeRangeMinimum, activeRangeMaximum);
            return localization.Resolve("ThreeD.Legend.Information",
                $"결측 {GetMissingPercentage():0.0}%\n표시 범위 미만 ↓{outside.Below:0.0}% · 초과 ↑{outside.Above:0.0}%",
                $"Missing {GetMissingPercentage():0.0}%\nBelow display range ↓{outside.Below:0.0}% · above ↑{outside.Above:0.0}%");
        }
    }

    public ThreeDLocalization Localization => localization;
    public HeightImageRoiWorkspaceViewModel RoiWorkspace { get; }
    public IReadOnlyList<C3DCompletenessCellOverlay> CompletenessCellOverlays =>
        completenessCellOverlays;
    public string? SelectedCompletenessCellId => selectedCompletenessCellId;
    public C3DHeightImageDisplayFrame? DisplayFrame
    {
        get => displayFrame;
        private set
        {
            if (ReferenceEquals(displayFrame, value))
            {
                return;
            }

            displayFrame = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayPixelSha256));
            OnPropertyChanged(nameof(InvalidOverlayPixelCount));
            OnPropertyChanged(nameof(InvalidOverlaySummary));
        }
    }

    public void SetCompletenessCellOverlays(
        IReadOnlyList<C3DCompletenessCellOverlay> overlays)
    {
        ArgumentNullException.ThrowIfNull(overlays);
        if (completenessCellOverlays.SequenceEqual(overlays))
        {
            return;
        }

        completenessCellOverlays = overlays.ToArray();
        OnPropertyChanged(nameof(CompletenessCellOverlays));
    }

    public void SetSelectedCompletenessCellId(string? cellId)
    {
        if (string.Equals(
            selectedCompletenessCellId,
            cellId,
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        selectedCompletenessCellId = cellId;
        OnPropertyChanged(nameof(SelectedCompletenessCellId));
    }

    public string DisplayPixelSha256 => DisplayFrame?.PixelSha256 ?? string.Empty;
    public int InvalidOverlayPixelCount => DisplayFrame?.InvalidOverlayPixelCount ?? 0;
    public string InvalidOverlaySummary => Frame is null
        ? localization.HeightImageInvalidOverlayUnavailable
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{(ShowInvalidCells ? localization.HeightImageInvalidOverlayLegend : localization.HeightImageInvalidOverlayHidden)} {Frame.MissingCount:N0} ({GetMissingPercentage():0.0}%)");

    public bool ShowInvalidCells
    {
        get => showInvalidCells;
        set
        {
            if (!SetField(ref showInvalidCells, value))
            {
                return;
            }

            OnPropertyChanged(nameof(InvalidOverlaySummary));
            RenderActiveDisplay();
        }
    }

    public bool IsLoading
    {
        get => isLoading;
        private set => SetField(ref isLoading, value);
    }

    public string Status
    {
        get => status;
        private set => SetField(ref status, value);
    }

    public string Error
    {
        get => error;
        private set
        {
            if (SetField(ref error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public string HoverSummary
    {
        get => hoverSummary;
        private set => SetField(ref hoverSummary, value);
    }

    public bool HasLinkedCursor => GetMatchingCursor() is not null;
    public SharedHeightCursorOrigin LinkedCursorOrigin => GetMatchingCursor()?.Origin ?? SharedHeightCursorOrigin.None;
    public int LinkedCursorRow => GetMatchingCursor()?.Row ?? -1;
    public int LinkedCursorColumn => GetMatchingCursor()?.Column ?? -1;
    public bool LinkedCursorIsValid => GetMatchingCursor()?.IsValid == true;
    public string CursorCoordinateSummary => FormatCursorCoordinates(includeUnit: true);
    public string CursorCoordinateValueSummary => FormatCursorCoordinates(includeUnit: false);

    public double ZoomPercent
    {
        get => zoomPercent;
        private set => SetField(ref zoomPercent, value);
    }

    public string ZoomSummary => string.Create(CultureInfo.InvariantCulture, $"{ZoomPercent:0.#}%");
    public string PixelsPerCellSummary => string.Create(CultureInfo.InvariantCulture, $"{ZoomPercent / 100:0.00} px/{localization.ViewerCellUnit}");
    public ToolRecipeGridRectangle? FitRoiRectangle => RoiWorkspace.VisibleOverlays.FirstOrDefault(item => item.IsActive)?.Rectangle;

    public string ImageSummary => Frame is null
        ? localization.HeightImageUnavailable
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{Frame.Width} \u00d7 {Frame.Height} | valid {Frame.ValidCount:N0} | missing {Frame.MissingCount:N0}");

    public IReadOnlyList<HeightImagePaletteChoice> PaletteChoices
    {
        get => paletteChoices;
        private set => SetField(ref paletteChoices, value);
    }

    public HeightImagePaletteChoice? SelectedPaletteChoice
    {
        get => PaletteChoices.FirstOrDefault(choice => choice.Value == SelectedPalette);
        set
        {
            if (value is not null)
            {
                SelectedPalette = value.Value;
            }
        }
    }

    public C3DHeightImagePalette SelectedPalette
    {
        get => selectedPalette;
        set
        {
            if (!Enum.IsDefined(value) || !SetField(ref selectedPalette, value))
            {
                return;
            }

            OnPropertyChanged(nameof(SelectedPaletteChoice));
            OnPropertyChanged(nameof(LegendColor0));
            OnPropertyChanged(nameof(LegendColor1));
            OnPropertyChanged(nameof(LegendColor2));
            OnPropertyChanged(nameof(LegendColor3));
            OnPropertyChanged(nameof(HeightLegendGradient));
            OnPropertyChanged(nameof(DisplayRangeSummary));
            RenderActiveDisplay();
        }
    }

    public ICommand HeightLegendEditCommand => heightLegendEditCommand ??= new RelayCommand(HandleHeightLegendEdit, _ => HasImage && !IsDisposed);
    public double HeightLegendMinimum => activeRangeMinimum;
    public double HeightLegendMaximum => activeRangeMaximum;
    public Brush HeightLegendGradient
    {
        get
        {
            var brush = new LinearGradientBrush { StartPoint = new System.Windows.Point(0.5, 1), EndPoint = new System.Windows.Point(0.5, 0) };
            foreach (var stop in C3DPointMapPalette.DisplayStops(activeRangeMinimum, activeRangeMaximum,
                Frame?.Minimum ?? 0, Frame?.Maximum ?? 1, HeightDisplayOptions.Palette, heightDisplayOptions))
                brush.GradientStops.Add(new GradientStop(Color.FromRgb((byte)(stop.Color.R * 255), (byte)(stop.Color.G * 255), (byte)(stop.Color.B * 255)), stop.Offset));
            brush.Freeze();
            return brush;
        }
    }

    public bool IsAutoRange
    {
        get => isAutoRange;
        private set
        {
            if (SetField(ref isAutoRange, value))
            {
                OnPropertyChanged(nameof(DisplayRangeMode));
                OnPropertyChanged(nameof(DisplayRangeSummary));
            }
        }
    }

    public string RangeMinimumText
    {
        get => rangeMinimumText;
        set
        {
            if (SetField(ref rangeMinimumText, value ?? string.Empty))
            {
                ValidateRangeDraft();
            }
        }
    }

    public string RangeMaximumText
    {
        get => rangeMaximumText;
        set
        {
            if (SetField(ref rangeMaximumText, value ?? string.Empty))
            {
                ValidateRangeDraft();
            }
        }
    }

    public string RangeError
    {
        get => rangeError;
        private set
        {
            if (SetField(ref rangeError, value))
            {
                OnPropertyChanged(nameof(HasRangeError));
            }
        }
    }

    public bool HasRangeError => !string.IsNullOrWhiteSpace(RangeError);
    public string DisplayRangeMode => heightDisplayOptions.ColorStyle == "tolerance"
        ? localization.Resolve("ThreeD.Legend.Mode.tolerance", "허용 범위 색", "Tolerance colours") : localization.Resolve("ThreeD.Legend.Mode." + heightDisplayOptions.RangeMode,
        heightDisplayOptions.RangeMode switch { "robust" => "자동·분포", "roi" => "영역 기준", "manual" => "수동", _ => "자동·전체" },
        heightDisplayOptions.RangeMode switch { "robust" => "Auto · distribution", "roi" => "Region", "manual" => "Manual", _ => "Auto · full" });
    public string DisplayRangeSummary => Frame is null
        ? localization.HeightImageUnavailable
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{DisplayRangeMode} | {activeRangeMinimum:0.##} \u2026 {activeRangeMaximum:0.##} {Frame.Unit} | {SelectedPalette}");
    public string LegendMaximum => Frame is null
        ? "\u2014"
        : activeRangeMaximum.ToString("0.00", CultureInfo.InvariantCulture);
    public string LegendMinimum => Frame is null
        ? "\u2014"
        : activeRangeMinimum.ToString("0.00", CultureInfo.InvariantCulture);
    public string LegendUnit => Frame?.Unit ?? string.Empty;
    public string LegendUpperQuartile => Frame is null ? "\u2014" : (activeRangeMaximum * 0.75 + activeRangeMinimum * 0.25).ToString("0.00", CultureInfo.InvariantCulture);
    public string LegendMidpoint => Frame is null ? "\u2014" : (activeRangeMaximum * 0.5 + activeRangeMinimum * 0.5).ToString("0.00", CultureInfo.InvariantCulture);
    public string LegendLowerQuartile => Frame is null ? "\u2014" : (activeRangeMaximum * 0.25 + activeRangeMinimum * 0.75).ToString("0.00", CultureInfo.InvariantCulture);
    public IReadOnlyList<string> LegendTickLabels => Frame is null ? Array.Empty<string>()
        : Enumerable.Range(0, 5).Select(index => (activeRangeMaximum * (1 - index / 4.0) + activeRangeMinimum * (index / 4.0)).ToString("0.00", CultureInfo.InvariantCulture)).ToArray();
    public string LegendColor0 => GetPaletteColor(0.0);
    public string LegendColor1 => GetPaletteColor(1.0 / 3.0);
    public string LegendColor2 => GetPaletteColor(2.0 / 3.0);
    public string LegendColor3 => GetPaletteColor(1.0);
    public int DisplayRangeRevision
    {
        get => displayRangeRevision;
        private set => SetField(ref displayRangeRevision, value);
    }

    public ICommand FitCommand => fitCommand;
    public ICommand FitRoiCommand => fitRoiCommand;
    public ICommand ActualPixelsCommand => actualPixelsCommand;
    public ICommand ZoomInCommand => zoomInCommand;
    public ICommand ZoomOutCommand => zoomOutCommand;
    public ICommand AutoRangeCommand => autoRangeCommand;
    public ICommand ApplyManualRangeCommand => applyManualRangeCommand;

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

    public bool ShowSignedDeltaArtifact(C3DSignedHeightDeltaArtifact artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (IsDisposed)
        {
            return false;
        }

        try
        {
            Clear(localization.HeightImageUnavailable);
            var nextFrame = C3DHeightImageFrame.Create(artifact);
            loadedSourceKey = $"{SignedDeltaSourceKeyPrefix}{artifact.ContentSha256}";
            Frame = nextFrame;
            Error = string.Empty;
            Status = ImageSummary;
            DisplayRequest?.Invoke(this, HeightImageDisplayRequest.Fit);
            return true;
        }
        catch (Exception exception)
        {
            loadedSourceKey = string.Empty;
            Frame = null;
            Error = exception.Message;
            Status = localization.HeightImageUnavailable;
            return false;
        }
    }

    internal bool ShowSignedDeltaArtifactIfChanged(C3DSignedHeightDeltaArtifact artifact) =>
        Frame is not null
        && string.Equals(loadedSourceKey, $"{SignedDeltaSourceKeyPrefix}{artifact.ContentSha256}", StringComparison.OrdinalIgnoreCase)
            || ShowSignedDeltaArtifact(artifact);

    internal void ClearSignedDeltaArtifact()
    {
        if (loadedSourceKey.StartsWith(SignedDeltaSourceKeyPrefix, StringComparison.Ordinal))
        {
            Clear(localization.HeightImageUnavailable);
        }
    }

    internal Task EnsureSourceAsync(
        string path,
        string entityId,
        string unit,
        string frameId,
        Func<CancellationToken, Task<C3DHeightFieldSnapshot>> loadSourceAsync)
    {
        ArgumentNullException.ThrowIfNull(loadSourceAsync);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            Clear(localization.HeightImageUnavailable);
            return Task.CompletedTask;
        }

        if (Frame is not null
            && loadedSourceKey.StartsWith(SignedDeltaSourceKeyPrefix, StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        var fullPath = Path.GetFullPath(path);
        var sourceKey = string.Join(
            "|",
            fullPath,
            new FileInfo(fullPath).Length.ToString(CultureInfo.InvariantCulture),
            File.GetLastWriteTimeUtc(fullPath).Ticks.ToString(CultureInfo.InvariantCulture),
            entityId,
            unit,
            frameId);
        AsyncLoadCancellation? previousCancellation;
        AsyncLoadCancellation cancellation;
        TaskCompletionSource<bool> completion;
        lock (loadGate)
        {
            if (string.Equals(loadedSourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
                && Frame is not null)
            {
                return Task.CompletedTask;
            }

            if (string.Equals(loadingSourceKey, sourceKey, StringComparison.OrdinalIgnoreCase)
                && sourceLoadTask is { IsCompleted: false } existingTask)
            {
                return existingTask;
            }

            cancellation = new AsyncLoadCancellation();
            previousCancellation = loadCancellation;
            loadCancellation = cancellation;
            var generation = ++loadGeneration;
            loadingSourceKey = sourceKey;
            completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            sourceLoadTask = completion.Task;
            IsLoading = true;
            Error = string.Empty;
            Status = localization.HeightImageLoading;
            HoverSummary = localization.HeightImageCoordinateHint;

            _ = RunSourceLoadAsync(
                sourceKey,
                entityId,
                unit,
                frameId,
                loadSourceAsync,
                cancellation,
                generation,
                completion);
        }

        previousCancellation?.Cancel();
        return completion.Task;
    }

    private async Task RunSourceLoadAsync(
        string sourceKey,
        string entityId,
        string unit,
        string frameId,
        Func<CancellationToken, Task<C3DHeightFieldSnapshot>> loadSourceAsync,
        AsyncLoadCancellation cancellation,
        int generation,
        TaskCompletionSource<bool> completion)
    {
        try
        {
            var source = await loadSourceAsync(cancellation.Token);
            var nextFrame = await Task.Run(
                () => C3DHeightImageFrame.Create(source, cancellation.Token),
                cancellation.Token);

            if (generation != loadGeneration || cancellation.Token.IsCancellationRequested)
            {
                return;
            }

            loadedSourceKey = sourceKey;
            Frame = nextFrame;
            Status = ImageSummary;
            DisplayRequest?.Invoke(this, HeightImageDisplayRequest.Fit);
        }
        catch (OperationCanceledException)
        {
            // A newer source owns the visible state.
        }
        catch (Exception exception)
        {
            if (generation == loadGeneration)
            {
                loadedSourceKey = string.Empty;
                Frame = null;
                Error = exception.Message;
                Status = localization.HeightImageUnavailable;
            }
        }
        finally
        {
            if (generation == loadGeneration)
            {
                IsLoading = false;
            }

            cancellation.Dispose();
            completion.TrySetResult(true);
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
            // Height Image owns expected latest-source cancellation.
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

    public void UpdateHover(int pixelX, int pixelY)
    {
        if (Frame?.TryGetCell(pixelX, pixelY, out var cell) != true)
        {
            ClearHover();
            return;
        }

        sharedCursor.Update(
            SharedHeightCursorOrigin.HeightImage,
            Frame.SourceContentSha256,
            cell.Row,
            cell.Column,
            cell.RawHeight,
            cell.IsValid);
    }

    public void ClearHover() =>
        sharedCursor.Clear(SharedHeightCursorOrigin.HeightImage);

    internal async Task EnsureSharedSourceAsync(string path, Func<Task<(C3DHeightImageFrame? Frame, string Error)>> loadFrame)
    {
        if (IsDisposed) return;
        var generation = ++loadGeneration;
        IsLoading = true;
        var result = await loadFrame();
        if (IsDisposed || generation != loadGeneration) return;
        if (result.Frame is { } next && string.Equals(next.SourcePath, path, StringComparison.OrdinalIgnoreCase))
        {
            Frame = next;
            loadedSourceKey = $"shared-input|{path}|{next.SourceContentSha256}";
            Error = string.Empty;
            Status = ImageSummary;
        }
        else
        {
            Frame = null;
            Error = result.Error;
            Status = localization.HeightImageUnavailable;
        }
        IsLoading = false;
    }

    internal void ClearSource() => Clear(localization.HeightImageUnavailable);

    internal void ReportSourceFailure(string message)
    {
        Clear(localization.HeightImageUnavailable);
        Error = message;
    }

    public void SetZoom(double percent)
    {
        var normalized = Math.Clamp(percent, 1.0, 3200.0);
        if (Math.Abs(ZoomPercent - normalized) < 0.01)
        {
            return;
        }

        ZoomPercent = normalized;
        OnPropertyChanged(nameof(ZoomSummary));
        OnPropertyChanged(nameof(PixelsPerCellSummary));
    }

    public void SetHeightDisplayContext(string workspace, string mapId, string mapName)
    {
        if (heightDisplayWorkspace == workspace && heightDisplayMapId == mapId && heightDisplayMapName == mapName) return;
        RememberHeightDisplayRange();
        heightDisplayWorkspace = workspace;
        heightDisplayMapId = mapId;
        heightDisplayMapName = mapName;
        ResetDisplayRangeForFrame();
    }

    public void SetHeightDisplayLinked(bool linked)
    {
        if (heightDisplayOptions.IsLinked == linked) return;
        heightDisplayOptions = heightDisplayOptions with { IsLinked = linked };
        NotifyRangeState();
        DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
    }

    public IReadOnlyDictionary<string, string> CaptureHeightDisplayModes()
    {
        RememberHeightDisplayRange();
        return heightDisplayRanges.Where(entry => entry.Value.Mode is "robust" or "roi").ToDictionary(entry => entry.Key, entry => entry.Value.Mode);
    }

    public void RestoreHeightDisplayModes(IReadOnlyDictionary<string, string> modes)
    {
        heightDisplayRanges.Clear();
        foreach (var entry in modes.Where(HeightDisplayOptions.IsStoredMapMode))
            heightDisplayRanges[entry.Key] = (string.Empty, entry.Value, double.NaN, double.NaN);
        heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto" };
        ResetDisplayRangeForFrame();
    }

    public bool TryApplyHeightDisplayOptions(HeightDisplayOptions options, double? minimum = null, double? maximum = null)
    {
        var next = options.Sanitize() with { Measurement = heightDisplayOptions.Measurement };
        if (Frame is not null && next.RangeMode == "roi" && !HasHeightDisplayRegion
            || next.ColorStyle == "tolerance" && !CanUseMeasurementColor) return false;
        if (next.RangeMode == "manual" && minimum.HasValue && (!maximum.HasValue || !double.IsFinite(minimum.Value)
            || !double.IsFinite(maximum.Value) || minimum.Value >= maximum.Value)) return false;
        var wasPinned = heightDisplayOptions.KeepsRange(heightDisplayWorkspace);
        heightDisplayOptions = next;
        SelectedPalette = next.Palette == "Solid" ? C3DHeightImagePalette.Monochrome : Enum.Parse<C3DHeightImagePalette>(next.Palette);
        if (wasPinned && !next.KeepsRange(heightDisplayWorkspace) && next.RangeMode == "manual")
            heightDisplayOptions = next with { RangeMode = "auto" };
        if (heightDisplayOptions.RangeMode == "manual") ApplyManualRange(minimum ?? activeRangeMinimum, maximum ?? activeRangeMaximum);
        else ApplyAutomaticDisplayRange();
        NotifyRangeState();
        return true;
    }

    public bool TryApplyLinkedDisplayState(HeightDisplayOptions options, double minimum, double maximum)
    {
        if (Frame is null || !double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum > maximum) return false;
        if (HeightDisplayOptions == options && activeRangeMinimum == minimum && activeRangeMaximum == maximum) return true;
        var measurement = options.Measurement;
        if (measurement?.MatchesSource(Frame.SourceContentSha256, Frame.Width, Frame.Height) != true) measurement = null;
        heightDisplayOptions = options.Sanitize() with { Measurement = measurement,
            ColorStyle = measurement is null && options.ColorStyle == "tolerance" ? "continuous" : options.ColorStyle };
        SelectedPalette = options.Palette == "Solid" ? C3DHeightImagePalette.Monochrome : Enum.Parse<C3DHeightImagePalette>(heightDisplayOptions.Palette);
        activeRangeMinimum = minimum;
        activeRangeMaximum = maximum;
        isAutoRange = options.RangeMode != "manual";
        rangeMinimumText = FormatRangeValue(minimum);
        rangeMaximumText = FormatRangeValue(maximum);
        NotifyRangeState();
        RenderActiveDisplay();
        DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
        return true;
    }

    private void RememberHeightDisplayRange()
    {
        if (Frame is not { } current) return;
        heightDisplayRanges[heightDisplayWorkspace + "|" + heightDisplayMapId] =
            (current.SourceContentSha256, heightDisplayOptions.RangeMode, activeRangeMinimum, activeRangeMaximum);
    }
    public bool TryApplyManualRange(double minimum, double maximum)
    {
        RangeMinimumText = FormatRangeValue(minimum);
        RangeMaximumText = FormatRangeValue(maximum);
        if (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum >= maximum)
        {
            RangeError = localization.HeightImageRangeInvalid;
            return false;
        }

        ApplyManualRange(minimum, maximum);
        return !IsAutoRange;
    }

    public bool TryApplyLinkedDisplayRange(double minimum, double maximum)
    {
        if (Frame is null
            || !double.IsFinite(minimum)
            || !double.IsFinite(maximum)
            || minimum >= maximum)
        {
            return false;
        }

        ApplyManualRange(minimum, maximum);
        return true;
    }

    public void UseAutoRange()
    {
        heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto", IsRangeDragActive = false,
            ColorStyle = heightDisplayOptions.ColorStyle == "tolerance" ? "continuous" : heightDisplayOptions.ColorStyle };
        ApplyAutomaticDisplayRange();
    }

    private void ApplyAutomaticDisplayRange()
    {
        if (Frame is null)
        {
            return;
        }

        IsAutoRange = true;
        activeRangeMinimum = Frame.DefaultDisplayMinimum;
        activeRangeMaximum = Frame.DefaultDisplayMaximum;
        if (heightDisplayOptions.RangeMode == "robust")
            (activeRangeMinimum, activeRangeMaximum) = C3DHeightImageFrame.GetDistributionDisplayRange(Frame.SortedDisplaySamples.Span);
        else if (heightDisplayOptions.RangeMode == "roi"
            && !C3DHeightImageFrame.TryGetRegionDisplayRange(Frame.DisplayValues.Span, Frame.Width, Frame.Height,
                FitRoiRectangle, out activeRangeMinimum, out activeRangeMaximum))
        {
            heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto" };
            (activeRangeMinimum, activeRangeMaximum) = (Frame.DefaultDisplayMinimum, Frame.DefaultDisplayMaximum);
        }
        if (Frame.IsSignedDelta)
        {
            var radius = Math.Max(Math.Abs(activeRangeMinimum), Math.Abs(activeRangeMaximum));
            (activeRangeMinimum, activeRangeMaximum) = (-radius, radius);
        }
        RangeMinimumText = FormatRangeValue(activeRangeMinimum);
        RangeMaximumText = FormatRangeValue(activeRangeMaximum);
        RangeError = string.Empty;
        NotifyRangeState();
        RenderActiveDisplay();
        DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
    }

    private void Clear(string nextStatus)
    {
        AsyncLoadCancellation? cancellation;
        lock (loadGate)
        {
            cancellation = loadCancellation;
            loadCancellation = null;
            loadingSourceKey = string.Empty;
            sourceLoadTask = null;
            loadGeneration++;
        }
        cancellation?.Cancel();
        loadedSourceKey = string.Empty;
        Frame = null;
        DisplayFrame = null;
        IsLoading = false;
        Error = string.Empty;
        Status = nextStatus;
        HoverSummary = localization.HeightImageCoordinateHint;
    }

    private void RaiseCommandState()
    {
        fitCommand.RaiseCanExecuteChanged();
        fitRoiCommand.RaiseCanExecuteChanged();
        actualPixelsCommand.RaiseCanExecuteChanged();
        zoomInCommand.RaiseCanExecuteChanged();
        zoomOutCommand.RaiseCanExecuteChanged();
        autoRangeCommand.RaiseCanExecuteChanged();
        applyManualRangeCommand.RaiseCanExecuteChanged();
    }

    private void HandleHeightLegendEdit(object? parameter)
    {
        if (parameter is not HeightLegendEditRequest request || Frame is not { } current) return;
        RangeError = string.Empty;
        var minimum = activeRangeMinimum;
        var maximum = activeRangeMaximum;
        switch (request.Kind)
        {
            case HeightLegendEditKind.Begin:
                heightLegendOptionsAtStart = heightDisplayOptions;
                heightLegendEditStart = (minimum, maximum, IsAutoRange, current.SourceContentSha256);
                heightDisplayOptions = heightDisplayOptions with { IsRangeDragActive = true };
                NotifyRangeState();
                DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
                return;
            case HeightLegendEditKind.Cancel:
                if (heightLegendEditStart is { } start && start.Source == current.SourceContentSha256)
                {
                    ApplyManualRange(start.Minimum, start.Maximum);
                    heightDisplayOptions = heightLegendOptionsAtStart ?? heightDisplayOptions;
                    IsAutoRange = start.Auto;
                    NotifyRangeState();
                    DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
                }
                heightLegendEditStart = null;
                return;
            case HeightLegendEditKind.Commit:
                heightLegendEditStart = null;
                heightDisplayOptions = heightDisplayOptions with { IsRangeDragActive = false };
                NotifyRangeState();
                DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
                return;
            case HeightLegendEditKind.Reset:
                UseAutoRange();
                heightLegendEditStart = null;
                return;
            case HeightLegendEditKind.InputLower:
            case HeightLegendEditKind.InputUpper:
                if (heightLegendEditStart?.Source != current.SourceContentSha256)
                { RangeError = localization.Resolve("ThreeD.Legend.InputChanged", "입력이 바뀌었습니다. 값을 다시 여세요.", "Input changed. Reopen the value editor."); return; }
                if (!TryParseRangeValue(request.Text, out var number)) { RangeError = localization.HeightImageRangeInvalid; return; }
                if (request.Kind == HeightLegendEditKind.InputLower) minimum = number; else maximum = number;
                if (minimum >= maximum) { RangeError = localization.HeightImageRangeInvalid; return; }
                break;
            case HeightLegendEditKind.Lower: minimum = request.Value; break;
            case HeightLegendEditKind.Upper: maximum = request.Value; break;
            case HeightLegendEditKind.Shift:
                if (current.IsSignedDelta) return;
                minimum += request.Value; maximum += request.Value;
                break;
            case HeightLegendEditKind.Scale:
                if (!double.IsFinite(request.Value) || request.Value <= 0 || !double.IsFinite(request.Pivot)) return;
                if (current.IsSignedDelta) { maximum *= request.Value; minimum = -maximum; }
                else
                {
                    minimum = request.Pivot - (request.Pivot - minimum) * request.Value;
                    maximum = request.Pivot + (maximum - request.Pivot) * request.Value;
                }
                break;
        }
        if (current.IsSignedDelta && request.Kind is HeightLegendEditKind.Lower or HeightLegendEditKind.InputLower) maximum = -minimum;
        if (current.IsSignedDelta && request.Kind is HeightLegendEditKind.Upper or HeightLegendEditKind.InputUpper) minimum = -maximum;
        if (minimum >= maximum && request.Kind is HeightLegendEditKind.InputLower or HeightLegendEditKind.InputUpper)
        { RangeError = localization.HeightImageRangeInvalid; return; }
        if (double.IsFinite(minimum) && double.IsFinite(maximum) && minimum < maximum) ApplyManualRange(minimum, maximum);
    }

    private void ResetDisplayRangeForFrame()
    {
        heightLegendEditStart = null;
        heightDisplayOptions = heightDisplayOptions with { IsRangeDragActive = false };
        if (Frame is null)
        {
            DisplayFrame = null;
            isAutoRange = true;
            activeRangeMinimum = 0;
            activeRangeMaximum = 0;
            rangeMinimumText = string.Empty;
            rangeMaximumText = string.Empty;
            RangeError = string.Empty;
            NotifyRangeState();
            return;
        }

        if (heightDisplayRanges.TryGetValue(heightDisplayWorkspace + "|" + heightDisplayMapId, out var saved)
            && (saved.Mode != "manual" || saved.Source == Frame.SourceContentSha256 || heightDisplayOptions.KeepsRange(heightDisplayWorkspace)))
        {
            heightDisplayOptions = heightDisplayOptions with { RangeMode = saved.Mode };
            if (saved.Mode == "manual") { ApplyManualRange(saved.Minimum, saved.Maximum); return; }
        }
        else if (heightDisplayOptions.RangeMode == "manual") heightDisplayOptions = heightDisplayOptions with { RangeMode = "auto" };
        ApplyAutomaticDisplayRange();
    }

    private void ApplyManualRange()
    {
        if (!TryReadManualRange(out var minimum, out var maximum))
        {
            ValidateRangeDraft();
            return;
        }

        ApplyManualRange(minimum, maximum);
    }

    private void ApplyManualRange(double minimum, double maximum)
    {
        if (Frame?.IsSignedDelta == true)
        {
            var radius = Math.Max(Math.Abs(minimum), Math.Abs(maximum));
            (minimum, maximum) = (-radius, radius);
        }
        heightDisplayOptions = heightDisplayOptions with { RangeMode = "manual" };
        IsAutoRange = false;
        activeRangeMinimum = minimum;
        activeRangeMaximum = maximum;
        RangeMinimumText = FormatRangeValue(minimum);
        RangeMaximumText = FormatRangeValue(maximum);
        RangeError = string.Empty;
        NotifyRangeState();
        RenderActiveDisplay();
        DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
    }

    private void RenderActiveDisplay()
    {
        if (Frame is null)
        {
            DisplayFrame = null;
            return;
        }

        DisplayFrame = IsAutoRange
                       && SelectedPalette == C3DHeightImagePalette.Height
                       && activeRangeMinimum == Frame.DefaultDisplayMinimum
                       && activeRangeMaximum == Frame.DefaultDisplayMaximum
                       && !ShowInvalidCells
                       && heightDisplayOptions.ColorStyle == "continuous" && !heightDisplayOptions.HighlightOutside
            ? Frame.DefaultDisplayFrame
            : Frame.CreateDisplayFrame(
                SelectedPalette,
                activeRangeMinimum,
                activeRangeMaximum,
                ShowInvalidCells
                    ? C3DHeightImageInvalidOverlayMode.Visible
                    : C3DHeightImageInvalidOverlayMode.Hidden,
                heightDisplayOptions);
    }

    private void ValidateRangeDraft()
    {
        RangeError = TryReadManualRange(out _, out _)
            ? string.Empty
            : localization.HeightImageRangeInvalid;
        applyManualRangeCommand.RaiseCanExecuteChanged();
    }

    private bool TryReadManualRange(out double minimum, out double maximum)
    {
        // An unchanged display string retains its raw endpoint, including ranges narrower than 0.01.
        var minimumValid = double.IsFinite(activeRangeMinimum) && RangeMinimumText == FormatRangeValue(activeRangeMinimum);
        minimum = minimumValid ? activeRangeMinimum : 0;
        if (!minimumValid) minimumValid = TryParseRangeValue(RangeMinimumText, out minimum);
        var maximumValid = double.IsFinite(activeRangeMaximum) && RangeMaximumText == FormatRangeValue(activeRangeMaximum);
        maximum = maximumValid ? activeRangeMaximum : 0;
        if (!maximumValid) maximumValid = TryParseRangeValue(RangeMaximumText, out maximum);
        return minimumValid && maximumValid && minimum < maximum;
    }

    private bool CanApplyManualRange() =>
        HasImage && TryReadManualRange(out _, out _);

    private void NotifyRangeState()
    {
        OnPropertyChanged(nameof(IsAutoRange));
        OnPropertyChanged(nameof(CanUseMeasurementColor));
        OnPropertyChanged(nameof(MeasurementColorSummary));
        OnPropertyChanged(nameof(HeightDisplayOptions));
        OnPropertyChanged(nameof(HeightDisplayWorkspace));
        OnPropertyChanged(nameof(HeightDisplayMapId));
        OnPropertyChanged(nameof(HasHeightDisplayRegion));
        OnPropertyChanged(nameof(IsHeightLegendCollapsed));
        OnPropertyChanged(nameof(HeightLegendInformation));
        OnPropertyChanged(nameof(HeightLegendGradient));
        OnPropertyChanged(nameof(DisplayRangeMode));
        OnPropertyChanged(nameof(DisplayRangeSummary));
        OnPropertyChanged(nameof(RangeMinimumText));
        OnPropertyChanged(nameof(RangeMaximumText));
        OnPropertyChanged(nameof(HeightLegendMinimum));
        OnPropertyChanged(nameof(HeightLegendMaximum));
        OnPropertyChanged(nameof(LegendMinimum));
        OnPropertyChanged(nameof(LegendMaximum));
        OnPropertyChanged(nameof(LegendUnit));
        OnPropertyChanged(nameof(LegendTickLabels));
        OnPropertyChanged(nameof(LegendUpperQuartile));
        OnPropertyChanged(nameof(LegendMidpoint));
        OnPropertyChanged(nameof(LegendLowerQuartile));
        applyManualRangeCommand.RaiseCanExecuteChanged();
    }

    private IReadOnlyList<HeightImagePaletteChoice> CreatePaletteChoices() =>
    [
        new(C3DHeightImagePalette.Height, GetPaletteDisplayName(C3DHeightImagePalette.Height)),
        new(C3DHeightImagePalette.Grayscale, GetPaletteDisplayName(C3DHeightImagePalette.Grayscale)),
        new(C3DHeightImagePalette.Thermal, GetPaletteDisplayName(C3DHeightImagePalette.Thermal)),
        new(C3DHeightImagePalette.ColorVisionAssist, GetPaletteDisplayName(C3DHeightImagePalette.ColorVisionAssist)),
        new(C3DHeightImagePalette.Monochrome, GetPaletteDisplayName(C3DHeightImagePalette.Monochrome))
    ];

    private string GetPaletteDisplayName(C3DHeightImagePalette palette) => palette switch
    {
        C3DHeightImagePalette.Height => localization.HeightImagePaletteHeight,
        C3DHeightImagePalette.Grayscale => localization.HeightImagePaletteGrayscale,
        C3DHeightImagePalette.Thermal => localization.HeightImagePaletteThermal,
        C3DHeightImagePalette.ColorVisionAssist => localization.Resolve("ThreeD.Viewer.ColorVisionAssist", "색각 보조", "Color vision assist"),
        C3DHeightImagePalette.Monochrome => localization.HeightImagePaletteMonochrome,
        _ => throw new ArgumentOutOfRangeException(nameof(palette), palette, null)
    };

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        OnPropertyChanged(nameof(PixelsPerCellSummary));
        OnPropertyChanged(nameof(Localization));
        foreach (var choice in PaletteChoices)
        {
            choice.UpdateDisplayName(GetPaletteDisplayName(choice.Value));
        }
        OnPropertyChanged(nameof(DisplayRangeMode));
        OnPropertyChanged(nameof(DisplayRangeSummary));
        OnPropertyChanged(nameof(InvalidOverlaySummary));
        OnPropertyChanged(nameof(HeightLegendInformation));
        RefreshSharedCursorPresentation();
        if (HasRangeError)
        {
            RangeError = localization.HeightImageRangeInvalid;
        }
    }

    private void OnSharedCursorChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SharedHeightCursorSession.Cursor)
            or nameof(SharedHeightCursorSession.HasCursor)
            or nameof(SharedHeightCursorSession.Revision))
        {
            RefreshSharedCursorPresentation();
        }
    }

    private void OnRoiWorkspaceChanged(object? sender, PropertyChangedEventArgs args)
    {
        fitRoiCommand.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(HasHeightDisplayRegion));
        if (heightDisplayOptions.RangeMode == "roi") ApplyAutomaticDisplayRange();
    }

    private void RefreshSharedCursorPresentation()
    {
        var cursor = GetMatchingCursor();
        OnPropertyChanged(nameof(HasLinkedCursor));
        OnPropertyChanged(nameof(LinkedCursorOrigin));
        OnPropertyChanged(nameof(LinkedCursorRow));
        OnPropertyChanged(nameof(LinkedCursorColumn));
        OnPropertyChanged(nameof(LinkedCursorIsValid));
        OnPropertyChanged(nameof(CursorCoordinateSummary));
        OnPropertyChanged(nameof(CursorCoordinateValueSummary));
        HoverSummary = cursor is not { } value
            ? localization.HeightImageCoordinateHint
            : FormatSharedCursor(value);
    }

    private SharedHeightCursorSnapshot? GetMatchingCursor()
    {
        var cursor = sharedCursor.Cursor;
        return Frame is { } currentFrame
               && cursor is { } value
               && string.Equals(
                   value.SourceContentSha256,
                   currentFrame.SourceContentSha256,
                   StringComparison.OrdinalIgnoreCase)
               && value.Row >= 0
               && value.Row < currentFrame.Height
               && value.Column >= 0
               && value.Column < currentFrame.Width
            ? value
            : null;
    }

    private double GetMissingPercentage() =>
        Frame is { } currentFrame && currentFrame.Width > 0 && currentFrame.Height > 0
            ? currentFrame.MissingCount * 100.0 / checked(currentFrame.Width * currentFrame.Height)
            : 0.0;

    private string FormatCursorCoordinates(bool includeUnit)
    {
        if (GetMatchingCursor() is not { } cursor) return "X — · Y — · Z —";
        var height = cursor.IsValid ? cursor.RawHeight.ToString("0.##", CultureInfo.InvariantCulture) : "—";
        var unit = includeUnit ? $" {Frame?.Unit}" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"X {cursor.Column:N0} · Y {height}{unit} · Z {cursor.Row:N0}");
    }

    private string FormatSharedCursor(SharedHeightCursorSnapshot cursor)
    {
        var origin = cursor.Origin == SharedHeightCursorOrigin.ThreeDViewer
            ? localization.HeightImageCursorFromThreeD
            : localization.HeightImageCursorFromHeightImage;
        return cursor.IsValid
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{origin} | column {cursor.Column:N0} | row {cursor.Row:N0} | H {cursor.RawHeight:0.##} {Frame?.Unit}")
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{origin} | column {cursor.Column:N0} | row {cursor.Row:N0} | {localization.HeightImageMissingValue}");
    }

    private string GetPaletteColor(double normalized)
    {
        var color = SelectedPalette switch
        {
            C3DHeightImagePalette.Grayscale => C3DPointMapPalette.GrayscaleBytes(normalized),
            C3DHeightImagePalette.Thermal => C3DPointMapPalette.ThermalBytes(normalized),
            C3DHeightImagePalette.ColorVisionAssist => C3DPointMapPalette.ColorVisionAssistBytes(normalized),
            C3DHeightImagePalette.Monochrome => (R: (byte)115, G: (byte)155, B: (byte)194),
            _ => C3DPointMapPalette.HeightBytes(normalized)
        };
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private static bool TryParseRangeValue(string? value, out double result)
    {
        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        return (double.TryParse(value, styles, CultureInfo.CurrentCulture, out result)
                || double.TryParse(value, styles, CultureInfo.InvariantCulture, out result))
               && double.IsFinite(result);
    }

    private static string FormatRangeValue(double value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

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

public enum HeightImageDisplayRequest
{
    Fit,
    ActualPixels,
    ZoomIn,
    ZoomOut,
    FitRoi
}

public sealed class HeightImagePaletteChoice : INotifyPropertyChanged
{
    private string displayName;

    public HeightImagePaletteChoice(C3DHeightImagePalette value, string displayName)
    {
        Value = value;
        this.displayName = displayName;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public C3DHeightImagePalette Value { get; }

    public string DisplayName => displayName;

    internal void UpdateDisplayName(string value)
    {
        if (string.Equals(displayName, value, StringComparison.Ordinal))
        {
            return;
        }

        displayName = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
    }
}

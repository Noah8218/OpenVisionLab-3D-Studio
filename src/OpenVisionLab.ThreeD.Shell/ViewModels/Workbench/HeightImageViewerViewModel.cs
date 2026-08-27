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
/// Owns Height Image loading, coordinate inspection, display-only controls,
/// and the composed ROI interaction surface. Recipe mutation remains outside
/// this type and inspection is never invoked by its presentation commands.
/// </summary>
public sealed class HeightImageViewerViewModel : INotifyPropertyChanged
{
    private readonly ThreeDLocalization localization;
    private readonly SharedHeightCursorSession sharedCursor;
    private readonly RelayCommand fitCommand;
    private readonly RelayCommand actualPixelsCommand;
    private readonly RelayCommand zoomInCommand;
    private readonly RelayCommand zoomOutCommand;
    private readonly RelayCommand autoRangeCommand;
    private readonly RelayCommand applyManualRangeCommand;
    private CancellationTokenSource? loadCancellation;
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
    private C3DConnectedRegionOutput? connectedRegionOutput;
    private string? selectedConnectedRegionId;
    private int loadGeneration;
    private int displayRangeRevision;

    public HeightImageViewerViewModel(
        ThreeDLocalization localization,
        SharedHeightCursorSession sharedCursor)
    {
        this.localization = localization ?? throw new ArgumentNullException(nameof(localization));
        this.sharedCursor = sharedCursor ?? throw new ArgumentNullException(nameof(sharedCursor));
        RoiWorkspace = new HeightImageRoiWorkspaceViewModel(localization);
        fitCommand = new RelayCommand(_ => DisplayRequest?.Invoke(this, HeightImageDisplayRequest.Fit), _ => HasImage);
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
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler<HeightImageDisplayRequest>? DisplayRequest;

    public C3DHeightImageFrame? Frame
    {
        get => frame;
        private set
        {
            if (ReferenceEquals(frame, value))
            {
                return;
            }

            sharedCursor.Clear();
            frame = value;
            ResetDisplayRangeForFrame();
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasImage));
            OnPropertyChanged(nameof(ImageSummary));
            RefreshSharedCursorPresentation();
            RaiseCommandState();
        }
    }

    public bool HasImage => Frame is not null;
    public ThreeDLocalization Localization => localization;
    public HeightImageRoiWorkspaceViewModel RoiWorkspace { get; }
    public IReadOnlyList<C3DCompletenessCellOverlay> CompletenessCellOverlays =>
        completenessCellOverlays;
    public string? SelectedCompletenessCellId => selectedCompletenessCellId;
    public C3DConnectedRegionOutput? ConnectedRegionOutput => connectedRegionOutput;
    public string? SelectedConnectedRegionId => selectedConnectedRegionId;
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

    public void SetConnectedRegionOverlay(
        C3DConnectedRegionOutput? output,
        string? selectedRegionId)
    {
        var normalizedSelectedRegionId = output?.Regions.FirstOrDefault(region =>
            string.Equals(region.RegionId, selectedRegionId, StringComparison.OrdinalIgnoreCase))?.RegionId;
        if (ReferenceEquals(connectedRegionOutput, output)
            && string.Equals(
                selectedConnectedRegionId,
                normalizedSelectedRegionId,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        connectedRegionOutput = output;
        selectedConnectedRegionId = normalizedSelectedRegionId;
        OnPropertyChanged(nameof(ConnectedRegionOutput));
        OnPropertyChanged(nameof(SelectedConnectedRegionId));
    }

    public string DisplayPixelSha256 => DisplayFrame?.PixelSha256 ?? string.Empty;
    public int InvalidOverlayPixelCount => DisplayFrame?.InvalidOverlayPixelCount ?? 0;
    public string InvalidOverlaySummary => Frame is null
        ? localization.HeightImageInvalidOverlayUnavailable
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{(ShowInvalidCells ? localization.HeightImageInvalidOverlayLegend : localization.HeightImageInvalidOverlayHidden)} {Frame.MissingCount:N0} ({GetMissingPercentage():0.0}%) · {localization.HeightImageViewOnlyShort}");

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
    public int LinkedCursorRow => GetMatchingCursor()?.Row ?? -1;
    public int LinkedCursorColumn => GetMatchingCursor()?.Column ?? -1;
    public bool LinkedCursorIsValid => GetMatchingCursor()?.IsValid == true;

    public double ZoomPercent
    {
        get => zoomPercent;
        private set => SetField(ref zoomPercent, value);
    }

    public string ZoomSummary => string.Create(CultureInfo.InvariantCulture, $"{ZoomPercent:0.#}%");

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

    public C3DHeightImagePalette SelectedPalette
    {
        get => selectedPalette;
        set
        {
            if (!Enum.IsDefined(value) || !SetField(ref selectedPalette, value))
            {
                return;
            }

            OnPropertyChanged(nameof(LegendColor0));
            OnPropertyChanged(nameof(LegendColor1));
            OnPropertyChanged(nameof(LegendColor2));
            OnPropertyChanged(nameof(LegendColor3));
            OnPropertyChanged(nameof(DisplayRangeSummary));
            RenderActiveDisplay();
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
    public string DisplayRangeMode => IsAutoRange
        ? localization.HeightImageAutoRange
        : localization.HeightImageManualRange;
    public string DisplayRangeSummary => Frame is null
        ? localization.HeightImageUnavailable
        : string.Create(
            CultureInfo.InvariantCulture,
            $"{DisplayRangeMode} | {activeRangeMinimum:0.###} \u2026 {activeRangeMaximum:0.###} {Frame.Unit} | {SelectedPalette}");
    public string LegendMaximum => Frame is null
        ? "\u2014"
        : activeRangeMaximum.ToString("0.###", CultureInfo.InvariantCulture);
    public string LegendMinimum => Frame is null
        ? "\u2014"
        : activeRangeMinimum.ToString("0.###", CultureInfo.InvariantCulture);
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

    internal async Task EnsureSourceAsync(
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
            return;
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
        if (string.Equals(loadedSourceKey, sourceKey, StringComparison.OrdinalIgnoreCase) && Frame is not null)
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
        Status = localization.HeightImageLoading;
        HoverSummary = localization.HeightImageCoordinateHint;

        try
        {
            var source = await loadSourceAsync(cancellationToken);
            var nextFrame = await Task.Run(
                () => C3DHeightImageFrame.Create(source, cancellationToken),
                cancellationToken);

            if (generation != loadGeneration || cancellationToken.IsCancellationRequested)
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
            if (generation != loadGeneration)
            {
                return;
            }

            loadedSourceKey = string.Empty;
            Frame = null;
            Error = exception.Message;
            Status = localization.HeightImageUnavailable;
        }
        finally
        {
            if (generation == loadGeneration)
            {
                IsLoading = false;
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

    internal void ClearSource() => Clear(localization.HeightImageUnavailable);

    public void SetZoom(double percent)
    {
        var normalized = Math.Clamp(percent, 1.0, 3200.0);
        if (Math.Abs(ZoomPercent - normalized) < 0.01)
        {
            return;
        }

        ZoomPercent = normalized;
        OnPropertyChanged(nameof(ZoomSummary));
    }

    public bool TryApplyManualRange(double minimum, double maximum)
    {
        RangeMinimumText = FormatRangeValue(minimum);
        RangeMaximumText = FormatRangeValue(maximum);
        if (!TryReadManualRange(out _, out _))
        {
            return false;
        }

        ApplyManualRange();
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
        if (Frame is null)
        {
            return;
        }

        IsAutoRange = true;
        activeRangeMinimum = Frame.Minimum;
        activeRangeMaximum = Frame.Maximum;
        RangeMinimumText = FormatRangeValue(activeRangeMinimum);
        RangeMaximumText = FormatRangeValue(activeRangeMaximum);
        RangeError = string.Empty;
        NotifyRangeState();
        RenderActiveDisplay();
        DisplayRangeRevision = unchecked(DisplayRangeRevision + 1);
    }

    private void Clear(string nextStatus)
    {
        loadCancellation?.Cancel();
        loadCancellation?.Dispose();
        loadCancellation = null;
        loadGeneration++;
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
        actualPixelsCommand.RaiseCanExecuteChanged();
        zoomInCommand.RaiseCanExecuteChanged();
        zoomOutCommand.RaiseCanExecuteChanged();
        autoRangeCommand.RaiseCanExecuteChanged();
        applyManualRangeCommand.RaiseCanExecuteChanged();
    }

    private void ResetDisplayRangeForFrame()
    {
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

        isAutoRange = true;
        activeRangeMinimum = Frame.Minimum;
        activeRangeMaximum = Frame.Maximum;
        rangeMinimumText = FormatRangeValue(activeRangeMinimum);
        rangeMaximumText = FormatRangeValue(activeRangeMaximum);
        RangeError = string.Empty;
        NotifyRangeState();
        RenderActiveDisplay();
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
                       && activeRangeMinimum == Frame.Minimum
                       && activeRangeMaximum == Frame.Maximum
                       && !ShowInvalidCells
            ? Frame.DefaultDisplayFrame
            : Frame.CreateDisplayFrame(
                SelectedPalette,
                activeRangeMinimum,
                activeRangeMaximum,
                ShowInvalidCells
                    ? C3DHeightImageInvalidOverlayMode.Visible
                    : C3DHeightImageInvalidOverlayMode.Hidden);
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
        var minimumValid = TryParseRangeValue(RangeMinimumText, out minimum);
        var maximumValid = TryParseRangeValue(RangeMaximumText, out maximum);
        return minimumValid && maximumValid && minimum < maximum;
    }

    private bool CanApplyManualRange() =>
        HasImage && TryReadManualRange(out _, out _);

    private void NotifyRangeState()
    {
        OnPropertyChanged(nameof(IsAutoRange));
        OnPropertyChanged(nameof(DisplayRangeMode));
        OnPropertyChanged(nameof(DisplayRangeSummary));
        OnPropertyChanged(nameof(RangeMinimumText));
        OnPropertyChanged(nameof(RangeMaximumText));
        OnPropertyChanged(nameof(LegendMinimum));
        OnPropertyChanged(nameof(LegendMaximum));
        applyManualRangeCommand.RaiseCanExecuteChanged();
    }

    private IReadOnlyList<HeightImagePaletteChoice> CreatePaletteChoices() =>
    [
        new(C3DHeightImagePalette.Height, localization.HeightImagePaletteHeight),
        new(C3DHeightImagePalette.Grayscale, localization.HeightImagePaletteGrayscale),
        new(C3DHeightImagePalette.Thermal, localization.HeightImagePaletteThermal)
    ];

    private void OnLocalizationChanged(object? sender, PropertyChangedEventArgs args)
    {
        PaletteChoices = CreatePaletteChoices();
        OnPropertyChanged(nameof(DisplayRangeMode));
        OnPropertyChanged(nameof(DisplayRangeSummary));
        OnPropertyChanged(nameof(InvalidOverlaySummary));
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

    private void RefreshSharedCursorPresentation()
    {
        var cursor = GetMatchingCursor();
        OnPropertyChanged(nameof(HasLinkedCursor));
        OnPropertyChanged(nameof(LinkedCursorRow));
        OnPropertyChanged(nameof(LinkedCursorColumn));
        OnPropertyChanged(nameof(LinkedCursorIsValid));
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

    private string FormatSharedCursor(SharedHeightCursorSnapshot cursor)
    {
        var origin = cursor.Origin == SharedHeightCursorOrigin.ThreeDViewer
            ? localization.HeightImageCursorFromThreeD
            : localization.HeightImageCursorFromHeightImage;
        return cursor.IsValid
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"{origin} | column {cursor.Column:N0} | row {cursor.Row:N0} | H {cursor.RawHeight:0.###} {Frame?.Unit}")
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
        value.ToString("0.###", CultureInfo.InvariantCulture);

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
    ZoomOut
}

public sealed record HeightImagePaletteChoice(
    C3DHeightImagePalette Value,
    string DisplayName);

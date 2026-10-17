using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using OpenVisionLab.ThreeD.Data;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// WPF binding adapter for the Viewer presentation bar. It owns only binding
/// notification and forwards display mutations to the existing Viewer Host
/// and pane Height Image owners.
/// </summary>
public sealed class ViewerPresentationBarViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly string[] BindingPropertyNames =
    [
        nameof(CanLoadTexture), nameof(HasTexture), nameof(TextureVisible), nameof(TextureName), nameof(TextureError),
        nameof(ShowTextureOptions),
        nameof(ProfileState),
        nameof(HasProfileEndpoints),
        nameof(IsRotateMode),
        nameof(IsMoveMode),
        nameof(IsProfileMode),
        nameof(IsRoiMode),
        nameof(IsSurfaceStyle),
        nameof(IsPointsStyle),
        nameof(CanSelectSurfaceStyle),
        nameof(CanSelectPointsStyle),
        nameof(FloorGridVisible),
        nameof(AvailableColorMaps),
        nameof(CanSelectHeightColorMap),
        nameof(CanSelectGrayscaleColorMap),
        nameof(CanSelectSolidColorMap),
        nameof(CanSelectDeviationColorMap),
        nameof(CanSelectThermalColorMap),
        nameof(CanSelectColorVisionAssistColorMap),
        nameof(SelectedColorMap),
        nameof(DefaultHeightRangeSummary),
        nameof(HeightRangeSummaryCaption),
        nameof(CanSelectColorMap),
        nameof(DiagnosticChannelOptions),
        nameof(SelectedDiagnosticChannel),
        nameof(CanSelectDiagnosticChannel),
        nameof(DiagnosticChannelSummary),
        nameof(SelectionOverlayVisible),
        nameof(ResultOverlayVisible),
        nameof(MeasurementVisible),
        nameof(C3DHeightDistributionVisible),
        nameof(C3DHeightColorRangeSummary),
        nameof(C3DHeightEmphasis),
        nameof(HasC3DHeightColorRange),
        nameof(IsDistributionRange), nameof(IsRegionRange), nameof(CanUseRegionRange), nameof(RegionRangeUnavailableReason),
        nameof(IsHeightRangeLinked), nameof(KeepHeightRange), nameof(HighlightOutsideRange), nameof(IsContinuousHeightColor),
        nameof(IsBandedHeightColor), nameof(IsToleranceHeightColor), nameof(CanUseMeasurementColor), nameof(MeasurementColorGuidance), nameof(HeightBandCount), nameof(HeightBandInterval)
    ];

    private IOpenVisionThreeDViewerHost? viewerHost;
    private readonly HeightImageViewerViewModel? heightImage;
    private bool isHeightDisplaySettings;
    private bool isDisposed;
    private bool isDisplaySettingsOpen;
    private bool isUpdatingDisplaySettingsDraft;
    private bool hasDisplaySettingsRangeError;
    private bool hasDisplaySettingsEmphasisError;
    private bool hasDisplaySettingsStaleError;
    private string textureSelectionError = string.Empty;
    private string draftHeightRangeMode = "auto";
    private string draftHeightRangeMinimum = string.Empty;
    private string draftHeightRangeMaximum = string.Empty;
    private string draftHeightEmphasis = "1";
    private string displaySettingsSourceSha256 = string.Empty;
    private int displaySettingsRangeRevision;
    private readonly RelayCommand loadTextureCommand;
    private readonly RelayCommand clearTextureCommand;
    private readonly RelayCommand profileActionCommand;
    private readonly RelayCommand profilePickCommand;
    private readonly RelayCommand rotateCommand;
    private readonly RelayCommand moveCommand;
    private readonly RelayCommand measureCommand;
    private readonly RelayCommand fitAllCommand;
    private readonly RelayCommand openDisplaySettingsCommand;
    private readonly RelayCommand openHeightDisplaySettingsCommand;
    private readonly RelayCommand applyDisplaySettingsCommand;
    private readonly RelayCommand cancelDisplaySettingsCommand;
    private readonly RelayCommand closeDisplaySettingsCommand;
    private readonly RelayCommand decreaseHeightMinimumCommand;
    private readonly RelayCommand increaseHeightMinimumCommand;
    private readonly RelayCommand decreaseHeightMaximumCommand;
    private readonly RelayCommand increaseHeightMaximumCommand;
    private readonly RelayCommand resetHeightColorRangeCommand;

    public ViewerPresentationBarViewModel(IOpenVisionThreeDViewerHost viewerHost, HeightImageViewerViewModel? heightImage = null, Func<string?>? selectTextureImage = null)
    {
        this.viewerHost = viewerHost ?? throw new ArgumentNullException(nameof(viewerHost));
        this.heightImage = heightImage;
        loadTextureCommand = new RelayCommand(_ =>
        {
            var sourceSha256 = Presentation.C3DHeightDistributionSourceSha256;
            textureSelectionError = string.Empty;
            // A native file dialog closes this popup; reopen it with the outcome visible.
            IsDisplaySettingsOpen = false;
            var path = selectTextureImage?.Invoke();
            if (isDisposed) return;
            if (path is not null)
            {
                if (sourceSha256 == Presentation.C3DHeightDistributionSourceSha256 && CanLoadTexture)
                    Execute(host => host.TryLoadC3DTexture(path));
                else textureSelectionError = ThreeDLocalization.Shared.Resolve("ThreeD.Texture.SourceChanged",
                    "영상을 선택하는 동안 입력이 바뀌었습니다. 현재 높이맵에서 다시 선택하세요.",
                    "The input changed while choosing an image. Choose again for the current height map.");
            }
            IsDisplaySettingsOpen = true;
            RaiseBindingProperties();
        }, _ => CanLoadTexture && selectTextureImage is not null);
        clearTextureCommand = new RelayCommand(_ => Execute(host => host.TryClearC3DTexture()), _ => HasTexture);
        profileActionCommand = new RelayCommand(parameter => Execute(host => host.TryRequestHeightProfile((ViewerHeightProfileAction)parameter!)),
            parameter => parameter is ViewerHeightProfileAction action && CanRequestProfile(action));
        profilePickCommand = new RelayCommand(parameter => Execute(host => host.TryPickHeightProfileCell((ViewerHeightProfileCell)parameter!)),
            parameter => CanExecuteHostCommand() && parameter is ViewerHeightProfileCell && ProfileState.IsActive);
        rotateCommand = new RelayCommand(_ => Execute(host => host.TrySetSelectionMode("Point")), _ => CanNavigate());
        moveCommand = new RelayCommand(_ => Execute(host => host.TrySetSelectionMode("Move")), _ => CanNavigate());
        measureCommand = new RelayCommand(_ => Execute(host => host.TryRequestHeightProfile(ViewerHeightProfileAction.Begin)), _ => CanExecuteHostCommand() && this.viewerHost?.HostState is { C3DSampleVisible: true, IsTeachingCaptureActive: false });
        fitAllCommand = new RelayCommand(_ => Execute(host => { host.FitAll(); return true; }), _ => CanNavigate());
        openDisplaySettingsCommand = new RelayCommand(_ => OpenDisplaySettings(false), _ => CanExecuteHostCommand());
        openHeightDisplaySettingsCommand = new RelayCommand(_ => OpenDisplaySettings(true), _ => CanExecuteHostCommand() && this.heightImage?.HasImage == true);
        applyDisplaySettingsCommand = new RelayCommand(_ => ApplyDisplaySettings(), _ => CanExecuteHostCommand());
        cancelDisplaySettingsCommand = new RelayCommand(_ => BeginDisplaySettingsEdit(), _ => CanExecuteHostCommand());
        closeDisplaySettingsCommand = new RelayCommand(_ => IsDisplaySettingsOpen = false, _ => CanExecuteHostCommand());
        decreaseHeightMinimumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMinimum(-1),
            _ => CanExecuteHostCommand());
        increaseHeightMinimumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMinimum(1),
            _ => CanExecuteHostCommand());
        decreaseHeightMaximumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMaximum(-1),
            _ => CanExecuteHostCommand());
        increaseHeightMaximumCommand = new RelayCommand(
            _ => ShiftC3DHeightColorMaximum(1),
            _ => CanExecuteHostCommand());
        resetHeightColorRangeCommand = new RelayCommand(
            _ => ResetC3DHeightColorRange(),
            _ => CanExecuteHostCommand());
        viewerHost.HostStateChanged += OnHostStateChanged;
        if (heightImage is not null) heightImage.PropertyChanged += OnHeightImageChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ViewerHostProfileState ProfileState => viewerHost?.HostState.Profile ?? ViewerHostProfileState.Empty;
    public bool HasProfileEndpoints => ProfileState.HasEndpoints && !ProfileState.IsPicking;
    public ICommand ProfileActionCommand => profileActionCommand;
    public ICommand ProfilePickCommand => profilePickCommand;
    public ICommand RotateCommand => rotateCommand;
    public ICommand MoveCommand => moveCommand;
    public ICommand MeasureCommand => measureCommand;
    public ICommand FitAllCommand => fitAllCommand;
    public bool IsRotateMode => viewerHost?.HostState.SelectionMode == "Point";
    public bool IsMoveMode => viewerHost?.HostState.SelectionMode == "Move";
    public bool IsProfileMode => viewerHost?.HostState.SelectionMode == "Profile";
    public bool IsRoiMode => viewerHost?.HostState.IsTeachingCaptureActive == true;
    private bool CanNavigate() => CanExecuteHostCommand() && viewerHost?.HostState is { } state && !state.IsTeachingCaptureActive && (state.C3DSampleVisible || state.GlbSampleVisible || state.LazSampleVisible);

    private bool CanRequestProfile(ViewerHeightProfileAction action)
    {
        if (!CanExecuteHostCommand() || viewerHost is not { } host) return false;
        var profile = host.HostState.Profile;
        return action switch
        {
            ViewerHeightProfileAction.Begin or ViewerHeightProfileAction.Repick => host.HostState is { C3DSampleVisible: true, IsTeachingCaptureActive: false },
            ViewerHeightProfileAction.Cancel => profile.IsPicking,
            ViewerHeightProfileAction.Clear or ViewerHeightProfileAction.ToggleCollapsed => profile.Visible,
            ViewerHeightProfileAction.Left or ViewerHeightProfileAction.Right or ViewerHeightProfileAction.Up or ViewerHeightProfileAction.Down => profile.IsActive && HasProfileEndpoints,
            _ => Enum.IsDefined(action) && HasProfileEndpoints
        };
    }

    public IReadOnlyList<string> AvailableColorMaps => Presentation.AvailableColorMaps
        .OrderBy(value => value switch { "Height" => 0, "Grayscale" => 1, "Solid" => 2, _ => 3 }).ToArray();

    public IReadOnlyList<string> DisplayColorMaps { get; } = ["Height", "Grayscale", "Solid", "Thermal", "ColorVisionAssist", "Deviation"];
    public bool CanSelectHeightColorMap => isHeightDisplaySettings ? heightImage?.HasImage == true : AvailableColorMaps.Contains("Height");
    public bool CanSelectGrayscaleColorMap => isHeightDisplaySettings ? heightImage?.HasImage == true : AvailableColorMaps.Contains("Grayscale");
    public bool CanSelectSolidColorMap => isHeightDisplaySettings ? heightImage?.HasImage == true : AvailableColorMaps.Contains("Solid");
    public bool CanSelectThermalColorMap => isHeightDisplaySettings ? heightImage?.HasImage == true : AvailableColorMaps.Contains("Thermal");
    public bool CanSelectColorVisionAssistColorMap => isHeightDisplaySettings ? heightImage?.HasImage == true : AvailableColorMaps.Contains("ColorVisionAssist");
    public bool CanSelectDeviationColorMap => !isHeightDisplaySettings && AvailableColorMaps.Contains("Deviation");

    public string DefaultHeightRangeSummary
    {
        get
        {
            var presentation = Presentation;
            if (heightImage?.Frame is { } frame && (isHeightDisplaySettings || frame.SourceContentSha256 == presentation.C3DHeightDistributionSourceSha256))
                return $"{frame.DefaultDisplayMinimum.ToString("F2", CultureInfo.InvariantCulture)} - {frame.DefaultDisplayMaximum.ToString("F2", CultureInfo.InvariantCulture)} {frame.Unit}";
            return presentation.C3DHeightColorRangeAuto && double.IsFinite(presentation.C3DHeightColorMinimumRaw) && double.IsFinite(presentation.C3DHeightColorMaximumRaw)
                ? $"{presentation.C3DHeightColorMinimumRaw.ToString("F2", CultureInfo.InvariantCulture)} - {presentation.C3DHeightColorMaximumRaw.ToString("F2", CultureInfo.InvariantCulture)} raw-height"
                : string.Empty;
        }
    }

    public string SelectedColorMap
    {
        get => isHeightDisplaySettings && heightImage is not null ? heightImage.SelectedPalette switch
        {
            C3DHeightImagePalette.Grayscale => "Grayscale",
            C3DHeightImagePalette.Thermal => "Thermal",
            C3DHeightImagePalette.ColorVisionAssist => "ColorVisionAssist",
            C3DHeightImagePalette.Monochrome => "Solid",
            _ => "Height"
        } : Presentation.SelectedColorMap;
        set
        {
            if (!isHeightDisplaySettings) Execute(host => host.TrySetSelectedColorMap(value));
            else if (!isDisposed && heightImage?.HasImage == true && value is "Height" or "Grayscale" or "Thermal" or "ColorVisionAssist" or "Solid")
            {
                heightImage.SelectedPalette = value switch
                {
                    "Grayscale" => C3DHeightImagePalette.Grayscale,
                    "Thermal" => C3DHeightImagePalette.Thermal,
                    "ColorVisionAssist" => C3DHeightImagePalette.ColorVisionAssist,
                    "Solid" => C3DHeightImagePalette.Monochrome,
                    _ => C3DHeightImagePalette.Height
                };
                RaiseBindingProperties();
            }
        }
    }

    public bool CanSelectColorMap => Presentation.CanSelectColorMap && (isHeightDisplaySettings || !TextureVisible);
    public bool ShowTextureOptions => !isHeightDisplaySettings;
    public bool CanLoadTexture => CanExecuteHostCommand() && Presentation.CanLoadC3DTexture && !isHeightDisplaySettings;
    public bool HasTexture => CanExecuteHostCommand() && Presentation.HasC3DTexture;
    public bool TextureVisible
    {
        get => Presentation.C3DTextureVisible;
        set => Execute(host => host.TrySetC3DTextureVisible(value));
    }
    public string TextureName => Presentation.C3DTextureName;
    public string TextureError => textureSelectionError.Length > 0 ? textureSelectionError : Presentation.C3DTextureError;
    public ICommand LoadTextureCommand => loadTextureCommand;
    public ICommand ClearTextureCommand => clearTextureCommand;

    public bool IsSurfaceStyle
    {
        get => Presentation.SelectedGeometryStyle == "Surface";
        set { if (value) Execute(host => host.TrySetSelectedGeometryStyle("Surface")); }
    }

    public bool IsPointsStyle
    {
        get => Presentation.SelectedGeometryStyle == "Points";
        set { if (value) Execute(host => host.TrySetSelectedGeometryStyle("Points")); }
    }

    public bool CanSelectSurfaceStyle => Presentation.CanSelectGeometryStyle && Presentation.AvailableGeometryStyles.Contains("Surface");
    public bool CanSelectPointsStyle => Presentation.CanSelectGeometryStyle && Presentation.AvailableGeometryStyles.Contains("Points");

    public bool FloorGridVisible
    {
        get => Presentation.FloorGridVisible;
        set => Execute(host => host.TrySetFloorGridVisible(value));
    }

    public IReadOnlyList<ViewerDiagnosticChannelOption> DiagnosticChannelOptions =>
        Presentation.DiagnosticChannelOptions;

    public ViewerDiagnosticChannelOption? SelectedDiagnosticChannel
    {
        get => Presentation.SelectedDiagnosticChannel;
        set => Execute(host => host.TrySetSelectedDiagnosticChannel(value));
    }

    public bool CanSelectDiagnosticChannel => Presentation.CanSelectDiagnosticChannel;

    public string DiagnosticChannelSummary => Presentation.DiagnosticChannelSummary;

    public bool SelectionOverlayVisible
    {
        get => viewerHost?.HostState.SelectionOverlayVisible ?? false;
        set => Execute(host => host.TrySetSelectionOverlayVisible(value));
    }

    public bool ResultOverlayVisible
    {
        get => Presentation.ResultOverlayVisible;
        set => Execute(host => host.TrySetResultOverlayVisible(value));
    }

    public bool MeasurementVisible
    {
        get => Presentation.MeasurementVisible;
        set => Execute(host => host.TrySetMeasurementVisible(value));
    }

    public bool C3DHeightDistributionVisible => Presentation.C3DHeightDistributionVisible;

    public string C3DHeightColorRangeSummary => Presentation.C3DHeightColorRangeSummary;

    public int C3DHeightEmphasis => Presentation.C3DHeightEmphasis;

    public bool HasC3DHeightColorRange =>
        viewerHost?.HostState.Presentation.C3DHeightDistributionVisible == true;

    public bool IsDisplaySettingsOpen
    {
        get => isDisplaySettingsOpen;
        set
        {
            if (isDisplaySettingsOpen == value)
            {
                return;
            }

            if (value)
            {
                BeginDisplaySettingsEdit();
            }
            else
            {
                ClearDisplaySettingsDraft();
            }

            isDisplaySettingsOpen = value;
            OnPropertyChanged(nameof(IsDisplaySettingsOpen));
        }
    }

    public string DraftHeightRangeMode
    {
        get => draftHeightRangeMode;
        set
        {
            if (draftHeightRangeMode == value)
            {
                return;
            }

            draftHeightRangeMode = value;
            hasDisplaySettingsRangeError = false;
            OnPropertyChanged(nameof(DraftHeightRangeMode));
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
            OnPropertyChanged(nameof(CanEditManualHeightRange));
            OnPropertyChanged(nameof(IsAutomaticRange));
            OnPropertyChanged(nameof(IsManualRange));
            OnPropertyChanged(nameof(IsDistributionRange));
            OnPropertyChanged(nameof(IsRegionRange));
            if (value is "auto" or "robust" or "roi" && isDisplaySettingsOpen && !isUpdatingDisplaySettingsDraft && viewerHost is { } host && !isDisposed)
            {
                var presentation = host.HostState.Presentation;
                if (!HasMatchingDisplaySettingsSource())
                {
                    HasDisplaySettingsStaleError = true;
                    return;
                }

                ApplyHeightOptions(CurrentHeightOptions with { RangeMode = value });
            }
        }
    }

    public bool CanEditManualHeightRange => DraftHeightRangeMode == "manual";
    public string HeightRangeSummaryCaption => isHeightDisplaySettings
        ? ThreeDLocalization.Shared.Resolve("ThreeD.Legend.FullDataRange", "전체 데이터 범위 ", "Full data range ")
        : ThreeDLocalization.Shared.Resolve("ThreeD.Legend.ColorRange", "색 범위 ", "Colour range ");

    public bool IsAutomaticRange { get => DraftHeightRangeMode == "auto"; set { if (value) DraftHeightRangeMode = "auto"; } }
    public bool IsManualRange { get => DraftHeightRangeMode == "manual"; set { if (value) DraftHeightRangeMode = "manual"; } }
    public bool IsDistributionRange { get => DraftHeightRangeMode == "robust"; set { if (value) DraftHeightRangeMode = "robust"; } }
    public bool IsRegionRange { get => DraftHeightRangeMode == "roi"; set { if (value) DraftHeightRangeMode = "roi"; } }
    public bool CanUseRegionRange => isHeightDisplaySettings ? heightImage?.HasHeightDisplayRegion == true : viewerHost?.HostState.Presentation.HasHeightDisplayRegion == true;
    public string RegionRangeUnavailableReason => CanUseRegionRange ? string.Empty : ThreeDLocalization.Shared.Resolve("ThreeD.Legend.NoRegion", "유효한 선택 영역이 없습니다.", "No valid selected region.");
    private HeightDisplayOptions CurrentHeightOptions => isHeightDisplaySettings ? heightImage?.HeightDisplayOptions ?? new() : viewerHost?.HostState.Presentation.HeightDisplayOptions ?? new();
    private string HeightWorkspace => isHeightDisplaySettings ? heightImage?.HeightDisplayWorkspace ?? "teach" : viewerHost?.HostState.Presentation.HeightDisplayWorkspace ?? "teach";
    public bool IsHeightRangeLinked { get => CurrentHeightOptions.IsLinked; set => ApplyHeightOptions(CurrentHeightOptions with { IsLinked = value }); }
    public bool HighlightOutsideRange { get => CurrentHeightOptions.HighlightOutside; set => ApplyHeightOptions(CurrentHeightOptions with { HighlightOutside = value }); }
    public bool KeepHeightRange
    {
        get => CurrentHeightOptions.KeepsRange(HeightWorkspace);
        set => ApplyHeightOptions(HeightWorkspace switch
        {
            "validate" => CurrentHeightOptions with { KeepValidationRange = value },
            "inspect" => CurrentHeightOptions with { KeepInspectionRange = value },
            _ => CurrentHeightOptions with { KeepTeachingRange = value }
        });
    }
    public bool IsContinuousHeightColor { get => CurrentHeightOptions.ColorStyle == "continuous"; set { if (value) ApplyHeightOptions(CurrentHeightOptions with { ColorStyle = "continuous" }); } }
    public bool IsBandedHeightColor { get => CurrentHeightOptions.ColorStyle == "bands"; set { if (value) ApplyHeightOptions(CurrentHeightOptions with { ColorStyle = "bands" }); } }
    public bool IsToleranceHeightColor { get => CurrentHeightOptions.ColorStyle == "tolerance"; set { if (value) ApplyHeightOptions(CurrentHeightOptions with { ColorStyle = "tolerance" }); } }
    public bool CanUseMeasurementColor => isHeightDisplaySettings ? heightImage?.CanUseMeasurementColor == true : viewerHost?.HostState.Presentation.CanUseMeasurementColor == true;
    public string MeasurementColorGuidance => CanUseMeasurementColor
        ? (isHeightDisplaySettings ? heightImage?.MeasurementColorSummary : viewerHost?.HostState.Presentation.MeasurementColorSummary) + ThreeDLocalization.Shared.Resolve(
            "ThreeD.Legend.MeasuredColor", " · 측정 영역의 실행 당시 허용 범위 · 판정 표시 아님", " · executed limits on the measured region · not a verdict")
        : ThreeDLocalization.Shared.Resolve("ThreeD.Legend.MeasurementRequired", "허용 범위 색: 선택 두께 도구를 이 맵에서 실행하고 허용 범위를 설정하세요.", "Tolerance colours: execute the selected thickness tool on this map with configured limits.");
    public IReadOnlyList<int> HeightBandCounts { get; } = [5, 8, 10, 16, 20];
    public int HeightBandCount { get => CurrentHeightOptions.BandCount; set { if (HeightBandCounts.Contains(value)) ApplyHeightOptions(CurrentHeightOptions with { BandCount = value }); } }
    public string HeightBandInterval
    {
        get
        {
            var minimum = isHeightDisplaySettings ? heightImage?.HeightLegendMinimum : viewerHost?.HostState.Presentation.C3DHeightColorMinimumRaw;
            var maximum = isHeightDisplaySettings ? heightImage?.HeightLegendMaximum : viewerHost?.HostState.Presentation.C3DHeightColorMaximumRaw;
            return minimum.HasValue && maximum.HasValue && double.IsFinite(minimum.Value) && double.IsFinite(maximum.Value)
                ? $"{(maximum - minimum) / HeightBandCount:0.##} raw-height" : "—";
        }
    }
    public bool IsHeightEmphasis1 { get => DraftHeightEmphasis == "1"; set { if (value) DraftHeightEmphasis = "1"; } }
    public bool IsHeightEmphasis2 { get => DraftHeightEmphasis == "2"; set { if (value) DraftHeightEmphasis = "2"; } }
    public bool IsHeightEmphasis3 { get => DraftHeightEmphasis == "3"; set { if (value) DraftHeightEmphasis = "3"; } }
    public bool IsHeightEmphasis5 { get => DraftHeightEmphasis == "5"; set { if (value) DraftHeightEmphasis = "5"; } }

    public string DraftHeightRangeMinimum
    {
        get => draftHeightRangeMinimum;
        set
        {
            if (draftHeightRangeMinimum == value)
            {
                return;
            }

            draftHeightRangeMinimum = value;
            hasDisplaySettingsRangeError = false;
            OnPropertyChanged(nameof(DraftHeightRangeMinimum));
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
        }
    }

    public string DraftHeightRangeMaximum
    {
        get => draftHeightRangeMaximum;
        set
        {
            if (draftHeightRangeMaximum == value)
            {
                return;
            }

            draftHeightRangeMaximum = value;
            hasDisplaySettingsRangeError = false;
            OnPropertyChanged(nameof(DraftHeightRangeMaximum));
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
        }
    }

    public string DraftHeightEmphasis
    {
        get => draftHeightEmphasis;
        set
        {
            if (draftHeightEmphasis == value)
            {
                return;
            }

            draftHeightEmphasis = value;
            OnPropertyChanged(nameof(DraftHeightEmphasis));
            OnPropertyChanged(nameof(IsHeightEmphasis1));
            OnPropertyChanged(nameof(IsHeightEmphasis2));
            OnPropertyChanged(nameof(IsHeightEmphasis3));
            OnPropertyChanged(nameof(IsHeightEmphasis5));
            if (isDisplaySettingsOpen && !isUpdatingDisplaySettingsDraft && viewerHost is { } host && !isDisposed
                && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var emphasis)
                && emphasis is 1 or 2 or 3 or 5)
            {
                var presentation = host.HostState.Presentation;
                if (!HasMatchingDisplaySettingsSource())
                {
                    HasDisplaySettingsStaleError = true;
                    return;
                }

                HasDisplaySettingsEmphasisError = !host.TrySetC3DHeightEmphasis(emphasis);
            }
        }
    }

    public bool HasDisplaySettingsRangeError
    {
        get => hasDisplaySettingsRangeError;
        private set
        {
            if (hasDisplaySettingsRangeError == value)
            {
                return;
            }

            hasDisplaySettingsRangeError = value;
            OnPropertyChanged(nameof(HasDisplaySettingsRangeError));
        }
    }

    public bool HasDisplaySettingsEmphasisError
    {
        get => hasDisplaySettingsEmphasisError;
        private set
        {
            if (hasDisplaySettingsEmphasisError == value)
            {
                return;
            }

            hasDisplaySettingsEmphasisError = value;
            OnPropertyChanged(nameof(HasDisplaySettingsEmphasisError));
        }
    }

    public bool HasDisplaySettingsStaleError
    {
        get => hasDisplaySettingsStaleError;
        private set
        {
            if (hasDisplaySettingsStaleError == value)
            {
                return;
            }

            hasDisplaySettingsStaleError = value;
            OnPropertyChanged(nameof(HasDisplaySettingsStaleError));
        }
    }

    public ICommand OpenDisplaySettingsCommand => openDisplaySettingsCommand;
    public ICommand OpenHeightDisplaySettingsCommand => openHeightDisplaySettingsCommand;

    public ICommand ApplyDisplaySettingsCommand => applyDisplaySettingsCommand;

    public ICommand CancelDisplaySettingsCommand => cancelDisplaySettingsCommand;

    public ICommand CloseDisplaySettingsCommand => closeDisplaySettingsCommand;

    public ICommand DecreaseHeightMinimumCommand => decreaseHeightMinimumCommand;

    public ICommand IncreaseHeightMinimumCommand => increaseHeightMinimumCommand;

    public ICommand DecreaseHeightMaximumCommand => decreaseHeightMaximumCommand;

    public ICommand IncreaseHeightMaximumCommand => increaseHeightMaximumCommand;

    public ICommand ResetHeightColorRangeCommand => resetHeightColorRangeCommand;

    public void ShiftC3DHeightColorMinimum(int direction) =>
        Execute(host => host.TryShiftC3DHeightColorMinimum(direction));

    public void ShiftC3DHeightColorMaximum(int direction) =>
        Execute(host => host.TryShiftC3DHeightColorMaximum(direction));

    public void ResetC3DHeightColorRange() =>
        Execute(host => host.TryResetC3DHeightColorRange());

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        IsDisplaySettingsOpen = false;
        if (viewerHost is { } host)
        {
            host.HostStateChanged -= OnHostStateChanged;
        }

        viewerHost = null;
        if (heightImage is not null) heightImage.PropertyChanged -= OnHeightImageChanged;
        rotateCommand.RaiseCanExecuteChanged();
        moveCommand.RaiseCanExecuteChanged();
        measureCommand.RaiseCanExecuteChanged();
        fitAllCommand.RaiseCanExecuteChanged();
        loadTextureCommand.RaiseCanExecuteChanged();
        clearTextureCommand.RaiseCanExecuteChanged();
        profileActionCommand.RaiseCanExecuteChanged();
        profilePickCommand.RaiseCanExecuteChanged();
        openDisplaySettingsCommand.RaiseCanExecuteChanged();
        openHeightDisplaySettingsCommand.RaiseCanExecuteChanged();
        applyDisplaySettingsCommand.RaiseCanExecuteChanged();
        cancelDisplaySettingsCommand.RaiseCanExecuteChanged();
        closeDisplaySettingsCommand.RaiseCanExecuteChanged();
        decreaseHeightMinimumCommand.RaiseCanExecuteChanged();
        increaseHeightMinimumCommand.RaiseCanExecuteChanged();
        decreaseHeightMaximumCommand.RaiseCanExecuteChanged();
        increaseHeightMaximumCommand.RaiseCanExecuteChanged();
        resetHeightColorRangeCommand.RaiseCanExecuteChanged();
    }

    private ViewerHostPresentationState Presentation =>
        viewerHost?.HostState.Presentation ?? ViewerHostPresentationState.Empty;

    private bool CanExecuteHostCommand() => !isDisposed && viewerHost is not null;

    private void OpenDisplaySettings(bool forHeightImage)
    {
        isHeightDisplaySettings = forHeightImage;
        IsDisplaySettingsOpen = true;
        BeginDisplaySettingsEdit();
        OnPropertyChanged(nameof(IsDisplaySettingsOpen));
        RaiseBindingProperties();
    }

    private bool HasMatchingDisplaySettingsSource() => isHeightDisplaySettings
        ? heightImage?.Frame is { } frame && displaySettingsSourceSha256 == frame.SourceContentSha256 && displaySettingsRangeRevision == heightImage.DisplayRangeRevision
        : viewerHost is { } host && displaySettingsSourceSha256 == host.HostState.Presentation.C3DHeightDistributionSourceSha256
            && displaySettingsRangeRevision == host.HostState.Presentation.C3DHeightColorRangeRevision;

    private void BeginDisplaySettingsEdit()
    {
        if (viewerHost is not { } host)
        {
            return;
        }

        var presentation = host.HostState.Presentation;
        isUpdatingDisplaySettingsDraft = true;
        try
        {
            displaySettingsSourceSha256 = isHeightDisplaySettings ? heightImage?.Frame?.SourceContentSha256 ?? string.Empty : presentation.C3DHeightDistributionSourceSha256;
            displaySettingsRangeRevision = isHeightDisplaySettings ? heightImage?.DisplayRangeRevision ?? 0 : presentation.C3DHeightColorRangeRevision;
            DraftHeightRangeMode = CurrentHeightOptions.RangeMode;
            DraftHeightRangeMinimum = isHeightDisplaySettings ? heightImage?.RangeMinimumText ?? string.Empty : FormatRawValue(presentation.C3DHeightColorMinimumRaw);
            DraftHeightRangeMaximum = isHeightDisplaySettings ? heightImage?.RangeMaximumText ?? string.Empty : FormatRawValue(presentation.C3DHeightColorMaximumRaw);
            DraftHeightEmphasis = presentation.C3DHeightEmphasis.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            isUpdatingDisplaySettingsDraft = false;
        }
        HasDisplaySettingsRangeError = false;
        HasDisplaySettingsEmphasisError = false;
        HasDisplaySettingsStaleError = false;
    }

    private void ApplyHeightOptions(HeightDisplayOptions options)
    {
        if (isUpdatingDisplaySettingsDraft || isDisposed || viewerHost is not { } host) return;
        if (!HasMatchingDisplaySettingsSource()) { HasDisplaySettingsStaleError = true; return; }
        var applied = isHeightDisplaySettings && heightImage is not null ? heightImage.TryApplyHeightDisplayOptions(options) : host.TryApplyHeightDisplayOptions(options);
        if (applied) { BeginDisplaySettingsEdit(); RaiseBindingProperties(); }
        else HasDisplaySettingsRangeError = true;
    }
    private void ApplyDisplaySettings()
    {
        if (!isDisplaySettingsOpen || isDisposed || viewerHost is not { } host)
        {
            return;
        }

        HasDisplaySettingsRangeError = false;
        HasDisplaySettingsStaleError = false;
        var presentation = host.HostState.Presentation;
        if (!HasMatchingDisplaySettingsSource())
        {
            HasDisplaySettingsStaleError = true;
            return;
        }

        var isAutoRange = DraftHeightRangeMode == "auto";
        if ((isHeightDisplaySettings ? heightImage?.Frame is null : !presentation.C3DHeightDistributionVisible) || DraftHeightRangeMode is not ("auto" or "manual"))
        {
            HasDisplaySettingsStaleError = true;
            return;
        }

        if (!int.TryParse(DraftHeightEmphasis, NumberStyles.None, CultureInfo.InvariantCulture, out var heightEmphasis)
            || heightEmphasis is not (1 or 2 or 3 or 5))
        {
            HasDisplaySettingsEmphasisError = true;
            return;
        }

        var minimum = double.NaN;
        var maximum = double.NaN;
        if (!isAutoRange
            && (!double.TryParse(DraftHeightRangeMinimum, NumberStyles.Float, CultureInfo.InvariantCulture, out minimum)
                || !double.IsFinite(minimum)
                || !double.TryParse(DraftHeightRangeMaximum, NumberStyles.Float, CultureInfo.InvariantCulture, out maximum)
                || !double.IsFinite(maximum)
                || minimum >= maximum))
        {
            HasDisplaySettingsRangeError = true;
            return;
        }

        var rangeApplied = isHeightDisplaySettings && heightImage is not null
            ? isAutoRange ? heightImage.IsAutoRange : heightImage.TryApplyManualRange(minimum, maximum)
            : isAutoRange
            ? presentation.C3DHeightColorRangeAuto || host.TryResetC3DHeightColorRange()
            : (!presentation.C3DHeightColorRangeAuto
                && minimum == presentation.C3DHeightColorMinimumRaw
                && maximum == presentation.C3DHeightColorMaximumRaw)
                || host.TryApplyLinkedC3DHeightColorRange(minimum, maximum);
        if (rangeApplied)
        {
            if (heightEmphasis != presentation.C3DHeightEmphasis && !host.TrySetC3DHeightEmphasis(heightEmphasis))
            {
                HasDisplaySettingsStaleError = true;
                return;
            }

            BeginDisplaySettingsEdit();
            return;
        }

        HasDisplaySettingsStaleError = true;
    }

    private void ClearDisplaySettingsDraft()
    {
        HasDisplaySettingsRangeError = false;
        HasDisplaySettingsEmphasisError = false;
        HasDisplaySettingsStaleError = false;
    }

    private static string FormatRawValue(double value) =>
        double.IsFinite(value) ? value.ToString("G", CultureInfo.InvariantCulture) : string.Empty;

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void Execute(Func<IOpenVisionThreeDViewerHost, bool> operation)
    {
        if (isDisposed || viewerHost is not { } host)
        {
            return;
        }

        operation(host);
        RaiseBindingProperties();
    }

    private void OnHostStateChanged(object? sender, ViewerHostStateChangedEventArgs args) =>
        RaiseBindingProperties();

    private void OnHeightImageChanged(object? sender, PropertyChangedEventArgs args) => RaiseBindingProperties();

    private void RaiseBindingProperties()
    {
        loadTextureCommand.RaiseCanExecuteChanged();
        clearTextureCommand.RaiseCanExecuteChanged();
        profileActionCommand.RaiseCanExecuteChanged();
        profilePickCommand.RaiseCanExecuteChanged();
        rotateCommand.RaiseCanExecuteChanged();
        moveCommand.RaiseCanExecuteChanged();
        measureCommand.RaiseCanExecuteChanged();
        fitAllCommand.RaiseCanExecuteChanged();
        openHeightDisplaySettingsCommand.RaiseCanExecuteChanged();
        foreach (var propertyName in BindingPropertyNames)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

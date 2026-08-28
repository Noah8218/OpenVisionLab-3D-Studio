using System.ComponentModel;
using System.Runtime.CompilerServices;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public sealed class ViewerDisplaySettingsViewModel : INotifyPropertyChanged
{
    private static readonly IReadOnlyList<ViewerGeometryStyle> PointGeometryStyles =
        Array.AsReadOnly([ViewerGeometryStyle.Points]);
    private static readonly IReadOnlyList<ViewerGeometryStyle> SurfaceGeometryStyles =
        Array.AsReadOnly([
            ViewerGeometryStyle.Points,
            ViewerGeometryStyle.Wireframe,
            ViewerGeometryStyle.Surface,
            ViewerGeometryStyle.SurfaceWithEdges
        ]);
    private static readonly IReadOnlyList<ViewerColorMap> GeneratedColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid, ViewerColorMap.Height]);
    private static readonly IReadOnlyList<ViewerColorMap> GeneratedResultColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid, ViewerColorMap.Height, ViewerColorMap.Deviation]);
    private static readonly IReadOnlyList<ViewerColorMap> C3DColorMaps =
        Array.AsReadOnly([
            ViewerColorMap.Solid,
            ViewerColorMap.Grayscale,
            ViewerColorMap.Height,
            ViewerColorMap.Thermal
        ]);
    private static readonly IReadOnlyList<ViewerColorMap> C3DResultColorMaps =
        Array.AsReadOnly([
            ViewerColorMap.Solid,
            ViewerColorMap.Grayscale,
            ViewerColorMap.Height,
            ViewerColorMap.Thermal,
            ViewerColorMap.Deviation
        ]);
    private static readonly IReadOnlyList<ViewerColorMap> ImportedMeshSourceColorMaps =
        Array.AsReadOnly([ViewerColorMap.Source]);
    private static readonly IReadOnlyList<ViewerColorMap> ImportedMeshSourceNormalColorMaps =
        Array.AsReadOnly([ViewerColorMap.Source, ViewerColorMap.Normal]);
    private static readonly IReadOnlyList<ViewerColorMap> ImportedMeshSolidColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid]);
    private static readonly IReadOnlyList<ViewerColorMap> ImportedMeshSolidNormalColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid, ViewerColorMap.Normal]);
    private static readonly IReadOnlyList<ViewerColorMap> PointCloudColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid, ViewerColorMap.Height, ViewerColorMap.Rgb]);
    private static readonly IReadOnlyList<ViewerColorMap> PointCloudRgbAndIntensityColorMaps =
        Array.AsReadOnly([
            ViewerColorMap.Solid,
            ViewerColorMap.Height,
            ViewerColorMap.Rgb,
            ViewerColorMap.Intensity
        ]);
    private static readonly IReadOnlyList<ViewerColorMap> PointCloudIntensityColorMaps =
        Array.AsReadOnly([
            ViewerColorMap.Solid,
            ViewerColorMap.Height,
            ViewerColorMap.Intensity
        ]);
    private static readonly IReadOnlyList<ViewerColorMap> PointCloudWithoutRgbColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid, ViewerColorMap.Height]);
    private static readonly IReadOnlyList<ViewerColorMap> NominalActualColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid]);
    private static readonly IReadOnlyList<ViewerColorMap> NominalActualResultColorMaps =
        Array.AsReadOnly([ViewerColorMap.Deviation]);

    private ViewerDisplaySourceKind activeSource = ViewerDisplaySourceKind.GeneratedGeometry;
    private IReadOnlyList<ViewerGeometryStyle> availableGeometryStyleIds = PointGeometryStyles;
    private IReadOnlyList<ViewerColorMap> availableColorMapIds = GeneratedColorMaps;
    private IReadOnlyList<string> availableGeometryStyles = ToGeometryStyleLabels(PointGeometryStyles);
    private IReadOnlyList<string> availableColorMaps = ToColorMapLabels(GeneratedColorMaps);
    private IReadOnlyList<ViewerDiagnosticChannelOption> diagnosticChannelOptions = [];
    private ViewerGeometryStyle selectedGeometryStyle = ViewerGeometryStyle.Points;
    private ViewerColorMap selectedColorMap = ViewerColorMap.Height;
    private ViewerDiagnosticChannelOption? selectedDiagnosticChannel;
    private bool fallbackApplied;
    private string fallbackSummary = "No display fallback.";
    private int displaySettingsRevision;
    private SourceNormalQualityReport? importedMeshNormalQuality;
    private double pointSize = 2.0;
    private string selectedRenderDensity = "Balanced";
    private string renderDensitySummary = FormatRenderDensitySummary("Balanced");

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? RenderSettingsChanged;

    public string ActiveSource => GetSourceLabel(activeSource);

    public IReadOnlyList<string> AvailableGeometryStyles
    {
        get => availableGeometryStyles;
        private set => SetField(ref availableGeometryStyles, value);
    }

    public IReadOnlyList<string> AvailableColorMaps
    {
        get => availableColorMaps;
        private set => SetField(ref availableColorMaps, value);
    }

    public IReadOnlyList<ViewerDiagnosticChannelOption> DiagnosticChannelOptions
    {
        get => diagnosticChannelOptions;
        private set => SetField(ref diagnosticChannelOptions, value);
    }

    public ViewerDiagnosticChannelOption? SelectedDiagnosticChannel
    {
        get => selectedDiagnosticChannel;
        set => SelectDiagnosticChannel(value);
    }

    public bool CanSelectDiagnosticChannel => DiagnosticChannelOptions.Count > 0;

    public string DiagnosticChannelSummary => selectedDiagnosticChannel is { } channel
        ? $"Diagnostic channel: {channel.Label} | {channel.Evidence} | Display only."
        : $"No selectable source diagnostic channel for {ActiveSource}; {EffectiveColorMap} remains display-only.";

    public string SelectedGeometryStyle
    {
        get => GetGeometryStyleLabel(selectedGeometryStyle);
        set
        {
            if (string.Equals(SelectedGeometryStyle, value, StringComparison.Ordinal))
            {
                return;
            }

            if (!CanSelectGeometryStyle)
            {
                SetFallback($"Geometry Style is not selectable for {ActiveSource}; effective style remains {SelectedGeometryStyle}. Display only.");
                return;
            }

            if (!TryGetGeometryStyle(value, out var requestedStyle)
                || !availableGeometryStyleIds.Contains(requestedStyle))
            {
                SetFallback($"Geometry Style '{value}' is unavailable for {ActiveSource}; using {SelectedGeometryStyle}. Display only.");
                return;
            }

            ClearFallback();
            if (SetField(ref selectedGeometryStyle, requestedStyle, nameof(SelectedGeometryStyle)))
            {
                OnPropertyChanged(nameof(EffectiveGeometryStyle));
                OnPropertyChanged(nameof(EffectiveSummary));
                OnPropertyChanged(nameof(EffectiveSettings));
                NotifyRenderSettingsChanged();
            }
        }
    }

    public string SelectedColorMap
    {
        get => GetColorMapLabel(selectedColorMap);
        set => SelectColorMap(value);
    }

    public string EffectiveGeometryStyle => SelectedGeometryStyle;

    public string EffectiveColorMap => SelectedColorMap;

    public bool CanSelectGeometryStyle =>
        (activeSource is ViewerDisplaySourceKind.C3DHeightGrid
            or ViewerDisplaySourceKind.ImportedTriangleMesh)
        && availableGeometryStyleIds.Count > 1;

    public bool CanSelectColorMap => availableColorMapIds.Count > 1;

    public bool IsDisplayOnly => true;

    public bool FallbackApplied
    {
        get => fallbackApplied;
        private set => SetField(ref fallbackApplied, value);
    }

    public string FallbackSummary
    {
        get => fallbackSummary;
        private set => SetField(ref fallbackSummary, value);
    }

    public string EffectiveSummary =>
        $"{ActiveSource} | {EffectiveGeometryStyle} | {EffectiveColorMap} | Display only";

    public string[] RenderDensityModes { get; } = ["Fast", "Balanced", "Detailed"];

    public int DisplaySettingsRevision => displaySettingsRevision;

    public double PointSize
    {
        get => pointSize;
        set => SetField(ref pointSize, Math.Clamp(value, 1.0, 6.0));
    }

    public string SelectedRenderDensity
    {
        get => selectedRenderDensity;
        set
        {
            var mode = RenderDensityModes.Contains(value) ? value : "Balanced";
            if (string.Equals(selectedRenderDensity, mode, StringComparison.Ordinal))
            {
                return;
            }

            selectedRenderDensity = mode;
            renderDensitySummary = FormatRenderDensitySummary(mode);
            OnPropertyChanged();
            OnPropertyChanged(nameof(RenderDensitySummary));
            OnPropertyChanged(nameof(C3DMaxRenderedPoints));
            OnPropertyChanged(nameof(LazMaxSampledPoints));
            OnPropertyChanged(nameof(ImportedMeshMaxRenderedTriangles));
            OnPropertyChanged(nameof(NominalActualMaxDisplaySamples));
        }
    }

    public string RenderDensitySummary => renderDensitySummary;

    public int C3DMaxRenderedPoints => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 140000,
        _ => 55000
    };

    public int LazMaxSampledPoints => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 150000,
        _ => 50000
    };

    public int ImportedMeshMaxRenderedTriangles => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 180000,
        _ => 60000
    };

    public int NominalActualMaxDisplaySamples => SelectedRenderDensity switch
    {
        "Fast" => 25000,
        "Detailed" => 150000,
        _ => 60000
    };

    internal ViewerDisplaySettingsSnapshot EffectiveSettings =>
        new(activeSource, selectedGeometryStyle, selectedColorMap, IsDisplayOnly);

    internal void ConfigureGeneratedGeometry(bool deviationAvailable) =>
        Configure(
            ViewerDisplaySourceKind.GeneratedGeometry,
            PointGeometryStyles,
            deviationAvailable ? GeneratedResultColorMaps : GeneratedColorMaps,
            ViewerGeometryStyle.Points,
            ViewerColorMap.Height,
            CreateUnavailableSourceChannels("Generated geometry does not expose source diagnostic channels."));

    internal void ConfigureC3DHeightGrid(
        bool deviationAvailable,
        bool surfaceGeometryAvailable = true,
        IReadOnlyList<SourceQualityChannelAvailability>? sourceChannels = null) =>
        Configure(
            ViewerDisplaySourceKind.C3DHeightGrid,
            surfaceGeometryAvailable ? SurfaceGeometryStyles : PointGeometryStyles,
            deviationAvailable ? C3DResultColorMaps : C3DColorMaps,
            surfaceGeometryAvailable ? ViewerGeometryStyle.Surface : ViewerGeometryStyle.Points,
            ViewerColorMap.Height,
            sourceChannels ?? CreateFallbackSourceChannels(
                ViewerDisplaySourceKind.C3DHeightGrid,
                sourceColorAvailable: false,
                sourceIntensityAvailable: false));

    internal void ResetC3DHeightGridGeometryStyle(bool surfaceGeometryAvailable = true)
    {
        var defaultStyle = surfaceGeometryAvailable
            ? ViewerGeometryStyle.Surface
            : ViewerGeometryStyle.Points;
        if (SetField(ref selectedGeometryStyle, defaultStyle, nameof(SelectedGeometryStyle)))
        {
            OnPropertyChanged(nameof(EffectiveGeometryStyle));
            OnPropertyChanged(nameof(EffectiveSummary));
            OnPropertyChanged(nameof(EffectiveSettings));
            NotifyRenderSettingsChanged();
        }
    }

    internal void ConfigureImportedMesh(
        bool sourceColorAvailable,
        IReadOnlyList<SourceQualityChannelAvailability>? sourceChannels = null,
        SourceNormalQualityReport? normalQuality = null)
    {
        importedMeshNormalQuality = normalQuality;
        var normalDisplayable = IsImportedMeshNormalDisplayable;
        Configure(
            ViewerDisplaySourceKind.ImportedTriangleMesh,
            SurfaceGeometryStyles,
            sourceColorAvailable
                ? normalDisplayable ? ImportedMeshSourceNormalColorMaps : ImportedMeshSourceColorMaps
                : normalDisplayable ? ImportedMeshSolidNormalColorMaps : ImportedMeshSolidColorMaps,
            ViewerGeometryStyle.SurfaceWithEdges,
            sourceColorAvailable ? ViewerColorMap.Source : ViewerColorMap.Solid,
            sourceChannels ?? CreateFallbackSourceChannels(
                ViewerDisplaySourceKind.ImportedTriangleMesh,
                sourceColorAvailable,
                sourceIntensityAvailable: false));
    }

    internal bool IsImportedMeshNormalDisplayable =>
        importedMeshNormalQuality is { IsUsable: true, IsDense: true };

    internal SourceNormalQualityReport? ImportedMeshNormalQuality => importedMeshNormalQuality;

    internal void ConfigurePointCloud(
        bool sourceColorAvailable,
        bool sourceIntensityAvailable = false,
        IReadOnlyList<SourceQualityChannelAvailability>? sourceChannels = null) =>
        Configure(
            ViewerDisplaySourceKind.PointCloud,
            PointGeometryStyles,
            sourceColorAvailable && sourceIntensityAvailable
                ? PointCloudRgbAndIntensityColorMaps
                : sourceColorAvailable
                    ? PointCloudColorMaps
                    : sourceIntensityAvailable
                        ? PointCloudIntensityColorMaps
                        : PointCloudWithoutRgbColorMaps,
            ViewerGeometryStyle.Points,
            sourceColorAvailable
                ? ViewerColorMap.Rgb
                : sourceIntensityAvailable
                    ? ViewerColorMap.Intensity
                    : ViewerColorMap.Height,
            sourceChannels ?? CreateFallbackSourceChannels(
                ViewerDisplaySourceKind.PointCloud,
                sourceColorAvailable,
                sourceIntensityAvailable));

    internal void ConfigureNominalActualComparison(bool deviationAvailable) =>
        Configure(
            ViewerDisplaySourceKind.NominalActualComparison,
            PointGeometryStyles,
            deviationAvailable ? NominalActualResultColorMaps : NominalActualColorMaps,
            ViewerGeometryStyle.Points,
            deviationAvailable ? ViewerColorMap.Deviation : ViewerColorMap.Solid,
            CreateUnavailableSourceChannels("Nominal/actual comparison does not expose a source diagnostic channel."));

    internal static string GetColorMapLabel(ViewerColorMap colorMap) => colorMap switch
    {
        ViewerColorMap.Source => "Source",
        ViewerColorMap.Solid => "Solid",
        ViewerColorMap.Grayscale => "Grayscale",
        ViewerColorMap.Height => "Height",
        ViewerColorMap.Thermal => "Thermal",
        ViewerColorMap.Deviation => "Deviation",
        ViewerColorMap.Rgb => "RGB",
        ViewerColorMap.Intensity => "Intensity",
        ViewerColorMap.Normal => "Normal",
        _ => throw new ArgumentOutOfRangeException(nameof(colorMap), colorMap, null)
    };

    internal void RefreshLocalizedPresentation()
    {
        OnPropertyChanged(nameof(AvailableGeometryStyles));
        OnPropertyChanged(nameof(AvailableColorMaps));
        OnPropertyChanged(nameof(DiagnosticChannelOptions));
        OnPropertyChanged(nameof(SelectedGeometryStyle));
        OnPropertyChanged(nameof(SelectedColorMap));
        OnPropertyChanged(nameof(SelectedDiagnosticChannel));
        OnPropertyChanged(nameof(DiagnosticChannelSummary));
        OnPropertyChanged(nameof(EffectiveGeometryStyle));
        OnPropertyChanged(nameof(EffectiveColorMap));
        OnPropertyChanged(nameof(EffectiveSummary));
    }

    private void Configure(
        ViewerDisplaySourceKind source,
        IReadOnlyList<ViewerGeometryStyle> geometryStyles,
        IReadOnlyList<ViewerColorMap> colorMaps,
        ViewerGeometryStyle defaultGeometryStyle,
        ViewerColorMap defaultColorMap,
        IReadOnlyList<SourceQualityChannelAvailability> sourceChannels)
    {
        var sourceChanged = activeSource != source;
        if (sourceChanged)
        {
            activeSource = source;
            OnPropertyChanged(nameof(ActiveSource));
        }

        if (!ReferenceEquals(availableGeometryStyleIds, geometryStyles))
        {
            availableGeometryStyleIds = geometryStyles;
            AvailableGeometryStyles = ToGeometryStyleLabels(geometryStyles);
        }

        if (!ReferenceEquals(availableColorMapIds, colorMaps))
        {
            availableColorMapIds = colorMaps;
            AvailableColorMaps = ToColorMapLabels(colorMaps);
        }

        var geometryChanged = false;
        if (sourceChanged || !geometryStyles.Contains(selectedGeometryStyle))
        {
            geometryChanged = SetField(
                ref selectedGeometryStyle,
                defaultGeometryStyle,
                nameof(SelectedGeometryStyle));
            if (geometryChanged)
            {
                OnPropertyChanged(nameof(EffectiveGeometryStyle));
            }
        }

        if (!colorMaps.Contains(selectedColorMap))
        {
            var requestedColorMap = GetColorMapLabel(selectedColorMap);
            ApplyColorMap(
                defaultColorMap,
                CreateColorFallbackSummary(requestedColorMap, GetColorMapLabel(defaultColorMap)));
        }
        else
        {
            ClearFallback();
        }

        ConfigureDiagnosticChannelOptions(sourceChannels);
        RefreshDiagnosticChannelSelection();

        OnPropertyChanged(nameof(CanSelectGeometryStyle));
        OnPropertyChanged(nameof(CanSelectColorMap));
        OnPropertyChanged(nameof(CanSelectDiagnosticChannel));
        OnPropertyChanged(nameof(EffectiveSummary));
        if (sourceChanged || geometryChanged)
        {
            OnPropertyChanged(nameof(EffectiveSettings));
        }
    }

    private void SelectColorMap(string? requestedColorMap)
    {
        var requested = string.IsNullOrWhiteSpace(requestedColorMap)
            ? string.Empty
            : requestedColorMap;
        if (!TryGetColorMap(requested, out var requestedColorMapId)
            || !availableColorMapIds.Contains(requestedColorMapId))
        {
            var fallback = DefaultColorMap();
            ApplyColorMap(
                fallback,
                CreateColorFallbackSummary(requested, GetColorMapLabel(fallback)));
            return;
        }

        ClearFallback();
        ApplyColorMap(requestedColorMapId, null);
    }

    private void ApplyColorMap(ViewerColorMap colorMap, string? fallback)
    {
        if (fallback is not null)
        {
            SetFallback(fallback);
        }

        if (SetField(ref selectedColorMap, colorMap, nameof(SelectedColorMap)))
        {
            OnPropertyChanged(nameof(EffectiveColorMap));
            OnPropertyChanged(nameof(EffectiveSummary));
            OnPropertyChanged(nameof(EffectiveSettings));
            NotifyRenderSettingsChanged();
        }

        RefreshDiagnosticChannelSelection();
        OnPropertyChanged(nameof(DiagnosticChannelSummary));
    }

    private void SelectDiagnosticChannel(ViewerDiagnosticChannelOption? requestedChannel)
    {
        if (requestedChannel is null)
        {
            if (SetField(ref selectedDiagnosticChannel, null, nameof(SelectedDiagnosticChannel)))
            {
                OnPropertyChanged(nameof(DiagnosticChannelSummary));
            }

            return;
        }

        var channel = DiagnosticChannelOptions.FirstOrDefault(
            option => option.Channel == requestedChannel.Channel);
        if (channel is null)
        {
            SetFallback($"Diagnostic channel '{requestedChannel.Label}' is not available for {ActiveSource}; display only.");
            return;
        }

        if (!channel.IsSelectable)
        {
            SetFallback($"Diagnostic channel '{channel.Label}' is not selectable for {ActiveSource}; {channel.HelpText}");
            return;
        }

        if (!TryGetColorMapForDiagnosticChannel(channel.Channel, out var colorMap)
            || !availableColorMapIds.Contains(colorMap))
        {
            SetFallback($"Diagnostic channel '{channel.Label}' has no display path for {ActiveSource}; display only.");
            return;
        }

        ClearFallback();
        if (SetField(ref selectedDiagnosticChannel, channel, nameof(SelectedDiagnosticChannel)))
        {
            OnPropertyChanged(nameof(DiagnosticChannelSummary));
        }

        ApplyColorMap(colorMap, null);
    }

    private void ConfigureDiagnosticChannelOptions(
        IReadOnlyList<SourceQualityChannelAvailability> sourceChannels)
    {
        ArgumentNullException.ThrowIfNull(sourceChannels);

        var options = sourceChannels
            .Select(channel => new ViewerDiagnosticChannelOption(
                channel.Channel,
                GetDiagnosticChannelLabel(channel.Channel),
                channel.State,
                IsDiagnosticChannelDisplayable(channel.Channel),
                GetDiagnosticChannelEvidence(channel)))
            .ToArray();
        DiagnosticChannelOptions = Array.AsReadOnly(options);
        OnPropertyChanged(nameof(CanSelectDiagnosticChannel));
        OnPropertyChanged(nameof(DiagnosticChannelSummary));
    }

    private void RefreshDiagnosticChannelSelection()
    {
        var channel = ResolveDiagnosticChannel(selectedColorMap);
        var next = channel is null
            ? null
            : DiagnosticChannelOptions.FirstOrDefault(
                option => option.Channel == channel.Value && option.IsSelectable);
        if (SetField(ref selectedDiagnosticChannel, next, nameof(SelectedDiagnosticChannel)))
        {
            OnPropertyChanged(nameof(DiagnosticChannelSummary));
        }
    }

    private SourceQualityChannel? ResolveDiagnosticChannel(ViewerColorMap colorMap) =>
        activeSource switch
        {
            ViewerDisplaySourceKind.C3DHeightGrid when colorMap is ViewerColorMap.Grayscale
                or ViewerColorMap.Height
                or ViewerColorMap.Thermal => SourceQualityChannel.Height,
            ViewerDisplaySourceKind.ImportedTriangleMesh when colorMap == ViewerColorMap.Source => SourceQualityChannel.Color,
            ViewerDisplaySourceKind.ImportedTriangleMesh when colorMap == ViewerColorMap.Normal => SourceQualityChannel.Normal,
            ViewerDisplaySourceKind.PointCloud when colorMap == ViewerColorMap.Rgb => SourceQualityChannel.Color,
            ViewerDisplaySourceKind.PointCloud when colorMap == ViewerColorMap.Intensity => SourceQualityChannel.Intensity,
            _ => null
        };

    private bool TryGetColorMapForDiagnosticChannel(
        SourceQualityChannel channel,
        out ViewerColorMap colorMap)
    {
        colorMap = (activeSource, channel) switch
        {
            (ViewerDisplaySourceKind.C3DHeightGrid, SourceQualityChannel.Height) => ViewerColorMap.Height,
            (ViewerDisplaySourceKind.ImportedTriangleMesh, SourceQualityChannel.Color) => ViewerColorMap.Source,
            (ViewerDisplaySourceKind.ImportedTriangleMesh, SourceQualityChannel.Normal) => ViewerColorMap.Normal,
            (ViewerDisplaySourceKind.PointCloud, SourceQualityChannel.Color) => ViewerColorMap.Rgb,
            (ViewerDisplaySourceKind.PointCloud, SourceQualityChannel.Intensity) => ViewerColorMap.Intensity,
            _ => default
        };
        return (activeSource, channel) is
            (ViewerDisplaySourceKind.C3DHeightGrid, SourceQualityChannel.Height)
            or (ViewerDisplaySourceKind.ImportedTriangleMesh, SourceQualityChannel.Color)
            or (ViewerDisplaySourceKind.ImportedTriangleMesh, SourceQualityChannel.Normal)
            or (ViewerDisplaySourceKind.PointCloud, SourceQualityChannel.Color)
            or (ViewerDisplaySourceKind.PointCloud, SourceQualityChannel.Intensity);
    }

    private bool IsDiagnosticChannelDisplayable(SourceQualityChannel channel) =>
        TryGetColorMapForDiagnosticChannel(channel, out var colorMap)
        && availableColorMapIds.Contains(colorMap);

    private string GetDiagnosticChannelEvidence(SourceQualityChannelAvailability channel)
    {
        if (channel.Channel == SourceQualityChannel.Normal
            && activeSource == ViewerDisplaySourceKind.ImportedTriangleMesh
            && importedMeshNormalQuality is { IsUsable: false } report)
        {
            return $"{channel.Evidence} Normal-quality report: {report.Evidence}";
        }

        return channel.Evidence;
    }

    private IReadOnlyList<SourceQualityChannelAvailability> CreateFallbackSourceChannels(
        ViewerDisplaySourceKind source,
        bool sourceColorAvailable,
        bool sourceIntensityAvailable) =>
        source switch
        {
            ViewerDisplaySourceKind.C3DHeightGrid => CreateSourceChannelCatalog(
                height: true,
                color: false,
                intensity: false,
                "C3D height grid"),
            ViewerDisplaySourceKind.ImportedTriangleMesh => CreateSourceChannelCatalog(
                height: false,
                color: sourceColorAvailable,
                intensity: false,
                "Imported triangle mesh"),
            ViewerDisplaySourceKind.PointCloud => CreateSourceChannelCatalog(
                height: false,
                color: sourceColorAvailable,
                intensity: sourceIntensityAvailable,
                "LAZ/LAS point cloud"),
            _ => CreateUnavailableSourceChannels(
                $"{GetSourceLabel(source)} does not expose source diagnostic channels.")
        };

    private static IReadOnlyList<SourceQualityChannelAvailability> CreateSourceChannelCatalog(
        bool height,
        bool color,
        bool intensity,
        string sourceLabel) =>
        Array.AsReadOnly<SourceQualityChannelAvailability>(
        [
            CreateChannel(SourceQualityChannel.Height, height, sourceLabel),
            CreateChannel(SourceQualityChannel.Intensity, intensity, sourceLabel),
            CreateChannel(SourceQualityChannel.Color, color, sourceLabel),
            CreateUnavailableChannel(SourceQualityChannel.Depth, sourceLabel),
            CreateUnavailableChannel(SourceQualityChannel.Normal, sourceLabel),
            CreateUnavailableChannel(SourceQualityChannel.Confidence, sourceLabel),
            CreateUnavailableChannel(SourceQualityChannel.SignalToNoiseRatio, sourceLabel)
        ]);

    private static IReadOnlyList<SourceQualityChannelAvailability> CreateUnavailableSourceChannels(
        string evidence) =>
        Array.AsReadOnly<SourceQualityChannelAvailability>(
        [
            new(SourceQualityChannel.Height, SourceQualityChannelState.Unavailable, evidence),
            new(SourceQualityChannel.Intensity, SourceQualityChannelState.Unavailable, evidence),
            new(SourceQualityChannel.Color, SourceQualityChannelState.Unavailable, evidence),
            new(SourceQualityChannel.Depth, SourceQualityChannelState.Unavailable, evidence),
            new(SourceQualityChannel.Normal, SourceQualityChannelState.Unavailable, evidence),
            new(SourceQualityChannel.Confidence, SourceQualityChannelState.Unavailable, evidence),
            new(SourceQualityChannel.SignalToNoiseRatio, SourceQualityChannelState.Unavailable, evidence)
        ]);

    private static SourceQualityChannelAvailability CreateChannel(
        SourceQualityChannel channel,
        bool available,
        string sourceLabel) =>
        available
            ? new(
                channel,
                SourceQualityChannelState.Available,
                $"{sourceLabel} exposes {GetDiagnosticChannelLabel(channel)} for display.")
            : CreateUnavailableChannel(channel, sourceLabel);

    private static SourceQualityChannelAvailability CreateUnavailableChannel(
        SourceQualityChannel channel,
        string sourceLabel) =>
        new(
            channel,
            SourceQualityChannelState.Unavailable,
            $"{sourceLabel} does not expose a supported {GetDiagnosticChannelLabel(channel)} channel.");

    private static string GetDiagnosticChannelLabel(SourceQualityChannel channel) => channel switch
    {
        SourceQualityChannel.Height => "Height",
        SourceQualityChannel.Intensity => "Intensity",
        SourceQualityChannel.Color => "Color",
        SourceQualityChannel.Depth => "Depth",
        SourceQualityChannel.Normal => "Normal",
        SourceQualityChannel.Confidence => "Confidence",
        SourceQualityChannel.SignalToNoiseRatio => "Signal/Noise",
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null)
    };

    private ViewerColorMap DefaultColorMap() => activeSource switch
    {
        ViewerDisplaySourceKind.C3DHeightGrid => ViewerColorMap.Height,
        ViewerDisplaySourceKind.ImportedTriangleMesh => availableColorMapIds[0],
        ViewerDisplaySourceKind.PointCloud when availableColorMapIds.Contains(ViewerColorMap.Rgb) => ViewerColorMap.Rgb,
        ViewerDisplaySourceKind.PointCloud when availableColorMapIds.Contains(ViewerColorMap.Intensity) => ViewerColorMap.Intensity,
        ViewerDisplaySourceKind.PointCloud => ViewerColorMap.Height,
        ViewerDisplaySourceKind.NominalActualComparison when availableColorMapIds.Contains(ViewerColorMap.Deviation) => ViewerColorMap.Deviation,
        ViewerDisplaySourceKind.NominalActualComparison => ViewerColorMap.Solid,
        _ => ViewerColorMap.Height
    };

    private string CreateColorFallbackSummary(string requestedColorMap, string fallbackColorMap)
    {
        var requested = string.IsNullOrWhiteSpace(requestedColorMap) ? "(none)" : requestedColorMap;
        return requested.Equals("Deviation", StringComparison.Ordinal)
            ? $"Deviation requires an active result for {ActiveSource}; using {fallbackColorMap}. Display only."
            : $"Color Map '{requested}' is unavailable for {ActiveSource}; using {fallbackColorMap}. Display only.";
    }

    private void NotifyRenderSettingsChanged()
    {
        displaySettingsRevision = unchecked(displaySettingsRevision + 1);
        OnPropertyChanged(nameof(DisplaySettingsRevision));
        RenderSettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string FormatRenderDensitySummary(string mode) => mode switch
    {
        "Fast" => "Fast: up to 25,000 C3D points / 25,000 LAZ/LAS points / 25,000 mesh triangles",
        "Detailed" => "Detailed: up to 140,000 C3D points / 150,000 LAZ/LAS points / 180,000 mesh triangles",
        _ => "Balanced: up to 55,000 C3D points / 50,000 LAZ/LAS points / 60,000 mesh triangles"
    };

    private void SetFallback(string summary)
    {
        FallbackSummary = summary;
        FallbackApplied = true;
        OnPropertyChanged(nameof(EffectiveSummary));
    }

    private void ClearFallback()
    {
        FallbackApplied = false;
        FallbackSummary = "No display fallback.";
    }

    private static IReadOnlyList<string> ToGeometryStyleLabels(IEnumerable<ViewerGeometryStyle> styles) =>
        Array.AsReadOnly(styles.Select(GetGeometryStyleLabel).ToArray());

    private static IReadOnlyList<string> ToColorMapLabels(IEnumerable<ViewerColorMap> colorMaps) =>
        Array.AsReadOnly(colorMaps.Select(GetColorMapLabel).ToArray());

    private static string GetSourceLabel(ViewerDisplaySourceKind source) => source switch
    {
        ViewerDisplaySourceKind.GeneratedGeometry => "Generated geometry",
        ViewerDisplaySourceKind.C3DHeightGrid => "C3D height grid",
        ViewerDisplaySourceKind.ImportedTriangleMesh => "Imported triangle mesh",
        ViewerDisplaySourceKind.PointCloud => "LAZ/LAS point cloud",
        ViewerDisplaySourceKind.NominalActualComparison => "Nominal/actual comparison",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, null)
    };

    private static string GetGeometryStyleLabel(ViewerGeometryStyle style) => style switch
    {
        ViewerGeometryStyle.Points => "Points",
        ViewerGeometryStyle.Wireframe => "Wireframe",
        ViewerGeometryStyle.Surface => "Surface",
        ViewerGeometryStyle.SurfaceWithEdges => "Surface + Edges",
        _ => throw new ArgumentOutOfRangeException(nameof(style), style, null)
    };

    private static bool TryGetGeometryStyle(string? label, out ViewerGeometryStyle style)
    {
        style = label switch
        {
            "Points" => ViewerGeometryStyle.Points,
            "Wireframe" => ViewerGeometryStyle.Wireframe,
            "Surface" => ViewerGeometryStyle.Surface,
            "Surface + Edges" => ViewerGeometryStyle.SurfaceWithEdges,
            _ => default
        };
        return label is "Points" or "Wireframe" or "Surface" or "Surface + Edges";
    }

    private static bool TryGetColorMap(string? label, out ViewerColorMap colorMap)
    {
        colorMap = label switch
        {
            "Source" => ViewerColorMap.Source,
            "Solid" => ViewerColorMap.Solid,
            "Grayscale" => ViewerColorMap.Grayscale,
            "Height" => ViewerColorMap.Height,
            "Thermal" => ViewerColorMap.Thermal,
            "Deviation" => ViewerColorMap.Deviation,
            "RGB" => ViewerColorMap.Rgb,
            "Intensity" => ViewerColorMap.Intensity,
            "Normal" => ViewerColorMap.Normal,
            _ => default
        };
        return label is "Source" or "Solid" or "Grayscale" or "Height" or "Thermal" or "Deviation" or "RGB" or "Intensity" or "Normal";
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
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

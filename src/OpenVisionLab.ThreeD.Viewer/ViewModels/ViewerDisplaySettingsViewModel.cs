using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    private static readonly IReadOnlyList<ViewerColorMap> ImportedMeshSolidColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid]);
    private static readonly IReadOnlyList<ViewerColorMap> PointCloudColorMaps =
        Array.AsReadOnly([ViewerColorMap.Solid, ViewerColorMap.Height, ViewerColorMap.Rgb]);
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
    private ViewerGeometryStyle selectedGeometryStyle = ViewerGeometryStyle.Points;
    private ViewerColorMap selectedColorMap = ViewerColorMap.Height;
    private bool fallbackApplied;
    private string fallbackSummary = "No display fallback.";

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
                RenderSettingsChanged?.Invoke(this, EventArgs.Empty);
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

    internal ViewerDisplaySettingsSnapshot EffectiveSettings =>
        new(activeSource, selectedGeometryStyle, selectedColorMap, IsDisplayOnly);

    internal void ConfigureGeneratedGeometry(bool deviationAvailable) =>
        Configure(
            ViewerDisplaySourceKind.GeneratedGeometry,
            PointGeometryStyles,
            deviationAvailable ? GeneratedResultColorMaps : GeneratedColorMaps,
            ViewerGeometryStyle.Points,
            ViewerColorMap.Height);

    internal void ConfigureC3DHeightGrid(
        bool deviationAvailable,
        bool surfaceGeometryAvailable = true) =>
        Configure(
            ViewerDisplaySourceKind.C3DHeightGrid,
            surfaceGeometryAvailable ? SurfaceGeometryStyles : PointGeometryStyles,
            deviationAvailable ? C3DResultColorMaps : C3DColorMaps,
            surfaceGeometryAvailable ? ViewerGeometryStyle.Wireframe : ViewerGeometryStyle.Points,
            ViewerColorMap.Height);

    internal void ResetC3DHeightGridGeometryStyle(bool surfaceGeometryAvailable = true)
    {
        var defaultStyle = surfaceGeometryAvailable
            ? ViewerGeometryStyle.Wireframe
            : ViewerGeometryStyle.Points;
        if (SetField(ref selectedGeometryStyle, defaultStyle, nameof(SelectedGeometryStyle)))
        {
            OnPropertyChanged(nameof(EffectiveGeometryStyle));
            OnPropertyChanged(nameof(EffectiveSummary));
            OnPropertyChanged(nameof(EffectiveSettings));
            RenderSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void ConfigureImportedMesh(bool sourceColorAvailable) =>
        Configure(
            ViewerDisplaySourceKind.ImportedTriangleMesh,
            SurfaceGeometryStyles,
            sourceColorAvailable ? ImportedMeshSourceColorMaps : ImportedMeshSolidColorMaps,
            ViewerGeometryStyle.SurfaceWithEdges,
            sourceColorAvailable ? ViewerColorMap.Source : ViewerColorMap.Solid);

    internal void ConfigurePointCloud(bool sourceColorAvailable) =>
        Configure(
            ViewerDisplaySourceKind.PointCloud,
            PointGeometryStyles,
            sourceColorAvailable ? PointCloudColorMaps : PointCloudWithoutRgbColorMaps,
            ViewerGeometryStyle.Points,
            sourceColorAvailable ? ViewerColorMap.Rgb : ViewerColorMap.Height);

    internal void ConfigureNominalActualComparison(bool deviationAvailable) =>
        Configure(
            ViewerDisplaySourceKind.NominalActualComparison,
            PointGeometryStyles,
            deviationAvailable ? NominalActualResultColorMaps : NominalActualColorMaps,
            ViewerGeometryStyle.Points,
            deviationAvailable ? ViewerColorMap.Deviation : ViewerColorMap.Solid);

    internal static string GetColorMapLabel(ViewerColorMap colorMap) => colorMap switch
    {
        ViewerColorMap.Source => "Source",
        ViewerColorMap.Solid => "Solid",
        ViewerColorMap.Grayscale => "Grayscale",
        ViewerColorMap.Height => "Height",
        ViewerColorMap.Thermal => "Thermal",
        ViewerColorMap.Deviation => "Deviation",
        ViewerColorMap.Rgb => "RGB",
        _ => throw new ArgumentOutOfRangeException(nameof(colorMap), colorMap, null)
    };

    private void Configure(
        ViewerDisplaySourceKind source,
        IReadOnlyList<ViewerGeometryStyle> geometryStyles,
        IReadOnlyList<ViewerColorMap> colorMaps,
        ViewerGeometryStyle defaultGeometryStyle,
        ViewerColorMap defaultColorMap)
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

        OnPropertyChanged(nameof(CanSelectGeometryStyle));
        OnPropertyChanged(nameof(CanSelectColorMap));
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
            RenderSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private ViewerColorMap DefaultColorMap() => activeSource switch
    {
        ViewerDisplaySourceKind.C3DHeightGrid => ViewerColorMap.Height,
        ViewerDisplaySourceKind.ImportedTriangleMesh => availableColorMapIds[0],
        ViewerDisplaySourceKind.PointCloud when availableColorMapIds.Contains(ViewerColorMap.Rgb) => ViewerColorMap.Rgb,
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
            _ => default
        };
        return label is "Source" or "Solid" or "Grayscale" or "Height" or "Thermal" or "Deviation" or "RGB";
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

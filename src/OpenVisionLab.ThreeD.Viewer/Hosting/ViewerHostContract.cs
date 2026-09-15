using System.Reflection;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

public static class ViewerHostContract
{
    public static string ApiVersion { get; } = typeof(ViewerHostContract).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "OpenVisionLabViewerHostApiVersion")
        ?.Value ?? "unknown";

    /// <summary>
    /// Checks the additive Host API compatibility policy. A provider may add
    /// members within the same major version, while an older provider or a
    /// changed major version is rejected.
    /// </summary>
    public static bool IsCompatibleVersion(string? requiredApiVersion, string? availableApiVersion)
    {
        if (!Version.TryParse(requiredApiVersion, out var required)
            || !Version.TryParse(availableApiVersion, out var available))
        {
            return false;
        }

        return required.Major == available.Major
            && available.Minor >= required.Minor;
    }
}

/// <summary>
/// Supplies recipe file paths to the Viewer command surface. The Viewer keeps
/// recipe validation, persistence, cancellation, and state transitions in its
/// existing workflows; a host only chooses a path or returns <see langword="null"/>
/// when the user cancels.
/// </summary>
public interface IViewerRecipeDialogHost
{
    string? SelectRecipeToOpen();

    string? SelectRecipeToSave(string defaultFileName);
}

/// <summary>
/// Resolves a Viewer-relative sample or recipe asset to an existing file path.
/// The resolver does not load data or mutate Viewer state; hosts may provide a
/// packaged-asset policy instead of using the default process-root search.
/// </summary>
public interface IViewerSamplePathResolver
{
    string? Resolve(string relativePath);
}

/// <summary>
/// Supplies Viewer translation and language-change notifications without
/// exposing WPF, file, or process-global state to an external host.
/// </summary>
public interface IViewerLocalizationProvider
{
    int Revision { get; }

    event EventHandler? LanguageChanged;

    string Resolve(string key, string korean, string english);

    string LocalizeRuntimeText(object? value, string? mode = null);
}

/// <summary>
/// WPF-neutral Nominal/Actual lifecycle states exposed to external Viewer
/// hosts. The concrete ViewModel enum remains internal to the Viewer state
/// owner; <see cref="Unknown"/> keeps the host contract safe if that owner
/// gains a new state before the contract is extended.
/// </summary>
public enum ViewerNominalActualState
{
    Unknown,
    NoInputs,
    InputsReady,
    PreviewStale,
    PreviewRunning,
    PreviewReady,
    Published,
    Failed
}

public sealed record ViewerHostState(
    bool C3DSampleVisible,
    string ActiveEntity,
    string SelectionMode,
    string PickCoordinate,
    string MeasurementSummary,
    string ResultSummary,
    string RecipeSummary,
    string ViewerStatus,
    string CoordinateFrameSummary)
{
    /// <summary>
    /// Safe initial value for WPF bindings before the Viewer control has
    /// created its concrete state coordinator.
    /// </summary>
    public static ViewerHostState Empty { get; } = new(
        C3DSampleVisible: false,
        ActiveEntity: string.Empty,
        SelectionMode: string.Empty,
        PickCoordinate: string.Empty,
        MeasurementSummary: string.Empty,
        ResultSummary: string.Empty,
        RecipeSummary: string.Empty,
        ViewerStatus: string.Empty,
        CoordinateFrameSummary: string.Empty);

    /// <summary>
    /// Reports mesh and point-cloud visibility without exposing the concrete
    /// Viewer ViewModel to a host.
    /// </summary>
    public bool GlbSampleVisible { get; init; }

    public bool LazSampleVisible { get; init; }

    public bool SelectionOverlayVisible { get; init; }

    /// <summary>
    /// Read-only selection text projected into the stable selection DTO. The
    /// scalar is kept on the outer snapshot so existing positional HostState
    /// construction remains source-compatible.
    /// </summary>
    public string SelectionSummary { get; init; } = string.Empty;

    /// <summary>
    /// Immutable Nominal/Actual lifecycle state for host-side progress and
    /// Preview readiness decisions.
    /// </summary>
    public ViewerNominalActualState NominalActualState { get; init; } = ViewerNominalActualState.NoInputs;

    /// <summary>
    /// Read-only Nominal/Actual display values for hosts that do not reference
    /// the concrete comparison ViewModel.
    /// </summary>
    public ViewerHostNominalActualDisplayState NominalActualDisplay { get; init; } = ViewerHostNominalActualDisplayState.Empty;

    /// <summary>
    /// Read-only C3D thickness evidence values for hosts that do not reference
    /// the concrete Viewer ViewModel. Teaching and Preview commands remain
    /// owned by the Viewer; this snapshot contains display state only.
    /// </summary>
    public ViewerHostThicknessEvidenceState ThicknessEvidence { get; init; } = ViewerHostThicknessEvidenceState.Empty;

    /// <summary>
    /// True when the Viewer has a validated or invalid Nominal/Actual input
    /// object configured. This is intentionally separate from lifecycle state
    /// so a host can preserve the existing input-present wait policy.
    /// </summary>
    public bool HasNominalActualInput { get; init; }

    /// <summary>
    /// Read-only height-profile display state for hosts that do not reference
    /// the concrete Viewer ViewModel.
    /// </summary>
    public ViewerHostProfileState Profile { get; init; } = ViewerHostProfileState.Empty;

    /// <summary>
    /// Read-only section-profile display state for hosts that do not reference
    /// the concrete Viewer ViewModel. This is distinct from <see cref="Profile"/>,
    /// which represents the P1-P2 height profile workflow.
    /// </summary>
    public ViewerHostSectionProfileState SectionProfile { get; init; } = ViewerHostSectionProfileState.Empty;

    /// <summary>
    /// Read-only imported point-cloud and mesh labels for hosts that do not
    /// reference the concrete Viewer ViewModel.
    /// </summary>
    public ViewerHostSourceSamplesState SourceSamples { get; init; } = ViewerHostSourceSamplesState.Empty;

    /// <summary>
    /// Read-only scene and transform summaries for hosts that do not reference
    /// the concrete Viewer ViewModel.
    /// </summary>
    public ViewerHostSceneDisplayState SceneDisplay { get; init; } = ViewerHostSceneDisplayState.Empty;

    /// <summary>
    /// Read-only source, preview, and published layer descriptors for hosts
    /// that do not reference the concrete Viewer ViewModel. The records are
    /// immutable; layer visibility is refreshed by the Viewer state snapshot.
    /// </summary>
    public IReadOnlyList<EntityLayer> EntityLayers { get; init; } = Array.Empty<EntityLayer>();

    /// <summary>
    /// Viewer-owned presentation state for hosts that do not reference the
    /// concrete WPF ViewModel. The snapshot is immutable; presentation
    /// changes are requested through the host operations below.
    /// </summary>
    public ViewerHostPresentationState Presentation { get; init; } = ViewerHostPresentationState.Empty;

    /// <summary>
    /// Read-only inspection state for host verification and smoke workflows.
    /// The identity tokens are process-local diagnostics; they let a host
    /// prove that a display or teaching action did not replace inspection
    /// state without exposing the concrete result objects.
    /// </summary>
    public ViewerHostInspectionState Inspection { get; init; } = ViewerHostInspectionState.Empty;

    /// <summary>
    /// Control-owned source identities. A source load updates this snapshot;
    /// the host still decides when to request another load.
    /// </summary>
    public ViewerHostSourceState Sources { get; init; } = ViewerHostSourceState.Empty;

    /// <summary>
    /// Stable selection DTO for host consumers that do not need the WPF
    /// ViewModel. The scalar properties above remain for Host API 1.0 source
    /// compatibility.
    /// </summary>
    public ViewerHostSelectionState Selection => new(
        ActiveEntity,
        SelectionMode,
        PickCoordinate,
        SelectionOverlayVisible)
    {
        Summary = SelectionSummary
    };
}

public sealed record ViewerHostNominalActualDisplayState(
    bool InputsReady,
    string EvidenceSummary)
{
    public string StateSummary { get; init; } = "Status: No inputs";

    public string DirectionSummary { get; init; } = "Direction: Actual to Nominal";

    public string CurrentDisplaySamplingSummary { get; init; } = "Current display: no comparison result";

    public string NextPreviewSamplingSummary { get; init; } = "Next Preview: Balanced | up to 60,000 points";

    public bool DisplaySamplingChangePending { get; init; }

    public double ProgressPercent { get; init; }

    public bool DistributionVisible { get; init; }

    public string DistributionSummary { get; init; } = "Deviation distribution: not available";

    public static ViewerHostNominalActualDisplayState Empty { get; } = new(
        InputsReady: false,
        EvidenceSummary: "No comparison evidence.");
}

public sealed record ViewerHostThicknessEvidenceState(
    bool Visible,
    string Summary,
    string Details,
    double Mean,
    double MinimumMeasured,
    double MaximumMeasured,
    double Range,
    int ValidSampleCount)
{
    public static ViewerHostThicknessEvidenceState Empty { get; } = new(
        Visible: false,
        Summary: "Thickness: teach one C3D ROI, then run Preview.",
        Details: "Declared scalar raw-height only; calibration is not inferred.",
        Mean: double.NaN,
        MinimumMeasured: double.NaN,
        MaximumMeasured: double.NaN,
        Range: double.NaN,
        ValidSampleCount: 0);
}

public sealed record ViewerHostSourceState(
    string? CurrentC3DSourcePath,
    string? CurrentViewerOnlySourcePath,
    string? CurrentViewerOnlySourceFormat)
{
    public static ViewerHostSourceState Empty { get; } = new(null, null, null);
}

public sealed record ViewerHostSourceSamplesState(
    string PointCloudName,
    string PointCloudSummary,
    string MeshName,
    string MeshSummary)
{
    public static ViewerHostSourceSamplesState Empty { get; } = new(
        PointCloudName: "Public LAZ/LAS Point Cloud",
        PointCloudSummary: "LAZ/LAS metadata hidden",
        MeshName: "Public GLB Box",
        MeshSummary: "Imported mesh hidden");
}

public sealed record ViewerHostSceneDisplayState(
    string SceneContractSummary,
    string TransformSummary,
    string AlignmentSummary,
    string CoordinateMappingSummary,
    string C3DSamplePointCount,
    string LazSamplingSummary)
{
    public static ViewerHostSceneDisplayState Empty { get; } = new(
        SceneContractSummary: "(pending)",
        TransformSummary: "Transform: Identity | T(0.000, 0.000, 0.000) | R(0.0, 0.0, 0.0) | S 1.000",
        AlignmentSummary: "Alignment: Not aligned | source frame",
        CoordinateMappingSummary: "Mapping: source = aligned | raw-height retained",
        C3DSamplePointCount: "(not loaded)",
        LazSamplingSummary: "LAZ/LAS sampling: not loaded");
}

public sealed record ViewerHostSelectionState(
    string ActiveEntity,
    string SelectionMode,
    string PickCoordinate,
    bool OverlayVisible)
{
    public string Summary { get; init; } = string.Empty;
}

public sealed record ViewerHostProfileState(
    bool Visible,
    string Summary,
    string EndpointSummary,
    string Range,
    string PathData,
    int ValidSampleCount,
    int MissingSampleCount,
    bool LinkedCursorVisible,
    double LinkedCursorX,
    double LinkedCursorY,
    double LinkedCursorMarkerLeft,
    double LinkedCursorMarkerTop,
    string LinkedCursorSummary)
{
    public static ViewerHostProfileState Empty { get; } = new(
        Visible: false,
        Summary: "Profile: choose P1 and P2 on the C3D height grid.",
        EndpointSummary: "P1: not set | P2: not set",
        Range: "Height range: pending",
        PathData: "M 0,30 L 240,30",
        ValidSampleCount: 0,
        MissingSampleCount: 0,
        LinkedCursorVisible: false,
        LinkedCursorX: 0.0,
        LinkedCursorY: 0.0,
        LinkedCursorMarkerLeft: 0.0,
        LinkedCursorMarkerTop: 0.0,
        LinkedCursorSummary: "Linked cursor: unavailable until P1–P2 trace is ready.");
}

public sealed record ViewerHostSectionProfileState(
    bool Visible,
    int SampleCount,
    string Summary,
    string Range,
    string PathData)
{
    public static ViewerHostSectionProfileState Empty { get; } = new(
        Visible: false,
        SampleCount: 0,
        Summary: "Profile: not loaded",
        Range: "Range: not loaded",
        PathData: "M 0,30 L 240,30");
}

public sealed record ViewerHostPresentationState(
    bool HudDetailsVisible,
    bool C3DHeightDistributionVisible,
    string C3DHeightDistributionSourceSha256,
    double C3DHeightColorMinimumRaw,
    double C3DHeightColorMaximumRaw,
    bool C3DHeightColorRangeAuto,
    int C3DHeightColorRangeRevision,
    string C3DHeightColorRangeSummary)
{
    public static ViewerHostPresentationState Empty { get; } = new(
        HudDetailsVisible: false,
        C3DHeightDistributionVisible: false,
        C3DHeightDistributionSourceSha256: "not loaded",
        C3DHeightColorMinimumRaw: double.NaN,
        C3DHeightColorMaximumRaw: double.NaN,
        C3DHeightColorRangeAuto: true,
        C3DHeightColorRangeRevision: 0,
        C3DHeightColorRangeSummary: "Height color range: not loaded");

    /// <summary>
    /// Display choices are copied into the host snapshot so WPF consumers can
    /// bind without reaching into the concrete Viewer ViewModel.
    /// </summary>
    public IReadOnlyList<string> AvailableColorMaps { get; init; } = [];

    public string SelectedColorMap { get; init; } = string.Empty;

    public bool CanSelectColorMap { get; init; }

    public IReadOnlyList<ViewerDiagnosticChannelOption> DiagnosticChannelOptions { get; init; } = [];

    public ViewerDiagnosticChannelOption? SelectedDiagnosticChannel { get; init; }

    public bool CanSelectDiagnosticChannel { get; init; }

    public string DiagnosticChannelSummary { get; init; } = string.Empty;

    public bool ResultOverlayVisible { get; init; }

    public bool MeasurementVisible { get; init; }
}

public sealed record ViewerHostInspectionState(
    ResultStatus PreviewStatus,
    int PreviewReferenceToken,
    int PublishedResultCount,
    int PublishedResultReferenceToken,
    ViewerProjectionMode ProjectionMode)
{
    public static ViewerHostInspectionState Empty { get; } = new(
        PreviewStatus: ResultStatus.NotRun,
        PreviewReferenceToken: 0,
        PublishedResultCount: 0,
        PublishedResultReferenceToken: 0,
        ProjectionMode: ViewerProjectionMode.Perspective);

    public bool IsTopOrthographicView => ProjectionMode == ViewerProjectionMode.TopOrthographic;

    public bool IsPerspectiveView => ProjectionMode == ViewerProjectionMode.Perspective;
}

public sealed class ViewerHostStateChangedEventArgs(
    ViewerHostState state,
    string? propertyName) : EventArgs
{
    public ViewerHostState State { get; } = state;

    public string? PropertyName { get; } = propertyName;
}

/// <remarks>
/// Except for <see cref="HostApiVersion"/> and the concrete control's
/// <c>Dispose()</c>, members are called on the Viewer control's WPF
/// <see cref="System.Windows.Threading.Dispatcher"/> thread. A call from a
/// different thread is rejected with <see cref="InvalidOperationException"/>.
/// <see cref="HostStateChanged"/> is raised on that Dispatcher thread and
/// consumers must unsubscribe before closing the Dispatcher. Asynchronous
/// source-load tasks report completion only after the current operation has
/// been applied; external cancellation propagates as
/// <see cref="OperationCanceledException"/>, while a superseded or disposed
/// operation does not apply stale state. The concrete
/// <see cref="OpenVisionLab.ThreeD.Viewer.OpenVisionThreeDViewerControl"/>
/// remains the lifetime and Dispose owner; the interface intentionally does
/// not inherit <see cref="IDisposable"/>.
/// </remarks>
public interface IOpenVisionThreeDViewerHost
{
    string HostApiVersion { get; }

    ViewerHostState HostState { get; }

    event EventHandler<ViewerHostStateChangedEventArgs>? HostStateChanged;

    /// <summary>
    /// Loads a C3D source for recipe teaching without configuring, previewing,
    /// publishing, or running an inspection.
    /// </summary>
    bool LoadC3DSource(string path);

    Task<bool> LoadC3DSourceAsync(
        string path,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null);

    /// <summary>
    /// Loads a mesh or point cloud for display while retaining the current
    /// recipe source.
    /// </summary>
    Task<bool> LoadViewerOnlySourceAsync(
        string path,
        CancellationToken cancellationToken = default,
        IProgress<double>? progress = null);

    bool TryGetCurrentC3DSourceBinding(
        string path,
        out ToolRecipeSelectionSourceBinding binding);

    ViewerCameraState CaptureCameraState();

    bool TryApplyCameraState(ViewerCameraState state);

    bool TrySetSelectionMode(string selectionMode);

    bool TrySetSelectionOverlayVisible(bool visible);

    bool TrySetHudDetailsVisible(bool visible);

    bool TrySetC3DSampleVisible(bool visible);

    bool TrySetSelectedColorMap(string colorMap);

    bool TrySetSelectedDiagnosticChannel(ViewerDiagnosticChannelOption? channel);

    bool TrySetResultOverlayVisible(bool visible);

    bool TrySetMeasurementVisible(bool visible);

    bool TrySetC3DHeightColorMinimumRaw(double value);

    bool TrySetC3DHeightColorMaximumRaw(double value);

    bool TryShiftC3DHeightColorMinimum(int direction);

    bool TryShiftC3DHeightColorMaximum(int direction);

    bool TryResetC3DHeightColorRange();

    bool TryApplyLinkedC3DHeightColorRange(double minimum, double maximum);

    void FitAll();

    void FitSelection();

    void ResetView();

    bool SaveRecipe(string path);
}

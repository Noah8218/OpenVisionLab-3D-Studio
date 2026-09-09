using System.Reflection;

namespace OpenVisionLab.ThreeD.Viewer.Hosting;

public static class ViewerHostContract
{
    public static string ApiVersion { get; } = typeof(ViewerHostContract).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "OpenVisionLabViewerHostApiVersion")
        ?.Value ?? "unknown";
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
    /// Read-only Nominal/Actual display state for hosts that do not reference
    /// the concrete comparison ViewModel.
    /// </summary>
    public ViewerHostNominalActualDisplayState NominalActualDisplay { get; init; } = ViewerHostNominalActualDisplayState.Empty;

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

public sealed class ViewerHostStateChangedEventArgs(
    ViewerHostState state,
    string? propertyName) : EventArgs
{
    public ViewerHostState State { get; } = state;

    public string? PropertyName { get; } = propertyName;
}

public interface IOpenVisionThreeDViewerHost
{
    string HostApiVersion { get; }

    ViewerHostState HostState { get; }

    event EventHandler<ViewerHostStateChangedEventArgs>? HostStateChanged;

    void FitAll();

    void FitSelection();

    void ResetView();

    bool SaveRecipe(string path);
}

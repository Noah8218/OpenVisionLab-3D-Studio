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
    string CoordinateFrameSummary);

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

using System.IO;
using OpenVisionLab.ThreeD.Viewer.Hosting;

namespace OpenVisionLab.ThreeD.Viewer.BinaryHost;

/// <summary>
/// Observes the public Viewer host contract and writes the stable consumer
/// report. It owns report policy, not the WPF Window or concrete control.
/// </summary>
internal sealed class ViewerConsumerHostApiReportCoordinator
{
    private readonly string reportPath;
    private readonly IOpenVisionThreeDViewerHost viewer;
    private int hostEventCount;
    private string? lastHostProperty;

    public ViewerConsumerHostApiReportCoordinator(
        string reportPath,
        IOpenVisionThreeDViewerHost viewer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        ArgumentNullException.ThrowIfNull(viewer);
        this.reportPath = Path.GetFullPath(reportPath);
        this.viewer = viewer;
    }

    public void ObserveStateChanged(ViewerHostStateChangedEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        hostEventCount++;
        lastHostProperty = eventArgs.PropertyName;
    }

    public void WriteReport(bool recipeSaved, string? recipePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        var state = viewer.HostState;
        File.WriteAllLines(
            reportPath,
            [
                $"HostApi|version={viewer.HostApiVersion}",
                $"HostState|activeEntity={state.ActiveEntity}|selectionMode={state.SelectionMode}|viewerStatus={state.ViewerStatus}",
                $"HostEvents|count={hostEventCount}|lastProperty={lastHostProperty ?? "(none)"}",
                "HostLifecycle|concreteDisposable=True|disposedAfterRun=True",
                $"HostCommands|invoked=ResetView,FitAll,FitSelection|saveRecipe={recipeSaved}|recipePath={recipePath ?? "(not requested)"}"
            ]);
    }
}

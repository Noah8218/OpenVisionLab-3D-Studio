using System.Windows.Threading;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;

namespace OpenVisionLab.ThreeD.Shell.Verification.Smoke;

/// <summary>
/// Owns command-line Viewer workspace presentation/layout Smoke orchestration.
/// MainWindow retains scenario selection and failure/shutdown policy; the
/// Workbench remains the owner of ViewerWorkspace state and transitions; the
/// View supplies its existing explicit smoke adapters.
/// </summary>
internal sealed class ShellViewerWorkspaceSmoke
{
    private readonly ToolRecipeWorkbenchView workbenchView;
    private readonly Dispatcher dispatcher;

    public ShellViewerWorkspaceSmoke(
        ToolRecipeWorkbenchView workbenchView,
        Dispatcher dispatcher)
    {
        this.workbenchView = workbenchView ?? throw new ArgumentNullException(nameof(workbenchView));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<ShellViewerWorkspaceSmokeResult> RunAsync(
        bool presentationSmoke,
        string? layoutSmoke,
        string? screenshotQualityReportPath)
    {
        if (presentationSmoke)
        {
            if (!workbenchView.ConfigureViewerWorkspacePresentationForSmoke())
            {
                return Failure(
                    "Viewer workspace presentation smoke could not activate two real linked Viewers.");
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(600);
            if (!workbenchView.VerifyViewerWorkspaceCameraLinkForSmoke(
                    out var cameraLinkSmokeSummary))
            {
                ShellSmokeArtifacts.WriteTextReport(
                    screenshotQualityReportPath,
                    [cameraLinkSmokeSummary]);
                return Failure(
                    "Viewer workspace camera-link propagation smoke failed.");
            }

            return Success(cameraLinkSmokeSummary);
        }

        if (layoutSmoke is not null)
        {
            if (!workbenchView.ConfigureViewerWorkspaceLayoutForSmoke(layoutSmoke))
            {
                return Failure(
                    $"Viewer workspace layout smoke could not activate '{layoutSmoke}'.");
            }

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);
            await Task.Delay(600);
        }

        return Success(null);
    }

    private static ShellViewerWorkspaceSmokeResult Success(
        string? cameraLinkSummary) =>
        new(null, cameraLinkSummary);

    private static ShellViewerWorkspaceSmokeResult Failure(string message) =>
        new(message, null);
}

internal sealed record ShellViewerWorkspaceSmokeResult(
    string? Failure,
    string? CameraLinkSummary)
{
    public bool Succeeded => Failure is null;
}

using System.Threading;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Centralizes retirement of a Workbench cancellation source. Execution owners
/// retain source creation, replacement, and operation state; this helper owns
/// only the shared cancel-then-dispose policy.
/// </summary>
internal static class ToolWorkbenchCancellationSourceLifetime
{
    public static void CancelAndDispose(CancellationTokenSource? cancellation)
    {
        if (cancellation is null)
        {
            return;
        }

        try
        {
            cancellation.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A concurrent owner disposal or replacement already released the token source.
        }

        cancellation.Dispose();
    }
}

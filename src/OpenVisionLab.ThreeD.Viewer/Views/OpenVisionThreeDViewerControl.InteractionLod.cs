using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private static readonly TimeSpan InteractionLodRestoreDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan InteractionLodStepDelay = TimeSpan.FromMilliseconds(90);

    private DispatcherTimer? interactionLodRestoreTimer;
    private C3DWireframeLodLevel interactionWireframeLodLevel = C3DWireframeLodLevel.Precise;
    private int interactionLodActivationCount;
    private int interactionLodMediumTransitionCount;
    private int interactionLodRestoreCount;
    private int interactionC3DDisplayListBuildCount;
    private int c3dSourceApplyCount;
    private bool smokeInteractionLodRequested;

    private bool interactionWireframeLodActive =>
        interactionWireframeLodLevel != C3DWireframeLodLevel.Precise;

    private bool CanUseInteractionWireframeLod =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.Display.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.Wireframe
        && c3dRenderProxyCache.Current is { CoarseInteractionGridEdgeCount: > 0 } renderProxy
        && renderProxy.CoarseInteractionGridEdgeCount < renderProxy.InteractionGridEdgeCount
        && renderProxy.InteractionGridEdgeCount < renderProxy.GridEdgeCount;

    private void BeginInteractionWireframeLod()
    {
        if (!CanUseInteractionWireframeLod)
        {
            return;
        }

        interactionLodRestoreTimer?.Stop();
        if (interactionWireframeLodActive)
        {
            interactionWireframeLodLevel = C3DWireframeLodLevel.Coarse;
            return;
        }

        interactionWireframeLodLevel = C3DWireframeLodLevel.Coarse;
        interactionLodActivationCount++;
    }

    private void ScheduleInteractionWireframeLodRestore()
    {
        if (!interactionWireframeLodActive)
        {
            return;
        }

        interactionLodRestoreTimer ??= CreateInteractionLodRestoreTimer();
        interactionLodRestoreTimer.Stop();
        interactionLodRestoreTimer.Start();
    }

    private DispatcherTimer CreateInteractionLodRestoreTimer()
    {
        var timer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = InteractionLodStepDelay
        };
        timer.Tick += OnInteractionLodRestoreTimerTick;
        return timer;
    }

    private void OnInteractionLodRestoreTimerTick(object? sender, EventArgs args)
    {
        if (IsDisposed)
        {
            return;
        }

        if (interactionLodRestoreTimer is not { } timer)
        {
            return;
        }

        timer.Stop();
        if (isOrbiting || isPanning || profileDraggedEndpoint != 0)
        {
            timer.Start();
            return;
        }

        if (interactionWireframeLodLevel == C3DWireframeLodLevel.Coarse)
        {
            interactionWireframeLodLevel = C3DWireframeLodLevel.Medium;
            interactionLodMediumTransitionCount++;
            timer.Start();
            return;
        }

        RestoreInteractionWireframeLod();
    }

    private void RestoreInteractionWireframeLod()
    {
        interactionLodRestoreTimer?.Stop();
        if (!interactionWireframeLodActive)
        {
            return;
        }

        interactionWireframeLodLevel = C3DWireframeLodLevel.Precise;
        interactionLodRestoreCount++;
    }

    private void ResetInteractionWireframeLodForSourceChange(bool sourceApplied)
    {
        interactionLodRestoreTimer?.Stop();
        interactionWireframeLodLevel = C3DWireframeLodLevel.Precise;
        c3dInteractionDisplayListKey = null;
        if (sourceApplied)
        {
            c3dSourceApplyCount++;
        }
    }

    private void StopInteractionWireframeLod()
    {
        interactionLodRestoreTimer?.Stop();
        interactionWireframeLodLevel = C3DWireframeLodLevel.Precise;
    }

    private void DisposeInteractionWireframeLod()
    {
        if (interactionLodRestoreTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnInteractionLodRestoreTimerTick;
            interactionLodRestoreTimer = null;
        }

        interactionWireframeLodLevel = C3DWireframeLodLevel.Precise;
    }
}

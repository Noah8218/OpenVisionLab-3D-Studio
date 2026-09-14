using System.Windows.Threading;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.Rendering;

namespace OpenVisionLab.ThreeD.Viewer;

public sealed partial class OpenVisionThreeDViewerControl
{
    private static readonly TimeSpan InteractionLodRestoreDelay = TimeSpan.FromMilliseconds(180);
    private static readonly TimeSpan InteractionLodStepDelay = TimeSpan.FromMilliseconds(90);

    private DispatcherTimer? interactionLodRestoreTimer;
    private C3DWireframeLodLevel interactionWireframeLodLevel => interactionLodState.Level;
    private int interactionLodActivationCount => interactionLodState.ActivationCount;
    private int interactionLodMediumTransitionCount => interactionLodState.MediumTransitionCount;
    private int interactionLodRestoreCount => interactionLodState.RestoreCount;
    private int interactionC3DDisplayListBuildCount;
    private int c3dSourceApplyCount => interactionLodState.SourceApplyCount;

    private bool interactionWireframeLodActive =>
        interactionLodState.IsActive;

    private bool CanUseInteractionWireframeLod =>
        c3dSample is not null
        && viewModel.C3DSampleVisible
        && viewModel.Display.EffectiveSettings.GeometryStyle == ViewerGeometryStyle.Wireframe
        && c3dRenderProxyCache.Current is { CoarseInteractionGridEdgeCount: > 0 } renderProxy
        && renderProxy.CoarseInteractionGridEdgeCount < renderProxy.InteractionGridEdgeCount
        && renderProxy.InteractionGridEdgeCount < renderProxy.GridEdgeCount;

    private void BeginInteractionWireframeLod()
    {
        if (!interactionLodState.TryBegin(CanUseInteractionWireframeLod))
        {
            return;
        }

        interactionLodRestoreTimer?.Stop();
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

        if (interactionLodState.AdvanceAfterInteraction())
        {
            timer.Start();
            return;
        }

        RestoreInteractionWireframeLod();
    }

    private void RestoreInteractionWireframeLod()
    {
        interactionLodRestoreTimer?.Stop();
        interactionLodState.Restore();
    }

    private void ResetInteractionWireframeLodForSourceChange(bool sourceApplied)
    {
        interactionLodRestoreTimer?.Stop();
        interactionLodState.ResetForSourceChange(sourceApplied);
        c3dInteractionDisplayListKey = null;
    }

    private void StopInteractionWireframeLod()
    {
        interactionLodRestoreTimer?.Stop();
        interactionLodState.Stop();
    }

    private void DisposeInteractionWireframeLod()
    {
        if (interactionLodRestoreTimer is { } timer)
        {
            timer.Stop();
            timer.Tick -= OnInteractionLodRestoreTimerTick;
            interactionLodRestoreTimer = null;
        }

        interactionLodState.Stop();
    }
}

using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.Coordination;

/// <summary>
/// Owns the cross-view state links for the Workbench Viewer workspace.
/// Layout and popup presentation stay with <see cref="ViewerWorkspaceView"/>;
/// this type only synchronizes the shared height range, hover cursor, and
/// linked 3D camera between the existing state owners.
/// </summary>
internal sealed class ViewerWorkspaceLinkCoordinator : IDisposable
{
    private ToolWorkbenchViewModel? workbench;
    private OpenVisionThreeDViewerControl? mainViewer;
    private OpenVisionThreeDViewerControl? auxiliaryViewer;
    private bool synchronizingLinkedHeightDisplayRange;
    private bool synchronizingLinkedCamera;
    private int disposalState;

    public void SetWorkbench(ToolWorkbenchViewModel? value)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        if (ReferenceEquals(workbench, value))
        {
            Refresh();
            return;
        }

        if (workbench is not null)
        {
            workbench.SharedHeightCursor.PropertyChanged -= OnSharedHeightCursorChanged;
            workbench.HeightImageViewer.PropertyChanged -= OnHeightImageViewerPropertyChanged;
        }

        workbench = value;
        if (workbench is not null)
        {
            workbench.SharedHeightCursor.PropertyChanged += OnSharedHeightCursorChanged;
            workbench.HeightImageViewer.PropertyChanged += OnHeightImageViewerPropertyChanged;
        }

        Refresh();
    }

    public void SetMainViewer(OpenVisionThreeDViewerControl? value)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        if (ReferenceEquals(mainViewer, value))
        {
            Refresh();
            return;
        }

        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged -= OnMainViewerC3DGridHoverChanged;
            mainViewer.CameraChanged -= OnMainViewerCameraChanged;
            mainViewer.HostStateChanged -= OnMainViewerHostStateChanged;
            mainViewer.SetLinkedHeightCursor(null);
        }

        mainViewer = value;
        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged += OnMainViewerC3DGridHoverChanged;
            mainViewer.CameraChanged += OnMainViewerCameraChanged;
            mainViewer.HostStateChanged += OnMainViewerHostStateChanged;
        }

        Refresh();
    }

    public void SetAuxiliaryViewer(OpenVisionThreeDViewerControl? value)
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        auxiliaryViewer = value;
    }

    public void Refresh()
    {
        if (Volatile.Read(ref disposalState) != 0)
        {
            return;
        }

        ApplySharedHeightCursorToMainViewer();
        SynchronizeLinkedHeightDisplayRangeFromMainViewer();
        SynchronizeLinkedCamera();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposalState, 1) != 0)
        {
            return;
        }

        if (workbench is not null)
        {
            workbench.SharedHeightCursor.PropertyChanged -= OnSharedHeightCursorChanged;
            workbench.HeightImageViewer.PropertyChanged -= OnHeightImageViewerPropertyChanged;
        }

        if (mainViewer is not null)
        {
            mainViewer.C3DGridHoverChanged -= OnMainViewerC3DGridHoverChanged;
            mainViewer.CameraChanged -= OnMainViewerCameraChanged;
            mainViewer.HostStateChanged -= OnMainViewerHostStateChanged;
            mainViewer.SetLinkedHeightCursor(null);
        }

        workbench = null;
        mainViewer = null;
        auxiliaryViewer = null;
    }

    private void OnMainViewerHostStateChanged(
        object? sender,
        ViewerHostStateChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ViewerHostState.Presentation)
            or nameof(ViewerHostState.C3DSampleVisible))
        {
            SynchronizeLinkedHeightDisplayRangeFromMainViewer();
        }
    }

    private void OnHeightImageViewerPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(HeightImageViewerViewModel.Frame))
        {
            SynchronizeLinkedHeightDisplayRangeFromMainViewer();
        }
        else if (args.PropertyName == nameof(HeightImageViewerViewModel.DisplayRangeRevision))
        {
            SynchronizeLinkedHeightDisplayRangeToMainViewer();
        }
    }

    private void OnMainViewerCameraChanged(object? sender, EventArgs args) =>
        SynchronizeLinkedCamera();

    private void SynchronizeLinkedHeightDisplayRangeFromMainViewer()
    {
        var viewerHost = mainViewer;
        var presentation = viewerHost?.HostState.Presentation;
        var heightImage = workbench?.HeightImageViewer;
        if (synchronizingLinkedHeightDisplayRange
            || viewerHost is null
            || presentation is null
            || heightImage?.Frame is null
            || !HasMatchingHeightSource(presentation, heightImage))
        {
            return;
        }

        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            if (presentation.C3DHeightColorRangeAuto)
            {
                heightImage.UseAutoRange();
            }
            else
            {
                heightImage.TryApplyLinkedDisplayRange(
                    presentation.C3DHeightColorMinimumRaw,
                    presentation.C3DHeightColorMaximumRaw);
            }
        }
        finally
        {
            synchronizingLinkedHeightDisplayRange = false;
        }
    }

    private void SynchronizeLinkedHeightDisplayRangeToMainViewer()
    {
        var viewerHost = mainViewer;
        var presentation = viewerHost?.HostState.Presentation;
        var heightImage = workbench?.HeightImageViewer;
        if (synchronizingLinkedHeightDisplayRange
            || viewerHost is null
            || presentation is null
            || heightImage?.DisplayFrame is not { } displayFrame
            || !HasMatchingHeightSource(presentation, heightImage))
        {
            return;
        }

        synchronizingLinkedHeightDisplayRange = true;
        try
        {
            if (heightImage.IsAutoRange)
            {
                viewerHost.TryResetC3DHeightColorRange();
            }
            else
            {
                viewerHost.TryApplyLinkedC3DHeightColorRange(
                    displayFrame.Minimum,
                    displayFrame.Maximum);
            }
        }
        finally
        {
            synchronizingLinkedHeightDisplayRange = false;
        }
    }

    private static bool HasMatchingHeightSource(
        ViewerHostPresentationState presentation,
        HeightImageViewerViewModel heightImage) =>
        heightImage.Frame is { } frame
        && string.Equals(
            presentation.C3DHeightDistributionSourceSha256,
            frame.SourceContentSha256,
            StringComparison.OrdinalIgnoreCase);

    private void OnMainViewerC3DGridHoverChanged(
        object? sender,
        C3DGridHoverChangedEventArgs args)
    {
        if (workbench is null)
        {
            return;
        }

        if (args.Cursor is not { } cursor)
        {
            workbench.SharedHeightCursor.Clear(
                SharedHeightCursorOrigin.ThreeDViewer);
            return;
        }

        workbench.SharedHeightCursor.Update(
            SharedHeightCursorOrigin.ThreeDViewer,
            cursor.SourceContentSha256,
            cursor.Row,
            cursor.Column,
            cursor.RawHeight,
            cursor.IsValid);
    }

    private void OnSharedHeightCursorChanged(
        object? sender,
        PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(SharedHeightCursorSession.Cursor)
            or nameof(SharedHeightCursorSession.HasCursor)
            or nameof(SharedHeightCursorSession.Revision))
        {
            ApplySharedHeightCursorToMainViewer();
        }
    }

    private void ApplySharedHeightCursorToMainViewer()
    {
        if (mainViewer is null)
        {
            return;
        }

        mainViewer.SetLinkedHeightCursor(
            workbench?.SharedHeightCursor.Cursor is { } cursor
                ? new C3DGridCursor(
                    cursor.Origin == SharedHeightCursorOrigin.ThreeDViewer
                        ? C3DGridCursorOrigin.ThreeDViewer
                        : C3DGridCursorOrigin.HeightImage,
                    cursor.SourceContentSha256,
                    cursor.Row,
                    cursor.Column,
                    cursor.RawHeight,
                    cursor.IsValid)
                : null);
    }

    private void SynchronizeLinkedCamera()
    {
        var currentWorkbench = workbench;
        if (currentWorkbench?.ViewerWorkspace.IsCameraLinked != true)
        {
            return;
        }

        var candidate = currentWorkbench.GetViewerWorkspaceCandidate(
            currentWorkbench.ViewerWorkspace.AuxiliaryContentId);
        var currentMainViewer = mainViewer;
        var currentAuxiliaryViewer = auxiliaryViewer;
        if (candidate is null
            || candidate.Kind != ViewerWorkspaceCandidateKind.ThreeDArtifact
            || currentMainViewer is null
            || currentAuxiliaryViewer is null
            || !File.Exists(candidate.SourcePath))
        {
            currentWorkbench.ViewerWorkspace.SetCameraLinked(false);
            return;
        }

        if (synchronizingLinkedCamera)
        {
            return;
        }

        synchronizingLinkedCamera = true;
        try
        {
            if (!currentAuxiliaryViewer.TryApplyCameraState(currentMainViewer.CaptureCameraState()))
            {
                currentWorkbench.ViewerWorkspace.SetCameraLinked(false);
            }
        }
        finally
        {
            synchronizingLinkedCamera = false;
        }
    }
}

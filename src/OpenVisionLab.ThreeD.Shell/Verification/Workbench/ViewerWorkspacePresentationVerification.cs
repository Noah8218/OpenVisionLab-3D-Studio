using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Shell.Views.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell;

internal static class ViewerWorkspacePresentationVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D independent Viewer presentation verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Boundary|Presentation state is per real Viewer instance; camera link is session-only Main -> Auxiliary; no recipe or inspection execution."
        };
        var passed = 0;

        void Check(string name, bool condition, string detail)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{name}: {detail}");
            }

            passed++;
            lines.Add($"PASS | {name} | {detail}");
        }

        try
        {
            var session = new ViewerWorkspaceSession();
            Check(
                "camera link starts disabled",
                !session.IsCameraLinked,
                $"linked={session.IsCameraLinked}");
            Check(
                "split session retains two explicit Viewer slots",
                session.TrySetLayout(ViewerWorkspaceLayout.SplitVertical, ["main", "aux"], "aux")
                && session.MainContentId.Length == 0
                && session.AuxiliaryContentId == "aux",
                $"layout={session.Layout};main={session.MainContentId};aux={session.AuxiliaryContentId}");
            session.SetCameraLinked(true);
            Check(
                "link is explicit and session-owned",
                session.IsCameraLinked,
                $"linked={session.IsCameraLinked}");
            session.SetLayout(ViewerWorkspaceLayout.Single);
            Check(
                "single layout fail-closes the link",
                !session.IsCameraLinked,
                $"layout={session.Layout};linked={session.IsCameraLinked}");
            session.SetCameraLinked(true);
            session.ClearAuxiliaryContent();
            Check(
                "clearing auxiliary content unlinks without selecting a replacement",
                !session.IsCameraLinked
                && session.AuxiliaryContentId.Length == 0
                && session.IsAuxiliaryContentExplicitlyCleared,
                $"aux={session.AuxiliaryContentId};cleared={session.IsAuxiliaryContentExplicitlyCleared};linked={session.IsCameraLinked}");

            using var mainViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var auxiliaryViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            using var disposedWorkspaceView = new ViewerWorkspaceView();
            disposedWorkspaceView.Dispose();
            disposedWorkspaceView.MainViewerContent = mainViewer;
            Check(
                "disposed workspace ignores late main Viewer content callback",
                !disposedWorkspaceView.HasAttachedMainViewer,
                $"attached={disposedWorkspaceView.HasAttachedMainViewer}|content={disposedWorkspaceView.MainViewerContent is not null}");
            var mainInspectionBeforePresentation = mainViewer.HostState.Inspection;
            var auxiliaryInspectionBeforePresentation = auxiliaryViewer.HostState.Inspection;
            var presentationApplied =
                mainViewer.TrySetSelectedColorMap("Solid")
                && auxiliaryViewer.TrySetSelectedColorMap("Height")
                && mainViewer.TrySetSelectionOverlayVisible(false)
                && auxiliaryViewer.TrySetSelectionOverlayVisible(true)
                && mainViewer.TrySetResultOverlayVisible(true)
                && auxiliaryViewer.TrySetResultOverlayVisible(false)
                && mainViewer.TrySetMeasurementVisible(false)
                && auxiliaryViewer.TrySetMeasurementVisible(true);
            var mainPresentation = mainViewer.HostState.Presentation;
            var auxiliaryPresentation = auxiliaryViewer.HostState.Presentation;
            Check(
                "two real Viewer instances keep independent palette and overlay state",
                presentationApplied
                && mainPresentation.SelectedColorMap == "Solid"
                && auxiliaryPresentation.SelectedColorMap == "Height"
                && !mainViewer.HostState.SelectionOverlayVisible
                && auxiliaryViewer.HostState.SelectionOverlayVisible
                && mainPresentation.ResultOverlayVisible
                && !auxiliaryPresentation.ResultOverlayVisible
                && !mainPresentation.MeasurementVisible
                && auxiliaryPresentation.MeasurementVisible,
                $"main={mainPresentation.SelectedColorMap};aux={auxiliaryPresentation.SelectedColorMap}");

            var mainCamera = new ViewerCameraState(
                18.0,
                36.0,
                7.25,
                1.0,
                2.0,
                3.0,
                ViewerProjectionMode.Perspective,
                10.0);
            var beforeAuxiliaryCamera = auxiliaryViewer.CaptureCameraState();
            Check(
                "camera state remains independent before an explicit link copy",
                mainViewer.TryApplyCameraState(mainCamera)
                && auxiliaryViewer.CaptureCameraState() == beforeAuxiliaryCamera,
                $"main={mainViewer.CaptureCameraState()}|aux={beforeAuxiliaryCamera}");
            Check(
                "explicit camera copy synchronizes Main to Auxiliary",
                auxiliaryViewer.TryApplyCameraState(mainViewer.CaptureCameraState())
                && auxiliaryViewer.CaptureCameraState() == mainViewer.CaptureCameraState(),
                $"main={mainViewer.CaptureCameraState()}|aux={auxiliaryViewer.CaptureCameraState()}");

            var beforeInvalidCamera = auxiliaryViewer.CaptureCameraState();
            Check(
                "invalid camera copy fails closed",
                !auxiliaryViewer.TryApplyCameraState(
                    mainCamera with { OrthographicHeight = double.NaN })
                && auxiliaryViewer.CaptureCameraState() == beforeInvalidCamera,
                auxiliaryViewer.CaptureCameraState().ToString());

            Check(
                "display and camera actions do not execute inspection",
                mainViewer.HostState.Inspection.PublishedResultCount == mainInspectionBeforePresentation.PublishedResultCount
                && auxiliaryViewer.HostState.Inspection.PublishedResultCount == auxiliaryInspectionBeforePresentation.PublishedResultCount
                && mainViewer.HostState.Inspection.PublishedResultCount == 0
                && auxiliaryViewer.HostState.Inspection.PublishedResultCount == 0,
                $"mainPreview={mainViewer.HostState.Inspection.PreviewStatus};auxPreview={auxiliaryViewer.HostState.Inspection.PreviewStatus};mainPublished={mainViewer.HostState.Inspection.PublishedResultCount};auxPublished={auxiliaryViewer.HostState.Inspection.PublishedResultCount}");

            summary = $"Viewer workspace presentation verification: Pass ({passed} checks)";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return true;
        }
        catch (Exception exception)
        {
            summary = $"Viewer workspace presentation verification: Fail after {passed} checks: {exception.Message}";
            lines.Add(summary);
            WriteReport(reportPath, lines);
            return false;
        }
    }

    private static void WriteReport(string reportPath, IEnumerable<string> lines)
    {
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines);
    }
}

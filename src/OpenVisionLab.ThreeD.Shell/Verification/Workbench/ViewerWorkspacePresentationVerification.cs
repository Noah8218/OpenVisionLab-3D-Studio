using System.IO;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

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

            var mainViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            var auxiliaryViewer = new OpenVisionThreeDViewerControl(loadDefaultSamples: false);
            mainViewer.ViewModel.UseC3DSmokeScene();
            auxiliaryViewer.ViewModel.UseC3DSmokeScene();
            var mainPublishedBeforePresentation = mainViewer.ViewModel.ResultEntities.Count;
            var auxiliaryPublishedBeforePresentation = auxiliaryViewer.ViewModel.ResultEntities.Count;
            var mainPreviewRequests = 0;
            var auxiliaryPreviewRequests = 0;
            var mainPublishRequests = 0;
            var auxiliaryPublishRequests = 0;
            mainViewer.ViewModel.PreviewThicknessRequested += (_, _) => mainPreviewRequests++;
            mainViewer.ViewModel.PreviewPlaneFlatnessRequested += (_, _) => mainPreviewRequests++;
            mainViewer.ViewModel.PreviewPointPairDimensionsRequested += (_, _) => mainPreviewRequests++;
            mainViewer.ViewModel.PreviewGapFlushRequested += (_, _) => mainPreviewRequests++;
            mainViewer.ViewModel.PreviewVolumeRequested += (_, _) => mainPreviewRequests++;
            mainViewer.ViewModel.PreviewCrossSectionRequested += (_, _) => mainPreviewRequests++;
            mainViewer.ViewModel.PreviewWarpageRequested += (_, _) => mainPreviewRequests++;
            mainViewer.ViewModel.PublishPreviewResultRequested += (_, _) => mainPublishRequests++;
            auxiliaryViewer.ViewModel.PreviewThicknessRequested += (_, _) => auxiliaryPreviewRequests++;
            auxiliaryViewer.ViewModel.PreviewPlaneFlatnessRequested += (_, _) => auxiliaryPreviewRequests++;
            auxiliaryViewer.ViewModel.PreviewPointPairDimensionsRequested += (_, _) => auxiliaryPreviewRequests++;
            auxiliaryViewer.ViewModel.PreviewGapFlushRequested += (_, _) => auxiliaryPreviewRequests++;
            auxiliaryViewer.ViewModel.PreviewVolumeRequested += (_, _) => auxiliaryPreviewRequests++;
            auxiliaryViewer.ViewModel.PreviewCrossSectionRequested += (_, _) => auxiliaryPreviewRequests++;
            auxiliaryViewer.ViewModel.PreviewWarpageRequested += (_, _) => auxiliaryPreviewRequests++;
            auxiliaryViewer.ViewModel.PublishPreviewResultRequested += (_, _) => auxiliaryPublishRequests++;
            mainViewer.ViewModel.Display.SelectedColorMap = "Thermal";
            auxiliaryViewer.ViewModel.Display.SelectedColorMap = "Grayscale";
            mainViewer.ViewModel.SelectionOverlayVisible = false;
            auxiliaryViewer.ViewModel.SelectionOverlayVisible = true;
            mainViewer.ViewModel.ResultOverlayVisible = true;
            auxiliaryViewer.ViewModel.ResultOverlayVisible = false;
            mainViewer.ViewModel.MeasurementVisible = false;
            auxiliaryViewer.ViewModel.MeasurementVisible = true;
            Check(
                "two real Viewer instances keep independent palette and overlay state",
                mainViewer.ViewModel.Display.SelectedColorMap == "Thermal"
                && auxiliaryViewer.ViewModel.Display.SelectedColorMap == "Grayscale"
                && !mainViewer.ViewModel.SelectionOverlayVisible
                && auxiliaryViewer.ViewModel.SelectionOverlayVisible
                && mainViewer.ViewModel.ResultOverlayVisible
                && !auxiliaryViewer.ViewModel.ResultOverlayVisible
                && !mainViewer.ViewModel.MeasurementVisible
                && auxiliaryViewer.ViewModel.MeasurementVisible,
                $"main={mainViewer.ViewModel.Display.SelectedColorMap};aux={auxiliaryViewer.ViewModel.Display.SelectedColorMap}");

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
                mainPreviewRequests == 0
                && auxiliaryPreviewRequests == 0
                && mainPublishRequests == 0
                && auxiliaryPublishRequests == 0
                && mainViewer.ViewModel.ResultEntities.Count == mainPublishedBeforePresentation
                && auxiliaryViewer.ViewModel.ResultEntities.Count == auxiliaryPublishedBeforePresentation,
                $"mainPreviewRequests={mainPreviewRequests};auxPreviewRequests={auxiliaryPreviewRequests};mainPublishRequests={mainPublishRequests};auxPublishRequests={auxiliaryPublishRequests};mainPublished={mainViewer.ViewModel.ResultEntities.Count};auxPublished={auxiliaryViewer.ViewModel.ResultEntities.Count}");

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

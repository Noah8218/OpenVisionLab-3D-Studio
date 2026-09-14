using System.IO;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerHostOperationFacadeVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer Host operation facade verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: operation ownership, validation, callback ports, and disposed gate"
        };
        var passed = 0;
        var total = 0;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        try
        {
            var viewModel = new MainWindowViewModel();
            var disposed = false;
            var visibleFrameRequests = 0;
            var commandCallbacks = new List<string>();
            var savedPaths = new List<string>();

            var facade = new ViewerHostOperationFacade(
                viewModel,
                () => disposed,
                () => visibleFrameRequests++,
                new ViewerHostOperationCallbacks(
                    () => commandCallbacks.Add("FitAll"),
                    () => commandCallbacks.Add("FitSelection"),
                    () => commandCallbacks.Add("FitRoi"),
                    () => commandCallbacks.Add("UseTopView"),
                    () => commandCallbacks.Add("UsePerspectiveView"),
                    () => commandCallbacks.Add("ResetView"),
                    path =>
                    {
                        savedPaths.Add(path);
                        return true;
                    }));

            var capturedState = facade.CaptureCameraState();
            Check(
                "camera capture stays a presentation snapshot",
                capturedState.IsValid,
                capturedState.ToString());

            var updatedState = capturedState with { YawDegrees = capturedState.YawDegrees + 7.0 };
            var applied = facade.TryApplyCameraState(updatedState);
            Check(
                "valid camera state applies and requests one visible frame",
                applied
                && facade.CaptureCameraState() == updatedState
                && visibleFrameRequests == 1,
                $"applied={applied}|frameRequests={visibleFrameRequests}|state={facade.CaptureCameraState()}");

            var invalidCameraState = updatedState with { Distance = double.NaN };
            var frameRequestsBeforeInvalid = visibleFrameRequests;
            Check(
                "invalid camera state fails before mutation or frame request",
                !facade.TryApplyCameraState(invalidCameraState)
                && facade.CaptureCameraState() == updatedState
                && visibleFrameRequests == frameRequestsBeforeInvalid,
                $"state={facade.CaptureCameraState()}|frameRequests={visibleFrameRequests}");

            Check(
                "selection and display mutations stay behind the operation owner",
                !facade.TrySetSelectionMode(string.Empty)
                && facade.TrySetSelectionMode("Box ROI")
                && facade.TrySetSelectionOverlayVisible(false)
                && facade.TrySetHudDetailsVisible(false)
                && facade.TrySetC3DSampleVisible(true)
                && facade.TrySetSelectedColorMap("Height")
                && facade.TrySetSelectedDiagnosticChannel(null)
                && facade.TrySetResultOverlayVisible(false)
                && facade.TrySetMeasurementVisible(true),
                $"selection={viewModel.SelectedSelectionMode}|selectionOverlay={viewModel.SelectionOverlayVisible}|hud={viewModel.HudDetailsVisible}|sample={viewModel.C3DSampleVisible}|color={viewModel.SelectedColorMode}");

            Check(
                "height range operations preserve input validation semantics",
                facade.TrySetC3DHeightColorMinimumRaw(12.5)
                && facade.TrySetC3DHeightColorMaximumRaw(24.5)
                && !facade.TrySetC3DHeightColorMinimumRaw(double.PositiveInfinity)
                && !facade.TrySetC3DHeightColorMaximumRaw(double.NaN)
                && !facade.TryShiftC3DHeightColorMinimum(0)
                && !facade.TryShiftC3DHeightColorMaximum(0)
                && facade.TryShiftC3DHeightColorMinimum(1)
                && facade.TryShiftC3DHeightColorMaximum(-1)
                && facade.TryResetC3DHeightColorRange()
                && !facade.TryApplyLinkedC3DHeightColorRange(2.0, 1.0),
                $"minimum={viewModel.C3DHeightColorMinimumRaw}|maximum={viewModel.C3DHeightColorMaximumRaw}");

            facade.FitAll();
            facade.FitSelection();
            facade.FitRoi();
            facade.UseTopView();
            facade.UsePerspectiveView();
            facade.ResetView();
            Check(
                "named command callbacks are dispatched once per Host operation",
                commandCallbacks.SequenceEqual(
                    ["FitAll", "FitSelection", "FitRoi", "UseTopView", "UsePerspectiveView", "ResetView"],
                    StringComparer.Ordinal),
                $"callbacks={string.Join(",", commandCallbacks)}");

            var recipePath = "verification.recipe.json";
            Check(
                "recipe save crosses the explicit callback port",
                facade.SaveRecipe(recipePath)
                && savedPaths.SequenceEqual([recipePath], StringComparer.Ordinal),
                $"saved={string.Join(",", savedPaths)}");

            Check(
                "publish remains rejected until a preview exists",
                !facade.PublishCurrentPreviewResult()
                && viewModel.ViewerStatus.Contains("No preview result", StringComparison.Ordinal),
                viewModel.ViewerStatus);

            viewModel.RecipeOutputEnabled = false;
            Check(
                "recipe output policy is enforced before publish",
                !facade.PublishCurrentPreviewResult()
                && viewModel.ViewerStatus.Contains("Recipe output is disabled", StringComparison.Ordinal),
                viewModel.ViewerStatus);

            var stateBeforeDispose = facade.CaptureCameraState();
            var frameRequestsBeforeDispose = visibleFrameRequests;
            var commandCallbacksBeforeDispose = commandCallbacks.Count;
            var savedPathCountBeforeDispose = savedPaths.Count;
            disposed = true;
            Check(
                "disposed gate rejects state, command, save, and publish operations",
                !facade.TryApplyCameraState(stateBeforeDispose)
                && !facade.TrySetSelectionMode("Point")
                && !facade.TrySetSelectionOverlayVisible(true)
                && !facade.TrySetC3DHeightColorMinimumRaw(0.0)
                && !facade.TryApplyLinkedC3DHeightColorRange(1.0, 2.0)
                && !facade.SaveRecipe("disposed.recipe.json")
                && !facade.PublishCurrentPreviewResult()
                && visibleFrameRequests == frameRequestsBeforeDispose
                && commandCallbacks.Count == commandCallbacksBeforeDispose
                && savedPaths.Count == savedPathCountBeforeDispose,
                $"frameRequests={visibleFrameRequests}|commands={commandCallbacks.Count}|saved={savedPaths.Count}");
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL | verifier exception | {exception.GetType().Name}: {exception.Message}");
        }

        var succeeded = passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ViewerHostOperationFacade|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}

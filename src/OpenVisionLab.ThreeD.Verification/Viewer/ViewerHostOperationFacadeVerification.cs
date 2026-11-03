using System.IO;
using OpenVisionLab.ThreeD.Viewer.Hosting;
using OpenVisionLab.ThreeD.Viewer.Models;
using OpenVisionLab.ThreeD.Viewer.ViewModels;

namespace OpenVisionLab.ThreeD.Verification.Viewer;

internal static class ViewerHostOperationFacadeVerification
{
    public static bool Verify(string reportPath, out string summary)
        => VerifyWithStageObserver(reportPath, out summary, null);

    internal static bool VerifyWithStageObserver(
        string reportPath, out string summary, Action<string>? stageCompleted)
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

            stageCompleted?.Invoke("facade-first-camera-check");

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

            var labelSource = new OpenVisionLab.ThreeD.Core.ToolRecipeSelectionSourceBinding(Format: "C3D", ContentSha256: new string('A', 64), GridWidth: 12, GridHeight: 8);
            var labelAnchor = new OpenVisionLab.ThreeD.Core.ToolRecipeSelection("roi.fixture", "Measure", "GridRectangle", "source.fixture", "frame.fixture",
                labelSource, new(2, 3, 2, 4), null, null);
            var exact = 8.1234567890123456;
            var label = new ViewerTeachingResultLabel("step.fixture", "Thickness", "Mean", exact, "8.123", "raw-height",
                OpenVisionLab.ThreeD.Core.ResultStatus.Pass, true, labelAnchor);
            var labels = new[] { label };
            var previewBeforeLabels = viewModel.PreviewToolResult;
            var publishedBeforeLabels = viewModel.ResultEntities;
            var frameBeforeLabels = visibleFrameRequests;
            Check("teaching labels copy raw values and identity without Preview or Publish",
                facade.TrySetTeachingResultLabels(labels) && viewModel.TeachingResultLabels.Single() == label
                    && viewModel.TeachingResultLabels[0].Value == exact && visibleFrameRequests == frameBeforeLabels + 1
                    && ReferenceEquals(previewBeforeLabels, viewModel.PreviewToolResult) && ReferenceEquals(publishedBeforeLabels, viewModel.ResultEntities),
                $"raw={viewModel.TeachingResultLabels[0].Value:R}|source={viewModel.TeachingResultLabels[0].Anchor.SourceBinding.ContentSha256}");
            labels[0] = label with { Value = 999 };
            Check("host owns a collection snapshot and invalid input is atomic",
                viewModel.TeachingResultLabels[0].Value == exact
                    && !facade.TrySetTeachingResultLabels([label with { Value = double.NaN }])
                    && !facade.TrySetTeachingResultLabels([label with { Anchor = labelAnchor with { GridRectangle = new(-1, 3, 2, 4) } }])
                    && !facade.TrySetTeachingResultLabels([label with { Anchor = labelAnchor with { GridRectangle = new(2, 11, 2, 4) } }])
                    && viewModel.TeachingResultLabels.Single() == label && visibleFrameRequests == frameBeforeLabels + 1,
                $"raw={viewModel.TeachingResultLabels[0].Value:R}|frames={visibleFrameRequests}");
            var hidden = label with { IsVisible = false };
            Check("hide clear and valid recovery change only label presentation",
                facade.TrySetTeachingResultLabels([hidden]) && !viewModel.TeachingResultLabels[0].IsVisible
                    && facade.TrySetTeachingResultLabels([]) && viewModel.TeachingResultLabels.Count == 0
                    && facade.TrySetTeachingResultLabels([label]) && viewModel.TeachingResultLabels.Single() == label
                    && ReferenceEquals(previewBeforeLabels, viewModel.PreviewToolResult) && ReferenceEquals(publishedBeforeLabels, viewModel.ResultEntities),
                $"labels={viewModel.TeachingResultLabels.Count}|caption={viewModel.TeachingResultLabels[0].Caption}");

            var stateBeforeDispose = facade.CaptureCameraState();
            var frameRequestsBeforeDispose = visibleFrameRequests;
            var commandCallbacksBeforeDispose = commandCallbacks.Count;
            var savedPathCountBeforeDispose = savedPaths.Count;
            disposed = true;
            Check(
                "disposed gate rejects state, command, save, and publish operations",
                !facade.TrySetTeachingResultLabels([]) && viewModel.TeachingResultLabels.Single() == label
                && !facade.TryApplyCameraState(stateBeforeDispose)
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
            Check("verifier exception", false, exception.ToString());
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

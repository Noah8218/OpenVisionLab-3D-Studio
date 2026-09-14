using System.ComponentModel;
using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchTeachingSelectionEventCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D teaching-selection event coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: selection store, capture, and landmark editor notification ownership"
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
            var store = new ToolWorkbenchTeachingSelectionStoreOwner(
                () => "root",
                () => "frame",
                () => null,
                _ => ToolWorkbenchPublishedSelectionBindingState.Unavailable,
                () => null,
                () => false,
                _ => { },
                _ => { });
            var capture = new ToolWorkbenchTeachingSelectionCaptureOwner(
                new ToolWorkbenchTeachingCaptureSession(),
                _ => null,
                () => null,
                () => null,
                () => null,
                () => false,
                _ => { },
                () => { },
                () => { },
                (_, _) => { },
                () => false,
                () => false,
                () => { },
                () => false,
                () => { });
            var landmark = new ToolWorkbenchLandmarkCorrespondenceEditorOwner(
                () => new ToolWorkbenchLandmarkCorrespondenceEditorContext(
                    null,
                    false,
                    null,
                    null,
                    "root",
                    "frame",
                    null,
                    []),
                (_, _) => "selection-id",
                _ => { },
                _ => { },
                () => { },
                (_, _) => { });

            var storeCallbacks = 0;
            var captureCallbacks = 0;
            var captureStateCallbacks = 0;
            var landmarkCallbacks = 0;
            using var coordinator = new ToolWorkbenchTeachingSelectionEventCoordinator(
                store,
                (_, _) => storeCallbacks++,
                capture,
                (_, _) => captureCallbacks++,
                (_, _) => captureStateCallbacks++,
                landmark,
                (_, _) => landmarkCallbacks++);

            var binding = new ToolRecipeSelectionSourceBinding("C3D", "hash", 1, 1);
            store.SelectedCompatibleSelection = new ToolRecipeSelection(
                "selection",
                "Selection",
                ToolRecipeSelectionKinds.GridRectangle,
                "root",
                "frame",
                binding,
                null,
                [],
                []);
            capture.UpdateState(true, 0, 1, false, "capture");
            landmark.SourceEntityId = "entity";
            Check(
                "Store, capture, state, and landmark notifications reach their callbacks",
                storeCallbacks == 1
                    && captureCallbacks > 0
                    && captureStateCallbacks == 1
                    && landmarkCallbacks == 1,
                $"store={storeCallbacks};capture={captureCallbacks};state={captureStateCallbacks};landmark={landmarkCallbacks}");

            coordinator.Dispose();
            coordinator.Dispose();
            var storeBeforeDispose = storeCallbacks;
            var captureBeforeDispose = captureCallbacks;
            var stateBeforeDispose = captureStateCallbacks;
            var landmarkBeforeDispose = landmarkCallbacks;
            store.SelectedCompatibleSelection = null;
            capture.UpdateState(false, 0, 0, false, "disposed");
            landmark.SourceEntityId = "after-dispose";
            Check(
                "Dispose is idempotent and suppresses late owner notifications",
                storeCallbacks == storeBeforeDispose
                    && captureCallbacks == captureBeforeDispose
                    && captureStateCallbacks == stateBeforeDispose
                    && landmarkCallbacks == landmarkBeforeDispose,
                $"before={storeBeforeDispose}/{captureBeforeDispose}/{stateBeforeDispose}/{landmarkBeforeDispose};after={storeCallbacks}/{captureCallbacks}/{captureStateCallbacks}/{landmarkCallbacks}");
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
        summary = $"ToolWorkbenchTeachingSelectionEventCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }
}

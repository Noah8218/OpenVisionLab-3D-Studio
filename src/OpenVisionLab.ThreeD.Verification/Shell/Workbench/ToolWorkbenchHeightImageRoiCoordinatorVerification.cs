using System.IO;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

namespace OpenVisionLab.ThreeD.Verification.Shell.Workbench;

internal static class ToolWorkbenchHeightImageRoiCoordinatorVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);
        var lines = new List<string>
        {
            "OpenVisionLab 3D ToolWorkbench Height Image ROI coordinator verification",
            $"Generated: {DateTimeOffset.Now:O}",
            "Scope: ROI event routing, warning propagation, command guards, and disposal"
        };
        var passed = 0;
        var total = 0;
        Exception? failure = null;

        void Check(string name, bool condition, string detail)
        {
            total++;
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition)
            {
                passed++;
            }
        }

        var workspace = new HeightImageRoiWorkspaceViewModel(ThreeDLocalization.Shared);
        using var pointerContractWorkspace = new HeightImageRoiWorkspaceViewModel(ThreeDLocalization.Shared);
        var candidateCount = 0;
        var selectedIds = new List<string>();
        var warningMessages = new List<string>();
        var applyCount = 0;
        var cancelCount = 0;
        var deleteCount = 0;
        var coordinator = new ToolWorkbenchHeightImageRoiCoordinator(
            workspace,
            rectangle =>
            {
                candidateCount++;
                return new HeightImageRoiCandidateUpdateResult(false, "test rejection");
            },
            selectionId => selectedIds.Add(selectionId),
            message => warningMessages.Add(message),
            () => applyCount++,
            () => cancelCount++,
            () => deleteCount++);

        try
        {
            pointerContractWorkspace.SetProjection(CreateProjection(
                isCaptureActive: true,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Drawing,
                candidate: null));
            var pointerBegin = pointerContractWorkspace.HandlePointer(
                new HeightImageRoiPointerInput(
                    HeightImageRoiPointerAction.Begin,
                    Row: 0,
                    Column: 0));
            var pointerUpdate = pointerContractWorkspace.HandlePointer(
                new HeightImageRoiPointerInput(
                    HeightImageRoiPointerAction.Update,
                    Row: 1,
                    Column: 1));
            var pointerEnd = pointerContractWorkspace.HandlePointer(
                new HeightImageRoiPointerInput(HeightImageRoiPointerAction.End));
            Check(
                "typed pointer input/result contract preserves ROI gesture lifecycle",
                pointerBegin.Handled
                && pointerBegin.CaptureMouse
                && pointerBegin.IsGestureActive
                && pointerUpdate.Handled
                && pointerEnd.Handled
                && pointerEnd.ReleaseMouse
                && !pointerEnd.IsGestureActive
                && pointerEnd.Candidate == new ToolRecipeGridRectangle(0, 0, 2, 2),
                $"begin={pointerBegin}; update={pointerUpdate}; end={pointerEnd}");

            workspace.SetProjection(CreateProjection(
                isCaptureActive: true,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Drawing,
                candidate: null));
            workspace.TryBeginPointer(row: 0, column: 0, rowTolerance: 0, columnTolerance: 0);
            workspace.EndPointer();
            Check(
                "candidate event reaches the Workbench callback",
                candidateCount == 1,
                $"candidateCount={candidateCount}");
            Check(
                "candidate rejection is converted to the existing warning message",
                warningMessages.SequenceEqual(["Height Image ROI edit rejected | reason=test rejection"]),
                $"warnings={string.Join(";", warningMessages)}");

            workspace.SetProjection(CreateProjection(
                isCaptureActive: false,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Applied,
                candidate: null,
                includeOverlay: true));
            workspace.TryBeginPointer(row: 1, column: 1, rowTolerance: 0, columnTolerance: 0);
            Check(
                "selection event reaches the Workbench callback",
                selectedIds.SequenceEqual(["selection-1"]),
                $"selectedIds={string.Join(",", selectedIds)}");

            workspace.SetProjection(CreateProjection(
                isCaptureActive: true,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Review,
                candidate: new ToolRecipeGridRectangle(0, 0, 2, 2)));
            workspace.ApplyCommand.Execute(null);
            workspace.CancelCommand.Execute(null);
            Check(
                "Apply and Cancel callbacks are routed without moving command guards",
                applyCount == 1 && cancelCount == 1,
                $"apply={applyCount}; cancel={cancelCount}");

            workspace.SetProjection(CreateProjection(
                isCaptureActive: false,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Applied,
                candidate: null,
                includeOverlay: true));
            workspace.DeleteCommand.Execute(null);
            Check(
                "Delete callback is routed for an active overlay",
                deleteCount == 1,
                $"delete={deleteCount}");

            coordinator.Dispose();
            coordinator.Dispose();
            var candidateCountAfterDispose = candidateCount;
            var selectedCountAfterDispose = selectedIds.Count;
            var applyCountAfterDispose = applyCount;
            var cancelCountAfterDispose = cancelCount;
            var deleteCountAfterDispose = deleteCount;

            workspace.SetProjection(CreateProjection(
                isCaptureActive: true,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Drawing,
                candidate: null));
            workspace.TryBeginPointer(row: 0, column: 0, rowTolerance: 0, columnTolerance: 0);
            workspace.EndPointer();
            workspace.SetProjection(CreateProjection(
                isCaptureActive: false,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Applied,
                candidate: null,
                includeOverlay: true));
            workspace.TryBeginPointer(row: 1, column: 1, rowTolerance: 0, columnTolerance: 0);
            workspace.SetProjection(CreateProjection(
                isCaptureActive: true,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Review,
                candidate: new ToolRecipeGridRectangle(0, 0, 2, 2)));
            workspace.ApplyCommand.Execute(null);
            workspace.CancelCommand.Execute(null);
            workspace.SetProjection(CreateProjection(
                isCaptureActive: false,
                lifecycle: InspectionWorkspaceRegionLifecycleState.Applied,
                candidate: null,
                includeOverlay: true));
            workspace.DeleteCommand.Execute(null);
            Check(
                "Dispose is idempotent and blocks every later ROI callback",
                candidateCount == candidateCountAfterDispose
                && selectedIds.Count == selectedCountAfterDispose
                && applyCount == applyCountAfterDispose
                && cancelCount == cancelCountAfterDispose
                && deleteCount == deleteCountAfterDispose,
                $"candidate={candidateCount}; selected={selectedIds.Count}; apply={applyCount}; cancel={cancelCount}; delete={deleteCount}");
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            coordinator.Dispose();
            workspace.Dispose();
        }

        if (failure is not null)
        {
            lines.Add($"FAIL | verifier exception | {failure.GetType().Name}: {failure.Message}");
        }

        var succeeded = failure is null && passed == total && total > 0;
        lines.Add($"Result: {(succeeded ? "Pass" : "Fail")} ({passed}/{total} checks)");
        var fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath)!);
        File.WriteAllLines(fullReportPath, lines);
        summary = $"ToolWorkbenchHeightImageRoiCoordinator|pass={succeeded}|checks={passed}/{total}|report={fullReportPath}";
        return succeeded;
    }

    private static HeightImageRoiProjection CreateProjection(
        bool isCaptureActive,
        InspectionWorkspaceRegionLifecycleState lifecycle,
        ToolRecipeGridRectangle? candidate,
        bool includeOverlay = false) =>
        new(
            HasContext: true,
            GridWidth: 3,
            GridHeight: 3,
            ActiveRole: InspectionWorkspaceRegionRole.Selection,
            Lifecycle: lifecycle,
            IsCaptureActive: isCaptureActive,
            Candidate: candidate,
            Overlays: includeOverlay
                ? [new HeightImageRoiOverlayItem(
                    "selection-1",
                    "Selection 1",
                    InspectionWorkspaceRegionRole.Selection,
                    InspectionWorkspaceRegionLifecycleState.Applied,
                    new ToolRecipeGridRectangle(0, 0, 3, 3),
                    IsActive: true,
                    IsCandidate: false)]
                : []);
}

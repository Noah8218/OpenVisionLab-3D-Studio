using System.IO;
using System.Text;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Viewer.ViewModels;

public static class TeachingCaptureViewModelVerification
{
    public static bool Verify(string reportPath, out string summary)
    {
        var lines = new List<string>
        {
            "OpenVisionLab 3D Viewer teaching-capture ViewModel verification",
            $"Generated: {DateTimeOffset.Now:O}"
        };
        var passed = 0;
        var failed = 0;

        void Check(string name, bool condition, string detail)
        {
            lines.Add($"{(condition ? "PASS" : "FAIL")} | {name} | {detail}");
            if (condition) passed++; else failed++;
        }

        try
        {
            var viewModel = new MainWindowViewModel();
            var initialSelectionMode = viewModel.SelectedSelectionMode;
            var initialPreviewStatus = viewModel.PreviewToolResult.Status;
            var initialResultCount = viewModel.ResultEntities.Count;
            var initialTwoPointVisible = viewModel.TwoPointMeasurementVisible;
            var binding = new ToolRecipeSelectionSourceBinding("C3D", new string('A', 64), 64, 48);

            var rectangleRequest = Request("selection.roi.01", "Inspection ROI", ToolRecipeSelectionKinds.GridRectangle, 2, binding);
            Check("rectangle capture begins", viewModel.BeginTeachingCapture(rectangleRequest, out _), viewModel.TeachingCaptureSnapshot.ProgressText);
            Check("rectangle first point", viewModel.TryAddTeachingCapturePoint(Point(2, 3, -1, 0.1, -2, 10), out _), viewModel.TeachingCaptureSnapshot.ProgressText);
            Check("rectangle remains incomplete", !viewModel.TeachingCaptureSnapshot.CanApply && viewModel.TeachingCaptureSnapshot.CapturedPointCount == 1, viewModel.TeachingCaptureSnapshot.ProgressText);
            Check("rectangle second point", viewModel.TryAddTeachingCapturePoint(Point(8, 12, 1, 0.2, 2, 20), out _), viewModel.TeachingCaptureSnapshot.ProgressText);
            Check("rectangle candidate ready", viewModel.TryGetTeachingCaptureCandidate(out var rectangle, out _) && rectangle?.GridRectangle == new ToolRecipeGridRectangle(2, 3, 7, 10), rectangle?.GridRectangle?.ToString() ?? "none");
            Check("undo returns to one point", viewModel.UndoTeachingCapture() && viewModel.TeachingCaptureSnapshot.CapturedPointCount == 1 && !viewModel.TeachingCaptureSnapshot.CanApply, viewModel.TeachingCaptureSnapshot.ProgressText);
            Check("rectangle can be completed again", viewModel.TryAddTeachingCapturePoint(Point(8, 12, 1, 0.2, 2, 20), out _) && viewModel.TryGetTeachingCaptureCandidate(out rectangle, out _), viewModel.TeachingCaptureSnapshot.ProgressText);
            viewModel.SetAppliedTeachingSelections([rectangle!]);
            viewModel.ConfirmTeachingCaptureApplied();
            Check("confirm clears transient and retains applied", !viewModel.IsTeachingCaptureActive && viewModel.AppliedTeachingSelections.Count == 1, viewModel.TeachingCaptureSnapshot.Message);

            var artifactBinding = new ToolRecipeSelectionSourceBinding(
                "TransformedHeightField", new string('B', 64), 20, 10,
                "derived.height-field", new string('C', 64), "fixture-unit", "frame.fixture");
            var artifactRequest = new TeachingCaptureRequest(
                "selection.artifact.roi", "Artifact ROI", ToolRecipeSelectionKinds.GridRectangle, 2,
                "source.c3d.height-map", "frame.fixture", artifactBinding);
            Check("artifact-owned rectangle capture begins", viewModel.BeginTeachingCapture(artifactRequest, out _), viewModel.TeachingCaptureSnapshot.ProgressText);
            viewModel.TryAddTeachingCapturePoint(Point(1, 2, 1, 2, 3, 4), out _);
            viewModel.TryAddTeachingCapturePoint(Point(5, 8, 5, 6, 7, 8), out _);
            Check("artifact binding and frame survive candidate creation",
                viewModel.TryGetTeachingCaptureCandidate(out var artifactSelection, out _)
                && artifactSelection is not null
                && artifactSelection.FrameId == "frame.fixture"
                && artifactSelection.SourceBinding == artifactBinding
                && artifactSelection.GridRectangle == new ToolRecipeGridRectangle(1, 2, 5, 7),
                artifactSelection?.SourceBinding.OwnerEntityId ?? "none");
            viewModel.CancelTeachingCapture();

            var flatnessReferenceRequest = Request(
                "selection.plane-flatness.reference-roi", "Plane Flatness Reference ROI",
                ToolRecipeSelectionKinds.GridRectangle, 2, binding);
            viewModel.BeginTeachingCapture(flatnessReferenceRequest, out _);
            viewModel.TryAddTeachingCapturePoint(Point(0, 0, 0, 10, 0, 10), out _);
            viewModel.TryAddTeachingCapturePoint(Point(1, 1, 1, 12, 1, 12), out _);
            var referenceReady = viewModel.TryGetTeachingCaptureCandidate(out var flatnessReference, out _);
            Check("Plane Flatness Reference ROI candidate keeps its role identity",
                referenceReady
                && flatnessReference?.Id == flatnessReferenceRequest.SelectionId
                && flatnessReference.GridRectangle == new ToolRecipeGridRectangle(0, 0, 2, 2),
                flatnessReference?.Id ?? "none");
            viewModel.ConfirmTeachingCaptureApplied();

            var flatnessMeasurementRequest = Request(
                "selection.plane-flatness.measurement-roi", "Plane Flatness Measurement ROI",
                ToolRecipeSelectionKinds.GridRectangle, 2, binding);
            viewModel.BeginTeachingCapture(flatnessMeasurementRequest, out _);
            viewModel.TryAddTeachingCapturePoint(Point(2, 2, 2, 14, 2, 14), out _);
            viewModel.TryAddTeachingCapturePoint(Point(3, 3, 3, 16, 3, 16), out _);
            var measurementReady = viewModel.TryGetTeachingCaptureCandidate(out var flatnessMeasurement, out _);
            Check("Plane Flatness Measurement ROI candidate keeps a distinct role identity",
                measurementReady
                && flatnessMeasurement?.Id == flatnessMeasurementRequest.SelectionId
                && flatnessMeasurement.GridRectangle == new ToolRecipeGridRectangle(2, 2, 2, 2)
                && !string.Equals(flatnessMeasurement.Id, flatnessReference?.Id, StringComparison.Ordinal),
                flatnessMeasurement?.Id ?? "none");
            viewModel.CancelTeachingCapture();

            var twoPointRequest = Request("selection.line.01", "Line points", ToolRecipeSelectionKinds.PointSet, 2, binding);
            viewModel.BeginTeachingCapture(twoPointRequest, out _);
            viewModel.TryAddTeachingCapturePoint(Point(4, 4, 0, 0, 0, 10), out _);
            Check("duplicate cell is rejected", !viewModel.TryAddTeachingCapturePoint(Point(4, 4, 0, 0, 0, 10), out _), viewModel.TeachingCaptureSnapshot.Message);
            viewModel.TryAddTeachingCapturePoint(Point(4, 14, 1, 0, 0, 11), out _);
            Check("two-point candidate ready", viewModel.TryGetTeachingCaptureCandidate(out var twoPoint, out _) && twoPoint?.Points?.Count == 2 && twoPoint.GridRectangle is null, twoPoint?.Kind ?? "none");
            viewModel.CancelTeachingCapture();
            Check("cancel changes no applied selection", !viewModel.IsTeachingCaptureActive && viewModel.AppliedTeachingSelections.Count == 1, viewModel.TeachingCaptureSnapshot.Message);

            var threePointRequest = Request("selection.plane.01", "Plane points", ToolRecipeSelectionKinds.PointSet, 3, binding);
            viewModel.BeginTeachingCapture(threePointRequest, out _);
            viewModel.TryAddTeachingCapturePoint(Point(1, 1, 0, 0, 0, 10), out _);
            viewModel.TryAddTeachingCapturePoint(Point(1, 2, 1, 0, 0, 11), out _);
            viewModel.TryAddTeachingCapturePoint(Point(1, 3, 2, 0, 0, 12), out _);
            Check("collinear three-point candidate is not applicable", !viewModel.TeachingCaptureSnapshot.CanApply && !viewModel.TryGetTeachingCaptureCandidate(out _, out _), viewModel.TeachingCaptureSnapshot.ProgressText);
            viewModel.UndoTeachingCapture();
            viewModel.TryAddTeachingCapturePoint(Point(9, 1, 0, 0, 1, 13), out _);
            Check("non-collinear three-point candidate is ready", viewModel.TryGetTeachingCaptureCandidate(out var threePoint, out _) && threePoint?.Points?.Count == 3, viewModel.TeachingCaptureSnapshot.ProgressText);

            Check(
                "capture does not invoke inspection state",
                viewModel.SelectedSelectionMode == initialSelectionMode
                && viewModel.PreviewToolResult.Status == initialPreviewStatus
                && viewModel.ResultEntities.Count == initialResultCount
                && viewModel.TwoPointMeasurementVisible == initialTwoPointVisible,
                $"mode={viewModel.SelectedSelectionMode}; preview={viewModel.PreviewToolResult.Status}; results={viewModel.ResultEntities.Count}; twoPoint={viewModel.TwoPointMeasurementVisible}");
        }
        catch (Exception exception)
        {
            failed++;
            lines.Add($"FAIL | unhandled verification exception | {exception}");
        }

        summary = failed == 0
            ? $"Pass ({passed}/{passed} checks)"
            : $"Fail ({failed} failed, {passed} passed)";
        lines.Add($"Result: {summary}");
        var fullPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllLines(fullPath, lines, new UTF8Encoding(false));
        return failed == 0;
    }

    private static TeachingCaptureRequest Request(
        string id,
        string name,
        string kind,
        int requiredPointCount,
        ToolRecipeSelectionSourceBinding binding) =>
        new(id, name, kind, requiredPointCount, "source.c3d.height-map", "frame.c3d-grid-index", binding);

    private static ToolRecipeSelectionPoint Point(
        int row,
        int column,
        double x,
        double y,
        double z,
        double raw) =>
        new(new ToolRecipeGridCellLocator("grid-cell", row, column), new ToolRecipeXyz(x, y, z), raw);
}

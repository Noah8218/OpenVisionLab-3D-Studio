using OpenVisionLab.ThreeD.Core;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Thin composition bridge between the recipe-owned teaching lifecycle and
/// the independent Height Image ROI presentation/gesture owner.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private void InitializeHeightImageRoiEditing()
    {
        HeightImageViewer.RoiWorkspace.CandidateChanged += OnHeightImageRoiCandidateChanged;
        HeightImageViewer.RoiWorkspace.SelectionRequested += OnHeightImageRoiSelectionRequested;
        HeightImageViewer.RoiWorkspace.ApplyRequested += OnHeightImageRoiApplyRequested;
        HeightImageViewer.RoiWorkspace.CancelRequested += OnHeightImageRoiCancelRequested;
        HeightImageViewer.RoiWorkspace.DeleteRequested += OnHeightImageRoiDeleteRequested;
        RefreshHeightImageRoiProjection();
    }

    public bool TryUpdateHeightImageRoiCandidate(
        ToolRecipeGridRectangle rectangle,
        out string message)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        var binding = SelectedStepTeachingSelection?.SourceBinding ?? SourceSession.SourceBinding;
        if (!IsTeachingSelectionCaptureActive
            || SelectedStepSelectionRequirement?.Kind != ToolRecipeSelectionKinds.GridRectangle
            || binding is null)
        {
            message = "Height Image ROI editing requires an active GridRectangle capture.";
            return false;
        }

        if (rectangle.Row < 0
            || rectangle.Column < 0
            || rectangle.RowCount <= 0
            || rectangle.ColumnCount <= 0
            || (long)rectangle.Row + rectangle.RowCount > binding.GridHeight
            || (long)rectangle.Column + rectangle.ColumnCount > binding.GridWidth)
        {
            message = "Height Image ROI candidate must stay inside the native source grid.";
            return false;
        }

        if (IsSelectedStepCrossSectionDimensions
            && (rectangle.RowCount != 1 || rectangle.ColumnCount < 2))
        {
            message = "Cross-section Dimensions requires one row and at least two columns.";
            return false;
        }

        UpdateTeachingGridRectangleDraft(rectangle);
        SetTeachingSelectionCaptureState(
            active: true,
            capturedPointCount: 2,
            requiredPointCount: Math.Max(2, SelectedStepSelectionRequirement.RequiredPointCount),
            canApply: true,
            message: "Height Image ROI candidate is ready for Review. Apply remains explicit.");
        TeachingGridRectangleDraftChanged?.Invoke(
            this,
            new ToolWorkbenchGridRectangleDraftChangedEventArgs(rectangle));
        message = "Height Image ROI candidate synchronized with the 3D Viewer.";
        return true;
    }

    private void RefreshHeightImageRoiProjection()
    {
        var overlays = new List<HeightImageRoiOverlayItem>();
        var activeRole = GetInspectionWorkspaceRegionRole();
        if (IsSelectedStepDualRoiMeasurement)
        {
            AddHeightImageRoiOverlay(
                overlays,
                PlaneFlatnessReferenceSelection,
                IsSelectedStepGapFlush
                    ? InspectionWorkspaceRegionRole.First
                    : InspectionWorkspaceRegionRole.Reference,
                activeRole);
            AddHeightImageRoiOverlay(
                overlays,
                PlaneFlatnessMeasurementSelection,
                IsSelectedStepGapFlush
                    ? InspectionWorkspaceRegionRole.Second
                    : InspectionWorkspaceRegionRole.Measurement,
                activeRole);
        }
        else
        {
            AddHeightImageRoiOverlay(
                overlays,
                SelectedStepTeachingSelection,
                InspectionWorkspaceRegionRole.Selection,
                activeRole);
        }

        var lifecycle = IsTeachingSelectionCaptureActive
            ? CanApplyTeachingSelectionCapture
                ? InspectionWorkspaceRegionLifecycleState.Review
                : InspectionWorkspaceRegionLifecycleState.Drawing
            : SelectedStepTeachingSelection is null
                ? InspectionWorkspaceRegionLifecycleState.Missing
                : InspectionWorkspaceRegionLifecycleState.Applied;
        var gridRectangleDraft = TeachingCaptureSession.GridRectangleDraft;
        var candidate = IsTeachingSelectionCaptureActive
                        && gridRectangleDraft.RowCount > 0
                        && gridRectangleDraft.ColumnCount > 0
            ? gridRectangleDraft
            : null;
        var binding = SelectedStepTeachingSelection?.SourceBinding ?? SourceSession.SourceBinding;
        HeightImageViewer.RoiWorkspace.SetProjection(new HeightImageRoiProjection(
            SelectedStepSelectionRequirement?.Kind == ToolRecipeSelectionKinds.GridRectangle
            && binding is not null,
            binding?.GridWidth ?? 0,
            binding?.GridHeight ?? 0,
            activeRole,
            lifecycle,
            IsTeachingSelectionCaptureActive,
            candidate,
            overlays));
    }

    private static void AddHeightImageRoiOverlay(
        ICollection<HeightImageRoiOverlayItem> overlays,
        ToolRecipeSelection? selection,
        InspectionWorkspaceRegionRole role,
        InspectionWorkspaceRegionRole activeRole)
    {
        if (selection?.GridRectangle is not { } rectangle)
        {
            return;
        }

        overlays.Add(new HeightImageRoiOverlayItem(
            selection.Id,
            selection.Name,
            role,
            InspectionWorkspaceRegionLifecycleState.Applied,
            rectangle,
            role == activeRole,
            false));
    }

    private void OnHeightImageRoiCandidateChanged(
        object? sender,
        HeightImageRoiCandidateChangedEventArgs args)
    {
        if (!TryUpdateHeightImageRoiCandidate(args.Rectangle, out var message))
        {
            AppendLog("Warning", $"Height Image ROI edit rejected | reason={message}");
        }
    }

    private void OnHeightImageRoiSelectionRequested(
        object? sender,
        HeightImageRoiSelectionRequestedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(args.SelectionId))
        {
            SelectPipelineStepForSelection(args.SelectionId);
        }
    }

    private void OnHeightImageRoiApplyRequested(object? sender, EventArgs args)
    {
        if (ApplyTeachingSelectionCaptureCommand.CanExecute(null))
        {
            ApplyTeachingSelectionCaptureCommand.Execute(null);
        }
    }

    private void OnHeightImageRoiCancelRequested(object? sender, EventArgs args)
    {
        if (CancelTeachingSelectionCaptureCommand.CanExecute(null))
        {
            CancelTeachingSelectionCaptureCommand.Execute(null);
        }
    }

    private void OnHeightImageRoiDeleteRequested(object? sender, EventArgs args)
    {
        if (RemoveSelectedTeachingSelectionCommand.CanExecute(null))
        {
            RemoveSelectedTeachingSelectionCommand.Execute(null);
        }
    }
}

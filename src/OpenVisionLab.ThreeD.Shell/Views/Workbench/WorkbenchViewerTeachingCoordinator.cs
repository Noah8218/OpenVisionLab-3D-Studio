using System.ComponentModel;
using OpenVisionLab.Logging;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;
using OpenVisionLab.ThreeD.Viewer;
using OpenVisionLab.ThreeD.Viewer.Models;

namespace OpenVisionLab.ThreeD.Shell.Views.Workbench;

internal sealed class WorkbenchViewerTeachingCoordinator : IDisposable
{
    private readonly ToolWorkbenchViewModel workbench;
    private readonly OpenVisionThreeDViewerControl viewer;
    private readonly Action collapseBottomPane;
    private bool disposed;

    public WorkbenchViewerTeachingCoordinator(
        ToolWorkbenchViewModel workbench,
        OpenVisionThreeDViewerControl viewer,
        Action collapseBottomPane)
    {
        this.workbench = workbench;
        this.viewer = viewer;
        this.collapseBottomPane = collapseBottomPane;

        workbench.BeginTeachingSelectionCaptureRequested += OnBeginTeachingCaptureRequested;
        workbench.UndoTeachingSelectionCaptureRequested += OnUndoTeachingCaptureRequested;
        workbench.CancelTeachingSelectionCaptureRequested += OnCancelTeachingCaptureRequested;
        workbench.ApplyTeachingSelectionCaptureRequested += OnApplyTeachingCaptureRequested;
        workbench.AppliedTeachingSelectionsChanged += OnAppliedTeachingSelectionsChanged;
        workbench.TeachingGridRectangleDraftChanged += OnGridRectangleDraftChanged;
        workbench.TeachingGridCircleDraftChanged += OnGridCircleDraftChanged;
        workbench.TeachingGridPolygonDraftChanged += OnGridPolygonDraftChanged;
        workbench.OrientedBoxEditor.DraftChanged += OnOrientedBoxDraftChanged;
        workbench.ThicknessRepeatGridPreviewChanged += OnThicknessRepeatGridPreviewChanged;
        workbench.FitWorkspaceRegionRequested += OnFitWorkspaceRegionRequested;
        workbench.PropertyChanged += OnWorkbenchPropertyChanged;
        viewer.TeachingCaptureStateChanged += OnViewerTeachingCaptureStateChanged;
        viewer.TeachingSelectionSelected += OnViewerTeachingSelectionSelected;
        viewer.TeachingOrientedBox3DDraftChanged += OnViewerOrientedBoxDraftChanged;
        viewer.TeachingRoiDisplayHeightChanged += OnViewerTeachingRoiDisplayHeightChanged;
    }

    public void SyncAppliedSelections()
    {
        viewer.SetAppliedTeachingSelections(workbench.GetCurrentAppliedTeachingSelections());
        SyncCompletenessCellOverlays();
        SyncConnectedRegionOverlays();
        SyncOrientedBoxDraft();
        SyncSelectedSelection();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        workbench.BeginTeachingSelectionCaptureRequested -= OnBeginTeachingCaptureRequested;
        workbench.UndoTeachingSelectionCaptureRequested -= OnUndoTeachingCaptureRequested;
        workbench.CancelTeachingSelectionCaptureRequested -= OnCancelTeachingCaptureRequested;
        workbench.ApplyTeachingSelectionCaptureRequested -= OnApplyTeachingCaptureRequested;
        workbench.AppliedTeachingSelectionsChanged -= OnAppliedTeachingSelectionsChanged;
        workbench.TeachingGridRectangleDraftChanged -= OnGridRectangleDraftChanged;
        workbench.TeachingGridCircleDraftChanged -= OnGridCircleDraftChanged;
        workbench.TeachingGridPolygonDraftChanged -= OnGridPolygonDraftChanged;
        workbench.OrientedBoxEditor.DraftChanged -= OnOrientedBoxDraftChanged;
        workbench.ThicknessRepeatGridPreviewChanged -= OnThicknessRepeatGridPreviewChanged;
        workbench.FitWorkspaceRegionRequested -= OnFitWorkspaceRegionRequested;
        workbench.PropertyChanged -= OnWorkbenchPropertyChanged;
        viewer.TeachingCaptureStateChanged -= OnViewerTeachingCaptureStateChanged;
        viewer.TeachingSelectionSelected -= OnViewerTeachingSelectionSelected;
        viewer.TeachingOrientedBox3DDraftChanged -= OnViewerOrientedBoxDraftChanged;
        viewer.TeachingRoiDisplayHeightChanged -= OnViewerTeachingRoiDisplayHeightChanged;
    }

    private void OnBeginTeachingCaptureRequested(
        object? sender,
        ToolWorkbenchTeachingCaptureRequestEventArgs args)
    {
        if (string.Equals(args.SourceBinding.Format, "HeightField", StringComparison.Ordinal))
        {
            if (!workbench.TryGetPublishedRoiCropOutput(
                    args.SourceBinding.OwnerEntityId ?? string.Empty,
                    out var croppedHeightField)
                || croppedHeightField is null
                || string.IsNullOrWhiteSpace(workbench.CurrentRoiCropPreviewPath)
                || !viewer.ShowC3DWorkbenchResult(
                    workbench.CurrentRoiCropPreviewPath,
                    $"Published ROI / Crop | {croppedHeightField.Width} x {croppedHeightField.Height}"))
            {
                workbench.RejectTeachingSelectionCapture(
                    "The ROI owner HeightField is not currently Published or displayable.");
                return;
            }

            SyncAppliedSelections();
        }
        else if (string.Equals(args.SourceBinding.Format, "TransformedHeightField", StringComparison.Ordinal))
        {
            if (!workbench.TryGetPublishedRegridHeightFieldOutput(
                    args.SourceBinding.OwnerEntityId ?? string.Empty,
                    out var transformedHeightField)
                || transformedHeightField is null)
            {
                workbench.RejectTeachingSelectionCapture(
                    "The ROI owner TransformedHeightField is not currently Published.");
                return;
            }

            viewer.ShowWorkbenchRegridHeightField(
                transformedHeightField,
                isPublished: true,
                standaloneReferenceDisplay: true);
            SyncAppliedSelections();
        }
        else
        {
            viewer.ClearWorkbenchRegridHeightField();
            viewer.ViewModel.C3DSampleVisible = true;
        }

        var request = new TeachingCaptureRequest(
            args.SelectionId,
            args.SelectionName,
            args.Kind,
            args.RequiredPointCount,
            args.RootSourceId,
            args.FrameId,
            args.SourceBinding);
        if (!viewer.BeginC3DTeachingCapture(request, args.ExistingSelection, out var message))
        {
            workbench.RejectTeachingSelectionCapture(message);
            return;
        }

        if (args.Kind is ToolRecipeSelectionKinds.GridRectangle
            or ToolRecipeSelectionKinds.GridCircle
            or ToolRecipeSelectionKinds.GridPolygon)
        {
            viewer.UseTopView();
        }

        ApplyViewerTeachingCaptureState(viewer.TeachingCaptureSnapshot);
    }

    private void OnUndoTeachingCaptureRequested(object? sender, EventArgs args)
    {
        viewer.UndoC3DTeachingCapture();
        ApplyViewerTeachingCaptureState(viewer.TeachingCaptureSnapshot);
    }

    private void OnCancelTeachingCaptureRequested(object? sender, EventArgs args)
    {
        viewer.CancelC3DTeachingCapture();
        ApplyViewerTeachingCaptureState(viewer.TeachingCaptureSnapshot);
    }

    private void OnApplyTeachingCaptureRequested(object? sender, EventArgs args)
    {
        if (!viewer.TryGetC3DTeachingCandidate(out var selection, out var message))
        {
            UpdateWorkbenchCaptureState(viewer.TeachingCaptureSnapshot, message);
            return;
        }

        if (!workbench.TryApplyCapturedTeachingSelection(selection, out message))
        {
            UpdateWorkbenchCaptureState(viewer.TeachingCaptureSnapshot, message);
            return;
        }

        viewer.ConfirmC3DTeachingCaptureApplied();
        SyncAppliedSelections();
    }

    private void OnAppliedTeachingSelectionsChanged(object? sender, EventArgs args) =>
        SyncAppliedSelections();

    private void OnViewerTeachingCaptureStateChanged(
        object? sender,
        TeachingCaptureStateChangedEventArgs args) =>
        ApplyViewerTeachingCaptureState(args.State);

    private void ApplyViewerTeachingCaptureState(TeachingCaptureState state)
    {
        if (state.IsActive)
        {
            collapseBottomPane();
        }

        UpdateWorkbenchCaptureState(state, state.Message);
        if (state.IsActive
            && string.Equals(state.Kind, ToolRecipeSelectionKinds.GridRectangle, StringComparison.Ordinal)
            && state.Points is [var first, var second])
        {
            workbench.UpdateTeachingGridRectangleDraft(
                new ToolRecipeGridRectangle(
                    Math.Min(first.Locator.Row, second.Locator.Row),
                    Math.Min(first.Locator.Column, second.Locator.Column),
                    Math.Abs(second.Locator.Row - first.Locator.Row) + 1,
                    Math.Abs(second.Locator.Column - first.Locator.Column) + 1));
        }
        else if (state.IsActive
                 && string.Equals(state.Kind, ToolRecipeSelectionKinds.GridCircle, StringComparison.Ordinal)
                 && state.GridCircle is { } circle)
        {
            workbench.UpdateTeachingGridCircleDraft(circle);
        }
        else if (state.IsActive
                 && string.Equals(state.Kind, ToolRecipeSelectionKinds.GridPolygon, StringComparison.Ordinal)
                 && state.GridPolygon is { } polygon)
        {
            workbench.UpdateTeachingGridPolygonDraft(polygon);
        }
    }

    private void UpdateWorkbenchCaptureState(TeachingCaptureState state, string message) =>
        workbench.UpdateTeachingSelectionCaptureState(
            state.IsActive,
            state.CapturedPointCount,
            state.RequiredPointCount,
            state.CanApply,
            message);

    private void SyncSelectedSelection() =>
        viewer.SetSelectedTeachingSelection(
            workbench.OrientedBoxEditor.DraftSelectionId
            ?? workbench.SelectedStepTeachingSelection?.Id);

    private void SyncOrientedBoxDraft() =>
        viewer.SetTeachingOrientedBox3DDraft(
            workbench.OrientedBoxEditor.CurrentDraftSelection);

    private void OnWorkbenchPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(ToolWorkbenchViewModel.SelectedPipelineStep)
            or nameof(ToolWorkbenchViewModel.SelectedStepTeachingSelection))
        {
            SyncSelectedSelection();
            SyncCompletenessCellOverlays();
        }

        if (args.PropertyName is nameof(ToolWorkbenchViewModel.HasCurrentMeasurementPreview)
            or nameof(ToolWorkbenchViewModel.IsSelectedStepCompletenessGrid)
            or nameof(ToolWorkbenchViewModel.SelectedCompletenessCellId))
        {
            SyncCompletenessCellOverlays();
        }

        if (args.PropertyName is nameof(ToolWorkbenchViewModel.CurrentConnectedRegionOutput)
            or nameof(ToolWorkbenchViewModel.HasConnectedRegionOutput)
            or nameof(ToolWorkbenchViewModel.SelectedConnectedRegionId))
        {
            SyncConnectedRegionOverlays();
        }
    }

    private void SyncCompletenessCellOverlays()
    {
        var overlays = workbench.IsSelectedStepCompletenessGrid
            && workbench.HasCurrentMeasurementPreview
            ? workbench.CurrentMeasurementOutput?.CompletenessGrid?.CellOverlays
              ?? []
            : [];
        viewer.SetCompletenessCellOverlays(overlays);
        viewer.SetSelectedCompletenessCellId(
            overlays.Count > 0 ? workbench.SelectedCompletenessCellId : null);
        workbench.HeightImageViewer.SetCompletenessCellOverlays(overlays);
        workbench.HeightImageViewer.SetSelectedCompletenessCellId(
            overlays.Count > 0 ? workbench.SelectedCompletenessCellId : null);
    }

    private void SyncConnectedRegionOverlays()
    {
        var output = workbench.HasConnectedRegionOutput
            ? workbench.CurrentConnectedRegionOutput
            : null;
        var selectedRegionId = output is null
            ? null
            : workbench.SelectedConnectedRegionId;
        viewer.SetConnectedRegionOverlay(output, selectedRegionId);
        workbench.HeightImageViewer.SetConnectedRegionOverlay(output, selectedRegionId);
    }

    private void OnGridRectangleDraftChanged(
        object? sender,
        ToolWorkbenchGridRectangleDraftChangedEventArgs args)
    {
        if (viewer.TrySetC3DTeachingGridRectangleCandidate(args.Rectangle, out var message))
        {
            return;
        }

        UpdateWorkbenchCaptureState(viewer.TeachingCaptureSnapshot, message);
    }

    private void OnGridCircleDraftChanged(
        object? sender,
        ToolWorkbenchGridCircleDraftChangedEventArgs args)
    {
        if (viewer.TrySetC3DTeachingGridCircleCandidate(args.Circle, out var message))
        {
            return;
        }

        UpdateWorkbenchCaptureState(viewer.TeachingCaptureSnapshot, message);
    }

    private void OnGridPolygonDraftChanged(
        object? sender,
        ToolWorkbenchGridPolygonDraftChangedEventArgs args)
    {
        if (viewer.TrySetC3DTeachingGridPolygonCandidate(args.Polygon, out var message))
        {
            return;
        }

        UpdateWorkbenchCaptureState(viewer.TeachingCaptureSnapshot, message);
    }

    private void OnOrientedBoxDraftChanged(
        object? sender,
        OrientedBox3DDraftChangedEventArgs args)
    {
        viewer.SetTeachingOrientedBox3DDraft(args.Selection);
        SyncSelectedSelection();
    }

    private void OnViewerOrientedBoxDraftChanged(
        object? sender,
        TeachingOrientedBox3DDraftChangedEventArgs args)
    {
        if (!workbench.OrientedBoxEditor.TryUpdateDraftFromViewer(args.Selection))
        {
            return;
        }

        if (args.Source.Contains("completed", StringComparison.OrdinalIgnoreCase))
        {
            OVLog.Write(
                LogCategory.UI,
                LogLevel.Info,
                $"OrientedBox3D draft changed | selection={args.Selection.Id} | source={args.Source} | recipeChanged=false | inspectionRun=false");
        }
    }

    private void OnThicknessRepeatGridPreviewChanged(
        object? sender,
        ThicknessRepeatGridPreviewChangedEventArgs args) =>
        viewer.SetRepeatPreviewTeachingSelections(args.Selections);

    private void OnFitWorkspaceRegionRequested(object? sender, EventArgs args) =>
        viewer.FitRoi();

    private void OnViewerTeachingSelectionSelected(
        object? sender,
        TeachingSelectionSelectedEventArgs args)
    {
        var orientedBox = workbench.OrientedBoxEditor.Selections.FirstOrDefault(
            selection => string.Equals(
                selection.Id,
                args.SelectionId,
                StringComparison.OrdinalIgnoreCase));
        if (orientedBox is not null)
        {
            workbench.OrientedBoxEditor.SelectedSelection = orientedBox;
        }

        workbench.SelectPipelineStepForSelection(args.SelectionId);
        SyncSelectedSelection();
    }

    private static void OnViewerTeachingRoiDisplayHeightChanged(
        object? sender,
        TeachingRoiDisplayHeightChangedEventArgs args)
    {
        OVLog.Write(
            LogCategory.UI,
            LogLevel.Info,
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"Surface ROI overlay Y position changed | selection={args.SelectionId} | source={args.Source} | automaticRawHeight={args.AutomaticRawHeight:F3} | offset={args.Offset:F3} | effectiveRawHeight={args.EffectiveRawHeight:F3} | viewOnly=true | roiSizeChanged=false | recipeChanged=false | inspectionRun=false"));
    }
}

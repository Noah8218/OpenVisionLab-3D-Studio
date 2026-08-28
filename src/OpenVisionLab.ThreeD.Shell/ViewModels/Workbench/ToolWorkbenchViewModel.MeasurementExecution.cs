using System.Windows.Input;
using OpenVisionLab.ThreeD.Core;
using OpenVisionLab.ThreeD.Tools;
using OpenVisionLab.ThreeD.Viewer;

namespace OpenVisionLab.ThreeD.Shell.ViewModels.Workbench;

/// <summary>
/// Measurement-tool bindings and dual-ROI teaching workflow.
/// </summary>
public sealed partial class ToolWorkbenchViewModel
{
    private RelayCommand capturePlaneFlatnessReferenceRoiCommand = null!;
    private RelayCommand capturePlaneFlatnessMeasurementRoiCommand = null!;
    private RelayCommand reusePlaneFlatnessReferenceRoiCommand = null!;
    private RelayCommand reusePlaneFlatnessMeasurementRoiCommand = null!;
    private RelayCommand removePlaneFlatnessReferenceRoiCommand = null!;
    private RelayCommand removePlaneFlatnessMeasurementRoiCommand = null!;
    private bool isPlaneFlatnessMeasurementRole;
    private string? planeFlatnessTeachingStepId;

    public bool IsSelectedStepThickness => string.Equals(SelectedPipelineStep?.ToolId, "thickness", StringComparison.Ordinal);
    public bool IsSelectedStepWarpage => string.Equals(SelectedPipelineStep?.ToolId, "warpage", StringComparison.Ordinal);
    public bool IsSelectedStepPlaneFlatness => string.Equals(SelectedPipelineStep?.ToolId, "plane-flatness", StringComparison.Ordinal);
    public bool IsSelectedStepPointPairDimensions => string.Equals(SelectedPipelineStep?.ToolId, "point-pair-dimensions", StringComparison.Ordinal);
    public bool IsSelectedStepGapFlush => string.Equals(SelectedPipelineStep?.ToolId, "gap-flush", StringComparison.Ordinal);
    public bool IsSelectedStepVolume => string.Equals(SelectedPipelineStep?.ToolId, "volume", StringComparison.Ordinal);
    public bool IsSelectedStepCrossSectionDimensions => string.Equals(SelectedPipelineStep?.ToolId, "cross-section-dimensions", StringComparison.Ordinal);
    public bool IsSelectedStepCompletenessGrid => string.Equals(SelectedPipelineStep?.ToolId, "completeness-grid", StringComparison.Ordinal);
    public bool IsSelectedStepCompletenessGridUsingEditableRegion =>
        IsSelectedStepCompletenessGrid
        && SelectedPipelineStep?.InputEntityIds.ElementAtOrDefault(2) is { } inspectionInputId
        && PipelineSteps.Any(step =>
            string.Equals(step.OutputEntityId, inspectionInputId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(step.ToolId, "editable-region", StringComparison.OrdinalIgnoreCase));
    public bool IsSelectedStepDualRoiMeasurement => IsSelectedStepThickness || IsSelectedStepPlaneFlatness || IsSelectedStepGapFlush || IsSelectedStepVolume || IsSelectedStepCompletenessGrid;
    public bool IsSelectedStepDualRoiTeaching =>
        IsSelectedStepDualRoiMeasurement && !IsSelectedStepCompletenessGridUsingEditableRegion;
    public bool IsSelectedStepMeasurement => IsSelectedStepThickness || IsSelectedStepWarpage || IsSelectedStepDualRoiMeasurement || IsSelectedStepPointPairDimensions || IsSelectedStepCrossSectionDimensions;
    public bool IsMeasurementPreviewRunning => heightMeasurementExecutionOwner.IsPreviewRunning;
    public bool HasCurrentMeasurementPreview => heightMeasurementExecutionOwner.HasCurrentPreview;
    public bool IsMeasurementPreviewPublished => heightMeasurementExecutionOwner.IsPreviewPublished;
    internal bool IsMeasurementPreviewStale => heightMeasurementExecutionOwner.IsPreviewStale;
    public string MeasurementExecutionSummary => heightMeasurementExecutionOwner.ExecutionSummary;
    public string MeasurementEvidenceSummary => heightMeasurementExecutionOwner.EvidenceSummary;
    internal ToolRecipeHeightMeasurementOutput? CurrentMeasurementOutput =>
        heightMeasurementExecutionOwner.CurrentOutput;
    public ICommand CapturePlaneFlatnessReferenceRoiCommand => capturePlaneFlatnessReferenceRoiCommand;
    public ICommand CapturePlaneFlatnessMeasurementRoiCommand => capturePlaneFlatnessMeasurementRoiCommand;
    public ICommand ReusePlaneFlatnessReferenceRoiCommand => reusePlaneFlatnessReferenceRoiCommand;
    public ICommand ReusePlaneFlatnessMeasurementRoiCommand => reusePlaneFlatnessMeasurementRoiCommand;
    public ICommand RemovePlaneFlatnessReferenceRoiCommand => removePlaneFlatnessReferenceRoiCommand;
    public ICommand RemovePlaneFlatnessMeasurementRoiCommand => removePlaneFlatnessMeasurementRoiCommand;
    public ToolRecipeSelection? PlaneFlatnessReferenceSelection => GetPlaneFlatnessRoleSelection(1);
    public ToolRecipeSelection? PlaneFlatnessMeasurementSelection => GetPlaneFlatnessRoleSelection(2);
    public bool HasDualRoiFirstSelection => PlaneFlatnessReferenceSelection is not null;
    public bool HasDualRoiSecondSelection => PlaneFlatnessMeasurementSelection is not null;
    public bool HasCompleteDualRoiTeaching =>
        HasDualRoiFirstSelection && HasDualRoiSecondSelection;
    public bool IsPlaneFlatnessReferenceRoleActive => IsSelectedStepDualRoiMeasurement && !isPlaneFlatnessMeasurementRole;
    public bool IsPlaneFlatnessMeasurementRoleActive => IsSelectedStepDualRoiMeasurement && isPlaneFlatnessMeasurementRole;
    public bool CanTeachPlaneFlatnessMeasurementRoi => PlaneFlatnessReferenceSelection is not null;
    public string PlaneFlatnessReferenceState => PlaneFlatnessReferenceSelection is null ? Localization.RoiWaiting : Localization.RoiComplete;
    public string PlaneFlatnessMeasurementState => PlaneFlatnessMeasurementSelection is null ? Localization.RoiWaiting : Localization.RoiComplete;
    public string PlaneFlatnessReferenceSummary => PlaneFlatnessReferenceSelection is { } selection
        ? FormatTeachingSelection(selection)
        : Localization.NoRoiTaught;
    public string PlaneFlatnessMeasurementSummary => PlaneFlatnessMeasurementSelection is { } selection
        ? FormatTeachingSelection(selection)
        : CanTeachPlaneFlatnessMeasurementRoi ? Localization.NoRoiTaught : DualRoiFirstRequired;
    public string PlaneFlatnessReferenceActionText => PlaneFlatnessReferenceSelection is null ? Localization.DrawRoi : Localization.EditRoi;
    public string PlaneFlatnessMeasurementActionText => PlaneFlatnessMeasurementSelection is null ? Localization.DrawRoi : Localization.EditRoi;
    public string DualRoiTeachingTitle => IsSelectedStepThickness
        ? Localization.ThicknessRoiTeaching
        : IsSelectedStepGapFlush
        ? Localization.GapFlushRoiTeaching
        : IsSelectedStepVolume
        ? Localization.VolumeRoiTeaching
        : IsSelectedStepCompletenessGrid
        ? Localization.CompletenessRoiTeaching
        : Localization.PlaneFlatnessRoiTeaching;
    public string DualRoiTeachingDetail => IsSelectedStepThickness
        ? Localization.ThicknessRoiTeachingDetail
        : IsSelectedStepGapFlush
        ? Localization.GapFlushRoiTeachingDetail
        : IsSelectedStepVolume
        ? Localization.VolumeRoiTeachingDetail
        : IsSelectedStepCompletenessGrid
        ? Localization.CompletenessRoiTeachingDetail
        : Localization.PlaneFlatnessRoiTeachingDetail;
    public string DualRoiFirstLabel => IsSelectedStepGapFlush ? Localization.FirstRoi : Localization.ReferenceRoi;
    public string DualRoiSecondLabel => IsSelectedStepGapFlush
        ? Localization.SecondRoi
        : IsSelectedStepCompletenessGrid
        ? Localization.InspectionGridRoi
        : Localization.MeasurementRoi;
    public string DualRoiFirstRequired => IsSelectedStepGapFlush ? Localization.FirstRoiRequiredFirst : Localization.ReferenceRoiRequiredFirst;

    private void InitializePlaneFlatnessTeaching()
    {
        capturePlaneFlatnessReferenceRoiCommand = new RelayCommand(
            _ => BeginPlaneFlatnessRoleCapture(measurementRole: false),
            _ => CanCapturePlaneFlatnessRole(measurementRole: false));
        capturePlaneFlatnessMeasurementRoiCommand = new RelayCommand(
            _ => BeginPlaneFlatnessRoleCapture(measurementRole: true),
            _ => CanCapturePlaneFlatnessRole(measurementRole: true));
        reusePlaneFlatnessReferenceRoiCommand = new RelayCommand(
            _ => ReusePlaneFlatnessRoleSelection(measurementRole: false),
            _ => CanReusePlaneFlatnessRoleSelection(measurementRole: false));
        reusePlaneFlatnessMeasurementRoiCommand = new RelayCommand(
            _ => ReusePlaneFlatnessRoleSelection(measurementRole: true),
            _ => CanReusePlaneFlatnessRoleSelection(measurementRole: true));
        removePlaneFlatnessReferenceRoiCommand = new RelayCommand(
            _ => RemovePlaneFlatnessRoleSelection(measurementRole: false),
            _ => CanRemovePlaneFlatnessRoleSelection(measurementRole: false));
        removePlaneFlatnessMeasurementRoiCommand = new RelayCommand(
            _ => RemovePlaneFlatnessRoleSelection(measurementRole: true),
            _ => CanRemovePlaneFlatnessRoleSelection(measurementRole: true));
        Localization.PropertyChanged += (_, _) => NotifyPlaneFlatnessTeachingState();
    }

    private ToolRecipeSelection? GetPlaneFlatnessRoleSelection(int inputIndex)
    {
        if (!IsSelectedStepDualRoiMeasurement || SelectedPipelineStep is not { } step)
        {
            return null;
        }

        if (step.DualRoiRouting is { } routing)
        {
            var routedSelectionId = inputIndex == 1
                ? routing.FirstRegionSelectionId
                : routing.SecondRegionSelectionId;
            return string.IsNullOrWhiteSpace(routedSelectionId)
                ? null
                : Selections.FirstOrDefault(selection =>
                    string.Equals(selection.Id, routedSelectionId, StringComparison.OrdinalIgnoreCase)
                    && selection.GridRectangle is not null);
        }

        var legacyThicknessRoute = IsSelectedStepThickness
            && step.InputEntityIds.Count == 2
            && !ToolRecipeDocument.SupportsArtifactOwnedSelections(RecipeSchemaVersion);
        var resolvedInputIndex = legacyThicknessRoute
            ? inputIndex == 1 ? -1 : 1
            : inputIndex;
        if (resolvedInputIndex < 0
            || step.InputEntityIds.ElementAtOrDefault(resolvedInputIndex) is not { } selectionId)
        {
            return null;
        }

        return Selections.FirstOrDefault(selection =>
            string.Equals(selection.Id, selectionId, StringComparison.OrdinalIgnoreCase)
            && selection.GridRectangle is not null);
    }

    private void BeginPlaneFlatnessRoleCapture(bool measurementRole)
    {
        SetPlaneFlatnessTeachingRole(measurementRole);
        BeginTeachingSelectionCapture();
    }

    private void ReusePlaneFlatnessRoleSelection(bool measurementRole)
    {
        SetPlaneFlatnessTeachingRole(measurementRole);
        UseExistingTeachingSelection();
    }

    private void RemovePlaneFlatnessRoleSelection(bool measurementRole)
    {
        SetPlaneFlatnessTeachingRole(measurementRole);
        RemoveSelectedTeachingSelection();
    }

    private bool CanCapturePlaneFlatnessRole(bool measurementRole) =>
        IsSelectedStepDualRoiMeasurement
        && !IsTeachingSelectionCaptureActive
        && (!measurementRole || CanTeachPlaneFlatnessMeasurementRoi)
        && !string.IsNullOrWhiteSpace(Source.Path)
        && SelectedPipelineStep is { } step
        && TryGetSelectionCaptureContext(step, out _, out _);

    private bool CanReusePlaneFlatnessRoleSelection(bool measurementRole)
    {
        if (!IsSelectedStepDualRoiMeasurement
            || IsTeachingSelectionCaptureActive
            || SelectedCompatibleSelection is not { } candidate
            || (measurementRole && !CanTeachPlaneFlatnessMeasurementRoi))
        {
            return false;
        }

        var otherRole = measurementRole ? PlaneFlatnessReferenceSelection : PlaneFlatnessMeasurementSelection;
        return !string.Equals(candidate.Id, otherRole?.Id, StringComparison.OrdinalIgnoreCase);
    }

    private bool CanRemovePlaneFlatnessRoleSelection(bool measurementRole) =>
        IsSelectedStepDualRoiMeasurement
        && !IsTeachingSelectionCaptureActive
        && (measurementRole ? PlaneFlatnessMeasurementSelection : PlaneFlatnessReferenceSelection) is not null;

    private void SetPlaneFlatnessTeachingRole(bool measurementRole)
    {
        isPlaneFlatnessMeasurementRole = measurementRole;
        NotifyPlaneFlatnessTeachingState();
        SynchronizeInspectionWorkspace();
    }

    private void AdvancePlaneFlatnessTeachingRole()
    {
        if (IsSelectedStepDualRoiMeasurement && !isPlaneFlatnessMeasurementRole)
        {
            SetPlaneFlatnessTeachingRole(measurementRole: true);
        }
    }

    private void RefreshPlaneFlatnessTeachingState()
    {
        var selectedStepId = IsSelectedStepDualRoiMeasurement ? SelectedPipelineStep?.Id : null;
        if (!string.Equals(planeFlatnessTeachingStepId, selectedStepId, StringComparison.OrdinalIgnoreCase))
        {
            planeFlatnessTeachingStepId = selectedStepId;
            isPlaneFlatnessMeasurementRole = selectedStepId is not null
                && PlaneFlatnessReferenceSelection is not null
                && PlaneFlatnessMeasurementSelection is null;
        }

        NotifyPlaneFlatnessTeachingState();
    }

    private ToolWorkbenchTeachingSelectionRequirement CreatePlaneFlatnessSelectionRequirement() =>
        isPlaneFlatnessMeasurementRole
            ? new(DualRoiSecondLabel, ToolRecipeSelectionKinds.GridRectangle, 2, true, DualRoiTeachingDetail)
            : new(DualRoiFirstLabel, ToolRecipeSelectionKinds.GridRectangle, 2, true, DualRoiTeachingDetail);

    private string CreatePlaneFlatnessSelectionName(ToolWorkbenchPipelineStepItem step) =>
        $"{step.ToolName} {(isPlaneFlatnessMeasurementRole ? DualRoiSecondLabel : DualRoiFirstLabel)}";

    private bool CanUseActivePlaneFlatnessRole() =>
        !IsSelectedStepDualRoiMeasurement || !isPlaneFlatnessMeasurementRole || CanTeachPlaneFlatnessMeasurementRoi;

    private void RoutePlaneFlatnessRoleSelection(ToolWorkbenchPipelineStepItem step, string selectionId)
    {
        var primaryInput = step.InputEntityIds.FirstOrDefault(input =>
            !Selections.Any(selection => string.Equals(selection.Id, input, StringComparison.OrdinalIgnoreCase)))
            ?? step.InputEntityIds.FirstOrDefault();
        var legacyThicknessRoute = step.DualRoiRouting is null
            && IsSelectedStepThickness
            && step.InputEntityIds.Count == 2
            && !ToolRecipeDocument.SupportsArtifactOwnedSelections(RecipeSchemaVersion);
        var referenceId = step.DualRoiRouting?.FirstRegionSelectionId
            ?? (legacyThicknessRoute ? null : PlaneFlatnessReferenceSelection?.Id);
        var measurementId = legacyThicknessRoute
            ? step.InputEntityIds[1]
            : step.DualRoiRouting?.SecondRegionSelectionId
              ?? PlaneFlatnessMeasurementSelection?.Id;
        if (isPlaneFlatnessMeasurementRole) measurementId = selectionId;
        else referenceId = selectionId;

        step.DualRoiRouting = new ToolRecipeDualRoiRouting(referenceId, measurementId);
        step.InputEntityIdsText = string.Join("; ", new[] { primaryInput, referenceId, measurementId }
            .Where(input => !string.IsNullOrWhiteSpace(input)));
    }

    private void NotifyPlaneFlatnessTeachingState()
    {
        OnPropertyChanged(nameof(PlaneFlatnessReferenceSelection));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementSelection));
        OnPropertyChanged(nameof(HasDualRoiFirstSelection));
        OnPropertyChanged(nameof(HasDualRoiSecondSelection));
        OnPropertyChanged(nameof(HasCompleteDualRoiTeaching));
        OnPropertyChanged(nameof(IsPlaneFlatnessReferenceRoleActive));
        OnPropertyChanged(nameof(IsPlaneFlatnessMeasurementRoleActive));
        OnPropertyChanged(nameof(CanTeachPlaneFlatnessMeasurementRoi));
        OnPropertyChanged(nameof(PlaneFlatnessReferenceState));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementState));
        OnPropertyChanged(nameof(PlaneFlatnessReferenceSummary));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementSummary));
        OnPropertyChanged(nameof(PlaneFlatnessReferenceActionText));
        OnPropertyChanged(nameof(PlaneFlatnessMeasurementActionText));
        OnPropertyChanged(nameof(DualRoiTeachingTitle));
        OnPropertyChanged(nameof(DualRoiTeachingDetail));
        OnPropertyChanged(nameof(DualRoiFirstLabel));
        OnPropertyChanged(nameof(DualRoiSecondLabel));
        OnPropertyChanged(nameof(DualRoiFirstRequired));
        capturePlaneFlatnessReferenceRoiCommand?.RaiseCanExecuteChanged();
        capturePlaneFlatnessMeasurementRoiCommand?.RaiseCanExecuteChanged();
        reusePlaneFlatnessReferenceRoiCommand?.RaiseCanExecuteChanged();
        reusePlaneFlatnessMeasurementRoiCommand?.RaiseCanExecuteChanged();
        removePlaneFlatnessReferenceRoiCommand?.RaiseCanExecuteChanged();
        removePlaneFlatnessMeasurementRoiCommand?.RaiseCanExecuteChanged();
    }

    public Task<bool> PreviewSelectedMeasurementAsync() =>
        heightMeasurementExecutionOwner.PreviewAsync();

    private bool CanPreviewSelectedMeasurement() =>
        heightMeasurementExecutionOwner.CanPreview();
    private void PublishSelectedMeasurement() =>
        heightMeasurementExecutionOwner.Publish();
    private void CancelMeasurementPreview() =>
        heightMeasurementExecutionOwner.Cancel();
    private void ClearMeasurementPreview(string summary) =>
        heightMeasurementExecutionOwner.Clear(summary);
    private void MarkMeasurementPreviewStaleIfNeeded(object? sender = null) =>
        heightMeasurementExecutionOwner.MarkStaleIfNeeded(sender);

    private void RefreshMeasurementExecutionState()
    {
        OnPropertyChanged(nameof(IsSelectedStepThickness));
        OnPropertyChanged(nameof(IsSelectedStepWarpage));
        OnPropertyChanged(nameof(IsSelectedStepPlaneFlatness));
        OnPropertyChanged(nameof(IsSelectedStepPointPairDimensions));
        OnPropertyChanged(nameof(IsSelectedStepGapFlush));
        OnPropertyChanged(nameof(IsSelectedStepVolume));
        OnPropertyChanged(nameof(IsSelectedStepCrossSectionDimensions));
        OnPropertyChanged(nameof(IsSelectedStepCompletenessGrid));
        OnPropertyChanged(nameof(IsSelectedStepCompletenessGridUsingEditableRegion));
        OnPropertyChanged(nameof(IsSelectedStepDualRoiMeasurement));
        OnPropertyChanged(nameof(IsSelectedStepDualRoiTeaching));
        OnPropertyChanged(nameof(IsSelectedStepMeasurement));
        RefreshPlaneFlatnessTeachingState();
        heightMeasurementExecutionOwner.RefreshState();
    }

    private void RefreshMeasurementCommands()
    {
        previewSelectedStepCommand?.RaiseCanExecuteChanged();
        publishSelectedStepCommand?.RaiseCanExecuteChanged();
        cancelFilterPreviewCommand?.RaiseCanExecuteChanged();
    }

}
